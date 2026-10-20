<script setup lang="ts">
import { computed } from 'vue';
import { useData, withBase } from 'vitepress';
const { page } = useData();
const english = computed(() => {
  const startsWithResult: boolean = page.value.relativePath.startsWith('en/');
  return startsWithResult;
});
const counterpart = computed(() => {
  const replaceResult = page.value.relativePath.replace(/^en\//, '');
  const replaceResult1 = replaceResult.replace(/(?:^|\/)index\.md$/, '/');
  const relative = replaceResult1.replace(/\.md$/, '');
  const path = relative.startsWith('/') ? relative : `/${relative}`;
  const withBaseResult: string = withBase(english.value ? path : `/en${path}`);
  return withBaseResult;
});
function rememberLanguage() {
  try {
    localStorage.setItem('sereinflow-docs-language', english.value ? 'zh' : 'en');
  }
  catch { /* The link still works when storage is disabled. */
  }
}
const readALang = computed(() => (english.value ? 'zh-CN' : 'en-US'));
const readTemplateText = computed(() => (english.value ? '简体中文' : 'English'));
</script>

<template>
  <a
    class="flow-language-switch"
    :href="counterpart"
    :lang="readALang"
    @click="rememberLanguage"
  >{{ readTemplateText }}</a>
</template>
