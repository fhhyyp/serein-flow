<script setup lang="ts">
type TemplateItem<T> = T extends readonly (infer Item)[] ? Item : T extends Record<string, infer Value> ? Value : T extends number ? number : never;
import { computed } from 'vue';
import { Code2, Database, GitBranch, LockKeyhole, Workflow } from 'lucide-vue-next';
import { t } from '../../i18n';
import { formatNodeType } from '../../flow/typeDisplay';
import type { FlowNode, NodeStatus } from '../../flow/types';
const props = defineProps<{
  node?: FlowNode;
  status: NodeStatus;
}>();
const nodeTitle = computed(() => {
  const node = props.node;
  const evaluatedValue: string = node?.data.displayName?.trim() || (node ? t(node.data.titleKey) : '');
  return evaluatedValue;
});
const nodeDescription = computed(() => {
  const node = props.node;
  const evaluatedValue1: string = node?.data.description?.trim() || (node ? t(node.data.subtitleKey) : '');
  return evaluatedValue1;
});
const runtimeEntries = computed(() => {
  const runtime = props.node?.data.runtime;
  if (!runtime) {
    return [];
  }
  const filterResult = [runtime.className, runtime.methodName].filter(Boolean);
  const member = filterResult.join('.');
  const entries: Array<{
    label: string;
    value: string;
    mono?: boolean;
  }> = [];
  if (runtime.dllName) {
    const resolveJoinResult = () => {
      const filterResult1 = [runtime.dllName, runtime.dllVersion].filter(Boolean);
      const joinResult1: string = filterResult1.join(' · ');
      return joinResult1;
    };
    const evaluatedValue2: {
      label: string;
      value: string;
      mono?: boolean;
    } = { label: t('console.snapshotLibrary'), value: resolveJoinResult(), mono: true };
    entries.push(evaluatedValue2);
  }
  if (member) {
    const evaluatedValue3: {
      label: string;
      value: string;
      mono?: boolean;
    } = { label: t('console.snapshotMember'), value: member, mono: true };
    entries.push(evaluatedValue3);
  }
  if (runtime.returnType) {
    const evaluatedValue4: {
      label: string;
      value: string;
      mono?: boolean;
    } = { label: t('console.snapshotReturnType'), value: formatNodeType(runtime.returnType), mono: true };
    entries.push(evaluatedValue4);
  }
  if (runtime.isPublic !== undefined) {
    const evaluatedValue5: {
      label: string;
      value: string;
      mono?: boolean;
    } = { label: t('console.snapshotPublicEntry'), value: runtime.isPublic ? t('console.snapshotYes') : t('console.snapshotNo') };
    entries.push(evaluatedValue5);
  }
  if (runtime.targetCanvasId) {
    const evaluatedValue6: {
      label: string;
      value: string;
      mono?: boolean;
    } = { label: t('console.snapshotTargetCanvas'), value: runtime.targetCanvasId, mono: true };
    entries.push(evaluatedValue6);
  }
  if (runtime.targetNodeId) {
    const evaluatedValue7: {
      label: string;
      value: string;
      mono?: boolean;
    } = { label: t('console.snapshotTargetNode'), value: runtime.targetNodeId, mono: true };
    entries.push(evaluatedValue7);
  }
  if (runtime.staticReturnType) {
    const evaluatedValue8: {
      label: string;
      value: string;
      mono?: boolean;
    } = { label: t('console.snapshotReturnModel'), value: formatNodeType(runtime.staticReturnType), mono: true };
    entries.push(evaluatedValue8);
  }
  else if (runtime.isDynamicReturnType) {
    const evaluatedValue9: {
      label: string;
      value: string;
      mono?: boolean;
    } = { label: t('console.snapshotReturnModel'), value: t('console.snapshotDynamicReturn') };
    entries.push(evaluatedValue9);
  }
  return entries;
});
function parameterName(parameter: FlowNode['data']['parameters'][number]): string {
  const evaluatedValue10: string = parameter.name?.trim() || t(parameter.nameKey);
  return evaluatedValue10;
}
function parameterSourceDetail(parameter: FlowNode['data']['parameters'][number]): string | undefined {
  if (parameter.source === 'literal') {
    return parameter.literalValue;
  }
  if (parameter.source === 'projectInput') {
    return parameter.projectInputKey;
  }
  if (parameter.source === 'expression') {
    return parameter.expression;
  }
  if (parameter.source === 'previousNode' && parameter.sourceNodeId) {
    return `${parameter.sourceNodeId}.${parameter.sourcePortId || 'data-out'}`;
  }
  return undefined;
}
function parameterSourceLabel(parameter: FlowNode['data']['parameters'][number]): string {
  const tResult: string = t(`parameter.${parameter.source}`);
  return tResult;
}
function enumMembers(parameter: FlowNode['data']['parameters'][number]): string | undefined {
  if (!parameter.enumMetadata || parameter.source !== 'literal') {
    return undefined;
  }
  const literalMembers = parameter.literalValue?.split(',');
  const trimmedMembers = literalMembers?.map((item) => {
    const trimResult: string = item.trim();
    return trimResult;
  });
  const selected = trimmedMembers?.filter(Boolean) ?? [];
  if (!selected.length) {
    return undefined;
  }
  const mapResult = selected
    .map((name) => {
    const option = parameter.enumMetadata?.options.find((candidate) => candidate.name === name);
    return option ? `${option.name} (${option.numericValue})` : name;
  });
  const joinResult: string | undefined = mapResult.join(', ');
  return joinResult;
}
const readSpanClass = computed(() => (['run-snapshot-node-inspector__status', `status-${props.status}`]));
const readTemplateText = computed(() => {
  const evaluatedValue11: string = (t(`status.${props.status}`));
  return evaluatedValue11;
});
const readDatabaseIf = computed(() => (props.node!.data.kind === 'action'));
const readGitBranchElseIf = computed(() => (props.node!.data.kind === 'flipflop'));
const readCode2ElseIf = computed(() => (props.node!.data.kind === 'script'));
const readTemplateText2 = computed(() => {
  const evaluatedValue12: {
    [x: string]: string | number;
  } | undefined = { kind: t(`node.kind.${props.node!.data.kind}`) };
  const evaluatedValue13: string = (t('inspector.nodeType', evaluatedValue12));
  return evaluatedValue13;
});
function readDivItems() {
  return (runtimeEntries.value);
}
function readDdClass(entry: TemplateItem<ReturnType<typeof readDivItems>>) {
  return ({ mono: entry.mono });
}
const readSectionIf = computed(() => (props.node!.data.kind === 'script' && props.node!.data.script));
function readLiItems() {
  return (props.node!.data.script!.inputs);
}
function readLiKey(input: TemplateItem<ReturnType<typeof readLiItems>>) {
  return (input.id || input.name);
}
function readTemplateText3(input: TemplateItem<ReturnType<typeof readLiItems>>) {
  const evaluatedValue14 = (formatNodeType(input.valueKind) || input.valueKind);
  return evaluatedValue14;
}
function readLiItems2() {
  return (props.node!.data.script!.outputs);
}
function readLiKey2(output: TemplateItem<ReturnType<typeof readLiItems2>>) {
  return (output.id || output.name);
}
function readTemplateText4(output: TemplateItem<ReturnType<typeof readLiItems2>>) {
  const evaluatedValue15 = (formatNodeType(output.valueKind) || output.valueKind);
  return evaluatedValue15;
}
const readTemplateText5 = computed(() => {
  const evaluatedValue16: string = (props.node!.data.kind === 'script' ? t('inspector.scriptInputs') : t('inspector.parameters'));
  return evaluatedValue16;
});
function readLiItems3() {
  return (props.node!.data.parameters);
}
function readTemplateText6(parameter: TemplateItem<ReturnType<typeof readLiItems3>>) {
  const evaluatedValue17 = (formatNodeType(parameter.type) || parameter.valueKind);
  return evaluatedValue17;
}
function readTemplateText7(parameter: TemplateItem<ReturnType<typeof readLiItems3>>) {
  const evaluatedValue18 = (parameter.required ? t('console.snapshotYes') : t('console.snapshotNo'));
  return evaluatedValue18;
}
function readTemplateText8(parameter: TemplateItem<ReturnType<typeof readLiItems3>>) {
  const evaluatedValue19 = (formatNodeType(parameter.enumMetadata!.typeName) || parameter.enumMetadata!.typeName);
  return evaluatedValue19;
}
function readDivIf(parameter: TemplateItem<ReturnType<typeof readLiItems3>>) {
  const evaluatedValue20 = (parameterSourceDetail(parameter) !== undefined);
  return evaluatedValue20;
}
</script>

