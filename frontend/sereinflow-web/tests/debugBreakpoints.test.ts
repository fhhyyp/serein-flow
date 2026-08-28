import assert from 'node:assert/strict'
import test from 'node:test'
import { normalizeBreakpointNodeIds } from '../src/flow/debugBreakpoints.ts'

test('debug breakpoints are sorted, de-duplicated, and constrained to current flow nodes', () => {
  const normalized = normalizeBreakpointNodeIds(
    [' node-b ', 'node-a', 'node-b', '', 'removed-node'],
    new Set(['node-a', 'node-b']),
  )

  assert.deepEqual(normalized, ['node-a', 'node-b'])
})

test('debug breakpoint normalization retains all valid node IDs when no flow filter is supplied', () => {
  assert.deepEqual(
    normalizeBreakpointNodeIds(['node-c', 'node-a', 'node-c']),
    ['node-a', 'node-c'],
  )
})
