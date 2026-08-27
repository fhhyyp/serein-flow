import assert from 'node:assert/strict'
import test from 'node:test'
import { formatNodeType } from '../src/flow/typeDisplay.ts'

test('formats framework types and generic task results for node cards', () => {
  assert.equal(formatNodeType('System.Threading.Tasks.Task`1<System.Boolean>'), 'Task<bool>')
  assert.equal(formatNodeType('System.Threading.Tasks.Task`1<System.Int32>'), 'Task<int>')
  assert.equal(formatNodeType('System.String'), 'string')
})

test('formats nullable, array, and nested generic node types', () => {
  assert.equal(formatNodeType('System.Nullable`1<System.Int32>'), 'int?')
  assert.equal(formatNodeType('System.Threading.Tasks.Task<System.Int32?>'), 'Task<int?>')
  assert.equal(formatNodeType('System.Nullable<System.Collections.Generic.List`1<System.String>>'), 'List<string>?')
  assert.equal(formatNodeType('System.Collections.Generic.Dictionary`2<System.String, System.Int32[]>'), 'Dictionary<string, int[]>')
})

test('preserves meaningful custom type names without namespaces or CLR arity', () => {
  assert.equal(formatNodeType('Acme.Quality.InspectionResult'), 'InspectionResult')
  assert.equal(formatNodeType('Acme.Container`1<Acme.Quality.InspectionResult>'), 'Container<InspectionResult>')
  assert.equal(formatNodeType(), '')
})
