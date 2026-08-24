export interface IdentifiedGraphItem {
  id: string
}

export interface CanvasGraph<TNode extends IdentifiedGraphItem, TEdge extends IdentifiedGraphItem> {
  nodes: TNode[]
  edges: TEdge[]
}

export function removeEdgesById<TEdge extends IdentifiedGraphItem>(edges: readonly TEdge[], removedIds: ReadonlySet<string>): TEdge[] {
  return edges.filter((edge) => !removedIds.has(edge.id))
}

export function cloneCanvasGraph<TNode extends IdentifiedGraphItem, TEdge extends IdentifiedGraphItem>(canvas: CanvasGraph<TNode, TEdge>): CanvasGraph<TNode, TEdge> {
  return {
    nodes: canvas.nodes.map((node) => ({ ...node })),
    edges: canvas.edges.map((edge) => ({ ...edge })),
  }
}
