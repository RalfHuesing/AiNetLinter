#nullable enable

namespace AiNetLinter.Mcp.Tools.Common;

/// <summary>Gemeinsame Wire-Budget-Grenzen fuer strukturierte MCP-Antworten.</summary>
internal static class McpResponseBudgetLimits
{
    /// <summary>Obergrenze fuer jedes explizite maxResponseBytes-Feld.</summary>
    internal const int MaxBytes = 64 * 1024;

    /// <summary>
    /// Mindestbudget fuer Projektionen, die einen strukturierten Status-/Truncation-
    /// Envelope mitliefern. Kleinere positive Werte werden vor dem Dispatch abgelehnt,
    /// damit kein unmarkierter Header-Schnipsel entsteht.
    /// </summary>
    internal const int MinimumStructuredBytes = 512;
}
