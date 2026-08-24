<script setup lang="ts">
import { computed, markRaw, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import {
  Activity,
  Check,
  ChevronDown,
  Code2,
  Database,
  FolderOpen,
  GitBranch,
  Languages,
  LayoutGrid,
  LocateFixed,
  Play,
  Plus,
  RotateCcw,
  RotateCw,
  Save,
  Settings2,
  Square,
  Terminal,
  Trash2,
  X,
  Zap,
} from 'lucide-vue-next'
import {
  ConnectionMode,
  MarkerType,
  VueFlow,
  applyNodeChanges,
  useVueFlow,
  type Connection,
  type EdgeChange,
  type NodeChange,
} from '@vue-flow/core'
import '@vue-flow/core/dist/style.css'
import '@vue-flow/core/dist/theme-default.css'
import FlowNodeCard from './components/flow/FlowNodeCard.vue'
import { FlowApiError, createProject, listProjects, loadFlow, saveFlow as saveFlowRequest, type ProjectWorkspaceDto } from './api/flowApi'
import { locale, setLocale, t, type Locale } from './i18n'
import { cloneCanvasGraph, removeEdgesById } from './flow/canvasGraph'
import { resolveConnectionSemantic } from './flow/connectionSeats'
import { flowDefinitionToWorkspace, workspaceToFlowDefinition } from './flow/flowDtoMapper'
import { createInitialCanvases } from './flow/initialCanvases'
import { isNodeKind } from './flow/nodeCatalog'
import { WorkspaceHistory, cloneWorkspaceSnapshot, workspaceFingerprint, type WorkspaceSnapshot } from './flow/workspaceHistory'
import { loadWorkspace, saveWorkspace } from './flow/workspaceStorage'
import type {
  ConnectionSemantic,
  CanvasLifecycle,
  CanvasState,
  FlowEdge,
  FlowNode,
  MethodParameter,
  NodeKind,
  ParameterSource,
} from './flow/types'

const nodeTypes = markRaw({ workflow: FlowNodeCard })
const { zoomIn, zoomOut, fitView, screenToFlowCoordinate } = useVueFlow()

const recoveryWorkspace = loadWorkspace()
const canvases = ref<CanvasState[]>(createInitialCanvases())
const activeCanvasId = ref('main')
const isRunning = ref(false)
const activeOutput = ref<'events' | 'payload'>('events')
const mobilePanel = ref<'nodes' | 'inspector' | null>(null)
const languageMenuOpen = ref(false)
const projectMenuOpen = ref(false)
const canvasMenuOpen = ref(false)
const isCanvasDropActive = ref(false)
const notice = ref('')
const nextNodeNumber = ref(1)
const workspaceHistory = new WorkspaceHistory()
const canUndo = ref(false)
const canRedo = ref(false)
const isDirty = ref(false)
const saveFailed = ref(false)
const saveConflict = ref(false)
const isWorkspaceLoading = ref(true)
const isSaving = ref(false)
const projectId = ref<string>()
const flowId = ref<string>()
const projectWorkspaces = ref<ProjectWorkspaceDto[]>([])
const canvasMountRevision = ref(0)
const projectName = ref(t('project.newProject'))
const flowVersion = ref(1)
const savedWorkspaceFingerprint = ref('')
let pendingTextEdit: WorkspaceSnapshot | undefined
let isRestoringWorkspace = false
let nodeDragHistoryOpen = false
let isSwitchingCanvas = false

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
const nodes = computed<FlowNode[]>({
  get: () => currentCanvas.value.nodes,
  set: (value) => {
    currentCanvas.value.nodes = value
  },
})
const edges = computed<FlowEdge[]>({
  get: () => currentCanvas.value.edges,
  set: (value) => {
    currentCanvas.value.edges = value
  },
})
const renderedCanvas = computed(() => cloneCanvasGraph(currentCanvas.value))
// Vue Flow validates edges against its current node store. Supplying nodes and
// edges through one element list makes setElements establish nodes before it
// validates the connections, avoiding an initialization-order race when a
// persisted canvas is restored.
const renderedElements = computed(() => [
  ...renderedCanvas.value.nodes,
  ...renderedCanvas.value.edges,
])
const selectedNode = computed(() => currentCanvas.value.nodes.find((node) => node.id === currentCanvas.value.selectedNodeId))
const selectedEdge = computed(() => currentCanvas.value.edges.find((edge) => edge.id === currentCanvas.value.selectedEdgeId))
const availableCanvasLifecycles = computed<CanvasLifecycle[]>(() =>
  (['init', 'loading', 'exit'] as CanvasLifecycle[])
    .filter((lifecycle) => !canvases.value.some((canvas) => canvas.lifecycle === lifecycle)),
)
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
      label: edge.data?.semantic === 'execution' ? t('edge.flow') : t('edge.value'),
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
  })
}

function syncHistoryAvailability(): void {
  canUndo.value = workspaceHistory.canUndo
  canRedo.value = workspaceHistory.canRedo
}

