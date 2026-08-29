[CmdletBinding()]
param(
    [string] $ProjectPath,
    [string] $RepositoryRoot,
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [string] $RuntimeIdentifier,
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
    param(
        [System.IO.DirectoryInfo] $ProjectDirectory,
        [string] $RequestedRoot
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedRoot)) {
        $resolved = Resolve-Path -LiteralPath $RequestedRoot -ErrorAction Stop
        $item = Get-Item -LiteralPath $resolved.Path
        if (-not $item.PSIsContainer) {
            throw "RepositoryRoot must point to an existing directory: $($item.FullName)"
        }
        return $item.FullName
    }

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
    '-getProperty:AssemblyName,Version,TargetFramework,TargetPath,EnableDynamicLoading,RuntimeIdentifier',
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
$repositoryRoot = Find-RepositoryRoot -ProjectDirectory $projectDirectory -RequestedRoot $RepositoryRoot

$properties = Get-EvaluatedProperties -Path $project.FullName -BuildConfiguration $Configuration
$assemblyName = [string] $properties.AssemblyName
$version = [string] $properties.Version
$targetPath = [string] $properties.TargetPath
$enableDynamicLoading = [string] $properties.EnableDynamicLoading
$evaluatedRuntimeIdentifier = [string] $properties.RuntimeIdentifier
$runtimeIdentifier = if ([string]::IsNullOrWhiteSpace($RuntimeIdentifier)) { $evaluatedRuntimeIdentifier } else { $RuntimeIdentifier.Trim() }

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
if (-not [string]::IsNullOrWhiteSpace($runtimeIdentifier) -and $runtimeIdentifier -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]*$') {
    throw "RuntimeIdentifier '$runtimeIdentifier' contains invalid characters."
}

$packageName = "$assemblyName-$version"
$expectedDllName = "$assemblyName.dll"
$targetFileName = [IO.Path]::GetFileName($targetPath)
if ($targetFileName -cne $expectedDllName) {
    throw "TargetPath '$targetPath' does not produce the expected DLL '$expectedDllName'."
}

$artifactDirectory = Join-Path $repositoryRoot 'artifacts\libraries'
$stagingRoot = Join-Path $projectDirectory.FullName 'obj\sereinflow-library-package'
$publishDirectory = Join-Path $stagingRoot 'publish'
$stagingDirectory = Join-Path $stagingRoot $packageName
$zipPath = Join-Path $artifactDirectory "$packageName.zip"

New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$artifactDirectory = (Resolve-Path -LiteralPath $artifactDirectory).Path
if (-not $SkipBuild) {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

    $publishArguments = @(
        'publish',
        $project.FullName,
        '--configuration',
        $Configuration,
        '--nologo',
        ("-property:PublishDir={0}" -f ((Resolve-Path -LiteralPath $publishDirectory).Path + [IO.Path]::DirectorySeparatorChar))
    )
    if (-not [string]::IsNullOrWhiteSpace($runtimeIdentifier)) {
        $publishArguments += @(
            '--runtime',
            $runtimeIdentifier,
            "-property:RuntimeIdentifier=$runtimeIdentifier",
            "-property:RuntimeIdentifiers=$runtimeIdentifier"
        )
    }
    & dotnet @publishArguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for '$($project.FullName)'."
    }
}
elseif (-not (Test-Path -LiteralPath $publishDirectory -PathType Container)) {
    throw "SkipBuild requires an existing publish directory: $publishDirectory"
}

if (-not (Test-Path -LiteralPath $targetPath -PathType Leaf)) {
    throw "The evaluated TargetPath does not exist after publishing: $targetPath"
}

$publishFiles = @(
    Get-ChildItem -LiteralPath $publishDirectory -Recurse -File |
        Sort-Object FullName
)
if ($publishFiles.Count -eq 0) {
    throw "The publish directory contains no output files: $publishDirectory"
}

$publishedDllPath = Join-Path $publishDirectory $expectedDllName
if (-not (Test-Path -LiteralPath $publishedDllPath -PathType Leaf)) {
    throw "The publish directory does not contain the expected library DLL '$expectedDllName'."
}
if ((Get-Item -LiteralPath $publishedDllPath).Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw "The published library DLL must not be a symbolic link or reparse point: $publishedDllPath"
}

