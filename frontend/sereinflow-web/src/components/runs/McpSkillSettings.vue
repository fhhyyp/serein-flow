<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { BookOpen, RefreshCw, RotateCcw, Save } from 'lucide-vue-next'
import {
  listMcpSkills, McpApiError, readMcpSkill, resetMcpSkill, saveMcpSkill,
  type McpSkillContentDto, type McpSkillResourceDto,
} from '../../api/mcpApi'
import { t } from '../../i18n'

const props = defineProps<{ secret: string }>()
const resources = ref<McpSkillResourceDto[]>([])
const selected = ref<McpSkillContentDto>()
const draft = ref('')
const isLoading = ref(false)
const isSaving = ref(false)
const error = ref('')
const notice = ref('')
let requestVersion = 0
const busy = computed(() => isLoading.value || isSaving.value)
const dirty = computed(() => Boolean(selected.value && draft.value !== selected.value.content))
const contentBytes = computed(() => new TextEncoder().encode(draft.value).length)
const tooLarge = computed(() => Boolean(selected.value && contentBytes.value > selected.value.maxBytes))
const canSave = computed(() => dirty.value && Boolean(draft.value.trim()) && !tooLarge.value && !busy.value)

function confirmDiscard(): boolean {
  if (isSaving.value) return false
  return !dirty.value || window.confirm(t('console.mcpSkillDiscardConfirm'))
}

function showContent(content: McpSkillContentDto): void {
  selected.value = content
  draft.value = content.content
  resources.value = resources.value.map(resource => resource.key === content.resource.key ? content.resource : resource)
}

function showError(exception: unknown): void {
  error.value = exception instanceof McpApiError && exception.status === 409
    ? t('console.mcpSkillConflict')
    : exception instanceof Error ? exception.message : t('console.mcpSkillOperationFailed')
}

async function refresh(): Promise<void> {
  if (busy.value || !confirmDiscard()) return
  const version = ++requestVersion
  isLoading.value = true
  error.value = ''
  notice.value = ''
  try {
    const nextResources = await listMcpSkills(props.secret)
    if (version !== requestVersion) return
    resources.value = nextResources
    const key = nextResources.find(resource => resource.key === selected.value?.resource.key)?.key ?? nextResources[0]?.key
    const content = key ? await readMcpSkill(props.secret, key) : undefined
    if (version !== requestVersion) return
    if (content) showContent(content)
    else { selected.value = undefined; draft.value = '' }
  } catch (exception) {
    if (version === requestVersion) showError(exception)
  } finally {
    if (version === requestVersion) isLoading.value = false
  }
}

async function selectResource(key: string): Promise<void> {
  if (key === selected.value?.resource.key || busy.value || !confirmDiscard()) return
  const version = ++requestVersion
  isLoading.value = true
  error.value = ''
  notice.value = ''
  try {
    const content = await readMcpSkill(props.secret, key)
    if (version === requestVersion) showContent(content)
  } catch (exception) {
    if (version === requestVersion) showError(exception)
  } finally {
    if (version === requestVersion) isLoading.value = false
  }
}

async function save(): Promise<void> {
  const current = selected.value
  if (!current || !canSave.value) return
  isSaving.value = true
  error.value = ''
  notice.value = ''
  try {
    showContent(await saveMcpSkill(props.secret, current.resource.key, draft.value, current.revision))
    notice.value = t('console.mcpSkillSaved')
  } catch (exception) {
    showError(exception)
  } finally {
    isSaving.value = false
  }
}

async function reset(): Promise<void> {
  const current = selected.value
  if (!current || busy.value || current.resource.source !== 'disk'
    || !window.confirm(t('console.mcpSkillResetConfirm', { name: current.resource.name }))) return
  isSaving.value = true
  error.value = ''
  notice.value = ''
  try {
    showContent(await resetMcpSkill(props.secret, current.resource.key, current.revision))
    notice.value = t('console.mcpSkillResetDone')
  } catch (exception) {
    showError(exception)
  } finally {
    isSaving.value = false
  }
}

function beforeUnload(event: BeforeUnloadEvent): void {
  if (!dirty.value && !isSaving.value) return
  event.preventDefault()
  event.returnValue = ''
}

