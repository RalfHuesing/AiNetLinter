#nullable enable

using AiNetLinter.Mcp.Scope;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal sealed record FindRegistrationsRequest(
    Solution Solution,
    INamedTypeSymbol Type,
    McpScopeType ScopeType,
    bool IncludeGenerated,
    McpScopeClassifier Classifier);
