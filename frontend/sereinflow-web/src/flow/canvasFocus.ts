import type { FlowEdge, FlowNode } from './types';
export interface CanvasFocusSettings {
  enabled: boolean;
  parameterSources: boolean;
  parameterConsumers: boolean;
  callers: boolean;
  callees: boolean;
}
export type CanvasFocusSettingKey = keyof CanvasFocusSettings;
export const defaultCanvasFocusSettings: CanvasFocusSettings = {
  enabled: true,
  parameterSources: true,
  parameterConsumers: true,
  callers: true,
  callees: true,
};
export interface CanvasFocusState {
  active: boolean;
  focusedNodeIds: ReadonlySet<string>;
  focusedEdgeIds: ReadonlySet<string>;
}
function normalizeBoolean(value: unknown, fallback: boolean): boolean {
  return typeof value === 'boolean' ? value : fallback;
}
export function normalizeCanvasFocusSettings(value?: Partial<CanvasFocusSettings> | null): CanvasFocusSettings {
  const evaluatedValue: CanvasFocusSettings = {
    enabled: normalizeBoolean(value?.enabled, defaultCanvasFocusSettings.enabled),
    parameterSources: normalizeBoolean(value?.parameterSources, defaultCanvasFocusSettings.parameterSources),
    parameterConsumers: normalizeBoolean(value?.parameterConsumers, defaultCanvasFocusSettings.parameterConsumers),
    callers: normalizeBoolean(value?.callers, defaultCanvasFocusSettings.callers),
    callees: normalizeBoolean(value?.callees, defaultCanvasFocusSettings.callees),
  };
  return evaluatedValue;
}
function unfocusedState(nodes: readonly FlowNode[], edges: readonly FlowEdge[]): CanvasFocusState {
  const resolveSetInstance = () => {
    const mapResult2: Iterable<string> | null | undefined = nodes.map((node) => node.id);
    const setInstance: ReadonlySet<string> = new Set(mapResult2);
    return setInstance;
  };
  const resolveSetInstance1 = () => {
    const mapResult3: Iterable<string> | null | undefined = edges.map((edge) => edge.id);
    const setInstance1: ReadonlySet<string> = new Set(mapResult3);
    return setInstance1;
  };
  const evaluatedValue1: CanvasFocusState = {
    active: false,
    focusedNodeIds: resolveSetInstance(),
    focusedEdgeIds: resolveSetInstance1(),
  };
  return evaluatedValue1;
}
/**
 * Derives the visible neighborhood from the editor selection without changing
 * the graph itself. Data edges are treated like execution edges so parameter
 * sources and return-value consumers stay in the focused context. A
 * multi-node selection intentionally focuses only the selected nodes; using
 * one node as the relationship root would make batch selection misleading.
 * 根据编辑器选择推导可见邻域，不修改图本身；数据边与执行边同等处理，保留参数来源和返回值使用方。多选时只聚焦所选节点，避免以单个节点为关系根而误导批量选择。
 */
export function canvasFocusState(nodes: readonly FlowNode[], edges: readonly FlowEdge[], selectedNodeId?: string, selectedEdgeId?: string, settings: CanvasFocusSettings = defaultCanvasFocusSettings): CanvasFocusState {
  const filterResult1 = nodes.filter((node) => node.selected);
  const selectedNodeIds = filterResult1.map((node) => node.id);
  const active = settings.enabled && Boolean(selectedNodeId || selectedEdgeId || selectedNodeIds.length > 0);
  if (!active) {
    const unfocusedStateResult: CanvasFocusState = unfocusedState(nodes, edges);
    return unfocusedStateResult;
  }
  if (selectedNodeIds.length > 1) {
    const filterResult = edges
      .filter((edge) => {
      const evaluatedValue2: boolean = selectedNodeIds.includes(edge.source) || selectedNodeIds.includes(edge.target);
      return evaluatedValue2;
    });
    const mapResult: Iterable<string> | null | undefined = filterResult.map((edge) => edge.id);
    const focusedEdgeIds = new Set(mapResult);
    const evaluatedValue3: CanvasFocusState = {
      active: true,
      focusedNodeIds: new Set(selectedNodeIds),
      focusedEdgeIds,
    };
    return evaluatedValue3;
  }
  if (selectedEdgeId) {
    const selectedEdge = edges.find((edge) => edge.id === selectedEdgeId);
    if (!selectedEdge) {
      const unfocusedStateResult1: CanvasFocusState = unfocusedState(nodes, edges);
      return unfocusedStateResult1;
    }
    const evaluatedValue4: CanvasFocusState = {
      active: true,
      focusedNodeIds: new Set([selectedEdge.source, selectedEdge.target]),
      focusedEdgeIds: new Set([selectedEdge.id]),
    };
    return evaluatedValue4;
  }
  const primaryNodeId = selectedNodeId ?? selectedNodeIds[0];
  if (!primaryNodeId || !nodes.some((node) => node.id === primaryNodeId)) {
    const unfocusedStateResult2: CanvasFocusState = unfocusedState(nodes, edges);
    return unfocusedStateResult2;
  }
  const filterResult2 = edges.filter((edge) => {
    if (edge.data.semantic === 'data') {
      return (edge.target === primaryNodeId && settings.parameterSources)
        || (edge.source === primaryNodeId && settings.parameterConsumers);
    }
    return (edge.target === primaryNodeId && settings.callers)
      || (edge.source === primaryNodeId && settings.callees);
  });
  const mapResult1: Iterable<string> | null | undefined = filterResult2.map((edge) => edge.id);
  const focusedEdgeIds = new Set(mapResult1);
  const focusedNodeIds = new Set([primaryNodeId]);
  for (const edge of edges) {
    if (focusedEdgeIds.has(edge.id)) {
      focusedNodeIds.add(edge.source);
      focusedNodeIds.add(edge.target);
    }
  }
  return { active: true, focusedNodeIds, focusedEdgeIds };
}
