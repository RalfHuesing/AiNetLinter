#nullable enable

using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools;

internal static partial class GetSymbolBodyTool
{
    internal static CallToolResult ApplyFinalResponseBudget(CallToolResult result, int maxResponseBytes)
    {
        if (maxResponseBytes <= 0 || Mcp.Wire.McpResponseSize.From(result).TotalBytes <= maxResponseBytes)
        {
            return result;
        }

        return BudgetTooSmall(maxResponseBytes);
    }

}
