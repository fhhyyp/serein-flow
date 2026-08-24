<script setup lang="ts">
import {
  Activity,
  Check,
  ChevronDown,
  Languages,
  Pencil,
  Play,
  Plus,
  RotateCcw,
  RotateCw,
  Save,
  Square,
  X,
} from 'lucide-vue-next'
import { t, type Locale } from '../../i18n'
import type { ProjectWorkspaceDto } from '../../api/flowApi'

const props = defineProps<{
  projectName: string
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
  languageMenuOpen: boolean
  locale: Locale
  nodeCount: number
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
  'toggle-language-menu': []
  'set-language': [locale: Locale]
}>()

function updateProjectNameDraft(event: Event): void {
  emit('update:projectNameDraft', (event.target as HTMLInputElement).value)
}
</script>

<template>
  <header class="command-bar">
    <div class="brand-lockup">
      <div class="brand-mark" aria-hidden="true"><Activity :size="18" :stroke-width="2.4" /></div>
      <span class="brand-name">SereinFlow</span><span class="brand-divider" aria-hidden="true"></span>
      <div class="project-menu">
        <div class="project-picker-row">
          <button class="project-picker" type="button" :title="t('command.switchProject')" :aria-expanded="props.projectMenuOpen" @click="emit('toggle-project-menu')">
            <span>{{ props.projectName }}</span><ChevronDown :size="14" />
          </button>
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
            <div class="project-rename-form__actions"><button type="button" :title="t('command.cancel')" :aria-label="t('command.cancel')" @click="emit('cancel-project-rename')"><X :size="14" /></button><button type="submit" :title="t('command.confirm')" :aria-label="t('command.confirm')" :disabled="props.isProjectRenaming"><Check :size="14" /></button></div>
          </form>
        </div>
      </div>
    </div>
    <div class="command-actions">
      <button class="icon-button" type="button" :title="t('command.undo')" :aria-label="t('command.undo')" :disabled="!props.canUndo" @click="emit('undo')"><RotateCcw :size="16" /></button>
      <button class="icon-button" type="button" :title="t('command.redo')" :aria-label="t('command.redo')" :disabled="!props.canRedo" @click="emit('redo')"><RotateCw :size="16" /></button><span class="command-divider" aria-hidden="true"></span>
      <button class="command-button quiet" type="button" :title="t('command.save')" :disabled="!props.isDirty || props.isSaving || props.isProjectRenaming || props.isWorkspaceLoading" @click="emit('save')"><Save :size="15" /><span>{{ t('command.save') }}</span></button>
      <button class="command-button run" type="button" :aria-pressed="props.isRunning" :disabled="props.nodeCount === 0 || props.isWorkspaceLoading" @click="emit('run')"><Square v-if="props.isRunning" :size="14" fill="currentColor" /><Play v-else :size="14" fill="currentColor" /><span>{{ props.isRunning ? t('command.stop') : t('command.run') }}</span></button>
      <div class="language-menu">
        <button class="language-button" type="button" :title="t('command.language')" :aria-label="t('command.language')" :aria-expanded="props.languageMenuOpen" @click="emit('toggle-language-menu')"><Languages :size="16" /><span>{{ props.locale === 'zh-CN' ? 'ZH' : 'EN' }}</span><ChevronDown :size="13" /></button>
        <div v-if="props.languageMenuOpen" class="language-popover" role="menu"><button type="button" role="menuitemradio" :aria-checked="props.locale === 'zh-CN'" :class="{ active: props.locale === 'zh-CN' }" @click="emit('set-language', 'zh-CN')">{{ t('language.zh') }}</button><button type="button" role="menuitemradio" :aria-checked="props.locale === 'en-US'" :class="{ active: props.locale === 'en-US' }" @click="emit('set-language', 'en-US')">{{ t('language.en') }}</button></div>
      </div>
      <button class="avatar" type="button" :title="t('command.workspaceSettings')" :aria-label="t('command.workspaceSettings')">SF</button>
    </div>
  </header>
</template>
