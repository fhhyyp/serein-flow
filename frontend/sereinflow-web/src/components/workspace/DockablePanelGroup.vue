<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, type Component } from 'vue'
import { ChevronDown, ChevronUp, Grip, Move, PanelBottom, PanelLeft, PanelRight, PanelTop, Plus, X } from 'lucide-vue-next'
import { t } from '../../i18n'
import type {
  DockablePanelGroupState,
  DockPosition,
  WorkspacePanelId,
  WorkspacePanelTab,
  WorkspaceSize,
} from '../../flow/dockableWorkspace'

interface GroupOption {
  id: string
  label: string
}

interface MoveEndEvent {
  panelId: WorkspacePanelId
  targetGroupId?: string
  dockTarget?: DockPosition
  clientX: number
  clientY: number
  moved: boolean
  interaction: 'move' | 'resize'
}

interface DragPreviewEvent {
  dock?: DockPosition
  targetGroupId?: string
  clientX?: number
  clientY?: number
}

const props = withDefaults(defineProps<{
  group: DockablePanelGroupState
  tabs: WorkspacePanelTab[]
  workspaceSize: WorkspaceSize
  groupOptions?: GroupOption[]
  canvasGroup?: boolean
  dockedOffset?: number
  dockedSize?: number
  layoutPriority?: 'full-row' | 'center-column'
  canvasInsets?: { top: number; right: number; bottom: number; left: number }
  dropTargeted?: boolean
}>(), {
  groupOptions: () => [],
  canvasGroup: false,
  dockedOffset: 0,
  layoutPriority: 'center-column',
  canvasInsets: () => ({ top: 0, right: 0, bottom: 0, left: 0 }),
  dropTargeted: false,
})

const emit = defineEmits<{
  'update:group': [patch: Partial<DockablePanelGroupState>]
  activate: [groupId: string, panelId: WorkspacePanelId]
  'move-end': [event: MoveEndEvent]
  'drag-preview': [event?: DragPreviewEvent]
  dock: [panelId: WorkspacePanelId, dock: DockPosition]
  combine: [panelId: WorkspacePanelId, targetGroupId: string]
  detach: [panelId: WorkspacePanelId]
  close: [panelId: WorkspacePanelId]
}>()

const root = ref<HTMLElement>()
const menuButton = ref<HTMLButtonElement>()
const menu = ref<HTMLElement>()
const menuOpen = ref(false)
const menuStyle = ref<Record<string, string>>({})
const resizing = ref(false)
const moving = ref(false)
const activeTab = computed(() => props.tabs.find((tab) => tab.id === props.group.activePanelId) ?? props.tabs[0])
const currentDock = computed(() => props.group.dock)
const canClose = computed(() => activeTab.value?.closable !== false)
const groupStyle = computed<Record<string, string | number>>(() => {
  const group = props.group
  const width = Math.max(240, Math.min(group.width, props.workspaceSize.width))
  const height = Math.max(160, Math.min(group.height, props.workspaceSize.height))
  const effectiveHeight = group.collapsed ? 76 : height
  const fullRowPriority = props.layoutPriority === 'full-row'
  if (group.dock === 'fill') {
    return {
      top: `${props.canvasInsets.top}px`,
      right: `${props.canvasInsets.right}px`,
      bottom: `${props.canvasInsets.bottom}px`,
      left: `${props.canvasInsets.left}px`,
      zIndex: group.zIndex,
    }
  }

  if (group.dock === 'left' || group.dock === 'right') {
    const effectiveWidth = group.collapsed ? 76 : width
    return {
      top: fullRowPriority ? `${props.canvasInsets.top}px` : '0px',
      bottom: fullRowPriority ? `${props.canvasInsets.bottom}px` : '0px',
      left: group.dock === 'left' ? `${props.dockedOffset}px` : 'auto',
      right: group.dock === 'right' ? `${props.dockedOffset}px` : 'auto',
      width: `${props.dockedSize ?? effectiveWidth}px`,
      height: 'auto',
      zIndex: group.zIndex,
    }
  }

  if (group.dock === 'top' || group.dock === 'bottom') {
    return {
      top: group.dock === 'top' ? `${props.dockedOffset}px` : 'auto',
      right: '0px',
      bottom: group.dock === 'bottom' ? `${props.dockedOffset}px` : 'auto',
      left: '0px',
      width: 'auto',
      height: `${props.dockedSize ?? (group.collapsed ? 76 : height)}px`,
      zIndex: group.zIndex,
    }
  }

  const style: Record<string, string | number> = {
    left: `${group.x}px`,
    top: `${group.y}px`,
    width: `${width}px`,
    height: `${effectiveHeight}px`,
    zIndex: group.zIndex,
  }
  return style
})

