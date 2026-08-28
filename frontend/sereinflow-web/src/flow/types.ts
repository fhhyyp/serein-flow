import type { EdgeMarkerType, XYPosition } from '@vue-flow/core'

/**
 * Node kinds exposed by the TRAE/SereinScript contract.
 *
 * The runtime supports only the four canonical node kinds.
 * 运行时只支持四种规范节点类型。
 */
export type ReferenceNodeKind =
  | 'action'
  | 'flipflop'
  | 'script'
  | 'flowCall'

export type NodeKind = ReferenceNodeKind
export type NodeCategory = 'method' | 'basic'
export type NodeStatus = 'idle' | 'running' | 'success' | 'failed' | 'error' | 'ready' | 'active'
export type ConnectionSemantic = 'execution' | 'data'
export type ExecutionBranch = 'success' | 'failure' | 'error'
/** Vue Flow's built-in edge/connection line types. `default` is Bezier. */
/** Vue Flow 内置的边/连接线类型；`default` 表示贝塞尔曲线。 */
export type FlowEdgeLineType = 'default' | 'simple-bezier' | 'straight' | 'step' | 'smoothstep'
export type ParameterSource = 'literal' | 'previousNode' | 'projectInput' | 'expression'
export type ParameterInputMode = 'connection' | 'manual' | 'select'
export type ConnectorType = 'input' | 'output' | 'param' | 'result'
export type CanvasLifecycle = 'main' | 'init' | 'loading' | 'exit' | 'custom'

export interface EnumValueOption {
  name: string
  numericValue: string
}

export interface EnumParameterMetadata {
  typeName: string
  isFlags: boolean
  underlyingType: string
  options: EnumValueOption[]
}

export interface NodeRuntimeMetadata {
  category?: NodeCategory
  libraryId?: string
  className?: string
  methodName?: string
  dllName?: string
  dllVersion?: string
  returnType?: string
  targetNodeId?: string
  targetFlowId?: string
  isAwaitable?: boolean
  staticReturnType?: string
  isDynamicReturnType?: boolean
  targetCanvasId?: string
  isPublic?: boolean
  flowCallParameterBindings?: Array<{ callParameterId: string; targetParameterId: string }>
  libraryNodeContractId?: string
}

export interface MethodParameter {
  id: string
  nameKey: string
  valueKind: string
  required?: boolean
  name?: string
  type?: string
  description?: string
  inputMode?: ParameterInputMode
  position?: { x: number; y: number }
  source: ParameterSource
  literalValue?: string
  projectInputKey?: string
  expression?: string
  sourceNodeId?: string
  sourcePortId?: string
  isVariadic?: boolean
  variadicGroupId?: string
  elementType?: string
  variadicMode?: 'expanded' | 'collection'
  enumMetadata?: EnumParameterMetadata
}

export interface FlowNodeData {
  kind: NodeKind
  titleKey: string
  subtitleKey: string
  displayName?: string
  description?: string
  status: NodeStatus
  hasDataOutput: boolean
  parameters: MethodParameter[]
  script?: ScriptNodeData
  runtime?: NodeRuntimeMetadata
  /** Local editor-only debug decoration; never persisted in FlowDefinition. */
  breakpoint?: boolean
  /** Current Worker pause boundary; never persisted in FlowDefinition. */
  debugPaused?: boolean
  breakpointLocked?: boolean
  onToggleBreakpoint?: (nodeId: string) => void
}

export interface ScriptNodeData {
  nodeId: string
  source: string
  languageVersion: string
  sourceHash: string
  inputs: Array<{ id?: string; name: string; valueKind: string; required: boolean; description?: string }>
  outputs: Array<{ id?: string; name: string; valueKind: string; required: boolean; description?: string }>
}

export interface FlowEdgeData {
  semantic: ConnectionSemantic
  targetParameterId?: string
  branch?: ExecutionBranch
  /** Optional per-edge override. If omitted, the semantic default is used. */
  /** 可选的单边覆盖设置；省略时使用语义默认值。 */
  lineType?: FlowEdgeLineType
}

export interface FlowNode {
  id: string
  position: XYPosition
  width?: number
  data: FlowNodeData
  type: 'workflow'
}

export interface FlowEdge {
  id: string
  source: string
  target: string
  sourceHandle?: string | null
  targetHandle?: string | null
  type?: string
  markerEnd?: EdgeMarkerType
  label?: string
  labelShowBg?: boolean
  labelBgPadding?: [number, number]
  labelBgBorderRadius?: number
  class?: string
  ariaLabel?: string
  data: FlowEdgeData
}

export interface CanvasState {
  id: string
  nameKey: string
  /** User-facing title for a custom canvas; built-in lifecycle canvases use nameKey. */
  /** 自定义画布的用户可见标题；内置生命周期画布使用 nameKey。 */
  name?: string
  lifecycle: CanvasLifecycle
  nodes: FlowNode[]
  edges: FlowEdge[]
  selectedNodeId?: string
  selectedEdgeId?: string
}
