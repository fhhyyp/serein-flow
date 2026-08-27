export type RuntimeLogLevel = 'info' | 'error'

export interface RuntimeLog {
  level: RuntimeLogLevel
  message?: string
}

export function parseRuntimeLog(type: string, payloadJson: string): RuntimeLog | undefined {
  if (type !== 'log' && type !== 'node.log') return undefined

  try {
    const payload: unknown = JSON.parse(payloadJson)
    if (!payload || typeof payload !== 'object') return undefined
    const value = payload as Record<string, unknown>
    const level: RuntimeLogLevel = value.level === 'error' ? 'error' : 'info'
    return {
      level,
      message: typeof value.message === 'string' && value.message.trim() ? value.message : undefined,
    }
  } catch {
    return undefined
  }
}
