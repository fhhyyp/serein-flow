import { computed, onBeforeUnmount, onMounted, reactive, ref, type Ref, watch } from 'vue'
import {
  createDefaultDockableWorkspaceLayout,
  createDebugDockableWorkspaceLayout,
  fitDebugDockableWorkspaceLayout,
  resizeFreeDockableGroups,
  normalizeDockableWorkspaceLayout,
  type DockablePanelGroupState,
  type DockableWorkspaceMode,
  type DockableWorkspaceLayout,
  type DockPosition,
  type WorkspacePanelId,
  type WorkspaceSize,
  type WorkspaceInsets,
} from '../flow/dockableWorkspace'
import { reflowDockableResize } from '../flow/dockableResize'
import { splitDockRect, type DockRect, type DockSplitZone } from '../flow/dockableSplit'
import { snapFreePanelMove, snapFreePanelResize } from '../flow/dockableSnap'

const storageKey = 'sereinflow.canvas-dock-layout.v3'
const legacyStorageKey = 'sereinflow.canvas-dock-layout.v2'
const storageVersion = 3

type WorkspaceModeLayouts = Record<DockableWorkspaceMode, DockableWorkspaceLayout>
type WorkspaceModeGeometry = Record<DockableWorkspaceMode, boolean>

interface LoadedModeLayouts {
  layouts: WorkspaceModeLayouts
  geometryCustomized: WorkspaceModeGeometry
  savedWorkspaceSize?: WorkspaceSize
}

function createModeLayout(mode: DockableWorkspaceMode, size: WorkspaceSize): DockableWorkspaceLayout {
  return mode === 'debug'
    ? createDebugDockableWorkspaceLayout(size)
    : createDefaultDockableWorkspaceLayout(size)
}

function migrateEditLayoutForCollapsedDiagnostics(layout: DockableWorkspaceLayout): DockableWorkspaceLayout {
  layout.panels.output.visible = false
  layout.panels.diagnostics.visible = true
  const diagnosticsGroup = layout.groups.find((group) => group.id === layout.panels.diagnostics.groupId)
    ?? layout.groups.find((group) => group.panelIds.includes('diagnostics'))
  if (diagnosticsGroup) {
    diagnosticsGroup.dock = 'bottom'
    diagnosticsGroup.collapsed = true
    diagnosticsGroup.activePanelId = 'diagnostics'
  }
  return layout
}

function loadModeLayouts(size: WorkspaceSize): LoadedModeLayouts {
  const defaults: WorkspaceModeLayouts = {
    edit: createModeLayout('edit', size),
    debug: createModeLayout('debug', size),
  }
  const defaultGeometryCustomized: WorkspaceModeGeometry = { edit: false, debug: false }
  if (typeof window === 'undefined') return { layouts: defaults, geometryCustomized: defaultGeometryCustomized }

  try {
    const raw = window.localStorage.getItem(storageKey)
    if (raw) {
      const stored = JSON.parse(raw) as {
        storageVersion?: number
        layouts?: Partial<Record<DockableWorkspaceMode, unknown>>
        geometryCustomized?: Partial<WorkspaceModeGeometry>
        workspaceSize?: Partial<WorkspaceSize>
      }
      if ((stored.storageVersion === 1 || stored.storageVersion === 2 || stored.storageVersion === storageVersion) && stored.layouts) {
        const editLayout = normalizeDockableWorkspaceLayout(stored.layouts.edit, size, defaults.edit) ?? defaults.edit
        const debugLayout = stored.storageVersion === storageVersion
          ? normalizeDockableWorkspaceLayout(stored.layouts.debug, size, defaults.debug) ?? defaults.debug
          : defaults.debug
        return {
          layouts: {
            edit: stored.storageVersion === 1
              ? migrateEditLayoutForCollapsedDiagnostics(editLayout)
              : editLayout,
            // Version 3 is the first cache version with the corrected 50/50 debug split.
            debug: debugLayout,
          },
          geometryCustomized: stored.storageVersion === storageVersion
            ? {
              edit: stored.geometryCustomized?.edit === true,
              debug: stored.geometryCustomized?.debug === true,
            }
            : {
              edit: true,
              debug: false,
            },
          savedWorkspaceSize: Number.isFinite(stored.workspaceSize?.width) && Number.isFinite(stored.workspaceSize?.height)
            && (stored.workspaceSize?.width ?? 0) > 0 && (stored.workspaceSize?.height ?? 0) > 0
            ? { width: stored.workspaceSize!.width!, height: stored.workspaceSize!.height! }
            : undefined,
        }
      }
    }

    // Keep the user's pre-display-mode layout as the edit-mode starting point.
    const legacyRaw = window.localStorage.getItem(legacyStorageKey)
    const legacyLayout = legacyRaw
      ? normalizeDockableWorkspaceLayout(JSON.parse(legacyRaw) as unknown, size, defaults.edit)
      : undefined
    if (legacyLayout) {
      return {
        layouts: { edit: migrateEditLayoutForCollapsedDiagnostics(legacyLayout), debug: defaults.debug },
        geometryCustomized: { edit: true, debug: false },
      }
    }
    return { layouts: defaults, geometryCustomized: defaultGeometryCustomized }
  } catch {
    return { layouts: defaults, geometryCustomized: defaultGeometryCustomized }
  }
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), Math.max(min, max))
}

