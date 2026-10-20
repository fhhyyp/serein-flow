<script setup lang="ts">
import { computed } from 'vue';
type TemplateItem<T> = T extends readonly (infer Item)[] ? Item : T extends Record<string, infer Value> ? Value : T extends number ? number : never;
import { AlertTriangle, Braces, Database, Link2, LayoutGrid, PanelLeftClose, RefreshCw, Search, Server, Zap } from 'lucide-vue-next';
import { t } from '../../i18n';
import type { NodeCreationDescriptorDto } from '../../api/flowApi';
import type { LibraryDto, LibraryNodeDto } from '../../api/libraryApi';
const props = withDefaults(defineProps<{
  embedded?: boolean;
  mobileVisible: boolean;
  librarySearch: string;
  isLoading: boolean;
  error: string;
  visibleLibraries: Array<{
    library: LibraryDto;
    nodes: LibraryNodeDto[];
  }>;
  visibleBuiltinNodes: NodeCreationDescriptorDto[];
  catalogNodeCount: number;
}>(), {
  embedded: false,
});
const emit = defineEmits<{
  'update:librarySearch': [
    value: string
  ];
  manage: [
  ];
  retry: [
  ];
  'node-pointer-down': [
    event: PointerEvent,
    node: LibraryNodeDto
  ];
  'builtin-node-pointer-down': [
    event: PointerEvent,
    node: NodeCreationDescriptorDto
  ];
  collapse: [
  ];
}>();
function updateSearch(event: Event): void {
  emit('update:librarySearch', (event.target as HTMLInputElement).value);
}
function iconForLibraryNode(node: LibraryNodeDto) {
  return node.type === 'flipflop' ? Zap : Database;
}
const readAsideClass = computed(() => ({ 'mobile-visible': props.mobileVisible, 'node-library--embedded': props.embedded }));
const readDivElseIf = computed(() => (props.visibleLibraries.length === 0 && props.visibleBuiltinNodes.length === 0));
const readTemplateText = computed(() => {
  const evaluatedValue: string = (props.librarySearch ? t('library.noSearchResults') : t('library.emptyCatalog'));
  return evaluatedValue;
});
const readTemplateText2 = computed(() => {
  const evaluatedValue1: string = (props.librarySearch ? t('library.empty') : t('library.projectEmptyHint'));
  return evaluatedValue1;
});
const readSectionIf = computed(() => (props.visibleBuiltinNodes.length > 0));
function readButtonItems() {
  return (props.visibleBuiltinNodes);
}
function readTemplateText3(node: TemplateItem<ReturnType<typeof readButtonItems>>) {
  const evaluatedValue2 = (t(node.ui.titleKey) === node.ui.titleKey ? node.displayName : t(node.ui.titleKey));
  return evaluatedValue2;
}
function readTemplateText4(node: TemplateItem<ReturnType<typeof readButtonItems>>) {
  const evaluatedValue3 = (node.description || t(node.ui.subtitleKey));
  return evaluatedValue3;
}
function readSectionItems() {
  return (props.visibleLibraries);
}
function readTemplateText5(entry: TemplateItem<ReturnType<typeof readSectionItems>>) {
  const evaluatedValue4 = (t('library.nodeCount', { count: entry.nodes.length }));
  return evaluatedValue4;
}
function readButtonItems2(entry: TemplateItem<ReturnType<typeof readSectionItems>>) {
  return (entry.nodes);
}
function readSpanClass(node: TemplateItem<ReturnType<typeof readButtonItems2>>) {
  return (`library-node__mark--${node.type}`);
}
const readSpanClass2 = computed(() => ({ 'status-dot--idle': props.isLoading || props.error }));
const readTemplateText6 = computed(() => {
  const evaluatedValue5: string = (props.error ? t('library.loadFailed') : props.catalogNodeCount > 0 ? t('library.nodeCount', { count: props.catalogNodeCount }) : t('library.catalogWaiting'));
  return evaluatedValue5;
});
</script>

