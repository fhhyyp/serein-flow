import { ConnectionLineType } from '@vue-flow/core'
import type { ConnectionSemantic, FlowEdge, FlowEdgeLineType } from './types'

export interface ConnectionLineStyle {
  /** Vue Flow edge type used for both persisted edges and the drag preview. */
  lineType: FlowEdgeLineType
  /** Dash pattern used only while a new connection is being previewed. */
  previewDashArray: string
  /** Theme color for the preview path. */
  color: string
}

/**
 * Semantic connection defaults. Keep this object intentionally mutable so a
 * host application can replace either line type without changing canvas code.
 * `default` is Vue Flow's cubic Bezier edge.
 */
export const connectionLineStyles: Record<ConnectionSemantic, ConnectionLineStyle> = {
  execution: {
    lineType: ConnectionLineType.Bezier,
    previewDashArray: '9 6',
    color: '#0369a1',
  },
  data: {
    lineType: ConnectionLineType.SmoothStep,
    previewDashArray: '6 4',
    color: '#6d42a5',
  },
}

export function connectionLineStyleFor(semantic: ConnectionSemantic): ConnectionLineStyle {
  return connectionLineStyles[semantic]
}

export function connectionLineTypeForEdge(edge: Pick<FlowEdge, 'data'>): FlowEdgeLineType {
  return normalizeLineType(edge.data.lineType) ?? connectionLineStyleFor(edge.data.semantic).lineType
}

export function normalizeLineType(value: string | undefined): FlowEdgeLineType | undefined {
  if (value === ConnectionLineType.Bezier
    || value === ConnectionLineType.SimpleBezier
    || value === ConnectionLineType.Straight
    || value === ConnectionLineType.Step
    || value === ConnectionLineType.SmoothStep) {
    return value
  }

  return undefined
}

export function semanticFromConnectionHandles(sourceHandle?: string | null, targetHandle?: string | null): ConnectionSemantic | undefined {
  // The source seat is available for the whole drag, so it is the strongest
  // signal while the pointer has not reached a target yet (or is currently on
  // an invalid target seat).
  if (sourceHandle === 'exec-out') {
    return 'execution'
  }

  if (sourceHandle === 'data-out') {
    return 'data'
  }

  if (targetHandle === 'exec-in') {
    return 'execution'
  }

  if (targetHandle?.startsWith('param-') === true) {
    return 'data'
  }

  return undefined
}
