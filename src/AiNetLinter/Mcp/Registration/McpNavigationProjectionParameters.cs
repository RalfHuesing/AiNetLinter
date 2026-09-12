#nullable enable

using AiNetLinter.Mcp.Projects;

namespace AiNetLinter.Mcp.Registration;

internal sealed record McpNavigationProjectionParameters(
    AnalysisTarget? Target,
    string OperationStatus,
    string Completeness,
    string? Hint = null,
    string? Code = null,
    string? TargetPath = null);
