#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiNetLinter.Mcp.Registration;
using AiNetLinter.Mcp.Wire;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

/// <summary>Projects find-symbol responses by complete symbol entries after navigation is present.</summary>
internal static class FindSymbolResponseBudget
{
    internal static CallToolResult Apply(CallToolResult result, int maxResponseBytes)
    {
        var minimumResponseBytes = McpResponseSize.From(result).TotalBytes;
        if (minimumResponseBytes <= maxResponseBytes) return result;
        return McpToolResults.Error(
            LinterErrorCodes.ResponseBudgetTooSmall,
            $"maxResponseBytes={maxResponseBytes} ist zu klein für die fachliche Mindestprojektion.",
            new McpErrorParameters(
                Hint: "maxResponseBytes erhöhen; die Antwort wird nur an vollständigen Symbol-Entries gekürzt.",
                FieldPath: "$.maxResponseBytes",
                RequestedBytes: maxResponseBytes,
                MinimumResponseBytes: minimumResponseBytes));
    }

}
