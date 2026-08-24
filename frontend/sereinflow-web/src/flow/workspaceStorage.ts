import type { CanvasLifecycle, CanvasState, FlowEdge, FlowNode, MethodParameter } from './types'
import { normalizeConnectionLineTypes } from './connectionLine'
import type { WorkspaceSnapshot } from './workspaceHistory'

const storageKey = 'sereinflow.workspace.v1'
const formatVersion = 1
const lifecycleValues: CanvasLifecycle[] = ['main', 'init', 'loading', 'exit']

interface StoredWorkspace extends WorkspaceSnapshot {
  formatVersion: number
}

export function loadWorkspace(): WorkspaceSnapshot | undefined {
  if (typeof window === 'undefined') {
    return undefined
  }

  try {
    const raw = window.localStorage.getItem(storageKey)
    if (!raw) {
      return undefined
    }

    const candidate: unknown = JSON.parse(raw)
    return isStoredWorkspace(candidate) ? {
      canvases: candidate.canvases,
      activeCanvasId: candidate.activeCanvasId,
      nextNodeNumber: candidate.nextNodeNumber,
      connectionLineTypes: normalizeConnectionLineTypes(candidate.connectionLineTypes),
    } : undefined
  } catch {
    return undefined
  }
}

export function saveWorkspace(snapshot: WorkspaceSnapshot): void {
  if (typeof window === 'undefined') {
    return
  }

  const stored: StoredWorkspace = { formatVersion, ...snapshot }
  window.localStorage.setItem(storageKey, JSON.stringify(stored))
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
    return false
  }

  return value.canvases.some((canvas) => canvas.id === value.activeCanvasId)
}

function isCanvas(value: unknown): value is CanvasState {
  return isRecord(value)
    && typeof value.id === 'string'
    && typeof value.nameKey === 'string'
    && lifecycleValues.includes(value.lifecycle as CanvasLifecycle)
    && Array.isArray(value.nodes)
    && value.nodes.every(isNode)
    && Array.isArray(value.edges)
    && value.edges.every(isEdge)
}

function isNode(value: unknown): value is FlowNode {
  return isRecord(value)
    && typeof value.id === 'string'
    && isRecord(value.position)
    && typeof value.position.x === 'number'
    && typeof value.position.y === 'number'
    && isRecord(value.data)
    && Array.isArray(value.data.parameters)
    && value.data.parameters.every(isParameter)
}

function isEdge(value: unknown): value is FlowEdge {
  return isRecord(value)
    && typeof value.id === 'string'
    && typeof value.source === 'string'
    && typeof value.target === 'string'
    && isRecord(value.data)
    && (value.data.semantic === 'execution' || value.data.semantic === 'data')
}

function isParameter(value: unknown): value is MethodParameter {
  return isRecord(value)
    && typeof value.id === 'string'
    && typeof value.nameKey === 'string'
    && typeof value.valueKind === 'string'
    && ['literal', 'previousNode', 'projectInput', 'expression'].includes(String(value.source))
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}
