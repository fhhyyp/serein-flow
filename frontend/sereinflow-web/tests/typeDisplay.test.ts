import assert from 'node:assert/strict';
import test from 'node:test';
import { formatNodeType } from '../src/flow/typeDisplay.ts';
test('formats framework types and generic task results for node cards', () => {
  const formatNodeTypeResult = formatNodeType('System.Threading.Tasks.Task`1<System.Boolean>');
  assert.equal(formatNodeTypeResult, 'Task<bool>');
  const formatNodeTypeResult1 = formatNodeType('System.Threading.Tasks.Task`1<System.Int32>');
  assert.equal(formatNodeTypeResult1, 'Task<int>');
  const formatNodeTypeResult2 = formatNodeType('System.String');
  assert.equal(formatNodeTypeResult2, 'string');
});
test('formats nullable, array, and nested generic node types', () => {
  const formatNodeTypeResult3 = formatNodeType('System.Nullable`1<System.Int32>');
  assert.equal(formatNodeTypeResult3, 'int?');
  const formatNodeTypeResult4 = formatNodeType('System.Threading.Tasks.Task<System.Int32?>');
  assert.equal(formatNodeTypeResult4, 'Task<int?>');
  const formatNodeTypeResult5 = formatNodeType('System.Nullable<System.Collections.Generic.List`1<System.String>>');
  assert.equal(formatNodeTypeResult5, 'List<string>?');
  const formatNodeTypeResult6 = formatNodeType('System.Collections.Generic.Dictionary`2<System.String, System.Int32[]>');
  assert.equal(formatNodeTypeResult6, 'Dictionary<string, int[]>');
});
test('preserves meaningful custom type names without namespaces or CLR arity', () => {
  const formatNodeTypeResult7 = formatNodeType('Acme.Quality.InspectionResult');
  assert.equal(formatNodeTypeResult7, 'InspectionResult');
  const formatNodeTypeResult8 = formatNodeType('Acme.Container`1<Acme.Quality.InspectionResult>');
  assert.equal(formatNodeTypeResult8, 'Container<InspectionResult>');
  const formatNodeTypeResult9 = formatNodeType();
  assert.equal(formatNodeTypeResult9, '');
});
