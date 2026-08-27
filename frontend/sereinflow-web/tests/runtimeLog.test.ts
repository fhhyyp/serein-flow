import assert from 'node:assert/strict'
import test from 'node:test'
import { parseRuntimeLog } from '../src/flow/runtimeLog.ts'

test('runtime log events retain their severity and message', () => {
  assert.deepEqual(
    parseRuntimeLog('log', '{"level":"error","message":"Device unavailable"}'),
    { level: 'error', message: 'Device unavailable' },
  )
  assert.deepEqual(
    parseRuntimeLog('node.log', '{"level":"info","message":"Started"}'),
    { level: 'info', message: 'Started' },
  )
})

test('runtime log parser ignores unrelated or malformed event payloads', () => {
  assert.equal(parseRuntimeLog('node.completed', '{"level":"error"}'), undefined)
  assert.equal(parseRuntimeLog('log', 'not json'), undefined)
  assert.deepEqual(parseRuntimeLog('log', '{}'), { level: 'info', message: undefined })
})
