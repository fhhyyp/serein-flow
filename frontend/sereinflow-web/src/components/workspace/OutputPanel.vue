<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { AlertCircle, AlertTriangle, CheckCircle2, ChevronDown, ChevronUp, CircleDashed, Code2, Copy, Maximize2, Minimize2, Terminal } from 'lucide-vue-next'
import { t } from '../../i18n'
import type { FlowValidationDiagnostic } from '../../api/flowApi'
import type { CanvasState } from '../../flow/types'
import FlowValidationDiagnostics from '../canvas/FlowValidationDiagnostics.vue'

interface RunEvent {
  time: string
  label: string
  detail: string
  success?: boolean
  status?: 'running' | 'success' | 'failed' | 'error' | 'idle'
  sequence?: number
  nodeId?: string
  logLevel?: 'info' | 'error'
  message?: string
}

type OutputPanelView = 'output' | 'diagnostics'

const props = defineProps<{
  activeOutput: 'events' | 'payload'
  runEvents: RunEvent[]
  runPayload: string
  hasRunOutput: boolean
  diagnostics: FlowValidationDiagnostic[]
  canvases: CanvasState[]
}>()

const emit = defineEmits<{
  'update:activeOutput': [value: 'events' | 'payload']
  'dismiss-diagnostics': []
  'locate-diagnostic': [diagnostic: FlowValidationDiagnostic]
}>()

const collapsed = ref(false)
const maximized = ref(false)
const panelHeight = ref(300)
const resizing = ref(false)
const eventFilter = ref<'all' | 'success' | 'error'>('all')
const selectedEventKey = ref<string>()
const activePanelView = ref<OutputPanelView>('output')
let stopResize: (() => void) | undefined

watch(
  () => props.diagnostics.length,
  (count) => {
    activePanelView.value = count > 0 ? 'diagnostics' : 'output'
  },
  { immediate: true },
)

const visibleEvents = computed(() => props.runEvents.filter((event) => {
  if (eventFilter.value === 'all') return true
  if (eventFilter.value === 'success') return event.status === 'success' || event.success === true
  return event.status === 'error' || event.status === 'failed' || event.label.includes('error') || event.label.includes('failed')
}))

const selectedEvent = computed(() => props.runEvents.find((event) => eventKey(event) === selectedEventKey.value))

function eventKey(event: RunEvent): string {
  return `${event.sequence ?? event.time}-${event.label}-${event.nodeId ?? ''}`
}

function eventStatus(event: RunEvent): 'running' | 'success' | 'failed' | 'error' | 'idle' {
  if (event.status) return event.status
  if (event.success) return 'success'
  if (event.logLevel === 'error') return 'error'
  if (event.label.includes('error')) return 'error'
  if (event.label.includes('failed')) return 'failed'
  if (event.label.includes('started')) return 'running'
  return 'idle'
}

function eventTitle(event: RunEvent): string {
  return event.message ?? event.label
}

function eventContext(event: RunEvent): string {
  if (!event.logLevel) return event.nodeId || t('output.runEvent')
  const level = t(`output.log.${event.logLevel}`)
  const node = event.nodeId || t('output.runEvent')
  return `${level} · ${node}`
}

function eventIcon(event: RunEvent) {
  const status = eventStatus(event)
  return status === 'success' ? CheckCircle2 : status === 'error' || status === 'failed' ? AlertCircle : status === 'running' ? CircleDashed : Terminal
}

function selectEvent(event: RunEvent): void {
  selectedEventKey.value = eventKey(event)
}

function beginResize(event: PointerEvent): void {
  if (maximized.value) return
  event.preventDefault()
  resizing.value = true
  const startY = event.clientY
  const startHeight = panelHeight.value
  const onMove = (moveEvent: PointerEvent) => {
    panelHeight.value = Math.max(180, Math.min(Math.round(window.innerHeight * 0.72), startHeight + startY - moveEvent.clientY))
  }
  const onUp = () => {
    resizing.value = false
    window.removeEventListener('pointermove', onMove)
    window.removeEventListener('pointerup', onUp)
    if (stopResize === onUp) stopResize = undefined
  }
  stopResize?.()
  stopResize = onUp
  window.addEventListener('pointermove', onMove)
  window.addEventListener('pointerup', onUp)
}

function copyPayload(): void {
  if (!props.runPayload || typeof navigator === 'undefined' || !navigator.clipboard) return
  void navigator.clipboard.writeText(props.runPayload)
}

function toggleCollapsed(): void {
  collapsed.value = !collapsed.value
}

function toggleMaximized(): void {
  maximized.value = !maximized.value
  collapsed.value = false
}

onBeforeUnmount(() => {
  stopResize?.()
})

function selectOutput(value: 'events' | 'payload'): void {
  emit('update:activeOutput', value)
}

function selectPanelView(value: OutputPanelView): void {
  activePanelView.value = value
}
</script>

