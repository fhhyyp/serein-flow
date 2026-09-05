<script setup lang="ts">
import { computed, markRaw, ref, watch } from 'vue'
import { Bug, LayoutGrid, ListOrdered, LockKeyhole, PackageOpen, Settings2, Terminal } from 'lucide-vue-next'
import { ConnectionMode, VueFlow } from '@vue-flow/core'
import FlowNodeCard from '../flow/FlowNodeCard.vue'
import RunSnapshotNodeInspector from './RunSnapshotNodeInspector.vue'
import RunSnapshotOutputViewer from './RunSnapshotOutputViewer.vue'
import RunWorkpiecePanel from './RunWorkpiecePanel.vue'
import DockablePanelGroup from '../workspace/DockablePanelGroup.vue'
import WorkspacePanelSwitcher, { type WorkspacePanelItem } from '../workspace/WorkspacePanelSwitcher.vue'
import { t } from '../../i18n'
import { flowDefinitionToWorkspace } from '../../flow/flowDtoMapper'
import { connectionLineTypeForEdge } from '../../flow/connectionLine'
import { buildNodeExecutionStates } from '../../flow/nodeExecutionState'
import { useRunSnapshotWorkspace } from '../../composables/useRunSnapshotWorkspace'
import type { NodeExecutionState } from '../../flow/nodeExecutionState'
import type { CanvasState, FlowEdge, FlowNode, NodeStatus } from '../../flow/types'
import type { DockPosition, DockablePanelGroupState, WorkspacePanelId, WorkspacePanelTab } from '../../flow/dockableWorkspace'
import type { FlowDefinitionDto, FlowRunEventDto, FlowRunOutputDto } from '../../api/flowApi'
import NodeExecutionInspector from '../debug/NodeExecutionInspector.vue'

const props = defineProps<{
  runId: string
  definition?: FlowDefinitionDto
  outputs: FlowRunOutputDto[]
  outputsError?: string
  events: FlowRunEventDto[]
  eventsError?: string
  isDebugRun?: boolean
}>()

const nodeTypes = markRaw({ workflow: FlowNodeCard })
const workspaceRoot = ref<HTMLElement>()
const activeCanvasId = ref('')
const selectedNodeId = ref('')
const workspaceSnapPreview = ref<{ groupId: string; targetGroupId?: string; dock?: DockPosition }>()

const {
  layout,
  groups: dockableGroups,
  visibleGroups,
  workspaceSize: dockableWorkspaceSize,
  layoutPriority: dockedLayoutPriority,
  canvasInsets: workspaceInsets,
  dockedExtentFor,
  dockedOffsetFor,
  updateGroup: updateDockableGroup,
  groupForPanel,
  activatePanel: activateDockablePanel,
  showPanel: showDockablePanel,
  hidePanel: hideDockablePanel,
  dockGroup: dockDockableGroup,
  combinePanel: combineDockablePanel,
  detachPanel: detachDockablePanel,
  focusGroup: focusDockableGroup,
  resetLayout: resetDockableLayout,
} = useRunSnapshotWorkspace(workspaceRoot)

