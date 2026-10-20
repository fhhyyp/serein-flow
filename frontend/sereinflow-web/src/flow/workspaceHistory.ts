import type { CanvasState } from './types';
import type { CanvasFocusSettings } from './canvasFocus';
import type { ConnectionLineSettings } from './connectionLine';
import type { FlowConcurrencyMode } from '../api/flowApi';
export interface WorkspaceSnapshot {
  canvases: CanvasState[];
  activeCanvasId: string;
  nextNodeNumber: number;
  entryNodeId?: string;
  projectName?: string;
  connectionLineTypes?: ConnectionLineSettings;
  canvasFocusSettings?: CanvasFocusSettings;
  runPolicy?: {
    concurrencyMode: FlowConcurrencyMode;
  };
}
function clone<T>(value: T): T {
  const stringifyResult: string = JSON.stringify(value);
  const evaluatedValue: T = JSON.parse(stringifyResult) as T;
  return evaluatedValue;
}
export function cloneWorkspaceSnapshot(snapshot: WorkspaceSnapshot): WorkspaceSnapshot {
  const cloneResult: WorkspaceSnapshot = clone(snapshot);
  return cloneResult;
}
export function workspaceFingerprint(snapshot: WorkspaceSnapshot): string {
  const stringifyResult1: string = JSON.stringify(snapshot);
  return stringifyResult1;
}
export class WorkspaceHistory {
  private readonly past: WorkspaceSnapshot[] = [];
  private readonly future: WorkspaceSnapshot[] = [];
  private readonly limit: number;
  public constructor(limit = 50) {
    this.limit = limit;
  }
  public get canUndo(): boolean {
    return this.past.length > 0;
  }
  public get canRedo(): boolean {
    return this.future.length > 0;
  }
  public record(beforeChange: WorkspaceSnapshot): void {
    const copy = cloneWorkspaceSnapshot(beforeChange);
    const latest = this.past.at(-1);
    if (latest && workspaceFingerprint(latest) === workspaceFingerprint(copy)) {
      return;
    }
    this.past.push(copy);
    if (this.past.length > this.limit) {
      this.past.shift();
    }
    this.future.length = 0;
  }
  public undo(current: WorkspaceSnapshot): WorkspaceSnapshot | undefined {
    const previous = this.past.pop();
    if (!previous) {
      return undefined;
    }
    const cloneWorkspaceSnapshotResult: WorkspaceSnapshot = cloneWorkspaceSnapshot(current);
    this.future.push(cloneWorkspaceSnapshotResult);
    const cloneWorkspaceSnapshotResult1: WorkspaceSnapshot | undefined = cloneWorkspaceSnapshot(previous);
    return cloneWorkspaceSnapshotResult1;
  }
  public redo(current: WorkspaceSnapshot): WorkspaceSnapshot | undefined {
    const next = this.future.pop();
    if (!next) {
      return undefined;
    }
    const cloneWorkspaceSnapshotResult2: WorkspaceSnapshot = cloneWorkspaceSnapshot(current);
    this.past.push(cloneWorkspaceSnapshotResult2);
    const cloneWorkspaceSnapshotResult3: WorkspaceSnapshot | undefined = cloneWorkspaceSnapshot(next);
    return cloneWorkspaceSnapshotResult3;
  }
  public clear(): void {
    this.past.length = 0;
    this.future.length = 0;
  }
}
