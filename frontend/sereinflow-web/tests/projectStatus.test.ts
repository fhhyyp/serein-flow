import assert from 'node:assert/strict'
import test from 'node:test'
import { isArchivedProjectStatus } from '../src/flow/projectStatus.ts'

test('recognizes archived project status regardless of legacy response casing', () => {
  assert.equal(isArchivedProjectStatus('archived'), true)
  assert.equal(isArchivedProjectStatus('Archived'), true)
  assert.equal(isArchivedProjectStatus(' ARCHIVED '), true)
  assert.equal(isArchivedProjectStatus('ready'), false)
})
