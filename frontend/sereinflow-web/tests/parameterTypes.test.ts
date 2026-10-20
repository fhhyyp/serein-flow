import assert from 'node:assert/strict';
import test from 'node:test';
import { isBooleanParameterType, isEnumOptionSelected, normalizeBooleanLiteralValue, parseEnumLiteralValue, updateFlagsLiteralValue, } from '../src/flow/parameterTypes.ts';
import type { EnumParameterMetadata } from '../src/flow/types.ts';
const accessFlags: EnumParameterMetadata = {
  typeName: 'System.IO.FileAccess',
  isFlags: true,
  underlyingType: 'System.Int32',
  options: [
    { name: 'None', numericValue: '0' },
    { name: 'Read', numericValue: '1' },
    { name: 'Write', numericValue: '2' },
  ],
};
test('recognizes Boolean CLR aliases and nullable wrappers', () => {
  const isBooleanParameterTypeResult = isBooleanParameterType('System.Boolean');
  assert.equal(isBooleanParameterTypeResult, true);
  const isBooleanParameterTypeResult1 = isBooleanParameterType('bool');
  assert.equal(isBooleanParameterTypeResult1, true);
  const isBooleanParameterTypeResult2 = isBooleanParameterType('System.Nullable`1<System.Boolean>');
  assert.equal(isBooleanParameterTypeResult2, true);
  const isBooleanParameterTypeResult3 = isBooleanParameterType('System.Nullable<System.Boolean>');
  assert.equal(isBooleanParameterTypeResult3, true);
  const isBooleanParameterTypeResult4 = isBooleanParameterType('System.String');
  assert.equal(isBooleanParameterTypeResult4, false);
});
test('normalizes legacy Boolean literals to selector values', () => {
  const normalizeBooleanLiteralValueResult = normalizeBooleanLiteralValue('True');
  assert.equal(normalizeBooleanLiteralValueResult, 'true');
  const normalizeBooleanLiteralValueResult1 = normalizeBooleanLiteralValue('0');
  assert.equal(normalizeBooleanLiteralValueResult1, 'false');
  const normalizeBooleanLiteralValueResult2 = normalizeBooleanLiteralValue('unknown');
  assert.equal(normalizeBooleanLiteralValueResult2, '');
  const normalizeBooleanLiteralValueResult3 = normalizeBooleanLiteralValue(undefined);
  assert.equal(normalizeBooleanLiteralValueResult3, '');
});
test('parses enum literals into trimmed member names', () => {
  const parseEnumLiteralValueResult = parseEnumLiteralValue('  自动, 手动 ,维护  ');
  assert.deepEqual(parseEnumLiteralValueResult, ['自动', '手动', '维护']);
  const isEnumOptionSelectedResult = isEnumOptionSelected('Read, Write', 'Write');
  assert.equal(isEnumOptionSelectedResult, true);
  const isEnumOptionSelectedResult1 = isEnumOptionSelected('Read', 'Execute');
  assert.equal(isEnumOptionSelectedResult1, false);
});
test('serializes Flags members in catalog order', () => {
  let value = updateFlagsLiteralValue(accessFlags, '', 'Write', true);
  value = updateFlagsLiteralValue(accessFlags, value, 'Read', true);
  assert.equal(value, 'Read, Write');
});
test('selecting a nonzero Flags member clears None', () => {
  const updateFlagsLiteralValueResult = updateFlagsLiteralValue(accessFlags, 'None', 'Read', true);
  assert.equal(updateFlagsLiteralValueResult, 'Read');
});
test('selecting None clears nonzero Flags members', () => {
  const updateFlagsLiteralValueResult1 = updateFlagsLiteralValue(accessFlags, 'Read, Write', 'None', true);
  assert.equal(updateFlagsLiteralValueResult1, 'None');
});
test('a Flags interaction removes duplicate and unknown legacy members', () => {
  const updateFlagsLiteralValueResult2 = updateFlagsLiteralValue(accessFlags, 'Read, Unknown, Read', 'Write', true);
  assert.equal(updateFlagsLiteralValueResult2, 'Read, Write');
});
