#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Output;
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
internal static partial class FindMagicValuesScanner
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
        // changedOnly: git diff im Solution-Root aufrufen, die geaenderten Dateien als Set
        // materialisieren. Forward-Slash normalisiert, weil ParseGitDiffHunks relative Pfade
        // in OS-spezifischer Form liefert (Windows: '\\'-Separator).
        HashSet<string>? changedFiles = null;
        if (p.ChangedOnly)
        {
            changedFiles = await ResolveChangedFilesAsync(p.Solution, p.CancellationToken);
        }

        var matchingDocuments = SelectDocuments(p.Solution, p.ScopeFilter, p.IncludeTests, changedFiles);

        if (matchingDocuments.Count == 0
            && (!string.IsNullOrWhiteSpace(p.ScopeFilter) || p.ChangedOnly || !p.IncludeTests))
        {
            return new FindMagicValuesResult(
                Text: BuildEmptyScopeText(p.ScopeFilter, p.ChangedOnly, p.IncludeTests),
                Payload: null,
                IsMalfunction: false,
                IsTruncated: false,
                Context: null);
        }

        var ignoreNumbers = p.IgnoreNumbers is null
            ? (IReadOnlySet<int>)new HashSet<int>()
            : new HashSet<int>(p.IgnoreNumbers);

        var (raw, malfunctionContext) = await WalkDocumentsAsync(matchingDocuments, p, ignoreNumbers, changedFiles);

        // Solution-weite Aggregation duplizierter const-Felder NACH dem Per-Document-Walk.
        // Bewusst getrennt von ProcessLiteral, weil FieldDeclarationSyntax-Aggregation nicht
        // in die Per-Literal-Pipeline passt und auf AST-Ebene laeuft (nicht ueber den
        // Per-Literal-SyntaxWalker, der pro Literal klassifiziert).
        if (raw.Count > 0 || malfunctionContext is null)
        {
            await DetectDuplicateConstFieldsAsync(raw, matchingDocuments, p.CancellationToken);
        }

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

    /// <summary>Loest die geaenderten Dateien via <see cref="DiffImpactAnalyzer.RunGitDiff"/>
    /// + <see cref="DiffImpactAnalyzer.ParseGitDiffHunks"/> auf. Liefert ein leeres Set,
    /// wenn das Verzeichnis kein Git-Repo ist oder keine Diffs vorhanden sind — der Aufrufer
    /// behandelt beides als "keine Dateien im Scope".</summary>
    private static async Task<HashSet<string>> ResolveChangedFilesAsync(
        Solution solution, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? string.Empty;
            if (solutionDir.Length == 0) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var repoRoot = GitRepositoryLocator.FindRoot(solutionDir);
            if (repoRoot is null) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var diffOutput = DiffImpactAnalyzer.RunGitDiff(repoRoot, gitSinceRef: null);
            if (string.IsNullOrEmpty(diffOutput)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var relative = Path.GetRelativePath(repoRoot, solutionDir).Replace('\\', '/');
            var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in DiffImpactAnalyzer.ParseGitDiffHunks(diffOutput).Keys)
            {
                // ParseGitDiffHunks liefert repo-relative Pfade; das ChangedFiles-Set braucht
                // solution-relative Pfade, weil SelectDocuments mit dem relativen Pfad zur
                // solutionDir filtert. Praefix-Strip + Separator-Normalisierung.
                var normalized = key.Replace('\\', '/');
                if (relative.Length > 0 && !relative.Equals(".", StringComparison.Ordinal)
                    && normalized.StartsWith(relative + "/", StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized.Substring(relative.Length + 1);
                }
                changed.Add(normalized);
            }
            return changed;
        }, ct);
    }

    private static string BuildEmptyScopeText(string? scopeFilter, bool changedOnly, bool includeTests)
    {
        var reasons = new List<string>();
        if (!string.IsNullOrWhiteSpace(scopeFilter)) reasons.Add($"Scope-Filter '{scopeFilter}'");
        if (changedOnly) reasons.Add("changedOnly aktiv (kein Git-Diff oder keine geaenderten Dateien)");
        if (!includeTests) reasons.Add("Test-Pfade ausgefiltert");
        var reasonText = reasons.Count == 0 ? "keine passenden Dateien" : string.Join(" + ", reasons);
        return $"Keine Dateien im Scope ({reasonText}) — Filter pruefen.";
    }

    /// <summary>Iteriert ueber alle matchenden Documents und sammelt Roh-Funde plus
    /// ggf. Malfunction-Kontext. Extrahiert aus <see cref="ScanAsync"/>, um dessen
    /// Code-Zeilen unter dem <c>MaxMethodLineCount: 60</c>-Limit zu halten.</summary>
    private static async Task<(List<RawMagicValue> Raw, string? MalfunctionContext)> WalkDocumentsAsync(
        IReadOnlyList<(Document Document, string FilePath)> matchingDocuments,
        FindMagicValuesScannerParameters p,
        IReadOnlySet<int> ignoreNumbers,
        IReadOnlySet<string>? changedFiles)
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
                var isTestPath = LooksLikeTestPath(filePath);
                var walker = new MagicValueSyntaxWalker(new MagicValueWalkerContext(
                    FilePath: filePath,
                    Model: model,
                    ValueTypeFilter: p.ValueType,
                    CategoryFilter: p.Category,
                    IgnoreNumbers: ignoreNumbers,
                    IncludeSuppressed: p.IncludeSuppressed,
                    ChangedFiles: changedFiles,
                    IsTestPath: isTestPath,
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

    private static IReadOnlyList<(Document Document, string FilePath)> SelectDocuments(
        Solution solution,
        string? scopeFilter,
        bool includeTests,
        IReadOnlySet<string>? changedFiles)
    {
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? string.Empty;
        var result = new List<(Document, string)>();
        foreach (var project in solution.Projects)
        {
            if (!includeTests && TestDetector.IsTestProject(project))
            {
                continue;
            }

            foreach (var document in project.Documents)
            {
                if (TrySelectDocument(document, solutionDir, scopeFilter, includeTests, changedFiles, out var entry))
                {
                    result.Add(entry);
                }
            }
        }

        return result;
    }

    /// <summary>Prueft ein einzelnes Document gegen die Filter (Source-Code-Kind, Endung,
    /// Generated-Path, Scope-Substring, includeTests, changedOnly) und liefert bei Pass den
    /// relativen Pfad zurueck.</summary>
    private static bool TrySelectDocument(
        Document document,
        string solutionDir,
        string? scopeFilter,
        bool includeTests,
        IReadOnlySet<string>? changedFiles,
        out (Document Document, string FilePath) entry)
    {
        entry = default;
        if (document.SourceCodeKind != SourceCodeKind.Regular) return false;
        if (document.FilePath is null) return false;
        if (!document.FilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return false;
        if (FileSystemExclusionHelpers.IsGeneratedPath(document.FilePath)) return false;

        var relativePath = solutionDir.Length == 0
            ? document.FilePath
            : Path.GetRelativePath(solutionDir, document.FilePath).Replace('\\', '/');

        if (!string.IsNullOrWhiteSpace(scopeFilter)
            && relativePath.IndexOf(scopeFilter, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        // includeTests=false: Testdateien und Testpfade ueberspringen.
        if (!includeTests && LooksLikeTestPath(relativePath))
        {
            return false;
        }

        // changedOnly: Datei MUSS in den geaenderten Dateien sein (Forward-Slash-normalisiert,
        // weil ResolveChangedFilesAsync die Keys bereits normalisiert). Bei leerem Set
        // (kein Git-Repo / keine Diffs) liefert der Filter 0 Dateien.
        if (changedFiles is not null && !changedFiles.Contains(relativePath))
        {
            return false;
        }

        entry = (document, relativePath);
        return true;
    }

    /// <summary>Erkennt Test-Pfade und Test-Dateien (delegiert an <see cref="TestDetector.IsTestFile"/>).</summary>
    private static bool LooksLikeTestPath(string path)
    {
        return TestDetector.IsTestFile(path);
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
