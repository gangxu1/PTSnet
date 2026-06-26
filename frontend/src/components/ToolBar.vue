<script setup>
import { ref } from 'vue'
import { Search, Refresh, Plus, CopyDocument, Edit, Delete, Download, Setting } from '@element-plus/icons-vue'

const props = defineProps({})

const emit = defineEmits([
  'search-change', 'refresh', 'new-row', 'copy-row', 'edit-row',
  'delete-row', 'export-csv', 'open-settings'
])

const localSearch = ref('')
let searchTimer = null
function onSearchInput(val) {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => emit('search-change', val), 400)
}
function onSearchClear() {
  localSearch.value = ''
  emit('search-change', '')
}
</script>

<template>
  <div class="toolbar">
    <!-- Left: search -->
    <div class="toolbar-left">
      <el-input
        v-model="localSearch"
        placeholder="搜索..."
        clearable
        size="small"
        style="width:220px"
        @input="onSearchInput"
        @clear="onSearchClear"
      >
        <template #prefix><el-icon><Search /></el-icon></template>
      </el-input>
    </div>

    <!-- Right: controls -->
    <div class="toolbar-right">
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
      <el-button size="small" style="background:#27ae60;border-color:#27ae60;color:#fff" tag="a" href="/0_Pre-Classfication.xlsx" download="0_Pre-Classfication.xlsx">
        <el-icon><Download /></el-icon> 模板
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
</style>
