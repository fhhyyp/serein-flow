import type { FlowNodeData } from './types'

export type ConnectionSeatKind = 'execution-input' | 'execution-output' | 'parameter-input' | 'data-output'

export interface ConnectionSeat {
  id: string
  kind: ConnectionSeatKind
  side: 'left' | 'right'
  handleType: 'target' | 'source'
  semantic: 'execution' | 'data'
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
 */
type ConnectionSeatNodeData = Pick<FlowNodeData, 'kind' | 'parameters' | 'hasDataOutput' | 'runtime'>

export function getConnectionSeats(data: ConnectionSeatNodeData): ConnectionSeat[] {
  const seats: ConnectionSeat[] = []

  if (data.kind !== 'trigger') {
    seats.push({
      id: 'exec-in',
      kind: 'execution-input',
      side: 'left',
      handleType: 'target',
      semantic: 'execution',
      labelKey: 'edge.executionDescription',
    })
  }

  data.parameters.forEach((parameter, index) => {
    seats.push({
      id: `param-${parameter.id}`,
      kind: 'parameter-input',
      side: 'left',
      handleType: 'target',
      semantic: 'data',
      parameterId: parameter.id,
      parameterIndex: index,
      labelKey: parameter.nameKey,
    })
  })

  seats.push({
    id: 'exec-out',
    kind: 'execution-output',
    side: 'right',
    handleType: 'source',
    semantic: 'execution',
    labelKey: 'edge.executionDescription',
  })

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
    top: seat.kind === 'execution-input' || seat.kind === 'execution-output'
      ? headerOffset
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
  if (sourceHandle === 'exec-out' && targetHandle === 'exec-in') {
    return 'execution'
  }

  if (sourceHandle === 'data-out' && targetHandle?.startsWith('param-')) {
    return 'data'
  }

  return undefined
}

export function isInputSeat(handleId: string | null | undefined): boolean {
  return handleId === 'exec-in' || handleId?.startsWith('param-') === true
}
