const breakpointStoragePrefix = 'sereinflow.debug.breakpoints.v1';
const sessionStoragePrefix = 'sereinflow.debug.session.v1';
function scopedKey(prefix: string, projectId?: string, flowId?: string): string | undefined {
  if (!projectId || !flowId) {
    return undefined;
  }
  return `${prefix}:${projectId}:${flowId}`;
}
export function normalizeBreakpointNodeIds(ids: readonly string[], knownNodeIds?: ReadonlySet<string>): string[] {
  const resolveSetInstance = () => {
    const filterResult = ids
      .filter((id) => typeof id === 'string');
    const mapResult = filterResult.map((id) => {
      const trimResult: string = id.trim();
      return trimResult;
    });
    const filterResult1: Iterable<string> | null | undefined = mapResult.filter((id) => {
      const evaluatedValue: boolean = id.length > 0 && (!knownNodeIds || knownNodeIds.has(id));
      return evaluatedValue;
    });
    const setInstance = new Set(filterResult1);
    return setInstance;
  };
  const evaluatedValue1 = [...resolveSetInstance()];
  const sortResult: string[] = evaluatedValue1.sort((left, right) => {
    const localeCompareResult: number = left.localeCompare(right);
    return localeCompareResult;
  });
  return sortResult;
}
export function loadBreakpointNodeIds(projectId: string | undefined, flowId: string | undefined, knownNodeIds: ReadonlySet<string>): string[] {
  const key = scopedKey(breakpointStoragePrefix, projectId, flowId);
  if (!key || typeof window === 'undefined') {
    return [];
  }
  try {
    const evaluatedValue2: string = window.localStorage.getItem(key) ?? '[]';
    const value: unknown = JSON.parse(evaluatedValue2);
    const resolveNormalizeBreakpointNodeIdsResult = (storedNodeIds: unknown[]) => {
      const filterResult2: readonly string[] = storedNodeIds.filter((id): id is string => typeof id === 'string');
      const normalizeBreakpointNodeIdsResult1: string[] = normalizeBreakpointNodeIds(filterResult2, knownNodeIds);
      return normalizeBreakpointNodeIdsResult1;
    };
    const evaluatedValue3: string[] = Array.isArray(value)
      ? resolveNormalizeBreakpointNodeIdsResult(value) : [];
    return evaluatedValue3;
  }
  catch {
    return [];
  }
}
export function saveBreakpointNodeIds(projectId: string | undefined, flowId: string | undefined, ids: readonly string[]): void {
  const key = scopedKey(breakpointStoragePrefix, projectId, flowId);
  if (!key || typeof window === 'undefined') {
    return;
  }
  try {
    const argumentValue: string = key;
    const normalizeBreakpointNodeIdsResult = normalizeBreakpointNodeIds(ids);
    const stringifyResult: string = JSON.stringify(normalizeBreakpointNodeIdsResult);
    window.localStorage.setItem(argumentValue, stringifyResult);
  }
  catch {
  }
}
export function loadStoredDebugSessionId(projectId?: string, flowId?: string): string | undefined {
  const key = scopedKey(sessionStoragePrefix, projectId, flowId);
  if (!key || typeof window === 'undefined') {
    return undefined;
  }
  const storedValue = window.localStorage.getItem(key);
  const value = storedValue?.trim();
  return value || undefined;
}
export function saveStoredDebugSessionId(projectId: string | undefined, flowId: string | undefined, sessionId: string): void {
  const key = scopedKey(sessionStoragePrefix, projectId, flowId);
  if (!key || typeof window === 'undefined' || !sessionId.trim()) {
    return;
  }
  window.localStorage.setItem(key, sessionId);
}
export function clearStoredDebugSessionId(projectId?: string, flowId?: string): void {
  const key = scopedKey(sessionStoragePrefix, projectId, flowId);
  if (!key || typeof window === 'undefined') {
    return;
  }
  window.localStorage.removeItem(key);
}
