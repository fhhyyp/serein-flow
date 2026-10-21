param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$work = Join-Path $repo ('artifacts/generator-smoke/' + [Guid]::NewGuid().ToString('N'))
$packages = Join-Path $work 'packages'
New-Item -ItemType Directory -Path $packages -Force | Out-Null
$version = '0.0.0-smoke.' + [Guid]::NewGuid().ToString('N')

function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE" }
}

function Initialize-Consumer([string]$Folder) {
    New-Item -ItemType Directory -Path $Folder -Force | Out-Null
    # Shadow repository defaults; restore writes only to this verification's cache.
    '<Project />' | Set-Content -LiteralPath (Join-Path $Folder 'Directory.Build.props')
    '<Project />' | Set-Content -LiteralPath (Join-Path $Folder 'Directory.Build.targets')
    '<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup></Project>' |
        Set-Content -LiteralPath (Join-Path $Folder 'Directory.Packages.props')
    $feed = [Security.SecurityElement]::Escape($packages)
    $cache = [Security.SecurityElement]::Escape((Join-Path $work 'cache'))
    $fallback = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
    $fallback = [Security.SecurityElement]::Escape($fallback)
    "<configuration><config><add key=`"globalPackagesFolder`" value=`"$cache`"/></config><packageSources><clear/><add key=`"local`" value=`"$feed`"/></packageSources><fallbackPackageFolders><add key=`"cached`" value=`"$fallback`"/></fallbackPackageFolders></configuration>" |
        Set-Content -LiteralPath (Join-Path $Folder 'NuGet.Config')
}

$scriptGenerator = Join-Path $repo 'src/ThirdParty/SereinScript/ScriptLang.Generator/ScriptLang.Generator.csproj'
$mcpGenerator = Join-Path $repo 'src/SereinFlow.Mcp.Generator/SereinFlow.Mcp.Generator.csproj'
$scriptRuntime = Join-Path $repo 'src/ThirdParty/SereinScript/ScriptLang/ScriptLang.csproj'
$mcpRuntime = Join-Path $repo 'src/SereinFlow.Mcp.Framework/SereinFlow.Mcp.Framework.csproj'
foreach ($project in @($scriptRuntime, $mcpRuntime)) {
    Invoke-Dotnet @('build', $project, '-c', $Configuration, '-m:1', '-p:BuildInParallel=false', '-p:UseAppHost=false', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false', '--verbosity', 'quiet')
}
foreach ($project in @($scriptGenerator, $mcpGenerator)) {
    Invoke-Dotnet @('pack', $project, '-c', $Configuration, '-o', $packages, "-p:PackageVersion=$version", '-p:UseSharedCompilation=false', '-p:NuGetAudit=false', '--no-restore', '--verbosity', 'quiet')
}
foreach ($package in Get-ChildItem -LiteralPath $packages -Filter '*.nupkg') {
    $archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $paths = @($archive.Entries.FullName)
        if ('analyzers/dotnet/cs/Serein.Generator.Framework.dll' -notin $paths) { throw "Framework assembly missing from $($package.Name)" }
        if ($paths.Where({ $_ -like 'lib/*' }).Count -ne 0) { throw "Generator package exposes runtime assemblies: $($package.Name)" }
        $spec = $archive.Entries | Where-Object FullName -Like '*.nuspec'
        $reader = [IO.StreamReader]::new($spec.Open())
        try { $xml = [xml]$reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($xml.SelectNodes("//*[local-name()='dependency' and @id='Serein.Generator.Framework']").Count -ne 0) {
            throw 'Framework should be bundled as an analyzer dependency, not a consumer package dependency.'
        }
    } finally { $archive.Dispose() }
}

$program = @'
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ScriptLang;
using ScriptLang.Runtime;
using SereinFlow.Mcp.Framework;

namespace GeneratorSmoke;

public static class Program
{
    public static async Task Main()
    {
        IPrototype prototype = new SamplePrototype();
        prototype.Init();
        if (prototype.GetMethod(Value.Null, "value", new ScriptEngine())?.ToString() != "prototype-ok") throw new Exception("Prototype call failed");
        using var services = new ServiceCollection().AddMcpTools().BuildServiceProvider();
        var result = await services.GetRequiredService<McpToolExecutor>().ExecuteAsync(Hosting.CreateCatalog(), "echo", JsonSerializer.SerializeToElement(new { }));
        if (result as string != "mcp-ok") throw new Exception("MCP call failed");
        if (typeof(Program).Assembly.GetReferencedAssemblies().Any(assembly => assembly.Name == "Serein.Generator.Framework" || assembly.Name!.EndsWith(".Generator", StringComparison.Ordinal)))
            throw new Exception("Generation dependency leaked into runtime references");
        Console.WriteLine("Both generators loaded and runtime calls passed.");
    }
}
[McpHost] public static partial class Hosting { }
public static class Tools
{
    [McpTool("echo", Description = "Smoke-test direct invocation.")] public static Task<string> Echo() => Task.FromResult("mcp-ok");
}
[PrototypeExtension] public partial class SamplePrototype
{
    public partial bool IsTarget(Value value) => true;
    [PrototypeProperty(Name = "value")] private static Value Read() => StringValue.Create("prototype-ok");
}
'@

foreach ($mode in @('Project', 'Package')) {
    $consumer = Join-Path $work $mode
    Initialize-Consumer $consumer
    if ($mode -eq 'Project') {
        $references = @"
    <ProjectReference Include="$scriptRuntime" />
    <ProjectReference Include="$mcpRuntime" />
    <ProjectReference Include="$scriptGenerator" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
    <ProjectReference Include="$mcpGenerator" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
"@
    } else {
        $scriptDll = Join-Path ([IO.Path]::GetDirectoryName($scriptRuntime)) "bin/$Configuration/net10.0/ScriptLang.dll"
        $mcpDll = Join-Path ([IO.Path]::GetDirectoryName($mcpRuntime)) "bin/$Configuration/net10.0/SereinFlow.Mcp.Framework.dll"
        $references = @"
    <Reference Include="ScriptLang"><HintPath>$scriptDll</HintPath></Reference>
    <Reference Include="SereinFlow.Mcp.Framework"><HintPath>$mcpDll</HintPath></Reference>
    <PackageReference Include="ScriptLang.Generator" Version="$version" PrivateAssets="all" />
    <PackageReference Include="SereinFlow.Mcp.Generator" Version="$version" PrivateAssets="all" />
"@
    }
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>GeneratorSmoke</AssemblyName>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
$references
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $consumer 'GeneratorSmoke.csproj')
    $program | Set-Content -LiteralPath (Join-Path $consumer 'Program.cs')
    Invoke-Dotnet @('build', (Join-Path $consumer 'GeneratorSmoke.csproj'), '-c', $Configuration, '-m:1', '-p:BuildInParallel=false', '-p:UseAppHost=false', '-p:UseSharedCompilation=false', '--verbosity', 'quiet')
    $output = Join-Path $consumer "bin/$Configuration/net10.0"
    if (Test-Path -LiteralPath (Join-Path $output 'Serein.Generator.Framework.dll')) { throw "Framework leaked into $mode runtime output" }
    Invoke-Dotnet @((Join-Path $output 'GeneratorSmoke.dll'))
}

