<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { ArrowRight, Check, CircleAlert, ClipboardCheck, RefreshCw, X } from 'lucide-vue-next'
import {
  applyLibraryUpgrade,
  applyLibraryUpgradeBatch,
  previewLibraryUpgrade,
  type FlowLibraryUpgradePreviewDto,
  type LibraryDto,
  type LibraryUpgradeBatchApplyResultDto,
  type LibraryUpgradeIssueDto,
  type LibraryUpgradePlanDto,
} from '../../api/libraryApi'
import type { FlowDefinitionSummaryDto } from '../../api/flowApi'
import { t } from '../../i18n'

const props = defineProps<{
  projectId: string
  projectName: string
  source: LibraryDto
  targets: LibraryDto[]
  flows: FlowDefinitionSummaryDto[]
}>()

const emit = defineEmits<{
  close: []
  applied: []
}>()

const targetArtifactId = ref(props.targets[0]?.id ?? '')
const selectedFlowIds = ref<Set<string>>(new Set(props.flows.slice(0, 1).map((flow) => flow.id)))
const activeFlowId = ref(props.flows[0]?.id ?? '')
const plan = ref<LibraryUpgradePlanDto>()
const applyResults = ref<LibraryUpgradeBatchApplyResultDto>()
const error = ref('')
const notice = ref('')
const isPreviewing = ref(false)
const isApplying = ref(false)
const acknowledgedIssueIds = ref<Set<string>>(new Set())

const selectedTarget = computed(() => props.targets.find((item) => item.id === targetArtifactId.value))
const selectedFlowCount = computed(() => selectedFlowIds.value.size)
const selectedPreviews = computed(() => plan.value?.flows.filter((item) => selectedFlowIds.value.has(item.flowId)) ?? [])
const activePreview = computed<FlowLibraryUpgradePreviewDto | undefined>(() =>
  plan.value?.flows.find((item) => item.flowId === activeFlowId.value))
const acknowledgementIssues = computed(() =>
  selectedPreviews.value.flatMap((preview) => preview.issues.filter((item) => item.requiresAcknowledgement)))
const hasBlockingIssues = computed(() =>
  selectedPreviews.value.length !== selectedFlowCount.value
  || selectedPreviews.value.some((preview) => !preview.canApply || preview.issues.some((item) => item.blocksApplication)))
const canApply = computed(() =>
  Boolean(plan.value && !applyResults.value && selectedPreviews.value.length > 0 && !hasBlockingIssues.value && acknowledgementIssues.value.every((item) => acknowledgedIssueIds.value.has(item.id))))

function shortHash(value: string): string {
  return value.length <= 10 ? value : value.slice(0, 10)
}

function classificationLabel(value: LibraryUpgradeIssueDto['classification']): string {
  return t(`libraryUpgrade.classification.${value}`)
}

function resetPreview(): void {
  plan.value = undefined
  applyResults.value = undefined
  error.value = ''
  notice.value = ''
  acknowledgedIssueIds.value = new Set()
}

function setFlowSelected(flowId: string, selected: boolean): void {
  const next = new Set(selectedFlowIds.value)
  if (selected) {
    next.add(flowId)
    activeFlowId.value = flowId
  } else {
    next.delete(flowId)
    if (activeFlowId.value === flowId) activeFlowId.value = [...next][0] ?? ''
  }
  selectedFlowIds.value = next
  resetPreview()
}

function toggleAcknowledgement(issueId: string, checked: boolean): void {
  const next = new Set(acknowledgedIssueIds.value)
  if (checked) next.add(issueId)
  else next.delete(issueId)
  acknowledgedIssueIds.value = next
}

function handleAcknowledgementChange(issueId: string, event: Event): void {
  toggleAcknowledgement(issueId, (event.target as HTMLInputElement).checked)
}

async function createPreview(): Promise<void> {
  if (!targetArtifactId.value || selectedFlowIds.value.size === 0) {
    error.value = t('libraryUpgrade.selectionRequired')
    return
  }

  isPreviewing.value = true
  error.value = ''
  notice.value = ''
  acknowledgedIssueIds.value = new Set()
  try {
    plan.value = await previewLibraryUpgrade(props.projectId, {
      sourceArtifactId: props.source.id,
      targetArtifactId: targetArtifactId.value,
      flowIds: [...selectedFlowIds.value],
    })
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : t('libraryUpgrade.previewFailed')
  } finally {
    isPreviewing.value = false
  }
}

