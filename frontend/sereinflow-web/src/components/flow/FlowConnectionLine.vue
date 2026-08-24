<script setup lang="ts">
import {
  getBezierPath,
  getSimpleBezierPath,
  getSmoothStepPath,
  getStraightPath,
  type ConnectionLineProps,
} from '@vue-flow/core'
import { computed } from 'vue'
import { connectionLineStyleFor, semanticFromConnectionHandles, type ConnectionLineSettings } from '../../flow/connectionLine'

interface FlowConnectionLineProps extends ConnectionLineProps {
  lineTypes?: ConnectionLineSettings
}

const props = defineProps<FlowConnectionLineProps>()

const semantic = computed(() => semanticFromConnectionHandles(props.sourceHandle?.id, props.targetHandle?.id))
const style = computed(() => semantic.value ? connectionLineStyleFor(semantic.value, props.lineTypes) : undefined)

const path = computed(() => {
  const params = {
    sourceX: props.sourceX,
    sourceY: props.sourceY,
    sourcePosition: props.sourcePosition,
    targetX: props.targetX,
    targetY: props.targetY,
    targetPosition: props.targetPosition,
  }

  switch (style.value?.lineType) {
    case 'simple-bezier':
      return getSimpleBezierPath(params)[0]
    case 'straight':
      return getStraightPath(params)[0]
    case 'step':
      return getSmoothStepPath({ ...params, borderRadius: 0 })[0]
    case 'smoothstep':
      return getSmoothStepPath(params)[0]
    case 'default':
    default:
      return getBezierPath(params)[0]
  }
})

const previewClass = computed(() => [
  'serein-flow__connection-preview',
  semantic.value ? `connection-preview-${semantic.value}` : 'connection-preview-unknown',
  props.connectionStatus ? `connection-${props.connectionStatus}` : undefined,
])
</script>

<template>
  <path
    :d="path"
    :class="previewClass"
    :style="{
      stroke: style?.color ?? '#64748b',
      strokeDasharray: style?.previewDashArray ?? '5 5',
    }"
    fill="none"
    stroke-linecap="round"
    stroke-linejoin="round"
    :marker-start="markerStart"
    :marker-end="markerEnd"
  />
</template>
