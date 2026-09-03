#nullable enable

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal readonly record struct ResponseBudgetOptions(
    int ResponseBudgetBytes = AssemblyAnalysisResponseLimits.DefaultResponseBytes,
    int CursorOffset = 0);
