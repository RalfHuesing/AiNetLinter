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
/// Linter-Regel <c>MaxMethodParameterCount</c>) eingehalten wird (Pattern 1:1 von
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
    IReadOnlyList<MagicValueCategorySummary> Categories,
    MagicValuesSummary Summary,
    string ResultType = "candidate");

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
    string? Recommendation,
    string ContextHint,
    int Occurrences,
    string EvidenceBoundary = "",
    string Scope = "",
    string ResultType = "candidate");

/// <summary>Normativer Status- und Evidenzblock je kanonischer Magic-Value-Kategorie.</summary>
internal sealed record MagicValueCategorySummary(
    string Category,
    int Total,
    int ReturnedCount,
    string Status,
    string Cause,
    string Confidence,
    MagicValueNextAction? Next,
    int TruncatedBy,
    string EvidenceBoundary,
    string Scope,
    string? Recommendation,
    string ResultType = "candidate");

/// <summary>Explizite, maschinenlesbare Folgeaktion fuer leere, begrenzte oder gepruefte
/// Kategorien.</summary>
internal sealed record MagicValueNextAction(string Action, string Reason);

/// <summary>Aggregat-Stats: <see cref="Total"/> zaehlt die Eintrags-Anzahl (ungekappt),
/// <see cref="ShownOccurrences"/> die im Content sichtbaren (nach Trunkierung).
/// Die ByCategory-Felder zaehlen jeweils auf der ungekappten Eintragsliste.</summary>
internal sealed record MagicValuesSummary(
    int Total,
    int ShownOccurrences,
    int ByCategoryConfig,
    int ByCategoryConstant,
    int ByCategoryStandard,
    int ByCategoryEnum = 0,
    int ByCategoryNameof = 0,
    int ByCategoryLocalization = 0,
    int ByCategorySecurity = 0,
    int ReturnedCount = 0,
    int TotalOccurrences = 0,
    int ReturnedOccurrences = 0,
    int FilesInScope = 0,
    string Status = "checked",
    string Cause = "",
    string Confidence = "high",
    MagicValueNextAction? Next = null,
    int TruncatedBy = 0,
    string EvidenceBoundary = "",
    string Scope = "",
    string? Recommendation = null,
    string ResultType = "candidate");

/// <summary>
/// String-Repraesentation fuer <see cref="MagicValueValueType"/> (Tool-Argumente und
/// Content).
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
