#requires -Version 7.0
<#
.SYNOPSIS
    Fuehrt StressTests fuer AiNetLinter aus und schreibt das Ergebnis nach TestResults/StressTests.trx.

.PARAMETER Filter
    xUnit-Filter fuer dotnet test (Standard: Category=Stress).
#>
[CmdletBinding()]
param(
    [string]$Filter = 'Category=Stress',
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AdditionalArgs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
    $root = git rev-parse --show-toplevel 2>$null
    if (-not $root) {
        $candidate = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
        if (Test-Path (Join-Path $candidate 'src/AiNetLinter/AiNetLinter.csproj')) {
            return $candidate
        }
        throw 'Repository-Root konnte nicht ermittelt werden.'
    }
    return (Resolve-Path $root).Path
}

$repoRoot = Get-RepoRoot
$resultsDir = Join-Path $repoRoot 'TestResults'
if (-not (Test-Path $resultsDir)) {
    New-Item -ItemType Directory -Path $resultsDir -Force | Out-Null
}

$trxFile = 'StressTests.trx'
$projectPath = Join-Path $repoRoot 'src/AiNetLinter.IntegrationTests'

Write-Host "[INFO] StressTests (Filter: $Filter) -> TestResults/$trxFile" -ForegroundColor Cyan

$allArgs = @(
    'test',
    $projectPath,
    '--filter', $Filter,
    '--logger', "trx;LogFileName=$trxFile",
    '--results-directory', $resultsDir
)
if ($AdditionalArgs) {
    $allArgs += $AdditionalArgs
}

& dotnet @allArgs
exit $LASTEXITCODE
