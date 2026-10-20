<script setup lang="ts">
import { computed } from 'vue';
import { Trash2, X } from 'lucide-vue-next';
import { t } from '../../i18n';
const templateProps = defineProps<{
  open: boolean;
  canvasName: string;
  nodes: number;
  edges: number;
}>();
const emit = defineEmits<{
  cancel: [
  ];
  confirm: [
  ];
}>();
const readTemplateText = computed(() => {
  const evaluatedValue: string = (t('canvas.deleteConfirmMessage', { canvas: templateProps.canvasName }));
  return evaluatedValue;
});
const readTemplateText2 = computed(() => {
  const evaluatedValue1: string = (t('canvas.deleteConfirmDetails', { nodes: templateProps.nodes, edges: templateProps.edges }));
  return evaluatedValue1;
});
</script>

<template>
  <div
    v-if="open"
    class="canvas-delete-confirm"
    @click.self="emit('cancel')"
  >
    <section
      class="canvas-delete-confirm__dialog"
      role="dialog"
      aria-modal="true"
      aria-labelledby="canvas-delete-confirm-title"
    >
      <div class="canvas-delete-confirm__header">
        <div>
          <span class="eyebrow">{{ t('canvas.deleteConfirmEyebrow') }}</span>
          <h2 id="canvas-delete-confirm-title">{{ t('canvas.deleteConfirmTitle') }}</h2>
        </div>
        <button
          class="icon-button compact"
          type="button"
          :title="t('command.cancel')"
          :aria-label="t('command.cancel')"
          @click="emit('cancel')"
        >
          <X :size="15" />
        </button>
      </div>
      <p class="canvas-delete-confirm__message">{{ readTemplateText }}</p>
      <p class="canvas-delete-confirm__details">{{ readTemplateText2 }}</p>
      <div class="canvas-delete-confirm__actions">
        <button
          class="command-button quiet"
          type="button"
          @click="emit('cancel')"
        >{{ t('command.cancel') }}</button>
        <button
          class="command-button danger"
          type="button"
          autofocus
          @click="emit('confirm')"
        >
          <Trash2 :size="15" />
          {{ t('canvas.deleteConfirmAction') }}
        </button>
      </div>
    </section>
  </div>
</template>
