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
  ScriptNodeDataDto,
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
  ScriptNodeData,
} from './types'
import { allNodeKinds } from './nodeCatalog.ts'
import { connectionLineStyleFor, defaultConnectionLineTypes, normalizeConnectionLineTypes } from './connectionLine.ts'
import { canonicalParameterId, executionBranchFromHandle, parameterHandleFor } from './connectionSeats.ts'
import type { WorkspaceSnapshot } from './workspaceHistory'

export interface FlowIdentity {
  id: string
  version: number
}

const lifecycleValues: CanvasLifecycle[] = ['main', 'init', 'loading', 'exit', 'custom']
const nodeKinds: NodeKind[] = [...allNodeKinds]
const nodeStatuses: NodeStatus[] = ['idle', 'running', 'success', 'failed', 'error', 'ready', 'active']
const parameterSources: ParameterSource[] = ['literal', 'previousNode', 'projectInput', 'expression']
const parameterInputModes: ParameterInputMode[] = ['connection', 'manual', 'select']

export function workspaceToFlowDefinition(snapshot: WorkspaceSnapshot, identity: FlowIdentity): FlowDefinitionDto {
  return {
    id: identity.id,
    schemaVersion: 5,
    version: identity.version,
    canvases: snapshot.canvases.map(toCanvasDto),
    // Entry selection is an explicit part of the workspace. Inferring it from
    // node order makes a canvas reorder change runtime behavior.
    // 入口选择是工作区的显式状态，不能从节点排列顺序推导，否则节点排序会改变运行行为。
    entryNodeId: snapshot.entryNodeId ?? '',
    checksum: '',
    runPolicy: snapshot.runPolicy ?? { concurrencyMode: 'parallel' },
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
    entryNodeId: definition.entryNodeId,
    connectionLineTypes: normalizeConnectionLineTypes(definition.ui?.connectionLineTypes),
    runPolicy: definition.runPolicy ?? { concurrencyMode: 'parallel' },
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
    script: node.data.script ? toScriptDto(node.data.script) : null,
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
      targetNodeId: node.data.runtime?.targetNodeId,
      targetFlowId: node.data.runtime?.targetFlowId,
      isAwaitable: node.data.runtime?.isAwaitable,
      staticReturnType: node.data.runtime?.staticReturnType,
      isDynamicReturnType: node.data.runtime?.isDynamicReturnType,
      targetCanvasId: node.data.runtime?.targetCanvasId,
      isPublic: node.data.runtime?.isPublic,
      flowCallParameterBindings: node.data.runtime?.flowCallParameterBindings,
    },
  }
}

function createPorts(node: FlowNode): NodePortDto[] {
  const ports: NodePortDto[] = []
  ports.push({ id: 'exec-in', name: 'Execution input', direction: 'input', required: false })
  ports.push({ id: 'exec-success', name: 'Success', direction: 'output', required: false })
  ports.push({ id: 'exec-failure', name: 'Failure', direction: 'output', required: false })
  ports.push({ id: 'exec-error', name: 'Error', direction: 'output', required: false })
  if (exposesDataOutput(node)) {
    ports.push({ id: 'data-out', name: 'Data output', direction: 'output', required: false })
  }
  for (const parameter of node.data.parameters) {
    ports.push({ id: parameterHandleFor(parameter.id), name: parameter.id, direction: 'input', required: false })
  }
  return ports
}

function toParameterDto(parameter: MethodParameter): NodeParameterDto {
  return {
    // The API/Worker name must remain the reflected method parameter name
    // (for example `left`), while `id` is only the stable canvas connector
    // identifier (for example `1`). Mixing the two makes the Worker unable
    // to bind values to DLL method arguments.
    // API/Worker 使用反射得到的方法参数名（例如 `left`），id 仅用于画布连接席位（例如 `1`）。
    name: parameter.name?.trim() || parameter.id,
    valueJson: parameter.source === 'literal' ? parameter.literalValue : undefined,
    source: parameter.source,
    required: parameter.required ?? false,
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
      isVariadic: parameter.isVariadic,
      variadicGroupId: parameter.variadicGroupId,
      elementType: parameter.elementType,
      variadicMode: parameter.variadicMode,
      enumMetadata: parameter.enumMetadata,
    },
  }
}

function toConnectionDto(edge: FlowEdge): ConnectionDto {
  return {
    id: edge.id,
    fromNodeId: edge.source,
    fromPortId: edge.data.semantic === 'execution'
      ? executionHandleForBranch(edge.data.branch ?? executionBranchFromHandle(edge.sourceHandle))
      : (edge.sourceHandle ?? 'data-out'),
    toNodeId: edge.target,
    toPortId: edge.data.semantic === 'execution'
      ? (edge.targetHandle ?? 'exec-in')
      : canonicalParameterId(edge.data.targetParameterId ?? edge.targetHandle?.replace(/^param-/, '') ?? 'input'),
    kind: edge.data.semantic === 'execution' ? 'execution' : 'data',
    branch: edge.data.semantic === 'execution' ? (edge.data.branch ?? executionBranchFromHandle(edge.sourceHandle)) : undefined,
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
      parameters: node.parameters.map((parameter) => toMethodParameter(parameter, node.ui?.category === 'method')),
      script: node.script ? toScriptData(node.script) : undefined,
      runtime,
    },
  }
}

