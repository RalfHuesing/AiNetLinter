#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AiNetLinter.Core;
using AiNetLinter.Models;
using AiNetLinter.Mcp.Tools.TestContext;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

/// <summary>
/// Vertragskonstanten und Cap-Normalisierung des <c>get_impact</c>-Zweigs
/// <c>detailLevel=change-context</c>: die Werte von <see cref="ModeGitDiff"/>,
/// <see cref="DetailLevelCallers"/> und <see cref="DetailLevelChangeContext"/> sowie die
/// Default-/Cap-Werte sind Teil des oeffentlichen Tool-Vertrags.
/// </summary>
internal static class ChangeContextContract
{
    internal const string ModeGitDiff = "gitDiff";
    internal const string DetailLevelCallers = "callers";
    internal const string DetailLevelChangeContext = "change-context";

    internal const int DefaultMaxChangedSymbols = 20;
    internal const int MaxChangedSymbolsLimit = 100;
    internal const int DefaultMaxTestsPerSymbol = 10;
    internal const int MaxTestsPerSymbolLimit = 50;

    /// <summary>Werte &lt; 1 laufen auf den jeweiligen Default, Werte ueber dem Cap auf den Cap zurueck.</summary>
    internal static (int MaxChangedSymbols, int MaxTestsPerSymbol) NormalizeCaps(
        int maxChangedSymbols, int maxTestsPerSymbol) =>
        (Clamp(maxChangedSymbols, DefaultMaxChangedSymbols, MaxChangedSymbolsLimit),
         Clamp(maxTestsPerSymbol, DefaultMaxTestsPerSymbol, MaxTestsPerSymbolLimit));

    private static int Clamp(int value, int defaultValue, int limit) =>
        value < 1 ? defaultValue : Math.Min(value, limit);
}

/// <summary>
/// Strukturierte Antwort des <c>get_impact</c>-Zweigs <c>detailLevel=change-context</c>. Die
/// JSON-Feldnamen entstehen per zentraler CamelCase-Policy 1:1 aus den Property-Namen und sind
/// vertraglich exakt — keine Umbenennung ohne Vertragsanpassung. Immer als Objekt serialisieren
/// (<see cref="McpToolResults.Text{T}"/> verbietet Top-Level-Arrays).
/// </summary>
internal sealed record ChangeContextPayload(
    string Mode,
    string DetailLevel,
    IReadOnlyList<ChangedFilePayload> ChangedFiles,
    IReadOnlyList<ChangedSymbolPayload> ChangedSymbols,
    IReadOnlyList<TransitiveCallSiteEntry> CallSites,
    IReadOnlyList<TestAssociationPayload> TestAssociations,
    IReadOnlyList<ViolationPayload> Violations,
    IReadOnlyList<string> RecommendedTestCommands,
    CompletenessPayload Completeness);

/// <summary>Eine geaenderte Datei mit ihren kompakten Hunk-Ranges (repo-root-relativer Pfad).</summary>
internal sealed record ChangedFilePayload(string FilePath, IReadOnlyList<HunkRangePayload> Ranges);

internal sealed record HunkRangePayload(int StartLine, int LineCount);

/// <summary>
/// Ein geaendertes Symbol; <see cref="DocumentationCommentId"/> traegt die stabile ID
/// (DocCommentId oder deterministischer Fallback). <see cref="Accessibility"/> ist bewusst ein
/// STRING (z. B. "Public") — die zentrale JSON-Policy hat keinen Enum-Converter, eine Zahl wuerde
/// den Vertrag verletzen.
/// </summary>
internal sealed record ChangedSymbolPayload(
    string DocumentationCommentId,
    string DisplayName,
    string Kind,
    string Accessibility,
    string ProjectName,
    string FilePath,
    int StartLine,
    int EndLine);

/// <summary>Statisch zugeordnete Testdatei fuer ein Symbol (Testmethoden je Symbol gekappt).</summary>
internal sealed record TestAssociationPayload(
    string SymbolId,
    string FilePath,
    IReadOnlyList<string> TestMethods,
    string MatchReason);

/// <summary>Kompakter Violation-Eintrag — bewusst OHNE Snippet/Source-Ausschnitt.</summary>
internal sealed record ViolationPayload(
    string FilePath,
    int LineNumber,
    string RuleName,
    string Severity,
    string Details);

/// <summary>Vollstaendigkeitsmetadaten der Antwort (Kappungs-Flags und Gesamtzahlen).</summary>
internal sealed record CompletenessPayload(
    int ChangedSymbolsTotal,
    int ChangedSymbolsShown,
    bool SymbolsTruncated,
    bool CallSitesTruncated,
    bool TestsTruncated);

