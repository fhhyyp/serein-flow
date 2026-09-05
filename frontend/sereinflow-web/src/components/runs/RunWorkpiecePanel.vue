<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { Download, FileText, Image as ImageIcon, PackageOpen, RefreshCw } from 'lucide-vue-next'
import { getFlowWorkpieceUrl, listFlowRunWorkpieces, type FlowWorkpieceDto } from '../../api/flowApi'
import { selectFlowWorkpieceId } from '../../flow/workpieceSelection'
import { locale, t } from '../../i18n'

const props = withDefaults(defineProps<{
  embedded?: boolean
  runId: string
  live?: boolean
  compact?: boolean
}>(), {
  live: false,
  compact: false,
  embedded: false,
})

const workpieces = ref<FlowWorkpieceDto[]>([])
const selectedId = ref('')
const isLoading = ref(true)
const isRefreshing = ref(false)
const errorKey = ref('')
let refreshTimer: number | undefined
let loadRevision = 0

const selectedWorkpiece = computed(() => workpieces.value.find((item) => item.id === selectedId.value))
const selectedUrl = computed(() => selectedWorkpiece.value
  ? getFlowWorkpieceUrl(props.runId, selectedWorkpiece.value.id)
  : '')
const selectedDownloadUrl = computed(() => selectedWorkpiece.value
  ? getFlowWorkpieceUrl(props.runId, selectedWorkpiece.value.id, true)
  : '')

function isImage(workpiece: FlowWorkpieceDto): boolean {
  return workpiece.kind.toLowerCase() === 'image' || workpiece.contentType.toLowerCase().startsWith('image/')
}

function isInlinePreview(workpiece: FlowWorkpieceDto): boolean {
  const contentType = workpiece.contentType.toLowerCase()
  return isImage(workpiece)
    || contentType.startsWith('text/')
    || contentType.includes('json')
    || contentType.includes('xml')
    || contentType === 'application/pdf'
}

function kindLabel(workpiece: FlowWorkpieceDto): string {
  return isImage(workpiece) ? t('workpiece.image') : t('workpiece.file')
}

function formatBytes(bytes: number): string {
  if (bytes < 1_024) return `${bytes} B`
  if (bytes < 1_024 * 1_024) return `${(bytes / 1_024).toFixed(bytes < 10 * 1_024 ? 1 : 0)} KB`
  if (bytes < 1_024 * 1_024 * 1_024) return `${(bytes / (1_024 * 1_024)).toFixed(bytes < 10 * 1_024 * 1_024 ? 1 : 0)} MB`
  return `${(bytes / (1_024 * 1_024 * 1_024)).toFixed(1)} GB`
}

function formatDate(value: string): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat(locale.value, { dateStyle: 'short', timeStyle: 'short' }).format(date)
}

async function refreshWorkpieces(background = false): Promise<void> {
  if (!props.runId) return
  const revision = ++loadRevision
  if (background) isRefreshing.value = true
  else isLoading.value = true
  try {
    const next = await listFlowRunWorkpieces(props.runId)
    if (revision !== loadRevision) return
    const previous = workpieces.value
    workpieces.value = next
    selectedId.value = selectFlowWorkpieceId(previous, next, selectedId.value, props.live && background)
    errorKey.value = ''
  } catch {
    if (revision !== loadRevision) return
    if (!background || workpieces.value.length === 0) errorKey.value = 'workpiece.loadFailed'
  } finally {
    if (revision !== loadRevision) return
    isLoading.value = false
    isRefreshing.value = false
  }
}

function stopAutoRefresh(): void {
  if (refreshTimer !== undefined) {
    window.clearInterval(refreshTimer)
    refreshTimer = undefined
  }
}

function syncAutoRefresh(): void {
  stopAutoRefresh()
  if (!props.live || typeof window === 'undefined') return
  refreshTimer = window.setInterval(() => void refreshWorkpieces(true), 2_000)
}

watch(() => props.runId, () => {
  selectedId.value = ''
  void refreshWorkpieces()
})
watch(() => props.live, syncAutoRefresh)
onMounted(() => {
  void refreshWorkpieces()
  syncAutoRefresh()
})
onBeforeUnmount(stopAutoRefresh)
</script>