<template>
  <aside
    class="run-snapshot-node-inspector"
    :aria-label="t('console.snapshotProperties')"
  >
    <template v-if="node">
      <header class="run-snapshot-node-inspector__header">
        <div>
          <p>{{ t('console.snapshotNodeConfiguration') }}</p>
          <h3>{{ nodeTitle }}</h3>
        </div>
        <span :class="readSpanClass">{{ readTemplateText }}</span>
      </header>
      <div class="run-snapshot-node-inspector__body">
        <section class="snapshot-inspector-section snapshot-inspector-section--identity">
          <div class="snapshot-node-type">
            <Database
              v-if="readDatabaseIf"
              :size="15"
             />
            <GitBranch
              v-else-if="readGitBranchElseIf"
              :size="15"
             />
            <Code2
              v-else-if="readCode2ElseIf"
              :size="15"
             />
            <Workflow
              v-else
              :size="15"
             />
            <span>{{ readTemplateText2 }}</span>
          </div>
          <dl class="snapshot-property-list snapshot-property-list--identity">
            <div>
              <dt>{{ t('console.snapshotNodeId') }}</dt>
              <dd class="mono">{{ node.id }}</dd>
            </div>
            <div>
              <dt>{{ t('inspector.description') }}</dt>
              <dd>{{ nodeDescription }}</dd>
            </div>
          </dl>
        </section>
        <section
          v-if="runtimeEntries.length"
          class="snapshot-inspector-section"
        >
          <h4>{{ t('console.snapshotRuntimeMetadata') }}</h4>
          <dl class="snapshot-property-list">
            <div
              v-for="entry in readDivItems()"
              :key="entry.label"
            >
              <dt>{{ entry.label }}</dt>
              <dd :class="readDdClass(entry)">{{ entry.value }}</dd>
            </div>
          </dl>
        </section>
        <section
          v-if="readSectionIf"
          class="snapshot-inspector-section"
        >
          <h4>{{ t('console.snapshotScript') }}</h4>
          <dl class="snapshot-property-list">
            <div>
              <dt>{{ t('console.snapshotLanguageVersion') }}</dt>
              <dd class="mono">{{ node.data.script!.languageVersion }}</dd>
            </div>
            <div>
              <dt>{{ t('console.snapshotSourceHash') }}</dt>
              <dd class="mono snapshot-property-list__hash">{{ node.data.script!.sourceHash }}</dd>
            </div>
          </dl>
          <div class="snapshot-code-block">
            <span>{{ t('console.snapshotScriptSource') }}</span>
            <pre>{{ node.data.script!.source }}</pre>
          </div>
          <div class="snapshot-contract-grid">
            <section>
              <h5>{{ t('console.snapshotScriptInputs') }}</h5>
              <ul v-if="node.data.script!.inputs.length">
                <li
                  v-for="input in readLiItems()"
                  :key="readLiKey(input)"
                >
                  <strong>{{ input.name }}</strong>
                  <span>{{ readTemplateText3(input) }}</span>
                  <small v-if="input.description">{{ input.description }}</small>
                </li>
              </ul>
              <p v-else>{{ t('console.snapshotNone') }}</p>
            </section>
            <section>
              <h5>{{ t('console.snapshotScriptOutputs') }}</h5>
              <ul v-if="node.data.script!.outputs.length">
                <li
                  v-for="output in readLiItems2()"
                  :key="readLiKey2(output)"
                >
                  <strong>{{ output.name }}</strong>
                  <span>{{ readTemplateText4(output) }}</span>
                  <small v-if="output.description">{{ output.description }}</small>
                </li>
              </ul>
              <p v-else>{{ t('console.snapshotNone') }}</p>
            </section>
          </div>
        </section>
        <section class="snapshot-inspector-section">
          <h4>{{ readTemplateText5 }}</h4>
          <ol
            v-if="node.data.parameters.length"
            class="snapshot-parameter-list"
          >
            <li
              v-for="parameter in readLiItems3()"
              :key="parameter.id"
            >
              <div class="snapshot-parameter-list__heading">
                <strong>{{ parameterName(parameter) }}</strong>
                <span>{{ readTemplateText6(parameter) }}</span>
              </div>
              <dl>
                <div>
                  <dt>{{ t('console.snapshotParameterId') }}</dt>
                  <dd class="mono">{{ parameter.id }}</dd>
                </div>
                <div>
                  <dt>{{ t('parameter.source') }}</dt>
                  <dd>{{ parameterSourceLabel(parameter) }}</dd>
                </div>
                <div>
                  <dt>{{ t('inspector.required') }}</dt>
                  <dd>{{ readTemplateText7(parameter) }}</dd>
                </div>
                <div v-if="parameter.enumMetadata">
                  <dt>{{ t('console.snapshotEnumType') }}</dt>
                  <dd class="mono">{{ readTemplateText8(parameter) }}</dd>
                </div>
                <div v-if="enumMembers(parameter)">
                  <dt>{{ t('console.snapshotEnumMembers') }}</dt>
                  <dd>{{ enumMembers(parameter) }}</dd>
                </div>
                <div v-if="readDivIf(parameter)">
                  <dt>{{ t('console.snapshotConfiguredValue') }}</dt>
                  <dd class="mono snapshot-parameter-list__source">{{ parameterSourceDetail(parameter) }}</dd>
                </div>
              </dl>
              <p
                v-if="parameter.description"
                class="snapshot-parameter-list__description"
              >{{ parameter.description }}</p>
            </li>
          </ol>
          <p
            v-else
            class="snapshot-inspector-empty"
          >{{ t('console.snapshotNoParameters') }}</p>
        </section>
      </div>
    </template>
    <div
      v-else
      class="run-snapshot-node-inspector__empty"
    >
      <LockKeyhole :size="17" />
      <p>{{ t('console.snapshotNodeSelect') }}</p>
    </div>
  </aside>
</template>
