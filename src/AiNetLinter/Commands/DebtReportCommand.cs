#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Models;
using AiNetLinter.Output;

namespace AiNetLinter.Commands;

/// <summary>
/// Führt den Technical Debt Report des Linters aus.
/// </summary>
internal static class DebtReportCommand
{
    /// <summary>
    /// Führt die Audit-Erfassung und den Tech-Debt-Berichtsaufbau aus.
    /// </summary>
    internal static async Task<int> RunAsync(LinterArgs args, CancellationToken ct = default, ILintConsole? console = null)
    {
        var c = console ?? LinterConsole.Instance;
        Config? config = null;
        IReadOnlyCollection<RuleViolation>? violations = null;

        if (!string.IsNullOrWhiteSpace(args.ConfigPath))
        {
            config = ConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: false);
            if (config != null)
            {
                string? rulesJsonContent = ConfigLoader.LoadRulesJsonContent(args.ConfigPath);
                var engine = new LinterEngine(config, rulesJsonContent);
                violations = await engine.RunAsync(args.TargetPath, args.NoCache, args.CacheTtlMinutes, ct);
            }
        }

        var report = await DebtReportBuilder.BuildAsync(args.TargetPath, violations);
        c.WriteLine(report);
        return 0;
    }
}
