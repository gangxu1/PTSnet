<script setup>
import { ref, computed, watch, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { UploadFilled, Delete } from '@element-plus/icons-vue'
import {
  HIDDEN_COLS, READONLY_COLS, REQUIRED_COLS, BLOB_COLS, NEW_HIDDEN_COLS,
  COL_OPTIONS, COL_AUTOCOMPLETE, COL_DEFAULTS, COL_ORDER,
  colLabel, today, formatBytes, fileIcon, isImage
} from '../constants.js'
import { getFileList, fileDownloadUrl, getDistinct } from '../api.js'

const props = defineProps({
  visible: Boolean,
  mode: String,      // 'new' | 'edit' | 'copy'
  rowData: Object,
  columns: Array,
  table: String,
  tablePKs: Array
})
const emit = defineEmits(['close', 'save'])

// ── Titles and colors ─────────────────────────────────────────────────────
const modalTitle = computed(() => ({
  new: '新增数据行', edit: '编辑数据行', copy: '复制数据行'
}[props.mode] || ''))

const headerColor = computed(() => ({
  new: '#1e6e42', edit: '#8e44ad', copy: '#d35400'
}[props.mode] || '#555'))

// ── Form fields to show ───────────────────────────────────────────────────
const formCols = computed(() => {
  const colSet = new Set(props.columns || [])
  const ordered = COL_ORDER.filter(c => colSet.has(c))
  const extra = (props.columns || []).filter(c => !COL_ORDER.includes(c))
  return [...ordered, ...extra].filter(col => {
    if (BLOB_COLS.has(col)) return false
    if (HIDDEN_COLS.has(col)) return false
    if (props.mode === 'new' && NEW_HIDDEN_COLS.has(col)) return false
    return true
  })
})

// ── Form data ─────────────────────────────────────────────────────────────
const formData = ref({})

function initFormData() {
  const d = {}
  for (const col of props.columns || []) {
    if (props.mode === 'new') {
      d[col] = COL_DEFAULTS[col] !== undefined ? COL_DEFAULTS[col] : ''
    } else {
      d[col] = props.rowData?.[col] ?? ''
    }
  }
  // Copy: clear CREATETIME so it gets set fresh
  if (props.mode === 'copy') {
    d['CREATETIME'] = ''
    d['MODIFYDATE'] = ''
    // Don't carry file data (handled separately)
  }
  formData.value = d
}

watch(() => props.visible, v => { if (v) initFormData() }, { immediate: true })

// ── File handling ─────────────────────────────────────────────────────────
const newFiles = ref([])        // File objects from picker
const existingFiles = ref([])   // {name, size} from DB
const deletedFiles = ref([])    // filenames to delete

onMounted(async () => {
  if (props.mode === 'edit' && props.tablePKs?.length && props.rowData) {
    const pk = props.tablePKs[0]
    const pkVal = props.rowData[pk]
    if (pkVal) {
      const res = await getFileList(props.table, pk, pkVal)
      if (res.ok) existingFiles.value = res.files || []
    }
  }
})

function onFileChange(uploadFile, uploadFiles) {
  newFiles.value = uploadFiles.map(f => f.raw)
}

function removeNewFile(idx) {
  newFiles.value.splice(idx, 1)
}

function removeExistingFile(idx) {
  const f = existingFiles.value[idx]
  deletedFiles.value.push(f.name)
  existingFiles.value.splice(idx, 1)
}

function getPKFileUrl(filename) {
  const pk = props.tablePKs?.[0]
  const pkVal = props.rowData?.[pk]
  if (!pk || !pkVal) return '#'
  return fileDownloadUrl(props.table, pk, pkVal, filename)
}

// Image files for lightbox preview
const imageExisting = computed(() => existingFiles.value.filter(f => isImage(f.name)))
const imageNewFiles = computed(() => newFiles.value.filter(f => isImage(f.name)))

// ── Autocomplete ──────────────────────────────────────────────────────────
const acCache = ref({})
async function queryAutocomplete(col, queryStr, cb) {
  if (!acCache.value[col]) {
    const res = await getDistinct(props.table, col)
    acCache.value[col] = (res.ok ? res.values : []).map(v => ({ value: v }))
  }
  const q = queryStr.toLowerCase()
  cb(acCache.value[col].filter(item => item.value.toLowerCase().includes(q)))
}

// ── Validation & Save ─────────────────────────────────────────────────────
async function onSave() {
  // Validate required
  for (const col of REQUIRED_COLS) {
    if (formCols.value.includes(col) && !formData.value[col]) {
      ElMessage.error(`${colLabel(col)} 不能为空`)
      return
    }
  }

  const fd = new FormData()
  fd.append('table', props.table)

  if (props.mode === 'edit') {
    const pk = props.tablePKs?.[0]
    if (!pk) { ElMessage.error('无主键'); return }
    fd.append('pkCol', pk)
    fd.append('pkVal', props.rowData?.[pk] ?? '')
    // Auto-fill MODIFYDATE
    fd.append('col_MODIFYDATE', today())
  } else {
    // New / Copy — auto-fill CREATETIME
    fd.append('col_CREATETIME', today())
  }

  for (const col of formCols.value) {
    if (READONLY_COLS.has(col)) continue
    if (BLOB_COLS.has(col)) continue
    if (col === 'CREATETIME' || col === 'MODIFYDATE') continue
    fd.append(`col_${col}`, formData.value[col] ?? '')
  }

  for (const file of newFiles.value) {
    fd.append('files', file)
  }
  for (const name of deletedFiles.value) {
    fd.append('deleteFile', name)
  }

  emit('save', fd)
}
</script>

<template>
  <el-dialog
    :model-value="visible"
    :title="modalTitle"
    width="540px"
    :close-on-click-modal="false"
    @close="emit('close')"
  >
    <template #header>
      <div :style="{ background: headerColor, color: '#fff', padding: '12px 16px', borderRadius: '4px 4px 0 0', fontWeight: 600 }">
        {{ modalTitle }}
      </div>
    </template>

    <el-form :model="formData" label-width="120px" size="small" style="max-height:60vh;overflow-y:auto;padding-right:4px">
      <el-form-item
        v-for="col in formCols"
        :key="col"
        :label="colLabel(col) || col"
        :required="REQUIRED_COLS.has(col)"
      >
        <!-- Dropdown (COL_OPTIONS) -->
        <el-select v-if="COL_OPTIONS[col]" v-model="formData[col]" :disabled="READONLY_COLS.has(col)" style="width:100%">
          <el-option v-for="opt in COL_OPTIONS[col]" :key="String(opt)" :label="opt || '(空)'" :value="opt" />
        </el-select>

        <!-- Autocomplete -->
        <el-autocomplete
          v-else-if="COL_AUTOCOMPLETE.has(col)"
          v-model="formData[col]"
          :fetch-suggestions="(q, cb) => queryAutocomplete(col, q, cb)"
          :disabled="READONLY_COLS.has(col)"
          style="width:100%"
          clearable
        />

        <!-- Regular input -->
        <el-input
          v-else
          v-model="formData[col]"
          :disabled="READONLY_COLS.has(col)"
          clearable
        />
      </el-form-item>

      <!-- File upload (edit and copy show existing files) -->
      <el-form-item label="文件">
        <!-- Existing files (edit mode) -->
        <div v-if="existingFiles.length" style="width:100%;margin-bottom:8px">
          <div v-for="(f, i) in existingFiles" :key="f.name" class="file-row">
            <span class="file-icon-lbl">{{ fileIcon(f.name) }}</span>
            <a :href="getPKFileUrl(f.name)" target="_blank" class="file-name-lbl">{{ f.name }}</a>
            <span class="file-size-lbl">{{ formatBytes(f.size) }}</span>
            <el-button size="small" link type="danger" @click="removeExistingFile(i)">
              <el-icon><Delete /></el-icon>
            </el-button>
          </div>
        </div>

        <!-- New files picked -->
        <div v-if="newFiles.length" style="width:100%;margin-bottom:8px">
          <div v-for="(f, i) in newFiles" :key="f.name + i" class="file-row">
            <span class="file-icon-lbl">{{ fileIcon(f.name) }}</span>
            <span class="file-name-lbl">{{ f.name }}</span>
            <span class="file-size-lbl">{{ formatBytes(f.size) }}</span>
            <el-button size="small" link type="danger" @click="removeNewFile(i)">
              <el-icon><Delete /></el-icon>
            </el-button>
          </div>
        </div>

        <!-- Drop zone -->
        <el-upload
          multiple
          :auto-upload="false"
          :show-file-list="false"
          drag
          style="width:100%"
          @change="onFileChange"
        >
          <el-icon :size="28"><UploadFilled /></el-icon>
          <div style="font-size:12px;color:#999;margin-top:4px">拖拽文件到此处，或点击上传</div>
        </el-upload>
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button @click="emit('close')">取消</el-button>
      <el-button type="primary" @click="onSave">保存</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.file-row {
  display: flex; align-items: center; gap: 6px;
  padding: 3px 0; font-size: 12px;
}
.file-icon-lbl { flex-shrink: 0; }
.file-name-lbl { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.file-size-lbl { color: #999; flex-shrink: 0; }
</style>
