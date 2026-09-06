import { onBeforeUnmount, onMounted, type Ref } from 'vue'

interface UseWorkspaceShortcutsOptions {
  canvasDeleteConfirmOpen: Ref<boolean>
  isEditorActive: Ref<boolean>
  cancelCanvasRemoval: () => void
  saveFlow: () => void
  undo: () => void
  redo: () => void
  selectAllNodes: () => void
}

function isTextEntryTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) {
    return false
  }

  return target.isContentEditable
    || target instanceof HTMLInputElement
    || target instanceof HTMLTextAreaElement
    || target instanceof HTMLSelectElement
}

export function useWorkspaceShortcuts(options: UseWorkspaceShortcutsOptions): void {
  function handleWorkspaceShortcut(event: KeyboardEvent): void {
    if (event.key === 'Escape' && options.canvasDeleteConfirmOpen.value) {
      event.preventDefault()
      options.cancelCanvasRemoval()
      return
    }

    const key = event.key.toLocaleLowerCase()
    const hasModifier = event.ctrlKey || event.metaKey

    if (hasModifier && key === 's') {
      event.preventDefault()
      options.saveFlow()
      return
    }

    if (isTextEntryTarget(event.target)) {
      return
    }

    if (options.isEditorActive.value && hasModifier && key === 'a') {
      event.preventDefault()
      options.selectAllNodes()
      return
    }

    if (hasModifier && key === 'z') {
      event.preventDefault()
      if (event.shiftKey) {
        options.redo()
      } else {
        options.undo()
      }
      return
    }

    if (hasModifier && key === 'y') {
      event.preventDefault()
      options.redo()
    }
  }

  onMounted(() => window.addEventListener('keydown', handleWorkspaceShortcut))
  onBeforeUnmount(() => window.removeEventListener('keydown', handleWorkspaceShortcut))
}
