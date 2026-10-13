import type { Component } from 'vue'

export type WorkspacePanelId =
  | 'canvas'
  | 'nodes'
  | 'inspector'
  | 'output'
  | 'diagnostics'
  | 'debug'
  | 'workpieces'
  | 'variables'

export type DockPosition = 'free' | 'left' | 'right' | 'top' | 'bottom' | 'fill'
export type DockableWorkspaceMode = 'edit' | 'debug'

export interface WorkspaceSize {
  width: number
  height: number
}

export interface WorkspaceInsets {
  left: number
  right: number
  top: number
  bottom: number
}

export interface DockablePanelGroupState {
  id: string
  x: number
  y: number
  width: number
  height: number
  dock: DockPosition
  panelIds: WorkspacePanelId[]
  activePanelId: WorkspacePanelId
  zIndex: number
  collapsed: boolean
}

export interface DockablePanelPlacement {
  visible: boolean
  groupId: string
}

export interface DockableWorkspaceLayout {
  formatVersion: 1
  groups: DockablePanelGroupState[]
  panels: Record<WorkspacePanelId, DockablePanelPlacement>
  nextGroupNumber: number
}

export interface WorkspacePanelTab {
  id: WorkspacePanelId
  label: string
  icon: Component
  closable?: boolean
}

export const workspacePanelIds: WorkspacePanelId[] = [
  'canvas',
  'nodes',
  'inspector',
  'output',
  'diagnostics',
  'debug',
  'workpieces',
  'variables',
]

function group(
  id: string,
  panelIds: WorkspacePanelId[],
  geometry: Pick<DockablePanelGroupState, 'x' | 'y' | 'width' | 'height' | 'dock'>,
  zIndex: number,
): DockablePanelGroupState {
  return {
    id,
    ...geometry,
    panelIds,
    activePanelId: panelIds[0]!,
    zIndex,
    collapsed: false,
  }
}

export function createDefaultDockableWorkspaceLayout(size: WorkspaceSize = { width: 1_920, height: 980 }): DockableWorkspaceLayout {
  const width = Math.max(720, size.width)
  const height = Math.max(480, size.height)
  const sideWidth = Math.round(width * .2)
  const diagnosticsHeight = Math.min(220, Math.max(150, Math.round(height * .16)))

  const groups = [
    group('canvas', ['canvas'], { x: 0, y: 0, width, height, dock: 'fill' }, 1),
    group('nodes', ['nodes'], { x: 0, y: 0, width: sideWidth, height, dock: 'left' }, 10),
    group('inspector', ['inspector'], { x: 0, y: 0, width: sideWidth, height, dock: 'right' }, 11),
    group('output', ['output'], { x: 0, y: 0, width, height: 220, dock: 'bottom' }, 12),
    group('diagnostics', ['diagnostics'], { x: 0, y: 0, width, height: diagnosticsHeight, dock: 'bottom' }, 13),
    group('debug', ['debug'], { x: Math.max(320, Math.round(width * .32)), y: 96, width: Math.min(520, Math.max(380, Math.round(width * .38))), height: Math.min(620, height - 32), dock: 'free' }, 14),
    group('workpieces', ['workpieces'], { x: Math.max(320, Math.round(width * .38)), y: 118, width: Math.min(600, Math.max(420, Math.round(width * .42))), height: Math.min(520, height - 40), dock: 'free' }, 15),
    group('variables', ['variables'], { x: Math.max(320, Math.round(width * .36)), y: 132, width: Math.min(620, Math.max(420, Math.round(width * .44))), height: Math.min(520, height - 40), dock: 'free' }, 16),
  ]
  groups.find((item) => item.id === 'diagnostics')!.collapsed = true

  return {
    formatVersion: 1,
    groups,
    panels: {
      canvas: { visible: true, groupId: 'canvas' },
      nodes: { visible: true, groupId: 'nodes' },
      inspector: { visible: true, groupId: 'inspector' },
      output: { visible: false, groupId: 'output' },
      diagnostics: { visible: false, groupId: 'diagnostics' },
      debug: { visible: false, groupId: 'debug' },
      workpieces: { visible: false, groupId: 'workpieces' },
      variables: { visible: false, groupId: 'variables' },
    },
    nextGroupNumber: 1,
  }
}

