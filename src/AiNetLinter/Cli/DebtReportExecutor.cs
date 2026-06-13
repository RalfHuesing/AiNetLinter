#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Models;
using AiNetLinter.Output;

namespace AiNetLinter.Cli;

/// <summary>
/// Führt den Technical Debt Report des Linters aus.
/// </summary>
public static class DebtReportExecutor
{
    /// <summary>
    /// Führt die Audit-Erfassung und den Tech-Debt-Berichtsaufbau aus.
    /// </summary>
    public static async Task<int> RunDebtReportAsync(LinterArgs args)
    {
        LinterConfig? config = null;
        IReadOnlyCollection<RuleViolation>? violations = null;

        if (!string.IsNullOrWhiteSpace(args.ConfigPath))
        {
            config = LinterConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: false);
            if (config != null)
            {
                var engine = new LinterEngine(config);
                violations = await engine.RunAsync(args.TargetPath);
            }
        }

        var report = await DebtReportBuilder.BuildAsync(args.TargetPath, violations);
        Console.WriteLine(report);
        return 0;
    }
}
