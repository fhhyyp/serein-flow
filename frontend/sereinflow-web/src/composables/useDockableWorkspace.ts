import { computed, onBeforeUnmount, onMounted, reactive, ref, type Ref, watch } from 'vue';
import { createDefaultDockableWorkspaceLayout, createDebugDockableWorkspaceLayout, fitDebugDockableWorkspaceLayout, resizeFreeDockableGroups, normalizeDockableWorkspaceLayout, type DockablePanelGroupState, type DockableWorkspaceMode, type DockableWorkspaceLayout, type DockPosition, type WorkspacePanelId, type WorkspaceSize, type WorkspaceInsets, } from '../flow/dockableWorkspace';
import { reflowDockableResize } from '../flow/dockableResize';
import { splitDockRect, type DockRect, type DockSplitZone } from '../flow/dockableSplit';
import { snapFreePanelMove, snapFreePanelResize } from '../flow/dockableSnap';
type FreePanelGeometry = Parameters<typeof snapFreePanelMove>[0];
const storageKey = 'sereinflow.canvas-dock-layout.v3';
const legacyStorageKey = 'sereinflow.canvas-dock-layout.v2';
const storageVersion = 3;
type WorkspaceModeLayouts = Record<DockableWorkspaceMode, DockableWorkspaceLayout>;
type WorkspaceModeGeometry = Record<DockableWorkspaceMode, boolean>;
interface LoadedModeLayouts {
  layouts: WorkspaceModeLayouts;
  geometryCustomized: WorkspaceModeGeometry;
  savedWorkspaceSize?: WorkspaceSize;
}
function createModeLayout(mode: DockableWorkspaceMode, size: WorkspaceSize): DockableWorkspaceLayout {
  const evaluatedValue: DockableWorkspaceLayout = mode === 'debug'
    ? createDebugDockableWorkspaceLayout(size)
    : createDefaultDockableWorkspaceLayout(size);
  return evaluatedValue;
}
function migrateEditLayoutForCollapsedDiagnostics(layout: DockableWorkspaceLayout): DockableWorkspaceLayout {
  layout.panels.output.visible = false;
  layout.panels.diagnostics.visible = true;
  const diagnosticsGroup = layout.groups.find((group) => group.id === layout.panels.diagnostics.groupId)
    ?? layout.groups.find((group) => {
      const includesResult: boolean = group.panelIds.includes('diagnostics');
      return includesResult;
    });
  if (diagnosticsGroup) {
    diagnosticsGroup.dock = 'bottom';
    diagnosticsGroup.collapsed = true;
    diagnosticsGroup.activePanelId = 'diagnostics';
  }
  return layout;
}
function loadModeLayouts(size: WorkspaceSize): LoadedModeLayouts {
  const defaults: WorkspaceModeLayouts = {
    edit: createModeLayout('edit', size),
    debug: createModeLayout('debug', size),
  };
  const defaultGeometryCustomized: WorkspaceModeGeometry = { edit: false, debug: false };
  if (typeof window === 'undefined') {
    return { layouts: defaults, geometryCustomized: defaultGeometryCustomized };
  }
  try {
    const raw = window.localStorage.getItem(storageKey);
    if (raw) {
      const stored = JSON.parse(raw) as {
        storageVersion?: number;
        layouts?: Partial<Record<DockableWorkspaceMode, unknown>>;
        geometryCustomized?: Partial<WorkspaceModeGeometry>;
        workspaceSize?: Partial<WorkspaceSize>;
      };
      if ((stored.storageVersion === 1 || stored.storageVersion === 2 || stored.storageVersion === storageVersion) && stored.layouts) {
        const editLayout = normalizeDockableWorkspaceLayout(stored.layouts.edit, size, defaults.edit) ?? defaults.edit;
        const debugLayout = stored.storageVersion === storageVersion
          ? normalizeDockableWorkspaceLayout(stored.layouts.debug, size, defaults.debug) ?? defaults.debug
          : defaults.debug;
        const evaluatedValue1: LoadedModeLayouts = {
          layouts: {
            edit: stored.storageVersion === 1
              ? migrateEditLayoutForCollapsedDiagnostics(editLayout)
              : editLayout,
            // Version 3 is the first cache version with the corrected 50/50 debug split.
            // 缓存版本 3 首次修正了调试布局的 50/50 分栏比例。
            debug: debugLayout,
          },
          geometryCustomized: stored.storageVersion === storageVersion
            ? {
              edit: stored.geometryCustomized?.edit === true,
              debug: stored.geometryCustomized?.debug === true,
            }
            : {
              edit: true,
              debug: false,
            },
          savedWorkspaceSize: Number.isFinite(stored.workspaceSize?.width) && Number.isFinite(stored.workspaceSize?.height)
            && (stored.workspaceSize?.width ?? 0) > 0 && (stored.workspaceSize?.height ?? 0) > 0
            ? { width: stored.workspaceSize!.width!, height: stored.workspaceSize!.height! }
            : undefined,
        };
        return evaluatedValue1;
      }
    }
    const legacyRaw = window.localStorage.getItem(legacyStorageKey);
    const resolveNormalizeDockableWorkspaceLayoutResult = () => {
      const evaluatedValue17 = JSON.parse(legacyRaw!) as unknown;
      const normalizeDockableWorkspaceLayoutResult = normalizeDockableWorkspaceLayout(evaluatedValue17, size, defaults.edit);
      return normalizeDockableWorkspaceLayoutResult;
    };
    const legacyLayout = legacyRaw
      ? resolveNormalizeDockableWorkspaceLayoutResult() : undefined;
    if (legacyLayout) {
      const evaluatedValue2: LoadedModeLayouts = {
        layouts: { edit: migrateEditLayoutForCollapsedDiagnostics(legacyLayout), debug: defaults.debug },
        geometryCustomized: { edit: true, debug: false },
      };
      return evaluatedValue2;
    }
    return { layouts: defaults, geometryCustomized: defaultGeometryCustomized };
  }
  catch {
    return { layouts: defaults, geometryCustomized: defaultGeometryCustomized };
  }
}
function clamp(value: number, min: number, max: number): number {
  const maxResult: number = Math.max(value, min);
  const maxResult1: number = Math.max(min, max);
  const minResult: number = Math.min(maxResult, maxResult1);
  return minResult;
}
function cloneLayout(layout: DockableWorkspaceLayout): DockableWorkspaceLayout {
  const stringifyResult: string = JSON.stringify(layout);
  const evaluatedValue3: DockableWorkspaceLayout = JSON.parse(stringifyResult) as DockableWorkspaceLayout;
  return evaluatedValue3;
}
interface LayoutRect {
  left: number;
  top: number;
  right: number;
  bottom: number;
}
function rectArea(rect: LayoutRect): number {
  const evaluatedValue4: number = Math.max(0, rect.right - rect.left) * Math.max(0, rect.bottom - rect.top);
  return evaluatedValue4;
}
function overlapArea(first: LayoutRect, second: LayoutRect): number {
  const resolveMaxResult = () => {
    const evaluatedValue18: number = Math.min(first.right, second.right) - Math.max(first.left, second.left);
    const maxResult12 = Math.max(0, evaluatedValue18);
    return maxResult12;
  };
  const resolveMaxResult1 = () => {
    const evaluatedValue19: number = Math.min(first.bottom, second.bottom) - Math.max(first.top, second.top);
    const maxResult13 = Math.max(0, evaluatedValue19);
    return maxResult13;
  };
  const evaluatedValue5: number = resolveMaxResult() * resolveMaxResult1();
  return evaluatedValue5;
}
export function useDockableWorkspace(workspaceElement: Ref<HTMLElement | undefined>) {
  const workspaceSize = reactive<WorkspaceSize>({ width: 1920, height: 980 });
  const displayMode = ref<DockableWorkspaceMode>('edit');
  const loadedLayouts = loadModeLayouts(workspaceSize);
  const modeLayouts = reactive<WorkspaceModeLayouts>(loadedLayouts.layouts);
  const geometryCustomized = reactive<WorkspaceModeGeometry>(loadedLayouts.geometryCustomized);
  const cloneLayoutResult: DockableWorkspaceLayout = cloneLayout(modeLayouts.edit);
  const layout = reactive<DockableWorkspaceLayout>(cloneLayoutResult);
  let resizeObserver: ResizeObserver | undefined;
  let saveTimer: number | undefined;
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
  let fallbackResizeHandler: (() => void) | undefined;
  function syncActiveModeLayout(): void {
    modeLayouts[displayMode.value] = cloneLayout(layout);
  }
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
        syncActiveModeLayout();
        const argumentValue: string = storageKey;
        const evaluatedValue6 = {
          storageVersion,
          workspaceSize: { ...workspaceSize },
          geometryCustomized: { ...geometryCustomized },
          layouts: {
            edit: cloneLayout(modeLayouts.edit),
            debug: cloneLayout(modeLayouts.debug),
          },
        };
        const stringifyResult1: string = JSON.stringify(evaluatedValue6);
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
    if (previous.dock === 'free' && group.dock === 'free'
      && ('width' in patch || 'height' in patch)) {
      const argumentValue1: {
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
      } = group;
      const argumentValue2: FreePanelGeometry = previous;
      const argumentValue3: FreePanelGeometry = group;
      const occupiedRectsResult: DockRect[] = occupiedRects(groupId);
      const snapFreePanelResizeResult: FreePanelGeometry = snapFreePanelResize(argumentValue2, argumentValue3, occupiedRectsResult, workspaceSize);
      Object.assign(argumentValue1, snapFreePanelResizeResult);
    }
    if ('x' in patch || 'y' in patch || 'width' in patch || 'height' in patch || 'dock' in patch || 'collapsed' in patch) {
      geometryCustomized[displayMode.value] = true;
    }
    const filterResult1 = layout.groups
      .filter((candidate) => {
      const someResult1: boolean = candidate.panelIds.some((panelId) => layout.panels[panelId].visible);
      return someResult1;
    });
    const mapResult: Iterable<string> | null | undefined = filterResult1.map((candidate) => candidate.id);
    const visibleGroupIds = new Set(mapResult);
    reflowDockableResize(layout.groups, groupId, previous, group, visibleGroupIds, workspaceSize);
    if (displayMode.value === 'debug' && groupId === layout.panels.debug.groupId
      && group.dock === 'right' && 'width' in patch) {
      fitDebugDockableWorkspaceLayout(layout, workspaceSize);
    }
    scheduleSave();
  }
  function groupForPanel(panelId: WorkspacePanelId): DockablePanelGroupState | undefined {
    const groupId = layout.panels[panelId].groupId;
    const evaluatedValue7: DockablePanelGroupState | undefined = layout.groups.find((group) => group.id === groupId)
      ?? layout.groups.find((group) => {
        const includesResult1: boolean = group.panelIds.includes(panelId);
        return includesResult1;
      });
    return evaluatedValue7;
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
    geometryCustomized[displayMode.value] = true;
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
      if (group.dock === 'free') {
        placeFreeGroup(group);
      }
    }
  }
  function hidePanel(panelId: WorkspacePanelId): void {
    setPanelVisible(panelId, false);
  }
  function setDisplayMode(nextMode: DockableWorkspaceMode): void {
    if (displayMode.value === nextMode) {
      return;
    }
    syncActiveModeLayout();
    const resolveCloneLayoutResult = () => {
      const evaluatedValue20: DockableWorkspaceLayout = modeLayouts[nextMode] ?? createModeLayout(nextMode, workspaceSize);
      const cloneLayoutResult1 = cloneLayout(evaluatedValue20);
      return cloneLayoutResult1;
    };
    const nextLayout = geometryCustomized[nextMode]
      ? resolveCloneLayoutResult() : createModeLayout(nextMode, workspaceSize);
    displayMode.value = nextMode;
    layout.groups = nextLayout.groups;
    layout.panels = nextLayout.panels;
    layout.nextGroupNumber = nextLayout.nextGroupNumber;
    clampGroupsToWorkspace();
    scheduleSave();
  }
  function dockGroup(groupId: string, dock: DockPosition): void {
    const group = layout.groups.find((item) => item.id === groupId);
    if (!group) {
      return;
    }
    updateGroup(groupId, { dock });
    focusGroup(groupId);
  }
  function snapGroup(groupId: string): void {
    const group = layout.groups.find((item) => item.id === groupId);
    if (!group || group.dock !== 'free') {
      return;
    }
    const argumentValue4: {
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
    } = group;
    const argumentValue5: FreePanelGeometry = group;
    const occupiedRectsResult1: DockRect[] = occupiedRects(groupId);
    const snapFreePanelMoveResult: FreePanelGeometry = snapFreePanelMove(argumentValue5, occupiedRectsResult1, workspaceSize);
    Object.assign(argumentValue4, snapFreePanelMoveResult);
    scheduleSave();
  }
  function combinePanel(panelId: WorkspacePanelId, targetGroupId: string): void {
    const sourceGroup = groupForPanel(panelId);
    const targetGroup = layout.groups.find((item) => item.id === targetGroupId);
    if (!sourceGroup || !targetGroup || sourceGroup.id === targetGroup.id) {
      return;
    }
    geometryCustomized[displayMode.value] = true;
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
  function splitPanel(panelId: WorkspacePanelId, targetGroupId: string, zone: Exclude<DockSplitZone, 'center'>, targetRect: DockRect): void {
    const initialSource = groupForPanel(panelId);
    const targetGroup = layout.groups.find((group) => group.id === targetGroupId);
    if (!initialSource || !targetGroup || (initialSource.id === targetGroup.id && initialSource.panelIds.length <= 1)) {
      return;
    }
    const sourceGroupId = initialSource.panelIds.length > 1 ? detachPanel(panelId) : initialSource.id;
    const sourceGroup = layout.groups.find((group) => group.id === sourceGroupId);
    if (!sourceGroup) {
      return;
    }
    const split = splitDockRect(targetRect, zone);
    const place = (group: DockablePanelGroupState, rect: DockRect) => {
      Object.assign(group, {
        dock: 'free' as const,
        collapsed: false,
        x: rect.left,
        y: rect.top,
        width: rect.right - rect.left,
        height: rect.bottom - rect.top,
      });
    };
    place(targetGroup, split.target);
    place(sourceGroup, split.source);
    geometryCustomized[displayMode.value] = true;
    focusGroup(sourceGroup.id);
    scheduleSave();
  }
  function detachPanel(panelId: WorkspacePanelId): string | undefined {
    const sourceGroup = groupForPanel(panelId);
    if (!sourceGroup || sourceGroup.panelIds.length <= 1) {
      return sourceGroup?.id;
    }
    geometryCustomized[displayMode.value] = true;
    const groupId = `${panelId}-${layout.nextGroupNumber++}`;
    const argumentValue6: number = sourceGroup.width;
    const maxResult2: number = Math.max(240, workspaceSize.width - 24);
    const detachedWidth = clamp(argumentValue6, 240, maxResult2);
    const argumentValue7: number = sourceGroup.height;
    const maxResult3: number = Math.max(160, workspaceSize.height - 24);
    const detachedHeight = clamp(argumentValue7, 160, maxResult3);
    const resolveClampResult = () => {
      const argumentValue14: number = sourceGroup.x + 24;
      const maxResult14: number = Math.max(12, workspaceSize.width - detachedWidth - 12);
      const clampResult: number = clamp(argumentValue14, 12, maxResult14);
      return clampResult;
    };
    const resolveClampResult1 = () => {
      const argumentValue15: number = sourceGroup.y + 24;
      const maxResult15: number = Math.max(12, workspaceSize.height - detachedHeight - 12);
      const clampResult1: number = clamp(argumentValue15, 12, maxResult15);
      return clampResult1;
    };
    const resolveMaxResult2 = () => {
      const mapResult2 = layout.groups.map((group) => group.zIndex);
      const maxResult16 = Math.max(...mapResult2, 1);
      return maxResult16;
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
      dock: panelId === 'canvas' ? 'fill' : 'free',
      zIndex: resolveMaxResult2() + 1,
    };
    sourceGroup.panelIds = sourceGroup.panelIds.filter((id) => id !== panelId);
    if (!sourceGroup.panelIds.includes(sourceGroup.activePanelId)) {
      sourceGroup.activePanelId = sourceGroup.panelIds.find((id) => layout.panels[id].visible) ?? sourceGroup.panelIds[0]!;
    }
    layout.groups.push(newGroup);
    layout.panels[panelId].groupId = groupId;
    if (newGroup.dock === 'free') {
      placeFreeGroup(newGroup);
    }
    scheduleSave();
    return groupId;
  }
  function resetLayout(): void {
    const next = createModeLayout(displayMode.value, workspaceSize);
    layout.groups = next.groups;
    layout.panels = next.panels;
    layout.nextGroupNumber = next.nextGroupNumber;
    geometryCustomized[displayMode.value] = false;
    syncActiveModeLayout();
    scheduleSave();
  }
  function clampGroupsToWorkspace(): void {
    for (const group of layout.groups) {
      if (group.dock === 'fill') {
        continue;
      }
      const argumentValue8: number = group.width;
      const maxResult4: number = Math.max(240, workspaceSize.width - 16);
      group.width = clamp(argumentValue8, 240, maxResult4);
      const argumentValue9: number = group.height;
      const maxResult5: number = Math.max(160, workspaceSize.height - 16);
      group.height = clamp(argumentValue9, 160, maxResult5);
      group.x = clamp(group.x, 0, workspaceSize.width - group.width);
      group.y = clamp(group.y, 0, workspaceSize.height - group.height);
    }
  }
  function edgeGroupExtent(group: DockablePanelGroupState, size = workspaceSize): number {
    if (group.collapsed) {
      return 76;
    }
    const raw = group.dock === 'left' || group.dock === 'right' ? group.width : group.height;
    const maximum = group.dock === 'left' || group.dock === 'right'
      ? size.width
      : size.height;
    const minResult1: number = Math.min(raw, maximum);
    const maxResult6: number = Math.max(0, minResult1);
    return maxResult6;
  }
  function workspaceInsetsForSize(size: WorkspaceSize): WorkspaceInsets {
    const insets = { left: 0, right: 0, top: 0, bottom: 0 };
    const visible = layout.groups.filter((group) => {
      const someResult2: boolean = group.panelIds.some((panelId) => layout.panels[panelId].visible);
      return someResult2;
    });
    for (const dock of ['left', 'right', 'top', 'bottom'] as const) {
      const peers = visible.filter((group) => group.dock === dock);
      const total = peers.reduce((sum, group) => {
        const evaluatedValue8: number = sum + edgeGroupExtent(group, size);
        return evaluatedValue8;
      }, 0);
      const capacity = dock === 'left' || dock === 'right' ? size.width : size.height;
      insets[dock] = Math.min(total, capacity);
    }
    return insets;
  }
  function visibleLayoutGroups(excludeGroupId: string): DockablePanelGroupState[] {
    const filterResult2: DockablePanelGroupState[] = layout.groups.filter((group) => {
      const evaluatedValue9: boolean = group.id !== excludeGroupId
        && group.panelIds.some((panelId) => layout.panels[panelId].visible);
      return evaluatedValue9;
    });
    return filterResult2;
  }
  function occupiedRects(excludeGroupId: string): LayoutRect[] {
    const groups = visibleLayoutGroups(excludeGroupId);
    const edgeGroups = groups.filter((group) => group.dock === 'left'
      || group.dock === 'right'
      || group.dock === 'top'
      || group.dock === 'bottom');
    const fullRowPriority = edgeGroups.some((group) => group.dock === 'top' || group.dock === 'bottom');
    const extents = new Map<string, number>();
    for (const dock of ['left', 'right', 'top', 'bottom'] as const) {
      const peers = edgeGroups.filter((group) => group.dock === dock);
      const total = peers.reduce((sum, group) => {
        const evaluatedValue10: number = sum + edgeGroupExtent(group);
        return evaluatedValue10;
      }, 0);
      const capacity = dock === 'left' || dock === 'right' ? workspaceSize.width : workspaceSize.height;
      const scale = total > capacity && total > 0 ? capacity / total : 1;
      for (const group of peers) {
        const groupExtent = edgeGroupExtent(group);
        const scaledExtent = groupExtent * scale;
        extents.set(group.id, scaledExtent);
      }
    }
    const insets = edgeGroups.reduce((result, group) => {
      const extent = extents.get(group.id) ?? 0;
      if (group.dock === 'left') {
        result.left += extent;
      }
      if (group.dock === 'right') {
        result.right += extent;
      }
      if (group.dock === 'top') {
        result.top += extent;
      }
      if (group.dock === 'bottom') {
        result.bottom += extent;
      }
      return result;
    }, { top: 0, right: 0, bottom: 0, left: 0 });
    const rects: LayoutRect[] = [];
    for (const group of groups) {
      if (group.dock === 'fill') {
        continue;
      }
      if (group.dock === 'free') {
        const minResult2: number = Math.min(group.width, workspaceSize.width);
        const width = Math.max(240, minResult2);
        const minResult3: number = Math.min(group.height, workspaceSize.height);
        const height = Math.max(160, minResult3);
        rects.push({ left: group.x, top: group.y, right: group.x + width, bottom: group.y + height });
        continue;
      }
      const extent = extents.get(group.id) ?? 0;
      const peers = edgeGroups.filter((candidate) => candidate.dock === group.dock);
      const findIndexResult: number = peers.findIndex((candidate) => candidate.id === group.id);
      const maxResult7: number | undefined = Math.max(0, findIndexResult);
      const sliceResult = peers.slice(0, maxResult7);
      const offset = sliceResult.reduce((sum, candidate) => {
        const evaluatedValue11: number = sum + (extents.get(candidate.id) ?? 0);
        return evaluatedValue11;
      }, 0);
      if (group.dock === 'left') {
        rects.push({ left: offset, top: fullRowPriority ? insets.top : 0, right: offset + extent, bottom: workspaceSize.height - (fullRowPriority ? insets.bottom : 0) });
      }
      if (group.dock === 'right') {
        rects.push({ left: workspaceSize.width - offset - extent, top: fullRowPriority ? insets.top : 0, right: workspaceSize.width - offset, bottom: workspaceSize.height - (fullRowPriority ? insets.bottom : 0) });
      }
      if (group.dock === 'top') {
        rects.push({ left: 0, top: offset, right: workspaceSize.width, bottom: offset + extent });
      }
      if (group.dock === 'bottom') {
        rects.push({ left: 0, top: workspaceSize.height - offset - extent, right: workspaceSize.width, bottom: workspaceSize.height - offset });
      }
    }
    return rects;
  }
  function placeFreeGroup(group: DockablePanelGroupState): void {
    if (group.dock !== 'free') {
      return;
    }
    const minResult4: number = Math.min(group.width, workspaceSize.width);
    const width = Math.max(240, minResult4);
    const minResult5: number = Math.min(group.height, workspaceSize.height);
    const height = Math.max(160, minResult5);
    const current: LayoutRect = { left: group.x, top: group.y, right: group.x + width, bottom: group.y + height };
    const blockers = occupiedRects(group.id);
    const outside = current.left < 0 || current.top < 0
      || current.right > workspaceSize.width || current.bottom > workspaceSize.height;
    const crowded = outside || blockers.some((blocker) => {
      const evaluatedValue12: boolean = overlapArea(current, blocker) > rectArea(current) * .22;
      return evaluatedValue12;
    });
    if (!crowded) {
      return;
    }
    const visibleLayoutGroupsResult = visibleLayoutGroups(group.id);
    const edgeGroups = visibleLayoutGroupsResult.filter((candidate) => candidate.dock === 'left'
      || candidate.dock === 'right'
      || candidate.dock === 'top'
      || candidate.dock === 'bottom');
    const edgeInset = (dock: 'left' | 'right' | 'top' | 'bottom'): number => {
      const peers = edgeGroups.filter((candidate) => candidate.dock === dock);
      const total = peers.reduce((sum, candidate) => {
        const evaluatedValue13: number = sum + edgeGroupExtent(candidate);
        return evaluatedValue13;
      }, 0);
      const capacity = dock === 'left' || dock === 'right' ? workspaceSize.width : workspaceSize.height;
      const scale = total > capacity && total > 0 ? capacity / total : 1;
      const reduceResult: number = peers.reduce((sum, candidate) => {
        const evaluatedValue14: number = sum + edgeGroupExtent(candidate) * scale;
        return evaluatedValue14;
      }, 0);
      return reduceResult;
    };
    const leftInset = edgeInset('left');
    const rightInset = edgeInset('right');
    const topInset = edgeInset('top');
    const bottomInset = edgeInset('bottom');
    const maxResult8: number = Math.max(12, leftInset + 12);
    const maxResult9: number = Math.max(12, workspaceSize.width - width - 12);
    const minLeft = Math.min(maxResult8, maxResult9);
    const maxResult10: number = Math.max(12, topInset + 12);
    const maxResult11: number = Math.max(12, workspaceSize.height - height - 12);
    const minTop = Math.min(maxResult10, maxResult11);
    const maxLeft = Math.max(minLeft, workspaceSize.width - rightInset - width - 12);
    const maxTop = Math.max(minTop, workspaceSize.height - bottomInset - height - 12);
    const candidates: Array<{
      x: number;
      y: number;
      distance: number;
    }> = [];
    for (let y = minTop; y <= maxTop; y += 24) {
      for (let x = minLeft; x <= maxLeft; x += 24) {
        const evaluatedValue15: {
          x: number;
          y: number;
          distance: number;
        } = { x, y, distance: Math.abs(x - group.x) + Math.abs(y - group.y) };
        candidates.push(evaluatedValue15);
      }
    }
    candidates.sort((first, second) => first.distance - second.distance);
    const candidate = candidates.find((position) => {
      const rect = { left: position.x, top: position.y, right: position.x + width, bottom: position.y + height };
      const everyResult: boolean = blockers.every((blocker) => {
        const evaluatedValue16: boolean = overlapArea(rect, blocker) <= 0;
        return evaluatedValue16;
      });
      return everyResult;
    });
    if (candidate) {
      group.x = candidate.x;
      group.y = candidate.y;
    }
  }
  function updateWorkspaceSize(element: HTMLElement): void {
    const nextSize = {
      width: Math.max(1, element.clientWidth),
      height: Math.max(1, element.clientHeight),
    };
    const previousSize = !hasMeasuredWorkspace && loadedLayouts.savedWorkspaceSize
      ? loadedLayouts.savedWorkspaceSize
      : { ...workspaceSize };
    const previousInsets = workspaceInsetsForSize(previousSize);
    const sizeChanged = nextSize.width !== workspaceSize.width || nextSize.height !== workspaceSize.height;
    const geometrySizeChanged = nextSize.width !== previousSize.width || nextSize.height !== previousSize.height;
    workspaceSize.width = nextSize.width;
    workspaceSize.height = nextSize.height;
    if (sizeChanged && !geometryCustomized[displayMode.value]) {
      const next = createModeLayout(displayMode.value, workspaceSize);
      layout.groups = next.groups;
      layout.panels = next.panels;
      layout.nextGroupNumber = next.nextGroupNumber;
    }
    else {
      if (geometrySizeChanged && (hasMeasuredWorkspace || loadedLayouts.savedWorkspaceSize)) {
        const argumentValue10: DockablePanelGroupState[] = layout.groups;
        const argumentValue11: WorkspaceSize = previousSize;
        const argumentValue12: WorkspaceSize = nextSize;
        const argumentValue13: WorkspaceInsets = previousInsets;
        const workspaceInsetsForSizeResult: WorkspaceInsets = workspaceInsetsForSize(nextSize);
        resizeFreeDockableGroups(argumentValue10, argumentValue11, argumentValue12, argumentValue13, workspaceInsetsForSizeResult);
      }
      clampGroupsToWorkspace();
    }
    if (displayMode.value === 'debug') {
      fitDebugDockableWorkspaceLayout(layout, workspaceSize);
    }
    hasMeasuredWorkspace = true;
    for (const group of layout.groups) {
      if (group.dock === 'free' && group.panelIds.some((panelId) => layout.panels[panelId].visible)) {
        placeFreeGroup(group);
      }
    }
  }
  watch(layout, scheduleSave, { deep: true });
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
    displayMode,
    groups,
    visibleGroups,
    workspaceSize,
    updateGroup,
    groupForPanel,
    activatePanel,
    setPanelVisible,
    showPanel,
    hidePanel,
    dockGroup,
    snapGroup,
    combinePanel,
    splitPanel,
    detachPanel,
    focusGroup,
    resetLayout,
    setDisplayMode,
  };
}
