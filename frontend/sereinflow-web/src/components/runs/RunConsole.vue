<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { Archive, CircleOff, Copy, Eye, FilePenLine, FolderKanban, LayoutDashboard, ListOrdered, PackageOpen, PackagePlus, Plus, RadioTower, RefreshCw, Save, Settings2, SlidersHorizontal, Square, Trash2, X } from 'lucide-vue-next'
import {
  FlowApiError,
  archiveProject as archiveProjectRequest,
  cancelFlowRun,
  interruptFlowRun,
  createFlowInterface,
  deleteFlowInterface,
  getFlowRunOverview,
  listFlowRunOutputs,
  listFlowRunEvents,
  getFlowRunSnapshot,
  getRunExecutionSettings,
  listFlowInterfaces,
  listFlowRuns,
  listProjects,
  saveRunExecutionSettings,
  subscribeFlowRunEvents,
  updateFlowInterface,
  type FlowDefinitionDto,
  type FlowInterfaceDto,
  type FlowInvocationMode,
  type FlowRunDto,
  type FlowRunOverviewDto,
  type FlowRunEventDto,
  type FlowRunOutputDto,
  type ProjectWorkspaceDto,
  type RunExecutionSettingsDto,
} from '../../api/flowApi'
import {
  archiveEnvironmentLibrary,
  listEnvironmentLibraries,
  listLibraryArtifactUsages,
  listLibraryFamilies,
  reindexEnvironmentLibrary,
  type LibraryArtifactUsageDto,
  type LibraryDto,
  type LibraryFamilyDto,
} from '../../api/libraryApi'
import { isArchivedProjectStatus } from '../../flow/projectStatus'
import { locale, t } from '../../i18n'
import LibraryFamilyDialog from '../library/LibraryFamilyDialog.vue'
import LibraryUploadDialog from '../library/LibraryUploadDialog.vue'
import McpKeySettings from './McpKeySettings.vue'
import RunSnapshotViewer from './RunSnapshotViewer.vue'
import RunWorkpiecePanel from './RunWorkpiecePanel.vue'

type ConsoleView = 'overview' | 'projects' | 'queue' | 'settings' | 'interfaces' | 'libraries' | 'archives'

const props = defineProps<{ projectWorkspaces: ProjectWorkspaceDto[] }>()
const emit = defineEmits<{
  'open-flow': [workspace: ProjectWorkspaceDto, flowId?: string]
  'start-new-project': []
  'project-directory-changed': [workspaces: ProjectWorkspaceDto[]]
}>()

const activeView = ref<ConsoleView>('overview')
const overview = ref<FlowRunOverviewDto>()
const workspaces = ref<ProjectWorkspaceDto[]>([...props.projectWorkspaces])
const runs = ref<FlowRunDto[]>([])
const interfaces = ref<FlowInterfaceDto[]>([])
const isLoading = ref(true)
const isSettingsSaving = ref(false)
const isInterfaceSaving = ref(false)
const loadErrorKey = ref('')
const noticeKey = ref('')
const cancellingRunIds = ref<Set<string>>(new Set())
const interruptingRunIds = ref<Set<string>>(new Set())
const snapshot = ref<FlowDefinitionDto>()
const snapshotOutputs = ref<FlowRunOutputDto[]>([])
const snapshotOutputsError = ref('')
const snapshotEvents = ref<FlowRunEventDto[]>([])
const snapshotEventsError = ref('')
const snapshotRun = ref<FlowRunDto>()
const isSnapshotLoading = ref(false)
const snapshotError = ref('')
const workpieceRun = ref<FlowRunDto>()
const environmentLibraries = ref<LibraryDto[]>([])
const libraryFamilies = ref<LibraryFamilyDto[]>([])
const libraryArtifactUsages = ref<LibraryArtifactUsageDto[]>([])
const libraryUploadOpen = ref(false)
const libraryFamilyAssignment = ref<LibraryDto>()
const isLibraryArchiving = ref<Set<string>>(new Set())
const isLibraryReindexing = ref<Set<string>>(new Set())
const isProjectArchiving = ref<Set<string>>(new Set())
const editingInterfaceId = ref<string>()
const subscriptions = new Map<string, () => void>()
let refreshTimer: number | undefined
let deferredRefreshTimer: number | undefined
let lastDirectoryRefreshAt = 0

const settingsForm = reactive<RunExecutionSettingsDto>({
  queueCapacity: 100,
  maxConcurrentRuns: 4,
  maxConcurrentListenerRuns: 1,
  maxConcurrentRunsPerProject: 2,
  queueWaitTimeoutSeconds: 60,
  shutdownGracePeriodSeconds: 10,
  synchronousInvocationTimeoutSeconds: 30,
  maxLibraryUploadBytes: 100 * 1024 * 1024,
})
const interfaceForm = reactive({
  name: '',
  projectId: '',
  flowId: '',
  invocationMode: 'asynchronous' as FlowInvocationMode,
  isEnabled: true,
})

const maxLibraryUploadMegabytes = computed({
  get: () => Math.round(settingsForm.maxLibraryUploadBytes / (1024 * 1024)),
  set: (value: number) => {
    settingsForm.maxLibraryUploadBytes = Math.round(value) * 1024 * 1024
  },
})

const selectedInterfaceProject = computed(() => activeWorkspaces.value.find((item) => item.project.id === interfaceForm.projectId))
const availableInterfaceFlows = computed(() => (selectedInterfaceProject.value?.flows ?? [])
  .filter((flow) => Boolean(flow.productionVersion)))
const availableInterfaceWorkspaces = computed(() => activeWorkspaces.value
  .filter((workspace) => workspace.flows.some((flow) => Boolean(flow.productionVersion))))
