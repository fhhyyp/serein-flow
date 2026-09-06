<script setup lang="ts">
import { computed, markRaw, nextTick, onBeforeUnmount, onMounted, provide, reactive, ref, watch } from 'vue'
import {
  Activity,
  AlertTriangle,
  Bug,
  Code2,
  Database,
  LayoutGrid,
  PackageOpen,
  PanelLeft,
  PanelRight,
  Terminal,
  Zap,
} from 'lucide-vue-next'
import { useVueFlow } from '@vue-flow/core'
import '@vue-flow/core/dist/style.css'
import '@vue-flow/core/dist/theme-default.css'
import NodeLibraryPanel from './components/library/NodeLibraryPanel.vue'
import ProjectLibraryDialog from './components/library/ProjectLibraryDialog.vue'
import CommandBar from './components/workspace/CommandBar.vue'
import DockablePanelGroup from './components/workspace/DockablePanelGroup.vue'
import FlowVersionHistoryDialog from './components/workspace/FlowVersionHistoryDialog.vue'
import OutputPanel from './components/workspace/OutputPanel.vue'
import CanvasPanel from './components/canvas/CanvasPanel.vue'
import FlowValidationDiagnostics from './components/canvas/FlowValidationDiagnostics.vue'
import InspectorPanel from './components/inspector/InspectorPanel.vue'
import RunConsole from './components/runs/RunConsole.vue'
import FlowDebugPanel from './components/workspace/FlowDebugPanel.vue'
import RunWorkpiecePanel from './components/runs/RunWorkpiecePanel.vue'
import {
  publishFlowVersion,
  type FlowConcurrencyMode,
  type FlowValidationDiagnostic,
  type FlowVersionSummaryDto,
  type ProjectWorkspaceDto,
} from './api/flowApi'
import { locale, setLocale, t, type Locale } from './i18n'
import {
  normalizeConnectionLineTypes,
  type ConnectionLineSettings,
} from './flow/connectionLine'
import {
  normalizeCanvasFocusSettings,
  type CanvasFocusSettings,
} from './flow/canvasFocus'
import { createInitialCanvases } from './flow/initialCanvases'
import { parameterHandleFor } from './flow/connectionSeats'
import { libraryNameResolverKey } from './flow/libraryNameResolver'
import { convertVariadicParameterMode } from './flow/variadicParameters'
import { parseFlowValidationDiagnosticTarget } from './flow/validationDiagnostics'
import { cloneWorkspaceSnapshot, workspaceFingerprint, type WorkspaceSnapshot } from './flow/workspaceHistory'
import { loadWorkspace } from './flow/workspaceStorage'
import { useWorkspaceHistory } from './composables/useWorkspaceHistory'
import { useLibraryCatalog } from './composables/useLibraryCatalog'
import { useFlowRunner } from './composables/useFlowRunner'
import { useFlowDebugger } from './composables/useFlowDebugger'
import { useCanvasManager } from './composables/useCanvasManager'
import { useProjectSession } from './composables/useProjectSession'
import { useFlowGraph } from './composables/useFlowGraph'
import { useNodeDrop } from './composables/useNodeDrop'
import { useWorkspaceShortcuts } from './composables/useWorkspaceShortcuts'
import { useDockableWorkspace } from './composables/useDockableWorkspace'
import { workspaceToFlowDefinition } from './flow/flowDtoMapper'
import type { NodeExecutionState } from './flow/nodeExecutionState'
import type {
  DockablePanelGroupState,
  DockableWorkspaceMode,
  DockPosition,
  WorkspacePanelId,
  WorkspacePanelTab,
} from './flow/dockableWorkspace'
import type {
  CanvasState,
  FlowNode,
  MethodParameter,
  NodeKind,
} from './flow/types'

const { zoomIn, zoomOut, fitView, screenToFlowCoordinate } = useVueFlow('workspace-editor')

const recoveryWorkspace = loadWorkspace()
const canvases = ref<CanvasState[]>(createInitialCanvases())
const connectionLineTypes = reactive<ConnectionLineSettings>(normalizeConnectionLineTypes(recoveryWorkspace?.connectionLineTypes))
const canvasFocusSettings = reactive<CanvasFocusSettings>(normalizeCanvasFocusSettings(recoveryWorkspace?.canvasFocusSettings))
const activeCanvasId = ref('main')
const projectLibraryOpen = ref(false)
const mobilePanel = ref<'nodes' | 'inspector' | null>(null)
const languageMenuOpen = ref(false)
const projectMenuOpen = ref(false)
const connectionSettingsOpen = ref(false)
const isCanvasDropActive = ref(false)
const notice = ref('')
let noticeTimer: number | undefined
watch(notice, (message) => {
  if (noticeTimer !== undefined) {
    window.clearTimeout(noticeTimer)
    noticeTimer = undefined
  }
  if (!message) return
  noticeTimer = window.setTimeout(() => {
    if (notice.value === message) notice.value = ''
    noticeTimer = undefined
  }, 3_500)
})
onBeforeUnmount(() => {
  if (noticeTimer !== undefined) window.clearTimeout(noticeTimer)
})
const {
  librarySearch,
  libraries,
  isLibraryCatalogLoading,
  libraryCatalogError,
  visibleLibraries,
  visibleBuiltinNodes,
  catalogNodeCount,
  refreshLibraryCatalog,
  replaceProjectLibraries,
} = useLibraryCatalog()
provide(libraryNameResolverKey, (runtime) => {
  const explicitName = runtime?.flowLibraryName?.trim()
  if (explicitName) {
    return explicitName
  }

  const library = libraries.value.find((candidate) => candidate.id === runtime?.libraryId)
  const catalogNode = library?.nodes.find((candidate) =>
    candidate.contractId === runtime?.libraryNodeContractId)
    ?? library?.nodes.find((candidate) =>
      candidate.className === runtime?.className
      && candidate.methodName === runtime?.methodName
      && candidate.dllName === runtime?.dllName
      && candidate.dllVersion === runtime?.dllVersion)
  return catalogNode?.flowLibraryName?.trim() || undefined
})
const nextNodeNumber = ref(1)
const isDirty = ref(false)
const saveFailed = ref(false)
const saveConflict = ref(false)
const saveDiagnostics = ref<FlowValidationDiagnostic[]>([])
const isWorkspaceLoading = ref(true)
const isSaving = ref(false)
const projectId = ref<string>()
const flowId = ref<string>()
const projectWorkspaces = ref<ProjectWorkspaceDto[]>([])
const canvasMountRevision = ref(0)
const projectName = ref(recoveryWorkspace?.projectName?.trim() || t('project.newProject'))
const projectNameDraft = ref('')
const projectRenameOpen = ref(false)
const isProjectRenaming = ref(false)
const projectVersion = ref(1)
const flowVersion = ref(1)
const entryNodeId = ref(recoveryWorkspace?.entryNodeId ?? '')
const savedWorkspaceFingerprint = ref('')
const isRestoringWorkspace = ref(false)
const isSwitchingCanvas = ref(false)
const workspaceView = ref<'console' | 'editor'>('console')
const workspaceRoot = ref<HTMLElement>()
const {
  layout: dockableLayout,
  displayMode: dockableDisplayMode,
  groups: dockableGroups,
  workspaceSize: dockableWorkspaceSize,
  updateGroup: updateDockableGroup,
  activatePanel: activateDockablePanel,
  showPanel: showDockablePanel,
  hidePanel: hideDockablePanel,
  dockGroup: dockDockableGroup,
  combinePanel: combineDockablePanel,
  detachPanel: detachDockablePanel,
  resetLayout: resetDockableLayout,
  setDisplayMode: setDockableDisplayMode,
  focusGroup: focusDockableGroup,
} = useDockableWorkspace(workspaceRoot)
const runPolicy = ref<{ concurrencyMode: FlowConcurrencyMode }>({ concurrencyMode: 'parallel' })
const flowVersionHistoryOpen = ref(false)
const isVersionPublishing = ref(false)

