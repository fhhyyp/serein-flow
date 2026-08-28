<script setup lang="ts">
import { computed, nextTick, onMounted, reactive, ref, watch } from 'vue'
import {
  Activity,
  Code2,
  Database,
  Zap,
} from 'lucide-vue-next'
import { useVueFlow } from '@vue-flow/core'
import '@vue-flow/core/dist/style.css'
import '@vue-flow/core/dist/theme-default.css'
import NodeLibraryPanel from './components/library/NodeLibraryPanel.vue'
import ProjectLibraryDialog from './components/library/ProjectLibraryDialog.vue'
import CommandBar from './components/workspace/CommandBar.vue'
import MobileWorkspaceTabs from './components/workspace/MobileWorkspaceTabs.vue'
import OutputPanel from './components/workspace/OutputPanel.vue'
import CanvasPanel from './components/canvas/CanvasPanel.vue'
import InspectorPanel from './components/inspector/InspectorPanel.vue'
import RunConsole from './components/runs/RunConsole.vue'
import type { FlowConcurrencyMode, FlowValidationDiagnostic, ProjectWorkspaceDto } from './api/flowApi'
import { locale, setLocale, t, type Locale } from './i18n'
import {
  normalizeConnectionLineTypes,
  type ConnectionLineSettings,
} from './flow/connectionLine'
import { createInitialCanvases } from './flow/initialCanvases'
import { parameterHandleFor } from './flow/connectionSeats'
import { convertVariadicParameterMode } from './flow/variadicParameters'
import { parseFlowValidationDiagnosticTarget } from './flow/validationDiagnostics'
import { cloneWorkspaceSnapshot, workspaceFingerprint, type WorkspaceSnapshot } from './flow/workspaceHistory'
import { loadWorkspace } from './flow/workspaceStorage'
import { useWorkspaceHistory } from './composables/useWorkspaceHistory'
import { useLibraryCatalog } from './composables/useLibraryCatalog'
import { useFlowRunner } from './composables/useFlowRunner'
import { useCanvasManager } from './composables/useCanvasManager'
import { useProjectSession } from './composables/useProjectSession'
import { useFlowGraph } from './composables/useFlowGraph'
import { useNodeDrop } from './composables/useNodeDrop'
import { useWorkspaceShortcuts } from './composables/useWorkspaceShortcuts'
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
const activeCanvasId = ref('main')
const projectLibraryOpen = ref(false)
const mobilePanel = ref<'nodes' | 'inspector' | null>(null)
const languageMenuOpen = ref(false)
const projectMenuOpen = ref(false)
const connectionSettingsOpen = ref(false)
const isCanvasDropActive = ref(false)
const notice = ref('')
const {
  librarySearch,
  isLibraryCatalogLoading,
  libraryCatalogError,
  visibleLibraries,
  visibleBuiltinNodes,
  catalogNodeCount,
  refreshLibraryCatalog,
  replaceProjectLibraries,
} = useLibraryCatalog()
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
const runPolicy = ref<{ concurrencyMode: FlowConcurrencyMode }>({ concurrencyMode: 'parallel' })

const currentProjectFlows = computed(() =>
  projectId.value
    ? projectWorkspaces.value.find((workspace) => workspace.project.id === projectId.value)?.flows ?? []
    : [])

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
  hasRunOutput,
  runFlow,
} = useFlowRunner({ nodes, notice, projectId, flowId, flowVersion })

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
      :language-menu-open="languageMenuOpen"
      :locale="locale"
      :node-count="nodes.length"
      :workspace-view="workspaceView"
      :concurrency-mode="runPolicy.concurrencyMode"
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
      @toggle-language-menu="languageMenuOpen = !languageMenuOpen"
      @set-language="setLanguage"
      @show-run-console="workspaceView = 'console'"
      @update-concurrency-mode="updateConcurrencyMode"
    />

    <RunConsole
      v-if="workspaceView === 'console'"
      :project-workspaces="projectWorkspaces"
      @open-flow="openProjectInEditor"
      @start-new-project="startNewProjectInEditor"
      @project-directory-changed="updateActiveProjectDirectory"
    />

    <MobileWorkspaceTabs v-if="workspaceView === 'editor'" v-model:mobile-panel="mobilePanel" />

    <main v-if="workspaceView === 'editor'" class="workspace-grid">
      <NodeLibraryPanel
        :mobile-visible="mobilePanel === 'nodes'"
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
      />

      <CanvasPanel
        :canvases="canvases"
        :active-canvas-id="activeCanvasId"
        :current-canvas-lifecycle="currentCanvas.lifecycle"
        :current-canvas-node-count="currentCanvas.nodes.length"
        :current-canvas-edge-count="currentCanvas.edges.length"
        :rendered-elements="renderedElements"
        :is-valid-connection="isValidConnection"
        :canvas-render-key="canvasRenderKey"
        :canvas-menu-open="canvasMenuOpen"
        :available-canvas-lifecycles="availableCanvasLifecycles"
        :custom-canvas-name-draft="customCanvasNameDraft"
        :connection-settings-open="connectionSettingsOpen"
        :connection-line-types="connectionLineTypes"
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
        @node-click="onNodeClick"
        @edge-click="onEdgeClick"
        @pane-click="clearSelection"
        @zoom-in="zoomIn"
        @zoom-out="zoomOut"
        @fit-view="fitView"
      />

      <InspectorPanel
        :mobile-visible="mobilePanel === 'inspector'"
        :selected-node="selectedNode"
        :selected-edge="selectedEdge"
        :icon-for-node-kind="iconForNodeKind"
        :node-title="nodeTitle"
        :source-node-title="sourceNodeTitle"
        :canvases="canvases"
        :entry-node-id="entryNodeId"
        @close="mobilePanel = null"
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
    </main>

    <ProjectLibraryDialog
      v-if="workspaceView === 'editor' && projectLibraryOpen && projectId"
      :project-id="projectId"
      :project-name="projectName"
      :flows="currentProjectFlows"
      @close="projectLibraryOpen = false"
      @changed="replaceProjectLibraries"
    />
    <OutputPanel
      v-if="workspaceView === 'editor'"
      v-model:active-output="activeOutput"
      :run-events="runEvents"
      :run-payload="runPayload"
      :has-run-output="hasRunOutput"
      :diagnostics="saveDiagnostics"
      :canvases="canvases"
      @dismiss-diagnostics="dismissSaveDiagnostics"
      @locate-diagnostic="locateSaveDiagnostic"
    />
  </div>
</template>
