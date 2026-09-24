<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import {
  Activity,
  ArrowLeft,
  Check,
  Bug,
  ChevronDown,
  History,
  Languages,
  Pencil,
  Play,
  Plus,
  RotateCcw,
  RotateCw,
  Save,
  StepForward,
  SlidersHorizontal,
  Square,
  Upload,
  X,
} from 'lucide-vue-next'
import { t, type Locale } from '../../i18n'
import type { FlowConcurrencyMode, ProjectWorkspaceDto } from '../../api/flowApi'
import type { DockableWorkspaceMode } from '../../flow/dockableWorkspace'
import WorkspacePanelSwitcher, { type WorkspacePanelItem } from './WorkspacePanelSwitcher.vue'

const openMenu = ref<'versions' | 'policy' | null>(null)
const projectMenuRoot = ref<HTMLElement>()
const languageMenuRoot = ref<HTMLElement>()
const versionsMenuRoot = ref<HTMLElement>()
const policyMenuRoot = ref<HTMLElement>()
const projectTrigger = ref<HTMLButtonElement>()
const languageTrigger = ref<HTMLButtonElement>()
const versionsTrigger = ref<HTMLButtonElement>()
const policyTrigger = ref<HTMLButtonElement>()

const props = defineProps<{
  projectName: string
  flowVersion: number
  productionVersion?: number
  projectWorkspaces: ProjectWorkspaceDto[]
  projectId?: string
  projectMenuOpen: boolean
  projectRenameOpen: boolean
  projectNameDraft: string
  isProjectRenaming: boolean
  canUndo: boolean
  canRedo: boolean
  isDirty: boolean
  isSaving: boolean
  isWorkspaceLoading: boolean
  isRunning: boolean
  isDebugActive: boolean
  isDebugPaused: boolean
  isDebugStarting: boolean
  isDebugControlling: boolean
  isDebugStopping: boolean
  canStartDebug: boolean
  canViewVersions: boolean
  canManageVersions: boolean
  languageMenuOpen: boolean
  locale: Locale
  nodeCount: number
  workspaceView: 'console' | 'editor'
  concurrencyMode: FlowConcurrencyMode
  workspacePanelItems: WorkspacePanelItem[]
  workspaceDisplayMode: DockableWorkspaceMode
  debugDisplayModeAvailable: boolean
}>()

const emit = defineEmits<{
  'toggle-project-menu': []
  'begin-project-rename': []
  'cancel-project-rename': []
  'submit-project-rename': []
  'update:projectNameDraft': [value: string]
  'open-project': [workspace: ProjectWorkspaceDto]
  'start-new-project': []
  undo: []
  redo: []
  save: []
  run: []
  debug: []
  'debug-continue': []
  'debug-step': []
  'debug-stop': []
  'toggle-language-menu': []
  'set-language': [locale: Locale]
  'show-run-console': []
  'show-version-history': []
  'publish-version': []
  'update-concurrency-mode': [mode: FlowConcurrencyMode]
  'toggle-workspace-panel': [panelId: WorkspacePanelItem['id']]
  'reset-workspace-layout': []
  'change-workspace-mode': [mode: DockableWorkspaceMode]
}>()

function updateProjectNameDraft(event: Event): void {
  emit('update:projectNameDraft', (event.target as HTMLInputElement).value)
}

function selectConcurrencyMode(mode: FlowConcurrencyMode): void {
  openMenu.value = null
  emit('update-concurrency-mode', mode)
}

function closeProjectMenu(): void {
  if (props.projectRenameOpen) emit('cancel-project-rename')
  else emit('toggle-project-menu')
}

function closeOtherMenus(except?: 'project' | 'language' | 'versions' | 'policy'): void {
  if (except !== 'project' && props.projectMenuOpen) closeProjectMenu()
  if (except !== 'language' && props.languageMenuOpen) emit('toggle-language-menu')
  if (openMenu.value !== except) openMenu.value = null
}

