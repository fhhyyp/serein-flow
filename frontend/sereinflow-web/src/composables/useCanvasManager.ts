import { computed, nextTick, ref, type ComputedRef, type Ref } from 'vue';
import { t } from '../i18n';
import { reorderCanvases } from '../flow/canvasOrder';
import type { CanvasLifecycle, CanvasState } from '../flow/types';
interface UseCanvasManagerOptions {
  canvases: Ref<CanvasState[]>;
  activeCanvasId: Ref<string>;
  currentCanvas: ComputedRef<CanvasState>;
  mobilePanel: Ref<'nodes' | 'inspector' | null>;
  notice: Ref<string>;
  isSwitchingCanvas: Ref<boolean>;
  recordWorkspaceMutation: () => void;
  markWorkspaceChanged: () => void;
}
export function useCanvasManager(options: UseCanvasManagerOptions) {
  const canvasMenuOpen = ref(false);
  const customCanvasNameDraft = ref('');
  const canvasDeleteConfirmOpen = ref(false);
  const pendingCanvasDeleteId = ref<string>();
  const availableCanvasLifecycles = computed<CanvasLifecycle[]>(() => {
    const filterResult: CanvasLifecycle[] = (['init', 'loading', 'exit'] as CanvasLifecycle[])
      .filter((lifecycle) => {
      const evaluatedValue: boolean = !options.canvases.value.some((canvas) => canvas.lifecycle === lifecycle);
      return evaluatedValue;
    });
    return filterResult;
  });
  const nextCustomCanvasNumber = computed(() => {
    const mapResult = options.canvases.value
      .map((canvas) => {
      const evaluatedValue1 = canvas.id.match(/^custom-(\d+)$/)?.[1] ?? 0;
      const numberResult: number = Number(evaluatedValue1);
      return numberResult;
    });
    const filterResult1: Iterable<number> | null | undefined = mapResult.filter((value) => value > 0);
    const used = new Set(filterResult1);
    let candidate = 1;
    while (used.has(candidate)) {
      candidate += 1;
    }
    return candidate;
  });
  const pendingCanvasDelete = computed(() => {
    const evaluatedValue2: CanvasState | undefined = pendingCanvasDeleteId.value
      ? options.canvases.value.find((canvas) => canvas.id === pendingCanvasDeleteId.value)
      : undefined;
    return evaluatedValue2;
  });
  function canvasLabel(canvas: CanvasState): string {
    const evaluatedValue3: string = canvas.name?.trim() || t(canvas.nameKey);
    return evaluatedValue3;
  }
  function selectCanvas(canvasId: string): void {
    if (canvasId === options.activeCanvasId.value) {
      return;
    }
    options.isSwitchingCanvas.value = true;
    options.activeCanvasId.value = canvasId;
    options.mobilePanel.value = null;
    canvasMenuOpen.value = false;
    const resolveThenResult = () => {
      const nextTickResult = nextTick();
      const thenResult = nextTickResult.then(() => {
        options.isSwitchingCanvas.value = false;
      });
      return thenResult;
    };
    void resolveThenResult();
  }
  function reorderCanvas(sourceCanvasId: string, targetCanvasId: string): void {
    const sourceCanvas = options.canvases.value.find((canvas) => canvas.id === sourceCanvasId);
    const targetCanvas = options.canvases.value.find((canvas) => canvas.id === targetCanvasId);
    if (!sourceCanvas || !targetCanvas || sourceCanvasId === targetCanvasId) {
      return;
    }
    options.recordWorkspaceMutation();
    options.canvases.value = reorderCanvases(options.canvases.value, sourceCanvasId, targetCanvasId);
    options.markWorkspaceChanged();
    options.notice.value = t('canvas.reordered');
  }
  function addCanvas(lifecycle: CanvasLifecycle): void {
    if (lifecycle === 'main' || lifecycle === 'custom' || options.canvases.value.some((canvas) => canvas.lifecycle === lifecycle)) {
      return;
    }
    options.recordWorkspaceMutation();
    const addedCanvas: CanvasState = {
      id: lifecycle,
      nameKey: `canvas.${lifecycle}`,
      lifecycle,
      nodes: [],
      edges: [],
    };
    options.canvases.value = [...options.canvases.value, addedCanvas];
    options.activeCanvasId.value = addedCanvas.id;
    canvasMenuOpen.value = false;
    options.markWorkspaceChanged();
    const evaluatedValue4: {
      [x: string]: string | number;
    } | undefined = { canvas: canvasLabel(addedCanvas) };
    options.notice.value = t('canvas.added', evaluatedValue4);
  }
  function toggleCanvasMenu(): void {
    canvasMenuOpen.value = !canvasMenuOpen.value;
    if (canvasMenuOpen.value) {
      customCanvasNameDraft.value = t('canvas.customDefault', { number: nextCustomCanvasNumber.value });
    }
  }
  function addCustomCanvas(): void {
    const index = nextCustomCanvasNumber.value;
    const name = customCanvasNameDraft.value.trim() || t('canvas.customDefault', { number: index });
    const canvas: CanvasState = {
      id: `custom-${index}`,
      nameKey: 'canvas.custom',
      name,
      lifecycle: 'custom',
      nodes: [],
      edges: [],
    };
    options.recordWorkspaceMutation();
    options.canvases.value = [...options.canvases.value, canvas];
    options.activeCanvasId.value = canvas.id;
    canvasMenuOpen.value = false;
    customCanvasNameDraft.value = '';
    options.markWorkspaceChanged();
    const evaluatedValue5: {
      [x: string]: string | number;
    } | undefined = { canvas: canvasLabel(canvas) };
    options.notice.value = t('canvas.added', evaluatedValue5);
  }
  function deleteCanvasById(canvasId: string): void {
    const removedCanvas = options.canvases.value.find((canvas) => canvas.id === canvasId);
    if (!removedCanvas || removedCanvas.lifecycle === 'main') {
      return;
    }
    options.recordWorkspaceMutation();
    options.canvases.value = options.canvases.value.filter((canvas) => canvas.id !== removedCanvas.id);
    if (options.activeCanvasId.value === removedCanvas.id) {
      options.activeCanvasId.value = 'main';
      options.mobilePanel.value = null;
    }
    options.markWorkspaceChanged();
    const evaluatedValue6: {
      [x: string]: string | number;
    } | undefined = { canvas: canvasLabel(removedCanvas) };
    options.notice.value = t('canvas.removed', evaluatedValue6);
  }
  function requestCanvasRemoval(): void {
    if (options.currentCanvas.value.lifecycle === 'main') {
      options.notice.value = t('canvas.cannotRemoveMain');
      return;
    }
    if (options.currentCanvas.value.nodes.length > 0 || options.currentCanvas.value.edges.length > 0) {
      pendingCanvasDeleteId.value = options.currentCanvas.value.id;
      canvasDeleteConfirmOpen.value = true;
      return;
    }
    deleteCanvasById(options.currentCanvas.value.id);
  }
  function cancelCanvasRemoval(): void {
    canvasDeleteConfirmOpen.value = false;
    pendingCanvasDeleteId.value = undefined;
  }
  function confirmCanvasRemoval(): void {
    const canvasId = pendingCanvasDeleteId.value;
    cancelCanvasRemoval();
    if (canvasId) {
      deleteCanvasById(canvasId);
    }
  }
  return {
    canvasMenuOpen,
    customCanvasNameDraft,
    canvasDeleteConfirmOpen,
    pendingCanvasDeleteId,
    pendingCanvasDelete,
    availableCanvasLifecycles,
    nextCustomCanvasNumber,
    canvasLabel,
    selectCanvas,
    reorderCanvas,
    addCanvas,
    toggleCanvasMenu,
    addCustomCanvas,
    deleteCanvasById,
    requestCanvasRemoval,
    cancelCanvasRemoval,
    confirmCanvasRemoval,
  };
}
