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
  script: Code2,
  condition: GitBranch,
  action: Database,
}

const icon = computed(() => icons[props.data.kind])
const title = computed(() => props.data.displayName?.trim() || t(props.data.titleKey))
const description = computed(() => props.data.description?.trim() || t(props.data.subtitleKey))
const seats = computed(() => layoutConnectionSeats(props.data))

function seatClass(seat: ConnectionSeatLayout): string {
  return `seat-${seat.kind}`
}

function seatLabel(seat: ConnectionSeatLayout): string {
  return t(seat.labelKey)
}

function isTargetSeat(seat: ConnectionSeatLayout): boolean {
  return seat.handleType === 'target'
}
</script>

<template>
  <article class="workflow-node" :class="[`kind-${data.kind}`, { selected }]" :aria-label="title">
    <Handle
      v-for="seat in seats"
      :id="seat.id"
      :key="seat.id"
      :type="seat.handleType"
      :position="seat.side === 'left' ? Position.Left : Position.Right"
      :style="{ top: `${seat.top}px` }"
      :class="['flow-handle', seatClass(seat)]"
      :connectable="isTargetSeat(seat) ? 'single' : connectable"
      :aria-label="seatLabel(seat)"
    />

    <div class="workflow-node__header drag-handle">
      <span class="workflow-node__icon" aria-hidden="true"><component :is="icon" :size="15" /></span>
      <span class="workflow-node__kind">{{ t(`node.kind.${data.kind}`) }}</span>
      <span class="workflow-node__status" :class="`status-${data.status}`" :title="t(`status.${data.status}`)"></span>
    </div>

    <div class="workflow-node__content">
      <strong>{{ title }}</strong>
      <span>{{ description }}</span>
    </div>

    <div v-if="data.parameters.length > 0" class="workflow-node__parameters">
      <div v-for="parameter in data.parameters" :key="parameter.id" class="workflow-node__parameter">
        <span class="workflow-node__seat-dot parameter-seat-dot" aria-hidden="true"></span>
        <span class="workflow-node__parameter-name">{{ t(parameter.nameKey) }}</span>
        <span class="workflow-node__parameter-source">{{ t(`parameter.${parameter.source}`) }}</span>
      </div>
    </div>

    <div class="workflow-node__footer">
      <span class="workflow-node__rail-label flow-rail-label"><i class="workflow-node__seat-dot execution-seat-dot" aria-hidden="true"></i>{{ t('edge.flow') }}</span>
      <span v-if="data.hasDataOutput" class="workflow-node__rail-label data-rail-label"><i class="workflow-node__seat-dot data-seat-dot" aria-hidden="true"></i>{{ t('edge.value') }}</span>
    </div>
  </article>
</template>
