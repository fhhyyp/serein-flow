<script setup lang="ts">
import { Settings2, Trash2, X } from 'lucide-vue-next'
import { t } from '../../i18n'
import ParameterEditor from './ParameterEditor.vue'
import type { FlowEdge, FlowNode, MethodParameter, NodeKind } from '../../flow/types'

const props = defineProps<{
  mobileVisible: boolean
  selectedNode: FlowNode | undefined
  selectedEdge: FlowEdge | undefined
  iconForNodeKind: (kind: NodeKind) => unknown
  nodeTitle: (node: FlowNode) => string
  sourceNodeTitle: (parameter: MethodParameter) => string
}>()

const emit = defineEmits<{
  close: []
  delete: []
  'update-parameter-source': [nodeId: string, parameter: MethodParameter, event: Event]
  'begin-text-edit': []
  'commit-text-edit': []
  'discard-text-edit': []
}>()

function updateParameterSource(nodeId: string, parameter: MethodParameter, event: Event): void {
  emit('update-parameter-source', nodeId, parameter, event)
}
</script>

<template>
  <aside class="inspector-panel" :class="{ 'mobile-visible': props.mobileVisible, 'inspector-panel--empty': !props.selectedNode && !props.selectedEdge }">
    <template v-if="props.selectedNode">
      <div class="inspector-heading"><div><span class="eyebrow">{{ t('inspector.title') }}</span><h2>{{ props.nodeTitle(props.selectedNode) }}</h2></div><button class="icon-button" type="button" :title="t('command.close')" :aria-label="t('command.close')" @click="emit('close')"><X :size="16" /></button></div>
      <div class="inspector-type"><span class="node-icon" :class="`kind-${props.selectedNode.data.kind}`"><component :is="props.iconForNodeKind(props.selectedNode.data.kind)" :size="15" /></span><span>{{ t('inspector.nodeType', { kind: t(`node.kind.${props.selectedNode.data.kind}`) }) }}</span><span class="inspector-id mono">#{{ props.selectedNode.id }}</span></div>
      <div class="inspector-section"><span class="section-label">{{ t('inspector.general') }}</span><label class="field-label">{{ t('inspector.displayName') }}<input v-model="props.selectedNode.data.displayName" type="text" :placeholder="t(props.selectedNode.data.titleKey)" @focus="emit('begin-text-edit')" @input="emit('commit-text-edit')" @blur="emit('discard-text-edit')" /></label><label class="field-label">{{ t('inspector.description') }}<textarea v-model="props.selectedNode.data.description" rows="2" :placeholder="t(props.selectedNode.data.subtitleKey)" @focus="emit('begin-text-edit')" @input="emit('commit-text-edit')" @blur="emit('discard-text-edit')"></textarea></label></div>
      <div class="inspector-section parameter-section"><span class="section-label">{{ t('inspector.parameters') }}</span><p v-if="props.selectedNode.data.parameters.length === 0" class="empty-copy">{{ t('inspector.noParameters') }}</p><ParameterEditor v-for="parameter in props.selectedNode.data.parameters" :key="parameter.id" :node-id="props.selectedNode.id" :parameter="parameter" :source-node-title="props.sourceNodeTitle" @update-source="updateParameterSource" @begin-text-edit="emit('begin-text-edit')" @commit-text-edit="emit('commit-text-edit')" @discard-text-edit="emit('discard-text-edit')" /></div>
    </template>
    <template v-else-if="props.selectedEdge">
      <div class="inspector-heading"><div><span class="eyebrow">{{ t('inspector.title') }}</span><h2>{{ t('inspector.edgeSelected') }}</h2></div><button class="icon-button" type="button" :title="t('command.delete')" :aria-label="t('command.delete')" @click="emit('delete')"><Trash2 :size="16" /></button></div><div class="edge-summary" :class="props.selectedEdge.data?.semantic"><span class="edge-sample"></span><strong>{{ props.selectedEdge.data?.semantic === 'execution' ? t('inspector.executionEdge') : t('inspector.dataEdge') }}</strong><p>{{ props.selectedEdge.data?.semantic === 'execution' ? t('edge.executionDescription') : t('edge.dataDescription') }}</p></div>
    </template>
    <div v-else class="inspector-empty"><Settings2 :size="20" /><strong>{{ t('canvas.emptySelection') }}</strong><p>{{ t('inspector.selectNode') }}</p></div>
  </aside>
</template>
