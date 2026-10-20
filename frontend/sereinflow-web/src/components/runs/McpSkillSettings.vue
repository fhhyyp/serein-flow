<script setup lang="ts">
type TemplateItem<T> = T extends readonly (infer Item)[] ? Item : T extends Record<string, infer Value> ? Value : T extends number ? number : never;
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { BookOpen, RefreshCw, RotateCcw, Save } from 'lucide-vue-next';
import { listMcpSkills, McpApiError, readMcpSkill, resetMcpSkill, saveMcpSkill, type McpSkillContentDto, type McpSkillResourceDto, } from '../../api/mcpApi';
import { t } from '../../i18n';
const props = defineProps<{
  secret: string;
}>();
const resources = ref<McpSkillResourceDto[]>([]);
const selected = ref<McpSkillContentDto>();
const draft = ref('');
const isLoading = ref(false);
const isSaving = ref(false);
const error = ref('');
const notice = ref('');
let requestVersion = 0;
const busy = computed(() => isLoading.value || isSaving.value);
const dirty = computed(() => {
  const booleanResult: boolean = Boolean(selected.value && draft.value !== selected.value.content);
  return booleanResult;
});
const contentBytes = computed(() => {
  const textEncoderInstance = new TextEncoder();
  const evaluatedValue: number = textEncoderInstance.encode(draft.value).length;
  return evaluatedValue;
});
const tooLarge = computed(() => {
  const booleanResult1: boolean = Boolean(selected.value && contentBytes.value > selected.value.maxBytes);
  return booleanResult1;
});
const canSave = computed(() => {
  const resolveBooleanResult = () => {
    const trimResult: string | undefined = draft.value.trim();
    const booleanResult2 = Boolean(trimResult);
    return booleanResult2;
  };
  const evaluatedValue1: boolean = dirty.value && resolveBooleanResult() && !tooLarge.value && !busy.value;
  return evaluatedValue1;
});
function confirmDiscard(): boolean {
  if (isSaving.value) {
    return false;
  }
  const resolveConfirmResult = () => {
    const tResult: string | undefined = t('console.mcpSkillDiscardConfirm');
    const confirmResult: boolean = window.confirm(tResult);
    return confirmResult;
  };
  const evaluatedValue2: boolean = !dirty.value || resolveConfirmResult();
  return evaluatedValue2;
}
function showContent(content: McpSkillContentDto): void {
  selected.value = content;
  draft.value = content.content;
  resources.value = resources.value.map(resource => resource.key === content.resource.key ? content.resource : resource);
}
function showError(exception: unknown): void {
  error.value = exception instanceof McpApiError && exception.status === 409
    ? t('console.mcpSkillConflict')
    : exception instanceof Error ? exception.message : t('console.mcpSkillOperationFailed');
}
async function refresh(): Promise<void> {
  if (busy.value || !confirmDiscard()) {
    return;
  }
  const version = ++requestVersion;
  isLoading.value = true;
  error.value = '';
  notice.value = '';
  try {
    const nextResources = await listMcpSkills(props.secret);
    if (version !== requestVersion) {
      return;
    }
    resources.value = nextResources;
    const key = nextResources.find(resource => resource.key === selected.value?.resource.key)?.key ?? nextResources[0]?.key;
    const content = key ? await readMcpSkill(props.secret, key) : undefined;
    if (version !== requestVersion) {
      return;
    }
    if (content) {
      showContent(content);
    }
    else {
      selected.value = undefined;
      draft.value = '';
    }
  }
  catch (exception) {
    if (version === requestVersion) {
      showError(exception);
    }
  }
  finally {
    if (version === requestVersion) {
      isLoading.value = false;
    }
  }
}
async function selectResource(key: string): Promise<void> {
  if (key === selected.value?.resource.key || busy.value || !confirmDiscard()) {
    return;
  }
  const version = ++requestVersion;
  isLoading.value = true;
  error.value = '';
  notice.value = '';
  try {
    const content = await readMcpSkill(props.secret, key);
    if (version === requestVersion) {
      showContent(content);
    }
  }
  catch (exception) {
    if (version === requestVersion) {
      showError(exception);
    }
  }
  finally {
    if (version === requestVersion) {
      isLoading.value = false;
    }
  }
}
async function save(): Promise<void> {
  const current = selected.value;
  if (!current || !canSave.value) {
    return;
  }
  isSaving.value = true;
  error.value = '';
  notice.value = '';
  try {
    const saveMcpSkillResult: McpSkillContentDto = await saveMcpSkill(props.secret, current.resource.key, draft.value, current.revision);
    showContent(saveMcpSkillResult);
    notice.value = t('console.mcpSkillSaved');
  }
  catch (exception) {
    showError(exception);
  }
  finally {
    isSaving.value = false;
  }
}
async function reset(): Promise<void> {
  const current = selected.value;
  const resolveConfirmResult1 = () => {
    const tResult1: string | undefined = t('console.mcpSkillResetConfirm', { name: current!.resource.name });
    const confirmResult1 = window.confirm(tResult1);
    return confirmResult1;
  };
  if (!current || busy.value || current.resource.source !== 'disk'
    || !resolveConfirmResult1()) {
    return;
  }
  isSaving.value = true;
  error.value = '';
  notice.value = '';
  try {
    const resetMcpSkillResult: McpSkillContentDto = await resetMcpSkill(props.secret, current.resource.key, current.revision);
    showContent(resetMcpSkillResult);
    notice.value = t('console.mcpSkillResetDone');
  }
  catch (exception) {
    showError(exception);
  }
  finally {
    isSaving.value = false;
  }
}
function beforeUnload(event: BeforeUnloadEvent): void {
  if (!dirty.value && !isSaving.value) {
    return;
  }
  event.preventDefault();
  event.returnValue = '';
}
watch(() => props.secret, () => {
  void refresh();
}, { immediate: true });
onMounted(() => {
  const addEventListenerResult = window.addEventListener('beforeunload', beforeUnload);
  return addEventListenerResult;
});
onBeforeUnmount(() => {
  requestVersion++;
  window.removeEventListener('beforeunload', beforeUnload);
});
defineExpose({ confirmDiscard });
const readRefreshCwClass = computed(() => ({ 'is-spinning': isLoading.value }));
function readButtonItems() {
  return (resources.value);
}
function readButtonClass(resource: TemplateItem<ReturnType<typeof readButtonItems>>) {
  return ({ active: selected.value?.resource.key === resource.key });
}
function readButtonAriaCurrent(resource: TemplateItem<ReturnType<typeof readButtonItems>>) {
  return (selected.value?.resource.key === resource.key ? 'true' : undefined);
}
function readSpanClass(resource: TemplateItem<ReturnType<typeof readButtonItems>>) {
  return (['mcp-skill-settings__source', `mcp-skill-settings__source--${resource.source}`]);
}
function readTemplateText(resource: TemplateItem<ReturnType<typeof readButtonItems>>) {
  const evaluatedValue3 = (t(`console.mcpSkillSource.${resource.source}`));
  return evaluatedValue3;
}
const readTextareaAriaInvalid = computed(() => {
  const evaluatedValue4: boolean = (tooLarge.value || !draft.value.trim());
  return evaluatedValue4;
});
const readSpanClass2 = computed(() => ({ 'mcp-skill-settings__invalid': tooLarge.value }));
const readTemplateText2 = computed(() => {
  const evaluatedValue5: {
    [x: string]: string | number;
  } | undefined = { size: contentBytes.value.toLocaleString(), max: selected.value!.maxBytes.toLocaleString() };
  const evaluatedValue6: string = (t('console.mcpSkillSize', evaluatedValue5));
  return evaluatedValue6;
});
const readTemplateText3 = computed(() => {
  const evaluatedValue7: string = (t(`console.mcpSkillSource.${selected.value!.resource.source}`));
  return evaluatedValue7;
});
const readButtonDisabled = computed(() => (busy.value || selected.value!.resource.source !== 'disk'));
const readButtonDisabled2 = computed(() => (busy.value || !dirty.value));
function handleButtonClick() {
  draft.value = selected.value!.content;
}
const readTemplateText4 = computed(() => {
  const evaluatedValue8: string = (t(isSaving.value ? 'console.mcpSkillSaving' : 'console.mcpSkillSave'));
  return evaluatedValue8;
});
const readPElseIf = computed(() => (!isLoading.value && !error.value));
</script>

