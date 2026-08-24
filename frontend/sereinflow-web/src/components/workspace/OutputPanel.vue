<script setup lang="ts">
import { Code2, Terminal } from 'lucide-vue-next'
import { t } from '../../i18n'

interface RunEvent {
  time: string
  label: string
  detail: string
  success?: boolean
}

const props = defineProps<{
  activeOutput: 'events' | 'payload'
  runEvents: RunEvent[]
  runPayload: string
  hasRunOutput: boolean
}>()

const emit = defineEmits<{
  'update:activeOutput': [value: 'events' | 'payload']
}>()

function selectOutput(value: 'events' | 'payload'): void {
  emit('update:activeOutput', value)
}
</script>

<template>
  <section class="output-panel">
    <div class="output-tabs" role="tablist" :aria-label="t('output.eventsLabel')">
      <button type="button" :class="{ active: props.activeOutput === 'events' }" @click="selectOutput('events')">
        <Terminal :size="14" />{{ t('output.events') }}<span class="tab-count">{{ props.runEvents.length }}</span>
      </button>
      <button type="button" :class="{ active: props.activeOutput === 'payload' }" @click="selectOutput('payload')">
        <Code2 :size="14" />{{ t('output.payload') }}
      </button>
      <span class="output-spacer"></span>
      <span v-if="props.hasRunOutput" class="run-label"><span class="status-dot"></span>{{ t('output.lastRunSucceeded') }}<span class="mono">preview</span></span>
      <span v-else class="run-label output-idle"><span class="status-dot"></span>{{ t('output.waiting') }}</span>
    </div>
    <div class="output-content">
      <template v-if="props.activeOutput === 'events'">
        <div v-if="props.runEvents.length === 0" class="output-empty"><Terminal :size="15" /><span>{{ t('output.emptyEvents') }}</span></div>
        <div v-else class="output-event-list">
          <div v-for="event in props.runEvents" :key="`${event.time}-${event.label}`" class="event-row">
            <span class="event-time mono">{{ event.time }}</span>
            <span class="event-dot" :class="{ success: event.success }"></span>
            <strong>{{ event.label }}</strong>
            <span class="event-detail">{{ event.detail }}</span>
          </div>
        </div>
      </template>
      <div v-else-if="!props.runPayload" class="output-empty"><Code2 :size="15" /><span>{{ t('output.emptyPayload') }}</span></div>
      <pre v-else class="payload-preview">{{ props.runPayload }}</pre>
    </div>
  </section>
</template>
