<script setup lang="ts">
type TemplateItem<T> = T extends readonly (infer Item)[] ? Item : T extends Record<string, infer Value> ? Value : T extends number ? number : never;
import { computed, onMounted, ref } from 'vue';
import { FolderKanban, RefreshCw, X } from 'lucide-vue-next';
import { assignLibraryFamily, listLibraryFamilies, type LibraryDto, type LibraryFamilyDto, } from '../../api/libraryApi';
import { t } from '../../i18n';
const props = defineProps<{
  library: LibraryDto;
}>();
const emit = defineEmits<{
  close: [
  ];
  assigned: [
    family: LibraryFamilyDto
  ];
}>();
const families = ref<LibraryFamilyDto[]>([]);
const existingFamilyId = ref(props.library.familyId ?? '');
const mode = ref<'existing' | 'new'>(props.library.familyId ? 'existing' : 'new');
const name = ref('');
const description = ref('');
const isLoading = ref(true);
const isSaving = ref(false);
const error = ref('');
const canSave = computed(() => {
  const resolveBooleanResult = () => {
    const trimResult: string | undefined = name.value.trim();
    const booleanResult: boolean = Boolean(trimResult);
    return booleanResult;
  };
  const evaluatedValue: boolean = !isSaving.value
    && (mode.value === 'existing' ? Boolean(existingFamilyId.value) : resolveBooleanResult());
  return evaluatedValue;
});
async function refresh(): Promise<void> {
  isLoading.value = true;
  error.value = '';
  try {
    families.value = await listLibraryFamilies();
    if (!props.library.familyId && families.value.length && !existingFamilyId.value) {
      existingFamilyId.value = families.value[0]!.id;
    }
  }
  catch (reason) {
    error.value = reason instanceof Error ? reason.message : t('libraryFamily.loadFailed');
  }
  finally {
    isLoading.value = false;
  }
}
async function save(): Promise<void> {
  if (!canSave.value) {
    return;
  }
  isSaving.value = true;
  error.value = '';
  try {
    const argumentValue: string = props.library.id;
    const evaluatedValue1: import("../../api/libraryApi").AssignLibraryFamilyRequestDto = mode.value === 'existing'
      ? { familyId: existingFamilyId.value }
      : { name: name.value.trim(), description: description.value.trim() || null };
    const family = await assignLibraryFamily(argumentValue, evaluatedValue1);
    emit('assigned', family);
    emit('close');
  }
  catch (reason) {
    error.value = reason instanceof Error ? reason.message : t('libraryFamily.saveFailed');
  }
  finally {
    isSaving.value = false;
  }
}
onMounted(() => {
  void refresh();
});
const readTemplateText = computed(() => (props.library.semanticVersion ?? props.library.version));
const readLabelIf = computed(() => (mode.value === 'existing'));
function readOptionItems() {
  return (families.value);
}
function readTemplateText2(family: TemplateItem<ReturnType<typeof readOptionItems>>) {
  return (family.artifacts?.length ?? 0);
}
</script>

<template>
  <div
    class="library-family-dialog-backdrop"
    role="presentation"
    @click.self="emit('close')"
  >
    <section
      class="library-family-dialog"
      role="dialog"
      aria-modal="true"
      :aria-label="t('libraryFamily.title')"
    >
      <header>
        <div>
          <p class="operations-console__eyebrow">{{ t('libraryFamily.eyebrow') }}</p>
          <h2>{{ t('libraryFamily.title') }}</h2>
          <span>{{ library.name }} · {{ readTemplateText }}</span>
        </div>
        <button
          class="icon-button"
          type="button"
          :title="t('command.close')"
          :aria-label="t('command.close')"
          @click="emit('close')"
        >
          <X :size="16" />
        </button>
      </header>
      <p
        v-if="error"
        class="library-family-dialog__error"
        role="alert"
      >{{ error }}</p>
      <div
        v-if="isLoading"
        class="library-family-dialog__state"
      >
        <RefreshCw
          class="is-spinning"
          :size="18"
         />
        {{ t('library.loading') }}
      </div>
      <form
        v-else
        class="library-family-dialog__form"
        @submit.prevent="save"
      >
        <fieldset>
          <legend>{{ t('libraryFamily.assignment') }}</legend>
          <label>
            <input
              v-model="mode"
              type="radio"
              value="existing"
              :disabled="!families.length"
             />
            <span>{{ t('libraryFamily.existing') }}</span>
          </label>
          <label>
            <input
              v-model="mode"
              type="radio"
              value="new"
             />
            <span>{{ t('libraryFamily.create') }}</span>
          </label>
        </fieldset>
        <label
          v-if="readLabelIf"
          class="library-family-field"
        >
          <span>{{ t('libraryFamily.select') }}</span>
          <select
            v-model="existingFamilyId"
            :disabled="!families.length"
          >
            <option
              v-for="family in readOptionItems()"
              :key="family.id"
              :value="family.id"
            >{{ family.name }} · {{ readTemplateText2(family) }}</option>
          </select>
        </label>
        <template v-else>
          <label class="library-family-field">
            <span>{{ t('libraryFamily.name') }}</span>
            <input
              v-model="name"
              :placeholder="t('libraryFamily.namePlaceholder')"
              maxlength="120"
             />
          </label>
          <label class="library-family-field">
            <span>{{ t('libraryFamily.description') }}</span>
            <textarea
              v-model="description"
              rows="3"
              maxlength="400"
             />
          </label>
        </template>
        <p class="library-family-dialog__hint">
          <FolderKanban :size="15" />
          {{ t('libraryFamily.hint') }}
        </p>
        <footer>
          <button
            class="command-button quiet"
            type="button"
            :disabled="isSaving"
            @click="emit('close')"
          >{{ t('command.cancel') }}</button>
          <button
            class="command-button run"
            type="submit"
            :disabled="!canSave"
          >
            <RefreshCw
              v-if="isSaving"
              :size="15"
              class="is-spinning"
             />
            <span>{{ t('libraryFamily.save') }}</span>
          </button>
        </footer>
      </form>
    </section>
  </div>
</template>
