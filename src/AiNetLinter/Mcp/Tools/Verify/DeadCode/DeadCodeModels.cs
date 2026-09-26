#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Tools.Verify.DeadCode;

/// <summary>
/// Ausfuehrungs-Argumente fuer Dead-Code-Hinweisprojektion.
/// </summary>
internal sealed record DeadCodeAdvisoryOptions(
    string? ScopeFilter = null,
    int MaxResults = 50,
    IReadOnlySet<string>? ScopeFiles = null,
    Config? Config = null,
    AiNetLinter.Mcp.AnalysisSymbolIdentity? HandoffIdentity = null,
    bool SolutionBudget = false,
    Microsoft.CodeAnalysis.Solution? PreviousSolution = null,
    TimeProvider? Clock = null,
    string? RequestedScope = null);

/// <summary>
/// Einzelner toter Code-Fund im Structured Output von Dead-Code-Hinweisprojektion.
/// </summary>
internal sealed record DeadCodeEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("containerType")] string ContainerType,
    [property: JsonPropertyName("symbolName")] string SymbolName,
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("line")] int Line,
    [property: JsonPropertyName("column")] int Column,
    [property: JsonPropertyName("accessibility")] string Accessibility,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("limitsApplies")] IReadOnlyList<string> LimitsApplies,
    [property: JsonPropertyName("resultType")] string ResultType = "candidate",
    [property: JsonPropertyName("evidenceBoundary")] string EvidenceBoundary = "statische Referenzsuche innerhalb der Solution; keine Laufzeit- oder externen Consumer-Beweise",
    [property: JsonPropertyName("countercheck")] IReadOnlyList<string>? Countercheck = null,
    [property: JsonPropertyName("internalSymbolIdentifier")] string? InternalSymbolIdentifier = null,
    [property: JsonIgnore] string? ProjectName = null,
    int Priority = 2);

/// <summary>
/// Zusammenfassende Statistik ueber den Dead-Code-Scan.
/// </summary>
internal sealed record DeadCodeSummary(
    [property: JsonPropertyName("documentsInScope")] int DocumentsInScope,
    [property: JsonPropertyName("scannedSymbols")] int ScannedSymbols,
    [property: JsonPropertyName("totalDead")] int TotalDead,
    [property: JsonPropertyName("byKind")] IReadOnlyDictionary<string, int> ByKind,
    [property: JsonPropertyName("status")] string Status = "checked",
    [property: JsonPropertyName("cause")] string Cause = "Statischer Scan im angeforderten Scope.",
    [property: JsonPropertyName("returnedCandidates")] int ReturnedCandidates = 0,
    [property: JsonPropertyName("truncatedBy")] int TruncatedBy = 0,
    [property: JsonPropertyName("next")] DeadCodeRecommendedNextAction? Next = null,
    [property: JsonPropertyName("apiProtected")] int ApiProtected = 0,
    DeadCodeScanCoverage? Coverage = null);

/// <summary>
/// Empfohlene naechste Aktion fuer den aufrufenden Agenten (Trust-Modell).
/// </summary>
internal sealed record DeadCodeRecommendedNextAction(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("reason")] string Reason);

/// <summary>
/// Gesamtergebnis des Dead-Code-Scanners.
/// </summary>
internal sealed record DeadCodeScanResult(
    [property: JsonPropertyName("candidates")] IReadOnlyList<DeadCodeEntry> DeadSymbols,
    [property: JsonPropertyName("summary")] DeadCodeSummary Summary,
    [property: JsonPropertyName("limits")] IReadOnlyList<string> Limits,
    [property: JsonPropertyName("recommendedNextAction")] DeadCodeRecommendedNextAction RecommendedNextAction,
    [property: JsonPropertyName("isTruncated")] bool IsTruncated,
    [property: JsonPropertyName("resultType")] string ResultType = "candidate",
    [property: JsonPropertyName("deletionClaim")] bool DeletionClaim = false);

/// <summary>
/// Konstante Standard-Limits fuer die Heuristik-Transparenz.
/// </summary>
internal static class DeadCodeLimits
{
    public static readonly IReadOnlyList<string> DefaultLimits =
    [
        "publicApiSurface: Public/Protected Symbole koennen von externen Consumern genutzt werden",
        "reflection: Dynamische Namen und nicht erkannte Reflection-Aufrufe sind statisch unsichtbar",
        "markup: Laufzeit-Markup und nicht aufgeloeste Bindungen sind nicht vollstaendig sichtbar",
        "dynamic: Aufrufe ueber dynamic sind statisch nicht aufloesbar",
        "externalConsumer: Externe Consumer sind nicht Teil der Solution",
        "internalsVisibleTo: Internal-Symbole koennen in befreundeten Assemblies referenziert sein",
        "projectRoles: Unbekannte Rollen unterdruecken Kandidaten"
    ];
}

internal sealed class DeadCodeScanContext(
    Microsoft.CodeAnalysis.Solution solution,
    string solutionDir,
    DeadCodeAdvisoryOptions args,
    int documentsInScope)
{
    public Microsoft.CodeAnalysis.Solution Solution { get; } = solution;
    public string SolutionDir { get; } = solutionDir;
    public DeadCodeAdvisoryOptions Args { get; set; } = args;
    public int DocumentsInScope { get; set; } = documentsInScope;
    public List<DeadCodeEntry> DeadSymbols { get; } = [];
    public Dictionary<string, int> ByKind { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<Microsoft.CodeAnalysis.INamedTypeSymbol> DeadContainerTypes { get; } = new(Microsoft.CodeAnalysis.SymbolEqualityComparer.Default);
    public HashSet<Microsoft.CodeAnalysis.INamedTypeSymbol> ScannedTypes { get; } = new(Microsoft.CodeAnalysis.SymbolEqualityComparer.Default);
    internal IReadOnlyList<Microsoft.CodeAnalysis.INamedTypeSymbol> EntryPointAttributeTypes { get; set; } = Array.Empty<Microsoft.CodeAnalysis.INamedTypeSymbol>();
    public DeadCodeUsageIndex UsageIndex { get; set; } = new();
    public HashSet<Microsoft.CodeAnalysis.ISymbol> ScannedMembers { get; } = new(Microsoft.CodeAnalysis.SymbolEqualityComparer.Default);
    public int ScannedCount { get; set; }
    public int ApiProtectedCount { get; set; }
    public DeadCodeScanProgress Progress { get; } = new(args.Clock);
}

internal sealed record DeadCodeScanCoverage(string RequestedScope, int ProcessedDocuments, int OpenDocuments,
    long ElapsedMilliseconds, string StopReason, bool ReferencesComplete,
    string ExcludedKinds = "fields,constants,properties,record_components,events,indexers,enum_values,locals,constructors,accessors,operators,finalizers,generated_declarations",
    string ChangesBasis = "not_applicable");

internal sealed class DeadCodeScanProgress(TimeProvider? clock = null)
{
    private readonly TimeProvider timer = clock ?? TimeProvider.System;
    private readonly long startedAt = (clock ?? TimeProvider.System).GetTimestamp();
    public int ProcessedDocuments { get; set; }
    public bool BudgetExpired { get; set; }
    public bool ReferencesComplete { get; set; }
    public long ElapsedMilliseconds => (long)timer.GetElapsedTime(startedAt).TotalMilliseconds;
    public long BudgetMilliseconds { get; set; }
    public string ChangesBasis { get; set; } = "not_applicable";
    public HashSet<string> PriorTargets { get; } = new(StringComparer.Ordinal);
}
