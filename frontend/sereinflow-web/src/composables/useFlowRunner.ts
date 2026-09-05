import { computed, ref, type Ref } from 'vue'
import { locale, t } from '../i18n'
import type { FlowNode } from '../flow/types'
import { parseRuntimeLog, type RuntimeLogLevel } from '../flow/runtimeLog'
import {
  cancelFlowRun,
  getFlowRun,
  listFlowRunEvents,
  startFlowRun,
  subscribeFlowRunEvents,
  type FlowRunDto,
  type FlowRunEventDto,
} from '../api/flowApi'

export interface RunEvent {
  time: string
  label: string
  detail: string
  success?: boolean
  status?: 'running' | 'success' | 'failed' | 'error' | 'idle'
  sequence?: number
  nodeId?: string
  logLevel?: RuntimeLogLevel
  message?: string
}

interface UseFlowRunnerOptions {
  nodes: Ref<FlowNode[]>
  notice: Ref<string>
  projectId: Ref<string | undefined>
  flowId: Ref<string | undefined>
  flowVersion: Ref<number>
}

export function useFlowRunner(options: UseFlowRunnerOptions) {
  const isRunning = ref(false)
  const isCancelling = ref(false)
  const activeOutput = ref<'events' | 'payload'>('events')
  const runEvents = ref<RunEvent[]>([])
  const runPayload = ref('')
  const hasRunOutput = computed(() => runEvents.value.length > 0)
  const lastRunId = ref<string>()
  let activeRunId: string | undefined
  let unsubscribe: (() => void) | undefined
  let eventPollTimer: ReturnType<typeof setTimeout> | undefined
  let pollInFlight = false
  let lastEventSequence = 0

  const terminalStatuses = new Set<FlowRunDto['status']>(['succeeded', 'failed', 'cancelled', 'timedOut'])

  function stopEventPolling(): void {
    if (eventPollTimer !== undefined) {
      clearTimeout(eventPollTimer)
      eventPollTimer = undefined
    }
  }

  function finishRun(runId: string): void {
    if (activeRunId !== runId) return
    isRunning.value = false
    isCancelling.value = false
    stopEventPolling()
    unsubscribe?.()
    unsubscribe = undefined
    activeRunId = undefined
  }

  async function pollRun(runId: string): Promise<void> {
    if (activeRunId !== runId || !isRunning.value || pollInFlight) return
    pollInFlight = true
    try {
      // SignalR/SSE is the low-latency path. This persisted-event poll is a
      // small safety net for proxy failures, short runs that finish before a
      // subscription is established, and lost terminal notifications.
      const [events, run] = await Promise.all([
        listFlowRunEvents(runId, lastEventSequence),
        getFlowRun(runId),
      ])
      events.forEach(handleEvent)
      if (terminalStatuses.has(run.status) && activeRunId === runId) {
        finishRun(runId)
      }
    } catch {
      // Keep polling while the run is active. A transient API/network error
      // must not leave the UI permanently stuck in the running state.
    } finally {
      pollInFlight = false
      if (activeRunId === runId && isRunning.value) {
        eventPollTimer = setTimeout(() => { void pollRun(runId) }, 500)
      }
    }
  }

  async function runFlow(): Promise<void> {
    if (options.nodes.value.length === 0) {
      options.notice.value = t('canvas.noNodesToRun')
      return
    }

    if (isRunning.value) {
      if (isCancelling.value) return
      isCancelling.value = true
      if (activeRunId) {
        try {
          await cancelFlowRun(activeRunId)
        } catch {
          // A 409 means the worker may have completed between the status
          // check and the cancel request. Polling below will reconcile the
          // final state and reset the button.
          options.notice.value = t('canvas.runCancelFailed')
        }
        if (activeRunId) void pollRun(activeRunId)
      }
      return
    }

    if (!options.projectId.value || !options.flowId.value) {
      options.notice.value = t('canvas.saveBeforeRun')
      return
    }

    runEvents.value = []
    runPayload.value = ''
    options.nodes.value = options.nodes.value.map((node) => ({
      ...node,
      data: { ...node.data, status: 'idle' },
    }))

    try {
      const run = await startFlowRun(options.projectId.value, options.flowId.value, {
        expectedFlowVersion: options.flowVersion.value,
      })
      activeRunId = run.id
      lastRunId.value = run.id
      isRunning.value = true
      isCancelling.value = false
      lastEventSequence = 0
      pollInFlight = false
      unsubscribe = subscribeFlowRunEvents(run.id, handleEvent, () => {
        options.notice.value = t('canvas.runStreamFailed')
      })
      void pollRun(run.id)
      options.notice.value = t('canvas.runStarted')
    } catch {
      options.notice.value = t('canvas.runStartFailed')
      isRunning.value = false
      isCancelling.value = false
    }
  }

  function handleEvent(event: FlowRunEventDto): void {
    if (event.sequence <= lastEventSequence) return
    lastEventSequence = event.sequence
    const time = new Date(event.timestamp).toLocaleTimeString(locale.value === 'zh-CN' ? 'zh-CN' : 'en-US', { hour12: false })
    const success = event.type === 'node.completed' || event.type === 'run.completed'
    const log = parseRuntimeLog(event.type, event.payloadJson)
    runEvents.value = [...runEvents.value, {
      time,
      label: event.type,
      detail: event.payloadJson,
      success,
      status: log?.level === 'error'
        ? 'error'
        : event.type === 'node.started'
        ? 'running'
        : success
          ? 'success'
          : event.type === 'node.error' || event.type === 'run.failed' || event.type === 'run.timed_out'
            ? 'error'
            : event.type === 'node.failed' || event.type === 'run.cancelled'
              ? 'failed'
              : 'idle',
      sequence: event.sequence,
      nodeId: event.nodeId ?? undefined,
      logLevel: log?.level,
      message: log?.message,
    }]
    runPayload.value = JSON.stringify(event, null, 2)
    if (event.nodeId && (event.type === 'node.started' || event.type === 'node.completed' || event.type === 'node.failed' || event.type === 'node.error')) {
      const status = event.type === 'node.started' ? 'running' : event.type === 'node.completed' ? 'success' : event.type === 'node.error' ? 'error' : 'failed'
      options.nodes.value = options.nodes.value.map((node) => node.id === event.nodeId
        ? { ...node, data: { ...node.data, status } }
        : node)
    }
    if (event.type === 'run.completed' || event.type === 'run.failed' || event.type === 'run.cancelled' || event.type === 'run.timed_out') {
      finishRun(event.runId)
    }
  }

  return {
    isRunning,
    isCancelling,
    activeOutput,
    runEvents,
    runPayload,
    hasRunOutput,
    lastRunId,
    runFlow,
  }
}
