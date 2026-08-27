#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Mcp.Tools.FileStructure;

internal sealed record GetFileTreeInput(
    string Root,
    string View,
    IReadOnlyList<string>? IncludeExtensions,
    string? FileFilter,
    IReadOnlyList<string>? ExcludePatterns,
    int? MaxDepth,
    int TreeDepth,
    int MaxResults,
    string SortBy,
    bool IncludeMetadata,
    bool IncludeLineCount);
