<script setup lang="ts">
import { ref } from 'vue'
import { AlertTriangle, Check, FileArchive, LoaderCircle, UploadCloud, X } from 'lucide-vue-next'
import { uploadLibrary, type LibraryDto } from '../../api/libraryApi'
import { t } from '../../i18n'

const emit = defineEmits<{
  close: []
  uploaded: [library: LibraryDto]
}>()
const props = defineProps<{ maxFileBytes: number }>()

const fileInput = ref<HTMLInputElement | null>(null)
const selectedFile = ref<File | null>(null)
const isDragOver = ref(false)
const isUploading = ref(false)
const errorMessage = ref('')
const successMessage = ref('')

function openFilePicker(): void {
  if (!isUploading.value) {
    fileInput.value?.click()
  }
}

function handleFileInput(event: Event): void {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (file) {
    chooseFile(file)
  }
}

function handleDrop(event: DragEvent): void {
  isDragOver.value = false
  const file = event.dataTransfer?.files?.[0]
  if (file) {
    chooseFile(file)
  }
}

function chooseFile(file: File): void {
  errorMessage.value = ''
  successMessage.value = ''
  if (!file.name.toLowerCase().endsWith('.zip')) {
    selectedFile.value = null
    errorMessage.value = t('libraryUpload.zipOnly')
    return
  }

  if (!/^.+-.+\.zip$/i.test(file.name)) {
    selectedFile.value = null
    errorMessage.value = t('libraryUpload.invalidName')
    return
  }

  if (file.size > props.maxFileBytes) {
    selectedFile.value = null
    errorMessage.value = t('libraryUpload.tooLarge', { size: formatFileSize(props.maxFileBytes) })
    return
  }

  selectedFile.value = file
}

function clearFile(): void {
  selectedFile.value = null
  errorMessage.value = ''
  successMessage.value = ''
  if (fileInput.value) {
    fileInput.value.value = ''
  }
}

async function submit(): Promise<void> {
  if (!selectedFile.value || isUploading.value) {
    return
  }

  isUploading.value = true
  errorMessage.value = ''
  successMessage.value = ''
  try {
    const result = await uploadLibrary(selectedFile.value)
    successMessage.value = result.alreadyExists
      ? t('libraryUpload.alreadyExists', { name: result.library.name, version: result.library.version })
      : t('libraryUpload.success', { name: result.library.name, count: result.library.nodes.length })
    emit('uploaded', result.library)
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('libraryUpload.failed')
  } finally {
    isUploading.value = false
  }
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}
</script>

<template>
  <div class="library-upload-overlay" role="presentation" @click.self="emit('close')">
    <section class="library-upload-dialog" role="dialog" aria-modal="true" :aria-label="t('libraryUpload.title')">
      <header class="library-upload-dialog__header">
        <div>
          <span class="eyebrow">{{ t('libraryUpload.eyebrow') }}</span>
          <h2>{{ t('libraryUpload.title') }}</h2>
        </div>
        <button class="icon-button" type="button" :title="t('command.close')" :aria-label="t('command.close')" :disabled="isUploading" @click="emit('close')"><X :size="16" /></button>
      </header>

      <div class="library-upload-dialog__body">
        <div class="library-upload-requirements">
          <strong>{{ t('libraryUpload.requirements') }}</strong>
          <p>{{ t('libraryUpload.requirementName') }}</p>
          <p>{{ t('libraryUpload.requirementDll') }}</p>
          <p>{{ t('libraryUpload.requirementSafety') }}</p>
        </div>

        <input ref="fileInput" class="library-upload-input" type="file" accept=".zip,application/zip" @change="handleFileInput" />
        <button class="library-upload-dropzone" :class="{ 'library-upload-dropzone--active': isDragOver }" type="button" :disabled="isUploading" @click="openFilePicker" @dragover.prevent="isDragOver = true" @dragleave.prevent="isDragOver = false" @drop.prevent="handleDrop">
          <UploadCloud :size="22" />
          <strong>{{ t('libraryUpload.dropTitle') }}</strong>
          <span>{{ t('libraryUpload.dropHint', { size: formatFileSize(props.maxFileBytes) }) }}</span>
        </button>

        <div v-if="selectedFile" class="library-upload-file">
          <FileArchive :size="18" />
          <div><strong>{{ selectedFile.name }}</strong><span>{{ formatFileSize(selectedFile.size) }}</span></div>
          <button type="button" :title="t('libraryUpload.removeFile')" :aria-label="t('libraryUpload.removeFile')" :disabled="isUploading" @click="clearFile"><X :size="15" /></button>
        </div>

        <p v-if="errorMessage" class="library-upload-message library-upload-message--error" role="alert"><AlertTriangle :size="15" />{{ errorMessage }}</p>
        <p v-if="successMessage" class="library-upload-message library-upload-message--success" role="status"><Check :size="15" />{{ successMessage }}</p>
      </div>

      <footer class="library-upload-dialog__footer">
        <button class="command-button quiet" type="button" :disabled="isUploading" @click="emit('close')">{{ t('command.cancel') }}</button>
        <button class="command-button run library-upload-submit" type="button" :disabled="!selectedFile || isUploading" @click="submit"><LoaderCircle v-if="isUploading" class="spin" :size="14" /><UploadCloud v-else :size="14" />{{ isUploading ? t('libraryUpload.uploading') : t('libraryUpload.submit') }}</button>
      </footer>
    </section>
  </div>
</template>
