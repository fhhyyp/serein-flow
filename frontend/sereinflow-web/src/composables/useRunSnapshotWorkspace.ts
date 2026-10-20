import { computed, onBeforeUnmount, onMounted, reactive, type Ref, watch } from 'vue';
import { createSnapshotDockableWorkspaceLayout, normalizeDockableWorkspaceLayout, type DockablePanelGroupState, type DockableWorkspaceLayout, type DockPosition, type WorkspacePanelId, type WorkspaceSize, } from '../flow/dockableWorkspace';
import { reflowDockableResize } from '../flow/dockableResize';
const storageKey = 'sereinflow.run-snapshot-dock-layout.v1';
type EdgeDockPosition = Exclude<DockPosition, 'free' | 'fill'>;
function cloneLayout(layout: DockableWorkspaceLayout): DockableWorkspaceLayout {
  const stringifyResult: string = JSON.stringify(layout);
  const evaluatedValue: DockableWorkspaceLayout = JSON.parse(stringifyResult) as DockableWorkspaceLayout;
  return evaluatedValue;
}
function isEdgeDockPosition(dock: DockPosition): dock is EdgeDockPosition {
  return dock === 'left' || dock === 'right' || dock === 'top' || dock === 'bottom';
}
function clamp(value: number, min: number, max: number): number {
  const maxResult: number = Math.max(value, min);
  const maxResult1: number = Math.max(min, max);
  const minResult: number = Math.min(maxResult, maxResult1);
  return minResult;
}
function loadLayout(size: WorkspaceSize): {
  layout: DockableWorkspaceLayout;
  stored: boolean;
} {
  const fallback = createSnapshotDockableWorkspaceLayout(size);
  if (typeof window === 'undefined') {
    return { layout: fallback, stored: false };
  }
  try {
    const raw = window.localStorage.getItem(storageKey);
    if (!raw) {
      return { layout: fallback, stored: false };
    }
    const evaluatedValue1 = JSON.parse(raw) as unknown;
    const stored = normalizeDockableWorkspaceLayout(evaluatedValue1, size);
    return stored ? { layout: stored, stored: true } : { layout: fallback, stored: false };
  }
  catch {
    return { layout: fallback, stored: false };
  }
}
export function useRunSnapshotWorkspace(workspaceElement: Ref<HTMLElement | undefined>) {
  const workspaceSize = reactive<WorkspaceSize>({ width: 1920, height: 980 });
  const initial = loadLayout(workspaceSize);
  const layout = reactive<DockableWorkspaceLayout>(initial.layout);
  let hasMeasuredWorkspace = false;
  const groups = computed(() => layout.groups);
  const visibleGroups = computed(() => {
    const filterResult: {
      id: string;
      x: number;
      y: number;
      width: number;
      height: number;
      dock: DockPosition;
      panelIds: WorkspacePanelId[];
      activePanelId: WorkspacePanelId;
      zIndex: number;
      collapsed: boolean;
    }[] = layout.groups.filter((group) => {
      const someResult: boolean = group.panelIds.some((panelId) => layout.panels[panelId].visible);
      return someResult;
    });
    return filterResult;
  });
  const renderedEdgeGroups = computed(() => {
    const filterResult1: {
      id: string;
      x: number;
      y: number;
      width: number;
      height: number;
      dock: DockPosition;
      panelIds: WorkspacePanelId[];
      activePanelId: WorkspacePanelId;
      zIndex: number;
      collapsed: boolean;
    }[] = visibleGroups.value.filter((group) => {
      const isEdgeDockPositionResult: boolean = isEdgeDockPosition(group.dock);
      return isEdgeDockPositionResult;
    });
    return filterResult1;
  });
  let resizeObserver: ResizeObserver | undefined;
  let fallbackResizeHandler: (() => void) | undefined;
  let saveTimer: number | undefined;
  function scheduleSave(): void {
    if (typeof window === 'undefined') {
      return;
    }
    if (saveTimer !== undefined) {
      window.clearTimeout(saveTimer);
    }
    saveTimer = window.setTimeout(() => {
      saveTimer = undefined;
      try {
        const argumentValue: string = storageKey;
        const cloneLayoutResult = cloneLayout(layout);
        const stringifyResult1: string = JSON.stringify(cloneLayoutResult);
        window.localStorage.setItem(argumentValue, stringifyResult1);
      }
      catch {
      }
    }, 120);
  }
  function updateGroup(groupId: string, patch: Partial<DockablePanelGroupState>): void {
    const group = layout.groups.find((item) => item.id === groupId);
    if (!group) {
      return;
    }
    const previous = {
      x: group.x,
      y: group.y,
      width: group.width,
      height: group.height,
      dock: group.dock,
    };
    Object.assign(group, patch);
    const filterResult2 = layout.groups
      .filter((candidate) => {
      const someResult1: boolean = candidate.panelIds.some((panelId) => layout.panels[panelId].visible);
      return someResult1;
    });
    const mapResult: Iterable<string> | null | undefined = filterResult2.map((candidate) => candidate.id);
    const visibleGroupIds = new Set(mapResult);
    reflowDockableResize(layout.groups, groupId, previous, group, visibleGroupIds, workspaceSize);
    scheduleSave();
  }
  function groupForPanel(panelId: WorkspacePanelId): DockablePanelGroupState | undefined {
    const groupId = layout.panels[panelId].groupId;
    const evaluatedValue2: DockablePanelGroupState | undefined = layout.groups.find((group) => group.id === groupId)
      ?? layout.groups.find((group) => {
        const includesResult: boolean = group.panelIds.includes(panelId);
        return includesResult;
      });
    return evaluatedValue2;
  }
  function focusGroup(groupId: string): void {
    const mapResult1 = layout.groups.map((group) => group.zIndex);
    const maxZ = Math.max(...mapResult1, 1);
    updateGroup(groupId, { zIndex: maxZ + 1 });
  }
  function activatePanel(groupId: string, panelId: WorkspacePanelId): void {
    const group = layout.groups.find((item) => item.id === groupId);
    if (!group || !group.panelIds.includes(panelId)) {
      return;
    }
    updateGroup(groupId, { activePanelId: panelId });
    focusGroup(groupId);
  }
  function setPanelVisible(panelId: WorkspacePanelId, visible: boolean): void {
    if (panelId === 'canvas' && !visible) {
      return;
    }
    layout.panels[panelId].visible = visible;
    const group = groupForPanel(panelId);
    if (group) {
      const visibleTab = group.panelIds.find((id) => layout.panels[id].visible);
      if (visibleTab) {
        group.activePanelId = visibleTab;
      }
      focusGroup(group.id);
    }
    scheduleSave();
  }
  function showPanel(panelId: WorkspacePanelId): void {
    setPanelVisible(panelId, true);
    const group = groupForPanel(panelId);
    if (group) {
      activatePanel(group.id, panelId);
    }
  }
  function hidePanel(panelId: WorkspacePanelId): void {
    setPanelVisible(panelId, false);
  }
  function dockGroup(groupId: string, dock: DockPosition): void {
    const group = layout.groups.find((item) => item.id === groupId);
    if (!group) {
      return;
    }
    updateGroup(groupId, { dock });
    focusGroup(groupId);
  }
  function combinePanel(panelId: WorkspacePanelId, targetGroupId: string): void {
    const sourceGroup = groupForPanel(panelId);
    const targetGroup = layout.groups.find((item) => item.id === targetGroupId);
    if (!sourceGroup || !targetGroup || sourceGroup.id === targetGroup.id) {
      return;
    }
    sourceGroup.panelIds = sourceGroup.panelIds.filter((id) => id !== panelId);
    if (!targetGroup.panelIds.includes(panelId)) {
      targetGroup.panelIds.push(panelId);
    }
    layout.panels[panelId].groupId = targetGroup.id;
    targetGroup.activePanelId = panelId;
    if (panelId === 'canvas') {
      targetGroup.dock = 'fill';
    }
    focusGroup(targetGroup.id);
    if (sourceGroup.panelIds.length === 0) {
      layout.groups = layout.groups.filter((group) => group.id !== sourceGroup.id);
    }
    else if (!sourceGroup.panelIds.includes(sourceGroup.activePanelId)) {
      sourceGroup.activePanelId = sourceGroup.panelIds.find((id) => layout.panels[id].visible) ?? sourceGroup.panelIds[0]!;
    }
    scheduleSave();
  }
  function detachPanel(panelId: WorkspacePanelId): string | undefined {
    const sourceGroup = groupForPanel(panelId);
    if (!sourceGroup || sourceGroup.panelIds.length <= 1) {
      return sourceGroup?.id;
    }
    const groupId = `snapshot-${panelId}-${layout.nextGroupNumber++}`;
    const argumentValue1: number = sourceGroup.width;
    const maxResult2: number = Math.max(240, workspaceSize.width - 24);
    const detachedWidth = clamp(argumentValue1, 240, maxResult2);
    const argumentValue2: number = sourceGroup.height;
    const maxResult3: number = Math.max(160, workspaceSize.height - 24);
    const detachedHeight = clamp(argumentValue2, 160, maxResult3);
    const resolveClampResult = () => {
      const argumentValue7: number = sourceGroup.x + 24;
      const maxResult10: number = Math.max(12, workspaceSize.width - detachedWidth - 12);
      const clampResult: number = clamp(argumentValue7, 12, maxResult10);
      return clampResult;
    };
    const resolveClampResult1 = () => {
      const argumentValue8: number = sourceGroup.y + 24;
      const maxResult11: number = Math.max(12, workspaceSize.height - detachedHeight - 12);
      const clampResult1: number = clamp(argumentValue8, 12, maxResult11);
      return clampResult1;
    };
    const resolveMaxResult = () => {
      const mapResult2 = layout.groups.map((group) => group.zIndex);
      const maxResult12 = Math.max(...mapResult2, 1);
      return maxResult12;
    };
    const newGroup: DockablePanelGroupState = {
      ...sourceGroup,
      id: groupId,
      x: resolveClampResult(),
      y: resolveClampResult1(),
      width: detachedWidth,
      height: detachedHeight,
      panelIds: [panelId],
      activePanelId: panelId,
      dock: 'free',
      zIndex: resolveMaxResult() + 1,
    };
    sourceGroup.panelIds = sourceGroup.panelIds.filter((id) => id !== panelId);
    if (!sourceGroup.panelIds.includes(sourceGroup.activePanelId)) {
      sourceGroup.activePanelId = sourceGroup.panelIds.find((id) => layout.panels[id].visible) ?? sourceGroup.panelIds[0]!;
    }
    layout.groups.push(newGroup);
    layout.panels[panelId].groupId = groupId;
    scheduleSave();
    return groupId;
  }
  function resetLayout(): void {
    const next = createSnapshotDockableWorkspaceLayout(workspaceSize);
    layout.groups = next.groups;
    layout.panels = next.panels;
    layout.nextGroupNumber = next.nextGroupNumber;
    scheduleSave();
  }
  function clampGroupsToWorkspace(): void {
    for (const group of layout.groups) {
      if (group.dock === 'fill') {
        continue;
      }
      const argumentValue3: number = group.width;
      const maxResult4: number = Math.max(240, workspaceSize.width - 16);
      group.width = clamp(argumentValue3, 240, maxResult4);
      const argumentValue4: number = group.height;
      const maxResult5: number = Math.max(160, workspaceSize.height - 16);
      group.height = clamp(argumentValue4, 160, maxResult5);
      const argumentValue5: number = group.x;
      const maxResult6: number = Math.max(0, workspaceSize.width - group.width);
      group.x = clamp(argumentValue5, 0, maxResult6);
      const argumentValue6: number = group.y;
      const maxResult7: number = Math.max(0, workspaceSize.height - group.height);
      group.y = clamp(argumentValue6, 0, maxResult7);
    }
  }
  function desiredEdgeGroupExtent(group: DockablePanelGroupState): number {
    if (group.collapsed) {
      return 76;
    }
    const raw = group.dock === 'left' || group.dock === 'right' ? group.width : group.height;
    const maximum = group.dock === 'left' || group.dock === 'right' ? workspaceSize.width : workspaceSize.height;
    const minResult1: number = Math.min(raw, maximum);
    const maxResult8: number = Math.max(0, minResult1);
    return maxResult8;
  }
  function dockedExtentFor(group: DockablePanelGroupState): number | undefined {
    if (!isEdgeDockPosition(group.dock)) {
      return undefined;
    }
    const peers = renderedEdgeGroups.value.filter((candidate) => candidate.dock === group.dock);
    const total = peers.reduce((sum, candidate) => {
      const evaluatedValue3: number = sum + desiredEdgeGroupExtent(candidate);
      return evaluatedValue3;
    }, 0);
    const capacity = group.dock === 'left' || group.dock === 'right' ? workspaceSize.width : workspaceSize.height;
    const scale = total > capacity && total > 0 ? capacity / total : 1;
    const evaluatedValue4: number | undefined = desiredEdgeGroupExtent(group) * scale;
    return evaluatedValue4;
  }
  function dockedOffsetFor(group: DockablePanelGroupState): number {
    if (!isEdgeDockPosition(group.dock)) {
      return 0;
    }
    const peers = renderedEdgeGroups.value.filter((candidate) => candidate.dock === group.dock);
    const groupIndex = peers.findIndex((candidate) => candidate.id === group.id);
    const maxResult9: number | undefined = Math.max(0, groupIndex);
    const sliceResult = peers.slice(0, maxResult9);
    const reduceResult: number = sliceResult.reduce((offset, candidate) => {
      const evaluatedValue5: number = offset + (dockedExtentFor(candidate) ?? 0);
      return evaluatedValue5;
    }, 0);
    return reduceResult;
  }
  const layoutPriority = computed<'full-row' | 'center-column'>(() => {
    const evaluatedValue6: "full-row" | "center-column" = renderedEdgeGroups.value.some((group) => group.dock === 'top' || group.dock === 'bottom')
      ? 'full-row'
      : 'center-column';
    return evaluatedValue6;
  });
  const canvasInsets = computed(() => {
    const reduceResult1: {
      top: number;
      right: number;
      bottom: number;
      left: number;
    } = renderedEdgeGroups.value.reduce((insets, group) => {
      const extent = dockedExtentFor(group) ?? 0;
      if (group.dock === 'left') {
        insets.left += extent;
      }
      if (group.dock === 'right') {
        insets.right += extent;
      }
      if (group.dock === 'top') {
        insets.top += extent;
      }
      if (group.dock === 'bottom') {
        insets.bottom += extent;
      }
      return insets;
    }, { top: 0, right: 0, bottom: 0, left: 0 });
    return reduceResult1;
  });
  function updateWorkspaceSize(element: HTMLElement): void {
    workspaceSize.width = Math.max(1, element.clientWidth);
    workspaceSize.height = Math.max(1, element.clientHeight);
    if (!hasMeasuredWorkspace && !initial.stored) {
      const next = createSnapshotDockableWorkspaceLayout(workspaceSize);
      layout.groups = next.groups;
      layout.panels = next.panels;
      layout.nextGroupNumber = next.nextGroupNumber;
    }
    hasMeasuredWorkspace = true;
    clampGroupsToWorkspace();
  }
  function observeWorkspace(element: HTMLElement): void {
    resizeObserver?.disconnect();
    if (fallbackResizeHandler) {
      window.removeEventListener('resize', fallbackResizeHandler);
    }
    updateWorkspaceSize(element);
    if (typeof ResizeObserver !== 'undefined') {
      resizeObserver = new ResizeObserver(() => {
        const updateWorkspaceSizeResult: void = updateWorkspaceSize(element);
        return updateWorkspaceSizeResult;
      });
      resizeObserver.observe(element);
    }
    else {
      fallbackResizeHandler = () => {
        const updateWorkspaceSizeResult1: void = updateWorkspaceSize(element);
        return updateWorkspaceSizeResult1;
      };
      window.addEventListener('resize', fallbackResizeHandler);
    }
  }
  watch(layout, scheduleSave, { deep: true });
  watch(workspaceElement, (element) => {
    if (element) {
      observeWorkspace(element);
    }
  }, { flush: 'post' });
  onMounted(() => {
    if (workspaceElement.value) {
      observeWorkspace(workspaceElement.value);
    }
  });
  onBeforeUnmount(() => {
    resizeObserver?.disconnect();
    if (fallbackResizeHandler && typeof window !== 'undefined') {
      window.removeEventListener('resize', fallbackResizeHandler);
    }
    if (saveTimer !== undefined && typeof window !== 'undefined') {
      window.clearTimeout(saveTimer);
    }
  });
  return {
    layout,
    groups,
    visibleGroups,
    workspaceSize,
    renderedEdgeGroups,
    layoutPriority,
    canvasInsets,
    dockedExtentFor,
    dockedOffsetFor,
    updateGroup,
    groupForPanel,
    activatePanel,
    showPanel,
    hidePanel,
    dockGroup,
    combinePanel,
    detachPanel,
    focusGroup,
    resetLayout,
  };
}
