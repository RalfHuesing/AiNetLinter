#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal static class TransitiveCallGraphFormatter
{
    internal static bool IsComplete(ReferenceTraversalResult result)
    {
        var completeness = result.Completeness;
        return !completeness.TruncatedByMaxResults &&
               !completeness.TruncatedByNodeLimit &&
               !completeness.DepthWasClamped;
    }

    internal static string Format(ReferenceTraversalResult result)
    {
        var completeness = result.Completeness;
        var lines = result.CallSites
            .Select(entry => FormatEntry(entry, completeness.EffectiveDepth > 1))
            .ToList();

        AppendLimitMessages(lines, completeness);
        return string.Join("\n", lines);
    }

    private static string FormatEntry(TransitiveCallSiteEntry entry, bool transitive)
    {
        return transitive
            ? $"{entry.FilePath}:{entry.Line} - transitiver Aufrufer"
            : $"{entry.FilePath}:{entry.Line} - Aufruf von '{entry.SymbolName}' in Projekt '{entry.ProjectName}'";
    }

    private static void AppendLimitMessages(
        List<string> lines,
        TraversalCompleteness completeness)
    {
        if (completeness.TruncatedByMaxResults)
        {
            lines.Add(CreateMaxResultsMessage(completeness));
        }

        if (completeness.TruncatedByNodeLimit)
        {
            lines.Add(
                $"[Traversal auf {CallGraphTraversal.MaxRecursionNodes} Knoten begrenzt — weitere Treffer nicht enthalten]");
        }

        if (completeness.DepthWasClamped)
        {
            lines.Add(
                $"[depth auf {completeness.EffectiveDepth} begrenzt — requestedDepth={completeness.RequestedDepth}]");
        }
    }

    private static string CreateMaxResultsMessage(TraversalCompleteness completeness)
    {
        return completeness.EffectiveDepth == 1
            ? $"[{completeness.TotalCallSiteCount} Treffer gesamt, " +
              $"{completeness.ShownCallSiteCount} gezeigt — Pattern verfeinern oder maxResults erhöhen]"
            : $"[{completeness.TotalCallSiteCount} Treffer gesamt " +
              $"(depth={completeness.EffectiveDepth}, hard-cap {CallGraphTraversal.MaxRecursionNodes}), " +
              $"{completeness.ShownCallSiteCount} gezeigt — depth reduzieren oder maxResults erhoehen]";
    }
}
