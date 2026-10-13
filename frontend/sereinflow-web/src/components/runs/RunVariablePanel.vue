<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { Database, RefreshCw } from 'lucide-vue-next'
import { FlowApiError, getFlowRunVariables, type RunVariableDto, type RunVariableSnapshotDto } from '../../api/flowApi'
import { locale, t } from '../../i18n'

const props = withDefaults(defineProps<{ runId: string; live?: boolean; embedded?: boolean }>(), { live: false, embedded: false })
const snapshot = ref<RunVariableSnapshotDto>()
const pageSize = 25
const offset = ref(0)
const busy = ref(false)
const inactive = ref(false)
const error = ref('')
let timer: ReturnType<typeof setTimeout> | undefined
let controller: AbortController | undefined
let revision = 0
const capturedTime = computed(() => snapshot.value ? new Date(snapshot.value.capturedAt).toLocaleTimeString(locale.value) : '')

function stop(): void {
  revision++
  if (timer !== undefined) clearTimeout(timer)
  timer = undefined
  controller?.abort()
  controller = undefined
  busy.value = false
}

async function refresh(): Promise<void> {
  if (busy.value || !props.runId) return
  if (timer !== undefined) clearTimeout(timer)
  timer = undefined
  const requestRevision = revision
  controller = new AbortController()
  busy.value = true
  let retryPage = false
  try {
    const result = await getFlowRunVariables(props.runId, offset.value, pageSize, controller.signal)
    if (requestRevision !== revision) return
    // Variables can be added during a run; reconcile pages when their count changes.
    if (result.variables.length === 0 && offset.value > 0) {
      offset.value = 0
      retryPage = true
      return
    }
    snapshot.value = result
    inactive.value = !!result.isFinal
    error.value = ''
  } catch (cause) {
    if (requestRevision !== revision) return
    if (cause instanceof FlowApiError && cause.status === 409) {
      inactive.value = true
      error.value = ''
    } else {
      error.value = cause instanceof Error ? cause.message : t('variables.loadFailed')
    }
  } finally {
    if (requestRevision === revision) {
      busy.value = false
      controller = undefined
      if (retryPage || (props.live && !inactive.value)) timer = setTimeout(() => { void refresh() }, retryPage ? 0 : 1000)
    }
  }
}

function changePage(next: number): void {
  stop()
  offset.value = next
  snapshot.value = undefined
  void refresh()
}

watch(() => props.runId, () => {
  stop()
  snapshot.value = undefined
  offset.value = 0
  error.value = ''
  inactive.value = false
  void refresh()
}, { immediate: true })
watch(() => props.live, () => {
  stop()
  inactive.value = false
  void refresh()
})
onBeforeUnmount(stop)

function formatValue(variable: RunVariableDto): string {
  // JSON.parse would round Int64 and decimal values; preserve the Worker's JSON text.
  return variable.valueJson ?? JSON.stringify(variable.value, null, 2) ?? 'null'
}
</script>

