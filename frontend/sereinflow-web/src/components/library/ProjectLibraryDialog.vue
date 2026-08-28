<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Archive, ArrowUpRight, Link2, PackagePlus, RefreshCw, Unlink, X } from 'lucide-vue-next'
import {
  listEnvironmentLibraries,
  listProjectLibraryArtifactUsages,
  listProjectLibraries,
  referenceProjectLibrary,
  unreferenceProjectLibrary,
  type LibraryArtifactUsageDto,
  type LibraryDto,
  type ProjectLibraryReferenceDto,
} from '../../api/libraryApi'
import type { FlowDefinitionSummaryDto } from '../../api/flowApi'
import { t } from '../../i18n'
import LibraryUpgradeDialog from './LibraryUpgradeDialog.vue'

const props = defineProps<{
  projectId: string
  projectName: string
  flows: FlowDefinitionSummaryDto[]
}>()

const emit = defineEmits<{
  close: []
  changed: [libraries: LibraryDto[]]
}>()

const references = ref<ProjectLibraryReferenceDto[]>([])
const environmentLibraries = ref<LibraryDto[]>([])
const artifactUsages = ref<LibraryArtifactUsageDto[]>([])
const isLoading = ref(true)
const changingLibraryIds = ref<Set<string>>(new Set())
const error = ref('')
const upgradeSource = ref<LibraryDto>()

const referencedIds = computed(() => new Set(references.value.map((item) => item.libraryId)))
const availableLibraries = computed(() => environmentLibraries.value.filter((library) => library.lifecycle === 'available' && !referencedIds.value.has(library.id)))
const usageByArtifactId = computed(() => new Map(artifactUsages.value.map((usage) => [usage.libraryArtifactId, usage])))

function upgradeTargets(source: LibraryDto): LibraryDto[] {
  if (!source.familyId) return []
  return environmentLibraries.value.filter((candidate) =>
    candidate.lifecycle === 'available'
    && candidate.id !== source.id
    && candidate.familyId === source.familyId)
}

function shortHash(value: string): string {
  return value.length <= 10 ? value : value.slice(0, 10)
}

function markChanging(libraryId: string, changing: boolean): void {
  const next = new Set(changingLibraryIds.value)
  if (changing) next.add(libraryId)
  else next.delete(libraryId)
  changingLibraryIds.value = next
}

function publishReferences(nextReferences: ProjectLibraryReferenceDto[]): void {
  references.value = nextReferences
  emit('changed', nextReferences.map((item) => item.library))
}

async function refresh(): Promise<void> {
  isLoading.value = true
  error.value = ''
  try {
    const [nextReferences, nextEnvironmentLibraries, nextArtifactUsages] = await Promise.all([
      listProjectLibraries(props.projectId),
      listEnvironmentLibraries(),
      listProjectLibraryArtifactUsages(props.projectId),
    ])
    publishReferences(nextReferences)
    environmentLibraries.value = nextEnvironmentLibraries
    artifactUsages.value = nextArtifactUsages
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : t('library.loadFailed')
  } finally {
    isLoading.value = false
  }
}

async function reference(library: LibraryDto): Promise<void> {
  markChanging(library.id, true)
  error.value = ''
  try {
    publishReferences(await referenceProjectLibrary(props.projectId, library.id))
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : t('library.referenceFailed')
  } finally {
    markChanging(library.id, false)
  }
}

async function unreference(reference: ProjectLibraryReferenceDto): Promise<void> {
  markChanging(reference.libraryId, true)
  error.value = ''
  try {
    publishReferences(await unreferenceProjectLibrary(props.projectId, reference.libraryId))
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : t('library.unreferenceFailed')
  } finally {
    markChanging(reference.libraryId, false)
  }
}

async function handleUpgradeApplied(): Promise<void> {
  try {
    await refresh()
  } catch {
    // The upgrade itself has succeeded; a later manual refresh can recover
    // the reference list if this secondary directory refresh fails.
    // 升级已经成功；若后续目录刷新失败，可由用户稍后手动刷新恢复引用列表。
  }
}

onMounted(() => { void refresh() })
</script>

