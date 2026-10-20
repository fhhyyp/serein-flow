<script setup lang="ts">
import { BaseEdge, type EdgeProps } from '@vue-flow/core';
import { computed } from 'vue';
import { smoothStepConnectionPath } from '../../flow/connectionPaths';
const props = defineProps<EdgeProps>();
const path = computed(() => {
  const smoothStepConnectionPathResult: string = smoothStepConnectionPath(props);
  return smoothStepConnectionPathResult;
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