<template>
  <section class="output-panel" :class="{ 'is-collapsed': collapsed, 'is-maximized': maximized, 'is-resizing': resizing }" :style="{ '--output-height': `${panelHeight}px` }">
    <div class="output-resize-grip" role="separator" aria-orientation="horizontal" :aria-label="t('output.resize')" @pointerdown="beginResize"><span></span></div>
    <div class="output-header">
      <div class="output-heading">
        <span class="output-heading__mark"><Terminal :size="16" /></span>
        <div><strong>{{ t('output.eventsLabel') }}</strong><span>{{ props.runEvents.length }} {{ t('output.records') }}</span></div>
      </div>
      <div class="output-controls">
        <span v-if="props.hasRunOutput" class="run-label"><span class="status-dot"></span>{{ t('output.lastRunSucceeded') }}</span>
        <span v-else class="run-label output-idle"><span class="status-dot"></span>{{ t('output.waiting') }}</span>
        <button type="button" class="output-icon-button" :title="maximized ? t('output.restore') : t('output.maximize')" :aria-label="maximized ? t('output.restore') : t('output.maximize')" @click="toggleMaximized"><Minimize2 v-if="maximized" :size="15" /><Maximize2 v-else :size="15" /></button>
        <button type="button" class="output-icon-button" :title="collapsed ? t('output.expand') : t('output.collapse')" :aria-label="collapsed ? t('output.expand') : t('output.collapse')" @click="toggleCollapsed"><ChevronUp v-if="collapsed" :size="16" /><ChevronDown v-else :size="16" /></button>
      </div>
    </div>
    <div v-if="!collapsed" class="output-body">
      <div class="output-tabs" role="tablist" :aria-label="t('output.eventsLabel')">
        <button type="button" role="tab" :aria-selected="activePanelView === 'output'" :class="{ active: activePanelView === 'output' }" @click="selectPanelView('output')">
          <Terminal :size="14" />{{ t('output.eventsLabel') }}<span class="tab-count">{{ props.runEvents.length }}</span>
        </button>
        <button type="button" role="tab" :aria-selected="activePanelView === 'diagnostics'" :class="['output-diagnostics-tab', { active: activePanelView === 'diagnostics' }]" @click="selectPanelView('diagnostics')">
          <AlertTriangle :size="14" />{{ t('diagnostics.tab') }}<span class="tab-count">{{ props.diagnostics.length }}</span>
        </button>
      </div>
      <template v-if="activePanelView === 'output'">
        <div class="output-mode-tabs" role="tablist" :aria-label="t('output.eventsLabel')">
          <button type="button" role="tab" :aria-selected="props.activeOutput === 'events'" :class="{ active: props.activeOutput === 'events' }" @click="selectOutput('events')"><Terminal :size="13" />{{ t('output.events') }}</button>
          <button type="button" role="tab" :aria-selected="props.activeOutput === 'payload'" :class="{ active: props.activeOutput === 'payload' }" @click="selectOutput('payload')"><Code2 :size="13" />{{ t('output.payload') }}</button>
        </div>
        <div v-if="props.activeOutput === 'events'" class="output-toolbar">
          <span class="output-toolbar__label">{{ t('output.filter') }}</span>
          <button v-for="filter in ['all', 'success', 'error'] as const" :key="filter" type="button" class="filter-chip" :class="{ active: eventFilter === filter }" @click="eventFilter = filter">{{ t(`output.filter.${filter}`) }}</button>
          <span class="output-toolbar__spacer"></span><span class="output-toolbar__hint">{{ t('output.selectHint') }}</span>
        </div>
        <div class="output-content">
          <template v-if="props.activeOutput === 'events'">
            <div v-if="visibleEvents.length === 0" class="output-empty"><Terminal :size="17" /><span>{{ props.runEvents.length === 0 ? t('output.emptyEvents') : t('output.emptyFiltered') }}</span></div>
            <div v-else class="output-event-layout">
              <div class="output-event-list">
                <button v-for="event in visibleEvents" :key="eventKey(event)" type="button" class="event-row" :class="[`status-${eventStatus(event)}`, { selected: selectedEventKey === eventKey(event) }]" @click="selectEvent(event)">
                  <span class="event-time mono">{{ event.time }}</span>
                  <span class="event-icon"><component :is="eventIcon(event)" :size="14" /></span>
                  <span class="event-copy"><strong>{{ eventTitle(event) }}</strong><span>{{ eventContext(event) }}</span></span>
                  <span v-if="event.sequence" class="event-sequence mono">#{{ event.sequence }}</span>
                </button>
              </div>
              <aside v-if="selectedEvent" class="event-detail-card">
                <div class="event-detail-card__header"><span>{{ t('output.eventDetail') }}</span><span class="mono">#{{ selectedEvent.sequence ?? '—' }}</span></div>
                <strong>{{ eventTitle(selectedEvent) }}</strong><span class="event-detail-card__node">{{ eventContext(selectedEvent) }} · {{ selectedEvent.time }}</span>
                <pre>{{ selectedEvent.detail }}</pre>
              </aside>
            </div>
          </template>
          <template v-else>
            <div class="payload-toolbar"><span>{{ t('output.payloadHint') }}</span><button type="button" class="output-icon-button" :title="t('output.copyPayload')" :aria-label="t('output.copyPayload')" :disabled="!props.runPayload" @click="copyPayload"><Copy :size="14" /></button></div>
            <div v-if="!props.runPayload" class="output-empty"><Code2 :size="17" /><span>{{ t('output.emptyPayload') }}</span></div>
            <pre v-else class="payload-preview">{{ props.runPayload }}</pre>
          </template>
        </div>
      </template>
      <div v-else class="output-content output-diagnostics-content">
        <FlowValidationDiagnostics :diagnostics="props.diagnostics" :canvases="props.canvases" @dismiss="emit('dismiss-diagnostics')" @locate="emit('locate-diagnostic', $event)" />
      </div>
    </div>
  </section>
</template>
