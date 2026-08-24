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

  assert.deepEqual(seats.map((seat) => seat.id), ['exec-in', 'param-payload', 'param-mode', 'exec-out', 'data-out'])
  assert.equal(seats.find((seat) => seat.id === 'param-payload')?.handleType, 'target')
  assert.equal(seats.find((seat) => seat.id === 'data-out')?.semantic, 'data')
})

test('trigger nodes have no control input and seat rows never overlap', () => {
  const layout = layoutConnectionSeats({ kind: 'trigger', hasDataOutput: true, parameters: [] })

  assert.deepEqual(layout.map((seat) => seat.id), ['exec-out', 'data-out'])
  assert.ok((layout[1]?.top ?? 0) > (layout[0]?.top ?? 0))
})

test('semantic resolution accepts only matching source and target seats', () => {
  assert.equal(resolveConnectionSemantic('exec-out', 'exec-in'), 'execution')
  assert.equal(resolveConnectionSemantic('data-out', 'param-payload'), 'data')
  assert.equal(resolveConnectionSemantic('exec-out', 'param-payload'), undefined)
  assert.equal(isInputSeat('exec-in'), true)
  assert.equal(isInputSeat('param-payload'), true)
  assert.equal(isInputSeat('data-out'), false)
})
