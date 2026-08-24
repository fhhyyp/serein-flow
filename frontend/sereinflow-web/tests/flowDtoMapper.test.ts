import assert from 'node:assert/strict'
import test from 'node:test'
import { createInitialCanvases } from '../src/flow/initialCanvases.ts'
import { flowDefinitionToWorkspace, workspaceToFlowDefinition } from '../src/flow/flowDtoMapper.ts'
import type { WorkspaceSnapshot } from '../src/flow/workspaceHistory.ts'

test('the workbench DTO round trip retains user-created multi-canvas execution and data connections', () => {
  const original: WorkspaceSnapshot = {
    canvases: [
      {
        id: 'main',
        nameKey: 'canvas.main',
        lifecycle: 'main',
        nodes: [
          {
            id: 'trigger',
            type: 'workflow',
            position: { x: 50, y: 180 },
            data: {
              kind: 'trigger',
              titleKey: 'node.httpTrigger',
              subtitleKey: 'node.triggerSubtitle',
              status: 'ready',
              hasDataOutput: true,
              parameters: [],
            },
          },
          {
            id: 'normalize',
            type: 'workflow',
            position: { x: 345, y: 180 },
            data: {
              kind: 'script',
              titleKey: 'node.normalizeOrder',
              subtitleKey: 'node.scriptSubtitle',
              status: 'ready',
              hasDataOutput: true,
              parameters: [
                {
                  id: 'payload',
                  nameKey: 'parameter.payload',
                  valueKind: 'JSON',
                  source: 'previousNode',
                  sourceNodeId: 'trigger',
                  sourcePortId: 'data-out',
                },
              ],
            },
          },
        ],
        edges: [
          {
            id: 'exec-trigger-normalize',
            source: 'trigger',
            target: 'normalize',
            sourceHandle: 'exec-out',
            targetHandle: 'exec-in',
            data: { semantic: 'execution' },
          },
          {
            id: 'data-trigger-normalize-payload',
            source: 'trigger',
            target: 'normalize',
            sourceHandle: 'data-out',
            targetHandle: 'param-payload',
            data: { semantic: 'data', targetParameterId: 'payload' },
          },
        ],
      },
      {
        id: 'init',
        nameKey: 'canvas.init',
        lifecycle: 'init',
        nodes: [
          {
            id: 'prepare',
            type: 'workflow',
            position: { x: 50, y: 180 },
            data: {
              kind: 'action',
              titleKey: 'node.prepareWorkspace',
              subtitleKey: 'node.initializationSubtitle',
              status: 'ready',
              hasDataOutput: true,
              parameters: [],
            },
          },
          {
            id: 'catalog',
            type: 'workflow',
            position: { x: 345, y: 180 },
            data: {
              kind: 'action',
              titleKey: 'node.loadCatalog',
              subtitleKey: 'node.loadingSubtitle',
              status: 'ready',
              hasDataOutput: true,
              parameters: [],
            },
          },
        ],
        edges: [
          {
            id: 'exec-prepare-catalog',
            source: 'prepare',
            target: 'catalog',
            sourceHandle: 'exec-out',
            targetHandle: 'exec-in',
            data: { semantic: 'execution' },
          },
        ],
      },
    ],
    activeCanvasId: 'init',
    nextNodeNumber: 4,
    connectionLineTypes: {
      execution: 'default',
      data: 'smoothstep',
    },
  }

  const definition = workspaceToFlowDefinition(original, {
    id: '9d922e9d-a0f0-48b4-babb-20f7e4dd982e',
    version: 1,
  })
  const restored = flowDefinitionToWorkspace(definition)
  const main = restored.canvases.find((canvas) => canvas.id === 'main')
  const init = restored.canvases.find((canvas) => canvas.id === 'init')

  assert.equal(definition.canvases.find((canvas) => canvas.id === 'main')?.connections.length, 2)
  assert.equal(definition.ui?.connectionLineTypes?.execution, 'default')
  assert.equal(definition.ui?.connectionLineTypes?.data, 'smoothstep')
  assert.equal(main?.edges.length, 2)
  assert.equal(main?.edges.filter((edge) => edge.data.semantic === 'execution').length, 1)
  assert.equal(main?.edges.filter((edge) => edge.data.semantic === 'data').length, 1)
  assert.equal(init?.edges[0]?.id, 'exec-prepare-catalog')
  assert.equal(main?.nodes.find((node) => node.id === 'normalize')?.data.parameters[0]?.sourceNodeId, 'trigger')
})

test('the empty default workspace round trips without an artificial entry node', () => {
  const original: WorkspaceSnapshot = {
    canvases: createInitialCanvases(),
    activeCanvasId: 'main',
    nextNodeNumber: 1,
  }

  const definition = workspaceToFlowDefinition(original, {
    id: '9d922e9d-a0f0-48b4-babb-20f7e4dd982e',
    version: 1,
  })
  const restored = flowDefinitionToWorkspace(definition)

  assert.equal(definition.entryNodeId, '')
  assert.equal(definition.canvases.length, 1)
  assert.deepEqual(definition.canvases[0]?.nodes, [])
  assert.deepEqual(definition.canvases[0]?.connections, [])
  assert.deepEqual(restored.canvases[0]?.nodes, [])
  assert.deepEqual(restored.canvases[0]?.edges, [])
  assert.deepEqual(restored.connectionLineTypes, { execution: 'smoothstep', data: 'default' })
})

test('custom canvas names and lifecycle survive DTO round trips', () => {
  const main = createInitialCanvases()[0]!
  const custom = {
    ...main,
    id: 'custom-1',
    nameKey: 'canvas.custom',
    name: '审计流程',
    lifecycle: 'custom' as const,
  }
  const snapshot: WorkspaceSnapshot = {
    canvases: [main, custom],
    activeCanvasId: custom.id,
    nextNodeNumber: 1,
    projectName: '运行编排',
  }

  const definition = workspaceToFlowDefinition(snapshot, {
    id: '9d922e9d-a0f0-48b4-babb-20f7e4dd982e',
    version: 1,
  })
  const restored = flowDefinitionToWorkspace(definition)
  const restoredCustom = restored.canvases.find((canvas) => canvas.id === custom.id)

  assert.equal(definition.canvases[1]?.lifecycle, 'custom')
  assert.equal(definition.canvases[1]?.name, '审计流程')
  assert.equal(restoredCustom?.lifecycle, 'custom')
  assert.equal(restoredCustom?.name, '审计流程')
  assert.equal(restored.activeCanvasId, 'main')
})
