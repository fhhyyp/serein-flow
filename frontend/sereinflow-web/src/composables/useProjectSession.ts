import type { Ref } from 'vue'
import { FlowApiError, createProject, listProjects, loadFlow, renameProject, saveFlow as saveFlowRequest, type ProjectWorkspaceDto } from '../api/flowApi'
import { t } from '../i18n'
import type { ConnectionLineSettings } from '../flow/connectionLine'
import { normalizeConnectionLineTypes } from '../flow/connectionLine'
import { flowDefinitionToWorkspace, workspaceToFlowDefinition } from '../flow/flowDtoMapper'
import { createInitialCanvases } from '../flow/initialCanvases'
import type { CanvasState } from '../flow/types'
import { saveWorkspace } from '../flow/workspaceStorage'
import type { WorkspaceSnapshot } from '../flow/workspaceHistory'

interface UseProjectSessionOptions {
  recoveryWorkspace?: WorkspaceSnapshot
  canvases: Ref<CanvasState[]>
  activeCanvasId: Ref<string>
  nextNodeNumber: Ref<number>
  connectionLineTypes: ConnectionLineSettings
  projectId: Ref<string | undefined>
  flowId: Ref<string | undefined>
  projectWorkspaces: Ref<ProjectWorkspaceDto[]>
  projectName: Ref<string>
  projectNameDraft: Ref<string>
  projectMenuOpen: Ref<boolean>
  projectRenameOpen: Ref<boolean>
  isProjectRenaming: Ref<boolean>
  projectVersion: Ref<number>
  flowVersion: Ref<number>
  isWorkspaceLoading: Ref<boolean>
  isSaving: Ref<boolean>
  isDirty: Ref<boolean>
  saveFailed: Ref<boolean>
  saveConflict: Ref<boolean>
  savedWorkspaceFingerprint: Ref<string>
  notice: Ref<string>
  currentWorkspaceSnapshot: () => WorkspaceSnapshot
  restoreWorkspace: (snapshot: WorkspaceSnapshot) => void
  refreshDirtyState: () => void
  markWorkspaceChanged: () => void
  recordWorkspaceMutation: () => void
  clearHistory: () => void
  syncHistoryAvailability: () => void
  localizeEdges: () => void
}

