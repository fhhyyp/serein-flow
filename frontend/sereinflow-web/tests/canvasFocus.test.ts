import assert from 'node:assert/strict';
import test from 'node:test';
import { canvasFocusState, normalizeCanvasFocusSettings } from '../src/flow/canvasFocus.ts';
import type { FlowEdge, FlowNode } from '../src/flow/types.ts';
function node(id: string): FlowNode {
  return {
    id,
    type: 'workflow',
    position: { x: 0, y: 0 },
    data: {
      kind: 'action',
      titleKey: id,
      subtitleKey: id,
      status: 'ready',
      hasDataOutput: true,
      parameters: [],
    },
  };
}
function edge(id: string, source: string, target: string, semantic: FlowEdge['data']['semantic']): FlowEdge {
  return {
    id,
    source,
    target,
    data: { semantic },
  };
}
const nodes = [node('source'), node('selected'), node('target'), node('unrelated')];
const edges = [
  edge('data-input', 'source', 'selected', 'data'),
  edge('execution-output', 'selected', 'target', 'execution'),
  edge('unrelated-flow', 'target', 'unrelated', 'execution'),
];
test('a selected node focuses every direct execution and parameter connection', () => {
  const focus = canvasFocusState(nodes, edges, 'selected');
  assert.equal(focus.active, true);
  const sortResult = [...focus.focusedNodeIds].sort();
  assert.deepEqual(sortResult, ['selected', 'source', 'target']);
  const sortResult1 = [...focus.focusedEdgeIds].sort();
  assert.deepEqual(sortResult1, ['data-input', 'execution-output']);
});
test('multiple selected nodes focus only the selected nodes', () => {
  const mapResult: readonly FlowNode[] = nodes.map((item) => ({ ...item, selected: item.id === 'source' || item.id === 'selected' }));
  const focus = canvasFocusState(mapResult, edges, 'selected');
  assert.equal(focus.active, true);
  const sortResult2 = [...focus.focusedNodeIds].sort();
  assert.deepEqual(sortResult2, ['selected', 'source']);
  const sortResult3 = [...focus.focusedEdgeIds].sort();
  assert.deepEqual(sortResult3, ['data-input', 'execution-output']);
});
test('a selected edge focuses only its endpoints and the selected connection', () => {
  const focus = canvasFocusState(nodes, edges, undefined, 'data-input');
  assert.equal(focus.active, true);
  const sortResult4 = [...focus.focusedNodeIds].sort();
  assert.deepEqual(sortResult4, ['selected', 'source']);
  assert.deepEqual([...focus.focusedEdgeIds], ['data-input']);
});
test('a selected node can focus each relationship direction independently', () => {
  const focus = canvasFocusState(nodes, edges, 'selected', undefined, {
    enabled: true,
    parameterSources: true,
    parameterConsumers: false,
    callers: false,
    callees: false,
  });
  assert.equal(focus.active, true);
  const sortResult5 = [...focus.focusedNodeIds].sort();
  assert.deepEqual(sortResult5, ['selected', 'source']);
  assert.deepEqual([...focus.focusedEdgeIds], ['data-input']);
});
test('a disabled focus setting leaves the entire graph visible', () => {
  const focus = canvasFocusState(nodes, edges, 'selected', undefined, {
    enabled: false,
    parameterSources: true,
    parameterConsumers: true,
    callers: true,
    callees: true,
  });
  assert.equal(focus.active, false);
  const sortResult6 = [...focus.focusedNodeIds].sort();
  assert.deepEqual(sortResult6, ['selected', 'source', 'target', 'unrelated']);
  const sortResult7 = [...focus.focusedEdgeIds].sort();
  assert.deepEqual(sortResult7, ['data-input', 'execution-output', 'unrelated-flow']);
});
test('missing or malformed focus settings use enabled defaults', () => {
  const normalizeCanvasFocusSettingsResult = normalizeCanvasFocusSettings();
  assert.deepEqual(normalizeCanvasFocusSettingsResult, {
    enabled: true,
    parameterSources: true,
    parameterConsumers: true,
    callers: true,
    callees: true,
  });
  const normalizeCanvasFocusSettingsResult1 = normalizeCanvasFocusSettings({ enabled: false, callers: 'no' as never });
  assert.deepEqual(normalizeCanvasFocusSettingsResult1, {
    enabled: false,
    parameterSources: true,
    parameterConsumers: true,
    callers: true,
    callees: true,
  });
});
test('an empty or stale selection leaves the entire graph visible', () => {
  const noSelection = canvasFocusState(nodes, edges);
  const staleSelection = canvasFocusState(nodes, edges, 'removed');
  assert.equal(noSelection.active, false);
  const sortResult8 = [...noSelection.focusedNodeIds].sort();
  assert.deepEqual(sortResult8, ['selected', 'source', 'target', 'unrelated']);
  const sortResult9 = [...noSelection.focusedEdgeIds].sort();
  assert.deepEqual(sortResult9, ['data-input', 'execution-output', 'unrelated-flow']);
  assert.equal(staleSelection.active, false);
  const sortResult10 = [...staleSelection.focusedNodeIds].sort();
  assert.deepEqual(sortResult10, ['selected', 'source', 'target', 'unrelated']);
});
