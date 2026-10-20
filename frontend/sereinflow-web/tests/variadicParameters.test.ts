import assert from 'node:assert/strict';
import test from 'node:test';
import { convertVariadicParameterMode } from '../src/flow/variadicParameters.ts';
import type { MethodParameter } from '../src/flow/types.ts';
function parameter(id: string, source: MethodParameter['source'] = 'literal', literalValue = ''): MethodParameter {
  return {
    id,
    nameKey: 'values',
    name: 'values',
    valueKind: 'System.Int32',
    type: 'System.Int32',
    source,
    literalValue,
    isVariadic: true,
    variadicGroupId: 'values',
    elementType: 'System.Int32',
    variadicMode: 'expanded',
  };
}
test('collapsing expanded literal values creates one JSON collection without losing values', () => {
  const evaluatedValue: readonly MethodParameter[] = [
    parameter('values', 'literal', '1'),
    parameter('values-2', 'literal', '2'),
  ];
  const result = convertVariadicParameterMode(evaluatedValue, 'values', 'collection');
  assert.equal(result.ok, true);
  if (!result.ok) {
    return;
  }
  const mapResult = result.parameters.map((item) => item.id);
  assert.deepEqual(mapResult, ['values']);
  assert.equal(result.parameters[0]?.literalValue, '[1,2]');
  assert.equal(result.parameters[0]?.variadicMode, 'collection');
  const getResult = result.targetParameterIdRemap.get('values-2');
  assert.equal(getResult, 'values');
});
test('collapsing one dynamic source retains its parameter ID so its data connection remains valid', () => {
  const connected = {
    ...parameter('values-2', 'previousNode'),
    sourceNodeId: 'source-node',
    sourcePortId: 'data-out',
  };
  const evaluatedValue1: readonly MethodParameter[] = [
    parameter('values'),
    connected,
  ];
  const result = convertVariadicParameterMode(evaluatedValue1, 'values', 'collection');
  assert.equal(result.ok, true);
  if (!result.ok) {
    return;
  }
  const mapResult1 = result.parameters.map((item) => item.id);
  assert.deepEqual(mapResult1, ['values-2']);
  assert.equal(result.parameters[0]?.source, 'previousNode');
  assert.equal(result.parameters[0]?.sourceNodeId, 'source-node');
  const getResult1 = result.targetParameterIdRemap.get('values');
  assert.equal(getResult1, 'values-2');
});
test('collapsing refuses combinations that would discard configured values or links', () => {
  const evaluatedValue2: readonly MethodParameter[] = [
    parameter('values', 'literal', '1'),
    parameter('values-2', 'projectInput'),
  ];
  const mixedResult = convertVariadicParameterMode(evaluatedValue2, 'values', 'collection');
  const evaluatedValue3: readonly MethodParameter[] = [
    parameter('values', 'projectInput'),
    parameter('values-2', 'expression'),
  ];
  const multipleSourcesResult = convertVariadicParameterMode(evaluatedValue3, 'values', 'collection');
  assert.equal(mixedResult.ok, false);
  assert.equal(multipleSourcesResult.ok, false);
});
test('expanding a literal collection restores separately editable parameter entries', () => {
  const collection = {
    ...parameter('values', 'literal', '[1,2]'),
    variadicMode: 'collection' as const,
  };
  const result = convertVariadicParameterMode([collection], 'values', 'expanded');
  assert.equal(result.ok, true);
  if (!result.ok) {
    return;
  }
  const mapResult2 = result.parameters.map((item) => item.id);
  assert.deepEqual(mapResult2, ['values', 'values-2']);
  const mapResult3 = result.parameters.map((item) => item.literalValue);
  assert.deepEqual(mapResult3, ['1', '2']);
  const mapResult4 = result.parameters.map((item) => item.variadicMode);
  assert.deepEqual(mapResult4, ['expanded', 'expanded']);
});
test('expanding a dynamic collection keeps the collection mode intact', () => {
  const collection = {
    ...parameter('values', 'previousNode'),
    sourceNodeId: 'source-node',
    sourcePortId: 'data-out',
    variadicMode: 'collection' as const,
  };
  const result = convertVariadicParameterMode([collection], 'values', 'expanded');
  assert.equal(result.ok, false);
});
test('an unspecified legacy mode is treated as expanded without rewriting its values', () => {
  const legacyParameter = parameter('values', 'literal', 'not-json');
  legacyParameter.variadicMode = undefined;
  const result = convertVariadicParameterMode([legacyParameter], 'values', 'expanded');
  assert.equal(result.ok, true);
  if (!result.ok) {
    return;
  }
  assert.equal(result.parameters[0]?.literalValue, 'not-json');
  assert.equal(result.parameters[0]?.variadicMode, undefined);
});
