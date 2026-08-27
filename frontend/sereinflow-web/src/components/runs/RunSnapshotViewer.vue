<script setup lang="ts">
import { computed, markRaw, ref, watch } from 'vue'
import { ConnectionMode, VueFlow } from '@vue-flow/core'
import { LockKeyhole } from 'lucide-vue-next'
import FlowNodeCard from '../flow/FlowNodeCard.vue'
import RunSnapshotNodeInspector from './RunSnapshotNodeInspector.vue'
import RunSnapshotOutputViewer from './RunSnapshotOutputViewer.vue'
import { t, locale } from '../../i18n'
import { flowDefinitionToWorkspace } from '../../flow/flowDtoMapper'
import { connectionLineTypeForEdge } from '../../flow/connectionLine'
import type { CanvasState, FlowEdge, FlowNode, NodeStatus } from '../../flow/types'
import type { FlowDefinitionDto, FlowRunEventDto, FlowRunOutputDto } from '../../api/flowApi'

const props = defineProps<{
  definition?: FlowDefinitionDto
  outputs: FlowRunOutputDto[]
  outputsError?: string
  events: FlowRunEventDto[]
  eventsError?: string
}>()

const nodeTypes = markRaw({ workflow: FlowNodeCard })
const activeCanvasId = ref('')
const selectedNodeId = ref('')
const sidePanel = ref<'properties' | 'audit' | 'output'>('properties')

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
const orderedOutputs = computed(() => [...props.outputs].sort((left, right) => left.sequence - right.sequence))
const viewKey = computed(() => `${props.definition?.id ?? 'empty'}-${activeCanvas.value?.id ?? 'none'}`)
const selectedNode = computed(() => activeCanvas.value?.nodes.find((node) => node.id === selectedNodeId.value))

watch(() => props.definition?.id, () => {
  const snapshot = workspace.value
  activeCanvasId.value = snapshot?.activeCanvasId ?? ''
  selectedNodeId.value = ''
  sidePanel.value = 'properties'
}, { immediate: true })

watch(activeCanvasId, () => {
  selectedNodeId.value = ''
})

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

function outputStatus(output: FlowRunOutputDto): NodeStatus {
  return output.outcome === 'error' ? 'error' : output.outcome === 'failed' ? 'failed' : 'success'
}

function branchLabel(output: FlowRunOutputDto): string {
  const branch = output.branch?.toLowerCase()
  return branch === 'success' || branch === 'failure' || branch === 'error'
    ? t(`branch.${branch}`)
    : '-'
}

function formatTimestamp(value: string): string {
  const timestamp = new Date(value)
  if (Number.isNaN(timestamp.getTime())) return value
  return new Intl.DateTimeFormat(locale.value, { dateStyle: 'short', timeStyle: 'medium' }).format(timestamp)
}

function formatValue(value: unknown): string {
  if (typeof value === 'string') {
    try {
      return JSON.stringify(JSON.parse(value), null, 2)
    } catch {
      return value
    }
  }

  return JSON.stringify(value ?? {}, null, 2)
}

function selectNode(nodeId: string): void {
  selectedNodeId.value = nodeId
  sidePanel.value = 'properties'
}
</script>

<template>
  <section class="run-snapshot-viewer" :aria-label="t('console.snapshotTitle')">
    <header class="run-snapshot-viewer__toolbar">
      <div class="run-snapshot-viewer__tabs" role="tablist" :aria-label="t('canvas.options')">
        <button
          v-for="canvas in workspace?.canvases ?? []"
          :key="canvas.id"
          type="button"
          role="tab"
          :aria-selected="canvas.id === activeCanvas?.id"
          :class="{ active: canvas.id === activeCanvas?.id }"
          @click="activeCanvasId = canvas.id"
        >{{ canvasLabel(canvas) }}</button>
      </div>
      <span class="run-snapshot-viewer__readonly"><LockKeyhole :size="13" />{{ t('console.snapshotReadOnlyMode') }}</span>
    </header>

    <div class="run-snapshot-viewer__layout">
      <div class="run-snapshot-viewer__canvas">
        <VueFlow
          id="run-snapshot-viewer"
          :key="viewKey"
          :model-value="renderedElements"
          :node-types="nodeTypes"
          :connection-mode="ConnectionMode.Strict"
          :nodes-draggable="false"
          :nodes-connectable="false"
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

      <aside class="run-snapshot-viewer__side-panel">
        <div class="run-snapshot-viewer__side-tabs" role="tablist" :aria-label="t('console.snapshotTitle')">
          <button type="button" role="tab" :aria-selected="sidePanel === 'properties'" :class="{ active: sidePanel === 'properties' }" @click="sidePanel = 'properties'">{{ t('console.snapshotProperties') }}</button>
          <button type="button" role="tab" :aria-selected="sidePanel === 'audit'" :class="{ active: sidePanel === 'audit' }" @click="sidePanel = 'audit'">{{ t('console.snapshotAudit') }}<span>{{ orderedOutputs.length }}</span></button>
          <button type="button" role="tab" :aria-selected="sidePanel === 'output'" :class="{ active: sidePanel === 'output' }" @click="sidePanel = 'output'">{{ t('console.snapshotRunOutput') }}<span>{{ events.length }}</span></button>
        </div>

        <RunSnapshotNodeInspector v-if="sidePanel === 'properties'" :node="selectedNode" :status="selectedNode ? nodeStatus(selectedNode.id) : 'idle'" />

        <section v-else-if="sidePanel === 'audit'" class="run-snapshot-viewer__outputs" :aria-label="t('console.snapshotAudit')">
          <header><div><p>{{ t('console.snapshotAudit') }}</p><strong>{{ orderedOutputs.length }} {{ t('output.records') }}</strong></div></header>
          <p v-if="outputsError" class="run-snapshot-viewer__outputs-error">{{ outputsError }}</p>
          <ol v-else-if="orderedOutputs.length" class="run-snapshot-output-list">
            <li v-for="output in orderedOutputs" :key="`${output.nodeId}-${output.sequence}`">
              <div class="run-snapshot-output-list__heading"><span :class="['run-snapshot-output-list__status', `status-${outputStatus(output)}`]"></span><strong>{{ output.nodeId }}</strong><code>#{{ output.sequence }}</code></div>
              <div class="run-snapshot-output-list__meta"><span>{{ branchLabel(output) }}</span><time>{{ formatTimestamp(output.timestamp) }}</time></div>
              <p v-if="output.errorCode || output.errorMessage" class="run-snapshot-output-list__error"><code v-if="output.errorCode">{{ output.errorCode }}</code><span v-if="output.errorMessage">{{ output.errorMessage }}</span></p>
              <section class="run-snapshot-output-list__payload">
                <span>{{ t('console.snapshotInputs') }}</span>
                <pre>{{ formatValue(output.inputs) }}</pre>
              </section>
              <section class="run-snapshot-output-list__payload">
                <span>{{ t('console.snapshotOutputs') }}</span>
                <pre>{{ formatValue(output.outputs) }}</pre>
              </section>
            </li>
          </ol>
          <p v-else class="run-snapshot-viewer__outputs-empty">{{ t('console.snapshotOutputsEmpty') }}</p>
        </section>

        <RunSnapshotOutputViewer v-else :events="events" :error="eventsError" />
      </aside>
    </div>
  </section>
</template>
