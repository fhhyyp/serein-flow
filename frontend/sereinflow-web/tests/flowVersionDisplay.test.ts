import assert from 'node:assert/strict';
import test from 'node:test';
import { flowVersionLabel, flowVersionOperationKey, flowVersionSourceLabel, flowVersionTrackKey, } from '../src/flow/flowVersionDisplay.ts';
test('flow version display renders stable labels for version heads and sources', () => {
  const flowVersionLabelResult = flowVersionLabel(12);
  assert.equal(flowVersionLabelResult, 'v12');
  const flowVersionLabelResult1 = flowVersionLabel(undefined);
  assert.equal(flowVersionLabelResult1, '—');
  const flowVersionSourceLabelResult = flowVersionSourceLabel(4);
  assert.equal(flowVersionSourceLabelResult, 'v4');
  const flowVersionSourceLabelResult1 = flowVersionSourceLabel(undefined);
  assert.equal(flowVersionSourceLabelResult1, undefined);
});
test('flow version display maps track and operation values to i18n keys', () => {
  const flowVersionTrackKeyResult = flowVersionTrackKey('development');
  assert.equal(flowVersionTrackKeyResult, 'version.track.development');
  const flowVersionTrackKeyResult1 = flowVersionTrackKey('production');
  assert.equal(flowVersionTrackKeyResult1, 'version.track.production');
  const flowVersionOperationKeyResult = flowVersionOperationKey('rolledBack');
  assert.equal(flowVersionOperationKeyResult, 'version.operation.rolledBack');
});
