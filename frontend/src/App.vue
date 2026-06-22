<script setup>
import { ref, reactive, computed, onMounted, provide } from 'vue'
import { ElMessage } from 'element-plus'
import ToolBar from './components/ToolBar.vue'
import DataTable from './components/DataTable.vue'
import RowModal from './components/RowModal.vue'
import SettingsModal from './components/SettingsModal.vue'
import Lightbox from './components/Lightbox.vue'
import DeleteConfirm from './components/DeleteConfirm.vue'
import {
  COL_ORDER, COL_VIS_KEY, COL_FILTER_DEFAULTS, HIDDEN_COLS, colLabel
} from './constants.js'
import {
  connectDB, getTables, getPK, queryTable, saveCell, deleteRow, insertRow, updateRow, getDistinct
} from './api.js'

// ── State ─────────────────────────────────────────────────────────────────
const columns = ref([])       // array of column name strings
const rows = ref([])
const currentPage = ref(1)
const rowsPerPage = ref(20)
const totalRows = ref(0)
const totalPages = ref(0)
const sortCol = ref('TABLEID')
const sortDir = ref('desc')
const search = ref('')
const colFilters = reactive({})
const selectedRowIndex = ref(-1)
const selectedRow = computed(() => rows.value[selectedRowIndex.value] ?? null)
const dbConnected = ref(false)
const currentTable = ref('PTS_TRA')
const tablePKs = ref([])
const loading = ref(false)

// Column visibility — persisted in localStorage
const colVisibility = reactive({})
function loadColVisibility() {
  try {
    const saved = JSON.parse(localStorage.getItem(COL_VIS_KEY) || '{}')
    Object.assign(colVisibility, saved)
  } catch {}
}
function saveColVisibility() {
  localStorage.setItem(COL_VIS_KEY, JSON.stringify(colVisibility))
}
function isColVisible(col) {
  if (HIDDEN_COLS.has(col)) return false
  return colVisibility[col] !== false
}

// Autocomplete cache
const autocompleteCache = reactive({})
async function fetchDistinct(col) {
  if (autocompleteCache[col]) return autocompleteCache[col]
  const res = await getDistinct(currentTable.value, col)
  if (res.ok) autocompleteCache[col] = res.values || []
  return autocompleteCache[col] || []
}

// ── Modals ────────────────────────────────────────────────────────────────
const rowModalVisible = ref(false)
const rowModalMode = ref('new')   // 'new' | 'edit' | 'copy'
const rowModalData = ref({})
const rowModalRef = ref(null)

const settingsVisible = ref(false)
const deleteConfirmVisible = ref(false)

const lightboxVisible = ref(false)
const lightboxImages = ref([])  // [{name, url}]
const lightboxIdx = ref(0)

// ── Data loading ──────────────────────────────────────────────────────────
async function fetchPage() {
  loading.value = true
  try {
    const params = {
      table: currentTable.value,
      page: currentPage.value,
      size: rowsPerPage.value,
    }
    if (sortCol.value) { params.sort = sortCol.value; params.dir = sortDir.value }
    if (search.value) params.search = search.value
    const activeFilters = {}
    for (const [k, v] of Object.entries(colFilters)) {
      if (v && v.length > 0) activeFilters[k] = v
    }
    if (Object.keys(activeFilters).length) params.colFilters = JSON.stringify(activeFilters)

    const res = await queryTable(params)
    if (res.ok) {
      columns.value = res.columns || []
      rows.value = (res.rows || []).map(r => {
        const obj = {}
        columns.value.forEach((c, i) => { obj[c] = r[i] })
        return obj
      })
      totalRows.value = res.total || 0
      totalPages.value = res.pages || 0

      // Init visibility for new columns
      for (const col of columns.value) {
        if (colVisibility[col] === undefined && !HIDDEN_COLS.has(col)) {
          colVisibility[col] = true
        }
      }
    } else {
      ElMessage.error(res.error || 'Query failed')
    }
  } finally {
    loading.value = false
  }
}

