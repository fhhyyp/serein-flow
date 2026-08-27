<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { AlertCircle, CheckCircle2, CircleDashed, Copy, Terminal } from 'lucide-vue-next'
import { locale, t } from '../../i18n'
import type { FlowRunEventDto } from '../../api/flowApi'

type EventTone = 'running' | 'success' | 'failed' | 'idle'

const props = defineProps<{
  events: FlowRunEventDto[]
  error?: string
}>()

const selectedSequence = ref<number>()
const copyFeedback = ref('')
let copyFeedbackTimer: number | undefined

const orderedEvents = computed(() => [...props.events].sort((left, right) => left.sequence - right.sequence))
const selectedEvent = computed(() => orderedEvents.value.find((event) => event.sequence === selectedSequence.value))

watch(orderedEvents, (events) => {
  if (events.some((event) => event.sequence === selectedSequence.value)) return
  selectedSequence.value = events.at(-1)?.sequence
}, { immediate: true })

function eventTone(event: FlowRunEventDto): EventTone {
  const type = event.type.toLowerCase()
  if (type.includes('error') || type.includes('failed')) return 'failed'
  if (type.includes('completed') || type.includes('succeeded')) return 'success'
  if (type.includes('started') || type.includes('running') || type.includes('queued')) return 'running'
  return 'idle'
}

function eventIcon(event: FlowRunEventDto) {
  const tone = eventTone(event)
  return tone === 'success' ? CheckCircle2 : tone === 'failed' ? AlertCircle : tone === 'running' ? CircleDashed : Terminal
}

function formatTimestamp(value: string): string {
  const timestamp = new Date(value)
  if (Number.isNaN(timestamp.getTime())) return value
  return new Intl.DateTimeFormat(locale.value, { dateStyle: 'short', timeStyle: 'medium' }).format(timestamp)
}

function formatPayload(payload: string): string {
  if (!payload.trim()) return t('console.snapshotEventPayloadEmpty')
  try {
    return JSON.stringify(JSON.parse(payload), null, 2)
  } catch {
    return payload
  }
}

function selectEvent(event: FlowRunEventDto): void {
  selectedSequence.value = event.sequence
}

function copyPayload(): void {
  if (!selectedEvent.value?.payloadJson || !navigator.clipboard) return
  void navigator.clipboard.writeText(selectedEvent.value.payloadJson)
    .then(() => {
      copyFeedback.value = t('console.snapshotEventCopied')
      if (copyFeedbackTimer !== undefined) window.clearTimeout(copyFeedbackTimer)
      copyFeedbackTimer = window.setTimeout(() => { copyFeedback.value = '' }, 2_000)
    })
    .catch(() => { copyFeedback.value = '' })
}
</script>

<template>
  <section class="run-snapshot-output-viewer" :aria-label="t('console.snapshotRunOutput')">
    <header class="run-snapshot-output-viewer__header">
      <div>
        <p>{{ t('console.snapshotRunOutput') }}</p>
        <strong>{{ orderedEvents.length }} {{ t('output.records') }}</strong>
      </div>
      <span class="run-snapshot-output-viewer__scope">{{ t('console.snapshotEventRun') }}</span>
    </header>

    <p v-if="error" class="run-snapshot-output-viewer__error" role="alert">{{ error }}</p>

    <template v-else-if="orderedEvents.length">
      <ol class="run-snapshot-output-list" :aria-label="t('console.snapshotRunOutput')">
        <li v-for="event in orderedEvents" :key="event.sequence">
          <button
            type="button"
            :class="['run-snapshot-output-list__item', `status-${eventTone(event)}`, { selected: selectedSequence === event.sequence }]"
            :aria-pressed="selectedSequence === event.sequence"
            @click="selectEvent(event)"
          >
            <span class="run-snapshot-output-list__icon"><component :is="eventIcon(event)" :size="14" /></span>
            <span class="run-snapshot-output-list__copy"><strong>{{ event.type }}</strong><span>{{ event.nodeId || t('console.snapshotEventRun') }}</span></span>
            <span class="run-snapshot-output-list__sequence">#{{ event.sequence }}</span>
          </button>
        </li>
      </ol>

      <section v-if="selectedEvent" class="run-snapshot-output-detail" :aria-label="t('console.snapshotEventDetails')">
        <header>
          <div>
            <p>{{ t('console.snapshotEventDetails') }}</p>
            <strong>{{ selectedEvent.type }}</strong>
          </div>
          <button type="button" class="run-snapshot-output-detail__copy" :title="t('console.snapshotCopyEventPayload')" :aria-label="t('console.snapshotCopyEventPayload')" @click="copyPayload"><Copy :size="14" /></button>
        </header>
        <dl class="run-snapshot-output-detail__meta">
          <div><dt>{{ t('console.snapshotEventNode') }}</dt><dd>{{ selectedEvent.nodeId || t('console.snapshotEventRun') }}</dd></div>
          <div><dt>{{ t('console.snapshotEventTime') }}</dt><dd>{{ formatTimestamp(selectedEvent.timestamp) }}</dd></div>
        </dl>
        <div class="run-snapshot-output-detail__payload">
          <span>{{ t('console.snapshotEventPayload') }}</span>
          <pre>{{ formatPayload(selectedEvent.payloadJson) }}</pre>
        </div>
        <p class="run-snapshot-output-detail__feedback" aria-live="polite">{{ copyFeedback }}</p>
      </section>
    </template>

    <p v-else class="run-snapshot-output-viewer__empty"><Terminal :size="17" />{{ t('console.snapshotRunOutputEmpty') }}</p>
  </section>
</template>
