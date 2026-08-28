<script setup lang="ts">
import { Bug, Maximize2, PanelRightClose, Plus, Settings2, Trash2 } from 'lucide-vue-next'
import { computed, defineAsyncComponent, inject, onBeforeUnmount, ref, watch } from 'vue'
import { t } from '../../i18n'
import ParameterEditor from './ParameterEditor.vue'
import FlowDebugPanel from '../workspace/FlowDebugPanel.vue'
import { libraryNameResolverKey } from '../../flow/libraryNameResolver'
import { formatNodeType } from '../../flow/typeDisplay'
import type { CanvasState, FlowEdge, FlowNode, MethodParameter, NodeKind } from '../../flow/types'
import type { FlowDebugSessionDto } from '../../api/flowApi'
import type { DebugPauseBoundary } from '../../composables/useFlowDebugger'
import type { NodeExecutionState } from '../../flow/nodeExecutionState'
import {
  clampInspectorPanelWidth,
  defaultInspectorPanelWidth,
  maximumInspectorPanelWidth,
  minimumInspectorPanelWidth,
  parseStoredInspectorPanelWidth,
} from '../../flow/inspectorPanelWidth'

const ScriptEditorDialog = defineAsyncComponent(() => import('../editor/ScriptEditorDialog.vue'))

const props = defineProps<{
  mobileVisible: boolean
  selectedNode: FlowNode | undefined
  selectedEdge: FlowEdge | undefined
  iconForNodeKind: (kind: NodeKind) => unknown
  nodeTitle: (node: FlowNode) => string
  sourceNodeTitle: (parameter: MethodParameter) => string
  canvases: CanvasState[]
  entryNodeId: string
  debugSession?: FlowDebugSessionDto
  debugBoundary?: DebugPauseBoundary
  debugExecutions: readonly NodeExecutionState[]
  debugNodeNames?: Record<string, string>
  debugIsControlling: boolean
  debugIsStopping: boolean
}>()

const emit = defineEmits<{
  close: []
  delete: []
  'update-parameter-source': [nodeId: string, parameter: MethodParameter, event: Event]
  'begin-text-edit': []
  'commit-text-edit': []
  'discard-text-edit': []
  'set-node-public': [nodeId: string, value: boolean]
  'set-flow-entry': [nodeId: string, value: boolean]
  'set-flowcall-target': [nodeId: string, canvasId: string, targetNodeId?: string]
  'add-script-input': [nodeId: string]
  'remove-script-input': [nodeId: string, parameterId: string]
  'set-variadic-mode': [nodeId: string, parameterId: string, mode: 'expanded' | 'collection']
  'add-variadic-input': [nodeId: string, parameterId: string]
  'remove-variadic-input': [nodeId: string, parameterId: string]
  'continue-debug': []
  'step-debug': []
  'stop-debug': []
  'select-debug-node': [nodeId: string]
}>()

const scriptEditorOpen = ref(false)
const scriptEditorSource = ref('')
const scriptEditorNodeId = ref('')
const scriptEditorNodeTitle = ref('')
const isDebugView = ref(false)
const inspectorWidthStorageKey = 'sereinflow.inspector-panel-width.v1'
const viewportWidth = ref(typeof window === 'undefined' ? 1_920 : window.innerWidth)
const panelWidth = ref(loadPanelWidth())
const isResizing = ref(false)
let stopResize: (() => void) | undefined
const libraryNameFor = inject(libraryNameResolverKey, () => undefined)
const runtime = computed(() => props.selectedNode?.data.runtime)
const runtimeLibraryName = computed(() => libraryNameFor(runtime.value))
const runtimeMember = computed(() => [runtime.value?.className, runtime.value?.methodName].filter(Boolean).join('.'))
const runtimeAssembly = computed(() => [runtime.value?.dllName, runtime.value?.dllVersion].filter(Boolean).join(' · '))
const runtimeReturnType = computed(() => formatNodeType(runtime.value?.returnType))
const runtimeIsAwaitable = computed(() => runtime.value?.isAwaitable)
const hasRuntimeMetadata = computed(() => Boolean(
  runtimeLibraryName.value
  || runtimeMember.value
  || runtimeAssembly.value
  || runtimeReturnType.value
  || runtimeIsAwaitable.value !== undefined,
))
const maximumPanelWidth = computed(() => maximumInspectorPanelWidth(viewportWidth.value))
const panelStyle = computed(() => panelWidth.value === defaultInspectorPanelWidth
  ? undefined
  : { '--inspector-width': `${panelWidth.value}px` })