const currentProjectFlows = computed(() =>
  projectId.value
    ? projectWorkspaces.value.find((workspace) => workspace.project.id === projectId.value)?.flows ?? []
    : [])
const productionVersion = computed(() => currentProjectFlows.value.find((flow) => flow.id === flowId.value)?.productionVersion)
const canViewVersions = computed(() => Boolean(projectId.value && flowId.value && !isWorkspaceLoading.value))
const canMutateVersions = computed(() => canViewVersions.value
  && !isDirty.value
  && !isSaving.value
  && !isDebugActive.value
  && !isVersionPublishing.value)
const canPublishVersion = computed(() => canMutateVersions.value && !flowVersionHistoryOpen.value)

function iconForNodeKind(kind: NodeKind) {
  if (kind === 'flipflop') {
    return Zap
  }

  if (kind === 'script') {
    return Code2
  }

  if (kind === 'flowCall') {
    return Activity
  }

  return Database
}

const currentCanvas = computed<CanvasState>(() => canvases.value.find((canvas) => canvas.id === activeCanvasId.value) ?? canvases.value[0]!)
const canvasRenderKey = computed(() => `${activeCanvasId.value}:${canvasMountRevision.value}`)
const saveStateKey = computed(() => {
  if (saveConflict.value) {
    return 'canvas.saveConflict'
  }

  if (saveFailed.value) {
    return 'canvas.saveFailed'
  }

  if (isWorkspaceLoading.value || isSaving.value) {
    return 'canvas.saving'
  }

  return isDirty.value ? 'canvas.unsaved' : 'canvas.saved'
})
function localizeEdges(): void {
  for (const canvas of canvases.value) {
    canvas.edges = canvas.edges.map((edge) => ({
      ...edge,
      label: undefined,
      labelShowBg: false,
      ariaLabel: edge.data?.semantic === 'execution' ? t('inspector.executionEdge') : t('inspector.dataEdge'),
    }))
  }
}

watch(locale, localizeEdges, { immediate: true })

function currentWorkspaceSnapshot(): WorkspaceSnapshot {
  const hasEntryNode = canvases.value.some((canvas) => canvas.nodes.some((node) => node.id === entryNodeId.value))
  return cloneWorkspaceSnapshot({
    canvases: canvases.value,
    activeCanvasId: activeCanvasId.value,
    nextNodeNumber: nextNodeNumber.value,
    entryNodeId: hasEntryNode ? entryNodeId.value : '',
    projectName: projectName.value,
    connectionLineTypes: { ...connectionLineTypes },
    canvasFocusSettings: { ...canvasFocusSettings },
    runPolicy: { ...runPolicy.value },
  })
}

function refreshDirtyState(): void {
  isDirty.value = workspaceFingerprint(currentWorkspaceSnapshot()) !== savedWorkspaceFingerprint.value
  if (isDirty.value) {
    saveFailed.value = false
  }
}

function markWorkspaceChanged(): void {
  if (saveDiagnostics.value.length) {
    saveDiagnostics.value = []
  }
  void nextTick().then(refreshDirtyState)
}

function restoreWorkspace(snapshot: WorkspaceSnapshot): void {
  isRestoringWorkspace.value = true
  canvasMountRevision.value += 1
  canvases.value = snapshot.canvases
  if (snapshot.projectName?.trim()) {
    projectName.value = snapshot.projectName.trim()
  }
  Object.assign(connectionLineTypes, normalizeConnectionLineTypes(snapshot.connectionLineTypes))
  Object.assign(canvasFocusSettings, normalizeCanvasFocusSettings(snapshot.canvasFocusSettings))
  runPolicy.value = snapshot.runPolicy ?? { concurrencyMode: 'parallel' }
  entryNodeId.value = snapshot.entryNodeId ?? ''
  activeCanvasId.value = snapshot.activeCanvasId
  nextNodeNumber.value = snapshot.nextNodeNumber
  mobilePanel.value = null
  void nextTick().then(() => {
    isRestoringWorkspace.value = false
    refreshDirtyState()
  })
}

const {
  canUndo,
  canRedo,
  recordWorkspaceMutation,
  undo: undoSnapshot,
  redo: redoSnapshot,
  beginTextEdit,
  commitTextEdit,
  discardTextEdit,
  syncHistoryAvailability,
  clear: clearHistory,
} = useWorkspaceHistory({
  currentSnapshot: currentWorkspaceSnapshot,
  restoreSnapshot: restoreWorkspace,
  markWorkspaceChanged,
})

const {
  canvasMenuOpen,
  customCanvasNameDraft,
  canvasDeleteConfirmOpen,
  pendingCanvasDelete,
  availableCanvasLifecycles,
  canvasLabel,
  selectCanvas,
  addCanvas,
  toggleCanvasMenu,
  addCustomCanvas,
  requestCanvasRemoval,
  cancelCanvasRemoval,
  confirmCanvasRemoval,
} = useCanvasManager({
  canvases,
  activeCanvasId,
  currentCanvas,
  mobilePanel,
  notice,
  isSwitchingCanvas,
  recordWorkspaceMutation,
  markWorkspaceChanged,
})

const {
  nodes,
  renderedElements,
  selectedNode,
  selectedEdge,
  selectNode,
  onNodeClick,
  onEdgeClick,
  clearSelection,
  isValidConnection,
  updateConnectionLineType,
  updateCanvasFocusSetting,
  onConnect,
  onNodesChange,
  onEdgesChange,
  updateParameterSource,
  addNode,
  removeSelection,
} = useFlowGraph({
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
})

const {
  isRunning,
  activeOutput,
  runEvents,
  runPayload,
  runFlow,
  lastRunId,
} = useFlowRunner({ nodes, notice, projectId, flowId, flowVersion })

function focusDebugCanvasNode(nodeId: string): void {
  showDockablePanel('workpieces')
  const canvas = canvases.value.find((candidate) => candidate.nodes.some((node) => node.id === nodeId))
  if (!canvas) return
  if (canvas.id !== activeCanvasId.value) selectCanvas(canvas.id)
  void nextTick().then(() => selectNode(nodeId))
}

