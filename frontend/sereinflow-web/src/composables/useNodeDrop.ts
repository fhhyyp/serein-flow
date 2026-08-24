import type { Ref } from 'vue'
import type { LibraryNodeDto } from '../api/libraryApi'
import { isNodeKind } from '../flow/nodeCatalog'
import type { MethodParameter, NodeKind, NodeRuntimeMetadata } from '../flow/types'
import { t } from '../i18n'

interface NodeDropOptions {
  screenToFlowCoordinate: (position: { x: number; y: number }) => { x: number; y: number }
  isCanvasDropActive: Ref<boolean>
  notice: Ref<string>
  addNode: (kind: NodeKind, titleKey: string, subtitleKey: string, position?: { x: number; y: number }, metadata?: { displayName?: string; description?: string; runtime?: NodeRuntimeMetadata; parameters?: MethodParameter[]; hasDataOutput?: boolean }) => void
}

export function useNodeDrop(options: NodeDropOptions) {
  function handleCanvasDragOver(event: DragEvent): void {
    if (!event.dataTransfer?.types.includes('application/sereinflow-node')) {
      return
    }

    event.preventDefault()
    event.dataTransfer.dropEffect = 'copy'
    options.isCanvasDropActive.value = true
  }

  function handleCanvasDragLeave(event: DragEvent): void {
    const currentTarget = event.currentTarget as HTMLElement | null
    const relatedTarget = event.relatedTarget as Node | null
    if (!currentTarget || (relatedTarget && currentTarget.contains(relatedTarget))) {
      return
    }

    options.isCanvasDropActive.value = false
  }

  function handleCanvasDrop(event: DragEvent): void {
    event.preventDefault()
    options.isCanvasDropActive.value = false
    const encoded = event.dataTransfer?.getData('application/sereinflow-node')
    if (!encoded) {
      return
    }

    try {
      const item = JSON.parse(encoded) as {
        kind: NodeKind
        titleKey: string
        subtitleKey: string
        displayName?: string
        description?: string
        runtime?: NodeRuntimeMetadata
        parameters?: MethodParameter[]
        hasDataOutput?: boolean
      }
      if (!isNodeKind(item.kind)) {
        return
      }

      const position = options.screenToFlowCoordinate({ x: event.clientX, y: event.clientY })
      options.addNode(item.kind, item.titleKey, item.subtitleKey, { x: Math.round(position.x / 16) * 16, y: Math.round(position.y / 16) * 16 }, item)
    } catch {
      options.notice.value = t('canvas.invalidNodeDrop')
    }
  }

  function handleLibraryNodeDragStart(event: DragEvent, node: LibraryNodeDto): void {
    if (!event.dataTransfer) {
      return
    }

    const runtime: NodeRuntimeMetadata = {
      category: 'method',
      libraryId: node.libraryId,
      className: node.className,
      methodName: node.methodName,
      dllName: node.dllName,
      dllVersion: node.dllVersion,
      returnType: node.returnType,
    }
    const parameters = node.parameters.map((parameter) => ({
      id: parameter.id,
      nameKey: parameter.name,
      name: parameter.name,
      valueKind: parameter.type || 'System.Object',
      type: parameter.type,
      description: parameter.description ?? undefined,
      source: 'literal' as const,
      inputMode: 'manual' as const,
      literalValue: '',
    }))
    event.dataTransfer.setData('application/sereinflow-node', JSON.stringify({
      kind: isNodeKind(node.type) ? node.type : 'action',
      titleKey: 'node.catalogMethod',
      subtitleKey: 'node.catalogSubtitle',
      displayName: node.displayName,
      description: node.description ?? `${node.className}.${node.methodName}`,
      runtime,
      parameters,
      hasDataOutput: node.returnType !== 'System.Void',
    }))
    event.dataTransfer.effectAllowed = 'copy'
  }

  return { handleCanvasDragOver, handleCanvasDragLeave, handleCanvasDrop, handleLibraryNodeDragStart }
}