<template>
  <aside
    class="node-library"
    :class="readAsideClass"
  >
    <div
      v-if="!props.embedded"
      class="panel-heading"
    >
      <div>
        <span class="eyebrow">{{ t('library.projectScope') }}</span>
        <h1>{{ t('library.nodeLibrary') }}</h1>
      </div>
      <div class="library-heading-actions">
        <span class="library-badge">
          <Server :size="11" />
          API
        </span>
        <button
          class="icon-button compact"
          type="button"
          :title="t('library.manageProject')"
          :aria-label="t('library.manageProject')"
          @click="emit('manage')"
        >
          <Link2 :size="15" />
        </button>
        <button
          class="icon-button compact"
          type="button"
          :title="t('panel.collapseLibrary')"
          :aria-label="t('panel.collapseLibrary')"
          @click="emit('collapse')"
        >
          <PanelLeftClose :size="15" />
        </button>
      </div>
    </div>
    <div
      v-else
      class="library-embedded-actions"
    >
      <span class="library-badge">
        <Server :size="11" />
        API
      </span>
      <button
        class="icon-button compact"
        type="button"
        :title="t('library.manageProject')"
        :aria-label="t('library.manageProject')"
        @click="emit('manage')"
      >
        <Link2 :size="15" />
      </button>
    </div>
    <label class="library-search">
      <Search
        :size="14"
        aria-hidden="true"
       />
      <input
        :value="props.librarySearch"
        type="search"
        :placeholder="t('library.searchPlaceholder')"
        @input="updateSearch"
       />
    </label>
    <div
      v-if="props.isLoading"
      class="library-catalog-state"
    >
      <RefreshCw
        class="spin"
        :size="18"
       />
      <strong>{{ t('library.loading') }}</strong>
    </div>
    <div
      v-else-if="props.error"
      class="library-catalog-state library-catalog-state--error"
    >
      <AlertTriangle :size="18" />
      <strong>{{ props.error }}</strong>
      <button
        type="button"
        @click="emit('retry')"
      >{{ t('command.retry') }}</button>
    </div>
    <div
      v-else-if="readDivElseIf"
      class="library-empty"
    >
      <div
        class="library-empty__mark"
        aria-hidden="true"
      >
        <LayoutGrid :size="18" />
      </div>
      <strong>{{ readTemplateText }}</strong>
      <p class="empty-copy">{{ readTemplateText2 }}</p>
      <button
        v-if="!props.librarySearch"
        class="library-empty__action"
        type="button"
        @click="emit('manage')"
      >
        <Link2 :size="14" />
        {{ t('library.manageProject') }}
      </button>
      <span class="library-empty__hint">{{ t('library.serverOnly') }}</span>
    </div>
    <div
      v-else
      class="library-catalog"
    >
      <section
        v-if="readSectionIf"
        class="library-catalog__group library-catalog__group--builtin"
      >
        <div class="library-catalog__heading">
          <div>
            <strong>{{ t('library.basicNodes') }}</strong>
            <span>{{ t('library.basicNodesHint') }}</span>
          </div>
          <span class="mono">{{ props.visibleBuiltinNodes.length }}</span>
        </div>
        <button
          v-for="node in readButtonItems()"
          :key="node.id"
          class="library-node library-node--builtin"
          type="button"
          draggable="false"
          @pointerdown="emit('builtin-node-pointer-down', $event, node)"
        >
          <span class="library-node__mark">
            <Braces :size="14" />
          </span>
          <span class="library-node__body">
            <strong>{{ readTemplateText3(node) }}</strong>
            <span>{{ readTemplateText4(node) }}</span>
          </span>
          <span class="library-node__drag-hint">{{ t('library.dragHint') }}</span>
        </button>
      </section>
      <section
        v-for="entry in readSectionItems()"
        :key="entry.library.id"
        class="library-catalog__group"
      >
        <div class="library-catalog__heading">
          <div>
            <strong>{{ entry.library.name }}</strong>
            <span>{{ entry.library.version }}</span>
          </div>
          <span class="mono">{{ readTemplateText5(entry) }}</span>
        </div>
        <button
          v-for="node in readButtonItems2(entry)"
          :key="node.id"
          class="library-node"
          type="button"
          draggable="false"
          @pointerdown="emit('node-pointer-down', $event, node)"
        >
          <span
            class="library-node__mark"
            :class="readSpanClass(node)"
          >
            <component
              :is="iconForLibraryNode(node)"
              :size="14"
              aria-hidden="true"
             />
          </span>
          <span class="library-node__body">
            <strong>{{ node.displayName }}</strong>
            <span>{{ node.className }}.{{ node.methodName }}</span>
          </span>
          <span class="library-node__drag-hint">{{ t('library.dragHint') }}</span>
        </button>
      </section>
    </div>
    <div class="library-footer">
      <div class="status-line">
        <span
          class="status-dot"
          :class="readSpanClass2"
        ></span>
        <span>{{ readTemplateText6 }}</span>
        <span class="mono">API</span>
      </div>
      <button
        class="footer-link"
        type="button"
        @click="emit('manage')"
      >
        <Link2 :size="14" />
        {{ t('library.manageProject') }}
      </button>
    </div>
  </aside>
</template>
