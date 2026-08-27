import assert from 'node:assert/strict'
import test from 'node:test'
import { isBooleanParameterType, normalizeBooleanLiteralValue } from '../src/flow/parameterTypes.ts'

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
