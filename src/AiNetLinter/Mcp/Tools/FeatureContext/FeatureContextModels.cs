#nullable enable

using System.Collections.Generic;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Tools.MetricsLookup;

namespace AiNetLinter.Mcp.Tools.FeatureContext;

/// <summary>
/// Optionen fuer den Aufruf des MCP-Tools <c>get_feature_context</c>.
/// </summary>
internal sealed record FeatureContextOptions(
    string Symbol,
    bool IncludeCallers = true,
    bool IncludeTests = true,
    bool IncludeMetrics = true,
    bool IncludeViolations = true,
    int MaxCallers = 10,
    int MaxTests = 10
);

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
    bool IsTruncated
);

/// <summary>
/// Testabdeckungs-Bericht fuer das Ziel-Symbol.
/// </summary>
internal sealed record TestCoverageReportDto(
    int TotalMatchingTests,
    int TotalTestFiles,
    IReadOnlyList<TestFileCoverageDto> TestFiles,
    bool IsTruncated
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
    int TotalClassTests
);

/// <summary>
/// Linter-Violations-Bericht fuer die Zieldatei/das Zielsymbol.
/// </summary>
internal sealed record ViolationsReportDto(
    int TotalViolationsOnFile,
    int ViolationsOnSymbol,
    IReadOnlyList<ViolationItemDto> Violations,
    bool IsTruncated
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
