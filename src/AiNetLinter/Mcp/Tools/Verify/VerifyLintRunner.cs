#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Models;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.Verify;

/// <summary>Führt die verbindliche Lint-Quelle des Verify-Gates genau einmal aus.</summary>
internal static class VerifyLintRunner
{
    internal static async Task<IReadOnlyCollection<RuleViolation>> RunAsync(
        Solution solution,
        ILinterEngineConfig config,
        ILintConsole console,
        CancellationToken ct)
    {
        var engine = new LinterEngine(
            config: (Config)config,
            configContent: null,
            profiler: null,
            console: console);
        return await engine.RunAsync(solution, noCache: true, cacheTtlMinutes: 0, ct);
    }
}
