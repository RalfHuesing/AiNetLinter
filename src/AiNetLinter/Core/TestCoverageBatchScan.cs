#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Core;

/// <summary>
/// Gebatchte Haelfte des <see cref="TestCoverageScanner"/>: Projekte und Dokumente werden
/// pro Aufruf GENAU EINMAL iteriert, SyntaxRoot und SemanticModel je Dokument genau einmal
/// bezogen und dann gegen ALLE Ziel-Symbole gematcht — kein vollstaendiger Testprojekt-Scan
/// pro Symbol. Evidenzarten, Konstanten und Prioritaeten kommen unveraendert aus dem
/// gemeinsamen per-Symbol-Kern.
/// </summary>
public static partial class TestCoverageScanner
{
    /// <summary>
    /// Ordnet alle Ziel-Symbole in EINEM Solution-Durchlauf ihre Testdateien zu.
    /// </summary>
    /// <param name="targetSymbols">Die (bereits gekappten) Ziel-Symbole.</param>
    /// <param name="solution">Die geladene Roslyn-Solution inklusive Testprojekten.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public static Task<TestCoverageBatchScanResult> FindTestsForSymbolsAsync(
        IReadOnlyList<ISymbol> targetSymbols,
        Solution solution,
        CancellationToken ct = default) =>
        FindTestsForSymbolsCoreAsync(targetSymbols, solution, counters: null, ct);

    internal static async Task<TestCoverageBatchScanResult> FindTestsForSymbolsCoreAsync(
        IReadOnlyList<ISymbol> targetSymbols,
        Solution solution,
        DiffImpactCounters? counters,
        CancellationToken ct = default)
    {
        var targets = NormalizeTargets(targetSymbols);
        if (targets.Count == 0)
        {
            return BuildBatchResult(targets, []);
        }

        // Performance Short-Circuit: wenn alle Projekte dekompilierte Assemblies ohne Testframework-Referenzen sind
        if (solution.ProjectIds.Count > 0 && solution.Projects.All(p => TestDetector.IsDecompiledAssemblyProject(p) && !TestDetector.HasTestFrameworkReferences(p)))
        {
            var emptyMatches = new List<TestFileCoverageResult>[targets.Count];
            for (var i = 0; i < targets.Count; i++) emptyMatches[i] = [];
            return BuildBatchResult(targets, emptyMatches);
        }

        if (counters is { } activeCounters)
        {
            Interlocked.Increment(ref activeCounters.TestSolutionScans);
        }

        var matchesPerTarget = await ScanAllTestDocumentsAsync(solution, targets, ct);
        return BuildBatchResult(targets, matchesPerTarget);
    }

    private static List<BatchTarget> NormalizeTargets(IReadOnlyList<ISymbol> targetSymbols)
    {
        var targets = new List<BatchTarget>(targetSymbols.Count);
        foreach (var symbol in targetSymbols)
        {
            if (symbol is null) continue;
            targets.Add(new BatchTarget(
                symbol,
                CallGraphTraversal.GetStableSymbolId(symbol),
                ResolveTargetTypeName(symbol),
                symbol is INamedTypeSymbol ? null : symbol.Name));
        }

        return targets;
    }

    private static string ResolveTargetTypeName(ISymbol symbol)
    {
        var containingType = symbol is INamedTypeSymbol named ? named : symbol.ContainingType;
        return containingType?.Name ?? symbol.Name;
    }

    private static async Task<List<TestFileCoverageResult>[]> ScanAllTestDocumentsAsync(
        Solution solution,
        IReadOnlyList<BatchTarget> targets,
        CancellationToken ct)
    {
        var states = targets.Select(target => new BatchTargetState(target)).ToArray();
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";

        foreach (var project in solution.Projects)
        {
            if (ct.IsCancellationRequested) break;
            if (!ShouldScanProject(project)) continue;

            await ScanProjectDocumentsAsync(project, solutionDir, states, ct).ConfigureAwait(false);
        }

        return states.Select(state => state.Files).ToArray();
    }

    private static bool ShouldScanProject(Project project)
    {
        if (TestDetector.IsDecompiledAssemblyProject(project) && !TestDetector.HasTestFrameworkReferences(project))
        {
            return false;
        }

        return TestDetector.IsTestProjectOrHasTestFiles(project);
    }

    private static async Task ScanProjectDocumentsAsync(
        Project project,
        string solutionDir,
        BatchTargetState[] states,
        CancellationToken ct)
    {
        var isTestProject = TestDetector.IsTestProject(project);
        foreach (var document in project.Documents)
        {
            if (ct.IsCancellationRequested) break;
            if (!isTestProject && !TestDetector.IsTestFile(document.FilePath ?? "")) continue;
            await ScanDocumentAgainstTargetsAsync(document, solutionDir, states, ct).ConfigureAwait(false);
        }
    }

    private static async Task ScanDocumentAgainstTargetsAsync(
        Document document,
        string solutionDir,
        BatchTargetState[] states,
        CancellationToken ct)
    {
        if (document.FilePath is null) return;
        var relativePath = PathNormalizer.ToRelative(solutionDir, document.FilePath);
        if (!TestDetector.IsTestFile(relativePath)) return;

        var syntaxRoot = await document.GetSyntaxRootAsync(ct);
        if (syntaxRoot is null) return;

        var semanticModel = await document.GetSemanticModelAsync(ct);
        if (semanticModel is null) return;

        var loadedDocument = new LoadedTestDocument(document, relativePath, syntaxRoot);
        foreach (var state in states)
        {
            var (fileMatches, reason, matchingMethods, totalClassTests) = AnalyzeDocument(
                syntaxRoot, semanticModel, state.Target.Symbol, state.Target.TypeName, state.Target.MemberName);
            if (!fileMatches || matchingMethods.Count == 0) continue;

            state.Files.Add(BuildFileCoverageResult(
                loadedDocument, reason, matchingMethods, totalClassTests));
        }
    }

    private static TestCoverageBatchScanResult BuildBatchResult(
        IReadOnlyList<BatchTarget> targets,
        IReadOnlyList<List<TestFileCoverageResult>> matchesPerTarget)
    {
        var symbols = new List<TestCoverageBatchSymbolResult>(targets.Count);
        for (var i = 0; i < targets.Count; i++)
        {
            var sorted = matchesPerTarget[i]
                .OrderBy(r => GetMatchReasonPriority(r.MatchReason))
                .ThenBy(r => r.FilePath)
                .ToList();
            symbols.Add(new TestCoverageBatchSymbolResult(
                targets[i].SymbolId,
                sorted.Sum(r => r.TestMethods.Count),
                sorted));
        }

        var distinctPaths = symbols
            .SelectMany(s => s.TestFiles)
            .Select(f => f.FilePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        return new TestCoverageBatchScanResult(symbols, distinctPaths.Count, distinctPaths);
    }

    /// <summary>Normalisiertes Ziel-Paar (Symbol plus TypeName/MemberName) mit stabiler ID.</summary>
    private sealed record BatchTarget(ISymbol Symbol, string SymbolId, string TypeName, string? MemberName);

    /// <summary>Sammelt die Treffer eines Ziels waehrend des gemeinsamen Dokument-Durchlaufs.</summary>
    private sealed class BatchTargetState(BatchTarget target)
    {
        public BatchTarget Target { get; } = target;

        public List<TestFileCoverageResult> Files { get; } = [];
    }
}
