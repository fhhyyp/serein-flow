# SereinLang Host Interoperation

Calls, indexing, `.`, and `?.` chain left to right at the highest precedence:

```sereinlang
makeAdder(1)(2)(3)
matrix[0][1]
person?.address?.city
```

Use `?.` when a missing intermediate member should produce `null` instead of a
member-access failure.

Imports use this form:

```sereinlang
import { createStore, member : alias } from "relative/path.script"
```

The path is relative to the current script. The imported file's final value is
the module object from which members are destructured. Import aliases use `:`.

The documented runtime surface includes `print`, `typeof`, `bool`, `double`,
`range`, `keys`, `len`, `now`, `date` and `timespan`. Array members may include
`.length`, `.count`, `.add` and `.toList`; object members may include `.has` and
`.keys`; string members may include `.split`, `.join`, `.toUpper`, `.toString`
and `.last` where the host supports them.

Use only functions, CLR factories and members exposed by the target SereinFlow
host. Do not invent file-system, network, environment-variable, assembly or
other capabilities. Compilation is diagnostic-only; persistence still requires
a flow preview, apply and readback.
