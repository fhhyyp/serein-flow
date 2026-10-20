import assert from 'node:assert/strict';
import test from 'node:test';
import { checkFile, checkScript, checkStyle, checkTemplate, isSimpleTemplateExpression } from './check-frontend.ts';
const fixtureName = 'fixture.ts';
const vueFixtureName = 'fixture.vue';
const styleFixtureName = 'fixture.css';

test('reports malformed source as a parsing violation', () => {
  const scriptDiagnostics = checkFile('const value = )', fixtureName);
  const styleDiagnostics = checkFile('.item { color:', styleFixtureName);
  const templateDiagnostics = checkFile('<template><div></template>', vueFixtureName);
  const hasScriptError = hasRule(scriptDiagnostics, 'PARSE001');
  const hasStyleError = hasRule(styleDiagnostics, 'PARSE001');
  const hasTemplateError = hasRule(templateDiagnostics, 'PARSE001');
  assert.ok(hasScriptError);
  assert.ok(hasStyleError);
  assert.ok(hasTemplateError);
});

test('checks a closing tag followed by a sibling opening tag', () => {
  const source = '<div>\n<span>\nText\n</span><b>\nText\n</b>\n</div>';
  const diagnostics = checkTemplate(source, vueFixtureName);
  const hasTagDiagnostic = hasRule(diagnostics, 'VUE001');
  assert.ok(hasTagDiagnostic);
});

