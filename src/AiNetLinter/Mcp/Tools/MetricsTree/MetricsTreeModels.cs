#nullable enable

using AiNetLinter.Mcp.Tools.MetricsTree;

namespace AiNetLinter.Mcp.Tools.MetricsTree;

internal sealed record MetricsTreePayload(
    string Mode,
    string? Root,
    int Depth,
    int TopN,
    MetricsTreeNode Tree);
