<script setup>
import { ref } from 'vue'
import { Search, Refresh, Plus, CopyDocument, Edit, Delete, Download, Setting } from '@element-plus/icons-vue'

const props = defineProps({
  dbConnected: Boolean,
  search: String,
  rowsPerPage: Number
})

const emit = defineEmits([
  'search-change', 'refresh', 'new-row', 'copy-row', 'edit-row',
  'delete-row', 'export-csv', 'open-settings', 'size-change'
])

let searchTimer = null
function onSearchInput(val) {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => emit('search-change', val), 400)
}
</script>

<template>
  <div class="toolbar">
    <!-- Left: search -->
    <div class="toolbar-left">
      <el-input
        :model-value="search"
        placeholder="搜索..."
        clearable
        size="small"
        style="width:220px"
        @input="onSearchInput"
        @clear="emit('search-change', '')"
      >
        <template #prefix><el-icon><Search /></el-icon></template>
      </el-input>
    </div>

    <!-- Right: controls -->
    <div class="toolbar-right">
      <el-select
        :model-value="rowsPerPage"
        size="small"
        style="width:80px"
        @change="v => emit('size-change', v)"
      >
        <el-option :value="20" label="20/页" />
        <el-option :value="50" label="50/页" />
        <el-option :value="100" label="100/页" />
      </el-select>

      <span class="db-status" :class="{ connected: dbConnected }">
        <span class="dot"></span>{{ dbConnected ? 'DB已连接' : 'DB未连接' }}
      </span>

      <el-button size="small" @click="emit('refresh')">
        <el-icon><Refresh /></el-icon> 刷新
      </el-button>
      <el-button size="small" type="primary" @click="emit('new-row')">
        <el-icon><Plus /></el-icon> New
      </el-button>
      <el-button size="small" style="background:#d35400;border-color:#d35400;color:#fff" @click="emit('copy-row')">
        <el-icon><CopyDocument /></el-icon> Copy
      </el-button>
      <el-button size="small" style="background:#8e44ad;border-color:#8e44ad;color:#fff" @click="emit('edit-row')">
        <el-icon><Edit /></el-icon> Edit
      </el-button>
      <el-button size="small" type="danger" @click="emit('delete-row')">
        <el-icon><Delete /></el-icon> Delete
      </el-button>
      <el-button size="small" @click="emit('export-csv')">
        <el-icon><Download /></el-icon> 导出 CSV
      </el-button>
      <el-button size="small" style="background:#555;border-color:#555;color:#fff" @click="emit('open-settings')">
        <el-icon><Setting /></el-icon> 设置
      </el-button>
    </div>
  </div>
</template>

<style scoped>
.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: #1e6e42;
  padding: 6px 10px;
  gap: 8px;
  flex-shrink: 0;
}
.toolbar-left { display: flex; align-items: center; }
.toolbar-right { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }

.db-status {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
  color: #ccc;
  padding: 0 4px;
}
.db-status.connected { color: #2ecc71; }
.dot {
  width: 8px; height: 8px; border-radius: 50%;
  background: #999;
  flex-shrink: 0;
}
.db-status.connected .dot { background: #2ecc71; }
</style>
