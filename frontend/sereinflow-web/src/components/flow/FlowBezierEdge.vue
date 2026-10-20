<script setup lang="ts">
import { BaseEdge, type EdgeProps } from '@vue-flow/core';
import { computed } from 'vue';
import { bezierConnectionPath } from '../../flow/connectionPaths';
const props = defineProps<EdgeProps>();
const path = computed(() => {
  const bezierConnectionPathResult: string = bezierConnectionPath(props);
  return bezierConnectionPathResult;
});
</script>

<template>
  <BaseEdge
    :id="props.id"
    :path="path"
    :style="props.style"
    :marker-start="props.markerStart"
    :marker-end="props.markerEnd"
    :interaction-width="props.interactionWidth"
   />
</template>