watch(() => props.debugSession?.id, (sessionId) => {
  isDebugView.value = Boolean(sessionId)
}, { immediate: true })

function showDebugView(): void {
  if (props.debugSession) isDebugView.value = true
}

function loadPanelWidth(): number {
  if (typeof window === 'undefined') return defaultInspectorPanelWidth
  try {
    return parseStoredInspectorPanelWidth(window.localStorage.getItem(inspectorWidthStorageKey), window.innerWidth)
  } catch {
    return defaultInspectorPanelWidth
  }
}

function setPanelWidth(width: number): void {
  const nextWidth = clampInspectorPanelWidth(width, viewportWidth.value)
  panelWidth.value = nextWidth
  if (typeof window === 'undefined') return
  try {
    if (nextWidth === defaultInspectorPanelWidth) window.localStorage.removeItem(inspectorWidthStorageKey)
    else window.localStorage.setItem(inspectorWidthStorageKey, String(nextWidth))
  } catch {
    // Local layout preferences must not block use of the inspector.
  }
}

function beginResize(event: PointerEvent): void {
  if (typeof window === 'undefined' || window.innerWidth <= 760) return
  event.preventDefault()
  isResizing.value = true
  const startX = event.clientX
  const startWidth = panelWidth.value
  const onMove = (moveEvent: PointerEvent) => {
    setPanelWidth(startWidth + startX - moveEvent.clientX)
  }
  const onUp = () => {
    isResizing.value = false
    window.removeEventListener('pointermove', onMove)
    window.removeEventListener('pointerup', onUp)
    if (stopResize === onUp) stopResize = undefined
  }
  stopResize?.()
  stopResize = onUp
  window.addEventListener('pointermove', onMove)
  window.addEventListener('pointerup', onUp)
}

function resizeWithKeyboard(event: KeyboardEvent): void {
  if (typeof window === 'undefined' || window.innerWidth <= 760) return
  const step = event.shiftKey ? 64 : 24
  if (event.key === 'ArrowLeft') setPanelWidth(panelWidth.value + step)
  else if (event.key === 'ArrowRight') setPanelWidth(panelWidth.value - step)
  else if (event.key === 'Home') setPanelWidth(minimumInspectorPanelWidth)
  else if (event.key === 'End') setPanelWidth(maximumPanelWidth.value)
  else return
  event.preventDefault()
}

function handleViewportResize(): void {
  viewportWidth.value = typeof window === 'undefined' ? viewportWidth.value : window.innerWidth
  if (viewportWidth.value > 760) setPanelWidth(panelWidth.value)
}

if (typeof window !== 'undefined') window.addEventListener('resize', handleViewportResize)

onBeforeUnmount(() => {
  stopResize?.()
  if (typeof window !== 'undefined') window.removeEventListener('resize', handleViewportResize)
})

function updateParameterSource(nodeId: string, parameter: MethodParameter, event: Event): void {
  emit('update-parameter-source', nodeId, parameter, event)
}

function updateScriptInputName(parameter: MethodParameter): void {
  parameter.name = parameter.name?.trimStart()
  parameter.nameKey = parameter.name || parameter.id
}

function onPublicChange(event: Event): void {
  emit('set-node-public', props.selectedNode?.id ?? '', (event.target as HTMLInputElement).checked)
}

function onFlowEntryChange(event: Event): void {
  emit('set-flow-entry', props.selectedNode?.id ?? '', (event.target as HTMLInputElement).checked)
}

function onTargetCanvasChange(event: Event): void {
  emit('set-flowcall-target', props.selectedNode?.id ?? '', (event.target as HTMLSelectElement).value)
}

function onTargetNodeChange(event: Event): void {
  const selectedNode = props.selectedNode
  emit(
    'set-flowcall-target',
    selectedNode?.id ?? '',
    selectedNode?.data.runtime?.targetCanvasId ?? '',
    (event.target as HTMLSelectElement).value || undefined,
  )
}

function publicNodes(canvasId: string, callNodeId: string): FlowNode[] {
  return props.canvases
    .find((canvas) => canvas.id === canvasId)
    ?.nodes.filter((node) => node.id !== callNodeId && node.data.runtime?.isPublic === true) ?? []
}

