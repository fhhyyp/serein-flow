<script setup lang="ts">
import { AlertTriangle, LocateFixed, X } from 'lucide-vue-next'
import { localizeMessage, t } from '../../i18n'
import type { FlowValidationDiagnostic } from '../../api/flowApi'
import type { CanvasState, FlowNode } from '../../flow/types'
import { parseFlowValidationDiagnosticTarget } from '../../flow/validationDiagnostics'

const props = defineProps<{
  diagnostics: FlowValidationDiagnostic[]
  canvases: CanvasState[]
}>()

const emit = defineEmits<{
  dismiss: []
  locate: [diagnostic: FlowValidationDiagnostic]
}>()

interface DiagnosticTarget {
  nodeId: string
  parameterName?: string
  node?: FlowNode
}

function targetFor(diagnostic: FlowValidationDiagnostic): DiagnosticTarget | undefined {
  const target = parseFlowValidationDiagnosticTarget(diagnostic.path)
  if (!target) {
    return undefined
  }

  return {
    ...target,
    node: props.canvases.flatMap((canvas) => canvas.nodes).find((node) => node.id === target.nodeId),
  }
}

function nodeLabel(diagnostic: FlowValidationDiagnostic): string {
  const target = targetFor(diagnostic)
  const displayName = target?.node?.data.displayName?.trim()
  return displayName || target?.nodeId || t('diagnostics.unknownNode')
}
</script>

<template>
  <section v-if="props.diagnostics.length" class="flow-validation-diagnostics" role="alert" aria-live="assertive">
    <header class="flow-validation-diagnostics__header">
      <div class="flow-validation-diagnostics__title"><AlertTriangle :size="17" aria-hidden="true" /><div><strong>{{ t('diagnostics.title') }}</strong><span>{{ t('diagnostics.count', { count: props.diagnostics.length }) }}</span></div></div>
      <button class="icon-button compact" type="button" :title="t('diagnostics.dismiss')" :aria-label="t('diagnostics.dismiss')" @click="emit('dismiss')"><X :size="15" /></button>
    </header>
    <ol class="flow-validation-diagnostics__list">
      <li v-for="diagnostic in props.diagnostics" :key="`${diagnostic.code}:${diagnostic.path ?? diagnostic.message}`">
        <div class="flow-validation-diagnostics__item-body">
          <div class="flow-validation-diagnostics__meta"><code>{{ diagnostic.code }}</code><span>{{ t('diagnostics.node') }}: {{ nodeLabel(diagnostic) }}</span><span v-if="targetFor(diagnostic)?.parameterName">{{ t('diagnostics.parameter') }}: {{ targetFor(diagnostic)?.parameterName }}</span></div>
          <p>{{ localizeMessage(diagnostic.message) }}</p>
        </div>
        <button v-if="targetFor(diagnostic)?.node" class="flow-validation-diagnostics__locate" type="button" :title="t('diagnostics.locate')" :aria-label="t('diagnostics.locate')" @click="emit('locate', diagnostic)"><LocateFixed :size="15" /></button>
      </li>
    </ol>
  </section>
</template>
