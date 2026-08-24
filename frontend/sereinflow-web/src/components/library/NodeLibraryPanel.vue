<script setup lang="ts">
import { AlertTriangle, Database, LayoutGrid, RefreshCw, Search, Server, UploadCloud } from 'lucide-vue-next'
import { t } from '../../i18n'
import type { LibraryDto, LibraryNodeDto } from '../../api/libraryApi'

defineProps<{
  mobileVisible: boolean
  librarySearch: string
  isLoading: boolean
  error: string
  visibleLibraries: Array<{ library: LibraryDto; nodes: LibraryNodeDto[] }>
  catalogNodeCount: number
}>()

const emit = defineEmits<{
  'update:librarySearch': [value: string]
  upload: []
  retry: []
  'drag-node': [event: DragEvent, node: LibraryNodeDto]
  'drag-end': []
}>()

function updateSearch(event: Event): void {
  emit('update:librarySearch', (event.target as HTMLInputElement).value)
}
</script>

<template>
  <aside class="node-library" :class="{ 'mobile-visible': mobileVisible }">
    <div class="panel-heading">
      <div><span class="eyebrow">{{ t('library.build') }}</span><h1>{{ t('library.nodeLibrary') }}</h1></div>
      <div class="library-heading-actions"><span class="library-badge"><Server :size="11" />API</span><button class="icon-button compact" type="button" :title="t('library.upload')" :aria-label="t('library.upload')" @click="emit('upload')"><UploadCloud :size="15" /></button></div>
    </div>
    <label class="library-search"><Search :size="14" aria-hidden="true" /><input :value="librarySearch" type="search" :placeholder="t('library.searchPlaceholder')" @input="updateSearch" /></label>

    <div v-if="isLoading" class="library-catalog-state"><RefreshCw class="spin" :size="18" /><strong>{{ t('library.loading') }}</strong></div>
    <div v-else-if="error" class="library-catalog-state library-catalog-state--error"><AlertTriangle :size="18" /><strong>{{ error }}</strong><button type="button" @click="emit('retry')">{{ t('command.retry') }}</button></div>
    <div v-else-if="visibleLibraries.length === 0" class="library-empty">
      <div class="library-empty__mark" aria-hidden="true"><LayoutGrid :size="18" /></div>
      <strong>{{ librarySearch ? t('library.noSearchResults') : t('library.emptyCatalog') }}</strong>
      <p class="empty-copy">{{ librarySearch ? t('library.empty') : t('library.emptyCatalogHint') }}</p>
      <span class="library-empty__hint">{{ t('library.serverOnly') }}</span>
    </div>
    <div v-else class="library-catalog">
      <section v-for="entry in visibleLibraries" :key="entry.library.id" class="library-catalog__group">
        <div class="library-catalog__heading"><div><strong>{{ entry.library.name }}</strong><span>{{ entry.library.version }}</span></div><span class="mono">{{ t('library.nodeCount', { count: entry.nodes.length }) }}</span></div>
        <button v-for="node in entry.nodes" :key="node.id" class="library-node" type="button" draggable="true" @dragstart="emit('drag-node', $event, node)" @dragend="emit('drag-end')">
          <span class="library-node__mark"><Database :size="14" /></span><span class="library-node__body"><strong>{{ node.displayName }}</strong><span>{{ node.className }}.{{ node.methodName }}</span></span><span class="library-node__drag-hint">{{ t('library.dragHint') }}</span>
        </button>
      </section>
    </div>
    <div class="library-footer"><div class="status-line"><span class="status-dot" :class="{ 'status-dot--idle': isLoading || error }"></span><span>{{ error ? t('library.loadFailed') : catalogNodeCount > 0 ? t('library.nodeCount', { count: catalogNodeCount }) : t('library.catalogWaiting') }}</span><span class="mono">API</span></div><button class="footer-link" type="button" @click="emit('upload')"><UploadCloud :size="14" />{{ t('library.upload') }}</button></div>
  </aside>
</template>
