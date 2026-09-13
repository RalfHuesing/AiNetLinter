<#
.SYNOPSIS
    Ausfuehrbares Vertragsaudit fuer alle oeffentlichen MCP-Symbol-Handoffs.
.DESCRIPTION
    Die Matrix ist die explizite Inventur der Producer, Consumer und zulaessigen Toolketten.
    Das Skript prueft gegen die tatsaechlichen Registrierungs- und Ausfuehrungspfade und fuehrt
    anschliessend die schema- und kettenbasierten FastTests aus. Ein fehlender Nachweis ist rot.

    Ohne -SkipContractTests wird ein vorhandenes FastTests-Build vorausgesetzt. So wird kein
    zweites, vom Gate abweichendes Build erzeugt; nach "dotnet build" ist der Audit-Aufruf echt.
#>

[CmdletBinding()]
param([switch]$SkipContractTests)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$McpRoot = Join-Path $RepoRoot "src/AiNetLinter/Mcp"
$FastTestsProject = Join-Path $RepoRoot "src/AiNetLinter.FastTests/AiNetLinter.FastTests.csproj"

Write-Host "=== AiNetLinter MCP-Handoff-Vertragsaudit ===" -ForegroundColor Cyan

# Jede Zeile steht fuer eine am MCP-Rand sichtbare Symboladresse. Producer emitieren h:-Handles;
# Consumer restaurieren sie vor der Fachlogik. Chains sind bewusst nur fachlich passende Paare.
$Contract = [ordered]@{
    Producers = @(
        @{ Tool = "find_symbol"; File = "Tools/SymbolGraph/FindSymbolTool.cs"; Evidence = "GetOpaqueHandleForOutputOrThrow" },
        @{ Tool = "get_file_skeleton"; File = "../Maps/Skeleton/SkeletonMarkdownRenderer.cs"; Evidence = "GetOpaqueHandle" },
        @{ Tool = "get_symbol_body"; File = "Tools/GetSymbolBodyTool.cs"; Evidence = "GetOpaqueHandle" },
        @{ Tool = "find_references"; File = "Tools/SymbolGraph/CallGraph/TransitiveCallGraphFormatter.cs"; Evidence = "GetOpaqueHandle" },
        @{ Tool = "get_call_tree"; File = "Tools/CallTree/CallGraphTextRenderer.cs"; Evidence = "GetOpaqueHandle" },
        @{ Tool = "get_type_hierarchy"; File = "Tools/SymbolGraph/GetTypeHierarchyFormatter.cs"; Evidence = "GetOpaqueHandle" },
        @{ Tool = "find_implementations"; File = "Tools/TypeHierarchy/FindImplementationsTool.cs"; Evidence = "GetOpaqueHandle" },
        @{ Tool = "get_class_structure"; File = "Tools/FileStructure/GetClassStructureTool.cs"; Evidence = "GetOpaqueHandle" },
        @{ Tool = "metrics_lookup"; File = "Tools/MetricsLookup/MetricsLookupTool.cs"; Evidence = "GetOrCreateOpaqueHandleForOutput" },
        @{ Tool = "search_assembly"; File = "Tools/AssemblyAnalysis/AssemblySearchTool.Execution.cs"; Evidence = "GetOpaqueHandle" },
        @{ Tool = "inspect_assembly"; File = "Tools/AssemblyAnalysis/InspectAssemblyFormatter.cs"; Evidence = "GetOpaqueHandle" },
        @{ Tool = "find_assembly_extensions"; File = "Tools/AssemblyAnalysis/Responses/FindAssemblyExtensionsResponseBuilder.cs"; Evidence = "GetOpaqueHandle" }
    )
    Consumers = @(
        @{ Tool = "get_symbol_body"; Field = "symbolIdentifiers"; Registration = "Registration/SymbolBodyToolRegistrations.cs"; Execution = "Tools/GetSymbolBodyTool.cs"; Restore = "TryRestoreSymbolIdentifiers" },
        @{ Tool = "metrics_lookup"; Field = "symbolIdentifiers"; Registration = "Registration/AnalysisToolRegistrations.cs"; Execution = "Tools/MetricsLookup/MetricsLookupTool.cs"; Restore = "TryRestoreSymbolIdentifiers" },
        @{ Tool = "find_references"; Field = "symbolIdentifier"; Registration = "Registration/SymbolGraphToolRegistrations.cs"; Execution = "Tools/SymbolGraph/FindReferencesTool.cs"; Restore = "TryRestoreSymbolIdentifier" },
        @{ Tool = "get_call_tree"; Field = "symbolIdentifier"; Registration = "Registration/SymbolGraphToolRegistrations.cs"; Execution = "Tools/CallTree/GetCallTreeTool.cs"; Restore = "TryRestoreSymbolIdentifier" },
        @{ Tool = "get_impact"; Field = "symbolIdentifier"; Registration = "Registration/SymbolGraphToolRegistrations.cs"; Execution = "Tools/SymbolGraph/GetImpactTool.cs"; Restore = "TryRestoreSymbolIdentifier" },
        @{ Tool = "get_type_hierarchy"; Field = "symbolIdentifier"; Registration = "Registration/SymbolGraphToolRegistrations.cs"; Execution = "Tools/SymbolGraph/GetTypeHierarchyTool.cs"; Restore = "TryRestoreSymbolIdentifier" },
        @{ Tool = "find_implementations"; Field = "symbolIdentifier"; Registration = "Registration/SymbolGraphToolRegistrations.cs"; Execution = "Tools/TypeHierarchy/FindImplementationsTool.cs"; Restore = "TryRestoreSymbolIdentifier" },
        @{ Tool = "dependency_graph"; Field = "symbolIdentifier"; Registration = "Registration/SymbolGraphToolRegistrations.cs"; Execution = "Tools/DependencyGraph/DependencyGraphTool.cs"; Restore = "TryRestoreSymbolIdentifier" },
        @{ Tool = "get_class_structure"; Field = "symbolIdentifier"; Registration = "Registration/FileStructureToolRegistrations.cs"; Execution = "Tools/FileStructure/GetClassStructureTool.cs"; Restore = "TryRestoreSymbolIdentifier" },
        @{ Tool = "get_feature_context"; Field = "symbolIdentifier"; Registration = "Registration/AnalysisToolRegistrations.cs"; Execution = "Tools/FeatureContext/GetFeatureContextTool.cs"; Restore = "TryRestoreSymbolIdentifier" },
        @{ Tool = "get_test_context"; Field = "symbolIdentifier"; Registration = "Registration/AnalysisToolRegistrations.cs"; Execution = "Tools/TestContext/GetTestContextTool.cs"; Restore = "TryRestoreSymbolIdentifier" },
        @{ Tool = "get_assembly_context"; Field = "symbolIdentifier"; Registration = "Registration/AssemblyAnalysisToolRegistrations.cs"; Execution = "Tools/AssemblyAnalysis/AssemblyAnalysisContextTool.cs"; Restore = "TryRestoreSymbolIdentifier" },
        @{ Tool = "find_duplicates"; Field = "helperSymbol"; Registration = "Registration/DuplicateDetectionToolRegistrations.cs"; Execution = "Tools/DuplicateDetection/DuplicateDetectionTool.cs"; Restore = "TryRestoreSymbolIdentifier" },
        @{ Tool = "resolve_type_origin"; Field = "typeName"; Registration = "Registration/SymbolGraphToolRegistrations.cs"; Execution = "Tools/TypeResolution/ResolveTypeOriginHandoffResolver.cs"; Restore = "RestoreInternalHandoffForInput" }
    )
    Chains = @(
        @{ Producer = "find_symbol"; Consumer = "get_symbol_body"; Field = "symbolIdentifiers"; Test = "GetSymbolBodyToolTests" },
        @{ Producer = "find_symbol"; Consumer = "find_references"; Field = "symbolIdentifier"; Test = "FindReferencesToolTests" },
        @{ Producer = "find_symbol"; Consumer = "find_duplicates"; Field = "helperSymbol"; Test = "DuplicateDetectionToolRefactoringDriftTests" },
        @{ Producer = "find_symbol"; Consumer = "resolve_type_origin"; Field = "typeName"; Test = "ResolveTypeOrigin" },
        @{ Producer = "inspect_assembly"; Consumer = "get_assembly_context"; Field = "symbolIdentifier"; Test = "AssemblyAnalysisContextTextModelTests" },
        @{ Producer = "search_assembly"; Consumer = "get_assembly_context"; Field = "symbolIdentifier"; Test = "AssemblyAnalysisContextTextModelTests" },
        @{ Producer = "find_assembly_extensions"; Consumer = "get_assembly_context"; Field = "symbolIdentifier"; Test = "AssemblyAnalysisContextTextModelTests" }
    )
}

