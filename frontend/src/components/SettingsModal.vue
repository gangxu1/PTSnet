<script setup>
import { ref, reactive, computed } from 'vue'
import { ElMessage } from 'element-plus'
import { testConnection, connectDB, getTables } from '../api.js'
import { HIDDEN_COLS, ABOUT_TEXT, colLabel } from '../constants.js'

const props = defineProps({
  visible: Boolean,
  columns: Array,
  colVisibility: Object,
  currentTable: String,
  dbConnected: Boolean
})
const emit = defineEmits(['close', 'db-connected', 'load-table', 'col-vis-change', 'col-vis-all'])

const dbForm = reactive({ host: '10.223.35.82', port: '1521', name: 'mes', user: 'protot', pass: 'protot' })
const tables = ref([])
const selectedTable = ref(props.currentTable)
const testing = ref(false)
const connecting = ref(false)

async function onTest() {
  testing.value = true
  const res = await testConnection(dbForm)
  testing.value = false
  if (res.ok) ElMessage.success('连接测试成功')
  else ElMessage.error('连接失败: ' + (res.error || ''))
}

async function onConnect() {
  connecting.value = true
  const res = await connectDB(dbForm)
  connecting.value = false
  if (res.ok) {
    ElMessage.success('连接成功')
    emit('db-connected', true)
    const tr = await getTables()
    if (tr.ok) tables.value = tr.tables || []
  } else {
    ElMessage.error('连接失败: ' + (res.error || ''))
    emit('db-connected', false)
  }
}

async function loadTableList() {
  const tr = await getTables()
  if (tr.ok) tables.value = tr.tables || []
}

function onQuery() {
  if (!selectedTable.value) { ElMessage.warning('请选择数据表'); return }
  emit('load-table', selectedTable.value)
}

const visibleCols = computed(() =>
  (props.columns || []).filter(c => !HIDDEN_COLS.has(c))
)
</script>

<template>
  <el-dialog
    :model-value="visible"
    title="⚙ 设置"
    width="520px"
    :close-on-click-modal="false"
    @close="emit('close')"
  >
    <div class="settings-body">
      <!-- DB Connection -->
      <div class="section-title">数据库连接</div>
      <el-form :model="dbForm" label-width="60px" size="small">
        <el-form-item label="主机"><el-input v-model="dbForm.host" /></el-form-item>
        <el-form-item label="端口"><el-input v-model="dbForm.port" /></el-form-item>
        <el-form-item label="库名"><el-input v-model="dbForm.name" /></el-form-item>
        <el-form-item label="用户"><el-input v-model="dbForm.user" /></el-form-item>
        <el-form-item label="密码"><el-input v-model="dbForm.pass" type="password" show-password /></el-form-item>
        <el-form-item>
          <el-button size="small" :loading="testing" @click="onTest">测试连接</el-button>
          <el-button size="small" type="primary" :loading="connecting" @click="onConnect">连接数据库</el-button>
        </el-form-item>
      </el-form>

      <!-- Table select -->
      <div class="section-title" style="margin-top:14px">数据表选择</div>
      <div style="display:flex;gap:8px;align-items:center;margin-bottom:8px">
        <el-select v-model="selectedTable" placeholder="选择数据表" size="small" style="flex:1" @visible-change="v=>v&&loadTableList()">
          <el-option v-for="t in tables" :key="t" :label="t" :value="t" />
          <el-option v-if="!tables.length" :label="currentTable" :value="currentTable" />
        </el-select>
        <el-button size="small" type="primary" @click="onQuery">查询</el-button>
      </div>

      <!-- About -->
      <div class="section-title" style="margin-top:14px">About</div>
      <el-input type="textarea" :value="ABOUT_TEXT" :rows="3" readonly resize="none"
        style="font-size:12px;color:#555;background:#f9f9f9" />

      <!-- Column Visibility -->
      <div class="section-title" style="margin-top:14px">显示列设置</div>
      <div style="margin-bottom:6px">
        <el-button size="small" @click="emit('col-vis-all', true)">全选</el-button>
        <el-button size="small" @click="emit('col-vis-all', false)">全不选</el-button>
      </div>
      <div class="col-vis-grid">
        <div v-for="col in visibleCols" :key="col" class="col-vis-item">
          <el-checkbox
            :model-value="colVisibility[col] !== false"
            @change="v => emit('col-vis-change', col, v)"
          >{{ colLabel(col) || col }}</el-checkbox>
        </div>
      </div>
    </div>

    <template #footer>
      <el-button @click="emit('close')">关闭</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.settings-body { max-height: 60vh; overflow-y: auto; padding-right: 4px; }
.section-title {
  font-weight: 600; font-size: 12px; color: #444;
  border-bottom: 1px solid #e0e0e0; padding-bottom: 4px; margin-bottom: 8px;
}
.col-vis-grid {
  display: grid; grid-template-columns: repeat(3, 1fr); gap: 4px;
}
.col-vis-item { font-size: 12px; }
</style>