const dockOptions: Array<{ value: DockPosition; labelKey: string; icon: Component }> = [
  { value: 'free', labelKey: 'panel.freePosition', icon: Move },
  { value: 'left', labelKey: 'panel.dockLeft', icon: PanelLeft },
  { value: 'right', labelKey: 'panel.dockRight', icon: PanelRight },
  { value: 'top', labelKey: 'panel.dockTop', icon: PanelTop },
  { value: 'bottom', labelKey: 'panel.dockBottom', icon: PanelBottom },
]

function workspaceRect(): DOMRect | undefined {
  return root.value?.parentElement?.getBoundingClientRect()
}

function edgeDockAtPoint(clientX: number, clientY: number): DockPosition | undefined {
  const bounds = workspaceRect()
  if (!bounds) return undefined
  const distances: Array<{ dock: DockPosition; distance: number }> = [
    { dock: 'left', distance: clientX - bounds.left },
    { dock: 'right', distance: bounds.right - clientX },
    { dock: 'top', distance: clientY - bounds.top },
    { dock: 'bottom', distance: bounds.bottom - clientY },
  ]
  const nearest = distances.sort((a, b) => a.distance - b.distance)[0]
  return nearest && nearest.distance <= 52 ? nearest.dock : undefined
}

function panelRect(): DOMRect | undefined {
  return root.value?.getBoundingClientRect()
}

function targetGroupAtPoint(clientX: number, clientY: number): string | undefined {
  if (typeof document === 'undefined') return undefined
  const candidates = Array.from(document.querySelectorAll<HTMLElement>('[data-dock-group]'))
  return candidates
    .filter((element) => element.getAttribute('data-dock-group') !== props.group.id)
    .sort((a, b) => Number(b.style.zIndex || 0) - Number(a.style.zIndex || 0))
    .find((element) => {
      const bounds = element.getBoundingClientRect()
      const inside = clientX >= bounds.left && clientX <= bounds.right && clientY >= bounds.top && clientY <= bounds.bottom
      if (!inside) return false
      if (!element.classList.contains('dock-panel--canvas')) return true
      const chrome = element.querySelector<HTMLElement>('.dock-panel__chrome')?.getBoundingClientRect()
      return Boolean(chrome
        && clientX >= chrome.left && clientX <= chrome.right
        && clientY >= chrome.top && clientY <= chrome.bottom)
    })?.getAttribute('data-dock-group') ?? undefined
}

