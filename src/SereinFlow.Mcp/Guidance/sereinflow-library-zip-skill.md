# SereinFlow Library ZIP

The ZIP is created by the library project's `CreatePackage` MSBuild target
with `AfterTargets="Build"`. The supported commands are:

```powershell
dotnet build path\to\Library.csproj -c Release
dotnet build path\to\Library.csproj -c Release -r win-x64
```

`dotnet pack` and `dotnet publish` are not the packaging commands for this
contract. Do not expect a NuGet `.nupkg` or a publish directory to contain the
upload ZIP.

The outer ZIP filename must be `[AssemblyName]-[Version].zip`, placed in the
library project directory, and it must have exactly one first-level directory:

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

The target copies every file under `$(TargetDir)` while excluding
`$(TargetDir)publish\**\*` and stale nested RID output directories such as
`$(TargetDir)win-x64\**\*`, preserving each remaining file's relative path.
The archive therefore contains the normal build output, managed dependencies, PDBs,
configuration files, `.deps.json`, `.runtimeconfig.json`, and native runtime
assets when present. The temporary staging directory is under
`$(IntermediateOutputPath)package` and is removed after the ZIP is created.

Do not package the whole `bin` or `obj` tree, source files, project/build
files, scripts, the excluded `publish` subtree, nested archives or unrelated
outputs. Do not rename files to conceal an assembly mismatch.

Before upload, verify:

- the calculated ZIP name is `[AssemblyName]-[Version].zip`;
- the ZIP is in the project directory and has exactly one root directory;
- the root directory name is `[AssemblyName]-[Version]`;
- the root contains the matching main DLL and the complete build-output file
  set, with relative paths preserved;
- there is no path traversal, duplicate entry, nested archive or forbidden
  file type;
- `TargetPath` and `TargetDir` are present, the version is well formed, and
  `EnableDynamicLoading` is exactly `true`.

Hard-fail on a missing or mismatched main DLL, missing build output, an unsafe
ZIP entry, or a package produced from the wrong project. The local check does
not replace MCP package preview.