<template>
  <section class="run-workpiece-panel" :class="{ 'run-workpiece-panel--compact': props.compact, 'run-workpiece-panel--embedded': props.embedded }" :aria-label="t('workpiece.title')">
    <header class="run-workpiece-panel__header">
      <div class="run-workpiece-panel__heading">
        <span class="run-workpiece-panel__eyebrow"><PackageOpen :size="13" />{{ t('workpiece.eyebrow') }}</span>
        <strong>{{ t('workpiece.title') }}</strong>
      </div>
      <div class="run-workpiece-panel__actions">
        <span v-if="props.live" class="run-workpiece-panel__live"><i></i>{{ t('workpiece.live') }}</span>
        <button class="icon-button compact" type="button" :title="t('workpiece.refresh')" :aria-label="t('workpiece.refresh')" :disabled="isLoading || isRefreshing" @click="refreshWorkpieces(true)">
          <RefreshCw :size="14" :class="{ 'is-spinning': isRefreshing }" />
        </button>
      </div>
    </header>

    <p class="run-workpiece-panel__hint">{{ t('workpiece.hint') }}</p>

    <div v-if="isLoading" class="run-workpiece-panel__status">{{ t('workpiece.loading') }}</div>
    <div v-else-if="errorKey" class="run-workpiece-panel__status run-workpiece-panel__status--error" role="alert">{{ t(errorKey) }}</div>
    <div v-else-if="workpieces.length === 0" class="run-workpiece-panel__status">
      <PackageOpen :size="18" />{{ t('workpiece.empty') }}
    </div>
    <div v-else class="run-workpiece-panel__body">
      <div class="run-workpiece-panel__list" role="listbox" :aria-label="t('workpiece.title')">
        <button
          v-for="workpiece in workpieces"
          :key="workpiece.id"
          class="run-workpiece-panel__item"
          :class="{ active: workpiece.id === selectedId }"
          type="button"
          role="option"
          :aria-selected="workpiece.id === selectedId"
          @click="selectedId = workpiece.id"
        >
          <span class="run-workpiece-panel__item-icon"><ImageIcon v-if="isImage(workpiece)" :size="15" /><FileText v-else :size="15" /></span>
          <span class="run-workpiece-panel__item-copy">
            <strong :title="workpiece.name">{{ workpiece.name }}</strong>
            <small>{{ kindLabel(workpiece) }} · {{ formatBytes(workpiece.length) }}</small>
          </span>
          <time :datetime="workpiece.createdAt">{{ formatDate(workpiece.createdAt) }}</time>
        </button>
      </div>

      <article v-if="selectedWorkpiece" class="run-workpiece-panel__preview">
        <header>
          <div>
            <span>{{ t('workpiece.preview') }}</span>
            <strong :title="selectedWorkpiece.name">{{ selectedWorkpiece.name }}</strong>
          </div>
          <a class="run-workpiece-panel__download" :href="selectedDownloadUrl" :download="selectedWorkpiece.name" :title="t('workpiece.download')" :aria-label="t('workpiece.download')">
            <Download :size="14" />
          </a>
        </header>
        <div v-if="isImage(selectedWorkpiece)" class="run-workpiece-panel__image-stage">
          <img :src="selectedUrl" :alt="selectedWorkpiece.name" />
        </div>
        <iframe v-else-if="isInlinePreview(selectedWorkpiece)" class="run-workpiece-panel__iframe" :src="selectedUrl" :title="selectedWorkpiece.name"></iframe>
        <div v-else class="run-workpiece-panel__unavailable">
          <FileText :size="24" />
          <strong>{{ t('workpiece.previewUnavailable') }}</strong>
          <span>{{ selectedWorkpiece.contentType }} · {{ formatBytes(selectedWorkpiece.length) }}</span>
          <a class="command-button quiet" :href="selectedDownloadUrl" :download="selectedWorkpiece.name"><Download :size="14" /><span>{{ t('workpiece.download') }}</span></a>
        </div>
      </article>
    </div>
  </section>
</template>