async function apply(): Promise<void> {
  if (!plan.value || !canApply.value) return

  isApplying.value = true
  error.value = ''
  notice.value = ''
  try {
    const requests = selectedPreviews.value.map((preview) => ({
      flowId: preview.flowId,
      expectedFlowVersion: preview.flowVersion,
      acknowledgedItemIds: [...acknowledgedIssueIds.value],
    }))
    if (requests.length === 1) {
      const result = await applyLibraryUpgrade(props.projectId, plan.value.id, requests[0])
      applyResults.value = { planId: plan.value.id, succeeded: [result], failed: [] }
    } else {
      applyResults.value = await applyLibraryUpgradeBatch(props.projectId, plan.value.id, { flows: requests })
    }

    const result = applyResults.value
    if (result.failed.length > 0) {
      notice.value = t('libraryUpgrade.batchPartial', { succeeded: result.succeeded.length, failed: result.failed.length })
    } else if (result.succeeded.length === 1) {
      notice.value = t('libraryUpgrade.applied', { version: result.succeeded[0].newVersion })
    } else {
      notice.value = t('libraryUpgrade.appliedBatch', { count: result.succeeded.length })
    }
    if (result.succeeded.length > 0) emit('applied')
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : t('libraryUpgrade.applyFailed')
  } finally {
    isApplying.value = false
  }
}

watch([targetArtifactId, selectedFlowIds], resetPreview)
watch(() => props.targets, (targets) => {
  if (!targets.some((item) => item.id === targetArtifactId.value)) {
    targetArtifactId.value = targets[0]?.id ?? ''
  }
})
watch(() => props.flows, (flows) => {
  const validIds = new Set(flows.map((item) => item.id))
  const next = new Set([...selectedFlowIds.value].filter((id) => validIds.has(id)))
  if (next.size === 0 && flows[0]) next.add(flows[0].id)
  selectedFlowIds.value = next
  if (!validIds.has(activeFlowId.value)) activeFlowId.value = [...next][0] ?? ''
})
</script>

