<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { Bug, Check, LayoutGrid, PanelLeft, RotateCcw } from 'lucide-vue-next'
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
  open: []
}>()

const root = ref<HTMLElement>()
const trigger = ref<HTMLButtonElement>()
const open = ref(false)

function onDocumentPointerdown(event: PointerEvent): void {
  const target = event.target
  if (open.value && root.value && target instanceof Node && !root.value.contains(target)) {
    open.value = false
  }
}

function onWindowKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape' && open.value) {
    open.value = false
    trigger.value?.focus()
  }
}

function toggleOpen(): void {
  if (!open.value) emit('open')
  open.value = !open.value
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
      ref="trigger"
      class="workspace-panel-menu__trigger"
      :class="{ open }"
      type="button"
      :title="t('panel.workspacePanels')"
      :aria-label="t('panel.workspacePanels')"
      aria-haspopup="dialog"
      :aria-expanded="open"
      @click="toggleOpen"
    >
      <PanelLeft :size="15" aria-hidden="true" />
      <span>{{ t('panel.workspacePanels') }}</span>
    </button>
    <aside v-if="open" class="workspace-panel-switcher" role="dialog" :aria-label="t('panel.workspacePanels')">
      <div class="workspace-panel-switcher__heading">{{ t('panel.workspacePanels') }}</div>
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
        <p v-if="!props.debugModeAvailable" class="workspace-panel-switcher__hint">{{ t('panel.debugModeUnavailable') }}</p>
      </template>
      <div class="workspace-panel-switcher__section-label">{{ t('panel.visiblePanels') }}</div>
      <button v-for="item in props.items" :key="item.id" type="button" role="checkbox" class="workspace-panel-switcher__item" :class="{ active: item.visible, required: item.required }" :disabled="!item.available || item.required" :aria-checked="item.visible" @click="emit('toggle', item.id)">
        <component :is="item.icon" :size="14" />
        <span>{{ item.label }}</span>
        <i class="workspace-panel-switcher__check" :class="{ on: item.visible }" aria-hidden="true"><Check v-if="item.visible" :size="12" :stroke-width="3" /></i>
      </button>
      <button type="button" class="workspace-panel-switcher__reset" @click="emit('reset'); open = false"><RotateCcw :size="14" />{{ t('panel.resetLayout') }}</button>
    </aside>
  </div>
</template>
