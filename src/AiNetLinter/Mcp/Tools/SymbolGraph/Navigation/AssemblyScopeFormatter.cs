#nullable enable

using System;
using System.Linq;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.SymbolGraph.Navigation;

/// <summary>Rendert den gemeinsamen, von Snapshot-Diagnosen getrennten Assembly-Abfrage-Scope.</summary>
internal static class AssemblyScopeFormatter
{
    internal static CallToolResult Append(CallToolResult result, AssemblyNavigationSummary navigation)
    {
        if (result.IsError == true) return result;

        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? string.Empty;
        var snapshotCompleteness = text.Contains("snapshotCompleteness=partial", StringComparison.Ordinal)
            ? "partial"
            : text.Contains("snapshotCompleteness=complete", StringComparison.Ordinal)
                ? "complete"
                : null;
        return McpToolResults.ReplaceText(result, text + "\n\n" + Format(navigation, snapshotCompleteness));
    }

    internal static string Format(
        AssemblyNavigationSummary navigation,
        string? snapshotCompleteness = null)
    {
        var leaseStatus = !navigation.AssembliesTruncated
            && navigation.SearchedAssemblyCount == navigation.TotalAssemblyCount
            ? "complete"
            : "partial";
        var diagnosticTotal = navigation.DiagnosticTotalCount > 0
            ? navigation.DiagnosticTotalCount
            : navigation.Diagnostics.Count;
        var diagnosticShown = navigation.DiagnosticShownCount > 0
            ? navigation.DiagnosticShownCount
            : navigation.Diagnostics.Count;
        var identities = navigation.ScopeIdentities ?? [];
        var completeness = snapshotCompleteness == "partial" || navigation.Completeness == "partial"
            ? "partial"
            : "complete";
        var identityLines = identities.Count == 0
            ? "Assembly-Identities: none"
            : "Assembly-Identities:\n" + string.Join("\n", identities.Select(FormatIdentity));

        return "Assembly-Scope: " +
               $"requestedIncludeReferences={(navigation.RequestedIncludeReferences || navigation.IncludeReferences).ToString().ToLowerInvariant()}; " +
               $"effectiveSearchMode={navigation.EffectiveSearchMode}; " +
               $"completeness={completeness}; " +
               $"leaseCompleteness={leaseStatus}; " +
               $"assembliesSearched={navigation.SearchedAssemblyCount}; " +
               $"assembliesTotal={navigation.TotalAssemblyCount}; " +
               $"assembliesTruncated={navigation.AssembliesTruncated.ToString().ToLowerInvariant()}; " +
               $"operationCompleteness={navigation.Completeness}; " +
               $"resultsTruncated={navigation.ResultsTruncated.ToString().ToLowerInvariant()}; " +
               $"diagnostics={diagnosticShown}/{diagnosticTotal}; " +
               $"diagnosticsTruncated={navigation.DiagnosticsTruncated.ToString().ToLowerInvariant()}\n" +
               identityLines;
    }

    private static string FormatIdentity(AssemblyScopeIdentity identity) =>
        "- assemblyName=" + identity.AssemblyName + "; targetToken=" +
        (identity.TargetToken ?? "unavailable") + "; contentToken=" +
        (identity.ContentToken ?? "unavailable");
}
