<script setup>
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import { ArrowLeft, ArrowRight, Close } from '@element-plus/icons-vue'

const props = defineProps({
  visible: Boolean,
  images: Array,   // [{name, url}]
  index: Number
})
const emit = defineEmits(['close'])

const idx = ref(props.index || 0)
watch(() => props.index, v => idx.value = v)
watch(() => props.visible, v => { if (v) idx.value = props.index || 0 })

const current = computed(() => props.images?.[idx.value] || null)

function prev() { if (idx.value > 0) idx.value-- }
function next() { if (idx.value < (props.images?.length || 0) - 1) idx.value++ }

function onKey(e) {
  if (!props.visible) return
  if (e.key === 'ArrowLeft') prev()
  else if (e.key === 'ArrowRight') next()
  else if (e.key === 'Escape') emit('close')
}
onMounted(() => window.addEventListener('keydown', onKey))
onUnmounted(() => window.removeEventListener('keydown', onKey))
</script>

<template>
  <div v-if="visible" class="lightbox-overlay" @click.self="emit('close')">
    <div class="lightbox-box">
      <button class="lb-close" @click="emit('close')"><el-icon><Close /></el-icon></button>
      <button v-if="images.length > 1 && idx > 0" class="lb-nav lb-prev" @click="prev">
        <el-icon><ArrowLeft /></el-icon>
      </button>
      <img v-if="current" :src="current.url" :alt="current.name" class="lb-img" />
      <button v-if="images.length > 1 && idx < images.length - 1" class="lb-nav lb-next" @click="next">
        <el-icon><ArrowRight /></el-icon>
      </button>
      <div v-if="current" class="lb-caption">{{ current.name }} ({{ idx + 1 }} / {{ images.length }})</div>
    </div>
  </div>
</template>

<style scoped>
.lightbox-overlay {
  position: fixed; inset: 0;
  background: rgba(0,0,0,.82);
  z-index: 9999;
  display: flex; align-items: center; justify-content: center;
}
.lightbox-box {
  position: relative;
  display: flex; flex-direction: column; align-items: center;
  max-width: 90vw; max-height: 90vh;
}
.lb-img { max-width: 80vw; max-height: 80vh; width: auto; height: auto; object-fit: contain; display: block; border-radius: 4px; }
.lb-close {
  position: absolute; top: -36px; right: 0;
  background: none; border: none; color: #fff; font-size: 22px; cursor: pointer;
}
.lb-nav {
  position: absolute; top: 50%; transform: translateY(-50%);
  background: rgba(0,0,0,.45); border: none; color: #fff;
  font-size: 24px; cursor: pointer; border-radius: 50%;
  width: 44px; height: 44px; display: flex; align-items: center; justify-content: center;
}
.lb-prev { left: -56px; }
.lb-next { right: -56px; }
.lb-caption { margin-top: 10px; color: #ddd; font-size: 13px; }
</style>