const workspace = computed(() => props.definition ? flowDefinitionToWorkspace(props.definition) : undefined)
const activeCanvas = computed<CanvasState | undefined>(() => {
  const snapshot = workspace.value
  if (!snapshot) return undefined
  return snapshot.canvases.find((canvas) => canvas.id === activeCanvasId.value) ?? snapshot.canvases[0]
})
const latestOutputs = computed(() => {
  const values = new Map<string, FlowRunOutputDto>()
  for (const output of [...props.outputs].sort((left, right) => left.sequence - right.sequence)) {
    values.set(output.nodeId, output)
  }
  return values
})
const executionStates = computed(() => {
  const durableOutputs = new Map<string, FlowRunOutputDto>()
  for (const output of [...props.outputs].sort((left, right) => left.sequence - right.sequence)) {
    durableOutputs.set(output.nodeId, output)
  }
  const states = buildNodeExecutionStates(props.events)
  const hydratedStates = states.map((state) => {
    const output = durableOutputs.get(state.nodeId)
    if (!output) return state
    return {
      ...state,
      inputs: state.inputs ?? output.inputs,
      outputs: state.outputs ?? output.outputs,
      errorCode: state.errorCode ?? output.errorCode,
      errorMessage: state.errorMessage ?? output.errorMessage,
    }
  })
  const hydratedNodeIds = new Set(hydratedStates.map((state) => state.nodeId))
  for (const output of durableOutputs.values()) {
    if (hydratedNodeIds.has(output.nodeId)) continue
    const status: NodeExecutionState['status'] = output.outcome === 'error'
      ? 'error'
      : output.outcome === 'failed' ? 'failed' : 'completed'
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
    })
  }
  return hydratedStates
})
const nodeNames = computed<Record<string, string>>(() => Object.fromEntries(
  (workspace.value?.canvases ?? []).flatMap((canvas) => canvas.nodes.map((node) => [
    node.id,
    node.data.displayName?.trim() || t(node.data.titleKey),
  ])),
))
const selectedNode = computed(() => activeCanvas.value?.nodes.find((node) => node.id === selectedNodeId.value))
const viewKey = computed(() => `${props.definition?.id ?? 'empty'}-${activeCanvas.value?.id ?? 'none'}`)
const renderedElements = computed<Array<FlowNode | FlowEdge>>(() => {
  const canvas = activeCanvas.value
  if (!canvas) return []
  const lineTypes = workspace.value?.connectionLineTypes
  return [
    ...canvas.nodes.map((node) => ({
      ...node,
      selected: node.id === selectedNodeId.value,
      data: {
        ...node.data,
        status: nodeStatus(node.id),
      },
    })),
    ...canvas.edges.map((edge) => ({
      ...edge,
      type: connectionLineTypeForEdge(edge, lineTypes),
      label: undefined,
      labelShowBg: false,
      labelBgPadding: undefined,
      labelBgBorderRadius: undefined,
    })),
  ]
})

const panelDefinitions = computed<WorkspacePanelTab[]>(() => [
  { id: 'canvas', label: t('panel.canvas'), icon: markRaw(LayoutGrid), closable: false },
  { id: 'workpieces', label: t('panel.workpieces'), icon: markRaw(PackageOpen) },
  { id: 'debug', label: t('console.snapshotExecution'), icon: markRaw(ListOrdered) },
  { id: 'inspector', label: t('console.snapshotProperties'), icon: markRaw(Settings2) },
  { id: 'output', label: t('console.snapshotRunOutput'), icon: markRaw(Terminal) },
])
const panelSwitcherItems = computed<WorkspacePanelItem[]>(() => panelDefinitions.value.map((panel) => ({
  ...panel,
  visible: layout.panels[panel.id].visible,
  available: true,
  required: panel.id === 'canvas',
})))

watch(() => props.definition?.id, () => {
  const snapshot = workspace.value
  activeCanvasId.value = snapshot?.activeCanvasId ?? ''
  selectedNodeId.value = ''
}, { immediate: true })

function nodeStatus(nodeId: string): NodeStatus {
  const output = latestOutputs.value.get(nodeId)
  if (!output) return 'idle'
  if (output.outcome === 'error') return 'error'
  if (output.outcome === 'failed') return 'failed'
  return 'success'
}

function canvasLabel(canvas: CanvasState): string {
  return canvas.name?.trim() || t(canvas.nameKey)
}

function selectCanvas(canvasId: string): void {
  activeCanvasId.value = canvasId
  selectedNodeId.value = ''
}

function tabsForGroup(group: DockablePanelGroupState): WorkspacePanelTab[] {
  return group.panelIds
    .filter((panelId) => layout.panels[panelId].visible)
    .map((panelId) => panelDefinitions.value.find((definition) => definition.id === panelId))
    .filter((definition): definition is WorkspacePanelTab => Boolean(definition))
}

function groupOptionsFor(groupId: string): Array<{ id: string; label: string }> {
  return visibleGroups.value
    .filter((group) => group.id !== groupId && tabsForGroup(group).length > 0)
    .map((group) => ({
      id: group.id,
      label: panelDefinitions.value.find((definition) => definition.id === group.activePanelId)?.label ?? t('panel.emptyGroup'),
    }))
}

function activatePanelFor(panelId: WorkspacePanelId): void {
  const group = groupForPanel(panelId)
  if (group) activateDockablePanel(group.id, panelId)
}

function selectNode(nodeId: string): void {
  selectedNodeId.value = nodeId
  showDockablePanel('inspector')
  activatePanelFor('inspector')
}

function selectExecutionNode(nodeId: string): void {
  const canvas = workspace.value?.canvases.find((candidate) => candidate.nodes.some((node) => node.id === nodeId))
  if (canvas) activeCanvasId.value = canvas.id
  selectedNodeId.value = nodeId
  showDockablePanel('debug')
  // The workpiece panel resolves the newest artifact produced by this node.
  showDockablePanel('workpieces')
}

