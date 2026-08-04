#nullable enable

using System;
using System.Collections.Generic;

namespace AiNetLinter.Mcp;

/// <summary>
/// Parameter-Record fuer <see cref="McpCodeGraphServerRefresh.Run"/>. Buendelt die drei
/// zusaetzlichen Eingabewerte neben <c>current</c> und <c>solutionDir</c>, damit das
/// projektweite <c>MaxMethodParameterCount: 4</c>-Limit eingehalten wird und kuenftige
/// Sweep-/Refresh-Optionen additiv wachsen koennen.
/// </summary>
internal sealed record McpCodeGraphServerRefreshParameters(
    Dictionary<string, McpFileState> FileState,
    Action<string> WriteWarn,
    Func<bool> ShouldSweep);
