import assert from 'node:assert/strict'
import test from 'node:test'
import { convertVariadicParameterMode } from '../src/flow/variadicParameters.ts'
import type { MethodParameter } from '../src/flow/types.ts'

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
  }
}

test('collapsing expanded literal values creates one JSON collection without losing values', () => {
  const result = convertVariadicParameterMode([
    parameter('values', 'literal', '1'),
    parameter('values-2', 'literal', '2'),
  ], 'values', 'collection')

  assert.equal(result.ok, true)
  if (!result.ok) return
  assert.deepEqual(result.parameters.map((item) => item.id), ['values'])
  assert.equal(result.parameters[0]?.literalValue, '[1,2]')
  assert.equal(result.parameters[0]?.variadicMode, 'collection')
  assert.equal(result.targetParameterIdRemap.get('values-2'), 'values')
})

test('collapsing one dynamic source retains its parameter ID so its data connection remains valid', () => {
  const connected = {
    ...parameter('values-2', 'previousNode'),
    sourceNodeId: 'source-node',
    sourcePortId: 'data-out',
  }
  const result = convertVariadicParameterMode([
    parameter('values'),
    connected,
  ], 'values', 'collection')

  assert.equal(result.ok, true)
  if (!result.ok) return
  assert.deepEqual(result.parameters.map((item) => item.id), ['values-2'])
  assert.equal(result.parameters[0]?.source, 'previousNode')
  assert.equal(result.parameters[0]?.sourceNodeId, 'source-node')
  assert.equal(result.targetParameterIdRemap.get('values'), 'values-2')
})

test('collapsing refuses combinations that would discard configured values or links', () => {
  const mixedResult = convertVariadicParameterMode([
    parameter('values', 'literal', '1'),
    parameter('values-2', 'projectInput'),
  ], 'values', 'collection')
  const multipleSourcesResult = convertVariadicParameterMode([
    parameter('values', 'projectInput'),
    parameter('values-2', 'expression'),
  ], 'values', 'collection')

  assert.equal(mixedResult.ok, false)
  assert.equal(multipleSourcesResult.ok, false)
})

test('expanding a literal collection restores separately editable parameter entries', () => {
  const collection = {
    ...parameter('values', 'literal', '[1,2]'),
    variadicMode: 'collection' as const,
  }
  const result = convertVariadicParameterMode([collection], 'values', 'expanded')

  assert.equal(result.ok, true)
  if (!result.ok) return
  assert.deepEqual(result.parameters.map((item) => item.id), ['values', 'values-2'])
  assert.deepEqual(result.parameters.map((item) => item.literalValue), ['1', '2'])
  assert.deepEqual(result.parameters.map((item) => item.variadicMode), ['expanded', 'expanded'])
})

test('expanding a dynamic collection keeps the collection mode intact', () => {
  const collection = {
    ...parameter('values', 'previousNode'),
    sourceNodeId: 'source-node',
    sourcePortId: 'data-out',
    variadicMode: 'collection' as const,
  }
  const result = convertVariadicParameterMode([collection], 'values', 'expanded')

  assert.equal(result.ok, false)
})

test('an unspecified legacy mode is treated as expanded without rewriting its values', () => {
  const legacyParameter = parameter('values', 'literal', 'not-json')
  legacyParameter.variadicMode = undefined
  const result = convertVariadicParameterMode([legacyParameter], 'values', 'expanded')

  assert.equal(result.ok, true)
  if (!result.ok) return
  assert.equal(result.parameters[0]?.literalValue, 'not-json')
  assert.equal(result.parameters[0]?.variadicMode, undefined)
})
