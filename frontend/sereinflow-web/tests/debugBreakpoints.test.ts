import assert from 'node:assert/strict';
import test from 'node:test';
import { normalizeBreakpointNodeIds } from '../src/flow/debugBreakpoints.ts';
test('debug breakpoints are sorted, de-duplicated, and constrained to current flow nodes', () => {
  const argumentValue: readonly string[] = [' node-b ', 'node-a', 'node-b', '', 'removed-node'];
  const setInstance: ReadonlySet<string> | undefined = new Set(['node-a', 'node-b']);
  const normalized = normalizeBreakpointNodeIds(argumentValue, setInstance);
  assert.deepEqual(normalized, ['node-a', 'node-b']);
});
test('debug breakpoint normalization retains all valid node IDs when no flow filter is supplied', () => {
  const normalizeBreakpointNodeIdsResult = normalizeBreakpointNodeIds(['node-c', 'node-a', 'node-c']);
  assert.deepEqual(normalizeBreakpointNodeIdsResult, ['node-a', 'node-c']);
});
