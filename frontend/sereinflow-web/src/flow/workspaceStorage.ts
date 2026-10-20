import type { CanvasLifecycle, CanvasState, FlowEdge, FlowNode, MethodParameter } from './types';
import { normalizeConnectionLineTypes } from './connectionLine';
import { normalizeCanvasFocusSettings } from './canvasFocus';
import type { WorkspaceSnapshot } from './workspaceHistory';
import type { FlowConcurrencyMode } from '../api/flowApi';
const storageKey = 'sereinflow.workspace.v1';
const formatVersion = 1;
const lifecycleValues: CanvasLifecycle[] = ['main', 'init', 'loading', 'exit', 'custom'];
interface StoredWorkspace extends WorkspaceSnapshot {
  formatVersion: number;
}
export function loadWorkspace(): WorkspaceSnapshot | undefined {
  if (typeof window === 'undefined') {
    return undefined;
  }
  try {
    const raw = window.localStorage.getItem(storageKey);
    if (!raw) {
      return undefined;
    }
    const candidate: unknown = JSON.parse(raw);
    const evaluatedValue: WorkspaceSnapshot | undefined = isStoredWorkspace(candidate) ? {
      canvases: candidate.canvases,
      activeCanvasId: candidate.activeCanvasId,
      nextNodeNumber: candidate.nextNodeNumber,
      entryNodeId: typeof candidate.entryNodeId === 'string' ? candidate.entryNodeId : undefined,
      projectName: typeof candidate.projectName === 'string' ? candidate.projectName : undefined,
      connectionLineTypes: normalizeConnectionLineTypes(candidate.connectionLineTypes),
      canvasFocusSettings: normalizeCanvasFocusSettings(candidate.canvasFocusSettings),
      runPolicy: isFlowConcurrencyMode(candidate.runPolicy?.concurrencyMode)
        ? { concurrencyMode: candidate.runPolicy.concurrencyMode }
        : undefined,
    } : undefined;
    return evaluatedValue;
  }
  catch {
    return undefined;
  }
}
export function saveWorkspace(snapshot: WorkspaceSnapshot): void {
  if (typeof window === 'undefined') {
    return;
  }
  const stored: StoredWorkspace = { formatVersion, ...snapshot };
  const argumentValue: string = storageKey;
  const stringifyResult: string = JSON.stringify(stored);
  window.localStorage.setItem(argumentValue, stringifyResult);
}
function isStoredWorkspace(value: unknown): value is StoredWorkspace {
  if (!isRecord(value)
    || value.formatVersion !== formatVersion
    || !Array.isArray(value.canvases)
    || value.canvases.length === 0
    || typeof value.activeCanvasId !== 'string'
    || typeof value.nextNodeNumber !== 'number'
    || !Number.isInteger(value.nextNodeNumber)
    || value.nextNodeNumber < 1
    || !value.canvases.every(isCanvas)) {
    return false;
  }
  const someResult: boolean = value.canvases.some((canvas) => canvas.id === value.activeCanvasId);
  return someResult;
}
function isCanvas(value: unknown): value is CanvasState {
  const evaluatedValue1: boolean = isRecord(value)
    && typeof value.id === 'string'
    && typeof value.nameKey === 'string'
    && lifecycleValues.includes(value.lifecycle as CanvasLifecycle)
    && (value.lifecycle !== 'custom' || (typeof value.name === 'string' && value.name.trim().length > 0))
    && Array.isArray(value.nodes)
    && value.nodes.every(isNode)
    && Array.isArray(value.edges)
    && value.edges.every(isEdge);
  return evaluatedValue1;
}
function isNode(value: unknown): value is FlowNode {
  const evaluatedValue2: boolean = isRecord(value)
    && typeof value.id === 'string'
    && isRecord(value.position)
    && typeof value.position.x === 'number'
    && typeof value.position.y === 'number'
    && isRecord(value.data)
    && Array.isArray(value.data.parameters)
    && value.data.parameters.every(isParameter);
  return evaluatedValue2;
}
function isEdge(value: unknown): value is FlowEdge {
  const evaluatedValue3: boolean = isRecord(value)
    && typeof value.id === 'string'
    && typeof value.source === 'string'
    && typeof value.target === 'string'
    && isRecord(value.data)
    && (value.data.semantic === 'execution' || value.data.semantic === 'data');
  return evaluatedValue3;
}
function isParameter(value: unknown): value is MethodParameter {
  const resolveIncludesResult = (parameterRecord: Record<string, unknown>) => {
    const stringResult: string = String(parameterRecord.source);
    const includesResult: boolean = ['literal', 'previousNode', 'projectInput', 'expression'].includes(stringResult);
    return includesResult;
  };
  const evaluatedValue4: boolean = isRecord(value)
    && typeof value.id === 'string'
    && typeof value.nameKey === 'string'
    && typeof value.valueKind === 'string'
    && resolveIncludesResult(value);
  return evaluatedValue4;
}
function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}
function isFlowConcurrencyMode(value: unknown): value is FlowConcurrencyMode {
  return value === 'parallel' || value === 'exclusiveReject';
}