<template>
  <div class="project-library-dialog-backdrop" role="presentation" @click.self="emit('close')">
    <section class="project-library-dialog" role="dialog" aria-modal="true" :aria-label="t('library.projectLibraries')">
      <header>
        <div><p class="operations-console__eyebrow">{{ t('library.projectScope') }}</p><h2>{{ t('library.projectLibraries') }}</h2><span>{{ projectName }}</span></div>
        <div class="project-library-dialog__actions"><button class="icon-button" type="button" :title="t('runs.refresh')" :aria-label="t('runs.refresh')" :disabled="isLoading" @click="refresh"><RefreshCw :size="16" :class="{ 'is-spinning': isLoading }" /></button><button class="icon-button" type="button" :title="t('command.close')" :aria-label="t('command.close')" @click="emit('close')"><X :size="16" /></button></div>
      </header>
      <p v-if="error" class="project-library-dialog__error" role="alert">{{ error }}</p>
      <div v-if="isLoading" class="project-library-dialog__state"><RefreshCw class="is-spinning" :size="18" />{{ t('library.loading') }}</div>
      <div v-else class="project-library-dialog__content">
        <section>
          <div class="project-library-dialog__heading"><div><h3>{{ t('library.referencedLibraries') }}</h3><p>{{ t('library.referencedLibrariesHint') }}</p></div><span>{{ references.length }}</span></div>
          <div v-if="references.length" class="project-library-list">
            <article v-for="reference in references" :key="reference.libraryId" class="project-library-item"><div class="project-library-item__icon"><Archive v-if="reference.library.lifecycle === 'archived'" :size="16" /><Link2 v-else :size="16" /></div><div class="project-library-item__body"><strong>{{ reference.library.name }}</strong><span>{{ t('library.version', { version: reference.library.semanticVersion ?? reference.library.version }) }} · {{ shortHash(reference.library.sha256) }}</span><small v-if="reference.library.lifecycle === 'archived'">{{ t('library.archived') }}</small><small v-else-if="reference.library.familyName">{{ reference.library.familyName }}</small><small v-else-if="reference.library.familyId">{{ t('library.familyBound') }}</small><small>{{ t('library.currentFlowUsage', { count: usageByArtifactId.get(reference.libraryId)?.currentFlowCount ?? 0 }) }}</small></div><button v-if="upgradeTargets(reference.library).length" class="icon-button" type="button" :title="t('library.checkUpgrade')" :aria-label="t('library.checkUpgrade')" :disabled="changingLibraryIds.has(reference.libraryId)" @click="upgradeSource = reference.library"><ArrowUpRight :size="15" /></button><button class="icon-button icon-button--danger" type="button" :title="t('library.unreference')" :aria-label="t('library.unreference')" :disabled="changingLibraryIds.has(reference.libraryId)" @click="unreference(reference)"><Unlink :size="15" /></button></article>
          </div>
          <p v-else class="project-library-dialog__empty">{{ t('library.referenceEmpty') }}</p>
        </section>
        <section>
          <div class="project-library-dialog__heading"><div><h3>{{ t('library.environmentLibraries') }}</h3><p>{{ t('library.environmentLibrariesHint') }}</p></div><span>{{ availableLibraries.length }}</span></div>
          <div v-if="availableLibraries.length" class="project-library-list">
            <article v-for="library in availableLibraries" :key="library.id" class="project-library-item"><div class="project-library-item__icon"><PackagePlus :size="16" /></div><div class="project-library-item__body"><strong>{{ library.name }}</strong><span>{{ t('library.version', { version: library.version }) }} · {{ shortHash(library.sha256) }}</span><small>{{ t('library.nodeCount', { count: library.nodes.length }) }}</small></div><button class="command-button quiet" type="button" :disabled="changingLibraryIds.has(library.id)" @click="reference(library)"><Link2 :size="14" /><span>{{ t('library.reference') }}</span></button></article>
          </div>
          <p v-else class="project-library-dialog__empty">{{ t('library.availableEmpty') }}</p>
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
      @close="upgradeSource = undefined"
      @applied="handleUpgradeApplied"
    />
  </div>
</template>
