<script setup lang="ts">
type TemplateItem<T> = T extends readonly (infer Item)[] ? Item : T extends Record<string, infer Value> ? Value : T extends number ? number : never;
import { computed, ref, watch } from 'vue';
import { AlertCircle, CheckCircle2, CircleDashed, Copy, Terminal } from 'lucide-vue-next';
import { locale, t } from '../../i18n';
import type { FlowRunEventDto } from '../../api/flowApi';
import { parseRuntimeLog } from '../../flow/runtimeLog';
type EventTone = 'running' | 'success' | 'failed' | 'error' | 'idle';
const props = defineProps<{
  events: FlowRunEventDto[];
  error?: string;
}>();
const selectedSequence = ref<number>();
const copyFeedback = ref('');
let copyFeedbackTimer: number | undefined;
const orderedEvents = computed(() => {
  const sortResult: FlowRunEventDto[] = [...props.events].sort((left, right) => left.sequence - right.sequence);
  return sortResult;
});
const selectedEvent = computed(() => {
  const findResult: FlowRunEventDto | undefined = orderedEvents.value.find((event) => event.sequence === selectedSequence.value);
  return findResult;
});
watch(orderedEvents, (events) => {
  if (events.some((event) => event.sequence === selectedSequence.value)) {
    return;
  }
  selectedSequence.value = events.at(-1)?.sequence;
}, { immediate: true });
function eventTone(event: FlowRunEventDto): EventTone {
  const log = parseRuntimeLog(event.type, event.payloadJson);
  if (log?.level === 'error') {
    return 'error';
  }
  const type = event.type.toLowerCase();
  if (type.includes('error') || type.includes('failed')) {
    return 'failed';
  }
  if (type.includes('completed') || type.includes('succeeded')) {
    return 'success';
  }
  if (type.includes('started') || type.includes('running') || type.includes('queued')) {
    return 'running';
  }
  return 'idle';
}
function eventIcon(event: FlowRunEventDto) {
  const tone = eventTone(event);
  return tone === 'success' ? CheckCircle2 : tone === 'failed' || tone === 'error' ? AlertCircle : tone === 'running' ? CircleDashed : Terminal;
}
function eventTitle(event: FlowRunEventDto): string {
  const evaluatedValue: string = parseRuntimeLog(event.type, event.payloadJson)?.message ?? event.type;
  return evaluatedValue;
}
function eventContext(event: FlowRunEventDto): string {
  const log = parseRuntimeLog(event.type, event.payloadJson);
  if (!log) {
    const evaluatedValue1: string = event.nodeId || t('console.snapshotEventRun');
    return evaluatedValue1;
  }
  const evaluatedValue2: string = `${t(`output.log.${log.level}`)} · ${event.nodeId || t('console.snapshotEventRun')}`;
  return evaluatedValue2;
}
function formatTimestamp(value: string): string {
  const timestamp = new Date(value);
  const getTimeResult: number = timestamp.getTime();
  if (Number.isNaN(getTimeResult)) {
    return value;
  }
  const dateTimeFormatInstance = new Intl.DateTimeFormat(locale.value, { dateStyle: 'short', timeStyle: 'medium' });
  const formatResult: string = dateTimeFormatInstance.format(timestamp);
  return formatResult;
}
function formatPayload(payload: string): string {
  if (!payload.trim()) {
    const tResult: string = t('console.snapshotEventPayloadEmpty');
    return tResult;
  }
  try {
    const parseResult = JSON.parse(payload);
    const stringifyResult: string = JSON.stringify(parseResult, null, 2);
    return stringifyResult;
  }
  catch {
    return payload;
  }
}
function selectEvent(event: FlowRunEventDto): void {
  selectedSequence.value = event.sequence;
}
function copyPayload(): void {
  if (!selectedEvent.value?.payloadJson || !navigator.clipboard) {
    return;
  }
  const resolveCatchResult = () => {
    const writeTextResult = navigator.clipboard.writeText(selectedEvent.value!.payloadJson);
    const thenResult = writeTextResult.then(() => {
      copyFeedback.value = t('console.snapshotEventCopied');
      if (copyFeedbackTimer !== undefined) {
        window.clearTimeout(copyFeedbackTimer);
      }
      copyFeedbackTimer = window.setTimeout(() => {
        copyFeedback.value = '';
      }, 2000);
    });
    const catchResult = thenResult.catch(() => {
      copyFeedback.value = '';
    });
    return catchResult;
  };
  void resolveCatchResult();
}
function readLiItems() {
  return (orderedEvents.value);
}
function readButtonClass(event: TemplateItem<ReturnType<typeof readLiItems>>) {
  const evaluatedValue3 = (['run-snapshot-output-list__item', `status-${eventTone(event)}`, { selected: selectedSequence.value === event.sequence }]);
  return evaluatedValue3;
}
function readButtonAriaPressed(event: TemplateItem<ReturnType<typeof readLiItems>>) {
  return (selectedSequence.value === event.sequence);
}
const readTemplateText = computed(() => {
  const evaluatedValue4: string = (selectedEvent.value!.nodeId || t('console.snapshotEventRun'));
  return evaluatedValue4;
});
</script>

