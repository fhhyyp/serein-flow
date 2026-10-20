export interface DockRect {
  left: number;
  top: number;
  right: number;
  bottom: number;
}
export type DockSplitZone = 'left' | 'right' | 'top' | 'bottom' | 'center';
const MIN_WIDTH = 240;
const MIN_HEIGHT = 160;
export function dockSplitZoneAtPoint(rect: DockRect, x: number, y: number): DockSplitZone {
  const width = rect.right - rect.left;
  const height = rect.bottom - rect.top;
  const candidates: Array<{
    zone: DockSplitZone;
    distance: number;
  }> = [];
  if (width >= MIN_WIDTH * 2) {
    candidates.push({ zone: 'left', distance: (x - rect.left) / width });
    candidates.push({ zone: 'right', distance: (rect.right - x) / width });
  }
  if (height >= MIN_HEIGHT * 2) {
    candidates.push({ zone: 'top', distance: (y - rect.top) / height });
    candidates.push({ zone: 'bottom', distance: (rect.bottom - y) / height });
  }
  const nearest = candidates.sort((first, second) => first.distance - second.distance)[0];
  return nearest && nearest.distance <= .25 ? nearest.zone : 'center';
}
export function splitDockRect(rect: DockRect, zone: Exclude<DockSplitZone, 'center'>): {
  source: DockRect;
  target: DockRect;
} {
  const width = rect.right - rect.left;
  const height = rect.bottom - rect.top;
  if (zone === 'left' || zone === 'right') {
    const argumentValue: number = width - MIN_WIDTH;
    const argumentValue1: number = MIN_WIDTH;
    const roundResult: number = Math.round(width * .5);
    const maxResult: number = Math.max(argumentValue1, roundResult);
    const sourceWidth = Math.min(argumentValue, maxResult);
    const boundary = zone === 'left' ? rect.left + sourceWidth : rect.right - sourceWidth;
    return zone === 'left'
      ? { source: { ...rect, right: boundary }, target: { ...rect, left: boundary } }
      : { source: { ...rect, left: boundary }, target: { ...rect, right: boundary } };
  }
  const argumentValue2: number = height - MIN_HEIGHT;
  const argumentValue3: number = MIN_HEIGHT;
  const roundResult1: number = Math.round(height * .5);
  const maxResult1: number = Math.max(argumentValue3, roundResult1);
  const sourceHeight = Math.min(argumentValue2, maxResult1);
  const boundary = zone === 'top' ? rect.top + sourceHeight : rect.bottom - sourceHeight;
  return zone === 'top'
    ? { source: { ...rect, bottom: boundary }, target: { ...rect, top: boundary } }
    : { source: { ...rect, top: boundary }, target: { ...rect, bottom: boundary } };
}