function handleCanvasNodeClick(event: { node: { id: string } }): void {
  onNodeClick(event)
  if (!debugSession.value) return
  const execution = debugExecutionStates.value.find((state) => state.nodeId === event.node.id)
  selectedDebugNodeId.value = event.node.id
  selectedDebugExecutionId.value = execution?.id
  workpieceFocusNodeId.value = event.node.id
  workpieceFocusExecutionId.value = execution?.executionId
  showDockablePanel('workpieces')
}

function focusDebugWorkpieceNode(nodeId: string, executionId?: string): void {
  if (!debugSession.value) return
  const execution = executionId
    ? debugExecutionStates.value.find((state) => state.id === executionId || state.executionId === executionId)
    : debugExecutionStates.value.find((state) => state.nodeId === nodeId)
  selectedDebugNodeId.value = nodeId
  selectedDebugExecutionId.value = execution?.id
  workpieceFocusNodeId.value = nodeId
  workpieceFocusExecutionId.value = executionId ?? execution?.executionId
  showDockablePanel('debug')
  focusDebugCanvasNode(nodeId)
}

function focusDebugExecution(execution: NodeExecutionState): void {
  if (!debugSession.value) return
  selectedDebugNodeId.value = execution.nodeId
  selectedDebugExecutionId.value = execution.id
  workpieceFocusNodeId.value = execution.nodeId
  workpieceFocusExecutionId.value = execution.executionId
  showDockablePanel('debug')
  focusDebugCanvasNode(execution.nodeId)
}

function handleDebugPause(nodeId: string, executionId?: string): void {
  selectedDebugNodeId.value = nodeId
  const execution = executionId
    ? debugExecutionStates.value.find((state) => state.id === executionId || state.executionId === executionId)
    : debugExecutionStates.value.find((state) => state.nodeId === nodeId)
  selectedDebugExecutionId.value = execution?.id
  workpieceFocusNodeId.value = nodeId
  workpieceFocusExecutionId.value = executionId ?? execution?.executionId
  focusDebugCanvasNode(nodeId)
  workpieceRefreshRevision.value += 1
}

const {
  isBreakpoint,
  toggleBreakpoint,
  debugSession,
  pauseBoundary,
  runEvents: debugRunEvents,
  executionStates: debugExecutionStates,
  runPayload: debugRunPayload,
  isStarting: isDebugStarting,
  isControlling: isDebugControlling,
  isStopping: isDebugStopping,
  isDebugActive,
  isDebugPaused,
  canStartDebug,
  startDebug,
  continueDebug,
  stepDebug,
  stopDebug,
} = useFlowDebugger({
  canvases,
  projectId,
  flowId,
  flowVersion,
  getCurrentDefinition: () => {
    if (!flowId.value) return undefined
    return workspaceToFlowDefinition(currentWorkspaceSnapshot(), {
      id: flowId.value,
      version: flowVersion.value,
    })
  },
  isWorkspaceLoading,
  isNormalRunActive: isRunning,
  notice,
  onPauseNode: handleDebugPause,
})

const debugNodeNames = computed<Record<string, string>>(() => Object.fromEntries(
  canvases.value.flatMap((canvas) => canvas.nodes.map((node) => [
    node.id,
    node.data.displayName?.trim() || t(node.data.titleKey),
  ])),
))

const visibleRunEvents = computed(() => isDebugActive.value || (!isRunning.value && debugSession.value) ? debugRunEvents.value : runEvents.value)
const visibleRunPayload = computed(() => isDebugActive.value || (!isRunning.value && debugSession.value) ? debugRunPayload.value : runPayload.value)
const visibleHasRunOutput = computed(() => visibleRunEvents.value.length > 0)
const visibleActiveOutput = computed({
  get: () => activeOutput.value,
  set: (value: 'events' | 'payload') => { activeOutput.value = value },
})

const activeWorkpieceRunId = computed(() => debugSession.value?.runId ?? lastRunId.value ?? '')
const selectedDebugNodeId = ref<string>()
const selectedDebugExecutionId = ref<string>()
const workpieceFocusNodeId = ref<string>()
const workpieceFocusExecutionId = ref<string>()
const workpieceRefreshRevision = ref(0)
const canUseDebugDisplayMode = computed(() => Boolean(flowId.value))
const panelDefinitions = computed<WorkspacePanelTab[]>(() => [
  { id: 'canvas', label: t('panel.canvas'), icon: markRaw(LayoutGrid), closable: false },
  { id: 'nodes', label: t('panel.nodes'), icon: markRaw(PanelLeft) },
  { id: 'inspector', label: t('panel.inspector'), icon: markRaw(PanelRight) },
  { id: 'output', label: t('panel.output'), icon: markRaw(Terminal) },
  { id: 'diagnostics', label: t('panel.diagnostics'), icon: markRaw(AlertTriangle) },
  { id: 'debug', label: t('panel.debug'), icon: markRaw(Bug) },
  { id: 'workpieces', label: t('panel.workpieces'), icon: markRaw(PackageOpen) },
])

function isPanelAvailable(panelId: WorkspacePanelId): boolean {
  if (panelId === 'debug') return Boolean(debugSession.value)
  if (panelId === 'workpieces') return Boolean(activeWorkpieceRunId.value)
  return true
}

function tabsForGroup(group: DockablePanelGroupState): WorkspacePanelTab[] {
  return group.panelIds
    .filter((panelId) => dockableLayout.panels[panelId].visible && isPanelAvailable(panelId))
    .map((panelId) => panelDefinitions.value.find((definition) => definition.id === panelId))
    .filter((definition): definition is WorkspacePanelTab => Boolean(definition))
}

function groupOptionsFor(groupId: string): Array<{ id: string; label: string }> {
  return dockableGroups.value
    .filter((group) => group.id !== groupId && tabsForGroup(group).length > 0)
    .map((group) => ({
      id: group.id,
      label: panelDefinitions.value.find((definition) => definition.id === group.activePanelId)?.label
        ?? t('panel.emptyGroup'),
    }))
}

type EdgeDockPosition = Exclude<DockPosition, 'free' | 'fill'>
type WorkspaceDropZone = EdgeDockPosition | 'fill'

interface WorkspaceRect {
  left: number
  top: number
  right: number
  bottom: number
}

interface WorkspaceSnapPreview {
  groupId: string
  dock?: EdgeDockPosition
  targetGroupId?: string
  region?: WorkspaceRect
  zone?: WorkspaceDropZone
}

function isEdgeDockPosition(dock: DockPosition): dock is EdgeDockPosition {
  return dock === 'left' || dock === 'right' || dock === 'top' || dock === 'bottom'
}

function desiredEdgeGroupExtent(group: DockablePanelGroupState, dock: EdgeDockPosition): number {
  if (group.collapsed) return 76
  const raw = dock === 'left' || dock === 'right' ? group.width : group.height
  const maximum = dock === 'left' || dock === 'right'
    ? dockableWorkspaceSize.width
    : dockableWorkspaceSize.height
  return Math.max(0, Math.min(raw, maximum))
}

const renderedEdgeGroups = computed(() => dockableGroups.value.filter((group) =>
  isEdgeDockPosition(group.dock) && tabsForGroup(group).length > 0))

