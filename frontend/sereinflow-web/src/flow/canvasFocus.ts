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
 * Derives the visible neighborhood from the persisted selection without
 * changing the graph itself. Data edges are treated like execution edges so
 * parameter sources and return-value consumers stay in the focused context.
 */
export function canvasFocusState(
  nodes: readonly FlowNode[],
  edges: readonly FlowEdge[],
  selectedNodeId?: string,
  selectedEdgeId?: string,
  settings: CanvasFocusSettings = defaultCanvasFocusSettings,
): CanvasFocusState {
  const active = settings.enabled && Boolean(selectedNodeId || selectedEdgeId)
  if (!active) {
    return unfocusedState(nodes, edges)
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

  if (!selectedNodeId || !nodes.some((node) => node.id === selectedNodeId)) {
    return unfocusedState(nodes, edges)
  }

  const focusedEdgeIds = new Set(edges.filter((edge) => {
    if (edge.data.semantic === 'data') {
      return (edge.target === selectedNodeId && settings.parameterSources)
        || (edge.source === selectedNodeId && settings.parameterConsumers)
    }

    return (edge.target === selectedNodeId && settings.callers)
      || (edge.source === selectedNodeId && settings.callees)
  }).map((edge) => edge.id))
  const focusedNodeIds = new Set([selectedNodeId])
  for (const edge of edges) {
    if (focusedEdgeIds.has(edge.id)) {
      focusedNodeIds.add(edge.source)
      focusedNodeIds.add(edge.target)
    }
  }

  return { active: true, focusedNodeIds, focusedEdgeIds }
}
