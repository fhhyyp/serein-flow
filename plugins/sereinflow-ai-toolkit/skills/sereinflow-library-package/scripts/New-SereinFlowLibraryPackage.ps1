[CmdletBinding()]
param(
    [string] $ProjectPath,
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'

function Resolve-LibraryProject {
    param([string] $RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        $resolved = Resolve-Path -LiteralPath $RequestedPath -ErrorAction Stop
        if ([IO.Path]::GetExtension($resolved.Path) -ne '.csproj') {
            throw "ProjectPath must point to a .csproj file: $($resolved.Path)"
        }
        return Get-Item -LiteralPath $resolved.Path
    }

    $start = (Get-Location).Path
    $projects = @(
        Get-ChildItem -LiteralPath $start -Filter '*.csproj' -File -Recurse |
            Where-Object { $_.FullName -notmatch '\\(bin|obj|node_modules)\\' }
    )

    if ($projects.Count -eq 1) {
        return $projects[0]
    }

    $declaredPackageProjects = @(
        $projects | Where-Object {
            Select-String -LiteralPath $_.FullName -Pattern '<TestLibraryPackageName>' -Quiet
        }
    )
    if ($declaredPackageProjects.Count -eq 1) {
        return $declaredPackageProjects[0]
    }

    if ($projects.Count -eq 0) {
        throw "No .csproj was found below the current workspace. Supply -ProjectPath."
    }

    $names = ($projects | ForEach-Object { $_.FullName }) -join [Environment]::NewLine
    throw "Multiple .csproj files were found. Supply -ProjectPath to choose one:$([Environment]::NewLine)$names"
}

function Find-RepositoryRoot {
    param([System.IO.DirectoryInfo] $ProjectDirectory)

    $current = $ProjectDirectory
    while ($null -ne $current) {
        if (
            (Test-Path -LiteralPath (Join-Path $current.FullName 'SereinFlow.sln')) -or
            (Test-Path -LiteralPath (Join-Path $current.FullName '.git'))
        ) {
            return $current.FullName
        }
        $current = $current.Parent
    }

    return $ProjectDirectory.FullName
}

function Get-EvaluatedProperties {
    param(
        [string] $Path,
        [string] $BuildConfiguration
    )

    $arguments = @(
        'msbuild',
        $Path,
        '-getProperty:AssemblyName,Version,TargetFramework,TargetPath,EnableDynamicLoading',
        "-property:Configuration=$BuildConfiguration",
        '-nologo'
    )
    $output = (& dotnet @arguments 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild property evaluation failed for '$Path':$([Environment]::NewLine)$output"
    }

    $jsonStart = $output.IndexOf('{')
    $jsonEnd = $output.LastIndexOf('}')
    if ($jsonStart -lt 0 -or $jsonEnd -le $jsonStart) {
        throw "MSBuild did not return evaluated properties for '$Path':$([Environment]::NewLine)$output"
    }

    $json = $output.Substring($jsonStart, $jsonEnd - $jsonStart + 1) | ConvertFrom-Json
    return $json.Properties
}

$project = Resolve-LibraryProject -RequestedPath $ProjectPath
$projectDirectory = $project.Directory
$repositoryRoot = Find-RepositoryRoot -ProjectDirectory $projectDirectory

if (-not $SkipBuild) {
    & dotnet build $project.FullName '--configuration' $Configuration '--nologo'
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed for '$($project.FullName)'."
    }
}

$properties = Get-EvaluatedProperties -Path $project.FullName -BuildConfiguration $Configuration
$assemblyName = [string] $properties.AssemblyName
$version = [string] $properties.Version
$targetPath = [string] $properties.TargetPath
$enableDynamicLoading = [string] $properties.EnableDynamicLoading

if ([string]::IsNullOrWhiteSpace($assemblyName) -or [string]::IsNullOrWhiteSpace($version)) {
    throw "AssemblyName and Version must be defined by the project or its evaluated build properties."
}
if (-not [string]::Equals($enableDynamicLoading, 'true', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "EnableDynamicLoading must evaluate to true before packaging. Add <EnableDynamicLoading>true</EnableDynamicLoading> to the project or its imported build props."
}
if ($assemblyName -match '[\\/:*?"<>|]' -or $version -match '[\\/:*?"<>|]') {
    throw "AssemblyName and Version contain path-invalid characters."
}
if ($version -notmatch '^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$') {
    throw "Version '$version' is not a supported package version. Use a three-part SemVer value."
}

$packageName = "$assemblyName-$version"
$expectedDllName = "$assemblyName.dll"
$targetFileName = [IO.Path]::GetFileName($targetPath)
if ($targetFileName -cne $expectedDllName) {
    throw "TargetPath '$targetPath' does not produce the expected DLL '$expectedDllName'."
}
if (-not (Test-Path -LiteralPath $targetPath -PathType Leaf)) {
    throw "The evaluated TargetPath does not exist: $targetPath"
}

$artifactDirectory = Join-Path $repositoryRoot 'artifacts\libraries'
$stagingRoot = Join-Path $projectDirectory.FullName 'obj\sereinflow-library-package'
$stagingDirectory = Join-Path $stagingRoot $packageName
$zipPath = Join-Path $artifactDirectory "$packageName.zip"

New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null
Copy-Item -LiteralPath $targetPath -Destination (Join-Path $stagingDirectory $expectedDllName)

Compress-Archive -LiteralPath $stagingDirectory -DestinationPath $zipPath -CompressionLevel Optimal -Force

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $fileEntries = @($archive.Entries | Where-Object { -not [string]::IsNullOrEmpty($_.Name) })
    $expectedEntry = "$packageName/$expectedDllName"
    $entryNames = @($fileEntries | ForEach-Object { $_.FullName.Replace('\', '/') })
    if ($fileEntries.Count -ne 1 -or $entryNames[0] -cne $expectedEntry) {
        throw "ZIP layout is invalid. Expected only '$expectedEntry', found: $($entryNames -join ', ')"
    }
    if ($entryNames[0] -match '(^/|^[A-Za-z]:|\.\.|[\\])') {
        throw "ZIP contains an unsafe entry name: $($entryNames[0])"
    }
}
finally {
    $archive.Dispose()
}

$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
$dllHash = (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash
[pscustomobject]@{
    ProjectPath = $project.FullName
    AssemblyName = $assemblyName
    Version = $version
    TargetFramework = [string] $properties.TargetFramework
    EnableDynamicLoading = $enableDynamicLoading
    DllPath = $targetPath
    DllSha256 = $dllHash
    ZipPath = $zipPath
    ZipSha256 = $zipHash
    ZipLength = (Get-Item -LiteralPath $zipPath).Length
}
