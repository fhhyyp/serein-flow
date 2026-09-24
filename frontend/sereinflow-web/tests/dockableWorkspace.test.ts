import test from 'node:test'
import assert from 'node:assert/strict'
import type { FlowWorkpieceDto } from '../src/api/flowApi.ts'
import {
  createDefaultDockableWorkspaceLayout,
  createDebugDockableWorkspaceLayout,
  createSnapshotDockableWorkspaceLayout,
  fitDebugDockableWorkspaceLayout,
  resizeFreeDockableGroups,
  normalizeDockableWorkspaceLayout,
} from '../src/flow/dockableWorkspace.ts'
import { reflowDockableResize } from '../src/flow/dockableResize.ts'
import { dockSplitZoneAtPoint, splitDockRect } from '../src/flow/dockableSplit.ts'
import { snapFreePanelMove, snapFreePanelResize } from '../src/flow/dockableSnap.ts'
import { firstFlowWorkpieceForExecution, firstFlowWorkpieceForNode, latestFlowWorkpieceForNode, selectFlowWorkpieceId } from '../src/flow/workpieceSelection.ts'

function workpiece(id: string, createdAt: string): FlowWorkpieceDto {
  return {
    runId: 'run-1',
    id,
    kind: 'File',
    name: `${id}.txt`,
    contentType: 'text/plain',
    length: 12,
    createdAt,
    downloadUrl: `/workpieces/${id}`,
  }
}

function executionWorkpiece(id: string, createdAt: string, nodeId: string, executionId: string): FlowWorkpieceDto {
  return { ...workpiece(id, createdAt), nodeId, executionId }
}

test('dockable layout keeps panel groups and active tabs across normalization', () => {
  const initial = createDefaultDockableWorkspaceLayout({ width: 1_200, height: 720 })
  const outputGroup = initial.groups.find((group) => group.id === 'output')!
  outputGroup.panelIds.push('diagnostics')
  outputGroup.activePanelId = 'diagnostics'
  initial.panels.diagnostics = { visible: true, groupId: outputGroup.id }

  const restored = normalizeDockableWorkspaceLayout(JSON.parse(JSON.stringify(initial)) as unknown, { width: 1_200, height: 720 })

  assert.ok(restored)
  assert.deepEqual(restored.panels.diagnostics, { visible: true, groupId: 'output' })
  assert.deepEqual(restored.groups.find((group) => group.id === 'output')?.panelIds, ['output', 'diagnostics'])
  assert.equal(restored.groups.find((group) => group.id === 'output')?.activePanelId, 'diagnostics')
})

test('default layout treats the flow canvas as the fill panel', () => {
  const layout = createDefaultDockableWorkspaceLayout({ width: 1_200, height: 720 })
  const canvasGroup = layout.groups.find((group) => group.id === 'canvas')
  const nodesGroup = layout.groups.find((group) => group.id === 'nodes')
  const inspectorGroup = layout.groups.find((group) => group.id === 'inspector')

  assert.equal(canvasGroup?.dock, 'fill')
  assert.deepEqual(canvasGroup?.panelIds, ['canvas'])
  assert.equal(layout.panels.canvas.visible, true)
  assert.equal(nodesGroup?.dock, 'left')
  assert.equal(nodesGroup?.width, 240)
  assert.equal(nodesGroup?.height, 720)
  assert.equal(inspectorGroup?.dock, 'right')
  assert.equal(inspectorGroup?.width, 240)
  assert.equal(inspectorGroup?.height, 720)
  assert.equal(layout.groups.find((group) => group.id === 'output')?.dock, 'bottom')
  assert.equal(layout.groups.find((group) => group.id === 'diagnostics')?.dock, 'bottom')
  assert.equal(layout.groups.find((group) => group.id === 'diagnostics')?.collapsed, true)
  assert.equal(layout.panels.output.visible, false)
  assert.equal(layout.panels.diagnostics.visible, false)
})

test('debug layout keeps the canvas and workpieces on the left and debugger on the right', () => {
  const layout = createDebugDockableWorkspaceLayout({ width: 1_200, height: 720 })
  const canvasGroup = layout.groups.find((group) => group.id === 'canvas')!
  const workpiecesGroup = layout.groups.find((group) => group.id === 'workpieces')!
  const debugGroup = layout.groups.find((group) => group.id === 'debug')!

  assert.equal(canvasGroup.dock, 'free')
  assert.equal(canvasGroup.x, 0)
  assert.equal(canvasGroup.width, 600)
  assert.equal(canvasGroup.height, 360)
  assert.equal(workpiecesGroup.x, 0)
  assert.equal(workpiecesGroup.width, 600)
  assert.equal(workpiecesGroup.y, canvasGroup.height)
  assert.equal(workpiecesGroup.height, 360)
  assert.equal(canvasGroup.height + workpiecesGroup.height, 720)
  assert.equal(debugGroup.dock, 'right')
  assert.equal(debugGroup.width, 600)
  assert.equal(debugGroup.height, 720)
  assert.equal(layout.panels.canvas.visible, true)
  assert.equal(layout.panels.workpieces.visible, true)
  assert.equal(layout.panels.debug.visible, true)
  assert.equal(layout.panels.nodes.visible, false)
  assert.equal(layout.panels.output.visible, false)
})

