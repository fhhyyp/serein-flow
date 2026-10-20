<script setup lang="ts">
type TemplateItem<T> = T extends readonly (infer Item)[] ? Item : T extends Record<string, infer Value> ? Value : T extends number ? number : never;
import { computed, ref, watch } from 'vue';
import { AlertCircle, CheckCircle2, CircleDashed, CirclePause, GitBranch, Layers3, ListTree } from 'lucide-vue-next';
import { t, locale } from '../../i18n';
import { sortNodeExecutionStates, type NodeExecutionState } from '../../flow/nodeExecutionState';
import StructuredValueTree from './StructuredValueTree.vue';
const props = withDefaults(defineProps<{
  executions: readonly NodeExecutionState[];
  nodeNames?: Record<string, string>;
  selectedNodeId?: string;
  selectedExecutionId?: string;
  compact?: boolean;
}>(), {
  nodeNames: () => ({}),
  compact: false,
});
const emit = defineEmits<{
  selectExecution: [
    execution: NodeExecutionState
  ];
}>();
const selectedExecutionId = ref('');
const timeline = computed(() => {
  const sortNodeExecutionStatesResult: NodeExecutionState[] = sortNodeExecutionStates(props.executions);
  return sortNodeExecutionStatesResult;
});
const selectedExecution = computed(() => {
  if (props.selectedExecutionId) {
    const selectedState = timeline.value.find((state) => (state.id === props.selectedExecutionId || state.executionId === props.selectedExecutionId));
    if (selectedState) {
      return selectedState;
    }
  }
  if (props.selectedNodeId) {
    const selectedState = timeline.value.find((state) => state.id === selectedExecutionId.value);
    if (selectedState?.nodeId === props.selectedNodeId) {
      return selectedState;
    }
    const reverseResult = [...timeline.value].reverse();
    const findResult: NodeExecutionState | undefined = reverseResult.find((state) => state.nodeId === props.selectedNodeId);
    return findResult;
  }
  const evaluatedValue: NodeExecutionState | undefined = timeline.value.find((state) => state.id === selectedExecutionId.value) ?? timeline.value[0];
  return evaluatedValue;
});
watch([timeline, () => props.selectedNodeId, () => props.selectedExecutionId], (inspectionState) => {
  const states = inspectionState[0];
  const selectedNodeId = inspectionState[1];
  const requestedExecutionId = inspectionState[2];
  if (requestedExecutionId) {
    const selectedState = states.find((state) => (state.id === requestedExecutionId || state.executionId === requestedExecutionId));
    if (selectedState) {
      selectedExecutionId.value = selectedState.id;
      return;
    }
  }
  const selectedState = states.find((state) => state.id === selectedExecutionId.value);
  if (selectedNodeId && selectedState?.nodeId === selectedNodeId) {
    return;
  }
  const resolveFindResult = () => {
    const reverseResult1 = [...states].reverse();
    const findResult1 = reverseResult1.find((state) => state.nodeId === selectedNodeId);
    return findResult1;
  };
  const selectedNodeExecution = selectedNodeId
    ? resolveFindResult() : undefined;
  if (selectedNodeExecution) {
    selectedExecutionId.value = selectedNodeExecution.id;
  }
  else if (selectedNodeId) {
    selectedExecutionId.value = '';
  }
  else if (!states.some((state) => state.id === selectedExecutionId.value)) {
    selectedExecutionId.value = states[0]?.id ?? '';
  }
}, { immediate: true });
function selectExecution(state: NodeExecutionState): void {
  selectedExecutionId.value = state.id;
  emit('selectExecution', state);
}
function nodeName(state: NodeExecutionState): string {
  return props.nodeNames[state.nodeId] || state.nodeId;
}
function statusIcon(status: NodeExecutionState['status']) {
  if (status === 'completed') {
    return CheckCircle2;
  }
  if (status === 'failed' || status === 'error') {
    return AlertCircle;
  }
  if (status === 'paused') {
    return CirclePause;
  }
  return CircleDashed;
}
function formatTimestamp(value: string | undefined): string {
  if (!value) {
    return '-';
  }
  const timestamp = new Date(value);
  const getTimeResult: number = timestamp.getTime();
  if (Number.isNaN(getTimeResult)) {
    return value;
  }
  const dateTimeFormatInstance = new Intl.DateTimeFormat(locale.value, { dateStyle: 'short', timeStyle: 'medium' });
  const formatResult: string = dateTimeFormatInstance.format(timestamp);
  return formatResult;
}
function statusLabel(status: NodeExecutionState['status']): string {
  const tResult: string = t(`execution.status.${status}`);
  return tResult;
}
function branchLabel(branch: string | undefined): string {
  if (!branch) {
    return '-';
  }
  const normalized = branch.toLowerCase();
  const evaluatedValue1: string = normalized === 'success' || normalized === 'failure' || normalized === 'error'
    ? t(`branch.${normalized}`)
    : branch;
  return evaluatedValue1;
}
const readSectionClass = computed(() => ({ 'node-execution-inspector--compact': props.compact }));
const readPIf = computed(() => (timeline.value.length === 0));
function readLiItems() {
  return (timeline.value);
}
function readButtonClass(state: TemplateItem<ReturnType<typeof readLiItems>>) {
  return ({ selected: state.id === selectedExecution.value?.id });
}
function readComponentClass(state: TemplateItem<ReturnType<typeof readLiItems>>) {
  return (`status-${state.status}`);
}
function readTemplateText(state: TemplateItem<ReturnType<typeof readLiItems>>) {
  return (state.step ?? '-');
}
const readSpanClass = computed(() => (['node-execution-inspector__status', `status-${selectedExecution.value!.status}`]));
const readTemplateText2 = computed(() => (selectedExecution.value!.step ?? '-'));
const readTemplateText3 = computed(() => (selectedExecution.value!.frameDepth ?? '-'));
const readPIf2 = computed(() => (selectedExecution.value!.errorCode || selectedExecution.value!.errorMessage));
</script>

