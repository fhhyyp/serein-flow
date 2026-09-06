export interface IdentifiableCanvas {
  id: string
}

/**
 * Returns a reordered copy with the source canvas inserted before the target.
 * Invalid moves leave the original order unchanged.
 * 将源画布移动到目标画布之前，并返回新的数组；无效移动保持原顺序。
 */
export function reorderCanvases<T extends IdentifiableCanvas>(
  canvases: readonly T[],
  sourceCanvasId: string,
  targetCanvasId: string,
): T[] {
  if (sourceCanvasId === targetCanvasId) {
    return [...canvases]
  }

  const sourceIndex = canvases.findIndex((canvas) => canvas.id === sourceCanvasId)
  const targetIndex = canvases.findIndex((canvas) => canvas.id === targetCanvasId)
  if (sourceIndex < 0 || targetIndex < 0) {
    return [...canvases]
  }

  const reordered = [...canvases]
  const [source] = reordered.splice(sourceIndex, 1)
  const insertionIndex = reordered.findIndex((canvas) => canvas.id === targetCanvasId)
  reordered.splice(insertionIndex, 0, source!)
  return reordered
}