$Failures = [System.Collections.Generic.List[string]]::new()

# Breite Basissuche: äquivalent zu rg '[^a-z]s:' und '[^a-z]a:' (hier ohne
# Gross-/Kleinschreibungs-Falle). Sie sucht absichtlich nicht nur "handoffId",
# damit ein neuer Renderer keine alte Wire-ID unter einem anderen Label ausgeben kann.
# Ausgenommen sind ausschließlich die zwei internen Parser/Registry-Implementierungen und
# der interne Resolver; öffentliche Renderer oder MCP-Responses stehen hier niemals.
$LegacyWirePattern = [regex]'(?i)(?<![a-z])[sa]:'
$LegacyWireInternalOnly = @(
    "Handoffs/HandoffHandleRegistry.cs",
    "Handoffs/SymbolHandoffIdentifier.cs",
    "Tools/SymbolGraph/SymbolIdentifierResolver.cs"
)

if (-not $LegacyWirePattern.IsMatch('handoffId: `s:internal`') -or -not $LegacyWirePattern.IsMatch('handoffId: `a:internal`')) {
    $Failures.Add("Basissuche erkennt sichtbare s:/a:-Wire-IDs nicht")
}
if ($LegacyWirePattern.IsMatch('Class: Value') -or $LegacyWirePattern.IsMatch('metadata: value')) {
    $Failures.Add("Basissuche verwechselt normale Bezeichner mit Wire-IDs")
}

