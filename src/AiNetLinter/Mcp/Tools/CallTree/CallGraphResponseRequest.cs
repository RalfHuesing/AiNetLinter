#nullable enable

using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools.SymbolGraph;

namespace AiNetLinter.Mcp.Tools.CallTree;

internal sealed record CallGraphResponseRequest(
    CallGraphPayload Graph,
    string? Format,
    string Direction,
    int RequestedDepth,
    int EffectiveDepth,
    int TopN,
    McpScopeType ScopeType,
    bool IncludeGenerated,
    int MaxResponseBytes,
    AssemblyNavigationSummary? AssemblyNavigation = null);
