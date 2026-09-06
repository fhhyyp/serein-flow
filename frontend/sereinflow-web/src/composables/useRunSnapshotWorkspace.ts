import { computed, onBeforeUnmount, onMounted, reactive, type Ref, watch } from 'vue'
import {
  createSnapshotDockableWorkspaceLayout,
  normalizeDockableWorkspaceLayout,
  type DockablePanelGroupState,
  type DockableWorkspaceLayout,
  type DockPosition,
  type WorkspacePanelId,
  type WorkspaceSize,
} from '../flow/dockableWorkspace'
import { reflowDockableResize } from '../flow/dockableResize'

const storageKey = 'sereinflow.run-snapshot-dock-layout.v1'

type EdgeDockPosition = Exclude<DockPosition, 'free' | 'fill'>

function cloneLayout(layout: DockableWorkspaceLayout): DockableWorkspaceLayout {
  return JSON.parse(JSON.stringify(layout)) as DockableWorkspaceLayout
}

function isEdgeDockPosition(dock: DockPosition): dock is EdgeDockPosition {
  return dock === 'left' || dock === 'right' || dock === 'top' || dock === 'bottom'
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), Math.max(min, max))
}

function loadLayout(size: WorkspaceSize): { layout: DockableWorkspaceLayout; stored: boolean } {
  const fallback = createSnapshotDockableWorkspaceLayout(size)
  if (typeof window === 'undefined') return { layout: fallback, stored: false }

  try {
    const raw = window.localStorage.getItem(storageKey)
    if (!raw) return { layout: fallback, stored: false }
    const stored = normalizeDockableWorkspaceLayout(JSON.parse(raw) as unknown, size)
    return stored ? { layout: stored, stored: true } : { layout: fallback, stored: false }
  } catch {
    return { layout: fallback, stored: false }
  }
}

