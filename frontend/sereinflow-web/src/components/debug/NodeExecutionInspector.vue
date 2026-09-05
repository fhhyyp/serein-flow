<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { AlertCircle, CheckCircle2, CircleDashed, CirclePause, GitBranch, Layers3, ListTree } from 'lucide-vue-next'
import { t, locale } from '../../i18n'
import { sortNodeExecutionStates, type NodeExecutionState } from '../../flow/nodeExecutionState'
import StructuredValueTree from './StructuredValueTree.vue'

const props = withDefaults(defineProps<{
  executions: readonly NodeExecutionState[]
  nodeNames?: Record<string, string>
  compact?: boolean
}>(), {
  nodeNames: () => ({}),
  compact: false,
})

const emit = defineEmits<{
  selectNode: [nodeId: string]
}>()

const selectedExecutionId = ref('')
const timeline = computed(() => sortNodeExecutionStates(props.executions))
const selectedExecution = computed(() => timeline.value.find((state) => state.id === selectedExecutionId.value) ?? timeline.value[0])

watch(timeline, (states) => {
  if (!states.some((state) => state.id === selectedExecutionId.value)) {
    selectedExecutionId.value = states[0]?.id ?? ''
  }
}, { immediate: true })

function selectExecution(state: NodeExecutionState): void {
  selectedExecutionId.value = state.id
  emit('selectNode', state.nodeId)
}

function nodeName(state: NodeExecutionState): string {
  return props.nodeNames[state.nodeId] || state.nodeId
}

function statusIcon(status: NodeExecutionState['status']) {
  if (status === 'completed') return CheckCircle2
  if (status === 'failed' || status === 'error') return AlertCircle
  if (status === 'paused') return CirclePause
  return CircleDashed
}

function formatTimestamp(value: string | undefined): string {
  if (!value) return '-'
  const timestamp = new Date(value)
  if (Number.isNaN(timestamp.getTime())) return value
  return new Intl.DateTimeFormat(locale.value, { dateStyle: 'short', timeStyle: 'medium' }).format(timestamp)
}

function statusLabel(status: NodeExecutionState['status']): string {
  return t(`execution.status.${status}`)
}

function branchLabel(branch: string | undefined): string {
  if (!branch) return '-'
  const normalized = branch.toLowerCase()
  return normalized === 'success' || normalized === 'failure' || normalized === 'error'
    ? t(`branch.${normalized}`)
    : branch
}
</script>

<template>
  <section class="node-execution-inspector" :class="{ 'node-execution-inspector--compact': props.compact }" :aria-label="t('execution.title')">
    <section class="node-execution-inspector__timeline">
      <header>
        <span><ListTree :size="15" aria-hidden="true" />{{ t('execution.title') }}</span>
        <code>{{ timeline.length }}</code>
      </header>
      <p v-if="timeline.length === 0" class="node-execution-inspector__empty">{{ t('execution.empty') }}</p>
      <ol v-else>
        <li v-for="state in timeline" :key="state.id">
          <button
            type="button"
            :class="{ selected: state.id === selectedExecution?.id }"
            @click="selectExecution(state)"
          >
            <component :is="statusIcon(state.status)" :size="15" :class="`status-${state.status}`" aria-hidden="true" />
            <span><strong>{{ nodeName(state) }}</strong><code>{{ state.nodeId }}</code></span>
            <small>#{{ state.step ?? '-' }}</small>
          </button>
        </li>
      </ol>
    </section>

    <section v-if="selectedExecution" class="node-execution-inspector__detail">
      <header>
        <div><span>{{ t('execution.node') }}</span><strong>{{ nodeName(selectedExecution) }}</strong><code>{{ selectedExecution.nodeId }}</code></div>
        <span :class="['node-execution-inspector__status', `status-${selectedExecution.status}`]"><component :is="statusIcon(selectedExecution.status)" :size="14" aria-hidden="true" />{{ statusLabel(selectedExecution.status) }}</span>
      </header>

      <dl class="node-execution-inspector__facts">
        <div><dt>{{ t('execution.step') }}</dt><dd>#{{ selectedExecution.step ?? '-' }}</dd></div>
        <div><dt>{{ t('execution.startedAt') }}</dt><dd>{{ formatTimestamp(selectedExecution.startedAt) }}</dd></div>
        <div><dt>{{ t('execution.endedAt') }}</dt><dd>{{ formatTimestamp(selectedExecution.endedAt) }}</dd></div>
        <div><dt>{{ t('execution.frameDepth') }}</dt><dd><Layers3 :size="13" aria-hidden="true" />{{ selectedExecution.frameDepth ?? '-' }}</dd></div>
        <div v-if="selectedExecution.branch"><dt>{{ t('execution.branch') }}</dt><dd><GitBranch :size="13" aria-hidden="true" />{{ branchLabel(selectedExecution.branch) }}</dd></div>
        <div v-if="selectedExecution.triggerInvocationId"><dt>{{ t('execution.invocation') }}</dt><dd><code>{{ selectedExecution.triggerInvocationId }}</code></dd></div>
      </dl>

      <p v-if="selectedExecution.errorCode || selectedExecution.errorMessage" class="node-execution-inspector__error"><code v-if="selectedExecution.errorCode">{{ selectedExecution.errorCode }}</code><span v-if="selectedExecution.errorMessage">{{ selectedExecution.errorMessage }}</span></p>

      <section class="node-execution-inspector__value"><header>{{ t('execution.inputs') }}</header><StructuredValueTree :value="selectedExecution.inputs" /></section>
      <section class="node-execution-inspector__value"><header>{{ t('execution.outputs') }}</header><StructuredValueTree :value="selectedExecution.outputs" /></section>
    </section>
  </section>
</template>
