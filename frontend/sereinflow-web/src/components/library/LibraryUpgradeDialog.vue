<script setup lang="ts">
type TemplateItem<T> = T extends readonly (infer Item)[] ? Item : T extends Record<string, infer Value> ? Value : T extends number ? number : never;
import { computed, ref, watch } from 'vue';
import { ArrowRight, Check, CircleAlert, ClipboardCheck, RefreshCw, X } from 'lucide-vue-next';
import { applyLibraryUpgrade, applyLibraryUpgradeBatch, previewLibraryUpgrade, type FlowLibraryUpgradePreviewDto, type LibraryDto, type LibraryUpgradeBatchApplyResultDto, type LibraryUpgradeIssueDto, type LibraryUpgradePlanDto, } from '../../api/libraryApi';
import type { FlowDefinitionSummaryDto } from '../../api/flowApi';
import { t } from '../../i18n';
const props = defineProps<{
  projectId: string;
  projectName: string;
  source: LibraryDto;
  targets: LibraryDto[];
  flows: FlowDefinitionSummaryDto[];
}>();
const emit = defineEmits<{
  close: [
  ];
  applied: [
  ];
}>();
const targetArtifactId = ref(props.targets[0]?.id ?? '');
const sliceResult = props.flows.slice(0, 1);
const mapResult1: Iterable<string> | null | undefined = sliceResult.map((flow) => flow.id);
const setInstance: Set<string> = new Set(mapResult1);
const selectedFlowIds = ref<Set<string>>(setInstance);
const activeFlowId = ref(props.flows[0]?.id ?? '');
const plan = ref<LibraryUpgradePlanDto>();
const applyResults = ref<LibraryUpgradeBatchApplyResultDto>();
const error = ref('');
const notice = ref('');
const isPreviewing = ref(false);
const isApplying = ref(false);
const setInstance1: Set<string> = new Set();
const acknowledgedIssueIds = ref<Set<string>>(setInstance1);
const selectedTarget = computed(() => {
  const findResult: LibraryDto | undefined = props.targets.find((item) => item.id === targetArtifactId.value);
  return findResult;
});
const selectedFlowCount = computed(() => selectedFlowIds.value.size);
const selectedPreviews = computed(() => {
  const evaluatedValue: FlowLibraryUpgradePreviewDto[] = plan.value?.flows.filter((item) => {
    const hasResult: boolean = selectedFlowIds.value.has(item.flowId);
    return hasResult;
  }) ?? [];
  return evaluatedValue;
});
const activePreview = computed<FlowLibraryUpgradePreviewDto | undefined>(() => {
  const findResult1: FlowLibraryUpgradePreviewDto | undefined = plan.value?.flows.find((item) => item.flowId === activeFlowId.value);
  return findResult1;
});
const acknowledgementIssues = computed(() => {
  const flatMapResult: LibraryUpgradeIssueDto[] = selectedPreviews.value.flatMap((preview) => {
    const filterResult: LibraryUpgradeIssueDto | readonly LibraryUpgradeIssueDto[] = preview.issues.filter((item) => item.requiresAcknowledgement);
    return filterResult;
  });
  return flatMapResult;
});
const hasBlockingIssues = computed(() => {
  const evaluatedValue2: boolean = selectedPreviews.value.length !== selectedFlowCount.value
    || selectedPreviews.value.some((preview) => {
      const evaluatedValue1: boolean = !preview.canApply || preview.issues.some((item) => item.blocksApplication);
      return evaluatedValue1;
    });
  return evaluatedValue2;
});
const canApply = computed(() => {
  const evaluatedValue3: boolean | undefined = plan.value && !applyResults.value && selectedPreviews.value.length > 0 && !hasBlockingIssues.value && acknowledgementIssues.value.every((item) => {
    const hasResult1: boolean = acknowledgedIssueIds.value.has(item.id);
    return hasResult1;
  });
  const booleanResult: boolean = Boolean(evaluatedValue3);
  return booleanResult;
});
function shortHash(value: string): string {
  const evaluatedValue4: string = value.length <= 10 ? value : value.slice(0, 10);
  return evaluatedValue4;
}
function classificationLabel(value: LibraryUpgradeIssueDto['classification']): string {
  const tResult: string = t(`libraryUpgrade.classification.${value}`);
  return tResult;
}
function resetPreview(): void {
  plan.value = undefined;
  applyResults.value = undefined;
  error.value = '';
  notice.value = '';
  acknowledgedIssueIds.value = new Set();
}
function setFlowSelected(flowId: string, selected: boolean): void {
  const next = new Set(selectedFlowIds.value);
  if (selected) {
    next.add(flowId);
    activeFlowId.value = flowId;
  }
  else {
    next.delete(flowId);
    if (activeFlowId.value === flowId) {
      activeFlowId.value = [...next][0] ?? '';
    }
  }
  selectedFlowIds.value = next;
  resetPreview();
}
function toggleAcknowledgement(issueId: string, checked: boolean): void {
  const next = new Set(acknowledgedIssueIds.value);
  if (checked) {
    next.add(issueId);
  }
  else {
    next.delete(issueId);
  }
  acknowledgedIssueIds.value = next;
}
function handleAcknowledgementChange(issueId: string, event: Event): void {
  toggleAcknowledgement(issueId, (event.target as HTMLInputElement).checked);
}
async function createPreview(): Promise<void> {
  if (!targetArtifactId.value || selectedFlowIds.value.size === 0) {
    error.value = t('libraryUpgrade.selectionRequired');
    return;
  }
  isPreviewing.value = true;
  error.value = '';
  notice.value = '';
  acknowledgedIssueIds.value = new Set();
  try {
    plan.value = await previewLibraryUpgrade(props.projectId, {
      sourceArtifactId: props.source.id,
      targetArtifactId: targetArtifactId.value,
      flowIds: [...selectedFlowIds.value],
    });
  }
  catch (reason) {
    error.value = reason instanceof Error ? reason.message : t('libraryUpgrade.previewFailed');
  }
  finally {
    isPreviewing.value = false;
  }
}
async function apply(): Promise<void> {
  if (!plan.value || !canApply.value) {
    return;
  }
  isApplying.value = true;
  error.value = '';
  notice.value = '';
  try {
    const requests = selectedPreviews.value.map((preview) => ({
      flowId: preview.flowId,
      expectedFlowVersion: preview.flowVersion,
      acknowledgedItemIds: [...acknowledgedIssueIds.value],
    }));
    if (requests.length === 1) {
      const result = await applyLibraryUpgrade(props.projectId, plan.value.id, requests[0]);
      applyResults.value = { planId: plan.value.id, succeeded: [result], failed: [] };
    }
    else {
      applyResults.value = await applyLibraryUpgradeBatch(props.projectId, plan.value.id, { flows: requests });
    }
    const result = applyResults.value;
    if (result.failed.length > 0) {
      notice.value = t('libraryUpgrade.batchPartial', { succeeded: result.succeeded.length, failed: result.failed.length });
    }
    else if (result.succeeded.length === 1) {
      notice.value = t('libraryUpgrade.applied', { version: result.succeeded[0].newVersion });
    }
    else {
      notice.value = t('libraryUpgrade.appliedBatch', { count: result.succeeded.length });
    }
    if (result.succeeded.length > 0) {
      emit('applied');
    }
  }
  catch (reason) {
    error.value = reason instanceof Error ? reason.message : t('libraryUpgrade.applyFailed');
  }
  finally {
    isApplying.value = false;
  }
}
watch([targetArtifactId, selectedFlowIds], resetPreview);
watch(() => props.targets, (targets) => {
  if (!targets.some((item) => item.id === targetArtifactId.value)) {
    targetArtifactId.value = targets[0]?.id ?? '';
  }
});
watch(() => props.flows, (flows) => {
  const mapResult: Iterable<string> | null | undefined = flows.map((item) => item.id);
  const validIds = new Set(mapResult);
  const filterResult1: Iterable<string> | null | undefined = [...selectedFlowIds.value].filter((id) => {
    const hasResult2: boolean = validIds.has(id);
    return hasResult2;
  });
  const next = new Set(filterResult1);
  if (next.size === 0 && flows[0]) {
    next.add(flows[0].id);
  }
  selectedFlowIds.value = next;
  if (!validIds.has(activeFlowId.value)) {
    activeFlowId.value = [...next][0] ?? '';
  }
});
const readTemplateText = computed(() => (props.source.semanticVersion ?? props.source.version));
const readSelectDisabled = computed(() => (isPreviewing.value || isApplying.value || !props.targets.length));
function readOptionItems() {
  return (props.targets);
}
function readTemplateText2(target: TemplateItem<ReturnType<typeof readOptionItems>>) {
  return (target.semanticVersion ?? target.version);
}
const readTemplateText3 = computed(() => {
  const evaluatedValue5: string = (t('libraryUpgrade.selectedFlowCount', { count: selectedFlowCount.value }));
  return evaluatedValue5;
});
function readLabelItems() {
  return (props.flows);
}
function readLabelClass(flow: TemplateItem<ReturnType<typeof readLabelItems>>) {
  const evaluatedValue6 = ({ 'is-selected': selectedFlowIds.value.has(flow.id) });
  return evaluatedValue6;
}
const readInputDisabled = computed(() => (isPreviewing.value || isApplying.value));
function readTemplateText4(flow: TemplateItem<ReturnType<typeof readLabelItems>>) {
  const evaluatedValue7 = (t('libraryUpgrade.flowVersion', { version: flow.version }));
  return evaluatedValue7;
}
const readButtonDisabled = computed(() => (isPreviewing.value || isApplying.value || !props.targets.length || !props.flows.length));
const readRefreshCwClass = computed(() => ({ 'is-spinning': isPreviewing.value }));
function readOptionItems2() {
  return (selectedPreviews.value);
}
function readTemplateText5(preview: TemplateItem<ReturnType<typeof readOptionItems2>>) {
  const evaluatedValue8 = (t('libraryUpgrade.flowVersion', { version: preview.flowVersion }));
  return evaluatedValue8;
}
const readSpanClass = computed(() => (['library-upgrade-status', activePreview.value!.canApply ? 'library-upgrade-status--ready' : 'library-upgrade-status--blocked']));
const readTemplateText6 = computed(() => {
  const evaluatedValue9: string = (activePreview.value!.canApply ? t('libraryUpgrade.ready') : t('libraryUpgrade.blocked'));
  return evaluatedValue9;
});
const readTemplateText7 = computed(() => {
  const evaluatedValue10: string = (t('libraryUpgrade.issueCount', { count: activePreview.value!.issues.length }));
  return evaluatedValue10;
});
function readTrItems() {
  return (activePreview.value!.issues);
}
function readSpanClass2(issue: TemplateItem<ReturnType<typeof readTrItems>>) {
  return (['library-upgrade-classification', `library-upgrade-classification--${issue.classification}`]);
}
function readTemplateText8(issue: TemplateItem<ReturnType<typeof readTrItems>>) {
  const evaluatedValue11 = (issue.nodeId ?? t('libraryUpgrade.flowScope'));
  return evaluatedValue11;
}
function readTemplateText9(issue: TemplateItem<ReturnType<typeof readTrItems>>) {
  const evaluatedValue12 = (issue.blocksApplication ? t('libraryUpgrade.manualRequired') : '—');
  return evaluatedValue12;
}
const readPElseIf = computed(() => (acknowledgementIssues.value.length && !canApply.value));
function readLiItems() {
  return (applyResults.value!.succeeded);
}
function readTemplateText10(result: TemplateItem<ReturnType<typeof readLiItems>>) {
  const evaluatedValue13 = (t('libraryUpgrade.resultSuccess', { version: result.newVersion }));
  return evaluatedValue13;
}
function readLiItems2() {
  return (applyResults.value!.failed);
}
function readTemplateText11(failure: TemplateItem<ReturnType<typeof readLiItems2>>) {
  const evaluatedValue14 = (t('libraryUpgrade.resultFailure', { code: failure.code ?? 'unknown' }));
  return evaluatedValue14;
}
const readButtonDisabled2 = computed(() => (isPreviewing.value || isApplying.value));
const readButtonDisabled3 = computed(() => (!canApply.value || isApplying.value));
const readCheckClass = computed(() => ({ 'is-spinning': isApplying.value }));
const readTemplateText12 = computed(() => {
  const evaluatedValue15: string = (selectedFlowCount.value > 1 ? t('libraryUpgrade.applyBatch') : t('libraryUpgrade.apply'));
  return evaluatedValue15;
});
</script>