function beginMove(event: PointerEvent, panelId = activeTab.value?.id): void {
  if (event.button !== 0 || !panelId) return
  const workspace = workspaceRect()
  const panel = panelRect()
  if (!workspace || !panel) return
  event.preventDefault()
  menuOpen.value = false
  moving.value = true
  emit('activate', props.group.id, panelId)
  const startX = panel.left - workspace.left
  const startY = panel.top - workspace.top
  const startPointerX = event.clientX
  const startPointerY = event.clientY
  let moved = false

  const onMove = (moveEvent: PointerEvent) => {
    const deltaX = moveEvent.clientX - startPointerX
    const deltaY = moveEvent.clientY - startPointerY
    if (Math.abs(deltaX) > 3 || Math.abs(deltaY) > 3) moved = true
    if (!moved) return
    const width = panel.width
    const height = panel.height
    if (props.group.dock !== 'free') {
      emit('update:group', { dock: 'free', x: startX, y: startY, width, height })
    }
    emit('update:group', {
      dock: 'free',
      x: Math.min(Math.max(0, startX + deltaX), Math.max(0, props.workspaceSize.width - width)),
      y: Math.min(Math.max(0, startY + deltaY), Math.max(0, props.workspaceSize.height - height)),
    })
    emit('drag-preview', {
      dock: edgeDockAtPoint(moveEvent.clientX, moveEvent.clientY),
      targetGroupId: targetGroupAtPoint(moveEvent.clientX, moveEvent.clientY),
      clientX: moveEvent.clientX,
      clientY: moveEvent.clientY,
    })
  }
  const onUp = (upEvent?: PointerEvent) => {
    moving.value = false
    window.removeEventListener('pointermove', onMove)
    window.removeEventListener('pointerup', onUp)
    if (stopInteraction === onUp) stopInteraction = undefined
    if (!upEvent) return
    emit('drag-preview', {
      dock: moved ? edgeDockAtPoint(upEvent.clientX, upEvent.clientY) : undefined,
      targetGroupId: moved ? targetGroupAtPoint(upEvent.clientX, upEvent.clientY) : undefined,
      clientX: upEvent.clientX,
      clientY: upEvent.clientY,
    })
    emit('move-end', {
      panelId,
      targetGroupId: moved ? targetGroupAtPoint(upEvent.clientX, upEvent.clientY) : undefined,
      dockTarget: moved ? edgeDockAtPoint(upEvent.clientX, upEvent.clientY) : undefined,
      clientX: upEvent.clientX,
      clientY: upEvent.clientY,
      moved,
      interaction: 'move',
    })
    emit('drag-preview')
  }
  stopInteraction?.()
  stopInteraction = onUp
  window.addEventListener('pointermove', onMove)
  window.addEventListener('pointerup', onUp)
}

function beginResize(event: PointerEvent, direction: string): void {
  if (event.button !== 0 || props.group.dock === 'fill') return
  const workspace = workspaceRect()
  const panel = panelRect()
  if (!workspace || !panel) return
  event.preventDefault()
  event.stopPropagation()
  menuOpen.value = false
  resizing.value = true
  const startPointerX = event.clientX
  const startPointerY = event.clientY
  const startX = panel.left - workspace.left
  const startY = panel.top - workspace.top
  const startWidth = panel.width
  const startHeight = panel.height
  const startDock = props.group.dock
  const edgeDocked = startDock === 'left' || startDock === 'right' || startDock === 'top' || startDock === 'bottom'
  const minimumWidth = 240
  const minimumHeight = 160
  if (!edgeDocked) {
    emit('update:group', { dock: 'free', x: startX, y: startY, width: startWidth, height: startHeight })
  }

  const onMove = (moveEvent: PointerEvent) => {
    const deltaX = moveEvent.clientX - startPointerX
    const deltaY = moveEvent.clientY - startPointerY
    const isWest = direction.includes('w')
    const isNorth = direction.includes('n')
    const isEast = direction.includes('e')
    const isSouth = direction.includes('s')
    let nextX = startX
    let nextY = startY
    let nextWidth = startWidth
    let nextHeight = startHeight
    if (edgeDocked) {
      if (startDock === 'left') {
        if (isEast) nextWidth = startWidth + deltaX
        if (isWest) nextWidth = startWidth - deltaX
      } else if (startDock === 'right') {
        if (isWest) nextWidth = startWidth - deltaX
        if (isEast) nextWidth = startWidth + deltaX
      } else if (startDock === 'top') {
        if (isSouth) nextHeight = startHeight + deltaY
        if (isNorth) nextHeight = startHeight - deltaY
      } else if (startDock === 'bottom') {
        if (isNorth) nextHeight = startHeight - deltaY
        if (isSouth) nextHeight = startHeight + deltaY
      }
    } else {
      if (isEast) nextWidth = startWidth + deltaX
      if (isSouth) nextHeight = startHeight + deltaY
      if (isWest) {
        nextWidth = startWidth - deltaX
        nextX = startX + deltaX
      }
      if (isNorth) {
        nextHeight = startHeight - deltaY
        nextY = startY + deltaY
      }
    }
    nextWidth = Math.min(Math.max(minimumWidth, nextWidth), props.workspaceSize.width)
    nextHeight = Math.min(Math.max(minimumHeight, nextHeight), props.workspaceSize.height)
    if (!edgeDocked) {
      nextX = Math.min(Math.max(0, nextX), Math.max(0, props.workspaceSize.width - nextWidth))
      nextY = Math.min(Math.max(0, nextY), Math.max(0, props.workspaceSize.height - nextHeight))
    }
    emit('update:group', { dock: edgeDocked ? startDock : 'free', x: nextX, y: nextY, width: nextWidth, height: nextHeight })
  }
  const onUp = (upEvent?: PointerEvent) => {
    resizing.value = false
    window.removeEventListener('pointermove', onMove)
    window.removeEventListener('pointerup', onUp)
    if (stopInteraction === onUp) stopInteraction = undefined
    if (!upEvent) return
    emit('move-end', {
      panelId: activeTab.value?.id ?? 'canvas',
      dockTarget: edgeDocked ? startDock : undefined,
      clientX: upEvent.clientX,
      clientY: upEvent.clientY,
      moved: true,
      interaction: 'resize',
    })
  }
  stopInteraction?.()
  stopInteraction = onUp
  window.addEventListener('pointermove', onMove)
  window.addEventListener('pointerup', onUp)
}

