<script setup lang="ts">
import { computed } from 'vue'
import { Bug, CirclePause, ListOrdered, PanelRightClose, Play, Settings2, Square, StepForward } from 'lucide-vue-next'
import { t } from '../../i18n'
import type { FlowDebugSessionDto } from '../../api/flowApi'
import type { DebugPauseBoundary } from '../../composables/useFlowDebugger'
import type { NodeExecutionState } from '../../flow/nodeExecutionState'
import NodeExecutionInspector from '../debug/NodeExecutionInspector.vue'

const props = withDefaults(defineProps<{
  embedded?: boolean
  session?: FlowDebugSessionDto
  boundary?: DebugPauseBoundary
  executions: readonly NodeExecutionState[]
  nodeNames?: Record<string, string>
  isControlling: boolean
  isStopping: boolean
}>(), {
  embedded: false,
})

const emit = defineEmits<{
  continue: []
  step: []
  stop: []
  inspect: []
  selectNode: [nodeId: string]
  close: []
}>()

const isPaused = computed(() => props.session?.status === 'paused' && !props.isStopping)
const invocationShortId = computed(() => props.session?.activeInvocationId?.slice(0, 8))
</script>

<template>
  <section v-if="props.session" class="flow-debug-panel" :class="[`flow-debug-panel--${props.isStopping ? 'running' : props.session.status}`, { 'flow-debug-panel--embedded': props.embedded }]" :aria-label="t('debug.panelTitle')">
    <header class="flow-debug-panel__header">
      <div>
        <span class="flow-debug-panel__eyebrow"><Bug :size="13" />{{ t('debug.panelEyebrow') }}</span>
        <strong>{{ t(`debug.status.${props.isStopping ? 'stopping' : props.session.status}`) }}</strong>
      </div>
      <div class="flow-debug-panel__header-actions">
        <span v-if="isPaused" class="flow-debug-panel__paused"><CirclePause :size="14" />{{ t('debug.paused') }}</span>
        <button class="icon-button compact" type="button" :title="t('debug.showInspector')" :aria-label="t('debug.showInspector')" @click="emit('inspect')"><Settings2 :size="15" /></button>
        <button v-if="!props.embedded" class="icon-button compact" type="button" :title="t('panel.collapseDebug')" :aria-label="t('panel.collapseDebug')" @click="emit('close')"><PanelRightClose :size="15" /></button>
      </div>
    </header>

    <dl class="flow-debug-panel__facts">
      <div><dt>{{ t('debug.currentNode') }}</dt><dd><code>{{ props.boundary?.nodeId ?? props.session.currentNodeId ?? '—' }}</code></dd></div>
      <div><dt>{{ t('debug.stepCount') }}</dt><dd>{{ props.boundary?.step ?? '—' }}</dd></div>
      <div><dt>{{ t('debug.frameDepth') }}</dt><dd>{{ props.boundary?.frameDepth ?? '—' }}</dd></div>
      <div v-if="invocationShortId"><dt>{{ t('debug.invocation') }}</dt><dd><code>{{ invocationShortId }}</code></dd></div>
      <div v-if="props.session.activeFlipflopNodeId"><dt>{{ t('debug.flipflop') }}</dt><dd><code>{{ props.session.activeFlipflopNodeId }}</code></dd></div>
      <div v-if="props.session.queuedTriggerCount > 0"><dt>{{ t('debug.queuedTriggers') }}</dt><dd><ListOrdered :size="13" />{{ props.session.queuedTriggerCount }}</dd></div>
    </dl>

    <NodeExecutionInspector :executions="props.executions" :node-names="props.nodeNames" compact @select-node="emit('selectNode', $event)" />

    <footer class="flow-debug-panel__controls">
      <button class="flow-debug-panel__control" type="button" :title="t('debug.continue')" :aria-label="t('debug.continue')" :disabled="!isPaused || props.isControlling || props.isStopping" @click="emit('continue')"><Play :size="16" fill="currentColor" /></button>
      <button class="flow-debug-panel__control" type="button" :title="t('debug.step')" :aria-label="t('debug.step')" :disabled="!isPaused || props.isControlling || props.isStopping" @click="emit('step')"><StepForward :size="16" /></button>
      <button class="flow-debug-panel__control flow-debug-panel__control--stop" type="button" :title="t('debug.stop')" :aria-label="t('debug.stop')" :disabled="props.isControlling || props.isStopping || ['completed', 'cancelled', 'failed'].includes(props.session.status)" @click="emit('stop')"><Square :size="15" fill="currentColor" /></button>
    </footer>
  </section>
</template>
