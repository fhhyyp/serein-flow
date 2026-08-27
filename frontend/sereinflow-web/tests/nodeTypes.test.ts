import assert from 'node:assert/strict'
import test from 'node:test'
import { flowDefinitionToWorkspace, workspaceToFlowDefinition } from '../src/flow/flowDtoMapper.ts'
import { basicNodeKinds, methodNodeKinds, referenceNodeKinds } from '../src/flow/nodeCatalog.ts'
import { getConnectionSeats } from '../src/flow/connectionSeats.ts'
import type { FlowNode, NodeKind } from '../src/flow/types.ts'
import type { WorkspaceSnapshot } from '../src/flow/workspaceHistory.ts'

const identity = { id: '9d922e9d-a0f0-48b4-babb-20f7e4dd982e', version: 1 }

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
  }
}

function workspace(nodes: FlowNode[]): WorkspaceSnapshot {
  return {
    canvases: [{ id: 'main', nameKey: 'canvas.main', lifecycle: 'main', nodes, edges: [] }],
    activeCanvasId: 'main',
    nextNodeNumber: nodes.length + 1,
  }
}

test('the catalog contract exposes only the four runtime node kinds', () => {
  assert.deepEqual(referenceNodeKinds, ['action', 'flipflop', 'script', 'flowCall'])
  assert.deepEqual(methodNodeKinds, ['action', 'flipflop'])
  assert.deepEqual(basicNodeKinds, ['script', 'flowCall'])
})

test('every runtime node type round trips without degrading to action', () => {
  const kinds: NodeKind[] = [...referenceNodeKinds]
  const definition = workspaceToFlowDefinition(workspace(kinds.map((kind) => node(kind))), identity)
  assert.deepEqual(definition.canvases[0]?.nodes.map((item) => item.type), kinds)

  const restored = flowDefinitionToWorkspace(definition)
  assert.deepEqual(restored.canvases[0]?.nodes.map((item) => item.data.kind), kinds)
})

test('every node exposes a control input and three execution outputs', () => {
  const definition = workspaceToFlowDefinition(workspace([node('flipflop'), node('script')]), identity)
  const flipflop = definition.canvases[0]?.nodes.find((item) => item.type === 'flipflop')
  const script = definition.canvases[0]?.nodes.find((item) => item.type === 'script')

  assert.ok(flipflop?.ports.some((port) => port.id === 'exec-in'))
  assert.ok(flipflop?.ports.some((port) => port.id === 'exec-success'))
  assert.ok(flipflop?.ports.some((port) => port.id === 'exec-failure'))
  assert.ok(flipflop?.ports.some((port) => port.id === 'exec-error'))
  assert.ok(script?.ports.some((port) => port.id === 'exec-error'))
})

test('return type controls result seats and parameter metadata survives persistence', () => {
  const voidNode = node('flowCall', {
    category: 'basic',
    className: 'Flows',
    methodName: 'Call',
    returnType: 'void',
  })
  const valueNode = node('action', {
    category: 'basic',
    className: 'Expressions',
    methodName: 'Add',
    returnType: 'System.Int32',
  })
  const definition = workspaceToFlowDefinition(workspace([voidNode, valueNode]), identity)
  const flowCall = definition.canvases[0]?.nodes.find((item) => item.type === 'flowCall')
  const expOp = definition.canvases[0]?.nodes.find((item) => item.type === 'action')

  assert.equal(flowCall?.ports.some((port) => port.id === 'data-out'), false)
  assert.equal(expOp?.ports.some((port) => port.id === 'data-out'), true)
  assert.equal(expOp?.ui?.className, 'Expressions')
  assert.equal(expOp?.parameters[0]?.ui?.type, 'System.String')
  assert.equal(expOp?.parameters[0]?.ui?.inputMode, 'connection')

  const restored = flowDefinitionToWorkspace(definition)
  const restoredCall = restored.canvases[0]?.nodes.find((item) => item.data.kind === 'flowCall')
  const restoredOp = restored.canvases[0]?.nodes.find((item) => item.data.kind === 'action')
  assert.equal(getConnectionSeats(restoredCall!.data).some((seat) => seat.id === 'data-out'), false)
  assert.equal(getConnectionSeats(restoredOp!.data).some((seat) => seat.id === 'data-out'), true)
  assert.equal(restoredOp?.data.runtime?.returnType, 'System.Int32')
  assert.equal(restoredOp?.data.parameters[0]?.inputMode, 'connection')
})
