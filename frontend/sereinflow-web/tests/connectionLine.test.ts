import assert from 'node:assert/strict'
import test from 'node:test'
import { ConnectionLineType } from '@vue-flow/core'
import {
  connectionLineStyleFor,
  connectionLineTypeForEdge,
  normalizeConnectionLineTypes,
  semanticFromConnectionHandles,
} from '../src/flow/connectionLine.ts'

test('semantic connection defaults expose distinct customizable line types', () => {
  const execution = connectionLineStyleFor('execution')
  const data = connectionLineStyleFor('data')

  assert.equal(execution.lineType, ConnectionLineType.SmoothStep)
  assert.equal(data.lineType, ConnectionLineType.Bezier)
  assert.notEqual(execution.previewDashArray, data.previewDashArray)
})

test('an edge can override its semantic line type without changing the default', () => {
  const edge = {
    data: { semantic: 'execution' as const, lineType: ConnectionLineType.SimpleBezier },
  }

  assert.equal(connectionLineTypeForEdge(edge), ConnectionLineType.SimpleBezier)
  assert.equal(connectionLineTypeForEdge({ data: { semantic: 'data' as const } }), ConnectionLineType.Bezier)
})

test('legacy straight settings migrate to the requested orthogonal segment style', () => {
  assert.deepEqual(normalizeConnectionLineTypes({ execution: ConnectionLineType.Straight }), {
    execution: ConnectionLineType.SmoothStep,
    data: ConnectionLineType.Bezier,
  })
})

test('drag previews resolve semantic type before the target handle is selected', () => {
  assert.equal(semanticFromConnectionHandles('exec-out', null), 'execution')
  assert.equal(semanticFromConnectionHandles('data-out', null), 'data')
  assert.equal(semanticFromConnectionHandles('exec-out', 'exec-in'), 'execution')
  assert.equal(semanticFromConnectionHandles('data-out', 'param-payload'), 'data')
  assert.equal(semanticFromConnectionHandles(undefined, undefined), undefined)
})
