<script setup lang="ts">
import { getSimpleBezierPath, getSmoothStepPath, getStraightPath, type ConnectionLineProps, } from '@vue-flow/core';
import { computed } from 'vue';
import { connectionLineStyleFor, semanticFromConnectionHandles, type ConnectionLineSettings } from '../../flow/connectionLine';
import { executionBranchFromHandle } from '../../flow/connectionSeats';
import { CONNECTION_ARROW_GAP, bezierConnectionPath, smoothStepConnectionPath } from '../../flow/connectionPaths';
interface FlowConnectionLineProps extends ConnectionLineProps {
  lineTypes?: ConnectionLineSettings;
}
const props = defineProps<FlowConnectionLineProps>();
const semantic = computed(() => {
  const semanticFromConnectionHandlesResult: import("../../flow/types").ConnectionSemantic | undefined = semanticFromConnectionHandles(props.sourceHandle?.id, props.targetHandle?.id);
  return semanticFromConnectionHandlesResult;
});
const style = computed(() => {
  const evaluatedValue: import("../../flow/connectionLine").ConnectionLineStyle | undefined = semantic.value ? connectionLineStyleFor(semantic.value, props.lineTypes) : undefined;
  return evaluatedValue;
});
const branch = computed(() => {
  const executionBranchFromHandleResult: import("../../flow/types").ExecutionBranch | undefined = executionBranchFromHandle(props.sourceHandle?.id);
  return executionBranchFromHandleResult;
});
const previewColor = computed(() => branch.value === 'failure'
  ? '#b45309'
  : branch.value === 'error'
    ? '#dc2626'
    : branch.value === 'success'
      ? '#15803d'
      : style.value?.color ?? '#64748b');
const path = computed(() => {
  const params = {
    sourceX: props.sourceX,
    sourceY: props.sourceY,
    sourcePosition: props.sourcePosition,
    targetX: props.targetX,
    targetY: props.targetY,
    targetPosition: props.targetPosition,
  };
  switch (style.value?.lineType) {
    case 'simple-bezier':
      const evaluatedValue1: string = getSimpleBezierPath(params)[0];
      return evaluatedValue1;
    case 'straight':
      const evaluatedValue2: string = getStraightPath(params)[0];
      return evaluatedValue2;
    case 'step':
      const evaluatedValue3: string = getSmoothStepPath({ ...params, borderRadius: 0 })[0];
      return evaluatedValue3;
    case 'smoothstep':
      const smoothStepConnectionPathResult: string = smoothStepConnectionPath(params, props.targetHandle ? CONNECTION_ARROW_GAP : 0);
      return smoothStepConnectionPathResult;
    case 'default':
    default:
      const bezierConnectionPathResult: string = bezierConnectionPath(params, props.targetHandle ? CONNECTION_ARROW_GAP : 0);
      return bezierConnectionPathResult;
  }
});
const previewClass = computed(() => [
  'serein-flow__connection-preview',
  semantic.value ? `connection-preview-${semantic.value}` : 'connection-preview-unknown',
  props.connectionStatus ? `connection-${props.connectionStatus}` : undefined,
]);
const readPathStyle = computed(() => ({
  stroke: previewColor.value,
  strokeDasharray: style.value?.previewDashArray ?? '5 5',
}));
</script>

<template>
  <path
    :d="path"
    :class="previewClass"
    :style="readPathStyle"
    fill="none"
    stroke-linecap="round"
    stroke-linejoin="round"
    :marker-start="markerStart"
    :marker-end="markerEnd"
   />
</template>