async function loadTable(tbl) {
  if (tbl) currentTable.value = tbl
  currentPage.value = 1
  selectedRowIndex.value = -1
  // Reset autocomplete cache on table change
  for (const k of Object.keys(autocompleteCache)) delete autocompleteCache[k]
  // Load PKs
  const pkRes = await getPK(currentTable.value)
  if (pkRes.ok) tablePKs.value = pkRes.pks || []
  // Init colFilters defaults
  for (const [col, vals] of Object.entries(COL_FILTER_DEFAULTS)) {
    if (colFilters[col] === undefined) colFilters[col] = [...vals]
  }
  await fetchPage()
}

function refreshData() {
  fetchPage()
}

function onSortChange({ column, prop, order }) {
  sortCol.value = prop || ''
  sortDir.value = order === 'descending' ? 'desc' : 'asc'
  currentPage.value = 1
  fetchPage()
}

function onPageChange(page) {
  currentPage.value = page
  fetchPage()
}

function onSizeChange(size) {
  rowsPerPage.value = size
  currentPage.value = 1
  fetchPage()
}

function onSearchChange(val) {
  search.value = val
  currentPage.value = 1
  fetchPage()
}

function onFilterChange(filters) {
  for (const [k, v] of Object.entries(filters)) {
    colFilters[k] = v
  }
  currentPage.value = 1
  fetchPage()
}

// ── CRUD ──────────────────────────────────────────────────────────────────
function openNewModal() {
  rowModalMode.value = 'new'
  rowModalData.value = {}
  rowModalVisible.value = true
}

function openEditModal() {
  if (!selectedRow.value) { ElMessage.warning('请先选择一行'); return }
  rowModalMode.value = 'edit'
  rowModalData.value = { ...selectedRow.value }
  rowModalVisible.value = true
}

function openCopyModal() {
  if (!selectedRow.value) { ElMessage.warning('请先选择一行'); return }
  rowModalMode.value = 'copy'
  rowModalData.value = { ...selectedRow.value }
  rowModalVisible.value = true
}

function openDeleteConfirm() {
  if (!selectedRow.value) { ElMessage.warning('请先选择一行'); return }
  deleteConfirmVisible.value = true
}

async function onRowSave(formData) {
  try {
    let res
    if (rowModalMode.value === 'edit') {
      res = await updateRow(formData)
    } else {
      res = await insertRow(formData)
    }
    if (res.ok) {
      rowModalRef.value?.onSaveResult(true)
      rowModalVisible.value = false
      ElMessage.success(rowModalMode.value === 'edit' ? '保存成功' : '新增成功')
      fetchPage()
    } else {
      rowModalRef.value?.onSaveResult(false)
      ElMessage.error(res.error || '保存失败')
    }
  } catch (e) {
    rowModalRef.value?.onSaveResult(false)
    ElMessage.error('网络错误，请重试')
  }
}

async function onDeleteConfirm() {
  deleteConfirmVisible.value = false
  const pk = tablePKs.value[0]
  if (!pk || !selectedRow.value) return
  const res = await deleteRow({ table: currentTable.value, pkCol: pk, pkVal: selectedRow.value[pk] })
  if (res.ok) { ElMessage.success('删除成功'); selectedRowIndex.value = -1; fetchPage() }
  else ElMessage.error(res.error || '删除失败')
}

// Inline cell save (double-click edit from DataTable)
async function onCellSave({ col, rowKey, keyValue, value }) {
  const res = await saveCell({ table: currentTable.value, col, rowKey, keyValue, value })
  if (!res.ok) ElMessage.error(res.error || '保存失败')
  else fetchPage()
}

// ── Lightbox ──────────────────────────────────────────────────────────────
function openLightbox(images, idx) {
  lightboxImages.value = images
  lightboxIdx.value = idx
  lightboxVisible.value = true
}

// ── Export CSV ────────────────────────────────────────────────────────────
function exportCSV() {
  const visibleCols = columns.value.filter(c => isColVisible(c))
  const lines = [visibleCols.map(c => colLabel(c)).join(',')]
  for (const row of rows.value) {
    lines.push(visibleCols.map(c => {
      const v = row[c] == null ? '' : String(row[c])
      return v.includes(',') || v.includes('"') || v.includes('\n')
        ? `"${v.replace(/"/g, '""')}"` : v
    }).join(','))
  }
  const blob = new Blob(['﻿' + lines.join('\n')], { type: 'text/csv;charset=utf-8;' })
  const a = document.createElement('a')
  a.href = URL.createObjectURL(blob)
  a.download = `${currentTable.value}.csv`
  a.click()
}

