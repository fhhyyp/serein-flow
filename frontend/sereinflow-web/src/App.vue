<script setup lang="ts">
import { computed, nextTick, onMounted, reactive, ref, watch } from 'vue'
import {
  Activity,
  Code2,
  Database,
  GitBranch,
  Zap,
} from 'lucide-vue-next'
import { useVueFlow } from '@vue-flow/core'
import '@vue-flow/core/dist/style.css'
import '@vue-flow/core/dist/theme-default.css'
import LibraryUploadDialog from './components/library/LibraryUploadDialog.vue'
import NodeLibraryPanel from './components/library/NodeLibraryPanel.vue'
import CommandBar from './components/workspace/CommandBar.vue'
import MobileWorkspaceTabs from './components/workspace/MobileWorkspaceTabs.vue'
import OutputPanel from './components/workspace/OutputPanel.vue'
import InspectorPanel from './components/inspector/InspectorPanel.vue'
import type { ProjectWorkspaceDto } from './api/flowApi'
import type { LibraryDto } from './api/libraryApi'
import { locale, setLocale, t, type Locale } from './i18n'
import {
  normalizeConnectionLineTypes,
  type ConnectionLineSettings,
} from './flow/connectionLine'
import { createInitialCanvases } from './flow/initialCanvases'
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

const { zoomIn, zoomOut, fitView, screenToFlowCoordinate } = useVueFlow()

const recoveryWorkspace = loadWorkspace()
const canvases = ref<CanvasState[]>(createInitialCanvases())
const connectionLineTypes = reactive<ConnectionLineSettings>(normalizeConnectionLineTypes(recoveryWorkspace?.connectionLineTypes))
const activeCanvasId = ref('main')
const libraryUploadOpen = ref(false)
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
  catalogNodeCount,
  refreshLibraryCatalog,
  handleLibraryUploaded: updateLibraryCatalog,
} = useLibraryCatalog({ notice })
const nextNodeNumber = ref(1)
const isDirty = ref(false)
const saveFailed = ref(false)
const saveConflict = ref(false)
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
const savedWorkspaceFingerprint = ref('')
const isRestoringWorkspace = ref(false)
const isSwitchingCanvas = ref(false)

function iconForNodeKind(kind: NodeKind) {
  if (kind === 'trigger' || kind === 'flipflop') {
    return Zap
  }

  if (kind === 'script' || kind === 'expression') {
    return Code2
  }

  if (kind === 'condition' || kind === 'expOp' || kind === 'expCondition') {
    return GitBranch
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
  return cloneWorkspaceSnapshot({
    canvases: canvases.value,
    activeCanvasId: activeCanvasId.value,
    nextNodeNumber: nextNodeNumber.value,
    projectName: projectName.value,
    connectionLineTypes: { ...connectionLineTypes },
  })
}

function refreshDirtyState(): void {
  isDirty.value = workspaceFingerprint(currentWorkspaceSnapshot()) !== savedWorkspaceFingerprint.value
  if (isDirty.value) {
    saveFailed.value = false
  }
}

function markWorkspaceChanged(): void {
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
} = useFlowRunner({ nodes, notice })

const {
  handleCanvasDragOver,
  handleCanvasDragLeave,
  handleCanvasDrop,
  handleLibraryNodeDragStart,
} = useNodeDrop({ screenToFlowCoordinate, isCanvasDropActive, notice, addNode })

const {
  beginProjectRename,
  cancelProjectRename,
  submitProjectRename,
  saveFlow,
  openProject,
  startNewProject,
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

function handleLibraryUploaded(library: LibraryDto): void {
  updateLibraryCatalog(library)
  libraryUploadOpen.value = false
}

function nodeTitle(node: FlowNode): string {
  return node.data.displayName?.trim() || t(node.data.titleKey)
}

function sourceNodeTitle(parameter: MethodParameter): string {
  const source = currentCanvas.value.nodes.find((node) => node.id === parameter.sourceNodeId)
  return source ? nodeTitle(source) : t('parameter.previousNode')
}

useWorkspaceShortcuts({ canvasDeleteConfirmOpen, cancelCanvasRemoval, saveFlow, undo, redo })

onMounted(() => {
  void Promise.allSettled([initializeWorkspace(), refreshLibraryCatalog()])
})

function setLanguage(nextLocale: Locale): void {
  setLocale(nextLocale)
  languageMenuOpen.value = false
}
</script>

<template>
  <div class="app-shell">
    <CommandBar
      :project-name="projectName"
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
      @toggle-project-menu="projectMenuOpen = !projectMenuOpen"
      @begin-project-rename="beginProjectRename"
      @cancel-project-rename="cancelProjectRename"
      @submit-project-rename="submitProjectRename"
      @update:project-name-draft="projectNameDraft = $event"
      @open-project="openProject"
      @start-new-project="startNewProject"
      @undo="undo"
      @redo="redo"
      @save="saveFlow"
      @run="runFlow"
      @toggle-language-menu="languageMenuOpen = !languageMenuOpen"
      @set-language="setLanguage"
    />

    <MobileWorkspaceTabs v-model:mobile-panel="mobilePanel" />

    <main class="workspace-grid">
      <NodeLibraryPanel
        :mobile-visible="mobilePanel === 'nodes'"
        :library-search="librarySearch"
        :is-loading="isLibraryCatalogLoading"
        :error="libraryCatalogError"
        :visible-libraries="visibleLibraries"
        :catalog-node-count="catalogNodeCount"
        @update:library-search="librarySearch = $event"
        @upload="libraryUploadOpen = true"
        @retry="refreshLibraryCatalog"
        @drag-node="handleLibraryNodeDragStart"
      />

      <CanvasPanel
        :project-name="projectName"
        :flow-version="flowVersion"
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
        @close="mobilePanel = null"
        @delete="removeSelection"
        @update-parameter-source="updateParameterSource"
        @begin-text-edit="beginTextEdit"
        @commit-text-edit="commitTextEdit"
        @discard-text-edit="discardTextEdit"
      />
    </main>

    <LibraryUploadDialog v-if="libraryUploadOpen" @close="libraryUploadOpen = false" @uploaded="handleLibraryUploaded" />
    <OutputPanel v-model:active-output="activeOutput" :run-events="runEvents" :run-payload="runPayload" :has-run-output="hasRunOutput" />
  </div>
</template>
