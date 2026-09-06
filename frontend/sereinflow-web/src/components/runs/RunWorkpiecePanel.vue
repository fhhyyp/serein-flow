<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { Download, FileText, Image as ImageIcon, Maximize2, PackageOpen, RefreshCw, X } from 'lucide-vue-next'
import { getFlowWorkpieceUrl, listFlowRunWorkpieces, type FlowWorkpieceDto } from '../../api/flowApi'
import {
  firstFlowWorkpieceForExecution,
  firstFlowWorkpieceForNode,
  latestFlowWorkpiece,
  selectFlowWorkpieceId,
} from '../../flow/workpieceSelection'
import { locale, t } from '../../i18n'

const props = withDefaults(defineProps<{
  embedded?: boolean
  runId: string
  focusNodeId?: string
  focusExecutionId?: string
  refreshSignal?: number
  live?: boolean
  compact?: boolean
}>(), {
  live: false,
  compact: false,
  embedded: false,
})

const emit = defineEmits<{
  'select-node': [nodeId: string, executionId?: string]
}>()

const workpieces = ref<FlowWorkpieceDto[]>([])
const selectedId = ref('')
const isLoading = ref(true)
const isRefreshing = ref(false)
const errorKey = ref('')
const imagePreviewWorkpiece = ref<FlowWorkpieceDto>()
const imagePreviewDialog = ref<HTMLElement>()
let previousImagePreviewTrigger: HTMLElement | null = null
let refreshTimer: number | undefined
let loadRevision = 0

const selectedWorkpiece = computed(() => workpieces.value.find((item) => item.id === selectedId.value))
const selectedUrl = computed(() => selectedWorkpiece.value
  ? getFlowWorkpieceUrl(props.runId, selectedWorkpiece.value.id)
  : '')
const selectedDownloadUrl = computed(() => selectedWorkpiece.value
  ? getFlowWorkpieceUrl(props.runId, selectedWorkpiece.value.id, true)
  : '')
const imagePreviewUrl = computed(() => imagePreviewWorkpiece.value
  ? getFlowWorkpieceUrl(props.runId, imagePreviewWorkpiece.value.id)
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

function openImagePreview(workpiece: FlowWorkpieceDto | undefined): void {
  if (!workpiece || !isImage(workpiece)) return
  previousImagePreviewTrigger = document.activeElement instanceof HTMLElement ? document.activeElement : null
  imagePreviewWorkpiece.value = workpiece
  void nextTick(() => imagePreviewDialog.value?.focus())
}

function closeImagePreview(restoreFocus = true): void {
  const wasOpen = imagePreviewWorkpiece.value !== undefined
  imagePreviewWorkpiece.value = undefined
  const trigger = previousImagePreviewTrigger
  previousImagePreviewTrigger = null
  if (!restoreFocus || !wasOpen) return
  void nextTick(() => trigger?.focus())
}

function handleImagePreviewKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape') {
    event.preventDefault()
    closeImagePreview()
  }
}

function selectWorkpiece(workpiece: FlowWorkpieceDto): void {
  selectedId.value = workpiece.id
  if (workpiece.nodeId) emit('select-node', workpiece.nodeId, workpiece.executionId)
}

async function refreshWorkpieces(background = false, selectLatest = false): Promise<void> {
  if (!props.runId) return
  const revision = ++loadRevision
  if (background) isRefreshing.value = true
  else isLoading.value = true
  try {
    const next = await listFlowRunWorkpieces(props.runId)
    if (revision !== loadRevision) return
    const previous = workpieces.value
    workpieces.value = next
    const currentSelection = next.find((workpiece) => workpiece.id === selectedId.value)
    const focusedWorkpiece = props.focusExecutionId
      ? firstFlowWorkpieceForExecution(next, props.focusExecutionId)
      : firstFlowWorkpieceForNode(next, props.focusNodeId)
    if (focusedWorkpiece) {
      selectedId.value = props.focusExecutionId && currentSelection?.executionId === props.focusExecutionId
        ? currentSelection.id
        : focusedWorkpiece.id
    } else if (props.focusExecutionId) {
      selectedId.value = ''
    } else if (!props.focusNodeId) {
      selectedId.value = selectLatest
        ? latestFlowWorkpiece(next)?.id ?? ''
        : selectFlowWorkpieceId(previous, next, selectedId.value, props.live && background)
    }
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
  closeImagePreview()
  selectedId.value = ''
  void refreshWorkpieces()
})
watch([() => props.focusNodeId, () => props.focusExecutionId, () => props.refreshSignal], ([focusNodeId, focusExecutionId, refreshSignal], [, previousFocusExecutionId, previousRefreshSignal]) => {
  if (focusExecutionId !== previousFocusExecutionId || focusNodeId !== undefined) {
    void refreshWorkpieces(true)
    return
  }
  void refreshWorkpieces(true, refreshSignal !== previousRefreshSignal && focusNodeId === undefined)
})
watch(() => props.live, syncAutoRefresh)
onMounted(() => {
  void refreshWorkpieces()
  syncAutoRefresh()
})
onBeforeUnmount(() => {
  stopAutoRefresh()
  closeImagePreview(false)
})
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

    <!-- <p class="run-workpiece-panel__hint">{{ t('workpiece.hint') }}</p> -->

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
          @click="selectWorkpiece(workpiece)"
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
          <button
            class="run-workpiece-panel__image-trigger"
            type="button"
            :aria-label="t('workpiece.openImage')"
            :title="t('workpiece.openImage')"
            @click="openImagePreview(selectedWorkpiece)"
          >
            <img :src="selectedUrl" :alt="selectedWorkpiece.name" />
            <span class="run-workpiece-panel__image-action" aria-hidden="true"><Maximize2 :size="14" />{{ t('workpiece.openImage') }}</span>
          </button>
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

    <Teleport to="body">
      <div
        v-if="imagePreviewWorkpiece"
        class="run-workpiece-image-lightbox"
        @mousedown.self="closeImagePreview()"
      >
        <section
          ref="imagePreviewDialog"
          class="run-workpiece-image-lightbox__dialog"
          role="dialog"
          aria-modal="true"
          :aria-label="imagePreviewWorkpiece.name"
          tabindex="-1"
          @keydown.capture="handleImagePreviewKeydown"
        >
          <header>
            <div>
              <span>{{ t('workpiece.preview') }}</span>
              <strong :title="imagePreviewWorkpiece.name">{{ imagePreviewWorkpiece.name }}</strong>
            </div>
            <button class="icon-button" type="button" :title="t('command.close')" :aria-label="t('command.close')" @click="closeImagePreview()">
              <X :size="17" />
            </button>
          </header>
          <div class="run-workpiece-image-lightbox__body">
            <img :src="imagePreviewUrl" :alt="imagePreviewWorkpiece.name" />
          </div>
        </section>
      </div>
    </Teleport>
  </section>
</template>
