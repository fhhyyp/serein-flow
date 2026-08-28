import type { FlowRunEventDto } from '../api/flowApi'

export type NodeExecutionStatus = 'running' | 'paused' | 'completed' | 'failed' | 'error'

export interface NodeExecutionState {
  id: string
  nodeId: string
  status: NodeExecutionStatus
  startedAt?: string
  endedAt?: string
  startSequence?: number
  terminalSequence?: number
  pauseSequence?: number
  step?: number
  frameDepth?: number
  triggerInvocationId?: string
  branch?: string
  inputs?: unknown
  outputs?: unknown
  errorCode?: string
  errorMessage?: string
  isGlobal?: boolean
}

type EventPayload = Record<string, unknown>

const terminalStatuses: Record<string, Extract<NodeExecutionStatus, 'completed' | 'failed' | 'error'>> = {
  'node.completed': 'completed',
  'node.failed': 'failed',
  'node.error': 'error',
}

export function buildNodeExecutionStates(events: readonly FlowRunEventDto[]): NodeExecutionState[] {
  const states: NodeExecutionState[] = []
  const orderedEvents = [...events].sort((left, right) => left.sequence - right.sequence)

  for (const event of orderedEvents) {
    if (event.type !== 'node.started' && event.type !== 'debug.paused' && !(event.type in terminalStatuses)) continue

    const payload = parsePayload(event.payloadJson)
    const nodeId = event.nodeId?.trim() || stringValue(payload?.nodeId)?.trim()
    if (!nodeId) continue

    if (event.type === 'node.started') {
      states.push(createState(nodeId, event, payload, 'running'))
      continue
    }

    const matching = findMatchingState(states, nodeId, payload)
    if (event.type === 'debug.paused') {
      const state = matching ?? createState(nodeId, event, payload, 'paused')
      if (!matching) states.push(state)
      updateContext(state, payload)
      state.status = 'paused'
      state.pauseSequence = event.sequence
      continue
    }

    const status = terminalStatuses[event.type]
    const state = matching ?? createState(nodeId, event, payload, status)
    if (!matching) states.push(state)
    updateContext(state, payload)
    state.status = status
    state.endedAt = event.timestamp
    state.terminalSequence = event.sequence
  }

  return states
}

function createState(
  nodeId: string,
  event: FlowRunEventDto,
  payload: EventPayload | undefined,
  status: NodeExecutionStatus,
): NodeExecutionState {
  const step = numberValue(payload?.step)
  const invocationId = stringValue(payload?.triggerInvocationId)
  const state: NodeExecutionState = {
    id: executionId(nodeId, event.sequence, step, invocationId),
    nodeId,
    status,
    step,
    frameDepth: numberValue(payload?.frameDepth),
    triggerInvocationId: invocationId,
    isGlobal: booleanValue(payload?.global),
  }

  if (status === 'running' || status === 'paused') {
    state.startedAt = event.timestamp
    state.startSequence = event.sequence
  } else {
    state.endedAt = event.timestamp
    state.terminalSequence = event.sequence
  }
  updateContext(state, payload)
  return state
}

function findMatchingState(
  states: readonly NodeExecutionState[],
  nodeId: string,
  payload: EventPayload | undefined,
): NodeExecutionState | undefined {
  const candidates = states.filter((state) => state.nodeId === nodeId && !isTerminal(state.status))
  if (candidates.length === 0) return undefined

  const step = numberValue(payload?.step)
  const invocationId = stringValue(payload?.triggerInvocationId)
  if (step !== undefined || invocationId !== undefined) {
    const exact = [...candidates].reverse().find((state) => (
      (step === undefined || state.step === step)
      && (invocationId === undefined || state.triggerInvocationId === invocationId)
    ))
    if (exact) return exact
  }

  return candidates.at(-1)
}

function updateContext(state: NodeExecutionState, payload: EventPayload | undefined): void {
  if (!payload) return
  const step = numberValue(payload.step)
  const frameDepth = numberValue(payload.frameDepth)
  const invocationId = stringValue(payload.triggerInvocationId)
  const branch = stringValue(payload.branch)
  const errorCode = stringValue(payload.errorCode)
  const errorMessage = stringValue(payload.errorMessage)

  if (step !== undefined) state.step = step
  if (frameDepth !== undefined) state.frameDepth = frameDepth
  if (invocationId !== undefined) state.triggerInvocationId = invocationId
  if (branch !== undefined) state.branch = branch
  if (errorCode !== undefined) state.errorCode = errorCode
  if (errorMessage !== undefined) state.errorMessage = errorMessage
  if (typeof payload.global === 'boolean') state.isGlobal = payload.global
  if (Object.hasOwn(payload, 'inputs')) state.inputs = payload.inputs
  if (Object.hasOwn(payload, 'outputs')) state.outputs = payload.outputs
}

function executionId(nodeId: string, sequence: number, step?: number, invocationId?: string): string {
  return `${nodeId}:${invocationId ?? 'root'}:${step === undefined ? `event-${sequence}` : `step-${step}`}`
}

function isTerminal(status: NodeExecutionStatus): boolean {
  return status === 'completed' || status === 'failed' || status === 'error'
}

function parsePayload(value: string): EventPayload | undefined {
  try {
    const parsed: unknown = JSON.parse(value)
    return isRecord(parsed) ? parsed : undefined
  } catch {
    return undefined
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function stringValue(value: unknown): string | undefined {
  return typeof value === 'string' ? value : undefined
}

function numberValue(value: unknown): number | undefined {
  return typeof value === 'number' && Number.isFinite(value) ? value : undefined
}

function booleanValue(value: unknown): boolean | undefined {
  return typeof value === 'boolean' ? value : undefined
}
