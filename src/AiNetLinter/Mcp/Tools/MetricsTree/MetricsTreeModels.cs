#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace AiNetLinter.Mcp.Tools.MetricsTree;

internal sealed record MetricsTreePayload(
    string Mode,
    string? Root,
    int Depth,
    int TopN,
    MetricsTreeNode Tree,
    int TotalCount,
    int ReturnedCount,
    MetricsTreeCompleteness Completeness,
    MetricsTreeNext Next);

internal sealed record MetricsTreeCompleteness(
    string Status,
    int TotalCount,
    int ReturnedCount,
    bool Truncated,
    IReadOnlyList<string> TruncatedBy);

internal sealed record MetricsTreeNext(string Kind, string Reason);
