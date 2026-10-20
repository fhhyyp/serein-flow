<script setup lang="ts">
type TemplateItem<T> = T extends readonly (infer Item)[] ? Item : T extends Record<string, infer Value> ? Value : T extends number ? number : never;
import { Maximize2, PanelRightClose, Plus, Settings2, Trash2 } from 'lucide-vue-next';
import { computed, defineAsyncComponent, inject, ref } from 'vue';
import { t } from '../../i18n';
import ParameterEditor from './ParameterEditor.vue';
import { libraryNameResolverKey } from '../../flow/libraryNameResolver';
import { formatNodeType } from '../../flow/typeDisplay';
import type { CanvasState, FlowEdge, FlowNode, MethodParameter, NodeKind } from '../../flow/types';
const ScriptEditorDialog = defineAsyncComponent(() => {
  const operationResult: Promise<any> = import('../editor/ScriptEditorDialog.vue');
  return operationResult;
});
const props = withDefaults(defineProps<{
  embedded?: boolean;
  mobileVisible: boolean;
  selectedNode: FlowNode | undefined;
  selectedEdge: FlowEdge | undefined;
  iconForNodeKind: (kind: NodeKind) => unknown;
  nodeTitle: (node: FlowNode) => string;
  sourceNodeTitle: (parameter: MethodParameter) => string;
  canvases: CanvasState[];
  entryNodeId: string;
}>(), {
  embedded: false,
});
const emit = defineEmits<{
  close: [
  ];
  delete: [
  ];
  'update-parameter-source': [
    nodeId: string,
    parameter: MethodParameter,
    event: Event
  ];
  'begin-text-edit': [
  ];
  'commit-text-edit': [
  ];
  'discard-text-edit': [
  ];
  'set-node-public': [
    nodeId: string,
    value: boolean
  ];
  'set-flow-entry': [
    nodeId: string,
    value: boolean
  ];
  'set-flowcall-target': [
    nodeId: string,
    canvasId: string,
    targetNodeId?: string
  ];
  'add-script-input': [
    nodeId: string
  ];
  'remove-script-input': [
    nodeId: string,
    parameterId: string
  ];
  'set-variadic-mode': [
    nodeId: string,
    parameterId: string,
    mode: 'expanded' | 'collection'
  ];
  'add-variadic-input': [
    nodeId: string,
    parameterId: string
  ];
  'remove-variadic-input': [
    nodeId: string,
    parameterId: string
  ];
  'continue-debug': [
  ];
  'step-debug': [
  ];
  'stop-debug': [
  ];
  'select-debug-node': [
    nodeId: string
  ];
}>();
const scriptEditorOpen = ref(false);
const scriptEditorSource = ref('');
const scriptEditorNodeId = ref('');
const scriptEditorNodeTitle = ref('');
const libraryNameFor = inject(libraryNameResolverKey, () => undefined);
const runtime = computed(() => props.selectedNode?.data.runtime);
const runtimeLibraryName = computed(() => {
  const libraryNameForResult: string | undefined = libraryNameFor(runtime.value);
  return libraryNameForResult;
});
const runtimeMember = computed(() => {
  const filterResult = [runtime.value?.className, runtime.value?.methodName].filter(Boolean);
  const joinResult: string = filterResult.join('.');
  return joinResult;
});
const runtimeAssembly = computed(() => {
  const filterResult1 = [runtime.value?.dllName, runtime.value?.dllVersion].filter(Boolean);
  const joinResult1: string = filterResult1.join(' · ');
  return joinResult1;
});
const runtimeReturnType = computed(() => {
  const formatNodeTypeResult: string = formatNodeType(runtime.value?.returnType);
  return formatNodeTypeResult;
});
const runtimeIsAwaitable = computed(() => runtime.value?.isAwaitable);
const hasRuntimeMetadata = computed(() => {
  const booleanResult: boolean = Boolean(runtimeLibraryName.value
    || runtimeMember.value
    || runtimeAssembly.value
    || runtimeReturnType.value
    || runtimeIsAwaitable.value !== undefined);
  return booleanResult;
});
function updateParameterSource(nodeId: string, parameter: MethodParameter, event: Event): void {
  emit('update-parameter-source', nodeId, parameter, event);
}
function updateScriptInputName(parameter: MethodParameter): void {
  parameter.name = parameter.name?.trimStart();
  parameter.nameKey = parameter.name || parameter.id;
}
function onPublicChange(event: Event): void {
  emit('set-node-public', props.selectedNode?.id ?? '', (event.target as HTMLInputElement).checked);
}
function onFlowEntryChange(event: Event): void {
  emit('set-flow-entry', props.selectedNode?.id ?? '', (event.target as HTMLInputElement).checked);
}
function onTargetCanvasChange(event: Event): void {
  emit('set-flowcall-target', props.selectedNode?.id ?? '', (event.target as HTMLSelectElement).value);
}
function onTargetNodeChange(event: Event): void {
  const selectedNode = props.selectedNode;
  emit('set-flowcall-target', selectedNode?.id ?? '', selectedNode?.data.runtime?.targetCanvasId ?? '', (event.target as HTMLSelectElement).value || undefined);
}
function publicNodes(canvasId: string, callNodeId: string): FlowNode[] {
  const targetCanvas = props.canvases.find((canvas) => canvas.id === canvasId);
  const evaluatedValue: FlowNode[] = targetCanvas?.nodes.filter((node) => node.id !== callNodeId && node.data.runtime?.isPublic === true) ?? [];
  return evaluatedValue;
}
function commitScriptInputName(parameter: MethodParameter): void {
  updateScriptInputName(parameter);
  emit('commit-text-edit');
}
function isFirstVariadic(node: FlowNode, parameter: MethodParameter): boolean {
  if (!parameter.isVariadic || !parameter.variadicGroupId) {
    return false;
  }
  const evaluatedValue1: boolean = node.data.parameters.find((candidate) => candidate.variadicGroupId === parameter.variadicGroupId)?.id === parameter.id;
  return evaluatedValue1;
}
function openScriptEditor(): void {
  const node = props.selectedNode;
  if (!node || node.data.kind !== 'script' || !node.data.script) {
    return;
  }
  scriptEditorNodeId.value = node.id;
  scriptEditorNodeTitle.value = props.nodeTitle(node);
  scriptEditorSource.value = node.data.script.source;
  scriptEditorOpen.value = true;
}
function closeScriptEditor(): void {
  scriptEditorOpen.value = false;
}
function applyScriptSource(source: string): void {
  const flatMapResult = props.canvases
    .flatMap((canvas) => canvas.nodes);
  const node = flatMapResult.find((candidate) => candidate.id === scriptEditorNodeId.value);
  if (!node?.data.script || source === node.data.script.source) {
    closeScriptEditor();
    return;
  }
  emit('begin-text-edit');
  node.data.script.source = source;
  emit('commit-text-edit');
  closeScriptEditor();
}
const readAsideClass = computed(() => ({ 'mobile-visible': props.mobileVisible, 'inspector-panel--empty': !props.selectedNode && !props.selectedEdge, 'inspector-panel--embedded': props.embedded }));
const readSpanClass = computed(() => (`kind-${props.selectedNode!.data.kind}`));
const readTemplateText = computed(() => {
  const evaluatedValue2: {
    [x: string]: string | number;
  } | undefined = { kind: t(`node.kind.${props.selectedNode!.data.kind}`) };
  const evaluatedValue3: string = (t('inspector.nodeType', evaluatedValue2));
  return evaluatedValue3;
});
const readInputChecked = computed(() => (props.entryNodeId === props.selectedNode!.id));
const readInputChecked2 = computed(() => (props.selectedNode!.data.runtime?.isPublic === true));
const readDivIf = computed(() => (runtimeIsAwaitable.value !== undefined));
const readTemplateText2 = computed(() => {
  const evaluatedValue4: string = (runtimeIsAwaitable.value ? t('inspector.yes') : t('inspector.no'));
  return evaluatedValue4;
});
const readDivIf2 = computed(() => (props.selectedNode!.data.kind === 'script' && props.selectedNode!.data.script));
const readDivIf3 = computed(() => (props.selectedNode!.data.kind === 'flowCall'));
const readSelectValue = computed(() => (props.selectedNode!.data.runtime?.targetCanvasId ?? ''));
function readOptionItems() {
  return (props.canvases);
}
function readTemplateText3(canvas: TemplateItem<ReturnType<typeof readOptionItems>>) {
  const evaluatedValue5 = (canvas.name || t(canvas.nameKey));
  return evaluatedValue5;
}
const readSelectValue2 = computed(() => (props.selectedNode!.data.runtime?.targetNodeId ?? ''));
function readOptionItems2() {
  const evaluatedValue6 = (publicNodes(props.selectedNode!.data.runtime?.targetCanvasId ?? '', props.selectedNode!.id));
  return evaluatedValue6;
}
const readPIf = computed(() => {
  const evaluatedValue7: boolean | "" | undefined = (props.selectedNode!.data.runtime?.targetCanvasId && publicNodes(props.selectedNode!.data.runtime.targetCanvasId, props.selectedNode!.id).length === 0);
  return evaluatedValue7;
});
const readTemplateText4 = computed(() => {
  const evaluatedValue8: string = (props.selectedNode!.data.kind === 'script' ? t('inspector.scriptInputs') : t('inspector.parameters'));
  return evaluatedValue8;
});
const readButtonIf = computed(() => (props.selectedNode!.data.kind === 'script'));
const readPIf2 = computed(() => (props.selectedNode!.data.parameters.length === 0));
const readTemplateText5 = computed(() => {
  const evaluatedValue9: string = (props.selectedNode!.data.kind === 'script' ? t('inspector.noScriptInputs') : t('inspector.noParameters'));
  return evaluatedValue9;
});
function readDivItems() {
  return (props.selectedNode!.data.parameters);
}
const readDivIf4 = computed(() => (props.selectedNode!.data.kind === 'script'));
const readDivClass = computed(() => ([props.selectedEdge!.data?.semantic, props.selectedEdge!.data?.semantic === 'execution' ? `branch-${props.selectedEdge!.data?.branch ?? 'success'}` : undefined]));
const readTemplateText6 = computed(() => {
  const evaluatedValue10: string = (props.selectedEdge!.data?.semantic === 'execution' ? `${t('inspector.executionEdge')} · ${t(`branch.${props.selectedEdge!.data?.branch ?? 'success'}`)}` : t('inspector.dataEdge'));
  return evaluatedValue10;
});
const readTemplateText7 = computed(() => {
  const evaluatedValue11: string = (props.selectedEdge!.data?.semantic === 'execution' ? t('edge.executionDescription') : t('edge.dataDescription'));
  return evaluatedValue11;
});
</script>

