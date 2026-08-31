# SereinLang MCP Skill

## Authority and workflow

This is the SereinLang syntax guidance served by SereinFlow MCP. It is
derived from `SereinScript-Language-Reference.md` version 1.0 and its lexer,
parser, AST, runtime examples, and formal grammar. For a concrete script,
the target node contract and compiler diagnostics returned by the server are
the final authority.

Before writing or repairing source:

1. Read the target node with `sereinflow_get_flow_edit_model`, including input
   names, types, required flags, defaults, output contract, source name, and
   language version.
2. Write source using the rules and grammar in this Resource.
3. Call `sereinflow_compile_sereinlang` with the complete source, declared
   inputs, and configured language version.
4. Correct every structured diagnostic at its reported source location.

Compilation is diagnostic-only. To persist accepted source, put
`replaceScriptSource` in `sereinflow_preview_flow_patch`, inspect the preview,
then apply it under the current flow task's authorization and reread the
affected flow. Do not ask for a duplicate confirmation when the user's request
already explicitly includes this source change.

## Lexical rules

- Use UTF-8 source. SereinLang is case-sensitive.
- Spaces, carriage returns, and tabs are ignored. Newlines increment line
  numbers but are not statement terminators.
- Use only `//` single-line comments. Block comments `/* ... */` are not
  supported.
- Reserved words are `let`, `var`, `if`, `then`, `else`, `when`, `for`, `in`,
  `return`, `import`, `from`, `true`, `false`, and `null`.
- The reference describes identifiers as starting with a letter, `_`, or `@`,
  followed by letters, digits, or `_`. Its lexer analysis allows Unicode
  letters through `char.IsLetter()`, while the formal EBNF summary is the
  ASCII form `[a-zA-Z_@][a-zA-Z0-9_]*`. For non-ASCII identifiers, rely on
  the server compiler rather than guessing portability.
- Use `@` for an identifier that conflicts with a C# keyword, for example
  `@class`.

The lexer recognizes both `=>` and `->` as the Lambda arrow token. They are
not two different Lambda syntaxes. Use one spelling consistently in a script.
The lexer also recognizes `?.` for null-safe member access, `:` for import
aliases, and `=` for assignment and object property definitions.

## Literals and data structures

Use these numeric and scalar literals:

```sereinlang
42              // inferred int, then long, then double when needed
42L             // long; l/L suffix
3.14f           // float; f/F suffix
3.14            // double by default for decimals
3.14d           // double; d/D suffix
10.5m           // decimal; m/M suffix
0xFF            // hexadecimal; 0x/0X prefix
0b1010          // binary; 0b/0B prefix
"text\nline"    // double-quoted string
true
false
null
```

Hexadecimal and binary literals also accept numeric suffixes. Unsuffixed
integers are inferred as `int`, then `long`, then `double` when required.

Strings use double quotes. Supported escapes include `\n`, `\t`, `\"`, and
`\\`. An unsupported escape is retained as the backslash and following
character. Strings cannot span source lines.

Use arrays and objects as follows:

```sereinlang
let values = [1, 2, 3]
let matrix = [
    [1, 2],
    [3, 4]
]
let person = {
    name = "Alice",
    age = 30,
    active = true
}
```

Object keys may be identifiers or strings. When a key and a variable have
the same name, `{ name }` is shorthand for `{ name = name }`. Object
properties use `=`, never JavaScript-style `:`.

SereinLang has no date literal syntax. The documented runtime functions
`now()`, `date("...")`, and `timespan(number, unit)` create date/time and
duration values. Date/time operations and members are available only when
the target SereinFlow script host exposes them.

## Bindings, scope, and assignment

```sereinlang
let limit = 10          // immutable binding
var count = 0           // reassignable binding
count = count + 1
items[0] = 100
profile.name = "Ada"
```

`let` bindings cannot be reassigned. `var` bindings can be reassigned.
Properties and array elements held by a `let` binding may still be changed.
An assignment target must be an identifier, an index access, or a member
access. Bindings are visible in their containing block. Lambdas capture outer
bindings. Do not redeclare a `let` or `var` name in the same scope.

Assignment is right-associative. The precedence order from low to high is:

```text
=
||
&&
== !=
< <= > >=
+ -
* / %
! unary-
call, index, member access
```

The first line above is the assignment operator `=`. Use parentheses when an
intentional precedence change should be unambiguous.

## Lambda expressions

Use either accepted arrow spelling:

```sereinlang
let add = (left, right) => left + right
let greet = name => "Hello " + name
let pi = () -> 3.14159
let factorial = (n) => {
    var result = 1
    for i in range(1, n + 1) {
        result = result * i
    }
    result
}
```

A Lambda body is either an expression or a block. A block evaluates to its
last expression. Lambdas are first-class values, capture outer bindings, can
be curried, can be passed as arguments, can be returned, and can be stored as
object properties.

The formal grammar spells the parenthesized zero-parameter and
multi-parameter forms with `=>`; the lexer maps `->` to the same Lambda arrow
token. Therefore `() -> body` is accepted by the lexer in the same way as
`() => body`.

## Control flow and blocks

