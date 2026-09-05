import test from 'node:test'
import assert from 'node:assert/strict'
import {
  createDefaultDockableWorkspaceLayout,
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

  assert.equal(canvasGroup?.dock, 'fill')
  assert.deepEqual(canvasGroup?.panelIds, ['canvas'])
  assert.equal(layout.panels.canvas.visible, true)
  assert.equal(layout.groups.find((group) => group.id === 'nodes')?.dock, 'left')
  assert.equal(layout.groups.find((group) => group.id === 'inspector')?.dock, 'right')
  assert.equal(layout.groups.find((group) => group.id === 'output')?.dock, 'bottom')
})

test('invalid persisted layout is rejected', () => {
  assert.equal(normalizeDockableWorkspaceLayout({ formatVersion: 99 }, { width: 1_200, height: 720 }), undefined)
})
