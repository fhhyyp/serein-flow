import assert from 'node:assert/strict';
import test from 'node:test';
import { ConnectionLineType } from '@vue-flow/core';
import { connectionLineStyleFor, connectionLineTypeForEdge, normalizeConnectionLineTypes, semanticFromConnectionHandles, } from '../src/flow/connectionLine.ts';
test('semantic connection defaults expose distinct customizable line types', () => {
  const execution = connectionLineStyleFor('execution');
  const data = connectionLineStyleFor('data');
  assert.equal(execution.lineType, ConnectionLineType.SmoothStep);
  assert.equal(data.lineType, ConnectionLineType.SmoothStep);
  assert.notEqual(execution.previewDashArray, data.previewDashArray);
});
test('an edge can override its semantic line type without changing the default', () => {
  const edge = {
    data: { semantic: 'execution' as const, lineType: ConnectionLineType.SimpleBezier },
  };
  const connectionLineTypeForEdgeResult = connectionLineTypeForEdge(edge);
  assert.equal(connectionLineTypeForEdgeResult, ConnectionLineType.SimpleBezier);
  const connectionLineTypeForEdgeResult1 = connectionLineTypeForEdge({ data: { semantic: 'data' as const } });
  assert.equal(connectionLineTypeForEdgeResult1, ConnectionLineType.SmoothStep);
});
test('legacy straight settings migrate to the requested orthogonal segment style', () => {
  const normalizeConnectionLineTypesResult = normalizeConnectionLineTypes({
    execution: ConnectionLineType.Straight,
    data: ConnectionLineType.Straight,
  });
  assert.deepEqual(normalizeConnectionLineTypesResult, {
    execution: ConnectionLineType.SmoothStep,
    data: ConnectionLineType.SmoothStep,
  });
});
test('drag previews resolve semantic type before the target handle is selected', () => {
  const semanticFromConnectionHandlesResult = semanticFromConnectionHandles('exec-success', null);
  assert.equal(semanticFromConnectionHandlesResult, 'execution');
  const semanticFromConnectionHandlesResult1 = semanticFromConnectionHandles('exec-failure', null);
  assert.equal(semanticFromConnectionHandlesResult1, 'execution');
  const semanticFromConnectionHandlesResult2 = semanticFromConnectionHandles('exec-error', null);
  assert.equal(semanticFromConnectionHandlesResult2, 'execution');
  const semanticFromConnectionHandlesResult3 = semanticFromConnectionHandles('data-out', null);
  assert.equal(semanticFromConnectionHandlesResult3, 'data');
  const semanticFromConnectionHandlesResult4 = semanticFromConnectionHandles('exec-error', 'exec-in');
  assert.equal(semanticFromConnectionHandlesResult4, 'execution');
  const semanticFromConnectionHandlesResult5 = semanticFromConnectionHandles('data-out', 'param-payload');
  assert.equal(semanticFromConnectionHandlesResult5, 'data');
  const semanticFromConnectionHandlesResult6 = semanticFromConnectionHandles(undefined, undefined);
  assert.equal(semanticFromConnectionHandlesResult6, undefined);
});
