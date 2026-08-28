import assert from 'node:assert/strict'
import test from 'node:test'
import {
  flowVersionLabel,
  flowVersionOperationKey,
  flowVersionSourceLabel,
  flowVersionTrackKey,
} from '../src/flow/flowVersionDisplay.ts'

test('flow version display renders stable labels for version heads and sources', () => {
  assert.equal(flowVersionLabel(12), 'v12')
  assert.equal(flowVersionLabel(undefined), '—')
  assert.equal(flowVersionSourceLabel(4), 'v4')
  assert.equal(flowVersionSourceLabel(undefined), undefined)
})

test('flow version display maps track and operation values to i18n keys', () => {
  assert.equal(flowVersionTrackKey('development'), 'version.track.development')
  assert.equal(flowVersionTrackKey('production'), 'version.track.production')
  assert.equal(flowVersionOperationKey('rolledBack'), 'version.operation.rolledBack')
})