function recordWorkspaceMutation(): void {
  workspaceHistory.record(currentWorkspaceSnapshot())
  syncHistoryAvailability()
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
  isRestoringWorkspace = true
  canvasMountRevision.value += 1
  canvases.value = snapshot.canvases
  activeCanvasId.value = snapshot.activeCanvasId
  nextNodeNumber.value = snapshot.nextNodeNumber
  mobilePanel.value = null
  pendingTextEdit = undefined
  void nextTick().then(() => {
    isRestoringWorkspace = false
    refreshDirtyState()
  })
}

function undo(): void {
  const previous = workspaceHistory.undo(currentWorkspaceSnapshot())
  if (!previous) {
    return
  }

  restoreWorkspace(previous)
  syncHistoryAvailability()
  notice.value = t('canvas.undoApplied')
}

function redo(): void {
  const next = workspaceHistory.redo(currentWorkspaceSnapshot())
  if (!next) {
    return
  }

  restoreWorkspace(next)
  syncHistoryAvailability()
  notice.value = t('canvas.redoApplied')
}

function beginTextEdit(): void {
  pendingTextEdit ??= currentWorkspaceSnapshot()
}

function commitTextEdit(): void {
  if (pendingTextEdit) {
    workspaceHistory.record(pendingTextEdit)
    pendingTextEdit = undefined
    syncHistoryAvailability()
  }
  markWorkspaceChanged()
}

function discardTextEdit(): void {
  pendingTextEdit = undefined
}

function selectCanvas(canvasId: string): void {
  if (canvasId === activeCanvasId.value) {
    return
  }

  isSwitchingCanvas = true
  activeCanvasId.value = canvasId
  mobilePanel.value = null
  canvasMenuOpen.value = false
  void nextTick().then(() => {
    isSwitchingCanvas = false
  })
}

function addCanvas(lifecycle: CanvasLifecycle): void {
  if (lifecycle === 'main' || canvases.value.some((canvas) => canvas.lifecycle === lifecycle)) {
    return
  }

  recordWorkspaceMutation()
  const id = lifecycle
  canvases.value = [...canvases.value, {
    id,
    nameKey: `canvas.${lifecycle}`,
    lifecycle,
    nodes: [],
    edges: [],
  }]
  activeCanvasId.value = id
  canvasMenuOpen.value = false
  markWorkspaceChanged()
  notice.value = t('canvas.added', { canvas: t(`canvas.${lifecycle}`) })
}

function removeCurrentCanvas(): void {
  if (currentCanvas.value.lifecycle === 'main') {
    notice.value = t('canvas.cannotRemoveMain')
    return
  }

  recordWorkspaceMutation()
  const removedCanvas = currentCanvas.value
  canvases.value = canvases.value.filter((canvas) => canvas.id !== removedCanvas.id)
  activeCanvasId.value = 'main'
  mobilePanel.value = null
  markWorkspaceChanged()
  notice.value = t('canvas.removed', { canvas: t(removedCanvas.nameKey) })
}

function selectNode(nodeId: string): void {
  currentCanvas.value.selectedNodeId = nodeId
  currentCanvas.value.selectedEdgeId = undefined
  mobilePanel.value = 'inspector'
}

function onNodeClick(event: { node: { id: string } }): void {
  selectNode(event.node.id)
}

function onEdgeClick(event: { edge: { id: string } }): void {
  currentCanvas.value.selectedNodeId = undefined
  currentCanvas.value.selectedEdgeId = event.edge.id
  mobilePanel.value = 'inspector'
}

function clearSelection(): void {
  currentCanvas.value.selectedNodeId = undefined
  currentCanvas.value.selectedEdgeId = undefined
}

function isValidConnection(connection: Connection): boolean {
  const semantic = resolveConnectionSemantic(connection.sourceHandle, connection.targetHandle)
  if (!semantic || connection.source === connection.target || !connection.source || !connection.target) {
    return false
  }

  const connectionId = 'id' in connection && typeof connection.id === 'string' ? connection.id : undefined
  const duplicate = edges.value.some((edge) =>
    edge.id !== connectionId
    &&
    edge.source === connection.source
    && edge.target === connection.target
    && edge.sourceHandle === connection.sourceHandle
    && edge.targetHandle === connection.targetHandle,
  )

  return !duplicate
}

function createEdge(connection: Connection, semantic: ConnectionSemantic, targetParameterId?: string): FlowEdge {
  const isExecution = semantic === 'execution'
  return {
    id: `${semantic}-${connection.source}-${connection.target}-${connection.targetHandle ?? 'flow'}`,
    source: connection.source,
    target: connection.target,
    sourceHandle: connection.sourceHandle,
    targetHandle: connection.targetHandle,
    type: 'smoothstep',
    markerEnd: {
      type: MarkerType.ArrowClosed,
      color: isExecution ? '#0369a1' : '#6d42a5',
      width: 14,
      height: 14,
    },
    label: isExecution ? t('edge.flow') : t('edge.value'),
    labelShowBg: true,
    labelBgPadding: [3, 5],
    labelBgBorderRadius: 2,
    data: { semantic, targetParameterId },
    class: isExecution ? 'edge-execution' : 'edge-data',
    ariaLabel: isExecution ? t('inspector.executionEdge') : t('inspector.dataEdge'),
  }
}

