#nullable enable

using AiNetLinter.Mcp.Scope;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal sealed record HierarchyBuildOptions(
    bool AbsolutePaths = false,
    AnalysisSymbolIdentity? HandoffIdentity = null,
    McpScopeType ScopeType = McpScopeType.All,
    bool IncludeGenerated = false,
    McpScopeClassifier? ScopeClassifier = null);