<template>
  <section
    class="node-execution-inspector"
    :class="readSectionClass"
    :aria-label="t('execution.title')"
  >
    <section class="node-execution-inspector__timeline">
      <header>
        <span>
          <ListTree
            :size="15"
            aria-hidden="true"
           />
          {{ t('execution.title') }}
        </span>
        <code>{{ timeline.length }}</code>
      </header>
      <p
        v-if="readPIf"
        class="node-execution-inspector__empty"
      >{{ t('execution.empty') }}</p>
      <ol v-else>
        <li
          v-for="state in readLiItems()"
          :key="state.id"
        >
          <button
            type="button"
            :class="readButtonClass(state)"
            @click="selectExecution(state)"
          >
            <component
              :is="statusIcon(state.status)"
              :size="15"
              :class="readComponentClass(state)"
              aria-hidden="true"
             />
            <span>
              <strong>{{ nodeName(state) }}</strong>
              <code>{{ state.nodeId }}</code>
            </span>
            <small>#{{ readTemplateText(state) }}</small>
          </button>
        </li>
      </ol>
    </section>
    <section
      v-if="selectedExecution"
      class="node-execution-inspector__detail"
    >
      <header>
        <div>
          <span>{{ t('execution.node') }}</span>
          <strong>{{ nodeName(selectedExecution) }}</strong>
          <code>{{ selectedExecution.nodeId }}</code>
        </div>
        <span :class="readSpanClass">
          <component
            :is="statusIcon(selectedExecution.status)"
            :size="14"
            aria-hidden="true"
           />
          {{ statusLabel(selectedExecution.status) }}
        </span>
      </header>
      <dl class="node-execution-inspector__facts">
        <div>
          <dt>{{ t('execution.step') }}</dt>
          <dd>#{{ readTemplateText2 }}</dd>
        </div>
        <div>
          <dt>{{ t('execution.startedAt') }}</dt>
          <dd>{{ formatTimestamp(selectedExecution.startedAt) }}</dd>
        </div>
        <div>
          <dt>{{ t('execution.endedAt') }}</dt>
          <dd>{{ formatTimestamp(selectedExecution.endedAt) }}</dd>
        </div>
        <div>
          <dt>{{ t('execution.frameDepth') }}</dt>
          <dd>
            <Layers3
              :size="13"
              aria-hidden="true"
             />
            {{ readTemplateText3 }}
          </dd>
        </div>
        <div v-if="selectedExecution.branch">
          <dt>{{ t('execution.branch') }}</dt>
          <dd>
            <GitBranch
              :size="13"
              aria-hidden="true"
             />
            {{ branchLabel(selectedExecution.branch) }}
          </dd>
        </div>
        <div v-if="selectedExecution.triggerInvocationId">
          <dt>{{ t('execution.invocation') }}</dt>
          <dd>
            <code>{{ selectedExecution.triggerInvocationId }}</code>
          </dd>
        </div>
      </dl>
      <p
        v-if="readPIf2"
        class="node-execution-inspector__error"
      >
        <code v-if="selectedExecution.errorCode">{{ selectedExecution.errorCode }}</code>
        <span v-if="selectedExecution.errorMessage">{{ selectedExecution.errorMessage }}</span>
      </p>
      <section class="node-execution-inspector__value">
        <header>{{ t('execution.inputs') }}</header>
        <StructuredValueTree :value="selectedExecution.inputs" />
      </section>
      <section class="node-execution-inspector__value">
        <header>{{ t('execution.outputs') }}</header>
        <StructuredValueTree :value="selectedExecution.outputs" />
      </section>
    </section>
  </section>
</template>
