#nullable enable

using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.MagicValues;

internal enum MagicValueValueType
{
    String,
    Number,
}

internal sealed record RawMagicValue(
    string FilePath,
    int Line,
    int Column,
    MagicValueValueType ValueType,
    string Value,
    MagicValueClassification Classification);

internal sealed record GroupedMagicValue(
    MagicValueCategory Category,
    string Value,
    MagicValueValueType ValueType,
    string FilePath,
    string Recommendation,
    string ContextHint,
    int Occurrences,
    int FirstLine,
    int FirstColumn);

/// <summary>
/// Parameter-Record fuer <see cref="FindMagicValuesScanner.ScanAsync"/>. Kapselt 9
/// Konfigurations-Eingaenge in einem Record, damit <c>MaxMethodParameterCount: 4</c> (siehe
/// <c>AiNetLinter.mdc</c>) eingehalten wird (Pattern 1:1 von
/// <c>GetViolationsScannerParameters</c>). <see cref="ValueType"/> ist nullable: <see langword="null"/>
/// = "all" (Strings UND Numbers akzeptieren).
/// </summary>
internal sealed record FindMagicValuesScannerParameters(
    Solution Solution,
    string? ScopeFilter,
    MagicValueValueType? ValueType,
    MagicValueCategory? Category,
    int MinOccurrences,
    int MaxResults,
    int[]? IgnoreNumbers,
    bool IncludeTests,
    bool IncludeSuppressed,
    bool ChangedOnly,
    CancellationToken CancellationToken);

/// <summary>
/// Ergebnis-Record fuer <see cref="FindMagicValuesScanner.ScanAsync"/>. <see cref="IsMalfunction"/>
/// unterscheidet eine echte Malfunction (unerwartete Roslyn-/Laufzeit-Exception im defensiven
/// try/catch — <see cref="Context"/> non-null, <see cref="Payload"/> null) von einem normalen
/// Report (auch "Keine Dateien im Scope" oder 0 Treffer zaehlen als normal).
/// </summary>
internal sealed record FindMagicValuesResult(
    string Text,
    FindMagicValuesPayload? Payload,
    bool IsMalfunction,
    bool IsTruncated = false,
    string? Context = null);

/// <summary>Structured-Content-Wurzel fuer <c>find_magic_values</c>: gefundene Magic-Value-
/// Eintraege plus Aggregat-Summary.</summary>
internal sealed record FindMagicValuesPayload(
    IReadOnlyList<MagicValueEntry> MagicValues,
    MagicValuesSummary Summary);

/// <summary>Ein aggregierter Magic-Value-Fund: <see cref="Occurrences"/> zaehlt identische
/// Literale in derselben Datei, <see cref="Value"/> ist die normalisierte String-Repraesentation
/// (bei Zahlen via <see cref="object.ToString"/>).</summary>
internal sealed record MagicValueEntry(
    string FilePath,
    int Line,
    int Column,
    string ValueType,
    string Value,
    string Category,
    string Recommendation,
    string ContextHint,
    int Occurrences);

/// <summary>Aggregat-Stats: <see cref="Total"/> zaehlt die Eintrags-Anzahl (ungekappt),
/// <see cref="ShownOccurrences"/> die im StructuredContent sichtbaren (nach Trunkierung).
/// Die ByCategory-Felder zaehlen jeweils auf der ungekappten Eintragsliste.</summary>
internal sealed record MagicValuesSummary(
    int Total,
    int ShownOccurrences,
    int ByCategoryConfig,
    int ByCategoryConstant,
    int ByCategoryStandard);

/// <summary>
/// String-Repraesentation fuer <see cref="MagicValueValueType"/> (Tool-Argumente und
/// <c>StructuredContent</c>).
/// </summary>
internal static class MagicValueValueTypeExtensions
{
    internal static string ToStringValue(this MagicValueValueType t) => t switch
    {
        MagicValueValueType.String => "string",
        MagicValueValueType.Number => "number",
        _ => t.ToString().ToLowerInvariant(),
    };
}
