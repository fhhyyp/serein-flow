param(
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/aspnetcore-feed'),
    [string] $PackageVersion = '0.1.0',
    [switch] $Resume
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$feed = [IO.Path]::GetFullPath($OutputDirectory)
$workerPublish = Join-Path ([IO.Path]::GetTempPath()) ('sereinflow-worker-pack-' + [guid]::NewGuid().ToString('N'))
$workerPublish = [IO.Path]::GetFullPath($workerPublish)
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $workerPublish.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Worker staging directory is outside the temporary directory: $workerPublish"
}
New-Item -ItemType Directory -Path $feed -Force | Out-Null
New-Item -ItemType Directory -Path $workerPublish -Force | Out-Null

try {
    $runnerProject = Join-Path $repoRoot 'src/SereinFlow.Worker.Runner/SereinFlow.Worker.Runner.csproj'
    dotnet publish $runnerProject -c Release --output $workerPublish -p:UseAppHost=false -v:q
    if ($LASTEXITCODE -ne 0) { throw 'Worker Runner publish failed.' }

    $projects = @(
        'SereinFlow.Library',
        'SereinFlow.Domain',
        'SereinFlow.Contracts',
        'SereinFlow.Runtime.Abstractions',
        'SereinFlow.Runtime',
        'SereinFlow.Application',
        'SereinFlow.Infrastructure',
        'SereinFlow.Worker.Protocol',
        'SereinFlow.Worker.Supervisor',
        'SereinFlow.Worker.Client',
        'SereinFlow.ScriptModules',
        'SereinFlow.ScriptAdapter',
        'SereinFlow.Mcp',
        'SereinFlow.AspNetCore'
    )

    $scriptLangProject = Join-Path $repoRoot 'src/ThirdParty/SereinScript/ScriptLang/ScriptLang.csproj'
    if (-not ($Resume -and (Test-Path -LiteralPath (Join-Path $feed "Serein.ScriptLang.$PackageVersion.nupkg")))) {
        dotnet pack $scriptLangProject -c Release --output $feed "-p:PackageVersion=$PackageVersion" -v:q
        if ($LASTEXITCODE -ne 0) { throw 'Serein.ScriptLang pack failed.' }
    }

    foreach ($project in $projects) {
        if ($Resume -and (Test-Path -LiteralPath (Join-Path $feed "$project.$PackageVersion.nupkg"))) {
            continue
        }
        $projectFile = Join-Path $repoRoot ("src/$project/$project.csproj")
        $arguments = @('pack', $projectFile, '-c', 'Release', '--output', $feed, "-p:PackageVersion=$PackageVersion", '-v:q')
        if ($project -eq 'SereinFlow.AspNetCore') {
            $arguments += "-p:SereinFlowWorkerPublishDir=$workerPublish"
        }
        dotnet @arguments
        if ($LASTEXITCODE -ne 0) { throw "$project pack failed." }
    }

    Write-Host "SereinFlow packages are available in $feed"
}
finally {
    if (Test-Path -LiteralPath $workerPublish) {
        Remove-Item -LiteralPath $workerPublish -Recurse -Force
    }
}
