import type { DockablePanelGroupState, DockPosition, WorkspaceSize } from './dockableWorkspace'

const EDGE_EPSILON = 4
const MIN_WIDTH = 240
const MIN_HEIGHT = 160

type ResizeSnapshot = Pick<DockablePanelGroupState, 'x' | 'y' | 'width' | 'height' | 'dock'>

interface Rect {
  left: number
  top: number
  right: number
  bottom: number
}

function isEdgeDockPosition(dock: DockPosition): dock is Exclude<DockPosition, 'free' | 'fill'> {
  return dock === 'left' || dock === 'right' || dock === 'top' || dock === 'bottom'
}

function rect(group: ResizeSnapshot): Rect {
  return {
    left: group.x,
    top: group.y,
    right: group.x + group.width,
    bottom: group.y + group.height,
  }
}

function edgeExtent(
  group: DockablePanelGroupState,
  peers: DockablePanelGroupState[],
  workspaceSize: WorkspaceSize,
): number {
  const horizontal = group.dock === 'left' || group.dock === 'right'
  const capacity = horizontal ? workspaceSize.width : workspaceSize.height
  const raw = group.collapsed ? 76 : horizontal ? group.width : group.height
  const total = peers.reduce((sum, peer) => {
    const peerRaw = peer.collapsed ? 76 : horizontal ? peer.width : peer.height
    return sum + Math.max(0, Math.min(peerRaw, capacity))
  }, 0)
  const scale = total > capacity && total > 0 ? capacity / total : 1
  return Math.max(0, Math.min(raw, capacity)) * scale
}

function renderedRect(
  group: DockablePanelGroupState,
  groups: DockablePanelGroupState[],
  visibleGroupIds: ReadonlySet<string>,
  workspaceSize: WorkspaceSize,
): Rect {
  if (group.dock === 'free') return rect(group)
  if (group.dock === 'fill') return { left: 0, top: 0, right: workspaceSize.width, bottom: workspaceSize.height }

  const peers = groups.filter((candidate) => candidate.dock === group.dock
    && candidate.panelIds.length > 0
    && visibleGroupIds.has(candidate.id))
  const groupIndex = peers.findIndex((candidate) => candidate.id === group.id)
  const offset = peers.slice(0, Math.max(0, groupIndex)).reduce(
    (sum, candidate) => sum + edgeExtent(candidate, peers, workspaceSize),
    0,
  )
  const extent = edgeExtent(group, peers, workspaceSize)
  if (group.dock === 'left') return { left: offset, top: 0, right: offset + extent, bottom: workspaceSize.height }
  if (group.dock === 'right') return { left: workspaceSize.width - offset - extent, top: 0, right: workspaceSize.width - offset, bottom: workspaceSize.height }
  if (group.dock === 'top') return { left: 0, top: offset, right: workspaceSize.width, bottom: offset + extent }
  return { left: 0, top: workspaceSize.height - offset - extent, right: workspaceSize.width, bottom: workspaceSize.height - offset }
}

function sameEdge(first: number, second: number): boolean {
  return Math.abs(first - second) <= EDGE_EPSILON
}

function connected(firstStart: number, firstEnd: number, secondStart: number, secondEnd: number): boolean {
  return Math.min(firstEnd, secondEnd) - Math.max(firstStart, secondStart) >= -EDGE_EPSILON
}

function clampDelta(delta: number, leftGroups: ResizeSnapshot[], rightGroups: ResizeSnapshot[], minSize: number): number {
  if (delta > 0 && rightGroups.length > 0) {
    return Math.min(delta, ...rightGroups.map((group) => Math.max(0, group.width - minSize)))
  }
  if (delta < 0 && leftGroups.length > 0) {
    return Math.max(delta, ...leftGroups.map((group) => -Math.max(0, group.width - minSize)))
  }
  return delta
}

