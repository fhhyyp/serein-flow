# SereinFlow Library ZIP

The outer ZIP filename must be `[AssemblyName]-[Version].zip` and it must have
exactly one first-level directory:

```text
[AssemblyName]-[Version].zip
└── [AssemblyName]-[Version]/
    ├── [AssemblyName].dll
    ├── [AssemblyName].pdb
    ├── [AssemblyName].deps.json
    ├── [AssemblyName].runtimeconfig.json
    ├── dependency.dll
    └── runtimes/<rid>/native/<native-library>
```

Copy every file from the clean `dotnet publish` output and preserve its
relative path. Include managed dependencies, PDBs, config files, `.deps.json`,
`.runtimeconfig.json` and native runtime assets when present. Do not package
the whole `bin` or `obj` tree, source files, project/build files, scripts,
nested archives or EXEs. Dependency DLLs are valid and are not duplicate main
DLLs. Do not rename files to conceal a mismatch.

Before upload, verify the calculated ZIP name, one root directory, matching
main DLL, complete clean-publish file set, no path traversal and no forbidden
file types. Hard-fail on missing `TargetPath`, malformed version, a missing or
mismatched main DLL, missing publish output, `EnableDynamicLoading != true` or
an unsafe ZIP entry. The local check does not replace MCP package preview.
