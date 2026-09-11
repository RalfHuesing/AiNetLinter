#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace AiNetLinter.Mcp.Validation;

/// <summary>
/// Kanonische Wirewerte fuer MCP-Enumargumente, die in Registrierung und Toollogik geteilt werden.
/// </summary>
internal static class McpEnumValues
{
    internal const string AssemblyDetailLevelCompact = "compact";
    internal const string AssemblyDetailLevelStandard = "standard";
    internal const string AssemblyDetailLevelFull = "full";

    internal static readonly IReadOnlyList<string> FindSymbolKinds =
    [
        "class", "interface", "record", "record class", "record struct", "struct", "enum", "delegate", "method", "property",
    ];

    internal static readonly IReadOnlyList<string> AssemblyDetailLevels =
    [
        AssemblyDetailLevelCompact,
        AssemblyDetailLevelStandard,
        AssemblyDetailLevelFull,
    ];

    internal static string FindSymbolKindsHint => string.Join(", ", FindSymbolKinds);

    internal static string AssemblyDetailLevelsHint => string.Join("/", AssemblyDetailLevels);

    internal static bool IsFindSymbolKind(string value) =>
        FindSymbolKinds.Contains(value, StringComparer.OrdinalIgnoreCase);

    internal static string? NormalizeAssemblyDetailLevel(string? value) =>
        value is null
            ? null
            : AssemblyDetailLevels.FirstOrDefault(candidate =>
                string.Equals(candidate, value.Trim(), StringComparison.OrdinalIgnoreCase));
}
