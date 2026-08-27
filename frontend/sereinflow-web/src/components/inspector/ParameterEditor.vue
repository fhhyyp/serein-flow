<script setup lang="ts">
import { GitBranch, ListPlus, Rows3 } from 'lucide-vue-next'
import { t } from '../../i18n'
import type { MethodParameter } from '../../flow/types'

defineProps<{
  nodeId: string
  parameter: MethodParameter
  sourceNodeTitle: (parameter: MethodParameter) => string
  showVariadicControls?: boolean
}>()

const emit = defineEmits<{
  'update-source': [nodeId: string, parameter: MethodParameter, event: Event]
  'begin-text-edit': []
  'commit-text-edit': []
  'discard-text-edit': []
  'set-variadic-mode': [mode: 'expanded' | 'collection']
  'add-variadic-input': []
  'remove-variadic-input': []
}>()
</script>

<template>
  <div class="parameter-editor">
    <div class="parameter-heading"><strong>{{ t(parameter.nameKey) }}</strong><span class="port-kind">{{ parameter.valueKind }}</span></div>
    <div v-if="showVariadicControls" class="variadic-controls"><span>{{ t('parameter.variadic') }}</span><div class="segmented-control"><button type="button" :class="{ active: parameter.variadicMode !== 'collection' }" @click="emit('set-variadic-mode', 'expanded')"><Rows3 :size="13" />{{ t('parameter.variadicExpanded') }}</button><button type="button" :class="{ active: parameter.variadicMode === 'collection' }" @click="emit('set-variadic-mode', 'collection')"><ListPlus :size="13" />{{ t('parameter.variadicCollection') }}</button></div><div v-if="parameter.variadicMode !== 'collection'" class="variadic-actions"><button class="icon-button compact" type="button" :title="t('parameter.addVariadic')" :aria-label="t('parameter.addVariadic')" @click="emit('add-variadic-input')"><ListPlus :size="14" /></button><button class="icon-button compact danger" type="button" :title="t('parameter.removeVariadic')" :aria-label="t('parameter.removeVariadic')" @click="emit('remove-variadic-input')"><Rows3 :size="14" /></button></div></div>
    <label class="field-label compact">{{ t('parameter.source') }}<select :value="parameter.source" @change="emit('update-source', nodeId, parameter, $event)"><option value="literal">{{ t('parameter.literal') }}</option><option value="previousNode">{{ t('parameter.previousNode') }}</option><option value="projectInput">{{ t('parameter.projectInput') }}</option><option value="expression">{{ t('parameter.expression') }}</option></select></label>
    <label v-if="parameter.source === 'literal'" class="field-label compact">{{ t('parameter.literalValue') }}<input v-model="parameter.literalValue" type="text" @focus="emit('begin-text-edit')" @input="emit('commit-text-edit')" @blur="emit('discard-text-edit')" /></label>
    <label v-else-if="parameter.source === 'projectInput'" class="field-label compact">{{ t('parameter.projectInputKey') }}<input v-model="parameter.projectInputKey" type="text" @focus="emit('begin-text-edit')" @input="emit('commit-text-edit')" @blur="emit('discard-text-edit')" /></label>
    <label v-else-if="parameter.source === 'expression'" class="field-label compact">{{ t('parameter.expressionValue') }}<textarea v-model="parameter.expression" rows="2" @focus="emit('begin-text-edit')" @input="emit('commit-text-edit')" @blur="emit('discard-text-edit')"></textarea></label>
    <p v-else class="source-detail"><GitBranch :size="13" />{{ t('inspector.connectedFrom', { node: sourceNodeTitle(parameter) }) }}</p>
  </div>
</template>
