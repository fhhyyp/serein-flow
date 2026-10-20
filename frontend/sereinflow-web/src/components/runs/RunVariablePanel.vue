<script setup lang="ts">
type TemplateItem<T> = T extends readonly (infer Item)[] ? Item : T extends Record<string, infer Value> ? Value : T extends number ? number : never;
import { computed, onBeforeUnmount, ref, watch } from 'vue';
import { Database, RefreshCw } from 'lucide-vue-next';
import { FlowApiError, getFlowRunVariables, type RunVariableDto, type RunVariableSnapshotDto } from '../../api/flowApi';
import { locale, t } from '../../i18n';
const props = withDefaults(defineProps<{
  runId: string;
  live?: boolean;
  embedded?: boolean;
}>(), { live: false, embedded: false });
const snapshot = ref<RunVariableSnapshotDto>();
const pageSize = 25;
const offset = ref(0);
const busy = ref(false);
const inactive = ref(false);
const error = ref('');
let timer: ReturnType<typeof setTimeout> | undefined;
let controller: AbortController | undefined;
let revision = 0;
const capturedTime = computed(() => {
  const resolveToLocaleTimeStringResult = () => {
    const dateInstance = new Date(snapshot.value!.capturedAt);
    const toLocaleTimeStringResult: string = dateInstance.toLocaleTimeString(locale.value);
    return toLocaleTimeStringResult;
  };
  const evaluatedValue: string = snapshot.value ? resolveToLocaleTimeStringResult() : '';
  return evaluatedValue;
});
function stop(): void {
  revision++;
  if (timer !== undefined) {
    clearTimeout(timer);
  }
  timer = undefined;
  controller?.abort();
  controller = undefined;
  busy.value = false;
}
async function refresh(): Promise<void> {
  if (busy.value || !props.runId) {
    return;
  }
  if (timer !== undefined) {
    clearTimeout(timer);
  }
  timer = undefined;
  const requestRevision = revision;
  controller = new AbortController();
  busy.value = true;
  let retryPage = false;
  try {
    const result = await getFlowRunVariables(props.runId, offset.value, pageSize, controller.signal);
    if (requestRevision !== revision) {
      return;
    }
    // Variables can be added during a run; reconcile pages when their count changes.
    if (result.variables.length === 0 && offset.value > 0) {
      offset.value = 0;
      retryPage = true;
      return;
    }
    snapshot.value = result;
    inactive.value = !!result.isFinal;
    error.value = '';
  }
  catch (cause) {
    if (requestRevision !== revision) {
      return;
    }
    if (cause instanceof FlowApiError && cause.status === 409) {
      inactive.value = true;
      error.value = '';
    }
    else {
      error.value = cause instanceof Error ? cause.message : t('variables.loadFailed');
    }
  }
  finally {
    if (requestRevision === revision) {
      busy.value = false;
      controller = undefined;
      if (retryPage || (props.live && !inactive.value)) {
        timer = setTimeout(() => {
          void refresh();
        }, retryPage ? 0 : 1000);
      }
    }
  }
}
function changePage(next: number): void {
  stop();
  offset.value = next;
  snapshot.value = undefined;
  void refresh();
}
watch(() => props.runId, () => {
  stop();
  snapshot.value = undefined;
  offset.value = 0;
  error.value = '';
  inactive.value = false;
  void refresh();
}, { immediate: true });
watch(() => props.live, () => {
  stop();
  inactive.value = false;
  void refresh();
});
onBeforeUnmount(stop);
function formatValue(variable: RunVariableDto): string {
  const evaluatedValue1: string = variable.valueJson ?? JSON.stringify(variable.value, null, 2) ?? 'null';
  // JSON.parse would round Int64 and decimal values; preserve the Worker's JSON text.
  return evaluatedValue1;
}
const readTemplateText = computed(() => {
  const evaluatedValue2: string = (t('variables.count', { count: snapshot.value!.totalCount }));
  return evaluatedValue2;
});
const readTemplateText2 = computed(() => {
  const evaluatedValue3: string = (snapshot.value?.isFinal ? t('variables.final') : inactive.value ? t('variables.inactive') : props.live ? t('variables.live') : t('variables.loading'));
  return evaluatedValue3;
});
const readTemplateText3 = computed(() => {
  const evaluatedValue4: string = (t('variables.updated', { time: capturedTime.value }));
  return evaluatedValue4;
});
const readPIf = computed(() => (!snapshot.value && busy.value));
const readPElseIf = computed(() => (snapshot.value && snapshot.value.totalCount === 0));
function readTrItems() {
  return (snapshot.value!.variables);
}
function readTemplateText4(variable: TemplateItem<ReturnType<typeof readTrItems>>) {
  return (variable.clrType ?? 'null');
}
function readTemplateText5(variable: TemplateItem<ReturnType<typeof readTrItems>>) {
  const evaluatedValue5 = (t(`variables.reason.${variable.unavailableReason ?? 'unsupported_type'}`));
  return evaluatedValue5;
}
const readFooterIf = computed(() => (snapshot.value && (snapshot.value.hasMore || offset.value > 0)));
const readButtonDisabled = computed(() => (busy.value || offset.value === 0));
function handleButtonClick() {
  const maxResult: number = Math.max(0, offset.value - pageSize);
  changePage(maxResult);
}
const readTemplateText6 = computed(() => (offset.value + 1));
const readTemplateText7 = computed(() => (offset.value + snapshot.value!.variables.length));
const readButtonDisabled2 = computed(() => (busy.value || !snapshot.value!.hasMore));
function handleButtonClick2() {
  changePage(offset.value + pageSize);
}
</script>

