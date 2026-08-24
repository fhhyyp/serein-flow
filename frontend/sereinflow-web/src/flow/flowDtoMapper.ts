import { MarkerType } from '@vue-flow/core'
import type {
  ApiCanvasLifecycle,
  ApiDataSource,
  ApiNodeType,
  CanvasDto,
  ConnectionDto,
  FlowDefinitionDto,
  NodeDto,
  NodeParameterDto,
  NodePortDto,
} from '../api/flowApi'
import type { CanvasLifecycle, CanvasState, ConnectionSemantic, FlowEdge, FlowNode, MethodParameter, NodeKind, NodeStatus, ParameterSource } from './types'
import type { WorkspaceSnapshot } from './workspaceHistory'

export interface FlowIdentity {
  id: string
  version: number
}

const lifecycleValues: CanvasLifecycle[] = ['main', 'init', 'loading', 'exit']
const nodeKinds: NodeKind[] = ['trigger', 'script', 'condition', 'action']
const nodeStatuses: NodeStatus[] = ['ready', 'active', 'success']
const parameterSources: ParameterSource[] = ['literal', 'previousNode', 'projectInput', 'expression']

export function workspaceToFlowDefinition(snapshot: WorkspaceSnapshot, identity: FlowIdentity): FlowDefinitionDto {
  const mainCanvas = snapshot.canvases.find((canvas) => canvas.lifecycle === 'main') ?? snapshot.canvases[0]
  const entryNodeId = mainCanvas?.nodes[0]?.id ?? ''
  return {
    id: identity.id,
    schemaVersion: 1,
    version: identity.version,
    canvases: snapshot.canvases.map(toCanvasDto),
    entryNodeId,
    checksum: '',
  }
}

export function flowDefinitionToWorkspace(definition: FlowDefinitionDto): WorkspaceSnapshot {
  const canvases = definition.canvases.map(toCanvasState)
  const activeCanvasId = canvases.find((canvas) => canvas.lifecycle === 'main')?.id ?? canvases[0]?.id ?? 'main'
  return {
    canvases,
    activeCanvasId,
    nextNodeNumber: getNextNodeNumber(canvases),
  }
}

function toCanvasDto(canvas: CanvasState): CanvasDto {
  return {
    id: canvas.id,
    lifecycle: canvas.lifecycle,
    nodes: canvas.nodes.map(toNodeDto),
    connections: canvas.edges.map(toConnectionDto),
  }
}

function toNodeDto(node: FlowNode): NodeDto {
  return {
    id: node.id,
    type: toApiNodeType(node.data.kind),
    displayName: node.data.displayName?.trim() || node.data.titleKey,
    x: node.position.x,
    y: node.position.y,
    ports: createPorts(node),
    parameters: node.data.parameters.map(toParameterDto),
    script: null,
    ui: {
      kind: node.data.kind,
      titleKey: node.data.titleKey,
      subtitleKey: node.data.subtitleKey,
      description: node.data.description,
      status: node.data.status,
      hasDataOutput: node.data.hasDataOutput,
      width: node.width,
    },
  }
}

function createPorts(node: FlowNode): NodePortDto[] {
  const ports: NodePortDto[] = []
  if (node.data.kind !== 'trigger') {
    ports.push({ id: 'exec-in', name: 'Execution input', direction: 'input', required: false })
  }
  ports.push({ id: 'exec-out', name: 'Execution output', direction: 'output', required: false })
  if (node.data.hasDataOutput) {
    ports.push({ id: 'data-out', name: 'Data output', direction: 'output', required: false })
  }
  for (const parameter of node.data.parameters) {
    ports.push({ id: `param-${parameter.id}`, name: parameter.id, direction: 'input', required: false })
  }
  return ports
}

function toParameterDto(parameter: MethodParameter): NodeParameterDto {
  return {
    name: parameter.id,
    valueJson: parameter.source === 'literal' ? parameter.literalValue : undefined,
    source: parameter.source,
    required: false,
    ui: {
      id: parameter.id,
      nameKey: parameter.nameKey,
      valueKind: parameter.valueKind,
      literalValue: parameter.literalValue,
      projectInputKey: parameter.projectInputKey,
      expression: parameter.expression,
      sourceNodeId: parameter.sourceNodeId,
      sourcePortId: parameter.sourcePortId,
    },
  }
}