function isFirstVariadic(node: FlowNode, parameter: MethodParameter): boolean {
  if (!parameter.isVariadic || !parameter.variadicGroupId) {
    return false
  }

  return node.data.parameters.find((candidate) => candidate.variadicGroupId === parameter.variadicGroupId)?.id === parameter.id
}

function openScriptEditor(): void {
  const node = props.selectedNode
  if (!node || node.data.kind !== 'script' || !node.data.script) {
    return
  }

  scriptEditorNodeId.value = node.id
  scriptEditorNodeTitle.value = props.nodeTitle(node)
  scriptEditorSource.value = node.data.script.source
  scriptEditorOpen.value = true
}

function closeScriptEditor(): void {
  scriptEditorOpen.value = false
}

function applyScriptSource(source: string): void {
  const node = props.canvases
    .flatMap((canvas) => canvas.nodes)
    .find((candidate) => candidate.id === scriptEditorNodeId.value)
  if (!node?.data.script || source === node.data.script.source) {
    closeScriptEditor()
    return
  }

  emit('begin-text-edit')
  node.data.script.source = source
  emit('commit-text-edit')
  closeScriptEditor()
}
</script>

<template>
  <aside class="inspector-panel" :class="{ 'mobile-visible': props.mobileVisible, 'inspector-panel--empty': !props.selectedNode && !props.selectedEdge && !props.debugSession, 'inspector-panel--debug': isDebugView, 'inspector-panel--resizing': isResizing }" :style="panelStyle">
    <div class="inspector-resize-grip" role="separator" aria-orientation="vertical" :aria-label="t('panel.resizeInspector')" :aria-valuemin="minimumInspectorPanelWidth" :aria-valuemax="maximumPanelWidth" :aria-valuenow="panelWidth" tabindex="0" @pointerdown="beginResize" @keydown="resizeWithKeyboard"><span></span></div>
    <FlowDebugPanel
      v-if="isDebugView && props.debugSession"
      :session="props.debugSession"
      :boundary="props.debugBoundary"
      :executions="props.debugExecutions"
      :node-names="props.debugNodeNames"
      :is-controlling="props.debugIsControlling"
      :is-stopping="props.debugIsStopping"
      @continue="emit('continue-debug')"
      @step="emit('step-debug')"
      @stop="emit('stop-debug')"
      @inspect="isDebugView = false"
      @select-node="emit('select-debug-node', $event)"
      @close="emit('close')"
    />
    <template v-else>
      <button v-if="props.debugSession" class="inspector-debug-switch" type="button" :title="t('debug.panelTitle')" :aria-label="t('debug.panelTitle')" @click="showDebugView"><Bug :size="15" /><span>{{ t('debug.panelEyebrow') }}</span><strong>{{ t(`debug.status.${props.debugSession.status}`) }}</strong></button>
    <template v-if="props.selectedNode">
      <div class="inspector-heading"><div><span class="eyebrow">{{ t('inspector.title') }}</span><h2>{{ props.nodeTitle(props.selectedNode) }}</h2></div><button class="icon-button" type="button" :title="t('panel.collapseInspector')" :aria-label="t('panel.collapseInspector')" @click="emit('close')"><PanelRightClose :size="16" /></button></div>
      <div class="inspector-type"><span class="node-icon" :class="`kind-${props.selectedNode.data.kind}`"><component :is="props.iconForNodeKind(props.selectedNode.data.kind)" :size="15" /></span><span>{{ t('inspector.nodeType', { kind: t(`node.kind.${props.selectedNode.data.kind}`) }) }}</span><span class="inspector-id mono">#{{ props.selectedNode.id }}</span></div>
      <div class="inspector-section"><span class="section-label">{{ t('inspector.general') }}</span><label class="field-label">{{ t('inspector.displayName') }}<input v-model="props.selectedNode.data.displayName" type="text" :placeholder="t(props.selectedNode.data.titleKey)" @focus="emit('begin-text-edit')" @input="emit('commit-text-edit')" @blur="emit('discard-text-edit')" /></label><label class="field-label">{{ t('inspector.description') }}<textarea v-model="props.selectedNode.data.description" rows="2" :placeholder="t(props.selectedNode.data.subtitleKey)" @focus="emit('begin-text-edit')" @input="emit('commit-text-edit')" @blur="emit('discard-text-edit')"></textarea></label><label class="toggle-field"><input type="checkbox" :checked="props.entryNodeId === props.selectedNode.id" @change="onFlowEntryChange" /><span>{{ t('inspector.flowEntry') }}</span><small>{{ t('inspector.flowEntryHint') }}</small></label><label class="toggle-field"><input type="checkbox" :checked="props.selectedNode.data.runtime?.isPublic === true" @change="onPublicChange" /><span>{{ t('inspector.publicNode') }}</span><small>{{ t('inspector.publicNodeHint') }}</small></label></div>

      <section v-if="hasRuntimeMetadata" class="inspector-section inspector-runtime"><span class="section-label">{{ t('inspector.runtime') }}</span><dl class="inspector-runtime__list"><div v-if="runtimeLibraryName"><dt>{{ t('inspector.runtimeLibrary') }}</dt><dd>{{ runtimeLibraryName }}</dd></div><div v-if="runtimeMember"><dt>{{ t('inspector.runtimeMember') }}</dt><dd><code>{{ runtimeMember }}</code></dd></div><div v-if="runtimeAssembly"><dt>{{ t('inspector.runtimeAssembly') }}</dt><dd><code>{{ runtimeAssembly }}</code></dd></div><div v-if="runtimeReturnType"><dt>{{ t('inspector.runtimeReturnType') }}</dt><dd><code>{{ runtimeReturnType }}</code></dd></div><div v-if="runtimeIsAwaitable !== undefined"><dt>{{ t('inspector.runtimeAwaitable') }}</dt><dd>{{ runtimeIsAwaitable ? t('inspector.yes') : t('inspector.no') }}</dd></div></dl></section>

      <div v-if="props.selectedNode.data.kind === 'script' && props.selectedNode.data.script" class="inspector-section script-editor"><div class="section-label-row"><span class="section-label">{{ t('inspector.script') }}</span><button class="icon-button compact" type="button" :title="t('inspector.openScriptEditor')" :aria-label="t('inspector.openScriptEditor')" @click="openScriptEditor"><Maximize2 :size="14" /></button></div><label class="field-label">{{ t('inspector.scriptSource') }}<textarea :value="props.selectedNode.data.script.source" class="script-source-input mono" rows="8" spellcheck="false" readonly></textarea></label></div>

      <div v-if="props.selectedNode.data.kind === 'flowCall'" class="inspector-section flowcall-editor"><span class="section-label">{{ t('inspector.flowCallTarget') }}</span><label class="field-label">{{ t('inspector.targetCanvas') }}<select :value="props.selectedNode.data.runtime?.targetCanvasId ?? ''" @change="onTargetCanvasChange"><option value="">{{ t('inspector.selectCanvas') }}</option><option v-for="canvas in props.canvases" :key="canvas.id" :value="canvas.id">{{ canvas.name || t(canvas.nameKey) }}</option></select></label><label v-if="props.selectedNode.data.runtime?.targetCanvasId" class="field-label">{{ t('inspector.publicTargetNode') }}<select :value="props.selectedNode.data.runtime?.targetNodeId ?? ''" @change="onTargetNodeChange"><option value="">{{ t('inspector.selectPublicNode') }}</option><option v-for="node in publicNodes(props.selectedNode.data.runtime?.targetCanvasId ?? '', props.selectedNode.id)" :key="node.id" :value="node.id">{{ props.nodeTitle(node) }}</option></select></label><p v-if="props.selectedNode.data.runtime?.targetCanvasId && publicNodes(props.selectedNode.data.runtime.targetCanvasId, props.selectedNode.id).length === 0" class="empty-copy">{{ t('inspector.noPublicNodes') }}</p></div>

      <div class="inspector-section parameter-section"><div class="section-label-row"><span class="section-label">{{ props.selectedNode.data.kind === 'script' ? t('inspector.scriptInputs') : t('inspector.parameters') }}</span><button v-if="props.selectedNode.data.kind === 'script'" class="icon-button compact" type="button" :title="t('inspector.addScriptInput')" :aria-label="t('inspector.addScriptInput')" @click="emit('add-script-input', props.selectedNode.id)"><Plus :size="14" /></button></div><p v-if="props.selectedNode.data.parameters.length === 0" class="empty-copy">{{ props.selectedNode.data.kind === 'script' ? t('inspector.noScriptInputs') : t('inspector.noParameters') }}</p><div v-for="parameter in props.selectedNode.data.parameters" :key="parameter.id" class="parameter-editor-wrap"><div v-if="props.selectedNode.data.kind === 'script'" class="script-input-meta"><label class="field-label compact">{{ t('inspector.inputName') }}<input v-model="parameter.name" type="text" @focus="emit('begin-text-edit')" @input="updateScriptInputName(parameter); emit('commit-text-edit')" @blur="emit('discard-text-edit')" /></label><label class="field-label compact">{{ t('parameter.valueKind') }}<input v-model="parameter.valueKind" type="text" @focus="emit('begin-text-edit')" @input="emit('commit-text-edit')" @blur="emit('discard-text-edit')" /></label><label class="field-label compact">{{ t('inspector.inputRemark') }}<input v-model="parameter.description" type="text" @focus="emit('begin-text-edit')" @input="emit('commit-text-edit')" @blur="emit('discard-text-edit')" /></label><label class="script-input-required"><input v-model="parameter.required" type="checkbox" @focus="emit('begin-text-edit')" @change="emit('commit-text-edit')" />{{ t('inspector.required') }}</label><button class="icon-button compact danger" type="button" :title="t('inspector.removeScriptInput')" :aria-label="t('inspector.removeScriptInput')" @click="emit('remove-script-input', props.selectedNode.id, parameter.id)"><Trash2 :size="14" /></button></div><ParameterEditor :node-id="props.selectedNode.id" :parameter="parameter" :source-node-title="props.sourceNodeTitle" :show-variadic-controls="isFirstVariadic(props.selectedNode, parameter)" @update-source="updateParameterSource" @begin-text-edit="emit('begin-text-edit')" @commit-text-edit="emit('commit-text-edit')" @discard-text-edit="emit('discard-text-edit')" @set-variadic-mode="emit('set-variadic-mode', props.selectedNode.id, parameter.id, $event)" @add-variadic-input="emit('add-variadic-input', props.selectedNode.id, parameter.id)" @remove-variadic-input="emit('remove-variadic-input', props.selectedNode.id, parameter.id)" /></div></div>
    </template>
    <template v-else-if="props.selectedEdge">
      <div class="inspector-heading"><div><span class="eyebrow">{{ t('inspector.title') }}</span><h2>{{ t('inspector.edgeSelected') }}</h2></div><div class="inspector-heading__actions"><button class="icon-button" type="button" :title="t('command.delete')" :aria-label="t('command.delete')" @click="emit('delete')"><Trash2 :size="16" /></button><button class="icon-button" type="button" :title="t('panel.collapseInspector')" :aria-label="t('panel.collapseInspector')" @click="emit('close')"><PanelRightClose :size="16" /></button></div></div><div class="edge-summary" :class="[props.selectedEdge.data?.semantic, props.selectedEdge.data?.semantic === 'execution' ? `branch-${props.selectedEdge.data?.branch ?? 'success'}` : undefined]"><span class="edge-sample"></span><strong>{{ props.selectedEdge.data?.semantic === 'execution' ? `${t('inspector.executionEdge')} · ${t(`branch.${props.selectedEdge.data?.branch ?? 'success'}`)}` : t('inspector.dataEdge') }}</strong><p>{{ props.selectedEdge.data?.semantic === 'execution' ? t('edge.executionDescription') : t('edge.dataDescription') }}</p></div>
    </template>
    <template v-else><div class="inspector-heading"><div><span class="eyebrow">{{ t('inspector.title') }}</span><h2>{{ t('canvas.emptySelection') }}</h2></div><button class="icon-button" type="button" :title="t('panel.collapseInspector')" :aria-label="t('panel.collapseInspector')" @click="emit('close')"><PanelRightClose :size="16" /></button></div><div class="inspector-empty"><Settings2 :size="20" /><p>{{ t('inspector.selectNode') }}</p></div></template>
    </template>
  </aside>
  <ScriptEditorDialog v-if="scriptEditorOpen" :open="scriptEditorOpen" :source="scriptEditorSource" :node-title="scriptEditorNodeTitle" @close="closeScriptEditor" @apply="applyScriptSource" />
</template>
