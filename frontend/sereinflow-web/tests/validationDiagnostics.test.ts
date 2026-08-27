import assert from 'node:assert/strict'
import test from 'node:test'
import { parseFlowValidationDiagnosticTarget } from '../src/flow/validationDiagnostics.ts'

test('validation paths identify a node and its parameter connector', () => {
  assert.deepEqual(
    parseFlowValidationDiagnosticTarget('nodes.action-main-3.parameters.合格数量'),
    { nodeId: 'action-main-3', parameterName: '合格数量' },
  )
})

test('node-level validation paths keep their node target', () => {
  assert.deepEqual(
    parseFlowValidationDiagnosticTarget('nodes.condition-main-2.type'),
    { nodeId: 'condition-main-2' },
  )
})

test('non-node validation paths are not treated as canvas targets', () => {
  assert.equal(parseFlowValidationDiagnosticTarget('entryNodeId'), undefined)
  assert.equal(parseFlowValidationDiagnosticTarget(), undefined)
})
