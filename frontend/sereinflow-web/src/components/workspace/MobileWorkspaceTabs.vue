<script setup lang="ts">
import { computed } from 'vue';
import { LayoutGrid, Settings2 } from 'lucide-vue-next';
import { t } from '../../i18n';
const templateProps = defineProps<{
  mobilePanel: 'nodes' | 'inspector' | null;
}>();
const emit = defineEmits<{
  'update:mobilePanel': [
    value: 'nodes' | 'inspector' | null
  ];
}>();
function toggle(panel: 'nodes' | 'inspector' | null): void {
  emit('update:mobilePanel', panel);
}
const readButtonClass = computed(() => ({ active: templateProps.mobilePanel === 'nodes' }));
function handleButtonClick() {
  toggle(templateProps.mobilePanel === 'nodes' ? null : 'nodes');
}
const readButtonClass2 = computed(() => ({ active: templateProps.mobilePanel === 'inspector' }));
function handleButtonClick2() {
  toggle(templateProps.mobilePanel === 'inspector' ? null : 'inspector');
}
</script>

<template>
  <div
    class="mobile-tabs"
    role="tablist"
    :aria-label="t('mobile.workspacePanels')"
  >
    <button
      type="button"
      :class="readButtonClass"
      @click="handleButtonClick()"
    >
      <LayoutGrid :size="15" />
      {{ t('mobile.nodes') }}
    </button>
    <button
      type="button"
      :class="readButtonClass2"
      @click="handleButtonClick2()"
    >
      <Settings2 :size="15" />
      {{ t('mobile.inspector') }}
    </button>
  </div>
</template>
