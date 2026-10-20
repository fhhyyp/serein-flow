import assert from 'node:assert/strict';
import test from 'node:test';
import { parseRuntimeLog } from '../src/flow/runtimeLog.ts';
test('runtime log events retain their severity and message', () => {
  const parseRuntimeLogResult = parseRuntimeLog('log', '{"level":"error","message":"Device unavailable"}');
  assert.deepEqual(parseRuntimeLogResult, { level: 'error', message: 'Device unavailable' });
  const parseRuntimeLogResult1 = parseRuntimeLog('node.log', '{"level":"info","message":"Started"}');
  assert.deepEqual(parseRuntimeLogResult1, { level: 'info', message: 'Started' });
});
test('runtime log parser ignores unrelated or malformed event payloads', () => {
  const parseRuntimeLogResult2 = parseRuntimeLog('node.completed', '{"level":"error"}');
  assert.equal(parseRuntimeLogResult2, undefined);
  const parseRuntimeLogResult3 = parseRuntimeLog('log', 'not json');
  assert.equal(parseRuntimeLogResult3, undefined);
  const parseRuntimeLogResult4 = parseRuntimeLog('log', '{}');
  assert.deepEqual(parseRuntimeLogResult4, { level: 'info', message: undefined });
});
