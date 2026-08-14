#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools.MagicValues;

/// <summary>
/// Scanner fuer das On-Demand-Audit-Tool <c>find_magic_values</c>: iteriert ueber alle
/// <c>.cs</c>-Dokumente der Solution, klassifiziert jedes Literal via
/// <see cref="MagicValuesClassifier"/>, aggregiert identische Funde (gleiches
/// <c>(category, value, filePath)</c>-Tupel) und kuerzt das Ergebnis via
/// <see cref="McpTruncation.TruncateLines"/>. Reine Daten-Schicht ohne
/// <c>McpCodeGraphServer</c>-Abhaengigkeit, direkt unit-testbar (Pattern 1:1 von
/// <see cref="Mcp.Tools.Analysis.GetViolationsScanner"/>).
/// </summary>
internal static class FindMagicValuesScanner
{
    /// <summary>
    /// Default-Obergrenze fuer die Anzahl gezeigter Magic-Value-Funde in Text-Report
    /// und <c>StructuredContent</c> — analog <see cref="Mcp.Tools.Analysis.GetViolationsScanner.DefaultMaxResults"/>
    /// und <see cref="Mcp.Tools.Analysis.SearchPatternScanner.DefaultMaxResults"/>. Schuetzt
    /// das Agent-Token-Budget.
    /// </summary>
    internal const int DefaultMaxResults = 50;

    internal static async Task<FindMagicValuesResult> ScanAsync(FindMagicValuesScannerParameters p)
    {
        var matchingDocuments = SelectDocuments(p.Solution, p.ScopeFilter);

        if (matchingDocuments.Count == 0 && !string.IsNullOrWhiteSpace(p.ScopeFilter))
        {
            return new FindMagicValuesResult(
                Text: $"Keine Dateien im Scope (Filter: '{p.ScopeFilter}') — Filter pruefen.",
                Payload: null,
                IsMalfunction: false,
                IsTruncated: false,
                Context: null);
        }

        var ignoreNumbers = p.IgnoreNumbers is null
            ? (IReadOnlySet<int>)new HashSet<int>()
            : new HashSet<int>(p.IgnoreNumbers);

        var (raw, malfunctionContext) = await WalkDocumentsAsync(matchingDocuments, p, ignoreNumbers);

        // Wenn kein einziges Dokument erfolgreich war UND wir einen Fehler gesehen haben, ist
        // das eine echte Malfunction (Pattern 1:1 von GetViolationsScanner — derselbe
        // 'LinterEngine hat global geworfen'-Fall, nur hier per Document). Bei Teilerfolg
        // liefern wir die aggregierten Funde ohne Malfunction-Flag.
        if (raw.Count == 0 && malfunctionContext is not null)
        {
            return new FindMagicValuesResult(
                Text: "Unerwarteter Fehler beim Magic-Value-Scan.",
                Payload: null,
                IsMalfunction: true,
                IsTruncated: false,
                Context: malfunctionContext);
        }

        return BuildResult(raw, p, matchingDocuments.Count);
    }

