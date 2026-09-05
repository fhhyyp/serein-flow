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
