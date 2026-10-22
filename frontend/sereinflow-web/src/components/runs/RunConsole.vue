<script setup lang="ts">
type TemplateItem<T> = T extends readonly (infer Item)[] ? Item : T extends Record<string, infer Value> ? Value : T extends number ? number : never;
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue';
import { Archive, CircleOff, Copy, Eye, FilePenLine, FolderKanban, LayoutDashboard, ListOrdered, PackageOpen, PackagePlus, Plus, RadioTower, RefreshCw, Save, Settings2, SlidersHorizontal, Square, Trash2, X } from 'lucide-vue-next';
import { FlowApiError, archiveProject as archiveProjectRequest, cancelFlowRun, interruptFlowRun, createFlowInterface, deleteFlowInterface, getFlowRunOverview, listFlowRunOutputs, listFlowRunEvents, getFlowRunSnapshot, getRunExecutionSettings, listFlowInterfaces, listFlowRuns, listProjects, saveRunExecutionSettings, subscribeFlowRunEvents, updateFlowInterface, type FlowDefinitionDto, type FlowInterfaceDto, type FlowInvocationMode, type FlowRunDto, type FlowRunOverviewDto, type FlowRunEventDto, type FlowRunOutputDto, type ProjectWorkspaceDto, type RunExecutionSettingsDto, } from '../../api/flowApi';
import { archiveEnvironmentLibrary, listEnvironmentLibraries, listLibraryArtifactUsages, listLibraryFamilies, reindexEnvironmentLibrary, type LibraryArtifactUsageDto, type LibraryDto, type LibraryFamilyDto, } from '../../api/libraryApi';
import { isArchivedProjectStatus } from '../../flow/projectStatus';
import { locale, t } from '../../i18n';
import LibraryFamilyDialog from '../library/LibraryFamilyDialog.vue';
import LibraryUploadDialog from '../library/LibraryUploadDialog.vue';
import ApiKeySettings from './ApiKeySettings.vue';
import { securityCapabilities } from '../../api/securityApi';
import RunSnapshotViewer from './RunSnapshotViewer.vue';
import RunWorkpiecePanel from './RunWorkpiecePanel.vue';
type ConsoleView = 'overview' | 'projects' | 'queue' | 'settings' | 'interfaces' | 'libraries' | 'archives';
const props = defineProps<{
  projectWorkspaces: ProjectWorkspaceDto[];
}>();
const emit = defineEmits<{
  'open-flow': [
    workspace: ProjectWorkspaceDto,
    flowId?: string
  ];
  'start-new-project': [
  ];
  'project-directory-changed': [
    workspaces: ProjectWorkspaceDto[]
  ];
}>();
const activeView = ref<ConsoleView>('overview');
const overview = ref<FlowRunOverviewDto>();
const workspaces = ref<ProjectWorkspaceDto[]>([...props.projectWorkspaces]);
const runs = ref<FlowRunDto[]>([]);
const interfaces = ref<FlowInterfaceDto[]>([]);
const isLoading = ref(true);
const isSettingsSaving = ref(false);
const mcpKeySettings = ref<InstanceType<typeof ApiKeySettings>>();
const isInterfaceSaving = ref(false);
const loadErrorKey = ref('');
const noticeKey = ref('');
const setInstance5: Set<string> = new Set();
const cancellingRunIds = ref<Set<string>>(setInstance5);
const setInstance6: Set<string> = new Set();
const interruptingRunIds = ref<Set<string>>(setInstance6);
const snapshot = ref<FlowDefinitionDto>();
const snapshotOutputs = ref<FlowRunOutputDto[]>([]);
const snapshotOutputsError = ref('');
const snapshotEvents = ref<FlowRunEventDto[]>([]);
const snapshotEventsError = ref('');
const snapshotRun = ref<FlowRunDto>();
const isSnapshotLoading = ref(false);
const snapshotError = ref('');
const workpieceRun = ref<FlowRunDto>();
const environmentLibraries = ref<LibraryDto[]>([]);
const libraryFamilies = ref<LibraryFamilyDto[]>([]);
const libraryArtifactUsages = ref<LibraryArtifactUsageDto[]>([]);
const libraryUploadOpen = ref(false);
const libraryFamilyAssignment = ref<LibraryDto>();
const setInstance7: Set<string> = new Set();
const isLibraryArchiving = ref<Set<string>>(setInstance7);
const setInstance8: Set<string> = new Set();
const isLibraryReindexing = ref<Set<string>>(setInstance8);
const setInstance9: Set<string> = new Set();
const isProjectArchiving = ref<Set<string>>(setInstance9);
const editingInterfaceId = ref<string>();
const subscriptions = new Map<string, () => void>();
let refreshTimer: number | undefined;
let deferredRefreshTimer: number | undefined;
let lastDirectoryRefreshAt = 0;
const settingsForm = reactive<RunExecutionSettingsDto>({
  queueCapacity: 100,
  maxConcurrentRuns: 4,
  maxConcurrentListenerRuns: 1,
  maxConcurrentRunsPerProject: 2,
  queueWaitTimeoutSeconds: 60,
  shutdownGracePeriodSeconds: 10,
  synchronousInvocationTimeoutSeconds: 30,
  maxLibraryUploadBytes: 100 * 1024 * 1024,
});
const interfaceForm = reactive({
  name: '',
  projectId: '',
  flowId: '',
  invocationMode: 'asynchronous' as FlowInvocationMode,
  isEnabled: true,
});
const maxLibraryUploadMegabytes = computed({
  get: () => {
    const roundResult: number = Math.round(settingsForm.maxLibraryUploadBytes / (1024 * 1024));
    return roundResult;
  },
  set: (value: number) => {
    settingsForm.maxLibraryUploadBytes = Math.round(value) * 1024 * 1024;
  },
});
const selectedInterfaceProject = computed(() => {
  const findResult: {
    project: {
      id: string;
      name: string;
      version: number;
      status: string;
      createdAt: string;
      updatedAt: string;
    };
    flows: {
      id: string;
      version: number;
      entryNodeId: string;
      canvasCount?: number | undefined;
      nodeCount?: number | undefined;
      productionVersion?: number | undefined;
    }[];
  } | undefined = activeWorkspaces.value.find((item) => item.project.id === interfaceForm.projectId);
  return findResult;
});
const availableInterfaceFlows = computed(() => {
  const filterResult: {
    id: string;
    version: number;
    entryNodeId: string;
    canvasCount?: number | undefined;
    nodeCount?: number | undefined;
    productionVersion?: number | undefined;
  }[] = (selectedInterfaceProject.value?.flows ?? [])
    .filter((flow) => {
    const booleanResult: boolean = Boolean(flow.productionVersion);
    return booleanResult;
  });
  return filterResult;
});
const availableInterfaceWorkspaces = computed(() => {
  const filterResult1: {
    project: {
      id: string;
      name: string;
      version: number;
      status: string;
      createdAt: string;
      updatedAt: string;
    };
    flows: {
      id: string;
      version: number;
      entryNodeId: string;
      canvasCount?: number | undefined;
      nodeCount?: number | undefined;
      productionVersion?: number | undefined;
    }[];
  }[] = activeWorkspaces.value
    .filter((workspace) => {
    const someResult: boolean = workspace.flows.some((flow) => {
      const booleanResult1: boolean = Boolean(flow.productionVersion);
      return booleanResult1;
    });
    return someResult;
  });
  return filterResult1;
});
const latestRuns = computed(() => {
  const sliceResult: {
    id: string;
    flowId: string;
    flowVersion: number;
    status: "pending" | "running" | "succeeded" | "failed" | "cancelled" | "timedOut" | "interrupted";
    startedAt?: string | undefined;
    endedAt?: string | undefined;
    errorSummary?: string | undefined;
    projectId?: string | undefined;
    createdAt?: string | undefined;
    cancellationReason?: string | undefined;
    concurrencyMode?: import("../../api/flowApi").FlowConcurrencyMode | undefined;
    isListenerRun?: boolean | undefined;
    queuedAt?: string | undefined;
    executionKind?: "production" | "debug" | undefined;
    debugSessionId?: string | undefined;
    definitionChecksum?: string | undefined;
  }[] = runs.value.slice(0, 6);
  return sliceResult;
});
const activeWorkspaces = computed(() => {
  const filterResult2: {
    project: {
      id: string;
      name: string;
      version: number;
      status: string;
      createdAt: string;
      updatedAt: string;
    };
    flows: {
      id: string;
      version: number;
      entryNodeId: string;
      canvasCount?: number | undefined;
      nodeCount?: number | undefined;
      productionVersion?: number | undefined;
    }[];
  }[] = workspaces.value.filter((workspace) => {
    const evaluatedValue: boolean = !isArchivedProjectStatus(workspace.project.status);
    return evaluatedValue;
  });
  return filterResult2;
});
const archivedWorkspaces = computed(() => {
  const filterResult3: {
    project: {
      id: string;
      name: string;
      version: number;
      status: string;
      createdAt: string;
      updatedAt: string;
    };
    flows: {
      id: string;
      version: number;
      entryNodeId: string;
      canvasCount?: number | undefined;
      nodeCount?: number | undefined;
      productionVersion?: number | undefined;
    }[];
  }[] = workspaces.value.filter((workspace) => {
    const isArchivedProjectStatusResult: boolean = isArchivedProjectStatus(workspace.project.status);
    return isArchivedProjectStatusResult;
  });
  return filterResult3;
});
const activeEnvironmentLibraries = computed(() => {
  const filterResult4: {
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
  }[] = environmentLibraries.value.filter((library) => library.lifecycle === 'available');
  return filterResult4;
});
const archivedEnvironmentLibraries = computed(() => {
  const filterResult5: {
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
  }[] = environmentLibraries.value.filter((library) => library.lifecycle === 'archived');
  return filterResult5;
});
const libraryUsageByArtifactId = computed(() => {
  const mapResult: Iterable<readonly [
    string,
    {
      libraryArtifactId: string;
      currentFlowCount: number;
      flowVersionCount: number;
      runSnapshotCount: number;
    }
  ]> | null | undefined = libraryArtifactUsages.value.map((usage) => [usage.libraryArtifactId, usage]);
  const mapInstance: Map<string, {
    libraryArtifactId: string;
    currentFlowCount: number;
    flowVersionCount: number;
    runSnapshotCount: number;
  }> = new Map(mapResult);
  return mapInstance;
});
const activeLibraryFamilyGroups = computed(() => {
  const mapResult1: Iterable<readonly [
    string,
    {
      id: string;
      name: string;
      description?: string | null | undefined;
      latestArtifactId?: string | null | undefined;
      createdAt: string;
      updatedAt: string;
      artifacts?: {
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
      }[] | null | undefined;
    }
  ]> | null | undefined = libraryFamilies.value.map((family) => [family.id, family]);
  const familyById = new Map(mapResult1);
  const groups = new Map<string, {
    id: string;
    name: string;
    latestArtifactId?: string | null;
    artifacts: LibraryDto[];
    isUnassigned: boolean;
  }>();
  for (const library of activeEnvironmentLibraries.value) {
    const family = library.familyId ? familyById.get(library.familyId) : undefined;
    const id = family?.id ?? library.familyId ?? 'unassigned';
    const name = family?.name ?? library.familyName ?? t('libraryFamily.unassigned');
    const current = groups.get(id) ?? {
      id,
      name,
      latestArtifactId: family?.latestArtifactId,
      artifacts: [],
      isUnassigned: !family,
    };
    current.artifacts.push(library);
    groups.set(id, current);
  }
  const evaluatedValue3 = [...groups.values()];
  const mapResult2 = evaluatedValue3.map((group) => {
    const artifacts = [...group.artifacts].sort((left, right) => {
      const leftRecommended = left.id === group.latestArtifactId ? 1 : 0;
      const rightRecommended = right.id === group.latestArtifactId ? 1 : 0;
      if (leftRecommended !== rightRecommended) {
        return rightRecommended - leftRecommended;
      }
      const localeCompareResult: number = right.uploadedAt.localeCompare(left.uploadedAt);
      return localeCompareResult;
    });
    const evaluatedValue1: {
      artifacts: LibraryDto[];
      lastUploadedAt: string;
      id: string;
      name: string;
      latestArtifactId?: string | null;
      isUnassigned: boolean;
    } = {
      ...group,
      artifacts,
      lastUploadedAt: artifacts.reduce((latest, artifact) => artifact.uploadedAt > latest ? artifact.uploadedAt : latest, ''),
    };
    return evaluatedValue1;
  });
  const sortResult: {
    artifacts: LibraryDto[];
    lastUploadedAt: string;
    id: string;
    name: string;
    latestArtifactId?: string | null;
    isUnassigned: boolean;
  }[] = mapResult2.sort((left, right) => {
    const evaluatedValue2: number = Number(left.isUnassigned) - Number(right.isUnassigned) || left.name.localeCompare(right.name, locale.value);
    return evaluatedValue2;
  });
  return sortResult;
});
const activeViewTitleKey = computed(() => ({
  overview: 'console.quickPreview',
  projects: 'console.projectListTitle',
  queue: 'console.queueTitle',
  settings: 'console.environmentSettingsTitle',
  interfaces: 'console.environmentInterfacesTitle',
  libraries: 'console.environmentLibrariesTitle',
  archives: 'console.archivesTitle',
} as const)[activeView.value]);
const selectedWorkpieceRun = computed(() => {
  const evaluatedValue4: FlowRunDto | undefined = workpieceRun.value
    ? runs.value.find((run) => run.id === workpieceRun.value?.id) ?? workpieceRun.value
    : undefined;
  return evaluatedValue4;
});
watch(() => props.projectWorkspaces, (nextWorkspaces) => {
  const mapResult3: Iterable<string> | null | undefined = nextWorkspaces.map((workspace) => workspace.project.id);
  const activeIds = new Set(mapResult3);
  const retainedArchives = workspaces.value.filter((workspace) => {
    const evaluatedValue5: boolean = isArchivedProjectStatus(workspace.project.status) && !activeIds.has(workspace.project.id);
    return evaluatedValue5;
  });
  workspaces.value = [...nextWorkspaces, ...retainedArchives];
  ensureInterfaceFormTarget();
});
function scheduleRefresh(delay = 250): void {
  if (deferredRefreshTimer !== undefined) {
    return;
  }
  deferredRefreshTimer = window.setTimeout(() => {
    deferredRefreshTimer = undefined;
    void refreshRunData(false);
  }, delay);
}
function syncActiveSubscriptions(activeRuns: FlowRunDto[]): void {
  const mapResult4: Iterable<string> | null | undefined = activeRuns.map((run) => run.id);
  const activeIds = new Set(mapResult4);
  for (const subscription of subscriptions) {
    const runId = subscription[0];
    const unsubscribe = subscription[1];
    if (activeIds.has(runId)) {
      continue;
    }
    unsubscribe();
    subscriptions.delete(runId);
  }
  for (const run of activeRuns) {
    if (subscriptions.has(run.id)) {
      continue;
    }
    const argumentValue: string = run.id;
    const subscribeFlowRunEventsResult: () => void = subscribeFlowRunEvents(run.id, () => {
      const scheduleRefreshResult: void = scheduleRefresh();
      return scheduleRefreshResult;
    }, () => {
      const scheduleRefreshResult1: void = scheduleRefresh(1000);
      return scheduleRefreshResult1;
    });
    subscriptions.set(argumentValue, subscribeFlowRunEventsResult);
  }
}
async function refreshRunData(showLoading = true): Promise<void> {
  if (showLoading) {
    isLoading.value = true;
  }
  try {
    const evaluatedValue6: [
      Promise<FlowRunOverviewDto>,
      Promise<FlowRunDto[]>
    ] = [getFlowRunOverview(), listFlowRuns({ take: 100 })];
    const bindingSource = await Promise.all(evaluatedValue6);
    const nextOverview = bindingSource[0];
    const nextRuns = bindingSource[1];
    overview.value = nextOverview;
    runs.value = nextRuns;
    syncActiveSubscriptions(nextOverview.activeRuns);
    loadErrorKey.value = '';
  }
  catch {
    loadErrorKey.value = 'runs.loadFailed';
  }
  finally {
    isLoading.value = false;
  }
}
async function refreshDirectoryData(): Promise<void> {
  const evaluatedValue7: [
    Promise<ProjectWorkspaceDto[]>,
    Promise<FlowInterfaceDto[]>,
    Promise<RunExecutionSettingsDto>,
    Promise<LibraryDto[]>,
    Promise<LibraryFamilyDto[]>,
    Promise<LibraryArtifactUsageDto[]>
  ] = [
    listProjects(true),
    listFlowInterfaces(),
    getRunExecutionSettings(),
    listEnvironmentLibraries(),
    listLibraryFamilies(),
    listLibraryArtifactUsages(),
  ];
  const bindingSource1 = await Promise.all(evaluatedValue7);
  const nextWorkspaces = bindingSource1[0];
  const nextInterfaces = bindingSource1[1];
  const nextSettings = bindingSource1[2];
  const nextLibraries = bindingSource1[3];
  const nextFamilies = bindingSource1[4];
  const nextUsages = bindingSource1[5];
  workspaces.value = nextWorkspaces;
  interfaces.value = nextInterfaces;
  Object.assign(settingsForm, {
    ...nextSettings,
    maxLibraryUploadBytes: nextSettings.maxLibraryUploadBytes ?? 100 * 1024 * 1024,
  });
  environmentLibraries.value = nextLibraries;
  libraryFamilies.value = nextFamilies;
  libraryArtifactUsages.value = nextUsages;
  ensureInterfaceFormTarget();
  lastDirectoryRefreshAt = Date.now();
}
async function refreshAll(showLoading = true): Promise<void> {
  if (showLoading) {
    isLoading.value = true;
  }
  try {
    const evaluatedValue8: [
      Promise<void>,
      Promise<void>
    ] = [refreshRunData(false), refreshDirectoryData()];
    await Promise.all(evaluatedValue8);
  }
  catch {
    loadErrorKey.value = 'runs.loadFailed';
  }
  finally {
    isLoading.value = false;
  }
}
function ensureInterfaceFormTarget(): void {
  if (!activeWorkspaces.value.some((workspace) => workspace.project.id === interfaceForm.projectId)) {
    interfaceForm.projectId = availableInterfaceWorkspaces.value[0]?.project.id ?? '';
  }
  if (!availableInterfaceFlows.value.some((flow) => flow.id === interfaceForm.flowId)) {
    interfaceForm.flowId = availableInterfaceFlows.value[0]?.id ?? '';
  }
}
function selectInterfaceProject(): void {
  interfaceForm.flowId = availableInterfaceFlows.value[0]?.id ?? '';
}
function projectNameFor(run: FlowRunDto): string {
  const evaluatedValue9: string = workspaces.value.find((workspace) => workspace.project.id === run.projectId)?.project.name ?? t('runs.unknownProject');
  return evaluatedValue9;
}
function shortId(id: string): string {
  const evaluatedValue10: string = id.length <= 8 ? id : id.slice(0, 8);
  return evaluatedValue10;
}
function flowNameFor(run: FlowRunDto): string {
  const shortIdResult: string = shortId(run.flowId);
  return shortIdResult;
}
function formatDate(value?: string): string {
  if (!value) {
    return '—';
  }
  const date = new Date(value);
  const getTimeResult: number = date.getTime();
  if (Number.isNaN(getTimeResult)) {
    return value;
  }
  const dateTimeFormatInstance = new Intl.DateTimeFormat(locale.value, { dateStyle: 'medium', timeStyle: 'short' });
  const formatResult: string = dateTimeFormatInstance.format(date);
  return formatResult;
}
function queueStatusKey(run: FlowRunDto): string {
  if (run.status === 'pending') {
    return 'console.status.queued';
  }
  if (run.status === 'running') {
    return 'console.status.running';
  }
  if (run.status === 'interrupted') {
    return 'console.status.interrupted';
  }
  return run.status === 'succeeded' ? 'console.status.completed' : 'console.status.failed';
}
function queueStatusClass(run: FlowRunDto): string {
  if (run.status === 'pending') {
    return 'pending';
  }
  if (run.status === 'running') {
    return 'running';
  }
  if (run.status === 'interrupted') {
    return 'interrupted';
  }
  return run.status === 'succeeded' ? 'succeeded' : 'failed';
}
function runKindKey(run: FlowRunDto): string {
  if (run.executionKind === 'debug') {
    return 'runs.kind.debug';
  }
  return run.isListenerRun ? 'runs.kind.listener' : 'runs.kind.standard';
}
function isCancellable(run: FlowRunDto): boolean {
  return run.status === 'pending' || run.status === 'running';
}
function isInterruptible(run: FlowRunDto): boolean {
  return run.status === 'running';
}
function isWorkpieceLive(run: FlowRunDto): boolean {
  return run.status === 'pending' || run.status === 'running';
}
async function cancelRun(run: FlowRunDto): Promise<void> {
  if (!isCancellable(run) || cancellingRunIds.value.has(run.id)) {
    return;
  }
  const setInstance = new Set(cancellingRunIds.value);
  cancellingRunIds.value = setInstance.add(run.id);
  try {
    await cancelFlowRun(run.id);
    await refreshRunData(false);
  }
  catch {
    loadErrorKey.value = 'runs.cancelFailed';
  }
  finally {
    const next = new Set(cancellingRunIds.value);
    next.delete(run.id);
    cancellingRunIds.value = next;
  }
}
async function interruptRun(run: FlowRunDto): Promise<void> {
  if (!isInterruptible(run) || interruptingRunIds.value.has(run.id)) {
    return;
  }
  const resolveConfirmResult = () => {
    const tResult: string | undefined = t('runs.interruptConfirm');
    const confirmResult = window.confirm(tResult);
    return confirmResult;
  };
  if (!resolveConfirmResult()) {
    return;
  }
  const setInstance1 = new Set(interruptingRunIds.value);
  interruptingRunIds.value = setInstance1.add(run.id);
  try {
    await interruptFlowRun(run.id);
    await refreshRunData(false);
  }
  catch {
    loadErrorKey.value = 'runs.interruptFailed';
  }
  finally {
    const next = new Set(interruptingRunIds.value);
    next.delete(run.id);
    interruptingRunIds.value = next;
  }
}
async function openSnapshot(run: FlowRunDto): Promise<void> {
  snapshotRun.value = run;
  snapshot.value = undefined;
  snapshotOutputs.value = [];
  snapshotOutputsError.value = '';
  snapshotEvents.value = [];
  snapshotEventsError.value = '';
  snapshotError.value = '';
  isSnapshotLoading.value = true;
  try {
    const definition = await getFlowRunSnapshot(run.id);
    snapshot.value = definition;
    const evaluatedValue11: [
      Promise<FlowRunOutputDto[]>,
      Promise<FlowRunEventDto[]>
    ] = [
      listFlowRunOutputs(run.id),
      listFlowRunEvents(run.id),
    ];
    const bindingSource2 = await Promise.allSettled(evaluatedValue11);
    const outputsResult = bindingSource2[0];
    const eventsResult = bindingSource2[1];
    if (outputsResult.status === 'fulfilled') {
      snapshotOutputs.value = outputsResult.value;
    }
    else {
      snapshotOutputsError.value = t('console.snapshotOutputsError');
    }
    if (eventsResult.status === 'fulfilled') {
      snapshotEvents.value = eventsResult.value;
    }
    else {
      snapshotEventsError.value = t('console.snapshotEventsError');
    }
  }
  catch {
    snapshotError.value = t('console.snapshotEmpty');
  }
  finally {
    isSnapshotLoading.value = false;
  }
}
function closeSnapshot(): void {
  snapshotRun.value = undefined;
  snapshot.value = undefined;
  snapshotOutputs.value = [];
  snapshotOutputsError.value = '';
  snapshotEvents.value = [];
  snapshotEventsError.value = '';
  snapshotError.value = '';
}
function openWorkpieces(run: FlowRunDto): void {
  workpieceRun.value = run;
}
function closeWorkpieces(): void {
  workpieceRun.value = undefined;
}
function openFlow(workspace: ProjectWorkspaceDto, flowId?: string): void {
  if (isArchivedProjectStatus(workspace.project.status)) {
    return;
  }
  emit('open-flow', workspace, flowId);
}
async function saveSettings(): Promise<void> {
  isSettingsSaving.value = true;
  noticeKey.value = '';
  try {
    const argumentValue1: {
      queueCapacity: number;
      maxConcurrentRuns: number;
      maxConcurrentListenerRuns: number;
      maxConcurrentRunsPerProject: number;
      queueWaitTimeoutSeconds: number;
      shutdownGracePeriodSeconds: number;
      synchronousInvocationTimeoutSeconds: number;
      maxLibraryUploadBytes: number;
    } = settingsForm;
    const saveRunExecutionSettingsResult: RunExecutionSettingsDto = await saveRunExecutionSettings({ ...settingsForm });
    Object.assign(argumentValue1, saveRunExecutionSettingsResult);
    noticeKey.value = 'console.settingsSaved';
    await refreshRunData(false);
  }
  catch {
    loadErrorKey.value = 'console.settingsSaveFailed';
  }
  finally {
    isSettingsSaving.value = false;
  }
}
function resetInterfaceForm(): void {
  editingInterfaceId.value = undefined;
  interfaceForm.name = '';
  interfaceForm.invocationMode = 'asynchronous';
  interfaceForm.isEnabled = true;
  interfaceForm.projectId = activeWorkspaces.value.find((workspace) => workspace.flows.length > 0)?.project.id ?? '';
  interfaceForm.flowId = availableInterfaceFlows.value[0]?.id ?? '';
}
function editInterface(flowInterface: FlowInterfaceDto): void {
  editingInterfaceId.value = flowInterface.id;
  interfaceForm.name = flowInterface.name;
  interfaceForm.projectId = flowInterface.projectId;
  interfaceForm.flowId = flowInterface.flowId;
  interfaceForm.invocationMode = flowInterface.invocationMode;
  interfaceForm.isEnabled = flowInterface.isEnabled;
}
async function saveInterface(): Promise<void> {
  if (!interfaceForm.projectId || !interfaceForm.flowId) {
    loadErrorKey.value = 'console.noProjectFlow';
    return;
  }
  isInterfaceSaving.value = true;
  noticeKey.value = '';
  try {
    if (editingInterfaceId.value) {
      await updateFlowInterface(editingInterfaceId.value, { name: interfaceForm.name, invocationMode: interfaceForm.invocationMode, isEnabled: interfaceForm.isEnabled });
      noticeKey.value = 'console.interfaceUpdated';
    }
    else {
      await createFlowInterface({ ...interfaceForm });
      noticeKey.value = 'console.interfaceCreated';
    }
    interfaces.value = await listFlowInterfaces();
    resetInterfaceForm();
  }
  catch {
    loadErrorKey.value = 'console.interfaceActionFailed';
  }
  finally {
    isInterfaceSaving.value = false;
  }
}
async function removeInterface(flowInterface: FlowInterfaceDto): Promise<void> {
  const resolveConfirmResult1 = () => {
    const tResult1: string | undefined = t('console.interfaceDeleteConfirm', { name: flowInterface.name });
    const confirmResult1 = window.confirm(tResult1);
    return confirmResult1;
  };
  if (!resolveConfirmResult1()) {
    return;
  }
  try {
    await deleteFlowInterface(flowInterface.id);
    interfaces.value = await listFlowInterfaces();
    if (editingInterfaceId.value === flowInterface.id) {
      resetInterfaceForm();
    }
    noticeKey.value = 'console.interfaceDeleted';
  }
  catch {
    loadErrorKey.value = 'console.interfaceActionFailed';
  }
}
function interfaceUrl(flowInterface: FlowInterfaceDto): string {
  const evaluatedValue12: string = `${window.location.origin.replace(/\/$/, '')}/api/public/flows/${flowInterface.id}/invoke`;
  return evaluatedValue12;
}
async function copyInterfaceUrl(flowInterface: FlowInterfaceDto): Promise<void> {
  try {
    const interfaceUrlResult: string = interfaceUrl(flowInterface);
    await navigator.clipboard.writeText(interfaceUrlResult);
    noticeKey.value = 'console.copySuccess';
  }
  catch {
    loadErrorKey.value = 'console.interfaceActionFailed';
  }
}
async function archiveLibrary(library: LibraryDto): Promise<void> {
  if (library.lifecycle === 'archived' || isLibraryArchiving.value.has(library.id)) {
    return;
  }
  const resolveConfirmResult2 = () => {
    const tResult2: string | undefined = t('console.archiveLibraryConfirm', { name: library.name });
    const confirmResult2 = window.confirm(tResult2);
    return confirmResult2;
  };
  if (!resolveConfirmResult2()) {
    return;
  }
  const setInstance2 = new Set(isLibraryArchiving.value);
  isLibraryArchiving.value = setInstance2.add(library.id);
  try {
    await archiveEnvironmentLibrary(library.id);
    const evaluatedValue13: [
      Promise<LibraryDto[]>,
      Promise<LibraryFamilyDto[]>
    ] = [listEnvironmentLibraries(), listLibraryFamilies()];
    const bindingSource3 = await Promise.all(evaluatedValue13);
    const nextLibraries = bindingSource3[0];
    const nextFamilies = bindingSource3[1];
    environmentLibraries.value = nextLibraries;
    libraryFamilies.value = nextFamilies;
    noticeKey.value = 'console.libraryArchived';
  }
  catch {
    loadErrorKey.value = 'console.libraryActionFailed';
  }
  finally {
    const next = new Set(isLibraryArchiving.value);
    next.delete(library.id);
    isLibraryArchiving.value = next;
  }
}
async function archiveProject(workspace: ProjectWorkspaceDto): Promise<void> {
  if (isArchivedProjectStatus(workspace.project.status) || isProjectArchiving.value.has(workspace.project.id)) {
    return;
  }
  const resolveConfirmResult3 = () => {
    const tResult3: string | undefined = t('console.archiveProjectConfirm', { name: workspace.project.name });
    const confirmResult3 = window.confirm(tResult3);
    return confirmResult3;
  };
  if (!resolveConfirmResult3()) {
    return;
  }
  const setInstance3 = new Set(isProjectArchiving.value);
  isProjectArchiving.value = setInstance3.add(workspace.project.id);
  loadErrorKey.value = '';
  try {
    const archived = await archiveProjectRequest(workspace.project.id);
    workspaces.value = workspaces.value.map((item) => item.project.id === archived.id
      ? { ...item, project: archived }
      : item);
    emit('project-directory-changed', activeWorkspaces.value);
    noticeKey.value = 'console.projectArchived';
  }
  catch (reason) {
    loadErrorKey.value = reason instanceof FlowApiError && reason.status === 409
      ? 'console.projectArchiveBlocked'
      : 'console.projectArchiveFailed';
  }
  finally {
    const next = new Set(isProjectArchiving.value);
    next.delete(workspace.project.id);
    isProjectArchiving.value = next;
  }
}
async function reindexLibrary(library: LibraryDto): Promise<void> {
  if (isLibraryReindexing.value.has(library.id)) {
    return;
  }
  const setInstance4 = new Set(isLibraryReindexing.value);
  isLibraryReindexing.value = setInstance4.add(library.id);
  try {
    const refreshed = await reindexEnvironmentLibrary(library.id);
    environmentLibraries.value = environmentLibraries.value.map((item) => item.id === library.id ? refreshed : item);
    noticeKey.value = 'console.libraryReindexed';
  }
  catch {
    loadErrorKey.value = 'console.libraryReindexFailed';
  }
  finally {
    const next = new Set(isLibraryReindexing.value);
    next.delete(library.id);
    isLibraryReindexing.value = next;
  }
}
async function handleLibraryUploaded(): Promise<void> {
  libraryUploadOpen.value = false;
  try {
    const evaluatedValue14: [
      Promise<LibraryDto[]>,
      Promise<LibraryFamilyDto[]>,
      Promise<LibraryArtifactUsageDto[]>
    ] = [
      listEnvironmentLibraries(),
      listLibraryFamilies(),
      listLibraryArtifactUsages(),
    ];
    const bindingSource4 = await Promise.all(evaluatedValue14);
    const nextLibraries = bindingSource4[0];
    const nextFamilies = bindingSource4[1];
    const nextUsages = bindingSource4[2];
    environmentLibraries.value = nextLibraries;
    libraryFamilies.value = nextFamilies;
    libraryArtifactUsages.value = nextUsages;
    noticeKey.value = 'console.libraryUploadSuccess';
  }
  catch {
    loadErrorKey.value = 'console.libraryActionFailed';
  }
}
async function handleLibraryFamilyAssigned(): Promise<void> {
  try {
    const evaluatedValue15: [
      Promise<LibraryDto[]>,
      Promise<LibraryFamilyDto[]>
    ] = [listEnvironmentLibraries(), listLibraryFamilies()];
    const bindingSource5 = await Promise.all(evaluatedValue15);
    const nextLibraries = bindingSource5[0];
    const nextFamilies = bindingSource5[1];
    environmentLibraries.value = nextLibraries;
    libraryFamilies.value = nextFamilies;
    noticeKey.value = 'libraryFamily.assigned';
  }
  catch {
    loadErrorKey.value = 'console.libraryActionFailed';
  }
}
function shortHash(value: string): string {
  const evaluatedValue16: string = value.length <= 12 ? value : value.slice(0, 12);
  return evaluatedValue16;
}
function interfaceModeKey(mode: FlowInvocationMode): string {
  return mode === 'synchronous' ? 'console.interfaceSync' : 'console.interfaceAsync';
}
function interfaceEnabledKey(flowInterface: FlowInterfaceDto): string {
  return flowInterface.isEnabled ? 'console.enabled' : 'console.disabled';
}
function selectView(view: ConsoleView): void {
  if (activeView.value === 'settings' && view !== 'settings' && mcpKeySettings.value?.confirmLeave() === false) {
    return;
  }
  activeView.value = view;
  noticeKey.value = '';
  if (Date.now() - lastDirectoryRefreshAt > 15000) {
    void refreshDirectoryData();
  }
}
defineExpose({ confirmLeave: () => {
    const evaluatedValue17 = mcpKeySettings.value?.confirmLeave() ?? true;
    return evaluatedValue17;
  } });