const dockedLayoutPriority = computed<'full-row' | 'center-column'>(() =>
  renderedEdgeGroups.value.some((group) => group.dock === 'top' || group.dock === 'bottom')
    ? 'full-row'
    : 'center-column')

function dockedExtentFor(group: DockablePanelGroupState): number | undefined {
  if (!isEdgeDockPosition(group.dock)) return undefined
  const dock = group.dock
  const peers = renderedEdgeGroups.value.filter((candidate) => candidate.dock === dock)
  const total = peers.reduce((sum, candidate) => sum + desiredEdgeGroupExtent(candidate, dock), 0)
  const capacity = dock === 'left' || dock === 'right'
    ? dockableWorkspaceSize.width
    : dockableWorkspaceSize.height
  const scale = total > capacity && total > 0 ? capacity / total : 1
  return desiredEdgeGroupExtent(group, dock) * scale
}

function dockedOffsetFor(group: DockablePanelGroupState): number {
  if (!isEdgeDockPosition(group.dock)) return 0
  const dock = group.dock
  const peers = renderedEdgeGroups.value.filter((candidate) => candidate.dock === dock)
  const groupIndex = peers.findIndex((candidate) => candidate.id === group.id)
  return peers.slice(0, Math.max(0, groupIndex)).reduce(
    (offset, candidate) => offset + (dockedExtentFor(candidate) ?? 0),
    0,
  )
}

const workspaceInsets = computed(() => renderedEdgeGroups.value.reduce((insets, group) => {
  const extent = dockedExtentFor(group) ?? 0
  if (group.dock === 'left') insets.left += extent
  if (group.dock === 'right') insets.right += extent
  if (group.dock === 'top') insets.top += extent
  if (group.dock === 'bottom') insets.bottom += extent
  return insets
}, { top: 0, right: 0, bottom: 0, left: 0 }))

function workspaceRectForGroup(group: DockablePanelGroupState): WorkspaceRect {
  const width = Math.max(240, Math.min(group.width, dockableWorkspaceSize.width))
  const height = Math.max(160, Math.min(group.height, dockableWorkspaceSize.height))
  return {
    left: group.x,
    top: group.y,
    right: group.x + width,
    bottom: group.y + height,
  }
}

function rectArea(rect: WorkspaceRect): number {
  return Math.max(0, rect.right - rect.left) * Math.max(0, rect.bottom - rect.top)
}

function rectOverlap(first: WorkspaceRect, second: WorkspaceRect): number {
  return Math.max(0, Math.min(first.right, second.right) - Math.max(first.left, second.left))
    * Math.max(0, Math.min(first.bottom, second.bottom) - Math.max(first.top, second.top))
}

function containsPoint(rect: WorkspaceRect, clientX: number, clientY: number): boolean {
  return clientX >= rect.left && clientX <= rect.right && clientY >= rect.top && clientY <= rect.bottom
}

function blankRegionForPointer(groupId: string, clientX: number, clientY: number): WorkspaceRect | undefined {
  const base: WorkspaceRect = {
    left: workspaceInsets.value.left,
    top: workspaceInsets.value.top,
    right: dockableWorkspaceSize.width - workspaceInsets.value.right,
    bottom: dockableWorkspaceSize.height - workspaceInsets.value.bottom,
  }
  if (!containsPoint(base, clientX, clientY) || base.right - base.left < 240 || base.bottom - base.top < 160) return undefined

  const blockers = dockableGroups.value
    .filter((group) => group.id !== groupId && group.dock === 'free' && tabsForGroup(group).length > 0)
    .map((group) => workspaceRectForGroup(group))
    .map((rect) => ({
      left: Math.max(base.left, rect.left),
      top: Math.max(base.top, rect.top),
      right: Math.min(base.right, rect.right),
      bottom: Math.min(base.bottom, rect.bottom),
    }))
    .filter((rect) => rect.right > rect.left && rect.bottom > rect.top)
  const xLines = Array.from(new Set([base.left, base.right, ...blockers.flatMap((rect) => [rect.left, rect.right])])).sort((a, b) => a - b)
  const yLines = Array.from(new Set([base.top, base.bottom, ...blockers.flatMap((rect) => [rect.top, rect.bottom])])).sort((a, b) => a - b)
  let best: WorkspaceRect | undefined
  for (let xIndex = 0; xIndex < xLines.length - 1; xIndex += 1) {
    for (let nextXIndex = xIndex + 1; nextXIndex < xLines.length; nextXIndex += 1) {
      for (let yIndex = 0; yIndex < yLines.length - 1; yIndex += 1) {
        for (let nextYIndex = yIndex + 1; nextYIndex < yLines.length; nextYIndex += 1) {
          const candidate = { left: xLines[xIndex]!, top: yLines[yIndex]!, right: xLines[nextXIndex]!, bottom: yLines[nextYIndex]! }
          if (!containsPoint(candidate, clientX, clientY)
            || candidate.right - candidate.left < 240
            || candidate.bottom - candidate.top < 160
            || blockers.some((blocker) => rectOverlap(candidate, blocker) > 0)) continue
          if (!best || rectArea(candidate) > rectArea(best)) best = candidate
        }
      }
    }
  }
  return best
}

function blankDropZone(region: WorkspaceRect, clientX: number, clientY: number): WorkspaceDropZone {
  const edgeWidth = Math.min(132, Math.max(56, Math.round((region.right - region.left) * .24)))
  const edgeHeight = Math.min(108, Math.max(48, Math.round((region.bottom - region.top) * .2)))
  if (clientX <= region.left + edgeWidth) return 'left'
  if (clientX >= region.right - edgeWidth) return 'right'
  if (clientY <= region.top + edgeHeight) return 'top'
  if (clientY >= region.bottom - edgeHeight) return 'bottom'
  return 'fill'
}

function dropRectForZone(region: WorkspaceRect, zone: WorkspaceDropZone): WorkspaceRect {
  const width = region.right - region.left
  const height = region.bottom - region.top
  const splitWidth = Math.min(width, Math.max(240, Math.round(width * .34)))
  const splitHeight = Math.min(height, Math.max(160, Math.round(height * .34)))
  if (zone === 'left') return { left: region.left, top: region.top, right: region.left + splitWidth, bottom: region.bottom }
  if (zone === 'right') return { left: region.right - splitWidth, top: region.top, right: region.right, bottom: region.bottom }
  if (zone === 'top') return { left: region.left, top: region.top, right: region.right, bottom: region.top + splitHeight }
  if (zone === 'bottom') return { left: region.left, top: region.bottom - splitHeight, right: region.right, bottom: region.bottom }
  return region
}

