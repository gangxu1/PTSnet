<script setup>
import { ref, computed, watch } from 'vue'
import {
  COL_ORDER, COL_OPTIONS, COL_FILTER_DEFAULTS, HIDDEN_COLS, READONLY_COLS, BLOB_COLS,
  IMAGE_EXTS, colLabel, truncate30, isImage, fileIcon
} from '../constants.js'
import { getFileList, fileDownloadUrl } from '../api.js'
import { Filter } from '@element-plus/icons-vue'

const props = defineProps({
  columns: Array,
  rows: Array,
  loading: Boolean,
  currentPage: Number,
  rowsPerPage: Number,
  totalRows: Number,
  totalPages: Number,
  sortCol: String,
  sortDir: String,
  colFilters: Object,
  colVisibility: Object,
  tablePKs: Array,
  currentTable: String
})

const emit = defineEmits([
  'sort-change', 'page-change', 'size-change', 'filter-change',
  'row-select', 'row-dblclick', 'cell-save', 'open-lightbox'
])

// ── Ordered visible columns ────────────────────────────────────────────────
const orderedCols = computed(() => {
  const colSet = new Set(props.columns || [])
  const ordered = COL_ORDER.filter(c => colSet.has(c) && !HIDDEN_COLS.has(c) && props.colVisibility[c] !== false)
  const extra = (props.columns || []).filter(c => !COL_ORDER.includes(c) && !HIDDEN_COLS.has(c) && !BLOB_COLS.has(c) && props.colVisibility[c] !== false)
  return [...ordered, ...extra]
})

// ── Row selection ──────────────────────────────────────────────────────────
const selectedIdx = ref(-1)
function onRowClick(row, col, e) {
  const idx = props.rows.indexOf(row)
  selectedIdx.value = idx
  emit('row-select', idx)
}
function onRowDblClick(row, col, e) {
  const colName = col?.property
  if (colName && READONLY_COLS.has(colName)) return
  emit('row-dblclick')
}
function rowClass({ row, rowIndex }) {
  return rowIndex === selectedIdx.value ? 'row-selected' : ''
}

// ── Sort ───────────────────────────────────────────────────────────────────
function onSortChange({ column, prop, order }) {
  emit('sort-change', { column, prop, order })
}

// ── ADDA column filter ─────────────────────────────────────────────────────
// Multi-select dropdown with 2s debounce
const filterPopover = ref({})
const filterLocal = ref({})  // local checkbox state before debounce timer fires

function initFilterLocal(col) {
  if (!filterLocal.value[col]) {
    filterLocal.value[col] = [...(props.colFilters[col] || COL_FILTER_DEFAULTS[col] || [])]
  }
}

let filterTimer = null
function onFilterCheck(col) {
  clearTimeout(filterTimer)
  filterTimer = setTimeout(() => {
    const filters = {}
    filters[col] = [...filterLocal.value[col]]
    emit('filter-change', filters)
  }, 2000)
}

function isFilterActive(col) {
  const def = COL_FILTER_DEFAULTS[col] || []
  const cur = props.colFilters[col] || []
  if (cur.length !== def.length) return true
  const defSet = new Set(def)
  return cur.some(v => !defSet.has(v))
}

// ── Inline cell editing ────────────────────────────────────────────────────
const editingCell = ref(null)   // {rowIndex, col}
const editingValue = ref('')

function startCellEdit(rowIndex, col) {
  if (READONLY_COLS.has(col)) return
  if (BLOB_COLS.has(col)) return
  editingCell.value = { rowIndex, col }
  editingValue.value = props.rows[rowIndex]?.[col] ?? ''
}

function stopCellEdit(save) {
  if (!editingCell.value) return
  if (save) {
    const { rowIndex, col } = editingCell.value
    const pk = props.tablePKs?.[0]
    if (pk) {
      emit('cell-save', {
        col,
        rowKey: pk,
        keyValue: props.rows[rowIndex]?.[pk],
        value: editingValue.value
      })
    }
  }
  editingCell.value = null
  editingValue.value = ''
}

function onCellKey(e) {
  if (e.key === 'Enter') { e.preventDefault(); stopCellEdit(true) }
  else if (e.key === 'Escape') stopCellEdit(false)
  else if (e.key === 'Tab') { e.preventDefault(); stopCellEdit(true) }
}

// ── File list in cells ─────────────────────────────────────────────────────
async function openFiles(row) {
  const pk = props.tablePKs?.[0]
  if (!pk) return
  const res = await getFileList(props.currentTable, pk, row[pk])
  if (!res.ok || !res.files?.length) return
  const imageFiles = res.files.filter(f => isImage(f.name))
  if (!imageFiles.length) return
  const images = imageFiles.map(f => ({
    name: f.name,
    url: fileDownloadUrl(props.currentTable, pk, row[pk], f.name)
  }))
  emit('open-lightbox', images, 0)
}

