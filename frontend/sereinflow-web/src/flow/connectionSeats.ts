import type { ExecutionBranch, FlowNodeData } from './types'

export type ConnectionSeatKind = 'execution-input' | 'execution-output' | 'parameter-input' | 'data-output'

export interface ConnectionSeat {
  id: string
  kind: ConnectionSeatKind
  side: 'left' | 'right'
  handleType: 'target' | 'source'
  semantic: 'execution' | 'data'
  branch?: ExecutionBranch
  parameterId?: string
  parameterIndex?: number
  labelKey: string
}

export interface ConnectionSeatLayout extends ConnectionSeat {
  top: number
}

const headerOffset = 46
const parameterStartOffset = 86
const parameterRowHeight = 27
const outputGap = 16

/**
 * The seat list is derived from node data instead of being encoded in the card
 * template. This keeps connection IDs stable when the visual card changes.
 * 连接席位列表由节点数据派生，而不是写死在卡片模板中，确保视觉卡片变化时连接 ID 保持稳定。
 */
type ConnectionSeatNodeData = Pick<FlowNodeData, 'kind' | 'parameters' | 'hasDataOutput' | 'runtime'>

export function getConnectionSeats(data: ConnectionSeatNodeData): ConnectionSeat[] {
  const seats: ConnectionSeat[] = []

  seats.push({
    id: 'exec-in',
    kind: 'execution-input',
    side: 'left',
    handleType: 'target',
    semantic: 'execution',
    labelKey: 'edge.executionDescription',
  })

  data.parameters.forEach((parameter, index) => {
    seats.push({
      id: parameterHandleFor(parameter.id),
      kind: 'parameter-input',
      side: 'left',
      handleType: 'target',
      semantic: 'data',
      parameterId: parameter.id,
      parameterIndex: index,
      labelKey: parameter.nameKey,
    })
  })

  for (const branch of ['success', 'failure', 'error'] as const) {
    seats.push({
      id: `exec-${branch}`,
      kind: 'execution-output',
      side: 'right',
      handleType: 'source',
      semantic: 'execution',
      branch,
      labelKey: `branch.${branch}`,
    })
  }

  if (hasDataOutput(data)) {
    seats.push({
      id: 'data-out',
      kind: 'data-output',
      side: 'right',
      handleType: 'source',
      semantic: 'data',
      labelKey: 'edge.dataDescription',
    })
  }

  return seats
}

export function layoutConnectionSeats(data: ConnectionSeatNodeData): ConnectionSeatLayout[] {
  return getConnectionSeats(data).map((seat) => ({
    ...seat,
    top: seat.kind === 'execution-input'
      ? headerOffset
      : seat.kind === 'execution-output'
        ? 18 + (seat.branch === 'failure' ? 18 : seat.branch === 'error' ? 36 : 0)
      : seat.kind === 'parameter-input'
        ? parameterStartOffset + (seat.parameterIndex ?? 0) * parameterRowHeight
        : parameterStartOffset + data.parameters.length * parameterRowHeight + outputGap,
  }))
}

function hasDataOutput(data: Pick<FlowNodeData, 'hasDataOutput' | 'runtime'>): boolean {
  const returnType = data.runtime?.returnType?.trim()
  if (returnType) {
    return returnType.toLowerCase() !== 'void'
  }

  return data.hasDataOutput
}

export function resolveConnectionSemantic(sourceHandle?: string | null, targetHandle?: string | null): 'execution' | 'data' | undefined {
  if (isExecutionOutputHandle(sourceHandle) && targetHandle === 'exec-in') {
    return 'execution'
  }

  if (sourceHandle === 'data-out' && targetHandle?.startsWith('param-')) {
    return 'data'
  }

  return undefined
}

/**
 * Parameter IDs in the library metadata historically used the transport
 * prefix `param-1`, while the canvas handle itself also adds `param-`.
 * Canonicalize numeric metadata IDs once so a restored edge targets the same
 * handle that the node card renders instead of Vue Flow's node center.
 * 类库元数据曾把 `param-` 作为参数 ID 前缀，而画布句柄又会追加一次；统一规范化后避免回载时句柄变成 `param-param-1`。
 */
export function canonicalParameterId(parameterId: string): string {
  return parameterId.replace(/^param-(?=\d+$)/, '')
}

export function parameterHandleFor(parameterId: string): string {
  return `param-${canonicalParameterId(parameterId)}`
}

export function isExecutionOutputHandle(handleId: string | null | undefined): boolean {
  return handleId === 'exec-success' || handleId === 'exec-failure' || handleId === 'exec-error'
}

export function executionBranchFromHandle(handleId: string | null | undefined): ExecutionBranch | undefined {
  if (handleId === 'exec-success') return 'success'
  if (handleId === 'exec-failure') return 'failure'
  if (handleId === 'exec-error') return 'error'
  return undefined
}

export function isInputSeat(handleId: string | null | undefined): boolean {
  return handleId === 'exec-in' || handleId?.startsWith('param-') === true
}
