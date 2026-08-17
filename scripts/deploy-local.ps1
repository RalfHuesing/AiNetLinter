#requires -Version 7.0
<#
.SYNOPSIS
    Erstellt ein lokales Release-Paket (z. B. AiNetLinter-win-x64.zip) und optional ein direktes Deployment in ein Zielverzeichnis.

.DESCRIPTION
    Fuehrt die gleichen Publish- und Packaging-Schritte wie der GitHub-Release-Workflow aus,
    allerdings komplett lokal ohne Git-Push, Tags oder GitHub-Abhaengigkeit:
    1. dotnet build & test (optional ueberspringbar oder mit Filtern)
    2. dotnet publish (Release, self-contained, win-x64 oder konfigurierbares RID)
    3. rules.json und README.md in das Publish-Verzeichnis kopieren
    4. AiNetLinter-<RID>.zip erstellen
    5. Optional: Entpacken / Kopieren in ein lokales Tools-Verzeichnis (-DestinationPath)

.PARAMETER DestinationPath
    Optionales Zielverzeichnis (z. B. C:\Tools\AiNetLinter), in das die entpackten Dateien direkt deployt werden.

.PARAMETER OutputDir
    Verzeichnis, in dem die Zip-Datei abgelegt wird (Standard: Repository-Root / artifacts).

.PARAMETER Runtime
    Runtime Identifier fuer das Publish (Standard: win-x64).

.PARAMETER SkipTests
    Ueberspringt die Testausfuehrung vor dem Build.

.PARAMETER FullTests
    Fuehrt die vollstaendige Test-Suite inkl. Integrationstests aus (Filter: Category!=Stress).

.PARAMETER TestFilter
    Benutzerdefinierter xUnit-Filter fuer dotnet test (Standard: Category=Unit).
#>
[CmdletBinding()]
param(
    [string]$DestinationPath,
    [string]$OutputDir,
    [string]$Runtime = 'win-x64',
    [switch]$SkipTests,
    [switch]$FullTests,
    [string]$TestFilter = 'Category=Unit'
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

function Invoke-DotNetValidation {
    param([string]$RepoRoot)

    Push-Location $RepoRoot
    try {
        Write-Host '[INFO] dotnet build...' -ForegroundColor Cyan
        dotnet build --nologo -v q
        if ($LASTEXITCODE -ne 0) {
            throw 'dotnet build fehlgeschlagen.'
        }

        if (-not $SkipTests) {
            $effectiveFilter = if ($FullTests) { 'Category!=Stress' } else { $TestFilter }
            Write-Host "[INFO] dotnet test (Filter: $effectiveFilter)..." -ForegroundColor Cyan
            dotnet test --nologo -v q --no-build --filter $effectiveFilter
            if ($LASTEXITCODE -ne 0) {
                throw 'dotnet test fehlgeschlagen.'
            }
        }
    }
    finally {
        Pop-Location
    }
}

$repoRoot = Get-RepoRoot
Push-Location $repoRoot
try {
    Write-Host ''
    Write-Host "AiNetLinter Lokales Deploy & Packaging" -ForegroundColor White
    Write-Host "  Runtime     : $Runtime"
    Write-Host "  Destination : $(if ($DestinationPath) { $DestinationPath } else { '(kein direktes Tool-Verzeichnis angegeben)' })"
    Write-Host ''

    Invoke-DotNetValidation -RepoRoot $repoRoot

    $publishDir = Join-Path $repoRoot 'artifacts/publish'
    $targetOutputDir = if ($OutputDir) { (Resolve-Path $OutputDir -ErrorAction SilentlyContinue)?.Path ?? $OutputDir } else { Join-Path $repoRoot 'artifacts' }

    if (Test-Path $publishDir) {
        Remove-Item -Path $publishDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
    if (-not (Test-Path $targetOutputDir)) {
        New-Item -ItemType Directory -Path $targetOutputDir -Force | Out-Null
    }

    Write-Host "[INFO] dotnet publish ($Runtime, Release, self-contained)..." -ForegroundColor Cyan
    $projectFile = Join-Path $repoRoot 'src/AiNetLinter/AiNetLinter.csproj'
    dotnet publish $projectFile `
        -c Release `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=false `
        -p:PublishTrimmed=false `
        -o $publishDir `
        --nologo -v q

    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet publish fehlgeschlagen.'
    }

    # Beilagen kopieren
    $rulesFile = Join-Path $repoRoot 'rules.json'
    if (Test-Path $rulesFile) {
        Copy-Item $rulesFile (Join-Path $publishDir 'rules.json') -Force
    }

    $readmeFile = Join-Path $repoRoot 'README.md'
    if (Test-Path $readmeFile) {
        Copy-Item $readmeFile (Join-Path $publishDir 'README.md') -Force
    }

    # Zip-Archiv erzeugen
    $zipFileName = "AiNetLinter-$Runtime.zip"
    $zipFilePath = Join-Path $targetOutputDir $zipFileName
    if (Test-Path $zipFilePath) {
        Remove-Item -Path $zipFilePath -Force
    }

    Write-Host "[INFO] Erstelle Archiv: $zipFilePath..." -ForegroundColor Cyan
    Compress-Archive -Path "$publishDir\*" -DestinationPath $zipFilePath -Force

    # Optional: Direktes Deployment in Tools-Verzeichnis
    if ($DestinationPath) {
        Write-Host "[INFO] Deploye nach $DestinationPath..." -ForegroundColor Cyan
        if (-not (Test-Path $DestinationPath)) {
            New-Item -ItemType Directory -Path $DestinationPath -Force | Out-Null
        }
        Copy-Item -Path "$publishDir\*" -Destination $DestinationPath -Recurse -Force
        Write-Host "[OK] Dateien erfolgreich nach $DestinationPath kopiert." -ForegroundColor Green
    }

    Write-Host ''
    Write-Host "[OK] Lokales Release erfolgreich erstellt!" -ForegroundColor Green
    Write-Host "  Zip-Archiv : $zipFilePath" -ForegroundColor Cyan
    Write-Host "  Publish-Ordner: $publishDir" -ForegroundColor Cyan
    Write-Host ''
}
finally {
    Pop-Location
}
