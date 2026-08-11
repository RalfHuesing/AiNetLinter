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
/// Method-Granularitaet) ueber die geladene Solution, siehe
/// <c>tasks/features/07-drift-audit-ideen.md</c> §A "Idee A". Bewusst duenner Dispatch ohne
/// eigene Scan-Logik — die eigentliche Engine-Arbeit steckt in
/// <see cref="Core.DuplicateDetection.DuplicateDetectionEngine"/> (geteilt mit
/// <c>DuplicateCodeChecker</c>), die Argument-Aufloesung/Trunkierung in
/// <see cref="DuplicateDetectionScanner"/>, hier nur Validierung + Text-/JSON-Formatierung.
/// </summary>
internal static class DuplicateDetectionTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, DuplicateDetectionInput input, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var (minBucket, thresholdError) = ParseSimilarityThreshold(input.SimilarityThreshold);
        if (thresholdError is not null) return thresholdError;

        if (input.MinTokens is < 1)
        {
            return McpToolResults.InvalidArgument("minTokens muss mindestens 1 sein.");
        }
        if (input.MaxResults is < 1)
        {
            return McpToolResults.InvalidArgument("maxResults muss mindestens 1 sein.");
        }

        var configSnapshot = state.GetConfigSnapshot();
        var config = configSnapshot.Config.Global;

        try
        {
            var result = await DuplicateDetectionScanner.ScanAsync(solution, config, input, minBucket, ct);
            return BuildResponse(solution, result);
        }
        catch (System.Exception ex) when (ex is not System.OperationCanceledException)
        {
            return McpToolResults.CompilationError($"Unerwarteter Fehler in find_duplicates: {ex.Message}");
        }
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
                $"Ungueltiger similarityThreshold-Wert '{value}' — gueltig sind 'exact', 'near', 'fuzzy'.")),
        };
    }

    private static CallToolResult BuildResponse(Microsoft.CodeAnalysis.Solution solution, DuplicateDetectionScanResultForTool result)
    {
        var solutionDir = System.IO.Path.GetDirectoryName(solution.FilePath) ?? "";
        var body = RenderText(solutionDir, result);
        // Trunkierungs-Meta-Zeile UND Sufficiency-Hinweis schliessen sich gegenseitig aus (siehe
        // McpSufficiencyHints-Doc-Kommentar) — nur bei vollstaendigem Ergebnis den Hinweis anhaengen.
        var finalText = result.Truncated ? body : McpSufficiencyHints.Append(body);

        var payload = new DuplicateDetectionPayload(
            Clusters: result.ShownClusters.Select(c => ToPayloadEntry(solutionDir, c)).ToList(),
            Summary: new DuplicateDetectionSummary(
                MethodsScanned: result.MethodsScanned,
                TotalClusters: result.TotalClusters,
                ShownClusters: result.ShownClusters.Count,
                Truncated: result.Truncated));

        // In ein Objekt gewrappt statt eines nackten Arrays (M2-Regressionslehre, siehe
        // McpToolResults.Text<T>-Doc-Kommentar).
        return McpToolResults.Text(finalText, payload);
    }

    private static DuplicateClusterPayloadEntry ToPayloadEntry(string solutionDir, DuplicateCluster cluster) =>
        new(
            Bucket: BucketLabel(cluster.Bucket),
            Score: cluster.Score,
            Members: cluster.Members.Select(m => ToEntry(solutionDir, m)).ToList());

    private static DuplicateClusterEntry ToEntry(string solutionDir, DuplicateClusterMember member) =>
        new(PathNormalizer.ToRelative(solutionDir, member.FilePath), member.LineNumber, member.SignatureName, member.TokenCount);

    private static string BucketLabel(DuplicateSimilarityBucket bucket) => bucket switch
    {
        DuplicateSimilarityBucket.Exact => "exact",
        DuplicateSimilarityBucket.Near => "near",
        _ => "fuzzy",
    };

    private static string RenderText(string solutionDir, DuplicateDetectionScanResultForTool result)
    {
        if (result.ShownClusters.Count == 0)
        {
            return $"Keine Duplikat-Cluster gefunden ({result.MethodsScanned} Methoden gescannt).";
        }

        var sb = new StringBuilder();
        sb.Append($"{result.ShownClusters.Count} von {result.TotalClusters} Duplikat-Cluster(n) ({result.MethodsScanned} Methoden gescannt):");
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
        }
    }
}
