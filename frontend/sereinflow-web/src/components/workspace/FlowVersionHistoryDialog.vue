<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { Clock3, FileSearch, LoaderCircle, RotateCcw, X } from 'lucide-vue-next'
import {
  FlowApiError,
  getFlowVersion,
  listFlowVersions,
  rollbackFlowVersion,
  type FlowVersionDetailDto,
  type FlowVersionSummaryDto,
  type FlowVersionTrack,
} from '../../api/flowApi'
import {
  flowVersionLabel,
  flowVersionOperationKey,
  flowVersionSourceLabel,
  flowVersionTrackKey,
} from '../../flow/flowVersionDisplay'
import { t } from '../../i18n'

const props = defineProps<{
  projectId: string
  flowId: string
  developmentVersion: number
  productionVersion?: number
  canMutate: boolean
}>()

const emit = defineEmits<{
  close: []
  changed: [version: FlowVersionSummaryDto]
}>()

const activeTrack = ref<FlowVersionTrack>('development')
const versions = ref<FlowVersionSummaryDto[]>([])
const selectedDetail = ref<FlowVersionDetailDto>()
const isLoading = ref(false)
const isDetailLoading = ref(false)
const isRollingBack = ref(false)
const errorMessage = ref('')

const activeHeadVersion = computed(() => activeTrack.value === 'development'
  ? props.developmentVersion
  : props.productionVersion)
const selectedVersion = computed(() => selectedDetail.value?.version)

function formatDate(value: string | undefined): string {
  if (!value) return '—'
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString()
}

async function selectVersion(item: FlowVersionSummaryDto): Promise<void> {
  if (isDetailLoading.value || selectedDetail.value?.version.version === item.version) return
  isDetailLoading.value = true
  errorMessage.value = ''
  try {
    selectedDetail.value = await getFlowVersion(props.projectId, props.flowId, item.version)
  } catch (error) {
    errorMessage.value = error instanceof FlowApiError ? error.message : t('version.loadFailed')
  } finally {
    isDetailLoading.value = false
  }
}

async function loadVersions(preferredVersion?: number): Promise<void> {
  isLoading.value = true
  errorMessage.value = ''
  try {
    versions.value = await listFlowVersions(props.projectId, props.flowId, activeTrack.value)
    const target = preferredVersion === undefined
      ? versions.value.find((item) => item.isCurrent) ?? versions.value[0]
      : versions.value.find((item) => item.version === preferredVersion) ?? versions.value.find((item) => item.isCurrent) ?? versions.value[0]
    selectedDetail.value = undefined
    if (target) await selectVersion(target)
  } catch (error) {
    versions.value = []
    selectedDetail.value = undefined
    errorMessage.value = error instanceof FlowApiError ? error.message : t('version.loadFailed')
  } finally {
    isLoading.value = false
  }
}

async function rollbackSelected(): Promise<void> {
  const selected = selectedVersion.value
  const expectedHeadVersion = activeHeadVersion.value
  if (!props.canMutate || !selected || selected.isCurrent || !expectedHeadVersion || isRollingBack.value) return

  isRollingBack.value = true
  errorMessage.value = ''
  try {
    const created = await rollbackFlowVersion(props.projectId, props.flowId, selected.version, {
      track: activeTrack.value,
      expectedHeadVersion,
    })
    emit('changed', created)
    await loadVersions(created.version)
  } catch (error) {
    errorMessage.value = error instanceof FlowApiError ? error.message : t('version.rollbackFailed')
  } finally {
    isRollingBack.value = false
  }
}

watch(activeTrack, () => { void loadVersions() })
onMounted(() => { void loadVersions() })
</script>

