export interface IdentifiedGraphItem {
  id: string
}

export interface CanvasGraph<TNode extends IdentifiedGraphItem, TEdge extends IdentifiedGraphItem> {
  nodes: TNode[]
  edges: TEdge[]
}

export interface NodePositionChange {
  id: string
  position?: { x: number; y: number }
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

/**
 * Vue Flow keeps a separate graph store while a node is being dragged. The
 * emitted position changes must be copied into the workspace model explicitly
 * before another controlled-model update can replace that store.
 * 拖拽节点时 Vue Flow 会维护独立的图存储；在受控模型更新替换该存储前，
 * 必须显式将发出的坐标变化复制到工作区模型中。
 */
export function applyNodePositionChanges<
  TNode extends IdentifiedGraphItem & { position: { x: number; y: number } },
>(nodes: readonly TNode[], changes: readonly NodePositionChange[]): TNode[] {
  const positions = new Map(
    changes
      .filter((change): change is NodePositionChange & { position: { x: number; y: number } } => change.position !== undefined)
      .map((change) => [change.id, change.position]),
  )

  if (positions.size === 0) {
    return [...nodes]
  }

  return nodes.map((node) => {
    const position = positions.get(node.id)
    return position ? { ...node, position: { ...position } } : node
  })
}
