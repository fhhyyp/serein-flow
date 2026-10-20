import assert from 'node:assert/strict';
import test from 'node:test';
import { reorderCanvases } from '../src/flow/canvasOrder.ts';
const canvases = [{ id: 'main' }, { id: 'init' }, { id: 'custom-1' }, { id: 'exit' }];
test('reorders a canvas before the drop target', () => {
  const reorderCanvasesResult = reorderCanvases(canvases, 'custom-1', 'main');
  const mapResult = reorderCanvasesResult.map((canvas) => canvas.id);
  assert.deepEqual(mapResult, ['custom-1', 'main', 'init', 'exit']);
  const reorderCanvasesResult1 = reorderCanvases(canvases, 'main', 'exit');
  const mapResult1 = reorderCanvasesResult1.map((canvas) => canvas.id);
  assert.deepEqual(mapResult1, ['init', 'custom-1', 'main', 'exit']);
});
test('ignores invalid or no-op canvas moves', () => {
  const reorderCanvasesResult2 = reorderCanvases(canvases, 'init', 'init');
  assert.deepEqual(reorderCanvasesResult2, canvases);
  const reorderCanvasesResult3 = reorderCanvases(canvases, 'missing', 'main');
  assert.deepEqual(reorderCanvasesResult3, canvases);
  const reorderCanvasesResult4 = reorderCanvases(canvases, 'main', 'missing');
  assert.deepEqual(reorderCanvasesResult4, canvases);
});
