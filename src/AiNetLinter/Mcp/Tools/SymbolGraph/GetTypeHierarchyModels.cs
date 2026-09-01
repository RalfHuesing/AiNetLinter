#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal sealed record TypeHierarchyPayload(
    string TypeName,
    IReadOnlyList<string> BaseTypes,
    IReadOnlyList<string> Interfaces,
    string SubtypeHeading,
    IReadOnlyList<string> Subtypes,
    int TotalSubtypeCount,
    int ShownSubtypeCount,
    bool SubtypesTruncated,
    IReadOnlyList<string> SubtypesTruncatedBy,
    IReadOnlyList<string> DiRegistrations);