function toggleProjectMenu(): void {
  if (props.projectMenuOpen) {
    closeProjectMenu()
    return
  }
  closeOtherMenus('project')
  emit('toggle-project-menu')
}

function toggleLanguageMenu(): void {
  closeOtherMenus('language')
  emit('toggle-language-menu')
}

function toggleMenu(menu: 'versions' | 'policy'): void {
  const shouldOpen = openMenu.value !== menu
  closeOtherMenus(menu)
  openMenu.value = shouldOpen ? menu : null
}

function onDocumentPointerdown(event: PointerEvent): void {
  const target = event.target
  if (!(target instanceof Node)) return
  if (props.projectMenuOpen && !projectMenuRoot.value?.contains(target)) closeProjectMenu()
  if (props.languageMenuOpen && !languageMenuRoot.value?.contains(target)) emit('toggle-language-menu')
  if (openMenu.value === 'versions' && !versionsMenuRoot.value?.contains(target)) openMenu.value = null
  if (openMenu.value === 'policy' && !policyMenuRoot.value?.contains(target)) openMenu.value = null
}

function onWindowKeydown(event: KeyboardEvent): void {
  if (event.key !== 'Escape') return
  if (props.projectMenuOpen) projectTrigger.value?.focus()
  if (props.languageMenuOpen) languageTrigger.value?.focus()
  if (openMenu.value === 'versions') versionsTrigger.value?.focus()
  if (openMenu.value === 'policy') policyTrigger.value?.focus()
  closeOtherMenus()
}

onMounted(() => {
  document.addEventListener('pointerdown', onDocumentPointerdown)
  window.addEventListener('keydown', onWindowKeydown)
})

onBeforeUnmount(() => {
  document.removeEventListener('pointerdown', onDocumentPointerdown)
  window.removeEventListener('keydown', onWindowKeydown)
})
</script>

