<script setup lang="ts">
import { computed } from 'vue'
import { useData, withBase } from 'vitepress'

const { page } = useData()
const english = computed(() => page.value.relativePath.startsWith('en/'))
const counterpart = computed(() => {
  const relative = page.value.relativePath.replace(/^en\//, '').replace(/(?:^|\/)index\.md$/, '/').replace(/\.md$/, '')
  const path = relative.startsWith('/') ? relative : `/${relative}`
  return withBase(english.value ? path : `/en${path}`)
})

function rememberLanguage() {
  try {
    localStorage.setItem('sereinflow-docs-language', english.value ? 'zh' : 'en')
  } catch { /* The link still works when storage is disabled. */ }
}
</script>

<template>
  <a class="flow-language-switch" :href="counterpart" :lang="english ? 'zh-CN' : 'en-US'" @click="rememberLanguage">
    {{ english ? '简体中文' : 'English' }}
  </a>
</template>