`if`, `when`, `for`, and code blocks are expressions.

```sereinlang
if score > 60 then "pass" else "fail"
if value > 0 { value } else { 0 }

when value {
    1 => "one",
    2 => "two",
    _ => "other"
}

for item in [10, 20, 30] {
    print("item:", item)
}

return result
```

- `then` is optional after an `if` condition. The `else` branch is optional;
  when omitted, the expression returns `null`.
- Write each `when` clause as `pattern => body`, with clauses separated by
  commas. A body may be an expression or a block. `_` and a Lambda are
  documented catch-all patterns.
- Write a `for` expression as `for identifier in iterable body`. The `for`
  expression normally returns the value of the last iteration body.
- Use `return expression` for an early value, or bare `return` to return
  `null`. `return` is intended for early exit from a function body.
- A block `{ ... }` evaluates to the value of its last expression.

## Calls, member access, and indexing

```sereinlang
makeAdder(1)(2)(3)
matrix[0][1]
person.name
person?.address?.city
```

Calls, index access, `.`, and `?.` chain left-to-right at the highest
precedence. Use `?.` when a missing intermediate member should produce `null`
instead of a member-access failure.

## Imports and host interoperation

Use imports in this form:

```sereinlang
import { createStore, member : alias } from "relative/path.script"
```

The path is a string resolved relative to the current script. The imported
file's final value, returned explicitly or produced by its top-level
expression, is the module object from which members are destructured. Import
aliases use `member : alias` with a colon.

The language reference documents these runtime functions and members:

- Functions include `print`, `typeof`, `bool`, `double`, `range`, `keys`,
  `len`, `now`, `date`, and `timespan`.
- Array members include `.length`, `.count`, `.add`, and `.toList`.
- Object members include `.has` and `.keys`.
- String members include `.split`, `.join`, `.toUpper`, `.toString`, and
  `.last` where supported by the host runtime.

Use only functions and CLR factories that the target SereinFlow host exposes.
Direct CLR property and method access is possible only for objects supplied by
that host. Do not invent factories, modules, file-system, network,
environment-variable, or assembly capabilities.

## Formal grammar excerpt

Use this excerpt to resolve syntax questions. It mirrors the EBNF in the
language reference. For a particular source and host, compiler diagnostics
remain authoritative.

```ebnf
Program          := (Declaration | Expression)* EOF
Declaration      := LetDeclaration | VarDeclaration
                  | ImportDeclaration | ReturnStatement
LetDeclaration   := "let" Identifier "=" Expression
VarDeclaration   := "var" Identifier "=" Expression
ReturnStatement  := "return" Expression?
ImportDeclaration := "import" "{" ImportMembers "}" "from" StringLiteral
ImportMembers    := ImportMember ("," ImportMember)*
ImportMember     := Identifier (":" Identifier)?

Expression       := Assignment
Assignment       := Or ("=" Assignment)?
Or               := And ("||" And)*
And              := Equality ("&&" Equality)*
Equality         := Comparison (("==" | "!=") Comparison)*
Comparison       := Term (("<" | "<=" | ">" | ">=") Term)*
Term             := Factor (("+" | "-") Factor)*
Factor           := Unary (("*" | "/" | "%") Unary)*
Unary            := ("!" | "-") Unary | Call
Call             := Primary ("(" Args? ")" | "[" Expression "]"
                  | ("." | "?.") Identifier)*

Primary          := NumericLiteral | StringLiteral | "true" | "false" | "null"
                  | Identifier ("=>" LambdaBody)?
                  | "(" LambdaOrGroup ")"
                  | "{" ObjectLiteral "}" | "[" ArrayLiteral "]"
                  | "if" Expression ("then")? (Block | Expression)
                    ("else" (Block | Expression))?
                  | "when" Expression "{" WhenClauses "}"
                  | "for" Identifier "in" Expression (Block | Expression)
LambdaOrGroup    := ")" "=>" LambdaBody
                  | Identifier ("," Identifier)* ")" "=>" LambdaBody
                  | Expression ")"
LambdaBody       := Block | Expression
ArrayLiteral     := (Expression ("," Expression)*)?
ObjectLiteral    := (ObjectProperty ("," ObjectProperty)*)?
ObjectProperty   := (Identifier | StringLiteral) ("=" Expression)?
WhenClauses      := WhenClause ("," WhenClause)*
WhenClause       := Expression "=>" (Block | Expression)
Block            := "{" Statement* "}"
Statement        := Declaration | Expression
Args             := Expression ("," Expression)*
NumericLiteral   := [0-9]+ ("." [0-9]+)? Suffix?
                  | "0x" [0-9a-fA-F]+ Suffix?
                  | "0b" [01]+ Suffix?
Suffix           := [lLfFdDmM]
StringLiteral    := "\"" (non-quote character | escape sequence)* "\""
Identifier       := [a-zA-Z_@][a-zA-Z0-9_]*
```

## MCP safety boundary

A successful compilation is not execution, persistence, publication, or
permission to mutate a flow. Keep source content and diagnostics bounded; do
not expose secrets or full sensitive source in logs or audit summaries.
