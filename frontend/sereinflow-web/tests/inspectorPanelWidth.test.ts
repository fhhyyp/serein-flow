import assert from 'node:assert/strict'
import test from 'node:test'
import {
  clampInspectorPanelWidth,
  defaultInspectorPanelWidth,
  maximumInspectorPanelWidth,
  minimumInspectorPanelWidth,
  parseStoredInspectorPanelWidth,
} from '../src/flow/inspectorPanelWidth.ts'

test('inspector width reserves canvas space and honours desktop bounds', () => {
  assert.equal(maximumInspectorPanelWidth(1_920), 640)
  assert.equal(maximumInspectorPanelWidth(760), 480)
  assert.equal(clampInspectorPanelWidth(1_000, 1_920), 640)
  assert.equal(clampInspectorPanelWidth(120, 1_920), minimumInspectorPanelWidth)
  assert.equal(clampInspectorPanelWidth(500, 760), 480)
})

test('stored inspector width falls back safely when missing or invalid', () => {
  assert.equal(parseStoredInspectorPanelWidth(null, 1_920), defaultInspectorPanelWidth)
  assert.equal(parseStoredInspectorPanelWidth('not-a-number', 1_920), defaultInspectorPanelWidth)
  assert.equal(parseStoredInspectorPanelWidth('590', 1_920), 590)
  assert.equal(parseStoredInspectorPanelWidth('590', 760), 480)
})