// ── Provide to children ───────────────────────────────────────────────────
provide('state', {
  columns, rows, currentPage, rowsPerPage, totalRows, totalPages,
  sortCol, sortDir, search, colFilters, selectedRowIndex, selectedRow,
  dbConnected, currentTable, tablePKs, loading, colVisibility,
  isColVisible, saveColVisibility, fetchDistinct, autocompleteCache
})

// ── Init ──────────────────────────────────────────────────────────────────
onMounted(async () => {
  loadColVisibility()
  const res = await connectDB({ host: '10.223.35.82', port: '1521', name: 'mes', user: 'protot', pass: 'protot' })
  dbConnected.value = res.ok
  if (res.ok) await loadTable('PTS_TRA')
  else ElMessage.error('数据库连接失败: ' + (res.error || ''))
})
</script>

<template>
  <div class="app-layout">
    <ToolBar
      :db-connected="dbConnected"
      :rows-per-page="rowsPerPage"
      @search-change="onSearchChange"
      @refresh="refreshData"
      @new-row="openNewModal"
      @copy-row="openCopyModal"
      @edit-row="openEditModal"
      @delete-row="openDeleteConfirm"
      @export-csv="exportCSV"
      @open-settings="settingsVisible = true"
      @size-change="onSizeChange"
    />

    <DataTable
      :columns="columns"
      :rows="rows"
      :loading="loading"
      :current-page="currentPage"
      :rows-per-page="rowsPerPage"
      :total-rows="totalRows"
      :total-pages="totalPages"
      :sort-col="sortCol"
      :sort-dir="sortDir"
      :col-filters="colFilters"
      :col-visibility="colVisibility"
      :table-p-ks="tablePKs"
      :current-table="currentTable"
      @sort-change="onSortChange"
      @page-change="onPageChange"
      @size-change="onSizeChange"
      @filter-change="onFilterChange"
      @row-select="i => selectedRowIndex = i"
      @row-dblclick="openEditModal"
      @cell-save="onCellSave"
      @open-lightbox="openLightbox"
    />

    <RowModal
      ref="rowModalRef"
      v-if="rowModalVisible"
      :visible="rowModalVisible"
      :mode="rowModalMode"
      :row-data="rowModalData"
      :columns="columns"
      :table="currentTable"
      :table-p-ks="tablePKs"
      @close="rowModalVisible = false"
      @save="onRowSave"
    />

    <SettingsModal
      v-if="settingsVisible"
      :visible="settingsVisible"
      :columns="columns"
      :col-visibility="colVisibility"
      :current-table="currentTable"
      :db-connected="dbConnected"
      @close="settingsVisible = false"
      @db-connected="v => { dbConnected = v }"
      @load-table="t => { loadTable(t); settingsVisible = false }"
      @col-vis-change="(col, v) => { colVisibility[col] = v; saveColVisibility() }"
      @col-vis-all="v => { for(const c of columns) if(!HIDDEN_COLS.has(c)) colVisibility[c] = v; saveColVisibility() }"
    />

    <DeleteConfirm
      v-if="deleteConfirmVisible"
      :visible="deleteConfirmVisible"
      :row="selectedRow"
      :table-p-ks="tablePKs"
      @close="deleteConfirmVisible = false"
      @confirm="onDeleteConfirm"
    />

    <Lightbox
      v-if="lightboxVisible"
      :visible="lightboxVisible"
      :images="lightboxImages"
      :index="lightboxIdx"
      @close="lightboxVisible = false"
    />
  </div>
</template>

<style>
* { margin: 0; padding: 0; box-sizing: border-box; }
html, body, #app { height: 100%; }
.app-layout {
  display: flex;
  flex-direction: column;
  height: 100vh;
  background: #f0f0f0;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
  font-size: 13px;
}
</style>