onMounted(() => {
  void refreshAll();
  refreshTimer = window.setInterval(() => {
    const evaluatedValue18 = void refreshRunData(false);
    return evaluatedValue18;
  }, 2000);
});
onBeforeUnmount(() => {
  if (refreshTimer !== undefined) {
    window.clearInterval(refreshTimer);
  }
  if (deferredRefreshTimer !== undefined) {
    window.clearTimeout(deferredRefreshTimer);
  }
  for (const unsubscribe of subscriptions.values())
    unsubscribe();
  subscriptions.clear();
});
const readButtonClass = computed(() => ({ active: activeView.value === 'overview' }));
const readButtonClass2 = computed(() => ({ active: activeView.value === 'projects' }));
const readButtonClass3 = computed(() => ({ active: activeView.value === 'queue' }));
const readButtonClass4 = computed(() => ({ active: activeView.value === 'settings' }));
const readButtonClass5 = computed(() => ({ active: activeView.value === 'interfaces' }));
const readButtonClass6 = computed(() => ({ active: activeView.value === 'libraries' }));
const readButtonClass7 = computed(() => ({ active: activeView.value === 'archives' }));
const readTemplateText = computed(() => (overview.value?.activeRunCount ?? 0));
const readTemplateText2 = computed(() => (overview.value?.maxConcurrentRuns ?? '—'));
const readRefreshCwClass = computed(() => ({ 'is-spinning': isLoading.value }));
const readTemplateIf = computed(() => (activeView.value === 'overview'));
const readTemplateText3 = computed(() => (overview.value?.queuedCount ?? 0));
const readTemplateText4 = computed(() => (overview.value?.queueCapacity ?? '—'));
const readTemplateText5 = computed(() => (overview.value?.activeRunCount ?? 0));
const readTemplateText6 = computed(() => (overview.value?.maxConcurrentRuns ?? '—'));
const readTemplateText7 = computed(() => (overview.value?.activeListenerRunCount ?? 0));
const readTemplateText8 = computed(() => (overview.value?.maxConcurrentListenerRuns ?? '—'));
const readTemplateText9 = computed(() => (overview.value?.maxConcurrentRunsPerProject ?? '—'));
const readTemplateText10 = computed(() => (overview.value?.activeRuns.length ?? 0));
function readTrItems() {
  return (overview.value!.activeRuns);
}
function readTemplateText11(run: TemplateItem<ReturnType<typeof readTrItems>>) {
  const runKindKeyResult: string = runKindKey(run);
  const evaluatedValue19 = (t(runKindKeyResult));
  return evaluatedValue19;
}
function readTemplateText12(run: TemplateItem<ReturnType<typeof readTrItems>>) {
  const evaluatedValue20 = (formatDate(run.startedAt ?? run.createdAt));
  return evaluatedValue20;
}
function readTrItems2() {
  return (latestRuns.value);
}
function readTemplateText13(run: TemplateItem<ReturnType<typeof readTrItems2>>) {
  const runKindKeyResult1: string = runKindKey(run);
  const evaluatedValue21 = (t(runKindKeyResult1));
  return evaluatedValue21;
}
function readSpanClass(run: TemplateItem<ReturnType<typeof readTrItems2>>) {
  const evaluatedValue22 = (['run-status', `run-status--${queueStatusClass(run)}`]);
  return evaluatedValue22;
}
function readTemplateText14(run: TemplateItem<ReturnType<typeof readTrItems2>>) {
  const queueStatusKeyResult: string = queueStatusKey(run);
  const evaluatedValue23 = (t(queueStatusKeyResult));
  return evaluatedValue23;
}
const readTemplateElseIf = computed(() => (activeView.value === 'projects'));
function readArticleItems() {
  return (activeWorkspaces.value);
}
function readTemplateText15(workspace: TemplateItem<ReturnType<typeof readArticleItems>>) {
  const evaluatedValue24: {
    [x: string]: string | number;
  } | undefined = { date: formatDate(workspace.project.createdAt) };
  const evaluatedValue25 = (t('console.projectCreatedAt', evaluatedValue24));
  return evaluatedValue25;
}
function readTemplateText16(workspace: TemplateItem<ReturnType<typeof readArticleItems>>) {
  const evaluatedValue26 = (workspace.flows.reduce((total, flow) => total + (flow.canvasCount ?? 0), 0));
  return evaluatedValue26;
}
function readTemplateText17(workspace: TemplateItem<ReturnType<typeof readArticleItems>>) {
  const evaluatedValue27 = (workspace.flows.reduce((total, flow) => total + (flow.nodeCount ?? 0), 0));
  return evaluatedValue27;
}
function readButtonDisabled(workspace: TemplateItem<ReturnType<typeof readArticleItems>>) {
  return (workspace.flows.length === 0);
}
const readPIf = computed(() => (activeWorkspaces.value.length === 0));
const readTemplateElseIf2 = computed(() => (activeView.value === 'queue'));
function readTrItems3() {
  return (runs.value);
}
function readTemplateText18(run: TemplateItem<ReturnType<typeof readTrItems3>>) {
  const runKindKeyResult2: string = runKindKey(run);
  const evaluatedValue28 = (t(runKindKeyResult2));
  return evaluatedValue28;
}
function readSpanClass2(run: TemplateItem<ReturnType<typeof readTrItems3>>) {
  const evaluatedValue29 = (['run-status', `run-status--${queueStatusClass(run)}`]);
  return evaluatedValue29;
}
function readTemplateText19(run: TemplateItem<ReturnType<typeof readTrItems3>>) {
  const queueStatusKeyResult1: string = queueStatusKey(run);
  const evaluatedValue30 = (t(queueStatusKeyResult1));
  return evaluatedValue30;
}
function readTemplateText20(run: TemplateItem<ReturnType<typeof readTrItems3>>) {
  const evaluatedValue31 = (t(`runs.policy.${run.concurrencyMode ?? 'parallel'}`));
  return evaluatedValue31;
}
const readTemplateElseIf3 = computed(() => (activeView.value === 'settings'));
const readTemplateElseIf4 = computed(() => (activeView.value === 'libraries'));
function handleButtonClick() {
  libraryUploadOpen.value = true;
}
function readDetailsItems() {
  return (activeLibraryFamilyGroups.value);
}
function readTemplateText21(group: TemplateItem<ReturnType<typeof readDetailsItems>>) {
  const evaluatedValue32 = (group.latestArtifactId ? (group.artifacts.find((artifact) => artifact.id === group.latestArtifactId)?.semanticVersion ?? group.artifacts.find((artifact) => artifact.id === group.latestArtifactId)?.version ?? '—') : '—');
  return evaluatedValue32;
}
function readTrItems4(group: TemplateItem<ReturnType<typeof readDetailsItems>>) {
  return (group.artifacts);
}
function readSpanClass3(group: TemplateItem<ReturnType<typeof readDetailsItems>>, library: TemplateItem<ReturnType<typeof readTrItems4>>) {
  return ({ 'library-version--recommended': library.id === group.latestArtifactId });
}
function readTemplateText22(library: TemplateItem<ReturnType<typeof readTrItems4>>) {
  return (library.semanticVersion ?? library.version);
}
function readTemplateText23(library: TemplateItem<ReturnType<typeof readTrItems4>>) {
  const evaluatedValue33 = (libraryUsageByArtifactId.value.get(library.id)?.currentFlowCount ?? 0);
  return evaluatedValue33;
}
function readTemplateText24(library: TemplateItem<ReturnType<typeof readTrItems4>>) {
  const evaluatedValue34 = (libraryUsageByArtifactId.value.get(library.id)?.flowVersionCount ?? 0);
  return evaluatedValue34;
}
function readTemplateText25(library: TemplateItem<ReturnType<typeof readTrItems4>>) {
  const evaluatedValue35 = (libraryUsageByArtifactId.value.get(library.id)?.runSnapshotCount ?? 0);
  return evaluatedValue35;
}
function handleButtonClick2(library: TemplateItem<ReturnType<typeof readTrItems4>>) {
  libraryFamilyAssignment.value = library;
}
function readRefreshCwClass2(library: TemplateItem<ReturnType<typeof readTrItems4>>) {
  const evaluatedValue36 = ({ 'is-spinning': isLibraryReindexing.value.has(library.id) });
  return evaluatedValue36;
}
const readTemplateElseIf5 = computed(() => (activeView.value === 'archives'));
function readArticleItems2() {
  return (archivedWorkspaces.value);
}
function readTemplateText26(workspace: TemplateItem<ReturnType<typeof readArticleItems2>>) {
  const evaluatedValue37: {
    [x: string]: string | number;
  } | undefined = { date: formatDate(workspace.project.createdAt) };
  const evaluatedValue38 = (t('console.projectCreatedAt', evaluatedValue37));
  return evaluatedValue38;
}
function readTemplateText27(workspace: TemplateItem<ReturnType<typeof readArticleItems2>>) {
  const evaluatedValue39 = (workspace.flows.reduce((total, flow) => total + (flow.canvasCount ?? 0), 0));
  return evaluatedValue39;
}
function readTemplateText28(workspace: TemplateItem<ReturnType<typeof readArticleItems2>>) {
  const evaluatedValue40 = (workspace.flows.reduce((total, flow) => total + (flow.nodeCount ?? 0), 0));
  return evaluatedValue40;
}
const readPIf2 = computed(() => (archivedWorkspaces.value.length === 0));
function readTrItems5() {
  return (archivedEnvironmentLibraries.value);
}
function readTemplateText29(library: TemplateItem<ReturnType<typeof readTrItems5>>) {
  const evaluatedValue41 = (library.familyName ?? (library.familyId ? t('library.familyBound') : t('libraryFamily.unassigned')));
  return evaluatedValue41;
}
function readTemplateText30(library: TemplateItem<ReturnType<typeof readTrItems5>>) {
  return (library.semanticVersion ?? library.version);
}
function readTemplateText31(library: TemplateItem<ReturnType<typeof readTrItems5>>) {
  const evaluatedValue42 = (libraryUsageByArtifactId.value.get(library.id)?.currentFlowCount ?? 0);
  return evaluatedValue42;
}
function readTemplateText32(library: TemplateItem<ReturnType<typeof readTrItems5>>) {
  const evaluatedValue43 = (libraryUsageByArtifactId.value.get(library.id)?.flowVersionCount ?? 0);
  return evaluatedValue43;
}
function readTemplateText33(library: TemplateItem<ReturnType<typeof readTrItems5>>) {
  const evaluatedValue44 = (libraryUsageByArtifactId.value.get(library.id)?.runSnapshotCount ?? 0);
  return evaluatedValue44;
}
const readTemplateText34 = computed(() => {
  const evaluatedValue45: string = (editingInterfaceId.value ? t('console.updateInterface') : t('console.createInterface'));
  return evaluatedValue45;
});
function readOptionItems() {
  return (availableInterfaceWorkspaces.value);
}
function readOptionItems2() {
  return (availableInterfaceFlows.value);
}
const readButtonDisabled2 = computed(() => (isInterfaceSaving.value || (!editingInterfaceId.value && !interfaceForm.flowId)));
const readTemplateText35 = computed(() => {
  const evaluatedValue46: string = (editingInterfaceId.value ? t('console.updateInterface') : t('console.createInterface'));
  return evaluatedValue46;
});
function readTrItems6() {
  return (interfaces.value);
}
function readTemplateText36(flowInterface: TemplateItem<ReturnType<typeof readTrItems6>>) {
  const interfaceModeKeyResult: string = interfaceModeKey(flowInterface.invocationMode);
  const evaluatedValue47 = (t(interfaceModeKeyResult));
  return evaluatedValue47;
}
function readTemplateText37(flowInterface: TemplateItem<ReturnType<typeof readTrItems6>>) {
  return (flowInterface.productionVersion ?? '—');
}
function readSpanClass4(flowInterface: TemplateItem<ReturnType<typeof readTrItems6>>) {
  return (['interface-state', { 'interface-state--disabled': !flowInterface.isEnabled }]);
}
function readTemplateText38(flowInterface: TemplateItem<ReturnType<typeof readTrItems6>>) {
  const interfaceEnabledKeyResult: string = interfaceEnabledKey(flowInterface);
  const evaluatedValue48 = (t(interfaceEnabledKeyResult));
  return evaluatedValue48;
}
function handleLibraryUploadDialogClose() {
  libraryUploadOpen.value = false;
}
function handleLibraryFamilyDialogClose() {
  libraryFamilyAssignment.value = undefined;
}
const readRunSnapshotViewerIsDebugRun = computed(() => (snapshotRun.value!.executionKind === 'debug'));
const readRunSnapshotViewerLive = computed(() => (snapshotRun.value!.status === 'running'));
</script>

