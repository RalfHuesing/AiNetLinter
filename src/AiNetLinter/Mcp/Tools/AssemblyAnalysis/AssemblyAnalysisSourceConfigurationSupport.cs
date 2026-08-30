#nullable enable

using System.Collections.Generic;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal static class AssemblyAnalysisSourceConfigurationSupport
{
    internal static CallToolResult CreateConfigurationFailureResult(
        AssemblySourceSelectionScope source,
        string assemblyPath)
    {
        var diagnostics = AssemblyAnalysisDiagnostics.FormatExternalDiagnostics(source.Diagnostics);
        var code = AssemblyAnalysisDiagnostics.GetConfigurationFailureCode(source.Diagnostics);
        return McpToolResults.Recoverable(
            code,
            AppendDiagnostics("Die externe Source-Konfiguration ist ungültig.", diagnostics),
            context: assemblyPath,
            hint: "ExternalSources-Konfiguration korrigieren und erneut versuchen.");
    }

    internal static string AppendDiagnostics(string message, IReadOnlyList<string> diagnostics) =>
        diagnostics.Count == 0
            ? message
            : $"{message} {string.Join(" ", diagnostics)}";
}
