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
$consumerRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('sereinflow-consumer-' + [guid]::NewGuid().ToString('N'))))
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $consumerRoot.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Consumer staging directory is outside the temporary directory: $consumerRoot"
}

try {
    dotnet new web --framework net10.0 --output $consumerRoot --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Creating the clean ASP.NET Core project failed.' }
    $project = Join-Path $consumerRoot ("$(Split-Path $consumerRoot -Leaf).csproj")
    dotnet add $project package SereinFlow.AspNetCore --version $PackageVersion --source $feed
    if ($LASTEXITCODE -ne 0) { throw 'Adding the SereinFlow package failed.' }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'package-smoke/Program.cs') -Destination (Join-Path $consumerRoot 'Program.cs') -Force
    $publishDir = Join-Path $consumerRoot 'publish'
    dotnet publish $project -c Release --output $publishDir --no-restore -v:q
    if ($LASTEXITCODE -ne 0) { throw 'Publishing the clean consumer failed.' }
    Push-Location $publishDir
    try {
        dotnet (Join-Path $publishDir ((Split-Path $consumerRoot -Leaf) + '.dll'))
        if ($LASTEXITCODE -ne 0) { throw 'Running the published consumer failed.' }
    }
    finally {
        Pop-Location
    }
}
finally {
    if (Test-Path -LiteralPath $consumerRoot) {
        Remove-Item -LiteralPath $consumerRoot -Recurse -Force
    }
}
