#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.Metrics;
using AiNetLinter.Models;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools.MetricsTree;

/// <summary>
/// Gebuendelte, bereits validierte Parameter fuer <see cref="MetricsTreeRoslynScanner.BuildTreeAsync"/>
/// (analog <see cref="GetViolationsScannerParameters"/>, wegen <c>MaxMethodParameterCount: 4</c>).
/// </summary>
internal sealed record MetricsTreeRoslynScanParameters(
    Solution Solution, ILinterEngineConfig Config, ILintConsole Console, CancellationToken CancellationToken);

/// <summary>
/// Walk + Aggregation fuer die zwei Roslyn-Modi von <c>metrics_tree</c> (<c>violation_density</c>,
/// <c>complexity</c>) — getrennt von <see cref="MetricsTreeScanner"/>, damit deren rein
/// dateibasierter Pfad (<c>code_size</c>/<c>comment_density</c>, synchron, kein
/// <see cref="Solution"/>-Overhead) nicht zusaetzlich zum <c>LinterEngine</c>-/Roslyn-Syntax-Pull-in
/// dieser Datei beitraegt (analoges Splitting-Prinzip wie <see cref="GetHotspotsScanner"/>/
/// <see cref="GetViolationsScanner"/> fuer zwei getrennte Datenquellen). Baut aus den pro-Datei-Werten
/// dieselben <see cref="FileMetric"/>/<see cref="BuilderNode"/>
/// wie die Datei-Modi und ruft deren (dafuer <c>internal</c> gemachten) Aggregations-Kern
/// (<see cref="MetricsTreeScanner.BuildNode"/>/<see cref="MetricsTreeScanner.ToMetricsTreeNode"/>)
/// wieder — keine zweite Baum-Implementierung.
/// </summary>
internal static class MetricsTreeRoslynScanner
{
    internal static async Task<string> BuildTreeAsync(MetricsTreeRoslynScanParameters scan, MetricsTreeQuery query)
    {
        var result = await BuildTreeResultAsync(scan, query);
        return result.Root is null
            ? result.Message!
            : MetricsTreeRenderer.Render(result.Root, query.TopN, sortDescending: true);
    }

    internal static async Task<MetricsTreeScanResult> BuildTreeResultAsync(
        MetricsTreeRoslynScanParameters scan, MetricsTreeQuery query)
    {
        var solutionDir = Path.GetDirectoryName(scan.Solution.FilePath) ?? "";
        var rootRelative = MetricsTreeScanner.NormalizeRoot(query.Root);

        var walked = SolutionFileWalker.CollectFiles(scan.Solution, solutionDir, scopeFilter: null, query.FileFilter);
        var scoped = walked.Where(f => f.RelativePath.StartsWith(rootRelative, StringComparison.OrdinalIgnoreCase)).ToList();

        if (scoped.Count == 0)
        {
            return new(null, $"Keine Dateien unter root='{rootRelative}'" +
                (query.FileFilter != null ? " mit file_filter" : "") + " — Pfad/Filter pruefen.");
        }

        var metrics = query.Mode == MetricsTreeMode.ViolationDensity
            ? await ComputeViolationDensityMetricsAsync(scan, scoped)
            : await ComputeComplexityMetricsAsync(scan, scoped);

        if (metrics.Count == 0)
        {
            return new(null, $"Keine auswertbaren Dateien unter root='{rootRelative}' — Pfad/Filter pruefen.");
        }

        var rootName = MetricsTreeScanner.ComputeRootName(solutionDir, rootRelative);
        var builderRoot = MetricsTreeScanner.BuildNode(rootName, rootRelative, metrics, level: 0, query.Depth);
        return new(MetricsTreeScanner.ToMetricsTreeNode(builderRoot, query.Mode), null);
    }

    private static async Task<List<FileMetric>> ComputeViolationDensityMetricsAsync(
        MetricsTreeRoslynScanParameters scan, List<WalkedFile> scoped)
    {
        // Konstruktion identisch zu GetViolationsScanner.BuildViolationsTextAsync: LinterEngine
        // verlangt den konkreten Config-Typ (Record-Semantik fuer `with {...}`); ILinterEngineConfig
        // wird projektweit ausschliesslich von Config implementiert, der Downcast ist nicht spekulativ.
        var concreteConfig = (Config)scan.Config;
        var engine = new LinterEngine(
            config: concreteConfig, rulesJsonContent: null, profiler: null, console: scan.Console);
        var violations = await engine.RunAsync(scan.Solution, noCache: true, cacheTtlMinutes: 0, scan.CancellationToken);

        var byFile = violations
            .GroupBy(v => v.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        return scoped.Select(f =>
        {
            var fileViolations = byFile.TryGetValue(f.AbsolutePath, out var list) ? list : new List<RuleViolation>();
            var errorCount = fileViolations.Count(v => RuleRegistry.ResolveSeverity(v) == "error");
            return new FileMetric(
                f.RelativePath, CommentLines: 0, CodeLines: 0, Bytes: 0,
                ViolationCount: fileViolations.Count, ErrorCount: errorCount,
                WarningCount: fileViolations.Count - errorCount);
        }).ToList();
    }

    private static async Task<List<FileMetric>> ComputeComplexityMetricsAsync(
        MetricsTreeRoslynScanParameters scan, List<WalkedFile> scoped)
    {
        var fileToDocument = BuildFileToDocumentMap(scan.Solution);
        var result = new List<FileMetric>(scoped.Count);

        foreach (var f in scoped)
        {
            if (!fileToDocument.TryGetValue(f.AbsolutePath, out var document))
            {
                result.Add(new FileMetric(f.RelativePath, CommentLines: 0, CodeLines: 0, Bytes: 0));
                continue;
            }

            result.Add(await ComputeFileComplexityMetricAsync(f.RelativePath, document, scan.CancellationToken));
        }

        return result;
    }

    private static async Task<FileMetric> ComputeFileComplexityMetricAsync(
        string relativePath, Document document, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct);
        if (root is null)
        {
            return new FileMetric(relativePath, CommentLines: 0, CodeLines: 0, Bytes: 0);
        }

        // Bewusste Scope-Grenze (kein Bug): nur Methoden (MethodDeclarationSyntax), keine Properties/
        // Konstruktoren/lokalen Funktionen — konsistent mit der bestehenden Signatur von
        // ComplexityCalculator, die im Projekt nirgendwo darueber hinaus erweitert wurde.
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
        if (methods.Count == 0)
        {
            return new FileMetric(relativePath, CommentLines: 0, CodeLines: 0, Bytes: 0);
        }

        var sumCyclomatic = 0;
        var maxCyclomatic = 0;
        var maxCognitive = 0;
        foreach (var method in methods)
        {
            var cyclomatic = ComplexityCalculator.GetCyclomaticComplexity(method);
            var cognitive = ComplexityCalculator.GetCognitiveComplexity(method);
            sumCyclomatic += cyclomatic;
            maxCyclomatic = Math.Max(maxCyclomatic, cyclomatic);
            maxCognitive = Math.Max(maxCognitive, cognitive);
        }

        return new FileMetric(
            relativePath, CommentLines: 0, CodeLines: 0, Bytes: 0,
            MethodCount: methods.Count, SumCyclomatic: sumCyclomatic,
            MaxCyclomatic: maxCyclomatic, MaxCognitive: maxCognitive);
    }

    private static Dictionary<string, Document> BuildFileToDocumentMap(Solution solution)
    {
        var map = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (document.FilePath is null) continue;
                map[document.FilePath] = document;
            }
        }
        return map;
    }
}