<template>
  <section class="run-variable-panel" :aria-label="t('variables.title')">
    <header class="run-variable-panel__header">
      <div><strong><Database :size="15" />{{ t('variables.title') }}</strong><span v-if="snapshot">{{ t('variables.count', { count: snapshot.totalCount }) }}</span></div>
      <button class="icon-button compact" type="button" :title="t('variables.refresh')" :aria-label="t('variables.refresh')" :disabled="busy" @click="refresh"><RefreshCw :size="15" /></button>
    </header>
    <p class="run-variable-panel__status" role="status">{{ snapshot?.isFinal ? t('variables.final') : inactive ? t('variables.inactive') : props.live ? t('variables.live') : t('variables.loading') }}<span v-if="capturedTime"> · {{ t('variables.updated', { time: capturedTime }) }}</span></p>
    <p v-if="snapshot?.isTruncated" class="run-variable-panel__status">{{ t('variables.truncated') }}</p>
    <p v-if="error" class="run-variable-panel__error" role="alert">{{ error }}</p>
    <p v-if="!snapshot && busy" class="run-variable-panel__empty">{{ t('variables.loading') }}</p>
    <p v-else-if="snapshot && snapshot.totalCount === 0" class="run-variable-panel__empty">{{ t('variables.empty') }}</p>
    <div v-else-if="snapshot?.variables.length" class="run-variable-panel__table">
      <table>
        <thead><tr><th scope="col">{{ t('variables.name') }}</th><th scope="col">{{ t('variables.type') }}</th><th scope="col">{{ t('variables.value') }}</th></tr></thead>
        <tbody><tr v-for="variable in snapshot.variables" :key="variable.name">
          <th scope="row"><code>{{ variable.name }}</code></th>
          <td><code>{{ variable.clrType ?? 'null' }}</code></td>
          <td><pre v-if="variable.valueAvailable">{{ formatValue(variable) }}</pre><span v-else class="run-variable-panel__unavailable">{{ t(`variables.reason.${variable.unavailableReason ?? 'unsupported_type'}`) }}</span></td>
        </tr></tbody>
      </table>
    </div>
    <footer v-if="snapshot && (snapshot.hasMore || offset > 0)" class="run-variable-panel__paging">
      <button type="button" :disabled="busy || offset === 0" @click="changePage(Math.max(0, offset - pageSize))">{{ t('variables.previous') }}</button>
      <span>{{ offset + 1 }}–{{ offset + snapshot.variables.length }} / {{ snapshot.totalCount }}</span>
      <button type="button" :disabled="busy || !snapshot.hasMore" @click="changePage(offset + pageSize)">{{ t('variables.next') }}</button>
    </footer>
  </section>
</template>

<style scoped>
.run-variable-panel { min-width: 0; height: 100%; overflow: auto; padding: 14px; color: var(--ink); background: var(--surface); }
.run-variable-panel__header, .run-variable-panel__header strong, .run-variable-panel__paging { display: flex; align-items: center; gap: 8px; }
.run-variable-panel__header { justify-content: space-between; }
.run-variable-panel__header > div { min-width: 0; }
.run-variable-panel__header span, .run-variable-panel__status, .run-variable-panel__unavailable { color: var(--muted); font-size: 12px; }
.run-variable-panel__header span { display: block; margin-top: 4px; }
.run-variable-panel__status { line-height: 1.6; margin: 12px 0; }
.run-variable-panel__empty { color: var(--muted); font-size: 13px; padding: 20px 0; }
.run-variable-panel__error { color: var(--danger, #b91c1c); font-size: 13px; overflow-wrap: anywhere; }
.run-variable-panel__table { width: 100%; overflow-x: auto; }
table { width: 100%; border-collapse: collapse; table-layout: fixed; font-size: 12px; }
th, td { padding: 10px 8px; border-bottom: 1px solid var(--line); vertical-align: top; text-align: left; overflow-wrap: anywhere; }
thead th { color: var(--muted); font-weight: 500; }
th:first-child { width: 32%; } th:nth-child(2) { width: 35%; }
tbody th { font-weight: 500; }
pre { margin: 0; max-height: 180px; overflow: auto; white-space: pre-wrap; overflow-wrap: anywhere; font-size: 12px; }
code, pre { font-family: ui-monospace, Consolas, monospace; }
.run-variable-panel__paging { justify-content: space-between; margin-top: 12px; font-size: 12px; }
.run-variable-panel__paging button { border: 1px solid var(--line); padding: 6px 10px; background: var(--surface); color: var(--ink); cursor: pointer; }
.run-variable-panel__paging button:hover:not(:disabled) { border-color: var(--accent); color: var(--accent); }
.run-variable-panel__paging button:disabled { opacity: .5; cursor: default; }
@media (max-width: 480px) { .run-variable-panel { padding: 10px; } th, td { padding: 8px 4px; } }
</style>
