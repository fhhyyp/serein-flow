<script setup lang="ts">
import { LayoutGrid, Settings2 } from 'lucide-vue-next'
import { t } from '../../i18n'

defineProps<{
  mobilePanel: 'nodes' | 'inspector' | null
}>()

const emit = defineEmits<{
  'update:mobilePanel': [value: 'nodes' | 'inspector' | null]
}>()

function toggle(panel: 'nodes' | 'inspector' | null): void {
  emit('update:mobilePanel', panel)
}
</script>

<template>
  <div class="mobile-tabs" role="tablist" :aria-label="t('mobile.workspacePanels')">
    <button type="button" :class="{ active: mobilePanel === 'nodes' }" @click="toggle(mobilePanel === 'nodes' ? null : 'nodes')">
      <LayoutGrid :size="15" />{{ t('mobile.nodes') }}
    </button>
    <button type="button" :class="{ active: mobilePanel === 'inspector' }" @click="toggle(mobilePanel === 'inspector' ? null : 'inspector')">
      <Settings2 :size="15" />{{ t('mobile.inspector') }}
    </button>
  </div>
</template>