test('debug layout closes saved gaps and fills the resized workspace', () => {
  const layout = createDebugDockableWorkspaceLayout({ width: 1_200, height: 720 })
  const canvas = layout.groups.find((group) => group.id === 'canvas')!
  const workpieces = layout.groups.find((group) => group.id === 'workpieces')!
  const session = layout.groups.find((group) => group.id === 'debug')!
  canvas.width -= 36
  workpieces.width -= 36
  workpieces.y += 80
  workpieces.height -= 80

  fitDebugDockableWorkspaceLayout(layout, { width: 1_400, height: 800 })

  assert.equal(canvas.width, 1_400 - session.width)
  assert.equal(workpieces.width, canvas.width)
  assert.equal(workpieces.y, canvas.height)
  assert.equal(workpieces.y + workpieces.height, 800)
})

test('target panel edges split into adjacent panels while its center combines tabs', () => {
  const rect = { left: 0, top: 0, right: 800, bottom: 600 }
  assert.equal(dockSplitZoneAtPoint(rect, 30, 300), 'left')
  assert.equal(dockSplitZoneAtPoint(rect, 770, 300), 'right')
  assert.equal(dockSplitZoneAtPoint(rect, 400, 30), 'top')
  assert.equal(dockSplitZoneAtPoint(rect, 400, 570), 'bottom')
  assert.equal(dockSplitZoneAtPoint(rect, 400, 300), 'center')
  const { source, target } = splitDockRect(rect, 'left')
  assert.equal(source.right, target.left)
  assert.equal(source.left, rect.left)
  assert.equal(target.right, rect.right)
  assert.equal(source.top, target.top)
  assert.equal(source.bottom, target.bottom)
})

test('free panels snap to adjacent panels when moved or resized', () => {
  const peers = [{ left: 0, top: 0, right: 400, bottom: 300 }]
  const size = { width: 800, height: 600 }
  assert.deepEqual(snapFreePanelMove({ x: 412, y: 8, width: 388, height: 300 }, peers, size), { x: 400, y: 0, width: 400, height: 300 })
  assert.deepEqual(
    snapFreePanelResize(
      { x: 0, y: 300, width: 400, height: 300 },
      { x: 0, y: 314, width: 400, height: 286 },
      peers,
      size,
    ),
    { x: 0, y: 300, width: 400, height: 300 },
  )
})

test('workspace resizing keeps split panels flush with a fixed-width side dock', () => {
  const layout = createDefaultDockableWorkspaceLayout({ width: 1_280, height: 720 })
  const left = layout.groups.find((group) => group.id === 'nodes')!
  const canvas = layout.groups.find((group) => group.id === 'canvas')!
  Object.assign(left, { x: 0, y: 0, width: 512, height: 720, dock: 'free' })
  Object.assign(canvas, { x: 512, y: 0, width: 512, height: 720, dock: 'free' })

  resizeFreeDockableGroups([left, canvas],
    { width: 1_280, height: 720 }, { width: 1_440, height: 800 },
    { left: 0, right: 256, top: 0, bottom: 0 },
    { left: 0, right: 256, top: 0, bottom: 0 })

  assert.equal(left.x + left.width, canvas.x)
  assert.equal(canvas.x + canvas.width, 1_440 - 256)
  assert.equal(left.height, 800)
  assert.equal(canvas.height, 800)
})

test('snapshot layout keeps all run-session panels available in the debugger group', () => {
  const layout = createSnapshotDockableWorkspaceLayout({ width: 1_200, height: 720 })
  const canvasGroup = layout.groups.find((group) => group.id === 'snapshot-canvas')!
  const workpiecesGroup = layout.groups.find((group) => group.id === 'snapshot-workpieces')!
  const sessionGroup = layout.groups.find((group) => group.id === 'snapshot-session')!

  assert.equal(canvasGroup.x, 0)
  assert.equal(canvasGroup.y, 0)
  assert.equal(workpiecesGroup.x, 0)
  assert.equal(workpiecesGroup.y, canvasGroup.height)
  assert.equal(sessionGroup.dock, 'right')
  assert.deepEqual(sessionGroup.panelIds, ['debug', 'inspector', 'output'])
  assert.equal(layout.panels.canvas.visible, true)
  assert.equal(layout.panels.workpieces.visible, true)
  assert.equal(layout.panels.debug.visible, true)
  assert.equal(layout.panels.inspector.visible, true)
  assert.equal(layout.panels.output.visible, true)
})

