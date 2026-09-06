import assert from 'node:assert/strict'
import test from 'node:test'
import { canvasFocusState, normalizeCanvasFocusSettings } from '../src/flow/canvasFocus.ts'
import type { FlowEdge, FlowNode } from '../src/flow/types.ts'

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
  }
}

function edge(id: string, source: string, target: string, semantic: FlowEdge['data']['semantic']): FlowEdge {
  return {
    id,
    source,
    target,
    data: { semantic },
  }
}

const nodes = [node('source'), node('selected'), node('target'), node('unrelated')]
const edges = [
  edge('data-input', 'source', 'selected', 'data'),
  edge('execution-output', 'selected', 'target', 'execution'),
  edge('unrelated-flow', 'target', 'unrelated', 'execution'),
]

test('a selected node focuses every direct execution and parameter connection', () => {
  const focus = canvasFocusState(nodes, edges, 'selected')

  assert.equal(focus.active, true)
  assert.deepEqual([...focus.focusedNodeIds].sort(), ['selected', 'source', 'target'])
  assert.deepEqual([...focus.focusedEdgeIds].sort(), ['data-input', 'execution-output'])
})

test('multiple selected nodes focus only the selected nodes', () => {
  const focus = canvasFocusState(
    nodes.map((item) => ({ ...item, selected: item.id === 'source' || item.id === 'selected' })),
    edges,
    'selected',
  )

  assert.equal(focus.active, true)
  assert.deepEqual([...focus.focusedNodeIds].sort(), ['selected', 'source'])
  assert.deepEqual([...focus.focusedEdgeIds].sort(), ['data-input', 'execution-output'])
})

test('a selected edge focuses only its endpoints and the selected connection', () => {
  const focus = canvasFocusState(nodes, edges, undefined, 'data-input')

  assert.equal(focus.active, true)
  assert.deepEqual([...focus.focusedNodeIds].sort(), ['selected', 'source'])
  assert.deepEqual([...focus.focusedEdgeIds], ['data-input'])
})

test('a selected node can focus each relationship direction independently', () => {
  const focus = canvasFocusState(nodes, edges, 'selected', undefined, {
    enabled: true,
    parameterSources: true,
    parameterConsumers: false,
    callers: false,
    callees: false,
  })

  assert.equal(focus.active, true)
  assert.deepEqual([...focus.focusedNodeIds].sort(), ['selected', 'source'])
  assert.deepEqual([...focus.focusedEdgeIds], ['data-input'])
})

test('a disabled focus setting leaves the entire graph visible', () => {
  const focus = canvasFocusState(nodes, edges, 'selected', undefined, {
    enabled: false,
    parameterSources: true,
    parameterConsumers: true,
    callers: true,
    callees: true,
  })

  assert.equal(focus.active, false)
  assert.deepEqual([...focus.focusedNodeIds].sort(), ['selected', 'source', 'target', 'unrelated'])
  assert.deepEqual([...focus.focusedEdgeIds].sort(), ['data-input', 'execution-output', 'unrelated-flow'])
})

test('missing or malformed focus settings use enabled defaults', () => {
  assert.deepEqual(normalizeCanvasFocusSettings(), {
    enabled: true,
    parameterSources: true,
    parameterConsumers: true,
    callers: true,
    callees: true,
  })
  assert.deepEqual(normalizeCanvasFocusSettings({ enabled: false, callers: 'no' as never }), {
    enabled: false,
    parameterSources: true,
    parameterConsumers: true,
    callers: true,
    callees: true,
  })
})

test('an empty or stale selection leaves the entire graph visible', () => {
  const noSelection = canvasFocusState(nodes, edges)
  const staleSelection = canvasFocusState(nodes, edges, 'removed')

  assert.equal(noSelection.active, false)
  assert.deepEqual([...noSelection.focusedNodeIds].sort(), ['selected', 'source', 'target', 'unrelated'])
  assert.deepEqual([...noSelection.focusedEdgeIds].sort(), ['data-input', 'execution-output', 'unrelated-flow'])
  assert.equal(staleSelection.active, false)
  assert.deepEqual([...staleSelection.focusedNodeIds].sort(), ['selected', 'source', 'target', 'unrelated'])
})