<template>
  <section
    class="operations-console__section mcp-skill-settings"
    aria-labelledby="mcp-skill-settings-title"
    :aria-busy="busy"
  >
    <div class="operations-console__section-heading">
      <div>
        <h2 id="mcp-skill-settings-title">
          <BookOpen :size="17" />
          {{ t('console.mcpSkillTitle') }}
        </h2>
        <p>{{ t('console.mcpSkillHint') }}</p>
      </div>
      <button
        class="icon-button"
        type="button"
        :title="t('console.mcpSkillRefresh')"
        :aria-label="t('console.mcpSkillRefresh')"
        :disabled="busy"
        @click="refresh"
      >
        <RefreshCw
          :size="15"
          :class="readRefreshCwClass"
         />
      </button>
    </div>
    <p
      v-if="error"
      class="mcp-key-settings__message mcp-key-settings__message--error"
      role="alert"
    >{{ error }}</p>
    <p
      v-else-if="notice"
      class="mcp-key-settings__message"
      role="status"
    >{{ notice }}</p>
    <p
      v-if="isLoading"
      class="mcp-skill-settings__empty"
      role="status"
    >{{ t('console.mcpSkillLoading') }}</p>
    <div
      v-if="resources.length"
      class="mcp-skill-settings__workspace"
    >
      <nav
        class="mcp-skill-settings__list"
        :aria-label="t('console.mcpSkillTitle')"
      >
        <button
          v-for="resource in readButtonItems()"
          :key="resource.key"
          type="button"
          :class="readButtonClass(resource)"
          :aria-current="readButtonAriaCurrent(resource)"
          :disabled="busy"
          @click="selectResource(resource.key)"
        >
          <span class="mcp-skill-settings__item-heading">
            <strong>{{ resource.name }}</strong>
            <span :class="readSpanClass(resource)">{{ readTemplateText(resource) }}</span>
          </span>
          <small>{{ resource.description }}</small>
        </button>
      </nav>
      <form
        v-if="selected"
        class="mcp-skill-settings__editor"
        @submit.prevent="save"
      >
        <div class="mcp-skill-settings__editor-heading">
          <h3>{{ selected.resource.name }}</h3>
          <span
            v-if="dirty"
            class="mcp-skill-settings__dirty"
          >{{ t('console.mcpSkillUnsaved') }}</span>
        </div>
        <p class="mcp-skill-settings__description">{{ selected.resource.description }}</p>
        <code class="mcp-skill-settings__uri">{{ selected.resource.uri }}</code>
        <label for="mcp-skill-content">{{ t('console.mcpSkillContent') }}</label>
        <textarea
          id="mcp-skill-content"
          v-model="draft"
          :disabled="busy"
          :aria-invalid="readTextareaAriaInvalid"
          spellcheck="false"
          wrap="off"
         />
        <div class="mcp-skill-settings__editor-footer">
          <span :class="readSpanClass2">{{ readTemplateText2 }}</span>
          <span>{{ readTemplateText3 }}</span>
        </div>
        <p
          v-if="tooLarge"
          class="mcp-skill-settings__invalid"
          role="alert"
        >{{ t('console.mcpSkillTooLarge') }}</p>
        <div class="mcp-skill-settings__actions">
          <button
            class="command-button quiet"
            type="button"
            :disabled="readButtonDisabled"
            @click="reset"
          >
            <RotateCcw :size="15" />
            <span>{{ t('console.mcpSkillReset') }}</span>
          </button>
          <button
            class="command-button quiet"
            type="button"
            :disabled="readButtonDisabled2"
            @click="handleButtonClick()"
          >{{ t('console.mcpSkillDiscard') }}</button>
          <button
            class="command-button run"
            type="submit"
            :disabled="!canSave"
          >
            <Save :size="15" />
            <span>{{ readTemplateText4 }}</span>
          </button>
        </div>
      </form>
    </div>
    <p
      v-else-if="readPElseIf"
      class="mcp-skill-settings__empty"
    >{{ t('console.mcpSkillEmpty') }}</p>
  </section>
</template>
