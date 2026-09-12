#nullable enable

using AiNetLinter.Mcp.Wire;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.CallTree;

internal static partial class CallGraphResponseBudget
{
    internal static CallToolResult ApplyFinalResponseBudget(CallToolResult result, int maxResponseBytes)
    {
        var minimumResponseBytes = McpResponseSize.From(result).TotalBytes;
        if (maxResponseBytes <= 0 || minimumResponseBytes <= maxResponseBytes) return result;
        return McpToolResults.Recoverable(
            LinterErrorCodes.ResponseBudgetTooSmall,
            "maxResponseBytes ist für den Call-Graph zu klein.",
            new McpErrorParameters(
                Hint: "maxResponseBytes erhöhen oder includeReferences/symbolIdentifier verfeinern.",
                FieldPath: "$.maxResponseBytes",
                RequestedBytes: maxResponseBytes,
                MinimumResponseBytes: minimumResponseBytes));
    }
}