let stopInteraction: ((event?: PointerEvent) => void) | undefined

function selectTab(panelId: WorkspacePanelId): void {
  emit('activate', props.group.id, panelId)
}

function chooseDock(dock: DockPosition): void {
  if (!activeTab.value) return
  menuOpen.value = false
  emit('dock', activeTab.value.id, dock)
}

function combineInto(groupId: string): void {
  if (!activeTab.value) return
  menuOpen.value = false
  emit('combine', activeTab.value.id, groupId)
}

function detach(): void {
  if (!activeTab.value) return
  menuOpen.value = false
  emit('detach', activeTab.value.id)
}

function closeActive(): void {
  if (!activeTab.value || !canClose.value) return
  menuOpen.value = false
  emit('close', activeTab.value.id)
}

function toggleMenu(): void {
  menuOpen.value = !menuOpen.value
  if (menuOpen.value) {
    focusGroup()
    void nextTick(updateMenuPosition)
  }
}

function updateMenuPosition(): void {
  if (!menuOpen.value || !menuButton.value || typeof window === 'undefined') return
  const anchor = menuButton.value.getBoundingClientRect()
  const menuWidth = Math.min(240, Math.max(190, menu.value?.getBoundingClientRect().width ?? 208))
  const viewportPadding = 8
  const left = Math.min(
    Math.max(viewportPadding, anchor.right - menuWidth),
    Math.max(viewportPadding, window.innerWidth - menuWidth - viewportPadding),
  )
  const menuHeight = menu.value?.getBoundingClientRect().height ?? 0
  const opensBelow = anchor.bottom + 6 + menuHeight <= window.innerHeight - viewportPadding || anchor.top < 160
  const top = opensBelow
    ? anchor.bottom + 6
    : Math.max(viewportPadding, anchor.top - menuHeight - 6)
  menuStyle.value = {
    left: `${left}px`,
    top: `${top}px`,
    maxHeight: `${Math.max(160, window.innerHeight - viewportPadding * 2)}px`,
  }
}

function handleViewportChange(): void {
  if (menuOpen.value) updateMenuPosition()
}

function toggleCollapsed(): void {
  emit('update:group', { collapsed: !props.group.collapsed })
}

function focusGroup(): void {
  emit('activate', props.group.id, activeTab.value?.id ?? 'canvas')
}

if (typeof window !== 'undefined') {
  window.addEventListener('resize', handleViewportChange)
  window.addEventListener('scroll', handleViewportChange, true)
}

onBeforeUnmount(() => {
  stopInteraction?.()
  if (typeof window !== 'undefined') {
    window.removeEventListener('resize', handleViewportChange)
    window.removeEventListener('scroll', handleViewportChange, true)
  }
})
</script>