function Read-McpFile([string]$RelativePath) {
    $path = Join-Path $McpRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path)) {
        $Failures.Add("Datei fehlt: $RelativePath")
        return $null
    }
    return Get-Content -Raw -LiteralPath $path
}

foreach ($producer in $Contract.Producers) {
    $content = Read-McpFile $producer.File
    if ($null -ne $content -and $content -notmatch [regex]::Escape($producer.Evidence)) {
        $Failures.Add("Producer $($producer.Tool): $($producer.Evidence) fehlt in $($producer.File)")
    }
}

foreach ($consumer in $Contract.Consumers) {
    $registration = Read-McpFile $consumer.Registration
    $execution = Read-McpFile $consumer.Execution
    if ($null -ne $registration -and $registration -notmatch "`"$($consumer.Tool)`"") {
        $Failures.Add("Consumer $($consumer.Tool).$($consumer.Field): Tool ist nicht registriert")
    }
    if ($null -ne $registration -and $registration -notmatch "\b$([regex]::Escape($consumer.Field))\b") {
        $Failures.Add("Consumer $($consumer.Tool).$($consumer.Field): Feld fehlt in der Registrierung")
    }
    if ($null -ne $execution -and $execution -notmatch [regex]::Escape($consumer.Restore)) {
        $Failures.Add("Consumer $($consumer.Tool).$($consumer.Field): $($consumer.Restore) fehlt vor der Fachlogik")
    }
}

foreach ($chain in $Contract.Chains) {
    if (-not ($Contract.Producers.Tool -contains $chain.Producer)) {
        $Failures.Add("Kette $($chain.Producer) -> $($chain.Consumer): Producer fehlt in der Matrix")
    }
    if (-not ($Contract.Consumers | Where-Object { $_.Tool -eq $chain.Consumer -and $_.Field -eq $chain.Field })) {
        $Failures.Add("Kette $($chain.Producer) -> $($chain.Consumer).$($chain.Field): Consumer fehlt in der Matrix")
    }
}

$rawWirePatterns = foreach ($file in Get-ChildItem -Path $McpRoot -Filter "*.cs" -Recurse) {
    $relativePath = $file.FullName.Substring($McpRoot.Length).TrimStart('\', '/').Replace('\', '/')
    if ($LegacyWireInternalOnly -contains $relativePath) {
        continue
    }

    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $file.FullName) {
        $lineNumber++
        if ($LegacyWirePattern.IsMatch($line)) {
            [pscustomobject]@{ Path = $relativePath; LineNumber = $lineNumber; Line = $line.Trim() }
        }
    }
}
foreach ($match in $rawWirePatterns) {
    $Failures.Add("Sichtbares Alt-Wire-Muster: $($match.Path):$($match.LineNumber): $($match.Line)")
}

if (-not $SkipContractTests) {
    Write-Host "--- Schema- und Kettenvertrag aus der echten Tool-Collection ---" -ForegroundColor Yellow
    & dotnet test $FastTestsProject --no-build --filter "FullyQualifiedName~HandoffToolContractTests|FullyQualifiedName~DuplicateDetectionToolRefactoringDriftTests" --nologo
    if ($LASTEXITCODE -ne 0) {
        $Failures.Add("Schema- oder Kettenvertragstest ist rot (dotnet test Exit $LASTEXITCODE)")
    }
}

Write-Host "--- Inventur: $($Contract.Producers.Count) Producer, $($Contract.Consumers.Count) Consumer, $($Contract.Chains.Count) Ketten ---" -ForegroundColor Yellow
if ($Failures.Count -gt 0) {
    $Failures | ForEach-Object { Write-Host "  [OFFEN] $_" -ForegroundColor Red }
    Write-Host "[ROT-TEST]: $($Failures.Count) Vertragsluecke(n) gefunden. (Exit 1)" -ForegroundColor Red
    exit 1
}

Write-Host "[PASS]: Vollstaendige Handoff-Matrix, reale Schemas und Toolketten bestaetigt. (Exit 0)" -ForegroundColor Green
