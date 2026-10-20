import assert from 'node:assert/strict';
import { after, test } from 'node:test';
import { createServer } from 'vite';
import { computed, ref } from 'vue';
const vite = await createServer({ server: { middlewareMode: true }, appType: 'custom' });
const { useFlowGraph } = await vite.ssrLoadModule('/src/composables/useFlowGraph.ts');
after(() => {
  const closeResult = vite.close();
  return closeResult;
});
function makeGraph(semantic: 'data' | 'execution') {
  const source = { id: 'source', type: 'workflow', position: { x: 0, y: 0 }, data: { kind: 'operation', parameters: [], hasDataOutput: true } };
  const parameter = (id: string) => ({ id, source: 'literal', sourceNodeId: undefined as string | undefined, sourcePortId: undefined as string | undefined });
  const first = { id: 'first', type: 'workflow', position: { x: 300, y: 0 }, data: { kind: 'operation', parameters: [parameter('a')], hasDataOutput: true } };
  const second = { id: 'second', type: 'workflow', position: { x: 600, y: 0 }, data: { kind: 'operation', parameters: [parameter('b')], hasDataOutput: true } };
  const third = { id: 'third', type: 'workflow', position: { x: 900, y: 0 }, data: { kind: 'operation', parameters: [parameter('c')], hasDataOutput: true } };
  if (semantic === 'data') {
    first.data.parameters[0]!.source = 'previousNode';
    first.data.parameters[0]!.sourceNodeId = 'source';
    first.data.parameters[0]!.sourcePortId = 'data-out';
  }
  const oldEdge = semantic === 'data'
    ? { id: 'old', source: 'source', target: 'first', sourceHandle: 'data-out', targetHandle: 'param-a', data: { semantic, targetParameterId: 'a', lineType: 'default' } }
    : { id: 'old', source: 'source', target: 'first', sourceHandle: 'exec-success', targetHandle: 'exec-in', data: { semantic, branch: 'success', lineType: 'default' } };
  const canvas = ref({ id: 'main', nodes: [source, first, second, third], edges: [oldEdge], selectedEdgeId: 'old' });
  let history = 0;
  let changed = 0;
  const evaluatedValue = {
    currentCanvas: computed(() => canvas.value),
    nextNodeNumber: ref(1),
    connectionLineTypes: { execution: 'smoothstep', data: 'smoothstep' },
    canvasFocusSettings: { enabled: false, parameterSources: true, parameterConsumers: true, callers: true, callees: true },
    mobilePanel: ref(null),
    notice: ref(''),
    isRestoringWorkspace: ref(false),
    isSwitchingCanvas: ref(false),
    recordWorkspaceMutation: () => {
      history++;
    },
    markWorkspaceChanged: () => {
      changed++;
    },
  };
  const graph = useFlowGraph(evaluatedValue);
  return { canvas, graph, first, second, third, oldEdge, counts: () => ({ history, changed }) };
}
test('reconnecting a data arrow moves its parameter source in one undoable change', () => {
  const state = makeGraph('data');
  state.graph.onEdgeUpdate({
    event: {},
    edge: state.oldEdge,
    connection: { source: 'source', sourceHandle: 'data-out', target: 'second', targetHandle: 'param-b' },
  });
  assert.equal(state.canvas.value.edges.length, 1);
  assert.equal(state.canvas.value.edges[0]!.target, 'second');
  assert.notEqual(state.canvas.value.edges[0]!.id, 'old');
  assert.equal(state.canvas.value.edges[0]!.data.lineType, 'default');
  assert.equal(state.first.data.parameters[0]!.source, 'literal');
  assert.equal(state.second.data.parameters[0]!.source, 'previousNode');
  assert.equal(state.second.data.parameters[0]!.sourceNodeId, 'source');
  const countsResult: {
    history: number;
    changed: number;
  } = state.counts();
  assert.deepEqual(countsResult, { history: 1, changed: 1 });
});
test('a cancelled or occupied reconnection leaves the original edge intact', () => {
  const state = makeGraph('data');
  state.graph.onEdgeUpdate({
    event: {},
    edge: state.oldEdge,
    connection: { source: 'source', sourceHandle: 'data-out', target: 'first', targetHandle: 'param-a' },
  });
  state.canvas.value.edges.push({ id: 'occupied', source: 'third', target: 'second', sourceHandle: 'data-out', targetHandle: 'param-b', data: { semantic: 'data', targetParameterId: 'b' } });
  state.graph.onEdgeUpdate({
    event: {},
    edge: state.oldEdge,
    connection: { source: 'source', sourceHandle: 'data-out', target: 'second', targetHandle: 'param-b' },
  });
  assert.equal(state.canvas.value.edges[0]!.id, 'old');
  assert.equal(state.first.data.parameters[0]!.source, 'previousNode');
  const countsResult1: {
    history: number;
    changed: number;
  } = state.counts();
  assert.deepEqual(countsResult1, { history: 0, changed: 0 });
});
test('reconnecting an execution arrow keeps its branch and line type', () => {
  const state = makeGraph('execution');
  state.graph.onEdgeUpdate({
    event: {},
    edge: state.oldEdge,
    connection: { source: 'source', sourceHandle: 'exec-success', target: 'second', targetHandle: 'exec-in' },
  });
  assert.equal(state.canvas.value.edges[0]!.target, 'second');
  assert.equal(state.canvas.value.edges[0]!.data.branch, 'success');
  assert.equal(state.canvas.value.edges[0]!.data.lineType, 'default');
  const countsResult2: {
    history: number;
    changed: number;
  } = state.counts();
  assert.deepEqual(countsResult2, { history: 1, changed: 1 });
});