const workspaceSnapPreview = ref<WorkspaceSnapPreview>()
const workspaceDropZones = computed(() => {
  const region = workspaceSnapPreview.value?.region
  if (!region || workspaceSnapPreview.value?.targetGroupId) return []
  const width = region.right - region.left
  const height = region.bottom - region.top
  const edgeWidth = Math.min(132, Math.max(56, Math.round(width * .24)))
  const edgeHeight = Math.min(108, Math.max(48, Math.round(height * .2)))
  return [
    { id: 'left' as const, style: { left: `${region.left}px`, top: `${region.top}px`, width: `${edgeWidth}px`, height: `${height}px` } },
    { id: 'right' as const, style: { left: `${region.right - edgeWidth}px`, top: `${region.top}px`, width: `${edgeWidth}px`, height: `${height}px` } },
    { id: 'top' as const, style: { left: `${region.left + edgeWidth}px`, top: `${region.top}px`, width: `${Math.max(0, width - edgeWidth * 2)}px`, height: `${edgeHeight}px` } },
    { id: 'bottom' as const, style: { left: `${region.left + edgeWidth}px`, top: `${region.bottom - edgeHeight}px`, width: `${Math.max(0, width - edgeWidth * 2)}px`, height: `${edgeHeight}px` } },
    { id: 'fill' as const, style: { left: `${region.left + edgeWidth}px`, top: `${region.top + edgeHeight}px`, width: `${Math.max(0, width - edgeWidth * 2)}px`, height: `${Math.max(0, height - edgeHeight * 2)}px` } },
  ]
})

const snapPreviewLabel = computed(() => {
  const preview = workspaceSnapPreview.value
  if (!preview) return ''
  if (preview.targetGroupId) return t('panel.combineHint')
  if (preview.dock) return t('panel.dockHint')
  if (preview.zone === 'fill') return t('panel.fillHint')
  return preview.zone ? t('panel.splitHint') : ''
})

function previewWorkspaceMove(groupId: string, event?: { dock?: DockPosition; targetGroupId?: string; clientX?: number; clientY?: number }): void {
  if (!event?.dock && !event?.targetGroupId && (event?.clientX === undefined || event.clientY === undefined)) {
    workspaceSnapPreview.value = undefined
    return
  }
  if (event.targetGroupId) {
    workspaceSnapPreview.value = {
      groupId,
      targetGroupId: event.targetGroupId,
    }
    return
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
        }
    workspaceSnapPreview.value = {
      groupId,
      dock: event.dock,
      region,
      zone: event.dock,
    }
    return
  }
  if (event.clientX === undefined || event.clientY === undefined) {
    workspaceSnapPreview.value = undefined
    return
  }
  const bounds = workspaceRoot.value?.getBoundingClientRect()
  if (!bounds) {
    workspaceSnapPreview.value = undefined
    return
  }
  const localX = event.clientX - bounds.left
  const localY = event.clientY - bounds.top
  const region = blankRegionForPointer(groupId, localX, localY)
  workspaceSnapPreview.value = region ? {
    groupId,
    region,
    zone: blankDropZone(region, localX, localY),
  } : undefined
}

const panelSwitcherItems = computed(() => panelDefinitions.value.map((definition) => ({
  ...definition,
  visible: dockableLayout.panels[definition.id].visible,
  available: isPanelAvailable(definition.id),
  required: definition.id === 'canvas',
})))

function toggleWorkspacePanel(panelId: WorkspacePanelId): void {
  if (panelId === 'canvas' || !isPanelAvailable(panelId)) return
  if (dockableLayout.panels[panelId].visible) hideDockablePanel(panelId)
  else showDockablePanel(panelId)
}

function changeWorkspaceDisplayMode(mode: DockableWorkspaceMode): void {
  if (mode === 'debug' && !canUseDebugDisplayMode.value) return
  setDockableDisplayMode(mode)
  if (mode === 'debug') {
    showDockablePanel('debug')
    if (activeWorkpieceRunId.value) showDockablePanel('workpieces')
  }
}

function updateWorkspaceGroup(groupId: string, patch: Partial<DockablePanelGroupState>): void {
  updateDockableGroup(groupId, patch)
}

function activateWorkspacePanel(groupId: string, panelId: WorkspacePanelId): void {
  activateDockablePanel(groupId, panelId)
}

function inspectDebugNode(): void {
  showDockablePanel('inspector')
  activateDockablePanel(dockableLayout.panels.inspector.groupId, 'inspector')
}

function dockForPointer(clientX: number, clientY: number): 'left' | 'right' | 'top' | 'bottom' | undefined {
  const bounds = workspaceRoot.value?.getBoundingClientRect()
  if (!bounds) return undefined
  const distances: Array<{ dock: 'left' | 'right' | 'top' | 'bottom'; distance: number }> = [
    { dock: 'left', distance: clientX - bounds.left },
    { dock: 'right', distance: bounds.right - clientX },
    { dock: 'top', distance: clientY - bounds.top },
    { dock: 'bottom', distance: bounds.bottom - clientY },
  ]
  const nearest = distances.sort((a, b) => a.distance - b.distance)[0]
  return nearest && nearest.distance <= 52 ? nearest.dock : undefined
}

function applyBlankWorkspaceDrop(groupId: string, panelId: WorkspacePanelId, region: WorkspaceRect, zone: WorkspaceDropZone): void {
  const sourceGroup = dockableGroups.value.find((group) => group.id === groupId)
  if (!sourceGroup) return
  let targetGroupId = groupId
  if (sourceGroup.panelIds.length > 1) targetGroupId = detachDockablePanel(panelId) ?? groupId
  const targetGroup = dockableGroups.value.find((group) => group.id === targetGroupId)
  if (!targetGroup) return
  const rect = dropRectForZone(region, zone)
  updateDockableGroup(targetGroupId, {
    dock: 'free',
    x: rect.left,
    y: rect.top,
    width: rect.right - rect.left,
    height: rect.bottom - rect.top,
  })
  focusDockableGroup(targetGroupId)
}

function finishWorkspaceMove(groupId: string, event: { panelId: WorkspacePanelId; targetGroupId?: string; dockTarget?: DockPosition; clientX: number; clientY: number; moved: boolean; interaction: 'move' | 'resize' }): void {
  const preview = workspaceSnapPreview.value?.groupId === groupId ? workspaceSnapPreview.value : undefined
  workspaceSnapPreview.value = undefined
  if (event.interaction === 'resize') {
    focusDockableGroup(groupId)
    return
  }
  if (event.targetGroupId && event.targetGroupId !== groupId && event.moved) {
    combineDockablePanel(event.panelId, event.targetGroupId)
    return
  }
  const dock = event.moved
    ? (event.dockTarget && isEdgeDockPosition(event.dockTarget) ? event.dockTarget : dockForPointer(event.clientX, event.clientY))
    : undefined
  if (dock) dockDockableGroup(groupId, dock)
  else if (event.moved && preview?.region && preview.zone) applyBlankWorkspaceDrop(groupId, event.panelId, preview.region, preview.zone)
  else focusDockableGroup(groupId)
}

function dockWorkspacePanel(panelId: WorkspacePanelId, dock: DockPosition): void {
  const group = dockableGroups.value.find((candidate) => candidate.panelIds.includes(panelId))
  if (!group) return
  if (group.panelIds.length > 1) {
    const groupId = detachDockablePanel(panelId)
    if (groupId) dockDockableGroup(groupId, dock)
    return
  }
  dockDockableGroup(group.id, dock)
}

