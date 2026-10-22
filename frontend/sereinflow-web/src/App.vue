<script setup lang="ts">
import { unref } from 'vue';
type TemplateItem<T> = T extends readonly (infer Item)[] ? Item : T extends Record<string, infer Value> ? Value : T extends number ? number : never;
import { computed, markRaw, nextTick, onBeforeUnmount, onMounted, provide, reactive, ref, watch } from 'vue';
import { Activity, AlertTriangle, Bug, Code2, Database, LayoutGrid, PackageOpen, PanelLeft, PanelRight, Terminal, Zap, } from 'lucide-vue-next';
import { useVueFlow } from '@vue-flow/core';
import '@vue-flow/core/dist/style.css';
import '@vue-flow/core/dist/theme-default.css';
import NodeLibraryPanel from './components/library/NodeLibraryPanel.vue';
import ProjectLibraryDialog from './components/library/ProjectLibraryDialog.vue';
import CommandBar from './components/workspace/CommandBar.vue';
import DockablePanelGroup from './components/workspace/DockablePanelGroup.vue';
import FlowVersionHistoryDialog from './components/workspace/FlowVersionHistoryDialog.vue';
import OutputPanel from './components/workspace/OutputPanel.vue';
import CanvasPanel from './components/canvas/CanvasPanel.vue';
import FlowValidationDiagnostics from './components/canvas/FlowValidationDiagnostics.vue';
import InspectorPanel from './components/inspector/InspectorPanel.vue';
import RunConsole from './components/runs/RunConsole.vue';
import FlowDebugPanel from './components/workspace/FlowDebugPanel.vue';
import RunWorkpiecePanel from './components/runs/RunWorkpiecePanel.vue';
import RunVariablePanel from './components/runs/RunVariablePanel.vue';
import { publishFlowVersion, type FlowConcurrencyMode, type FlowValidationDiagnostic, type FlowVersionSummaryDto, type ProjectWorkspaceDto, } from './api/flowApi';
import { locale, setLocale, t, type Locale } from './i18n';
import { normalizeConnectionLineTypes, type ConnectionLineSettings, } from './flow/connectionLine';
import { normalizeCanvasFocusSettings, type CanvasFocusSettings, } from './flow/canvasFocus';
import { createInitialCanvases } from './flow/initialCanvases';
import { parameterHandleFor } from './flow/connectionSeats';
import { libraryNameResolverKey } from './flow/libraryNameResolver';
import { convertVariadicParameterMode } from './flow/variadicParameters';
import { parseFlowValidationDiagnosticTarget } from './flow/validationDiagnostics';
import { cloneWorkspaceSnapshot, workspaceFingerprint, type WorkspaceSnapshot } from './flow/workspaceHistory';
import { loadWorkspace } from './flow/workspaceStorage';
import { useWorkspaceHistory } from './composables/useWorkspaceHistory';
import { useLibraryCatalog } from './composables/useLibraryCatalog';
import { useFlowRunner } from './composables/useFlowRunner';
import { useFlowDebugger } from './composables/useFlowDebugger';
import { useCanvasManager } from './composables/useCanvasManager';
import { useProjectSession } from './composables/useProjectSession';
import { useWorkspaceSync } from './composables/useWorkspaceSync';
import { useFlowGraph } from './composables/useFlowGraph';
import { useNodeDrop } from './composables/useNodeDrop';
import { useWorkspaceShortcuts } from './composables/useWorkspaceShortcuts';
import { useDockableWorkspace } from './composables/useDockableWorkspace';
import { dockSplitZoneAtPoint, splitDockRect, type DockSplitZone } from './flow/dockableSplit';
import { workspaceToFlowDefinition } from './flow/flowDtoMapper';
import type { NodeExecutionState } from './flow/nodeExecutionState';
import type { DockablePanelGroupState, DockableWorkspaceMode, DockPosition, WorkspacePanelId, WorkspacePanelTab, } from './flow/dockableWorkspace';
import type { CanvasState, FlowNode, MethodParameter, NodeKind, } from './flow/types';
const bindingSource = useVueFlow('workspace-editor');
const { zoomIn } = bindingSource;
const { zoomOut } = bindingSource;
const { fitView } = bindingSource;
const { screenToFlowCoordinate } = bindingSource;
const recoveryWorkspace = loadWorkspace();
const createInitialCanvasesResult: CanvasState[] = createInitialCanvases();
const canvases = ref<CanvasState[]>(createInitialCanvasesResult);
const normalizeConnectionLineTypesResult1: ConnectionLineSettings = normalizeConnectionLineTypes(recoveryWorkspace?.connectionLineTypes);
const connectionLineTypes = reactive<ConnectionLineSettings>(normalizeConnectionLineTypesResult1);
const normalizeCanvasFocusSettingsResult1: CanvasFocusSettings = normalizeCanvasFocusSettings(recoveryWorkspace?.canvasFocusSettings);
const canvasFocusSettings = reactive<CanvasFocusSettings>(normalizeCanvasFocusSettingsResult1);
const activeCanvasId = ref('main');
const projectLibraryOpen = ref(false);
const mobilePanel = ref<'nodes' | 'inspector' | null>(null);
const languageMenuOpen = ref(false);
const projectMenuOpen = ref(false);
const connectionSettingsOpen = ref(false);
const isCanvasDropActive = ref(false);
const notice = ref('');
let noticeTimer: number | undefined;
watch(notice, (message) => {
  if (noticeTimer !== undefined) {
    window.clearTimeout(noticeTimer);
    noticeTimer = undefined;
  }
  if (!message) {
    return;
  }
  noticeTimer = window.setTimeout(() => {
    if (notice.value === message) {
      notice.value = '';
    }
    noticeTimer = undefined;
  }, 3500);
});
onBeforeUnmount(() => {
  if (noticeTimer !== undefined) {
    window.clearTimeout(noticeTimer);
  }
});
const bindingSource1 = useLibraryCatalog();
const { librarySearch } = bindingSource1;
const { libraries } = bindingSource1;
const { isLibraryCatalogLoading } = bindingSource1;
const { libraryCatalogError } = bindingSource1;
const { visibleLibraries } = bindingSource1;
const { visibleBuiltinNodes } = bindingSource1;
const { catalogNodeCount } = bindingSource1;
const { refreshLibraryCatalog } = bindingSource1;
const { replaceProjectLibraries } = bindingSource1;
provide(libraryNameResolverKey, (runtime) => {
  const explicitName = runtime?.flowLibraryName?.trim();
  if (explicitName) {
    return explicitName;
  }
  const library = libraries.value.find((candidate) => candidate.id === runtime?.libraryId);
  const catalogNode = library?.nodes.find((candidate) => candidate.contractId === runtime?.libraryNodeContractId)
    ?? library?.nodes.find((candidate) => candidate.className === runtime?.className
      && candidate.methodName === runtime?.methodName
      && candidate.dllName === runtime?.dllName
      && candidate.dllVersion === runtime?.dllVersion);
  const evaluatedValue: string | undefined = catalogNode?.flowLibraryName?.trim() || undefined;
  return evaluatedValue;
});
const nextNodeNumber = ref(1);
const isDirty = ref(false);
const saveFailed = ref(false);
const saveConflict = ref(false);
const saveDiagnostics = ref<FlowValidationDiagnostic[]>([]);
const isWorkspaceLoading = ref(true);
const isSaving = ref(false);
const projectId = ref<string>();
const flowId = ref<string>();
const projectWorkspaces = ref<ProjectWorkspaceDto[]>([]);
const canvasMountRevision = ref(0);
const evaluatedValue42: string = recoveryWorkspace?.projectName?.trim() || t('project.newProject');
const projectName = ref(evaluatedValue42);
const projectNameDraft = ref('');
const projectRenameOpen = ref(false);
const isProjectRenaming = ref(false);
const projectVersion = ref(1);
const flowVersion = ref(1);
const entryNodeId = ref(recoveryWorkspace?.entryNodeId ?? '');
const savedWorkspaceFingerprint = ref('');
const isRestoringWorkspace = ref(false);
const isSwitchingCanvas = ref(false);
const workspaceView = ref<'console' | 'editor'>('console');
const runConsole = ref<InstanceType<typeof RunConsole>>();
const isEditorActive = computed(() => workspaceView.value === 'editor');
const workspaceRoot = ref<HTMLElement>();
const bindingSource2 = useDockableWorkspace(workspaceRoot);
const { layout: dockableLayout } = bindingSource2;
const { displayMode: dockableDisplayMode } = bindingSource2;
const { groups: dockableGroups } = bindingSource2;
const { workspaceSize: dockableWorkspaceSize } = bindingSource2;
const { updateGroup: updateDockableGroup } = bindingSource2;
const { activatePanel: activateDockablePanel } = bindingSource2;
const { showPanel: showDockablePanel } = bindingSource2;
const { hidePanel: hideDockablePanel } = bindingSource2;
const { dockGroup: dockDockableGroup } = bindingSource2;
const { snapGroup: snapDockableGroup } = bindingSource2;
const { combinePanel: combineDockablePanel } = bindingSource2;
const { splitPanel: splitDockablePanel } = bindingSource2;
const { detachPanel: detachDockablePanel } = bindingSource2;
const { resetLayout: resetDockableLayout } = bindingSource2;
const { setDisplayMode: setDockableDisplayMode } = bindingSource2;
const { focusGroup: focusDockableGroup } = bindingSource2;
const runPolicy = ref<{
  concurrencyMode: FlowConcurrencyMode;
}>({ concurrencyMode: 'parallel' });
const flowVersionHistoryOpen = ref(false);
const isVersionPublishing = ref(false);
const currentProjectFlows = computed(() => {
  const evaluatedValue1: {
    id: string;
    version: number;
    entryNodeId: string;
    canvasCount?: number | undefined;
    nodeCount?: number | undefined;
    productionVersion?: number | undefined;
  }[] = projectId.value
    ? projectWorkspaces.value.find((workspace) => workspace.project.id === projectId.value)?.flows ?? []
    : [];
  return evaluatedValue1;
});
const productionVersion = computed(() => {
  const evaluatedValue2: number | undefined = currentProjectFlows.value.find((flow) => flow.id === flowId.value)?.productionVersion;
  return evaluatedValue2;
});
const canViewVersions = computed(() => {
  const booleanResult: boolean = Boolean(projectId.value && flowId.value && !isWorkspaceLoading.value);
  return booleanResult;
});
const canMutateVersions = computed(() => canViewVersions.value
  && !isDirty.value
  && !isSaving.value
  && !isDebugActive.value
  && !isVersionPublishing.value);