function togglePanel(panelId: WorkspacePanelId): void {
  if (layout.panels[panelId].visible) hideDockablePanel(panelId)
  else showDockablePanel(panelId)
}

function updateWorkspaceGroup(groupId: string, patch: Partial<DockablePanelGroupState>): void {
  updateDockableGroup(groupId, patch)
}

function previewWorkspaceMove(groupId: string, event?: { dock?: DockPosition; targetGroupId?: string }): void {
  workspaceSnapPreview.value = event
    ? { groupId, dock: event.dock, targetGroupId: event.targetGroupId }
    : undefined
}

function finishWorkspaceMove(groupId: string, event: { panelId: WorkspacePanelId; targetGroupId?: string; dockTarget?: DockPosition; moved: boolean; interaction: 'move' | 'resize' }): void {
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
  const dock = event.moved ? event.dockTarget ?? preview?.dock : undefined
  if (dock) dockDockableGroup(groupId, dock)
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
</script>

<template>
  <section class="run-snapshot-viewer" :aria-label="t('console.snapshotTitle')">
    <header class="run-snapshot-viewer__toolbar">
      <div class="run-snapshot-viewer__toolbar-main">
        <div class="run-snapshot-viewer__tabs" role="tablist" :aria-label="t('canvas.options')">
          <button
            v-for="canvas in workspace?.canvases ?? []"
            :key="canvas.id"
            type="button"
            role="tab"
            :aria-selected="canvas.id === activeCanvas?.id"
            :class="{ active: canvas.id === activeCanvas?.id }"
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
        <span v-if="props.isDebugRun" class="run-snapshot-viewer__debug"><Bug :size="13" />{{ t('runs.kind.debug') }}</span>
        <span class="run-snapshot-viewer__readonly"><LockKeyhole :size="13" />{{ t('console.snapshotReadOnlyMode') }}</span>
      </div>
    </header>

    <div ref="workspaceRoot" class="run-snapshot-viewer__workspace">
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
          @activate="activateDockablePanel"
          @drag-preview="previewWorkspaceMove(group.id, $event)"
          @move-end="finishWorkspaceMove(group.id, $event)"
          @dock="dockWorkspacePanel"
          @combine="combineWorkspacePanel"
          @detach="detachWorkspacePanel"
          @close="closeWorkspacePanel"
        >
          <template #default="{ panelId }">
            <div v-if="panelId === 'canvas'" class="run-snapshot-viewer__canvas">
              <VueFlow
                id="run-snapshot-viewer"
                :key="viewKey"
                :model-value="renderedElements"
                :node-types="nodeTypes"
                :connection-mode="ConnectionMode.Strict"
                :nodes-draggable="false"
                :nodes-connectable="false"
                :edges-updatable="false"
                :elements-selectable="true"
                :delete-key-code="null"
                :min-zoom="0.2"
                :max-zoom="2"
                :snap-to-grid="true"
                :snap-grid="[16, 16]"
                :fit-view-on-init="true"
                :pan-on-drag="true"
                :zoom-on-scroll="true"
                :zoom-on-pinch="true"
                class="serein-flow run-snapshot-viewer__flow"
                @node-click="selectNode($event.node.id)"
              />
              <p v-if="!activeCanvas || activeCanvas.nodes.length === 0" class="run-snapshot-viewer__empty">{{ t('console.snapshotCanvasEmpty') }}</p>
              <div class="canvas-legend run-snapshot-viewer__legend" aria-hidden="true"><span><i class="legend-port execution"></i>{{ t('edge.flow') }}</span><span><i class="legend-port data"></i>{{ t('edge.value') }}</span></div>
            </div>

            <RunWorkpiecePanel
              v-else-if="panelId === 'workpieces'"
              :run-id="props.runId"
              :focus-node-id="selectedNodeId || undefined"
              :embedded="true"
              @select-node="selectExecutionNode"
            />

            <NodeExecutionInspector
              v-else-if="panelId === 'debug'"
              :executions="executionStates"
              :node-names="nodeNames"
              :selected-node-id="selectedNodeId || undefined"
              :compact="true"
              @select-node="selectExecutionNode"
            />

            <RunSnapshotNodeInspector
              v-else-if="panelId === 'inspector'"
              :node="selectedNode"
              :status="selectedNode ? nodeStatus(selectedNode.id) : 'idle'"
            />

            <RunSnapshotOutputViewer
              v-else-if="panelId === 'output'"
              :events="props.events"
              :error="props.eventsError"
            />
          </template>
        </DockablePanelGroup>
      </template>
    </div>
  </section>
</template>