<template>
  <div
    class="library-upgrade-dialog-backdrop"
    role="presentation"
    @click.self="emit('close')"
  >
    <section
      class="library-upgrade-dialog"
      role="dialog"
      aria-modal="true"
      :aria-label="t('libraryUpgrade.title')"
    >
      <header>
        <div>
          <p class="operations-console__eyebrow">{{ t('libraryUpgrade.eyebrow') }}</p>
          <h2>{{ t('libraryUpgrade.title') }}</h2>
          <span>{{ projectName }}</span>
        </div>
        <button
          class="icon-button"
          type="button"
          :title="t('command.close')"
          :aria-label="t('command.close')"
          @click="emit('close')"
        >
          <X :size="16" />
        </button>
      </header>
      <div
        class="library-upgrade-dialog__message-slot"
        aria-live="polite"
      >
        <p
          v-if="error"
          class="library-upgrade-dialog__message library-upgrade-dialog__message--error"
          role="alert"
        >
          <CircleAlert :size="15" />
          {{ error }}
        </p>
        <p
          v-else-if="notice"
          class="library-upgrade-dialog__message library-upgrade-dialog__message--success"
        >
          <Check :size="15" />
          {{ notice }}
        </p>
      </div>
      <div class="library-upgrade-dialog__content">
        <section
          class="library-upgrade-dialog__selection"
          :aria-label="t('libraryUpgrade.selectionTitle')"
        >
          <div class="library-upgrade-dialog__heading">
            <div>
              <h3>{{ t('libraryUpgrade.selectionTitle') }}</h3>
              <p>{{ t('libraryUpgrade.selectionHint') }}</p>
            </div>
          </div>
          <dl class="library-upgrade-artifact">
            <div>
              <dt>{{ t('libraryUpgrade.source') }}</dt>
              <dd>
                <strong>{{ source.name }}</strong>
                <span>{{ readTemplateText }} · {{ shortHash(source.sha256) }}</span>
              </dd>
            </div>
            <ArrowRight
              :size="17"
              aria-hidden="true"
             />
            <div>
              <dt>{{ t('libraryUpgrade.target') }}</dt>
              <dd>
                <label
                  class="sr-only"
                  for="library-upgrade-target"
                >{{ t('libraryUpgrade.target') }}</label>
                <select
                  id="library-upgrade-target"
                  v-model="targetArtifactId"
                  :disabled="readSelectDisabled"
                >
                  <option
                    v-for="target in readOptionItems()"
                    :key="target.id"
                    :value="target.id"
                  >{{ target.name }} · {{ readTemplateText2(target) }} · {{ shortHash(target.sha256) }}</option>
                </select>
                <span v-if="selectedTarget">{{ selectedTarget.fileName }}</span>
              </dd>
            </div>
          </dl>
          <fieldset class="library-upgrade-flow-picker">
            <legend>
              <span>{{ t('libraryUpgrade.flows') }}</span>
              <small>{{ readTemplateText3 }}</small>
            </legend>
            <div class="library-upgrade-flow-picker__list">
              <label
                v-for="flow in readLabelItems()"
                :key="flow.id"
                :class="readLabelClass(flow)"
              >
                <input
                  type="checkbox"
                  :checked="selectedFlowIds.has(flow.id)"
                  :disabled="readInputDisabled"
                  @change="setFlowSelected(flow.id, ($event.target as HTMLInputElement).checked)"
                 />
                <span>
                  <strong>{{ flow.id }}</strong>
                  <small>{{ readTemplateText4(flow) }}</small>
                </span>
              </label>
            </div>
          </fieldset>
          <p
            v-if="!targets.length"
            class="library-upgrade-dialog__empty"
          >{{ t('libraryUpgrade.noTargets') }}</p>
          <p
            v-else-if="!flows.length"
            class="library-upgrade-dialog__empty"
          >{{ t('libraryUpgrade.noFlows') }}</p>
          <button
            class="command-button run"
            type="button"
            :disabled="readButtonDisabled"
            @click="createPreview"
          >
            <RefreshCw
              :size="15"
              :class="readRefreshCwClass"
             />
            <span>{{ t('libraryUpgrade.createPreview') }}</span>
          </button>
        </section>
        <section
          class="library-upgrade-dialog__report"
          :aria-label="t('libraryUpgrade.reportTitle')"
        >
          <div class="library-upgrade-dialog__heading">
            <div>
              <h3>{{ t('libraryUpgrade.reportTitle') }}</h3>
              <p>{{ t('libraryUpgrade.reportHint') }}</p>
            </div>
            <span
              v-if="activePreview"
              class="library-upgrade-report__count"
            >{{ activePreview.affectedNodeCount }}</span>
          </div>
          <p
            v-if="!plan"
            class="library-upgrade-dialog__empty"
          >{{ t('libraryUpgrade.reportEmpty') }}</p>
          <template v-else-if="activePreview">
            <label
              class="library-upgrade-field"
              for="library-upgrade-report-flow"
            >
              <span>{{ t('libraryUpgrade.reportFlow') }}</span>
              <select
                id="library-upgrade-report-flow"
                v-model="activeFlowId"
                :disabled="isApplying"
              >
                <option
                  v-for="preview in readOptionItems2()"
                  :key="preview.flowId"
                  :value="preview.flowId"
                >{{ preview.flowId }} · {{ readTemplateText5(preview) }}</option>
              </select>
            </label>
            <div class="library-upgrade-summary">
              <span :class="readSpanClass">{{ readTemplateText6 }}</span>
              <span>{{ readTemplateText7 }}</span>
            </div>
            <div class="library-upgrade-table-wrap">
              <table class="library-upgrade-table">
                <thead>
                  <tr>
                    <th>{{ t('libraryUpgrade.classification') }}</th>
                    <th>{{ t('libraryUpgrade.scope') }}</th>
                    <th>{{ t('libraryUpgrade.diagnostic') }}</th>
                    <th>{{ t('libraryUpgrade.confirm') }}</th>
                  </tr>
                </thead>
                <tbody>
                  <tr
                    v-for="issue in readTrItems()"
                    :key="issue.id"
                  >
                    <td>
                      <span :class="readSpanClass2(issue)">{{ classificationLabel(issue.classification) }}</span>
                    </td>
                    <td>
                      <code>{{ readTemplateText8(issue) }}</code>
                      <small v-if="issue.sourceParameterId">
                        {{ issue.sourceParameterId }}
                        <template v-if="issue.targetParameterId"> → {{ issue.targetParameterId }}</template>
                      </small>
                    </td>
                    <td>
                      <strong>{{ issue.code }}</strong>
                      <span>{{ issue.message }}</span>
                    </td>
                    <td>
                      <label
                        v-if="issue.requiresAcknowledgement"
                        class="library-upgrade-confirm"
                      >
                        <input
                          type="checkbox"
                          :checked="acknowledgedIssueIds.has(issue.id)"
                          :disabled="isApplying"
                          @change="handleAcknowledgementChange(issue.id, $event)"
                         />
                        <span>{{ t('libraryUpgrade.acknowledge') }}</span>
                      </label>
                      <span
                        v-else
                        class="library-upgrade-no-confirm"
                      >{{ readTemplateText9(issue) }}</span>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
            <p
              v-if="hasBlockingIssues"
              class="library-upgrade-dialog__warning"
            >
              <CircleAlert :size="15" />
              {{ t('libraryUpgrade.blockedHint') }}
            </p>
            <p
              v-else-if="readPElseIf"
              class="library-upgrade-dialog__warning"
            >
              <ClipboardCheck :size="15" />
              {{ t('libraryUpgrade.acknowledgementHint') }}
            </p>
            <ul
              v-if="applyResults"
              class="library-upgrade-results"
              :aria-label="t('libraryUpgrade.resultsTitle')"
            >
              <li
                v-for="result in readLiItems()"
                :key="result.flowId"
                class="library-upgrade-results__success"
              >
                <Check :size="14" />
                <span>
                  <strong>{{ result.flowId }}</strong>
                  {{ readTemplateText10(result) }}
                </span>
              </li>
              <li
                v-for="failure in readLiItems2()"
                :key="failure.flowId"
                class="library-upgrade-results__failure"
              >
                <CircleAlert :size="14" />
                <span>
                  <strong>{{ failure.flowId }}</strong>
                  {{ readTemplateText11(failure) }}
                  <small>{{ failure.message }}</small>
                </span>
              </li>
            </ul>
          </template>
        </section>
      </div>
      <footer>
        <button
          class="command-button quiet"
          type="button"
          :disabled="readButtonDisabled2"
          @click="emit('close')"
        >{{ t('command.cancel') }}</button>
        <button
          class="command-button run"
          type="button"
          :disabled="readButtonDisabled3"
          @click="apply"
        >
          <Check
            :size="15"
            :class="readCheckClass"
           />
          <span>{{ readTemplateText12 }}</span>
        </button>
      </footer>
    </section>
  </div>
</template>
