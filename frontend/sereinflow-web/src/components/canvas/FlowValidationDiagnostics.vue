<script setup lang="ts">
import { computed } from 'vue';
type TemplateItem<T> = T extends readonly (infer Item)[] ? Item : T extends Record<string, infer Value> ? Value : T extends number ? number : never;
import { AlertTriangle, LocateFixed, X } from 'lucide-vue-next';
import { localizeMessage, t } from '../../i18n';
import type { FlowValidationDiagnostic } from '../../api/flowApi';
import type { CanvasState, FlowNode } from '../../flow/types';
import { parseFlowValidationDiagnosticTarget } from '../../flow/validationDiagnostics';
const props = defineProps<{
  diagnostics: FlowValidationDiagnostic[];
  canvases: CanvasState[];
}>();
const emit = defineEmits<{
  dismiss: [
  ];
  locate: [
    diagnostic: FlowValidationDiagnostic
  ];
}>();
interface DiagnosticTarget {
  nodeId: string;
  parameterName?: string;
  node?: FlowNode;
}
function targetFor(diagnostic: FlowValidationDiagnostic): DiagnosticTarget | undefined {
  const target = parseFlowValidationDiagnosticTarget(diagnostic.path);
  if (!target) {
    return undefined;
  }
  const resolveFindResult = () => {
    const flatMapResult = props.canvases.flatMap((canvas) => canvas.nodes);
    const findResult: FlowNode | undefined = flatMapResult.find((node) => node.id === target.nodeId);
    return findResult;
  };
  const evaluatedValue: DiagnosticTarget | undefined = {
    ...target,
    node: resolveFindResult(),
  };
  return evaluatedValue;
}
function nodeLabel(diagnostic: FlowValidationDiagnostic): string {
  const target = targetFor(diagnostic);
  const displayName = target?.node?.data.displayName?.trim();
  const evaluatedValue1: string = displayName || target?.nodeId || t('diagnostics.unknownNode');
  return evaluatedValue1;
}
const readTemplateText = computed(() => {
  const evaluatedValue2: string = (t('diagnostics.count', { count: props.diagnostics.length }));
  return evaluatedValue2;
});
function readLiItems() {
  return (props.diagnostics);
}
function readLiKey(diagnostic: TemplateItem<ReturnType<typeof readLiItems>>) {
  return (`${diagnostic.code}:${diagnostic.path ?? diagnostic.message}`);
}
function readSpanIf(diagnostic: TemplateItem<ReturnType<typeof readLiItems>>) {
  const evaluatedValue3 = (targetFor(diagnostic)?.parameterName);
  return evaluatedValue3;
}
function readTemplateText2(diagnostic: TemplateItem<ReturnType<typeof readLiItems>>) {
  const evaluatedValue4 = (targetFor(diagnostic)?.parameterName);
  return evaluatedValue4;
}
function readButtonIf(diagnostic: TemplateItem<ReturnType<typeof readLiItems>>) {
  const evaluatedValue5 = (targetFor(diagnostic)?.node);
  return evaluatedValue5;
}
</script>

<template>
  <section
    v-if="props.diagnostics.length"
    class="flow-validation-diagnostics"
    role="alert"
    aria-live="assertive"
  >
    <header class="flow-validation-diagnostics__header">
      <div class="flow-validation-diagnostics__title">
        <AlertTriangle
          :size="17"
          aria-hidden="true"
         />
        <div>
          <strong>{{ t('diagnostics.title') }}</strong>
          <span>{{ readTemplateText }}</span>
        </div>
      </div>
      <button
        class="icon-button compact"
        type="button"
        :title="t('diagnostics.dismiss')"
        :aria-label="t('diagnostics.dismiss')"
        @click="emit('dismiss')"
      >
        <X :size="15" />
      </button>
    </header>
    <ol class="flow-validation-diagnostics__list">
      <li
        v-for="diagnostic in readLiItems()"
        :key="readLiKey(diagnostic)"
      >
        <div class="flow-validation-diagnostics__item-body">
          <div class="flow-validation-diagnostics__meta">
            <code>{{ diagnostic.code }}</code>
            <span>{{ t('diagnostics.node') }}: {{ nodeLabel(diagnostic) }}</span>
            <span v-if="readSpanIf(diagnostic)">{{ t('diagnostics.parameter') }}: {{ readTemplateText2(diagnostic) }}</span>
          </div>
          <p>{{ localizeMessage(diagnostic.message) }}</p>
        </div>
        <button
          v-if="readButtonIf(diagnostic)"
          class="flow-validation-diagnostics__locate"
          type="button"
          :title="t('diagnostics.locate')"
          :aria-label="t('diagnostics.locate')"
          @click="emit('locate', diagnostic)"
        >
          <LocateFixed :size="15" />
        </button>
      </li>
    </ol>
  </section>
</template>