function combineWorkspacePanel(panelId: WorkspacePanelId, targetGroupId: string): void {
  combineDockablePanel(panelId, targetGroupId)
}

function detachWorkspacePanel(panelId: WorkspacePanelId): void {
  detachDockablePanel(panelId)
}

function closeWorkspacePanel(panelId: WorkspacePanelId): void {
  hideDockablePanel(panelId)
}

watch(() => saveDiagnostics.value.length, (count) => {
  if (count > 0 && dockableDisplayMode.value === 'edit') showDockablePanel('diagnostics')
})

watch([isDebugActive, () => workspaceView.value, () => debugSession.value?.id], ([active, view, sessionId]) => {
  if (view !== 'editor') return
  if (active) {
    if (dockableDisplayMode.value !== 'debug') setDockableDisplayMode('debug')
    showDockablePanel('debug')
    if (activeWorkpieceRunId.value) showDockablePanel('workpieces')
  } else if (!sessionId && dockableDisplayMode.value === 'debug') {
    setDockableDisplayMode('edit')
  }
}, { immediate: true })

watch(() => debugSession.value?.id, (sessionId, previousSessionId) => {
  if (sessionId !== previousSessionId) {
    selectedDebugNodeId.value = undefined
    selectedDebugExecutionId.value = undefined
    workpieceFocusNodeId.value = undefined
    workpieceFocusExecutionId.value = undefined
  }
})

// Keep canvas-driven selections in sync with the debug execution list. The
// explicit canvas click handler separately marks manual selections as eligible
// to focus the matching workpiece.
watch(() => selectedNode.value?.id, (nodeId) => {
  if (!debugSession.value || !nodeId || selectedDebugNodeId.value === nodeId) return
  selectedDebugNodeId.value = nodeId
})

watch(() => debugSession.value?.status, (status, previousStatus) => {
  if (previousStatus && status && status !== previousStatus && ['completed', 'cancelled', 'failed'].includes(status)) {
    selectedDebugNodeId.value = undefined
    selectedDebugExecutionId.value = undefined
    workpieceFocusNodeId.value = undefined
    workpieceFocusExecutionId.value = undefined
    workpieceRefreshRevision.value += 1
  }
})

const debugRenderedElements = computed(() => renderedElements.value.map((element) => {
  if (!('data' in element) || !('position' in element)) return element
  const node = element as FlowNode
  return {
    ...node,
    data: {
      ...node.data,
      breakpoint: isBreakpoint(node.id),
      debugPaused: debugSession.value?.currentNodeId === node.id,
      breakpointLocked: isDebugActive.value,
      onToggleBreakpoint: toggleBreakpoint,
    },
  }
}))

const {
  handleCanvasDragOver,
  handleCanvasDragLeave,
  handleCanvasDrop,
  handleLibraryNodePointerDown,
  handleBuiltinNodePointerDown,
} = useNodeDrop({ screenToFlowCoordinate, isCanvasDropActive, notice, addNode })

const {
  beginProjectRename,
  cancelProjectRename,
  submitProjectRename,
  saveFlow,
  openProject: loadProject,
  startNewProject: beginNewProject,
  initializeWorkspace,
} = useProjectSession({
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
})

function undo(): void {
  if (undoSnapshot()) {
    notice.value = t('canvas.undoApplied')
  }
}

function redo(): void {
  if (redoSnapshot()) {
    notice.value = t('canvas.redoApplied')
  }
}

function nodeTitle(node: FlowNode): string {
  return node.data.displayName?.trim() || t(node.data.titleKey)
}

function sourceNodeTitle(parameter: MethodParameter): string {
  const source = currentCanvas.value.nodes.find((node) => node.id === parameter.sourceNodeId)
  return source ? nodeTitle(source) : t('parameter.previousNode')
}

function nodeIdFromDiagnostic(diagnostic: FlowValidationDiagnostic): string | undefined {
  return parseFlowValidationDiagnosticTarget(diagnostic.path)?.nodeId
}

function dismissSaveDiagnostics(): void {
  saveDiagnostics.value = []
}

function locateSaveDiagnostic(diagnostic: FlowValidationDiagnostic): void {
  const nodeId = nodeIdFromDiagnostic(diagnostic)
  if (!nodeId) {
    return
  }

  const canvas = canvases.value.find((candidate) => candidate.nodes.some((node) => node.id === nodeId))
  if (!canvas) {
    return
  }

  selectCanvas(canvas.id)
  void nextTick().then(() => selectNode(nodeId))
}

async function openProjectInEditor(workspace: ProjectWorkspaceDto, requestedFlowId?: string): Promise<void> {
  await loadProject(workspace, requestedFlowId)
  workspaceView.value = 'editor'
}

async function startNewProjectInEditor(): Promise<void> {
  workspaceView.value = 'editor'
  await beginNewProject()
}

function refreshProjectLibraryCatalog(): void {
  void refreshLibraryCatalog(projectId.value)
}

function updateActiveProjectDirectory(workspaces: ProjectWorkspaceDto[]): void {
  projectWorkspaces.value = workspaces
}

function updateCurrentFlowVersionSummary(version: FlowVersionSummaryDto): void {
  if (!projectId.value || !flowId.value) return
  projectWorkspaces.value = projectWorkspaces.value.map((workspace) => workspace.project.id === projectId.value
    ? {
        ...workspace,
        flows: workspace.flows.map((flow) => flow.id === flowId.value
          ? version.track === 'development'
            ? { ...flow, version: version.version }
            : { ...flow, productionVersion: version.version }
          : flow),
      }
    : workspace)
}

async function publishCurrentFlowVersion(): Promise<void> {
  if (!projectId.value || !flowId.value || !canPublishVersion.value) return

  isVersionPublishing.value = true
  try {
    const published = await publishFlowVersion(projectId.value, flowId.value, {
      expectedDevelopmentVersion: flowVersion.value,
    })
    updateCurrentFlowVersionSummary(published)
    notice.value = t('version.published', { version: published.version })
  } catch {
    notice.value = t('version.publishFailed')
  } finally {
    isVersionPublishing.value = false
  }
}

async function handleFlowVersionChanged(version: FlowVersionSummaryDto): Promise<void> {
  updateCurrentFlowVersionSummary(version)
  if (version.track === 'development' && projectId.value && flowId.value) {
    const workspace = projectWorkspaces.value.find((item) => item.project.id === projectId.value)
    if (workspace) await loadProject(workspace, flowId.value)
  }
  notice.value = t('version.rolledBack', { version: version.version })
}

function findNode(nodeId: string): FlowNode | undefined {
  return canvases.value.flatMap((canvas) => canvas.nodes).find((node) => node.id === nodeId)
}

function removeParameterConnections(nodeId: string, parameterIds: ReadonlySet<string>): void {
  for (const canvas of canvases.value) {
    canvas.edges = canvas.edges.filter((edge) =>
      edge.data.semantic !== 'data'
      || edge.target !== nodeId
      || !edge.data.targetParameterId
      || !parameterIds.has(edge.data.targetParameterId))
  }
}

