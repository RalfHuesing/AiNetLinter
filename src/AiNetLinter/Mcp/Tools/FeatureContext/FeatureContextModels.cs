#nullable enable

using System.Collections.Generic;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Tools.MetricsLookup;

namespace AiNetLinter.Mcp.Tools.FeatureContext;

/// <summary>
/// Optionen fuer den Aufruf des MCP-Tools <c>get_feature_context</c>.
/// <c>symbolIdentifier</c> ist die primaere Konvention; <c>symbol</c> bleibt als kompatibler Alias
/// unterstuetzt. Die Record-Reihenfolge bleibt fuer bestehende interne Aufrufer kompatibel.
/// </summary>
internal sealed record FeatureContextOptions(
    string? Symbol = null,
    string? SymbolIdentifier = null,
    bool IncludeCallers = true,
    bool IncludeTests = true,
    bool IncludeMetrics = true,
    bool IncludeViolations = true,
    int MaxCallers = 10,
    int MaxTests = 10
)
{
    public string EffectiveSymbol => !string.IsNullOrWhiteSpace(SymbolIdentifier)
        ? SymbolIdentifier
        : (Symbol ?? string.Empty);
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
    string Semantics = FeatureContextSemantics.StaticCallSites
);

/// <summary>
/// Testabdeckungs-Bericht fuer das Ziel-Symbol.
/// </summary>
internal sealed record TestCoverageReportDto(
    int TotalMatchingTests,
    int TotalTestFiles,
    IReadOnlyList<TestFileCoverageDto> TestFiles,
    bool IsTruncated,
    int DisplayedTestMethods = 0,
    IReadOnlyList<string>? TruncatedBy = null
);

/// <summary>
/// DTO fuer eine zugeordnete Testdatei.
/// </summary>
internal sealed record TestFileCoverageDto(
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
    IReadOnlyList<string>? TruncatedBy = null
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
    TestCoverageReportDto? Tests,
    ViolationsReportDto? Violations
);

internal static class FeatureContextStatus
{
    internal const string Complete = "complete";
    internal const string Truncated = "truncated";
    internal const string Unavailable = "unavailable";
    internal const string Failed = "failed";
    internal const string NotApplicable = "notApplicable";
}

internal static class FeatureContextReasonCodes
{
    internal const string ViolationsScanFailed = "violations-scan-failed";
    internal const string SourceFileUnavailable = "source-file-unavailable";
    internal const string OperationCanceled = "operation-canceled";
}

internal static class FeatureContextSemantics
{
    internal const string StaticCallSites = "static-references/call-sites";
}
