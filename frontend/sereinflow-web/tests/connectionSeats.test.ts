import assert from 'node:assert/strict'
import test from 'node:test'
import { getConnectionSeats, isInputSeat, layoutConnectionSeats, resolveConnectionSemantic } from '../src/flow/connectionSeats.ts'

test('connection seats expose control, parameter, and result rails in stable order', () => {
  const seats = getConnectionSeats({
    kind: 'action',
    hasDataOutput: true,
    parameters: [
      { id: 'payload', nameKey: 'parameter.payload', valueKind: 'JSON', source: 'literal' },
      { id: 'mode', nameKey: 'parameter.mode', valueKind: 'string', source: 'literal' },
    ],
  })

  assert.deepEqual(seats.map((seat) => seat.id), ['exec-in', 'param-payload', 'param-mode', 'exec-success', 'exec-failure', 'exec-error', 'data-out'])
  assert.equal(seats.find((seat) => seat.id === 'param-payload')?.handleType, 'target')
  assert.equal(seats.find((seat) => seat.id === 'data-out')?.semantic, 'data')
})

test('all nodes expose three execution branches and result rows never overlap', () => {
  const layout = layoutConnectionSeats({ kind: 'flipflop', hasDataOutput: true, parameters: [] })

  assert.deepEqual(layout.map((seat) => seat.id), ['exec-in', 'exec-success', 'exec-failure', 'exec-error', 'data-out'])
  assert.ok((layout.find((seat) => seat.id === 'exec-failure')?.top ?? 0) > (layout.find((seat) => seat.id === 'exec-success')?.top ?? 0))
  assert.ok((layout.find((seat) => seat.id === 'exec-error')?.top ?? 0) > (layout.find((seat) => seat.id === 'exec-failure')?.top ?? 0))
})

test('semantic resolution accepts only matching source and target seats', () => {
  assert.equal(resolveConnectionSemantic('exec-success', 'exec-in'), 'execution')
  assert.equal(resolveConnectionSemantic('exec-failure', 'exec-in'), 'execution')
  assert.equal(resolveConnectionSemantic('exec-error', 'exec-in'), 'execution')
  assert.equal(resolveConnectionSemantic('data-out', 'param-payload'), 'data')
  assert.equal(resolveConnectionSemantic('exec-success', 'param-payload'), undefined)
  assert.equal(isInputSeat('exec-in'), true)
  assert.equal(isInputSeat('param-payload'), true)
  assert.equal(isInputSeat('data-out'), false)
})