function migrateParameterConnections(nodeId: string, targetParameterIdRemap: ReadonlyMap<string, string>): void {
  if (targetParameterIdRemap.size === 0) {
    return
  }

  for (const canvas of canvases.value) {
    canvas.edges = canvas.edges.map((edge) => {
      const nextParameterId = edge.data.semantic === 'data' && edge.target === nodeId && edge.data.targetParameterId
        ? targetParameterIdRemap.get(edge.data.targetParameterId)
        : undefined
      if (!nextParameterId) {
        return edge
      }

      return {
        ...edge,
        targetHandle: parameterHandleFor(nextParameterId),
        data: {
          ...edge.data,
          targetParameterId: nextParameterId,
        },
      }
    })
  }
}

function setNodePublic(nodeId: string, value: boolean): void {
  const node = findNode(nodeId)
  if (!node || node.data.runtime?.isPublic === value) {
    return
  }

  recordWorkspaceMutation()
  node.data.runtime = { ...(node.data.runtime ?? { category: node.data.kind === 'action' || node.data.kind === 'flipflop' ? 'method' : 'basic' }), isPublic: value }
  markWorkspaceChanged()
}

function setFlowEntry(nodeId: string, value: boolean): void {
  const node = findNode(nodeId)
  if (!node || (value && entryNodeId.value === nodeId) || (!value && entryNodeId.value !== nodeId)) {
    return
  }

  recordWorkspaceMutation()
  entryNodeId.value = value ? nodeId : ''
  markWorkspaceChanged()
}

function setFlowCallTarget(nodeId: string, canvasId: string, targetNodeId?: string): void {
  const callNode = findNode(nodeId)
  if (!callNode || callNode.data.kind !== 'flowCall') {
    return
  }

  const targetCanvas = canvases.value.find((canvas) => canvas.id === canvasId)
  const target = targetNodeId ? targetCanvas?.nodes.find((node) => node.id === targetNodeId && node.data.runtime?.isPublic === true) : undefined
  const previousParameterIds = new Set(callNode.data.parameters.map((parameter) => parameter.id))
  recordWorkspaceMutation()

  if (!target) {
    removeParameterConnections(nodeId, previousParameterIds)
    callNode.data.parameters = []
    callNode.data.runtime = {
      ...(callNode.data.runtime ?? { category: 'basic', returnType: 'System.Object' }),
      targetCanvasId: canvasId || undefined,
      targetNodeId: undefined,
      targetFlowId: undefined,
      flowCallParameterBindings: [],
    }
    markWorkspaceChanged()
    return
  }

  const existingBindings = new Map(
    (callNode.data.runtime?.flowCallParameterBindings ?? []).map((binding) => [binding.targetParameterId, binding.callParameterId]),
  )
  const existingInputs = new Map(callNode.data.parameters.map((parameter) => [parameter.id, parameter]))
  const nextParameters = target.data.parameters.map((parameter) => {
    const callParameterId = existingBindings.get(parameter.id) ?? parameter.id
    const existing = existingInputs.get(callParameterId)
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
    }
  })
  const nextIds = new Set(nextParameters.map((parameter) => parameter.id))
  removeParameterConnections(nodeId, new Set([...previousParameterIds].filter((id) => !nextIds.has(id))))
  callNode.data.parameters = nextParameters
  callNode.data.runtime = {
    ...(callNode.data.runtime ?? { category: 'basic', returnType: 'System.Object' }),
    targetCanvasId: canvasId,
    targetNodeId: target.id,
    targetFlowId: undefined,
    flowCallParameterBindings: nextParameters.map((parameter, index) => ({
      callParameterId: parameter.id,
      targetParameterId: target.data.parameters[index]!.id,
    })),
  }
  markWorkspaceChanged()
}

function addScriptInput(nodeId: string): void {
  const node = findNode(nodeId)
  if (!node || node.data.kind !== 'script') {
    return
  }

  recordWorkspaceMutation()
  let index = node.data.parameters.length + 1
  let id = `input-${index}`
  while (node.data.parameters.some((parameter) => parameter.id === id)) {
    id = `input-${++index}`
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
  })
  markWorkspaceChanged()
}

function removeScriptInput(nodeId: string, parameterId: string): void {
  const node = findNode(nodeId)
  if (!node || node.data.kind !== 'script') {
    return
  }

  recordWorkspaceMutation()
  removeParameterConnections(nodeId, new Set([parameterId]))
  node.data.parameters = node.data.parameters.filter((parameter) => parameter.id !== parameterId)
  markWorkspaceChanged()
}

function setVariadicMode(nodeId: string, parameterId: string, mode: 'expanded' | 'collection'): void {
  const node = findNode(nodeId)
  const parameter = node?.data.parameters.find((item) => item.id === parameterId)
  const groupId = parameter?.variadicGroupId
  if (!node || !parameter || !groupId) {
    return
  }

  const conversion = convertVariadicParameterMode(node.data.parameters, groupId, mode)
  if (!conversion.ok) {
    notice.value = t('parameter.variadicConversionBlocked')
    return
  }

  recordWorkspaceMutation()
  node.data.parameters = conversion.parameters
  migrateParameterConnections(nodeId, conversion.targetParameterIdRemap)
  markWorkspaceChanged()
}

function addVariadicInput(nodeId: string, parameterId: string): void {
  const node = findNode(nodeId)
  const parameter = node?.data.parameters.find((item) => item.id === parameterId)
  const groupId = parameter?.variadicGroupId
  if (!node || !parameter || !groupId) {
    return
  }

  recordWorkspaceMutation()
  const existing = node.data.parameters.filter((item) => item.variadicGroupId === groupId)
  const id = `${groupId}-${existing.length + 1}`
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
  })
  markWorkspaceChanged()
}

function removeVariadicInput(nodeId: string, parameterId: string): void {
  const node = findNode(nodeId)
  const parameter = node?.data.parameters.find((item) => item.id === parameterId)
  const groupId = parameter?.variadicGroupId
  if (!node || !parameter || !groupId) {
    return
  }

  const members = node.data.parameters.filter((item) => item.variadicGroupId === groupId)
  if (members.length <= 1) {
    notice.value = t('parameter.keepVariadicPlaceholder')
    return
  }

  recordWorkspaceMutation()
  removeParameterConnections(nodeId, new Set([parameterId]))
  node.data.parameters = node.data.parameters.filter((item) => item.id !== parameterId)
  markWorkspaceChanged()
}

watch(projectId, (nextProjectId) => {
  projectLibraryOpen.value = false
  void refreshLibraryCatalog(nextProjectId)
})

function updateConcurrencyMode(mode: FlowConcurrencyMode): void {
  if (runPolicy.value.concurrencyMode === mode) {
    return
  }

  recordWorkspaceMutation()
  runPolicy.value = { concurrencyMode: mode }
  markWorkspaceChanged()
}

useWorkspaceShortcuts({ canvasDeleteConfirmOpen, cancelCanvasRemoval, saveFlow, undo, redo })

onMounted(() => {
  void initializeWorkspace()
  void refreshLibraryCatalog()
})

function setLanguage(nextLocale: Locale): void {
  setLocale(nextLocale)
  languageMenuOpen.value = false
}
</script>

