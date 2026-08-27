#nullable enable

using AiNetLinter.Mcp.Tools.FileStructure;

namespace AiNetLinter.FastTests.Mcp.Tools.FileStructure;

internal static class GetFileTreeTestData
{
    internal static GetFileTreeInput Input() => new(
        Root: ".",
        View: "files",
        IncludeExtensions: null,
        FileFilter: null,
        ExcludePatterns: null,
        MaxDepth: null,
        TreeDepth: 2,
        MaxResults: 200,
        SortBy: "path",
        IncludeMetadata: true,
        IncludeLineCount: false);
}
