<script setup lang="ts">
type TemplateItem<T> = T extends readonly (infer Item)[] ? Item : T extends Record<string, infer Value> ? Value : T extends number ? number : never;
import { computed, ref } from 'vue';
import { AlertCircle, CheckCircle2, CircleDashed, Code2, Copy, Terminal } from 'lucide-vue-next';
import { t } from '../../i18n';
interface RunEvent {
  time: string;
  label: string;
  detail: string;
  success?: boolean;
  status?: 'running' | 'success' | 'failed' | 'error' | 'idle';
  sequence?: number;
  nodeId?: string;
  logLevel?: 'info' | 'error';
  message?: string;
}
const props = withDefaults(defineProps<{
  embedded?: boolean;
  activeOutput: 'events' | 'payload';
  runEvents: RunEvent[];
  runPayload: string;
  hasRunOutput: boolean;
}>(), {
  embedded: false,
});
const emit = defineEmits<{
  'update:activeOutput': [
    value: 'events' | 'payload'
  ];
}>();
const eventFilter = ref<'all' | 'success' | 'error'>('all');
const selectedEventKey = ref<string>();
const visibleEvents = computed(() => {
  const filterResult: RunEvent[] = props.runEvents.filter((event) => {
    if (eventFilter.value === 'all') {
      return true;
    }
    if (eventFilter.value === 'success') {
      return event.status === 'success' || event.success === true;
    }
    const evaluatedValue: boolean = event.status === 'error' || event.status === 'failed' || event.label.includes('error') || event.label.includes('failed');
    return evaluatedValue;
  });
  return filterResult;
});
const selectedEvent = computed(() => {
  const findResult: RunEvent | undefined = props.runEvents.find((event) => {
    const evaluatedValue1: boolean = eventKey(event) === selectedEventKey.value;
    return evaluatedValue1;
  });
  return findResult;
});
function eventKey(event: RunEvent): string {
  return `${event.sequence ?? event.time}-${event.label}-${event.nodeId ?? ''}`;
}
function eventStatus(event: RunEvent): 'running' | 'success' | 'failed' | 'error' | 'idle' {
  if (event.status) {
    return event.status;
  }
  if (event.success) {
    return 'success';
  }
  if (event.logLevel === 'error') {
    return 'error';
  }
  if (event.label.includes('error')) {
    return 'error';
  }
  if (event.label.includes('failed')) {
    return 'failed';
  }
  if (event.label.includes('started')) {
    return 'running';
  }
  return 'idle';
}
function eventTitle(event: RunEvent): string {
  return event.message ?? event.label;
}
function eventContext(event: RunEvent): string {
  if (!event.logLevel) {
    const evaluatedValue2: string = event.nodeId || t('output.runEvent');
    return evaluatedValue2;
  }
  const level = t(`output.log.${event.logLevel}`);
  const node = event.nodeId || t('output.runEvent');
  return `${level} · ${node}`;
}
function eventIcon(event: RunEvent) {
  const status = eventStatus(event);
  return status === 'success' ? CheckCircle2 : status === 'error' || status === 'failed' ? AlertCircle : status === 'running' ? CircleDashed : Terminal;
}
function selectEvent(event: RunEvent): void {
  selectedEventKey.value = eventKey(event);
}
function copyPayload(): void {
  if (!props.runPayload || typeof navigator === 'undefined' || !navigator.clipboard) {
    return;
  }
  void navigator.clipboard.writeText(props.runPayload);
}
function selectOutput(value: 'events' | 'payload'): void {
  emit('update:activeOutput', value);
}
const readSectionClass = computed(() => ({ 'output-panel--embedded': props.embedded }));
const readButtonAriaSelected = computed(() => (props.activeOutput === 'events'));
const readButtonClass = computed(() => ({ active: props.activeOutput === 'events' }));
const readButtonAriaSelected2 = computed(() => (props.activeOutput === 'payload'));
const readButtonClass2 = computed(() => ({ active: props.activeOutput === 'payload' }));
const readDivIf = computed(() => (props.activeOutput === 'events'));
function readButtonItems() {
  return (['all', 'success', 'error'] as const);
}
function readButtonClass3(filter: TemplateItem<ReturnType<typeof readButtonItems>>) {
  return ({ active: eventFilter.value === filter });
}
function handleButtonClick(filter: TemplateItem<ReturnType<typeof readButtonItems>>) {
  eventFilter.value = filter;
}
function readTemplateText(filter: TemplateItem<ReturnType<typeof readButtonItems>>) {
  const evaluatedValue3 = (t(`output.filter.${filter}`));
  return evaluatedValue3;
}
const readTemplateIf = computed(() => (props.activeOutput === 'events'));
const readDivIf2 = computed(() => (visibleEvents.value.length === 0));
const readTemplateText2 = computed(() => {
  const evaluatedValue4: string = (props.runEvents.length === 0 ? t('output.emptyEvents') : t('output.emptyFiltered'));
  return evaluatedValue4;
});
function readButtonItems2() {
  return (visibleEvents.value);
}
function readButtonClass4(event: TemplateItem<ReturnType<typeof readButtonItems2>>) {
  const evaluatedValue5 = ([`status-${eventStatus(event)}`, { selected: selectedEventKey.value === eventKey(event) }]);
  return evaluatedValue5;
}
const readTemplateText3 = computed(() => (selectedEvent.value!.sequence ?? '—'));
</script>

