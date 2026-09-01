# SereinLang Grammar

Use this compact grammar for syntax questions. For a concrete source and host,
the server compiler diagnostics remain authoritative.

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
StringLiteral    := "\"" (non-quote character | EscapeSequence)* "\""
EscapeSequence   := "\\r" | "\\n" | "\\t" | "\\\"" | "\\\\"
Identifier       := [a-zA-Z_][a-zA-Z0-9_]*
```

Comments are ignored by the lexer. Both `//` line comments and `/* ... */`
block comments are supported.