const canPublishVersion = computed(() => canMutateVersions.value && !flowVersionHistoryOpen.value);
function iconForNodeKind(kind: NodeKind) {
  if (kind === 'flipflop') {
    return Zap;
  }
  if (kind === 'script') {
    return Code2;
  }
  if (kind === 'flowCall') {
    return Activity;
  }
  return Database;
}
const currentCanvas = computed<CanvasState>(() => {
  const evaluatedValue3: CanvasState = canvases.value.find((canvas) => canvas.id === activeCanvasId.value) ?? canvases.value[0]!;
  return evaluatedValue3;
});
const canvasRenderKey = computed(() => `${activeCanvasId.value}:${canvasMountRevision.value}`);
const saveStateKey = computed(() => {
  if (saveConflict.value) {
    return 'canvas.saveConflict';
  }
  if (saveFailed.value) {
    return 'canvas.saveFailed';
  }
  if (isWorkspaceLoading.value || isSaving.value) {
    return 'canvas.saving';
  }
  return isDirty.value ? 'canvas.unsaved' : 'canvas.saved';
});
function localizeEdges(): void {
  for (const canvas of canvases.value) {
    canvas.edges = canvas.edges.map((edge) => {
      const evaluatedValue4: {
        label: undefined;
        labelShowBg: false;
        ariaLabel: string;
        id: string;
        source: string;
        target: string;
        selected?: boolean | undefined;
        sourceHandle?: string | null | undefined;
        targetHandle?: string | null | undefined;
        type?: string | undefined;
        markerEnd?: string | {
          id?: string | undefined;
          type: import("@vue-flow/core").MarkerType;
          color?: string | undefined;
          width?: number | undefined;
          height?: number | undefined;
          markerUnits?: string | undefined;
          orient?: string | undefined;
          strokeWidth?: number | undefined;
        } | undefined;
        labelBgPadding?: [
          number,
          number
        ] | undefined;
        labelBgBorderRadius?: number | undefined;
        class?: string | undefined;
        data: {
          semantic: import("./flow/types").ConnectionSemantic;
          targetParameterId?: string | undefined;
          branch?: import("./flow/types").ExecutionBranch | undefined;
          lineType?: import("./flow/types").FlowEdgeLineType | undefined;
        };
      } = ({
        ...edge,
        label: undefined,
        labelShowBg: false,
        ariaLabel: edge.data?.semantic === 'execution' ? t('inspector.executionEdge') : t('inspector.dataEdge'),
      });
      return evaluatedValue4;
    });
  }
}
watch(locale, localizeEdges, { immediate: true });
function currentWorkspaceSnapshot(): WorkspaceSnapshot {
  const hasEntryNode = canvases.value.some((canvas) => {
    const someResult: boolean = canvas.nodes.some((node) => node.id === entryNodeId.value);
    return someResult;
  });
  const cloneWorkspaceSnapshotResult: WorkspaceSnapshot = cloneWorkspaceSnapshot({
    canvases: canvases.value,
    activeCanvasId: activeCanvasId.value,
    nextNodeNumber: nextNodeNumber.value,
    entryNodeId: hasEntryNode ? entryNodeId.value : '',
    projectName: projectName.value,
    connectionLineTypes: { ...connectionLineTypes },
    canvasFocusSettings: { ...canvasFocusSettings },
    runPolicy: { ...runPolicy.value },
  });
  return cloneWorkspaceSnapshotResult;
}
function refreshDirtyState(): void {
  const resolveWorkspaceFingerprintResult = () => {
    const currentWorkspaceSnapshotResult1: WorkspaceSnapshot = currentWorkspaceSnapshot();
    const workspaceFingerprintResult = workspaceFingerprint(currentWorkspaceSnapshotResult1);
    return workspaceFingerprintResult;
  };
  isDirty.value = resolveWorkspaceFingerprintResult() !== savedWorkspaceFingerprint.value;
  if (isDirty.value) {
    saveFailed.value = false;
  }
}
function markWorkspaceChanged(): void {
  if (saveDiagnostics.value.length) {
    saveDiagnostics.value = [];
  }
  const resolveThenResult = () => {
    const nextTickResult = nextTick();
    const thenResult = nextTickResult.then(refreshDirtyState);
    return thenResult;
  };
  void resolveThenResult();
}
function restoreWorkspace(snapshot: WorkspaceSnapshot): void {
  isRestoringWorkspace.value = true;
  canvasMountRevision.value += 1;
  canvases.value = snapshot.canvases;
  if (snapshot.projectName?.trim()) {
    projectName.value = snapshot.projectName.trim();
  }
  const argumentValue: {
    data: import("./flow/types").FlowEdgeLineType;
    execution: import("./flow/types").FlowEdgeLineType;
  } = connectionLineTypes;
  const normalizeConnectionLineTypesResult: ConnectionLineSettings = normalizeConnectionLineTypes(snapshot.connectionLineTypes);
  Object.assign(argumentValue, normalizeConnectionLineTypesResult);
  const argumentValue1: {
    enabled: boolean;
    parameterSources: boolean;
    parameterConsumers: boolean;
    callers: boolean;
    callees: boolean;
  } = canvasFocusSettings;
  const normalizeCanvasFocusSettingsResult: CanvasFocusSettings = normalizeCanvasFocusSettings(snapshot.canvasFocusSettings);
  Object.assign(argumentValue1, normalizeCanvasFocusSettingsResult);
  runPolicy.value = snapshot.runPolicy ?? { concurrencyMode: 'parallel' };
  entryNodeId.value = snapshot.entryNodeId ?? '';
  activeCanvasId.value = snapshot.activeCanvasId;
  nextNodeNumber.value = snapshot.nextNodeNumber;
  mobilePanel.value = null;
  const resolveThenResult1 = () => {
    const nextTickResult1 = nextTick();
    const thenResult1 = nextTickResult1.then(() => {
      isRestoringWorkspace.value = false;
      refreshDirtyState();
    });
    return thenResult1;
  };
  void resolveThenResult1();
}
const bindingSource3 = useWorkspaceHistory({
  currentSnapshot: currentWorkspaceSnapshot,
  restoreSnapshot: restoreWorkspace,
  markWorkspaceChanged,
});
const { canUndo } = bindingSource3;
const { canRedo } = bindingSource3;
const { recordWorkspaceMutation } = bindingSource3;
const { undo: undoSnapshot } = bindingSource3;
const { redo: redoSnapshot } = bindingSource3;
const { beginTextEdit } = bindingSource3;
const { commitTextEdit } = bindingSource3;
const { discardTextEdit } = bindingSource3;
const { syncHistoryAvailability } = bindingSource3;
const { clear: clearHistory } = bindingSource3;
const bindingSource4 = useCanvasManager({
  canvases,
  activeCanvasId,
  currentCanvas,
  mobilePanel,
  notice,
  isSwitchingCanvas,
  recordWorkspaceMutation,
  markWorkspaceChanged,
});
const { canvasMenuOpen } = bindingSource4;
const { customCanvasNameDraft } = bindingSource4;
const { canvasDeleteConfirmOpen } = bindingSource4;
const { pendingCanvasDelete } = bindingSource4;
const { availableCanvasLifecycles } = bindingSource4;
const { canvasLabel } = bindingSource4;
const { selectCanvas } = bindingSource4;
const { reorderCanvas } = bindingSource4;
const { addCanvas } = bindingSource4;
const { toggleCanvasMenu } = bindingSource4;
const { addCustomCanvas } = bindingSource4;
const { requestCanvasRemoval } = bindingSource4;
const { cancelCanvasRemoval } = bindingSource4;
const { confirmCanvasRemoval } = bindingSource4;
const bindingSource5 = useFlowGraph({
  currentCanvas,
  nextNodeNumber,
  connectionLineTypes,
  canvasFocusSettings,
  mobilePanel,
  notice,
  isRestoringWorkspace,
  isSwitchingCanvas,
  recordWorkspaceMutation,
  markWorkspaceChanged,
});
const { nodes } = bindingSource5;
const { renderedElements } = bindingSource5;
const { selectedNode } = bindingSource5;
const { selectedEdge } = bindingSource5;
const { selectedNodeCount } = bindingSource5;
const { selectedEdgeCount } = bindingSource5;
const { selectNode } = bindingSource5;
const { selectAllNodes } = bindingSource5;
const { onNodeClick } = bindingSource5;
const { onEdgeClick } = bindingSource5;
const { clearSelection } = bindingSource5;
const { isValidConnection } = bindingSource5;
const { updateConnectionLineType } = bindingSource5;
const { updateCanvasFocusSetting } = bindingSource5;
const { onConnect } = bindingSource5;
const { onEdgeUpdate } = bindingSource5;
const { onNodesChange } = bindingSource5;
const { onEdgesChange } = bindingSource5;
const { updateParameterSource } = bindingSource5;
const { addNode } = bindingSource5;
const { removeSelection } = bindingSource5;
const bindingSource6 = useFlowRunner({ nodes, notice, projectId, flowId, flowVersion });
const { isRunning } = bindingSource6;
const { activeOutput } = bindingSource6;
const { runEvents } = bindingSource6;
const { runPayload } = bindingSource6;
const { runFlow } = bindingSource6;
const { lastRunId } = bindingSource6;
function focusDebugCanvasNode(nodeId: string): void {
  showDockablePanel('workpieces');
  const canvas = canvases.value.find((candidate) => {
    const someResult1: boolean = candidate.nodes.some((node) => node.id === nodeId);
    return someResult1;
  });
  if (!canvas) {
    return;
  }
  if (canvas.id !== activeCanvasId.value) {
    selectCanvas(canvas.id);
  }
  const resolveThenResult2 = () => {
    const nextTickResult2 = nextTick();
    const thenResult2 = nextTickResult2.then(() => {
      const selectNodeResult: void | PromiseLike<void> = selectNode(nodeId);
      return selectNodeResult;
    });
    return thenResult2;
  };
  void resolveThenResult2();
}
function handleCanvasNodeClick(event: {
  node: {
    id: string;
  };
}): void {
  onNodeClick(event);
  // A completed debug session remains available so its results can still be
  // inspected. It must not make normal editor clicks reopen the workpieces
  // panel after the user has switched back to edit mode.
  // 保留已完成的调试会话供检查结果；用户切回编辑模式后，普通编辑器点击不应重新打开运行工件面板。
  if (!debugSession.value || dockableDisplayMode.value !== 'debug') {
    return;
  }
  const execution = debugExecutionStates.value.find((state) => state.nodeId === event.node.id);
  selectedDebugNodeId.value = event.node.id;
  selectedDebugExecutionId.value = execution?.id;
  workpieceFocusNodeId.value = event.node.id;
  workpieceFocusExecutionId.value = execution?.executionId;
  showDockablePanel('workpieces');
}
function focusDebugWorkpieceNode(nodeId: string, executionId?: string): void {
  if (!debugSession.value) {
    return;
  }
  const execution = executionId
    ? debugExecutionStates.value.find((state) => state.id === executionId || state.executionId === executionId)
    : debugExecutionStates.value.find((state) => state.nodeId === nodeId);
  selectedDebugNodeId.value = nodeId;
  selectedDebugExecutionId.value = execution?.id;
  workpieceFocusNodeId.value = nodeId;
  workpieceFocusExecutionId.value = executionId ?? execution?.executionId;
  showDockablePanel('debug');
  focusDebugCanvasNode(nodeId);
}
function focusDebugExecution(execution: NodeExecutionState): void {
  if (!debugSession.value) {
    return;
  }
  selectedDebugNodeId.value = execution.nodeId;
  selectedDebugExecutionId.value = execution.id;
  workpieceFocusNodeId.value = execution.nodeId;
  workpieceFocusExecutionId.value = execution.executionId;
  showDockablePanel('debug');
  focusDebugCanvasNode(execution.nodeId);
}
function handleDebugPause(nodeId: string, executionId?: string): void {
  selectedDebugNodeId.value = nodeId;
  const execution = executionId
    ? debugExecutionStates.value.find((state) => state.id === executionId || state.executionId === executionId)
    : debugExecutionStates.value.find((state) => state.nodeId === nodeId);
  selectedDebugExecutionId.value = execution?.id;
  workpieceFocusNodeId.value = nodeId;
  workpieceFocusExecutionId.value = executionId ?? execution?.executionId;
  focusDebugCanvasNode(nodeId);
  workpieceRefreshRevision.value += 1;
}
const bindingSource7 = useFlowDebugger({
  canvases,
  projectId,
  flowId,
  flowVersion,
  getCurrentDefinition: () => {
    if (!flowId.value) {
      return undefined;
    }
    const currentWorkspaceSnapshotResult: WorkspaceSnapshot = currentWorkspaceSnapshot();
    const workspaceToFlowDefinitionResult: import("./api/flowApi").FlowDefinitionDto | undefined = workspaceToFlowDefinition(currentWorkspaceSnapshotResult, {
      id: flowId.value,
      version: flowVersion.value,
    });
    return workspaceToFlowDefinitionResult;
  },
  isWorkspaceLoading,
  isNormalRunActive: isRunning,
  notice,
  onPauseNode: handleDebugPause,
});
const { isBreakpoint } = bindingSource7;
const { toggleBreakpoint } = bindingSource7;
const { debugSession } = bindingSource7;
const { pauseBoundary } = bindingSource7;
const { runEvents: debugRunEvents } = bindingSource7;
const { executionStates: debugExecutionStates } = bindingSource7;
const { runPayload: debugRunPayload } = bindingSource7;
const { isStarting: isDebugStarting } = bindingSource7;
const { isControlling: isDebugControlling } = bindingSource7;
const { isStopping: isDebugStopping } = bindingSource7;
const { isDebugActive } = bindingSource7;
const { isDebugPaused } = bindingSource7;
const { canStartDebug } = bindingSource7;
const { startDebug } = bindingSource7;
const { continueDebug } = bindingSource7;
const { stepDebug } = bindingSource7;
const { stopDebug } = bindingSource7;
const debugNodeNames = computed<Record<string, string>>(() => {
  const flatMapResult: Iterable<readonly [
    PropertyKey,
    string
  ]> = canvases.value.flatMap((canvas) => {
    const mapResult: [
      string,
      string
    ] | readonly [
      string,
      string
    ][] = canvas.nodes.map((node) => {
      const evaluatedValue5: [
        string,
        string
      ] = [
        node.id,
        node.data.displayName?.trim() || t(node.data.titleKey),
      ];
      return evaluatedValue5;
    });
    return mapResult;
  });
  const fromEntriesResult: Record<string, string> = Object.fromEntries(flatMapResult);
  return fromEntriesResult;
});
const visibleRunEvents = computed(() => isDebugActive.value || (!isRunning.value && debugSession.value) ? debugRunEvents.value : runEvents.value);
const visibleRunPayload = computed(() => isDebugActive.value || (!isRunning.value && debugSession.value) ? debugRunPayload.value : runPayload.value);
const visibleHasRunOutput = computed(() => visibleRunEvents.value.length > 0);
const visibleActiveOutput = computed({
  get: () => activeOutput.value,
  set: (value: 'events' | 'payload') => {
    activeOutput.value = value;
  },
});
const activeWorkpieceRunId = computed(() => debugSession.value?.runId ?? lastRunId.value ?? '');
const activeVariableRunId = ref('');
watch([lastRunId, () => debugSession.value?.runId], (activeRuns, previousActiveRuns) => {
  const normalRun = activeRuns[0];
  const debugRun = activeRuns[1];
  const previousNormal = previousActiveRuns[0];
  const previousDebug = previousActiveRuns[1];
  if (debugRun && debugRun !== previousDebug && isDebugActive.value) {
    activeVariableRunId.value = debugRun;
  }
  else if (normalRun && normalRun !== previousNormal) {
    activeVariableRunId.value = normalRun;
  }
  else if (debugRun && debugRun !== previousDebug) {
    activeVariableRunId.value = debugRun;
  }
  else if (!normalRun && !debugRun) {
    activeVariableRunId.value = '';
  }
}, { immediate: true });
const variableRunIsLive = computed(() => {
  const booleanResult1: boolean = Boolean((activeVariableRunId.value === lastRunId.value && isRunning.value)
    || (activeVariableRunId.value === debugSession.value?.runId && isDebugActive.value));
  return booleanResult1;
});
const selectedDebugNodeId = ref<string>();
const selectedDebugExecutionId = ref<string>();
const workpieceFocusNodeId = ref<string>();
const workpieceFocusExecutionId = ref<string>();
const workpieceRefreshRevision = ref(0);
const canUseDebugDisplayMode = computed(() => {
  const booleanResult2: boolean = Boolean(debugSession.value);
  return booleanResult2;
});
const panelDefinitions = computed<WorkspacePanelTab[]>(() => {
  const evaluatedValue6: WorkspacePanelTab[] = [
    { id: 'canvas', label: t('panel.canvas'), icon: markRaw(LayoutGrid), closable: false },
    { id: 'nodes', label: t('panel.nodes'), icon: markRaw(PanelLeft) },
    { id: 'inspector', label: t('panel.inspector'), icon: markRaw(PanelRight) },
    { id: 'output', label: t('panel.output'), icon: markRaw(Terminal) },
    { id: 'diagnostics', label: t('panel.diagnostics'), icon: markRaw(AlertTriangle) },
    { id: 'debug', label: t('panel.debug'), icon: markRaw(Bug) },
    { id: 'workpieces', label: t('panel.workpieces'), icon: markRaw(PackageOpen) },
    { id: 'variables', label: t('variables.title'), icon: markRaw(Database) },
  ];
  return evaluatedValue6;
});
function isPanelAvailable(panelId: WorkspacePanelId): boolean {
  if (panelId === 'debug') {
    const booleanResult3: boolean = Boolean(debugSession.value);
    return booleanResult3;
  }
  if (panelId === 'workpieces') {
    const booleanResult4: boolean = Boolean(activeWorkpieceRunId.value);
    return booleanResult4;
  }
  if (panelId === 'variables') {
    const booleanResult5: boolean = Boolean(activeVariableRunId.value);
    return booleanResult5;
  }
  return true;
}
function tabsForGroup(group: DockablePanelGroupState): WorkspacePanelTab[] {
  const filterResult = group.panelIds
    .filter((panelId) => {
    const evaluatedValue7: boolean = dockableLayout.panels[panelId].visible && isPanelAvailable(panelId);
    return evaluatedValue7;
  });
  const mapResult1 = filterResult.map((panelId) => {
    const findResult: WorkspacePanelTab | undefined = panelDefinitions.value.find((definition) => definition.id === panelId);
    return findResult;
  });
  const filterResult1: WorkspacePanelTab[] = mapResult1.filter((definition): definition is WorkspacePanelTab => {
    const booleanResult6: boolean = Boolean(definition);
    return booleanResult6;
  });
  return filterResult1;
}
function groupOptionsFor(groupId: string): Array<{
  id: string;
  label: string;
}> {
  const filterResult2 = dockableGroups.value
    .filter((group) => {
    const evaluatedValue8: boolean = group.id !== groupId && tabsForGroup(group).length > 0;
    return evaluatedValue8;
  });
  const mapResult2: {
    id: string;
    label: string;
  }[] = filterResult2.map((group) => {
    const evaluatedValue9: {
      id: string;
      label: string;
    } = ({
      id: group.id,
      label: panelDefinitions.value.find((definition) => definition.id === group.activePanelId)?.label
        ?? t('panel.emptyGroup'),
    });
    return evaluatedValue9;
  });
  return mapResult2;
}
type EdgeDockPosition = Exclude<DockPosition, 'free' | 'fill'>;
type WorkspaceDropZone = EdgeDockPosition | 'fill';
interface WorkspaceRect {
  left: number;
  top: number;
  right: number;
  bottom: number;
}
interface WorkspaceSnapPreview {
  groupId: string;
  dock?: EdgeDockPosition;
  targetGroupId?: string;
  targetRect?: WorkspaceRect;
  region?: WorkspaceRect;
  zone?: WorkspaceDropZone | DockSplitZone;
}
function isEdgeDockPosition(dock: DockPosition): dock is EdgeDockPosition {
  return dock === 'left' || dock === 'right' || dock === 'top' || dock === 'bottom';
}
function desiredEdgeGroupExtent(group: DockablePanelGroupState, dock: EdgeDockPosition): number {
  if (group.collapsed) {
    return 76;
  }
  const raw = dock === 'left' || dock === 'right' ? group.width : group.height;
  const maximum = dock === 'left' || dock === 'right'
    ? dockableWorkspaceSize.width
    : dockableWorkspaceSize.height;
  const minResult: number = Math.min(raw, maximum);
  const maxResult: number = Math.max(0, minResult);
  return maxResult;
}
const renderedEdgeGroups = computed(() => {
  const filterResult3: {
    id: string;
    x: number;
    y: number;
    width: number;
    height: number;
    dock: DockPosition;
    panelIds: WorkspacePanelId[];
    activePanelId: WorkspacePanelId;
    zIndex: number;
    collapsed: boolean;
  }[] = dockableGroups.value.filter((group) => {
    const evaluatedValue10: boolean = isEdgeDockPosition(group.dock) && tabsForGroup(group).length > 0;
    return evaluatedValue10;
  });
  return filterResult3;
});
const dockedLayoutPriority = computed<'full-row' | 'center-column'>(() => {
  const evaluatedValue11: "full-row" | "center-column" = renderedEdgeGroups.value.some((group) => group.dock === 'top' || group.dock === 'bottom')
    ? 'full-row'
    : 'center-column';
  return evaluatedValue11;
});
function dockedExtentFor(group: DockablePanelGroupState): number | undefined {
  if (!isEdgeDockPosition(group.dock)) {
    return undefined;
  }
  const dock = group.dock;
  const peers = renderedEdgeGroups.value.filter((candidate) => candidate.dock === dock);
  const total = peers.reduce((sum, candidate) => {
    const evaluatedValue12: number = sum + desiredEdgeGroupExtent(candidate, dock);
    return evaluatedValue12;
  }, 0);
  const capacity = dock === 'left' || dock === 'right'
    ? dockableWorkspaceSize.width
    : dockableWorkspaceSize.height;
  const scale = total > capacity && total > 0 ? capacity / total : 1;
  const evaluatedValue13: number | undefined = desiredEdgeGroupExtent(group, dock) * scale;
  return evaluatedValue13;
}
function dockedOffsetFor(group: DockablePanelGroupState): number {
  if (!isEdgeDockPosition(group.dock)) {
    return 0;
  }
  const dock = group.dock;
  const peers = renderedEdgeGroups.value.filter((candidate) => candidate.dock === dock);
  const groupIndex = peers.findIndex((candidate) => candidate.id === group.id);
  const maxResult1: number | undefined = Math.max(0, groupIndex);
  const sliceResult = peers.slice(0, maxResult1);
  const reduceResult: number = sliceResult.reduce((offset, candidate) => {
    const evaluatedValue14: number = offset + (dockedExtentFor(candidate) ?? 0);
    return evaluatedValue14;
  }, 0);
  return reduceResult;
}
const workspaceInsets = computed(() => {
  const reduceResult1: {
    top: number;
    right: number;
    bottom: number;
    left: number;
  } = renderedEdgeGroups.value.reduce((insets, group) => {
    const extent = dockedExtentFor(group) ?? 0;
    if (group.dock === 'left') {
      insets.left += extent;
    }
    if (group.dock === 'right') {
      insets.right += extent;
    }
    if (group.dock === 'top') {
      insets.top += extent;
    }
    if (group.dock === 'bottom') {
      insets.bottom += extent;
    }
    return insets;
  }, { top: 0, right: 0, bottom: 0, left: 0 });
  return reduceResult1;
});
function workspaceRectForGroup(group: DockablePanelGroupState): WorkspaceRect {
  const minResult1: number = Math.min(group.width, dockableWorkspaceSize.width);
  const width = Math.max(240, minResult1);
  const minResult2: number = Math.min(group.height, dockableWorkspaceSize.height);
  const height = Math.max(160, minResult2);
  return {
    left: group.x,
    top: group.y,
    right: group.x + width,
    bottom: group.y + height,
  };
}
function rectArea(rect: WorkspaceRect): number {
  const evaluatedValue15: number = Math.max(0, rect.right - rect.left) * Math.max(0, rect.bottom - rect.top);
  return evaluatedValue15;
}
function rectOverlap(first: WorkspaceRect, second: WorkspaceRect): number {
  const resolveMaxResult = () => {
    const evaluatedValue43: number = Math.min(first.right, second.right) - Math.max(first.left, second.left);
    const maxResult10 = Math.max(0, evaluatedValue43);
    return maxResult10;
  };
  const resolveMaxResult1 = () => {
    const evaluatedValue44: number = Math.min(first.bottom, second.bottom) - Math.max(first.top, second.top);
    const maxResult11 = Math.max(0, evaluatedValue44);
    return maxResult11;
  };
  const evaluatedValue16: number = resolveMaxResult() * resolveMaxResult1();
  return evaluatedValue16;
}
function containsPoint(rect: WorkspaceRect, clientX: number, clientY: number): boolean {
  return clientX >= rect.left && clientX <= rect.right && clientY >= rect.top && clientY <= rect.bottom;
}
function blankRegionForPointer(groupId: string, clientX: number, clientY: number): WorkspaceRect | undefined {
  const base: WorkspaceRect = {
    left: workspaceInsets.value.left,
    top: workspaceInsets.value.top,
    right: dockableWorkspaceSize.width - workspaceInsets.value.right,
    bottom: dockableWorkspaceSize.height - workspaceInsets.value.bottom,
  };
  if (!containsPoint(base, clientX, clientY) || base.right - base.left < 240 || base.bottom - base.top < 160) {
    return undefined;
  }
  const filterResult4 = dockableGroups.value
    .filter((group) => {
    const evaluatedValue17: boolean = group.id !== groupId && group.dock === 'free' && tabsForGroup(group).length > 0;
    return evaluatedValue17;
  });
  const mapResult3 = filterResult4.map((group) => {
    const workspaceRectForGroupResult: WorkspaceRect = workspaceRectForGroup(group);
    return workspaceRectForGroupResult;
  });
  const mapResult4 = mapResult3.map((rect) => {
    const evaluatedValue18: {
      left: number;
      top: number;
      right: number;
      bottom: number;
    } = ({
      left: Math.max(base.left, rect.left),
      top: Math.max(base.top, rect.top),
      right: Math.min(base.right, rect.right),
      bottom: Math.min(base.bottom, rect.bottom),
    });
    return evaluatedValue18;
  });
  const blockers = mapResult4.filter((rect) => rect.right > rect.left && rect.bottom > rect.top);
  const evaluatedValue20: Iterable<number> | null | undefined = [base.left, base.right, ...blockers.flatMap((rect) => [rect.left, rect.right])];
  const setInstance: Iterable<number> | ArrayLike<number> = new Set(evaluatedValue20);
  const fromResult = Array.from(setInstance);
  const xLines = fromResult.sort((a, b) => a - b);
  const evaluatedValue21: Iterable<number> | null | undefined = [base.top, base.bottom, ...blockers.flatMap((rect) => [rect.top, rect.bottom])];
  const setInstance1: Iterable<number> | ArrayLike<number> = new Set(evaluatedValue21);
  const fromResult1 = Array.from(setInstance1);
  const yLines = fromResult1.sort((a, b) => a - b);
  let best: WorkspaceRect | undefined;
  for (let xIndex = 0; xIndex < xLines.length - 1; xIndex += 1) {
    for (let nextXIndex = xIndex + 1; nextXIndex < xLines.length; nextXIndex += 1) {
      for (let yIndex = 0; yIndex < yLines.length - 1; yIndex += 1) {
        for (let nextYIndex = yIndex + 1; nextYIndex < yLines.length; nextYIndex += 1) {
          const candidate = { left: xLines[xIndex]!, top: yLines[yIndex]!, right: xLines[nextXIndex]!, bottom: yLines[nextYIndex]! };
          if (!containsPoint(candidate, clientX, clientY)
            || candidate.right - candidate.left < 240
            || candidate.bottom - candidate.top < 160
            || blockers.some((blocker) => {
              const evaluatedValue19: boolean = rectOverlap(candidate, blocker) > 0;
              return evaluatedValue19;
            })) {
            continue;
          }
          if (!best || rectArea(candidate) > rectArea(best)) {
            best = candidate;
          }
        }
      }
    }
  }
  return best;
}
function blankDropZone(region: WorkspaceRect, clientX: number, clientY: number): WorkspaceDropZone {
  const roundResult: number = Math.round((region.right - region.left) * .24);
  const maxResult2: number = Math.max(56, roundResult);
  const edgeWidth = Math.min(132, maxResult2);
  const roundResult1: number = Math.round((region.bottom - region.top) * .2);
  const maxResult3: number = Math.max(48, roundResult1);
  const edgeHeight = Math.min(108, maxResult3);
  if (clientX <= region.left + edgeWidth) {
    return 'left';
  }
  if (clientX >= region.right - edgeWidth) {
    return 'right';
  }
  if (clientY <= region.top + edgeHeight) {
    return 'top';
  }
  if (clientY >= region.bottom - edgeHeight) {
    return 'bottom';
  }
  return 'fill';
}
function dropRectForZone(region: WorkspaceRect, zone: WorkspaceDropZone): WorkspaceRect {
  const width = region.right - region.left;
  const height = region.bottom - region.top;
  const argumentValue2: number = width;
  const roundResult2: number = Math.round(width * .34);
  const maxResult4: number = Math.max(240, roundResult2);
  const splitWidth = Math.min(argumentValue2, maxResult4);
  const argumentValue3: number = height;
  const roundResult3: number = Math.round(height * .34);
  const maxResult5: number = Math.max(160, roundResult3);
  const splitHeight = Math.min(argumentValue3, maxResult5);
  if (zone === 'left') {
    return { left: region.left, top: region.top, right: region.left + splitWidth, bottom: region.bottom };
  }
  if (zone === 'right') {
    return { left: region.right - splitWidth, top: region.top, right: region.right, bottom: region.bottom };
  }
  if (zone === 'top') {
    return { left: region.left, top: region.top, right: region.right, bottom: region.top + splitHeight };
  }
  if (zone === 'bottom') {
    return { left: region.left, top: region.bottom - splitHeight, right: region.right, bottom: region.bottom };
  }
  return region;
}
const workspaceSnapPreview = ref<WorkspaceSnapPreview>();
const workspaceDropZones = computed(() => {
  const region = workspaceSnapPreview.value?.region;
  if (!region) {
    return [];
  }
  if (workspaceSnapPreview.value?.targetGroupId) {
    return [{
        id: workspaceSnapPreview.value.zone ?? 'center',
        style: { left: `${region.left}px`, top: `${region.top}px`, width: `${region.right - region.left}px`, height: `${region.bottom - region.top}px` },
      }];
  }
  const width = region.right - region.left;
  const height = region.bottom - region.top;
  const roundResult4: number = Math.round(width * .24);
  const maxResult6: number = Math.max(56, roundResult4);
  const edgeWidth = Math.min(132, maxResult6);
  const roundResult5: number = Math.round(height * .2);
  const maxResult7: number = Math.max(48, roundResult5);
  const edgeHeight = Math.min(108, maxResult7);
  const evaluatedValue22: {
    id: "fill" | "left" | "right" | "top" | "bottom" | "center";
    style: {
      left: string;
      top: string;
      width: string;
      height: string;
    };
  }[] = [
    { id: 'left' as const, style: { left: `${region.left}px`, top: `${region.top}px`, width: `${edgeWidth}px`, height: `${height}px` } },
    { id: 'right' as const, style: { left: `${region.right - edgeWidth}px`, top: `${region.top}px`, width: `${edgeWidth}px`, height: `${height}px` } },
    { id: 'top' as const, style: { left: `${region.left + edgeWidth}px`, top: `${region.top}px`, width: `${Math.max(0, width - edgeWidth * 2)}px`, height: `${edgeHeight}px` } },
    { id: 'bottom' as const, style: { left: `${region.left + edgeWidth}px`, top: `${region.bottom - edgeHeight}px`, width: `${Math.max(0, width - edgeWidth * 2)}px`, height: `${edgeHeight}px` } },
    { id: 'fill' as const, style: { left: `${region.left + edgeWidth}px`, top: `${region.top + edgeHeight}px`, width: `${Math.max(0, width - edgeWidth * 2)}px`, height: `${Math.max(0, height - edgeHeight * 2)}px` } },
  ];
  return evaluatedValue22;
});
const snapPreviewLabel = computed(() => {
  const preview = workspaceSnapPreview.value;
  if (!preview) {
    return '';
  }
  if (preview.targetGroupId) {
    const evaluatedValue23: string = preview.zone === 'center' ? t('panel.combineHint') : t('panel.splitHint');
    return evaluatedValue23;
  }
  if (preview.dock) {
    const tResult: string = t('panel.dockHint');
    return tResult;
  }
  if (preview.zone === 'fill') {
    const tResult1: string = t('panel.fillHint');
    return tResult1;
  }
  const evaluatedValue24: string = preview.zone ? t('panel.splitHint') : '';
  return evaluatedValue24;
});
const snapPreviewLabelStyle = computed(() => {
  const region = workspaceSnapPreview.value?.region;
  return region ? { left: `${(region.left + region.right) / 2}px`, top: `${(region.top + region.bottom) / 2}px` } : undefined;
});
function renderedWorkspaceRect(groupId: string): WorkspaceRect | undefined {
  const root = workspaceRoot.value;
  const evaluatedValue25: Iterable<HTMLElement> | ArrayLike<HTMLElement> = root?.querySelectorAll<HTMLElement>('[data-dock-group]') ?? [];
  const fromResult2 = Array.from(evaluatedValue25);
  const element = fromResult2.find((candidate) => candidate.dataset.dockGroup === groupId);
  if (!root || !element) {
    return undefined;
  }
  const bounds = root.getBoundingClientRect();
  const rect = element.getBoundingClientRect();
  return { left: rect.left - bounds.left, top: rect.top - bounds.top, right: rect.right - bounds.left, bottom: rect.bottom - bounds.top };
}
function previewWorkspaceMove(groupId: string, event?: {
  dock?: DockPosition;
  targetGroupId?: string;
  clientX?: number;
  clientY?: number;
}): void {
  if (!event?.dock && !event?.targetGroupId && (event?.clientX === undefined || event.clientY === undefined)) {
    workspaceSnapPreview.value = undefined;
    return;
  }
  if (event.targetGroupId) {
    const targetRect = renderedWorkspaceRect(event.targetGroupId);
    const bounds = workspaceRoot.value?.getBoundingClientRect();
    if (!targetRect || !bounds || event.clientX === undefined || event.clientY === undefined) {
      return;
    }
    const zone = dockSplitZoneAtPoint(targetRect, event.clientX - bounds.left, event.clientY - bounds.top);
    workspaceSnapPreview.value = {
      groupId,
      targetGroupId: event.targetGroupId,
      targetRect,
      region: zone === 'center' ? targetRect : splitDockRect(targetRect, zone).source,
      zone,
    };
    return;
  }
  if (event.dock && isEdgeDockPosition(event.dock)) {
    const region: WorkspaceRect = event.dock === 'left' || event.dock === 'right'
      ? {
        left: 0,
        top: dockedLayoutPriority.value === 'full-row' ? workspaceInsets.value.top : 0,
        right: dockableWorkspaceSize.width,
        bottom: dockedLayoutPriority.value === 'full-row' ? dockableWorkspaceSize.height - workspaceInsets.value.bottom : dockableWorkspaceSize.height,
      }
      : {
        left: 0,
        top: 0,
        right: dockableWorkspaceSize.width,
        bottom: dockableWorkspaceSize.height,
      };
    workspaceSnapPreview.value = {
      groupId,
      dock: event.dock,
      region,
      zone: event.dock,
    };
    return;
  }
  if (event.clientX === undefined || event.clientY === undefined) {
    workspaceSnapPreview.value = undefined;
    return;
  }
  const bounds = workspaceRoot.value?.getBoundingClientRect();
  if (!bounds) {
    workspaceSnapPreview.value = undefined;
    return;
  }
  const localX = event.clientX - bounds.left;
  const localY = event.clientY - bounds.top;
  const region = blankRegionForPointer(groupId, localX, localY);
  workspaceSnapPreview.value = region ? {
    groupId,
    region,
    zone: blankDropZone(region, localX, localY),
  } : undefined;
}
const panelSwitcherItems = computed(() => {
  const mapResult5: {
    visible: boolean;
    available: boolean;
    required: boolean;
    id: WorkspacePanelId;
    label: string;
    icon: import("vue").Component;
    closable?: boolean;
  }[] = panelDefinitions.value.map((definition) => {
    const evaluatedValue26: {
      visible: boolean;
      available: boolean;
      required: boolean;
      id: WorkspacePanelId;
      label: string;
      icon: import("vue").Component;
      closable?: boolean;
    } = ({
      ...definition,
      visible: dockableLayout.panels[definition.id].visible,
      available: isPanelAvailable(definition.id),
      required: definition.id === 'canvas',
    });
    return evaluatedValue26;
  });
  return mapResult5;
});
function toggleWorkspacePanel(panelId: WorkspacePanelId): void {
  if (panelId === 'canvas' || !isPanelAvailable(panelId)) {
    return;
  }
  if (dockableLayout.panels[panelId].visible) {
    hideDockablePanel(panelId);
  }
  else {
    showDockablePanel(panelId);
  }
}
function changeWorkspaceDisplayMode(mode: DockableWorkspaceMode): void {
  if (mode === 'debug' && !canUseDebugDisplayMode.value) {
    return;
  }
  setDockableDisplayMode(mode);
  if (mode === 'debug') {
    showDockablePanel('debug');
    if (activeWorkpieceRunId.value) {
      showDockablePanel('workpieces');
    }
  }
}
function updateWorkspaceGroup(groupId: string, patch: Partial<DockablePanelGroupState>): void {
  updateDockableGroup(groupId, patch);
}
function activateWorkspacePanel(groupId: string, panelId: WorkspacePanelId): void {
  activateDockablePanel(groupId, panelId);
}
function inspectDebugNode(): void {
  showDockablePanel('inspector');
  activateDockablePanel(dockableLayout.panels.inspector.groupId, 'inspector');
}
function dockForPointer(clientX: number, clientY: number): 'left' | 'right' | 'top' | 'bottom' | undefined {
  const bounds = workspaceRoot.value?.getBoundingClientRect();
  if (!bounds) {
    return undefined;
  }
  const distances: Array<{
    dock: 'left' | 'right' | 'top' | 'bottom';
    distance: number;
  }> = [
    { dock: 'left', distance: clientX - bounds.left },
    { dock: 'right', distance: bounds.right - clientX },
    { dock: 'top', distance: clientY - bounds.top },
    { dock: 'bottom', distance: bounds.bottom - clientY },
  ];
  const nearest = distances.sort((a, b) => a.distance - b.distance)[0];
  return nearest && nearest.distance <= 52 ? nearest.dock : undefined;
}
function applyBlankWorkspaceDrop(groupId: string, panelId: WorkspacePanelId, region: WorkspaceRect, zone: WorkspaceDropZone): void {
  const sourceGroup = dockableGroups.value.find((group) => group.id === groupId);
  if (!sourceGroup) {
    return;
  }
  let targetGroupId = groupId;
  if (sourceGroup.panelIds.length > 1) {
    targetGroupId = detachDockablePanel(panelId) ?? groupId;
  }
  const targetGroup = dockableGroups.value.find((group) => group.id === targetGroupId);
  if (!targetGroup) {
    return;
  }
  const rect = dropRectForZone(region, zone);
  updateDockableGroup(targetGroupId, {
    dock: 'free',
    x: rect.left,
    y: rect.top,
    width: rect.right - rect.left,
    height: rect.bottom - rect.top,
  });
  focusDockableGroup(targetGroupId);
}
function finishWorkspaceMove(groupId: string, event: {
  panelId: WorkspacePanelId;
  targetGroupId?: string;
  dockTarget?: DockPosition;
  clientX: number;
  clientY: number;
  moved: boolean;
  interaction: 'move' | 'resize';
}): void {
  const preview = workspaceSnapPreview.value?.groupId === groupId ? workspaceSnapPreview.value : undefined;
  workspaceSnapPreview.value = undefined;
  if (event.interaction === 'resize') {
    focusDockableGroup(groupId);
    return;
  }
  const sourceGroup = dockableGroups.value.find((group) => group.id === groupId);
  const movingTab = Boolean(sourceGroup && sourceGroup.panelIds.length > 1);
  if (event.targetGroupId && event.moved
    && (event.targetGroupId !== groupId || (movingTab && preview?.zone && preview.zone !== 'center'))) {
    if (preview?.targetGroupId === event.targetGroupId && preview.targetRect && preview.zone && preview.zone !== 'center'
      && preview.zone !== 'fill') {
      splitDockablePanel(event.panelId, event.targetGroupId, preview.zone, preview.targetRect);
    }
    else {
      combineDockablePanel(event.panelId, event.targetGroupId);
    }
    return;
  }
  const dock = event.moved
    ? (event.dockTarget && isEdgeDockPosition(event.dockTarget) ? event.dockTarget : dockForPointer(event.clientX, event.clientY))
    : undefined;
  if (dock) {
    if (movingTab) {
      dockWorkspacePanel(event.panelId, dock);
    }
    else {
      dockDockableGroup(groupId, dock);
    }
  }
  else if (event.moved && preview?.region && preview.zone && preview.zone !== 'center') {
    applyBlankWorkspaceDrop(groupId, event.panelId, preview.region, preview.zone);
  }
  else {
    const bounds = workspaceRoot.value?.getBoundingClientRect();
    const localX = bounds ? event.clientX - bounds.left : 0;
    const localY = bounds ? event.clientY - bounds.top : 0;
    const sourceRect = renderedWorkspaceRect(groupId);
    if (event.moved && movingTab && bounds && (!sourceRect || !containsPoint(sourceRect, localX, localY))) {
      const detachedId = detachDockablePanel(event.panelId);
      const detached = dockableGroups.value.find((group) => group.id === detachedId);
      if (detached) {
        const argumentValue4: number = detached.width;
        const roundResult6: number = Math.round(dockableWorkspaceSize.width * .45);
        const maxResult8: number = Math.max(320, roundResult6);
        const width = Math.min(argumentValue4, maxResult8);
        const argumentValue5: number = detached.height;
        const roundResult7: number = Math.round(dockableWorkspaceSize.height * .55);
        const maxResult9: number = Math.max(240, roundResult7);
        const height = Math.min(argumentValue5, maxResult9);
        const argumentValue6: string = detached.id;
        const resolveMaxResult2 = () => {
          const argumentValue10: number = dockableWorkspaceSize.width - width;
          const roundResult8: number = Math.round(localX - width / 2);
          const minResult3: number = Math.min(argumentValue10, roundResult8);
          const maxResult12: number | undefined = Math.max(0, minResult3);
          return maxResult12;
        };
        const resolveMaxResult3 = () => {
          const argumentValue11: number = dockableWorkspaceSize.height - height;
          const roundResult9: number = Math.round(localY - 20);
          const minResult4: number = Math.min(argumentValue11, roundResult9);
          const maxResult13: number | undefined = Math.max(0, minResult4);
          return maxResult13;
        };
        const evaluatedValue27: Partial<DockablePanelGroupState> = {
          x: resolveMaxResult2(),
          y: resolveMaxResult3(),
          width,
          height,
        };
        updateDockableGroup(argumentValue6, evaluatedValue27);
        snapDockableGroup(detached.id);
        focusDockableGroup(detached.id);
      }
    }
    else {
      if (event.moved) {
        snapDockableGroup(groupId);
      }
      focusDockableGroup(groupId);
    }
  }
}
function dockWorkspacePanel(panelId: WorkspacePanelId, dock: DockPosition): void {
  const group = dockableGroups.value.find((candidate) => {
    const includesResult: boolean = candidate.panelIds.includes(panelId);
    return includesResult;
  });
  if (!group) {
    return;
  }
  if (group.panelIds.length > 1) {
    const groupId = detachDockablePanel(panelId);
    if (groupId) {
      dockDockableGroup(groupId, dock);
    }
    return;
  }
  dockDockableGroup(group.id, dock);
}
function combineWorkspacePanel(panelId: WorkspacePanelId, targetGroupId: string): void {
  combineDockablePanel(panelId, targetGroupId);
}
function detachWorkspacePanel(panelId: WorkspacePanelId): void {
  detachDockablePanel(panelId);
}
function closeWorkspacePanel(panelId: WorkspacePanelId): void {
  hideDockablePanel(panelId);
}
watch(() => saveDiagnostics.value.length, (count) => {
  if (count > 0 && dockableDisplayMode.value === 'edit') {
    showDockablePanel('diagnostics');
  }
});
watch([isDebugActive, () => workspaceView.value, () => debugSession.value?.id], (workspaceState) => {
  const active = workspaceState[0];
  const view = workspaceState[1];
  const sessionId = workspaceState[2];
  if (view !== 'editor') {
    return;
  }
  if (active) {
    if (dockableDisplayMode.value !== 'debug') {
      setDockableDisplayMode('debug');
    }
    showDockablePanel('debug');
    if (activeWorkpieceRunId.value) {
      showDockablePanel('workpieces');
    }
  }
  else if (!sessionId && dockableDisplayMode.value === 'debug') {
    setDockableDisplayMode('edit');
  }
}, { immediate: true });
watch(() => debugSession.value?.id, (sessionId, previousSessionId) => {
  if (sessionId !== previousSessionId) {
    selectedDebugNodeId.value = undefined;
    selectedDebugExecutionId.value = undefined;
    workpieceFocusNodeId.value = undefined;
    workpieceFocusExecutionId.value = undefined;
  }
});
// Keep canvas-driven selections in sync with the debug execution list. The
// explicit canvas click handler separately marks manual selections as eligible
// to focus the matching workpiece.
// 同步画布选择与调试执行列表；画布点击处理器另行标记手动选择，使其可以聚焦对应工件。
watch(() => selectedNode.value?.id, (nodeId) => {
  if (!debugSession.value || !nodeId || selectedDebugNodeId.value === nodeId) {
    return;
  }
  selectedDebugNodeId.value = nodeId;
});
watch(() => debugSession.value?.status, (status, previousStatus) => {
  if (previousStatus && status && status !== previousStatus && ['completed', 'cancelled', 'failed'].includes(status)) {
    selectedDebugNodeId.value = undefined;
    selectedDebugExecutionId.value = undefined;
    workpieceFocusNodeId.value = undefined;
    workpieceFocusExecutionId.value = undefined;
    workpieceRefreshRevision.value += 1;
  }
});
const debugRenderedElements = computed(() => {
  const mapResult6: (import("./flow/types").FlowEdge | {
    data: {
      breakpoint: boolean;
      debugPaused: boolean;
      breakpointLocked: boolean;
      onToggleBreakpoint: (nodeId: string) => void;
      kind: NodeKind;
      titleKey: string;
      subtitleKey: string;
      displayName?: string;
      description?: string;
      status: import("./flow/types").NodeStatus;
      hasDataOutput: boolean;
      parameters: MethodParameter[];
      script?: import("./flow/types").ScriptNodeData;
      runtime?: import("./flow/types").NodeRuntimeMetadata;
    };
    id: string;
    position: import("@vue-flow/core").XYPosition;
    width?: number;
    selected?: boolean;
    class?: string | string[];
    type: "workflow";
  })[] = renderedElements.value.map((element) => {
    if (!('data' in element) || !('position' in element)) {
      return element;
    }
    const node = element as FlowNode;
    const evaluatedValue28: import("./flow/types").FlowEdge | {
      data: {
        breakpoint: boolean;
        debugPaused: boolean;
        breakpointLocked: boolean;
        onToggleBreakpoint: (nodeId: string) => void;
        kind: NodeKind;
        titleKey: string;
        subtitleKey: string;
        displayName?: string;
        description?: string;
        status: import("./flow/types").NodeStatus;
        hasDataOutput: boolean;
        parameters: MethodParameter[];
        script?: import("./flow/types").ScriptNodeData;
        runtime?: import("./flow/types").NodeRuntimeMetadata;
      };
      id: string;
      position: import("@vue-flow/core").XYPosition;
      width?: number;
      selected?: boolean;
      class?: string | string[];
      type: "workflow";
    } = {
      ...node,
      data: {
        ...node.data,
        breakpoint: isBreakpoint(node.id),
        debugPaused: debugSession.value?.currentNodeId === node.id,
        breakpointLocked: isDebugActive.value,
        onToggleBreakpoint: toggleBreakpoint,
      },
    };
    return evaluatedValue28;
  });
  return mapResult6;
});
const bindingSource8 = useNodeDrop({ screenToFlowCoordinate, isCanvasDropActive, notice, addNode });
const { handleCanvasDragOver } = bindingSource8;
const { handleCanvasDragLeave } = bindingSource8;
const { handleCanvasDrop } = bindingSource8;
const { handleLibraryNodePointerDown } = bindingSource8;
const { handleBuiltinNodePointerDown } = bindingSource8;
const bindingSource9 = useProjectSession({
  recoveryWorkspace,
  canvases,
  activeCanvasId,
  nextNodeNumber,
  connectionLineTypes,
  canvasFocusSettings,
  projectId,
  flowId,
  projectWorkspaces,
  projectName,
  projectNameDraft,
  projectMenuOpen,
  projectRenameOpen,
  isProjectRenaming,
  projectVersion,
  flowVersion,
  isWorkspaceLoading,
  isSaving,
  isDirty,
  saveFailed,
  saveConflict,
  saveDiagnostics,
  savedWorkspaceFingerprint,
  notice,
  currentWorkspaceSnapshot,
  restoreWorkspace,
  refreshDirtyState,
  markWorkspaceChanged,
  recordWorkspaceMutation,
  clearHistory,
  syncHistoryAvailability,
  localizeEdges,
});
const { beginProjectRename } = bindingSource9;
const { cancelProjectRename } = bindingSource9;
const { submitProjectRename } = bindingSource9;
const { saveFlow } = bindingSource9;
const { openProject: loadProject } = bindingSource9;
const { startNewProject: beginNewProject } = bindingSource9;
const { initializeWorkspace } = bindingSource9;
const { reloadCurrentFlow } = bindingSource9;
const { libraryRefreshRevision } = useWorkspaceSync({
  projectId,
  flowId,
  flowVersion,
  isDirty,
  isSaving,
  isWorkspaceLoading,
  notice,
  reloadCurrentFlow,
  refreshLibraryCatalog,
});
function undo(): void {
  if (undoSnapshot()) {
    notice.value = t('canvas.undoApplied');
  }
}
function redo(): void {
  if (redoSnapshot()) {
    notice.value = t('canvas.redoApplied');
  }
}
function nodeTitle(node: FlowNode): string {
  const evaluatedValue29: string = node.data.displayName?.trim() || t(node.data.titleKey);
  return evaluatedValue29;
}
function sourceNodeTitle(parameter: MethodParameter): string {
  const source = currentCanvas.value.nodes.find((node) => node.id === parameter.sourceNodeId);
  const evaluatedValue30: string = source ? nodeTitle(source) : t('parameter.previousNode');
  return evaluatedValue30;
}
function nodeIdFromDiagnostic(diagnostic: FlowValidationDiagnostic): string | undefined {
  const evaluatedValue31: string | undefined = parseFlowValidationDiagnosticTarget(diagnostic.path)?.nodeId;
  return evaluatedValue31;
}
function dismissSaveDiagnostics(): void {
  saveDiagnostics.value = [];
}
function locateSaveDiagnostic(diagnostic: FlowValidationDiagnostic): void {
  const nodeId = nodeIdFromDiagnostic(diagnostic);
  if (!nodeId) {
    return;
  }
  const canvas = canvases.value.find((candidate) => {
    const someResult2: boolean = candidate.nodes.some((node) => node.id === nodeId);
    return someResult2;
  });
  if (!canvas) {
    return;
  }
  selectCanvas(canvas.id);
  const resolveThenResult3 = () => {
    const nextTickResult3 = nextTick();
    const thenResult3 = nextTickResult3.then(() => {
      const selectNodeResult1: void | PromiseLike<void> = selectNode(nodeId);
      return selectNodeResult1;
    });
    return thenResult3;
  };
  void resolveThenResult3();
}
async function openProjectInEditor(workspace: ProjectWorkspaceDto, requestedFlowId?: string): Promise<void> {
  if (runConsole.value?.confirmLeave() === false) {
    return;
  }
  await loadProject(workspace, requestedFlowId);
  workspaceView.value = 'editor';
}
async function startNewProjectInEditor(): Promise<void> {
  if (runConsole.value?.confirmLeave() === false) {
    return;
  }
  workspaceView.value = 'editor';
  await beginNewProject();
}
function refreshProjectLibraryCatalog(): void {
  void refreshLibraryCatalog(projectId.value);
}
function updateActiveProjectDirectory(workspaces: ProjectWorkspaceDto[]): void {
  projectWorkspaces.value = workspaces;
}
function updateCurrentFlowVersionSummary(version: FlowVersionSummaryDto): void {
  if (!projectId.value || !flowId.value) {
    return;
  }
  projectWorkspaces.value = projectWorkspaces.value.map((workspace) => {
    const evaluatedValue32: {
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
    } = workspace.project.id === projectId.value
      ? {
        ...workspace,
        flows: workspace.flows.map((flow) => flow.id === flowId.value
          ? version.track === 'development'
            ? { ...flow, version: version.version }
            : { ...flow, productionVersion: version.version }
          : flow),
      }
      : workspace;
    return evaluatedValue32;
  });
}
async function publishCurrentFlowVersion(): Promise<void> {
  if (!projectId.value || !flowId.value || !canPublishVersion.value) {
    return;
  }
  isVersionPublishing.value = true;
  try {
    const published = await publishFlowVersion(projectId.value, flowId.value, {
      expectedDevelopmentVersion: flowVersion.value,
    });
    updateCurrentFlowVersionSummary(published);
    notice.value = t('version.published', { version: published.version });
  }
  catch {
    notice.value = t('version.publishFailed');
  }
  finally {
    isVersionPublishing.value = false;
  }
}
async function handleFlowVersionChanged(version: FlowVersionSummaryDto): Promise<void> {
  updateCurrentFlowVersionSummary(version);
  if (version.track === 'development' && projectId.value && flowId.value) {
    const workspace = projectWorkspaces.value.find((item) => item.project.id === projectId.value);
    if (workspace) {
      await loadProject(workspace, flowId.value);
    }
  }
  notice.value = t('version.rolledBack', { version: version.version });
}
function findNode(nodeId: string): FlowNode | undefined {
  const flatMapResult1 = canvases.value.flatMap((canvas) => canvas.nodes);
  const findResult1: FlowNode | undefined = flatMapResult1.find((node) => node.id === nodeId);
  return findResult1;
}
function removeParameterConnections(nodeId: string, parameterIds: ReadonlySet<string>): void {
  for (const canvas of canvases.value) {
    canvas.edges = canvas.edges.filter((edge) => {
      const evaluatedValue33: boolean = edge.data.semantic !== 'data'
        || edge.target !== nodeId
        || !edge.data.targetParameterId
        || !parameterIds.has(edge.data.targetParameterId);
      return evaluatedValue33;
    });
  }
}
function migrateParameterConnections(nodeId: string, targetParameterIdRemap: ReadonlyMap<string, string>): void {
  if (targetParameterIdRemap.size === 0) {
    return;
  }
  for (const canvas of canvases.value) {
    canvas.edges = canvas.edges.map((edge) => {
      const nextParameterId = edge.data.semantic === 'data' && edge.target === nodeId && edge.data.targetParameterId
        ? targetParameterIdRemap.get(edge.data.targetParameterId)
        : undefined;
      if (!nextParameterId) {
        return edge;
      }
      const evaluatedValue34: {
        id: string;
        source: string;
        target: string;
        selected?: boolean | undefined;
        sourceHandle?: string | null | undefined;
        targetHandle?: string | null | undefined;
        type?: string | undefined;
        markerEnd?: string | {
          id?: string | undefined;
          type: import("@vue-flow/core").MarkerType;
          color?: string | undefined;
          width?: number | undefined;
          height?: number | undefined;
          markerUnits?: string | undefined;
          orient?: string | undefined;
          strokeWidth?: number | undefined;
        } | undefined;
        label?: string | undefined;
        labelShowBg?: boolean | undefined;
        labelBgPadding?: [
          number,
          number
        ] | undefined;
        labelBgBorderRadius?: number | undefined;
        class?: string | undefined;
        ariaLabel?: string | undefined;
        data: {
          semantic: import("./flow/types").ConnectionSemantic;
          targetParameterId?: string | undefined;
          branch?: import("./flow/types").ExecutionBranch | undefined;
          lineType?: import("./flow/types").FlowEdgeLineType | undefined;
        };
      } = {
        ...edge,
        targetHandle: parameterHandleFor(nextParameterId),
        data: {
          ...edge.data,
          targetParameterId: nextParameterId,
        },
      };
      return evaluatedValue34;
    });
  }
}
function setNodePublic(nodeId: string, value: boolean): void {
  const node = findNode(nodeId);
  if (!node || node.data.runtime?.isPublic === value) {
    return;
  }
  recordWorkspaceMutation();
  node.data.runtime = { ...(node.data.runtime ?? { category: node.data.kind === 'action' || node.data.kind === 'flipflop' ? 'method' : 'basic' }), isPublic: value };
  markWorkspaceChanged();
}
function setFlowEntry(nodeId: string, value: boolean): void {
  const node = findNode(nodeId);
  if (!node || (value && entryNodeId.value === nodeId) || (!value && entryNodeId.value !== nodeId)) {
    return;
  }
  recordWorkspaceMutation();
  entryNodeId.value = value ? nodeId : '';
  markWorkspaceChanged();
}
function setFlowCallTarget(nodeId: string, canvasId: string, targetNodeId?: string): void {
  const callNode = findNode(nodeId);
  if (!callNode || callNode.data.kind !== 'flowCall') {
    return;
  }
  const targetCanvas = canvases.value.find((canvas) => canvas.id === canvasId);
  const target = targetNodeId ? targetCanvas?.nodes.find((node) => node.id === targetNodeId && node.data.runtime?.isPublic === true) : undefined;
  const mapResult7: Iterable<string> | null | undefined = callNode.data.parameters.map((parameter) => parameter.id);
  const previousParameterIds = new Set(mapResult7);
  recordWorkspaceMutation();
  if (!target) {
    removeParameterConnections(nodeId, previousParameterIds);
    callNode.data.parameters = [];
    callNode.data.runtime = {
      ...(callNode.data.runtime ?? { category: 'basic', returnType: 'System.Object' }),
      targetCanvasId: canvasId || undefined,
      targetNodeId: undefined,
      targetFlowId: undefined,
      flowCallParameterBindings: [],
    };
    markWorkspaceChanged();
    return;
  }
  const mapResult8: Iterable<readonly [
    string,
    string
  ]> | null | undefined = (callNode.data.runtime?.flowCallParameterBindings ?? []).map((binding) => [binding.targetParameterId, binding.callParameterId]);
  const existingBindings = new Map(mapResult8);
  const mapResult9: Iterable<readonly [
    string,
    MethodParameter
  ]> | null | undefined = callNode.data.parameters.map((parameter) => [parameter.id, parameter]);
  const existingInputs = new Map(mapResult9);
  const nextParameters = target.data.parameters.map((parameter) => {
    const callParameterId = existingBindings.get(parameter.id) ?? parameter.id;
    const existing = existingInputs.get(callParameterId);
    return {
      ...parameter,
      id: callParameterId,
      nameKey: parameter.nameKey,
      name: parameter.name,
      source: existing?.source ?? 'literal',
      literalValue: existing?.literalValue ?? '',
      projectInputKey: existing?.projectInputKey,
      expression: existing?.expression,
      sourceNodeId: existing?.sourceNodeId,
      sourcePortId: existing?.sourcePortId,
    };
  });
  const mapResult10: Iterable<string> | null | undefined = nextParameters.map((parameter) => parameter.id);
  const nextIds = new Set(mapResult10);
  const argumentValue7: string = nodeId;
  const filterResult5: Iterable<string> | null | undefined = [...previousParameterIds].filter((id) => {
    const evaluatedValue35: boolean = !nextIds.has(id);
    return evaluatedValue35;
  });
  const setInstance2: ReadonlySet<string> = new Set(filterResult5);
  removeParameterConnections(argumentValue7, setInstance2);
  callNode.data.parameters = nextParameters;
  callNode.data.runtime = {
    ...(callNode.data.runtime ?? { category: 'basic', returnType: 'System.Object' }),
    targetCanvasId: canvasId,
    targetNodeId: target.id,
    targetFlowId: undefined,
    flowCallParameterBindings: nextParameters.map((parameter, index) => ({
      callParameterId: parameter.id,
      targetParameterId: target.data.parameters[index]!.id,
    })),
  };
  markWorkspaceChanged();
}
function addScriptInput(nodeId: string): void {
  const node = findNode(nodeId);
  if (!node || node.data.kind !== 'script') {
    return;
  }
  recordWorkspaceMutation();
  let index = node.data.parameters.length + 1;
  let id = `input-${index}`;
  while (node.data.parameters.some((parameter) => parameter.id === id)) {
    id = `input-${++index}`;
  }
  node.data.parameters.push({
    id,
    nameKey: `input${index}`,
    name: `input${index}`,
    valueKind: 'System.Object',
    type: 'System.Object',
    description: '',
    required: false,
    source: 'literal',
    literalValue: '',
  });
  markWorkspaceChanged();
}
function removeScriptInput(nodeId: string, parameterId: string): void {
  const node = findNode(nodeId);
  if (!node || node.data.kind !== 'script') {
    return;
  }
  recordWorkspaceMutation();
  const argumentValue8: string = nodeId;
  const setInstance3: ReadonlySet<string> = new Set([parameterId]);
  removeParameterConnections(argumentValue8, setInstance3);
  node.data.parameters = node.data.parameters.filter((parameter) => parameter.id !== parameterId);
  markWorkspaceChanged();
}
function setVariadicMode(nodeId: string, parameterId: string, mode: 'expanded' | 'collection'): void {
  const node = findNode(nodeId);
  const parameter = node?.data.parameters.find((item) => item.id === parameterId);
  const groupId = parameter?.variadicGroupId;
  if (!node || !parameter || !groupId) {
    return;
  }
  const conversion = convertVariadicParameterMode(node.data.parameters, groupId, mode);
  if (!conversion.ok) {
    notice.value = t('parameter.variadicConversionBlocked');
    return;
  }
  recordWorkspaceMutation();
  node.data.parameters = conversion.parameters;
  migrateParameterConnections(nodeId, conversion.targetParameterIdRemap);
  markWorkspaceChanged();
}
function addVariadicInput(nodeId: string, parameterId: string): void {
  const node = findNode(nodeId);
  const parameter = node?.data.parameters.find((item) => item.id === parameterId);
  const groupId = parameter?.variadicGroupId;
  if (!node || !parameter || !groupId) {
    return;
  }
  recordWorkspaceMutation();
  const existing = node.data.parameters.filter((item) => item.variadicGroupId === groupId);
  const id = `${groupId}-${existing.length + 1}`;
  node.data.parameters.push({
    ...parameter,
    id,
    nameKey: `${parameter.nameKey} ${existing.length + 1}`,
    name: `${parameter.name ?? parameter.nameKey} ${existing.length + 1}`,
    source: 'literal',
    literalValue: '',
    projectInputKey: undefined,
    expression: undefined,
    sourceNodeId: undefined,
    sourcePortId: undefined,
    variadicMode: 'expanded',
  });
  markWorkspaceChanged();
}
function removeVariadicInput(nodeId: string, parameterId: string): void {
  const node = findNode(nodeId);
  const parameter = node?.data.parameters.find((item) => item.id === parameterId);
  const groupId = parameter?.variadicGroupId;
  if (!node || !parameter || !groupId) {
    return;
  }
  const members = node.data.parameters.filter((item) => item.variadicGroupId === groupId);
  if (members.length <= 1) {
    notice.value = t('parameter.keepVariadicPlaceholder');
    return;
  }
  recordWorkspaceMutation();
  const argumentValue9: string = nodeId;
  const setInstance4: ReadonlySet<string> = new Set([parameterId]);
  removeParameterConnections(argumentValue9, setInstance4);
  node.data.parameters = node.data.parameters.filter((item) => item.id !== parameterId);
  markWorkspaceChanged();
}
watch(projectId, (nextProjectId) => {
  projectLibraryOpen.value = false;
  void refreshLibraryCatalog(nextProjectId);
});
function updateConcurrencyMode(mode: FlowConcurrencyMode): void {
  if (runPolicy.value.concurrencyMode === mode) {
    return;
  }
  recordWorkspaceMutation();
  runPolicy.value = { concurrencyMode: mode };
  markWorkspaceChanged();
}
useWorkspaceShortcuts({ canvasDeleteConfirmOpen, isEditorActive, cancelCanvasRemoval, saveFlow, undo, redo, selectAllNodes });
onMounted(() => {
  void initializeWorkspace();
  void refreshLibraryCatalog();
});
function setLanguage(nextLocale: Locale): void {
  setLocale(nextLocale);
  languageMenuOpen.value = false;
}
const readDivClass = computed(() => ({ 'app-shell--console': workspaceView.value === 'console' }));
function handleCommandBarToggleProjectMenu() {
  projectMenuOpen.value = !projectMenuOpen.value;
}
function handleCommandBarUpdateProjectNameDraft($event: any) {
  projectNameDraft.value = $event;
}
function handleCommandBarToggleLanguageMenu() {
  languageMenuOpen.value = !languageMenuOpen.value;
}
function handleCommandBarShowRunConsole() {
  workspaceView.value = 'console';
}
function handleCommandBarShowVersionHistory() {
  flowVersionHistoryOpen.value = true;
}
const readRunConsoleIf = computed(() => (workspaceView.value === 'console'));
const readMainIf = computed(() => (workspaceView.value === 'editor'));
function readDivItems() {
  return (workspaceDropZones.value);
}
function readDivClass2(zone: TemplateItem<ReturnType<typeof readDivItems>>) {
  return ([`workspace-docking-preview__zone--${zone.id}`, { active: workspaceSnapPreview.value!.zone === zone.id }]);
}
function readTemplateItems() {
  const evaluatedValue36 = (unref(dockableGroups));
  return evaluatedValue36;
}
function readDockablePanelGroupIf(group: TemplateItem<ReturnType<typeof readTemplateItems>>) {
  const evaluatedValue37 = (tabsForGroup(group).length > 0);
  return evaluatedValue37;
}
function readDockablePanelGroupDropTargeted(group: TemplateItem<ReturnType<typeof readTemplateItems>>) {
  return (workspaceSnapPreview.value?.targetGroupId === group.id);
}
function readNodeLibraryPanelIf(panelId: any) {
  return (panelId === 'nodes');
}
function handleNodeLibraryPanelUpdateLibrarySearch($event: any) {
  librarySearch.value = $event;
}
function handleNodeLibraryPanelManage() {
  projectLibraryOpen.value = true;
}
function readCanvasPanelElseIf(panelId: any) {
  return (panelId === 'canvas');
}
function handleCanvasPanelUpdateCustomCanvasNameDraft($event: any) {
  customCanvasNameDraft.value = $event;
}
function handleCanvasPanelToggleConnectionSettings() {
  connectionSettingsOpen.value = !connectionSettingsOpen.value;
}
function readInspectorPanelElseIf(panelId: any) {
  return (panelId === 'inspector');
}
function readOutputPanelElseIf(panelId: any) {
  return (panelId === 'output');
}
function readDivElseIf(panelId: any) {
  return (panelId === 'diagnostics');
}
function readFlowDebugPanelElseIf(panelId: any) {
  const evaluatedValue38 = (panelId === 'debug' && unref(debugSession));
  return evaluatedValue38;
}
function readRunVariablePanelElseIf(panelId: any) {
  return (panelId === 'variables' && activeVariableRunId.value);
}
function readRunWorkpiecePanelElseIf(panelId: any) {
  return (panelId === 'workpieces' && activeWorkpieceRunId.value);
}
const readRunWorkpiecePanelFocusNodeId = computed(() => {
  const evaluatedValue39: string | undefined = (unref(debugSession) ? workpieceFocusNodeId.value : undefined);
  return evaluatedValue39;
});
const readRunWorkpiecePanelFocusExecutionId = computed(() => {
  const evaluatedValue40: string | undefined = (unref(debugSession) ? workpieceFocusExecutionId.value : undefined);
  return evaluatedValue40;
});
const readRunWorkpiecePanelLive = computed(() => {
  const evaluatedValue41: boolean = (unref(isRunning) || unref(isDebugActive));
  return evaluatedValue41;
});
const readProjectLibraryDialogIf = computed(() => (workspaceView.value === 'editor' && projectLibraryOpen.value && projectId.value));
function handleProjectLibraryDialogClose() {
  projectLibraryOpen.value = false;
}
const readFlowVersionHistoryDialogIf = computed(() => (workspaceView.value === 'editor' && flowVersionHistoryOpen.value && projectId.value && flowId.value));
function handleFlowVersionHistoryDialogClose() {
  flowVersionHistoryOpen.value = false;
}
</script>

