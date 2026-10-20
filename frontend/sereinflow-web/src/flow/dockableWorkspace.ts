import type { Component } from 'vue';
export type WorkspacePanelId = 'canvas' | 'nodes' | 'inspector' | 'output' | 'diagnostics' | 'debug' | 'workpieces' | 'variables';
export type DockPosition = 'free' | 'left' | 'right' | 'top' | 'bottom' | 'fill';
export type DockableWorkspaceMode = 'edit' | 'debug';
export interface WorkspaceSize {
  width: number;
  height: number;
}
export interface WorkspaceInsets {
  left: number;
  right: number;
  top: number;
  bottom: number;
}
export interface DockablePanelGroupState {
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
}
export interface DockablePanelPlacement {
  visible: boolean;
  groupId: string;
}
export interface DockableWorkspaceLayout {
  formatVersion: 1;
  groups: DockablePanelGroupState[];
  panels: Record<WorkspacePanelId, DockablePanelPlacement>;
  nextGroupNumber: number;
}
export interface WorkspacePanelTab {
  id: WorkspacePanelId;
  label: string;
  icon: Component;
  closable?: boolean;
}
export const workspacePanelIds: WorkspacePanelId[] = [
  'canvas',
  'nodes',
  'inspector',
  'output',
  'diagnostics',
  'debug',
  'workpieces',
  'variables',
];
function group(id: string, panelIds: WorkspacePanelId[], geometry: Pick<DockablePanelGroupState, 'x' | 'y' | 'width' | 'height' | 'dock'>, zIndex: number): DockablePanelGroupState {
  return {
    id,
    ...geometry,
    panelIds,
    activePanelId: panelIds[0]!,
    zIndex,
    collapsed: false,
  };
}
export function createDefaultDockableWorkspaceLayout(size: WorkspaceSize = { width: 1920, height: 980 }): DockableWorkspaceLayout {
  const width = Math.max(720, size.width);
  const height = Math.max(480, size.height);
  const sideWidth = Math.round(width * .2);
  const roundResult: number = Math.round(height * .16);
  const maxResult: number = Math.max(150, roundResult);
  const diagnosticsHeight = Math.min(220, maxResult);
  const resolveGroupResult = () => {
    const argumentValue1: WorkspacePanelId[] = ['debug'];
    const resolveMaxResult = () => {
      const roundResult10: number = Math.round(width * .32);
      const maxResult2: number = Math.max(320, roundResult10);
      return maxResult2;
    };
    const resolveMinResult = () => {
      const roundResult11: number = Math.round(width * .38);
      const maxResult3: number = Math.max(380, roundResult11);
      const minResult: number = Math.min(520, maxResult3);
      return minResult;
    };
    const evaluatedValue11: Pick<DockablePanelGroupState, "x" | "y" | "width" | "height" | "dock"> = { x: resolveMaxResult(), y: 96, width: resolveMinResult(), height: Math.min(620, height - 32), dock: 'free' };
    const groupResult = group('debug', argumentValue1, evaluatedValue11, 14);
    return groupResult;
  };
  const resolveGroupResult1 = () => {
    const argumentValue2: WorkspacePanelId[] = ['workpieces'];
    const resolveMaxResult1 = () => {
      const roundResult12: number = Math.round(width * .38);
      const maxResult4: number = Math.max(320, roundResult12);
      return maxResult4;
    };
    const resolveMinResult1 = () => {
      const roundResult13: number = Math.round(width * .42);
      const maxResult5: number = Math.max(420, roundResult13);
      const minResult1: number = Math.min(600, maxResult5);
      return minResult1;
    };
    const evaluatedValue12: Pick<DockablePanelGroupState, "x" | "y" | "width" | "height" | "dock"> = { x: resolveMaxResult1(), y: 118, width: resolveMinResult1(), height: Math.min(520, height - 40), dock: 'free' };
    const groupResult1 = group('workpieces', argumentValue2, evaluatedValue12, 15);
    return groupResult1;
  };
  const resolveGroupResult2 = () => {
    const argumentValue3: WorkspacePanelId[] = ['variables'];
    const resolveMaxResult2 = () => {
      const roundResult14: number = Math.round(width * .36);
      const maxResult6: number = Math.max(320, roundResult14);
      return maxResult6;
    };
    const resolveMinResult2 = () => {
      const roundResult15: number = Math.round(width * .44);
      const maxResult7: number = Math.max(420, roundResult15);
      const minResult2: number = Math.min(620, maxResult7);
      return minResult2;
    };
    const evaluatedValue13: Pick<DockablePanelGroupState, "x" | "y" | "width" | "height" | "dock"> = { x: resolveMaxResult2(), y: 132, width: resolveMinResult2(), height: Math.min(520, height - 40), dock: 'free' };
    const groupResult2 = group('variables', argumentValue3, evaluatedValue13, 16);
    return groupResult2;
  };
  const groups = [
    group('canvas', ['canvas'], { x: 0, y: 0, width, height, dock: 'fill' }, 1),
    group('nodes', ['nodes'], { x: 0, y: 0, width: sideWidth, height, dock: 'left' }, 10),
    group('inspector', ['inspector'], { x: 0, y: 0, width: sideWidth, height, dock: 'right' }, 11),
    group('output', ['output'], { x: 0, y: 0, width, height: 220, dock: 'bottom' }, 12),
    group('diagnostics', ['diagnostics'], { x: 0, y: 0, width, height: diagnosticsHeight, dock: 'bottom' }, 13),
    resolveGroupResult(),
    resolveGroupResult1(),
    resolveGroupResult2(),
  ];
  groups.find((item) => item.id === 'diagnostics')!.collapsed = true;
  return {
    formatVersion: 1,
    groups,
    panels: {
      canvas: { visible: true, groupId: 'canvas' },
      nodes: { visible: true, groupId: 'nodes' },
      inspector: { visible: true, groupId: 'inspector' },
      output: { visible: false, groupId: 'output' },
      diagnostics: { visible: false, groupId: 'diagnostics' },
      debug: { visible: false, groupId: 'debug' },
      workpieces: { visible: false, groupId: 'workpieces' },
      variables: { visible: false, groupId: 'variables' },
    },
    nextGroupNumber: 1,
  };
}
export function createDebugDockableWorkspaceLayout(size: WorkspaceSize = { width: 1920, height: 980 }): DockableWorkspaceLayout {
  const layout = createDefaultDockableWorkspaceLayout(size);
  const width = Math.max(720, size.width);
  const height = Math.max(480, size.height);
  const leftWidth = Math.round(width * .5);
  const canvasHeight = Math.round(height * .5);
  const workpiecesHeight = height - canvasHeight;
  const canvasGroup = layout.groups.find((item) => item.id === 'canvas')!;
  const workpiecesGroup = layout.groups.find((item) => item.id === 'workpieces')!;
  const debugGroup = layout.groups.find((item) => item.id === 'debug')!;
  Object.assign(canvasGroup, {
    x: 0,
    y: 0,
    width: leftWidth,
    height: canvasHeight,
    dock: 'free' as const,
  });
  Object.assign(workpiecesGroup, {
    x: 0,
    y: canvasHeight,
    width: leftWidth,
    height: workpiecesHeight,
    dock: 'free' as const,
  });
  Object.assign(debugGroup, {
    x: leftWidth,
    y: 0,
    width: width - leftWidth,
    height,
    dock: 'right' as const,
  });
  for (const panelId of ['nodes', 'inspector', 'output', 'diagnostics'] as const) {
    layout.panels[panelId].visible = false;
  }
  layout.panels.canvas.visible = true;
  layout.panels.workpieces.visible = true;
  layout.panels.debug.visible = true;
  return layout;
}
// Keep the two free panels in the debugger's left column attached to each other
// and to the right-hand session panel after a workspace resize or an older saved layout.
export function fitDebugDockableWorkspaceLayout(layout: DockableWorkspaceLayout, size: WorkspaceSize): void {
  const canvas = layout.groups.find((group) => group.id === layout.panels.canvas.groupId);
  const workpieces = layout.groups.find((group) => group.id === layout.panels.workpieces.groupId);
  const session = layout.groups.find((group) => group.id === layout.panels.debug.groupId);
  if (!canvas || !workpieces || !session
    || !layout.panels.canvas.visible || !layout.panels.workpieces.visible || !layout.panels.debug.visible
    || canvas.collapsed || workpieces.collapsed || session.collapsed
    || canvas.id === workpieces.id || canvas.dock !== 'free' || workpieces.dock !== 'free' || session.dock !== 'right') {
    return;
  }
  const maxGap = Math.max(120, size.height * .15);
  if (Math.abs(canvas.x) > 8 || Math.abs(canvas.y) > 8 || Math.abs(workpieces.x) > 8
    || Math.abs(canvas.width - workpieces.width) > Math.max(120, size.width * .1)
    || workpieces.y < canvas.height - 8 || workpieces.y > canvas.height + maxGap) {
    return;
  }
  const columnWidth = size.width - Math.min(session.width, size.width);
  if (columnWidth < 240 || size.height < 320) {
    return;
  }
  const argumentValue: number = size.height - 160;
  const evaluatedValue: number = size.height * canvas.height / Math.max(1, canvas.height + workpieces.height);
  const roundResult1: number = Math.round(evaluatedValue);
  const maxResult1: number = Math.max(160, roundResult1);
  const split = Math.min(argumentValue, maxResult1);
  Object.assign(canvas, { x: 0, y: 0, width: columnWidth, height: split });
  Object.assign(workpieces, { x: 0, y: split, width: columnWidth, height: size.height - split });
}
export function resizeFreeDockableGroups(groups: DockablePanelGroupState[], previousSize: WorkspaceSize, nextSize: WorkspaceSize, previousInsets: WorkspaceInsets, nextInsets: WorkspaceInsets): void {
  const map = (value: number, oldStart: number, oldEnd: number, oldLimit: number, newStart: number, newEnd: number, newLimit: number): number => {
    if (oldEnd <= oldStart) {
      const evaluatedValue1: number = value * newLimit / Math.max(1, oldLimit);
      const roundResult2: number = Math.round(evaluatedValue1);
      return roundResult2;
    }
    if (value < oldStart) {
      const evaluatedValue2: number = value * newStart / Math.max(1, oldStart);
      const roundResult3: number = Math.round(evaluatedValue2);
      return roundResult3;
    }
    if (value > oldEnd) {
      const evaluatedValue3: number = newEnd + (value - oldEnd) * (newLimit - newEnd) / Math.max(1, oldLimit - oldEnd);
      const roundResult4: number = Math.round(evaluatedValue3);
      return roundResult4;
    }
    const roundResult5: number = Math.round(newStart + (value - oldStart) * (newEnd - newStart) / (oldEnd - oldStart));
    return roundResult5;
  };
  const oldRight = previousSize.width - previousInsets.right;
  const newRight = nextSize.width - nextInsets.right;
  const oldBottom = previousSize.height - previousInsets.bottom;
  const newBottom = nextSize.height - nextInsets.bottom;
  for (const group of groups) {
    if (group.dock !== 'free') {
      continue;
    }
    const x = map(group.x, previousInsets.left, oldRight, previousSize.width, nextInsets.left, newRight, nextSize.width);
    const right = map(group.x + group.width, previousInsets.left, oldRight, previousSize.width, nextInsets.left, newRight, nextSize.width);
    const y = map(group.y, previousInsets.top, oldBottom, previousSize.height, nextInsets.top, newBottom, nextSize.height);
    const bottom = map(group.y + group.height, previousInsets.top, oldBottom, previousSize.height, nextInsets.top, newBottom, nextSize.height);
    Object.assign(group, { x, y, width: right - x, height: bottom - y });
  }
}
export function createSnapshotDockableWorkspaceLayout(size: WorkspaceSize = { width: 1920, height: 980 }): DockableWorkspaceLayout {
  const width = Math.max(240, size.width);
  const height = Math.max(480, size.height);
  const groups = width < 900
    ? (() => {
      const roundResult6: number = Math.round(height * .44);
      const canvasHeight = Math.max(200, roundResult6);
      const roundResult7: number = Math.round(height * .24);
      const workpiecesHeight = Math.max(150, roundResult7);
      const resolveGroupResult3 = () => {
        const argumentValue4: WorkspacePanelId[] = ['debug', 'inspector', 'output'];
        const evaluatedValue14: Pick<DockablePanelGroupState, "x" | "y" | "width" | "height" | "dock"> = { x: 0, y: canvasHeight + workpiecesHeight, width, height: Math.max(160, height - canvasHeight - workpiecesHeight), dock: 'free' };
        const groupResult3 = group('snapshot-session', argumentValue4, evaluatedValue14, 12);
        return groupResult3;
      };
      const evaluatedValue4 = [
        group('snapshot-canvas', ['canvas'], { x: 0, y: 0, width, height: canvasHeight, dock: 'free' }, 10),
        group('snapshot-workpieces', ['workpieces', 'variables'], { x: 0, y: canvasHeight, width, height: workpiecesHeight, dock: 'free' }, 11),
        resolveGroupResult3(),
      ];
      return evaluatedValue4;
    })()
    : (() => {
      const roundResult8: number = Math.round(width * .52);
      const leftWidth = Math.max(360, roundResult8);
      const roundResult9: number = Math.round(height * .62);
      const canvasHeight = Math.max(240, roundResult9);
      const workpiecesHeight = Math.max(160, height - canvasHeight);
      const sessionWidth = Math.max(320, width - leftWidth);
      const evaluatedValue5 = [
        group('snapshot-canvas', ['canvas'], { x: 0, y: 0, width: leftWidth, height: canvasHeight, dock: 'free' }, 10),
        group('snapshot-workpieces', ['workpieces', 'variables'], { x: 0, y: canvasHeight, width: leftWidth, height: workpiecesHeight, dock: 'free' }, 11),
        group('snapshot-session', ['debug', 'inspector', 'output'], { x: leftWidth, y: 0, width: sessionWidth, height, dock: 'right' }, 12),
      ];
      return evaluatedValue5;
    })();
  return {
    formatVersion: 1,
    groups,
    panels: {
      canvas: { visible: true, groupId: 'snapshot-canvas' },
      nodes: { visible: false, groupId: 'nodes' },
      inspector: { visible: true, groupId: 'snapshot-session' },
      output: { visible: true, groupId: 'snapshot-session' },
      diagnostics: { visible: false, groupId: 'diagnostics' },
      debug: { visible: true, groupId: 'snapshot-session' },
      workpieces: { visible: true, groupId: 'snapshot-workpieces' },
      variables: { visible: false, groupId: 'snapshot-workpieces' },
    },
    nextGroupNumber: 1,
  };
}
function isPanelId(value: unknown): value is WorkspacePanelId {
  const evaluatedValue6: boolean = typeof value === 'string' && workspacePanelIds.includes(value as WorkspacePanelId);
  return evaluatedValue6;
}
function isDockPosition(value: unknown): value is DockPosition {
  return value === 'free'
    || value === 'left'
    || value === 'right'
    || value === 'top'
    || value === 'bottom'
    || value === 'fill';
}
function isFiniteNumber(value: unknown): value is number {
  const evaluatedValue7: boolean = typeof value === 'number' && Number.isFinite(value);
  return evaluatedValue7;
}
function isGroup(value: unknown): value is DockablePanelGroupState {
  if (!value || typeof value !== 'object') {
    return false;
  }
  const candidate = value as Partial<DockablePanelGroupState>;
  const evaluatedValue8: boolean = typeof candidate.id === 'string'
    && isFiniteNumber(candidate.x)
    && isFiniteNumber(candidate.y)
    && isFiniteNumber(candidate.width)
    && isFiniteNumber(candidate.height)
    && isDockPosition(candidate.dock)
    && Array.isArray(candidate.panelIds)
    && candidate.panelIds.length > 0
    && candidate.panelIds.every(isPanelId)
    && isPanelId(candidate.activePanelId)
    && isFiniteNumber(candidate.zIndex)
    && typeof candidate.collapsed === 'boolean';
  return evaluatedValue8;
}
export function normalizeDockableWorkspaceLayout(value: unknown, size: WorkspaceSize, fallback = createDefaultDockableWorkspaceLayout(size)): DockableWorkspaceLayout | undefined {
  if (!value || typeof value !== 'object') {
    return undefined;
  }
  const candidate = value as Partial<DockableWorkspaceLayout>;
  if (candidate.formatVersion !== 1 || !Array.isArray(candidate.groups) || !candidate.panels || typeof candidate.panels !== 'object') {
    return undefined;
  }
  const defaults = fallback;
  const validGroups = candidate.groups.filter(isGroup);
  if (validGroups.length === 0) {
    return undefined;
  }
  const groups = defaults.groups.map((defaultGroup) => {
    const storedGroup = validGroups.find((item) => item.id === defaultGroup.id);
    return storedGroup ? {
      ...defaultGroup,
      ...storedGroup,
      panelIds: [...storedGroup.panelIds],
    } : defaultGroup;
  });
  const mapResult: Iterable<string> | null | undefined = groups.map((item) => item.id);
  const knownGroupIds = new Set(mapResult);
  for (const storedGroup of validGroups) {
    if (!knownGroupIds.has(storedGroup.id)) {
      groups.push({ ...storedGroup, panelIds: [...storedGroup.panelIds] });
    }
  }
  const panels = { ...defaults.panels };
  for (const panelId of workspacePanelIds) {
    const stored = (candidate.panels as Record<string, unknown>)[panelId];
    if (!stored || typeof stored !== 'object') {
      continue;
    }
    const placement = stored as Partial<DockablePanelPlacement>;
    if (typeof placement.visible === 'boolean' && typeof placement.groupId === 'string') {
      panels[panelId] = { visible: placement.visible, groupId: placement.groupId };
    }
  }
  for (const panelId of workspacePanelIds) {
    const placement = panels[panelId];
    let targetGroup = groups.find((item) => item.id === placement.groupId);
    if (!targetGroup) {
      targetGroup = groups.find((item) => {
        const includesResult: boolean = item.panelIds.includes(panelId);
        return includesResult;
      }) ?? groups.find((item) => {
        const evaluatedValue9: boolean = item.id === defaultGroupForPanel(panelId);
        return evaluatedValue9;
      });
    }
    if (!targetGroup) {
      continue;
    }
    for (const item of groups) {
      if (item !== targetGroup) {
        item.panelIds = item.panelIds.filter((id) => id !== panelId);
      }
    }
    if (!targetGroup.panelIds.includes(panelId)) {
      targetGroup.panelIds.push(panelId);
    }
    panels[panelId] = { ...placement, groupId: targetGroup.id };
    if (!targetGroup.panelIds.includes(targetGroup.activePanelId)) {
      targetGroup.activePanelId = panelId;
    }
  }
  for (const item of groups) {
    if (!item.panelIds.includes(item.activePanelId)) {
      item.activePanelId = item.panelIds[0]!;
    }
  }
  const evaluatedValue10: DockableWorkspaceLayout | undefined = {
    formatVersion: 1,
    groups,
    panels,
    nextGroupNumber: isFiniteNumber(candidate.nextGroupNumber) && candidate.nextGroupNumber > 0
      ? Math.floor(candidate.nextGroupNumber)
      : 1,
  };
  return evaluatedValue10;
}
function defaultGroupForPanel(panelId: WorkspacePanelId): string {
  return panelId;
}
