import assert from 'node:assert/strict';
import test from 'node:test';
import { clampInspectorPanelWidth, defaultInspectorPanelWidth, maximumInspectorPanelWidth, minimumInspectorPanelWidth, parseStoredInspectorPanelWidth, } from '../src/flow/inspectorPanelWidth.ts';
test('inspector width reserves canvas space and honours desktop bounds', () => {
  const maximumInspectorPanelWidthResult = maximumInspectorPanelWidth(1920);
  assert.equal(maximumInspectorPanelWidthResult, 640);
  const maximumInspectorPanelWidthResult1 = maximumInspectorPanelWidth(760);
  assert.equal(maximumInspectorPanelWidthResult1, 480);
  const clampInspectorPanelWidthResult = clampInspectorPanelWidth(1000, 1920);
  assert.equal(clampInspectorPanelWidthResult, 640);
  const clampInspectorPanelWidthResult1 = clampInspectorPanelWidth(120, 1920);
  assert.equal(clampInspectorPanelWidthResult1, minimumInspectorPanelWidth);
  const clampInspectorPanelWidthResult2 = clampInspectorPanelWidth(500, 760);
  assert.equal(clampInspectorPanelWidthResult2, 480);
});
test('stored inspector width falls back safely when missing or invalid', () => {
  const parseStoredInspectorPanelWidthResult = parseStoredInspectorPanelWidth(null, 1920);
  assert.equal(parseStoredInspectorPanelWidthResult, defaultInspectorPanelWidth);
  const parseStoredInspectorPanelWidthResult1 = parseStoredInspectorPanelWidth('not-a-number', 1920);
  assert.equal(parseStoredInspectorPanelWidthResult1, defaultInspectorPanelWidth);
  const parseStoredInspectorPanelWidthResult2 = parseStoredInspectorPanelWidth('590', 1920);
  assert.equal(parseStoredInspectorPanelWidthResult2, 590);
  const parseStoredInspectorPanelWidthResult3 = parseStoredInspectorPanelWidth('590', 760);
  assert.equal(parseStoredInspectorPanelWidthResult3, 480);
});
