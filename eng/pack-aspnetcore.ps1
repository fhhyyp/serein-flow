param(
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/aspnetcore-package'),
    [string] $PackageVersion = '0.1.0'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$feed = [IO.Path]::GetFullPath($OutputDirectory)
$workerPublish = Join-Path ([IO.Path]::GetTempPath()) ('sereinflow-worker-pack-' + [guid]::NewGuid().ToString('N'))
$workerPublish = [IO.Path]::GetFullPath($workerPublish)
$hostPublish = Join-Path ([IO.Path]::GetTempPath()) ('sereinflow-host-pack-' + [guid]::NewGuid().ToString('N'))
$hostPublish = [IO.Path]::GetFullPath($hostPublish)
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
foreach ($directory in @($workerPublish, $hostPublish)) {
    if (-not $directory.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Staging directory is outside the temporary directory: $directory"
    }
}
New-Item -ItemType Directory -Path $feed -Force | Out-Null
New-Item -ItemType Directory -Path $workerPublish -Force | Out-Null
New-Item -ItemType Directory -Path $hostPublish -Force | Out-Null

try {
    $runnerProject = Join-Path $repoRoot 'src/SereinFlow.Worker.Runner/SereinFlow.Worker.Runner.csproj'
    dotnet publish $runnerProject -c Release --output $workerPublish -p:UseAppHost=false -p:BaseOutputPath=artifacts/package-build/bin/ -m:1 -p:BuildInParallel=false -v:q
    if ($LASTEXITCODE -ne 0) { throw 'Worker Runner publish failed.' }

    $projectFile = Join-Path $repoRoot 'src/SereinFlow.AspNetCore/SereinFlow.AspNetCore.csproj'
    dotnet publish $projectFile -c Release --output $hostPublish -p:BaseOutputPath=artifacts/package-build/bin/ -m:1 -p:BuildInParallel=false -v:q
    if ($LASTEXITCODE -ne 0) { throw 'ASP.NET Core host publish failed.' }

    dotnet pack $projectFile -c Release --output $feed "-p:PackageVersion=$PackageVersion" "-p:SereinFlowWorkerPublishDir=$workerPublish" "-p:SereinFlowHostPublishDir=$hostPublish" -p:BaseOutputPath=artifacts/package-build/bin/ -m:1 -p:BuildInParallel=false -v:q
    if ($LASTEXITCODE -ne 0) { throw 'SereinFlow.AspNetCore pack failed.' }

    $securityProject = Join-Path $repoRoot 'src/SereinFlow.Security.ApiKeys/SereinFlow.Security.ApiKeys.csproj'
    dotnet pack $securityProject -c Release --output $feed "-p:PackageVersion=$PackageVersion" -p:UseAppHost=false -p:BaseOutputPath=artifacts/package-build/bin/ -m:1 -p:BuildInParallel=false -v:q
    if ($LASTEXITCODE -ne 0) { throw 'SereinFlow.Security.ApiKeys pack failed.' }

    Write-Host "SereinFlow package is available at $(Join-Path $feed "SereinFlow.AspNetCore.$PackageVersion.nupkg")"
}
finally {
    if (Test-Path -LiteralPath $workerPublish) {
        Remove-Item -LiteralPath $workerPublish -Recurse -Force
    }
    if (Test-Path -LiteralPath $hostPublish) {
        Remove-Item -LiteralPath $hostPublish -Recurse -Force
    }
}
