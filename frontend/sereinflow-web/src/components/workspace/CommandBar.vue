<script setup lang="ts">
import { ref } from 'vue'
import {
  Activity,
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
  Settings,
  Square,
  Upload,
  X,
} from 'lucide-vue-next'
import { t, type Locale } from '../../i18n'
import type { FlowConcurrencyMode, ProjectWorkspaceDto } from '../../api/flowApi'
import WorkspacePanelSwitcher, { type WorkspacePanelItem } from './WorkspacePanelSwitcher.vue'

const policyMenuOpen = ref(false)

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
}>()

function updateProjectNameDraft(event: Event): void {
  emit('update:projectNameDraft', (event.target as HTMLInputElement).value)
}

function selectConcurrencyMode(mode: FlowConcurrencyMode): void {
  policyMenuOpen.value = false
  emit('update-concurrency-mode', mode)
}
</script>

<template>
  <header class="command-bar">
    <div class="brand-lockup">
      <div class="brand-mark" aria-hidden="true"><Activity :size="18" :stroke-width="2.4" /></div>
      <span class="brand-name">SereinFlow</span><span class="brand-divider" aria-hidden="true"></span>
      <span v-if="props.workspaceView === 'console'" class="command-console-label">{{ t('console.application') }}</span>
      <div v-else class="project-menu">
        <div class="project-picker-row">
          <button class="project-picker" type="button" :title="t('command.switchProject')" :aria-expanded="props.projectMenuOpen" @click="emit('toggle-project-menu')">
            <span>{{ props.projectName }}</span><ChevronDown :size="14" />
          </button>
          <span class="version-pill version-pill--development" :title="t('version.track.development')">D v{{ props.flowVersion }}</span>
          <span class="version-pill version-pill--production" :title="t('version.track.production')">P {{ props.productionVersion ? `v${props.productionVersion}` : '—' }}</span>
          <button class="project-rename-button" type="button" :title="t('project.rename')" :aria-label="t('project.rename')" :disabled="props.isProjectRenaming" @click="emit('begin-project-rename')"><Pencil :size="13" /></button>
        </div>
        <div v-if="props.projectMenuOpen" class="project-popover" role="menu">
          <span class="project-popover__label">{{ t('project.switchProject') }}</span>
          <button v-for="workspace in props.projectWorkspaces" :key="workspace.project.id" type="button" role="menuitem" :class="{ active: workspace.project.id === props.projectId }" @click="emit('open-project', workspace)">
            {{ workspace.project.name }}<span>{{ workspace.flows.length }} {{ t('project.flows') }}</span>
          </button>
          <span v-if="props.projectWorkspaces.length === 0" class="project-popover__empty">{{ t('project.noProjects') }}</span>
          <button class="project-popover__new" type="button" role="menuitem" @click="emit('start-new-project')"><Plus :size="14" />{{ t('project.newProject') }}</button>
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
        <button class="icon-button" type="button" :title="t('command.showRunConsole')" :aria-label="t('command.showRunConsole')" @click="emit('show-run-console')"><Activity :size="16" /></button><span class="command-divider" aria-hidden="true"></span>
        <button class="icon-button" type="button" :title="t('command.undo')" :aria-label="t('command.undo')" :disabled="!props.canUndo" @click="emit('undo')"><RotateCcw :size="16" /></button>
        <button class="icon-button" type="button" :title="t('command.redo')" :aria-label="t('command.redo')" :disabled="!props.canRedo" @click="emit('redo')"><RotateCw :size="16" /></button><span class="command-divider" aria-hidden="true"></span>
        <button class="command-button quiet" type="button" :title="t('command.save')" :disabled="!props.isDirty || props.isSaving || props.isProjectRenaming || props.isWorkspaceLoading" @click="emit('save')"><Save :size="15" /><span>{{ t('command.save') }}</span></button>
        <button class="icon-button" type="button" :title="t('version.historyTitle')" :aria-label="t('version.historyTitle')" :disabled="!props.canViewVersions" @click="emit('show-version-history')"><History :size="16" /></button>
        <button class="icon-button" type="button" :title="t('version.publish')" :aria-label="t('version.publish')" :disabled="!props.canManageVersions" @click="emit('publish-version')"><Upload :size="16" /></button><span class="command-divider" aria-hidden="true"></span>
        <button class="command-button run" type="button" :aria-pressed="props.isRunning" :disabled="props.nodeCount === 0 || props.isWorkspaceLoading || props.isDebugActive" @click="emit('run')"><Square v-if="props.isRunning" :size="14" fill="currentColor" /><Play v-else :size="14" fill="currentColor" /><span>{{ props.isRunning ? t('command.stop') : t('command.run') }}</span></button>
        <template v-if="props.isDebugActive">
          <span class="command-debug-status" :class="{ paused: props.isDebugPaused }"><Bug :size="14" />{{ t(`debug.status.${props.isDebugStopping ? 'stopping' : props.isDebugPaused ? 'paused' : 'running'}`) }}</span>
          <button class="icon-button" type="button" :title="t('debug.continue')" :aria-label="t('debug.continue')" :disabled="!props.isDebugPaused || props.isDebugControlling || props.isDebugStopping" @click="emit('debug-continue')"><Play :size="16" fill="currentColor" /></button>
          <button class="icon-button" type="button" :title="t('debug.step')" :aria-label="t('debug.step')" :disabled="!props.isDebugPaused || props.isDebugControlling || props.isDebugStopping" @click="emit('debug-step')"><StepForward :size="17" /></button>
          <button class="icon-button icon-button--danger" type="button" :title="t('debug.stop')" :aria-label="t('debug.stop')" :disabled="props.isDebugControlling || props.isDebugStopping" @click="emit('debug-stop')"><Square :size="14" fill="currentColor" /></button>
        </template>
        <button v-else class="command-button debug" type="button" :disabled="!props.canStartDebug || props.isDebugStarting" @click="emit('debug')"><Bug :size="15" /><span>{{ t('command.debug') }}</span></button>
        <WorkspacePanelSwitcher
          :items="props.workspacePanelItems"
          @toggle="emit('toggle-workspace-panel', $event)"
          @reset="emit('reset-workspace-layout')"
        />
        <div class="workspace-settings">
          <button class="icon-button" type="button" :title="t('command.workspaceSettings')" :aria-label="t('command.workspaceSettings')" :aria-expanded="policyMenuOpen" @click="policyMenuOpen = !policyMenuOpen"><Settings :size="16" /></button>
          <div v-if="policyMenuOpen" class="workspace-settings__popover" role="menu">
            <span>{{ t('flowPolicy.title') }}</span>
            <button type="button" role="menuitemradio" :aria-checked="props.concurrencyMode === 'parallel'" :class="{ active: props.concurrencyMode === 'parallel' }" @click="selectConcurrencyMode('parallel')"><strong>{{ t('flowPolicy.parallel') }}</strong><small>{{ t('flowPolicy.parallelHint') }}</small></button>
            <button type="button" role="menuitemradio" :aria-checked="props.concurrencyMode === 'exclusiveReject'" :class="{ active: props.concurrencyMode === 'exclusiveReject' }" @click="selectConcurrencyMode('exclusiveReject')"><strong>{{ t('flowPolicy.exclusiveReject') }}</strong><small>{{ t('flowPolicy.exclusiveRejectHint') }}</small></button>
          </div>
        </div>
      </template>
      <div class="language-menu">
        <button class="language-button" type="button" :title="t('command.language')" :aria-label="t('command.language')" :aria-expanded="props.languageMenuOpen" @click="emit('toggle-language-menu')"><Languages :size="16" /><span>{{ props.locale === 'zh-CN' ? 'ZH' : 'EN' }}</span><ChevronDown :size="13" /></button>
        <div v-if="props.languageMenuOpen" class="language-popover" role="menu"><button type="button" role="menuitemradio" :aria-checked="props.locale === 'zh-CN'" :class="{ active: props.locale === 'zh-CN' }" @click="emit('set-language', 'zh-CN')">{{ t('language.zh') }}</button><button type="button" role="menuitemradio" :aria-checked="props.locale === 'en-US'" :class="{ active: props.locale === 'en-US' }" @click="emit('set-language', 'en-US')">{{ t('language.en') }}</button></div>
      </div>
    </div>
  </header>
</template>