// ── Pagination ─────────────────────────────────────────────────────────────
function onCurrentChange(page) { emit('page-change', page) }
</script>

<template>
  <div class="table-wrap">
    <el-table
      :data="rows"
      v-loading="loading"
      :row-class-name="rowClass"
      size="small"
      border
      height="100%"
      style="width:100%"
      @sort-change="onSortChange"
      @row-click="onRowClick"
      @row-dblclick="onRowDblClick"
    >
      <!-- Row number column -->
      <el-table-column type="index" width="48" fixed="left" label="#" />

      <!-- Data columns -->
      <el-table-column
        v-for="col in orderedCols"
        :key="col"
        :prop="col"
        :label="colLabel(col) || col"
        :sortable="'custom'"
        :min-width="100"
        :show-overflow-tooltip="false"
      >
        <template #header>
          <div class="col-header">
            <span class="col-label-text">{{ colLabel(col) || col }}</span>
            <!-- ADDA filter button -->
            <el-popover
              v-if="col === 'ADDA'"
              :width="160"
              trigger="click"
              @show="initFilterLocal(col)"
            >
              <template #reference>
                <el-icon
                  class="filter-icon"
                  :class="{ 'filter-active': isFilterActive(col) }"
                  @click.stop
                ><Filter /></el-icon>
              </template>
              <div class="filter-popup">
                <div v-for="opt in (COL_OPTIONS[col] || [])" :key="String(opt)" class="filter-item">
                  <el-checkbox
                    :label="opt === '' ? '(空白)' : opt"
                    :model-value="(filterLocal[col] || []).includes(opt)"
                    @change="v => {
                      if (!filterLocal[col]) filterLocal[col] = []
                      if (v) filterLocal[col].push(opt)
                      else filterLocal[col] = filterLocal[col].filter(x => x !== opt)
                      onFilterCheck(col)
                    }"
                  />
                </div>
              </div>
            </el-popover>
          </div>
        </template>

        <template #default="{ row, $index }">
          <!-- Inline editing for non-readonly non-blob cols -->
          <template v-if="editingCell && editingCell.rowIndex === $index && editingCell.col === col">
            <template v-if="COL_OPTIONS[col]">
              <el-select v-model="editingValue" size="small" style="width:100%" @change="stopCellEdit(true)" @keydown="onCellKey">
                <el-option v-for="o in COL_OPTIONS[col]" :key="String(o)" :label="o || '(空)'" :value="o" />
              </el-select>
            </template>
            <template v-else>
              <el-input
                v-model="editingValue"
                size="small"
                autofocus
                @blur="stopCellEdit(true)"
                @keydown="onCellKey"
              />
            </template>
          </template>

          <!-- Display -->
          <template v-else>
            <span
              class="cell-text"
              :title="row[col] != null ? String(row[col]) : ''"
              @dblclick.stop="startCellEdit($index, col)"
            >{{ truncate30(row[col]) }}</span>
          </template>
        </template>
      </el-table-column>

      <!-- File column -->
      <el-table-column
        v-if="columns.includes('FILECONTENT')"
        label="文件"
        width="70"
        align="center"
      >
        <template #default="{ row }">
          <el-button v-if="row['FILECONTENT'] !== undefined" size="small" link @click.stop="openFiles(row)">
            🖼
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <div class="pagination-bar">
      <span class="pg-info">共 {{ totalRows }} 条，第 {{ currentPage }}/{{ totalPages }} 页</span>
      <el-pagination
        :current-page="currentPage"
        :page-size="rowsPerPage"
        :total="totalRows"
        :page-sizes="[20, 50, 100]"
        layout="sizes, prev, pager, next, jumper"
        small
        @current-change="onCurrentChange"
        @size-change="v => emit('size-change', v)"
      />
    </div>
  </div>
</template>

<style scoped>
.table-wrap {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  min-height: 0;
}
.col-header {
  display: flex; align-items: center; gap: 4px; white-space: nowrap;
}
.col-label-text { flex: 1; overflow: hidden; text-overflow: ellipsis; }
.filter-icon { cursor: pointer; color: #999; flex-shrink: 0; }
.filter-icon.filter-active { color: #1e6e42; }
.filter-popup { max-height: 200px; overflow-y: auto; }
.filter-item { padding: 2px 0; }

.cell-text {
  display: block;
  max-width: 200px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  cursor: default;
}

.pagination-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 6px 12px;
  border-top: 1px solid #e0e0e0;
  background: #fff;
  flex-shrink: 0;
}
.pg-info { font-size: 12px; color: #666; }

:deep(.row-selected) { background: #dceefa !important; }
:deep(.el-table .el-table__row:hover > td) { background: #f0f9ff; }
</style>