export function useProjectSession(options: UseProjectSessionOptions) {
  function saveRecoveryDraft(snapshot: WorkspaceSnapshot): void {
    try {
      saveWorkspace(snapshot)
    } catch {
      // A recovery draft must not mask a server save result.
      // 恢复草稿不能覆盖服务端保存结果。
    }
  }

  function updateSavedProjectName(name: string): void {
    if (!options.savedWorkspaceFingerprint.value) {
      return
    }

    try {
      const savedSnapshot = JSON.parse(options.savedWorkspaceFingerprint.value) as WorkspaceSnapshot
      savedSnapshot.projectName = name
      options.savedWorkspaceFingerprint.value = JSON.stringify(savedSnapshot)
    } catch {
      // A malformed recovery fingerprint should not block the rename itself.
      // 格式错误的恢复指纹不应阻止项目重命名本身。
    }
  }

  function beginProjectRename(): void {
    options.projectNameDraft.value = options.projectName.value
    options.projectRenameOpen.value = true
    options.projectMenuOpen.value = true
  }

  function cancelProjectRename(): void {
    options.projectRenameOpen.value = false
    options.projectMenuOpen.value = false
    options.projectNameDraft.value = ''
  }

  async function submitProjectRename(): Promise<void> {
    const nextName = options.projectNameDraft.value.trim()
    if (!nextName) {
      options.notice.value = t('project.renameRequired')
      return
    }

    if (nextName === options.projectName.value.trim()) {
      cancelProjectRename()
      return
    }

    if (!options.projectId.value) {
      options.recordWorkspaceMutation()
      options.projectName.value = nextName
      cancelProjectRename()
      saveRecoveryDraft(options.currentWorkspaceSnapshot())
      options.markWorkspaceChanged()
      options.notice.value = t('project.renamedLocal')
      return
    }

    options.isProjectRenaming.value = true
    try {
      const workspace = await renameProject(options.projectId.value, {
        name: nextName,
        expectedVersion: options.projectVersion.value,
      })
      options.projectName.value = workspace.project.name
      options.projectVersion.value = workspace.project.version
      options.projectWorkspaces.value = options.projectWorkspaces.value.map((item) => item.project.id === workspace.project.id ? workspace : item)
      updateSavedProjectName(options.projectName.value)
      options.refreshDirtyState()
      cancelProjectRename()
      options.notice.value = t('project.renamed')
    } catch (error) {
      options.notice.value = error instanceof FlowApiError && error.status === 409
        ? t('project.renameConflict')
        : t('project.renameFailed')
    } finally {
      options.isProjectRenaming.value = false
    }
  }

  async function saveFlow(): Promise<void> {
    if (options.isSaving.value || options.isWorkspaceLoading.value) {
      return
    }

    const snapshot = options.currentWorkspaceSnapshot()
    const snapshotFingerprint = JSON.stringify(snapshot)
    saveRecoveryDraft(snapshot)
    options.isSaving.value = true
    options.saveFailed.value = false
    options.saveConflict.value = false

    try {
      let savedDefinition
      if (!options.projectId.value || !options.flowId.value) {
        const newFlowId = crypto.randomUUID()
        const definition = workspaceToFlowDefinition(snapshot, { id: newFlowId, version: 1 })
        const workspace = await createProject({ name: options.projectName.value, definition })
        options.projectId.value = workspace.project.id
        options.projectName.value = workspace.project.name
        options.projectVersion.value = workspace.project.version
        options.flowId.value = newFlowId
        savedDefinition = definition
        options.projectWorkspaces.value = [
          ...options.projectWorkspaces.value.filter((item) => item.project.id !== workspace.project.id),
          { project: workspace.project, flows: [{ id: newFlowId, version: savedDefinition.version, entryNodeId: savedDefinition.entryNodeId }] },
        ]
      } else {
        const definition = workspaceToFlowDefinition(snapshot, { id: options.flowId.value, version: options.flowVersion.value })
        savedDefinition = await saveFlowRequest(options.projectId.value, options.flowId.value, {
          expectedVersion: options.flowVersion.value,
          definition,
        })
      }

      options.flowVersion.value = savedDefinition.version
      options.projectWorkspaces.value = options.projectWorkspaces.value.map((item) => item.project.id === options.projectId.value
        ? {
            ...item,
            project: { ...item.project, name: options.projectName.value, updatedAt: new Date().toISOString() },
            flows: item.flows.some((flow) => flow.id === options.flowId.value)
              ? item.flows.map((flow) => flow.id === options.flowId.value ? { ...flow, version: savedDefinition.version, entryNodeId: savedDefinition.entryNodeId } : flow)
              : [...item.flows, { id: options.flowId.value!, version: savedDefinition.version, entryNodeId: savedDefinition.entryNodeId }],
          }
        : item)
      options.savedWorkspaceFingerprint.value = snapshotFingerprint
      options.refreshDirtyState()
      options.notice.value = t('canvas.savedNow')
    } catch (error) {
      if (error instanceof FlowApiError && error.status === 409) {
        options.saveConflict.value = true
        options.notice.value = t('canvas.saveConflictNow')
      } else {
        options.saveFailed.value = true
        options.notice.value = t('canvas.saveFailed')
      }
    } finally {
      options.isSaving.value = false
    }
  }

  function applyServerWorkspace(definition: ReturnType<typeof workspaceToFlowDefinition>): void {
    const workspace = { ...flowDefinitionToWorkspace(definition), projectName: options.projectName.value }
    options.restoreWorkspace(workspace)
    options.clearHistory()
    options.savedWorkspaceFingerprint.value = JSON.stringify(workspace)
    options.isDirty.value = false
    options.saveFailed.value = false
    options.saveConflict.value = false
    options.localizeEdges()
  }

  async function openProject(workspace: ProjectWorkspaceDto, requestedFlowId?: string): Promise<void> {
    const summary = requestedFlowId
      ? workspace.flows.find((flow) => flow.id === requestedFlowId) ?? workspace.flows[0]
      : workspace.flows[0]
    if (!summary) {
      options.notice.value = t('project.noFlow')
      return
    }

    options.projectRenameOpen.value = false
    options.projectMenuOpen.value = false
    options.isWorkspaceLoading.value = true
    try {
      const definition = await loadFlow(workspace.project.id, summary.id)
      options.projectId.value = workspace.project.id
      options.projectName.value = workspace.project.name
      options.projectVersion.value = workspace.project.version
      options.flowId.value = definition.id
      options.flowVersion.value = definition.version
      applyServerWorkspace(definition)
      options.notice.value = t('project.switched', { project: workspace.project.name })
    } catch {
      options.notice.value = t('canvas.loadFailed')
    } finally {
      options.isWorkspaceLoading.value = false
    }
  }

  function startNewProject(): void {
    options.projectRenameOpen.value = false
    options.projectMenuOpen.value = false
    options.projectId.value = undefined
    options.flowId.value = undefined
    options.flowVersion.value = 1
    options.projectVersion.value = 1
    options.projectName.value = t('project.newProject')
    options.restoreWorkspace({
      canvases: createInitialCanvases(),
      activeCanvasId: 'main',
      nextNodeNumber: 1,
      projectName: options.projectName.value,
      runPolicy: { concurrencyMode: 'parallel' },
    })
    options.clearHistory()
    options.savedWorkspaceFingerprint.value = ''
    options.isDirty.value = true
    options.saveFailed.value = false
    options.saveConflict.value = false
    options.notice.value = t('project.newProjectStarted')
  }

  async function initializeWorkspace(): Promise<void> {
    options.isWorkspaceLoading.value = true
    try {
      const workspaces = await listProjects()
      options.projectWorkspaces.value = workspaces
      const workspace = workspaces[0]
      if (workspace?.flows[0]) {
        const definition = await loadFlow(workspace.project.id, workspace.flows[0].id)
        options.projectId.value = workspace.project.id
        options.projectName.value = workspace.project.name
        options.projectVersion.value = workspace.project.version
        options.flowId.value = definition.id
        options.flowVersion.value = definition.version
        applyServerWorkspace(definition)
        options.notice.value = t('canvas.loadedFromServer')
        return
      }

      options.projectId.value = undefined
      options.flowId.value = undefined
      options.flowVersion.value = 1
      options.projectVersion.value = 1
      options.projectName.value = t('project.newProject')
      const snapshot: WorkspaceSnapshot = {
        canvases: createInitialCanvases(),
        activeCanvasId: 'main',
        nextNodeNumber: 1,
        projectName: options.projectName.value,
        connectionLineTypes: normalizeConnectionLineTypes(),
        runPolicy: { concurrencyMode: 'parallel' },
      }
      options.restoreWorkspace(snapshot)
      options.clearHistory()
      options.syncHistoryAvailability()
      options.savedWorkspaceFingerprint.value = JSON.stringify(snapshot)
      options.isDirty.value = false
      options.saveFailed.value = false
      options.saveConflict.value = false
      options.notice.value = ''
    } catch {
      if (options.recoveryWorkspace) {
        options.restoreWorkspace(options.recoveryWorkspace)
        options.savedWorkspaceFingerprint.value = ''
        options.isDirty.value = true
        options.notice.value = t('canvas.recoveredDraft')
      } else {
        options.isDirty.value = true
        options.saveFailed.value = true
        options.notice.value = t('canvas.loadFailed')
      }
    } finally {
      options.isWorkspaceLoading.value = false
    }
  }

  return {
    beginProjectRename,
    cancelProjectRename,
    submitProjectRename,
    saveFlow,
    applyServerWorkspace,
    openProject,
    startNewProject,
    initializeWorkspace,
  }
}
