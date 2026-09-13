<#
.SYNOPSIS
    Audit- und Rot-Test-Skript zur Überprüfung aller MCP-Handoff-Einbaustellen.
.DESCRIPTION
    Prüft, ob alle Producer an HandoffHandleRegistry.GetOrCreateOpaqueHandleForOutput
    und alle Consumer an HandoffHandleRegistry.RestoreInternalHandoffForInput angebunden sind.
    Liefert Exit-Code 1 (Rot-Test), solange unmigrierte Stellen vorhanden sind, und 0 (Grün),
    wenn die Migration vollständig ist.
#>

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$McpRoot = Join-Path $RepoRoot "src/AiNetLinter/Mcp"

Write-Host "=== AiNetLinter Handoff-Audit & Rot-Test ===" -ForegroundColor Cyan

$ProducerFiles = @(
    "Tools/SymbolGraph/FindSymbolTool.cs",
    "Tools/GetSymbolBodyTool.cs",
    "Tools/SymbolGraph/CallGraph/TransitiveCallGraphFormatter.cs",
    "Tools/SymbolGraph/GetTypeHierarchyFormatter.cs",
    "Tools/FileStructure/GetClassStructureTool.cs",
    "Tools/TypeHierarchy/FindImplementationsTool.cs",
    "../Maps/Skeleton/SkeletonMarkdownRenderer.cs",
    "Tools/CallTree/CallGraphTextRenderer.cs",
    "Tools/MetricsLookup/MetricsLookupTool.cs",
    "Tools/AssemblyAnalysis/AssemblySearchTool.Execution.cs",
    "Tools/AssemblyAnalysis/InspectAssemblyFormatter.cs",
    "Tools/AssemblyAnalysis/Responses/FindAssemblyExtensionsResponseBuilder.cs"
)

$ConsumerFiles = @(
    "Tools/SymbolGraph/SymbolIdentifierResolver.cs",
    "Tools/TypeResolution/ResolveTypeOriginHandoffResolver.cs",
    "Tools/TypeHierarchy/FindImplementationsTool.cs",
    "Tools/SymbolGraph/FindReferencesTool.cs",
    "Tools/SymbolGraph/GetTypeHierarchyTool.cs",
    "Tools/GetSymbolBodyTool.cs",
    "Tools/FileStructure/GetClassStructureTool.cs",
    "Tools/CallTree/GetCallTreeTool.cs",
    "Tools/CallTree/AssemblyGetCallTreeTool.cs",
    "Tools/SymbolGraph/GetImpactTool.cs",
    "Tools/DependencyGraph/DependencyGraphTool.cs",
    "Tools/FeatureContext/GetFeatureContextTool.cs",
    "Tools/TestContext/GetTestContextTool.cs",
    "Tools/MetricsLookup/MetricsLookupTool.cs",
    "Tools/DuplicateDetection/DuplicateDetectionTool.cs",
    "Tools/AssemblyAnalysis/AssemblyAnalysisContextTool.cs",
    "Tools/SymbolGraph/AssemblyFindReferencesTool.cs"
)

$UnmigratedProducers = [System.Collections.Generic.List[string]]::new()
$UnmigratedConsumers = [System.Collections.Generic.List[string]]::new()

# 1. Producer prüfen
foreach ($relPath in $ProducerFiles) {
    $fullPath = Join-Path $McpRoot $relPath
    if (-not (Test-Path $fullPath)) {
        continue
    }
    $content = Get-Content -Raw $fullPath
    if ($content -notmatch "GetOrCreateOpaqueHandleForOutput|GetOpaqueHandleForOutputOrThrow|GetOpaqueHandleOrDefault") {
        $UnmigratedProducers.Add("$relPath (fehlt GetOrCreateOpaqueHandleForOutput / GetOpaqueHandleForOutputOrThrow / GetOpaqueHandleOrDefault)")
    }
}

# 2. Consumer prüfen
foreach ($relPath in $ConsumerFiles) {
    $fullPath = Join-Path $McpRoot $relPath
    if (-not (Test-Path $fullPath)) {
        continue
    }
    $content = Get-Content -Raw $fullPath
    if ($content -notmatch "RestoreInternalHandoffForInput|TryRestoreSymbolIdentifier") {
        $UnmigratedConsumers.Add("$relPath (fehlt RestoreInternalHandoffForInput / TryRestoreSymbolIdentifier)")
    }
}

# 3. Direkte Ausgabe alter s:/a: Handoff-IDs im Quellcode prüfen
$rawWirePatterns = Get-ChildItem -Path $McpRoot -Filter "*.cs" -Recurse | Where-Object {
    $_.FullName -notmatch "HandoffCounter|Result\.cs"
} | Select-String -Pattern 'handoffId:\s*`[sa]:'

# 4. Bericht ausgeben
Write-Host ""
Write-Host "--- Producer-Status ($($ProducerFiles.Count - $UnmigratedProducers.Count) / $($ProducerFiles.Count) migriert) ---" -ForegroundColor Yellow
if ($UnmigratedProducers.Count -gt 0) {
    foreach ($item in $UnmigratedProducers) {
        Write-Host "  [OFFEN PRODUCER] $item" -ForegroundColor Red
    }
} else {
    Write-Host "  [OK] Alle Producer externalisieren über HandoffHandleRegistry." -ForegroundColor Green
}

Write-Host ""
Write-Host "--- Consumer-Status ($($ConsumerFiles.Count - $UnmigratedConsumers.Count) / $($ConsumerFiles.Count) migriert) ---" -ForegroundColor Yellow
if ($UnmigratedConsumers.Count -gt 0) {
    foreach ($item in $UnmigratedConsumers) {
        Write-Host "  [OFFEN CONSUMER] $item" -ForegroundColor Red
    }
} else {
    Write-Host "  [OK] Alle Consumer restaurieren über HandoffHandleRegistry." -ForegroundColor Green
}

if ($rawWirePatterns.Count -gt 0) {
    Write-Host ""
    Write-Host "--- Hardcodierte Alt-Wire-Muster gefunden: ---" -ForegroundColor Red
    foreach ($match in $rawWirePatterns) {
        Write-Host "  $($match.Filename):$($match.LineNumber): $($match.Line.Trim())" -ForegroundColor Red
    }
}

$totalOpen = $UnmigratedProducers.Count + $UnmigratedConsumers.Count + $rawWirePatterns.Count

Write-Host ""
if ($totalOpen -gt 0) {
    Write-Host "[ROT-TEST]: $totalOpen unmigrierte Stellen gefunden. (Exit 1)" -ForegroundColor Red
    exit 1
} else {
    Write-Host "[PASS]: Alle Handoff-Stellen sind vollständig migriert! (Exit 0)" -ForegroundColor Green
    exit 0
}
