<script setup lang="ts">
import { unref } from 'vue';
type TemplateItem<T> = T extends readonly (infer Item)[] ? Item : T extends Record<string, infer Value> ? Value : T extends number ? number : never;
import { computed, markRaw, ref, watch } from 'vue';
import { Bug, LayoutGrid, ListOrdered, LockKeyhole, PackageOpen, Settings2, Terminal } from 'lucide-vue-next';
import { ConnectionMode, VueFlow } from '@vue-flow/core';
import FlowBezierEdge from '../flow/FlowBezierEdge.vue';
import FlowSmoothStepEdge from '../flow/FlowSmoothStepEdge.vue';
import FlowNodeCard from '../flow/FlowNodeCard.vue';
import RunSnapshotNodeInspector from './RunSnapshotNodeInspector.vue';
import RunSnapshotOutputViewer from './RunSnapshotOutputViewer.vue';
import RunWorkpiecePanel from './RunWorkpiecePanel.vue';
import RunVariablePanel from './RunVariablePanel.vue';
import DockablePanelGroup from '../workspace/DockablePanelGroup.vue';
import WorkspacePanelSwitcher, { type WorkspacePanelItem } from '../workspace/WorkspacePanelSwitcher.vue';
import { t } from '../../i18n';
import { flowDefinitionToWorkspace } from '../../flow/flowDtoMapper';
import { connectionLineTypeForEdge } from '../../flow/connectionLine';
import { buildNodeExecutionStates } from '../../flow/nodeExecutionState';
import { useRunSnapshotWorkspace } from '../../composables/useRunSnapshotWorkspace';
import type { NodeExecutionState } from '../../flow/nodeExecutionState';
import type { CanvasState, FlowEdge, FlowNode, NodeStatus } from '../../flow/types';
import type { DockPosition, DockablePanelGroupState, WorkspacePanelId, WorkspacePanelTab } from '../../flow/dockableWorkspace';
import type { FlowDefinitionDto, FlowRunEventDto, FlowRunOutputDto } from '../../api/flowApi';
import NodeExecutionInspector from '../debug/NodeExecutionInspector.vue';
const props = defineProps<{
  runId: string;
  definition?: FlowDefinitionDto;
  outputs: FlowRunOutputDto[];
  outputsError?: string;
  events: FlowRunEventDto[];
  eventsError?: string;
  isDebugRun?: boolean;
  live?: boolean;
}>();
const nodeTypes = markRaw({ workflow: FlowNodeCard });
const edgeTypes = markRaw({ default: FlowBezierEdge, smoothstep: FlowSmoothStepEdge });
const workspaceRoot = ref<HTMLElement>();
const activeCanvasId = ref('');
const selectedNodeId = ref('');
const selectedExecutionId = ref('');
const workspaceSnapPreview = ref<{
  groupId: string;
  targetGroupId?: string;
  dock?: DockPosition;
}>();
const bindingSource = useRunSnapshotWorkspace(workspaceRoot);
const { layout } = bindingSource;
const { groups: dockableGroups } = bindingSource;
const { visibleGroups } = bindingSource;
const { workspaceSize: dockableWorkspaceSize } = bindingSource;
const { layoutPriority: dockedLayoutPriority } = bindingSource;
const { canvasInsets: workspaceInsets } = bindingSource;
const { dockedExtentFor } = bindingSource;
const { dockedOffsetFor } = bindingSource;
const { updateGroup: updateDockableGroup } = bindingSource;
const { groupForPanel } = bindingSource;
const { activatePanel: activateDockablePanel } = bindingSource;
const { showPanel: showDockablePanel } = bindingSource;
const { hidePanel: hideDockablePanel } = bindingSource;
const { dockGroup: dockDockableGroup } = bindingSource;
const { combinePanel: combineDockablePanel } = bindingSource;
const { detachPanel: detachDockablePanel } = bindingSource;
const { focusGroup: focusDockableGroup } = bindingSource;
const { resetLayout: resetDockableLayout } = bindingSource;
const workspace = computed(() => {
  const evaluatedValue: import("../../flow/workspaceHistory").WorkspaceSnapshot | undefined = props.definition ? flowDefinitionToWorkspace(props.definition) : undefined;
  return evaluatedValue;
});
const activeCanvas = computed<CanvasState | undefined>(() => {
  const snapshot = workspace.value;
  if (!snapshot) {
    return undefined;
  }
  const evaluatedValue1: CanvasState | undefined = snapshot.canvases.find((canvas) => canvas.id === activeCanvasId.value) ?? snapshot.canvases[0];
  return evaluatedValue1;
});
const latestOutputs = computed(() => {
  const values = new Map<string, FlowRunOutputDto>();
  for (const output of [...props.outputs].sort((left, right) => left.sequence - right.sequence)) {
    values.set(output.nodeId, output);
  }
  return values;
});
const executionStates = computed(() => {
  const durableOutputsBySequence = new Map<number, FlowRunOutputDto>();
  const durableOutputsByNode = new Map<string, FlowRunOutputDto>();
  for (const output of [...props.outputs].sort((left, right) => left.sequence - right.sequence)) {
    durableOutputsBySequence.set(output.sequence, output);
    durableOutputsByNode.set(output.nodeId, output);
  }
  const states = buildNodeExecutionStates(props.events);
  const hydratedStates = states.map((state) => {
    const output = (state.terminalSequence !== undefined
      ? durableOutputsBySequence.get(state.terminalSequence)
      : undefined) ?? durableOutputsByNode.get(state.nodeId);
    if (!output) {
      return state;
    }
    return {
      ...state,
      inputs: state.inputs ?? output.inputs,
      outputs: state.outputs ?? output.outputs,
      errorCode: state.errorCode ?? output.errorCode,
      errorMessage: state.errorMessage ?? output.errorMessage,
    };
  });
  const mapResult = hydratedStates
    .map((state) => state.terminalSequence);
  const filterResult: Iterable<number> | null | undefined = mapResult.filter((sequence): sequence is number => sequence !== undefined);
  const hydratedOutputSequences = new Set(filterResult);
  for (const output of durableOutputsBySequence.values()) {
    if (hydratedOutputSequences.has(output.sequence)) {
      continue;
    }
    const status: NodeExecutionState['status'] = output.outcome === 'error'
      ? 'error'
      : output.outcome === 'failed' ? 'failed' : 'completed';
    hydratedStates.push({
      id: `${output.nodeId}:output-${output.sequence}`,
      nodeId: output.nodeId,
      status,
      startedAt: output.timestamp,
      endedAt: output.timestamp,
      terminalSequence: output.sequence,
      inputs: output.inputs,
      outputs: output.outputs,
      errorCode: output.errorCode,
      errorMessage: output.errorMessage,
    });
  }
  return hydratedStates;
});
const nodeNames = computed<Record<string, string>>(() => {
  const flatMapResult: Iterable<readonly [
    PropertyKey,
    string
  ]> = (workspace.value?.canvases ?? []).flatMap((canvas) => {
    const mapResult1: [
      string,
      string
    ] | readonly [
      string,
      string
    ][] = canvas.nodes.map((node) => {
      const evaluatedValue2: [
        string,
        string
      ] = [
        node.id,
        node.data.displayName?.trim() || t(node.data.titleKey),
      ];
      return evaluatedValue2;
    });
    return mapResult1;
  });
  const fromEntriesResult: Record<string, string> = Object.fromEntries(flatMapResult);
  return fromEntriesResult;
});
const selectedNode = computed(() => {
  const findResult: FlowNode | undefined = activeCanvas.value?.nodes.find((node) => node.id === selectedNodeId.value);
  return findResult;
});
const selectedWorkpieceExecutionId = computed(() => {
  const evaluatedValue3: string | undefined = executionStates.value.find((state) => state.id === selectedExecutionId.value)?.executionId;
  return evaluatedValue3;
});
const viewKey = computed(() => `${props.definition?.id ?? 'empty'}-${activeCanvas.value?.id ?? 'none'}`);
const renderedElements = computed<Array<FlowNode | FlowEdge>>(() => {
  const canvas = activeCanvas.value;
  if (!canvas) {
    return [];
  }
  const lineTypes = workspace.value?.connectionLineTypes;
  const evaluatedValue6: (FlowNode | FlowEdge)[] = [
    ...canvas.nodes.map((node) => {
      const evaluatedValue4: {
        selected: boolean;
        data: {
          status: NodeStatus;
          kind: import("../../flow/types").NodeKind;
          titleKey: string;
          subtitleKey: string;
          displayName?: string;
          description?: string;
          hasDataOutput: boolean;
          parameters: import("../../flow/types").MethodParameter[];
          script?: import("../../flow/types").ScriptNodeData;
          runtime?: import("../../flow/types").NodeRuntimeMetadata;
          breakpoint?: boolean;
          debugPaused?: boolean;
          breakpointLocked?: boolean;
          onToggleBreakpoint?: (nodeId: string) => void;
        };
        id: string;
        position: import("@vue-flow/core").XYPosition;
        width?: number;
        class?: string | string[];
        type: "workflow";
      } = ({
        ...node,
        selected: node.id === selectedNodeId.value,
        data: {
          ...node.data,
          status: nodeStatus(node.id),
        },
      });
      return evaluatedValue4;
    }),
    ...canvas.edges.map((edge) => {
      const evaluatedValue5: {
        type: import("../../flow/types").FlowEdgeLineType;
        label: undefined;
        labelShowBg: boolean;
        labelBgPadding: undefined;
        labelBgBorderRadius: undefined;
        id: string;
        source: string;
        target: string;
        selected?: boolean;
        sourceHandle?: string | null;
        targetHandle?: string | null;
        markerEnd?: import("@vue-flow/core").EdgeMarkerType;
        class?: string;
        ariaLabel?: string;
        data: import("../../flow/types").FlowEdgeData;
      } = ({
        ...edge,
        type: connectionLineTypeForEdge(edge, lineTypes),
        label: undefined,
        labelShowBg: false,
        labelBgPadding: undefined,
        labelBgBorderRadius: undefined,
      });
      return evaluatedValue5;
    }),
  ];
  return evaluatedValue6;
});
const panelDefinitions = computed<WorkspacePanelTab[]>(() => {
  const evaluatedValue7: WorkspacePanelTab[] = [
    { id: 'canvas', label: t('panel.canvas'), icon: markRaw(LayoutGrid), closable: false },
    { id: 'workpieces', label: t('panel.workpieces'), icon: markRaw(PackageOpen) },
    { id: 'variables', label: t('variables.title'), icon: markRaw(ListOrdered) },
    { id: 'debug', label: t('console.snapshotExecution'), icon: markRaw(ListOrdered) },
    { id: 'inspector', label: t('console.snapshotProperties'), icon: markRaw(Settings2) },
    { id: 'output', label: t('console.snapshotRunOutput'), icon: markRaw(Terminal) },
  ];
  return evaluatedValue7;
});
const panelSwitcherItems = computed<WorkspacePanelItem[]>(() => {
  const mapResult2: WorkspacePanelItem[] = panelDefinitions.value.map((panel) => ({
    ...panel,
    visible: layout.panels[panel.id].visible,
    available: true,
    required: panel.id === 'canvas',
  }));
  return mapResult2;
});
watch(() => props.definition?.id, () => {
  const snapshot = workspace.value;
  activeCanvasId.value = snapshot?.activeCanvasId ?? '';
  selectedNodeId.value = '';
  selectedExecutionId.value = '';
}, { immediate: true });
function nodeStatus(nodeId: string): NodeStatus {
  const output = latestOutputs.value.get(nodeId);
  if (!output) {
    return 'idle';
  }
  if (output.outcome === 'error') {
    return 'error';
  }
  if (output.outcome === 'failed') {
    return 'failed';
  }
  return 'success';
}
function canvasLabel(canvas: CanvasState): string {
  const evaluatedValue8: string = canvas.name?.trim() || t(canvas.nameKey);
  return evaluatedValue8;
}
function selectCanvas(canvasId: string): void {
  activeCanvasId.value = canvasId;
  selectedNodeId.value = '';
  selectedExecutionId.value = '';
}
function tabsForGroup(group: DockablePanelGroupState): WorkspacePanelTab[] {
  const filterResult1 = group.panelIds
    .filter((panelId) => layout.panels[panelId].visible);
  const mapResult3 = filterResult1.map((panelId) => {
    const findResult1: WorkspacePanelTab | undefined = panelDefinitions.value.find((definition) => definition.id === panelId);
    return findResult1;
  });
  const filterResult2: WorkspacePanelTab[] = mapResult3.filter((definition): definition is WorkspacePanelTab => {
    const booleanResult: boolean = Boolean(definition);
    return booleanResult;
  });
  return filterResult2;
}
function groupOptionsFor(groupId: string): Array<{
  id: string;
  label: string;
}> {
  const filterResult3 = visibleGroups.value
    .filter((group) => {
    const evaluatedValue9: boolean = group.id !== groupId && tabsForGroup(group).length > 0;
    return evaluatedValue9;
  });
  const mapResult4: {
    id: string;
    label: string;
  }[] = filterResult3.map((group) => {
    const evaluatedValue10: {
      id: string;
      label: string;
    } = ({
      id: group.id,
      label: panelDefinitions.value.find((definition) => definition.id === group.activePanelId)?.label ?? t('panel.emptyGroup'),
    });
    return evaluatedValue10;
  });
  return mapResult4;
}
function activatePanelFor(panelId: WorkspacePanelId): void {
  const group = groupForPanel(panelId);
  if (group) {
    activateDockablePanel(group.id, panelId);
  }
}
function selectNode(nodeId: string): void {
  selectedNodeId.value = nodeId;
  selectedExecutionId.value = executionStates.value.find((state) => state.nodeId === nodeId)?.id ?? '';
  showDockablePanel('inspector');
  activatePanelFor('inspector');
}
function selectExecutionNode(nodeId: string, executionId?: string): void {
  const canvas = workspace.value?.canvases.find((candidate) => {
    const someResult: boolean = candidate.nodes.some((node) => node.id === nodeId);
    return someResult;
  });
  if (canvas) {
    activeCanvasId.value = canvas.id;
  }
  selectedNodeId.value = nodeId;
  const selectedState = executionId
    ? executionStates.value.find((state) => state.id === executionId || state.executionId === executionId)
    : executionStates.value.find((state) => state.nodeId === nodeId);
  selectedExecutionId.value = selectedState?.id ?? executionId ?? '';
  showDockablePanel('debug');
  showDockablePanel('workpieces');
}
function selectExecution(execution: NodeExecutionState): void {
  const canvas = workspace.value?.canvases.find((candidate) => {
    const someResult1: boolean = candidate.nodes.some((node) => node.id === execution.nodeId);
    return someResult1;
  });
  if (canvas) {
    activeCanvasId.value = canvas.id;
  }
  selectedNodeId.value = execution.nodeId;
  selectedExecutionId.value = execution.id;
  showDockablePanel('workpieces');
}
function togglePanel(panelId: WorkspacePanelId): void {
  if (layout.panels[panelId].visible) {
    hideDockablePanel(panelId);
  }
  else {
    showDockablePanel(panelId);
  }
}
function updateWorkspaceGroup(groupId: string, patch: Partial<DockablePanelGroupState>): void {
  updateDockableGroup(groupId, patch);
}
function previewWorkspaceMove(groupId: string, event?: {
  dock?: DockPosition;
  targetGroupId?: string;
}): void {
  workspaceSnapPreview.value = event
    ? { groupId, dock: event.dock, targetGroupId: event.targetGroupId }
    : undefined;
}
function finishWorkspaceMove(groupId: string, event: {
  panelId: WorkspacePanelId;
  targetGroupId?: string;
  dockTarget?: DockPosition;
  moved: boolean;
  interaction: 'move' | 'resize';
}): void {
  const preview = workspaceSnapPreview.value?.groupId === groupId ? workspaceSnapPreview.value : undefined;
  workspaceSnapPreview.value = undefined;
  if (event.interaction === 'resize') {
    focusDockableGroup(groupId);
    return;
  }
  if (event.targetGroupId && event.targetGroupId !== groupId && event.moved) {
    combineDockablePanel(event.panelId, event.targetGroupId);
    return;
  }
  const dock = event.moved ? event.dockTarget ?? preview?.dock : undefined;
  if (dock) {
    dockDockableGroup(groupId, dock);
  }
  else {
    focusDockableGroup(groupId);
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
function readButtonItems() {
  return (workspace.value?.canvases ?? []);
}
function readButtonAriaSelected(canvas: TemplateItem<ReturnType<typeof readButtonItems>>) {
  return (canvas.id === activeCanvas.value?.id);
}
function readButtonClass(canvas: TemplateItem<ReturnType<typeof readButtonItems>>) {
  return ({ active: canvas.id === activeCanvas.value?.id });
}
function readTemplateItems() {
  const evaluatedValue11 = (unref(dockableGroups));
  return evaluatedValue11;
}
function readDockablePanelGroupIf(group: TemplateItem<ReturnType<typeof readTemplateItems>>) {
  const evaluatedValue12 = (tabsForGroup(group).length > 0);
  return evaluatedValue12;
}
function readDockablePanelGroupDropTargeted(group: TemplateItem<ReturnType<typeof readTemplateItems>>) {
  return (workspaceSnapPreview.value?.targetGroupId === group.id);
}
function readDivIf(panelId: any) {
  return (panelId === 'canvas');
}
const readVueFlowSnapGrid = computed<[
  number,
  number
]>(() => ([16, 16]));
const readPIf = computed(() => (!activeCanvas.value || activeCanvas.value.nodes.length === 0));
function readRunVariablePanelElseIf(panelId: any) {
  return (panelId === 'variables');
}
function readRunWorkpiecePanelElseIf(panelId: any) {
  return (panelId === 'workpieces');
}
const readRunWorkpiecePanelFocusNodeId = computed(() => (selectedNodeId.value || undefined));
const readRunWorkpiecePanelFocusExecutionId = computed(() => (selectedWorkpieceExecutionId.value || undefined));
function readNodeExecutionInspectorElseIf(panelId: any) {
  return (panelId === 'debug');
}
const readNodeExecutionInspectorSelectedNodeId = computed(() => (selectedNodeId.value || undefined));
const readNodeExecutionInspectorSelectedExecutionId = computed(() => (selectedExecutionId.value || undefined));
function readRunSnapshotNodeInspectorElseIf(panelId: any) {
  return (panelId === 'inspector');
}
const readRunSnapshotNodeInspectorStatus = computed(() => {
  const evaluatedValue13: NodeStatus = (selectedNode.value ? nodeStatus(selectedNode.value.id) : 'idle');
  return evaluatedValue13;
});
function readRunSnapshotOutputViewerElseIf(panelId: any) {
  return (panelId === 'output');
}
</script>

<template>
  <section
    class="run-snapshot-viewer"
    :aria-label="t('console.snapshotTitle')"
  >
    <header class="run-snapshot-viewer__toolbar">
      <div class="run-snapshot-viewer__toolbar-main">
        <div
          class="run-snapshot-viewer__tabs"
          role="tablist"
          :aria-label="t('canvas.options')"
        >
          <button
            v-for="canvas in readButtonItems()"
            :key="canvas.id"
            type="button"
            role="tab"
            :aria-selected="readButtonAriaSelected(canvas)"
            :class="readButtonClass(canvas)"
            @click="selectCanvas(canvas.id)"
          >{{ canvasLabel(canvas) }}</button>
        </div>
        <WorkspacePanelSwitcher
          :items="panelSwitcherItems"
          display-mode="debug"
          :debug-mode-available="true"
          :show-display-modes="false"
          @toggle="togglePanel"
          @reset="resetDockableLayout"
         />
      </div>
      <div class="run-snapshot-viewer__mode">
        <span
          v-if="props.isDebugRun"
          class="run-snapshot-viewer__debug"
        >
          <Bug :size="13" />
          {{ t('runs.kind.debug') }}
        </span>
        <span class="run-snapshot-viewer__readonly">
          <LockKeyhole :size="13" />
          {{ t('console.snapshotReadOnlyMode') }}
        </span>
      </div>
    </header>
    <div
      ref="workspaceRoot"
      class="run-snapshot-viewer__workspace"
    >
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
          @update:group="updateWorkspaceGroup(group.id, $event)"
          @activate="activateDockablePanel"
          @drag-preview="previewWorkspaceMove(group.id, $event)"
          @move-end="finishWorkspaceMove(group.id, $event)"
          @dock="dockWorkspacePanel"
          @combine="combineWorkspacePanel"
          @detach="detachWorkspacePanel"
          @close="closeWorkspacePanel"
        >
          <template #default="{ panelId }">
            <div
              v-if="readDivIf(panelId)"
              class="run-snapshot-viewer__canvas"
            >
              <VueFlow
                id="run-snapshot-viewer"
                :key="viewKey"
                :model-value="renderedElements"
                :node-types="nodeTypes"
                :edge-types="edgeTypes"
                :connection-mode="ConnectionMode.Strict"
                :nodes-draggable="false"
                :nodes-connectable="false"
                :edges-updatable="false"
                :elements-selectable="true"
                :delete-key-code="null"
                :min-zoom="0.2"
                :max-zoom="2"
                :snap-to-grid="true"
                :snap-grid="readVueFlowSnapGrid"
                :fit-view-on-init="true"
                :pan-on-drag="true"
                :zoom-on-scroll="true"
                :zoom-on-pinch="true"
                class="serein-flow run-snapshot-viewer__flow"
                @node-click="selectNode($event.node.id)"
               />
              <p
                v-if="readPIf"
                class="run-snapshot-viewer__empty"
              >{{ t('console.snapshotCanvasEmpty') }}</p>
              <div
                class="canvas-legend run-snapshot-viewer__legend"
                aria-hidden="true"
              >
                <span>
                  <i class="legend-port execution"></i>
                  {{ t('edge.flow') }}
                </span>
                <span>
                  <i class="legend-port data"></i>
                  {{ t('edge.value') }}
                </span>
              </div>
            </div>
            <RunVariablePanel
              v-else-if="readRunVariablePanelElseIf(panelId)"
              :run-id="props.runId"
              :live="props.live"
              :embedded="true"
             />
            <RunWorkpiecePanel
              v-else-if="readRunWorkpiecePanelElseIf(panelId)"
              :run-id="props.runId"
              :focus-node-id="readRunWorkpiecePanelFocusNodeId"
              :focus-execution-id="readRunWorkpiecePanelFocusExecutionId"
              :embedded="true"
              @select-node="selectExecutionNode"
             />
            <NodeExecutionInspector
              v-else-if="readNodeExecutionInspectorElseIf(panelId)"
              :executions="executionStates"
              :node-names="nodeNames"
              :selected-node-id="readNodeExecutionInspectorSelectedNodeId"
              :selected-execution-id="readNodeExecutionInspectorSelectedExecutionId"
              :compact="true"
              @select-execution="selectExecution"
             />
            <RunSnapshotNodeInspector
              v-else-if="readRunSnapshotNodeInspectorElseIf(panelId)"
              :node="selectedNode"
              :status="readRunSnapshotNodeInspectorStatus"
             />
            <RunSnapshotOutputViewer
              v-else-if="readRunSnapshotOutputViewerElseIf(panelId)"
              :events="props.events"
              :error="props.eventsError"
             />
          </template>
        </DockablePanelGroup>
      </template>
    </div>
  </section>
</template>
