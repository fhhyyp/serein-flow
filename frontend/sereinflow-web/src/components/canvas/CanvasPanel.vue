<script setup lang="ts">
import { unref } from 'vue';
import { computed } from 'vue';
type TemplateItem<T> = T extends readonly (infer Item)[] ? Item : T extends Record<string, infer Value> ? Value : T extends number ? number : never;
import { markRaw, ref } from 'vue';
import { Check, LayoutGrid, ListChecks, LocateFixed, Plus, Save, Settings2, Trash2, X } from 'lucide-vue-next';
import { ConnectionMode, SelectionMode, VueFlow, type Connection, type EdgeChange, type EdgeUpdateEvent, type NodeChange, } from '@vue-flow/core';
import FlowConnectionLine from '../flow/FlowConnectionLine.vue';
import FlowBezierEdge from '../flow/FlowBezierEdge.vue';
import FlowSmoothStepEdge from '../flow/FlowSmoothStepEdge.vue';
import FlowNodeCard from '../flow/FlowNodeCard.vue';
import CanvasDeleteConfirmDialog from './CanvasDeleteConfirmDialog.vue';
import { t } from '../../i18n';
import type { CanvasState, ConnectionSemantic, FlowEdge, FlowNode } from '../../flow/types';
import { connectionLineTypeOptions, type ConnectionLineSettings } from '../../flow/connectionLine';
import type { CanvasFocusSettingKey, CanvasFocusSettings } from '../../flow/canvasFocus';
const nodeTypes = markRaw({ workflow: FlowNodeCard });
const edgeTypes = markRaw({ default: FlowBezierEdge, smoothstep: FlowSmoothStepEdge });
const draggedCanvasId = ref<string>();
const dropTargetCanvasId = ref<string>();
const props = defineProps<{
  canvases: CanvasState[];
  activeCanvasId: string;
  currentCanvasLifecycle: CanvasState['lifecycle'];
  currentCanvasNodeCount: number;
  currentCanvasEdgeCount: number;
  renderedElements: Array<FlowNode | FlowEdge>;
  isValidConnection: (connection: Connection) => boolean;
  canvasRenderKey: string;
  canvasMenuOpen: boolean;
  availableCanvasLifecycles: CanvasState['lifecycle'][];
  customCanvasNameDraft: string;
  connectionSettingsOpen: boolean;
  connectionLineTypes: ConnectionLineSettings;
  canvasFocusSettings: CanvasFocusSettings;
  isDirty: boolean;
  isSaving: boolean;
  isWorkspaceLoading: boolean;
  saveFailed: boolean;
  saveConflict: boolean;
  saveStateKey: string;
  selectedNode: FlowNode | undefined;
  selectedEdge: FlowEdge | undefined;
  selectedNodeCount: number;
  selectedEdgeCount: number;
  isCanvasDropActive: boolean;
  notice: string;
  pendingCanvasDelete?: CanvasState;
  canvasDeleteConfirmOpen: boolean;
  canvasLabel: (canvas: CanvasState) => string;
}>();
const emit = defineEmits<{
  'select-canvas': [
    canvasId: string
  ];
  'reorder-canvases': [
    sourceCanvasId: string,
    targetCanvasId: string
  ];
  'toggle-canvas-menu': [
  ];
  'add-canvas': [
    lifecycle: CanvasState['lifecycle']
  ];
  'add-custom-canvas': [
  ];
  'update:customCanvasNameDraft': [
    value: string
  ];
  'toggle-connection-settings': [
  ];
  'update-connection-line-type': [
    semantic: ConnectionSemantic,
    event: Event
  ];
  'update-canvas-focus-setting': [
    setting: CanvasFocusSettingKey,
    event: Event
  ];
  'remove-selection': [
  ];
  'select-all-nodes': [
  ];
  'request-canvas-removal': [
  ];
  'cancel-canvas-removal': [
  ];
  'confirm-canvas-removal': [
  ];
  'canvas-dragover': [
    event: DragEvent
  ];
  'canvas-dragenter': [
    event: DragEvent
  ];
  'canvas-dragleave': [
    event: DragEvent
  ];
  'canvas-drop': [
    event: DragEvent
  ];
  connect: [
    connection: Connection
  ];
  'edge-update': [
    event: EdgeUpdateEvent
  ];
  'nodes-change': [
    changes: NodeChange[]
  ];
  'edges-change': [
    changes: EdgeChange[]
  ];
  'node-click': [
    event: {
      node: {
        id: string;
      };
    }
  ];
  'edge-click': [
    event: {
      edge: {
        id: string;
      };
    }
  ];
  'pane-click': [
  ];
  'zoom-in': [
  ];
  'zoom-out': [
  ];
  'fit-view': [
  ];
}>();
function updateCustomCanvasName(event: Event): void {
  emit('update:customCanvasNameDraft', (event.target as HTMLInputElement).value);
}
function startCanvasDrag(event: DragEvent, canvasId: string): void {
  draggedCanvasId.value = canvasId;
  dropTargetCanvasId.value = undefined;
  if (event.dataTransfer) {
    event.dataTransfer.effectAllowed = 'move';
    event.dataTransfer.setData('text/plain', canvasId);
  }
}
function dragOverCanvas(event: DragEvent, canvasId: string): void {
  event.preventDefault();
  if (!draggedCanvasId.value || draggedCanvasId.value === canvasId) {
    return;
  }
  if (event.dataTransfer) {
    event.dataTransfer.dropEffect = 'move';
  }
  dropTargetCanvasId.value = canvasId;
}
function dropCanvas(event: DragEvent, targetCanvasId: string): void {
  event.preventDefault();
  const sourceCanvasId = draggedCanvasId.value || event.dataTransfer?.getData('text/plain');
  clearCanvasDragState();
  if (!sourceCanvasId || sourceCanvasId === targetCanvasId) {
    return;
  }
  emit('reorder-canvases', sourceCanvasId, targetCanvasId);
}
function clearCanvasDragState(): void {
  draggedCanvasId.value = undefined;
  dropTargetCanvasId.value = undefined;
}
function readButtonItems() {
  return (props.canvases);
}
function readButtonId(canvas: TemplateItem<ReturnType<typeof readButtonItems>>) {
  return (`canvas-tab-${canvas.id}`);
}
function readButtonAriaSelected(canvas: TemplateItem<ReturnType<typeof readButtonItems>>) {
  return (canvas.id === props.activeCanvasId);
}
const readButtonDraggable = computed(() => (props.canvases.length > 1));
const readButtonTitle = computed(() => {
  const evaluatedValue: string | undefined = (props.canvases.length > 1 ? t('canvas.reorderHint') : undefined);
  return evaluatedValue;
});
function readButtonClass(canvas: TemplateItem<ReturnType<typeof readButtonItems>>) {
  return ({ active: canvas.id === props.activeCanvasId, dragging: canvas.id === draggedCanvasId.value, 'drop-target': canvas.id === dropTargetCanvasId.value });
}
function handleButtonDragleave() {
  dropTargetCanvasId.value = undefined;
}
function readButtonItems2() {
  return (props.availableCanvasLifecycles);
}
function readTemplateText(lifecycle: TemplateItem<ReturnType<typeof readButtonItems2>>) {
  const evaluatedValue1 = (t(`canvas.${lifecycle}`));
  return evaluatedValue1;
}
const readPIf = computed(() => (props.availableCanvasLifecycles.length === 0));
const readSpanIf = computed(() => (props.currentCanvasNodeCount > 0 && props.selectedNodeCount === 0));
const readCheckIf = computed(() => (!props.isDirty && !props.saveFailed && !props.saveConflict && !props.isSaving && !props.isWorkspaceLoading));
function readOptionItems() {
  const evaluatedValue2 = (unref(connectionLineTypeOptions));
  return evaluatedValue2;
}
function readOptionItems2() {
  const evaluatedValue3 = (unref(connectionLineTypeOptions));
  return evaluatedValue3;
}
const readSpanIf2 = computed(() => (props.selectedNodeCount > 1));
const readTemplateText2 = computed(() => {
  const evaluatedValue4: string = (t('canvas.selectedNodes', { count: props.selectedNodeCount }));
  return evaluatedValue4;
});
const readButtonDisabled = computed(() => (props.currentCanvasNodeCount === 0));
const readButtonDisabled2 = computed(() => (props.selectedNodeCount === 0 && props.selectedEdgeCount === 0));
const readButtonDisabled3 = computed(() => (props.currentCanvasLifecycle === 'main'));
const readDivClass = computed(() => ({ 'canvas-drop-active': props.isCanvasDropActive }));
const readVueFlowMultiSelectionKeyCode = computed(() => (['Control', 'Meta']));
const readVueFlowSnapGrid = computed<[
  number,
  number
]>(() => ([16, 16]));
const readVueFlowDeleteKeyCode = computed(() => (['Backspace', 'Delete']));
const readDivIf = computed(() => (props.currentCanvasNodeCount === 0));
</script>

