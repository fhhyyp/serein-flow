<script setup lang="ts">
import { Check, Code2, X } from 'lucide-vue-next'
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { t } from '../../i18n'
import SereinScriptCodeEditor from './SereinScriptCodeEditor.vue'

const props = defineProps<{
  open: boolean
  source: string
  nodeTitle: string
}>()

const emit = defineEmits<{
  close: []
  apply: [source: string]
}>()

const draftSource = ref('')
let previousBodyOverflow = ''

const lineCount = computed(() => Math.max(1, draftSource.value.split(/\r?\n/).length))
const characterCount = computed(() => draftSource.value.length)
const hasChanges = computed(() => draftSource.value !== props.source)

watch(() => props.open, (open) => {
  if (open) {
    draftSource.value = props.source
  }
}, { immediate: true })

onMounted(() => {
  previousBodyOverflow = document.body.style.overflow
  document.body.style.overflow = 'hidden'
})

onBeforeUnmount(() => {
  document.body.style.overflow = previousBodyOverflow
})

function close(): void {
  emit('close')
}

function apply(): void {
  emit('apply', draftSource.value)
}

function handleKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape') {
    event.preventDefault()
    close()
    return
  }

  if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') {
    event.preventDefault()
    if (hasChanges.value) {
      apply()
    }
  }
}
</script>

<template>
  <Teleport to="body">
    <div v-if="open" class="script-editor-backdrop">
      <section class="script-editor-dialog" role="dialog" aria-modal="true" aria-labelledby="script-editor-title" @keydown.capture="handleKeydown">
        <header>
          <div>
            <p class="eyebrow"><Code2 :size="13" />{{ t('scriptEditor.language') }}</p>
            <h2 id="script-editor-title">{{ t('scriptEditor.title') }}</h2>
            <span>{{ nodeTitle }}</span>
          </div>
          <button class="icon-button" type="button" :title="t('command.close')" :aria-label="t('command.close')" @click="close"><X :size="17" /></button>
        </header>
        <div class="script-editor-dialog__body">
          <SereinScriptCodeEditor v-model="draftSource" :editor-label="t('scriptEditor.editorLabel')" />
        </div>
        <footer>
          <div class="script-editor-dialog__metrics" aria-live="polite"><span>{{ t('scriptEditor.lineCount', { count: lineCount }) }}</span><span>{{ t('scriptEditor.characterCount', { count: characterCount }) }}</span></div>
          <div class="script-editor-dialog__actions">
            <button class="command-button quiet" type="button" @click="close"><X :size="14" /><span>{{ t('command.cancel') }}</span></button>
            <button class="command-button run" type="button" :disabled="!hasChanges" @click="apply"><Check :size="14" /><span>{{ t('scriptEditor.apply') }}</span></button>
          </div>
        </footer>
      </section>
    </div>
  </Teleport>
</template>
