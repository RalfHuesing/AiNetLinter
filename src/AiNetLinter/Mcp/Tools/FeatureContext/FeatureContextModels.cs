#nullable enable

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Tools.MetricsLookup;

namespace AiNetLinter.Mcp.Tools.FeatureContext;

/// <summary>
/// Optionen fuer den Aufruf des MCP-Tools <c>get_feature_context</c>.
/// <c>symbolIdentifier</c> ist der einzige fachliche Identifikator.
/// </summary>
internal sealed record FeatureContextOptions(
    string? SymbolIdentifier = null,
    bool IncludeCallers = true,
    bool IncludeTests = true,
    bool IncludeMetrics = true,
    bool IncludeViolations = true,
    int MaxCallers = 10,
    int MaxTests = 10
)
{
    public string EffectiveSymbol => SymbolIdentifier ?? string.Empty;
}

/// <summary>
/// Deklarationsdetails des Ziel-Symbols.
/// </summary>
internal sealed record SymbolDeclarationDto(
    string Name,
    string Kind,
    string Accessibility,
    string FilePath,
    int StartLine,
    int EndLine,
    int LineCount,
    string? ContainerType,
    string? ReturnType,
    IReadOnlyList<string> Parameters,
    string? DocCommentId
);

/// <summary>
/// Aufrufer-Bericht fuer das Ziel-Symbol.
/// </summary>
internal sealed record CallersReportDto(
    int TotalCallers,
    IReadOnlyList<CallSiteEntry> CallSites,
    bool IsTruncated,
    IReadOnlyList<string>? TruncatedBy = null,
    string Semantics = FeatureContextSemantics.StaticCallSites,
    string Completeness = FeatureContextStatus.Complete,
    string? NextStep = null
);

/// <summary>
/// Bericht statischer Testkandidaten fuer das Ziel-Symbol. Dies ist kein
/// Laufzeit- oder Coverage-Nachweis.
/// </summary>
internal sealed record StaticTestContextReportDto(
    int TotalMatchingTests,
    int TotalTestFiles,
    IReadOnlyList<StaticTestCandidateFileDto> TestFiles,
    bool IsTruncated,
    int DisplayedTestMethods = 0,
    IReadOnlyList<string>? TruncatedBy = null,
    string Completeness = FeatureContextStatus.Complete,
    string EvidenceBoundary = FeatureContextSemantics.StaticTestCandidates,
    string? NextStep = null
);

/// <summary>
/// DTO fuer eine statisch zugeordnete Testdatei.
/// </summary>
internal sealed record StaticTestCandidateFileDto(
    string FilePath,
    string TestClassName,
    string Category,
    string MatchReason,
    IReadOnlyList<string> TestMethods,
    int TotalClassTests,
    int TotalMatchingMethods = 0
);

/// <summary>
/// Linter-Violations-Bericht fuer die Zieldatei/das Zielsymbol.
/// </summary>
internal sealed record ViolationsReportDto(
    int TotalViolationsOnFile,
    int ViolationsOnSymbol,
    IReadOnlyList<ViolationItemDto> Violations,
    bool IsTruncated,
    string Status = FeatureContextStatus.Complete,
    string? ReasonCode = null,
    IReadOnlyList<string>? TruncatedBy = null,
    string? NextStep = null
);

/// <summary>
/// Einzelne Linter-Violation.
/// </summary>
internal sealed record ViolationItemDto(
    string RuleId,
    string Message,
    int Line,
    bool IsDirectlyOnSymbol
);

/// <summary>
/// Vollstaendige strukturierte Payload fuer <c>get_feature_context</c> StructuredContent.
/// </summary>
internal sealed record FeatureContextPayload(
    SymbolDeclarationDto Declaration,
    MetricsLookupResultDto? Metrics,
    CallersReportDto? Callers,
    [property: JsonPropertyName("testContext")] StaticTestContextReportDto? Tests,
    ViolationsReportDto? Violations,
    string? MetricsStatus = null,
    string Completeness = FeatureContextStatus.Complete,
    string? NextStep = null
);

internal static class FeatureContextStatus
{
    internal const string Complete = "complete";
    internal const string Empty = "empty";
    internal const string Partial = "partial";
    internal const string Truncated = "truncated";
    internal const string NotConfigured = "not_configured";
    internal const string NotDecidable = "not_decidable";
    internal const string Error = "error";
    internal const string NotApplicable = "not_applicable";
}

internal static class FeatureContextReasonCodes
{
    internal const string ViolationsScanFailed = "violations-scan-failed";
    internal const string SourceFileUnavailable = "source-file-unavailable";
    internal const string RulesNotConfigured = "rules-not-configured";
    internal const string OperationCanceled = "operation-canceled";
}

internal static class FeatureContextSemantics
{
    internal const string StaticCallSites = "static-references/call-sites";
    internal const string StaticTestCandidates = "static-test-candidates-only";
}
