import assert from 'node:assert/strict'
import test from 'node:test'
import { cloneCanvasGraph, removeEdgesById } from '../src/flow/canvasGraph.ts'

test('removing one edge retains unrelated edges', () => {
  const edges = [
    { id: 'execution-a', source: 'a', target: 'b' },
    { id: 'execution-b', source: 'b', target: 'c' },
    { id: 'data-a', source: 'a', target: 'c' },
  ]

  const remaining = removeEdgesById(edges, new Set(['execution-b']))

  assert.deepEqual(remaining.map((edge) => edge.id), ['execution-a', 'data-a'])
})

test('a canvas graph clone keeps the selected canvas connections', () => {
  const main = {
    id: 'main',
    nodes: [{ id: 'trigger' }],
    edges: [{ id: 'main-edge', source: 'trigger', target: 'action' }],
  }
  const init = {
    id: 'init',
    nodes: [{ id: 'prepare' }],
    edges: [{ id: 'init-edge', source: 'prepare', target: 'catalog' }],
  }

  const activeAfterSwitchingBack = cloneCanvasGraph(main)

  assert.notEqual(activeAfterSwitchingBack.nodes, main.nodes)
  assert.notEqual(activeAfterSwitchingBack.edges, main.edges)
  assert.deepEqual(activeAfterSwitchingBack.edges, main.edges)
  assert.equal(init.edges[0]?.id, 'init-edge')
})
