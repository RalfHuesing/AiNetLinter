#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core.DuplicateDetection;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.DuplicateDetection;

/// <summary>
/// MCP-Tool <c>find_duplicates</c>: Token-basierte Code-Clone-Detection (Jaccard-N-Gram,
/// Method-Granularitaet) ueber die geladene Solution. Bewusst duenner Dispatch ohne eigene
/// Scan-Logik — die eigentliche Engine-Arbeit steckt in
/// <see cref="Core.DuplicateDetection.DuplicateDetectionEngine"/> (geteilt mit
/// <c>DuplicateCodeChecker</c>), die Argument-Aufloesung/Trunkierung in
/// <see cref="DuplicateDetectionScanner"/> (<c>mode="clone"</c>, Default) bzw.
/// <see cref="RefactoringDriftScanner"/> (<c>mode="refactoring-drift"</c>), hier nur
/// Mode-Dispatch, Validierung + Text-/JSON-Formatierung.
/// </summary>
internal static class DuplicateDetectionTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, DuplicateDetectionInput input, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var mode = DuplicateDetectionModeParser.TryParse(input.Mode);
        if (mode is null)
        {
            return McpToolResults.InvalidArgument(
                $"Ungueltiger mode-Wert '{input.Mode}' — gueltig sind 'clone', 'refactoring-drift', 'structural'.",
                hint: "mode='clone', 'refactoring-drift' oder 'structural' angeben (Default: 'clone').");
        }
        if (input.MinTokens is < 1)
        {
            return McpToolResults.InvalidArgument("minTokens muss mindestens 1 sein.",
                hint: "minTokens als positive Ganzzahl >= 1 angeben.");
        }
        if (input.MaxResults is < 1)
        {
            return McpToolResults.InvalidArgument("maxResults muss mindestens 1 sein.",
                hint: "maxResults als positive Ganzzahl >= 1 angeben.");
        }
        if (!string.IsNullOrWhiteSpace(input.ScopeType))
        {
            var st = input.ScopeType.Trim().ToLowerInvariant();
            if (st is not ("all" or "production" or "tests"))
            {
                return McpToolResults.InvalidArgument(
                    $"Ungueltiger scopeType-Wert '{input.ScopeType}' — gueltig sind 'all', 'production', 'tests'.",
                    hint: "scopeType='all', 'production' oder 'tests' angeben (Default: 'all').");
            }
        }

        var configSnapshot = state.GetConfigSnapshot();
        var config = configSnapshot.Config.Global;

        try
        {
            return mode.Value switch
            {
                DuplicateDetectionMode.RefactoringDrift => await ExecuteRefactoringDriftAsync(solution, config, input, ct),
                _ => await ExecuteClusterScanAsync(new ClusterScanRequest(solution, config, input, mode.Value), ct),
            };
        }
        catch (System.Exception ex) when (ex is not System.OperationCanceledException)
        {
            return McpToolResults.CompilationError($"Unerwarteter Fehler in find_duplicates: {ex.Message}");
        }
    }

    private sealed record ClusterScanRequest(
        Microsoft.CodeAnalysis.Solution Solution,
        Configuration.GlobalConfig Config,
        DuplicateDetectionInput Input,
        DuplicateDetectionMode Mode);

    /// <summary>Gemeinsamer Pfad beider Cluster-Modi (<see cref="DuplicateDetectionMode.Clone"/> und
    /// <see cref="DuplicateDetectionMode.Structural"/>): identischer Threshold-Parse, Scan und
    /// Response-Aufbau — nur der Scanner und das Wire-Label unterscheiden sich.</summary>
    private static async Task<CallToolResult> ExecuteClusterScanAsync(ClusterScanRequest request, CancellationToken ct)
    {
        var (minBucket, thresholdError) = ParseSimilarityThreshold(request.Input.SimilarityThreshold);
        if (thresholdError is not null) return thresholdError;

        var isStructural = request.Mode == DuplicateDetectionMode.Structural;
        var result = isStructural
            ? await StructuralDuplicateScanner.ScanAsync(request.Solution, request.Config, request.Input, minBucket, ct)
            : await DuplicateDetectionScanner.ScanAsync(request.Solution, request.Config, request.Input, minBucket, ct);
        return BuildResponse(request.Solution, result, isStructural ? DuplicateDetectionModeLabels.Structural : DuplicateDetectionModeLabels.Clone);
    }

    private static async Task<CallToolResult> ExecuteRefactoringDriftAsync(
        Microsoft.CodeAnalysis.Solution solution, Configuration.GlobalConfig config, DuplicateDetectionInput input,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.HelperSymbol))
        {
            return McpToolResults.InvalidArgument(
                "helperSymbol ist bei mode='refactoring-drift' Pflicht (Datei:Zeile:Spalte, stabile " +
                "DocumentationCommentId oder qualifizierter Name — Format wie bei find_references).",
                hint: "helperSymbol als C#-Symbol-Identifikator angeben (z. B. 'Klasse.Methode' oder 'M:Namespace.Klasse.Methode').");
        }

        var (result, error) = await RefactoringDriftScanner.ScanAsync(solution, config, input, ct);
        if (error is not null) return error;

        return RefactoringDriftResponseBuilder.Build(solution, result!);
    }

    /// <summary>Case-insensitiv, leer/<see langword="null"/> = Default <c>fuzzy</c> (niedrigste
    /// Stufe — zeigt alles, was die Engine ueberhaupt meldet).</summary>
    private static (DuplicateSimilarityBucket Bucket, CallToolResult? Error) ParseSimilarityThreshold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return (DuplicateSimilarityBucket.Fuzzy, null);
        return value.Trim().ToLowerInvariant() switch
        {
            "exact" => (DuplicateSimilarityBucket.Exact, null),
            "near" => (DuplicateSimilarityBucket.Near, null),
            "fuzzy" => (DuplicateSimilarityBucket.Fuzzy, null),
            _ => (DuplicateSimilarityBucket.Fuzzy, McpToolResults.InvalidArgument(
                $"Ungueltiger similarityThreshold-Wert '{value}' — gueltig sind 'exact', 'near', 'fuzzy'.",
                hint: "similarityThreshold='exact', 'near' oder 'fuzzy' angeben (Default: 'fuzzy').")),
        };
    }

    private static CallToolResult BuildResponse(
        Microsoft.CodeAnalysis.Solution solution, DuplicateDetectionScanResultForTool result, string mode)
    {
        var solutionDir = System.IO.Path.GetDirectoryName(solution.FilePath) ?? "";
        var body = RenderText(solutionDir, result, mode);
        var finalText = result.Truncated ? body : McpSufficiencyHints.Append(body);

        var payload = new DuplicateDetectionPayload(
            Clusters: result.ShownClusters.Select(c => ToPayloadEntry(solutionDir, c)).ToList(),
            Summary: new DuplicateDetectionSummary(
                MethodsScanned: result.MethodsScanned,
                TotalClusters: result.TotalClusters,
                ShownClusters: result.ShownClusters.Count,
                Truncated: result.Truncated,
                Mode: mode));

        // In ein Objekt gewrappt statt eines nackten Arrays (siehe
        // McpToolResults.Text<T>-Doc-Kommentar).
        return McpToolResults.Text(finalText, payload);
    }

    private static DuplicateClusterPayloadEntry ToPayloadEntry(string solutionDir, DuplicateCluster cluster) =>
        new(
            Bucket: BucketLabel(cluster.Bucket),
            Score: cluster.Score,
            Members: cluster.Members.Select(m => ToEntry(solutionDir, m)).ToList());

    private static DuplicateClusterEntry ToEntry(string solutionDir, DuplicateClusterMember member) =>
        new(
            PathNormalizer.ToRelative(solutionDir, member.FilePath),
            member.LineNumber,
            member.SignatureName,
            member.TokenCount,
            member.StructureProfile);

    private static string BucketLabel(DuplicateSimilarityBucket bucket) => bucket switch
    {
        DuplicateSimilarityBucket.Exact => "exact",
        DuplicateSimilarityBucket.Near => "near",
        _ => "fuzzy",
    };

    private static string RenderText(string solutionDir, DuplicateDetectionScanResultForTool result, string mode)
    {
        var isStructural = mode == DuplicateDetectionModeLabels.Structural;
        if (result.ShownClusters.Count == 0)
        {
            return isStructural
                ? $"Keine strukturellen Kandidatencluster gefunden ({result.MethodsScanned} Methoden gescannt). Kandidaten, keine Verstoesse."
                : $"Keine Duplikat-Cluster gefunden ({result.MethodsScanned} Methoden gescannt).";
        }

        var sb = new StringBuilder();
        if (isStructural)
        {
            sb.Append($"{result.ShownClusters.Count} von {result.TotalClusters} strukturelle(n) Kandidatencluster(n) ({result.MethodsScanned} Methoden gescannt). ");
            sb.Append("Pruefempfehlungen, keine Verstoesse — semantische Aehnlichkeit ist nicht zwingend Duplikation:");
        }
        else
        {
            sb.Append($"{result.ShownClusters.Count} von {result.TotalClusters} Duplikat-Cluster(n) ({result.MethodsScanned} Methoden gescannt):");
        }

        if (result.TotalClusters > 20 || result.ShownClusters.Count > 20)
        {
            sb.Append("\n\n### Top-Cluster Uebersicht:");
            var topCount = Math.Min(5, result.ShownClusters.Count);
            for (int i = 0; i < topCount; i++)
            {
                var c = result.ShownClusters[i];
                var files = string.Join(", ", c.Members.Select(m => PathNormalizer.ToRelative(solutionDir, m.FilePath)).Distinct());
                sb.Append($"\n- Cluster {i + 1} ({BucketLabel(c.Bucket)}, Score {c.Score:F2}, {c.Members.Count} Methoden): {files}");
            }
        }

        var index = 0;
        foreach (var cluster in result.ShownClusters)
        {
            index++;
            AppendCluster(sb, solutionDir, index, cluster);
        }

        if (result.Truncated)
        {
            sb.Append('\n');
            sb.Append($"[{result.TotalClusters} Cluster gesamt, {result.ShownClusters.Count} gezeigt — maxResults erhoehen oder scopeDir eingrenzen]");
        }

        return sb.ToString();
    }

    private static void AppendCluster(StringBuilder sb, string solutionDir, int index, DuplicateCluster cluster)
    {
        sb.Append($"\n\n## {index}. {BucketLabel(cluster.Bucket)} (Score {cluster.Score:F2}, {cluster.Members.Count} Methoden)");
        foreach (var member in cluster.Members)
        {
            var relativePath = PathNormalizer.ToRelative(solutionDir, member.FilePath);
            sb.Append($"\n- {member.SignatureName} ({relativePath}:{member.LineNumber}, {member.TokenCount} Tokens)");
            if (!string.IsNullOrEmpty(member.StructureProfile))
            {
                sb.Append($"\n  Profil: {member.StructureProfile}");
            }
        }
    }
}