function toConnectionDto(edge: FlowEdge): ConnectionDto {
  return {
    id: edge.id,
    fromNodeId: edge.source,
    fromPortId: edge.sourceHandle ?? (edge.data.semantic === 'execution' ? 'exec-out' : 'data-out'),
    toNodeId: edge.target,
    toPortId: edge.targetHandle ?? (edge.data.semantic === 'execution' ? 'exec-in' : `param-${edge.data.targetParameterId ?? 'input'}`),
    kind: edge.data.semantic === 'execution' ? 'execution' : 'data',
    branch: edge.data.semantic === 'execution' ? 'success' : undefined,
    dataSource: edge.data.semantic === 'data' ? 'previousNode' : undefined,
    priority: 0,
  }
}

function toCanvasState(canvas: CanvasDto): CanvasState {
  const lifecycle = isLifecycle(canvas.lifecycle) ? canvas.lifecycle : 'main'
  return {
    id: canvas.id,
    lifecycle,
    nameKey: `canvas.${lifecycle}`,
    nodes: canvas.nodes.map(toFlowNode),
    edges: canvas.connections.map(toFlowEdge),
  }
}

function toFlowNode(node: NodeDto): FlowNode {
  const kind = isNodeKind(node.ui?.kind) ? node.ui.kind : fromApiNodeType(node.type)
  const titleKey = node.ui?.titleKey || `node.kind.${kind}`
  return {
    id: node.id,
    type: 'workflow',
    position: { x: node.x, y: node.y },
    width: node.ui?.width,
    data: {
      kind,
      titleKey,
      subtitleKey: node.ui?.subtitleKey || titleKey,
      displayName: node.displayName === titleKey ? undefined : node.displayName,
      description: node.ui?.description,
      status: isNodeStatus(node.ui?.status) ? node.ui.status : 'ready',
      hasDataOutput: node.ui?.hasDataOutput ?? true,
      parameters: node.parameters.map(toMethodParameter),
    },
  }
}

function toMethodParameter(parameter: NodeParameterDto): MethodParameter {
  const source = isParameterSource(parameter.source) ? parameter.source : 'literal'
  return {
    id: parameter.ui?.id || parameter.name,
    nameKey: parameter.ui?.nameKey || parameter.name,
    valueKind: parameter.ui?.valueKind || 'JSON',
    source,
    literalValue: parameter.ui?.literalValue ?? parameter.valueJson,
    projectInputKey: parameter.ui?.projectInputKey,
    expression: parameter.ui?.expression,
    sourceNodeId: parameter.ui?.sourceNodeId,
    sourcePortId: parameter.ui?.sourcePortId,
  }
}

function toFlowEdge(connection: ConnectionDto): FlowEdge {
  const semantic: ConnectionSemantic = connection.kind === 'execution' ? 'execution' : 'data'
  const isExecution = semantic === 'execution'
  return {
    id: connection.id,
    source: connection.fromNodeId,
    target: connection.toNodeId,
    sourceHandle: connection.fromPortId,
    targetHandle: connection.toPortId,
    type: 'smoothstep',
    markerEnd: {
      type: MarkerType.ArrowClosed,
      color: isExecution ? '#0369a1' : '#6d42a5',
      width: 14,
      height: 14,
    },
    label: isExecution ? 'Flow' : 'Value',
    labelShowBg: true,
    labelBgPadding: [3, 5],
    labelBgBorderRadius: 2,
    data: {
      semantic,
      targetParameterId: isExecution ? undefined : connection.toPortId.replace(/^param-/, ''),
    },
    class: isExecution ? 'edge-execution' : 'edge-data',
    ariaLabel: isExecution ? 'Flow scheduling connection' : 'Parameter source connection',
  }
}

function toApiNodeType(kind: NodeKind): ApiNodeType {
  return kind
}

function fromApiNodeType(type: ApiNodeType): NodeKind {
  return type === 'script' || type === 'condition' || type === 'trigger' ? type : 'action'
}

function getNextNodeNumber(canvases: CanvasState[]): number {
  const highest = canvases.flatMap((canvas) => canvas.nodes)
    .map((node) => Number(node.id.match(/-(\d+)$/)?.[1] ?? 0))
    .reduce((maximum, value) => Math.max(maximum, value), 0)
  return highest + 1
}

function isLifecycle(value: ApiCanvasLifecycle): value is CanvasLifecycle {
  return lifecycleValues.includes(value as CanvasLifecycle)
}

function isNodeKind(value: string | undefined): value is NodeKind {
  return value !== undefined && nodeKinds.includes(value as NodeKind)
}

function isNodeStatus(value: string | undefined): value is NodeStatus {
  return value !== undefined && nodeStatuses.includes(value as NodeStatus)
}

function isParameterSource(value: ApiDataSource): value is ParameterSource {
  return parameterSources.includes(value as ParameterSource)
}
