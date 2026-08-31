import assert from 'node:assert/strict'
import test from 'node:test'
import { createInitialCanvases } from '../src/flow/initialCanvases.ts'
import { flowDefinitionToWorkspace, workspaceToFlowDefinition } from '../src/flow/flowDtoMapper.ts'
import type { FlowDefinitionDto } from '../src/api/flowApi.ts'
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
            id: 'flipflop',
            type: 'workflow',
            position: { x: 50, y: 180 },
            data: {
              kind: 'flipflop',
              titleKey: 'node.kind.flipflop',
              subtitleKey: 'node.kind.flipflop',
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
                  sourceNodeId: 'flipflop',
                  sourcePortId: 'data-out',
                },
              ],
            },
          },
        ],
        edges: [
          {
            id: 'exec-flipflop-normalize',
            source: 'flipflop',
            target: 'normalize',
            sourceHandle: 'exec-success',
            targetHandle: 'exec-in',
            data: { semantic: 'execution', branch: 'success' },
          },
          {
            id: 'data-flipflop-normalize-payload',
            source: 'flipflop',
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
            sourceHandle: 'exec-success',
            targetHandle: 'exec-in',
            data: { semantic: 'execution', branch: 'success' },
          },
        ],
      },
    ],
    activeCanvasId: 'init',
    nextNodeNumber: 4,
    entryNodeId: 'prepare',
    connectionLineTypes: {
      execution: 'default',
      data: 'smoothstep',
    },
    canvasFocusSettings: {
      enabled: true,
      parameterSources: true,
      parameterConsumers: false,
      callers: false,
      callees: true,
    },
  }

  const definition = workspaceToFlowDefinition(original, {
    id: '9d922e9d-a0f0-48b4-babb-20f7e4dd982e',
    version: 1,
  })
  const restored = flowDefinitionToWorkspace(definition)
  const main = restored.canvases.find((canvas) => canvas.id === 'main')
  const init = restored.canvases.find((canvas) => canvas.id === 'init')

  assert.equal(definition.schemaVersion, 5)
  assert.equal(definition.entryNodeId, 'prepare')
  assert.equal(definition.canvases.find((canvas) => canvas.id === 'main')?.connections.length, 2)
  assert.equal(definition.ui?.connectionLineTypes?.execution, 'default')
  assert.equal(definition.ui?.connectionLineTypes?.data, 'smoothstep')
  assert.equal(definition.ui?.canvasFocusSettings?.parameterConsumers, false)
  assert.equal(definition.ui?.canvasFocusSettings?.callers, false)
  assert.equal(main?.edges.length, 2)
  assert.equal(main?.edges.filter((edge) => edge.data.semantic === 'execution').length, 1)
  assert.equal(main?.edges.filter((edge) => edge.data.semantic === 'data').length, 1)
  assert.equal(init?.edges[0]?.id, 'exec-prepare-catalog')
  assert.equal(main?.nodes.find((node) => node.id === 'normalize')?.data.parameters[0]?.sourceNodeId, 'flipflop')
  assert.equal(restored.entryNodeId, 'prepare')
  assert.deepEqual(restored.canvasFocusSettings, original.canvasFocusSettings)
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
  assert.deepEqual(restored.connectionLineTypes, { execution: 'smoothstep', data: 'smoothstep' })
  assert.deepEqual(restored.canvasFocusSettings, {
    enabled: true,
    parameterSources: true,
    parameterConsumers: true,
    callers: true,
    callees: true,
  })
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

test('restored parameter connections target the rendered parameter handle', () => {
  const definition: FlowDefinitionDto = {
    id: 'flow',
    schemaVersion: 3,
    version: 1,
    entryNodeId: 'source',
    checksum: '',
    canvases: [{
      id: 'main',
      lifecycle: 'main',
      nodes: [
        { id: 'source', type: 'action', displayName: 'Source', x: 0, y: 0, ports: [{ id: 'data-out', name: 'Data', direction: 'output', required: false }], parameters: [], script: null },
        { id: 'target', type: 'action', displayName: 'Target', x: 320, y: 0, ports: [{ id: 'param-param-1', name: 'param-1', direction: 'input', required: false }], parameters: [{ name: 'param-1', source: 'previousNode', required: false, ui: { id: 'param-1', nameKey: 'value', valueKind: 'JSON' } }], script: null },
      ],
      connections: [{ id: 'data-1', fromNodeId: 'source', fromPortId: 'data-out', toNodeId: 'target', toPortId: 'param-1', kind: 'data', dataSource: 'previousNode', priority: 0 }],
    }],
  }

  const restored = flowDefinitionToWorkspace(definition)
  const edge = restored.canvases[0]?.edges[0]
  const parameter = restored.canvases[0]?.nodes.find((node) => node.id === 'target')?.data.parameters[0]

  assert.equal(parameter?.id, '1')
  assert.equal(edge?.targetHandle, 'param-1')
  assert.equal(edge?.data.targetParameterId, '1')
})

