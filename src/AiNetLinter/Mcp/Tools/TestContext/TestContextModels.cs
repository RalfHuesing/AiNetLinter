#nullable enable

using System.Collections.Generic;
using AiNetLinter.Core;

namespace AiNetLinter.Mcp.Tools.TestContext;

/// <summary>
/// Optionen fuer den Aufruf des MCP-Tools <c>get_test_context</c>.
/// <c>symbolIdentifier</c> ist der einzige fachliche Identifikator.
/// </summary>
internal sealed record TestContextOptions(
    string? SymbolIdentifier = null,
    int MaxResults = 30
)
{
    public string EffectiveSymbol => SymbolIdentifier ?? string.Empty;
}

/// <summary>
/// Vollstaendige strukturierte Payload fuer <c>get_test_context</c> StructuredContent.
/// </summary>
public sealed record TestContextPayload(
    string TargetSymbol,
    string TargetKind,
    string TargetFilePath,
    int TotalMatchingTests,
    int TotalTestFiles,
    IReadOnlyList<TestFileCoverageResult> TestFiles,
    IReadOnlyList<string> RecommendedTestCommands,
    bool IsUntested,
    bool IsTruncated,
    string? SuggestedTestFilePath = null
);