function clampHorizontalDelta(delta: number, topGroups: ResizeSnapshot[], bottomGroups: ResizeSnapshot[], minSize: number): number {
  if (delta > 0 && bottomGroups.length > 0) {
    return Math.min(delta, ...bottomGroups.map((group) => Math.max(0, group.height - minSize)))
  }
  if (delta < 0 && topGroups.length > 0) {
    return Math.max(delta, ...topGroups.map((group) => -Math.max(0, group.height - minSize)))
  }
  return delta
}

function moveVerticalBoundary(
  groups: DockablePanelGroupState[],
  referenceGroups: DockablePanelGroupState[],
  sourceGroupId: string,
  oldBoundary: number,
  delta: number,
  sourceRange: { top: number; bottom: number },
  visibleGroupIds: ReadonlySet<string>,
  workspaceSize: WorkspaceSize,
): number {
  if (Math.abs(delta) <= EDGE_EPSILON) return 0
  const leftGroups: DockablePanelGroupState[] = []
  const rightGroups: DockablePanelGroupState[] = []

  for (const group of groups) {
    if (group.id === sourceGroupId || group.dock === 'fill' || !visibleGroupIds.has(group.id)) continue
    const reference = referenceGroups.find((candidate) => candidate.id === group.id) ?? group
    const current = renderedRect(reference, referenceGroups, visibleGroupIds, workspaceSize)
    if (!connected(current.top, current.bottom, sourceRange.top, sourceRange.bottom)) continue
    if (sameEdge(current.right, oldBoundary)) leftGroups.push(group)
    if (sameEdge(current.left, oldBoundary)) rightGroups.push(group)
  }

  const effectiveDelta = clampDelta(delta, leftGroups, rightGroups, MIN_WIDTH)
  for (const group of leftGroups) group.width = Math.max(MIN_WIDTH, group.width + effectiveDelta)
  for (const group of rightGroups) {
    if (group.dock === 'free') group.x += effectiveDelta
    group.width = Math.max(MIN_WIDTH, group.width - effectiveDelta)
  }
  return effectiveDelta
}

function moveHorizontalBoundary(
  groups: DockablePanelGroupState[],
  referenceGroups: DockablePanelGroupState[],
  sourceGroupId: string,
  oldBoundary: number,
  delta: number,
  sourceRange: { left: number; right: number },
  visibleGroupIds: ReadonlySet<string>,
  workspaceSize: WorkspaceSize,
): number {
  if (Math.abs(delta) <= EDGE_EPSILON) return 0
  const topGroups: DockablePanelGroupState[] = []
  const bottomGroups: DockablePanelGroupState[] = []

  for (const group of groups) {
    if (group.id === sourceGroupId || group.dock === 'fill' || !visibleGroupIds.has(group.id)) continue
    const reference = referenceGroups.find((candidate) => candidate.id === group.id) ?? group
    const current = renderedRect(reference, referenceGroups, visibleGroupIds, workspaceSize)
    if (!connected(current.left, current.right, sourceRange.left, sourceRange.right)) continue
    if (sameEdge(current.bottom, oldBoundary)) topGroups.push(group)
    if (sameEdge(current.top, oldBoundary)) bottomGroups.push(group)
  }

  const effectiveDelta = clampHorizontalDelta(delta, topGroups, bottomGroups, MIN_HEIGHT)
  for (const group of topGroups) group.height = Math.max(MIN_HEIGHT, group.height + effectiveDelta)
  for (const group of bottomGroups) {
    if (group.dock === 'free') group.y += effectiveDelta
    group.height = Math.max(MIN_HEIGHT, group.height - effectiveDelta)
  }
  return effectiveDelta
}