test('library method parameters keep reflected names separate from connector ids', () => {
  const snapshot: WorkspaceSnapshot = {
    canvases: [{
      id: 'main',
      nameKey: 'canvas.main',
      lifecycle: 'main',
      nodes: [{
        id: 'add',
        type: 'workflow',
        position: { x: 0, y: 0 },
        data: {
          kind: 'action',
          titleKey: 'node.catalogMethod',
          subtitleKey: 'node.catalogSubtitle',
          status: 'ready',
          hasDataOutput: true,
          runtime: { category: 'method', methodName: 'Add' },
          parameters: [
            { id: '1', nameKey: 'left', name: 'left', valueKind: 'System.Int32', required: true, source: 'literal', literalValue: '10' },
            { id: '2', nameKey: 'right', name: 'right', valueKind: 'System.Int32', required: true, source: 'literal', literalValue: '20' },
          ],
        },
      }],
      edges: [],
    }],
    activeCanvasId: 'main',
    nextNodeNumber: 2,
  }

  const definition = workspaceToFlowDefinition(snapshot, { id: 'flow', version: 1 })
  const parameters = definition.canvases[0]?.nodes[0]?.parameters ?? []
  assert.deepEqual(parameters.map((parameter) => parameter.name), ['left', 'right'])
  assert.deepEqual(parameters.map((parameter) => parameter.ui?.id), ['1', '2'])
  assert.deepEqual(parameters.map((parameter) => parameter.required), [true, true])

  const restored = flowDefinitionToWorkspace(definition)
  assert.deepEqual(restored.canvases[0]?.nodes[0]?.data.parameters.map((parameter) => parameter.name), ['left', 'right'])

  const legacyDefinition = {
    ...definition,
    canvases: definition.canvases.map((canvas) => ({
      ...canvas,
      nodes: canvas.nodes.map((node) => ({
        ...node,
        parameters: node.parameters.map((parameter) => ({ ...parameter, name: parameter.ui?.id ?? parameter.name })),
      })),
    })),
  }
  const migrated = flowDefinitionToWorkspace(legacyDefinition)
  assert.deepEqual(migrated.canvases[0]?.nodes[0]?.data.parameters.map((parameter) => parameter.name), ['left', 'right'])
})

test('enum parameter metadata remains available after a workspace DTO round trip', () => {
  const snapshot: WorkspaceSnapshot = {
    canvases: [{
      id: 'main',
      nameKey: 'canvas.main',
      lifecycle: 'main',
      nodes: [{
        id: 'configure-mode',
        type: 'workflow',
        position: { x: 0, y: 0 },
        data: {
          kind: 'action',
          titleKey: 'node.catalogMethod',
          subtitleKey: 'node.catalogSubtitle',
          status: 'ready',
          hasDataOutput: true,
          runtime: {
            flowLibraryName: '生产线设备与质量数据示例库',
          },
          parameters: [{
            id: 'mode',
            nameKey: 'parameter.mode',
            name: 'mode',
            valueKind: 'DeviceMode',
            required: true,
            source: 'literal',
            literalValue: 'Manual',
            enumMetadata: {
              typeName: 'Example.DeviceMode',
              isFlags: false,
              underlyingType: 'System.Int32',
              options: [
                { name: 'Automatic', numericValue: '0' },
                { name: 'Manual', numericValue: '1' },
              ],
            },
          }],
        },
      }],
      edges: [],
    }],
    activeCanvasId: 'main',
    nextNodeNumber: 2,
  }

  const definition = workspaceToFlowDefinition(snapshot, { id: 'flow', version: 1 })
  const restored = flowDefinitionToWorkspace(definition)
  const parameter = restored.canvases[0]?.nodes[0]?.data.parameters[0]

  assert.equal(definition.canvases[0]?.nodes[0]?.ui?.flowLibraryName, '生产线设备与质量数据示例库')
  assert.equal(restored.canvases[0]?.nodes[0]?.data.runtime?.flowLibraryName, '生产线设备与质量数据示例库')
  assert.deepEqual(definition.canvases[0]?.nodes[0]?.parameters[0]?.ui?.enumMetadata, snapshot.canvases[0]?.nodes[0]?.data.parameters[0]?.enumMetadata)
  assert.deepEqual(parameter?.enumMetadata, snapshot.canvases[0]?.nodes[0]?.data.parameters[0]?.enumMetadata)
})
