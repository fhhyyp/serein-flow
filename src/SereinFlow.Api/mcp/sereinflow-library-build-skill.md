# SereinFlow Library Build

Build the library locally with the caller's Visual Studio or .NET toolchain.
The MCP service does not compile C# source, execute `.csproj`, `.props` or
`.targets` files, run MSBuild, run `dotnet build` or `dotnet publish`, or run a
build script. Send the service a completed ZIP, never an arbitrary local path.

Use an SDK-style class library targeting a supported runtime. Reference the
public package from NuGet.org:

```xml
<PackageReference Include="SereinFlow.Library" Version="*" />
```

Use a fixed version when reproducible builds or deployment compatibility
requires it. With Central Package Management, keep the project reference
versionless and set the version in `Directory.Packages.props`. Do not replace
the package with a DLL reference, copy SDK contracts into the project or
silently modify global NuGet configuration.

Let MSBuild evaluate `AssemblyName`, `Version`, `TargetPath`,
`TargetFramework` and `EnableDynamicLoading`; the latter must be `true`. When
multiple `.csproj` files exist, prefer the one declaring
`TestLibraryPackageName`, otherwise ask which project to use. Stop on restore,
build or publish errors.

Produce a clean publish directory under the project's `obj` directory. The
ZIP module defines the staging and validation contract. Report the absolute
publish directory, ZIP path, file count, size and SHA-256 before previewing it.