export function createDebugDockableWorkspaceLayout(size: WorkspaceSize = { width: 1_920, height: 980 }): DockableWorkspaceLayout {
  const layout = createDefaultDockableWorkspaceLayout(size)
  const width = Math.max(720, size.width)
  const height = Math.max(480, size.height)
  const leftWidth = Math.round(width * .5)
  const canvasHeight = Math.round(height * .5)
  const workpiecesHeight = height - canvasHeight
  const canvasGroup = layout.groups.find((item) => item.id === 'canvas')!
  const workpiecesGroup = layout.groups.find((item) => item.id === 'workpieces')!
  const debugGroup = layout.groups.find((item) => item.id === 'debug')!

  Object.assign(canvasGroup, {
    x: 0,
    y: 0,
    width: leftWidth,
    height: canvasHeight,
    dock: 'free' as const,
  })
  Object.assign(workpiecesGroup, {
    x: 0,
    y: canvasHeight,
    width: leftWidth,
    height: workpiecesHeight,
    dock: 'free' as const,
  })
  Object.assign(debugGroup, {
    x: leftWidth,
    y: 0,
    width: width - leftWidth,
    height,
    dock: 'right' as const,
  })

  for (const panelId of ['nodes', 'inspector', 'output', 'diagnostics'] as const) {
    layout.panels[panelId].visible = false
  }
  layout.panels.canvas.visible = true
  layout.panels.workpieces.visible = true
  layout.panels.debug.visible = true
  return layout
}

// Keep the two free panels in the debugger's left column attached to each other
// and to the right-hand session panel after a workspace resize or an older saved layout.
export function fitDebugDockableWorkspaceLayout(layout: DockableWorkspaceLayout, size: WorkspaceSize): void {
  const canvas = layout.groups.find((group) => group.id === layout.panels.canvas.groupId)
  const workpieces = layout.groups.find((group) => group.id === layout.panels.workpieces.groupId)
  const session = layout.groups.find((group) => group.id === layout.panels.debug.groupId)
  if (!canvas || !workpieces || !session
    || !layout.panels.canvas.visible || !layout.panels.workpieces.visible || !layout.panels.debug.visible
    || canvas.collapsed || workpieces.collapsed || session.collapsed
    || canvas.id === workpieces.id || canvas.dock !== 'free' || workpieces.dock !== 'free' || session.dock !== 'right') return

  const maxGap = Math.max(120, size.height * .15)
  if (Math.abs(canvas.x) > 8 || Math.abs(canvas.y) > 8 || Math.abs(workpieces.x) > 8
    || Math.abs(canvas.width - workpieces.width) > Math.max(120, size.width * .1)
    || workpieces.y < canvas.height - 8 || workpieces.y > canvas.height + maxGap) return

  const columnWidth = size.width - Math.min(session.width, size.width)
  if (columnWidth < 240 || size.height < 320) return
  const split = Math.min(size.height - 160, Math.max(160,
    Math.round(size.height * canvas.height / Math.max(1, canvas.height + workpieces.height))))
  Object.assign(canvas, { x: 0, y: 0, width: columnWidth, height: split })
  Object.assign(workpieces, { x: 0, y: split, width: columnWidth, height: size.height - split })
}

