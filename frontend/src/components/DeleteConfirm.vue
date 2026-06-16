<script setup>
const props = defineProps({
  visible: Boolean,
  row: Object,
  tablePKs: Array
})
const emit = defineEmits(['close', 'confirm'])

const pkInfo = () => {
  if (!props.row || !props.tablePKs?.length) return ''
  return props.tablePKs.map(pk => `${pk}=${props.row[pk]}`).join(', ')
}
</script>

<template>
  <el-dialog
    :model-value="visible"
    title="确认删除"
    width="400px"
    :close-on-click-modal="false"
    @close="emit('close')"
  >
    <p style="margin-bottom:12px">确认要删除这一行吗？</p>
    <p style="color:#999;font-size:12px">{{ pkInfo() }}</p>
    <template #footer>
      <el-button @click="emit('close')">取消</el-button>
      <el-button type="danger" @click="emit('confirm')">删除</el-button>
    </template>
  </el-dialog>
</template>
