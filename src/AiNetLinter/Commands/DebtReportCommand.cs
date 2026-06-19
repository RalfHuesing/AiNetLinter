#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
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
    internal static async Task<int> RunAsync(LinterArgs args)
    {
        LinterConfig? config = null;
        IReadOnlyCollection<RuleViolation>? violations = null;

        if (!string.IsNullOrWhiteSpace(args.ConfigPath))
        {
            config = LinterConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: false);
            if (config != null)
            {
                string? rulesJsonContent = null;
                if (!string.IsNullOrEmpty(args.ConfigPath) && File.Exists(args.ConfigPath))
                {
                    rulesJsonContent = File.ReadAllText(args.ConfigPath, System.Text.Encoding.UTF8);
                }
                var engine = new LinterEngine(config, rulesJsonContent);
                violations = await engine.RunAsync(args.TargetPath, args.NoCache, args.CacheTtlMinutes);
            }
        }

        var report = await DebtReportBuilder.BuildAsync(args.TargetPath, violations);
        Console.WriteLine(report);
        return 0;
    }
}
