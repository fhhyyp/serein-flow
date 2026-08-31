import type { CanvasState } from './types'
import type { CanvasFocusSettings } from './canvasFocus'
import type { ConnectionLineSettings } from './connectionLine'
import type { FlowConcurrencyMode } from '../api/flowApi'

export interface WorkspaceSnapshot {
  canvases: CanvasState[]
  activeCanvasId: string
  nextNodeNumber: number
  entryNodeId?: string
  projectName?: string
  connectionLineTypes?: ConnectionLineSettings
  canvasFocusSettings?: CanvasFocusSettings
  runPolicy?: {
    concurrencyMode: FlowConcurrencyMode
  }
}

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

export function cloneWorkspaceSnapshot(snapshot: WorkspaceSnapshot): WorkspaceSnapshot {
  return clone(snapshot)
}

export function workspaceFingerprint(snapshot: WorkspaceSnapshot): string {
  return JSON.stringify(snapshot)
}

export class WorkspaceHistory {
  private readonly past: WorkspaceSnapshot[] = []
  private readonly future: WorkspaceSnapshot[] = []
  private readonly limit: number

  public constructor(limit = 50) {
    this.limit = limit
  }

  public get canUndo(): boolean {
    return this.past.length > 0
  }

  public get canRedo(): boolean {
    return this.future.length > 0
  }

  public record(beforeChange: WorkspaceSnapshot): void {
    const copy = cloneWorkspaceSnapshot(beforeChange)
    const latest = this.past.at(-1)
    if (latest && workspaceFingerprint(latest) === workspaceFingerprint(copy)) {
      return
    }

    this.past.push(copy)
    if (this.past.length > this.limit) {
      this.past.shift()
    }
    this.future.length = 0
  }

  public undo(current: WorkspaceSnapshot): WorkspaceSnapshot | undefined {
    const previous = this.past.pop()
    if (!previous) {
      return undefined
    }

    this.future.push(cloneWorkspaceSnapshot(current))
    return cloneWorkspaceSnapshot(previous)
  }

  public redo(current: WorkspaceSnapshot): WorkspaceSnapshot | undefined {
    const next = this.future.pop()
    if (!next) {
      return undefined
    }

    this.past.push(cloneWorkspaceSnapshot(current))
    return cloneWorkspaceSnapshot(next)
  }

  public clear(): void {
    this.past.length = 0
    this.future.length = 0
  }
}