if (Test-Path -LiteralPath $stagingDirectory) {
    Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null
foreach ($file in $publishFiles) {
    if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "The publish output must not contain symbolic links or reparse points: $($file.FullName)"
    }

    $relativePath = [IO.Path]::GetRelativePath($publishDirectory, $file.FullName).Replace('\', '/')
    if ([IO.Path]::IsPathRooted($relativePath) -or $relativePath -match '(^|/)\.\.(/|$)|(^|/):|\\') {
        throw "The publish output contains an unsafe relative path: $relativePath"
    }

    $destination = Join-Path $stagingDirectory ($relativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
    $destinationDirectory = Split-Path -Parent $destination
    New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

$archive = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $publishFiles) {
        $relativePath = [IO.Path]::GetRelativePath($publishDirectory, $file.FullName).Replace('\', '/')
        $sourcePath = Join-Path $stagingDirectory ($relativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
        $entry = $archive.CreateEntry("$packageName/$relativePath", [IO.Compression.CompressionLevel]::Optimal)
        $source = [IO.File]::OpenRead($sourcePath)
        $destination = $null
        try {
            $destination = $entry.Open()
            $source.CopyTo($destination)
        }
        finally {
            if ($null -ne $destination) {
                $destination.Dispose()
            }
            $source.Dispose()
        }
    }
}
finally {
    $archive.Dispose()
}

$archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $seenEntries = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $fileEntries = @($archive.Entries | Where-Object { -not [string]::IsNullOrEmpty($_.Name) })
    $expectedEntry = "$packageName/$expectedDllName"
    $entryNames = @($fileEntries | ForEach-Object { $_.FullName.Replace('\', '/') })
    $expectedEntryNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $publishFiles) {
        $relativePath = [IO.Path]::GetRelativePath($publishDirectory, $file.FullName).Replace('\', '/')
        [void] $expectedEntryNames.Add("$packageName/$relativePath")
    }
    foreach ($entryName in $entryNames) {
        if ([IO.Path]::IsPathRooted($entryName) -or $entryName -match '(^|/)\.\.(/|$)|(^|/):|\\') {
            throw "ZIP contains an unsafe entry name: $entryName"
        }
        if (-not $entryName.StartsWith("$packageName/", [StringComparison]::Ordinal)) {
            throw "ZIP contains a file outside the package directory '$packageName': $entryName"
        }
        if (-not $seenEntries.Add($entryName)) {
            throw "ZIP contains a duplicate file entry: $entryName"
        }
        $extension = [IO.Path]::GetExtension($entryName).ToLowerInvariant()
        if ($extension -in @('.cs', '.csx', '.fs', '.fsx', '.vb', '.vbs', '.py', '.ps1', '.psm1', '.psd1', '.cmd', '.bat', '.sh', '.bash', '.zsh', '.fish', '.js', '.mjs', '.cjs', '.sln', '.slnx', '.csproj', '.fsproj', '.vbproj', '.props', '.targets', '.user', '.zip', '.nupkg', '.exe', '.com')) {
            throw "ZIP contains a source, build, script, archive, or executable file: $entryName"
        }
    }
    $unexpectedEntries = @($entryNames | Where-Object { -not $expectedEntryNames.Contains($_) })
    $missingEntries = @($expectedEntryNames | Where-Object { -not $seenEntries.Contains($_) })
    if ($unexpectedEntries.Count -gt 0 -or $missingEntries.Count -gt 0 -or $entryNames.Count -ne $expectedEntryNames.Count) {
        throw "ZIP entries do not exactly match the clean publish output. Missing: $($missingEntries -join ', '); unexpected: $($unexpectedEntries -join ', ')"
    }
    if (-not $seenEntries.Contains($expectedEntry)) {
        throw "ZIP does not contain the expected library DLL '$expectedEntry'."
    }
}
finally {
    $archive.Dispose()
}

$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
[pscustomobject]@{
    ProjectPath = $project.FullName
    AssemblyName = $assemblyName
    Version = $version
    TargetFramework = [string] $properties.TargetFramework
    RuntimeIdentifier = $runtimeIdentifier
    EnableDynamicLoading = $enableDynamicLoading
    DllPath = $publishedDllPath
    DllSha256 = (Get-FileHash -LiteralPath $publishedDllPath -Algorithm SHA256).Hash
    PublishDirectory = $publishDirectory
    PublishedFileCount = $publishFiles.Count
    ZipPath = $zipPath
    ZipSha256 = $zipHash
    ZipLength = (Get-Item -LiteralPath $zipPath).Length
}
