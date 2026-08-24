import assert from 'node:assert/strict'
import test from 'node:test'
import { createInitialCanvases } from '../src/flow/initialCanvases.ts'

test('a new workspace contains only an empty main canvas', () => {
  const canvases = createInitialCanvases()

  assert.equal(canvases.length, 1)
  assert.equal(canvases[0]?.id, 'main')
  assert.equal(canvases[0]?.lifecycle, 'main')
  assert.deepEqual(canvases[0]?.nodes, [])
  assert.deepEqual(canvases[0]?.edges, [])
  assert.equal(canvases[0]?.selectedNodeId, undefined)
  assert.equal(canvases[0]?.selectedEdgeId, undefined)
})
