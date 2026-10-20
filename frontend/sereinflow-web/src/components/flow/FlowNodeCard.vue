<script setup lang="ts">
type TemplateItem<T> = T extends readonly (infer Item)[] ? Item : T extends Record<string, infer Value> ? Value : T extends number ? number : never;
import { computed, inject } from 'vue';
import { Activity, Circle, CircleDot, Code2, Database, Zap } from 'lucide-vue-next';
import { Handle, Position, type NodeProps } from '@vue-flow/core';
import { t } from '../../i18n';
import { layoutConnectionSeats, type ConnectionSeatLayout } from '../../flow/connectionSeats';
import { formatNodeType } from '../../flow/typeDisplay';
import { libraryNameResolverKey } from '../../flow/libraryNameResolver';
import type { FlowNodeData, NodeKind } from '../../flow/types';
const props = defineProps<NodeProps<FlowNodeData>>();
const icons: Record<NodeKind, typeof Activity> = {
  flipflop: Zap,
  script: Code2,
  flowCall: Activity,
  action: Database,
};
const icon = computed(() => icons[props.data.kind]);
const title = computed(() => {
  const evaluatedValue: string = props.data.displayName?.trim() || t(props.data.titleKey);
  return evaluatedValue;
});
const description = computed(() => {
  const evaluatedValue1: string = props.data.description?.trim() || t(props.data.subtitleKey);
  return evaluatedValue1;
});
const libraryNameFor = inject(libraryNameResolverKey, () => undefined);
const libraryName = computed(() => {
  const libraryNameForResult: string | undefined = libraryNameFor(props.data.runtime);
  return libraryNameForResult;
});
const seats = computed(() => {
  const layoutConnectionSeatsResult: ConnectionSeatLayout[] = layoutConnectionSeats(props.data);
  return layoutConnectionSeatsResult;
});
const executionInputSeat = computed(() => {
  const findResult: ConnectionSeatLayout | undefined = seats.value.find((seat) => seat.kind === 'execution-input');
  return findResult;
});
const executionOutputSeats = computed(() => {
  const filterResult: ConnectionSeatLayout[] = seats.value.filter((seat) => seat.kind === 'execution-output');
  return filterResult;
});
const parameterSeats = computed(() => {
  const filterResult1: ConnectionSeatLayout[] = seats.value.filter((seat) => seat.kind === 'parameter-input');
  return filterResult1;
});
const dataOutputSeat = computed(() => {
  const findResult1: ConnectionSeatLayout | undefined = seats.value.find((seat) => seat.kind === 'data-output');
  return findResult1;
});
const returnType = computed(() => {
  const formatNodeTypeResult: string = formatNodeType(props.data.runtime?.returnType);
  return formatNodeTypeResult;
});
function seatClass(seat: ConnectionSeatLayout): string {
  return `seat-${seat.kind}`;
}
function seatLabel(seat: ConnectionSeatLayout | undefined): string {
  const evaluatedValue2: string = seat ? t(seat.labelKey) : '';
  return evaluatedValue2;
}
function isTargetSeat(seat: ConnectionSeatLayout | undefined): boolean {
  return seat?.handleType === 'target';
}
const readArticleClass = computed(() => ([`kind-${props.data.kind}`, { selected: props.selected, 'has-breakpoint': props.data.breakpoint, 'is-debug-paused': props.data.debugPaused }]));
const readHandleClass = computed(() => {
  const evaluatedValue3: string[] = (['flow-handle', seatClass(executionInputSeat.value!)]);
  return evaluatedValue3;
});
const readHandleConnectable = computed(() => {
  const evaluatedValue4: import("@vue-flow/core").HandleConnectable = (isTargetSeat(executionInputSeat.value) ? 'single' : props.connectable);
  return evaluatedValue4;
});
const readTemplateText = computed(() => {
  const evaluatedValue5: string = (t(`node.kind.${props.data.kind}`));
  return evaluatedValue5;
});
const readSpanClass = computed(() => (`status-${props.data.status}`));
const readSpanTitle = computed(() => {
  const evaluatedValue6: string = (t(`status.${props.data.status}`));
  return evaluatedValue6;
});
const readButtonClass = computed(() => ({ active: props.data.breakpoint }));
const readButtonTitle = computed(() => {
  const evaluatedValue7: string = (props.data.breakpoint ? t('debug.removeBreakpoint') : t('debug.addBreakpoint'));
  return evaluatedValue7;
});
const readButtonAriaLabel = computed(() => {
  const evaluatedValue8: string = (props.data.breakpoint ? t('debug.removeBreakpoint') : t('debug.addBreakpoint'));
  return evaluatedValue8;
});
function readHandleItems() {
  return (executionOutputSeats.value);
}
function readHandleStyle(seat: TemplateItem<ReturnType<typeof readHandleItems>>) {
  return ({ top: `${seat.top}px` });
}
function readHandleClass2(seat: TemplateItem<ReturnType<typeof readHandleItems>>) {
  const evaluatedValue9 = (['flow-handle', seatClass(seat), `branch-${seat.branch}`]);
  return evaluatedValue9;
}
const readDivIf = computed(() => (props.data.parameters.length > 0));
function readDivItems() {
  return (props.data.parameters);
}
function readHandleClass3(index: number) {
  const evaluatedValue10 = (['flow-handle', seatClass(parameterSeats.value[index])]);
  return evaluatedValue10;
}
function readHandleConnectable2(index: number) {
  const evaluatedValue11 = (isTargetSeat(parameterSeats.value[index]) ? 'single' : props.connectable);
  return evaluatedValue11;
}
function readSpanTitle2(parameter: TemplateItem<ReturnType<typeof readDivItems>>) {
  const evaluatedValue12 = (parameter.description || parameter.name || t(parameter.nameKey));
  return evaluatedValue12;
}
function readTemplateText2(parameter: TemplateItem<ReturnType<typeof readDivItems>>) {
  const evaluatedValue13 = (parameter.name || t(parameter.nameKey));
  return evaluatedValue13;
}
function readSpanTitle3(parameter: TemplateItem<ReturnType<typeof readDivItems>>) {
  return (parameter.type || parameter.valueKind);
}
function readTemplateText3(parameter: TemplateItem<ReturnType<typeof readDivItems>>) {
  const evaluatedValue14 = (formatNodeType(parameter.type) || parameter.valueKind);
  return evaluatedValue14;
}
function readSpanTitle4(parameter: TemplateItem<ReturnType<typeof readDivItems>>) {
  const evaluatedValue15 = (t(`parameter.${parameter.source}`));
  return evaluatedValue15;
}
function readTemplateText4(parameter: TemplateItem<ReturnType<typeof readDivItems>>) {
  const evaluatedValue16 = (t(`parameter.${parameter.source}`));
  return evaluatedValue16;
}
const readHandleClass4 = computed(() => {
  const evaluatedValue17: string[] = (['flow-handle', seatClass(dataOutputSeat.value!)]);
  return evaluatedValue17;
});
const readHandleConnectable3 = computed(() => {
  const evaluatedValue18: import("@vue-flow/core").HandleConnectable = (isTargetSeat(dataOutputSeat.value) ? 'single' : props.connectable);
  return evaluatedValue18;
});
</script>

