#nullable enable

using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools;

internal static partial class GetSymbolBodyTool
{
    internal static CallToolResult ApplyFinalResponseBudget(CallToolResult result, int maxResponseBytes)
    {
        var minimumResponseBytes = Mcp.Wire.McpResponseSize.From(result).TotalBytes;
        if (maxResponseBytes <= 0 || minimumResponseBytes <= maxResponseBytes)
        {
            return result;
        }

        return BudgetTooSmall(maxResponseBytes, minimumResponseBytes);
    }

}
