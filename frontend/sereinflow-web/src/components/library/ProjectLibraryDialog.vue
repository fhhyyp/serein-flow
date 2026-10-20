<script setup lang="ts">
type TemplateItem<T> = T extends readonly (infer Item)[] ? Item : T extends Record<string, infer Value> ? Value : T extends number ? number : never;
import { computed, onMounted, ref, watch } from 'vue';
import { Archive, ArrowUpRight, Link2, PackagePlus, RefreshCw, Unlink, X } from 'lucide-vue-next';
import { listEnvironmentLibraries, listProjectLibraryArtifactUsages, listProjectLibraries, referenceProjectLibrary, unreferenceProjectLibrary, type LibraryArtifactUsageDto, type LibraryDto, type ProjectLibraryReferenceDto, } from '../../api/libraryApi';
import type { FlowDefinitionSummaryDto } from '../../api/flowApi';
import { t } from '../../i18n';
import LibraryUpgradeDialog from './LibraryUpgradeDialog.vue';
const props = defineProps<{
  projectId: string;
  projectName: string;
  flows: FlowDefinitionSummaryDto[];
  refreshRevision: number;
}>();
const emit = defineEmits<{
  close: [
  ];
  changed: [
    libraries: LibraryDto[]
  ];
}>();
const references = ref<ProjectLibraryReferenceDto[]>([]);
const environmentLibraries = ref<LibraryDto[]>([]);
const artifactUsages = ref<LibraryArtifactUsageDto[]>([]);
const isLoading = ref(true);
const setInstance1: Set<string> = new Set();
const changingLibraryIds = ref<Set<string>>(setInstance1);
const error = ref('');
const upgradeSource = ref<LibraryDto>();
const referencedIds = computed(() => {
  const mapResult: Iterable<string> | null | undefined = references.value.map((item) => item.libraryId);
  const setInstance: Set<string> = new Set(mapResult);
  return setInstance;
});
const availableLibraries = computed(() => {
  const filterResult: {
    id: string;
    name: string;
    version: string;
    fileName: string;
    sizeBytes: number;
    sha256: string;
    uploadedAt: string;
    nodes: {
      id: string;
      type: import("../../api/flowApi").ApiNodeType;
      displayName: string;
      description?: string | null | undefined;
      libraryId: string;
      flowLibraryName?: string | null | undefined;
      className: string;
      methodName: string;
      dllName: string;
      dllVersion: string;
      returnType: string;
      isAwaitable?: boolean | undefined;
      contractId?: string | null | undefined;
      overloadSignature?: string | null | undefined;
      identityConfidence?: "explicit" | "legacy" | undefined;
      parameters: {
        id: string;
        name: string;
        type: string;
        description?: string | null | undefined;
        required: boolean;
        isVariadic?: boolean | undefined;
        variadicGroupId?: string | null | undefined;
        elementType?: string | null | undefined;
        enumMetadata?: {
          typeName: string;
          isFlags: boolean;
          underlyingType: string;
          options: {
            name: string;
            numericValue: string;
          }[];
        } | null | undefined;
        defaultValue?: string | null | undefined;
        aliases?: string[] | null | undefined;
        identityConfidence?: "explicit" | "legacy" | undefined;
      }[];
    }[];
    lifecycle: "available" | "archived";
    familyId?: string | null | undefined;
    familyName?: string | null | undefined;
    semanticVersion?: string | null | undefined;
  }[] = environmentLibraries.value.filter((library) => {
    const evaluatedValue: boolean = library.lifecycle === 'available' && !referencedIds.value.has(library.id);
    return evaluatedValue;
  });
  return filterResult;
});
const usageByArtifactId = computed(() => {
  const mapResult1: Iterable<readonly [
    string,
    {
      libraryArtifactId: string;
      currentFlowCount: number;
      flowVersionCount: number;
      runSnapshotCount: number;
    }
  ]> | null | undefined = artifactUsages.value.map((usage) => [usage.libraryArtifactId, usage]);
  const mapInstance: Map<string, {
    libraryArtifactId: string;
    currentFlowCount: number;
    flowVersionCount: number;
    runSnapshotCount: number;
  }> = new Map(mapResult1);
  return mapInstance;
});
function upgradeTargets(source: LibraryDto): LibraryDto[] {
  if (!source.familyId) {
    return [];
  }
  const filterResult1: LibraryDto[] = environmentLibraries.value.filter((candidate) => candidate.lifecycle === 'available'
    && candidate.id !== source.id
    && candidate.familyId === source.familyId);
  return filterResult1;
}
function shortHash(value: string): string {
  const evaluatedValue1: string = value.length <= 10 ? value : value.slice(0, 10);
  return evaluatedValue1;
}
function markChanging(libraryId: string, changing: boolean): void {
  const next = new Set(changingLibraryIds.value);
  if (changing) {
    next.add(libraryId);
  }
  else {
    next.delete(libraryId);
  }
  changingLibraryIds.value = next;
}
function publishReferences(nextReferences: ProjectLibraryReferenceDto[]): void {
  references.value = nextReferences;
  const mapResult2: LibraryDto[] = nextReferences.map((item) => item.library);
  emit('changed', mapResult2);
}
async function refresh(): Promise<void> {
  isLoading.value = true;
  error.value = '';
  try {
    const evaluatedValue2: [
      Promise<ProjectLibraryReferenceDto[]>,
      Promise<LibraryDto[]>,
      Promise<LibraryArtifactUsageDto[]>
    ] = [
      listProjectLibraries(props.projectId),
      listEnvironmentLibraries(),
      listProjectLibraryArtifactUsages(props.projectId),
    ];
    const bindingSource = await Promise.all(evaluatedValue2);
    const nextReferences = bindingSource[0];
    const nextEnvironmentLibraries = bindingSource[1];
    const nextArtifactUsages = bindingSource[2];
    publishReferences(nextReferences);
    environmentLibraries.value = nextEnvironmentLibraries;
    artifactUsages.value = nextArtifactUsages;
  }
  catch (reason) {
    error.value = reason instanceof Error ? reason.message : t('library.loadFailed');
  }
  finally {
    isLoading.value = false;
  }
}
async function reference(library: LibraryDto): Promise<void> {
  markChanging(library.id, true);
  error.value = '';
  try {
    const referenceProjectLibraryResult: ProjectLibraryReferenceDto[] = await referenceProjectLibrary(props.projectId, library.id);
    publishReferences(referenceProjectLibraryResult);
  }
  catch (reason) {
    error.value = reason instanceof Error ? reason.message : t('library.referenceFailed');
  }
  finally {
    markChanging(library.id, false);
  }
}
async function unreference(reference: ProjectLibraryReferenceDto): Promise<void> {
  markChanging(reference.libraryId, true);
  error.value = '';
  try {
    const unreferenceProjectLibraryResult: ProjectLibraryReferenceDto[] = await unreferenceProjectLibrary(props.projectId, reference.libraryId);
    publishReferences(unreferenceProjectLibraryResult);
  }
  catch (reason) {
    error.value = reason instanceof Error ? reason.message : t('library.unreferenceFailed');
  }
  finally {
    markChanging(reference.libraryId, false);
  }
}
async function handleUpgradeApplied(): Promise<void> {
  try {
    await refresh();
  }
  catch {
  }
}
onMounted(() => {
  void refresh();
});
watch(() => props.refreshRevision, () => {
  void refresh();
});
const readRefreshCwClass = computed(() => ({ 'is-spinning': isLoading.value }));
function readArticleItems() {
  return (references.value);
}
function readArchiveIf(reference: TemplateItem<ReturnType<typeof readArticleItems>>) {
  return (reference.library.lifecycle === 'archived');
}
function readTemplateText(reference: TemplateItem<ReturnType<typeof readArticleItems>>) {
  const evaluatedValue3 = (t('library.version', { version: reference.library.semanticVersion ?? reference.library.version }));
  return evaluatedValue3;
}
function readSmallIf(reference: TemplateItem<ReturnType<typeof readArticleItems>>) {
  return (reference.library.lifecycle === 'archived');
}
function readTemplateText2(reference: TemplateItem<ReturnType<typeof readArticleItems>>) {
  const evaluatedValue4: {
    [x: string]: string | number;
  } | undefined = { count: usageByArtifactId.value.get(reference.libraryId)?.currentFlowCount ?? 0 };
  const evaluatedValue5 = (t('library.currentFlowUsage', evaluatedValue4));
  return evaluatedValue5;
}
function readButtonIf(reference: TemplateItem<ReturnType<typeof readArticleItems>>) {
  const evaluatedValue6 = (upgradeTargets(reference.library).length);
  return evaluatedValue6;
}
function handleButtonClick(reference: TemplateItem<ReturnType<typeof readArticleItems>>) {
  upgradeSource.value = reference.library;
}
function readArticleItems2() {
  return (availableLibraries.value);
}
function readTemplateText3(library: TemplateItem<ReturnType<typeof readArticleItems2>>) {
  const evaluatedValue7 = (t('library.version', { version: library.version }));
  return evaluatedValue7;
}
function readTemplateText4(library: TemplateItem<ReturnType<typeof readArticleItems2>>) {
  const evaluatedValue8 = (t('library.nodeCount', { count: library.nodes.length }));
  return evaluatedValue8;
}
function handleLibraryUpgradeDialogClose() {
  upgradeSource.value = undefined;
}
</script>