function onConnect(connection: Connection): void {
  const semantic = resolveConnectionSemantic(connection.sourceHandle, connection.targetHandle)
  if (!semantic) {
    notice.value = t('canvas.invalidConnection')
    return
  }

  if (!isValidConnection(connection)) {
    notice.value = t('canvas.duplicateConnection')
    return
  }

  recordWorkspaceMutation()
  const targetParameterId = semantic === 'data' ? connection.targetHandle?.replace('param-', '') : undefined
  edges.value = [...edges.value, createEdge(connection, semantic, targetParameterId)]

  if (semantic === 'data' && targetParameterId) {
    const targetNode = currentCanvas.value.nodes.find((node) => node.id === connection.target)
    const parameter = targetNode?.data.parameters.find((item) => item.id === targetParameterId)
    if (parameter && targetNode) {
      parameter.source = 'previousNode'
      parameter.sourceNodeId = connection.source
      parameter.sourcePortId = connection.sourceHandle ?? 'data-out'
      selectNode(targetNode.id)
    }
  }

  markWorkspaceChanged()
}

function onNodesChange(changes: NodeChange[]): void {
  if (isRestoringWorkspace || isSwitchingCanvas) {
    return
  }

  const removedIds = new Set(changes.filter((change) => change.type === 'remove').map((change) => change.id))
  const positionChanges = changes.filter((change) => change.type === 'position')
  const dragStarted = positionChanges.some((change) => change.dragging === true)
  const dragEnded = positionChanges.some((change) => change.dragging === false)
  const positionChangedWithoutDrag = positionChanges.length > 0 && !dragStarted && !nodeDragHistoryOpen

  if (removedIds.size > 0 || (dragStarted && !nodeDragHistoryOpen) || positionChangedWithoutDrag) {
    recordWorkspaceMutation()
  }
  if (dragStarted) {
    nodeDragHistoryOpen = true
  }
  if (dragEnded) {
    nodeDragHistoryOpen = false
  }

  if (removedIds.size > 0) {
    const removedEdges = edges.value.filter((edge) => removedIds.has(edge.source) || removedIds.has(edge.target))
    removedEdges.forEach(resetDataEdgeSource)
    edges.value = edges.value.filter((edge) => !removedIds.has(edge.source) && !removedIds.has(edge.target))
    if (currentCanvas.value.selectedNodeId && removedIds.has(currentCanvas.value.selectedNodeId)) {
      clearSelection()
    }
  }

  nodes.value = applyNodeChanges(changes, nodes.value as never) as unknown as FlowNode[]
  if (removedIds.size > 0 || positionChanges.length > 0) {
    markWorkspaceChanged()
  }
}

function onEdgesChange(changes: EdgeChange[]): void {
  if (isRestoringWorkspace || isSwitchingCanvas) {
    return
  }

  const removedIds = new Set(changes
    .filter((change) => change.type === 'remove')
    .map((change) => change.id)
    .filter((id) => edges.value.some((edge) => edge.id === id)))

  if (removedIds.size === 0) {
    return
  }

  const removedEdges = edges.value.filter((edge) => removedIds.has(edge.id))

  recordWorkspaceMutation()
  removedEdges.forEach(resetDataEdgeSource)
  edges.value = removeEdgesById(edges.value, removedIds)

  notice.value = t('canvas.edgeRemoved')
  if (removedEdges.some((edge) => edge.id === currentCanvas.value.selectedEdgeId)) {
    currentCanvas.value.selectedEdgeId = undefined
  }
  markWorkspaceChanged()
}

function resetDataEdgeSource(edge: FlowEdge): void {
  if (edge.data?.semantic !== 'data' || !edge.data.targetParameterId) {
    return
  }

  const targetNode = currentCanvas.value.nodes.find((node) => node.id === edge.target)
  const parameter = targetNode?.data.parameters.find((item) => item.id === edge.data?.targetParameterId)
  if (parameter?.source === 'previousNode' && parameter.sourceNodeId === edge.source) {
    parameter.source = 'literal'
    parameter.sourceNodeId = undefined
    parameter.sourcePortId = undefined
  }
}

function removeDataEdgeForParameter(nodeId: string, parameterId: string): void {
  const removedEdges = edges.value.filter((edge) =>
    edge.data?.semantic === 'data'
    && edge.target === nodeId
    && edge.data.targetParameterId === parameterId,
  )
  removedEdges.forEach(resetDataEdgeSource)
  edges.value = edges.value.filter((edge) => !removedEdges.some((removed) => removed.id === edge.id))
}

function updateParameterSource(nodeId: string, parameter: MethodParameter, event: Event): void {
  const source = (event.target as HTMLSelectElement).value as ParameterSource
  if (source === parameter.source) {
    return
  }

  recordWorkspaceMutation()
  removeDataEdgeForParameter(nodeId, parameter.id)
  parameter.source = source
  parameter.sourceNodeId = undefined
  parameter.sourcePortId = undefined
  markWorkspaceChanged()
}