function toMethodParameter(parameter: NodeParameterDto, isMethodNode: boolean): MethodParameter {
  const source = isParameterSource(parameter.source) ? parameter.source : 'literal'
  const reflectedName = parameter.name?.trim()
  const uiName = parameter.ui?.nameKey?.trim()
  return {
    id: canonicalParameterId(parameter.ui?.id || parameter.name),
    nameKey: uiName || reflectedName,
    valueKind: parameter.ui?.valueKind || 'JSON',
    // Flows saved before the mapping fix used the connector id (`1`, `2`, …)
    // as `name`. Method-node UI metadata still carries the reflected name,
    // so restore it when loading those definitions.
    // 兼容修复前把连接器 ID 写入 name 的流程；类库节点的 UI 元数据仍保留真实参数名。
    name: isMethodNode && uiName ? uiName : reflectedName,
    type: parameter.ui?.type || parameter.ui?.valueKind || 'JSON',
    required: parameter.required,
    description: parameter.ui?.description,
    inputMode: normalizeParameterInputMode(parameter.ui?.inputMode),
    source,
    literalValue: parameter.ui?.literalValue ?? parameter.valueJson,
    projectInputKey: parameter.ui?.projectInputKey,
    expression: parameter.ui?.expression,
    sourceNodeId: parameter.ui?.sourceNodeId,
    sourcePortId: parameter.ui?.sourcePortId,
    isVariadic: parameter.ui?.isVariadic,
    variadicGroupId: parameter.ui?.variadicGroupId,
    elementType: parameter.ui?.elementType,
    variadicMode: parameter.ui?.variadicMode,
    enumMetadata: parameter.ui?.enumMetadata,
  }
}

function toScriptDto(script: ScriptNodeData): ScriptNodeDataDto {
  return {
    nodeId: script.nodeId,
    source: script.source,
    languageVersion: script.languageVersion,
    sourceHash: script.sourceHash,
    inputs: script.inputs,
    outputs: script.outputs,
  }
}

function toScriptData(script: ScriptNodeDataDto): ScriptNodeData {
  return {
    nodeId: script.nodeId,
    source: script.source,
    languageVersion: script.languageVersion,
    sourceHash: script.sourceHash,
    inputs: script.inputs,
    outputs: script.outputs,
  }
}

function toFlowEdge(connection: ConnectionDto): FlowEdge {
  const semantic: ConnectionSemantic = connection.kind === 'execution' ? 'execution' : 'data'
  const isExecution = semantic === 'execution'
  if (isExecution && !connection.branch) {
    throw new Error('Execution connections must declare Success, Failure, or Error. 流程连接必须声明 Success、Failure 或 Error 分支。')
  }
  const branch = isExecution ? connection.branch : undefined
  const lineType = connectionLineStyleFor(semantic).lineType
  return {
    id: connection.id,
    source: connection.fromNodeId,
    target: connection.toNodeId,
    sourceHandle: isExecution ? executionHandleForBranch(branch) : connection.fromPortId,
    targetHandle: isExecution ? connection.toPortId : parameterHandleFor(connection.toPortId),
    type: lineType,
    markerEnd: {
      type: MarkerType.ArrowClosed,
      color: isExecution ? executionBranchColor(branch) : '#6d42a5',
      width: 14,
      height: 14,
    },
    data: {
      semantic,
      branch,
      targetParameterId: isExecution ? undefined : canonicalParameterId(connection.toPortId),
    },
    class: isExecution ? `edge-execution branch-${branch}` : 'edge-data',
    ariaLabel: isExecution
      ? `Flow scheduling connection · ${branch} branch`
      : 'Parameter source connection',
  }
}

function executionHandleForBranch(branch: 'success' | 'failure' | 'error' | undefined): string {
  if (!branch) {
    throw new Error('Execution connections must declare Success, Failure, or Error. 流程连接必须声明 Success、Failure 或 Error 分支。')
  }

  return `exec-${branch}`
}

function executionBranchColor(branch: 'success' | 'failure' | 'error' | undefined): string {
  return branch === 'failure' ? '#b45309' : branch === 'error' ? '#dc2626' : '#15803d'
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
  if (!ui || [ui.category, ui.libraryId, ui.className, ui.methodName, ui.dllName, ui.dllVersion, ui.returnType, ui.targetNodeId, ui.targetFlowId, ui.isAwaitable, ui.staticReturnType, ui.isDynamicReturnType].every((value) => value === undefined)) {
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
    targetNodeId: ui.targetNodeId,
    targetFlowId: ui.targetFlowId,
    isAwaitable: ui.isAwaitable,
    staticReturnType: ui.staticReturnType,
    isDynamicReturnType: ui.isDynamicReturnType,
    targetCanvasId: ui.targetCanvasId,
    isPublic: ui.isPublic,
    flowCallParameterBindings: ui.flowCallParameterBindings,
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
