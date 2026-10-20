import assert from 'node:assert/strict';
import test from 'node:test';
import { parseFlowValidationDiagnosticTarget } from '../src/flow/validationDiagnostics.ts';
test('validation paths identify a node and its parameter connector', () => {
  const parseFlowValidationDiagnosticTargetResult = parseFlowValidationDiagnosticTarget('nodes.action-main-3.parameters.合格数量');
  assert.deepEqual(parseFlowValidationDiagnosticTargetResult, { nodeId: 'action-main-3', parameterName: '合格数量' });
});
test('node-level validation paths keep their node target', () => {
  const parseFlowValidationDiagnosticTargetResult1 = parseFlowValidationDiagnosticTarget('nodes.condition-main-2.type');
  assert.deepEqual(parseFlowValidationDiagnosticTargetResult1, { nodeId: 'condition-main-2' });
});
test('non-node validation paths are not treated as canvas targets', () => {
  const parseFlowValidationDiagnosticTargetResult2 = parseFlowValidationDiagnosticTarget('entryNodeId');
  assert.equal(parseFlowValidationDiagnosticTargetResult2, undefined);
  const parseFlowValidationDiagnosticTargetResult3 = parseFlowValidationDiagnosticTarget();
  assert.equal(parseFlowValidationDiagnosticTargetResult3, undefined);
});
