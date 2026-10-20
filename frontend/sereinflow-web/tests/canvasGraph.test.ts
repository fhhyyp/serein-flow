import assert from 'node:assert/strict';
import test from 'node:test';
import { applyNodePositionChanges, cloneCanvasGraph, removeEdgesById } from '../src/flow/canvasGraph.ts';
test('removing one edge retains unrelated edges', () => {
  const edges = [
    { id: 'execution-a', source: 'a', target: 'b' },
    { id: 'execution-b', source: 'b', target: 'c' },
    { id: 'data-a', source: 'a', target: 'c' },
  ];
  const argumentValue: readonly {
    id: string;
    source: string;
    target: string;
  }[] = edges;
  const setInstance: ReadonlySet<string> = new Set(['execution-b']);
  const remaining = removeEdgesById(argumentValue, setInstance);
  const mapResult = remaining.map((edge) => edge.id);
  assert.deepEqual(mapResult, ['execution-a', 'data-a']);
});
test('a canvas graph clone keeps the selected canvas connections', () => {
  const main = {
    id: 'main',
    nodes: [{ id: 'trigger' }],
    edges: [{ id: 'main-edge', source: 'trigger', target: 'action' }],
  };
  const init = {
    id: 'init',
    nodes: [{ id: 'prepare' }],
    edges: [{ id: 'init-edge', source: 'prepare', target: 'catalog' }],
  };
  const activeAfterSwitchingBack = cloneCanvasGraph(main);
  assert.notEqual(activeAfterSwitchingBack.nodes, main.nodes);
  assert.notEqual(activeAfterSwitchingBack.edges, main.edges);
  assert.deepEqual(activeAfterSwitchingBack.edges, main.edges);
  assert.equal(init.edges[0]?.id, 'init-edge');
});
test('position changes are copied into the workspace model before controlled updates', () => {
  const nodes = [
    { id: 'first', position: { x: 12, y: 24 } },
    { id: 'second', position: { x: 120, y: 240 } },
  ];
  const moved = applyNodePositionChanges(nodes, [
    { type: 'position', id: 'first', position: { x: 96, y: 144 } },
  ]);
  const mapResult1 = moved.map((node) => node.position);
  assert.deepEqual(mapResult1, [{ x: 96, y: 144 }, { x: 120, y: 240 }]);
  assert.deepEqual(nodes[0]?.position, { x: 12, y: 24 });
});
