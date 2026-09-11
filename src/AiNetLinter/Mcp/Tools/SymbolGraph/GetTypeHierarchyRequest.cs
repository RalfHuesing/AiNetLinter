#nullable enable

using System.Threading;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Scope;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal sealed record GetTypeHierarchyRequest(
    ISolutionStateProvider State,
    string? SymbolIdentifier,
    int MaxResults,
    McpScopeType ScopeType,
    bool IncludeGenerated,
    CancellationToken CancellationToken,
    int MaxResponseBytes = GetTypeHierarchyTool.DefaultMaxResponseBytes);
