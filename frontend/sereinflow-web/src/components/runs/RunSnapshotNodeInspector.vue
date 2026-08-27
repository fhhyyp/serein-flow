<script setup lang="ts">
import { computed } from 'vue'
import { Code2, Database, GitBranch, LockKeyhole, Workflow } from 'lucide-vue-next'
import { t } from '../../i18n'
import { formatNodeType } from '../../flow/typeDisplay'
import type { FlowNode, NodeStatus } from '../../flow/types'

const props = defineProps<{
  node?: FlowNode
  status: NodeStatus
}>()

const nodeTitle = computed(() => {
  const node = props.node
  return node?.data.displayName?.trim() || (node ? t(node.data.titleKey) : '')
})

const nodeDescription = computed(() => {
  const node = props.node
  return node?.data.description?.trim() || (node ? t(node.data.subtitleKey) : '')
})

const runtimeEntries = computed(() => {
  const runtime = props.node?.data.runtime
  if (!runtime) return []

  const member = [runtime.className, runtime.methodName].filter(Boolean).join('.')
  const entries: Array<{ label: string; value: string; mono?: boolean }> = []
  if (runtime.dllName) entries.push({ label: t('console.snapshotLibrary'), value: [runtime.dllName, runtime.dllVersion].filter(Boolean).join(' · '), mono: true })
  if (member) entries.push({ label: t('console.snapshotMember'), value: member, mono: true })
  if (runtime.returnType) entries.push({ label: t('console.snapshotReturnType'), value: formatNodeType(runtime.returnType), mono: true })
  if (runtime.isPublic !== undefined) entries.push({ label: t('console.snapshotPublicEntry'), value: runtime.isPublic ? t('console.snapshotYes') : t('console.snapshotNo') })
  if (runtime.targetCanvasId) entries.push({ label: t('console.snapshotTargetCanvas'), value: runtime.targetCanvasId, mono: true })
  if (runtime.targetNodeId) entries.push({ label: t('console.snapshotTargetNode'), value: runtime.targetNodeId, mono: true })
  if (runtime.staticReturnType) entries.push({ label: t('console.snapshotReturnModel'), value: formatNodeType(runtime.staticReturnType), mono: true })
  else if (runtime.isDynamicReturnType) entries.push({ label: t('console.snapshotReturnModel'), value: t('console.snapshotDynamicReturn') })
  return entries
})

function parameterName(parameter: FlowNode['data']['parameters'][number]): string {
  return parameter.name?.trim() || t(parameter.nameKey)
}

function parameterSourceDetail(parameter: FlowNode['data']['parameters'][number]): string | undefined {
  if (parameter.source === 'literal') return parameter.literalValue
  if (parameter.source === 'projectInput') return parameter.projectInputKey
  if (parameter.source === 'expression') return parameter.expression
  if (parameter.source === 'previousNode' && parameter.sourceNodeId) {
    return `${parameter.sourceNodeId}.${parameter.sourcePortId || 'data-out'}`
  }
  return undefined
}

function parameterSourceLabel(parameter: FlowNode['data']['parameters'][number]): string {
  return t(`parameter.${parameter.source}`)
}
</script>

