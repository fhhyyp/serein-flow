<script setup lang="ts">
import { AlertTriangle, Braces, Database, Link2, LayoutGrid, RefreshCw, Search, Server } from 'lucide-vue-next'
import { t } from '../../i18n'
import type { NodeCreationDescriptorDto } from '../../api/flowApi'
import type { LibraryDto, LibraryNodeDto } from '../../api/libraryApi'

defineProps<{
  mobileVisible: boolean
  librarySearch: string
  isLoading: boolean
  error: string
  visibleLibraries: Array<{ library: LibraryDto; nodes: LibraryNodeDto[] }>
  visibleBuiltinNodes: NodeCreationDescriptorDto[]
  catalogNodeCount: number
}>()

const emit = defineEmits<{
  'update:librarySearch': [value: string]
  manage: []
  retry: []
  'node-pointer-down': [event: PointerEvent, node: LibraryNodeDto]
  'builtin-node-pointer-down': [event: PointerEvent, node: NodeCreationDescriptorDto]
}>()

function updateSearch(event: Event): void {
  emit('update:librarySearch', (event.target as HTMLInputElement).value)
}
</script>

<template>
  <aside class="node-library" :class="{ 'mobile-visible': mobileVisible }">
    <div class="panel-heading">
      <div><span class="eyebrow">{{ t('library.projectScope') }}</span><h1>{{ t('library.nodeLibrary') }}</h1></div>
      <div class="library-heading-actions"><span class="library-badge"><Server :size="11" />API</span><button class="icon-button compact" type="button" :title="t('library.manageProject')" :aria-label="t('library.manageProject')" @click="emit('manage')"><Link2 :size="15" /></button></div>
    </div>
    <label class="library-search"><Search :size="14" aria-hidden="true" /><input :value="librarySearch" type="search" :placeholder="t('library.searchPlaceholder')" @input="updateSearch" /></label>

    <div v-if="isLoading" class="library-catalog-state"><RefreshCw class="spin" :size="18" /><strong>{{ t('library.loading') }}</strong></div>
    <div v-else-if="error" class="library-catalog-state library-catalog-state--error"><AlertTriangle :size="18" /><strong>{{ error }}</strong><button type="button" @click="emit('retry')">{{ t('command.retry') }}</button></div>
    <div v-else-if="visibleLibraries.length === 0 && visibleBuiltinNodes.length === 0" class="library-empty">
      <div class="library-empty__mark" aria-hidden="true"><LayoutGrid :size="18" /></div>
      <strong>{{ librarySearch ? t('library.noSearchResults') : t('library.emptyCatalog') }}</strong>
      <p class="empty-copy">{{ librarySearch ? t('library.empty') : t('library.projectEmptyHint') }}</p>
      <button v-if="!librarySearch" class="library-empty__action" type="button" @click="emit('manage')"><Link2 :size="14" />{{ t('library.manageProject') }}</button>
      <span class="library-empty__hint">{{ t('library.serverOnly') }}</span>
    </div>
    <div v-else class="library-catalog">
      <section v-if="visibleBuiltinNodes.length > 0" class="library-catalog__group library-catalog__group--builtin">
        <div class="library-catalog__heading"><div><strong>{{ t('library.basicNodes') }}</strong><span>{{ t('library.basicNodesHint') }}</span></div><span class="mono">{{ visibleBuiltinNodes.length }}</span></div>
        <button v-for="node in visibleBuiltinNodes" :key="node.id" class="library-node library-node--builtin" type="button" draggable="false" @pointerdown="emit('builtin-node-pointer-down', $event, node)">
          <span class="library-node__mark"><Braces :size="14" /></span><span class="library-node__body"><strong>{{ t(node.ui.titleKey) === node.ui.titleKey ? node.displayName : t(node.ui.titleKey) }}</strong><span>{{ node.description || t(node.ui.subtitleKey) }}</span></span><span class="library-node__drag-hint">{{ t('library.dragHint') }}</span>
        </button>
      </section>
      <section v-for="entry in visibleLibraries" :key="entry.library.id" class="library-catalog__group">
        <div class="library-catalog__heading"><div><strong>{{ entry.library.name }}</strong><span>{{ entry.library.version }}</span></div><span class="mono">{{ t('library.nodeCount', { count: entry.nodes.length }) }}</span></div>
        <button v-for="node in entry.nodes" :key="node.id" class="library-node" type="button" draggable="false" @pointerdown="emit('node-pointer-down', $event, node)">
          <span class="library-node__mark"><Database :size="14" /></span><span class="library-node__body"><strong>{{ node.displayName }}</strong><span>{{ node.className }}.{{ node.methodName }}</span></span><span class="library-node__drag-hint">{{ t('library.dragHint') }}</span>
        </button>
      </section>
    </div>
    <div class="library-footer"><div class="status-line"><span class="status-dot" :class="{ 'status-dot--idle': isLoading || error }"></span><span>{{ error ? t('library.loadFailed') : catalogNodeCount > 0 ? t('library.nodeCount', { count: catalogNodeCount }) : t('library.catalogWaiting') }}</span><span class="mono">API</span></div><button class="footer-link" type="button" @click="emit('manage')"><Link2 :size="14" />{{ t('library.manageProject') }}</button></div>
  </aside>
</template>