<template>
  <main
    class="operations-console"
    aria-label="SereinFlow operations console"
  >
    <aside class="operations-console__sidebar">
      <div class="operations-console__identity">
        <span class="operations-console__eyebrow">SEREINFLOW</span>
        <strong>{{ t('console.consoleLabel') }}</strong>
      </div>
      <nav
        class="operations-console__nav"
        :aria-label="t('runs.title')"
      >
        <button
          type="button"
          :class="readButtonClass"
          @click="selectView('overview')"
        >
          <LayoutDashboard :size="17" />
          <span>{{ t('console.overview') }}</span>
        </button>
        <button
          type="button"
          :class="readButtonClass2"
          @click="selectView('projects')"
        >
          <FolderKanban :size="17" />
          <span>{{ t('console.projects') }}</span>
        </button>
        <button
          type="button"
          :class="readButtonClass3"
          @click="selectView('queue')"
        >
          <ListOrdered :size="17" />
          <span>{{ t('console.queue') }}</span>
        </button>
        <span
          class="operations-console__nav-divider"
          aria-hidden="true"
        ></span>
        <button
          type="button"
          :class="readButtonClass4"
          @click="selectView('settings')"
        >
          <SlidersHorizontal :size="17" />
          <span>{{ t('console.environmentSettings') }}</span>
        </button>
        <button
          type="button"
          :class="readButtonClass5"
          @click="selectView('interfaces')"
        >
          <RadioTower :size="17" />
          <span>{{ t('console.environmentInterfaces') }}</span>
        </button>
        <button
          type="button"
          :class="readButtonClass6"
          @click="selectView('libraries')"
        >
          <PackagePlus :size="17" />
          <span>{{ t('console.environmentLibraries') }}</span>
        </button>
        <button
          type="button"
          :class="readButtonClass7"
          @click="selectView('archives')"
        >
          <Archive :size="17" />
          <span>{{ t('console.archives') }}</span>
        </button>
      </nav>
      <div class="operations-console__sidebar-footer">
        <span>{{ t('runs.activeWorkers') }}</span>
        <strong>{{ readTemplateText }} / {{ readTemplateText2 }}</strong>
      </div>
    </aside>
    <section class="operations-console__content">
      <header class="operations-console__header">
        <div>
          <p class="operations-console__eyebrow">{{ t('console.consoleLabel') }}</p>
          <h1>{{ t(activeViewTitleKey) }}</h1>
        </div>
        <button
          class="icon-button"
          type="button"
          :title="t('runs.refresh')"
          :aria-label="t('runs.refresh')"
          :disabled="isLoading"
          @click="refreshAll()"
        >
          <RefreshCw
            :size="16"
            :class="readRefreshCwClass"
           />
        </button>
      </header>
      <p
        v-if="loadErrorKey"
        class="operations-console__notice operations-console__notice--error"
        role="alert"
      >{{ t(loadErrorKey) }}</p>
      <p
        v-else-if="noticeKey"
        class="operations-console__notice"
        role="status"
      >{{ t(noticeKey) }}</p>
      <template v-if="readTemplateIf">
        <p class="operations-console__intro">{{ t('console.quickPreviewHint') }}</p>
        <dl class="operations-console__metrics">
          <div>
            <dt>{{ t('runs.queueCapacity') }}</dt>
            <dd>
              {{ readTemplateText3 }}
              {{ ' ' }}
              <span>/ {{ readTemplateText4 }}</span>
            </dd>
          </div>
          <div>
            <dt>{{ t('runs.activeWorkers') }}</dt>
            <dd>
              {{ readTemplateText5 }}
              {{ ' ' }}
              <span>/ {{ readTemplateText6 }}</span>
            </dd>
          </div>
          <div>
            <dt>{{ t('runs.listenerWorkers') }}</dt>
            <dd>
              {{ readTemplateText7 }}
              {{ ' ' }}
              <span>/ {{ readTemplateText8 }}</span>
            </dd>
          </div>
          <div>
            <dt>{{ t('runs.projectLimit') }}</dt>
            <dd>{{ readTemplateText9 }}</dd>
          </div>
        </dl>
        <section class="operations-console__section">
          <div class="operations-console__section-heading">
            <h2>{{ t('runs.running') }}</h2>
            <span>{{ readTemplateText10 }}</span>
          </div>
          <div class="operations-table-wrap">
            <table class="operations-table operations-table--active">
              <thead>
                <tr>
                  <th>{{ t('runs.project') }}</th>
                  <th>{{ t('runs.flow') }}</th>
                  <th>{{ t('runs.kind') }}</th>
                  <th>{{ t('runs.startedAt') }}</th>
                  <th>{{ t('runs.actions') }}</th>
                </tr>
              </thead>
              <tbody v-if="overview?.activeRuns.length">
                <tr
                  v-for="run in readTrItems()"
                  :key="run.id"
                >
                  <td>{{ projectNameFor(run) }}</td>
                  <td>
                    <code>{{ flowNameFor(run) }}</code>
                  </td>
                  <td>{{ readTemplateText11(run) }}</td>
                  <td>{{ readTemplateText12(run) }}</td>
                  <td class="operations-table__actions">
                    <button
                      class="icon-button"
                      type="button"
                      :title="t('workpiece.open')"
                      :aria-label="t('workpiece.open')"
                      @click="openWorkpieces(run)"
                    >
                      <PackageOpen :size="16" />
                    </button>
                    <button
                      v-if="isCancellable(run)"
                      class="icon-button icon-button--danger"
                      type="button"
                      :title="t('runs.cancel')"
                      :aria-label="t('runs.cancel')"
                      :disabled="cancellingRunIds.has(run.id)"
                      @click="cancelRun(run)"
                    >
                      <Square
                        :size="14"
                        fill="currentColor"
                       />
                    </button>
                  </td>
                </tr>
              </tbody>
              <tbody v-else>
                <tr>
                  <td
                    class="operations-table__empty"
                    colspan="5"
                  >{{ t('runs.emptyActive') }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
        <section class="operations-console__section">
          <div class="operations-console__section-heading">
            <h2>{{ t('console.queue') }}</h2>
            <button
              class="text-button text-button--with-icon"
              type="button"
              @click="selectView('queue')"
            >
              <ListOrdered :size="14" />
              <span>{{ t('console.viewQueue') }}</span>
            </button>
          </div>
          <div class="operations-table-wrap">
            <table class="operations-table operations-table--overview">
              <thead>
                <tr>
                  <th>{{ t('runs.project') }}</th>
                  <th>{{ t('runs.flow') }}</th>
                  <th>{{ t('runs.kind') }}</th>
                  <th>{{ t('runs.status') }}</th>
                  <th>{{ t('runs.queuedAt') }}</th>
                  <th>{{ t('runs.actions') }}</th>
                </tr>
              </thead>
              <tbody v-if="latestRuns.length">
                <tr
                  v-for="run in readTrItems2()"
                  :key="run.id"
                >
                  <td>{{ projectNameFor(run) }}</td>
                  <td>
                    <code>{{ flowNameFor(run) }}</code>
                  </td>
                  <td>{{ readTemplateText13(run) }}</td>
                  <td>
                    <span :class="readSpanClass(run)">{{ readTemplateText14(run) }}</span>
                  </td>
                  <td>{{ formatDate(run.createdAt) }}</td>
                  <td class="operations-table__actions">
                    <button
                      class="icon-button"
                      type="button"
                      :title="t('console.snapshot')"
                      :aria-label="t('console.snapshot')"
                      @click="openSnapshot(run)"
                    >
                      <Eye :size="16" />
                    </button>
                    <button
                      class="icon-button"
                      type="button"
                      :title="t('workpiece.open')"
                      :aria-label="t('workpiece.open')"
                      @click="openWorkpieces(run)"
                    >
                      <PackageOpen :size="16" />
                    </button>
                    <button
                      v-if="isInterruptible(run)"
                      class="icon-button icon-button--warning"
                      type="button"
                      :title="t('runs.interrupt')"
                      :aria-label="t('runs.interrupt')"
                      :disabled="interruptingRunIds.has(run.id)"
                      @click="interruptRun(run)"
                    >
                      <CircleOff :size="15" />
                    </button>
                    <button
                      v-if="isCancellable(run)"
                      class="icon-button icon-button--danger"
                      type="button"
                      :title="t('runs.cancel')"
                      :aria-label="t('runs.cancel')"
                      :disabled="cancellingRunIds.has(run.id)"
                      @click="cancelRun(run)"
                    >
                      <Square
                        :size="14"
                        fill="currentColor"
                       />
                    </button>
                  </td>
                </tr>
              </tbody>
              <tbody v-else>
                <tr>
                  <td
                    class="operations-table__empty"
                    colspan="6"
                  >{{ t('runs.emptyRecent') }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </template>
      <template v-else-if="readTemplateElseIf">
        <p class="operations-console__intro">{{ t('console.projectListHint') }}</p>
        <section class="project-catalog">
          <article
            v-for="workspace in readArticleItems()"
            :key="workspace.project.id"
            class="project-catalog__item"
          >
            <div>
              <h2>{{ workspace.project.name }}</h2>
              <p>{{ readTemplateText15(workspace) }}</p>
            </div>
            <dl>
              <div>
                <dt>{{ t('project.flows') }}</dt>
                <dd>{{ workspace.flows.length }}</dd>
              </div>
              <div>
                <dt>{{ t('console.canvasCount') }}</dt>
                <dd>{{ readTemplateText16(workspace) }}</dd>
              </div>
              <div>
                <dt>{{ t('console.nodeCount') }}</dt>
                <dd>{{ readTemplateText17(workspace) }}</dd>
              </div>
            </dl>
            <div class="project-catalog__actions">
              <button
                class="command-button quiet"
                type="button"
                :disabled="readButtonDisabled(workspace)"
                @click="openFlow(workspace, workspace.flows[0]?.id)"
              >
                <FilePenLine :size="15" />
                <span>{{ t('console.openEditor') }}</span>
              </button>
              <button
                class="icon-button icon-button--danger"
                type="button"
                :title="t('console.archiveProject')"
                :aria-label="t('console.archiveProject')"
                :disabled="isProjectArchiving.has(workspace.project.id)"
                @click="archiveProject(workspace)"
              >
                <Archive :size="15" />
              </button>
            </div>
          </article>
          <p
            v-if="readPIf"
            class="operations-console__empty"
          >{{ t('runs.emptyProjects') }}</p>
        </section>
        <button
          class="command-button operations-console__create-project"
          type="button"
          @click="emit('start-new-project')"
        >
          <Plus :size="16" />
          <span>{{ t('project.newProject') }}</span>
        </button>
      </template>
      <template v-else-if="readTemplateElseIf2">
        <p class="operations-console__intro">{{ t('console.queueHint') }}</p>
        <div class="operations-table-wrap">
          <table class="operations-table operations-table--queue">
            <thead>
              <tr>
                <th>{{ t('runs.project') }}</th>
                <th>{{ t('runs.flow') }}</th>
                <th>{{ t('runs.kind') }}</th>
                <th>{{ t('runs.status') }}</th>
                <th>{{ t('runs.policy') }}</th>
                <th>{{ t('runs.queuedAt') }}</th>
                <th>{{ t('runs.actions') }}</th>
              </tr>
            </thead>
            <tbody v-if="runs.length">
              <tr
                v-for="run in readTrItems3()"
                :key="run.id"
              >
                <td>{{ projectNameFor(run) }}</td>
                <td>
                  <code>{{ flowNameFor(run) }}</code>
                </td>
                <td>{{ readTemplateText18(run) }}</td>
                <td>
                  <span :class="readSpanClass2(run)">{{ readTemplateText19(run) }}</span>
                </td>
                <td>{{ readTemplateText20(run) }}</td>
                <td>{{ formatDate(run.createdAt) }}</td>
                <td class="operations-table__actions">
                  <button
                    class="icon-button"
                    type="button"
                    :title="t('console.snapshot')"
                    :aria-label="t('console.snapshot')"
                    @click="openSnapshot(run)"
                  >
                    <FilePenLine :size="15" />
                  </button>
                  <button
                    class="icon-button"
                    type="button"
                    :title="t('workpiece.open')"
                    :aria-label="t('workpiece.open')"
                    @click="openWorkpieces(run)"
                  >
                    <PackageOpen :size="15" />
                  </button>
                  <button
                    v-if="isInterruptible(run)"
                    class="icon-button icon-button--warning"
                    type="button"
                    :title="t('runs.interrupt')"
                    :aria-label="t('runs.interrupt')"
                    :disabled="interruptingRunIds.has(run.id)"
                    @click="interruptRun(run)"
                  >
                    <CircleOff :size="15" />
                  </button>
                  <button
                    v-if="isCancellable(run)"
                    class="icon-button icon-button--danger"
                    type="button"
                    :title="t('runs.cancel')"
                    :aria-label="t('runs.cancel')"
                    :disabled="cancellingRunIds.has(run.id)"
                    @click="cancelRun(run)"
                  >
                    <Square
                      :size="14"
                      fill="currentColor"
                     />
                  </button>
                </td>
              </tr>
            </tbody>
            <tbody v-else>
              <tr>
                <td
                  class="operations-table__empty"
                  colspan="7"
                >{{ t('runs.emptyRecent') }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </template>
      <template v-else-if="readTemplateElseIf3">
        <p class="operations-console__intro">{{ t('console.environmentSettingsHint') }}</p>
        <form
          class="environment-settings"
          @submit.prevent="saveSettings"
        >
          <label>
            <span>{{ t('runs.queueCapacity') }}</span>
            <input
              v-model.number="settingsForm.queueCapacity"
              type="number"
              min="1"
              max="10000"
              required
             />
          </label>
          <label>
            <span>{{ t('runs.activeWorkers') }}</span>
            <input
              v-model.number="settingsForm.maxConcurrentRuns"
              type="number"
              min="1"
              max="1024"
              required
             />
          </label>
          <label>
            <span>{{ t('runs.listenerWorkers') }}</span>
            <input
              v-model.number="settingsForm.maxConcurrentListenerRuns"
              type="number"
              min="0"
              max="1024"
              required
             />
          </label>
          <label>
            <span>{{ t('runs.projectLimit') }}</span>
            <input
              v-model.number="settingsForm.maxConcurrentRunsPerProject"
              type="number"
              min="1"
              max="1024"
              required
             />
          </label>
          <label>
            <span>{{ t('console.queueWaitTimeout') }}</span>
            <input
              v-model.number="settingsForm.queueWaitTimeoutSeconds"
              type="number"
              min="1"
              max="86400"
              required
             />
          </label>
          <label>
            <span>{{ t('console.shutdownGrace') }}</span>
            <input
              v-model.number="settingsForm.shutdownGracePeriodSeconds"
              type="number"
              min="1"
              max="300"
              required
             />
          </label>
          <label>
            <span>{{ t('console.syncTimeout') }}</span>
            <input
              v-model.number="settingsForm.synchronousInvocationTimeoutSeconds"
              type="number"
              min="1"
              max="300"
              required
             />
          </label>
          <label>
            <span>{{ t('console.maxLibraryUploadSize') }}</span>
            <input
              v-model.number="maxLibraryUploadMegabytes"
              type="number"
              min="1"
              max="512"
              step="1"
              required
             />
            <small>{{ t('console.maxLibraryUploadSizeHint') }}</small>
          </label>
          <div class="environment-settings__actions">
            <button
              class="command-button run"
              type="submit"
              :disabled="isSettingsSaving"
            >
              <Save :size="15" />
              <span>{{ t('console.saveSettings') }}</span>
            </button>
          </div>
        </form>
        <ApiKeySettings
          v-if="securityCapabilities?.apiKeyManagement"
          ref="mcpKeySettings"
          :project-workspaces="activeWorkspaces"
         />
      </template>
      <template v-else-if="readTemplateElseIf4">
        <p class="operations-console__intro">{{ t('console.environmentLibrariesHint') }}</p>
        <section class="operations-console__section environment-libraries">
          <div class="operations-console__section-heading">
            <div>
              <h2>{{ t('console.environmentLibrariesTitle') }}</h2>
              <p>{{ t('console.environmentLibrariesTableHint') }}</p>
            </div>
            <button
              class="command-button run"
              type="button"
              @click="handleButtonClick()"
            >
              <PackagePlus :size="15" />
              <span>{{ t('library.upload') }}</span>
            </button>
          </div>
          <div
            v-if="activeLibraryFamilyGroups.length"
            class="library-family-groups"
          >
            <details
              v-for="group in readDetailsItems()"
              :key="group.id"
              class="library-family-group"
              :open="group.isUnassigned"
            >
              <summary>
                <div class="library-family-group__title">
                  <FolderKanban :size="16" />
                  <strong>{{ group.name }}</strong>
                  <span
                    v-if="group.isUnassigned"
                    class="library-family-group__state"
                  >{{ t('libraryFamily.unassigned') }}</span>
                </div>
                <dl class="library-family-group__summary">
                  <div>
                    <dt>{{ t('console.libraryFamilyArtifacts') }}</dt>
                    <dd>{{ group.artifacts.length }}</dd>
                  </div>
                  <div>
                    <dt>{{ t('console.libraryFamilyRecommended') }}</dt>
                    <dd>{{ readTemplateText21(group) }}</dd>
                  </div>
                  <div>
                    <dt>{{ t('console.libraryFamilyLatestUpload') }}</dt>
                    <dd>{{ formatDate(group.lastUploadedAt) }}</dd>
                  </div>
                </dl>
              </summary>
              <div class="operations-table-wrap library-family-group__artifacts">
                <table class="operations-table operations-table--libraries">
                  <thead>
                    <tr>
                      <th>{{ t('console.libraryName') }}</th>
                      <th>{{ t('console.libraryVersion') }}</th>
                      <th>SHA</th>
                      <th>{{ t('console.libraryNodes') }}</th>
                      <th>{{ t('console.libraryCurrentFlows') }}</th>
                      <th>{{ t('console.libraryFlowVersions') }}</th>
                      <th>{{ t('console.libraryRunSnapshots') }}</th>
                      <th>{{ t('console.libraryUploadedAt') }}</th>
                      <th>{{ t('console.libraryStatus') }}</th>
                      <th>{{ t('runs.actions') }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr
                      v-for="library in readTrItems4(group)"
                      :key="library.id"
                    >
                      <td>
                        <strong>{{ library.name }}</strong>
                        <small>{{ library.fileName }}</small>
                      </td>
                      <td>
                        <span
                          class="library-version"
                          :class="readSpanClass3(group, library)"
                        >{{ readTemplateText22(library) }}</span>
                      </td>
                      <td>
                        <code>{{ shortHash(library.sha256) }}</code>
                      </td>
                      <td>{{ library.nodes.length }}</td>
                      <td>{{ readTemplateText23(library) }}</td>
                      <td>{{ readTemplateText24(library) }}</td>
                      <td>{{ readTemplateText25(library) }}</td>
                      <td>{{ formatDate(library.uploadedAt) }}</td>
                      <td>
                        <span class="library-state library-state--available">{{ t('console.libraryStatusAvailable') }}</span>
                      </td>
                      <td class="operations-table__actions">
                        <button
                          class="icon-button"
                          type="button"
                          :title="t('libraryFamily.assign')"
                          :aria-label="t('libraryFamily.assign')"
                          @click="handleButtonClick2(library)"
                        >
                          <FolderKanban :size="15" />
                        </button>
                        <button
                          class="icon-button"
                          type="button"
                          :title="t('console.reindexLibrary')"
                          :aria-label="t('console.reindexLibrary')"
                          :disabled="isLibraryReindexing.has(library.id)"
                          @click="reindexLibrary(library)"
                        >
                          <RefreshCw
                            :size="15"
                            :class="readRefreshCwClass2(library)"
                           />
                        </button>
                        <button
                          class="icon-button icon-button--danger"
                          type="button"
                          :title="t('console.archiveLibrary')"
                          :aria-label="t('console.archiveLibrary')"
                          :disabled="isLibraryArchiving.has(library.id)"
                          @click="archiveLibrary(library)"
                        >
                          <Archive :size="15" />
                        </button>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </details>
          </div>
          <p
            v-else
            class="operations-console__empty"
          >{{ t('console.libraryEmpty') }}</p>
        </section>
      </template>
      <template v-else-if="readTemplateElseIf5">
        <p class="operations-console__intro">{{ t('console.archivesHint') }}</p>
        <section class="operations-console__section">
          <div class="operations-console__section-heading">
            <h2>{{ t('console.archivedProjects') }}</h2>
            <span>{{ archivedWorkspaces.length }}</span>
          </div>
          <div class="project-catalog">
            <article
              v-for="workspace in readArticleItems2()"
              :key="workspace.project.id"
              class="project-catalog__item project-catalog__item--archived"
            >
              <div>
                <h2>{{ workspace.project.name }}</h2>
                <p>{{ readTemplateText26(workspace) }}</p>
              </div>
              <dl>
                <div>
                  <dt>{{ t('project.flows') }}</dt>
                  <dd>{{ workspace.flows.length }}</dd>
                </div>
                <div>
                  <dt>{{ t('console.canvasCount') }}</dt>
                  <dd>{{ readTemplateText27(workspace) }}</dd>
                </div>
                <div>
                  <dt>{{ t('console.nodeCount') }}</dt>
                  <dd>{{ readTemplateText28(workspace) }}</dd>
                </div>
              </dl>
              <span class="project-catalog__readonly">
                <Archive :size="14" />
                {{ t('console.projectArchivedReadonly') }}
              </span>
            </article>
            <p
              v-if="readPIf2"
              class="operations-console__empty"
            >{{ t('console.archivedProjectsEmpty') }}</p>
          </div>
        </section>
        <section class="operations-console__section">
          <div class="operations-console__section-heading">
            <h2>{{ t('console.archivedLibraries') }}</h2>
            <span>{{ archivedEnvironmentLibraries.length }}</span>
          </div>
          <div class="operations-table-wrap">
            <table class="operations-table operations-table--libraries">
              <thead>
                <tr>
                  <th>{{ t('console.libraryName') }}</th>
                  <th>{{ t('console.libraryFamily') }}</th>
                  <th>{{ t('console.libraryVersion') }}</th>
                  <th>SHA</th>
                  <th>{{ t('console.libraryNodes') }}</th>
                  <th>{{ t('console.libraryCurrentFlows') }}</th>
                  <th>{{ t('console.libraryFlowVersions') }}</th>
                  <th>{{ t('console.libraryRunSnapshots') }}</th>
                  <th>{{ t('console.libraryUploadedAt') }}</th>
                  <th>{{ t('console.libraryStatus') }}</th>
                </tr>
              </thead>
              <tbody v-if="archivedEnvironmentLibraries.length">
                <tr
                  v-for="library in readTrItems5()"
                  :key="library.id"
                >
                  <td>
                    <strong>{{ library.name }}</strong>
                    <small>{{ library.fileName }}</small>
                  </td>
                  <td>{{ readTemplateText29(library) }}</td>
                  <td>{{ readTemplateText30(library) }}</td>
                  <td>
                    <code>{{ shortHash(library.sha256) }}</code>
                  </td>
                  <td>{{ library.nodes.length }}</td>
                  <td>{{ readTemplateText31(library) }}</td>
                  <td>{{ readTemplateText32(library) }}</td>
                  <td>{{ readTemplateText33(library) }}</td>
                  <td>{{ formatDate(library.uploadedAt) }}</td>
                  <td>
                    <span class="library-state library-state--archived">
                      <Archive :size="13" />
                      {{ t('console.libraryStatusArchived') }}
                    </span>
                  </td>
                </tr>
              </tbody>
              <tbody v-else>
                <tr>
                  <td
                    class="operations-table__empty"
                    colspan="10"
                  >{{ t('console.archivedLibrariesEmpty') }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </template>
      <template v-else>
        <p class="operations-console__intro">{{ t('console.environmentInterfacesHint') }}</p>
        <section class="interface-layout">
          <form
            class="interface-form"
            @submit.prevent="saveInterface"
          >
            <h2>{{ readTemplateText34 }}</h2>
            <label>
              <span>{{ t('console.interfaceName') }}</span>
              <input
                v-model.trim="interfaceForm.name"
                type="text"
                maxlength="80"
                required
               />
            </label>
            <label>
              <span>{{ t('console.interfaceProject') }}</span>
              <select
                v-model="interfaceForm.projectId"
                :disabled="Boolean(editingInterfaceId)"
                required
                @change="selectInterfaceProject"
              >
                <option
                  value=""
                  disabled
                >{{ t('console.noProductionFlow') }}</option>
                <option
                  v-for="workspace in readOptionItems()"
                  :key="workspace.project.id"
                  :value="workspace.project.id"
                >{{ workspace.project.name }}</option>
              </select>
            </label>
            <label>
              <span>{{ t('console.interfaceFlow') }}</span>
              <select
                v-model="interfaceForm.flowId"
                :disabled="Boolean(editingInterfaceId)"
                required
              >
                <option
                  v-for="flow in readOptionItems2()"
                  :key="flow.id"
                  :value="flow.id"
                >{{ shortId(flow.id) }} · v{{ flow.productionVersion }}</option>
              </select>
            </label>
            <fieldset>
              <legend>{{ t('console.interfaceMode') }}</legend>
              <label class="interface-form__choice">
                <input
                  v-model="interfaceForm.invocationMode"
                  type="radio"
                  value="asynchronous"
                 />
                <span>
                  <strong>{{ t('console.interfaceAsync') }}</strong>
                  <small>{{ t('console.interfaceAsyncHint') }}</small>
                </span>
              </label>
              <label class="interface-form__choice">
                <input
                  v-model="interfaceForm.invocationMode"
                  type="radio"
                  value="synchronous"
                 />
                <span>
                  <strong>{{ t('console.interfaceSync') }}</strong>
                  <small>{{ t('console.interfaceSyncHint') }}</small>
                </span>
              </label>
            </fieldset>
            <label class="interface-form__toggle">
              <input
                v-model="interfaceForm.isEnabled"
                type="checkbox"
               />
              <span>{{ t('console.interfaceEnabled') }}</span>
            </label>
            <div class="interface-form__actions">
              <button
                class="command-button run"
                type="submit"
                :disabled="readButtonDisabled2"
              >
                <Save :size="15" />
                <span>{{ readTemplateText35 }}</span>
              </button>
              <button
                v-if="editingInterfaceId"
                class="command-button quiet"
                type="button"
                @click="resetInterfaceForm"
              >
                <X :size="15" />
                <span>{{ t('console.cancelEdit') }}</span>
              </button>
            </div>
          </form>
          <div class="interface-list">
            <div class="operations-console__section-heading">
              <h2>{{ t('console.environmentInterfaces') }}</h2>
              <span>{{ interfaces.length }}</span>
            </div>
            <div class="operations-table-wrap">
              <table class="operations-table operations-table--interfaces">
                <thead>
                  <tr>
                    <th>{{ t('console.interfaceName') }}</th>
                    <th>{{ t('console.interfaceMode') }}</th>
                    <th>{{ t('console.interfaceProductionVersion') }}</th>
                    <th>{{ t('console.interfaceEnabled') }}</th>
                    <th>{{ t('console.interfaceUrl') }}</th>
                    <th>{{ t('console.interfaceActions') }}</th>
                  </tr>
                </thead>
                <tbody v-if="interfaces.length">
                  <tr
                    v-for="flowInterface in readTrItems6()"
                    :key="flowInterface.id"
                  >
                    <td>{{ flowInterface.name }}</td>
                    <td>{{ readTemplateText36(flowInterface) }}</td>
                    <td>
                      <code>v{{ readTemplateText37(flowInterface) }}</code>
                    </td>
                    <td>
                      <span :class="readSpanClass4(flowInterface)">{{ readTemplateText38(flowInterface) }}</span>
                    </td>
                    <td>
                      <code class="interface-list__url">{{ interfaceUrl(flowInterface) }}</code>
                    </td>
                    <td class="operations-table__actions">
                      <button
                        class="icon-button"
                        type="button"
                        :title="t('console.copyUrl')"
                        :aria-label="t('console.copyUrl')"
                        @click="copyInterfaceUrl(flowInterface)"
                      >
                        <Copy :size="15" />
                      </button>
                      <button
                        class="icon-button"
                        type="button"
                        :title="t('console.updateInterface')"
                        :aria-label="t('console.updateInterface')"
                        @click="editInterface(flowInterface)"
                      >
                        <Settings2 :size="15" />
                      </button>
                      <button
                        class="icon-button icon-button--danger"
                        type="button"
                        :title="t('command.delete')"
                        :aria-label="t('command.delete')"
                        @click="removeInterface(flowInterface)"
                      >
                        <Trash2 :size="15" />
                      </button>
                    </td>
                  </tr>
                </tbody>
                <tbody v-else>
                  <tr>
                    <td
                      class="operations-table__empty"
                      colspan="6"
                    >{{ t('console.interfaceEmpty') }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </section>
      </template>
    </section>
    <LibraryUploadDialog
      v-if="libraryUploadOpen"
      :max-file-bytes="settingsForm.maxLibraryUploadBytes"
      @close="handleLibraryUploadDialogClose()"
      @uploaded="handleLibraryUploaded"
     />
    <LibraryFamilyDialog
      v-if="libraryFamilyAssignment"
      :library="libraryFamilyAssignment"
      @close="handleLibraryFamilyDialogClose()"
      @assigned="handleLibraryFamilyAssigned"
     />
    <div
      v-if="snapshotRun"
      class="snapshot-dialog-backdrop"
      role="presentation"
      @click.self="closeSnapshot"
    >
      <section
        class="snapshot-dialog"
        role="dialog"
        aria-modal="true"
        :aria-label="t('console.snapshotTitle')"
      >
        <header>
          <div class="snapshot-dialog__heading">
            <p class="operations-console__eyebrow">{{ shortId(snapshotRun.id) }}</p>
            <h2>{{ t('console.snapshotTitle') }}</h2>
          </div>
          <button
            class="icon-button"
            type="button"
            :title="t('command.close')"
            :aria-label="t('command.close')"
            @click="closeSnapshot"
          >
            <X :size="16" />
          </button>
        </header>
        <div
          v-if="isSnapshotLoading"
          class="snapshot-dialog__status"
        >{{ t('console.snapshotLoading') }}</div>
        <div
          v-else-if="snapshotError"
          class="snapshot-dialog__status snapshot-dialog__status--error"
        >{{ snapshotError }}</div>
        <RunSnapshotViewer
          v-else
          :run-id="snapshotRun.id"
          :definition="snapshot"
          :outputs="snapshotOutputs"
          :outputs-error="snapshotOutputsError"
          :events="snapshotEvents"
          :events-error="snapshotEventsError"
          :is-debug-run="readRunSnapshotViewerIsDebugRun"
          :live="readRunSnapshotViewerLive"
         />
      </section>
    </div>
    <div
      v-if="selectedWorkpieceRun"
      class="workpiece-dialog-backdrop"
      role="presentation"
      @click.self="closeWorkpieces"
    >
      <section
        class="workpiece-dialog"
        role="dialog"
        aria-modal="true"
        :aria-label="t('workpiece.title')"
      >
        <header>
          <div>
            <p class="operations-console__eyebrow">{{ shortId(selectedWorkpieceRun.id) }}</p>
            <h2>{{ t('workpiece.title') }}</h2>
            <span>{{ projectNameFor(selectedWorkpieceRun) }} · {{ flowNameFor(selectedWorkpieceRun) }}</span>
          </div>
          <button
            class="icon-button"
            type="button"
            :title="t('command.close')"
            :aria-label="t('command.close')"
            @click="closeWorkpieces"
          >
            <X :size="16" />
          </button>
        </header>
        <RunWorkpiecePanel
          :run-id="selectedWorkpieceRun.id"
          :live="isWorkpieceLive(selectedWorkpieceRun)"
         />
      </section>
    </div>
  </main>
</template>
