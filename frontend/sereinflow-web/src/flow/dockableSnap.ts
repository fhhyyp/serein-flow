import type { WorkspaceSize } from './dockableWorkspace'
import type { DockRect } from './dockableSplit'

const SNAP_DISTANCE = 20
const MIN_WIDTH = 240
const MIN_HEIGHT = 160

interface FreePanelGeometry {
  x: number
  y: number
  width: number
  height: number
}

function nearest(value: number, anchors: number[]): number {
  if (anchors.length === 0) return value
  const candidate = anchors.reduce((best, anchor) =>
    Math.abs(anchor - value) < Math.abs(best - value) ? anchor : best, anchors[0]!)
  return Math.abs(candidate - value) <= SNAP_DISTANCE ? candidate : value
}

function overlaps(firstStart: number, firstEnd: number, secondStart: number, secondEnd: number): boolean {
  return Math.min(firstEnd, secondEnd) - Math.max(firstStart, secondStart) > 32
}

export function snapFreePanelMove(group: FreePanelGeometry, peers: DockRect[], size: WorkspaceSize): FreePanelGeometry {
  const xAnchors: number[] = []
  const yAnchors: number[] = []
  for (const peer of peers) {
    if (overlaps(group.y, group.y + group.height, peer.top, peer.bottom)) {
      xAnchors.push(peer.left - group.width, peer.right, peer.left, peer.right - group.width)
    }
    if (overlaps(group.x, group.x + group.width, peer.left, peer.right)) {
      yAnchors.push(peer.top - group.height, peer.bottom, peer.top, peer.bottom - group.height)
    }
  }
  const peerX = nearest(group.x, xAnchors)
  const peerY = nearest(group.y, yAnchors)
  const x = Math.max(0, Math.min(size.width - group.width,
    peerX !== group.x ? peerX : nearest(group.x, [0, size.width - group.width])))
  const y = Math.max(0, Math.min(size.height - group.height,
    peerY !== group.y ? peerY : nearest(group.y, [0, size.height - group.height])))
  return {
    x,
    y,
    width: x !== group.x && Math.abs(group.x + group.width - size.width) <= SNAP_DISTANCE
      ? size.width - x : group.width,
    height: y !== group.y && Math.abs(group.y + group.height - size.height) <= SNAP_DISTANCE
      ? size.height - y : group.height,
  }
}

export function snapFreePanelResize(
  previous: FreePanelGeometry,
  current: FreePanelGeometry,
  peers: DockRect[],
  size: WorkspaceSize,
): FreePanelGeometry {
  const result = { ...current }
  const xAnchors = [0, size.width]
  const yAnchors = [0, size.height]
  for (const peer of peers) {
    if (overlaps(current.y, current.y + current.height, peer.top, peer.bottom)) xAnchors.push(peer.left, peer.right)
    if (overlaps(current.x, current.x + current.width, peer.left, peer.right)) yAnchors.push(peer.top, peer.bottom)
  }
  if (current.width !== previous.width) {
    if (current.x !== previous.x) {
      const right = current.x + current.width
      const x = nearest(current.x, xAnchors)
      if (right - x >= MIN_WIDTH) Object.assign(result, { x, width: right - x })
    } else {
      const right = nearest(current.x + current.width, xAnchors)
      if (right - current.x >= MIN_WIDTH) result.width = right - current.x
    }
  }
  if (current.height !== previous.height) {
    if (current.y !== previous.y) {
      const bottom = current.y + current.height
      const y = nearest(current.y, yAnchors)
      if (bottom - y >= MIN_HEIGHT) Object.assign(result, { y, height: bottom - y })
    } else {
      const bottom = nearest(current.y + current.height, yAnchors)
      if (bottom - current.y >= MIN_HEIGHT) result.height = bottom - current.y
    }
  }
  return result
}