function reflowRenderedBoundaries(
  groups: DockablePanelGroupState[],
  sourceGroupId: string,
  previous: ResizeSnapshot,
  current: DockablePanelGroupState,
  visibleGroupIds: ReadonlySet<string>,
  workspaceSize: WorkspaceSize,
): void {
  const beforeGroups = groups.map((group) => group.id === sourceGroupId
    ? { ...group, ...previous }
    : group)
  const beforeSource = beforeGroups.find((group) => group.id === sourceGroupId)
  if (!beforeSource) return
  const before = renderedRect(beforeSource, beforeGroups, visibleGroupIds, workspaceSize)
  const after = renderedRect(current, groups, visibleGroupIds, workspaceSize)

  const freeSource = previous.dock === 'free' && current.dock === 'free'
  const horizontalEdgeSource = isEdgeDockPosition(current.dock)
    && (current.dock === 'left' || current.dock === 'right')
  const verticalEdgeSource = isEdgeDockPosition(current.dock)
    && (current.dock === 'top' || current.dock === 'bottom')

  if ((freeSource || horizontalEdgeSource) && current.width !== previous.width) {
    const leftDelta = after.left - before.left
    const rightDelta = after.right - before.right
    const effectiveLeftDelta = moveVerticalBoundary(groups, beforeGroups, sourceGroupId, before.left, leftDelta, { top: before.top, bottom: before.bottom }, visibleGroupIds, workspaceSize)
    const effectiveRightDelta = moveVerticalBoundary(groups, beforeGroups, sourceGroupId, before.right, rightDelta, { top: before.top, bottom: before.bottom }, visibleGroupIds, workspaceSize)
    if (freeSource) {
      const nextLeft = Math.abs(leftDelta) > EDGE_EPSILON ? before.left + effectiveLeftDelta : after.left
      const nextRight = Math.abs(rightDelta) > EDGE_EPSILON ? before.right + effectiveRightDelta : after.right
      current.x = nextLeft
      current.width = Math.max(MIN_WIDTH, nextRight - nextLeft)
    } else if (current.dock === 'left' && Math.abs(rightDelta) > EDGE_EPSILON) {
      current.width = Math.max(MIN_WIDTH, previous.width + effectiveRightDelta)
    } else if (current.dock === 'right' && Math.abs(leftDelta) > EDGE_EPSILON) {
      current.width = Math.max(MIN_WIDTH, previous.width - effectiveLeftDelta)
    }
  }

  if ((freeSource || verticalEdgeSource) && current.height !== previous.height) {
    const topDelta = after.top - before.top
    const bottomDelta = after.bottom - before.bottom
    const effectiveTopDelta = moveHorizontalBoundary(groups, beforeGroups, sourceGroupId, before.top, topDelta, { left: before.left, right: before.right }, visibleGroupIds, workspaceSize)
    const effectiveBottomDelta = moveHorizontalBoundary(groups, beforeGroups, sourceGroupId, before.bottom, bottomDelta, { left: before.left, right: before.right }, visibleGroupIds, workspaceSize)
    if (freeSource) {
      const nextTop = Math.abs(topDelta) > EDGE_EPSILON ? before.top + effectiveTopDelta : after.top
      const nextBottom = Math.abs(bottomDelta) > EDGE_EPSILON ? before.bottom + effectiveBottomDelta : after.bottom
      current.y = nextTop
      current.height = Math.max(MIN_HEIGHT, nextBottom - nextTop)
    } else if (current.dock === 'top' && Math.abs(bottomDelta) > EDGE_EPSILON) {
      current.height = Math.max(MIN_HEIGHT, previous.height + effectiveBottomDelta)
    } else if (current.dock === 'bottom' && Math.abs(topDelta) > EDGE_EPSILON) {
      current.height = Math.max(MIN_HEIGHT, previous.height - effectiveTopDelta)
    }
  }
}

export function reflowDockableResize(
  groups: DockablePanelGroupState[],
  sourceGroupId: string,
  previous: ResizeSnapshot,
  current: DockablePanelGroupState,
  visibleGroupIds: ReadonlySet<string>,
  workspaceSize: WorkspaceSize,
): void {
  if (previous.dock === current.dock
    && (current.dock === 'free' || isEdgeDockPosition(current.dock))) {
    reflowRenderedBoundaries(groups, sourceGroupId, previous, current, visibleGroupIds, workspaceSize)
  }
}