test('resizing a shared free-panel boundary propagates through adjacent rows', () => {
  const groups = [
    { id: 'a', x: 0, y: 0, width: 400, height: 300, dock: 'free' as const, panelIds: ['canvas' as const], activePanelId: 'canvas' as const, zIndex: 1, collapsed: false },
    { id: 'b', x: 400, y: 0, width: 400, height: 300, dock: 'free' as const, panelIds: ['nodes' as const], activePanelId: 'nodes' as const, zIndex: 2, collapsed: false },
    { id: 'c', x: 0, y: 300, width: 400, height: 300, dock: 'free' as const, panelIds: ['inspector' as const], activePanelId: 'inspector' as const, zIndex: 3, collapsed: false },
    { id: 'd', x: 400, y: 300, width: 400, height: 300, dock: 'free' as const, panelIds: ['output' as const], activePanelId: 'output' as const, zIndex: 4, collapsed: false },
  ]
  const source = groups[0]!
  const previous = { ...source }
  source.width = 500

  reflowDockableResize(groups, source.id, previous, source, new Set(groups.map((group) => group.id)), { width: 800, height: 600 })

  assert.equal(groups[1]!.x, 500)
  assert.equal(groups[1]!.width, 300)
  assert.equal(groups[2]!.width, 500)
  assert.equal(groups[3]!.x, 500)
  assert.equal(groups[3]!.width, 300)
})

test('resizing an edge-docked panel reflows free peers on the shared boundary', () => {
  const groups = [
    { id: 'a', x: 0, y: 0, width: 400, height: 300, dock: 'free' as const, panelIds: ['canvas' as const], activePanelId: 'canvas' as const, zIndex: 1, collapsed: false },
    { id: 'b', x: 0, y: 300, width: 400, height: 300, dock: 'free' as const, panelIds: ['workpieces' as const], activePanelId: 'workpieces' as const, zIndex: 2, collapsed: false },
    { id: 'session', x: 400, y: 0, width: 400, height: 600, dock: 'right' as const, panelIds: ['debug' as const], activePanelId: 'debug' as const, zIndex: 3, collapsed: false },
  ]
  const source = groups[2]!
  const previous = { ...source }
  source.width = 500
  reflowDockableResize(groups, source.id, previous, source, new Set(groups.map((group) => group.id)), { width: 800, height: 600 })

  assert.equal(groups[0]!.width, 300)
  assert.equal(groups[1]!.width, 300)
  assert.equal(groups[2]!.width, 500)
})

test('invalid persisted layout is rejected', () => {
  assert.equal(normalizeDockableWorkspaceLayout({ formatVersion: 99 }, { width: 1_200, height: 720 }), undefined)
})

test('live workpiece refresh selects only newly arrived workpieces', () => {
  const first = workpiece('first', '2026-09-05T09:00:00.000Z')
  const second = workpiece('second', '2026-09-05T09:01:00.000Z')
  const third = workpiece('third', '2026-09-05T09:02:00.000Z')

  assert.equal(selectFlowWorkpieceId([], [first, second], '', false), 'second')
  assert.equal(selectFlowWorkpieceId([first, second], [first, second], 'first', true), 'first')
  assert.equal(selectFlowWorkpieceId([first, second], [first, second, third], 'first', true), 'third')
  assert.equal(selectFlowWorkpieceId([first, second, third], [first, second, third], 'second', true), 'second')
})

test('workpiece focus selects the latest matching node output without filtering the list', () => {
  const source = workpiece('source', '2026-09-05T09:00:00.000Z')
  const output = workpiece('output', '2026-09-05T09:01:00.000Z')
  const latestOutput = workpiece('output-latest', '2026-09-05T09:02:00.000Z')
  source.nodeId = 'opencv-source'
  output.nodeId = 'opencv-gray'
  latestOutput.nodeId = 'opencv-gray'

  assert.equal(latestFlowWorkpieceForNode([source, output, latestOutput], 'opencv-gray')?.id, 'output-latest')
  assert.equal(latestFlowWorkpieceForNode([source, output, latestOutput], 'missing'), undefined)
})

test('workpiece focus resolves the first artifact for a repeated execution', () => {
  const firstExecution = executionWorkpiece('first-step', '2026-09-05T09:00:00.000Z', 'opencv-gray', 'execution-1')
  const secondExecution = executionWorkpiece('second-step', '2026-09-05T09:01:00.000Z', 'opencv-gray', 'execution-2')
  const extraFirstArtifact = executionWorkpiece('first-step-extra', '2026-09-05T09:02:00.000Z', 'opencv-gray', 'execution-1')

  assert.equal(firstFlowWorkpieceForExecution([secondExecution, extraFirstArtifact, firstExecution], 'execution-1')?.id, 'first-step')
  assert.equal(firstFlowWorkpieceForNode([secondExecution, extraFirstArtifact, firstExecution], 'opencv-gray')?.id, 'first-step')
})
