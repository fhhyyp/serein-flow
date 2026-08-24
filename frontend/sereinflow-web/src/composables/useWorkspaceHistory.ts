import { ref } from 'vue'
import { WorkspaceHistory, type WorkspaceSnapshot } from '../flow/workspaceHistory'

interface UseWorkspaceHistoryOptions {
  currentSnapshot: () => WorkspaceSnapshot
  restoreSnapshot: (snapshot: WorkspaceSnapshot) => void
  markWorkspaceChanged: () => void
}

export function useWorkspaceHistory(options: UseWorkspaceHistoryOptions) {
  const history = new WorkspaceHistory()
  const canUndo = ref(false)
  const canRedo = ref(false)
  let pendingTextEdit: WorkspaceSnapshot | undefined

  function syncHistoryAvailability(): void {
    canUndo.value = history.canUndo
    canRedo.value = history.canRedo
  }

  function recordWorkspaceMutation(): void {
    history.record(options.currentSnapshot())
    syncHistoryAvailability()
  }

  function undo(): boolean {
    const previous = history.undo(options.currentSnapshot())
    if (!previous) {
      return false
    }

    options.restoreSnapshot(previous)
    syncHistoryAvailability()
    return true
  }

  function redo(): boolean {
    const next = history.redo(options.currentSnapshot())
    if (!next) {
      return false
    }

    options.restoreSnapshot(next)
    syncHistoryAvailability()
    return true
  }

  function beginTextEdit(): void {
    pendingTextEdit ??= options.currentSnapshot()
  }

  function commitTextEdit(): void {
    if (pendingTextEdit) {
      history.record(pendingTextEdit)
      pendingTextEdit = undefined
      syncHistoryAvailability()
    }
    options.markWorkspaceChanged()
  }

  function discardTextEdit(): void {
    pendingTextEdit = undefined
  }

  function clear(): void {
    history.clear()
    pendingTextEdit = undefined
    syncHistoryAvailability()
  }

  return {
    canUndo,
    canRedo,
    recordWorkspaceMutation,
    undo,
    redo,
    beginTextEdit,
    commitTextEdit,
    discardTextEdit,
    syncHistoryAvailability,
    clear,
  }
}
