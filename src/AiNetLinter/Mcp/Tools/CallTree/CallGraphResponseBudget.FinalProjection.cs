#nullable enable

using AiNetLinter.Mcp.Wire;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.CallTree;

internal static partial class CallGraphResponseBudget
{
    internal static CallToolResult ApplyFinalResponseBudget(CallToolResult result, int maxResponseBytes)
    {
        if (maxResponseBytes <= 0 || McpResponseSize.From(result).TotalBytes <= maxResponseBytes) return result;
        return McpToolResults.InvalidArgument(
            "maxResponseBytes ist für den Call-Graph zu klein.",
            "maxResponseBytes erhöhen oder includeReferences/symbolIdentifier verfeinern.",
            "$.maxResponseBytes");
    }
}
