import assert from 'node:assert/strict'
import test from 'node:test'
import { reorderCanvases } from '../src/flow/canvasOrder.ts'

const canvases = [{ id: 'main' }, { id: 'init' }, { id: 'custom-1' }, { id: 'exit' }]

test('reorders a canvas before the drop target', () => {
  assert.deepEqual(
    reorderCanvases(canvases, 'custom-1', 'main').map((canvas) => canvas.id),
    ['custom-1', 'main', 'init', 'exit'],
  )
  assert.deepEqual(
    reorderCanvases(canvases, 'main', 'exit').map((canvas) => canvas.id),
    ['init', 'custom-1', 'main', 'exit'],
  )
})

test('ignores invalid or no-op canvas moves', () => {
  assert.deepEqual(reorderCanvases(canvases, 'init', 'init'), canvases)
  assert.deepEqual(reorderCanvases(canvases, 'missing', 'main'), canvases)
  assert.deepEqual(reorderCanvases(canvases, 'main', 'missing'), canvases)
})
