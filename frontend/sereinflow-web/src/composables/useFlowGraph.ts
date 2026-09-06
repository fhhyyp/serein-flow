import { computed, ref, type ComputedRef, type Ref } from 'vue'
import { MarkerType, applyNodeChanges, type Connection, type EdgeChange, type NodeChange } from '@vue-flow/core'
import { applyNodePositionChanges, cloneCanvasGraph, removeEdgesById } from '../flow/canvasGraph'
import { canvasFocusState, type CanvasFocusSettingKey, type CanvasFocusSettings } from '../flow/canvasFocus'
import { canonicalParameterId, executionBranchFromHandle, resolveConnectionSemantic } from '../flow/connectionSeats'
import { connectionLineStyleFor, connectionLineTypeForEdge, connectionLineTypeOptions, type ConnectionLineSettings } from '../flow/connectionLine'
import type { CanvasState, ConnectionSemantic, FlowEdge, FlowEdgeLineType, FlowNode, MethodParameter, NodeKind, NodeRuntimeMetadata, ParameterSource, ScriptNodeData } from '../flow/types'
import { t } from '../i18n'

interface UseFlowGraphOptions {
  currentCanvas: ComputedRef<CanvasState>
  nextNodeNumber: Ref<number>
  connectionLineTypes: ConnectionLineSettings
  canvasFocusSettings: CanvasFocusSettings
  mobilePanel: Ref<'nodes' | 'inspector' | null>
  notice: Ref<string>
  isRestoringWorkspace: Ref<boolean>
  isSwitchingCanvas: Ref<boolean>
  recordWorkspaceMutation: () => void
  markWorkspaceChanged: () => void
}

