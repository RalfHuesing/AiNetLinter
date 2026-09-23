#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AiNetLinter.Mcp.Tools.Verify.DeadCode;

/// <summary>
/// Filter fuer Deklarations-Sichtbarkeit bei Dead-Code-Hinweisprojektion.
/// </summary>
internal enum DeadCodeAccessibilityFilter
{
    All,
    Private,
    Internal,
    Public,
    PrivateInternal
}

/// <summary>
/// Filter fuer Vertrauensstufe bei Dead-Code-Hinweisprojektion.
/// </summary>
internal enum DeadCodeConfidenceFilter
{
    Both,
    High,
    Low
}

/// <summary>
/// Filter fuer Symbol-Art bei Dead-Code-Hinweisprojektion.
/// </summary>
internal enum DeadCodeKindFilter
{
    All,
    Type,
    Class,
    Method,
    Field,
    Property,
    Event,
    Delegate
}

/// <summary>
/// Modus fuer Dead-Code-Hinweisprojektion (Symbol-Graph, Compiler-Diagnosen oder beides).
/// </summary>
internal enum DeadCodeMode
{
    Members,
    Locals,
    Both
}

/// <summary>
/// Ausfuehrungs-Argumente fuer Dead-Code-Hinweisprojektion.
/// </summary>
internal sealed record DeadCodeAdvisoryOptions(
    DeadCodeAccessibilityFilter Accessibility = DeadCodeAccessibilityFilter.PrivateInternal,
    DeadCodeConfidenceFilter Confidence = DeadCodeConfidenceFilter.Both,
    DeadCodeKindFilter Kind = DeadCodeKindFilter.All,
    string? ScopeFilter = null,
    bool IncludeTests = false,
    DeadCodeMode Mode = DeadCodeMode.Members,
    int MaxResults = 50,
    IReadOnlySet<string>? ScopeFiles = null)
{
    public static bool IsKnownAccessibility(string? value) => value?.ToLowerInvariant() is "all" or "private" or "internal" or "public" or "private_internal";
    public static bool IsKnownConfidence(string? value) => value?.ToLowerInvariant() is "both" or "high" or "low";
    public static bool IsKnownKind(string? value) => value?.ToLowerInvariant() is "all" or "type" or "class" or "method" or "field" or "property" or "event" or "delegate";
    public static bool IsKnownMode(string? value) => value?.ToLowerInvariant() is "members" or "locals" or "both";

    public static DeadCodeAccessibilityFilter ParseAccessibility(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "all" => DeadCodeAccessibilityFilter.All,
            "private" => DeadCodeAccessibilityFilter.Private,
            "internal" => DeadCodeAccessibilityFilter.Internal,
            "public" => DeadCodeAccessibilityFilter.Public,
            "private_internal" => DeadCodeAccessibilityFilter.PrivateInternal,
            _ => DeadCodeAccessibilityFilter.PrivateInternal
        };

    public static DeadCodeConfidenceFilter ParseConfidence(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "both" => DeadCodeConfidenceFilter.Both,
            "high" => DeadCodeConfidenceFilter.High,
            "low" => DeadCodeConfidenceFilter.Low,
            _ => DeadCodeConfidenceFilter.Both
        };

    public static DeadCodeKindFilter ParseKind(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "all" => DeadCodeKindFilter.All,
            "type" => DeadCodeKindFilter.Type,
            "class" => DeadCodeKindFilter.Class,
            "method" => DeadCodeKindFilter.Method,
            "field" => DeadCodeKindFilter.Field,
            "property" => DeadCodeKindFilter.Property,
            "event" => DeadCodeKindFilter.Event,
            "delegate" => DeadCodeKindFilter.Delegate,
            _ => DeadCodeKindFilter.All
        };

    public static DeadCodeMode ParseMode(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "members" => DeadCodeMode.Members,
            "locals" => DeadCodeMode.Locals,
            "both" => DeadCodeMode.Both,
            _ => DeadCodeMode.Members
        };
}

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
    [property: JsonPropertyName("confidence")] string Confidence,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("limitsApplies")] IReadOnlyList<string> LimitsApplies,
    [property: JsonPropertyName("resultType")] string ResultType = "candidate",
    [property: JsonPropertyName("evidenceBoundary")] string EvidenceBoundary = "statische Referenzsuche innerhalb der Solution; keine Laufzeit- oder externen Consumer-Beweise",
    [property: JsonPropertyName("countercheck")] IReadOnlyList<string>? Countercheck = null,
    [property: JsonPropertyName("usage")] string Usage = "unreferenced",
    [property: JsonPropertyName("testReferences")] int TestReferences = 0);

/// <summary>
/// Zusammenfassende Statistik ueber den Dead-Code-Scan.
/// </summary>
internal sealed record DeadCodeSummary(
    [property: JsonPropertyName("documentsInScope")] int DocumentsInScope,
    [property: JsonPropertyName("scannedSymbols")] int ScannedSymbols,
    [property: JsonPropertyName("totalDead")] int TotalDead,
    [property: JsonPropertyName("high")] int High,
    [property: JsonPropertyName("low")] int Low,
    [property: JsonPropertyName("byKind")] IReadOnlyDictionary<string, int> ByKind,
    [property: JsonPropertyName("status")] string Status = "checked",
    [property: JsonPropertyName("cause")] string Cause = "Statischer Scan im angeforderten Scope.",
    [property: JsonPropertyName("confidence")] string Confidence = "medium",
    [property: JsonPropertyName("returnedCandidates")] int ReturnedCandidates = 0,
    [property: JsonPropertyName("truncatedBy")] int TruncatedBy = 0,
    [property: JsonPropertyName("next")] DeadCodeRecommendedNextAction? Next = null,
    [property: JsonPropertyName("undecidable")] int Undecidable = 0);

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
        "reflection: Dynamische Aufrufe per Reflection (Type.GetMethod o.ae.) sind statisch unsichtbar",
        "interfaceImplementation: Aufrufe koennen indirekt ueber Interface-Typen erfolgen",
        "jsonSerializer: DTO-Properties werden per JSON/XML-Serializer oder Model-Binding instanziiert",
        "optionsBinding: Configuration-POCOs werden per IOptions<T> gebunden",
        "aspNetRouting: Endpunkte und Controller werden per HTTP-Routing aufgerufen",
        "internalsVisibleTo: Internal-Symbole koennen in befreundeten Assemblies referenziert sein",
        "di: Dependency-Injection Container loesen Konstruktoren und Typen dynamisch auf"
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
    public DeadCodeAdvisoryOptions Args { get; } = args;
    public int DocumentsInScope { get; } = documentsInScope;
    public List<DeadCodeEntry> DeadSymbols { get; } = [];
    public Dictionary<string, int> ByKind { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<Microsoft.CodeAnalysis.INamedTypeSymbol> DeadContainerTypes { get; } = new(Microsoft.CodeAnalysis.SymbolEqualityComparer.Default);
    public HashSet<Microsoft.CodeAnalysis.INamedTypeSymbol> ScannedTypes { get; } = new(Microsoft.CodeAnalysis.SymbolEqualityComparer.Default);
    public RazorGeneratedEvidenceIndex RazorEvidenceIndex { get; set; } = RazorGeneratedEvidenceIndex.Empty;
    public int ScannedCount { get; set; }
    public int UndecidableCount { get; set; }
}
