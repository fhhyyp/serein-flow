import { computed, ref, type Ref } from 'vue'
import { locale, t } from '../i18n'
import type { FlowNode } from '../flow/types'

export interface RunEvent {
  time: string
  label: string
  detail: string
  success?: boolean
}

interface UseFlowRunnerOptions {
  nodes: Ref<FlowNode[]>
  notice: Ref<string>
}

export function useFlowRunner(options: UseFlowRunnerOptions) {
  const isRunning = ref(false)
  const activeOutput = ref<'events' | 'payload'>('events')
  const runEvents = ref<RunEvent[]>([])
  const runPayload = ref('')
  const hasRunOutput = computed(() => runEvents.value.length > 0)

  function runFlow(): void {
    if (options.nodes.value.length === 0) {
      options.notice.value = t('canvas.noNodesToRun')
      return
    }

    isRunning.value = !isRunning.value
    if (isRunning.value) {
      runEvents.value = []
      runPayload.value = ''
      options.nodes.value = options.nodes.value.map((node, index) => ({
        ...node,
        data: { ...node.data, status: index === 0 ? 'running' : 'idle' },
      }))
      return
    }

    const time = new Date().toLocaleTimeString(locale.value === 'zh-CN' ? 'zh-CN' : 'en-US', { hour12: false })
    options.nodes.value = options.nodes.value.map((node) => ({ ...node, data: { ...node.data, status: 'success' } }))
    runEvents.value = [
      { time, label: t('output.runCompleted'), detail: t('output.nodesDuration', { count: options.nodes.value.length, duration: 'preview' }), success: true },
    ]
    runPayload.value = JSON.stringify({ status: 'preview', nodes: options.nodes.value.length }, null, 2)
  }

  return {
    isRunning,
    activeOutput,
    runEvents,
    runPayload,
    hasRunOutput,
    runFlow,
  }
}