<template>
  <section
    class="canvas-panel"
    :aria-label="t('canvas.mainHint')"
  >
    <div class="canvas-toolbar">
      <div class="canvas-tab-row">
        <div
          class="canvas-tabs"
          role="tablist"
          :aria-label="t('canvas.options')"
        >
          <button
            v-for="canvas in readButtonItems()"
            :id="readButtonId(canvas)"
            :key="canvas.id"
            type="button"
            role="tab"
            :aria-selected="readButtonAriaSelected(canvas)"
            :draggable="readButtonDraggable"
            :title="readButtonTitle"
            :class="readButtonClass(canvas)"
            @click="emit('select-canvas', canvas.id)"
            @dragstart="startCanvasDrag($event, canvas.id)"
            @dragenter="dragOverCanvas($event, canvas.id)"
            @dragover="dragOverCanvas($event, canvas.id)"
            @dragleave="handleButtonDragleave()"
            @drop="dropCanvas($event, canvas.id)"
            @dragend="clearCanvasDragState"
          >{{ props.canvasLabel(canvas) }}</button>
        </div>
        <div class="canvas-menu">
          <button
            class="icon-button compact"
            type="button"
            :title="t('canvas.add')"
            :aria-label="t('canvas.add')"
            :aria-expanded="props.canvasMenuOpen"
            @click="emit('toggle-canvas-menu')"
          >
            <Plus :size="15" />
          </button>
          <div
            v-if="props.canvasMenuOpen"
            class="canvas-popover"
            role="menu"
          >
            <button
              v-for="lifecycle in readButtonItems2()"
              :key="lifecycle"
              type="button"
              role="menuitem"
              @click="emit('add-canvas', lifecycle)"
            >{{ readTemplateText(lifecycle) }}</button>
            <p v-if="readPIf">{{ t('canvas.allLifecycleCanvases') }}</p>
            <form
              class="canvas-custom-form"
              @submit.prevent="emit('add-custom-canvas')"
            >
              <label>
                {{ t('canvas.customName') }}
                <input
                  :value="props.customCanvasNameDraft"
                  type="text"
                  :placeholder="t('canvas.customNamePlaceholder')"
                  maxlength="60"
                  @input="updateCustomCanvasName"
                 />
              </label>
              <button
                type="submit"
                :title="t('canvas.addCustom')"
                :aria-label="t('canvas.addCustom')"
              >
                <Plus :size="14" />
              </button>
            </form>
          </div>
        </div>
      </div>
      <div class="canvas-tools">
        <span
          v-if="readSpanIf"
          class="canvas-selection-hint"
        >{{ t('canvas.selectionHint') }}</span>
        <span
          class="save-state"
          role="status"
        >
          <Check
            v-if="readCheckIf"
            :size="14"
           />
          <Save
            v-else
            :size="14"
           />
          {{ t(props.saveStateKey) }}
        </span>
        <div class="connection-settings">
          <button
            class="icon-button compact"
            type="button"
            :title="t('canvas.canvasSettings')"
            :aria-label="t('canvas.canvasSettings')"
            :aria-expanded="props.connectionSettingsOpen"
            @click="emit('toggle-connection-settings')"
          >
            <Settings2 :size="15" />
          </button>
          <div
            v-if="props.connectionSettingsOpen"
            class="connection-settings-popover"
            role="dialog"
            :aria-label="t('canvas.canvasSettings')"
          >
            <section
              class="connection-settings-panel"
              :aria-label="t('canvas.connectionSettings')"
            >
              <span class="connection-settings-popover__title">{{ t('canvas.connectionSettings') }}</span>
              <p>{{ t('canvas.connectionSettingsHint') }}</p>
              <label class="connection-settings-field">
                {{ t('canvas.executionLineType') }}
                <select
                  :value="props.connectionLineTypes.execution"
                  @change="emit('update-connection-line-type', 'execution', $event)"
                >
                  <option
                    v-for="option in readOptionItems()"
                    :key="option.value"
                    :value="option.value"
                  >{{ t(option.labelKey) }}</option>
                </select>
              </label>
              <label class="connection-settings-field">
                {{ t('canvas.dataLineType') }}
                <select
                  :value="props.connectionLineTypes.data"
                  @change="emit('update-connection-line-type', 'data', $event)"
                >
                  <option
                    v-for="option in readOptionItems2()"
                    :key="option.value"
                    :value="option.value"
                  >{{ t(option.labelKey) }}</option>
                </select>
              </label>
            </section>
            <section
              class="connection-settings-panel"
              :aria-label="t('canvas.focusSettings')"
            >
              <span class="connection-settings-popover__title">{{ t('canvas.focusSettings') }}</span>
              <p>{{ t('canvas.focusSettingsHint') }}</p>
              <label class="focus-settings-toggle">
                <span>{{ t('canvas.focusEnabled') }}</span>
                <input
                  :checked="props.canvasFocusSettings.enabled"
                  type="checkbox"
                  role="switch"
                  @change="emit('update-canvas-focus-setting', 'enabled', $event)"
                 />
                <span
                  class="focus-settings-toggle__track"
                  aria-hidden="true"
                >
                  <span></span>
                </span>
              </label>
              <div
                class="focus-settings-relations"
                :aria-disabled="!props.canvasFocusSettings.enabled"
              >
                <label class="focus-settings-option">
                  <input
                    :checked="props.canvasFocusSettings.parameterSources"
                    type="checkbox"
                    :disabled="!props.canvasFocusSettings.enabled"
                    @change="emit('update-canvas-focus-setting', 'parameterSources', $event)"
                   />
                  <span>{{ t('canvas.focusParameterSources') }}</span>
                </label>
                <label class="focus-settings-option">
                  <input
                    :checked="props.canvasFocusSettings.parameterConsumers"
                    type="checkbox"
                    :disabled="!props.canvasFocusSettings.enabled"
                    @change="emit('update-canvas-focus-setting', 'parameterConsumers', $event)"
                   />
                  <span>{{ t('canvas.focusParameterConsumers') }}</span>
                </label>
                <label class="focus-settings-option">
                  <input
                    :checked="props.canvasFocusSettings.callers"
                    type="checkbox"
                    :disabled="!props.canvasFocusSettings.enabled"
                    @change="emit('update-canvas-focus-setting', 'callers', $event)"
                   />
                  <span>{{ t('canvas.focusCallers') }}</span>
                </label>
                <label class="focus-settings-option">
                  <input
                    :checked="props.canvasFocusSettings.callees"
                    type="checkbox"
                    :disabled="!props.canvasFocusSettings.enabled"
                    @change="emit('update-canvas-focus-setting', 'callees', $event)"
                   />
                  <span>{{ t('canvas.focusCallees') }}</span>
                </label>
              </div>
            </section>
          </div>
        </div>
        <span
          v-if="readSpanIf2"
          class="canvas-selection-count"
          role="status"
          aria-live="polite"
        >{{ readTemplateText2 }}</span>
        <button
          class="icon-button compact"
          type="button"
          :title="t('canvas.selectAllNodes')"
          :aria-label="t('canvas.selectAllNodes')"
          :disabled="readButtonDisabled"
          @click="emit('select-all-nodes')"
        >
          <ListChecks :size="15" />
        </button>
        <button
          class="icon-button"
          type="button"
          :title="t('command.delete')"
          :aria-label="t('command.delete')"
          :disabled="readButtonDisabled2"
          @click="emit('remove-selection')"
        >
          <Trash2 :size="16" />
        </button>
        <button
          class="icon-button canvas-delete-button"
          type="button"
          :title="t('canvas.remove')"
          :aria-label="t('canvas.remove')"
          :disabled="readButtonDisabled3"
          @click="emit('request-canvas-removal')"
        >
          <X :size="16" />
        </button>
      </div>
    </div>
    <div
      class="canvas-area"
      :class="readDivClass"
      @dragenter.prevent="emit('canvas-dragenter', $event)"
      @dragover.prevent="emit('canvas-dragover', $event)"
      @dragleave="emit('canvas-dragleave', $event)"
      @drop.prevent="emit('canvas-drop', $event)"
    >
      <VueFlow
        id="workspace-editor"
        :key="props.canvasRenderKey"
        :model-value="props.renderedElements"
        :node-types="nodeTypes"
        :edge-types="edgeTypes"
        :connection-mode="ConnectionMode.Strict"
        :is-valid-connection="props.isValidConnection"
        :edges-updatable="'target'"
        :nodes-draggable="true"
        :elements-selectable="true"
        :select-nodes-on-drag="true"
        :selection-key-code="'Shift'"
        :multi-selection-key-code="readVueFlowMultiSelectionKeyCode"
        :selection-mode="SelectionMode.Partial"
        :min-zoom="0.2"
        :max-zoom="2"
        :snap-to-grid="true"
        :snap-grid="readVueFlowSnapGrid"
        :fit-view-on-init="true"
        :delete-key-code="readVueFlowDeleteKeyCode"
        class="serein-flow"
        @connect="emit('connect', $event)"
        @edge-update="emit('edge-update', $event)"
        @nodes-change="emit('nodes-change', $event)"
        @edges-change="emit('edges-change', $event)"
        @node-click="emit('node-click', $event)"
        @edge-click="emit('edge-click', $event)"
        @pane-click="emit('pane-click')"
      >
        <template #connection-line="connectionLineProps">
          <FlowConnectionLine
            v-bind="connectionLineProps"
            :line-types="props.connectionLineTypes"
           />
        </template>
      </VueFlow>
      <div
        v-if="readDivIf"
        class="canvas-empty-state"
        aria-live="polite"
      >
        <div class="canvas-empty-state__mark">
          <LayoutGrid :size="20" />
        </div>
        <strong>{{ t('canvas.emptyTitle') }}</strong>
        <p>{{ t('canvas.emptyHint') }}</p>
        <span>{{ t('canvas.emptySecondary') }}</span>
      </div>
      <span
        v-if="props.isCanvasDropActive"
        class="canvas-drop-hint"
      >{{ t('canvas.dropNode') }}</span>
      <Transition name="canvas-notice">
        <p
          v-if="props.notice"
          class="canvas-notice"
          role="status"
        >{{ props.notice }}</p>
      </Transition>
      <div
        class="canvas-legend"
        aria-hidden="true"
      >
        <span>
          <i class="legend-port execution"></i>
          {{ t('edge.flow') }}
        </span>
        <span>
          <i class="legend-port data"></i>
          {{ t('edge.value') }}
        </span>
      </div>
      <div
        class="zoom-control"
        :aria-label="t('canvas.options')"
      >
        <button
          type="button"
          :title="t('canvas.zoomOut')"
          :aria-label="t('canvas.zoomOut')"
          @click="emit('zoom-out')"
        >-</button>
        <button
          type="button"
          :title="t('canvas.fitView')"
          :aria-label="t('canvas.fitView')"
          @click="emit('fit-view')"
        >
          <LocateFixed :size="14" />
        </button>
        <button
          type="button"
          :title="t('canvas.zoomIn')"
          :aria-label="t('canvas.zoomIn')"
          @click="emit('zoom-in')"
        >+</button>
      </div>
      <CanvasDeleteConfirmDialog
        v-if="props.pendingCanvasDelete"
        :open="props.canvasDeleteConfirmOpen"
        :canvas-name="props.canvasLabel(props.pendingCanvasDelete)"
        :nodes="props.pendingCanvasDelete.nodes.length"
        :edges="props.pendingCanvasDelete.edges.length"
        @cancel="emit('cancel-canvas-removal')"
        @confirm="emit('confirm-canvas-removal')"
       />
    </div>
  </section>
</template>
