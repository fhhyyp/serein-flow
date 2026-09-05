<script setup lang="ts">
import { GitBranch, ListPlus, Rows3 } from 'lucide-vue-next'
import { t } from '../../i18n'
import { hasEnumOptions, isBooleanParameterType, isEnumOptionSelected, normalizeBooleanLiteralValue, updateFlagsLiteralValue } from '../../flow/parameterTypes'
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

function isBooleanParameter(parameter: MethodParameter): boolean {
  return isBooleanParameterType(parameter.type ?? parameter.valueKind)
}

function booleanLiteralValue(parameter: MethodParameter): 'true' | 'false' | '' {
  return normalizeBooleanLiteralValue(parameter.literalValue)
}

function updateBooleanLiteral(parameter: MethodParameter, event: Event): void {
  parameter.literalValue = (event.target as HTMLSelectElement).value
  emit('commit-text-edit')
}

function hasEnumParameterOptions(parameter: MethodParameter): boolean {
  return hasEnumOptions(parameter.enumMetadata)
}

function isFlagsParameter(parameter: MethodParameter): boolean {
  return parameter.enumMetadata?.isFlags === true
}

function updateEnumLiteral(parameter: MethodParameter, event: Event): void {
  parameter.literalValue = (event.target as HTMLSelectElement).value
  emit('commit-text-edit')
}

function isFlagSelected(parameter: MethodParameter, optionName: string): boolean {
  return isEnumOptionSelected(parameter.literalValue, optionName)
}

function updateFlagsLiteral(parameter: MethodParameter, optionName: string, event: Event): void {
  if (!parameter.enumMetadata) {
    return
  }

  parameter.literalValue = updateFlagsLiteralValue(
    parameter.enumMetadata,
    parameter.literalValue,
    optionName,
    (event.target as HTMLInputElement).checked,
  )
  emit('commit-text-edit')
}
</script>

<template>
  <div class="parameter-editor">
    <div class="parameter-heading"><strong>{{ t(parameter.nameKey) }}</strong><span class="port-kind">{{ parameter.valueKind }}</span></div>
    <div v-if="showVariadicControls" class="variadic-controls"><span>{{ t('parameter.variadic') }}</span><div class="segmented-control"><button type="button" :class="{ active: parameter.variadicMode !== 'collection' }" @click="emit('set-variadic-mode', 'expanded')"><Rows3 :size="13" />{{ t('parameter.variadicExpanded') }}</button><button type="button" :class="{ active: parameter.variadicMode === 'collection' }" @click="emit('set-variadic-mode', 'collection')"><ListPlus :size="13" />{{ t('parameter.variadicCollection') }}</button></div><div v-if="parameter.variadicMode !== 'collection'" class="variadic-actions"><button class="icon-button compact" type="button" :title="t('parameter.addVariadic')" :aria-label="t('parameter.addVariadic')" @click="emit('add-variadic-input')"><ListPlus :size="14" /></button><button class="icon-button compact danger" type="button" :title="t('parameter.removeVariadic')" :aria-label="t('parameter.removeVariadic')" @click="emit('remove-variadic-input')"><Rows3 :size="14" /></button></div></div>
    <label class="field-label compact">{{ t('parameter.source') }}<select :value="parameter.source" @change="emit('update-source', nodeId, parameter, $event)"><option value="literal">{{ t('parameter.literal') }}</option><option value="previousNode">{{ t('parameter.previousNode') }}</option><option value="projectInput">{{ t('parameter.projectInput') }}</option><option value="expression">{{ t('parameter.expression') }}</option></select></label>
    <p class="parameter-source-hint">{{ t(`parameter.${parameter.source}Hint`) }}</p>
    <label v-if="parameter.source === 'literal' && isBooleanParameter(parameter)" class="field-label compact">{{ t('parameter.literalValue') }}<select :value="booleanLiteralValue(parameter)" @focus="emit('begin-text-edit')" @change="updateBooleanLiteral(parameter, $event)" @blur="emit('discard-text-edit')"><option value="" disabled>{{ t('parameter.booleanSelect') }}</option><option value="true">{{ t('parameter.booleanTrue') }}</option><option value="false">{{ t('parameter.booleanFalse') }}</option></select></label>
    <label v-else-if="parameter.source === 'literal' && hasEnumParameterOptions(parameter) && !isFlagsParameter(parameter)" class="field-label compact">{{ t('parameter.literalValue') }}<select :value="parameter.literalValue ?? ''" @focus="emit('begin-text-edit')" @change="updateEnumLiteral(parameter, $event)" @blur="emit('discard-text-edit')"><option value="" disabled>{{ t('parameter.enumSelect') }}</option><option v-for="option in parameter.enumMetadata?.options" :key="option.name" :value="option.name">{{ option.name }}</option></select><small class="enum-parameter-type mono">{{ parameter.enumMetadata?.typeName }}</small></label>
    <fieldset v-else-if="parameter.source === 'literal' && hasEnumParameterOptions(parameter) && isFlagsParameter(parameter)" class="enum-flags-editor"><legend>{{ t('parameter.literalValue') }}</legend><span class="enum-parameter-type mono">{{ parameter.enumMetadata?.typeName }}</span><div class="enum-flags-editor__options"><label v-for="option in parameter.enumMetadata?.options" :key="option.name" class="enum-flags-editor__option"><input type="checkbox" :checked="isFlagSelected(parameter, option.name)" @focus="emit('begin-text-edit')" @change="updateFlagsLiteral(parameter, option.name, $event)" @blur="emit('discard-text-edit')" /><span>{{ option.name }}</span><code>{{ option.numericValue }}</code></label></div></fieldset>
    <label v-else-if="parameter.source === 'literal'" class="field-label compact">{{ t('parameter.literalValue') }}<input v-model="parameter.literalValue" type="text" @focus="emit('begin-text-edit')" @input="emit('commit-text-edit')" @blur="emit('discard-text-edit')" /></label>
    <label v-else-if="parameter.source === 'projectInput'" class="field-label compact">{{ t('parameter.projectInputKey') }}<input v-model="parameter.projectInputKey" type="text" :placeholder="t('parameter.projectInputKeyPlaceholder')" @focus="emit('begin-text-edit')" @input="emit('commit-text-edit')" @blur="emit('discard-text-edit')" /></label>
    <label v-else-if="parameter.source === 'expression'" class="field-label compact">{{ t('parameter.expressionValue') }}<textarea v-model="parameter.expression" rows="2" :placeholder="t('parameter.expressionPlaceholder')" @focus="emit('begin-text-edit')" @input="emit('commit-text-edit')" @blur="emit('discard-text-edit')"></textarea></label>
    <p v-else class="source-detail"><GitBranch :size="13" />{{ t('inspector.connectedFrom', { node: sourceNodeTitle(parameter) }) }}</p>
  </div>
</template>