/// <summary>
/// Parameter-Bundel der reinen Abbildung auf das <see cref="ChangeContextPayload"/> — ab fuenf
/// Eingaben vorgeschriebenes Input-Record statt Parameterliste.
/// </summary>
internal sealed record ChangeContextResponseInput(
    DiffImpactAnalysis Analysis,
    TestCoverageBatchScanResult Batch,
    IReadOnlyList<RuleViolation> Violations,
    int MaxTestsPerSymbol);

/// <summary>
/// Reine Abbildung der Analyse-/Stufen-Ergebnisse auf den change-context-Antwortvertrag: keine
/// I/O, kein Git, kein Lint — nur Mapping inklusive deterministischer Test-Kappung (Reihenfolge =
/// bestehende MatchReason-Prioritaet → FilePath aus dem gebatchten Scan).
/// </summary>
internal static class ChangeContextResponseMapper
{
    /// <summary>Leere, aber vertragsgueltige Struktur fuer "kein Repo / leerer Diff".</summary>
    internal static ChangeContextPayload BuildEmptyPayload() => new(
        Mode: ChangeContextContract.ModeGitDiff,
        DetailLevel: ChangeContextContract.DetailLevelChangeContext,
        ChangedFiles: [],
        ChangedSymbols: [],
        CallSites: [],
        TestAssociations: [],
        Violations: [],
        RecommendedTestCommands: [],
        Completeness: new CompletenessPayload(0, 0, false, false, false));

    internal static ChangeContextPayload BuildPayload(ChangeContextResponseInput input)
    {
        var analysis = input.Analysis;
        var associations = MapTestAssociations(input.Batch.Symbols, input.MaxTestsPerSymbol);
        return new ChangeContextPayload(
            ChangeContextContract.ModeGitDiff,
            ChangeContextContract.DetailLevelChangeContext,
            analysis.ChangedFiles.Select(MapChangedFile).ToList(),
            analysis.ChangedSymbols.Select(MapChangedSymbol).ToList(),
            analysis.References.CallSites,
            associations.Associations,
            input.Violations.Select(MapViolation).ToList(),
            TestRecommendationBuilder.BuildDotNetTestCommands(associations.ShownTestFiles),
            BuildCompleteness(analysis, associations.TestsTruncated));
    }

    private static CompletenessPayload BuildCompleteness(DiffImpactAnalysis analysis, bool testsTruncated) =>
        new(
            analysis.ChangedSymbolsTotal,
            analysis.ChangedSymbols.Count,
            analysis.ChangedSymbolsTotal > analysis.ChangedSymbols.Count,
            analysis.References.Completeness.TruncatedByMaxResults
                || analysis.References.Completeness.TruncatedByNodeLimit,
            testsTruncated);

    private static ChangedFilePayload MapChangedFile(ChangedFileRange file) => new(
        file.FilePath,
        file.Ranges.Select(range => new HunkRangePayload(range.StartLine, range.LineCount)).ToList());

    private static ChangedSymbolPayload MapChangedSymbol(ChangedSymbolEntry entry) => new(
        entry.SymbolId,
        entry.DisplayName,
        entry.Kind,
        entry.Accessibility.ToString(),
        entry.ProjectName,
        entry.FilePath,
        entry.StartLine,
        entry.EndLine);

    private static ViolationPayload MapViolation(RuleViolation violation) => new(
        violation.FilePath,
        violation.LineNumber,
        violation.RuleName,
        RuleRegistry.ResolveSeverity(violation),
        violation.Details);

    private static TestAssociationMapping MapTestAssociations(
        IReadOnlyList<TestCoverageBatchSymbolResult> symbols, int maxTestsPerSymbol)
    {
        var associations = new List<TestAssociationPayload>();
        var shownTestFiles = new List<TestFileCoverageResult>();
        var testsTruncated = false;

        foreach (var symbol in symbols)
        {
            var remaining = maxTestsPerSymbol;
            foreach (var file in symbol.TestFiles)
            {
                if (remaining <= 0)
                {
                    testsTruncated = true;
                    break;
                }

                IReadOnlyList<string> taken = file.TestMethods.Count <= remaining
                    ? file.TestMethods
                    : file.TestMethods.Take(remaining).ToList();
                remaining -= taken.Count;
                testsTruncated |= taken.Count < file.TestMethods.Count;
                associations.Add(new TestAssociationPayload(symbol.SymbolId, file.FilePath, taken, file.MatchReason));
                shownTestFiles.Add(file);
            }
        }

        return new TestAssociationMapping(associations, shownTestFiles, testsTruncated);
    }
}

/// <summary>
/// Ergebnis der Assoziations-Mapping-Stufe: die vertraglichen Eintraege plus die NACH Test-Cap
/// gezeigten Treffer (Basis der deduplizierten empfohlenen Befehle) und das Kappungs-Flag.
/// </summary>
internal sealed record TestAssociationMapping(
    IReadOnlyList<TestAssociationPayload> Associations,
    IReadOnlyList<TestFileCoverageResult> ShownTestFiles,
    bool TestsTruncated);
