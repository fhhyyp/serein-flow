export const defaultInspectorPanelWidth = 318;
export const minimumInspectorPanelWidth = 280;
const inspectorViewportReserve = 280;
const inspectorPanelWidthLimit = 640;
export function maximumInspectorPanelWidth(viewportWidth: number): number {
  const argumentValue: number = minimumInspectorPanelWidth;
  const availableWidth: number = Math.round(viewportWidth) - inspectorViewportReserve;
  const constrainedWidth: number = Math.min(inspectorPanelWidthLimit, availableWidth);
  const maximumWidth: number = Math.max(argumentValue, constrainedWidth);
  return maximumWidth;
}
export function clampInspectorPanelWidth(width: number, viewportWidth: number): number {
  const maximum = maximumInspectorPanelWidth(viewportWidth);
  if (!Number.isFinite(width)) {
    const minResult1: number = Math.min(defaultInspectorPanelWidth, maximum);
    return minResult1;
  }
  const argumentValue1: number = minimumInspectorPanelWidth;
  const minResult2: number = Math.min(maximum, width);
  const maxResult1: number = Math.max(argumentValue1, minResult2);
  const roundResult: number = Math.round(maxResult1);
  return roundResult;
}
export function parseStoredInspectorPanelWidth(value: string | null, viewportWidth: number): number {
  if (!value) {
    const argumentValue2: number = defaultInspectorPanelWidth;
    const maximumInspectorPanelWidthResult: number = maximumInspectorPanelWidth(viewportWidth);
    const minResult3: number = Math.min(argumentValue2, maximumInspectorPanelWidthResult);
    return minResult3;
  }
  const numberResult: number = Number(value);
  const clampInspectorPanelWidthResult: number = clampInspectorPanelWidth(numberResult, viewportWidth);
  return clampInspectorPanelWidthResult;
}