<template>
  <section
    ref="root"
    class="dock-panel"
    :class="{ 'dock-panel--dragging': moving, 'dock-panel--resizing': resizing, 'dock-panel--collapsed': props.group.collapsed, 'dock-panel--canvas': props.canvasGroup, 'dock-panel--docked': props.group.dock !== 'free' && props.group.dock !== 'fill', 'dock-panel--menu-open': menuOpen, 'dock-panel--drop-targeted': props.dropTargeted }"
    :style="groupStyle"
    :data-dock-group="props.group.id"
    @pointerdown="focusGroup"
  >
    <header class="dock-panel__chrome">
      <div class="dock-panel__titlebar" @pointerdown="beginMove($event)">
        <Grip class="dock-panel__grip" :size="14" aria-hidden="true" />
        <component :is="activeTab?.icon" :size="15" aria-hidden="true" />
        <strong>{{ activeTab?.label }}</strong>
        <span v-if="props.group.dock !== 'free' && props.group.dock !== 'fill'" class="dock-panel__dock-state">{{ t(`panel.dock.${props.group.dock}`) }}</span>
      </div>
      <div class="dock-panel__actions" @pointerdown.stop>
        <button v-if="props.group.collapsed" class="dock-panel__button" type="button" :title="t('panel.expand')" :aria-label="t('panel.expand')" @click="toggleCollapsed"><ChevronDown :size="15" /></button>
        <button v-else class="dock-panel__button" type="button" :title="t('panel.collapse')" :aria-label="t('panel.collapse')" @click="toggleCollapsed"><ChevronUp :size="15" /></button>
        <button ref="menuButton" class="dock-panel__button" type="button" :title="t('panel.layout')" :aria-label="t('panel.layout')" :aria-expanded="menuOpen" @click="toggleMenu"><Plus :size="15" /></button>
        <Teleport to="body">
          <div v-if="menuOpen" ref="menu" class="dock-panel__menu" :style="menuStyle" role="menu" @pointerdown.stop>
            <span>{{ t('panel.layout') }}</span>
            <button v-for="option in dockOptions" :key="option.value" type="button" role="menuitem" :class="{ active: currentDock === option.value }" @click="chooseDock(option.value)"><component :is="option.icon" :size="14" />{{ t(option.labelKey) }}</button>
            <button v-if="props.tabs.length > 1" type="button" role="menuitem" @click="detach"><Move :size="14" />{{ t('panel.detach') }}</button>
            <template v-if="props.groupOptions.length">
              <span>{{ t('panel.combineInto') }}</span>
              <button v-for="option in props.groupOptions" :key="option.id" type="button" role="menuitem" @click="combineInto(option.id)"><Plus :size="14" />{{ option.label }}</button>
            </template>
            <button v-if="canClose" class="dock-panel__menu-danger" type="button" role="menuitem" @click="closeActive"><X :size="14" />{{ t('panel.close') }}</button>
          </div>
        </Teleport>
        <button v-if="canClose" class="dock-panel__button dock-panel__button--close" type="button" :title="t('panel.close')" :aria-label="t('panel.close')" @click="closeActive"><X :size="15" /></button>
      </div>
      <nav v-if="props.tabs.length > 1" class="dock-panel__tabs" role="tablist" :aria-label="t('panel.tabs')">
        <button v-for="tab in props.tabs" :key="tab.id" type="button" role="tab" :aria-selected="tab.id === activeTab?.id" :class="{ active: tab.id === activeTab?.id }" @pointerdown.stop="beginMove($event, tab.id)" @click="selectTab(tab.id)"><component :is="tab.icon" :size="13" /><span>{{ tab.label }}</span></button>
      </nav>
    </header>
    <div v-if="!props.group.collapsed" class="dock-panel__body"><slot :panel-id="activeTab?.id"></slot></div>
    <div v-if="props.group.dock !== 'fill' && !props.group.collapsed" class="dock-panel__resize-grips" aria-hidden="true">
      <span v-for="direction in ['n', 'ne', 'e', 'se', 's', 'sw', 'w', 'nw']" :key="direction" class="dock-panel__resize-handle" :class="`dock-panel__resize-handle--${direction}`" @pointerdown="beginResize($event, direction)"></span>
    </div>
  </section>
</template>