<template>
  <div class="library-upgrade-dialog-backdrop" role="presentation" @click.self="emit('close')">
    <section class="library-upgrade-dialog" role="dialog" aria-modal="true" :aria-label="t('libraryUpgrade.title')">
      <header>
        <div>
          <p class="operations-console__eyebrow">{{ t('libraryUpgrade.eyebrow') }}</p>
          <h2>{{ t('libraryUpgrade.title') }}</h2>
          <span>{{ projectName }}</span>
        </div>
        <button class="icon-button" type="button" :title="t('command.close')" :aria-label="t('command.close')" @click="emit('close')"><X :size="16" /></button>
      </header>

      <div class="library-upgrade-dialog__message-slot" aria-live="polite">
        <p v-if="error" class="library-upgrade-dialog__message library-upgrade-dialog__message--error" role="alert"><CircleAlert :size="15" />{{ error }}</p>
        <p v-else-if="notice" class="library-upgrade-dialog__message library-upgrade-dialog__message--success"><Check :size="15" />{{ notice }}</p>
      </div>

      <div class="library-upgrade-dialog__content">
        <section class="library-upgrade-dialog__selection" :aria-label="t('libraryUpgrade.selectionTitle')">
          <div class="library-upgrade-dialog__heading">
            <div><h3>{{ t('libraryUpgrade.selectionTitle') }}</h3><p>{{ t('libraryUpgrade.selectionHint') }}</p></div>
          </div>
          <dl class="library-upgrade-artifact">
            <div><dt>{{ t('libraryUpgrade.source') }}</dt><dd><strong>{{ source.name }}</strong><span>{{ source.semanticVersion ?? source.version }} · {{ shortHash(source.sha256) }}</span></dd></div>
            <ArrowRight :size="17" aria-hidden="true" />
            <div><dt>{{ t('libraryUpgrade.target') }}</dt><dd>
              <label class="sr-only" for="library-upgrade-target">{{ t('libraryUpgrade.target') }}</label>
              <select id="library-upgrade-target" v-model="targetArtifactId" :disabled="isPreviewing || isApplying || !targets.length">
                <option v-for="target in targets" :key="target.id" :value="target.id">{{ target.name }} · {{ target.semanticVersion ?? target.version }} · {{ shortHash(target.sha256) }}</option>
              </select>
              <span v-if="selectedTarget">{{ selectedTarget.fileName }}</span>
            </dd></div>
          </dl>

          <fieldset class="library-upgrade-flow-picker">
            <legend><span>{{ t('libraryUpgrade.flows') }}</span><small>{{ t('libraryUpgrade.selectedFlowCount', { count: selectedFlowCount }) }}</small></legend>
            <div class="library-upgrade-flow-picker__list">
              <label v-for="flow in flows" :key="flow.id" :class="{ 'is-selected': selectedFlowIds.has(flow.id) }"><input type="checkbox" :checked="selectedFlowIds.has(flow.id)" :disabled="isPreviewing || isApplying" @change="setFlowSelected(flow.id, ($event.target as HTMLInputElement).checked)" /><span><strong>{{ flow.id }}</strong><small>{{ t('libraryUpgrade.flowVersion', { version: flow.version }) }}</small></span></label>
            </div>
          </fieldset>
          <p v-if="!targets.length" class="library-upgrade-dialog__empty">{{ t('libraryUpgrade.noTargets') }}</p>
          <p v-else-if="!flows.length" class="library-upgrade-dialog__empty">{{ t('libraryUpgrade.noFlows') }}</p>
          <button class="command-button run" type="button" :disabled="isPreviewing || isApplying || !targets.length || !flows.length" @click="createPreview"><RefreshCw :size="15" :class="{ 'is-spinning': isPreviewing }" /><span>{{ t('libraryUpgrade.createPreview') }}</span></button>
        </section>

        <section class="library-upgrade-dialog__report" :aria-label="t('libraryUpgrade.reportTitle')">
          <div class="library-upgrade-dialog__heading"><div><h3>{{ t('libraryUpgrade.reportTitle') }}</h3><p>{{ t('libraryUpgrade.reportHint') }}</p></div><span v-if="activePreview" class="library-upgrade-report__count">{{ activePreview.affectedNodeCount }}</span></div>
          <p v-if="!plan" class="library-upgrade-dialog__empty">{{ t('libraryUpgrade.reportEmpty') }}</p>
          <template v-else-if="activePreview">
            <label class="library-upgrade-field" for="library-upgrade-report-flow"><span>{{ t('libraryUpgrade.reportFlow') }}</span><select id="library-upgrade-report-flow" v-model="activeFlowId" :disabled="isApplying"><option v-for="preview in selectedPreviews" :key="preview.flowId" :value="preview.flowId">{{ preview.flowId }} · {{ t('libraryUpgrade.flowVersion', { version: preview.flowVersion }) }}</option></select></label>
            <div class="library-upgrade-summary">
              <span :class="['library-upgrade-status', activePreview.canApply ? 'library-upgrade-status--ready' : 'library-upgrade-status--blocked']">{{ activePreview.canApply ? t('libraryUpgrade.ready') : t('libraryUpgrade.blocked') }}</span>
              <span>{{ t('libraryUpgrade.issueCount', { count: activePreview.issues.length }) }}</span>
            </div>
            <div class="library-upgrade-table-wrap">
              <table class="library-upgrade-table">
                <thead><tr><th>{{ t('libraryUpgrade.classification') }}</th><th>{{ t('libraryUpgrade.scope') }}</th><th>{{ t('libraryUpgrade.diagnostic') }}</th><th>{{ t('libraryUpgrade.confirm') }}</th></tr></thead>
                <tbody>
                  <tr v-for="issue in activePreview.issues" :key="issue.id">
                    <td><span :class="['library-upgrade-classification', `library-upgrade-classification--${issue.classification}`]">{{ classificationLabel(issue.classification) }}</span></td>
                    <td><code>{{ issue.nodeId ?? t('libraryUpgrade.flowScope') }}</code><small v-if="issue.sourceParameterId">{{ issue.sourceParameterId }}<template v-if="issue.targetParameterId"> → {{ issue.targetParameterId }}</template></small></td>
                    <td><strong>{{ issue.code }}</strong><span>{{ issue.message }}</span></td>
                    <td><label v-if="issue.requiresAcknowledgement" class="library-upgrade-confirm"><input type="checkbox" :checked="acknowledgedIssueIds.has(issue.id)" :disabled="isApplying" @change="handleAcknowledgementChange(issue.id, $event)" /><span>{{ t('libraryUpgrade.acknowledge') }}</span></label><span v-else class="library-upgrade-no-confirm">{{ issue.blocksApplication ? t('libraryUpgrade.manualRequired') : '—' }}</span></td>
                  </tr>
                </tbody>
              </table>
            </div>
            <p v-if="hasBlockingIssues" class="library-upgrade-dialog__warning"><CircleAlert :size="15" />{{ t('libraryUpgrade.blockedHint') }}</p>
            <p v-else-if="acknowledgementIssues.length && !canApply" class="library-upgrade-dialog__warning"><ClipboardCheck :size="15" />{{ t('libraryUpgrade.acknowledgementHint') }}</p>
            <ul v-if="applyResults" class="library-upgrade-results" :aria-label="t('libraryUpgrade.resultsTitle')"><li v-for="result in applyResults.succeeded" :key="result.flowId" class="library-upgrade-results__success"><Check :size="14" /><span><strong>{{ result.flowId }}</strong>{{ t('libraryUpgrade.resultSuccess', { version: result.newVersion }) }}</span></li><li v-for="failure in applyResults.failed" :key="failure.flowId" class="library-upgrade-results__failure"><CircleAlert :size="14" /><span><strong>{{ failure.flowId }}</strong>{{ t('libraryUpgrade.resultFailure', { code: failure.code ?? 'unknown' }) }}<small>{{ failure.message }}</small></span></li></ul>
          </template>
        </section>
      </div>

      <footer>
        <button class="command-button quiet" type="button" :disabled="isPreviewing || isApplying" @click="emit('close')">{{ t('command.cancel') }}</button>
        <button class="command-button run" type="button" :disabled="!canApply || isApplying" @click="apply"><Check :size="15" :class="{ 'is-spinning': isApplying }" /><span>{{ selectedFlowCount > 1 ? t('libraryUpgrade.applyBatch') : t('libraryUpgrade.apply') }}</span></button>
      </footer>
    </section>
  </div>
</template>