<template>
  <aside class="run-snapshot-node-inspector" :aria-label="t('console.snapshotProperties')">
    <template v-if="node">
      <header class="run-snapshot-node-inspector__header">
        <div>
          <p>{{ t('console.snapshotNodeConfiguration') }}</p>
          <h3>{{ nodeTitle }}</h3>
        </div>
        <span :class="['run-snapshot-node-inspector__status', `status-${status}`]">{{ t(`status.${status}`) }}</span>
      </header>

      <div class="run-snapshot-node-inspector__body">
        <section class="snapshot-inspector-section snapshot-inspector-section--identity">
          <div class="snapshot-node-type">
            <Database v-if="node.data.kind === 'action'" :size="15" />
            <GitBranch v-else-if="node.data.kind === 'flipflop'" :size="15" />
            <Code2 v-else-if="node.data.kind === 'script'" :size="15" />
            <Workflow v-else :size="15" />
            <span>{{ t('inspector.nodeType', { kind: t(`node.kind.${node.data.kind}`) }) }}</span>
          </div>
          <dl class="snapshot-property-list snapshot-property-list--identity">
            <div><dt>{{ t('console.snapshotNodeId') }}</dt><dd class="mono">{{ node.id }}</dd></div>
            <div><dt>{{ t('inspector.description') }}</dt><dd>{{ nodeDescription }}</dd></div>
          </dl>
        </section>

        <section v-if="runtimeEntries.length" class="snapshot-inspector-section">
          <h4>{{ t('console.snapshotRuntimeMetadata') }}</h4>
          <dl class="snapshot-property-list">
            <div v-for="entry in runtimeEntries" :key="entry.label"><dt>{{ entry.label }}</dt><dd :class="{ mono: entry.mono }">{{ entry.value }}</dd></div>
          </dl>
        </section>

        <section v-if="node.data.kind === 'script' && node.data.script" class="snapshot-inspector-section">
          <h4>{{ t('console.snapshotScript') }}</h4>
          <dl class="snapshot-property-list">
            <div><dt>{{ t('console.snapshotLanguageVersion') }}</dt><dd class="mono">{{ node.data.script.languageVersion }}</dd></div>
            <div><dt>{{ t('console.snapshotSourceHash') }}</dt><dd class="mono snapshot-property-list__hash">{{ node.data.script.sourceHash }}</dd></div>
          </dl>
          <div class="snapshot-code-block"><span>{{ t('console.snapshotScriptSource') }}</span><pre>{{ node.data.script.source }}</pre></div>
          <div class="snapshot-contract-grid">
            <section><h5>{{ t('console.snapshotScriptInputs') }}</h5><ul v-if="node.data.script.inputs.length"><li v-for="input in node.data.script.inputs" :key="input.id || input.name"><strong>{{ input.name }}</strong><span>{{ formatNodeType(input.valueKind) || input.valueKind }}</span><small v-if="input.description">{{ input.description }}</small></li></ul><p v-else>{{ t('console.snapshotNone') }}</p></section>
            <section><h5>{{ t('console.snapshotScriptOutputs') }}</h5><ul v-if="node.data.script.outputs.length"><li v-for="output in node.data.script.outputs" :key="output.id || output.name"><strong>{{ output.name }}</strong><span>{{ formatNodeType(output.valueKind) || output.valueKind }}</span><small v-if="output.description">{{ output.description }}</small></li></ul><p v-else>{{ t('console.snapshotNone') }}</p></section>
          </div>
        </section>

        <section class="snapshot-inspector-section">
          <h4>{{ node.data.kind === 'script' ? t('inspector.scriptInputs') : t('inspector.parameters') }}</h4>
          <ol v-if="node.data.parameters.length" class="snapshot-parameter-list">
            <li v-for="parameter in node.data.parameters" :key="parameter.id">
              <div class="snapshot-parameter-list__heading"><strong>{{ parameterName(parameter) }}</strong><span>{{ formatNodeType(parameter.type) || parameter.valueKind }}</span></div>
              <dl>
                <div><dt>{{ t('console.snapshotParameterId') }}</dt><dd class="mono">{{ parameter.id }}</dd></div>
                <div><dt>{{ t('parameter.source') }}</dt><dd>{{ parameterSourceLabel(parameter) }}</dd></div>
                <div><dt>{{ t('inspector.required') }}</dt><dd>{{ parameter.required ? t('console.snapshotYes') : t('console.snapshotNo') }}</dd></div>
                <div v-if="parameterSourceDetail(parameter) !== undefined"><dt>{{ t('console.snapshotConfiguredValue') }}</dt><dd class="mono snapshot-parameter-list__source">{{ parameterSourceDetail(parameter) }}</dd></div>
              </dl>
              <p v-if="parameter.description" class="snapshot-parameter-list__description">{{ parameter.description }}</p>
            </li>
          </ol>
          <p v-else class="snapshot-inspector-empty">{{ t('console.snapshotNoParameters') }}</p>
        </section>
      </div>
    </template>

    <div v-else class="run-snapshot-node-inspector__empty"><LockKeyhole :size="17" /><p>{{ t('console.snapshotNodeSelect') }}</p></div>
  </aside>
</template>
