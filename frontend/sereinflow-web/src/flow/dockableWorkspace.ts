import type { Component } from 'vue'

export type WorkspacePanelId =
  | 'canvas'
  | 'nodes'
  | 'inspector'
  | 'output'
  | 'diagnostics'
  | 'debug'
  | 'workpieces'

export type DockPosition = 'free' | 'left' | 'right' | 'top' | 'bottom' | 'fill'

export interface WorkspaceSize {
  width: number
  height: number
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
  const panelHeight = Math.max(260, height - 32)

  const groups = [
    group('canvas', ['canvas'], { x: 0, y: 0, width, height, dock: 'fill' }, 1),
  group('nodes', ['nodes'], { x: 0, y: 0, width: 286, height: panelHeight, dock: 'left' }, 10),
  group('inspector', ['inspector'], { x: 0, y: 0, width: 334, height: Math.min(panelHeight, 620), dock: 'right' }, 11),
  group('output', ['output'], { x: 0, y: 0, width: Math.min(720, Math.max(420, Math.round(width * .48))), height: 276, dock: 'bottom' }, 12),
    group('diagnostics', ['diagnostics'], { x: Math.max(320, Math.round(width * .36)), y: 86, width: Math.min(520, Math.max(360, Math.round(width * .34))), height: 330, dock: 'free' }, 13),
    group('debug', ['debug'], { x: Math.max(320, Math.round(width * .32)), y: 96, width: Math.min(520, Math.max(380, Math.round(width * .38))), height: Math.min(620, height - 32), dock: 'free' }, 14),
    group('workpieces', ['workpieces'], { x: Math.max(320, Math.round(width * .38)), y: 118, width: Math.min(600, Math.max(420, Math.round(width * .42))), height: Math.min(520, height - 40), dock: 'free' }, 15),
  ]

  return {
    formatVersion: 1,
    groups,
    panels: {
      canvas: { visible: true, groupId: 'canvas' },
      nodes: { visible: true, groupId: 'nodes' },
      inspector: { visible: true, groupId: 'inspector' },
      output: { visible: true, groupId: 'output' },
      diagnostics: { visible: false, groupId: 'diagnostics' },
      debug: { visible: false, groupId: 'debug' },
      workpieces: { visible: false, groupId: 'workpieces' },
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

export function normalizeDockableWorkspaceLayout(value: unknown, size: WorkspaceSize): DockableWorkspaceLayout | undefined {
  if (!value || typeof value !== 'object') return undefined
  const candidate = value as Partial<DockableWorkspaceLayout>
  if (candidate.formatVersion !== 1 || !Array.isArray(candidate.groups) || !candidate.panels || typeof candidate.panels !== 'object') {
    return undefined
  }

  const defaults = createDefaultDockableWorkspaceLayout(size)
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
