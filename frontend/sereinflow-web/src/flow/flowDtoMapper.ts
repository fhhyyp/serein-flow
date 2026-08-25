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
import type {
  CanvasLifecycle,
  CanvasState,
  ConnectionSemantic,
  FlowEdge,
  FlowNode,
  MethodParameter,
  NodeKind,
  NodeStatus,
  ParameterInputMode,
  ParameterSource,
} from './types'
import { allNodeKinds } from './nodeCatalog.ts'
import { connectionLineStyleFor, defaultConnectionLineTypes, normalizeConnectionLineTypes } from './connectionLine.ts'
import type { WorkspaceSnapshot } from './workspaceHistory'

export interface FlowIdentity {
  id: string
  version: number
}

const lifecycleValues: CanvasLifecycle[] = ['main', 'init', 'loading', 'exit', 'custom']
const nodeKinds: NodeKind[] = [...allNodeKinds]
const nodeStatuses: NodeStatus[] = ['idle', 'running', 'success', 'failed', 'ready', 'active']
const parameterSources: ParameterSource[] = ['literal', 'previousNode', 'projectInput', 'expression']
const parameterInputModes: ParameterInputMode[] = ['connection', 'manual', 'select']

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
    ui: {
      connectionLineTypes: {
        execution: snapshot.connectionLineTypes?.execution ?? defaultConnectionLineTypes.execution,
        data: snapshot.connectionLineTypes?.data ?? defaultConnectionLineTypes.data,
      },
    },
  }
}

export function flowDefinitionToWorkspace(definition: FlowDefinitionDto): WorkspaceSnapshot {
  const canvases = definition.canvases.map(toCanvasState)
  const activeCanvasId = canvases.find((canvas) => canvas.lifecycle === 'main')?.id ?? canvases[0]?.id ?? 'main'
  return {
    canvases,
    activeCanvasId,
    nextNodeNumber: getNextNodeNumber(canvases),
    connectionLineTypes: normalizeConnectionLineTypes(definition.ui?.connectionLineTypes),
  }
}

function toCanvasDto(canvas: CanvasState): CanvasDto {
  return {
    id: canvas.id,
    lifecycle: canvas.lifecycle,
    nodes: canvas.nodes.map(toNodeDto),
    connections: canvas.edges.map(toConnectionDto),
    name: canvas.name,
  }
}

function toNodeDto(node: FlowNode): NodeDto {
  const hasDataOutput = exposesDataOutput(node)
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
      hasDataOutput,
      width: node.width,
      category: node.data.runtime?.category,
      libraryId: node.data.runtime?.libraryId,
      className: node.data.runtime?.className,
      methodName: node.data.runtime?.methodName,
      dllName: node.data.runtime?.dllName,
      dllVersion: node.data.runtime?.dllVersion,
      returnType: node.data.runtime?.returnType,
    },
  }
}

function createPorts(node: FlowNode): NodePortDto[] {
  const ports: NodePortDto[] = []
  if (node.data.kind !== 'trigger') {
    ports.push({ id: 'exec-in', name: 'Execution input', direction: 'input', required: false })
  }
  ports.push({ id: 'exec-out', name: 'Execution output', direction: 'output', required: false })
  if (exposesDataOutput(node)) {
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
      type: parameter.type ?? parameter.valueKind,
      description: parameter.description,
      inputMode: parameter.inputMode,
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
    name: canvas.name?.trim() || undefined,
    nodes: canvas.nodes.map(toFlowNode),
    edges: canvas.connections.map(toFlowEdge),
  }
}

function toFlowNode(node: NodeDto): FlowNode {
  const kind = normalizeNodeKind(node.ui?.kind) ?? fromApiNodeType(node.type)
  const titleKey = node.ui?.titleKey || `node.kind.${kind}`
  const runtime = toRuntimeMetadata(node)
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
      status: normalizeNodeStatus(node.ui?.status),
      hasDataOutput: hasDataOutputFromDto(node),
      parameters: node.parameters.map(toMethodParameter),
      runtime,
    },
  }
}

