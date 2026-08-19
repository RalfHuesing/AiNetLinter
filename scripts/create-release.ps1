#requires -Version 7.0
<#
.SYNOPSIS
    Erhoeht die Projektversion, laedt ausstehende Commits hoch und erzeugt ein GitHub-Release per Tag.

.DESCRIPTION
    1. Prueft, ob der Working Tree sauber ist (bricht bei uncommitteten Aenderungen ab)
    2. Synchronisiert mit origin/main und laedt ausstehende lokale Commits vorab hoch
    3. Liest die Version aus src/AiNetLinter/AiNetLinter.csproj
    4. Erhoeht die Patch-Version um 1 (z. B. 1.0.5 -> 1.0.6)
    5. Fuehrt dotnet build/test aus
    6. Committet die Versionsaenderung, pusht main und den Tag vX.Y.Z
    7. Der GitHub-Workflow .github/workflows/release.yml erstellt das Release

    Git-Authentifizierung erfolgt ueber die lokale Umgebung (Credential Manager / SSH).
    gh ist optional und wird nur zum Status-Monitoring genutzt, falls angemeldet.

.PARAMETER Branch
    Branch-Name, auf dem das Release durchgefuehrt wird (Standard: main).

.PARAMETER DryRun
    Zeigt geplante Schritte ohne Aenderungen, Commit, Push oder Tag.

.PARAMETER SkipTests
    Ueberspringt die Testausfuehrung vor dem Release.

.PARAMETER FullTests
    Fuehrt die vollstaendige Test-Suite inkl. Integrationstests (Category!=Stress) aus.

.PARAMETER TestFilter
    Benutzerdefinierter xUnit-Filter fuer dotnet test (Standard: Category=Unit).
#>
[CmdletBinding()]
param(
    [string]$Branch = 'main',
    [switch]$DryRun,
    [switch]$SkipTests,
    [switch]$FullTests,
    [string]$TestFilter = 'Category=Unit'
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
    $status = @(git status --porcelain 2>$null)
    if ($status.Count -gt 0) {
        Write-Host '[ERROR] Es sind uncommittete Aenderungen im Working Tree vorhanden:' -ForegroundColor Red
        foreach ($line in $status) {
            Write-Host "        $line" -ForegroundColor Yellow
        }
        Write-Host '[ERROR] Bitte alle Aenderungen committen oder stashen, bevor das Release ausgefuehrt wird.' -ForegroundColor Red
        throw "Working Tree ist nicht sauber ($($status.Count) geaenderte/untracked Datei(en)). Release abgebrochen."
    }
}

function Sync-PreReleaseCommits {
    param([string]$TargetBranch)

    Write-Host "[INFO] Pruefe Branch- und Remote-Status ($TargetBranch)..." -ForegroundColor Cyan

    $currentBranch = (git branch --show-current 2>$null)
    if ($null -ne $currentBranch) { $currentBranch = $currentBranch.Trim() }

    if (-not $currentBranch) {
        throw 'Konnte aktuellen Git-Branch nicht ermitteln (Detached HEAD?).'
    }

    if ($currentBranch -ne $TargetBranch) {
        throw "Aktueller Branch ist '$currentBranch', Release ist nur auf Branch '$TargetBranch' erlaubt."
    }

    Write-Host "[INFO] git fetch origin $TargetBranch..." -ForegroundColor Cyan
    git fetch origin $TargetBranch 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Fehler beim Abrufen von Remote 'origin $TargetBranch'."
    }

    # Pruefen, ob lokaler Branch hinter origin/$TargetBranch liegt
    $behindOutput = (git rev-list --count "HEAD..origin/$TargetBranch" 2>$null)
    $behindCount = if ($behindOutput -match '^\d+$') { [int]$behindOutput.Trim() } else { 0 }
    if ($behindCount -gt 0) {
        throw "Lokaler Branch '$TargetBranch' ist um $behindCount Commit(s) hinter 'origin/$TargetBranch'. Bitte zuerst pullen/rebasen."
    }

    # Pruefen, ob ungesendete lokale Commits vor dem Release existieren
    $aheadOutput = (git rev-list --count "origin/$TargetBranch..HEAD" 2>$null)
    $aheadCount = if ($aheadOutput -match '^\d+$') { [int]$aheadOutput.Trim() } else { 0 }

    if ($aheadCount -gt 0) {
        Write-Host "[INFO] $aheadCount ungesendete(r) lokale(r) Commit(s) gefunden. Lade Commits vor dem Release hoch..." -ForegroundColor Cyan
        Invoke-GitStep "git push origin $TargetBranch (Vorab-Sync)" { git push origin $TargetBranch }
    }
    else {
        Write-Host "[INFO] Lokaler Branch ist bereits synchron mit origin/$TargetBranch." -ForegroundColor Green
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
    Sync-PreReleaseCommits -TargetBranch $Branch

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
    Write-Host "  Branch  : $Branch"
    Write-Host "  Aktuell : $currentVersion"
    Write-Host "  Neu     : $newVersion"
    Write-Host "  Tag     : $tagName"
    Write-Host "  DryRun  : $DryRun"
    Write-Host ''

    if (git tag --list $tagName) {
        throw "Tag $tagName existiert bereits lokal."
    }

    $remoteTag = (git ls-remote --tags origin "refs/tags/$tagName" 2>$null)
    if ($remoteTag) {
        throw "Tag $tagName existiert bereits auf Remote 'origin'."
    }

    if (-not $DryRun) {
        Invoke-DotNetValidation -RepoRoot $repoRoot
        Assert-CleanWorkingTree
        Set-ProjectVersion -ProjectPath $projectPath -NewVersion $newVersion
    }

    Invoke-GitStep "git add $ProjectFile" { git add -- $ProjectFile }
    Invoke-GitStep "git commit -m `"$commitMessage`"" { git commit -m $commitMessage }
    Invoke-GitStep "git push origin $Branch" { git push origin $Branch }
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
