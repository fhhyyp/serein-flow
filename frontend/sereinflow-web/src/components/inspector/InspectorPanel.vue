<script setup lang="ts">
import { Maximize2, Plus, Settings2, Trash2, X } from 'lucide-vue-next'
import { defineAsyncComponent, ref } from 'vue'
import { t } from '../../i18n'
import ParameterEditor from './ParameterEditor.vue'
import type { CanvasState, FlowEdge, FlowNode, MethodParameter, NodeKind } from '../../flow/types'

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
}>()

const scriptEditorOpen = ref(false)
const scriptEditorSource = ref('')
const scriptEditorNodeId = ref('')
const scriptEditorNodeTitle = ref('')

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
  <aside class="inspector-panel" :class="{ 'mobile-visible': props.mobileVisible, 'inspector-panel--empty': !props.selectedNode && !props.selectedEdge }">
    <template v-if="props.selectedNode">
      <div class="inspector-heading"><div><span class="eyebrow">{{ t('inspector.title') }}</span><h2>{{ props.nodeTitle(props.selectedNode) }}</h2></div><button class="icon-button" type="button" :title="t('command.close')" :aria-label="t('command.close')" @click="emit('close')"><X :size="16" /></button></div>
      <div class="inspector-type"><span class="node-icon" :class="`kind-${props.selectedNode.data.kind}`"><component :is="props.iconForNodeKind(props.selectedNode.data.kind)" :size="15" /></span><span>{{ t('inspector.nodeType', { kind: t(`node.kind.${props.selectedNode.data.kind}`) }) }}</span><span class="inspector-id mono">#{{ props.selectedNode.id }}</span></div>
      <div class="inspector-section"><span class="section-label">{{ t('inspector.general') }}</span><label class="field-label">{{ t('inspector.displayName') }}<input v-model="props.selectedNode.data.displayName" type="text" :placeholder="t(props.selectedNode.data.titleKey)" @focus="emit('begin-text-edit')" @input="emit('commit-text-edit')" @blur="emit('discard-text-edit')" /></label><label class="field-label">{{ t('inspector.description') }}<textarea v-model="props.selectedNode.data.description" rows="2" :placeholder="t(props.selectedNode.data.subtitleKey)" @focus="emit('begin-text-edit')" @input="emit('commit-text-edit')" @blur="emit('discard-text-edit')"></textarea></label><label class="toggle-field"><input type="checkbox" :checked="props.entryNodeId === props.selectedNode.id" @change="onFlowEntryChange" /><span>{{ t('inspector.flowEntry') }}</span><small>{{ t('inspector.flowEntryHint') }}</small></label><label class="toggle-field"><input type="checkbox" :checked="props.selectedNode.data.runtime?.isPublic === true" @change="onPublicChange" /><span>{{ t('inspector.publicNode') }}</span><small>{{ t('inspector.publicNodeHint') }}</small></label></div>

      <div v-if="props.selectedNode.data.kind === 'script' && props.selectedNode.data.script" class="inspector-section script-editor"><div class="section-label-row"><span class="section-label">{{ t('inspector.script') }}</span><button class="icon-button compact" type="button" :title="t('inspector.openScriptEditor')" :aria-label="t('inspector.openScriptEditor')" @click="openScriptEditor"><Maximize2 :size="14" /></button></div><label class="field-label">{{ t('inspector.scriptSource') }}<textarea :value="props.selectedNode.data.script.source" class="script-source-input mono" rows="8" spellcheck="false" readonly></textarea></label></div>

      <div v-if="props.selectedNode.data.kind === 'flowCall'" class="inspector-section flowcall-editor"><span class="section-label">{{ t('inspector.flowCallTarget') }}</span><label class="field-label">{{ t('inspector.targetCanvas') }}<select :value="props.selectedNode.data.runtime?.targetCanvasId ?? ''" @change="onTargetCanvasChange"><option value="">{{ t('inspector.selectCanvas') }}</option><option v-for="canvas in props.canvases" :key="canvas.id" :value="canvas.id">{{ canvas.name || t(canvas.nameKey) }}</option></select></label><label v-if="props.selectedNode.data.runtime?.targetCanvasId" class="field-label">{{ t('inspector.publicTargetNode') }}<select :value="props.selectedNode.data.runtime?.targetNodeId ?? ''" @change="onTargetNodeChange"><option value="">{{ t('inspector.selectPublicNode') }}</option><option v-for="node in publicNodes(props.selectedNode.data.runtime?.targetCanvasId ?? '', props.selectedNode.id)" :key="node.id" :value="node.id">{{ props.nodeTitle(node) }}</option></select></label><p v-if="props.selectedNode.data.runtime?.targetCanvasId && publicNodes(props.selectedNode.data.runtime.targetCanvasId, props.selectedNode.id).length === 0" class="empty-copy">{{ t('inspector.noPublicNodes') }}</p></div>

      <div class="inspector-section parameter-section"><div class="section-label-row"><span class="section-label">{{ props.selectedNode.data.kind === 'script' ? t('inspector.scriptInputs') : t('inspector.parameters') }}</span><button v-if="props.selectedNode.data.kind === 'script'" class="icon-button compact" type="button" :title="t('inspector.addScriptInput')" :aria-label="t('inspector.addScriptInput')" @click="emit('add-script-input', props.selectedNode.id)"><Plus :size="14" /></button></div><p v-if="props.selectedNode.data.parameters.length === 0" class="empty-copy">{{ props.selectedNode.data.kind === 'script' ? t('inspector.noScriptInputs') : t('inspector.noParameters') }}</p><div v-for="parameter in props.selectedNode.data.parameters" :key="parameter.id" class="parameter-editor-wrap"><div v-if="props.selectedNode.data.kind === 'script'" class="script-input-meta"><label class="field-label compact">{{ t('inspector.inputName') }}<input v-model="parameter.name" type="text" @focus="emit('begin-text-edit')" @input="updateScriptInputName(parameter); emit('commit-text-edit')" @blur="emit('discard-text-edit')" /></label><label class="field-label compact">{{ t('parameter.valueKind') }}<input v-model="parameter.valueKind" type="text" @focus="emit('begin-text-edit')" @input="emit('commit-text-edit')" @blur="emit('discard-text-edit')" /></label><label class="field-label compact">{{ t('inspector.inputRemark') }}<input v-model="parameter.description" type="text" @focus="emit('begin-text-edit')" @input="emit('commit-text-edit')" @blur="emit('discard-text-edit')" /></label><label class="script-input-required"><input v-model="parameter.required" type="checkbox" @focus="emit('begin-text-edit')" @change="emit('commit-text-edit')" />{{ t('inspector.required') }}</label><button class="icon-button compact danger" type="button" :title="t('inspector.removeScriptInput')" :aria-label="t('inspector.removeScriptInput')" @click="emit('remove-script-input', props.selectedNode.id, parameter.id)"><Trash2 :size="14" /></button></div><ParameterEditor :node-id="props.selectedNode.id" :parameter="parameter" :source-node-title="props.sourceNodeTitle" :show-variadic-controls="isFirstVariadic(props.selectedNode, parameter)" @update-source="updateParameterSource" @begin-text-edit="emit('begin-text-edit')" @commit-text-edit="emit('commit-text-edit')" @discard-text-edit="emit('discard-text-edit')" @set-variadic-mode="emit('set-variadic-mode', props.selectedNode.id, parameter.id, $event)" @add-variadic-input="emit('add-variadic-input', props.selectedNode.id, parameter.id)" @remove-variadic-input="emit('remove-variadic-input', props.selectedNode.id, parameter.id)" /></div></div>
    </template>
    <template v-else-if="props.selectedEdge">
      <div class="inspector-heading"><div><span class="eyebrow">{{ t('inspector.title') }}</span><h2>{{ t('inspector.edgeSelected') }}</h2></div><button class="icon-button" type="button" :title="t('command.delete')" :aria-label="t('command.delete')" @click="emit('delete')"><Trash2 :size="16" /></button></div><div class="edge-summary" :class="[props.selectedEdge.data?.semantic, props.selectedEdge.data?.semantic === 'execution' ? `branch-${props.selectedEdge.data?.branch ?? 'success'}` : undefined]"><span class="edge-sample"></span><strong>{{ props.selectedEdge.data?.semantic === 'execution' ? `${t('inspector.executionEdge')} · ${t(`branch.${props.selectedEdge.data?.branch ?? 'success'}`)}` : t('inspector.dataEdge') }}</strong><p>{{ props.selectedEdge.data?.semantic === 'execution' ? t('edge.executionDescription') : t('edge.dataDescription') }}</p></div>
    </template>
    <div v-else class="inspector-empty"><Settings2 :size="20" /><strong>{{ t('canvas.emptySelection') }}</strong><p>{{ t('inspector.selectNode') }}</p></div>
  </aside>
  <ScriptEditorDialog v-if="scriptEditorOpen" :open="scriptEditorOpen" :source="scriptEditorSource" :node-title="scriptEditorNodeTitle" @close="closeScriptEditor" @apply="applyScriptSource" />
</template>
