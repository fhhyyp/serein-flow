param(
    [string] $FeedDirectory = (Join-Path $PSScriptRoot '../artifacts/aspnetcore-package'),
    [string] $PackageVersion = '0.1.0'
)

$ErrorActionPreference = 'Stop'
$feed = [IO.Path]::GetFullPath($FeedDirectory)
$package = Join-Path $feed "SereinFlow.AspNetCore.$PackageVersion.nupkg"
if (-not (Test-Path -LiteralPath $package)) {
    throw "SereinFlow.AspNetCore package is missing: $package"
}
Add-Type -AssemblyName System.IO.Compression
$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $files = @($archive.Entries | ForEach-Object FullName)
    if ($files -contains 'lib/net10.0/SereinFlow.Security.ApiKeys.dll') { throw 'The base package must not contain the optional key implementation.' }
    foreach ($required in @(
        'lib/net10.0/SereinFlow.Application.dll',
        'lib/net10.0/ScriptLang.dll',
        'worker/SereinFlow.Worker.Runner.dll',
        'worker/SereinFlow.Worker.Runner.deps.json',
        'worker/SereinFlow.Worker.Runner.runtimeconfig.json'
    )) {
        if ($required -notin $files) { throw "Package asset is missing: $required" }
    }
    $nuspecEntry = $archive.Entries | Where-Object FullName -Like '*.nuspec' | Select-Object -First 1
    $reader = [IO.StreamReader]::new($nuspecEntry.Open())
    try { [xml] $nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
    foreach ($dependency in $nuspec.SelectNodes('//*[local-name()="dependency"]')) {
        if ($dependency.id -like 'SereinFlow.*' -or $dependency.id -eq 'Serein.ScriptLang') {
            throw "Package still depends on a separate internal package: $($dependency.id)"
        }
    }
}
finally {
    $archive.Dispose()
}
Write-Host "Package contents and dependency declarations are valid: $package"

$securityPackage = Join-Path $feed "SereinFlow.Security.ApiKeys.$PackageVersion.nupkg"
$securityArchive = [IO.Compression.ZipFile]::OpenRead($securityPackage)
try {
    $assemblies = @($securityArchive.Entries | Where-Object FullName -Like 'lib/*/*.dll' | ForEach-Object FullName)
    if ($assemblies.Count -ne 1 -or $assemblies[0] -ne 'lib/net10.0/SereinFlow.Security.ApiKeys.dll') {
        throw 'The optional package must contain only its own runtime assembly.'
    }
    $spec = $securityArchive.Entries | Where-Object FullName -Like '*.nuspec' | Select-Object -First 1
    $reader = [IO.StreamReader]::new($spec.Open())
    try { [xml] $securitySpec = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $baseDependency = @($securitySpec.SelectNodes('//*[local-name()="dependency"]') | Where-Object id -EQ 'SereinFlow.AspNetCore')
    if ($baseDependency.Count -ne 1) { throw 'The optional package must depend on the base hosting package.' }
}
finally { $securityArchive.Dispose() }
Write-Host "Optional security package is valid: $securityPackage"