function addNode(kind: NodeKind, titleKey: string, subtitleKey: string, position?: { x: number; y: number }): void {
  recordWorkspaceMutation()
  const number = nextNodeNumber.value++
  const id = `${kind}-${currentCanvas.value.id}-${number}`
  const column = currentCanvas.value.nodes.length % 3
  const row = Math.floor(currentCanvas.value.nodes.length / 3)
  const parameters = kind === 'trigger'
    ? []
    : [{ id: 'input', nameKey: 'parameter.value', valueKind: 'JSON', source: 'literal' as const, literalValue: '' }]

  const newNode: FlowNode = {
    id,
    type: 'workflow',
    position: position ?? { x: 120 + column * 300, y: 450 + row * 180 },
    width: 224,
    data: { kind, titleKey, subtitleKey, status: 'ready', hasDataOutput: true, parameters },
  }

  nodes.value = [...nodes.value, newNode]
  selectNode(id)
  markWorkspaceChanged()
  notice.value = t('canvas.nodeAdded')
}

function handleCanvasDragOver(event: DragEvent): void {
  if (!event.dataTransfer?.types.includes('application/sereinflow-node')) {
    return
  }

  event.preventDefault()
  event.dataTransfer.dropEffect = 'copy'
  isCanvasDropActive.value = true
}

function handleCanvasDragLeave(event: DragEvent): void {
  const currentTarget = event.currentTarget as HTMLElement | null
  const relatedTarget = event.relatedTarget as Node | null
  if (!currentTarget || (relatedTarget && currentTarget.contains(relatedTarget))) {
    return
  }

  isCanvasDropActive.value = false
}

function handleCanvasDrop(event: DragEvent): void {
  event.preventDefault()
  isCanvasDropActive.value = false
  const encoded = event.dataTransfer?.getData('application/sereinflow-node')
  if (!encoded) {
    return
  }

  try {
    const item = JSON.parse(encoded) as { kind: NodeKind; titleKey: string; subtitleKey: string }
    if (!isNodeKind(item.kind)) {
      return
    }

    const position = screenToFlowCoordinate({ x: event.clientX, y: event.clientY })
    addNode(item.kind, item.titleKey, item.subtitleKey, { x: Math.round(position.x / 16) * 16, y: Math.round(position.y / 16) * 16 })
  } catch {
    notice.value = t('canvas.invalidNodeDrop')
  }
}

function removeSelection(): void {
  if (selectedEdge.value) {
    recordWorkspaceMutation()
    resetDataEdgeSource(selectedEdge.value)
    edges.value = edges.value.filter((edge) => edge.id !== selectedEdge.value?.id)
    currentCanvas.value.selectedEdgeId = undefined
    markWorkspaceChanged()
    notice.value = t('canvas.edgeRemoved')
    return
  }

  if (selectedNode.value) {
    recordWorkspaceMutation()
    const nodeId = selectedNode.value.id
    const connected = edges.value.filter((edge) => edge.source === nodeId || edge.target === nodeId)
    connected.forEach(resetDataEdgeSource)
    edges.value = edges.value.filter((edge) => edge.source !== nodeId && edge.target !== nodeId)
    nodes.value = nodes.value.filter((node) => node.id !== nodeId)
    currentCanvas.value.selectedNodeId = undefined
    markWorkspaceChanged()
  }
}

function runFlow(): void {
  isRunning.value = !isRunning.value
  nodes.value = nodes.value.map((node, index) => ({
    ...node,
    data: { ...node.data, status: isRunning.value && index === 2 ? 'active' : isRunning.value && index < 2 ? 'success' : 'ready' },
  }))
}

function saveRecoveryDraft(snapshot: WorkspaceSnapshot): void {
  try {
    saveWorkspace(snapshot)
  } catch {
    // A recovery draft must not mask a server save result.
  }
}

async function saveFlow(): Promise<void> {
  if (isSaving.value || isWorkspaceLoading.value) {
    return
  }

  const snapshot = currentWorkspaceSnapshot()
  const snapshotFingerprint = workspaceFingerprint(snapshot)
  saveRecoveryDraft(snapshot)
  isSaving.value = true
  saveFailed.value = false
  saveConflict.value = false

  try {
    let savedDefinition
    if (!projectId.value || !flowId.value) {
      const newFlowId = crypto.randomUUID()
      const definition = workspaceToFlowDefinition(snapshot, { id: newFlowId, version: 1 })
      const workspace = await createProject({ name: projectName.value, definition })
      projectId.value = workspace.project.id
      projectName.value = workspace.project.name
      flowId.value = newFlowId
      savedDefinition = definition
      projectWorkspaces.value = [
        ...projectWorkspaces.value.filter((item) => item.project.id !== workspace.project.id),
        {
          project: workspace.project,
          flows: [{ id: newFlowId, version: savedDefinition.version, entryNodeId: savedDefinition.entryNodeId }],
        },
      ]
    } else {
      const definition = workspaceToFlowDefinition(snapshot, { id: flowId.value, version: flowVersion.value })
      savedDefinition = await saveFlowRequest(projectId.value, flowId.value, {
        expectedVersion: flowVersion.value,
        definition,
      })
    }

    flowVersion.value = savedDefinition.version
    projectWorkspaces.value = projectWorkspaces.value.map((item) => item.project.id === projectId.value
      ? {
          ...item,
          project: { ...item.project, name: projectName.value, updatedAt: new Date().toISOString() },
          flows: item.flows.some((flow) => flow.id === flowId.value)
            ? item.flows.map((flow) => flow.id === flowId.value ? { ...flow, version: savedDefinition.version, entryNodeId: savedDefinition.entryNodeId } : flow)
            : [...item.flows, { id: flowId.value!, version: savedDefinition.version, entryNodeId: savedDefinition.entryNodeId }],
        }
      : item)
    savedWorkspaceFingerprint.value = snapshotFingerprint
    refreshDirtyState()
    notice.value = t('canvas.savedNow')
  } catch (error) {
    if (error instanceof FlowApiError && error.status === 409) {
      saveConflict.value = true
      notice.value = t('canvas.saveConflictNow')
    } else {
      saveFailed.value = true
      notice.value = t('canvas.saveFailed')
    }
  } finally {
    isSaving.value = false
  }
}