test('checks loop sources and dynamic directive arguments', () => {
  const loopDiagnostics = checkTemplate('<div v-for="item in values.filter(isVisible)"></div>', vueFixtureName);
  const dynamicDiagnostics = checkTemplate('<div :[prefix+suffix]="value"></div>', vueFixtureName);
  const hasLoopDiagnostic = hasRule(loopDiagnostics, 'VUE003');
  const hasDynamicDiagnostic = hasRule(dynamicDiagnostics, 'VUE003');
  assert.equal(hasLoopDiagnostic, false);
  assert.ok(hasDynamicDiagnostic);
  const complexLoopDiagnostics = checkTemplate('<div v-for="item in values.filter(isVisible).slice(0)"></div>', vueFixtureName);
  const hasComplexLoopDiagnostic = hasRule(complexLoopDiagnostics, 'VUE003');
  assert.ok(hasComplexLoopDiagnostic);
});
function hasRule(diagnostics: any[], rule: string): boolean {
  const matchingDiagnostic = diagnostics.some((diagnostic) => diagnostic.rule === rule);
  return matchingDiagnostic;
}
test('reports parameter nesting, constructors and receiver chains', () => {
  const parameterDiagnostics = checkScript('const value = outer(inner())', fixtureName);
  const constructorDiagnostics = checkScript('const value = new Container(createValue())', fixtureName);
  const receiverDiagnostics = checkScript('const value = source().next()', fixtureName);
  const hasParameterDiagnostic = hasRule(parameterDiagnostics, 'CALL001');
  const hasConstructorDiagnostic = hasRule(constructorDiagnostics, 'CALL001');
  const hasReceiverDiagnostic = hasRule(receiverDiagnostics, 'CALL002');
  assert.ok(hasParameterDiagnostic);
  assert.ok(hasConstructorDiagnostic);
  assert.ok(hasReceiverDiagnostic);
});
test('checks callback bodies without treating callback execution as parameter nesting', () => {
  const validSource = `register(() => {
    const value = read()
    return value
  })`;
  const validDiagnostics = checkScript(validSource, fixtureName);
  assert.deepEqual(validDiagnostics, []);
  const invalidDiagnostics = checkScript('register(() => read())', fixtureName);
  const hasReturnDiagnostic = hasRule(invalidDiagnostics, 'RETURN001');
  const hasParameterDiagnostic = hasRule(invalidDiagnostics, 'CALL001');
  assert.ok(hasReturnDiagnostic);
  assert.equal(hasParameterDiagnostic, false);
});
test('checks optional chains and awaited return values', () => {
  const optionalDiagnostics = checkScript('const value = source()?.next()', fixtureName);
  const awaitedDiagnostics = checkScript('async function run() { return await read() }', fixtureName);
  const hasOptionalDiagnostic = hasRule(optionalDiagnostics, 'CALL002');
  const hasAwaitDiagnostic = hasRule(awaitedDiagnostics, 'RETURN001');
  assert.ok(hasOptionalDiagnostic);
  assert.ok(hasAwaitDiagnostic);
});
test('checks callback destructuring and TypeScript module variants', () => {
  const bindingDiagnostics = checkScript('items.map(([first, second]) => first)', fixtureName);
  const hasBindingDiagnostic = hasRule(bindingDiagnostics, 'DECL002');
  assert.ok(hasBindingDiagnostic);
  const moduleDiagnostics = checkFile('const value = outer(inner())', 'fixture.mts');
  const hasModuleDiagnostic = hasRule(moduleDiagnostics, 'CALL001');
  assert.ok(hasModuleDiagnostic);
  const jsxDiagnostics = checkFile('const view = <div />', 'fixture.tsx');
  assert.deepEqual(jsxDiagnostics, []);
});
test('permits the Vue defaults compiler macro and still checks its callbacks', () => {
  const source = 'const props = withDefaults(defineProps<Props>(), { value: () => read() })';
  const diagnostics = checkScript(source, vueFixtureName);
  const hasParameterDiagnostic = hasRule(diagnostics, 'CALL001');
  const hasReturnDiagnostic = hasRule(diagnostics, 'RETURN001');
  assert.equal(hasParameterDiagnostic, false);
  assert.ok(hasReturnDiagnostic);
  const defaultsDiagnostics = checkScript('const props = withDefaults(defineProps<Props>(), readDefaults())', vueFixtureName);
  const hasDefaultsDiagnostic = hasRule(defaultsDiagnostics, 'CALL001');
  assert.ok(hasDefaultsDiagnostic);
  const ordinaryDiagnostics = checkScript('const props = withDefaults(defineProps<Props>(), {})', fixtureName);
  const hasOrdinaryDiagnostic = hasRule(ordinaryDiagnostics, 'CALL001');
  assert.ok(hasOrdinaryDiagnostic);
});
test('requires separate declarations, statements and braced branches', () => {
  const source = 'let first = 1, second = 2; if (enabled) run(); else stop()';
  const diagnostics = checkScript(source, fixtureName);
  const hasDeclarationDiagnostic = hasRule(diagnostics, 'DECL001');
  const hasStatementDiagnostic = hasRule(diagnostics, 'LINE001');
  const hasBranchDiagnostic = hasRule(diagnostics, 'FLOW001');
  assert.ok(hasDeclarationDiagnostic);
  assert.ok(hasStatementDiagnostic);
  assert.ok(hasBranchDiagnostic);
  const loopDiagnostics = checkScript('for (let index = 0; index < count; index += 1) {\n run()\n}', fixtureName);
  const hasLoopStatementDiagnostic = hasRule(loopDiagnostics, 'LINE001');
  assert.equal(hasLoopStatementDiagnostic, false);
  const nestedDiagnostics = checkScript('if (enabled) { run() }', fixtureName);
  const hasNestedStatementDiagnostic = hasRule(nestedDiagnostics, 'LINE001');
  assert.ok(hasNestedStatementDiagnostic);
  const sharedEndDiagnostics = checkScript('const first = read(\n value); const second = value', fixtureName);
  const hasSharedEndDiagnostic = hasRule(sharedEndDiagnostics, 'LINE001');
  assert.ok(hasSharedEndDiagnostic);
});
test('permits simple bindings, negation and simple method calls', () => {
  const sources = ['value', 'props.value', '!busy', 'read(item.id)', "t('label')"];
  for (const source of sources) {
    const isSimple = isSimpleTemplateExpression(source);
    assert.equal(isSimple, true, source);
  }
  const complexSources = ['count > 0', 'busy ? first : second', 'first + second', '{ active: enabled }', 'read(format(value))'];
  for (const source of complexSources) {
    const isSimple = isSimpleTemplateExpression(source);
    assert.equal(isSimple, false, source);
  }
});
test('checks nested tags and attribute placement', () => {
  const invalidSource = '<div><span class="label" :title="title">Text</span></div>';
  const invalidDiagnostics = checkTemplate(invalidSource, vueFixtureName);
  const hasTagDiagnostic = hasRule(invalidDiagnostics, 'VUE001');
  const hasAttributeDiagnostic = hasRule(invalidDiagnostics, 'VUE002');
  assert.ok(hasTagDiagnostic);
  assert.ok(hasAttributeDiagnostic);
  const validSource = `<div>
  <span
    class="label"
    :title="title"
  >Text</span>
</div>`;
  const validDiagnostics = checkTemplate(validSource, vueFixtureName);
  assert.deepEqual(validDiagnostics, []);
});
test('checks directive and interpolation logic', () => {
  const source = '<div v-if="first && second">{{ count + 1 }}</div>';
  const diagnostics = checkTemplate(source, vueFixtureName);
  const hasExpressionDiagnostic = hasRule(diagnostics, 'VUE003');
  assert.ok(hasExpressionDiagnostic);
});
test('counts CSS declarations and actual block depth', () => {
  const declarations = checkStyle('.item { color: red; display: block; }', styleFixtureName);
  const hasDeclarationDiagnostic = hasRule(declarations, 'CSS001');
  assert.ok(hasDeclarationDiagnostic);
  const validSource = '@media (width > 10px) {\n .item {\n  &:hover {\n   color: red;\n  }\n }\n}';
  const validDiagnostics = checkStyle(validSource, styleFixtureName);
  assert.deepEqual(validDiagnostics, []);
  const invalidSource = '@media (width > 10px) { .item { &:hover { .child { color: red; } } } }';
  const invalidDiagnostics = checkStyle(invalidSource, styleFixtureName);
  const hasDepthDiagnostic = hasRule(invalidDiagnostics, 'CSS002');
  assert.ok(hasDepthDiagnostic);
});
