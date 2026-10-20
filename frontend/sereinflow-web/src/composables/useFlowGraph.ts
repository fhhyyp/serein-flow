import { computed, ref, type ComputedRef, type Ref } from 'vue';
import { MarkerType, applyNodeChanges, type Connection, type EdgeChange, type EdgeUpdateEvent, type NodeChange } from '@vue-flow/core';
import { applyNodePositionChanges, cloneCanvasGraph, removeEdgesById } from '../flow/canvasGraph';
import { canvasFocusState, type CanvasFocusSettingKey, type CanvasFocusSettings } from '../flow/canvasFocus';
import { canonicalParameterId, executionBranchFromHandle, resolveConnectionSemantic } from '../flow/connectionSeats';
import { connectionLineStyleFor, connectionLineTypeForEdge, connectionLineTypeOptions, type ConnectionLineSettings } from '../flow/connectionLine';
import type { CanvasState, ConnectionSemantic, FlowEdge, FlowEdgeLineType, FlowNode, MethodParameter, NodeKind, NodeRuntimeMetadata, ParameterSource, ScriptNodeData } from '../flow/types';
import { t } from '../i18n';
interface UseFlowGraphOptions {
  currentCanvas: ComputedRef<CanvasState>;
  nextNodeNumber: Ref<number>;
  connectionLineTypes: ConnectionLineSettings;
  canvasFocusSettings: CanvasFocusSettings;
  mobilePanel: Ref<'nodes' | 'inspector' | null>;
  notice: Ref<string>;
  isRestoringWorkspace: Ref<boolean>;
  isSwitchingCanvas: Ref<boolean>;
  recordWorkspaceMutation: () => void;
  markWorkspaceChanged: () => void;
}
export function useFlowGraph(options: UseFlowGraphOptions) {
  const currentCanvas = options.currentCanvas;
  const nodes = computed<FlowNode[]>({
    get: () => currentCanvas.value.nodes,
    set: (value) => {
      currentCanvas.value.nodes = value;
    },
  });
  const edges = computed<FlowEdge[]>({
    get: () => currentCanvas.value.edges,
    set: (value) => {
      currentCanvas.value.edges = value;
    },
  });
  const renderedCanvas = computed(() => {
    const canvas = cloneCanvasGraph(currentCanvas.value);
    const focus = canvasFocusState(canvas.nodes, canvas.edges, currentCanvas.value.selectedNodeId, currentCanvas.value.selectedEdgeId, options.canvasFocusSettings);
    canvas.nodes = canvas.nodes.map((node) => {
      const focusClass = focus.active
        ? focus.focusedNodeIds.has(node.id) ? 'canvas-focus-highlighted' : 'canvas-focus-dimmed'
        : undefined;
      const existingClasses = Array.isArray(node.class) ? node.class : node.class ? [node.class] : [];
      const evaluatedValue: {
        selected: boolean;
        class: string | string[] | undefined;
        id: string;
        position: import("@vue-flow/core").XYPosition;
        width?: number;
        data: import("../flow/types").FlowNodeData;
        type: "workflow";
      } = {
        ...node,
        selected: node.selected === true || node.id === currentCanvas.value.selectedNodeId,
        class: focusClass ? [...existingClasses, focusClass].join(' ') : node.class,
      };
      return evaluatedValue;
    });
    canvas.edges = canvas.edges.map((edge) => {
      const resolveJoinResult = () => {
        const evaluatedValue14 = [edge.class, focus.active
            ? focus.focusedEdgeIds.has(edge.id) ? 'canvas-focus-highlighted' : 'canvas-focus-dimmed'
            : undefined];
        const filterResult7 = evaluatedValue14.filter(Boolean);
        const joinResult: string = filterResult7.join(' ');
        return joinResult;
      };
      const evaluatedValue1: {
        type: FlowEdgeLineType;
        label: undefined;
        labelShowBg: false;
        labelBgPadding: undefined;
        labelBgBorderRadius: undefined;
        class: string;
        id: string;
        source: string;
        target: string;
        selected?: boolean;
        sourceHandle?: string | null;
        targetHandle?: string | null;
        markerEnd?: import("@vue-flow/core").EdgeMarkerType;
        ariaLabel?: string;
        data: import("../flow/types").FlowEdgeData;
      } = ({
        ...edge,
        type: connectionLineTypeForEdge(edge, options.connectionLineTypes),
        label: undefined,
        labelShowBg: false,
        labelBgPadding: undefined,
        labelBgBorderRadius: undefined,
        class: resolveJoinResult(),
      });
      return evaluatedValue1;
    });
    return canvas;
  });
  const renderedElements = computed(() => [
    ...renderedCanvas.value.nodes,
    ...renderedCanvas.value.edges,
  ]);
  const selectedNode = computed(() => {
    const evaluatedValue2: FlowNode | undefined = currentCanvas.value.nodes.find((node) => node.id === currentCanvas.value.selectedNodeId)
      ?? currentCanvas.value.nodes.find((node) => node.selected);
    return evaluatedValue2;
  });
  const selectedEdge = computed(() => {
    const evaluatedValue3: FlowEdge | undefined = currentCanvas.value.edges.find((edge) => edge.id === currentCanvas.value.selectedEdgeId)
      ?? currentCanvas.value.edges.find((edge) => edge.selected);
    return evaluatedValue3;
  });
  const selectedNodeCount = computed(() => {
    const selected = currentCanvas.value.nodes.filter((node) => node.selected).length;
    const evaluatedValue4: number = selected || (currentCanvas.value.selectedNodeId
      && currentCanvas.value.nodes.some((node) => node.id === currentCanvas.value.selectedNodeId) ? 1 : 0);
    return evaluatedValue4;
  });
  const selectedEdgeCount = computed(() => {
    const selected = currentCanvas.value.edges.filter((edge) => edge.selected).length;
    const evaluatedValue5: number = selected || (currentCanvas.value.selectedEdgeId
      && currentCanvas.value.edges.some((edge) => edge.id === currentCanvas.value.selectedEdgeId) ? 1 : 0);
    return evaluatedValue5;
  });
  const nodeDragHistoryOpen = ref(false);
  function syncPrimaryNodeSelection(): void {
    const filterResult = nodes.value.filter((node) => node.selected);
    const selectedNodeIds = filterResult.map((node) => node.id);
    if (selectedNodeIds.length === 0) {
      currentCanvas.value.selectedNodeId = undefined;
      return;
    }
    if (currentCanvas.value.selectedNodeId && selectedNodeIds.includes(currentCanvas.value.selectedNodeId)) {
      return;
    }
    currentCanvas.value.selectedNodeId = selectedNodeIds.at(-1);
  }
  function syncPrimaryEdgeSelection(): void {
    const filterResult1 = edges.value.filter((edge) => edge.selected);
    const selectedEdgeIds = filterResult1.map((edge) => edge.id);
    if (selectedEdgeIds.length === 0) {
      currentCanvas.value.selectedEdgeId = undefined;
      return;
    }
    if (currentCanvas.value.selectedEdgeId && selectedEdgeIds.includes(currentCanvas.value.selectedEdgeId)) {
      return;
    }
    currentCanvas.value.selectedEdgeId = selectedEdgeIds.at(-1);
  }
  function selectNode(nodeId: string): void {
    nodes.value = nodes.value.map((node) => ({ ...node, selected: node.id === nodeId }));
    edges.value = edges.value.map((edge) => edge.selected ? { ...edge, selected: false } : edge);
    currentCanvas.value.selectedNodeId = nodeId;
    currentCanvas.value.selectedEdgeId = undefined;
    options.mobilePanel.value = 'inspector';
  }
  function onNodeClick(event: {
    node: {
      id: string;
    };
  }): void {
    const node = nodes.value.find((item) => item.id === event.node.id);
    if (node?.selected) {
      currentCanvas.value.selectedNodeId = node.id;
    }
    else {
      syncPrimaryNodeSelection();
    }
    currentCanvas.value.selectedEdgeId = undefined;
    options.mobilePanel.value = 'inspector';
  }
  function onEdgeClick(event: {
    edge: {
      id: string;
    };
  }): void {
    currentCanvas.value.selectedNodeId = undefined;
    currentCanvas.value.selectedEdgeId = event.edge.id;
    options.mobilePanel.value = 'inspector';
  }
  function clearSelection(): void {
    nodes.value = nodes.value.map((node) => node.selected ? { ...node, selected: false } : node);
    edges.value = edges.value.map((edge) => edge.selected ? { ...edge, selected: false } : edge);
    currentCanvas.value.selectedNodeId = undefined;
    currentCanvas.value.selectedEdgeId = undefined;
  }
  function selectAllNodes(): void {
    if (nodes.value.length === 0) {
      return;
    }
    nodes.value = nodes.value.map((node) => ({ ...node, selected: true }));
    edges.value = edges.value.map((edge) => edge.selected ? { ...edge, selected: false } : edge);
    currentCanvas.value.selectedNodeId ??= nodes.value[0]?.id;
    currentCanvas.value.selectedEdgeId = undefined;
    options.mobilePanel.value = 'inspector';
  }
  function isValidConnection(connection: Connection & {
    id?: string;
  }): boolean {
    const semantic = resolveConnectionSemantic(connection.sourceHandle, connection.targetHandle);
    if (!semantic || connection.source === connection.target || !connection.source || !connection.target) {
      return false;
    }
    const connectionId = connection.id;
    const duplicate = edges.value.some((edge) => edge.id !== connectionId
      && edge.source === connection.source
      && edge.target === connection.target
      && edge.sourceHandle === connection.sourceHandle
      && edge.targetHandle === connection.targetHandle);
    const occupiedParameter = semantic === 'data' && edges.value.some((edge) => edge.id !== connectionId
      && edge.data.semantic === 'data'
      && edge.target === connection.target
      && edge.targetHandle === connection.targetHandle);
    return !duplicate && !occupiedParameter;
  }
  function createEdge(connection: Connection, semantic: ConnectionSemantic, targetParameterId?: string): FlowEdge {
    const isExecution = semantic === 'execution';
    const branch = isExecution ? executionBranchFromHandle(connection.sourceHandle) : undefined;
    if (isExecution && !branch) {
      throw new Error('Execution connections must declare Success, Failure, or Error. 流程连接必须声明 Success、Failure 或 Error 分支。');
    }
    const lineType = connectionLineStyleFor(semantic, options.connectionLineTypes).lineType;
    const evaluatedValue6: FlowEdge = {
      id: `${semantic}-${connection.source}-${connection.sourceHandle ?? 'out'}-${connection.target}-${connection.targetHandle ?? 'flow'}`,
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
    };
    return evaluatedValue6;
  }
  function updateConnectionLineType(semantic: ConnectionSemantic, event: Event): void {
    const value = (event.target as HTMLSelectElement | null)?.value as FlowEdgeLineType | undefined;
    if (!value || !connectionLineTypeOptions.some((option) => option.value === value) || options.connectionLineTypes[semantic] === value) {
      return;
    }
    options.recordWorkspaceMutation();
    options.connectionLineTypes[semantic] = value;
    options.markWorkspaceChanged();
  }
  function updateCanvasFocusSetting(setting: CanvasFocusSettingKey, event: Event): void {
    const value = (event.target as HTMLInputElement | null)?.checked;
    if (typeof value !== 'boolean' || options.canvasFocusSettings[setting] === value) {
      return;
    }
    options.recordWorkspaceMutation();
    options.canvasFocusSettings[setting] = value;
    options.markWorkspaceChanged();
  }
  function onConnect(connection: Connection): void {
    const semantic = resolveConnectionSemantic(connection.sourceHandle, connection.targetHandle);
    if (!semantic) {
      options.notice.value = t('canvas.invalidConnection');
      return;
    }
    if (!isValidConnection(connection)) {
      options.notice.value = t('canvas.duplicateConnection');
      return;
    }
    if (semantic === 'execution' && !executionBranchFromHandle(connection.sourceHandle)) {
      options.notice.value = t('canvas.invalidConnection');
      return;
    }
    options.recordWorkspaceMutation();
    const resolveCanonicalParameterIdResult = () => {
      const replaceResult: string = connection.targetHandle!.replace(/^param-/, '');
      const canonicalParameterIdResult = canonicalParameterId(replaceResult);
      return canonicalParameterIdResult;
    };
    const targetParameterId = semantic === 'data' && connection.targetHandle
      ? resolveCanonicalParameterIdResult() : undefined;
    edges.value = [...edges.value, createEdge(connection, semantic, targetParameterId)];
    setDataEdgeSource(connection, targetParameterId);
    options.markWorkspaceChanged();
  }
  function onEdgeUpdate(updateEvent: EdgeUpdateEvent): void {
    const edge = updateEvent.edge;
    const connection = updateEvent.connection;
    if (options.isRestoringWorkspace.value || options.isSwitchingCanvas.value) {
      return;
    }
    const original = edges.value.find((item) => item.id === edge.id);
    if (!original || connection.source !== original.source || connection.sourceHandle !== original.sourceHandle) {
      return;
    }
    if (connection.target === original.target && connection.targetHandle === original.targetHandle) {
      return;
    }
    const semantic = resolveConnectionSemantic(connection.sourceHandle, connection.targetHandle);
    if (semantic !== original.data.semantic || !isValidConnection({ ...connection, id: original.id })) {
      options.notice.value = t('canvas.invalidConnection');
      return;
    }
    const resolveCanonicalParameterIdResult1 = () => {
      const replaceResult1: string = connection.targetHandle!.replace(/^param-/, '');
      const canonicalParameterIdResult1 = canonicalParameterId(replaceResult1);
      return canonicalParameterIdResult1;
    };
    const targetParameterId = semantic === 'data' && connection.targetHandle
      ? resolveCanonicalParameterIdResult1() : undefined;
    const replacement = createEdge(connection, semantic, targetParameterId);
    replacement.data.lineType = original.data.lineType;
    replacement.selected = original.selected;
    options.recordWorkspaceMutation();
    resetDataEdgeSource(original);
    edges.value = edges.value.map((item) => item.id === original.id ? replacement : item);
    if (currentCanvas.value.selectedEdgeId === original.id) {
      currentCanvas.value.selectedEdgeId = replacement.id;
    }
    setDataEdgeSource(connection, targetParameterId);
    options.markWorkspaceChanged();
    options.notice.value = t('canvas.edgeReconnected');
  }
  function setDataEdgeSource(connection: Connection, targetParameterId: string | undefined): void {
    if (!targetParameterId) {
      return;
    }
    const targetNode = currentCanvas.value.nodes.find((node) => node.id === connection.target);
    const parameter = targetNode?.data.parameters.find((item) => item.id === targetParameterId);
    if (parameter && targetNode) {
      parameter.source = 'previousNode';
      parameter.sourceNodeId = connection.source;
      parameter.sourcePortId = connection.sourceHandle ?? 'data-out';
      selectNode(targetNode.id);
    }
  }
  function resetDataEdgeSource(edge: FlowEdge): void {
    if (edge.data?.semantic !== 'data' || !edge.data.targetParameterId) {
      return;
    }
    const targetNode = currentCanvas.value.nodes.find((node) => node.id === edge.target);
    const parameter = targetNode?.data.parameters.find((item) => item.id === edge.data?.targetParameterId);
    if (parameter?.source === 'previousNode' && parameter.sourceNodeId === edge.source) {
      parameter.source = 'literal';
      parameter.sourceNodeId = undefined;
      parameter.sourcePortId = undefined;
    }
  }
  function onNodesChange(changes: NodeChange[]): void {
    if (options.isRestoringWorkspace.value || options.isSwitchingCanvas.value) {
      return;
    }
    const filterResult2 = changes.filter((change) => change.type === 'remove');
    const mapResult: Iterable<string> | null | undefined = filterResult2.map((change) => change.id);
    const removedIds = new Set(mapResult);
    const positionChanges = changes.filter((change) => change.type === 'position');
    const dragStarted = positionChanges.some((change) => change.dragging === true);
    const dragEnded = positionChanges.some((change) => change.dragging === false);
    const positionChangedWithoutDrag = positionChanges.length > 0 && !dragStarted && !nodeDragHistoryOpen.value;
    if (removedIds.size > 0 || (dragStarted && !nodeDragHistoryOpen.value) || positionChangedWithoutDrag) {
      options.recordWorkspaceMutation();
    }
    if (dragStarted) {
      nodeDragHistoryOpen.value = true;
    }
    if (dragEnded) {
      nodeDragHistoryOpen.value = false;
    }
    if (removedIds.size > 0) {
      const removedEdges = edges.value.filter((edge) => {
        const evaluatedValue7: boolean = removedIds.has(edge.source) || removedIds.has(edge.target);
        return evaluatedValue7;
      });
      removedEdges.forEach(resetDataEdgeSource);
      edges.value = edges.value.filter((edge) => {
        const evaluatedValue8: boolean = !removedIds.has(edge.source) && !removedIds.has(edge.target);
        return evaluatedValue8;
      });
      if (currentCanvas.value.selectedNodeId && removedIds.has(currentCanvas.value.selectedNodeId)) {
        clearSelection();
      }
    }
    const changedNodes = applyNodeChanges(changes, nodes.value as never) as unknown as FlowNode[];
    nodes.value = applyNodePositionChanges(changedNodes, positionChanges);
    syncPrimaryNodeSelection();
    if (removedIds.size > 0 || positionChanges.length > 0) {
      options.markWorkspaceChanged();
    }
  }
  function onEdgesChange(changes: EdgeChange[]): void {
    if (options.isRestoringWorkspace.value || options.isSwitchingCanvas.value) {
      return;
    }
    const filterResult3 = changes
      .filter((change) => change.type === 'remove');
    const mapResult1 = filterResult3.map((change) => change.id);
    const filterResult4: Iterable<string> | null | undefined = mapResult1.filter((id) => {
      const someResult: boolean = edges.value.some((edge) => edge.id === id);
      return someResult;
    });
    const removedIds = new Set(filterResult4);
    if (removedIds.size === 0) {
      syncPrimaryEdgeSelection();
      return;
    }
    const removedEdges = edges.value.filter((edge) => {
      const hasResult: boolean = removedIds.has(edge.id);
      return hasResult;
    });
    options.recordWorkspaceMutation();
    removedEdges.forEach(resetDataEdgeSource);
    edges.value = removeEdgesById(edges.value, removedIds);
    options.notice.value = t('canvas.edgeRemoved');
    if (removedEdges.some((edge) => edge.id === currentCanvas.value.selectedEdgeId)) {
      currentCanvas.value.selectedEdgeId = undefined;
    }
    syncPrimaryEdgeSelection();
    options.markWorkspaceChanged();
  }
  function removeDataEdgeForParameter(nodeId: string, parameterId: string): void {
    const removedEdges = edges.value.filter((edge) => edge.data?.semantic === 'data' && edge.target === nodeId && edge.data.targetParameterId === parameterId);
    removedEdges.forEach(resetDataEdgeSource);
    edges.value = edges.value.filter((edge) => {
      const evaluatedValue9: boolean = !removedEdges.some((removed) => removed.id === edge.id);
      return evaluatedValue9;
    });
  }
  function updateParameterSource(nodeId: string, parameter: MethodParameter, event: Event): void {
    const source = (event.target as HTMLSelectElement).value as ParameterSource;
    if (source === parameter.source) {
      return;
    }
    options.recordWorkspaceMutation();
    removeDataEdgeForParameter(nodeId, parameter.id);
    parameter.source = source;
    parameter.sourceNodeId = undefined;
    parameter.sourcePortId = undefined;
    options.markWorkspaceChanged();
  }
  function addNode(kind: NodeKind, titleKey: string, subtitleKey: string, position?: {
    x: number;
    y: number;
  }, metadata?: {
    displayName?: string;
    description?: string;
    runtime?: NodeRuntimeMetadata;
    parameters?: MethodParameter[];
    script?: ScriptNodeData;
    hasDataOutput?: boolean;
  }): void {
    options.recordWorkspaceMutation();
    const number = options.nextNodeNumber.value++;
    const id = `${kind}-${currentCanvas.value.id}-${number}`;
    const column = currentCanvas.value.nodes.length % 3;
    const row = Math.floor(currentCanvas.value.nodes.length / 3);
    const parameters = metadata?.parameters?.map((parameter) => ({ ...parameter })) ?? [];
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
    };
    nodes.value = [...nodes.value, newNode];
    selectNode(id);
    options.markWorkspaceChanged();
    options.notice.value = t('canvas.nodeAdded');
  }
  function removeSelection(): void {
    const filterResult5 = nodes.value
      .filter((node) => node.selected || node.id === currentCanvas.value.selectedNodeId);
    const mapResult2: Iterable<string> | null | undefined = filterResult5.map((node) => node.id);
    const selectedNodeIds = new Set(mapResult2);
    if (selectedNodeIds.size > 0) {
      options.recordWorkspaceMutation();
      const connected = edges.value.filter((edge) => {
        const evaluatedValue10: boolean = selectedNodeIds.has(edge.source) || selectedNodeIds.has(edge.target);
        return evaluatedValue10;
      });
      connected.forEach(resetDataEdgeSource);
      edges.value = edges.value.filter((edge) => {
        const evaluatedValue11: boolean = !selectedNodeIds.has(edge.source) && !selectedNodeIds.has(edge.target);
        return evaluatedValue11;
      });
      nodes.value = nodes.value.filter((node) => {
        const evaluatedValue12: boolean = !selectedNodeIds.has(node.id);
        return evaluatedValue12;
      });
      currentCanvas.value.selectedNodeId = undefined;
      currentCanvas.value.selectedEdgeId = undefined;
      options.markWorkspaceChanged();
      return;
    }
    const filterResult6 = edges.value
      .filter((edge) => edge.selected || edge.id === currentCanvas.value.selectedEdgeId);
    const mapResult3: Iterable<string> | null | undefined = filterResult6.map((edge) => edge.id);
    const selectedEdgeIds = new Set(mapResult3);
    if (selectedEdgeIds.size > 0) {
      options.recordWorkspaceMutation();
      const removedEdges = edges.value.filter((edge) => {
        const hasResult1: boolean = selectedEdgeIds.has(edge.id);
        return hasResult1;
      });
      removedEdges.forEach(resetDataEdgeSource);
      edges.value = edges.value.filter((edge) => {
        const evaluatedValue13: boolean = !selectedEdgeIds.has(edge.id);
        return evaluatedValue13;
      });
      currentCanvas.value.selectedEdgeId = undefined;
      options.markWorkspaceChanged();
      options.notice.value = t('canvas.edgeRemoved');
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
    onEdgeUpdate,
    onNodesChange,
    onEdgesChange,
    resetDataEdgeSource,
    updateParameterSource,
    addNode,
    removeSelection,
  };
}