function applyServerWorkspace(definition: ReturnType<typeof workspaceToFlowDefinition>): void {
  const workspace = flowDefinitionToWorkspace(definition)
  isRestoringWorkspace = true
  canvasMountRevision.value += 1
  canvases.value = workspace.canvases
  activeCanvasId.value = workspace.activeCanvasId
  nextNodeNumber.value = workspace.nextNodeNumber
  workspaceHistory.clear()
  syncHistoryAvailability()
  savedWorkspaceFingerprint.value = workspaceFingerprint(workspace)
  isDirty.value = false
  saveFailed.value = false
  saveConflict.value = false
  localizeEdges()
  void nextTick().then(() => {
    isRestoringWorkspace = false
  })
}

async function openProject(workspace: ProjectWorkspaceDto): Promise<void> {
  const summary = workspace.flows[0]
  if (!summary) {
    notice.value = t('project.noFlow')
    return
  }

  projectMenuOpen.value = false
  isWorkspaceLoading.value = true
  try {
    const definition = await loadFlow(workspace.project.id, summary.id)
    projectId.value = workspace.project.id
    projectName.value = workspace.project.name
    flowId.value = definition.id
    flowVersion.value = definition.version
    applyServerWorkspace(definition)
    notice.value = t('project.switched', { project: workspace.project.name })
  } catch {
    notice.value = t('canvas.loadFailed')
  } finally {
    isWorkspaceLoading.value = false
  }
}

function startNewProject(): void {
  projectMenuOpen.value = false
  projectId.value = undefined
  flowId.value = undefined
  flowVersion.value = 1
  projectName.value = t('project.newProject')
  restoreWorkspace({ canvases: createInitialCanvases(), activeCanvasId: 'main', nextNodeNumber: 1 })
  workspaceHistory.clear()
  savedWorkspaceFingerprint.value = ''
  isDirty.value = true
  saveFailed.value = false
  saveConflict.value = false
  notice.value = t('project.newProjectStarted')
}

async function initializeWorkspace(): Promise<void> {
  isWorkspaceLoading.value = true
  try {
    const workspaces = await listProjects()
    projectWorkspaces.value = workspaces
    const workspace = workspaces[0]
    if (workspace?.flows[0]) {
      const definition = await loadFlow(workspace.project.id, workspace.flows[0].id)
      projectId.value = workspace.project.id
      projectName.value = workspace.project.name
      flowId.value = definition.id
      flowVersion.value = definition.version
      applyServerWorkspace(definition)
      notice.value = t('canvas.loadedFromServer')
      return
    }

    // An empty server database remains an unsaved blank workspace. Do not
    // manufacture a sample project on first launch; saving is the explicit
    // boundary that creates a project in the API.
    projectId.value = undefined
    flowId.value = undefined
    flowVersion.value = 1
    projectName.value = t('project.newProject')
    const snapshot: WorkspaceSnapshot = {
      canvases: createInitialCanvases(),
      activeCanvasId: 'main',
      nextNodeNumber: 1,
    }
    restoreWorkspace(snapshot)
    workspaceHistory.clear()
    syncHistoryAvailability()
    savedWorkspaceFingerprint.value = workspaceFingerprint(snapshot)
    isDirty.value = false
    saveFailed.value = false
    saveConflict.value = false
    notice.value = ''
  } catch {
    if (recoveryWorkspace) {
      restoreWorkspace(recoveryWorkspace)
      savedWorkspaceFingerprint.value = ''
      isDirty.value = true
      notice.value = t('canvas.recoveredDraft')
    } else {
      isDirty.value = true
      saveFailed.value = true
      notice.value = t('canvas.loadFailed')
    }
  } finally {
    isWorkspaceLoading.value = false
  }
}

function nodeTitle(node: FlowNode): string {
  return node.data.displayName?.trim() || t(node.data.titleKey)
}

function sourceNodeTitle(parameter: MethodParameter): string {
  const source = currentCanvas.value.nodes.find((node) => node.id === parameter.sourceNodeId)
  return source ? nodeTitle(source) : t('parameter.previousNode')
}

function isTextEntryTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) {
    return false
  }

  return target.isContentEditable
    || target instanceof HTMLInputElement
    || target instanceof HTMLTextAreaElement
    || target instanceof HTMLSelectElement
}

function handleWorkspaceShortcut(event: KeyboardEvent): void {
  const key = event.key.toLocaleLowerCase()
  const hasModifier = event.ctrlKey || event.metaKey

  if (hasModifier && key === 's') {
    event.preventDefault()
    saveFlow()
    return
  }

  if (isTextEntryTarget(event.target)) {
    return
  }

  if (hasModifier && key === 'z') {
    event.preventDefault()
    if (event.shiftKey) {
      redo()
    } else {
      undo()
    }
    return
  }

  if (hasModifier && key === 'y') {
    event.preventDefault()
    redo()
  }
}

onMounted(() => {
  window.addEventListener('keydown', handleWorkspaceShortcut)
  void initializeWorkspace()
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleWorkspaceShortcut)
})

function setLanguage(nextLocale: Locale): void {
  setLocale(nextLocale)
  languageMenuOpen.value = false
}
</script>

<template>
  <div class="app-shell">
    <header class="command-bar">
      <div class="brand-lockup">
        <div class="brand-mark" aria-hidden="true"><Activity :size="18" :stroke-width="2.4" /></div>
        <span class="brand-name">SereinFlow</span><span class="brand-divider" aria-hidden="true"></span>
        <div class="project-menu">
          <button class="project-picker" type="button" :title="t('command.switchProject')" :aria-expanded="projectMenuOpen" @click="projectMenuOpen = !projectMenuOpen"><span>{{ projectName }}</span><ChevronDown :size="14" /></button>
          <div v-if="projectMenuOpen" class="project-popover" role="menu">
            <span class="project-popover__label">{{ t('project.switchProject') }}</span>
            <button v-for="workspace in projectWorkspaces" :key="workspace.project.id" type="button" role="menuitem" :class="{ active: workspace.project.id === projectId }" @click="openProject(workspace)">{{ workspace.project.name }}<span>{{ workspace.flows.length }} {{ t('project.flows') }}</span></button>
            <span v-if="projectWorkspaces.length === 0" class="project-popover__empty">{{ t('project.noProjects') }}</span>
            <button class="project-popover__new" type="button" role="menuitem" @click="startNewProject"><Plus :size="14" />{{ t('project.newProject') }}</button>
          </div>
        </div>
      </div>
      <div class="command-actions">
        <button class="icon-button" type="button" :title="t('command.undo')" :aria-label="t('command.undo')" :disabled="!canUndo" @click="undo"><RotateCcw :size="16" /></button>
        <button class="icon-button" type="button" :title="t('command.redo')" :aria-label="t('command.redo')" :disabled="!canRedo" @click="redo"><RotateCw :size="16" /></button><span class="command-divider" aria-hidden="true"></span>
        <button class="command-button quiet" type="button" :title="t('command.save')" :disabled="!isDirty || isSaving || isWorkspaceLoading" @click="saveFlow"><Save :size="15" /><span>{{ t('command.save') }}</span></button>
        <button class="command-button run" type="button" :aria-pressed="isRunning" @click="runFlow"><Square v-if="isRunning" :size="14" fill="currentColor" /><Play v-else :size="14" fill="currentColor" /><span>{{ isRunning ? t('command.stop') : t('command.run') }}</span></button>
        <div class="language-menu">
          <button class="language-button" type="button" :title="t('command.language')" :aria-label="t('command.language')" :aria-expanded="languageMenuOpen" @click="languageMenuOpen = !languageMenuOpen"><Languages :size="16" /><span>{{ locale === 'zh-CN' ? 'ZH' : 'EN' }}</span><ChevronDown :size="13" /></button>
          <div v-if="languageMenuOpen" class="language-popover" role="menu"><button type="button" role="menuitemradio" :aria-checked="locale === 'zh-CN'" :class="{ active: locale === 'zh-CN' }" @click="setLanguage('zh-CN')">{{ t('language.zh') }}</button><button type="button" role="menuitemradio" :aria-checked="locale === 'en-US'" :class="{ active: locale === 'en-US' }" @click="setLanguage('en-US')">{{ t('language.en') }}</button></div>
        </div>
        <button class="avatar" type="button" :title="t('command.workspaceSettings')" :aria-label="t('command.workspaceSettings')">SF</button>
      </div>
    </header>

    <div class="mobile-tabs" role="tablist" :aria-label="t('mobile.workspacePanels')"><button type="button" :class="{ active: mobilePanel === 'nodes' }" @click="mobilePanel = mobilePanel === 'nodes' ? null : 'nodes'"><LayoutGrid :size="15" />{{ t('mobile.nodes') }}</button><button type="button" :class="{ active: mobilePanel === 'inspector' }" @click="mobilePanel = mobilePanel === 'inspector' ? null : 'inspector'"><Settings2 :size="15" />{{ t('mobile.inspector') }}</button></div>

    <main class="workspace-grid">
      <aside class="node-library" :class="{ 'mobile-visible': mobilePanel === 'nodes' }">
        <div class="panel-heading"><div><span class="eyebrow">{{ t('library.build') }}</span><h1>{{ t('library.nodeLibrary') }}</h1></div></div>
        <div class="library-empty"><p class="empty-copy">{{ t('library.empty') }}</p></div>
        <div class="library-footer"><div class="status-line"><span class="status-dot"></span><span>{{ t('library.workerConnected') }}</span><span class="mono">v0.1</span></div><button class="footer-link" type="button"><FolderOpen :size="14" />{{ t('library.openProject') }}</button></div>
      </aside>

      <section class="canvas-panel" :aria-label="t('canvas.mainHint')">
        <div class="canvas-toolbar">
          <div class="canvas-context"><div class="breadcrumb"><span>{{ t('canvas.projects') }}</span><ChevronDown :size="13" /><strong>{{ projectName }}</strong><span class="version-pill">v{{ flowVersion }}</span></div><div class="canvas-tab-row"><div class="canvas-tabs" role="tablist" :aria-label="t('canvas.options')"><button v-for="canvas in canvases" :id="`canvas-tab-${canvas.id}`" :key="canvas.id" type="button" role="tab" :aria-selected="canvas.id === activeCanvasId" :class="{ active: canvas.id === activeCanvasId }" @click="selectCanvas(canvas.id)">{{ t(canvas.nameKey) }}</button></div><div class="canvas-menu"><button class="icon-button compact" type="button" :title="t('canvas.add')" :aria-label="t('canvas.add')" :aria-expanded="canvasMenuOpen" :disabled="availableCanvasLifecycles.length === 0" @click="canvasMenuOpen = !canvasMenuOpen"><Plus :size="15" /></button><div v-if="canvasMenuOpen" class="canvas-popover" role="menu"><button v-for="lifecycle in availableCanvasLifecycles" :key="lifecycle" type="button" role="menuitem" @click="addCanvas(lifecycle)">{{ t(`canvas.${lifecycle}`) }}</button><p v-if="availableCanvasLifecycles.length === 0">{{ t('canvas.allLifecycleCanvases') }}</p></div></div><button class="icon-button compact" type="button" :title="t('canvas.remove')" :aria-label="t('canvas.remove')" :disabled="currentCanvas.lifecycle === 'main'" @click="removeCurrentCanvas"><X :size="15" /></button></div></div>
          <div class="canvas-tools"><span class="save-state" role="status"><Check v-if="!isDirty && !saveFailed && !saveConflict && !isSaving && !isWorkspaceLoading" :size="14" /><Save v-else :size="14" />{{ t(saveStateKey) }}</span><button class="icon-button" type="button" :title="t('command.delete')" :aria-label="t('command.delete')" :disabled="!selectedNode && !selectedEdge" @click="removeSelection"><Trash2 :size="16" /></button></div>
        </div>
        <div class="canvas-area" :class="{ 'canvas-drop-active': isCanvasDropActive }" @dragover="handleCanvasDragOver" @dragleave="handleCanvasDragLeave" @drop="handleCanvasDrop">
          <VueFlow :key="canvasRenderKey" :model-value="renderedElements" :node-types="nodeTypes" :connection-mode="ConnectionMode.Strict" :is-valid-connection="isValidConnection" :min-zoom="0.2" :max-zoom="2" :snap-to-grid="true" :snap-grid="[16, 16]" :fit-view-on-init="true" :delete-key-code="['Backspace', 'Delete']" class="serein-flow" @connect="onConnect" @nodes-change="onNodesChange" @edges-change="onEdgesChange" @node-click="onNodeClick" @edge-click="onEdgeClick" @pane-click="clearSelection" />
          <span v-if="isCanvasDropActive" class="canvas-drop-hint">{{ t('canvas.dropNode') }}</span>
          <p v-if="notice" class="canvas-notice" role="status">{{ notice }}</p><div class="canvas-legend" aria-hidden="true"><span><i class="legend-port execution"></i>{{ t('edge.flow') }}</span><span><i class="legend-port data"></i>{{ t('edge.value') }}</span></div>
          <div class="zoom-control" :aria-label="t('canvas.options')"><button type="button" :title="t('canvas.zoomOut')" :aria-label="t('canvas.zoomOut')" @click="zoomOut()">-</button><button type="button" :title="t('canvas.fitView')" :aria-label="t('canvas.fitView')" @click="fitView()"><LocateFixed :size="14" /></button><button type="button" :title="t('canvas.zoomIn')" :aria-label="t('canvas.zoomIn')" @click="zoomIn()">+</button></div>
        </div>
      </section>

      <aside class="inspector-panel" :class="{ 'mobile-visible': mobilePanel === 'inspector' }">
        <template v-if="selectedNode">
          <div class="inspector-heading"><div><span class="eyebrow">{{ t('inspector.title') }}</span><h2>{{ nodeTitle(selectedNode) }}</h2></div><button class="icon-button" type="button" :title="t('command.close')" :aria-label="t('command.close')" @click="mobilePanel = null"><X :size="16" /></button></div>
          <div class="inspector-type"><span class="node-icon" :class="`kind-${selectedNode.data.kind}`"><component :is="iconForNodeKind(selectedNode.data.kind)" :size="15" /></span><span>{{ t('inspector.nodeType', { kind: t(`node.kind.${selectedNode.data.kind}`) }) }}</span><span class="inspector-id mono">#{{ selectedNode.id }}</span></div>
          <div class="inspector-section"><span class="section-label">{{ t('inspector.general') }}</span><label class="field-label">{{ t('inspector.displayName') }}<input v-model="selectedNode.data.displayName" type="text" :placeholder="t(selectedNode.data.titleKey)" @focus="beginTextEdit" @input="commitTextEdit" @blur="discardTextEdit" /></label><label class="field-label">{{ t('inspector.description') }}<textarea v-model="selectedNode.data.description" rows="2" :placeholder="t(selectedNode.data.subtitleKey)" @focus="beginTextEdit" @input="commitTextEdit" @blur="discardTextEdit"></textarea></label></div>
          <div class="inspector-section parameter-section"><span class="section-label">{{ t('inspector.parameters') }}</span><p v-if="selectedNode.data.parameters.length === 0" class="empty-copy">{{ t('inspector.noParameters') }}</p><div v-for="parameter in selectedNode.data.parameters" :key="parameter.id" class="parameter-editor"><div class="parameter-heading"><strong>{{ t(parameter.nameKey) }}</strong><span class="port-kind">{{ parameter.valueKind }}</span></div><label class="field-label compact">{{ t('parameter.source') }}<select :value="parameter.source" @change="updateParameterSource(selectedNode.id, parameter, $event)"><option value="literal">{{ t('parameter.literal') }}</option><option value="previousNode">{{ t('parameter.previousNode') }}</option><option value="projectInput">{{ t('parameter.projectInput') }}</option><option value="expression">{{ t('parameter.expression') }}</option></select></label><label v-if="parameter.source === 'literal'" class="field-label compact">{{ t('parameter.literalValue') }}<input v-model="parameter.literalValue" type="text" @focus="beginTextEdit" @input="commitTextEdit" @blur="discardTextEdit" /></label><label v-else-if="parameter.source === 'projectInput'" class="field-label compact">{{ t('parameter.projectInputKey') }}<input v-model="parameter.projectInputKey" type="text" @focus="beginTextEdit" @input="commitTextEdit" @blur="discardTextEdit" /></label><label v-else-if="parameter.source === 'expression'" class="field-label compact">{{ t('parameter.expressionValue') }}<textarea v-model="parameter.expression" rows="2" @focus="beginTextEdit" @input="commitTextEdit" @blur="discardTextEdit"></textarea></label><p v-else class="source-detail"><GitBranch :size="13" />{{ t('inspector.connectedFrom', { node: sourceNodeTitle(parameter) }) }}</p></div></div>
        </template>
        <template v-else-if="selectedEdge">
          <div class="inspector-heading"><div><span class="eyebrow">{{ t('inspector.title') }}</span><h2>{{ t('inspector.edgeSelected') }}</h2></div><button class="icon-button" type="button" :title="t('command.delete')" :aria-label="t('command.delete')" @click="removeSelection"><Trash2 :size="16" /></button></div><div class="edge-summary" :class="selectedEdge.data?.semantic"><span class="edge-sample"></span><strong>{{ selectedEdge.data?.semantic === 'execution' ? t('inspector.executionEdge') : t('inspector.dataEdge') }}</strong><p>{{ selectedEdge.data?.semantic === 'execution' ? t('edge.executionDescription') : t('edge.dataDescription') }}</p></div>
        </template>
        <div v-else class="inspector-empty"><Settings2 :size="20" /><strong>{{ t('canvas.emptySelection') }}</strong><p>{{ t('inspector.selectNode') }}</p></div>
      </aside>
    </main>

    <section class="output-panel"><div class="output-tabs" role="tablist" :aria-label="t('output.eventsLabel')"><button type="button" :class="{ active: activeOutput === 'events' }" @click="activeOutput = 'events'"><Terminal :size="14" />{{ t('output.events') }}<span class="tab-count">4</span></button><button type="button" :class="{ active: activeOutput === 'payload' }" @click="activeOutput = 'payload'"><Code2 :size="14" />{{ t('output.payload') }}</button><span class="output-spacer"></span><span class="run-label"><span class="status-dot"></span>{{ t('output.lastRunSucceeded') }}<span class="mono">184 ms</span></span></div><div class="output-content"><template v-if="activeOutput === 'events'"><div class="event-row"><span class="event-time mono">14:32:08.921</span><span class="event-dot success"></span><strong>{{ t('output.runCompleted') }}</strong><span class="event-detail">{{ t('output.nodesDuration', { count: 4, duration: '184 ms' }) }}</span></div><div class="event-row"><span class="event-time mono">14:32:08.783</span><span class="event-dot"></span><strong>{{ t('output.persistOrder') }}</strong><span class="event-detail">{{ t('output.sqliteInsert', { duration: '32 ms' }) }}</span></div></template><pre v-else class="payload-preview">{ "orderId": "ord_2048", "status": "accepted", "total": 128.40 }</pre></div></section>
  </div>
</template>
