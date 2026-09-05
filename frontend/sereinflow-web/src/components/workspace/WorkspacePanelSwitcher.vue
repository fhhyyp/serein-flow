<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { Bug, LayoutGrid, PanelLeft, Settings2 } from 'lucide-vue-next'
import { t } from '../../i18n'
import type { DockableWorkspaceMode, WorkspacePanelId, WorkspacePanelTab } from '../../flow/dockableWorkspace'

export interface WorkspacePanelItem extends WorkspacePanelTab {
  visible: boolean
  available: boolean
  required?: boolean
}

const props = withDefaults(defineProps<{
  items: WorkspacePanelItem[]
  displayMode: DockableWorkspaceMode
  debugModeAvailable: boolean
  showDisplayModes?: boolean
}>(), {
  showDisplayModes: true,
})

const emit = defineEmits<{
  toggle: [panelId: WorkspacePanelId]
  reset: []
  'change-mode': [mode: DockableWorkspaceMode]
}>()

const root = ref<HTMLElement>()
const open = ref(false)

function onDocumentPointerdown(event: PointerEvent): void {
  const target = event.target
  if (open.value && root.value && target instanceof Node && !root.value.contains(target)) {
    open.value = false
  }
}

function onWindowKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape') open.value = false
}

function selectMode(mode: DockableWorkspaceMode): void {
  if (mode === 'debug' && !props.debugModeAvailable) return
  emit('change-mode', mode)
  open.value = false
}

onMounted(() => {
  document.addEventListener('pointerdown', onDocumentPointerdown)
  window.addEventListener('keydown', onWindowKeydown)
})

onBeforeUnmount(() => {
  document.removeEventListener('pointerdown', onDocumentPointerdown)
  window.removeEventListener('keydown', onWindowKeydown)
})
</script>

<template>
  <div ref="root" class="workspace-panel-menu">
    <button
      class="workspace-panel-menu__trigger"
      :class="{ open }"
      type="button"
      :title="t('panel.workspacePanels')"
      :aria-label="t('panel.workspacePanels')"
      aria-haspopup="menu"
      :aria-expanded="open"
      @click="open = !open"
    >
      <PanelLeft :size="15" aria-hidden="true" />
      <span>{{ t('panel.workspacePanels') }}</span>
    </button>
    <aside v-if="open" class="workspace-panel-switcher" :aria-label="t('panel.workspacePanels')" @keydown.esc="open = false">
      <div class="workspace-panel-switcher__heading"><span>{{ t('panel.workspacePanels') }}</span><button type="button" :title="t('panel.resetLayout')" :aria-label="t('panel.resetLayout')" @click="emit('reset')"><Settings2 :size="14" /></button></div>
      <template v-if="props.showDisplayModes">
        <div class="workspace-panel-switcher__mode-label">{{ t('panel.displayMode') }}</div>
        <div class="workspace-panel-switcher__modes" role="radiogroup" :aria-label="t('panel.displayMode')">
          <button type="button" role="radio" class="workspace-panel-switcher__mode" :class="{ active: props.displayMode === 'edit' }" :aria-checked="props.displayMode === 'edit'" @click="selectMode('edit')">
            <LayoutGrid :size="14" /><span>{{ t('panel.editMode') }}</span>
          </button>
          <button type="button" role="radio" class="workspace-panel-switcher__mode" :class="{ active: props.displayMode === 'debug' }" :aria-checked="props.displayMode === 'debug'" :disabled="!props.debugModeAvailable" :title="!props.debugModeAvailable ? t('panel.debugModeUnavailable') : undefined" @click="selectMode('debug')">
            <Bug :size="14" /><span>{{ t('panel.debugMode') }}</span>
          </button>
        </div>
      </template>
      <button v-for="item in props.items" :key="item.id" type="button" class="workspace-panel-switcher__item" :class="{ active: item.visible, required: item.required }" :disabled="!item.available || item.required" :aria-pressed="item.visible" @click="emit('toggle', item.id)">
        <component :is="item.icon" :size="14" />
        <span>{{ item.label }}</span>
        <i :class="{ on: item.visible }" aria-hidden="true"></i>
      </button>
    </aside>
  </div>
</template>
