import assert from 'node:assert/strict';
import test from 'node:test';
import { isArchivedProjectStatus } from '../src/flow/projectStatus.ts';
test('recognizes archived project status regardless of legacy response casing', () => {
  const isArchivedProjectStatusResult = isArchivedProjectStatus('archived');
  assert.equal(isArchivedProjectStatusResult, true);
  const isArchivedProjectStatusResult1 = isArchivedProjectStatus('Archived');
  assert.equal(isArchivedProjectStatusResult1, true);
  const isArchivedProjectStatusResult2 = isArchivedProjectStatus(' ARCHIVED ');
  assert.equal(isArchivedProjectStatusResult2, true);
  const isArchivedProjectStatusResult3 = isArchivedProjectStatus('ready');
  assert.equal(isArchivedProjectStatusResult3, false);
});