<template>
  <section
    class="output-panel"
    :class="readSectionClass"
    :aria-label="t('output.eventsLabel')"
  >
    <header
      v-if="!props.embedded"
      class="output-header"
    >
      <div class="output-heading">
        <span class="output-heading__mark">
          <Terminal :size="16" />
        </span>
        <div>
          <strong>{{ t('output.eventsLabel') }}</strong>
          <span>{{ props.runEvents.length }} {{ t('output.records') }}</span>
        </div>
      </div>
    </header>
    <div class="output-body">
      <div
        class="output-mode-tabs"
        role="tablist"
        :aria-label="t('output.eventsLabel')"
      >
        <button
          type="button"
          role="tab"
          :aria-selected="readButtonAriaSelected"
          :class="readButtonClass"
          @click="selectOutput('events')"
        >
          <Terminal :size="13" />
          {{ t('output.events') }}
        </button>
        <button
          type="button"
          role="tab"
          :aria-selected="readButtonAriaSelected2"
          :class="readButtonClass2"
          @click="selectOutput('payload')"
        >
          <Code2 :size="13" />
          {{ t('output.payload') }}
        </button>
      </div>
      <div
        v-if="readDivIf"
        class="output-toolbar"
      >
        <span class="output-toolbar__label">{{ t('output.filter') }}</span>
        <button
          v-for="filter in readButtonItems()"
          :key="filter"
          type="button"
          class="filter-chip"
          :class="readButtonClass3(filter)"
          @click="handleButtonClick(filter)"
        >{{ readTemplateText(filter) }}</button>
        <span class="output-toolbar__spacer"></span>
        <span class="output-toolbar__hint">{{ t('output.selectHint') }}</span>
      </div>
      <div class="output-content">
        <template v-if="readTemplateIf">
          <div
            v-if="readDivIf2"
            class="output-empty"
          >
            <Terminal :size="17" />
            <span>{{ readTemplateText2 }}</span>
          </div>
          <div
            v-else
            class="output-event-layout"
          >
            <div class="output-event-list">
              <button
                v-for="event in readButtonItems2()"
                :key="eventKey(event)"
                type="button"
                class="event-row"
                :class="readButtonClass4(event)"
                @click="selectEvent(event)"
              >
                <span class="event-time mono">{{ event.time }}</span>
                <span class="event-icon">
                  <component
                    :is="eventIcon(event)"
                    :size="14"
                   />
                </span>
                <span class="event-copy">
                  <strong>{{ eventTitle(event) }}</strong>
                  <span>{{ eventContext(event) }}</span>
                </span>
                <span
                  v-if="event.sequence"
                  class="event-sequence mono"
                >#{{ event.sequence }}</span>
              </button>
            </div>
            <aside
              v-if="selectedEvent"
              class="event-detail-card"
            >
              <div class="event-detail-card__header">
                <span>{{ t('output.eventDetail') }}</span>
                <span class="mono">#{{ readTemplateText3 }}</span>
              </div>
              <strong>{{ eventTitle(selectedEvent) }}</strong>
              <span class="event-detail-card__node">{{ eventContext(selectedEvent) }} · {{ selectedEvent.time }}</span>
              <pre>{{ selectedEvent.detail }}</pre>
            </aside>
          </div>
        </template>
        <template v-else>
          <div class="payload-toolbar">
            <span>{{ t('output.payloadHint') }}</span>
            <button
              type="button"
              class="output-icon-button"
              :title="t('output.copyPayload')"
              :aria-label="t('output.copyPayload')"
              :disabled="!props.runPayload"
              @click="copyPayload"
            >
              <Copy :size="14" />
            </button>
          </div>
          <div
            v-if="!props.runPayload"
            class="output-empty"
          >
            <Code2 :size="17" />
            <span>{{ t('output.emptyPayload') }}</span>
          </div>
          <pre
            v-else
            class="payload-preview"
          >{{ props.runPayload }}</pre>
        </template>
      </div>
    </div>
  </section>
</template>
