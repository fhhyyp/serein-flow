import { ConnectionLineType } from '@vue-flow/core'
import type { ConnectionSemantic, FlowEdge, FlowEdgeLineType } from './types'

export type ConnectionLineSettings = Record<ConnectionSemantic, FlowEdgeLineType>

export interface ConnectionLineTypeOption {
  value: FlowEdgeLineType
  labelKey: string
}

export interface ConnectionLineStyle {
  /** Vue Flow edge type used for both persisted edges and the drag preview. */
  /** 持久化连接线和拖拽预览共用的 Vue Flow 边类型。 */
  lineType: FlowEdgeLineType
  /** Dash pattern used only while a new connection is being previewed. */
  /** 仅在预创建新连接时使用的虚线样式。 */
  previewDashArray: string
  /** Theme color for the preview path. */
  /** 预览路径使用的主题色。 */
  color: string
}

export const defaultConnectionLineTypes: ConnectionLineSettings = {
  execution: ConnectionLineType.SmoothStep,
  data: ConnectionLineType.Bezier,
}

/** The console intentionally exposes only the two requested editor choices. */
/** 控制台只暴露需求中指定的两种编辑器选项。 */
export const connectionLineTypeOptions: readonly ConnectionLineTypeOption[] = [
  { value: ConnectionLineType.SmoothStep, labelKey: 'connectionLine.segment' },
  { value: ConnectionLineType.Bezier, labelKey: 'connectionLine.bezier' },
]

/**
 * Semantic connection defaults. Keep this object intentionally mutable so a
 * host application can replace either line type without changing canvas code.
 * `default` is Vue Flow's cubic Bezier edge.
 * 语义连接默认值。保留对象可变性，使宿主应用无需修改画布代码即可替换任一线型。
 * `default` 是 Vue Flow 的三次贝塞尔边。
 */
export const connectionLineStyles: Record<ConnectionSemantic, ConnectionLineStyle> = {
  execution: {
    lineType: defaultConnectionLineTypes.execution,
    previewDashArray: '9 6',
    color: '#0369a1',
  },
  data: {
    lineType: defaultConnectionLineTypes.data,
    previewDashArray: '6 4',
    color: '#6d42a5',
  },
}

export function connectionLineStyleFor(semantic: ConnectionSemantic, settings?: Partial<ConnectionLineSettings>): ConnectionLineStyle {
  const style = connectionLineStyles[semantic]
  const configuredType = normalizeLineType(settings?.[semantic])
  return configuredType ? { ...style, lineType: configuredType } : style
}

export function connectionLineTypeForEdge(edge: Pick<FlowEdge, 'data'>, settings?: Partial<ConnectionLineSettings>): FlowEdgeLineType {
  return normalizeLineType(edge.data.lineType) ?? connectionLineStyleFor(edge.data.semantic, settings).lineType
}

export function normalizeConnectionLineTypes(value?: Partial<ConnectionLineSettings> | null): ConnectionLineSettings {
  const configuredExecution = normalizeLineType(value?.execution)
  return {
    // `straight` was never exposed by the console; migrate any interim
    // preview setting to the requested orthogonal line-segment style.
    // 控制台从未暴露 `straight`；将临时预览设置迁移为所需的正交线段样式。
    execution: configuredExecution === ConnectionLineType.Straight
      ? defaultConnectionLineTypes.execution
      : configuredExecution ?? defaultConnectionLineTypes.execution,
    data: normalizeLineType(value?.data) ?? defaultConnectionLineTypes.data,
  }
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
  // 源连接席位在整个拖拽过程中都可用，因此在指针尚未到达目标或位于无效目标时，
  // 它是最可靠的连接类型信号。
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
