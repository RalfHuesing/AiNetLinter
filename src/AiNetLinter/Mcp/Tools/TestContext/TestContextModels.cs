#nullable enable

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Scope;

namespace AiNetLinter.Mcp.Tools.TestContext;

/// <summary>
/// Optionen fuer den Aufruf des MCP-Tools <c>get_test_context</c>.
/// <c>symbolIdentifier</c> ist der einzige fachliche Identifikator.
/// </summary>
internal sealed record TestContextOptions(
    string? SymbolIdentifier = null,
    int MaxResults = 30,
    int MaxResponseBytes = TestContextResponseBudget.DefaultMaxResponseBytes,
    McpScopeInput Scope = default
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
    IReadOnlyList<StaticTestCandidateFile> TestFiles,
    IReadOnlyList<string> RecommendedTestCommands,
    bool IsUntested,
    bool IsTruncated,
    string? SuggestedTestFilePath = null,
    string Completeness = "complete",
    int ReturnedTestFiles = 0,
    int ReturnedTestMethods = 0,
    IReadOnlyList<string>? TruncatedBy = null,
    string EvidenceBoundary = "static-test-candidates-only",
    string? NextStep = null,
    McpScopeMetadata? Scope = null,
    int ExcludedTestFileCount = 0,
    string? Id = null
);

/// <summary>
/// Wire-Modell fuer einen statischen Testkandidaten. Die Zuordnung basiert auf
/// sichtbaren Source-Heuristiken und ist kein Laufzeit- oder Coverage-Nachweis.
/// </summary>
public sealed record StaticTestCandidateFile(
    string FilePath,
    string TestClassName,
    string Category,
    string MatchReason,
    IReadOnlyList<string> TestMethods,
    int TotalClassTests,
    string? ProjectDirectory = null,
    [property: JsonPropertyName("evidenceKind")] string EvidenceKind = "typeNamingConvention",
    string Confidence = "low",
    [property: JsonPropertyName("totalTestCount")] int TotalTestCount = 0,
    [property: JsonPropertyName("testClassNames")] IReadOnlyList<string>? TestClassNames = null,
    string ScopeType = "unknown",
    string SourceKind = "editable"
);
