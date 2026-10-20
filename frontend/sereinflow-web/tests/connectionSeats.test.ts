import assert from 'node:assert/strict';
import test from 'node:test';
import { getConnectionSeats, isInputSeat, layoutConnectionSeats, resolveConnectionSemantic } from '../src/flow/connectionSeats.ts';
test('connection seats expose control, parameter, and result rails in stable order', () => {
  const seats = getConnectionSeats({
    kind: 'action',
    hasDataOutput: true,
    parameters: [
      { id: 'payload', nameKey: 'parameter.payload', valueKind: 'JSON', source: 'literal' },
      { id: 'mode', nameKey: 'parameter.mode', valueKind: 'string', source: 'literal' },
    ],
  });
  const mapResult = seats.map((seat) => seat.id);
  assert.deepEqual(mapResult, ['exec-in', 'param-payload', 'param-mode', 'exec-success', 'exec-failure', 'exec-error', 'data-out']);
  const evaluatedValue = seats.find((seat) => seat.id === 'param-payload')?.handleType;
  assert.equal(evaluatedValue, 'target');
  const evaluatedValue1 = seats.find((seat) => seat.id === 'data-out')?.semantic;
  assert.equal(evaluatedValue1, 'data');
});
test('all nodes expose three execution branches and result rows never overlap', () => {
  const layout = layoutConnectionSeats({ kind: 'flipflop', hasDataOutput: true, parameters: [] });
  const mapResult1 = layout.map((seat) => seat.id);
  assert.deepEqual(mapResult1, ['exec-in', 'exec-success', 'exec-failure', 'exec-error', 'data-out']);
  const evaluatedValue2 = (layout.find((seat) => seat.id === 'exec-failure')?.top ?? 0) > (layout.find((seat) => seat.id === 'exec-success')?.top ?? 0);
  assert.ok(evaluatedValue2);
  const evaluatedValue3 = (layout.find((seat) => seat.id === 'exec-error')?.top ?? 0) > (layout.find((seat) => seat.id === 'exec-failure')?.top ?? 0);
  assert.ok(evaluatedValue3);
});
test('semantic resolution accepts only matching source and target seats', () => {
  const resolveConnectionSemanticResult = resolveConnectionSemantic('exec-success', 'exec-in');
  assert.equal(resolveConnectionSemanticResult, 'execution');
  const resolveConnectionSemanticResult1 = resolveConnectionSemantic('exec-failure', 'exec-in');
  assert.equal(resolveConnectionSemanticResult1, 'execution');
  const resolveConnectionSemanticResult2 = resolveConnectionSemantic('exec-error', 'exec-in');
  assert.equal(resolveConnectionSemanticResult2, 'execution');
  const resolveConnectionSemanticResult3 = resolveConnectionSemantic('data-out', 'param-payload');
  assert.equal(resolveConnectionSemanticResult3, 'data');
  const resolveConnectionSemanticResult4 = resolveConnectionSemantic('exec-success', 'param-payload');
  assert.equal(resolveConnectionSemanticResult4, undefined);
  const isInputSeatResult = isInputSeat('exec-in');
  assert.equal(isInputSeatResult, true);
  const isInputSeatResult1 = isInputSeat('param-payload');
  assert.equal(isInputSeatResult1, true);
  const isInputSeatResult2 = isInputSeat('data-out');
  assert.equal(isInputSeatResult2, false);
});
