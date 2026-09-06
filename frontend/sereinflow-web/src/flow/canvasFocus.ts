import type { FlowEdge, FlowNode } from './types'

export interface CanvasFocusSettings {
  enabled: boolean
  parameterSources: boolean
  parameterConsumers: boolean
  callers: boolean
  callees: boolean
}

export type CanvasFocusSettingKey = keyof CanvasFocusSettings

export const defaultCanvasFocusSettings: CanvasFocusSettings = {
  enabled: true,
  parameterSources: true,
  parameterConsumers: true,
  callers: true,
  callees: true,
}

export interface CanvasFocusState {
  active: boolean
  focusedNodeIds: ReadonlySet<string>
  focusedEdgeIds: ReadonlySet<string>
}

function normalizeBoolean(value: unknown, fallback: boolean): boolean {
  return typeof value === 'boolean' ? value : fallback
}

export function normalizeCanvasFocusSettings(value?: Partial<CanvasFocusSettings> | null): CanvasFocusSettings {
  return {
    enabled: normalizeBoolean(value?.enabled, defaultCanvasFocusSettings.enabled),
    parameterSources: normalizeBoolean(value?.parameterSources, defaultCanvasFocusSettings.parameterSources),
    parameterConsumers: normalizeBoolean(value?.parameterConsumers, defaultCanvasFocusSettings.parameterConsumers),
    callers: normalizeBoolean(value?.callers, defaultCanvasFocusSettings.callers),
    callees: normalizeBoolean(value?.callees, defaultCanvasFocusSettings.callees),
  }
}

function unfocusedState(nodes: readonly FlowNode[], edges: readonly FlowEdge[]): CanvasFocusState {
  return {
    active: false,
    focusedNodeIds: new Set(nodes.map((node) => node.id)),
    focusedEdgeIds: new Set(edges.map((edge) => edge.id)),
  }
}

/**
 * Derives the visible neighborhood from the editor selection without changing
 * the graph itself. Data edges are treated like execution edges so parameter
 * sources and return-value consumers stay in the focused context. A
 * multi-node selection intentionally focuses only the selected nodes; using
 * one node as the relationship root would make batch selection misleading.
 */
export function canvasFocusState(
  nodes: readonly FlowNode[],
  edges: readonly FlowEdge[],
  selectedNodeId?: string,
  selectedEdgeId?: string,
  settings: CanvasFocusSettings = defaultCanvasFocusSettings,
): CanvasFocusState {
  const selectedNodeIds = nodes.filter((node) => node.selected).map((node) => node.id)
  const active = settings.enabled && Boolean(selectedNodeId || selectedEdgeId || selectedNodeIds.length > 0)
  if (!active) {
    return unfocusedState(nodes, edges)
  }

  if (selectedNodeIds.length > 1) {
    const focusedEdgeIds = new Set(edges
      .filter((edge) => selectedNodeIds.includes(edge.source) || selectedNodeIds.includes(edge.target))
      .map((edge) => edge.id))
    return {
      active: true,
      focusedNodeIds: new Set(selectedNodeIds),
      focusedEdgeIds,
    }
  }

  if (selectedEdgeId) {
    const selectedEdge = edges.find((edge) => edge.id === selectedEdgeId)
    if (!selectedEdge) {
      return unfocusedState(nodes, edges)
    }

    return {
      active: true,
      focusedNodeIds: new Set([selectedEdge.source, selectedEdge.target]),
      focusedEdgeIds: new Set([selectedEdge.id]),
    }
  }

  const primaryNodeId = selectedNodeId ?? selectedNodeIds[0]
  if (!primaryNodeId || !nodes.some((node) => node.id === primaryNodeId)) {
    return unfocusedState(nodes, edges)
  }

  const focusedEdgeIds = new Set(edges.filter((edge) => {
    if (edge.data.semantic === 'data') {
      return (edge.target === primaryNodeId && settings.parameterSources)
        || (edge.source === primaryNodeId && settings.parameterConsumers)
    }

    return (edge.target === primaryNodeId && settings.callers)
      || (edge.source === primaryNodeId && settings.callees)
  }).map((edge) => edge.id))
  const focusedNodeIds = new Set([primaryNodeId])
  for (const edge of edges) {
    if (focusedEdgeIds.has(edge.id)) {
      focusedNodeIds.add(edge.source)
      focusedNodeIds.add(edge.target)
    }
  }

  return { active: true, focusedNodeIds, focusedEdgeIds }
}
