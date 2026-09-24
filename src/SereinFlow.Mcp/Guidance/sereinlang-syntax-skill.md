# SereinLang Syntax

SereinLang is UTF-8 and case-sensitive. Spaces, carriage returns and tabs are
ignored; newlines only increment source line numbers. Use `//` line comments or
`/* ... */` block comments. Reserved words are `let`, `var`, `if`, `then`,
`else`, `when`, `for`, `in`, `return`, `import`, `from`, `true`, `false` and
`null`. Identifiers normally use `[a-zA-Z_][a-zA-Z0-9_]*`; the compiler is
authoritative for non-ASCII identifiers.

The lexer accepts both `=>` and `->` as the lambda arrow. Use one spelling
consistently. It also accepts `?.` for null-safe member access, `:` for import
aliases and `=` for assignment and object properties.

## Values and bindings

```sereinlang
42  42L  3.14f  3.14  3.14d  10.5m  0xFF  0b1010
"text\nline"  true  false  null

let values = [1, 2, 3]
let person = { name = "Alice", age = 30, active = true }
var count = 0
count = count + 1
```

Unsuffixed integers infer as `int`, then `long`, then `double` when required.
Supported string escapes are `\r`, `\n`, `\t`, `\"` and `\\`; strings cannot
span source lines. Object keys may be identifiers or strings, and object properties
use `=`, never `:`. `{ name }` is shorthand for `{ name = name }`.

`let` bindings cannot be reassigned, but their object properties and array
elements can be changed. `var` bindings can be reassigned. Bindings are visible
in their containing block and lambdas capture outer bindings. Do not redeclare
a binding in the same scope. Assignment targets are identifiers, index access
or member access. Assignment is right-associative; precedence from low to high:

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

## Expressions

Lambdas can use expression or block bodies and can capture, curry, be passed,
returned or stored as values:

```sereinlang
let add = (left, right) => left + right
let greet = name => "Hello " + name
let factorial = (n) => {
    var result = 1
    for i in range(1, n + 1) { result = result * i }
    result
}
```

`if`, `when`, `for` and blocks are expressions:

```sereinlang
if score > 60 then "pass" else "fail"
when value { 1 => "one", 2 => "two", _ => "other" }
for item in [10, 20, 30] { print("item:", item) }
return result
```

`then` is optional, `else` is optional and returns `null` when omitted. `when`
clauses are comma-separated `pattern => body`; `_` is a catch-all. A `for`
expression normally returns the last iteration value. A block returns its last
expression. Use `return` for early exit; bare `return` returns `null`.

Use the server compiler to settle any syntax or portability uncertainty.
