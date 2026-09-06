import type { FlowWorkpieceDto } from '../api/flowApi'

function createdAtValue(workpiece: FlowWorkpieceDto): number {
  const value = Date.parse(workpiece.createdAt)
  return Number.isFinite(value) ? value : Number.NEGATIVE_INFINITY
}

export function latestFlowWorkpiece(workpieces: readonly FlowWorkpieceDto[]): FlowWorkpieceDto | undefined {
  return workpieces.reduce<FlowWorkpieceDto | undefined>((latest, workpiece) => {
    if (!latest || createdAtValue(workpiece) >= createdAtValue(latest)) return workpiece
    return latest
  }, undefined)
}

export function latestFlowWorkpieceForNode(
  workpieces: readonly FlowWorkpieceDto[],
  nodeId?: string,
): FlowWorkpieceDto | undefined {
  if (!nodeId) return undefined
  return latestFlowWorkpiece(workpieces.filter((workpiece) => workpiece.nodeId === nodeId))
}

export function firstFlowWorkpiece(workpieces: readonly FlowWorkpieceDto[]): FlowWorkpieceDto | undefined {
  return workpieces.reduce<FlowWorkpieceDto | undefined>((first, workpiece) => {
    if (!first || createdAtValue(workpiece) < createdAtValue(first)) return workpiece
    return first
  }, undefined)
}

export function firstFlowWorkpieceForNode(
  workpieces: readonly FlowWorkpieceDto[],
  nodeId?: string,
): FlowWorkpieceDto | undefined {
  if (!nodeId) return undefined
  return firstFlowWorkpiece(workpieces.filter((workpiece) => workpiece.nodeId === nodeId))
}

export function firstFlowWorkpieceForExecution(
  workpieces: readonly FlowWorkpieceDto[],
  executionId?: string,
): FlowWorkpieceDto | undefined {
  if (!executionId) return undefined
  return firstFlowWorkpiece(workpieces.filter((workpiece) => workpiece.executionId === executionId))
}

export function selectFlowWorkpieceId(
  previous: readonly FlowWorkpieceDto[],
  next: readonly FlowWorkpieceDto[],
  selectedId: string,
  autoSelectLatest: boolean,
): string {
  if (next.length === 0) return ''

  const previousIds = new Set(previous.map((workpiece) => workpiece.id))
  const newWorkpieces = next.filter((workpiece) => !previousIds.has(workpiece.id))
  if (autoSelectLatest && newWorkpieces.length > 0) {
    return latestFlowWorkpiece(newWorkpieces)?.id ?? selectedId
  }
  if (selectedId && next.some((workpiece) => workpiece.id === selectedId)) return selectedId
  return latestFlowWorkpiece(next)?.id ?? ''
}
