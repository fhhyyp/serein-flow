import assert from 'node:assert/strict'
import test from 'node:test'
import { buildNodeExecutionStates, sortNodeExecutionStates } from '../src/flow/nodeExecutionState.ts'
import type { FlowRunEventDto } from '../src/api/flowApi.ts'

function event(
  sequence: number,
  type: string,
  payload: unknown,
  nodeId = 'node-a',
): FlowRunEventDto {
  return {
    runId: 'run-1',
    sequence,
    timestamp: `2026-08-28T08:00:0${sequence}.000Z`,
    type,
    nodeId,
    payloadJson: typeof payload === 'string' ? payload : JSON.stringify(payload),
  }
}

test('builds one execution state from start, pause, and terminal node events', () => {
  const states = buildNodeExecutionStates([
    event(1, 'node.started', { step: 4, frameDepth: 1, triggerInvocationId: 'trigger-1' }),
    event(2, 'debug.paused', { nodeId: 'node-a', step: 4, frameDepth: 1, triggerInvocationId: 'trigger-1', inputs: { count: 42 } }),
    event(3, 'node.completed', { step: 4, frameDepth: 1, triggerInvocationId: 'trigger-1', branch: 'Success', outputs: { result: 'ok' } }),
  ])

  assert.equal(states.length, 1)
  assert.deepEqual(states[0], {
    id: 'node-a:trigger-1:step-4',
    nodeId: 'node-a',
    status: 'completed',
    startedAt: '2026-08-28T08:00:01.000Z',
    endedAt: '2026-08-28T08:00:03.000Z',
    startSequence: 1,
    terminalSequence: 3,
    pauseSequence: 2,
    step: 4,
    frameDepth: 1,
    triggerInvocationId: 'trigger-1',
    branch: 'Success',
    inputs: { count: 42 },
    outputs: { result: 'ok' },
    isGlobal: undefined,
  })
})

test('keeps repeated node executions distinct and carries terminal failure details', () => {
  const states = buildNodeExecutionStates([
    event(2, 'node.started', { step: 2, triggerInvocationId: 'first' }),
    event(3, 'node.failed', { step: 2, triggerInvocationId: 'first', branch: 'Failure', errorCode: 'first.failed', errorMessage: 'First failed', inputs: { value: 1 }, outputs: {} }),
    event(4, 'node.started', { step: 4, triggerInvocationId: 'second' }),
    event(5, 'node.error', { step: 4, triggerInvocationId: 'second', branch: 'Error', errorCode: 'second.error', errorMessage: 'Second errored', inputs: { value: 2 }, outputs: {} }),
  ])

  assert.equal(states.length, 2)
  assert.deepEqual(states.map((state) => ({ id: state.id, status: state.status, branch: state.branch, errorCode: state.errorCode })), [
    { id: 'node-a:first:step-2', status: 'failed', branch: 'Failure', errorCode: 'first.failed' },
    { id: 'node-a:second:step-4', status: 'error', branch: 'Error', errorCode: 'second.error' },
  ])
})

test('accepts legacy events and ignores malformed payloads without throwing', () => {
  const states = buildNodeExecutionStates([
    event(1, 'node.started', { step: 1 }),
    event(2, 'node.completed', { branch: 'Success', outputs: { done: true } }),
    event(3, 'node.started', 'not json', 'node-b'),
    event(4, 'node.completed', { outputs: { done: false } }, 'node-b'),
    event(5, 'node.started', { step: 5 }, 'node-c'),
    event(6, 'debug.paused', 'invalid JSON', 'node-c'),
  ])

  assert.equal(states.length, 3)
  assert.equal(states[0].status, 'completed')
  assert.equal(states[0].step, 1)
  assert.equal(states[0].triggerInvocationId, undefined)
  assert.deepEqual(states[0].outputs, { done: true })
  assert.equal(states[1].status, 'completed')
  assert.equal(states[2].status, 'paused')
})

test('orders execution steps in ascending flow step order', () => {
  const states = buildNodeExecutionStates([
    event(1, 'node.started', { step: 3 }, 'node-c'),
    event(2, 'node.started', { step: 1 }, 'node-a'),
    event(3, 'node.started', { step: 2 }, 'node-b'),
  ])

  assert.deepEqual(sortNodeExecutionStates(states).map((state) => state.step), [1, 2, 3])
})