export function resizeFreeDockableGroups(
  groups: DockablePanelGroupState[],
  previousSize: WorkspaceSize,
  nextSize: WorkspaceSize,
  previousInsets: WorkspaceInsets,
  nextInsets: WorkspaceInsets,
): void {
  const map = (value: number, oldStart: number, oldEnd: number, oldLimit: number,
    newStart: number, newEnd: number, newLimit: number): number => {
    if (oldEnd <= oldStart) return Math.round(value * newLimit / Math.max(1, oldLimit))
    if (value < oldStart) return Math.round(value * newStart / Math.max(1, oldStart))
    if (value > oldEnd) return Math.round(newEnd + (value - oldEnd) * (newLimit - newEnd) / Math.max(1, oldLimit - oldEnd))
    return Math.round(newStart + (value - oldStart) * (newEnd - newStart) / (oldEnd - oldStart))
  }
  const oldRight = previousSize.width - previousInsets.right
  const newRight = nextSize.width - nextInsets.right
  const oldBottom = previousSize.height - previousInsets.bottom
  const newBottom = nextSize.height - nextInsets.bottom
  for (const group of groups) {
    if (group.dock !== 'free') continue
    const x = map(group.x, previousInsets.left, oldRight, previousSize.width,
      nextInsets.left, newRight, nextSize.width)
    const right = map(group.x + group.width, previousInsets.left, oldRight, previousSize.width,
      nextInsets.left, newRight, nextSize.width)
    const y = map(group.y, previousInsets.top, oldBottom, previousSize.height,
      nextInsets.top, newBottom, nextSize.height)
    const bottom = map(group.y + group.height, previousInsets.top, oldBottom, previousSize.height,
      nextInsets.top, newBottom, nextSize.height)
    Object.assign(group, { x, y, width: right - x, height: bottom - y })
  }
}

export function createSnapshotDockableWorkspaceLayout(size: WorkspaceSize = { width: 1_920, height: 980 }): DockableWorkspaceLayout {
  const width = Math.max(240, size.width)
  const height = Math.max(480, size.height)
  const groups = width < 900
    ? (() => {
      const canvasHeight = Math.max(200, Math.round(height * .44))
      const workpiecesHeight = Math.max(150, Math.round(height * .24))
      return [
        group('snapshot-canvas', ['canvas'], { x: 0, y: 0, width, height: canvasHeight, dock: 'free' }, 10),
        group('snapshot-workpieces', ['workpieces', 'variables'], { x: 0, y: canvasHeight, width, height: workpiecesHeight, dock: 'free' }, 11),
        group('snapshot-session', ['debug', 'inspector', 'output'], { x: 0, y: canvasHeight + workpiecesHeight, width, height: Math.max(160, height - canvasHeight - workpiecesHeight), dock: 'free' }, 12),
      ]
    })()
    : (() => {
      const leftWidth = Math.max(360, Math.round(width * .52))
      const canvasHeight = Math.max(240, Math.round(height * .62))
      const workpiecesHeight = Math.max(160, height - canvasHeight)
      const sessionWidth = Math.max(320, width - leftWidth)
      return [
        group('snapshot-canvas', ['canvas'], { x: 0, y: 0, width: leftWidth, height: canvasHeight, dock: 'free' }, 10),
        group('snapshot-workpieces', ['workpieces', 'variables'], { x: 0, y: canvasHeight, width: leftWidth, height: workpiecesHeight, dock: 'free' }, 11),
        group('snapshot-session', ['debug', 'inspector', 'output'], { x: leftWidth, y: 0, width: sessionWidth, height, dock: 'right' }, 12),
      ]
    })()

  return {
    formatVersion: 1,
    groups,
    panels: {
      canvas: { visible: true, groupId: 'snapshot-canvas' },
      nodes: { visible: false, groupId: 'nodes' },
      inspector: { visible: true, groupId: 'snapshot-session' },
      output: { visible: true, groupId: 'snapshot-session' },
      diagnostics: { visible: false, groupId: 'diagnostics' },
      debug: { visible: true, groupId: 'snapshot-session' },
      workpieces: { visible: true, groupId: 'snapshot-workpieces' },
      variables: { visible: false, groupId: 'snapshot-workpieces' },
    },
    nextGroupNumber: 1,
  }
}

function isPanelId(value: unknown): value is WorkspacePanelId {
  return typeof value === 'string' && workspacePanelIds.includes(value as WorkspacePanelId)
}

