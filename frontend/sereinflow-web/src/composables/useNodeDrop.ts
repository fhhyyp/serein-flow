import { onBeforeUnmount, type Ref } from 'vue'
import type { NodeCreationDescriptorDto } from '../api/flowApi'
import type { LibraryNodeDto } from '../api/libraryApi'
import { isNodeKind } from '../flow/nodeCatalog'
import { canonicalParameterId } from '../flow/connectionSeats'
import { initialLibraryParameterLiteral } from '../flow/libraryParameterDefaults'
import type { MethodParameter, NodeKind, NodeRuntimeMetadata, ScriptNodeData } from '../flow/types'
import { t } from '../i18n'

interface NodeDropOptions {
  screenToFlowCoordinate: (position: { x: number; y: number }) => { x: number; y: number }
  isCanvasDropActive: Ref<boolean>
  notice: Ref<string>
  addNode: (kind: NodeKind, titleKey: string, subtitleKey: string, position?: { x: number; y: number }, metadata?: { displayName?: string; description?: string; runtime?: NodeRuntimeMetadata; parameters?: MethodParameter[]; script?: ScriptNodeData; hasDataOutput?: boolean }) => void
}

const nodeDragMimeType = 'application/sereinflow-node'

interface NodeDropPayload {
  kind: NodeKind
  titleKey: string
  subtitleKey: string
  displayName?: string
  description?: string
  runtime?: NodeRuntimeMetadata
  parameters?: MethodParameter[]
  script?: ScriptNodeData
  hasDataOutput?: boolean
}