function toMethodParameter(parameter: NodeParameterDto): MethodParameter {
  const source = isParameterSource(parameter.source) ? parameter.source : 'literal'
  return {
    id: parameter.ui?.id || parameter.name,
    nameKey: parameter.ui?.nameKey || parameter.name,
    valueKind: parameter.ui?.valueKind || 'JSON',
    name: parameter.name,
    type: parameter.ui?.type || parameter.ui?.valueKind || 'JSON',
    description: parameter.ui?.description,
    inputMode: normalizeParameterInputMode(parameter.ui?.inputMode),
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
  const lineType = connectionLineStyleFor(semantic).lineType
  return {
    id: connection.id,
    source: connection.fromNodeId,
    target: connection.toNodeId,
    sourceHandle: connection.fromPortId,
    targetHandle: connection.toPortId,
    type: lineType,
    markerEnd: {
      type: MarkerType.ArrowClosed,
      color: isExecution ? '#0369a1' : '#6d42a5',
      width: 14,
      height: 14,
    },
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
  return type
}

function exposesDataOutput(node: FlowNode): boolean {
  const returnType = node.data.runtime?.returnType?.trim()
  if (returnType) {
    return returnType.toLowerCase() !== 'void'
  }

  return node.data.hasDataOutput
}

function hasDataOutputFromDto(node: NodeDto): boolean {
  const returnType = node.ui?.returnType?.trim()
  if (returnType) {
    return returnType.toLowerCase() !== 'void'
  }

  if (node.ui?.hasDataOutput !== undefined) {
    return node.ui.hasDataOutput
  }

  // Older documents omitted UI metadata. An explicit result port is the
  // strongest signal; an empty port collection falls back to the old default.
  // 旧文档可能没有 UI 元数据；显式结果端口优先级最高，空端口集合回退到旧默认值。
  if (node.ports.length > 0) {
    return node.ports.some((port) => port.id === 'data-out' || port.id === 'result')
  }

  return true
}

function toRuntimeMetadata(node: NodeDto) {
  const ui = node.ui
  if (!ui || [ui.category, ui.libraryId, ui.className, ui.methodName, ui.dllName, ui.dllVersion, ui.returnType].every((value) => value === undefined)) {
    return undefined
  }

  return {
    category: isNodeCategory(ui.category) ? ui.category : undefined,
    libraryId: ui.libraryId,
    className: ui.className,
    methodName: ui.methodName,
    dllName: ui.dllName,
    dllVersion: ui.dllVersion,
    returnType: ui.returnType,
  }
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

function normalizeNodeKind(value: string | undefined): NodeKind | undefined {
  if (!value) {
    return undefined
  }

  if (isNodeKind(value)) {
    return value
  }

  const normalized = value.toLowerCase()
  return nodeKinds.find((kind) => kind.toLowerCase() === normalized)
}

function isNodeStatus(value: string | undefined): value is NodeStatus {
  return value !== undefined && nodeStatuses.includes(value as NodeStatus)
}

function normalizeNodeStatus(value: string | undefined): NodeStatus {
  const normalized = value?.trim().toLowerCase()
  return isNodeStatus(normalized) ? normalized : 'ready'
}

function isNodeCategory(value: string | undefined): value is 'method' | 'basic' {
  return value === 'method' || value === 'basic'
}

function normalizeParameterInputMode(value: string | undefined): ParameterInputMode | undefined {
  const normalized = value?.trim().toLowerCase()
  return normalized !== undefined && parameterInputModes.includes(normalized as ParameterInputMode)
    ? normalized as ParameterInputMode
    : undefined
}

function isParameterSource(value: ApiDataSource): value is ParameterSource {
  return parameterSources.includes(value as ParameterSource)
}
