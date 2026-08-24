#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Mcp.Tools.MetricsLookup;

/// <summary>
/// Status-Konstanten für Schwellwert-Prüfungen.
/// </summary>
public static class ThresholdStatus
{
    public const string Ok = "OK";
    public const string Warn = "WARN";
    public const string Violation = "VIOLATION";
}

/// <summary>
/// Ein einzelner Schwellwert-Vergleich für eine Metrik.
/// </summary>
public sealed record ThresholdCheckDto(
    string Metric,
    int Value,
    int Limit,
    string Status,
    string RuleId
);

/// <summary>
/// Quellcode-Fundstelle eines Symbols.
/// </summary>
public sealed record SymbolLocationDto(
    string FilePath,
    int StartLine,
    int EndLine
);

/// <summary>
/// Eine Haupt-Abhängigkeit im AI-Context-Footprint.
/// </summary>
public sealed record TopDependencyDto(
    string Name,
    int Lines
);

/// <summary>
/// Spezifische Metriken für Methoden, Konstruktoren und Operatoren.
/// </summary>
public sealed record MethodMetricsDto(
    int CodeLines,
    int CyclomaticComplexity,
    int CognitiveComplexity,
    int TotalParameters,
    int EffectiveParameters,
    IReadOnlyList<string> IgnoredParameters
);

/// <summary>
/// Spezifische Metriken für Typen (Klassen, Records, Structs, Interfaces, Enums).
/// </summary>
public sealed record TypeMetricsDto(
    int CodeLines,
    int AiContextFootprint,
    int PublicMemberCount,
    int TotalMemberCount,
    int MethodCount,
    int PropertyCount,
    IReadOnlyList<TopDependencyDto> TopDependencies
);

/// <summary>
/// Spezifische Metriken für Properties und Indexer.
/// </summary>
public sealed record PropertyMetricsDto(
    int CodeLines,
    int CyclomaticComplexity,
    int CognitiveComplexity,
    bool HasGetter,
    bool HasSetter
);

/// <summary>
/// Vollständiges Ergebnis eines Metrics-Lookups für ein beliebiges C#-Symbol.
/// Wird als StructuredContent (JSON-Objekt) serialisiert.
/// </summary>
public sealed record MetricsLookupResultDto(
    string SymbolName,
    string SymbolKind,
    string QualifiedName,
    string? DocCommentId,
    SymbolLocationDto? Location,
    MethodMetricsDto? MethodMetrics,
    TypeMetricsDto? TypeMetrics,
    PropertyMetricsDto? PropertyMetrics,
    IReadOnlyList<ThresholdCheckDto> ThresholdChecks
);

/// <summary>
/// StructuredContent-Hülle für <c>metrics_lookup</c> (<c>symbolIdentifiers</c>). Das MCP-Protokoll
/// verlangt <c>structuredContent</c> als JSON-Objekt — ein Top-Level-Array lässt reale Clients
/// den kompletten Tool-Call schema-seitig ablehnen (siehe Doc-Kommentar
/// <c>McpToolResults.Text``1</c>). Liefert immer <see cref="MetricsLookupBatchDto"/>, auch bei genau einem angefragten Identifier.
/// </summary>
public sealed record MetricsLookupBatchDto(
    IReadOnlyList<MetricsLookupResultDto> Results,
    int RequestedCount
);