const latestRuns = computed(() => runs.value.slice(0, 6))
const activeWorkspaces = computed(() => workspaces.value.filter((workspace) => !isArchivedProjectStatus(workspace.project.status)))
const archivedWorkspaces = computed(() => workspaces.value.filter((workspace) => isArchivedProjectStatus(workspace.project.status)))
const activeEnvironmentLibraries = computed(() => environmentLibraries.value.filter((library) => library.lifecycle === 'available'))
const archivedEnvironmentLibraries = computed(() => environmentLibraries.value.filter((library) => library.lifecycle === 'archived'))
const libraryUsageByArtifactId = computed(() => new Map(libraryArtifactUsages.value.map((usage) => [usage.libraryArtifactId, usage])))
const activeLibraryFamilyGroups = computed(() => {
  const familyById = new Map(libraryFamilies.value.map((family) => [family.id, family]))
  const groups = new Map<string, {
    id: string
    name: string
    latestArtifactId?: string | null
    artifacts: LibraryDto[]
    isUnassigned: boolean
  }>()
  for (const library of activeEnvironmentLibraries.value) {
    const family = library.familyId ? familyById.get(library.familyId) : undefined
    const id = family?.id ?? library.familyId ?? 'unassigned'
    const name = family?.name ?? library.familyName ?? t('libraryFamily.unassigned')
    const current = groups.get(id) ?? {
      id,
      name,
      latestArtifactId: family?.latestArtifactId,
      artifacts: [],
      isUnassigned: !family,
    }
    current.artifacts.push(library)
    groups.set(id, current)
  }
  return [...groups.values()]
    .map((group) => {
      const artifacts = [...group.artifacts].sort((left, right) => {
        const leftRecommended = left.id === group.latestArtifactId ? 1 : 0
        const rightRecommended = right.id === group.latestArtifactId ? 1 : 0
        if (leftRecommended !== rightRecommended) return rightRecommended - leftRecommended
        return right.uploadedAt.localeCompare(left.uploadedAt)
      })
      return {
        ...group,
        artifacts,
        lastUploadedAt: artifacts.reduce((latest, artifact) => artifact.uploadedAt > latest ? artifact.uploadedAt : latest, ''),
      }
    })
    .sort((left, right) => Number(left.isUnassigned) - Number(right.isUnassigned) || left.name.localeCompare(right.name, locale.value))
})
const activeViewTitleKey = computed(() => ({
  overview: 'console.quickPreview',
  projects: 'console.projectListTitle',
  queue: 'console.queueTitle',
  settings: 'console.environmentSettingsTitle',
  interfaces: 'console.environmentInterfacesTitle',
  libraries: 'console.environmentLibrariesTitle',
  archives: 'console.archivesTitle',
} as const)[activeView.value])
const selectedWorkpieceRun = computed(() => workpieceRun.value
  ? runs.value.find((run) => run.id === workpieceRun.value?.id) ?? workpieceRun.value
  : undefined)

watch(() => props.projectWorkspaces, (nextWorkspaces) => {
  const activeIds = new Set(nextWorkspaces.map((workspace) => workspace.project.id))
  const retainedArchives = workspaces.value.filter((workspace) =>
    isArchivedProjectStatus(workspace.project.status) && !activeIds.has(workspace.project.id))
  workspaces.value = [...nextWorkspaces, ...retainedArchives]
  ensureInterfaceFormTarget()
})

function scheduleRefresh(delay = 250): void {
  if (deferredRefreshTimer !== undefined) return
  deferredRefreshTimer = window.setTimeout(() => {
    deferredRefreshTimer = undefined
    void refreshRunData(false)
  }, delay)
}

function syncActiveSubscriptions(activeRuns: FlowRunDto[]): void {
  const activeIds = new Set(activeRuns.map((run) => run.id))
  for (const [runId, unsubscribe] of subscriptions) {
    if (activeIds.has(runId)) continue
    unsubscribe()
    subscriptions.delete(runId)
  }
  for (const run of activeRuns) {
    if (subscriptions.has(run.id)) continue
    subscriptions.set(run.id, subscribeFlowRunEvents(run.id, () => scheduleRefresh(), () => scheduleRefresh(1_000)))
  }
}

async function refreshRunData(showLoading = true): Promise<void> {
  if (showLoading) isLoading.value = true
  try {
    const [nextOverview, nextRuns] = await Promise.all([getFlowRunOverview(), listFlowRuns({ take: 100 })])
    overview.value = nextOverview
    runs.value = nextRuns
    syncActiveSubscriptions(nextOverview.activeRuns)
    loadErrorKey.value = ''
  } catch {
    loadErrorKey.value = 'runs.loadFailed'
  } finally {
    isLoading.value = false
  }
}

async function refreshDirectoryData(): Promise<void> {
  const [nextWorkspaces, nextInterfaces, nextSettings, nextLibraries, nextFamilies, nextUsages] = await Promise.all([
    listProjects(true),
    listFlowInterfaces(),
    getRunExecutionSettings(),
    listEnvironmentLibraries(),
    listLibraryFamilies(),
    listLibraryArtifactUsages(),
  ])
  workspaces.value = nextWorkspaces
  interfaces.value = nextInterfaces
  Object.assign(settingsForm, {
    ...nextSettings,
    maxLibraryUploadBytes: nextSettings.maxLibraryUploadBytes ?? 100 * 1024 * 1024,
  })
  environmentLibraries.value = nextLibraries
  libraryFamilies.value = nextFamilies
  libraryArtifactUsages.value = nextUsages
  ensureInterfaceFormTarget()
  lastDirectoryRefreshAt = Date.now()
}

async function refreshAll(showLoading = true): Promise<void> {
  if (showLoading) isLoading.value = true
  try {
    await Promise.all([refreshRunData(false), refreshDirectoryData()])
  } catch {
    loadErrorKey.value = 'runs.loadFailed'
  } finally {
    isLoading.value = false
  }
}

function ensureInterfaceFormTarget(): void {
  if (!activeWorkspaces.value.some((workspace) => workspace.project.id === interfaceForm.projectId)) {
    interfaceForm.projectId = availableInterfaceWorkspaces.value[0]?.project.id ?? ''
  }
  if (!availableInterfaceFlows.value.some((flow) => flow.id === interfaceForm.flowId)) {
    interfaceForm.flowId = availableInterfaceFlows.value[0]?.id ?? ''
  }
}

function selectInterfaceProject(): void {
  interfaceForm.flowId = availableInterfaceFlows.value[0]?.id ?? ''
}

function projectNameFor(run: FlowRunDto): string {
  return workspaces.value.find((workspace) => workspace.project.id === run.projectId)?.project.name ?? t('runs.unknownProject')
}

