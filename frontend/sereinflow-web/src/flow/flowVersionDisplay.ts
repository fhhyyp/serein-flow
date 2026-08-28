import type { FlowVersionOperation, FlowVersionTrack } from '../api/flowApi'

export function flowVersionLabel(version: number | undefined): string {
  return version && version > 0 ? `v${version}` : '—'
}

export function flowVersionTrackKey(track: FlowVersionTrack): 'version.track.development' | 'version.track.production' {
  return track === 'production' ? 'version.track.production' : 'version.track.development'
}

export function flowVersionOperationKey(operation: FlowVersionOperation): string {
  return `version.operation.${operation}`
}

export function flowVersionSourceLabel(sourceVersion: number | undefined): string | undefined {
  return sourceVersion && sourceVersion > 0 ? `v${sourceVersion}` : undefined
}