# Exercise the public framework package as a generator author's dependency too.
$framework = Join-Path $repo 'src/ThirdParty/Serein.Generator.Framework/Serein.Generator.Framework.csproj'
Invoke-Dotnet @('pack', $framework, '-c', $Configuration, '-o', $packages, "-p:PackageVersion=$version", '-p:UseSharedCompilation=false', '-p:NuGetAudit=false', '--no-restore', '--verbosity', 'quiet')
$author = Join-Path $work 'Author'
Initialize-Consumer $author
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <IsRoslynComponent>true</IsRoslynComponent>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
    <OutputItemType>Analyzer</OutputItemType>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>
    <NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Serein.Generator.Framework" Version="$version" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.3.0" PrivateAssets="all" />
    <None Include="`$(OutputPath)Author.dll" Pack="true" PackagePath="analyzers/dotnet/cs" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $author 'Author.csproj')
@'
using Microsoft.CodeAnalysis;
using Serein.Generator.Framework;
[Generator]
public sealed class ExampleGenerator : FrameworkGenerator
{
    protected override void Configure(GeneratorBuilder builder)
    {
        builder.AddInitializationSource("GenerateInfo.g.cs", "namespace Example { internal sealed class GenerateInfoAttribute : System.Attribute { } }");
        builder.ForClassAttribute("Example.GenerateInfoAttribute")
            .Analyze(static (input, context) => context.Success(new AssemblyModel(input.Compilation.AssemblyName ?? "Host")))
            .Emit(static (model, output) =>
            {
                var code = new CodeBuilder<AssemblyModel>(output, model);
                code.AppendCode("internal static class GeneratedInfo")
                    .AppendCodeBlock(() => code.AppendCode("public const string Assembly = " + CSharpLiteral.RenderValue(model.Name) + ";"));
                output.AddSource("GeneratedInfo.g.cs", code.ToCode());
            }, "ExampleOutput");
    }
    private sealed class AssemblyModel : System.IEquatable<AssemblyModel>
    {
        public AssemblyModel(string name) => Name = name;
        public string Name { get; }
        public bool Equals(AssemblyModel other) => other != null && Name == other.Name;
        public override bool Equals(object other) => Equals(other as AssemblyModel);
        public override int GetHashCode() => System.StringComparer.Ordinal.GetHashCode(Name);
    }
}
'@ | Set-Content -LiteralPath (Join-Path $author 'ExampleGenerator.cs')
Invoke-Dotnet @('pack', (Join-Path $author 'Author.csproj'), '-c', $Configuration, '-o', $packages, "-p:PackageVersion=$version", '-p:UseSharedCompilation=false', '--verbosity', 'quiet')
$example = Join-Path $work 'FrameworkPackage'
Initialize-Consumer $example
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><AssemblyName>FrameworkPackageSmoke</AssemblyName><NuGetAudit>false</NuGetAudit></PropertyGroup>
  <ItemGroup><PackageReference Include="Author" Version="$version" PrivateAssets="all" /></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $example 'FrameworkPackageSmoke.csproj')
'if (GeneratedInfo.Assembly != "FrameworkPackageSmoke") throw new System.Exception("Framework package output failed"); System.Console.WriteLine("Framework package author and consumer passed."); [Example.GenerateInfo] internal class SmokeTarget { }' |
    Set-Content -LiteralPath (Join-Path $example 'Program.cs')
Invoke-Dotnet @('build', (Join-Path $example 'FrameworkPackageSmoke.csproj'), '-c', $Configuration, '-p:UseSharedCompilation=false', '-p:UseAppHost=false', '--verbosity', 'quiet')
$exampleOutput = Join-Path $example "bin/$Configuration/net10.0"
if (Test-Path -LiteralPath (Join-Path $exampleOutput 'Serein.Generator.Framework.dll')) { throw 'Framework package leaked into the example runtime' }
Invoke-Dotnet @((Join-Path $exampleOutput 'FrameworkPackageSmoke.dll'))
Write-Output "Generator smoke checks passed. Artifacts: $work"