<template>
  <div
    class="app-shell"
    :class="readDivClass"
  >
    <CommandBar
      :project-name="projectName"
      :flow-version="flowVersion"
      :production-version="productionVersion"
      :project-workspaces="projectWorkspaces"
      :project-id="projectId!"
      :project-menu-open="projectMenuOpen"
      :project-rename-open="projectRenameOpen"
      :project-name-draft="projectNameDraft"
      :is-project-renaming="isProjectRenaming"
      :can-undo="canUndo"
      :can-redo="canRedo"
      :is-dirty="isDirty"
      :is-saving="isSaving"
      :is-workspace-loading="isWorkspaceLoading"
      :is-running="isRunning"
      :is-debug-active="isDebugActive"
      :is-debug-paused="isDebugPaused"
      :is-debug-starting="isDebugStarting"
      :is-debug-controlling="isDebugControlling"
      :is-debug-stopping="isDebugStopping"
      :can-start-debug="canStartDebug"
      :can-view-versions="canViewVersions"
      :can-manage-versions="canPublishVersion"
      :language-menu-open="languageMenuOpen"
      :locale="locale"
      :node-count="nodes.length"
      :workspace-view="workspaceView"
      :concurrency-mode="runPolicy.concurrencyMode"
      :workspace-panel-items="panelSwitcherItems"
      :workspace-display-mode="dockableDisplayMode"
      :debug-display-mode-available="canUseDebugDisplayMode"
      @toggle-project-menu="handleCommandBarToggleProjectMenu()"
      @begin-project-rename="beginProjectRename"
      @cancel-project-rename="cancelProjectRename"
      @submit-project-rename="submitProjectRename"
      @update:project-name-draft="handleCommandBarUpdateProjectNameDraft($event)"
      @open-project="openProjectInEditor"
      @start-new-project="startNewProjectInEditor"
      @undo="undo"
      @redo="redo"
      @save="saveFlow"
      @run="runFlow"
      @debug="startDebug"
      @debug-continue="continueDebug"
      @debug-step="stepDebug"
      @debug-stop="stopDebug"
      @toggle-language-menu="handleCommandBarToggleLanguageMenu()"
      @set-language="setLanguage"
      @show-run-console="handleCommandBarShowRunConsole()"
      @show-version-history="handleCommandBarShowVersionHistory()"
      @publish-version="publishCurrentFlowVersion"
      @update-concurrency-mode="updateConcurrencyMode"
      @toggle-workspace-panel="toggleWorkspacePanel"
      @reset-workspace-layout="resetDockableLayout"
      @change-workspace-mode="changeWorkspaceDisplayMode"
     />
    <RunConsole
      v-if="readRunConsoleIf"
      ref="runConsole"
      :project-workspaces="projectWorkspaces"
      @open-flow="openProjectInEditor"
      @start-new-project="startNewProjectInEditor"
      @project-directory-changed="updateActiveProjectDirectory"
     />
    <main
      v-if="readMainIf"
      ref="workspaceRoot"
      class="workspace-grid"
    >
      <div
        v-if="workspaceSnapPreview"
        class="workspace-docking-preview"
        aria-hidden="true"
      >
        <div
          v-for="zone in readDivItems()"
          :key="zone.id"
          class="workspace-docking-preview__zone"
          :class="readDivClass2(zone)"
          :style="zone.style"
        ></div>
        <div
          v-if="snapPreviewLabel"
          class="workspace-docking-preview__label"
          :style="snapPreviewLabelStyle"
        >{{ snapPreviewLabel }}</div>
      </div>
      <template
        v-for="group in readTemplateItems()"
        :key="group.id"
      >
        <DockablePanelGroup
          v-if="readDockablePanelGroupIf(group)"
          :group="group"
          :tabs="tabsForGroup(group)"
          :group-options="groupOptionsFor(group.id)"
          :workspace-size="dockableWorkspaceSize"
          :canvas-group="group.panelIds.includes('canvas')"
          :docked-offset="dockedOffsetFor(group)"
          :docked-size="dockedExtentFor(group)"
          :layout-priority="dockedLayoutPriority"
          :canvas-insets="workspaceInsets"
          :drop-targeted="readDockablePanelGroupDropTargeted(group)"
          :split-targets="true"
          :drag-individual-tabs="true"
          @update:group="updateWorkspaceGroup(group.id, $event)"
          @activate="activateWorkspacePanel"
          @drag-preview="previewWorkspaceMove(group.id, $event)"
          @move-end="finishWorkspaceMove(group.id, $event)"
          @dock="dockWorkspacePanel"
          @combine="combineWorkspacePanel"
          @detach="detachWorkspacePanel"
          @close="closeWorkspacePanel"
        >
          <template #default="{ panelId }">
            <NodeLibraryPanel
              v-if="readNodeLibraryPanelIf(panelId)"
              :embedded="true"
              :mobile-visible="false"
              :library-search="librarySearch"
              :is-loading="isLibraryCatalogLoading"
              :error="libraryCatalogError"
              :visible-libraries="visibleLibraries"
              :visible-builtin-nodes="visibleBuiltinNodes"
              :catalog-node-count="catalogNodeCount"
              @update:library-search="handleNodeLibraryPanelUpdateLibrarySearch($event)"
              @manage="handleNodeLibraryPanelManage()"
              @retry="refreshProjectLibraryCatalog"
              @node-pointer-down="handleLibraryNodePointerDown"
              @builtin-node-pointer-down="handleBuiltinNodePointerDown"
              @collapse="closeWorkspacePanel('nodes')"
             />
            <CanvasPanel
              v-else-if="readCanvasPanelElseIf(panelId)"
              :canvases="canvases"
              :active-canvas-id="activeCanvasId"
              :current-canvas-lifecycle="currentCanvas.lifecycle"
              :current-canvas-node-count="currentCanvas.nodes.length"
              :current-canvas-edge-count="currentCanvas.edges.length"
              :rendered-elements="debugRenderedElements"
              :is-valid-connection="isValidConnection"
              :canvas-render-key="canvasRenderKey"
              :canvas-menu-open="canvasMenuOpen"
              :available-canvas-lifecycles="availableCanvasLifecycles"
              :custom-canvas-name-draft="customCanvasNameDraft"
              :connection-settings-open="connectionSettingsOpen"
              :connection-line-types="connectionLineTypes"
              :canvas-focus-settings="canvasFocusSettings"
              :is-dirty="isDirty"
              :is-saving="isSaving"
              :is-workspace-loading="isWorkspaceLoading"
              :save-failed="saveFailed"
              :save-conflict="saveConflict"
              :save-state-key="saveStateKey"
              :selected-node="selectedNode"
              :selected-edge="selectedEdge"
              :selected-node-count="selectedNodeCount"
              :selected-edge-count="selectedEdgeCount"
              :is-canvas-drop-active="isCanvasDropActive"
              :notice="notice"
              :pending-canvas-delete="pendingCanvasDelete"
              :canvas-delete-confirm-open="canvasDeleteConfirmOpen"
              :canvas-label="canvasLabel"
              @select-canvas="selectCanvas"
              @reorder-canvases="reorderCanvas"
              @toggle-canvas-menu="toggleCanvasMenu"
              @add-canvas="addCanvas"
              @add-custom-canvas="addCustomCanvas"
              @update:custom-canvas-name-draft="handleCanvasPanelUpdateCustomCanvasNameDraft($event)"
              @toggle-connection-settings="handleCanvasPanelToggleConnectionSettings()"
              @update-connection-line-type="updateConnectionLineType"
              @update-canvas-focus-setting="updateCanvasFocusSetting"
              @remove-selection="removeSelection"
              @select-all-nodes="selectAllNodes"
              @request-canvas-removal="requestCanvasRemoval"
              @cancel-canvas-removal="cancelCanvasRemoval"
              @confirm-canvas-removal="confirmCanvasRemoval"
              @canvas-dragenter="handleCanvasDragOver"
              @canvas-dragover="handleCanvasDragOver"
              @canvas-dragleave="handleCanvasDragLeave"
              @canvas-drop="handleCanvasDrop"
              @connect="onConnect"
              @edge-update="onEdgeUpdate"
              @nodes-change="onNodesChange"
              @edges-change="onEdgesChange"
              @node-click="handleCanvasNodeClick"
              @edge-click="onEdgeClick"
              @pane-click="clearSelection"
              @zoom-in="zoomIn"
              @zoom-out="zoomOut"
              @fit-view="fitView"
             />
            <InspectorPanel
              v-else-if="readInspectorPanelElseIf(panelId)"
              :embedded="true"
              :mobile-visible="false"
              :selected-node="selectedNode"
              :selected-edge="selectedEdge"
              :icon-for-node-kind="iconForNodeKind"
              :node-title="nodeTitle"
              :source-node-title="sourceNodeTitle"
              :canvases="canvases"
              :entry-node-id="entryNodeId"
              @close="closeWorkspacePanel('inspector')"
              @delete="removeSelection"
              @update-parameter-source="updateParameterSource"
              @begin-text-edit="beginTextEdit"
              @commit-text-edit="commitTextEdit"
              @discard-text-edit="discardTextEdit"
              @set-node-public="setNodePublic"
              @set-flow-entry="setFlowEntry"
              @set-flowcall-target="setFlowCallTarget"
              @add-script-input="addScriptInput"
              @remove-script-input="removeScriptInput"
              @set-variadic-mode="setVariadicMode"
              @add-variadic-input="addVariadicInput"
              @remove-variadic-input="removeVariadicInput"
             />
            <OutputPanel
              v-else-if="readOutputPanelElseIf(panelId)"
              v-model:active-output="visibleActiveOutput"
              :embedded="true"
              :run-events="visibleRunEvents"
              :run-payload="visibleRunPayload"
              :has-run-output="visibleHasRunOutput"
             />
            <div
              v-else-if="readDivElseIf(panelId)"
              class="workspace-panel-content workspace-panel-content--diagnostics"
            >
              <FlowValidationDiagnostics
                v-if="saveDiagnostics.length"
                :diagnostics="saveDiagnostics"
                :canvases="canvases"
                @dismiss="dismissSaveDiagnostics"
                @locate="locateSaveDiagnostic"
               />
              <div
                v-else
                class="workspace-panel-empty"
              >
                <AlertTriangle :size="18" />
                <strong>{{ t('diagnostics.empty') }}</strong>
              </div>
            </div>
            <FlowDebugPanel
              v-else-if="readFlowDebugPanelElseIf(panelId)"
              :session="debugSession"
              :boundary="pauseBoundary"
              :executions="debugExecutionStates"
              :node-names="debugNodeNames"
              :selected-node-id="selectedDebugNodeId"
              :selected-execution-id="selectedDebugExecutionId"
              :is-controlling="isDebugControlling"
              :is-stopping="isDebugStopping"
              :embedded="true"
              @continue="continueDebug"
              @step="stepDebug"
              @stop="stopDebug"
              @inspect="inspectDebugNode"
              @select-execution="focusDebugExecution"
              @close="closeWorkspacePanel('debug')"
             />
            <RunVariablePanel
              v-else-if="readRunVariablePanelElseIf(panelId)"
              :run-id="activeVariableRunId"
              :live="variableRunIsLive"
              :embedded="true"
             />
            <RunWorkpiecePanel
              v-else-if="readRunWorkpiecePanelElseIf(panelId)"
              :run-id="activeWorkpieceRunId"
              :focus-node-id="readRunWorkpiecePanelFocusNodeId"
              :focus-execution-id="readRunWorkpiecePanelFocusExecutionId"
              :refresh-signal="workpieceRefreshRevision"
              :live="readRunWorkpiecePanelLive"
              :embedded="true"
              @select-node="focusDebugWorkpieceNode"
             />
          </template>
        </DockablePanelGroup>
      </template>
    </main>
    <ProjectLibraryDialog
      v-if="readProjectLibraryDialogIf"
      :project-id="projectId!"
      :project-name="projectName"
      :flows="currentProjectFlows"
      :refresh-revision="libraryRefreshRevision"
      @close="handleProjectLibraryDialogClose()"
      @changed="replaceProjectLibraries"
     />
    <FlowVersionHistoryDialog
      v-if="readFlowVersionHistoryDialogIf"
      :project-id="projectId!"
      :flow-id="flowId!"
      :development-version="flowVersion"
      :production-version="productionVersion"
      :can-mutate="canMutateVersions"
      @close="handleFlowVersionHistoryDialogClose()"
      @changed="handleFlowVersionChanged"
     />
  </div>
</template>
