const breakpointStoragePrefix = 'sereinflow.debug.breakpoints.v1'
const sessionStoragePrefix = 'sereinflow.debug.session.v1'

function scopedKey(prefix: string, projectId?: string, flowId?: string): string | undefined {
  if (!projectId || !flowId) return undefined
  return `${prefix}:${projectId}:${flowId}`
}

export function normalizeBreakpointNodeIds(ids: readonly string[], knownNodeIds?: ReadonlySet<string>): string[] {
  return [...new Set(ids
    .filter((id) => typeof id === 'string')
    .map((id) => id.trim())
    .filter((id) => id.length > 0 && (!knownNodeIds || knownNodeIds.has(id))))]
    .sort((left, right) => left.localeCompare(right))
}

export function loadBreakpointNodeIds(
  projectId: string | undefined,
  flowId: string | undefined,
  knownNodeIds: ReadonlySet<string>,
): string[] {
  const key = scopedKey(breakpointStoragePrefix, projectId, flowId)
  if (!key || typeof window === 'undefined') return []

  try {
    const value: unknown = JSON.parse(window.localStorage.getItem(key) ?? '[]')
    return Array.isArray(value)
      ? normalizeBreakpointNodeIds(value.filter((id): id is string => typeof id === 'string'), knownNodeIds)
      : []
  } catch {
    return []
  }
}

export function saveBreakpointNodeIds(
  projectId: string | undefined,
  flowId: string | undefined,
  ids: readonly string[],
): void {
  const key = scopedKey(breakpointStoragePrefix, projectId, flowId)
  if (!key || typeof window === 'undefined') return

  try {
    window.localStorage.setItem(key, JSON.stringify(normalizeBreakpointNodeIds(ids)))
  } catch {
    // Local editor preferences must not prevent opening or running a flow.
    // 本地编辑器偏好保存失败不能阻止流程打开或运行。
  }
}

export function loadStoredDebugSessionId(projectId?: string, flowId?: string): string | undefined {
  const key = scopedKey(sessionStoragePrefix, projectId, flowId)
  if (!key || typeof window === 'undefined') return undefined
  const value = window.localStorage.getItem(key)?.trim()
  return value || undefined
}

export function saveStoredDebugSessionId(projectId: string | undefined, flowId: string | undefined, sessionId: string): void {
  const key = scopedKey(sessionStoragePrefix, projectId, flowId)
  if (!key || typeof window === 'undefined' || !sessionId.trim()) return
  window.localStorage.setItem(key, sessionId)
}

export function clearStoredDebugSessionId(projectId?: string, flowId?: string): void {
  const key = scopedKey(sessionStoragePrefix, projectId, flowId)
  if (!key || typeof window === 'undefined') return
  window.localStorage.removeItem(key)
}
