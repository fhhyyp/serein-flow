import test from 'node:test'
import assert from 'node:assert/strict'
import {
  createDefaultDockableWorkspaceLayout,
  createDebugDockableWorkspaceLayout,
  normalizeDockableWorkspaceLayout,
} from '../src/flow/dockableWorkspace.ts'

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
  assert.equal(inspectorGroup?.dock, 'right')
  assert.equal(inspectorGroup?.width, 240)
  assert.equal(layout.groups.find((group) => group.id === 'output')?.dock, 'bottom')
  assert.equal(layout.groups.find((group) => group.id === 'diagnostics')?.dock, 'bottom')
  assert.equal(layout.groups.find((group) => group.id === 'diagnostics')?.collapsed, true)
  assert.equal(layout.panels.output.visible, false)
  assert.equal(layout.panels.diagnostics.visible, true)
})

test('debug layout keeps the canvas and workpieces on the left and debugger on the right', () => {
  const layout = createDebugDockableWorkspaceLayout({ width: 1_200, height: 720 })
  const canvasGroup = layout.groups.find((group) => group.id === 'canvas')!
  const workpiecesGroup = layout.groups.find((group) => group.id === 'workpieces')!
  const debugGroup = layout.groups.find((group) => group.id === 'debug')!

  assert.equal(canvasGroup.dock, 'free')
  assert.equal(canvasGroup.x, 0)
  assert.equal(canvasGroup.width, 600)
  assert.equal(workpiecesGroup.x, 0)
  assert.equal(workpiecesGroup.width, 600)
  assert.equal(workpiecesGroup.y, canvasGroup.height)
  assert.equal(debugGroup.dock, 'right')
  assert.equal(debugGroup.width, 600)
  assert.equal(debugGroup.height, 720)
  assert.equal(layout.panels.canvas.visible, true)
  assert.equal(layout.panels.workpieces.visible, true)
  assert.equal(layout.panels.debug.visible, true)
  assert.equal(layout.panels.nodes.visible, false)
  assert.equal(layout.panels.output.visible, false)
})

test('invalid persisted layout is rejected', () => {
  assert.equal(normalizeDockableWorkspaceLayout({ formatVersion: 99 }, { width: 1_200, height: 720 }), undefined)
})
