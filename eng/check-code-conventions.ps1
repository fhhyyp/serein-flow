param(
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$frontendChecker = Join-Path $PSScriptRoot 'code-conventions/check-frontend.ts'
$frontendTests = Join-Path $PSScriptRoot 'code-conventions/check-frontend.test.ts'
$csharpProject = Join-Path $PSScriptRoot 'code-conventions/CSharpConventions/CSharpConventions.csproj'

if ($SelfTest) {
    & node --test $frontendTests
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    & dotnet run --project $csharpProject --configuration Release -- $repositoryRoot --self-test
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

& node $frontendChecker
$frontendExitCode = $LASTEXITCODE
& dotnet run --project $csharpProject --configuration Release -- $repositoryRoot
$csharpExitCode = $LASTEXITCODE
if ($frontendExitCode -ne 0 -or $csharpExitCode -ne 0) {
    exit 1
}
