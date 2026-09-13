#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiNetLinter.Core.TestCoverage;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.SymbolGraph.CallGraph;

/// <summary>
/// Formatiert die statische Impact-Zusammenfassung unabhängig von der sichtbaren Top-N-Liste.
/// </summary>
internal static class SymbolImpactFormatter
{
    internal static string FormatSource(
        Solution solution,
        ISymbol symbol,
        TestCoverageScannerResult testCoverage,
        TransitiveCallGraphFormatResult formatted)
    {
        var summary = CreateSourceSummary(solution, symbol, formatted.Traversal);
        var sb = CreateHeader(symbol, summary, formatted.Traversal);
        sb.AppendLine($"- **Betroffene Projekte ({summary.AffectedProjects.Count}):** {FormatProjects(summary.AffectedProjects)}");
        sb.AppendLine($"- **Zugeordnete Tests:** {testCoverage.TotalMatchingTests} Testmethode(n) in {testCoverage.TestFiles.Count} Testdatei(en)");
        AppendCallSites(sb, formatted.Text);
        AppendTestCandidates(sb, testCoverage);
        return sb.ToString().TrimEnd();
    }

    internal static string FormatAssembly(ISymbol symbol, TransitiveCallGraphFormatResult formatted)
    {
        var summary = CreateAssemblySummary(formatted.Traversal);
        var sb = CreateHeader(symbol, summary, formatted.Traversal);
        sb.AppendLine("- **Betroffene Projekte:** `not_decidable` (Assembly-Target enthält keine Projektzuordnung)");
        sb.AppendLine("- **Zugeordnete Tests:** `not_decidable` (Assembly-Target enthält keine Quelltest-Zuordnung)");
        AppendCallSites(sb, formatted.Text);
        return sb.ToString().TrimEnd();
    }

    private static SymbolImpactSummary CreateSourceSummary(
        Solution solution,
        ISymbol symbol,
        ReferenceTraversalResult traversal)
    {
        var allCallSites = traversal.AllCallSites ?? traversal.CallSites;
        var affectedProjects = allCallSites
            .Select(callSite => callSite.ProjectName)
            .Where(project => !string.IsNullOrWhiteSpace(project))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(project => project, StringComparer.OrdinalIgnoreCase)
            .ToList();
        AddSourceProject(solution, symbol, affectedProjects);
        return CreateSummary(affectedProjects, allCallSites, traversal.Completeness, assemblyTarget: false);
    }

    private static SymbolImpactSummary CreateAssemblySummary(ReferenceTraversalResult traversal) =>
        CreateSummary([], traversal.CallSites, traversal.Completeness, assemblyTarget: true);

    private static SymbolImpactSummary CreateSummary(
        IReadOnlyList<string> affectedProjects,
        IReadOnlyList<TransitiveCallSiteEntry> callSites,
        TraversalCompleteness completeness,
        bool assemblyTarget)
    {
        var direct = callSites.Count(callSite => callSite.Depth == 1);
        var transitive = callSites.Count - direct;
        var isComplete = !completeness.TruncatedByNodeLimit && !completeness.DepthWasClamped;
        var risk = assemblyTarget || !isComplete
            ? "not_decidable"
            : callSites.Count == 0
                ? "low"
                : affectedProjects.Count > 1 || transitive > 0
                    ? "high"
                    : "medium";
        return new(affectedProjects, direct, transitive, risk, isComplete ? "complete" : "partial");
    }

    private static StringBuilder CreateHeader(
        ISymbol symbol,
        SymbolImpactSummary summary,
        ReferenceTraversalResult traversal)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Auswirkungsanalyse (Impact): {symbol.ToDisplayString()}");
        sb.AppendLine(
            $"- **Impact-Summary:** `risk={summary.RiskLevel}; completeness={summary.Completeness}; " +
            $"directCallSites={summary.DirectCallSiteCount}; transitiveCallSites={summary.TransitiveCallSiteCount}; " +
            $"affectedProjectCount={summary.AffectedProjects.Count}; shownCallSites={traversal.CallSites.Count}`");
        return sb;
    }

    private static void AddSourceProject(Solution solution, ISymbol symbol, List<string> affectedProjects)
    {
        var sourceProject = solution.Projects.FirstOrDefault(project =>
            string.Equals(project.AssemblyName, symbol.ContainingAssembly?.Name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(project.Name, symbol.ContainingAssembly?.Name, StringComparison.OrdinalIgnoreCase));
        if (sourceProject is not null && !affectedProjects.Contains(sourceProject.Name, StringComparer.OrdinalIgnoreCase))
        {
            affectedProjects.Insert(0, sourceProject.Name);
        }
    }

    private static string FormatProjects(IReadOnlyList<string> projects) =>
        projects.Count > 0 ? string.Join(", ", projects) : "keine";

    private static void AppendCallSites(StringBuilder sb, string formattedCallSites)
    {
        sb.AppendLine();
        sb.AppendLine("## Aufrufstellen");
        sb.AppendLine(formattedCallSites);
    }

    private static void AppendTestCandidates(StringBuilder sb, TestCoverageScannerResult testCoverage)
    {
        if (testCoverage.TestFiles.Count == 0) return;

        sb.AppendLine();
        sb.AppendLine($"## Zugeordnete Testkandidaten ({testCoverage.TotalMatchingTests})");
        foreach (var testFile in testCoverage.TestFiles.Take(10))
        {
            var methodsPreview = testFile.TestMethods.Count > 0
                ? $" ({string.Join(", ", testFile.TestMethods.Take(3))}{(testFile.TestMethods.Count > 3 ? ", ..." : "")})"
                : "";
            sb.AppendLine($"- {testFile.FilePath}{methodsPreview}");
        }
    }

    private sealed record SymbolImpactSummary(
        IReadOnlyList<string> AffectedProjects,
        int DirectCallSiteCount,
        int TransitiveCallSiteCount,
        string RiskLevel,
        string Completeness);
}
