# Embedded SereinScript Source

This directory contains the SereinScript source snapshot required by
SereinFlow's ScriptLang runtime and source generator integration.

Included projects:

- `ScriptLang/ScriptLang.csproj`
- `ScriptLang.Generator/ScriptLang.Generator.csproj`

The source was copied from the SereinScript checkout at commit
`56ec14a4124af715f2b8839703374bc44a5bdd68` on 2026-08-28. Build output,
Visual Studio state, and unrelated SereinScript applications are intentionally
excluded.

The SereinFlow ScriptAdapter and ScriptModules projects reference this
snapshot directly. `Directory.Build.props` also retains the source-root
property mapping for compatibility with auxiliary projects that opt into
property-based references.

When updating this snapshot, copy only the two projects above, preserve their
sibling directory relationship, record the new upstream commit here, and run
the full SereinFlow restore, build, and test commands.
