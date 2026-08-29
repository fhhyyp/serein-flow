---
name: sereinflow-library-package
description: Build a completed SereinFlow C# class library with the user's local VS/.NET toolchain and create a validated upload ZIP named [AssemblyName]-[Version].zip. Use when an AI or user has a SereinFlow library project or DLL and needs a deterministic package for sereinflow_preview_library_package; automatically discover the project and artifact directory instead of requiring an absolute output path.
---

# SereinFlow Library Package

Create the upload artifact locally. The SereinFlow server only receives the
finished ZIP, scans its PE metadata, and imports it; it does not compile C# or
execute any project/build file.

## Required Contract

Use an SDK-style C# project targeting the runtime supported by the project
(`net10.0` in the current SereinFlow repository). The evaluated MSBuild
properties must provide:

- `AssemblyName`: the library name and DLL stem;
- `Version`: the package version, normally three-part SemVer such as `1.6.1`;
- `TargetPath`: the built DLL path;
- `EnableDynamicLoading`: must evaluate to `true` so the host can dynamically
  load the library.

The package contract is strict:

```text
[AssemblyName]-[Version].zip
└── [AssemblyName]-[Version]/
    └── [AssemblyName].dll
```

The DLL must be at most one directory below the ZIP root and its name must
match `AssemblyName`. Do not rename a DLL by hand to disguise a project or
version mismatch. Include only the built DLL unless the server contract has
explicitly been extended to accept another file.

For a project that generates its own test artifact, use the same calculated
values rather than duplicating literal paths:

```xml
<TestLibraryPackageName>$(AssemblyName)-$(Version)</TestLibraryPackageName>
<EnableDynamicLoading>true</EnableDynamicLoading>
<TestLibraryArtifactDirectory>$(MSBuildProjectDirectory)\..\..\artifacts\libraries\</TestLibraryArtifactDirectory>
<TestLibraryZipPath>$(TestLibraryArtifactDirectory)$(TestLibraryPackageName).zip</TestLibraryZipPath>
```

## Package Workflow

1. Discover the intended `.csproj` from the current workspace. If there is
   exactly one candidate, use it. If several exist, prefer the project that
   declares `TestLibraryPackageName`; otherwise ask for the project file. A
   project path may be supplied, but an output path is not required.
2. Find the repository root by walking upward from the project directory and
   looking for `SereinFlow.sln`, `.git`, or another repository marker.
3. Run `scripts/New-SereinFlowLibraryPackage.ps1` or reproduce its exact
   behavior. Let MSBuild calculate `AssemblyName`, `Version`, `TargetFramework`,
   `TargetPath`, and `EnableDynamicLoading`; do not parse inherited
   `Directory.Build.props` by hand.
4. Build with the local VS/.NET command-line toolchain. Use the selected
   configuration and stop on a build error. Never ask the SereinFlow service to
   run `dotnet build`, MSBuild, a `.csproj`, `.props`, `.targets`, or a build
   script.
5. Copy only `TargetPath` into a temporary staging directory under the project
   `obj` directory. Create the artifact directory automatically at
   `<repository-root>/artifacts/libraries`; do not require the caller to
   recreate or paste this path.
6. Create `[AssemblyName]-[Version].zip` with the package directory as the
   first-level entry. Replace only the same calculated output file.
7. Inspect the ZIP before reporting it: verify the expected single DLL entry,
   the DLL filename, the package filename, and that no path traversal or
   unexpected executable/script files exist. Report the absolute path selected
   by the Skill, the package name, size, and SHA-256.
8. Submit that ZIP through `sereinflow_preview_library_package`. Review the
   returned PE scan and contract diagnostics, then wait for explicit user
   confirmation before calling `sereinflow_apply_library_package`.

## Project Metadata

The class library should define `FlowLibraryAttribute` on its library class,
`FlowNodeAttribute` on node methods, and `NodeParamAttribute` on parameters.
Prefer the current attribute defaults so the scanner can derive stable IDs:
the node ID uses FlowLibrary name (or class name) plus method name, and the
parameter ID uses the CLR parameter name. Do not introduce IDs that change
between library builds unless a deliberate contract migration is intended.

Keep the project instance-node convention used by SereinFlow. If the project
uses static node methods only to silence analyzers, do not silently change the
runtime model. Check the generated DLL with the library preview, not by loading
it into the packaging script.

## Safety And Failure Handling

- Never accept or invent an arbitrary server-local path for remote MCP upload.
- Never package `bin`, `obj`, `.pdb`, source files, project files, build scripts,
  or dependencies unless the current server contract explicitly permits them.
- Treat an absent `TargetPath`, a missing DLL, a malformed version, a name
  mismatch, `EnableDynamicLoading` not evaluating to `true`, a multi-DLL
  package, and an unsafe ZIP entry as hard failures.
- Do not delete source or existing immutable artifacts. The script may remove
  only its own generated staging directory and replace the calculated artifact
  with the same package name.
- A successful local ZIP is not proof that the library is compatible. Use the
  MCP preview as the authoritative PE scan and compatibility check.

## Bundled Automation

Use [scripts/New-SereinFlowLibraryPackage.ps1](scripts/New-SereinFlowLibraryPackage.ps1)
for repeatable local packaging. It discovers the project when possible,
calculates the repository-relative artifact directory, builds with the local
toolchain, and validates the resulting ZIP. Read
[references/package-contract.md](references/package-contract.md) when adding or
reviewing project packaging targets.
