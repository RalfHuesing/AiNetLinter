#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AiNetLinter.Mcp.Tools.DeadCode;

/// <summary>
/// Filter fuer Deklarations-Sichtbarkeit bei find_dead_code.
/// </summary>
public enum DeadCodeAccessibilityFilter
{
    All,
    Private,
    Internal,
    Public,
    PrivateInternal
}

/// <summary>
/// Filter fuer Vertrauensstufe bei find_dead_code.
/// </summary>
public enum DeadCodeConfidenceFilter
{
    Both,
    High,
    Low
}

/// <summary>
/// Filter fuer Symbol-Art bei find_dead_code.
/// </summary>
public enum DeadCodeKindFilter
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
/// Modus fuer find_dead_code (Symbol-Graph, Compiler-Diagnosen oder beides).
/// </summary>
public enum DeadCodeMode
{
    Members,
    Locals,
    Both
}

/// <summary>
/// Ausfuehrungs-Argumente fuer find_dead_code.
/// </summary>
public sealed record FindDeadCodeArgs(
    DeadCodeAccessibilityFilter Accessibility = DeadCodeAccessibilityFilter.PrivateInternal,
    DeadCodeConfidenceFilter Confidence = DeadCodeConfidenceFilter.Both,
    DeadCodeKindFilter Kind = DeadCodeKindFilter.All,
    string? ScopeFilter = null,
    bool IncludeTests = false,
    DeadCodeMode Mode = DeadCodeMode.Members,
    int MaxResults = 50)
{
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
/// Einzelner toter Code-Fund im Structured Output von find_dead_code.
/// </summary>
public sealed record DeadCodeEntry(
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
    [property: JsonPropertyName("limitsApplies")] IReadOnlyList<string> LimitsApplies);

/// <summary>
/// Zusammenfassende Statistik ueber den Dead-Code-Scan.
/// </summary>
public sealed record DeadCodeSummary(
    [property: JsonPropertyName("scannedSymbols")] int ScannedSymbols,
    [property: JsonPropertyName("totalDead")] int TotalDead,
    [property: JsonPropertyName("high")] int High,
    [property: JsonPropertyName("low")] int Low,
    [property: JsonPropertyName("byKind")] IReadOnlyDictionary<string, int> ByKind);

/// <summary>
/// Empfohlene naechste Aktion fuer den aufrufenden Agenten (Trust-Modell).
/// </summary>
public sealed record DeadCodeRecommendedNextAction(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("reason")] string Reason);

/// <summary>
/// Gesamtergebnis des Dead-Code-Scanners.
/// </summary>
public sealed record DeadCodeScanResult(
    [property: JsonPropertyName("deadSymbols")] IReadOnlyList<DeadCodeEntry> DeadSymbols,
    [property: JsonPropertyName("summary")] DeadCodeSummary Summary,
    [property: JsonPropertyName("limits")] IReadOnlyList<string> Limits,
    [property: JsonPropertyName("recommendedNextAction")] DeadCodeRecommendedNextAction RecommendedNextAction,
    [property: JsonPropertyName("isTruncated")] bool IsTruncated);

/// <summary>
/// Konstante Standard-Limits fuer die Heuristik-Transparenz.
/// </summary>
public static class DeadCodeLimits
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