<template>
  <div
    class="project-library-dialog-backdrop"
    role="presentation"
    @click.self="emit('close')"
  >
    <section
      class="project-library-dialog"
      role="dialog"
      aria-modal="true"
      :aria-label="t('library.projectLibraries')"
    >
      <header>
        <div>
          <p class="operations-console__eyebrow">{{ t('library.projectScope') }}</p>
          <h2>{{ t('library.projectLibraries') }}</h2>
          <span>{{ projectName }}</span>
        </div>
        <div class="project-library-dialog__actions">
          <button
            class="icon-button"
            type="button"
            :title="t('runs.refresh')"
            :aria-label="t('runs.refresh')"
            :disabled="isLoading"
            @click="refresh"
          >
            <RefreshCw
              :size="16"
              :class="readRefreshCwClass"
             />
          </button>
          <button
            class="icon-button"
            type="button"
            :title="t('command.close')"
            :aria-label="t('command.close')"
            @click="emit('close')"
          >
            <X :size="16" />
          </button>
        </div>
      </header>
      <p
        v-if="error"
        class="project-library-dialog__error"
        role="alert"
      >{{ error }}</p>
      <div
        v-if="isLoading"
        class="project-library-dialog__state"
      >
        <RefreshCw
          class="is-spinning"
          :size="18"
         />
        {{ t('library.loading') }}
      </div>
      <div
        v-else
        class="project-library-dialog__content"
      >
        <section>
          <div class="project-library-dialog__heading">
            <div>
              <h3>{{ t('library.referencedLibraries') }}</h3>
              <p>{{ t('library.referencedLibrariesHint') }}</p>
            </div>
            <span>{{ references.length }}</span>
          </div>
          <div
            v-if="references.length"
            class="project-library-list"
          >
            <article
              v-for="reference in readArticleItems()"
              :key="reference.libraryId"
              class="project-library-item"
            >
              <div class="project-library-item__icon">
                <Archive
                  v-if="readArchiveIf(reference)"
                  :size="16"
                 />
                <Link2
                  v-else
                  :size="16"
                 />
              </div>
              <div class="project-library-item__body">
                <strong>{{ reference.library.name }}</strong>
                <span>{{ readTemplateText(reference) }} · {{ shortHash(reference.library.sha256) }}</span>
                <small v-if="readSmallIf(reference)">{{ t('library.archived') }}</small>
                <small v-else-if="reference.library.familyName">{{ reference.library.familyName }}</small>
                <small v-else-if="reference.library.familyId">{{ t('library.familyBound') }}</small>
                <small>{{ readTemplateText2(reference) }}</small>
              </div>
              <button
                v-if="readButtonIf(reference)"
                class="icon-button"
                type="button"
                :title="t('library.checkUpgrade')"
                :aria-label="t('library.checkUpgrade')"
                :disabled="changingLibraryIds.has(reference.libraryId)"
                @click="handleButtonClick(reference)"
              >
                <ArrowUpRight :size="15" />
              </button>
              <button
                class="icon-button icon-button--danger"
                type="button"
                :title="t('library.unreference')"
                :aria-label="t('library.unreference')"
                :disabled="changingLibraryIds.has(reference.libraryId)"
                @click="unreference(reference)"
              >
                <Unlink :size="15" />
              </button>
            </article>
          </div>
          <p
            v-else
            class="project-library-dialog__empty"
          >{{ t('library.referenceEmpty') }}</p>
        </section>
        <section>
          <div class="project-library-dialog__heading">
            <div>
              <h3>{{ t('library.environmentLibraries') }}</h3>
              <p>{{ t('library.environmentLibrariesHint') }}</p>
            </div>
            <span>{{ availableLibraries.length }}</span>
          </div>
          <div
            v-if="availableLibraries.length"
            class="project-library-list"
          >
            <article
              v-for="library in readArticleItems2()"
              :key="library.id"
              class="project-library-item"
            >
              <div class="project-library-item__icon">
                <PackagePlus :size="16" />
              </div>
              <div class="project-library-item__body">
                <strong>{{ library.name }}</strong>
                <span>{{ readTemplateText3(library) }} · {{ shortHash(library.sha256) }}</span>
                <small>{{ readTemplateText4(library) }}</small>
              </div>
              <button
                class="command-button quiet"
                type="button"
                :disabled="changingLibraryIds.has(library.id)"
                @click="reference(library)"
              >
                <Link2 :size="14" />
                <span>{{ t('library.reference') }}</span>
              </button>
            </article>
          </div>
          <p
            v-else
            class="project-library-dialog__empty"
          >{{ t('library.availableEmpty') }}</p>
        </section>
      </div>
    </section>
    <LibraryUpgradeDialog
      v-if="upgradeSource"
      :project-id="projectId"
      :project-name="projectName"
      :source="upgradeSource"
      :targets="upgradeTargets(upgradeSource)"
      :flows="flows"
      @close="handleLibraryUpgradeDialogClose()"
      @applied="handleUpgradeApplied"
     />
  </div>
</template>
