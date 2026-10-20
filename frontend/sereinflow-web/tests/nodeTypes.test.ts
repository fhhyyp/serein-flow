import assert from 'node:assert/strict';
import test from 'node:test';
import { flowDefinitionToWorkspace, workspaceToFlowDefinition } from '../src/flow/flowDtoMapper.ts';
import { basicNodeKinds, methodNodeKinds, referenceNodeKinds } from '../src/flow/nodeCatalog.ts';
import { getConnectionSeats } from '../src/flow/connectionSeats.ts';
import { initialLibraryParameterLiteral } from '../src/flow/libraryParameterDefaults.ts';
import type { FlowNode, NodeKind } from '../src/flow/types.ts';
import type { WorkspaceSnapshot } from '../src/flow/workspaceHistory.ts';
const identity = { id: '9d922e9d-a0f0-48b4-babb-20f7e4dd982e', version: 1 };
function node(kind: NodeKind, runtime?: FlowNode['data']['runtime']): FlowNode {
  return {
    id: `${kind}-1`,
    type: 'workflow',
    position: { x: 64, y: 96 },
    data: {
      kind,
      titleKey: `node.kind.${kind}`,
      subtitleKey: `node.kind.${kind}`,
      status: 'idle',
      hasDataOutput: true,
      parameters: [
        {
          id: 'input',
          nameKey: 'parameter.input',
          valueKind: 'string',
          type: 'System.String',
          inputMode: 'connection',
          source: 'literal',
        },
      ],
      runtime,
    },
  };
}
function workspace(nodes: FlowNode[]): WorkspaceSnapshot {
  return {
    canvases: [{ id: 'main', nameKey: 'canvas.main', lifecycle: 'main', nodes, edges: [] }],
    activeCanvasId: 'main',
    nextNodeNumber: nodes.length + 1,
  };
}
test('the catalog contract exposes only the four runtime node kinds', () => {
  assert.deepEqual(referenceNodeKinds, ['action', 'flipflop', 'script', 'flowCall']);
  assert.deepEqual(methodNodeKinds, ['action', 'flipflop']);
  assert.deepEqual(basicNodeKinds, ['script', 'flowCall']);
});
test('every runtime node type round trips without degrading to action', () => {
  const kinds: NodeKind[] = [...referenceNodeKinds];
  const mapResult: FlowNode[] = kinds.map((kind) => {
    const nodeResult: FlowNode = node(kind);
    return nodeResult;
  });
  const workspaceResult: WorkspaceSnapshot = workspace(mapResult);
  const definition = workspaceToFlowDefinition(workspaceResult, identity);
  const mapResult1 = definition.canvases[0]?.nodes.map((item) => item.type);
  assert.deepEqual(mapResult1, kinds);
  const restored = flowDefinitionToWorkspace(definition);
  const mapResult2 = restored.canvases[0]?.nodes.map((item) => item.data.kind);
  assert.deepEqual(mapResult2, kinds);
});
test('every node exposes a control input and three execution outputs', () => {
  const evaluatedValue: FlowNode[] = [node('flipflop'), node('script')];
  const workspaceResult1: WorkspaceSnapshot = workspace(evaluatedValue);
  const definition = workspaceToFlowDefinition(workspaceResult1, identity);
  const flipflop = definition.canvases[0]?.nodes.find((item) => item.type === 'flipflop');
  const script = definition.canvases[0]?.nodes.find((item) => item.type === 'script');
  const someResult = flipflop?.ports.some((port) => port.id === 'exec-in');
  assert.ok(someResult);
  const someResult1 = flipflop?.ports.some((port) => port.id === 'exec-success');
  assert.ok(someResult1);
  const someResult2 = flipflop?.ports.some((port) => port.id === 'exec-failure');
  assert.ok(someResult2);
  const someResult3 = flipflop?.ports.some((port) => port.id === 'exec-error');
  assert.ok(someResult3);
  const someResult4 = script?.ports.some((port) => port.id === 'exec-error');
  assert.ok(someResult4);
});
test('return type controls result seats and parameter metadata survives persistence', () => {
  const voidNode = node('flowCall', {
    category: 'basic',
    className: 'Flows',
    methodName: 'Call',
    returnType: 'void',
  });
  const valueNode = node('action', {
    category: 'basic',
    className: 'Expressions',
    methodName: 'Add',
    returnType: 'System.Int32',
  });
  const workspaceResult2: WorkspaceSnapshot = workspace([voidNode, valueNode]);
  const definition = workspaceToFlowDefinition(workspaceResult2, identity);
  const flowCall = definition.canvases[0]?.nodes.find((item) => item.type === 'flowCall');
  const expOp = definition.canvases[0]?.nodes.find((item) => item.type === 'action');
  const someResult5 = flowCall?.ports.some((port) => port.id === 'data-out');
  assert.equal(someResult5, false);
  const someResult6 = expOp?.ports.some((port) => port.id === 'data-out');
  assert.equal(someResult6, true);
  assert.equal(expOp?.ui?.className, 'Expressions');
  assert.equal(expOp?.parameters[0]?.ui?.type, 'System.String');
  assert.equal(expOp?.parameters[0]?.ui?.inputMode, 'connection');
  const restored = flowDefinitionToWorkspace(definition);
  const restoredCall = restored.canvases[0]?.nodes.find((item) => item.data.kind === 'flowCall');
  const restoredOp = restored.canvases[0]?.nodes.find((item) => item.data.kind === 'action');
  const getConnectionSeatsResult = getConnectionSeats(restoredCall!.data);
  const someResult7 = getConnectionSeatsResult.some((seat) => seat.id === 'data-out');
  assert.equal(someResult7, false);
  const getConnectionSeatsResult1 = getConnectionSeats(restoredOp!.data);
  const someResult8 = getConnectionSeatsResult1.some((seat) => seat.id === 'data-out');
  assert.equal(someResult8, true);
  assert.equal(restoredOp?.data.runtime?.returnType, 'System.Int32');
  assert.equal(restoredOp?.data.parameters[0]?.inputMode, 'connection');
});
test('library node creation initializes literal parameters from reflected defaults', () => {
  const initialLibraryParameterLiteralResult = initialLibraryParameterLiteral('3');
  assert.equal(initialLibraryParameterLiteralResult, '3');
  const initialLibraryParameterLiteralResult1 = initialLibraryParameterLiteral('Automatic');
  assert.equal(initialLibraryParameterLiteralResult1, 'Automatic');
  const initialLibraryParameterLiteralResult2 = initialLibraryParameterLiteral(null);
  assert.equal(initialLibraryParameterLiteralResult2, '');
  const initialLibraryParameterLiteralResult3 = initialLibraryParameterLiteral(undefined);
  assert.equal(initialLibraryParameterLiteralResult3, '');
});
