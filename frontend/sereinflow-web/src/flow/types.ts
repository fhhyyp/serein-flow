import type { EdgeMarkerType, XYPosition } from '@vue-flow/core'

export type NodeKind = 'trigger' | 'script' | 'condition' | 'action'
export type NodeStatus = 'ready' | 'active' | 'success'
export type ConnectionSemantic = 'execution' | 'data'
export type ParameterSource = 'literal' | 'previousNode' | 'projectInput' | 'expression'
export type CanvasLifecycle = 'main' | 'init' | 'loading' | 'exit'

export interface MethodParameter {
  id: string
  nameKey: string
  valueKind: string
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
