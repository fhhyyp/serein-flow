<script setup lang="ts">
import { computed } from 'vue'
import { Activity, Code2, Database, GitBranch, Zap } from 'lucide-vue-next'
import { Handle, Position, type NodeProps } from '@vue-flow/core'
import { t } from '../../i18n'
import { layoutConnectionSeats, type ConnectionSeatLayout } from '../../flow/connectionSeats'
import type { FlowNodeData, NodeKind } from '../../flow/types'

const props = defineProps<NodeProps<FlowNodeData>>()

const icons: Record<NodeKind, typeof Activity> = {
  trigger: Zap,
  flipflop: Zap,
  script: Code2,
  condition: GitBranch,
  expOp: GitBranch,
  expCondition: GitBranch,
  flowCall: Activity,
  globalData: Database,
  value: Database,
  expression: Code2,
  action: Database,
}

const icon = computed(() => icons[props.data.kind])
const title = computed(() => props.data.displayName?.trim() || t(props.data.titleKey))
const description = computed(() => props.data.description?.trim() || t(props.data.subtitleKey))
const seats = computed(() => layoutConnectionSeats(props.data))
const executionInputSeat = computed(() => seats.value.find((seat) => seat.kind === 'execution-input'))
const executionOutputSeat = computed(() => seats.value.find((seat) => seat.kind === 'execution-output'))
const parameterSeats = computed(() => seats.value.filter((seat) => seat.kind === 'parameter-input'))
const dataOutputSeat = computed(() => seats.value.find((seat) => seat.kind === 'data-output'))
const hasDataOutput = computed(() => seats.value.some((seat) => seat.kind === 'data-output'))
const runtimeSignature = computed(() => {
  const runtime = props.data.runtime
  const method = [runtime?.className, runtime?.methodName].filter(Boolean).join('.')
  return method || runtime?.returnType || ''
})

function seatClass(seat: ConnectionSeatLayout): string {
  return `seat-${seat.kind}`
}

function seatLabel(seat: ConnectionSeatLayout | undefined): string {
  return seat ? t(seat.labelKey) : ''
}

function isTargetSeat(seat: ConnectionSeatLayout | undefined): boolean {
  return seat?.handleType === 'target'
}
</script>

<template>
  <article class="workflow-node" :class="[`kind-${data.kind}`, { selected }]" :aria-label="title">
    <div class="workflow-node__header drag-handle">
      <Handle
        v-if="executionInputSeat"
        :id="executionInputSeat.id"
        :type="executionInputSeat.handleType"
        :position="Position.Left"
        :class="['flow-handle', seatClass(executionInputSeat)]"
        :connectable="isTargetSeat(executionInputSeat) ? 'single' : connectable"
        :aria-label="seatLabel(executionInputSeat)"
      />
      <Handle
        v-if="executionOutputSeat"
        :id="executionOutputSeat.id"
        :type="executionOutputSeat.handleType"
        :position="Position.Right"
        :class="['flow-handle', seatClass(executionOutputSeat)]"
        :connectable="isTargetSeat(executionOutputSeat) ? 'single' : connectable"
        :aria-label="seatLabel(executionOutputSeat)"
      />
      <span class="workflow-node__icon" aria-hidden="true"><component :is="icon" :size="15" /></span>
      <span class="workflow-node__kind">{{ t(`node.kind.${data.kind}`) }}</span>
      <span v-if="runtimeSignature" class="workflow-node__runtime-header" :title="runtimeSignature">{{ runtimeSignature }}</span>
      <span class="workflow-node__status" :class="`status-${data.status}`" :title="t(`status.${data.status}`)"></span>
    </div>

    <div class="workflow-node__content">
      <strong>{{ title }}</strong>
      <span>{{ description }}</span>
      <span v-if="runtimeSignature" class="workflow-node__runtime">{{ runtimeSignature }}</span>
    </div>

    <div v-if="data.parameters.length > 0" class="workflow-node__parameters">
      <div v-for="(parameter, index) in data.parameters" :key="parameter.id" class="workflow-node__parameter">
        <Handle
          v-if="parameterSeats[index]"
          :id="parameterSeats[index].id"
          :type="parameterSeats[index].handleType"
          :position="Position.Left"
          :class="['flow-handle', seatClass(parameterSeats[index])]"
          :connectable="isTargetSeat(parameterSeats[index]) ? 'single' : connectable"
          :aria-label="seatLabel(parameterSeats[index])"
        />
        <span class="workflow-node__seat-dot parameter-seat-dot" aria-hidden="true"></span>
        <span class="workflow-node__parameter-name" :title="parameter.description || parameter.name || t(parameter.nameKey)">{{ parameter.name || t(parameter.nameKey) }}</span>
        <span class="workflow-node__parameter-type">{{ parameter.type || parameter.valueKind }}</span>
        <span class="workflow-node__parameter-source">{{ t(`parameter.${parameter.source}`) }}</span>
      </div>
    </div>

    <div class="workflow-node__footer">
      <Handle
        v-if="dataOutputSeat"
        :id="dataOutputSeat.id"
        :type="dataOutputSeat.handleType"
        :position="Position.Right"
        :class="['flow-handle', seatClass(dataOutputSeat)]"
        :connectable="isTargetSeat(dataOutputSeat) ? 'single' : connectable"
        :aria-label="seatLabel(dataOutputSeat)"
      />
      <span class="workflow-node__rail-label flow-rail-label"><i class="workflow-node__seat-dot execution-seat-dot" aria-hidden="true"></i>{{ t('edge.flow') }}</span>
      <span v-if="hasDataOutput" class="workflow-node__rail-label data-rail-label"><i class="workflow-node__seat-dot data-seat-dot" aria-hidden="true"></i>{{ t('edge.value') }}</span>
      <span v-if="data.runtime?.returnType" class="workflow-node__return-type" :title="data.runtime.returnType">→ {{ data.runtime.returnType }}</span>
    </div>
  </article>
</template>
