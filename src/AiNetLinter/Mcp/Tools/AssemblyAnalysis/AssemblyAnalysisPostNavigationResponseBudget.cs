#nullable enable

using AiNetLinter;
using AiNetLinter.Mcp.Wire;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

/// <summary>Verifies the final visible assembly response against its requested byte budget.</summary>
internal static class AssemblyAnalysisPostNavigationResponseBudget
{
    internal static CallToolResult ApplyInspect(CallToolResult result, int maxResponseBytes, bool publicOnly) =>
        FitOrReturnMinimum(result, maxResponseBytes);

    internal static CallToolResult ApplyExtensions(CallToolResult result, int maxResponseBytes) =>
        FitOrReturnMinimum(result, maxResponseBytes);

    internal static CallToolResult ApplyContext(CallToolResult result, int maxResponseBytes) =>
        FitOrReturnMinimum(result, maxResponseBytes);

    private static CallToolResult FitOrReturnMinimum(CallToolResult result, int maxResponseBytes)
    {
        var responseBytes = McpResponseSize.From(result).TotalBytes;
        return responseBytes <= maxResponseBytes
            ? result
            : McpToolResults.Error(
                LinterErrorCodes.ResponseBudgetTooSmall,
                $"maxResponseBytes={maxResponseBytes} ist zu klein für die fachliche Mindestprojektion; Mindestwert: {responseBytes} Bytes.",
                new McpErrorParameters(
                    Hint: $"maxResponseBytes auf mindestens {responseBytes} setzen; die Antwort wird nur an vollständigen Facheinheiten gekürzt.",
                    FieldPath: "$.maxResponseBytes",
                    RequestedBytes: maxResponseBytes,
                    MinimumResponseBytes: responseBytes));
    }
}
