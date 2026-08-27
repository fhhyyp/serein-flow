import assert from 'node:assert/strict'
import test from 'node:test'
import {
  isBooleanParameterType,
  isEnumOptionSelected,
  normalizeBooleanLiteralValue,
  parseEnumLiteralValue,
  updateFlagsLiteralValue,
} from '../src/flow/parameterTypes.ts'
import type { EnumParameterMetadata } from '../src/flow/types.ts'

const accessFlags: EnumParameterMetadata = {
  typeName: 'System.IO.FileAccess',
  isFlags: true,
  underlyingType: 'System.Int32',
  options: [
    { name: 'None', numericValue: '0' },
    { name: 'Read', numericValue: '1' },
    { name: 'Write', numericValue: '2' },
  ],
}

test('recognizes Boolean CLR aliases and nullable wrappers', () => {
  assert.equal(isBooleanParameterType('System.Boolean'), true)
  assert.equal(isBooleanParameterType('bool'), true)
  assert.equal(isBooleanParameterType('System.Nullable`1<System.Boolean>'), true)
  assert.equal(isBooleanParameterType('System.Nullable<System.Boolean>'), true)
  assert.equal(isBooleanParameterType('System.String'), false)
})

test('normalizes legacy Boolean literals to selector values', () => {
  assert.equal(normalizeBooleanLiteralValue('True'), 'true')
  assert.equal(normalizeBooleanLiteralValue('0'), 'false')
  assert.equal(normalizeBooleanLiteralValue('unknown'), '')
  assert.equal(normalizeBooleanLiteralValue(undefined), '')
})

test('parses enum literals into trimmed member names', () => {
  assert.deepEqual(parseEnumLiteralValue('  自动, 手动 ,维护  '), ['自动', '手动', '维护'])
  assert.equal(isEnumOptionSelected('Read, Write', 'Write'), true)
  assert.equal(isEnumOptionSelected('Read', 'Execute'), false)
})

test('serializes Flags members in catalog order', () => {
  let value = updateFlagsLiteralValue(accessFlags, '', 'Write', true)
  value = updateFlagsLiteralValue(accessFlags, value, 'Read', true)

  assert.equal(value, 'Read, Write')
})

test('selecting a nonzero Flags member clears None', () => {
  assert.equal(updateFlagsLiteralValue(accessFlags, 'None', 'Read', true), 'Read')
})

test('selecting None clears nonzero Flags members', () => {
  assert.equal(updateFlagsLiteralValue(accessFlags, 'Read, Write', 'None', true), 'None')
})

test('a Flags interaction removes duplicate and unknown legacy members', () => {
  assert.equal(updateFlagsLiteralValue(accessFlags, 'Read, Unknown, Read', 'Write', true), 'Read, Write')
})
