export interface FlowValidationDiagnosticTarget {
  nodeId: string
  parameterName?: string
}

/**
 * Translates the API validation path into the canvas node and parameter target.
 * 将 API 校验路径转换为画布中的节点和参数目标。
 */
export function parseFlowValidationDiagnosticTarget(path?: string | null): FlowValidationDiagnosticTarget | undefined {
  if (!path?.startsWith('nodes.')) {
    return undefined
  }

  const value = path.slice('nodes.'.length)
  const parameterMarker = '.parameters.'
  const parameterIndex = value.indexOf(parameterMarker)
  const nodeFieldIndex = value.indexOf('.')
  const nodeId = parameterIndex >= 0
    ? value.slice(0, parameterIndex)
    : nodeFieldIndex >= 0 ? value.slice(0, nodeFieldIndex) : value
  if (!nodeId) {
    return undefined
  }

  if (parameterIndex < 0) {
    return { nodeId }
  }

  const parameterName = value.slice(parameterIndex + parameterMarker.length)
  return parameterName ? { nodeId, parameterName } : { nodeId }
}
