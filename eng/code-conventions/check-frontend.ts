import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
const frontendManifest = new URL('../../frontend/sereinflow-web/package.json', import.meta.url);
const requireFrontend = createRequire(frontendManifest);
const ts = requireFrontend('typescript');
const vue = requireFrontend('@vue/compiler-sfc');
const compiler = requireFrontend('@vue/compiler-dom');
const postcss = requireFrontend('postcss');
const repositoryUrl = new URL('../../', import.meta.url);
const repositoryPath = fileURLToPath(repositoryUrl);
const repositoryRoot = path.resolve(repositoryPath);
const sourceExtensions = new Set(['.vue', '.ts', '.mts', '.cts', '.tsx', '.css']);
const emptyOffset = 0;
const firstLine = 1;
const attributeLimit = 1;
const maximumStyleDepth = 3;
const failureExitCode = 1;
const successExitCode = 0;
export function checkScript(source: string, file: string, offset = emptyOffset): any[] {
  const scriptKind = file.endsWith('.tsx') ? ts.ScriptKind.TSX : ts.ScriptKind.TS;
  const tree = ts.createSourceFile(file, source, ts.ScriptTarget.Latest, true, scriptKind);
  const diagnostics: any[] = [];
  const statementLines = new Set<number>();
  for (const parseDiagnostic of tree.parseDiagnostics) {
    const message = ts.flattenDiagnosticMessageText(parseDiagnostic.messageText, ' ');
    const syntaxDiagnostic = {
      rule: 'PARSE001',
      file,
      offset: offset + (parseDiagnostic.start ?? emptyOffset),
      message,
    };
    diagnostics.push(syntaxDiagnostic);
  }
  if (diagnostics.length) {
    return diagnostics;
  }
  function report(rule: string, node: any, message: string): void {
    const diagnostic: {
      rule: string;
      file: string;
      offset: any;
      message: string;
    } = { rule, file, offset: offset + node.getStart(tree), message };
    diagnostics.push(diagnostic);
  }
  function callsWithin(node: any): boolean {
    if (ts.isFunctionLike(node)) {
      return false;
    }
    if (ts.isCallExpression(node) || ts.isNewExpression(node)) {
      return true;
    }
    const forEachChildResult = ts.forEachChild(node, callsWithin);
    const booleanResult: boolean = Boolean(forEachChildResult);
    return booleanResult;
  }
  function visit(node: any): void {
    if (ts.isStatement(node) && !ts.isBlock(node) && !ts.isEmptyStatement(node)) {
      const statementStart = node.getStart(tree);
      const statementLine = tree.getLineAndCharacterOfPosition(statementStart).line;
      if (statementLines.has(statementLine)) {
        report('LINE001', node, 'Place each statement on a separate line.');
      }
      statementLines.add(statementLine);
    }
    if (ts.isCallExpression(node) || ts.isNewExpression(node)) {
      const macro = file.endsWith('.vue') && ts.isIdentifier(node.expression) && node.expression.text === 'withDefaults';
      const hasNestedArgument = node.arguments?.some((argument: any, index: number) => {
        const isPropsMacro = macro && index === emptyOffset && ts.isCallExpression(argument)
          && ts.isIdentifier(argument.expression) && argument.expression.text === 'defineProps';
        if (isPropsMacro) {
          return false;
        }
        const containsNestedCall = callsWithin(argument);
        return containsNestedCall;
      });
      if (hasNestedArgument) {
        report('CALL001', node, 'Store argument call results before invoking another function.');
      }
      if (callsWithin(node.expression)) {
        report('CALL002', node, 'Store the receiver before chaining calls.');
      }
    }
    if (ts.isReturnStatement(node) && node.expression && callsWithin(node.expression)) {
      report('RETURN001', node, 'Store the returned result in a named variable.');
    }
    if (ts.isArrowFunction(node) && !ts.isBlock(node.body) && callsWithin(node.body)) {
      report('RETURN001', node.body, 'Use a block and a named result for an implicit call return.');
    }
    if (ts.isVariableDeclarationList(node) && node.declarations.length > attributeLimit) {
      report('DECL001', node, 'Declare one variable per declaration.');
    }
    if ((ts.isVariableDeclaration(node) || ts.isParameter(node)) && (ts.isObjectBindingPattern(node.name) || ts.isArrayBindingPattern(node.name))) {
      const bindings = node.name.elements.filter((element: any) => {
        const isBinding = !ts.isOmittedExpression(element);
        return isBinding;
      });
      if (bindings.length > attributeLimit) {
        report('DECL002', node, 'Declare each destructured binding separately.');
      }
    }
    if (ts.isIfStatement(node)) {
      if (!ts.isBlock(node.thenStatement)) {
        report('FLOW001', node, 'Use braces for every conditional branch.');
      }
      if (node.elseStatement && !ts.isBlock(node.elseStatement) && !ts.isIfStatement(node.elseStatement)) {
        report('FLOW001', node.elseStatement, 'Use braces for every else branch.');
      }
      if (node.elseStatement) {
        const thenLine = tree.getLineAndCharacterOfPosition(node.thenStatement.end - firstLine).line;
        const getStartResult = node.elseStatement.getStart(tree);
        const elseLine = tree.getLineAndCharacterOfPosition(getStartResult).line;
        if (thenLine === elseLine) {
          report('FLOW002', node.elseStatement, 'Place else on a separate line.');
        }
      }
    }
    if (ts.isBlock(node) || ts.isSourceFile(node) || ts.isCaseClause(node) || ts.isDefaultClause(node)) {
      let previousStartLine = -firstLine;
      let previousEndLine = -firstLine;
      for (const statement of node.statements) {
        const statementStart = statement.getStart(tree);
        const startLine = tree.getLineAndCharacterOfPosition(statementStart).line;
        if (startLine === previousEndLine && startLine !== previousStartLine) {
          report('LINE001', statement, 'Place each statement on a separate line.');
        }
        previousStartLine = startLine;
        previousEndLine = tree.getLineAndCharacterOfPosition(statement.end - firstLine).line;
      }
    }
    ts.forEachChild(node, visit);
  }
  visit(tree);
  return diagnostics;
}
export function isSimpleTemplateExpression(source: string): boolean {
  const wrappedSource = `const templateValue = (${source})`;
  const tree = ts.createSourceFile('template.ts', wrappedSource, ts.ScriptTarget.Latest, true);
  if (tree.parseDiagnostics.length) {
    return false;
  }
  const declaration = tree.statements[emptyOffset]?.declarationList?.declarations[emptyOffset];
  const expression = declaration?.initializer?.expression;
  function simple(node: any, allowCall: boolean): boolean {
    if (!node) {
      return false;
    }
    if (ts.isIdentifier(node) || ts.isLiteralExpression(node) || node.kind === ts.SyntaxKind.NullKeyword || node.kind === ts.SyntaxKind.TrueKeyword || node.kind === ts.SyntaxKind.FalseKeyword) {
      return true;
    }
    if (ts.isPropertyAccessExpression(node)) {
      const simpleResult1: boolean = simple(node.expression, false);
      return simpleResult1;
    }
    if (ts.isElementAccessExpression(node)) {
      const isSimpleElementAccess: boolean = simple(node.expression, false) && simple(node.argumentExpression, false);
      return isSimpleElementAccess;
    }
    if (ts.isParenthesizedExpression(node) || ts.isAsExpression(node) || ts.isNonNullExpression(node)) {
      const simpleResult2: boolean = simple(node.expression, allowCall);
      return simpleResult2;
    }
    if (ts.isPrefixUnaryExpression(node) && node.operator === ts.SyntaxKind.ExclamationToken) {
      const simpleResult3: boolean = simple(node.operand, false);
      return simpleResult3;
    }
    if (allowCall && ts.isCallExpression(node)) {
      const isSimpleCall = simple(node.expression, false) && node.arguments.every((argument: any) => {
        const simpleResult = simple(argument, false);
        return simpleResult;
      });
      return isSimpleCall;
    }
    return false;
  }
  const simpleResult4: boolean = simple(expression, true);
  return simpleResult4;
}
export function checkTemplate(source: string, file: string, offset = emptyOffset): any[] {
  const diagnostics: any[] = [];
  const tree = compiler.baseParse(source);
  const occupiedLines = new Map<number, any>();
  function report(rule: string, node: any, message: string): void {
    diagnostics.push({ rule, file, offset: offset + node.loc.start.offset, message });
  }
  function occupyLine(line: number, node: any): void {
    const previous = occupiedLines.get(line);
    if (previous && previous !== node) {
      report('VUE001', node, 'Use at most one tag pair or standalone tag per line.');
    }
    occupiedLines.set(line, node);
  }
  function visit(node: any): void {
    if (node.type === compiler.NodeTypes.ELEMENT) {
      const startLine = node.loc.start.line;
      occupyLine(startLine, node);
      occupyLine(node.loc.end.line, node);
      if (node.props.length > attributeLimit) {
        const attributeLines = new Set<number>();
        for (const attribute of node.props) {
          const line = attribute.loc.start.line;
          if (line === startLine || attributeLines.has(line)) {
            report('VUE002', attribute, 'Put each attribute on a separate line when there is more than one.');
          }
          attributeLines.add(line);
        }
      }
      for (const attribute of node.props) {
        if (attribute.type !== compiler.NodeTypes.DIRECTIVE || !attribute.exp || attribute.name === 'slot') {
          continue;
        }
        const expression = attribute.name === 'for' ? attribute.forParseResult?.source : attribute.exp;
        if (expression && !isSimpleTemplateExpression(expression.content)) {
          report('VUE003', expression, 'Move template logic into a computed value or method.');
        }
        if (attribute.arg && !attribute.arg.isStatic && !isSimpleTemplateExpression(attribute.arg.content)) {
          report('VUE003', attribute.arg, 'Move dynamic directive argument logic into a computed value or method.');
        }
      }
      const children = node.children.filter((child: any) => child.type === compiler.NodeTypes.ELEMENT);
      for (const child of children) {
        if (child.loc.start.line === node.loc.start.line || child.loc.end.line === node.loc.end.line) {
          report('VUE001', child, 'Put nested tags on separate lines.');
        }
      }
    }
    if (node.type === compiler.NodeTypes.INTERPOLATION && !isSimpleTemplateExpression(node.content.content)) {
      report('VUE003', node.content, 'Move interpolation logic into a computed value or method.');
    }
    if (node.children) {
      for (const child of node.children)
        visit(child);
    }
  }
  visit(tree);
  return diagnostics;
}
export function checkStyle(source: string, file: string, offset = emptyOffset): any[] {
  const tree = postcss.parse(source, { from: file });
  const diagnostics: any[] = [];
  const declarationLines = new Set<number>();
  tree.walk((node: any) => {
    if (node.type === 'decl') {
      const line = node.source.start.line;
      if (declarationLines.has(line)) {
        diagnostics.push({ rule: 'CSS001', file, offset: offset + node.source.start.offset, message: 'Put each CSS property on a separate line.' });
      }
      declarationLines.add(line);
    }
    if (node.type === 'rule' || node.type === 'atrule' && node.nodes) {
      let depth = firstLine;
      let ancestor = node.parent;
      while (ancestor && ancestor.type !== 'root') {
        depth += firstLine;
        ancestor = ancestor.parent;
      }
      if (depth > maximumStyleDepth) {
        diagnostics.push({ rule: 'CSS002', file, offset: offset + node.source.start.offset, message: 'CSS block nesting must not exceed three levels.' });
      }
    }
  });
  return diagnostics;
}
export function checkFile(source: string, file: string): any[] {
  try {
    const diagnostics = checkFileContents(source, file);
    return diagnostics;
  }
  catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    const diagnostic = { rule: 'PARSE001', file, offset: emptyOffset, message };
    return [diagnostic];
  }
}