function isDockPosition(value: unknown): value is DockPosition {
  return value === 'free'
    || value === 'left'
    || value === 'right'
    || value === 'top'
    || value === 'bottom'
    || value === 'fill'
}

function isFiniteNumber(value: unknown): value is number {
  return typeof value === 'number' && Number.isFinite(value)
}

function isGroup(value: unknown): value is DockablePanelGroupState {
  if (!value || typeof value !== 'object') return false
  const candidate = value as Partial<DockablePanelGroupState>
  return typeof candidate.id === 'string'
    && isFiniteNumber(candidate.x)
    && isFiniteNumber(candidate.y)
    && isFiniteNumber(candidate.width)
    && isFiniteNumber(candidate.height)
    && isDockPosition(candidate.dock)
    && Array.isArray(candidate.panelIds)
    && candidate.panelIds.length > 0
    && candidate.panelIds.every(isPanelId)
    && isPanelId(candidate.activePanelId)
    && isFiniteNumber(candidate.zIndex)
    && typeof candidate.collapsed === 'boolean'
}

export function normalizeDockableWorkspaceLayout(
  value: unknown,
  size: WorkspaceSize,
  fallback = createDefaultDockableWorkspaceLayout(size),
): DockableWorkspaceLayout | undefined {
  if (!value || typeof value !== 'object') return undefined
  const candidate = value as Partial<DockableWorkspaceLayout>
  if (candidate.formatVersion !== 1 || !Array.isArray(candidate.groups) || !candidate.panels || typeof candidate.panels !== 'object') {
    return undefined
  }

  const defaults = fallback
  const validGroups = candidate.groups.filter(isGroup)
  if (validGroups.length === 0) return undefined

  const groups = defaults.groups.map((defaultGroup) => {
    const storedGroup = validGroups.find((item) => item.id === defaultGroup.id)
    return storedGroup ? {
      ...defaultGroup,
      ...storedGroup,
      panelIds: [...storedGroup.panelIds],
    } : defaultGroup
  })
  const knownGroupIds = new Set(groups.map((item) => item.id))
  for (const storedGroup of validGroups) {
    if (!knownGroupIds.has(storedGroup.id)) {
      groups.push({ ...storedGroup, panelIds: [...storedGroup.panelIds] })
    }
  }

  const panels = { ...defaults.panels }
  for (const panelId of workspacePanelIds) {
    const stored = (candidate.panels as Record<string, unknown>)[panelId]
    if (!stored || typeof stored !== 'object') continue
    const placement = stored as Partial<DockablePanelPlacement>
    if (typeof placement.visible === 'boolean' && typeof placement.groupId === 'string') {
      panels[panelId] = { visible: placement.visible, groupId: placement.groupId }
    }
  }

  for (const panelId of workspacePanelIds) {
    const placement = panels[panelId]
    let targetGroup = groups.find((item) => item.id === placement.groupId)
    if (!targetGroup) {
      targetGroup = groups.find((item) => item.panelIds.includes(panelId)) ?? groups.find((item) => item.id === defaultGroupForPanel(panelId))
    }
    if (!targetGroup) continue
    for (const item of groups) {
      if (item !== targetGroup) item.panelIds = item.panelIds.filter((id) => id !== panelId)
    }
    if (!targetGroup.panelIds.includes(panelId)) targetGroup.panelIds.push(panelId)
    panels[panelId] = { ...placement, groupId: targetGroup.id }
    if (!targetGroup.panelIds.includes(targetGroup.activePanelId)) targetGroup.activePanelId = panelId
  }

  for (const item of groups) {
    if (!item.panelIds.includes(item.activePanelId)) item.activePanelId = item.panelIds[0]!
  }

  return {
    formatVersion: 1,
    groups,
    panels,
    nextGroupNumber: isFiniteNumber(candidate.nextGroupNumber) && candidate.nextGroupNumber > 0
      ? Math.floor(candidate.nextGroupNumber)
      : 1,
  }
}

function defaultGroupForPanel(panelId: WorkspacePanelId): string {
  return panelId
}