<template>
  <section
    class="run-variable-panel"
    :aria-label="t('variables.title')"
  >
    <header class="run-variable-panel__header">
      <div>
        <strong>
          <Database :size="15" />
          {{ t('variables.title') }}
        </strong>
        <span v-if="snapshot">{{ readTemplateText }}</span>
      </div>
      <button
        class="icon-button compact"
        type="button"
        :title="t('variables.refresh')"
        :aria-label="t('variables.refresh')"
        :disabled="busy"
        @click="refresh"
      >
        <RefreshCw :size="15" />
      </button>
    </header>
    <p
      class="run-variable-panel__status"
      role="status"
    >
      {{ readTemplateText2 }}
      <span v-if="capturedTime"> · {{ readTemplateText3 }}</span>
    </p>
    <p
      v-if="snapshot?.isTruncated"
      class="run-variable-panel__status"
    >{{ t('variables.truncated') }}</p>
    <p
      v-if="error"
      class="run-variable-panel__error"
      role="alert"
    >{{ error }}</p>
    <p
      v-if="readPIf"
      class="run-variable-panel__empty"
    >{{ t('variables.loading') }}</p>
    <p
      v-else-if="readPElseIf"
      class="run-variable-panel__empty"
    >{{ t('variables.empty') }}</p>
    <div
      v-else-if="snapshot?.variables.length"
      class="run-variable-panel__table"
    >
      <table>
        <thead>
          <tr>
            <th scope="col">{{ t('variables.name') }}</th>
            <th scope="col">{{ t('variables.type') }}</th>
            <th scope="col">{{ t('variables.value') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="variable in readTrItems()"
            :key="variable.name"
          >
            <th scope="row">
              <code>{{ variable.name }}</code>
            </th>
            <td>
              <code>{{ readTemplateText4(variable) }}</code>
            </td>
            <td>
              <pre v-if="variable.valueAvailable">{{ formatValue(variable) }}</pre>
              <span
                v-else
                class="run-variable-panel__unavailable"
              >{{ readTemplateText5(variable) }}</span>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <footer
      v-if="readFooterIf"
      class="run-variable-panel__paging"
    >
      <button
        type="button"
        :disabled="readButtonDisabled"
        @click="handleButtonClick()"
      >{{ t('variables.previous') }}</button>
      <span>{{ readTemplateText6 }}–{{ readTemplateText7 }} / {{ snapshot!.totalCount }}</span>
      <button
        type="button"
        :disabled="readButtonDisabled2"
        @click="handleButtonClick2()"
      >{{ t('variables.next') }}</button>
    </footer>
  </section>
</template>

<style scoped>
.run-variable-panel {
  min-width: 0;
  height: 100%;
  overflow: auto;
  padding: 14px;
  color: var(--ink);
  background: var(--surface);
}

.run-variable-panel__header, .run-variable-panel__header strong, .run-variable-panel__paging {
  display: flex;
  align-items: center;
  gap: 8px;
}

.run-variable-panel__header {
  justify-content: space-between;
}

.run-variable-panel__header > div {
  min-width: 0;
}

.run-variable-panel__header span, .run-variable-panel__status, .run-variable-panel__unavailable {
  color: var(--muted);
  font-size: 12px;
}

.run-variable-panel__header span {
  display: block;
  margin-top: 4px;
}

.run-variable-panel__status {
  line-height: 1.6;
  margin: 12px 0;
}

.run-variable-panel__empty {
  color: var(--muted);
  font-size: 13px;
  padding: 20px 0;
}

.run-variable-panel__error {
  color: var(--danger, #b91c1c);
  font-size: 13px;
  overflow-wrap: anywhere;
}

.run-variable-panel__table {
  width: 100%;
  overflow-x: auto;
}

table {
  width: 100%;
  border-collapse: collapse;
  table-layout: fixed;
  font-size: 12px;
}

th, td {
  padding: 10px 8px;
  border-bottom: 1px solid var(--line);
  vertical-align: top;
  text-align: left;
  overflow-wrap: anywhere;
}

thead th {
  color: var(--muted);
  font-weight: 500;
}

th:first-child {
  width: 32%;
}

th:nth-child(2) {
  width: 35%;
}

tbody th {
  font-weight: 500;
}

pre {
  margin: 0;
  max-height: 180px;
  overflow: auto;
  white-space: pre-wrap;
  overflow-wrap: anywhere;
  font-size: 12px;
}

code, pre {
  font-family: ui-monospace, Consolas, monospace;
}

.run-variable-panel__paging {
  justify-content: space-between;
  margin-top: 12px;
  font-size: 12px;
}

.run-variable-panel__paging button {
  border: 1px solid var(--line);
  padding: 6px 10px;
  background: var(--surface);
  color: var(--ink);
  cursor: pointer;
}

.run-variable-panel__paging button:hover:not(:disabled) {
  border-color: var(--accent);
  color: var(--accent);
}

.run-variable-panel__paging button:disabled {
  opacity: .5;
  cursor: default;
}

@media (max-width: 480px) {
  .run-variable-panel {
    padding: 10px;
  }
  th, td {
    padding: 8px 4px;
  }
}

</style>