function checkFileContents(source: string, file: string): any[] {
  if (file.endsWith('.css')) {
    const checkStyleResult: any[] = checkStyle(source, file);
    return checkStyleResult;
  }
  if (!file.endsWith('.vue')) {
    const checkScriptResult1: any[] = checkScript(source, file);
    return checkScriptResult1;
  }
  const parsed = vue.parse(source, { filename: file });
  if (parsed.errors.length) {
    const stringResult: string = String(parsed.errors);
    throw new Error(stringResult);
  }
  const descriptor = parsed.descriptor;
  const diagnostics: any[] = [];
  for (const script of [descriptor.script, descriptor.scriptSetup]) {
    if (script) {
      const checkScriptResult = checkScript(script.content, file, script.loc.start.offset);
      diagnostics.push(...checkScriptResult);
    }
  }
  if (descriptor.template) {
    const checkTemplateResult = checkTemplate(descriptor.template.content, file, descriptor.template.loc.start.offset);
    diagnostics.push(...checkTemplateResult);
  }
  for (const style of descriptor.styles) {
    const styleDiagnostics = checkStyle(style.content, file, style.loc.start.offset);
    diagnostics.push(...styleDiagnostics);
  }
  return diagnostics;
}
function main(): void {
  const trackedOutput = execFileSync('git', ['ls-files', '--cached', '--others', '--exclude-standard', '-z'], { cwd: repositoryRoot, encoding: 'utf8' });
  const trackedFiles = trackedOutput.split('\0');
  const files = trackedFiles.filter((file: string) => {
    const extension = path.extname(file);
    const hasSupportedExtension = sourceExtensions.has(extension);
    const isOwnedSource = hasSupportedExtension && !file.startsWith('src/ThirdParty/') && !file.endsWith('.d.ts');
    return isOwnedSource;
  });
  const uniqueFiles = new Set<string>(files);
  let violationCount = emptyOffset;
  for (const file of uniqueFiles) {
    const absolutePath = path.join(repositoryRoot, file);
    const source = fs.readFileSync(absolutePath, 'utf8');
    const diagnostics = checkFile(source, file);
    for (const diagnostic of diagnostics) {
      const prefix = source.slice(emptyOffset, diagnostic.offset);
      const lines = prefix.split('\n');
      const line = lines.length;
      const column = lines.at(-firstLine)!.length + firstLine;
      console.error(`${file}(${line},${column}): ${diagnostic.rule} ${diagnostic.message}`);
      violationCount += firstLine;
    }
  }
  console.log(`Frontend conventions: ${uniqueFiles.size} files, ${violationCount} violations.`);
  process.exitCode = violationCount ? failureExitCode : successExitCode;
}
const entryUrl = process.argv[firstLine];
let isEntry = false;
if (entryUrl) {
  const entryPath = path.resolve(entryUrl);
  const moduleFile = fileURLToPath(import.meta.url);
  const modulePath = path.resolve(moduleFile);
  isEntry = entryPath === modulePath;
}
if (isEntry) {
  main();
}