<template>
  <article
    class="workflow-node"
    :class="readArticleClass"
    :aria-label="title"
  >
    <div class="workflow-node__header drag-handle">
      <Handle
        v-if="executionInputSeat"
        :id="executionInputSeat.id"
        :type="executionInputSeat.handleType"
        :position="Position.Left"
        :class="readHandleClass"
        :connectable="readHandleConnectable"
        :aria-label="seatLabel(executionInputSeat)"
       />
      <span
        class="workflow-node__icon"
        aria-hidden="true"
      >
        <component
          :is="icon"
          :size="15"
         />
      </span>
      <span class="workflow-node__kind">{{ readTemplateText }}</span>
      <span
        v-if="libraryName"
        class="workflow-node__library"
        :title="libraryName"
      >{{ libraryName }}</span>
      <span
        class="workflow-node__status"
        :class="readSpanClass"
        :title="readSpanTitle"
      ></span>
      <button
        v-if="data.onToggleBreakpoint"
        class="workflow-node__breakpoint"
        type="button"
        :class="readButtonClass"
        :title="readButtonTitle"
        :aria-label="readButtonAriaLabel"
        :aria-pressed="Boolean(data.breakpoint)"
        :disabled="data.breakpointLocked"
        @pointerdown.stop
        @click.stop="data.onToggleBreakpoint(id)"
      >
        <CircleDot
          v-if="data.breakpoint"
          :size="15"
         />
        <Circle
          v-else
          :size="15"
         />
      </button>
    </div>
    <div
      class="workflow-node__branch-handles"
      :aria-label="t('edge.executionBranches')"
    >
      <Handle
        v-for="seat in readHandleItems()"
        :key="seat.id"
        :id="seat.id"
        :type="seat.handleType"
        :position="Position.Right"
        :style="readHandleStyle(seat)"
        :class="readHandleClass2(seat)"
        :connectable="connectable"
        :aria-label="seatLabel(seat)"
       />
    </div>
    <div class="workflow-node__content">
      <strong>{{ title }}</strong>
      <span>{{ description }}</span>
    </div>
    <div
      v-if="readDivIf"
      class="workflow-node__parameters"
    >
      <div
        v-for="(parameter, index) in readDivItems()"
        :key="parameter.id"
        class="workflow-node__parameter"
      >
        <Handle
          v-if="parameterSeats[index]"
          :id="parameterSeats[index].id"
          :type="parameterSeats[index].handleType"
          :position="Position.Left"
          :class="readHandleClass3(index)"
          :connectable="readHandleConnectable2(index)"
          :aria-label="seatLabel(parameterSeats[index])"
         />
        <span
          class="workflow-node__seat-dot parameter-seat-dot"
          aria-hidden="true"
        ></span>
        <span
          class="workflow-node__parameter-name"
          :title="readSpanTitle2(parameter)"
        >{{ readTemplateText2(parameter) }}</span>
        <span
          class="workflow-node__parameter-type"
          :title="readSpanTitle3(parameter)"
        >{{ readTemplateText3(parameter) }}</span>
        <span
          class="workflow-node__parameter-source"
          :title="readSpanTitle4(parameter)"
        >{{ readTemplateText4(parameter) }}</span>
      </div>
    </div>
    <div class="workflow-node__footer">
      <Handle
        v-if="dataOutputSeat"
        :id="dataOutputSeat.id"
        :type="dataOutputSeat.handleType"
        :position="Position.Right"
        :class="readHandleClass4"
        :connectable="readHandleConnectable3"
        :aria-label="seatLabel(dataOutputSeat)"
       />
      <span
        v-if="returnType"
        class="workflow-node__return-type"
        :title="data.runtime?.returnType"
      >→ {{ returnType }}</span>
    </div>
  </article>
</template>