export function useRunSnapshotWorkspace(workspaceElement: Ref<HTMLElement | undefined>) {
  const workspaceSize = reactive<WorkspaceSize>({ width: 1_920, height: 980 })
  const initial = loadLayout(workspaceSize)
  const layout = reactive<DockableWorkspaceLayout>(initial.layout)
  let hasMeasuredWorkspace = false
  const groups = computed(() => layout.groups)
  const visibleGroups = computed(() => layout.groups.filter((group) =>
    group.panelIds.some((panelId) => layout.panels[panelId].visible)))
  const renderedEdgeGroups = computed(() => visibleGroups.value.filter((group) => isEdgeDockPosition(group.dock)))
  let resizeObserver: ResizeObserver | undefined
  let fallbackResizeHandler: (() => void) | undefined
  let saveTimer: number | undefined

  function scheduleSave(): void {
    if (typeof window === 'undefined') return
    if (saveTimer !== undefined) window.clearTimeout(saveTimer)
    saveTimer = window.setTimeout(() => {
      saveTimer = undefined
      try {
        window.localStorage.setItem(storageKey, JSON.stringify(cloneLayout(layout)))
      } catch {
        // The layout is a preference and must never block a snapshot session.
      }
    }, 120)
  }

  function updateGroup(groupId: string, patch: Partial<DockablePanelGroupState>): void {
    const group = layout.groups.find((item) => item.id === groupId)
    if (!group) return
    const previous = {
      x: group.x,
      y: group.y,
      width: group.width,
      height: group.height,
      dock: group.dock,
    }
    Object.assign(group, patch)
    const visibleGroupIds = new Set(layout.groups
      .filter((candidate) => candidate.panelIds.some((panelId) => layout.panels[panelId].visible))
      .map((candidate) => candidate.id))
    reflowDockableResize(layout.groups, groupId, previous, group, visibleGroupIds, workspaceSize)
    scheduleSave()
  }

  function groupForPanel(panelId: WorkspacePanelId): DockablePanelGroupState | undefined {
    const groupId = layout.panels[panelId].groupId
    return layout.groups.find((group) => group.id === groupId)
      ?? layout.groups.find((group) => group.panelIds.includes(panelId))
  }

  function focusGroup(groupId: string): void {
    const maxZ = Math.max(...layout.groups.map((group) => group.zIndex), 1)
    updateGroup(groupId, { zIndex: maxZ + 1 })
  }

  function activatePanel(groupId: string, panelId: WorkspacePanelId): void {
    const group = layout.groups.find((item) => item.id === groupId)
    if (!group || !group.panelIds.includes(panelId)) return
    updateGroup(groupId, { activePanelId: panelId })
    focusGroup(groupId)
  }

  function setPanelVisible(panelId: WorkspacePanelId, visible: boolean): void {
    if (panelId === 'canvas' && !visible) return
    layout.panels[panelId].visible = visible
    const group = groupForPanel(panelId)
    if (group) {
      const visibleTab = group.panelIds.find((id) => layout.panels[id].visible)
      if (visibleTab) group.activePanelId = visibleTab
      focusGroup(group.id)
    }
    scheduleSave()
  }

  function showPanel(panelId: WorkspacePanelId): void {
    setPanelVisible(panelId, true)
    const group = groupForPanel(panelId)
    if (group) activatePanel(group.id, panelId)
  }

  function hidePanel(panelId: WorkspacePanelId): void {
    setPanelVisible(panelId, false)
  }

  function dockGroup(groupId: string, dock: DockPosition): void {
    const group = layout.groups.find((item) => item.id === groupId)
    if (!group) return
    updateGroup(groupId, { dock })
    focusGroup(groupId)
  }

  function combinePanel(panelId: WorkspacePanelId, targetGroupId: string): void {
    const sourceGroup = groupForPanel(panelId)
    const targetGroup = layout.groups.find((item) => item.id === targetGroupId)
    if (!sourceGroup || !targetGroup || sourceGroup.id === targetGroup.id) return

    sourceGroup.panelIds = sourceGroup.panelIds.filter((id) => id !== panelId)
    if (!targetGroup.panelIds.includes(panelId)) targetGroup.panelIds.push(panelId)
    layout.panels[panelId].groupId = targetGroup.id
    targetGroup.activePanelId = panelId
    if (panelId === 'canvas') targetGroup.dock = 'fill'
    focusGroup(targetGroup.id)
    if (sourceGroup.panelIds.length === 0) {
      layout.groups = layout.groups.filter((group) => group.id !== sourceGroup.id)
    } else if (!sourceGroup.panelIds.includes(sourceGroup.activePanelId)) {
      sourceGroup.activePanelId = sourceGroup.panelIds.find((id) => layout.panels[id].visible) ?? sourceGroup.panelIds[0]!
    }
    scheduleSave()
  }

  function detachPanel(panelId: WorkspacePanelId): string | undefined {
    const sourceGroup = groupForPanel(panelId)
    if (!sourceGroup || sourceGroup.panelIds.length <= 1) return sourceGroup?.id

    const groupId = `snapshot-${panelId}-${layout.nextGroupNumber++}`
    const detachedWidth = clamp(sourceGroup.width, 240, Math.max(240, workspaceSize.width - 24))
    const detachedHeight = clamp(sourceGroup.height, 160, Math.max(160, workspaceSize.height - 24))
    const newGroup: DockablePanelGroupState = {
      ...sourceGroup,
      id: groupId,
      x: clamp(sourceGroup.x + 24, 12, Math.max(12, workspaceSize.width - detachedWidth - 12)),
      y: clamp(sourceGroup.y + 24, 12, Math.max(12, workspaceSize.height - detachedHeight - 12)),
      width: detachedWidth,
      height: detachedHeight,
      panelIds: [panelId],
      activePanelId: panelId,
      dock: 'free',
      zIndex: Math.max(...layout.groups.map((group) => group.zIndex), 1) + 1,
    }
    sourceGroup.panelIds = sourceGroup.panelIds.filter((id) => id !== panelId)
    if (!sourceGroup.panelIds.includes(sourceGroup.activePanelId)) {
      sourceGroup.activePanelId = sourceGroup.panelIds.find((id) => layout.panels[id].visible) ?? sourceGroup.panelIds[0]!
    }
    layout.groups.push(newGroup)
    layout.panels[panelId].groupId = groupId
    scheduleSave()
    return groupId
  }

  function resetLayout(): void {
    const next = createSnapshotDockableWorkspaceLayout(workspaceSize)
    layout.groups = next.groups
    layout.panels = next.panels
    layout.nextGroupNumber = next.nextGroupNumber
    scheduleSave()
  }

  function clampGroupsToWorkspace(): void {
    for (const group of layout.groups) {
      if (group.dock === 'fill') continue
      group.width = clamp(group.width, 240, Math.max(240, workspaceSize.width - 16))
      group.height = clamp(group.height, 160, Math.max(160, workspaceSize.height - 16))
      group.x = clamp(group.x, 0, Math.max(0, workspaceSize.width - group.width))
      group.y = clamp(group.y, 0, Math.max(0, workspaceSize.height - group.height))
    }
  }

  function desiredEdgeGroupExtent(group: DockablePanelGroupState): number {
    if (group.collapsed) return 76
    const raw = group.dock === 'left' || group.dock === 'right' ? group.width : group.height
    const maximum = group.dock === 'left' || group.dock === 'right' ? workspaceSize.width : workspaceSize.height
    return Math.max(0, Math.min(raw, maximum))
  }

  function dockedExtentFor(group: DockablePanelGroupState): number | undefined {
    if (!isEdgeDockPosition(group.dock)) return undefined
    const peers = renderedEdgeGroups.value.filter((candidate) => candidate.dock === group.dock)
    const total = peers.reduce((sum, candidate) => sum + desiredEdgeGroupExtent(candidate), 0)
    const capacity = group.dock === 'left' || group.dock === 'right' ? workspaceSize.width : workspaceSize.height
    const scale = total > capacity && total > 0 ? capacity / total : 1
    return desiredEdgeGroupExtent(group) * scale
  }

  function dockedOffsetFor(group: DockablePanelGroupState): number {
    if (!isEdgeDockPosition(group.dock)) return 0
    const peers = renderedEdgeGroups.value.filter((candidate) => candidate.dock === group.dock)
    const groupIndex = peers.findIndex((candidate) => candidate.id === group.id)
    return peers.slice(0, Math.max(0, groupIndex)).reduce(
      (offset, candidate) => offset + (dockedExtentFor(candidate) ?? 0),
      0,
    )
  }

  const layoutPriority = computed<'full-row' | 'center-column'>(() =>
    renderedEdgeGroups.value.some((group) => group.dock === 'top' || group.dock === 'bottom')
      ? 'full-row'
      : 'center-column')

  const canvasInsets = computed(() => renderedEdgeGroups.value.reduce((insets, group) => {
    const extent = dockedExtentFor(group) ?? 0
    if (group.dock === 'left') insets.left += extent
    if (group.dock === 'right') insets.right += extent
    if (group.dock === 'top') insets.top += extent
    if (group.dock === 'bottom') insets.bottom += extent
    return insets
  }, { top: 0, right: 0, bottom: 0, left: 0 }))

  function updateWorkspaceSize(element: HTMLElement): void {
    workspaceSize.width = Math.max(1, element.clientWidth)
    workspaceSize.height = Math.max(1, element.clientHeight)
    if (!hasMeasuredWorkspace && !initial.stored) {
      const next = createSnapshotDockableWorkspaceLayout(workspaceSize)
      layout.groups = next.groups
      layout.panels = next.panels
      layout.nextGroupNumber = next.nextGroupNumber
    }
    hasMeasuredWorkspace = true
    clampGroupsToWorkspace()
  }

  function observeWorkspace(element: HTMLElement): void {
    resizeObserver?.disconnect()
    if (fallbackResizeHandler) window.removeEventListener('resize', fallbackResizeHandler)
    updateWorkspaceSize(element)
    if (typeof ResizeObserver !== 'undefined') {
      resizeObserver = new ResizeObserver(() => updateWorkspaceSize(element))
      resizeObserver.observe(element)
    } else {
      fallbackResizeHandler = () => updateWorkspaceSize(element)
      window.addEventListener('resize', fallbackResizeHandler)
    }
  }

  watch(layout, scheduleSave, { deep: true })
  watch(workspaceElement, (element) => {
    if (element) observeWorkspace(element)
  }, { flush: 'post' })

  onMounted(() => {
    if (workspaceElement.value) observeWorkspace(workspaceElement.value)
  })

  onBeforeUnmount(() => {
    resizeObserver?.disconnect()
    if (fallbackResizeHandler && typeof window !== 'undefined') window.removeEventListener('resize', fallbackResizeHandler)
    if (saveTimer !== undefined && typeof window !== 'undefined') window.clearTimeout(saveTimer)
  })

  return {
    layout,
    groups,
    visibleGroups,
    workspaceSize,
    renderedEdgeGroups,
    layoutPriority,
    canvasInsets,
    dockedExtentFor,
    dockedOffsetFor,
    updateGroup,
    groupForPanel,
    activatePanel,
    showPanel,
    hidePanel,
    dockGroup,
    combinePanel,
    detachPanel,
    focusGroup,
    resetLayout,
  }
}