    /// <summary>Iteriert ueber alle matchenden Documents und sammelt Roh-Funde plus
    /// ggf. Malfunction-Kontext. Extrahiert aus <see cref="ScanAsync"/>, um dessen
    /// Code-Zeilen unter dem <c>MaxMethodLineCount: 60</c>-Limit zu halten.</summary>
    private static async Task<(List<RawMagicValue> Raw, string? MalfunctionContext)> WalkDocumentsAsync(
        IReadOnlyList<(Document Document, string FilePath)> matchingDocuments,
        FindMagicValuesScannerParameters p,
        IReadOnlySet<int> ignoreNumbers)
    {
        var raw = new List<RawMagicValue>();
        string? malfunctionContext = null;
        var ct = p.CancellationToken;
        foreach (var (document, filePath) in matchingDocuments)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var tree = await document.GetSyntaxTreeAsync(ct).ConfigureAwait(false);
                if (tree is null) continue;
                var root = await tree.GetRootAsync(ct).ConfigureAwait(false);
                var model = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
                var walker = new MagicValueSyntaxWalker(new MagicValueWalkerContext(
                    FilePath: filePath,
                    Model: model,
                    ValueTypeFilter: p.ValueType,
                    CategoryFilter: p.Category,
                    IgnoreNumbers: ignoreNumbers,
                    IncludeSuppressed: p.IncludeSuppressed,
                    Sink: raw));
                walker.Visit(root);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Per-Datei-Fehler werden aggregiert; die erste Fehlermeldung dient als
                // Malfunction-Kontext, wenn KEIN Dokument erfolgreich gescannt werden konnte.
                malfunctionContext ??= ex.Message;
            }
        }
        return (raw, malfunctionContext);
    }

    /// <summary>Aggregiert Roh-Funde, baut Text-Report + StructuredContent-Payload und
    /// liefert den finalen <see cref="FindMagicValuesResult"/>. Aus <see cref="ScanAsync"/>
    /// extrahiert, um dessen Code-Zeilen unter dem 60-Limit zu halten.</summary>
    private static FindMagicValuesResult BuildResult(
        List<RawMagicValue> raw,
        FindMagicValuesScannerParameters p,
        int matchingFileCount)
    {
        var grouped = AggregateAndFilter(raw, p.MinOccurrences, p.Category);
        var report = FormatReport(grouped, matchingFileCount, p.ScopeFilter, p.MaxResults);
        var payload = BuildPayload(grouped, p.MaxResults);

        return new FindMagicValuesResult(
            Text: report,
            Payload: payload,
            IsMalfunction: false,
            IsTruncated: payload is not null && payload.Summary.ShownOccurrences < payload.Summary.Total,
            Context: null);
    }

    private static IReadOnlyList<(Document Document, string FilePath)> SelectDocuments(Solution solution, string? scopeFilter)
    {
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? string.Empty;
        var result = new List<(Document, string)>();
        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (TrySelectDocument(document, solutionDir, scopeFilter, out var entry))
                {
                    result.Add(entry);
                }
            }
        }

        return result;
    }

    /// <summary>Prueft ein einzelnes Document gegen die Filter (Source-Code-Kind, Endung,
    /// Generated-Path, Scope-Substring) und liefert bei Pass den relativen Pfad zurueck.
    /// Extrahiert aus <see cref="SelectDocuments"/>, um dessen kognitive Komplexitaet
    /// unter dem <c>MaxCognitiveComplexity: 15</c>-Limit zu halten.</summary>
    private static bool TrySelectDocument(
        Document document,
        string solutionDir,
        string? scopeFilter,
        out (Document Document, string FilePath) entry)
    {
        entry = default;
        if (document.SourceCodeKind != SourceCodeKind.Regular) return false;
        if (document.FilePath is null) return false;
        if (!document.FilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return false;
        if (IsGeneratedPath(document.FilePath)) return false;

        var relativePath = solutionDir.Length == 0
            ? document.FilePath
            : Path.GetRelativePath(solutionDir, document.FilePath).Replace('\\', '/');

        if (!string.IsNullOrWhiteSpace(scopeFilter)
            && relativePath.IndexOf(scopeFilter, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        entry = (document, relativePath);
        return true;
    }

    private static bool IsGeneratedPath(string path)
    {
        var sep = Path.DirectorySeparatorChar;
        return path.Contains($"{sep}obj{sep}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{sep}bin{sep}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{sep}.ainetlinter{sep}", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<GroupedMagicValue> AggregateAndFilter(
        List<RawMagicValue> raw,
        int minOccurrences,
        MagicValueCategory? categoryFilter)
    {
        var filtered = raw
            .Where(r => r.Classification.IsMagic)
            .Where(r => categoryFilter is null || r.Classification.Category == categoryFilter.Value);

        var grouped = filtered
            .GroupBy(r => (r.Classification.Category, r.Value, r.FilePath))
            .Select(g => new GroupedMagicValue(
                Category: g.Key.Category,
                Value: g.Key.Value,
                ValueType: g.First().ValueType,
                FilePath: g.Key.FilePath,
                Recommendation: g.First().Classification.Recommendation,
                ContextHint: g.First().Classification.ContextHint,
                Occurrences: g.Count(),
                FirstLine: g.Min(x => x.Line),
                FirstColumn: g.Min(x => x.Column)))
            .Where(g => g.Occurrences >= minOccurrences)
            .OrderBy(g => g.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.FirstLine)
            .ThenBy(g => g.Category)
            .ToList();

        return grouped;
    }

    private static string FormatReport(
        IReadOnlyList<GroupedMagicValue> grouped,
        int matchingFileCount,
        string? scopeFilter,
        int maxResults)
    {
        var scopeSuffix = string.IsNullOrWhiteSpace(scopeFilter) ? "" : $" | Scope-Filter: '{scopeFilter}'";
        var totalOccurrences = grouped.Sum(g => g.Occurrences);

        var sb = new StringBuilder();
        sb.AppendLine(
            $"Magic-Value-Audit: {totalOccurrences} Treffer in {grouped.Count} eindeutigen Einträgen " +
            $"über {matchingFileCount} Dateien im Scope{scopeSuffix}");
        sb.AppendLine();

        if (grouped.Count == 0)
        {
            sb.AppendLine("Keine Magic Values.");
            return sb.ToString().TrimEnd();
        }

        var lines = grouped
            .Select(g => $"{g.FilePath}:{g.FirstLine} - {g.Category.ToStringValue()}: " +
                         $"{(g.ValueType == MagicValueValueType.Number ? g.Value : $"\"{g.Value}\"")} " +
                         $"({g.Occurrences}x, Empfehlung: {g.Recommendation})")
            .ToList();

        sb.AppendLine(McpTruncation.TruncateLines(lines, grouped.Count, maxResults));
        return sb.ToString().TrimEnd();
    }

    private static FindMagicValuesPayload BuildPayload(IReadOnlyList<GroupedMagicValue> grouped, int maxResults)
    {
        var shown = grouped.Take(maxResults).ToList();
        return new FindMagicValuesPayload(
            MagicValues: shown.Select(g => new MagicValueEntry(
                FilePath: g.FilePath,
                Line: g.FirstLine,
                Column: g.FirstColumn,
                ValueType: g.ValueType.ToStringValue(),
                Value: g.Value,
                Category: g.Category.ToStringValue(),
                Recommendation: g.Recommendation,
                ContextHint: g.ContextHint,
                Occurrences: g.Occurrences)).ToList(),
            Summary: new MagicValuesSummary(
                Total: grouped.Count,
                ShownOccurrences: shown.Count,
                ByCategoryConfig: grouped.Count(g => g.Category == MagicValueCategory.ConfigCandidates),
                ByCategoryConstant: grouped.Count(g => g.Category == MagicValueCategory.ConstantCandidates),
                ByCategoryStandard: grouped.Count(g => g.Category == MagicValueCategory.StandardCandidates)));
    }
}

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
/// Parameter-Bundel fuer <see cref="MagicValueSyntaxWalker"/>. Fasst die sieben Walker-Felder
/// (filePath/model/valueTypeFilter/categoryFilter/ignoreNumbers/includeSuppressed/sink) zu
/// einem <c>WalkerContext</c>-Record zusammen, damit <c>MaxConstructorDependencies: 5</c>
/// (siehe <c>AiNetLinter.mdc</c>) eingehalten wird.
/// </summary>
internal sealed record MagicValueWalkerContext(
    string FilePath,
    SemanticModel? Model,
    MagicValueValueType? ValueTypeFilter,
    MagicValueCategory? CategoryFilter,
    IReadOnlySet<int> IgnoreNumbers,
    bool IncludeSuppressed,
    List<RawMagicValue> Sink);

/// <summary>
/// Roslyn <see cref="CSharpSyntaxWalker"/>, der jedes <see cref="LiteralExpressionSyntax"/>
/// plus jedes statische <c>InterpolatedStringText</c>-Segment in <see cref="InterpolatedStringExpressionSyntax"/>
/// an <see cref="MagicValuesClassifier.Classify"/> uebergibt. Trivial-/Attribut-/Index-/Loop-
/// Filterung uebernimmt der Classifier; hier nur die Ast-Walk-Mechanik.
/// </summary>
internal sealed class MagicValueSyntaxWalker : CSharpSyntaxWalker
{
    private readonly MagicValueWalkerContext context;

    internal MagicValueSyntaxWalker(MagicValueWalkerContext context)
    {
        this.context = context;
    }

    public override void VisitLiteralExpression(LiteralExpressionSyntax node)
    {
        ProcessLiteral(node);
        base.VisitLiteralExpression(node);
    }

    public override void VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
    {
        // Statische Text-Segmente in interpolated strings ($"...{x}..."). Dynamische
        // Segmente (Interpolationen) werden NICHT ausgewertet — das wuerde eine
        // Laufzeit-Aufloesung erfordern, die fuer ein On-Demand-Audit zu teuer und
        // semantisch fragwuerdig waere (Konzept §"Wie" Punkt 1). In EPIC-1 ist die
        // Verarbeitung ein No-op-Hook fuer Folge-Versionen, der strukturelle Aufbau ist
        // aber bereits da.
        _ = node;
    }

    private void ProcessLiteral(LiteralExpressionSyntax node)
    {
        if (!IsInScope(node.Kind())) return;

        var (valueType, value) = ExtractValue(node);
        if (value is null) return;

        var classification = MagicValuesClassifier.Classify(
            node, context.Model, context.IgnoreNumbers, new MagicValueClassifierOptions(
                IncludeTests: false,
                IncludeSuppressed: context.IncludeSuppressed));
        if (!classification.IsMagic) return;

        if (context.CategoryFilter is not null && classification.Category != context.CategoryFilter.Value) return;

        var lineSpan = node.GetLocation().GetLineSpan();
        var line = lineSpan.StartLinePosition.Line + 1;
        var column = lineSpan.StartLinePosition.Character + 1;

        context.Sink.Add(new RawMagicValue(context.FilePath, line, column, valueType, value, classification));
    }

    private bool IsInScope(SyntaxKind kind) => kind switch
    {
        SyntaxKind.StringLiteralExpression or SyntaxKind.Utf8StringLiteralExpression
            => context.ValueTypeFilter is null or MagicValueValueType.String,
        SyntaxKind.CharacterLiteralExpression => false, // EPIC-1: char-Literale nicht gemeldet
        SyntaxKind.NumericLiteralExpression
            => context.ValueTypeFilter is null or MagicValueValueType.Number,
        _ => false,
    };

    private static (MagicValueValueType, string?) ExtractValue(LiteralExpressionSyntax node)
    {
        switch (node.Kind())
        {
            case SyntaxKind.StringLiteralExpression:
            case SyntaxKind.Utf8StringLiteralExpression:
                return (MagicValueValueType.String, node.Token.ValueText);
            case SyntaxKind.NumericLiteralExpression:
            {
                if (node.Token.Value is null) return (MagicValueValueType.Number, null);
                // InvariantCulture: locale-unabhaengige Repraesentation (z. B. "0.19" statt
                // "0,19" auf de-DE), damit Tests und JSON-Output stabil sind.
                return (MagicValueValueType.Number, Convert.ToString(node.Token.Value, System.Globalization.CultureInfo.InvariantCulture));
            }
            default:
                return (MagicValueValueType.String, null);
        }
    }
}

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