function shortId(id: string): string { return id.length <= 8 ? id : id.slice(0, 8) }
function flowNameFor(run: FlowRunDto): string { return shortId(run.flowId) }
function formatDate(value?: string): string {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat(locale.value, { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}
function queueStatusKey(run: FlowRunDto): string {
  if (run.status === 'pending') return 'console.status.queued'
  if (run.status === 'running') return 'console.status.running'
  if (run.status === 'interrupted') return 'console.status.interrupted'
  return run.status === 'succeeded' ? 'console.status.completed' : 'console.status.failed'
}
function queueStatusClass(run: FlowRunDto): string {
  if (run.status === 'pending') return 'pending'
  if (run.status === 'running') return 'running'
  if (run.status === 'interrupted') return 'interrupted'
  return run.status === 'succeeded' ? 'succeeded' : 'failed'
}
function runKindKey(run: FlowRunDto): string {
  if (run.executionKind === 'debug') return 'runs.kind.debug'
  return run.isListenerRun ? 'runs.kind.listener' : 'runs.kind.standard'
}
function isCancellable(run: FlowRunDto): boolean { return run.status === 'pending' || run.status === 'running' }
function isInterruptible(run: FlowRunDto): boolean { return run.status === 'running' }
function isWorkpieceLive(run: FlowRunDto): boolean { return run.status === 'pending' || run.status === 'running' }

async function cancelRun(run: FlowRunDto): Promise<void> {
  if (!isCancellable(run) || cancellingRunIds.value.has(run.id)) return
  cancellingRunIds.value = new Set(cancellingRunIds.value).add(run.id)
  try {
    await cancelFlowRun(run.id)
    await refreshRunData(false)
  } catch {
    loadErrorKey.value = 'runs.cancelFailed'
  } finally {
    const next = new Set(cancellingRunIds.value)
    next.delete(run.id)
    cancellingRunIds.value = next
  }
}

async function interruptRun(run: FlowRunDto): Promise<void> {
  if (!isInterruptible(run) || interruptingRunIds.value.has(run.id)) return
  if (!window.confirm(t('runs.interruptConfirm'))) return
  interruptingRunIds.value = new Set(interruptingRunIds.value).add(run.id)
  try {
    await interruptFlowRun(run.id)
    await refreshRunData(false)
  } catch {
    loadErrorKey.value = 'runs.interruptFailed'
  } finally {
    const next = new Set(interruptingRunIds.value)
    next.delete(run.id)
    interruptingRunIds.value = next
  }
}

async function openSnapshot(run: FlowRunDto): Promise<void> {
  snapshotRun.value = run
  snapshot.value = undefined
  snapshotOutputs.value = []
  snapshotOutputsError.value = ''
  snapshotEvents.value = []
  snapshotEventsError.value = ''
  snapshotError.value = ''
  isSnapshotLoading.value = true
  try {
    const definition = await getFlowRunSnapshot(run.id)
    snapshot.value = definition
    const [outputsResult, eventsResult] = await Promise.allSettled([
      listFlowRunOutputs(run.id),
      listFlowRunEvents(run.id),
    ])
    if (outputsResult.status === 'fulfilled') snapshotOutputs.value = outputsResult.value
    else snapshotOutputsError.value = t('console.snapshotOutputsError')
    if (eventsResult.status === 'fulfilled') snapshotEvents.value = eventsResult.value
    else snapshotEventsError.value = t('console.snapshotEventsError')
  } catch {
    snapshotError.value = t('console.snapshotEmpty')
  } finally {
    isSnapshotLoading.value = false
  }
}
function closeSnapshot(): void { snapshotRun.value = undefined; snapshot.value = undefined; snapshotOutputs.value = []; snapshotOutputsError.value = ''; snapshotEvents.value = []; snapshotEventsError.value = ''; snapshotError.value = '' }
function openWorkpieces(run: FlowRunDto): void { workpieceRun.value = run }
function closeWorkpieces(): void { workpieceRun.value = undefined }
function openFlow(workspace: ProjectWorkspaceDto, flowId?: string): void {
  if (isArchivedProjectStatus(workspace.project.status)) return
  emit('open-flow', workspace, flowId)
}

async function saveSettings(): Promise<void> {
  isSettingsSaving.value = true
  noticeKey.value = ''
  try {
    Object.assign(settingsForm, await saveRunExecutionSettings({ ...settingsForm }))
    noticeKey.value = 'console.settingsSaved'
    await refreshRunData(false)
  } catch {
    loadErrorKey.value = 'console.settingsSaveFailed'
  } finally {
    isSettingsSaving.value = false
  }
}

function resetInterfaceForm(): void {
  editingInterfaceId.value = undefined
  interfaceForm.name = ''
  interfaceForm.invocationMode = 'asynchronous'
  interfaceForm.isEnabled = true
  interfaceForm.projectId = activeWorkspaces.value.find((workspace) => workspace.flows.length > 0)?.project.id ?? ''
  interfaceForm.flowId = availableInterfaceFlows.value[0]?.id ?? ''
}
function editInterface(flowInterface: FlowInterfaceDto): void {
  editingInterfaceId.value = flowInterface.id
  interfaceForm.name = flowInterface.name
  interfaceForm.projectId = flowInterface.projectId
  interfaceForm.flowId = flowInterface.flowId
  interfaceForm.invocationMode = flowInterface.invocationMode
  interfaceForm.isEnabled = flowInterface.isEnabled
}
async function saveInterface(): Promise<void> {
  if (!interfaceForm.projectId || !interfaceForm.flowId) { loadErrorKey.value = 'console.noProjectFlow'; return }
  isInterfaceSaving.value = true
  noticeKey.value = ''
  try {
    if (editingInterfaceId.value) {
      await updateFlowInterface(editingInterfaceId.value, { name: interfaceForm.name, invocationMode: interfaceForm.invocationMode, isEnabled: interfaceForm.isEnabled })
      noticeKey.value = 'console.interfaceUpdated'
    } else {
      await createFlowInterface({ ...interfaceForm })
      noticeKey.value = 'console.interfaceCreated'
    }
    interfaces.value = await listFlowInterfaces()
    resetInterfaceForm()
  } catch {
    loadErrorKey.value = 'console.interfaceActionFailed'
  } finally {
    isInterfaceSaving.value = false
  }
}
async function removeInterface(flowInterface: FlowInterfaceDto): Promise<void> {
  if (!window.confirm(t('console.interfaceDeleteConfirm', { name: flowInterface.name }))) return
  try {
    await deleteFlowInterface(flowInterface.id)
    interfaces.value = await listFlowInterfaces()
    if (editingInterfaceId.value === flowInterface.id) resetInterfaceForm()
    noticeKey.value = 'console.interfaceDeleted'
  } catch {
    loadErrorKey.value = 'console.interfaceActionFailed'
  }
}
function interfaceUrl(flowInterface: FlowInterfaceDto): string {
  return `${window.location.origin.replace(/\/$/, '')}/api/public/flows/${flowInterface.id}/invoke`
}
async function copyInterfaceUrl(flowInterface: FlowInterfaceDto): Promise<void> {
  try { await navigator.clipboard.writeText(interfaceUrl(flowInterface)); noticeKey.value = 'console.copySuccess' }
  catch { loadErrorKey.value = 'console.interfaceActionFailed' }
}

async function archiveLibrary(library: LibraryDto): Promise<void> {
  if (library.lifecycle === 'archived' || isLibraryArchiving.value.has(library.id)) return
  if (!window.confirm(t('console.archiveLibraryConfirm', { name: library.name }))) return
  isLibraryArchiving.value = new Set(isLibraryArchiving.value).add(library.id)
  try {
    await archiveEnvironmentLibrary(library.id)
    const [nextLibraries, nextFamilies] = await Promise.all([listEnvironmentLibraries(), listLibraryFamilies()])
    environmentLibraries.value = nextLibraries
    libraryFamilies.value = nextFamilies
    noticeKey.value = 'console.libraryArchived'
  } catch {
    loadErrorKey.value = 'console.libraryActionFailed'
  } finally {
    const next = new Set(isLibraryArchiving.value)
    next.delete(library.id)
    isLibraryArchiving.value = next
  }
}

async function archiveProject(workspace: ProjectWorkspaceDto): Promise<void> {
  if (isArchivedProjectStatus(workspace.project.status) || isProjectArchiving.value.has(workspace.project.id)) return
  if (!window.confirm(t('console.archiveProjectConfirm', { name: workspace.project.name }))) return
  isProjectArchiving.value = new Set(isProjectArchiving.value).add(workspace.project.id)
  loadErrorKey.value = ''
  try {
    const archived = await archiveProjectRequest(workspace.project.id)
    workspaces.value = workspaces.value.map((item) => item.project.id === archived.id
      ? { ...item, project: archived }
      : item)
    emit('project-directory-changed', activeWorkspaces.value)
    noticeKey.value = 'console.projectArchived'
  } catch (reason) {
    loadErrorKey.value = reason instanceof FlowApiError && reason.status === 409
      ? 'console.projectArchiveBlocked'
      : 'console.projectArchiveFailed'
  } finally {
    const next = new Set(isProjectArchiving.value)
    next.delete(workspace.project.id)
    isProjectArchiving.value = next
  }
}

async function reindexLibrary(library: LibraryDto): Promise<void> {
  if (isLibraryReindexing.value.has(library.id)) return
  isLibraryReindexing.value = new Set(isLibraryReindexing.value).add(library.id)
  try {
    const refreshed = await reindexEnvironmentLibrary(library.id)
    environmentLibraries.value = environmentLibraries.value.map((item) => item.id === library.id ? refreshed : item)
    noticeKey.value = 'console.libraryReindexed'
  } catch {
    loadErrorKey.value = 'console.libraryReindexFailed'
  } finally {
    const next = new Set(isLibraryReindexing.value)
    next.delete(library.id)
    isLibraryReindexing.value = next
  }
}

async function handleLibraryUploaded(): Promise<void> {
  libraryUploadOpen.value = false
  try {
    const [nextLibraries, nextFamilies, nextUsages] = await Promise.all([
      listEnvironmentLibraries(),
      listLibraryFamilies(),
      listLibraryArtifactUsages(),
    ])
    environmentLibraries.value = nextLibraries
    libraryFamilies.value = nextFamilies
    libraryArtifactUsages.value = nextUsages
    noticeKey.value = 'console.libraryUploadSuccess'
  } catch {
    loadErrorKey.value = 'console.libraryActionFailed'
  }
}

async function handleLibraryFamilyAssigned(): Promise<void> {
  try {
    const [nextLibraries, nextFamilies] = await Promise.all([listEnvironmentLibraries(), listLibraryFamilies()])
    environmentLibraries.value = nextLibraries
    libraryFamilies.value = nextFamilies
    noticeKey.value = 'libraryFamily.assigned'
  } catch {
    loadErrorKey.value = 'console.libraryActionFailed'
  }
}

function shortHash(value: string): string { return value.length <= 12 ? value : value.slice(0, 12) }
function interfaceModeKey(mode: FlowInvocationMode): string { return mode === 'synchronous' ? 'console.interfaceSync' : 'console.interfaceAsync' }
function interfaceEnabledKey(flowInterface: FlowInterfaceDto): string { return flowInterface.isEnabled ? 'console.enabled' : 'console.disabled' }
function selectView(view: ConsoleView): void {
  activeView.value = view
  noticeKey.value = ''
  if (Date.now() - lastDirectoryRefreshAt > 15_000) void refreshDirectoryData()
}

onMounted(() => { void refreshAll(); refreshTimer = window.setInterval(() => void refreshRunData(false), 2_000) })
onBeforeUnmount(() => {
  if (refreshTimer !== undefined) window.clearInterval(refreshTimer)
  if (deferredRefreshTimer !== undefined) window.clearTimeout(deferredRefreshTimer)
  for (const unsubscribe of subscriptions.values()) unsubscribe()
  subscriptions.clear()
})
</script>

<template>
  <main class="operations-console" aria-label="SereinFlow operations console">
    <aside class="operations-console__sidebar">
      <div class="operations-console__identity"><span class="operations-console__eyebrow">SEREINFLOW</span><strong>{{ t('console.consoleLabel') }}</strong></div>
      <nav class="operations-console__nav" :aria-label="t('runs.title')">
        <button type="button" :class="{ active: activeView === 'overview' }" @click="selectView('overview')"><LayoutDashboard :size="17" /><span>{{ t('console.overview') }}</span></button>
        <button type="button" :class="{ active: activeView === 'projects' }" @click="selectView('projects')"><FolderKanban :size="17" /><span>{{ t('console.projects') }}</span></button>
        <button type="button" :class="{ active: activeView === 'queue' }" @click="selectView('queue')"><ListOrdered :size="17" /><span>{{ t('console.queue') }}</span></button>
        <span class="operations-console__nav-divider" aria-hidden="true"></span>
        <button type="button" :class="{ active: activeView === 'settings' }" @click="selectView('settings')"><SlidersHorizontal :size="17" /><span>{{ t('console.environmentSettings') }}</span></button>
        <button type="button" :class="{ active: activeView === 'interfaces' }" @click="selectView('interfaces')"><RadioTower :size="17" /><span>{{ t('console.environmentInterfaces') }}</span></button>
        <button type="button" :class="{ active: activeView === 'libraries' }" @click="selectView('libraries')"><PackagePlus :size="17" /><span>{{ t('console.environmentLibraries') }}</span></button>
        <button type="button" :class="{ active: activeView === 'archives' }" @click="selectView('archives')"><Archive :size="17" /><span>{{ t('console.archives') }}</span></button>
      </nav>
      <div class="operations-console__sidebar-footer"><span>{{ t('runs.activeWorkers') }}</span><strong>{{ overview?.activeRunCount ?? 0 }} / {{ overview?.maxConcurrentRuns ?? '—' }}</strong></div>
    </aside>

    <section class="operations-console__content">
      <header class="operations-console__header"><div><p class="operations-console__eyebrow">{{ t('console.consoleLabel') }}</p><h1>{{ t(activeViewTitleKey) }}</h1></div><button class="icon-button" type="button" :title="t('runs.refresh')" :aria-label="t('runs.refresh')" :disabled="isLoading" @click="refreshAll()"><RefreshCw :size="16" :class="{ 'is-spinning': isLoading }" /></button></header>
      <p v-if="loadErrorKey" class="operations-console__notice operations-console__notice--error" role="alert">{{ t(loadErrorKey) }}</p><p v-else-if="noticeKey" class="operations-console__notice" role="status">{{ t(noticeKey) }}</p>

      <template v-if="activeView === 'overview'">
        <p class="operations-console__intro">{{ t('console.quickPreviewHint') }}</p>
        <dl class="operations-console__metrics"><div><dt>{{ t('runs.queueCapacity') }}</dt><dd>{{ overview?.queuedCount ?? 0 }} <span>/ {{ overview?.queueCapacity ?? '—' }}</span></dd></div><div><dt>{{ t('runs.activeWorkers') }}</dt><dd>{{ overview?.activeRunCount ?? 0 }} <span>/ {{ overview?.maxConcurrentRuns ?? '—' }}</span></dd></div><div><dt>{{ t('runs.listenerWorkers') }}</dt><dd>{{ overview?.activeListenerRunCount ?? 0 }} <span>/ {{ overview?.maxConcurrentListenerRuns ?? '—' }}</span></dd></div><div><dt>{{ t('runs.projectLimit') }}</dt><dd>{{ overview?.maxConcurrentRunsPerProject ?? '—' }}</dd></div></dl>
        <section class="operations-console__section"><div class="operations-console__section-heading"><h2>{{ t('runs.running') }}</h2><span>{{ overview?.activeRuns.length ?? 0 }}</span></div><div class="operations-table-wrap"><table class="operations-table operations-table--active"><thead><tr><th>{{ t('runs.project') }}</th><th>{{ t('runs.flow') }}</th><th>{{ t('runs.kind') }}</th><th>{{ t('runs.startedAt') }}</th><th>{{ t('runs.actions') }}</th></tr></thead><tbody v-if="overview?.activeRuns.length"><tr v-for="run in overview.activeRuns" :key="run.id"><td>{{ projectNameFor(run) }}</td><td><code>{{ flowNameFor(run) }}</code></td><td>{{ t(runKindKey(run)) }}</td><td>{{ formatDate(run.startedAt ?? run.createdAt) }}</td><td class="operations-table__actions"><button class="icon-button" type="button" :title="t('workpiece.open')" :aria-label="t('workpiece.open')" @click="openWorkpieces(run)"><PackageOpen :size="16" /></button><button v-if="isCancellable(run)" class="icon-button icon-button--danger" type="button" :title="t('runs.cancel')" :aria-label="t('runs.cancel')" :disabled="cancellingRunIds.has(run.id)" @click="cancelRun(run)"><Square :size="14" fill="currentColor" /></button></td></tr></tbody><tbody v-else><tr><td class="operations-table__empty" colspan="5">{{ t('runs.emptyActive') }}</td></tr></tbody></table></div></section>
         <section class="operations-console__section"><div class="operations-console__section-heading"><h2>{{ t('console.queue') }}</h2><button class="text-button text-button--with-icon" type="button" @click="selectView('queue')"><ListOrdered :size="14" /><span>{{ t('console.viewQueue') }}</span></button></div><div class="operations-table-wrap"><table class="operations-table operations-table--overview"><thead><tr><th>{{ t('runs.project') }}</th><th>{{ t('runs.flow') }}</th><th>{{ t('runs.kind') }}</th><th>{{ t('runs.status') }}</th><th>{{ t('runs.queuedAt') }}</th><th>{{ t('runs.actions') }}</th></tr></thead><tbody v-if="latestRuns.length"><tr v-for="run in latestRuns" :key="run.id"><td>{{ projectNameFor(run) }}</td><td><code>{{ flowNameFor(run) }}</code></td><td>{{ t(runKindKey(run)) }}</td><td><span :class="['run-status', `run-status--${queueStatusClass(run)}`]">{{ t(queueStatusKey(run)) }}</span></td><td>{{ formatDate(run.createdAt) }}</td><td class="operations-table__actions"><button class="icon-button" type="button" :title="t('console.snapshot')" :aria-label="t('console.snapshot')" @click="openSnapshot(run)"><Eye :size="16" /></button><button class="icon-button" type="button" :title="t('workpiece.open')" :aria-label="t('workpiece.open')" @click="openWorkpieces(run)"><PackageOpen :size="16" /></button><button v-if="isInterruptible(run)" class="icon-button icon-button--warning" type="button" :title="t('runs.interrupt')" :aria-label="t('runs.interrupt')" :disabled="interruptingRunIds.has(run.id)" @click="interruptRun(run)"><CircleOff :size="15" /></button><button v-if="isCancellable(run)" class="icon-button icon-button--danger" type="button" :title="t('runs.cancel')" :aria-label="t('runs.cancel')" :disabled="cancellingRunIds.has(run.id)" @click="cancelRun(run)"><Square :size="14" fill="currentColor" /></button></td></tr></tbody><tbody v-else><tr><td class="operations-table__empty" colspan="6">{{ t('runs.emptyRecent') }}</td></tr></tbody></table></div></section>
      </template>

      <template v-else-if="activeView === 'projects'">
        <p class="operations-console__intro">{{ t('console.projectListHint') }}</p>
        <section class="project-catalog"><article v-for="workspace in activeWorkspaces" :key="workspace.project.id" class="project-catalog__item"><div><h2>{{ workspace.project.name }}</h2><p>{{ t('console.projectCreatedAt', { date: formatDate(workspace.project.createdAt) }) }}</p></div><dl><div><dt>{{ t('project.flows') }}</dt><dd>{{ workspace.flows.length }}</dd></div><div><dt>{{ t('console.canvasCount') }}</dt><dd>{{ workspace.flows.reduce((total, flow) => total + (flow.canvasCount ?? 0), 0) }}</dd></div><div><dt>{{ t('console.nodeCount') }}</dt><dd>{{ workspace.flows.reduce((total, flow) => total + (flow.nodeCount ?? 0), 0) }}</dd></div></dl><div class="project-catalog__actions"><button class="command-button quiet" type="button" :disabled="workspace.flows.length === 0" @click="openFlow(workspace, workspace.flows[0]?.id)"><FilePenLine :size="15" /><span>{{ t('console.openEditor') }}</span></button><button class="icon-button icon-button--danger" type="button" :title="t('console.archiveProject')" :aria-label="t('console.archiveProject')" :disabled="isProjectArchiving.has(workspace.project.id)" @click="archiveProject(workspace)"><Archive :size="15" /></button></div></article><p v-if="activeWorkspaces.length === 0" class="operations-console__empty">{{ t('runs.emptyProjects') }}</p></section>
        <button class="command-button operations-console__create-project" type="button" @click="emit('start-new-project')"><Plus :size="16" /><span>{{ t('project.newProject') }}</span></button>
      </template>

      <template v-else-if="activeView === 'queue'">
        <p class="operations-console__intro">{{ t('console.queueHint') }}</p>
         <div class="operations-table-wrap"><table class="operations-table operations-table--queue"><thead><tr><th>{{ t('runs.project') }}</th><th>{{ t('runs.flow') }}</th><th>{{ t('runs.kind') }}</th><th>{{ t('runs.status') }}</th><th>{{ t('runs.policy') }}</th><th>{{ t('runs.queuedAt') }}</th><th>{{ t('runs.actions') }}</th></tr></thead><tbody v-if="runs.length"><tr v-for="run in runs" :key="run.id"><td>{{ projectNameFor(run) }}</td><td><code>{{ flowNameFor(run) }}</code></td><td>{{ t(runKindKey(run)) }}</td><td><span :class="['run-status', `run-status--${queueStatusClass(run)}`]">{{ t(queueStatusKey(run)) }}</span></td><td>{{ t(`runs.policy.${run.concurrencyMode ?? 'parallel'}`) }}</td><td>{{ formatDate(run.createdAt) }}</td><td class="operations-table__actions"><button class="icon-button" type="button" :title="t('console.snapshot')" :aria-label="t('console.snapshot')" @click="openSnapshot(run)"><FilePenLine :size="15" /></button><button class="icon-button" type="button" :title="t('workpiece.open')" :aria-label="t('workpiece.open')" @click="openWorkpieces(run)"><PackageOpen :size="15" /></button><button v-if="isInterruptible(run)" class="icon-button icon-button--warning" type="button" :title="t('runs.interrupt')" :aria-label="t('runs.interrupt')" :disabled="interruptingRunIds.has(run.id)" @click="interruptRun(run)"><CircleOff :size="15" /></button><button v-if="isCancellable(run)" class="icon-button icon-button--danger" type="button" :title="t('runs.cancel')" :aria-label="t('runs.cancel')" :disabled="cancellingRunIds.has(run.id)" @click="cancelRun(run)"><Square :size="14" fill="currentColor" /></button></td></tr></tbody><tbody v-else><tr><td class="operations-table__empty" colspan="7">{{ t('runs.emptyRecent') }}</td></tr></tbody></table></div>
      </template>

       <template v-else-if="activeView === 'settings'">
         <p class="operations-console__intro">{{ t('console.environmentSettingsHint') }}</p>
         <form class="environment-settings" @submit.prevent="saveSettings"><label><span>{{ t('runs.queueCapacity') }}</span><input v-model.number="settingsForm.queueCapacity" type="number" min="1" max="10000" required /></label><label><span>{{ t('runs.activeWorkers') }}</span><input v-model.number="settingsForm.maxConcurrentRuns" type="number" min="1" max="1024" required /></label><label><span>{{ t('runs.listenerWorkers') }}</span><input v-model.number="settingsForm.maxConcurrentListenerRuns" type="number" min="0" max="1024" required /></label><label><span>{{ t('runs.projectLimit') }}</span><input v-model.number="settingsForm.maxConcurrentRunsPerProject" type="number" min="1" max="1024" required /></label><label><span>{{ t('console.queueWaitTimeout') }}</span><input v-model.number="settingsForm.queueWaitTimeoutSeconds" type="number" min="1" max="86400" required /></label><label><span>{{ t('console.shutdownGrace') }}</span><input v-model.number="settingsForm.shutdownGracePeriodSeconds" type="number" min="1" max="300" required /></label><label><span>{{ t('console.syncTimeout') }}</span><input v-model.number="settingsForm.synchronousInvocationTimeoutSeconds" type="number" min="1" max="300" required /></label><label><span>{{ t('console.maxLibraryUploadSize') }}</span><input v-model.number="maxLibraryUploadMegabytes" type="number" min="1" max="512" step="1" required /><small>{{ t('console.maxLibraryUploadSizeHint') }}</small></label><div class="environment-settings__actions"><button class="command-button run" type="submit" :disabled="isSettingsSaving"><Save :size="15" /><span>{{ t('console.saveSettings') }}</span></button></div></form>
        <McpKeySettings :project-workspaces="activeWorkspaces" />
      </template>

      <template v-else-if="activeView === 'libraries'">
        <p class="operations-console__intro">{{ t('console.environmentLibrariesHint') }}</p>
        <section class="operations-console__section environment-libraries">
          <div class="operations-console__section-heading">
            <div><h2>{{ t('console.environmentLibrariesTitle') }}</h2><p>{{ t('console.environmentLibrariesTableHint') }}</p></div>
            <button class="command-button run" type="button" @click="libraryUploadOpen = true"><PackagePlus :size="15" /><span>{{ t('library.upload') }}</span></button>
          </div>
          <div v-if="activeLibraryFamilyGroups.length" class="library-family-groups">
            <details v-for="group in activeLibraryFamilyGroups" :key="group.id" class="library-family-group" :open="group.isUnassigned">
              <summary>
                <div class="library-family-group__title"><FolderKanban :size="16" /><strong>{{ group.name }}</strong><span v-if="group.isUnassigned" class="library-family-group__state">{{ t('libraryFamily.unassigned') }}</span></div>
                <dl class="library-family-group__summary">
                  <div><dt>{{ t('console.libraryFamilyArtifacts') }}</dt><dd>{{ group.artifacts.length }}</dd></div>
                  <div><dt>{{ t('console.libraryFamilyRecommended') }}</dt><dd>{{ group.latestArtifactId ? (group.artifacts.find((artifact) => artifact.id === group.latestArtifactId)?.semanticVersion ?? group.artifacts.find((artifact) => artifact.id === group.latestArtifactId)?.version ?? '—') : '—' }}</dd></div>
                  <div><dt>{{ t('console.libraryFamilyLatestUpload') }}</dt><dd>{{ formatDate(group.lastUploadedAt) }}</dd></div>
                </dl>
              </summary>
              <div class="operations-table-wrap library-family-group__artifacts">
                <table class="operations-table operations-table--libraries">
                  <thead><tr><th>{{ t('console.libraryName') }}</th><th>{{ t('console.libraryVersion') }}</th><th>SHA</th><th>{{ t('console.libraryNodes') }}</th><th>{{ t('console.libraryCurrentFlows') }}</th><th>{{ t('console.libraryFlowVersions') }}</th><th>{{ t('console.libraryRunSnapshots') }}</th><th>{{ t('console.libraryUploadedAt') }}</th><th>{{ t('console.libraryStatus') }}</th><th>{{ t('runs.actions') }}</th></tr></thead>
                  <tbody>
                    <tr v-for="library in group.artifacts" :key="library.id">
                      <td><strong>{{ library.name }}</strong><small>{{ library.fileName }}</small></td>
                      <td><span class="library-version" :class="{ 'library-version--recommended': library.id === group.latestArtifactId }">{{ library.semanticVersion ?? library.version }}</span></td>
                      <td><code>{{ shortHash(library.sha256) }}</code></td>
                      <td>{{ library.nodes.length }}</td>
                      <td>{{ libraryUsageByArtifactId.get(library.id)?.currentFlowCount ?? 0 }}</td>
                      <td>{{ libraryUsageByArtifactId.get(library.id)?.flowVersionCount ?? 0 }}</td>
                      <td>{{ libraryUsageByArtifactId.get(library.id)?.runSnapshotCount ?? 0 }}</td>
                      <td>{{ formatDate(library.uploadedAt) }}</td>
                      <td><span class="library-state library-state--available">{{ t('console.libraryStatusAvailable') }}</span></td>
                      <td class="operations-table__actions"><button class="icon-button" type="button" :title="t('libraryFamily.assign')" :aria-label="t('libraryFamily.assign')" @click="libraryFamilyAssignment = library"><FolderKanban :size="15" /></button><button class="icon-button" type="button" :title="t('console.reindexLibrary')" :aria-label="t('console.reindexLibrary')" :disabled="isLibraryReindexing.has(library.id)" @click="reindexLibrary(library)"><RefreshCw :size="15" :class="{ 'is-spinning': isLibraryReindexing.has(library.id) }" /></button><button class="icon-button icon-button--danger" type="button" :title="t('console.archiveLibrary')" :aria-label="t('console.archiveLibrary')" :disabled="isLibraryArchiving.has(library.id)" @click="archiveLibrary(library)"><Archive :size="15" /></button></td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </details>
          </div>
          <p v-else class="operations-console__empty">{{ t('console.libraryEmpty') }}</p>
        </section>
      </template>

      <template v-else-if="activeView === 'archives'">
        <p class="operations-console__intro">{{ t('console.archivesHint') }}</p>
        <section class="operations-console__section"><div class="operations-console__section-heading"><h2>{{ t('console.archivedProjects') }}</h2><span>{{ archivedWorkspaces.length }}</span></div><div class="project-catalog"><article v-for="workspace in archivedWorkspaces" :key="workspace.project.id" class="project-catalog__item project-catalog__item--archived"><div><h2>{{ workspace.project.name }}</h2><p>{{ t('console.projectCreatedAt', { date: formatDate(workspace.project.createdAt) }) }}</p></div><dl><div><dt>{{ t('project.flows') }}</dt><dd>{{ workspace.flows.length }}</dd></div><div><dt>{{ t('console.canvasCount') }}</dt><dd>{{ workspace.flows.reduce((total, flow) => total + (flow.canvasCount ?? 0), 0) }}</dd></div><div><dt>{{ t('console.nodeCount') }}</dt><dd>{{ workspace.flows.reduce((total, flow) => total + (flow.nodeCount ?? 0), 0) }}</dd></div></dl><span class="project-catalog__readonly"><Archive :size="14" />{{ t('console.projectArchivedReadonly') }}</span></article><p v-if="archivedWorkspaces.length === 0" class="operations-console__empty">{{ t('console.archivedProjectsEmpty') }}</p></div></section>
        <section class="operations-console__section"><div class="operations-console__section-heading"><h2>{{ t('console.archivedLibraries') }}</h2><span>{{ archivedEnvironmentLibraries.length }}</span></div><div class="operations-table-wrap"><table class="operations-table operations-table--libraries"><thead><tr><th>{{ t('console.libraryName') }}</th><th>{{ t('console.libraryFamily') }}</th><th>{{ t('console.libraryVersion') }}</th><th>SHA</th><th>{{ t('console.libraryNodes') }}</th><th>{{ t('console.libraryCurrentFlows') }}</th><th>{{ t('console.libraryFlowVersions') }}</th><th>{{ t('console.libraryRunSnapshots') }}</th><th>{{ t('console.libraryUploadedAt') }}</th><th>{{ t('console.libraryStatus') }}</th></tr></thead><tbody v-if="archivedEnvironmentLibraries.length"><tr v-for="library in archivedEnvironmentLibraries" :key="library.id"><td><strong>{{ library.name }}</strong><small>{{ library.fileName }}</small></td><td>{{ library.familyName ?? (library.familyId ? t('library.familyBound') : t('libraryFamily.unassigned')) }}</td><td>{{ library.semanticVersion ?? library.version }}</td><td><code>{{ shortHash(library.sha256) }}</code></td><td>{{ library.nodes.length }}</td><td>{{ libraryUsageByArtifactId.get(library.id)?.currentFlowCount ?? 0 }}</td><td>{{ libraryUsageByArtifactId.get(library.id)?.flowVersionCount ?? 0 }}</td><td>{{ libraryUsageByArtifactId.get(library.id)?.runSnapshotCount ?? 0 }}</td><td>{{ formatDate(library.uploadedAt) }}</td><td><span class="library-state library-state--archived"><Archive :size="13" />{{ t('console.libraryStatusArchived') }}</span></td></tr></tbody><tbody v-else><tr><td class="operations-table__empty" colspan="10">{{ t('console.archivedLibrariesEmpty') }}</td></tr></tbody></table></div></section>
      </template>

      <template v-else>
        <p class="operations-console__intro">{{ t('console.environmentInterfacesHint') }}</p>
        <section class="interface-layout"><form class="interface-form" @submit.prevent="saveInterface"><h2>{{ editingInterfaceId ? t('console.updateInterface') : t('console.createInterface') }}</h2><label><span>{{ t('console.interfaceName') }}</span><input v-model.trim="interfaceForm.name" type="text" maxlength="80" required /></label><label><span>{{ t('console.interfaceProject') }}</span><select v-model="interfaceForm.projectId" :disabled="Boolean(editingInterfaceId)" required @change="selectInterfaceProject"><option value="" disabled>{{ t('console.noProductionFlow') }}</option><option v-for="workspace in availableInterfaceWorkspaces" :key="workspace.project.id" :value="workspace.project.id">{{ workspace.project.name }}</option></select></label><label><span>{{ t('console.interfaceFlow') }}</span><select v-model="interfaceForm.flowId" :disabled="Boolean(editingInterfaceId)" required><option v-for="flow in availableInterfaceFlows" :key="flow.id" :value="flow.id">{{ shortId(flow.id) }} · v{{ flow.productionVersion }}</option></select></label><fieldset><legend>{{ t('console.interfaceMode') }}</legend><label class="interface-form__choice"><input v-model="interfaceForm.invocationMode" type="radio" value="asynchronous" /><span><strong>{{ t('console.interfaceAsync') }}</strong><small>{{ t('console.interfaceAsyncHint') }}</small></span></label><label class="interface-form__choice"><input v-model="interfaceForm.invocationMode" type="radio" value="synchronous" /><span><strong>{{ t('console.interfaceSync') }}</strong><small>{{ t('console.interfaceSyncHint') }}</small></span></label></fieldset><label class="interface-form__toggle"><input v-model="interfaceForm.isEnabled" type="checkbox" /><span>{{ t('console.interfaceEnabled') }}</span></label><div class="interface-form__actions"><button class="command-button run" type="submit" :disabled="isInterfaceSaving || (!editingInterfaceId && !interfaceForm.flowId)"><Save :size="15" /><span>{{ editingInterfaceId ? t('console.updateInterface') : t('console.createInterface') }}</span></button><button v-if="editingInterfaceId" class="command-button quiet" type="button" @click="resetInterfaceForm"><X :size="15" /><span>{{ t('console.cancelEdit') }}</span></button></div></form><div class="interface-list"><div class="operations-console__section-heading"><h2>{{ t('console.environmentInterfaces') }}</h2><span>{{ interfaces.length }}</span></div><div class="operations-table-wrap"><table class="operations-table operations-table--interfaces"><thead><tr><th>{{ t('console.interfaceName') }}</th><th>{{ t('console.interfaceMode') }}</th><th>{{ t('console.interfaceProductionVersion') }}</th><th>{{ t('console.interfaceEnabled') }}</th><th>{{ t('console.interfaceUrl') }}</th><th>{{ t('console.interfaceActions') }}</th></tr></thead><tbody v-if="interfaces.length"><tr v-for="flowInterface in interfaces" :key="flowInterface.id"><td>{{ flowInterface.name }}</td><td>{{ t(interfaceModeKey(flowInterface.invocationMode)) }}</td><td><code>v{{ flowInterface.productionVersion ?? '—' }}</code></td><td><span :class="['interface-state', { 'interface-state--disabled': !flowInterface.isEnabled }]">{{ t(interfaceEnabledKey(flowInterface)) }}</span></td><td><code class="interface-list__url">{{ interfaceUrl(flowInterface) }}</code></td><td class="operations-table__actions"><button class="icon-button" type="button" :title="t('console.copyUrl')" :aria-label="t('console.copyUrl')" @click="copyInterfaceUrl(flowInterface)"><Copy :size="15" /></button><button class="icon-button" type="button" :title="t('console.updateInterface')" :aria-label="t('console.updateInterface')" @click="editInterface(flowInterface)"><Settings2 :size="15" /></button><button class="icon-button icon-button--danger" type="button" :title="t('command.delete')" :aria-label="t('command.delete')" @click="removeInterface(flowInterface)"><Trash2 :size="15" /></button></td></tr></tbody><tbody v-else><tr><td class="operations-table__empty" colspan="6">{{ t('console.interfaceEmpty') }}</td></tr></tbody></table></div></div></section>
      </template>
    </section>

     <LibraryUploadDialog v-if="libraryUploadOpen" :max-file-bytes="settingsForm.maxLibraryUploadBytes" @close="libraryUploadOpen = false" @uploaded="handleLibraryUploaded" />
    <LibraryFamilyDialog v-if="libraryFamilyAssignment" :library="libraryFamilyAssignment" @close="libraryFamilyAssignment = undefined" @assigned="handleLibraryFamilyAssigned" />
    <div v-if="snapshotRun" class="snapshot-dialog-backdrop" role="presentation" @click.self="closeSnapshot"><section class="snapshot-dialog" role="dialog" aria-modal="true" :aria-label="t('console.snapshotTitle')"><header><div><p class="operations-console__eyebrow">{{ shortId(snapshotRun.id) }}</p><h2>{{ t('console.snapshotTitle') }}</h2></div><button class="icon-button" type="button" :title="t('command.close')" :aria-label="t('command.close')" @click="closeSnapshot"><X :size="16" /></button></header><p>{{ t('console.snapshotReadonly') }}</p><div v-if="isSnapshotLoading" class="snapshot-dialog__status">{{ t('console.snapshotLoading') }}</div><div v-else-if="snapshotError" class="snapshot-dialog__status snapshot-dialog__status--error">{{ snapshotError }}</div><RunSnapshotViewer v-else :definition="snapshot" :outputs="snapshotOutputs" :outputs-error="snapshotOutputsError" :events="snapshotEvents" :events-error="snapshotEventsError" :is-debug-run="snapshotRun.executionKind === 'debug'" /></section></div>
    <div v-if="selectedWorkpieceRun" class="workpiece-dialog-backdrop" role="presentation" @click.self="closeWorkpieces"><section class="workpiece-dialog" role="dialog" aria-modal="true" :aria-label="t('workpiece.title')"><header><div><p class="operations-console__eyebrow">{{ shortId(selectedWorkpieceRun.id) }}</p><h2>{{ t('workpiece.title') }}</h2><span>{{ projectNameFor(selectedWorkpieceRun) }} · {{ flowNameFor(selectedWorkpieceRun) }}</span></div><button class="icon-button" type="button" :title="t('command.close')" :aria-label="t('command.close')" @click="closeWorkpieces"><X :size="16" /></button></header><RunWorkpiecePanel :run-id="selectedWorkpieceRun.id" :live="isWorkpieceLive(selectedWorkpieceRun)" /></section></div>
  </main>
</template>