function cloneLayout(layout: DockableWorkspaceLayout): DockableWorkspaceLayout {
  return JSON.parse(JSON.stringify(layout)) as DockableWorkspaceLayout
}

interface LayoutRect {
  left: number
  top: number
  right: number
  bottom: number
}

function rectArea(rect: LayoutRect): number {
  return Math.max(0, rect.right - rect.left) * Math.max(0, rect.bottom - rect.top)
}

function overlapArea(first: LayoutRect, second: LayoutRect): number {
  return Math.max(0, Math.min(first.right, second.right) - Math.max(first.left, second.left))
    * Math.max(0, Math.min(first.bottom, second.bottom) - Math.max(first.top, second.top))
}

export function useDockableWorkspace(workspaceElement: Ref<HTMLElement | undefined>) {
  const workspaceSize = reactive<WorkspaceSize>({ width: 1_920, height: 980 })
  const displayMode = ref<DockableWorkspaceMode>('edit')
  const loadedLayouts = loadModeLayouts(workspaceSize)
  const modeLayouts = reactive<WorkspaceModeLayouts>(loadedLayouts.layouts)
  const geometryCustomized = reactive<WorkspaceModeGeometry>(loadedLayouts.geometryCustomized)
  const layout = reactive<DockableWorkspaceLayout>(cloneLayout(modeLayouts.edit))
  let resizeObserver: ResizeObserver | undefined
  let saveTimer: number | undefined
  let hasMeasuredWorkspace = false

  const groups = computed(() => layout.groups)
  const visibleGroups = computed(() => layout.groups.filter((group) =>
    group.panelIds.some((panelId) => layout.panels[panelId].visible)))
  let fallbackResizeHandler: (() => void) | undefined

  function syncActiveModeLayout(): void {
    modeLayouts[displayMode.value] = cloneLayout(layout)
  }

  function scheduleSave(): void {
    if (typeof window === 'undefined') return
    if (saveTimer !== undefined) window.clearTimeout(saveTimer)
    saveTimer = window.setTimeout(() => {
      saveTimer = undefined
      try {
        syncActiveModeLayout()
        window.localStorage.setItem(storageKey, JSON.stringify({
          storageVersion,
          workspaceSize: { ...workspaceSize },
          geometryCustomized: { ...geometryCustomized },
          layouts: {
            edit: cloneLayout(modeLayouts.edit),
            debug: cloneLayout(modeLayouts.debug),
          },
        }))
      } catch {
        // Layout preferences are optional and must never block editing.
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
    if (previous.dock === 'free' && group.dock === 'free'
      && ('width' in patch || 'height' in patch)) {
      Object.assign(group, snapFreePanelResize(previous, group, occupiedRects(groupId), workspaceSize))
    }
    if ('x' in patch || 'y' in patch || 'width' in patch || 'height' in patch || 'dock' in patch || 'collapsed' in patch) {
      geometryCustomized[displayMode.value] = true
    }
    const visibleGroupIds = new Set(layout.groups
      .filter((candidate) => candidate.panelIds.some((panelId) => layout.panels[panelId].visible))
      .map((candidate) => candidate.id))
    reflowDockableResize(layout.groups, groupId, previous, group, visibleGroupIds, workspaceSize)
    if (displayMode.value === 'debug' && groupId === layout.panels.debug.groupId
      && group.dock === 'right' && 'width' in patch) {
      fitDebugDockableWorkspaceLayout(layout, workspaceSize)
    }
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
    geometryCustomized[displayMode.value] = true
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
    if (group) {
      activatePanel(group.id, panelId)
      if (group.dock === 'free') placeFreeGroup(group)
    }
  }

  function hidePanel(panelId: WorkspacePanelId): void {
    setPanelVisible(panelId, false)
  }

  function setDisplayMode(nextMode: DockableWorkspaceMode): void {
    if (displayMode.value === nextMode) return
    syncActiveModeLayout()
    const nextLayout = geometryCustomized[nextMode]
      ? cloneLayout(modeLayouts[nextMode] ?? createModeLayout(nextMode, workspaceSize))
      : createModeLayout(nextMode, workspaceSize)
    displayMode.value = nextMode
    layout.groups = nextLayout.groups
    layout.panels = nextLayout.panels
    layout.nextGroupNumber = nextLayout.nextGroupNumber
    clampGroupsToWorkspace()
    scheduleSave()
  }

  function dockGroup(groupId: string, dock: DockPosition): void {
    const group = layout.groups.find((item) => item.id === groupId)
    if (!group) return
    updateGroup(groupId, { dock })
    focusGroup(groupId)
  }

  function snapGroup(groupId: string): void {
    const group = layout.groups.find((item) => item.id === groupId)
    if (!group || group.dock !== 'free') return
    Object.assign(group, snapFreePanelMove(group, occupiedRects(groupId), workspaceSize))
    scheduleSave()
  }

  function combinePanel(panelId: WorkspacePanelId, targetGroupId: string): void {
    const sourceGroup = groupForPanel(panelId)
    const targetGroup = layout.groups.find((item) => item.id === targetGroupId)
    if (!sourceGroup || !targetGroup || sourceGroup.id === targetGroup.id) return

    geometryCustomized[displayMode.value] = true

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

  function splitPanel(panelId: WorkspacePanelId, targetGroupId: string, zone: Exclude<DockSplitZone, 'center'>, targetRect: DockRect): void {
    const initialSource = groupForPanel(panelId)
    const targetGroup = layout.groups.find((group) => group.id === targetGroupId)
    if (!initialSource || !targetGroup || (initialSource.id === targetGroup.id && initialSource.panelIds.length <= 1)) return
    const sourceGroupId = initialSource.panelIds.length > 1 ? detachPanel(panelId) : initialSource.id
    const sourceGroup = layout.groups.find((group) => group.id === sourceGroupId)
    if (!sourceGroup) return
    const split = splitDockRect(targetRect, zone)
    const place = (group: DockablePanelGroupState, rect: DockRect) => {
      Object.assign(group, {
        dock: 'free' as const,
        collapsed: false,
        x: rect.left,
        y: rect.top,
        width: rect.right - rect.left,
        height: rect.bottom - rect.top,
      })
    }
    place(targetGroup, split.target)
    place(sourceGroup, split.source)
    geometryCustomized[displayMode.value] = true
    focusGroup(sourceGroup.id)
    scheduleSave()
  }

  function detachPanel(panelId: WorkspacePanelId): string | undefined {
    const sourceGroup = groupForPanel(panelId)
    if (!sourceGroup || sourceGroup.panelIds.length <= 1) return sourceGroup?.id

    geometryCustomized[displayMode.value] = true

    const groupId = `${panelId}-${layout.nextGroupNumber++}`
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
      dock: panelId === 'canvas' ? 'fill' : 'free',
      zIndex: Math.max(...layout.groups.map((group) => group.zIndex), 1) + 1,
    }
    sourceGroup.panelIds = sourceGroup.panelIds.filter((id) => id !== panelId)
    if (!sourceGroup.panelIds.includes(sourceGroup.activePanelId)) {
      sourceGroup.activePanelId = sourceGroup.panelIds.find((id) => layout.panels[id].visible) ?? sourceGroup.panelIds[0]!
    }
    layout.groups.push(newGroup)
    layout.panels[panelId].groupId = groupId
    if (newGroup.dock === 'free') placeFreeGroup(newGroup)
    scheduleSave()
    return groupId
  }

  function resetLayout(): void {
    const next = createModeLayout(displayMode.value, workspaceSize)
    layout.groups = next.groups
    layout.panels = next.panels
    layout.nextGroupNumber = next.nextGroupNumber
    geometryCustomized[displayMode.value] = false
    syncActiveModeLayout()
    scheduleSave()
  }

  function clampGroupsToWorkspace(): void {
    for (const group of layout.groups) {
      if (group.dock === 'fill') continue
      group.width = clamp(group.width, 240, Math.max(240, workspaceSize.width - 16))
      group.height = clamp(group.height, 160, Math.max(160, workspaceSize.height - 16))
      group.x = clamp(group.x, 0, workspaceSize.width - group.width)
      group.y = clamp(group.y, 0, workspaceSize.height - group.height)
    }
  }

  function edgeGroupExtent(group: DockablePanelGroupState, size = workspaceSize): number {
    if (group.collapsed) return 76
    const raw = group.dock === 'left' || group.dock === 'right' ? group.width : group.height
    const maximum = group.dock === 'left' || group.dock === 'right'
      ? size.width
      : size.height
    return Math.max(0, Math.min(raw, maximum))
  }

  function workspaceInsetsForSize(size: WorkspaceSize): WorkspaceInsets {
    const insets = { left: 0, right: 0, top: 0, bottom: 0 }
    const visible = layout.groups.filter((group) => group.panelIds.some((panelId) => layout.panels[panelId].visible))
    for (const dock of ['left', 'right', 'top', 'bottom'] as const) {
      const peers = visible.filter((group) => group.dock === dock)
      const total = peers.reduce((sum, group) => sum + edgeGroupExtent(group, size), 0)
      const capacity = dock === 'left' || dock === 'right' ? size.width : size.height
      insets[dock] = Math.min(total, capacity)
    }
    return insets
  }

  function visibleLayoutGroups(excludeGroupId: string): DockablePanelGroupState[] {
    return layout.groups.filter((group) => group.id !== excludeGroupId
      && group.panelIds.some((panelId) => layout.panels[panelId].visible))
  }

  function occupiedRects(excludeGroupId: string): LayoutRect[] {
    const groups = visibleLayoutGroups(excludeGroupId)
    const edgeGroups = groups.filter((group) => group.dock === 'left'
      || group.dock === 'right'
      || group.dock === 'top'
      || group.dock === 'bottom')
    const fullRowPriority = edgeGroups.some((group) => group.dock === 'top' || group.dock === 'bottom')
    const extents = new Map<string, number>()
    for (const dock of ['left', 'right', 'top', 'bottom'] as const) {
      const peers = edgeGroups.filter((group) => group.dock === dock)
      const total = peers.reduce((sum, group) => sum + edgeGroupExtent(group), 0)
      const capacity = dock === 'left' || dock === 'right' ? workspaceSize.width : workspaceSize.height
      const scale = total > capacity && total > 0 ? capacity / total : 1
      for (const group of peers) extents.set(group.id, edgeGroupExtent(group) * scale)
    }
    const insets = edgeGroups.reduce((result, group) => {
      const extent = extents.get(group.id) ?? 0
      if (group.dock === 'left') result.left += extent
      if (group.dock === 'right') result.right += extent
      if (group.dock === 'top') result.top += extent
      if (group.dock === 'bottom') result.bottom += extent
      return result
    }, { top: 0, right: 0, bottom: 0, left: 0 })

    const rects: LayoutRect[] = []
    for (const group of groups) {
      if (group.dock === 'fill') continue
      if (group.dock === 'free') {
        const width = Math.max(240, Math.min(group.width, workspaceSize.width))
        const height = Math.max(160, Math.min(group.height, workspaceSize.height))
        rects.push({ left: group.x, top: group.y, right: group.x + width, bottom: group.y + height })
        continue
      }
      const extent = extents.get(group.id) ?? 0
      const peers = edgeGroups.filter((candidate) => candidate.dock === group.dock)
      const offset = peers.slice(0, Math.max(0, peers.findIndex((candidate) => candidate.id === group.id)))
        .reduce((sum, candidate) => sum + (extents.get(candidate.id) ?? 0), 0)
      if (group.dock === 'left') rects.push({ left: offset, top: fullRowPriority ? insets.top : 0, right: offset + extent, bottom: workspaceSize.height - (fullRowPriority ? insets.bottom : 0) })
      if (group.dock === 'right') rects.push({ left: workspaceSize.width - offset - extent, top: fullRowPriority ? insets.top : 0, right: workspaceSize.width - offset, bottom: workspaceSize.height - (fullRowPriority ? insets.bottom : 0) })
      if (group.dock === 'top') rects.push({ left: 0, top: offset, right: workspaceSize.width, bottom: offset + extent })
      if (group.dock === 'bottom') rects.push({ left: 0, top: workspaceSize.height - offset - extent, right: workspaceSize.width, bottom: workspaceSize.height - offset })
    }
    return rects
  }

  function placeFreeGroup(group: DockablePanelGroupState): void {
    if (group.dock !== 'free') return
    const width = Math.max(240, Math.min(group.width, workspaceSize.width))
    const height = Math.max(160, Math.min(group.height, workspaceSize.height))
    const current: LayoutRect = { left: group.x, top: group.y, right: group.x + width, bottom: group.y + height }
    const blockers = occupiedRects(group.id)
    const outside = current.left < 0 || current.top < 0
      || current.right > workspaceSize.width || current.bottom > workspaceSize.height
    const crowded = outside || blockers.some((blocker) => overlapArea(current, blocker) > rectArea(current) * .22)
    if (!crowded) return

    const edgeGroups = visibleLayoutGroups(group.id).filter((candidate) => candidate.dock === 'left'
      || candidate.dock === 'right'
      || candidate.dock === 'top'
      || candidate.dock === 'bottom')
    const edgeInset = (dock: 'left' | 'right' | 'top' | 'bottom'): number => {
      const peers = edgeGroups.filter((candidate) => candidate.dock === dock)
      const total = peers.reduce((sum, candidate) => sum + edgeGroupExtent(candidate), 0)
      const capacity = dock === 'left' || dock === 'right' ? workspaceSize.width : workspaceSize.height
      const scale = total > capacity && total > 0 ? capacity / total : 1
      return peers.reduce((sum, candidate) => sum + edgeGroupExtent(candidate) * scale, 0)
    }
    const leftInset = edgeInset('left')
    const rightInset = edgeInset('right')
    const topInset = edgeInset('top')
    const bottomInset = edgeInset('bottom')
    const minLeft = Math.min(Math.max(12, leftInset + 12), Math.max(12, workspaceSize.width - width - 12))
    const minTop = Math.min(Math.max(12, topInset + 12), Math.max(12, workspaceSize.height - height - 12))
    const maxLeft = Math.max(minLeft, workspaceSize.width - rightInset - width - 12)
    const maxTop = Math.max(minTop, workspaceSize.height - bottomInset - height - 12)
    const candidates: Array<{ x: number; y: number; distance: number }> = []
    for (let y = minTop; y <= maxTop; y += 24) {
      for (let x = minLeft; x <= maxLeft; x += 24) {
        candidates.push({ x, y, distance: Math.abs(x - group.x) + Math.abs(y - group.y) })
      }
    }
    candidates.sort((first, second) => first.distance - second.distance)
    const candidate = candidates.find((position) => {
      const rect = { left: position.x, top: position.y, right: position.x + width, bottom: position.y + height }
      return blockers.every((blocker) => overlapArea(rect, blocker) <= 0)
    })
    if (candidate) {
      group.x = candidate.x
      group.y = candidate.y
    }
  }

  function updateWorkspaceSize(element: HTMLElement): void {
    const nextSize = {
      width: Math.max(1, element.clientWidth),
      height: Math.max(1, element.clientHeight),
    }
    const previousSize = !hasMeasuredWorkspace && loadedLayouts.savedWorkspaceSize
      ? loadedLayouts.savedWorkspaceSize
      : { ...workspaceSize }
    const previousInsets = workspaceInsetsForSize(previousSize)
    const sizeChanged = nextSize.width !== workspaceSize.width || nextSize.height !== workspaceSize.height
    const geometrySizeChanged = nextSize.width !== previousSize.width || nextSize.height !== previousSize.height
    workspaceSize.width = nextSize.width
    workspaceSize.height = nextSize.height
    if (sizeChanged && !geometryCustomized[displayMode.value]) {
      const next = createModeLayout(displayMode.value, workspaceSize)
      layout.groups = next.groups
      layout.panels = next.panels
      layout.nextGroupNumber = next.nextGroupNumber
    } else {
      if (geometrySizeChanged && (hasMeasuredWorkspace || loadedLayouts.savedWorkspaceSize)) {
        resizeFreeDockableGroups(layout.groups, previousSize, nextSize,
          previousInsets, workspaceInsetsForSize(nextSize))
      }
      clampGroupsToWorkspace()
    }
    if (displayMode.value === 'debug') fitDebugDockableWorkspaceLayout(layout, workspaceSize)
    hasMeasuredWorkspace = true
    for (const group of layout.groups) {
      if (group.dock === 'free' && group.panelIds.some((panelId) => layout.panels[panelId].visible)) placeFreeGroup(group)
    }
  }

  watch(layout, scheduleSave, { deep: true })

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
    displayMode,
    groups,
    visibleGroups,
    workspaceSize,
    updateGroup,
    groupForPanel,
    activatePanel,
    setPanelVisible,
    showPanel,
    hidePanel,
    dockGroup,
    snapGroup,
    combinePanel,
    splitPanel,
    detachPanel,
    focusGroup,
    resetLayout,
    setDisplayMode,
  }
}