<template>
  <section
    class="run-snapshot-output-viewer"
    :aria-label="t('console.snapshotRunOutput')"
  >
    <header class="run-snapshot-output-viewer__header">
      <div>
        <p>{{ t('console.snapshotRunOutput') }}</p>
        <strong>{{ orderedEvents.length }} {{ t('output.records') }}</strong>
      </div>
      <span class="run-snapshot-output-viewer__scope">{{ t('console.snapshotEventRun') }}</span>
    </header>
    <p
      v-if="error"
      class="run-snapshot-output-viewer__error"
      role="alert"
    >{{ error }}</p>
    <template v-else-if="orderedEvents.length">
      <ol
        class="run-snapshot-output-list"
        :aria-label="t('console.snapshotRunOutput')"
      >
        <li
          v-for="event in readLiItems()"
          :key="event.sequence"
        >
          <button
            type="button"
            :class="readButtonClass(event)"
            :aria-pressed="readButtonAriaPressed(event)"
            @click="selectEvent(event)"
          >
            <span class="run-snapshot-output-list__icon">
              <component
                :is="eventIcon(event)"
                :size="14"
               />
            </span>
            <span class="run-snapshot-output-list__copy">
              <strong>{{ eventTitle(event) }}</strong>
              <span>{{ eventContext(event) }}</span>
            </span>
            <span class="run-snapshot-output-list__sequence">#{{ event.sequence }}</span>
          </button>
        </li>
      </ol>
      <section
        v-if="selectedEvent"
        class="run-snapshot-output-detail"
        :aria-label="t('console.snapshotEventDetails')"
      >
        <header>
          <div>
            <p>{{ t('console.snapshotEventDetails') }}</p>
            <strong>{{ eventTitle(selectedEvent) }}</strong>
          </div>
          <button
            type="button"
            class="run-snapshot-output-detail__copy"
            :title="t('console.snapshotCopyEventPayload')"
            :aria-label="t('console.snapshotCopyEventPayload')"
            @click="copyPayload"
          >
            <Copy :size="14" />
          </button>
        </header>
        <dl class="run-snapshot-output-detail__meta">
          <div>
            <dt>{{ t('console.snapshotEventNode') }}</dt>
            <dd>{{ readTemplateText }}</dd>
          </div>
          <div>
            <dt>{{ t('console.snapshotEventTime') }}</dt>
            <dd>{{ formatTimestamp(selectedEvent.timestamp) }}</dd>
          </div>
        </dl>
        <div class="run-snapshot-output-detail__payload">
          <span>{{ t('console.snapshotEventPayload') }}</span>
          <pre>{{ formatPayload(selectedEvent.payloadJson) }}</pre>
        </div>
        <p
          class="run-snapshot-output-detail__feedback"
          aria-live="polite"
        >{{ copyFeedback }}</p>
      </section>
    </template>
    <p
      v-else
      class="run-snapshot-output-viewer__empty"
    >
      <Terminal :size="17" />
      {{ t('console.snapshotRunOutputEmpty') }}
    </p>
  </section>
</template>
