import type { CanvasState } from './types'

export function createInitialCanvases(): CanvasState[] {
  return [
    {
      id: 'main',
      nameKey: 'canvas.main',
      lifecycle: 'main',
      edges: [],
      nodes: [],
    },
  ]
}
