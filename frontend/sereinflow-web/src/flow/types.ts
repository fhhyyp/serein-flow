import type { EdgeMarkerType, XYPosition } from '@vue-flow/core'

/**
 * Node kinds exposed by the TRAE/SereinScript contract.
 *
 * The legacy `trigger`, `condition`, `value`, and `expression` values remain
 * valid so documents created by the first web workbench can still be opened.
 * New catalog entries should use the canonical kinds from `ReferenceNodeKind`.
 */
export type ReferenceNodeKind =
  | 'action'
  | 'flipflop'
  | 'script'
  | 'expOp'
  | 'expCondition'
  | 'flowCall'
  | 'globalData'

export type NodeKind = ReferenceNodeKind | 'trigger' | 'condition' | 'value' | 'expression'
export type NodeCategory = 'method' | 'basic'
export type NodeStatus = 'idle' | 'running' | 'success' | 'failed' | 'ready' | 'active'
export type ConnectionSemantic = 'execution' | 'data'
export type ParameterSource = 'literal' | 'previousNode' | 'projectInput' | 'expression'
export type ParameterInputMode = 'connection' | 'manual' | 'select'
export type ConnectorType = 'input' | 'output' | 'param' | 'result'
export type CanvasLifecycle = 'main' | 'init' | 'loading' | 'exit'

export interface NodeRuntimeMetadata {
  category?: NodeCategory
  libraryId?: string
  className?: string
  methodName?: string
  dllName?: string
  dllVersion?: string
  returnType?: string
}

export interface MethodParameter {
  id: string
  nameKey: string
  valueKind: string
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
  runtime?: NodeRuntimeMetadata
}

export interface FlowEdgeData {
  semantic: ConnectionSemantic
  targetParameterId?: string
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
  lifecycle: CanvasLifecycle
  nodes: FlowNode[]
  edges: FlowEdge[]
  selectedNodeId?: string
  selectedEdgeId?: string
}