<template>
  <div class="app-shell" :class="{ 'app-shell--console': workspaceView === 'console' }">
    <CommandBar
      :project-name="projectName"
      :flow-version="flowVersion"
      :production-version="productionVersion"
      :project-workspaces="projectWorkspaces"
      :project-id="projectId"
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
      @toggle-project-menu="projectMenuOpen = !projectMenuOpen"
      @begin-project-rename="beginProjectRename"
      @cancel-project-rename="cancelProjectRename"
      @submit-project-rename="submitProjectRename"
      @update:project-name-draft="projectNameDraft = $event"
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
      @toggle-language-menu="languageMenuOpen = !languageMenuOpen"
      @set-language="setLanguage"
      @show-run-console="workspaceView = 'console'"
      @show-version-history="flowVersionHistoryOpen = true"
      @publish-version="publishCurrentFlowVersion"
      @update-concurrency-mode="updateConcurrencyMode"
      @toggle-workspace-panel="toggleWorkspacePanel"
      @reset-workspace-layout="resetDockableLayout"
      @change-workspace-mode="changeWorkspaceDisplayMode"
    />

    <RunConsole
      v-if="workspaceView === 'console'"
      :project-workspaces="projectWorkspaces"
      @open-flow="openProjectInEditor"
      @start-new-project="startNewProjectInEditor"
      @project-directory-changed="updateActiveProjectDirectory"
    />

    <main v-if="workspaceView === 'editor'" ref="workspaceRoot" class="workspace-grid">
      <div v-if="workspaceSnapPreview" class="workspace-docking-preview" aria-hidden="true">
        <div v-if="workspaceSnapPreview.targetGroupId && snapPreviewLabel" class="workspace-docking-preview__label workspace-docking-preview__label--combine">{{ snapPreviewLabel }}</div>
        <template v-else>
          <div
            v-for="zone in workspaceDropZones"
            :key="zone.id"
            class="workspace-docking-preview__zone"
            :class="[`workspace-docking-preview__zone--${zone.id}`, { active: workspaceSnapPreview.zone === zone.id }]"
            :style="zone.style"
          ></div>
          <div v-if="snapPreviewLabel" class="workspace-docking-preview__label">{{ snapPreviewLabel }}</div>
        </template>
      </div>
      <template v-for="group in dockableGroups" :key="group.id">
        <DockablePanelGroup
          v-if="tabsForGroup(group).length > 0"
          :group="group"
          :tabs="tabsForGroup(group)"
          :group-options="groupOptionsFor(group.id)"
          :workspace-size="dockableWorkspaceSize"
          :canvas-group="group.panelIds.includes('canvas')"
          :docked-offset="dockedOffsetFor(group)"
          :docked-size="dockedExtentFor(group)"
          :layout-priority="dockedLayoutPriority"
          :canvas-insets="workspaceInsets"
          :drop-targeted="workspaceSnapPreview?.targetGroupId === group.id"
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
              v-if="panelId === 'nodes'"
              :embedded="true"
              :mobile-visible="false"
              :library-search="librarySearch"
              :is-loading="isLibraryCatalogLoading"
              :error="libraryCatalogError"
              :visible-libraries="visibleLibraries"
              :visible-builtin-nodes="visibleBuiltinNodes"
              :catalog-node-count="catalogNodeCount"
              @update:library-search="librarySearch = $event"
              @manage="projectLibraryOpen = true"
              @retry="refreshProjectLibraryCatalog"
              @node-pointer-down="handleLibraryNodePointerDown"
              @builtin-node-pointer-down="handleBuiltinNodePointerDown"
              @collapse="closeWorkspacePanel('nodes')"
            />
            <CanvasPanel
              v-else-if="panelId === 'canvas'"
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
              :is-canvas-drop-active="isCanvasDropActive"
              :notice="notice"
              :pending-canvas-delete="pendingCanvasDelete"
              :canvas-delete-confirm-open="canvasDeleteConfirmOpen"
              :canvas-label="canvasLabel"
              @select-canvas="selectCanvas"
              @toggle-canvas-menu="toggleCanvasMenu"
              @add-canvas="addCanvas"
              @add-custom-canvas="addCustomCanvas"
              @update:custom-canvas-name-draft="customCanvasNameDraft = $event"
              @toggle-connection-settings="connectionSettingsOpen = !connectionSettingsOpen"
              @update-connection-line-type="updateConnectionLineType"
              @update-canvas-focus-setting="updateCanvasFocusSetting"
              @remove-selection="removeSelection"
              @request-canvas-removal="requestCanvasRemoval"
              @cancel-canvas-removal="cancelCanvasRemoval"
              @confirm-canvas-removal="confirmCanvasRemoval"
              @canvas-dragenter="handleCanvasDragOver"
              @canvas-dragover="handleCanvasDragOver"
              @canvas-dragleave="handleCanvasDragLeave"
              @canvas-drop="handleCanvasDrop"
              @connect="onConnect"
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
              v-else-if="panelId === 'inspector'"
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
              v-else-if="panelId === 'output'"
              v-model:active-output="visibleActiveOutput"
              :embedded="true"
              :run-events="visibleRunEvents"
              :run-payload="visibleRunPayload"
              :has-run-output="visibleHasRunOutput"
            />
            <div v-else-if="panelId === 'diagnostics'" class="workspace-panel-content workspace-panel-content--diagnostics">
              <FlowValidationDiagnostics v-if="saveDiagnostics.length" :diagnostics="saveDiagnostics" :canvases="canvases" @dismiss="dismissSaveDiagnostics" @locate="locateSaveDiagnostic" />
              <div v-else class="workspace-panel-empty"><AlertTriangle :size="18" /><strong>{{ t('diagnostics.empty') }}</strong></div>
            </div>
            <FlowDebugPanel
              v-else-if="panelId === 'debug' && debugSession"
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
            <RunWorkpiecePanel
              v-else-if="panelId === 'workpieces' && activeWorkpieceRunId"
              :run-id="activeWorkpieceRunId"
              :focus-node-id="debugSession ? workpieceFocusNodeId : undefined"
              :focus-execution-id="debugSession ? workpieceFocusExecutionId : undefined"
              :refresh-signal="workpieceRefreshRevision"
              :live="isRunning || isDebugActive"
              :embedded="true"
              @select-node="focusDebugWorkpieceNode"
            />
          </template>
        </DockablePanelGroup>
      </template>
    </main>

    <ProjectLibraryDialog
      v-if="workspaceView === 'editor' && projectLibraryOpen && projectId"
      :project-id="projectId"
      :project-name="projectName"
      :flows="currentProjectFlows"
      @close="projectLibraryOpen = false"
      @changed="replaceProjectLibraries"
    />
    <FlowVersionHistoryDialog
      v-if="workspaceView === 'editor' && flowVersionHistoryOpen && projectId && flowId"
      :project-id="projectId"
      :flow-id="flowId"
      :development-version="flowVersion"
      :production-version="productionVersion"
      :can-mutate="canMutateVersions"
      @close="flowVersionHistoryOpen = false"
      @changed="handleFlowVersionChanged"
    />
  </div>
</template>