export function useNodeDrop(options: NodeDropOptions) {
  let pointerId: number | undefined
  let pointerSource: HTMLElement | undefined
  let pointerStart: { x: number; y: number } | undefined
  let pointerPayload: NodeDropPayload | undefined
  let pointerDragging = false
  let previousBodyCursor = ''
  let previousBodyUserSelect = ''

  function isCanvasPoint(x: number, y: number): boolean {
    const target = document.elementFromPoint(x, y)
    return target instanceof Element && target.closest('.canvas-area') !== null
  }

  function clearPointerDrag(): void {
    if (pointerId !== undefined && pointerSource?.hasPointerCapture(pointerId)) {
      pointerSource.releasePointerCapture(pointerId)
    }

    window.removeEventListener('pointermove', handlePointerMove)
    window.removeEventListener('pointerup', handlePointerUp)
    window.removeEventListener('pointercancel', handlePointerCancel)
    window.removeEventListener('keydown', handlePointerKeyDown)
    document.body.style.cursor = previousBodyCursor
    document.body.style.userSelect = previousBodyUserSelect
    pointerId = undefined
    pointerSource = undefined
    pointerStart = undefined
    pointerPayload = undefined
    pointerDragging = false
    options.isCanvasDropActive.value = false
  }

  function handlePointerMove(event: PointerEvent): void {
    if (event.pointerId !== pointerId || !pointerPayload || !pointerStart) {
      return
    }

    const distance = Math.hypot(event.clientX - pointerStart.x, event.clientY - pointerStart.y)
    if (!pointerDragging && distance < 5) {
      return
    }

    pointerDragging = true
    options.isCanvasDropActive.value = isCanvasPoint(event.clientX, event.clientY)
  }

  function handlePointerUp(event: PointerEvent): void {
    if (event.pointerId !== pointerId || !pointerPayload) {
      return
    }

    const payload = pointerPayload
    const shouldCreate = pointerDragging && isCanvasPoint(event.clientX, event.clientY)
    let position: { x: number; y: number } | undefined
    try {
      position = shouldCreate ? options.screenToFlowCoordinate({ x: event.clientX, y: event.clientY }) : undefined
    } catch {
      options.notice.value = t('canvas.invalidNodeDrop')
    } finally {
      clearPointerDrag()
    }

    if (!position) {
      return
    }

    options.addNode(
      payload.kind,
      payload.titleKey,
      payload.subtitleKey,
      { x: Math.round(position.x / 16) * 16, y: Math.round(position.y / 16) * 16 },
      payload,
    )
  }

  function handlePointerCancel(): void {
    clearPointerDrag()
  }

  function handlePointerKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault()
      clearPointerDrag()
    }
  }

  function createLibraryNodePayload(node: LibraryNodeDto): NodeDropPayload {
    const runtime: NodeRuntimeMetadata = {
      category: 'method',
      libraryId: node.libraryId,
      className: node.className,
      methodName: node.methodName,
      dllName: node.dllName,
      dllVersion: node.dllVersion,
      returnType: node.returnType,
      isAwaitable: node.isAwaitable,
      libraryNodeContractId: node.contractId ?? undefined,
    }
    const parameters: MethodParameter[] = node.parameters.map((parameter) => ({
      id: canonicalParameterId(parameter.id),
      nameKey: parameter.name,
      name: parameter.name,
      valueKind: parameter.type || 'System.Object',
      type: parameter.type,
      required: parameter.required,
      description: parameter.description ?? undefined,
      isVariadic: parameter.isVariadic,
      variadicGroupId: parameter.variadicGroupId ? canonicalParameterId(parameter.variadicGroupId) : undefined,
      elementType: parameter.elementType ?? undefined,
      variadicMode: parameter.isVariadic ? 'expanded' : undefined,
      enumMetadata: parameter.enumMetadata ?? undefined,
      source: 'literal' as const,
      inputMode: 'manual' as const,
      literalValue: initialLibraryParameterLiteral(parameter.defaultValue),
    }))

    return {
      kind: isNodeKind(node.type) ? node.type : 'action',
      titleKey: 'node.catalogMethod',
      subtitleKey: 'node.catalogSubtitle',
      displayName: node.displayName,
      description: node.description ?? `${node.className}.${node.methodName}`,
      runtime,
      parameters,
      hasDataOutput: node.returnType !== 'System.Void',
    }
  }

  function createBuiltinNodePayload(node: NodeCreationDescriptorDto): NodeDropPayload | undefined {
    if (!isNodeKind(node.type)) {
      return undefined
    }

    const runtime: NodeRuntimeMetadata = {
      category: 'basic',
      returnType: node.ui.returnType,
      targetNodeId: node.ui.targetNodeId,
      targetFlowId: node.ui.targetFlowId,
      targetCanvasId: node.ui.targetCanvasId,
      isAwaitable: node.ui.isAwaitable,
      staticReturnType: node.ui.staticReturnType,
      isDynamicReturnType: node.ui.isDynamicReturnType,
      isPublic: node.ui.isPublic,
      flowCallParameterBindings: node.ui.flowCallParameterBindings,
    }
    const parameters: MethodParameter[] = (node.parameters ?? []).map((parameter) => ({
      id: canonicalParameterId(parameter.ui?.id ?? parameter.name),
      nameKey: parameter.ui?.nameKey ?? parameter.name,
      name: parameter.name,
      valueKind: parameter.ui?.valueKind ?? 'System.Object',
      type: parameter.ui?.type ?? parameter.ui?.valueKind ?? 'System.Object',
      required: parameter.required,
      description: parameter.ui?.description,
      source: parameter.source,
      inputMode: parameter.ui?.inputMode,
      literalValue: parameter.ui?.literalValue ?? parameter.valueJson ?? '',
      projectInputKey: parameter.ui?.projectInputKey,
      expression: parameter.ui?.expression,
      sourceNodeId: parameter.ui?.sourceNodeId,
      sourcePortId: parameter.ui?.sourcePortId,
      isVariadic: parameter.ui?.isVariadic,
      variadicGroupId: parameter.ui?.variadicGroupId,
      elementType: parameter.ui?.elementType,
      variadicMode: parameter.ui?.variadicMode === 'collection'
        ? 'collection'
        : parameter.ui?.isVariadic
          ? 'expanded'
          : undefined,
      enumMetadata: parameter.ui?.enumMetadata,
    }))

    return {
      kind: node.type,
      titleKey: node.ui.titleKey,
      subtitleKey: node.ui.subtitleKey,
      runtime,
      parameters,
      script: node.script
        ? {
            nodeId: node.script.nodeId,
            source: node.script.source,
            languageVersion: node.script.languageVersion,
            sourceHash: node.script.sourceHash,
            inputs: node.script.inputs.map((input) => ({ ...input })),
            outputs: node.script.outputs.map((output) => ({ ...output })),
          }
        : undefined,
      hasDataOutput: node.ui.hasDataOutput,
    }
  }

  function beginPointerNodeDrag(event: PointerEvent, payload: NodeDropPayload): void {
    if (event.button !== 0 || event.isPrimary === false) {
      return
    }

    clearPointerDrag()
    event.preventDefault()
    pointerId = event.pointerId
    pointerSource = event.currentTarget instanceof HTMLElement ? event.currentTarget : undefined
    pointerStart = { x: event.clientX, y: event.clientY }
    pointerPayload = payload
    previousBodyCursor = document.body.style.cursor
    previousBodyUserSelect = document.body.style.userSelect
    document.body.style.cursor = 'grabbing'
    document.body.style.userSelect = 'none'
    pointerSource?.setPointerCapture(event.pointerId)
    window.addEventListener('pointermove', handlePointerMove)
    window.addEventListener('pointerup', handlePointerUp)
    window.addEventListener('pointercancel', handlePointerCancel)
    window.addEventListener('keydown', handlePointerKeyDown)
  }

  function handleLibraryNodePointerDown(event: PointerEvent, node: LibraryNodeDto): void {
    beginPointerNodeDrag(event, createLibraryNodePayload(node))
  }

  function handleBuiltinNodePointerDown(event: PointerEvent, node: NodeCreationDescriptorDto): void {
    const payload = createBuiltinNodePayload(node)
    if (payload) {
      beginPointerNodeDrag(event, payload)
    }
  }

  onBeforeUnmount(clearPointerDrag)

  function handleCanvasDragOver(event: DragEvent): void {
    const dataTransfer = event.dataTransfer
    if (!dataTransfer) {
      return
    }

    // A browser may hide custom MIME types while a drag is crossing
    // component boundaries. Always accept the dragover so the subsequent
    // drop event can expose the payload through one of the supported types.
    // 拖拽跨越组件边界时，浏览器可能隐藏自定义 MIME 类型；始终接受 dragover，
    // 让后续 drop 事件可以通过受支持的类型暴露载荷。
    event.preventDefault()
    dataTransfer.dropEffect = 'copy'
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
    const encoded = event.dataTransfer?.getData(nodeDragMimeType)
      || event.dataTransfer?.getData('application/json')
      || event.dataTransfer?.getData('text/plain')
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

  return { handleCanvasDragOver, handleCanvasDragLeave, handleCanvasDrop, handleLibraryNodePointerDown, handleBuiltinNodePointerDown }
}