<template>
  <header class="command-bar">
    <div class="brand-lockup">
      <div class="brand-mark" aria-hidden="true"><Activity :size="18" :stroke-width="2.4" /></div>
      <span class="brand-name">SereinFlow</span><span class="brand-divider" aria-hidden="true"></span>
      <span v-if="props.workspaceView === 'console'" class="command-console-label">{{ t('console.application') }}</span>
      <button v-else class="command-back" type="button" :title="t('command.showRunConsole')" :aria-label="t('command.showRunConsole')" @click="emit('show-run-console')"><ArrowLeft :size="15" /><span>{{ t('command.showRunConsole') }}</span></button>
      <div v-if="props.workspaceView === 'editor'" ref="projectMenuRoot" class="project-menu">
        <div class="project-picker-row">
          <button ref="projectTrigger" class="project-picker" type="button" :title="t('command.switchProject')" :aria-expanded="props.projectMenuOpen" aria-haspopup="dialog" @click="toggleProjectMenu">
            <span>{{ props.projectName }}</span><ChevronDown :size="14" />
          </button>
          <span class="version-pill version-pill--development" :title="t('version.track.development')">D v{{ props.flowVersion }}</span>
          <span class="version-pill version-pill--production" :title="t('version.track.production')">P {{ props.productionVersion ? `v${props.productionVersion}` : '—' }}</span>
        </div>
        <div v-if="props.projectMenuOpen" class="project-popover" role="dialog" :aria-label="t('project.switchProject')">
          <span class="project-popover__label">{{ t('project.switchProject') }}</span>
          <button v-for="workspace in props.projectWorkspaces" :key="workspace.project.id" type="button" :class="{ active: workspace.project.id === props.projectId }" @click="emit('open-project', workspace)">
            {{ workspace.project.name }}<span>{{ workspace.flows.length }} {{ t('project.flows') }}</span>
          </button>
          <span v-if="props.projectWorkspaces.length === 0" class="project-popover__empty">{{ t('project.noProjects') }}</span>
          <button class="project-popover__new" type="button" @click="emit('start-new-project')"><Plus :size="14" />{{ t('project.newProject') }}</button>
          <button class="project-popover__rename" type="button" :disabled="props.isProjectRenaming" @click="emit('begin-project-rename')"><Pencil :size="14" />{{ t('project.rename') }}</button>
          <form v-if="props.projectRenameOpen" class="project-rename-form" @submit.prevent="emit('submit-project-rename')">
            <label>{{ t('project.renameTitle') }}<input :value="props.projectNameDraft" type="text" :placeholder="t('project.renamePlaceholder')" maxlength="80" autofocus @input="updateProjectNameDraft" /></label>
            <div class="project-rename-form__actions">
              <button type="button" :title="t('command.cancel')" :aria-label="t('command.cancel')" @click="emit('cancel-project-rename')">
                <X :size="14" />
              </button>
              <button type="submit" :title="t('command.confirm')" :aria-label="t('command.confirm')" :disabled="props.isProjectRenaming">
                <Check :size="14" />
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
    <div class="command-actions">
      <template v-if="props.workspaceView === 'editor'">
        <div class="command-group command-group--edit" role="group" :aria-label="t('command.editActions')">
          <button class="icon-button" type="button" :title="t('command.undo')" :aria-label="t('command.undo')" :disabled="!props.canUndo" @click="emit('undo')"><RotateCcw :size="16" /></button>
          <button class="icon-button" type="button" :title="t('command.redo')" :aria-label="t('command.redo')" :disabled="!props.canRedo" @click="emit('redo')"><RotateCw :size="16" /></button>
        </div>
        <span class="command-divider" aria-hidden="true"></span>
        <button class="command-button quiet command-save" type="button" :title="t('command.save')" :aria-label="t('command.save')" :disabled="!props.isDirty || props.isSaving || props.isProjectRenaming || props.isWorkspaceLoading" @click="emit('save')"><Save :size="15" /><span>{{ t('command.save') }}</span></button>
        <div ref="versionsMenuRoot" class="command-popover-root">
          <button ref="versionsTrigger" class="command-button quiet command-menu-trigger" type="button" :title="t('command.versions')" :aria-label="t('command.versions')" :aria-expanded="openMenu === 'versions'" aria-haspopup="menu" @click="toggleMenu('versions')"><History :size="15" /><span>{{ t('command.versions') }}</span><ChevronDown :size="13" /></button>
          <div v-if="openMenu === 'versions'" class="command-menu" role="menu" :aria-label="t('command.versions')">
            <button type="button" role="menuitem" :disabled="!props.canViewVersions" @click="openMenu = null; emit('show-version-history')"><History :size="15" /><span>{{ t('version.historyTitle') }}</span></button>
            <button type="button" role="menuitem" :disabled="!props.canManageVersions" @click="openMenu = null; emit('publish-version')"><Upload :size="15" /><span>{{ t('version.publish') }}</span></button>
          </div>
        </div>
        <span class="command-divider" aria-hidden="true"></span>
        <button class="command-button run" type="button" :title="props.isRunning ? t('command.stop') : t('command.run')" :aria-label="props.isRunning ? t('command.stop') : t('command.run')" :aria-pressed="props.isRunning" :disabled="props.nodeCount === 0 || props.isWorkspaceLoading || props.isDebugActive" @click="emit('run')"><Square v-if="props.isRunning" :size="14" fill="currentColor" /><Play v-else :size="14" fill="currentColor" /><span>{{ props.isRunning ? t('command.stop') : t('command.run') }}</span></button>
        <template v-if="props.isDebugActive">
          <span class="command-debug-status" :class="{ paused: props.isDebugPaused }"><Bug :size="14" />{{ t(`debug.status.${props.isDebugStopping ? 'stopping' : props.isDebugPaused ? 'paused' : 'running'}`) }}</span>
          <button class="icon-button" type="button" :title="t('debug.continue')" :aria-label="t('debug.continue')" :disabled="!props.isDebugPaused || props.isDebugControlling || props.isDebugStopping" @click="emit('debug-continue')"><Play :size="16" fill="currentColor" /></button>
          <button class="icon-button" type="button" :title="t('debug.step')" :aria-label="t('debug.step')" :disabled="!props.isDebugPaused || props.isDebugControlling || props.isDebugStopping" @click="emit('debug-step')"><StepForward :size="17" /></button>
          <button class="icon-button icon-button--danger" type="button" :title="t('debug.stop')" :aria-label="t('debug.stop')" :disabled="props.isDebugControlling || props.isDebugStopping" @click="emit('debug-stop')"><Square :size="14" fill="currentColor" /></button>
        </template>
        <button v-else class="command-button debug" type="button" :title="t('command.debug')" :aria-label="t('command.debug')" :disabled="!props.canStartDebug || props.isDebugStarting" @click="emit('debug')"><Bug :size="15" /><span>{{ t('command.debug') }}</span></button>
        <div ref="policyMenuRoot" class="command-popover-root">
          <button ref="policyTrigger" class="icon-button command-policy-trigger" type="button" :title="t('flowPolicy.title')" :aria-label="t('flowPolicy.title')" :aria-expanded="openMenu === 'policy'" aria-haspopup="menu" @click="toggleMenu('policy')"><SlidersHorizontal :size="16" /></button>
          <div v-if="openMenu === 'policy'" class="flow-policy-popover" role="menu" :aria-label="t('flowPolicy.title')">
            <span>{{ t('flowPolicy.title') }}</span>
            <button type="button" role="menuitemradio" :aria-checked="props.concurrencyMode === 'parallel'" :class="{ active: props.concurrencyMode === 'parallel' }" @click="selectConcurrencyMode('parallel')"><strong>{{ t('flowPolicy.parallel') }}</strong><small>{{ t('flowPolicy.parallelHint') }}</small></button>
            <button type="button" role="menuitemradio" :aria-checked="props.concurrencyMode === 'exclusiveReject'" :class="{ active: props.concurrencyMode === 'exclusiveReject' }" @click="selectConcurrencyMode('exclusiveReject')"><strong>{{ t('flowPolicy.exclusiveReject') }}</strong><small>{{ t('flowPolicy.exclusiveRejectHint') }}</small></button>
          </div>
        </div>
        <span class="command-divider" aria-hidden="true"></span>
        <WorkspacePanelSwitcher
          :items="props.workspacePanelItems"
          :display-mode="props.workspaceDisplayMode"
          :debug-mode-available="props.debugDisplayModeAvailable"
          :show-display-modes="true"
          @toggle="emit('toggle-workspace-panel', $event)"
          @reset="emit('reset-workspace-layout')"
          @change-mode="emit('change-workspace-mode', $event)"
          @open="closeOtherMenus()"
        />
      </template>
      <div ref="languageMenuRoot" class="language-menu">
        <button ref="languageTrigger" class="language-button" type="button" :title="t('command.language')" :aria-label="t('command.language')" :aria-expanded="props.languageMenuOpen" aria-haspopup="menu" @click="toggleLanguageMenu"><Languages :size="16" /><span>{{ props.locale === 'zh-CN' ? 'ZH' : 'EN' }}</span><ChevronDown :size="13" /></button>
        <div v-if="props.languageMenuOpen" class="language-popover" role="menu"><button type="button" role="menuitemradio" :aria-checked="props.locale === 'zh-CN'" :class="{ active: props.locale === 'zh-CN' }" @click="emit('set-language', 'zh-CN')">{{ t('language.zh') }}</button><button type="button" role="menuitemradio" :aria-checked="props.locale === 'en-US'" :class="{ active: props.locale === 'en-US' }" @click="emit('set-language', 'en-US')">{{ t('language.en') }}</button></div>
      </div>
    </div>
  </header>
</template>
