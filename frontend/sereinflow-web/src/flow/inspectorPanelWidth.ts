export const defaultInspectorPanelWidth = 318
export const minimumInspectorPanelWidth = 280

export function maximumInspectorPanelWidth(viewportWidth: number): number {
  return Math.max(
    minimumInspectorPanelWidth,
    Math.min(640, Math.round(viewportWidth) - 280),
  )
}

export function clampInspectorPanelWidth(width: number, viewportWidth: number): number {
  const maximum = maximumInspectorPanelWidth(viewportWidth)
  if (!Number.isFinite(width)) return Math.min(defaultInspectorPanelWidth, maximum)
  return Math.round(Math.max(minimumInspectorPanelWidth, Math.min(maximum, width)))
}

export function parseStoredInspectorPanelWidth(value: string | null, viewportWidth: number): number {
  if (!value) return Math.min(defaultInspectorPanelWidth, maximumInspectorPanelWidth(viewportWidth))
  return clampInspectorPanelWidth(Number(value), viewportWidth)
}
