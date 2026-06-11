#requires -Version 7.0
<#
.SYNOPSIS
    Erhoeht die Projektversion, pusht main und erzeugt ein GitHub-Release per Tag.

.DESCRIPTION
    1. Liest die Version aus src/AiNetLinter/AiNetLinter.csproj
    2. Erhoeht die Patch-Version um 1 (z. B. 1.0.5 -> 1.0.6)
    3. Fuehrt dotnet build/test aus
    4. Committet die Versionsaenderung, pusht main und den Tag vX.Y.Z
    5. Der GitHub-Workflow .github/workflows/release.yml erstellt das Release

    Git-Authentifizierung erfolgt ueber die lokale Umgebung (Credential Manager / SSH).
    gh ist optional und wird nur zum Status-Monitoring genutzt, falls angemeldet.

.PARAMETER DryRun
    Zeigt geplante Schritte ohne Aenderungen, Commit, Push oder Tag.

.PARAMETER SkipTests
    Ueberspringt dotnet test (nicht empfohlen).
#>
[CmdletBinding()]
param(
    [switch]$DryRun,
    [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ProjectFile = 'src/AiNetLinter/AiNetLinter.csproj'
$VersionPattern = '(?<=<Version>)\d+\.\d+\.\d+(?=</Version>)'

function Get-RepoRoot {
    $root = git rev-parse --show-toplevel 2>$null
    if (-not $root) {
        throw 'Kein Git-Repository gefunden. Bitte im AiNetLinter-Repo ausfuehren.'
    }

    return (Resolve-Path $root).Path
}

function Assert-CleanWorkingTree {
    $status = git status --porcelain
    if ($status) {
        throw "Working tree ist nicht sauber:`n$status"
    }
}

function Get-ProjectVersion {
    param([string]$ProjectPath)

    $content = Get-Content -Path $ProjectPath -Raw
    if ($content -notmatch $VersionPattern) {
        throw "Keine <Version> in $ProjectPath gefunden."
    }

    return [version]$Matches[0]
}

function Set-ProjectVersion {
    param(
        [string]$ProjectPath,
        [version]$NewVersion
    )

    $content = Get-Content -Path $ProjectPath -Raw
    $updated = [regex]::Replace(
        $content,
        $VersionPattern,
        $NewVersion.ToString(),
        1)

    if ($updated -eq $content) {
        throw "Version in $ProjectPath konnte nicht aktualisiert werden."
    }

    Set-Content -Path $ProjectPath -Value $updated -NoNewline -Encoding utf8
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
            Write-Host '[INFO] dotnet test...' -ForegroundColor Cyan
            dotnet test --nologo -v q --no-build
            if ($LASTEXITCODE -ne 0) {
                throw 'dotnet test fehlgeschlagen.'
            }
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-GitStep {
    param(
        [string]$Description,
        [scriptblock]$Action
    )

    Write-Host "[INFO] $Description" -ForegroundColor Cyan
    if ($DryRun) {
        Write-Host "       (DryRun)" -ForegroundColor DarkGray
        return
    }

    & $Action
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "Git-Befehl fehlgeschlagen: $Description"
    }
}

function Test-GhAuthenticated {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        return $false
    }

    gh auth status 2>$null | Out-Null
    return $LASTEXITCODE -eq 0
}

function Wait-ReleaseWorkflow {
    param(
        [string]$TagName,
        [int]$TimeoutMinutes = 20
    )

    if (-not (Test-GhAuthenticated)) {
        Write-Host '[INFO] gh nicht angemeldet – Workflow-Status manuell pruefen:' -ForegroundColor Yellow
        Write-Host '       https://github.com/RalfHuesing/AiNetLinter/actions/workflows/release.yml'
        return
    }

    Write-Host "[INFO] Warte auf Release-Workflow fuer $TagName..." -ForegroundColor Cyan
    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)

    while ((Get-Date) -lt $deadline) {
        $runJson = gh run list --workflow release.yml --limit 5 --json databaseId,headBranch,status,conclusion,url
        $run = $runJson | ConvertFrom-Json | Where-Object { $_.headBranch -eq $TagName } | Select-Object -First 1

        if ($run) {
            if ($run.status -eq 'completed') {
                if ($run.conclusion -eq 'success') {
                    Write-Host "[OK] Release-Workflow erfolgreich: $($run.url)" -ForegroundColor Green
                    gh release view $TagName --json url -q .url 2>$null | ForEach-Object {
                        Write-Host "[OK] GitHub Release: $_" -ForegroundColor Green
                    }
                    return
                }

                throw "Release-Workflow fehlgeschlagen: $($run.url)"
            }

            Write-Host "       Status: $($run.status) – $($run.url)" -ForegroundColor DarkGray
        }

        Start-Sleep -Seconds 15
    }

    throw "Timeout beim Warten auf den Release-Workflow fuer $TagName."
}

$repoRoot = Get-RepoRoot
Push-Location $repoRoot
try {
    Assert-CleanWorkingTree

    $projectPath = Join-Path $repoRoot $ProjectFile
    if (-not (Test-Path $projectPath)) {
        throw "Projektdatei nicht gefunden: $projectPath"
    }

    $currentVersion = Get-ProjectVersion -ProjectPath $projectPath
    $newVersion = [version]"$($currentVersion.Major).$($currentVersion.Minor).$($currentVersion.Build + 1)"
    $tagName = "v$newVersion"
    $commitMessage = "chore(release): Version $newVersion"

    Write-Host ''
    Write-Host "AiNetLinter Release" -ForegroundColor White
    Write-Host "  Aktuell : $currentVersion"
    Write-Host "  Neu     : $newVersion"
    Write-Host "  Tag     : $tagName"
    Write-Host "  DryRun  : $DryRun"
    Write-Host ''

    if (git tag --list $tagName) {
        throw "Tag $tagName existiert bereits lokal."
    }

    if (-not $DryRun) {
        Invoke-DotNetValidation -RepoRoot $repoRoot
        Set-ProjectVersion -ProjectPath $projectPath -NewVersion $newVersion
    }

    Invoke-GitStep "git add $ProjectFile" { git add -- $ProjectFile }
    Invoke-GitStep "git commit" { git commit -m $commitMessage }
    Invoke-GitStep 'git push origin main' { git push origin main }
    Invoke-GitStep "git tag $tagName" { git tag $tagName }
    Invoke-GitStep "git push origin $tagName" { git push origin $tagName }

    if ($DryRun) {
        Write-Host '[OK] DryRun abgeschlossen – keine Aenderungen vorgenommen.' -ForegroundColor Green
        return
    }

    Wait-ReleaseWorkflow -TagName $tagName
    Write-Host "[OK] Release $tagName ausgeloest." -ForegroundColor Green
}
finally {
    Pop-Location
}