watch(() => props.secret, () => { void refresh() }, { immediate: true })
onMounted(() => window.addEventListener('beforeunload', beforeUnload))
onBeforeUnmount(() => {
  requestVersion++
  window.removeEventListener('beforeunload', beforeUnload)
})
defineExpose({ confirmDiscard })
</script>

<template>
  <section class="operations-console__section mcp-skill-settings" aria-labelledby="mcp-skill-settings-title" :aria-busy="busy">
    <div class="operations-console__section-heading">
      <div>
        <h2 id="mcp-skill-settings-title"><BookOpen :size="17" />{{ t('console.mcpSkillTitle') }}</h2>
        <p>{{ t('console.mcpSkillHint') }}</p>
      </div>
      <button class="icon-button" type="button" :title="t('console.mcpSkillRefresh')" :aria-label="t('console.mcpSkillRefresh')" :disabled="busy" @click="refresh"><RefreshCw :size="15" :class="{ 'is-spinning': isLoading }" /></button>
    </div>
    <p v-if="error" class="mcp-key-settings__message mcp-key-settings__message--error" role="alert">{{ error }}</p>
    <p v-else-if="notice" class="mcp-key-settings__message" role="status">{{ notice }}</p>
    <p v-if="isLoading" class="mcp-skill-settings__empty" role="status">{{ t('console.mcpSkillLoading') }}</p>
    <div v-if="resources.length" class="mcp-skill-settings__workspace">
      <nav class="mcp-skill-settings__list" :aria-label="t('console.mcpSkillTitle')">
        <button v-for="resource in resources" :key="resource.key" type="button" :class="{ active: selected?.resource.key === resource.key }" :aria-current="selected?.resource.key === resource.key ? 'true' : undefined" :disabled="busy" @click="selectResource(resource.key)">
          <span class="mcp-skill-settings__item-heading"><strong>{{ resource.name }}</strong><span :class="['mcp-skill-settings__source', `mcp-skill-settings__source--${resource.source}`]">{{ t(`console.mcpSkillSource.${resource.source}`) }}</span></span>
          <small>{{ resource.description }}</small>
        </button>
      </nav>
      <form v-if="selected" class="mcp-skill-settings__editor" @submit.prevent="save">
        <div class="mcp-skill-settings__editor-heading"><h3>{{ selected.resource.name }}</h3><span v-if="dirty" class="mcp-skill-settings__dirty">{{ t('console.mcpSkillUnsaved') }}</span></div>
        <p class="mcp-skill-settings__description">{{ selected.resource.description }}</p>
        <code class="mcp-skill-settings__uri">{{ selected.resource.uri }}</code>
        <label for="mcp-skill-content">{{ t('console.mcpSkillContent') }}</label>
        <textarea id="mcp-skill-content" v-model="draft" :disabled="busy" :aria-invalid="tooLarge || !draft.trim()" spellcheck="false" wrap="off" />
        <div class="mcp-skill-settings__editor-footer"><span :class="{ 'mcp-skill-settings__invalid': tooLarge }">{{ t('console.mcpSkillSize', { size: contentBytes.toLocaleString(), max: selected.maxBytes.toLocaleString() }) }}</span><span>{{ t(`console.mcpSkillSource.${selected.resource.source}`) }}</span></div>
        <p v-if="tooLarge" class="mcp-skill-settings__invalid" role="alert">{{ t('console.mcpSkillTooLarge') }}</p>
        <div class="mcp-skill-settings__actions">
          <button class="command-button quiet" type="button" :disabled="busy || selected.resource.source !== 'disk'" @click="reset"><RotateCcw :size="15" /><span>{{ t('console.mcpSkillReset') }}</span></button>
          <button class="command-button quiet" type="button" :disabled="busy || !dirty" @click="draft = selected.content">{{ t('console.mcpSkillDiscard') }}</button>
          <button class="command-button run" type="submit" :disabled="!canSave"><Save :size="15" /><span>{{ t(isSaving ? 'console.mcpSkillSaving' : 'console.mcpSkillSave') }}</span></button>
        </div>
      </form>
    </div>
    <p v-else-if="!isLoading && !error" class="mcp-skill-settings__empty">{{ t('console.mcpSkillEmpty') }}</p>
  </section>
</template>
