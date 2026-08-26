#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Mcp;

internal sealed record McpLaunchSpec(
    string Command,
    IReadOnlyList<string> Arguments,
    bool IsRuntimeResolved);
