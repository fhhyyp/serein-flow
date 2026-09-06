import { onBeforeUnmount, ref, watch, type Ref } from 'vue'
import {
  subscribeWorkspaceChanges,
  type WorkspaceChangeEventDto,
} from '../api/flowApi'
import { t } from '../i18n'

interface UseWorkspaceSyncOptions {
  projectId: Ref<string | undefined>
  flowId: Ref<string | undefined>
  flowVersion: Ref<number>
  isDirty: Ref<boolean>
  isSaving: Ref<boolean>
  isWorkspaceLoading: Ref<boolean>
  notice: Ref<string>
  reloadCurrentFlow: () => Promise<boolean>
  refreshLibraryCatalog: (projectId?: string) => Promise<void>
}

/**
 * Keeps the editor aligned with mutations committed through MCP or another
 * browser. Events invalidate local state; the server flow and catalog APIs
 * remain authoritative.
 * 让编辑器跟随 MCP 或其它浏览器提交的修改。事件只使本地状态失效，服务器流程和类库
 * API 仍然是权威数据源。
 */
export function useWorkspaceSync(options: UseWorkspaceSyncOptions) {
  const isConnected = ref(false)
  const remoteChangePending = ref(false)
  const remoteChangeVersion = ref<number>()
  const libraryRefreshRevision = ref(0)

  let unsubscribe: (() => void) | undefined
  let reconnectNeedsReconcile = false
  let flowReconcileInFlight = false
  let libraryRefreshInFlight: Promise<void> | undefined
  let libraryRefreshQueued = false

  function isProjectEventForCurrentProject(event: WorkspaceChangeEventDto): boolean {
    return !event.projectId || event.projectId === options.projectId.value
  }

  function refreshLibraries(): void {
    if (!options.projectId.value) return
    if (libraryRefreshInFlight) {
      libraryRefreshQueued = true
      return
    }

    libraryRefreshInFlight = options.refreshLibraryCatalog(options.projectId.value)
      .catch(() => {
        options.notice.value = t('sync.libraryRefreshFailed')
      })
      .finally(() => {
        libraryRefreshInFlight = undefined
        if (libraryRefreshQueued) {
          libraryRefreshQueued = false
          refreshLibraries()
        }
      })
  }

  async function reconcileFlow(): Promise<void> {
    if (flowReconcileInFlight
      || !reconnectNeedsReconcile
      || !options.projectId.value
      || !options.flowId.value
      || options.isDirty.value
      || options.isSaving.value
      || options.isWorkspaceLoading.value) {
      return
    }

    flowReconcileInFlight = true
    const requestedRemoteVersion = remoteChangeVersion.value
    let needsAnotherReconcile = false
    try {
      const reloaded = await options.reloadCurrentFlow()
      if (reloaded) {
        const newerEventArrived = remoteChangeVersion.value !== requestedRemoteVersion
          && remoteChangeVersion.value !== undefined
          && remoteChangeVersion.value > options.flowVersion.value
        if (!newerEventArrived) {
          reconnectNeedsReconcile = false
          remoteChangePending.value = false
          remoteChangeVersion.value = undefined
        } else {
          needsAnotherReconcile = true
        }
      }
    } catch {
      options.notice.value = t('sync.flowRefreshFailed')
    } finally {
      flowReconcileInFlight = false
      if (needsAnotherReconcile) {
        void reconcileFlow()
      }
    }
  }

  function handleChange(event: WorkspaceChangeEventDto): void {
    if (!isProjectEventForCurrentProject(event)) return

    const changesLibrarySurface = event.changeType === 'library.catalog.changed'
      || event.changeType === 'project.library.changed'
      || event.operation === 'library.upgrade'
    if (changesLibrarySurface) {
      libraryRefreshRevision.value += 1
      refreshLibraries()
    }

    if (event.changeType !== 'flow.changed'
      || event.flowId !== options.flowId.value) {
      return
    }

    if (event.version && event.version <= options.flowVersion.value) {
      return
    }

    reconnectNeedsReconcile = true
    remoteChangeVersion.value = event.version ?? undefined
    if (options.isDirty.value || options.isSaving.value) {
      remoteChangePending.value = true
      options.notice.value = t('sync.flowChangedWhileEditing')
      return
    }

    options.notice.value = event.origin === 'mcp'
      ? t('sync.flowChangedByAi')
      : t('sync.flowChanged')
    void reconcileFlow()
  }

  function handleConnected(): void {
    isConnected.value = true
    reconnectNeedsReconcile = true
    void reconcileFlow()
  }

  function handleError(): void {
    isConnected.value = false
  }

  const stopProjectWatch = watch(options.projectId, (nextProjectId) => {
    unsubscribe?.()
    unsubscribe = undefined
    isConnected.value = false
    reconnectNeedsReconcile = false
    remoteChangePending.value = false
    remoteChangeVersion.value = undefined

    if (!nextProjectId) return

    unsubscribe = subscribeWorkspaceChanges(
      nextProjectId,
      handleChange,
      handleConnected,
      handleError,
    )
  }, { immediate: true })

  const stopReconcileWatch = watch(
    [options.isWorkspaceLoading, options.isSaving, options.isDirty, options.flowVersion],
    () => { void reconcileFlow() },
  )

  onBeforeUnmount(() => {
    stopProjectWatch()
    stopReconcileWatch()
    unsubscribe?.()
    unsubscribe = undefined
  })

  return {
    isConnected,
    remoteChangePending,
    remoteChangeVersion,
    libraryRefreshRevision,
  }
}