<template>
  <aside
    class="inspector-panel"
    :class="readAsideClass"
  >
    <template v-if="props.selectedNode">
      <div class="inspector-heading">
        <div>
          <span class="eyebrow">{{ t('inspector.title') }}</span>
          <h2>{{ props.nodeTitle(props.selectedNode) }}</h2>
        </div>
        <button
          v-if="!props.embedded"
          class="icon-button"
          type="button"
          :title="t('panel.collapseInspector')"
          :aria-label="t('panel.collapseInspector')"
          @click="emit('close')"
        >
          <PanelRightClose :size="16" />
        </button>
      </div>
      <div class="inspector-type">
        <span
          class="node-icon"
          :class="readSpanClass"
        >
          <component
            :is="props.iconForNodeKind(props.selectedNode.data.kind)"
            :size="15"
           />
        </span>
        <span>{{ readTemplateText }}</span>
        <span class="inspector-id mono">#{{ props.selectedNode.id }}</span>
      </div>
      <div class="inspector-section">
        <span class="section-label">{{ t('inspector.general') }}</span>
        <label class="field-label">
          {{ t('inspector.displayName') }}
          <input
            v-model="props.selectedNode.data.displayName"
            type="text"
            :placeholder="t(props.selectedNode.data.titleKey)"
            @focus="emit('begin-text-edit')"
            @input="emit('commit-text-edit')"
            @blur="emit('discard-text-edit')"
           />
        </label>
        <label class="field-label">
          {{ t('inspector.description') }}
          <textarea
            v-model="props.selectedNode.data.description"
            rows="2"
            :placeholder="t(props.selectedNode.data.subtitleKey)"
            @focus="emit('begin-text-edit')"
            @input="emit('commit-text-edit')"
            @blur="emit('discard-text-edit')"
          ></textarea>
        </label>
        <label class="toggle-field">
          <input
            type="checkbox"
            :checked="readInputChecked"
            @change="onFlowEntryChange"
           />
          <span>{{ t('inspector.flowEntry') }}</span>
          <small>{{ t('inspector.flowEntryHint') }}</small>
        </label>
        <label class="toggle-field">
          <input
            type="checkbox"
            :checked="readInputChecked2"
            @change="onPublicChange"
           />
          <span>{{ t('inspector.publicNode') }}</span>
          <small>{{ t('inspector.publicNodeHint') }}</small>
        </label>
      </div>
      <section
        v-if="hasRuntimeMetadata"
        class="inspector-section inspector-runtime"
      >
        <span class="section-label">{{ t('inspector.runtime') }}</span>
        <dl class="inspector-runtime__list">
          <div v-if="runtimeLibraryName">
            <dt>{{ t('inspector.runtimeLibrary') }}</dt>
            <dd>{{ runtimeLibraryName }}</dd>
          </div>
          <div v-if="runtimeMember">
            <dt>{{ t('inspector.runtimeMember') }}</dt>
            <dd>
              <code>{{ runtimeMember }}</code>
            </dd>
          </div>
          <div v-if="runtimeAssembly">
            <dt>{{ t('inspector.runtimeAssembly') }}</dt>
            <dd>
              <code>{{ runtimeAssembly }}</code>
            </dd>
          </div>
          <div v-if="runtimeReturnType">
            <dt>{{ t('inspector.runtimeReturnType') }}</dt>
            <dd>
              <code>{{ runtimeReturnType }}</code>
            </dd>
          </div>
          <div v-if="readDivIf">
            <dt>{{ t('inspector.runtimeAwaitable') }}</dt>
            <dd>{{ readTemplateText2 }}</dd>
          </div>
        </dl>
      </section>
      <div
        v-if="readDivIf2"
        class="inspector-section script-editor"
      >
        <div class="section-label-row">
          <span class="section-label">{{ t('inspector.script') }}</span>
          <button
            class="icon-button compact"
            type="button"
            :title="t('inspector.openScriptEditor')"
            :aria-label="t('inspector.openScriptEditor')"
            @click="openScriptEditor"
          >
            <Maximize2 :size="14" />
          </button>
        </div>
        <label class="field-label">
          {{ t('inspector.scriptSource') }}
          <textarea
            :value="props.selectedNode.data.script!.source"
            class="script-source-input mono"
            rows="8"
            spellcheck="false"
            readonly
          ></textarea>
        </label>
      </div>
      <div
        v-if="readDivIf3"
        class="inspector-section flowcall-editor"
      >
        <span class="section-label">{{ t('inspector.flowCallTarget') }}</span>
        <label class="field-label">
          {{ t('inspector.targetCanvas') }}
          <select
            :value="readSelectValue"
            @change="onTargetCanvasChange"
          >
            <option value="">{{ t('inspector.selectCanvas') }}</option>
            <option
              v-for="canvas in readOptionItems()"
              :key="canvas.id"
              :value="canvas.id"
            >{{ readTemplateText3(canvas) }}</option>
          </select>
        </label>
        <label
          v-if="props.selectedNode.data.runtime?.targetCanvasId"
          class="field-label"
        >
          {{ t('inspector.publicTargetNode') }}
          <select
            :value="readSelectValue2"
            @change="onTargetNodeChange"
          >
            <option value="">{{ t('inspector.selectPublicNode') }}</option>
            <option
              v-for="node in readOptionItems2()"
              :key="node.id"
              :value="node.id"
            >{{ props.nodeTitle(node) }}</option>
          </select>
        </label>
        <p
          v-if="readPIf"
          class="empty-copy"
        >{{ t('inspector.noPublicNodes') }}</p>
      </div>
      <div class="inspector-section parameter-section">
        <div class="section-label-row">
          <span class="section-label">{{ readTemplateText4 }}</span>
          <button
            v-if="readButtonIf"
            class="icon-button compact"
            type="button"
            :title="t('inspector.addScriptInput')"
            :aria-label="t('inspector.addScriptInput')"
            @click="emit('add-script-input', props.selectedNode.id)"
          >
            <Plus :size="14" />
          </button>
        </div>
        <p
          v-if="readPIf2"
          class="empty-copy"
        >{{ readTemplateText5 }}</p>
        <div
          v-for="parameter in readDivItems()"
          :key="parameter.id"
          class="parameter-editor-wrap"
        >
          <div
            v-if="readDivIf4"
            class="script-input-meta"
          >
            <label class="field-label compact">
              {{ t('inspector.inputName') }}
              <input
                v-model="parameter.name"
                type="text"
                @focus="emit('begin-text-edit')"
                @input="commitScriptInputName(parameter)"
                @blur="emit('discard-text-edit')"
               />
            </label>
            <label class="field-label compact">
              {{ t('parameter.valueKind') }}
              <input
                v-model="parameter.valueKind"
                type="text"
                @focus="emit('begin-text-edit')"
                @input="emit('commit-text-edit')"
                @blur="emit('discard-text-edit')"
               />
            </label>
            <label class="field-label compact">
              {{ t('inspector.inputRemark') }}
              <input
                v-model="parameter.description"
                type="text"
                @focus="emit('begin-text-edit')"
                @input="emit('commit-text-edit')"
                @blur="emit('discard-text-edit')"
               />
            </label>
            <label class="script-input-required">
              <input
                v-model="parameter.required"
                type="checkbox"
                @focus="emit('begin-text-edit')"
                @change="emit('commit-text-edit')"
               />
              {{ t('inspector.required') }}
            </label>
            <button
              class="icon-button compact danger"
              type="button"
              :title="t('inspector.removeScriptInput')"
              :aria-label="t('inspector.removeScriptInput')"
              @click="emit('remove-script-input', props.selectedNode.id, parameter.id)"
            >
              <Trash2 :size="14" />
            </button>
          </div>
          <ParameterEditor
            :node-id="props.selectedNode.id"
            :parameter="parameter"
            :source-node-title="props.sourceNodeTitle"
            :show-variadic-controls="isFirstVariadic(props.selectedNode, parameter)"
            @update-source="updateParameterSource"
            @begin-text-edit="emit('begin-text-edit')"
            @commit-text-edit="emit('commit-text-edit')"
            @discard-text-edit="emit('discard-text-edit')"
            @set-variadic-mode="emit('set-variadic-mode', props.selectedNode.id, parameter.id, $event)"
            @add-variadic-input="emit('add-variadic-input', props.selectedNode.id, parameter.id)"
            @remove-variadic-input="emit('remove-variadic-input', props.selectedNode.id, parameter.id)"
           />
        </div>
      </div>
    </template>
    <template v-else-if="props.selectedEdge">
      <div class="inspector-heading">
        <div>
          <span class="eyebrow">{{ t('inspector.title') }}</span>
          <h2>{{ t('inspector.edgeSelected') }}</h2>
        </div>
        <div class="inspector-heading__actions">
          <button
            class="icon-button"
            type="button"
            :title="t('command.delete')"
            :aria-label="t('command.delete')"
            @click="emit('delete')"
          >
            <Trash2 :size="16" />
          </button>
          <button
            class="icon-button"
            type="button"
            :title="t('panel.collapseInspector')"
            :aria-label="t('panel.collapseInspector')"
            @click="emit('close')"
          >
            <PanelRightClose :size="16" />
          </button>
        </div>
      </div>
      <div
        class="edge-summary"
        :class="readDivClass"
      >
        <span class="edge-sample"></span>
        <strong>{{ readTemplateText6 }}</strong>
        <p>{{ readTemplateText7 }}</p>
      </div>
    </template>
    <template v-else>
      <div class="inspector-heading">
        <div>
          <span class="eyebrow">{{ t('inspector.title') }}</span>
          <h2>{{ t('canvas.emptySelection') }}</h2>
        </div>
        <button
          class="icon-button"
          type="button"
          :title="t('panel.collapseInspector')"
          :aria-label="t('panel.collapseInspector')"
          @click="emit('close')"
        >
          <PanelRightClose :size="16" />
        </button>
      </div>
      <div class="inspector-empty">
        <Settings2 :size="20" />
        <p>{{ t('inspector.selectNode') }}</p>
      </div>
    </template>
  </aside>
  <ScriptEditorDialog
    v-if="scriptEditorOpen"
    :open="scriptEditorOpen"
    :source="scriptEditorSource"
    :node-title="scriptEditorNodeTitle"
    @close="closeScriptEditor"
    @apply="applyScriptSource"
   />
</template>