export function useFlowGraph(options: UseFlowGraphOptions) {
  const currentCanvas = options.currentCanvas
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
  const renderedCanvas = computed(() => {
    const canvas = cloneCanvasGraph(currentCanvas.value)
    // cloneCanvasGraph deliberately copies graph elements only; the current
    // selection is transient editor state and must come from the original graph.
    const focus = canvasFocusState(
      canvas.nodes,
      canvas.edges,
      currentCanvas.value.selectedNodeId,
      currentCanvas.value.selectedEdgeId,
      options.canvasFocusSettings,
    )
    canvas.nodes = canvas.nodes.map((node) => {
      const focusClass = focus.active
        ? focus.focusedNodeIds.has(node.id) ? 'canvas-focus-highlighted' : 'canvas-focus-dimmed'
        : undefined
      const existingClasses = Array.isArray(node.class) ? node.class : node.class ? [node.class] : []
      return {
        ...node,
        selected: node.selected === true || node.id === currentCanvas.value.selectedNodeId,
        class: focusClass ? [...existingClasses, focusClass].join(' ') : node.class,
      }
    })
    canvas.edges = canvas.edges.map((edge) => ({
      ...edge,
      type: connectionLineTypeForEdge(edge, options.connectionLineTypes),
      label: undefined,
      labelShowBg: false,
      labelBgPadding: undefined,
      labelBgBorderRadius: undefined,
      class: [edge.class, focus.active
        ? focus.focusedEdgeIds.has(edge.id) ? 'canvas-focus-highlighted' : 'canvas-focus-dimmed'
        : undefined].filter(Boolean).join(' '),
    }))
    return canvas
  })
  const renderedElements = computed(() => [
    ...renderedCanvas.value.nodes,
    ...renderedCanvas.value.edges,
  ])
  const selectedNode = computed(() => currentCanvas.value.nodes.find((node) => node.id === currentCanvas.value.selectedNodeId)
    ?? currentCanvas.value.nodes.find((node) => node.selected))
  const selectedEdge = computed(() => currentCanvas.value.edges.find((edge) => edge.id === currentCanvas.value.selectedEdgeId)
    ?? currentCanvas.value.edges.find((edge) => edge.selected))
  const selectedNodeCount = computed(() => {
    const selected = currentCanvas.value.nodes.filter((node) => node.selected).length
    return selected || (currentCanvas.value.selectedNodeId
      && currentCanvas.value.nodes.some((node) => node.id === currentCanvas.value.selectedNodeId) ? 1 : 0)
  })
  const selectedEdgeCount = computed(() => {
    const selected = currentCanvas.value.edges.filter((edge) => edge.selected).length
    return selected || (currentCanvas.value.selectedEdgeId
      && currentCanvas.value.edges.some((edge) => edge.id === currentCanvas.value.selectedEdgeId) ? 1 : 0)
  })
  const nodeDragHistoryOpen = ref(false)

  function syncPrimaryNodeSelection(): void {
    const selectedNodeIds = nodes.value.filter((node) => node.selected).map((node) => node.id)
    if (selectedNodeIds.length === 0) {
      currentCanvas.value.selectedNodeId = undefined
      return
    }

    if (currentCanvas.value.selectedNodeId && selectedNodeIds.includes(currentCanvas.value.selectedNodeId)) {
      return
    }

    currentCanvas.value.selectedNodeId = selectedNodeIds.at(-1)
  }

  function syncPrimaryEdgeSelection(): void {
    const selectedEdgeIds = edges.value.filter((edge) => edge.selected).map((edge) => edge.id)
    if (selectedEdgeIds.length === 0) {
      currentCanvas.value.selectedEdgeId = undefined
      return
    }

    if (currentCanvas.value.selectedEdgeId && selectedEdgeIds.includes(currentCanvas.value.selectedEdgeId)) {
      return
    }

    currentCanvas.value.selectedEdgeId = selectedEdgeIds.at(-1)
  }

  function selectNode(nodeId: string): void {
    nodes.value = nodes.value.map((node) => ({ ...node, selected: node.id === nodeId }))
    edges.value = edges.value.map((edge) => edge.selected ? { ...edge, selected: false } : edge)
    currentCanvas.value.selectedNodeId = nodeId
    currentCanvas.value.selectedEdgeId = undefined
    options.mobilePanel.value = 'inspector'
  }

  function onNodeClick(event: { node: { id: string } }): void {
    const node = nodes.value.find((item) => item.id === event.node.id)
    if (node?.selected) {
      currentCanvas.value.selectedNodeId = node.id
    } else {
      syncPrimaryNodeSelection()
    }
    currentCanvas.value.selectedEdgeId = undefined
    options.mobilePanel.value = 'inspector'
  }

  function onEdgeClick(event: { edge: { id: string } }): void {
    currentCanvas.value.selectedNodeId = undefined
    currentCanvas.value.selectedEdgeId = event.edge.id
    options.mobilePanel.value = 'inspector'
  }

  function clearSelection(): void {
    nodes.value = nodes.value.map((node) => node.selected ? { ...node, selected: false } : node)
    edges.value = edges.value.map((edge) => edge.selected ? { ...edge, selected: false } : edge)
    currentCanvas.value.selectedNodeId = undefined
    currentCanvas.value.selectedEdgeId = undefined
  }

  function selectAllNodes(): void {
    if (nodes.value.length === 0) return
    nodes.value = nodes.value.map((node) => ({ ...node, selected: true }))
    edges.value = edges.value.map((edge) => edge.selected ? { ...edge, selected: false } : edge)
    currentCanvas.value.selectedNodeId ??= nodes.value[0]?.id
    currentCanvas.value.selectedEdgeId = undefined
    options.mobilePanel.value = 'inspector'
  }

  function isValidConnection(connection: Connection): boolean {
    const semantic = resolveConnectionSemantic(connection.sourceHandle, connection.targetHandle)
    if (!semantic || connection.source === connection.target || !connection.source || !connection.target) {
      return false
    }

    const connectionId = 'id' in connection && typeof connection.id === 'string' ? connection.id : undefined
    const duplicate = edges.value.some((edge) =>
      edge.id !== connectionId
      && edge.source === connection.source
      && edge.target === connection.target
      && edge.sourceHandle === connection.sourceHandle
      && edge.targetHandle === connection.targetHandle,
    )
    return !duplicate
  }

  function createEdge(connection: Connection, semantic: ConnectionSemantic, targetParameterId?: string): FlowEdge {
    const isExecution = semantic === 'execution'
    const branch = isExecution ? executionBranchFromHandle(connection.sourceHandle) : undefined
    if (isExecution && !branch) {
      throw new Error('Execution connections must declare Success, Failure, or Error. 流程连接必须声明 Success、Failure 或 Error 分支。')
    }
    const lineType = connectionLineStyleFor(semantic, options.connectionLineTypes).lineType
    return {
      id: `${semantic}-${connection.source}-${connection.target}-${connection.targetHandle ?? 'flow'}`,
      source: connection.source,
      target: connection.target,
      sourceHandle: connection.sourceHandle,
      targetHandle: connection.targetHandle,
      type: lineType,
      markerEnd: {
        type: MarkerType.ArrowClosed,
        color: isExecution
          ? branch === 'failure' ? '#b45309' : branch === 'error' ? '#dc2626' : '#15803d'
          : '#6d42a5',
        width: 14,
        height: 14,
      },
      data: { semantic, targetParameterId, branch },
      class: isExecution ? `edge-execution branch-${branch}` : 'edge-data',
      ariaLabel: isExecution
        ? `${t('inspector.executionEdge')} · ${t(`branch.${branch}`)}`
        : t('inspector.dataEdge'),
    }
  }

  function updateConnectionLineType(semantic: ConnectionSemantic, event: Event): void {
    const value = (event.target as HTMLSelectElement | null)?.value as FlowEdgeLineType | undefined
    if (!value || !connectionLineTypeOptions.some((option) => option.value === value) || options.connectionLineTypes[semantic] === value) {
      return
    }

    options.recordWorkspaceMutation()
    options.connectionLineTypes[semantic] = value
    options.markWorkspaceChanged()
  }

  function updateCanvasFocusSetting(setting: CanvasFocusSettingKey, event: Event): void {
    const value = (event.target as HTMLInputElement | null)?.checked
    if (typeof value !== 'boolean' || options.canvasFocusSettings[setting] === value) {
      return
    }

    options.recordWorkspaceMutation()
    options.canvasFocusSettings[setting] = value
    options.markWorkspaceChanged()
  }

  function onConnect(connection: Connection): void {
    const semantic = resolveConnectionSemantic(connection.sourceHandle, connection.targetHandle)
    if (!semantic) {
      options.notice.value = t('canvas.invalidConnection')
      return
    }

    if (!isValidConnection(connection)) {
      options.notice.value = t('canvas.duplicateConnection')
      return
    }

    if (semantic === 'execution' && !executionBranchFromHandle(connection.sourceHandle)) {
      options.notice.value = t('canvas.invalidConnection')
      return
    }

    options.recordWorkspaceMutation()
    const targetParameterId = semantic === 'data' && connection.targetHandle
      ? canonicalParameterId(connection.targetHandle.replace(/^param-/, ''))
      : undefined
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

    options.markWorkspaceChanged()
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

  function onNodesChange(changes: NodeChange[]): void {
    if (options.isRestoringWorkspace.value || options.isSwitchingCanvas.value) {
      return
    }

    const removedIds = new Set(changes.filter((change) => change.type === 'remove').map((change) => change.id))
    const positionChanges = changes.filter((change) => change.type === 'position')
    const dragStarted = positionChanges.some((change) => change.dragging === true)
    const dragEnded = positionChanges.some((change) => change.dragging === false)
    const positionChangedWithoutDrag = positionChanges.length > 0 && !dragStarted && !nodeDragHistoryOpen.value

    if (removedIds.size > 0 || (dragStarted && !nodeDragHistoryOpen.value) || positionChangedWithoutDrag) {
      options.recordWorkspaceMutation()
    }
    if (dragStarted) {
      nodeDragHistoryOpen.value = true
    }
    if (dragEnded) {
      nodeDragHistoryOpen.value = false
    }

    if (removedIds.size > 0) {
      const removedEdges = edges.value.filter((edge) => removedIds.has(edge.source) || removedIds.has(edge.target))
      removedEdges.forEach(resetDataEdgeSource)
      edges.value = edges.value.filter((edge) => !removedIds.has(edge.source) && !removedIds.has(edge.target))
      if (currentCanvas.value.selectedNodeId && removedIds.has(currentCanvas.value.selectedNodeId)) {
        clearSelection()
      }
    }

    const changedNodes = applyNodeChanges(changes, nodes.value as never) as unknown as FlowNode[]
    nodes.value = applyNodePositionChanges(changedNodes, positionChanges)
    syncPrimaryNodeSelection()
    if (removedIds.size > 0 || positionChanges.length > 0) {
      options.markWorkspaceChanged()
    }
  }

  function onEdgesChange(changes: EdgeChange[]): void {
    if (options.isRestoringWorkspace.value || options.isSwitchingCanvas.value) {
      return
    }

    const removedIds = new Set(changes
      .filter((change) => change.type === 'remove')
      .map((change) => change.id)
      .filter((id) => edges.value.some((edge) => edge.id === id)))

    if (removedIds.size === 0) {
      syncPrimaryEdgeSelection()
      return
    }

    const removedEdges = edges.value.filter((edge) => removedIds.has(edge.id))
    options.recordWorkspaceMutation()
    removedEdges.forEach(resetDataEdgeSource)
    edges.value = removeEdgesById(edges.value, removedIds)
    options.notice.value = t('canvas.edgeRemoved')
    if (removedEdges.some((edge) => edge.id === currentCanvas.value.selectedEdgeId)) {
      currentCanvas.value.selectedEdgeId = undefined
    }
    syncPrimaryEdgeSelection()
    options.markWorkspaceChanged()
  }

  function removeDataEdgeForParameter(nodeId: string, parameterId: string): void {
    const removedEdges = edges.value.filter((edge) => edge.data?.semantic === 'data' && edge.target === nodeId && edge.data.targetParameterId === parameterId)
    removedEdges.forEach(resetDataEdgeSource)
    edges.value = edges.value.filter((edge) => !removedEdges.some((removed) => removed.id === edge.id))
  }

  function updateParameterSource(nodeId: string, parameter: MethodParameter, event: Event): void {
    const source = (event.target as HTMLSelectElement).value as ParameterSource
    if (source === parameter.source) {
      return
    }

    options.recordWorkspaceMutation()
    removeDataEdgeForParameter(nodeId, parameter.id)
    parameter.source = source
    parameter.sourceNodeId = undefined
    parameter.sourcePortId = undefined
    options.markWorkspaceChanged()
  }

  function addNode(kind: NodeKind, titleKey: string, subtitleKey: string, position?: { x: number; y: number }, metadata?: { displayName?: string; description?: string; runtime?: NodeRuntimeMetadata; parameters?: MethodParameter[]; script?: ScriptNodeData; hasDataOutput?: boolean }): void {
    options.recordWorkspaceMutation()
    const number = options.nextNodeNumber.value++
    const id = `${kind}-${currentCanvas.value.id}-${number}`
    const column = currentCanvas.value.nodes.length % 3
    const row = Math.floor(currentCanvas.value.nodes.length / 3)
    const parameters = metadata?.parameters?.map((parameter) => ({ ...parameter })) ?? []
    const newNode: FlowNode = {
      id,
      type: 'workflow',
      position: position ?? { x: 120 + column * 300, y: 450 + row * 180 },
      width: 224,
      data: {
        kind,
        titleKey,
        subtitleKey,
        displayName: metadata?.displayName,
        description: metadata?.description,
        runtime: metadata?.runtime ? { ...metadata.runtime } : undefined,
        status: 'ready',
        hasDataOutput: metadata?.hasDataOutput ?? true,
        parameters,
        script: metadata?.script ? { ...metadata.script, nodeId: id } : undefined,
      },
    }

    nodes.value = [...nodes.value, newNode]
    selectNode(id)
    options.markWorkspaceChanged()
    options.notice.value = t('canvas.nodeAdded')
  }

  function removeSelection(): void {
    const selectedNodeIds = new Set(nodes.value
      .filter((node) => node.selected || node.id === currentCanvas.value.selectedNodeId)
      .map((node) => node.id))
    if (selectedNodeIds.size > 0) {
      options.recordWorkspaceMutation()
      const connected = edges.value.filter((edge) => selectedNodeIds.has(edge.source) || selectedNodeIds.has(edge.target))
      connected.forEach(resetDataEdgeSource)
      edges.value = edges.value.filter((edge) => !selectedNodeIds.has(edge.source) && !selectedNodeIds.has(edge.target))
      nodes.value = nodes.value.filter((node) => !selectedNodeIds.has(node.id))
      currentCanvas.value.selectedNodeId = undefined
      currentCanvas.value.selectedEdgeId = undefined
      options.markWorkspaceChanged()
      return
    }

    const selectedEdgeIds = new Set(edges.value
      .filter((edge) => edge.selected || edge.id === currentCanvas.value.selectedEdgeId)
      .map((edge) => edge.id))
    if (selectedEdgeIds.size > 0) {
      options.recordWorkspaceMutation()
      const removedEdges = edges.value.filter((edge) => selectedEdgeIds.has(edge.id))
      removedEdges.forEach(resetDataEdgeSource)
      edges.value = edges.value.filter((edge) => !selectedEdgeIds.has(edge.id))
      currentCanvas.value.selectedEdgeId = undefined
      options.markWorkspaceChanged()
      options.notice.value = t('canvas.edgeRemoved')
    }
  }

  return {
    nodes,
    edges,
    renderedElements,
    selectedNode,
    selectedEdge,
    selectedNodeCount,
    selectedEdgeCount,
    selectNode,
    selectAllNodes,
    onNodeClick,
    onEdgeClick,
    clearSelection,
    isValidConnection,
    updateConnectionLineType,
    updateCanvasFocusSetting,
    onConnect,
    onNodesChange,
    onEdgesChange,
    resetDataEdgeSource,
    updateParameterSource,
    addNode,
    removeSelection,
  }
}