<template>
  <div class="flow-version-dialog-backdrop" role="presentation" @click.self="emit('close')">
    <section class="flow-version-dialog" role="dialog" aria-modal="true" :aria-label="t('version.historyTitle')">
      <header>
        <div>
          <p class="eyebrow"><Clock3 :size="13" />{{ t('version.historyEyebrow') }}</p>
          <h2>{{ t('version.historyTitle') }}</h2>
        </div>
        <button class="icon-button" type="button" :title="t('command.close')" :aria-label="t('command.close')" @click="emit('close')"><X :size="16" /></button>
      </header>

      <div class="flow-version-dialog__tabs" role="tablist" :aria-label="t('version.historyTitle')">
        <button type="button" role="tab" :aria-selected="activeTrack === 'development'" :class="{ active: activeTrack === 'development' }" @click="activeTrack = 'development'">
          <span>{{ t(flowVersionTrackKey('development')) }}</span><code>{{ flowVersionLabel(developmentVersion) }}</code>
        </button>
        <button type="button" role="tab" :aria-selected="activeTrack === 'production'" :class="{ active: activeTrack === 'production' }" @click="activeTrack = 'production'">
          <span>{{ t(flowVersionTrackKey('production')) }}</span><code>{{ flowVersionLabel(productionVersion) }}</code>
        </button>
      </div>

      <p v-if="errorMessage" class="flow-version-dialog__error">{{ errorMessage }}</p>
      <div class="flow-version-dialog__content">
        <div class="flow-version-list" :aria-busy="isLoading">
          <p v-if="isLoading" class="flow-version-dialog__state"><LoaderCircle :size="16" class="is-spinning" />{{ t('version.loading') }}</p>
          <p v-else-if="versions.length === 0" class="flow-version-dialog__state">{{ t('version.empty') }}</p>
          <template v-else>
            <button
              v-for="item in versions"
              :key="item.version"
              class="flow-version-list__item"
              :class="{ selected: selectedVersion?.version === item.version, current: item.isCurrent }"
              type="button"
              :aria-pressed="selectedVersion?.version === item.version"
              @click="selectVersion(item)"
            >
              <span class="flow-version-list__version">{{ flowVersionLabel(item.version) }}</span>
              <span class="flow-version-list__copy"><strong>{{ t(flowVersionOperationKey(item.operation)) }}</strong><small>{{ item.remark }}</small></span>
              <span v-if="item.isCurrent" class="flow-version-list__current">{{ t('version.current') }}</span>
            </button>
          </template>
        </div>

        <div class="flow-version-detail" :aria-busy="isDetailLoading">
          <p v-if="isDetailLoading" class="flow-version-dialog__state"><LoaderCircle :size="16" class="is-spinning" />{{ t('version.loading') }}</p>
          <p v-else-if="!selectedDetail" class="flow-version-dialog__state"><FileSearch :size="17" />{{ t('version.select') }}</p>
          <template v-else>
            <header class="flow-version-detail__header">
              <div><span>{{ t(flowVersionTrackKey(selectedDetail.version.track)) }}</span><strong>{{ flowVersionLabel(selectedDetail.version.version) }}</strong></div>
              <button
                v-if="!selectedDetail.version.isCurrent"
                class="command-button quiet"
                type="button"
                :disabled="!canMutate || isRollingBack"
                @click="rollbackSelected"
              ><RotateCcw :size="15" /><span>{{ isRollingBack ? t('version.rollingBack') : t('version.rollback') }}</span></button>
            </header>
            <dl class="flow-version-detail__facts">
              <div><dt>{{ t('version.operation') }}</dt><dd>{{ t(flowVersionOperationKey(selectedDetail.version.operation)) }}</dd></div>
              <div><dt>{{ t('version.source') }}</dt><dd>{{ flowVersionSourceLabel(selectedDetail.version.sourceVersion) ?? '—' }}</dd></div>
              <div><dt>{{ t('version.createdAt') }}</dt><dd>{{ formatDate(selectedDetail.version.createdAt) }}</dd></div>
              <div><dt>{{ t('version.entryNode') }}</dt><dd><code>{{ selectedDetail.definition.entryNodeId }}</code></dd></div>
              <div><dt>{{ t('version.canvases') }}</dt><dd>{{ selectedDetail.definition.canvases.length }}</dd></div>
              <div><dt>{{ t('version.nodes') }}</dt><dd>{{ selectedDetail.definition.canvases.reduce((count, canvas) => count + canvas.nodes.length, 0) }}</dd></div>
            </dl>
            <div class="flow-version-detail__remark"><span>{{ t('version.remark') }}</span><p>{{ selectedDetail.version.remark || '—' }}</p></div>
          </template>
        </div>
      </div>
    </section>
  </div>
</template>
