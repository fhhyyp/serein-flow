import type { FlowWorkpieceDto } from '../api/flowApi';
function createdAtValue(workpiece: FlowWorkpieceDto): number {
  const value = Date.parse(workpiece.createdAt);
  const evaluatedValue: number = Number.isFinite(value) ? value : Number.NEGATIVE_INFINITY;
  return evaluatedValue;
}
export function latestFlowWorkpiece(workpieces: readonly FlowWorkpieceDto[]): FlowWorkpieceDto | undefined {
  const reduceResult: FlowWorkpieceDto | undefined = workpieces.reduce<FlowWorkpieceDto | undefined>((latest, workpiece) => {
    if (!latest || createdAtValue(workpiece) >= createdAtValue(latest)) {
      return workpiece;
    }
    return latest;
  }, undefined);
  return reduceResult;
}
export function latestFlowWorkpieceForNode(workpieces: readonly FlowWorkpieceDto[], nodeId?: string): FlowWorkpieceDto | undefined {
  if (!nodeId) {
    return undefined;
  }
  const filterResult: readonly FlowWorkpieceDto[] = workpieces.filter((workpiece) => workpiece.nodeId === nodeId);
  const latestFlowWorkpieceResult: FlowWorkpieceDto | undefined = latestFlowWorkpiece(filterResult);
  return latestFlowWorkpieceResult;
}
export function firstFlowWorkpiece(workpieces: readonly FlowWorkpieceDto[]): FlowWorkpieceDto | undefined {
  const reduceResult1: FlowWorkpieceDto | undefined = workpieces.reduce<FlowWorkpieceDto | undefined>((first, workpiece) => {
    if (!first || createdAtValue(workpiece) < createdAtValue(first)) {
      return workpiece;
    }
    return first;
  }, undefined);
  return reduceResult1;
}
export function firstFlowWorkpieceForNode(workpieces: readonly FlowWorkpieceDto[], nodeId?: string): FlowWorkpieceDto | undefined {
  if (!nodeId) {
    return undefined;
  }
  const filterResult1: readonly FlowWorkpieceDto[] = workpieces.filter((workpiece) => workpiece.nodeId === nodeId);
  const firstFlowWorkpieceResult: FlowWorkpieceDto | undefined = firstFlowWorkpiece(filterResult1);
  return firstFlowWorkpieceResult;
}
export function firstFlowWorkpieceForExecution(workpieces: readonly FlowWorkpieceDto[], executionId?: string): FlowWorkpieceDto | undefined {
  if (!executionId) {
    return undefined;
  }
  const filterResult2: readonly FlowWorkpieceDto[] = workpieces.filter((workpiece) => workpiece.executionId === executionId);
  const firstFlowWorkpieceResult1: FlowWorkpieceDto | undefined = firstFlowWorkpiece(filterResult2);
  return firstFlowWorkpieceResult1;
}
export function selectFlowWorkpieceId(previous: readonly FlowWorkpieceDto[], next: readonly FlowWorkpieceDto[], selectedId: string, autoSelectLatest: boolean): string {
  if (next.length === 0) {
    return '';
  }
  const mapResult: Iterable<string> | null | undefined = previous.map((workpiece) => workpiece.id);
  const previousIds = new Set(mapResult);
  const newWorkpieces = next.filter((workpiece) => {
    const evaluatedValue1: boolean = !previousIds.has(workpiece.id);
    return evaluatedValue1;
  });
  if (autoSelectLatest && newWorkpieces.length > 0) {
    const evaluatedValue2: string = latestFlowWorkpiece(newWorkpieces)?.id ?? selectedId;
    return evaluatedValue2;
  }
  if (selectedId && next.some((workpiece) => workpiece.id === selectedId)) {
    return selectedId;
  }
  const evaluatedValue3: string = latestFlowWorkpiece(next)?.id ?? '';
  return evaluatedValue3;
}
