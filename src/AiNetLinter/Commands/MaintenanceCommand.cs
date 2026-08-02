#nullable enable

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Cli;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Output;
using AiNetLinter.Suppression;

namespace AiNetLinter.Commands;

/// <summary>
/// Führt Wartungsaktionen wie das Verwalten von Baselines und das Injizieren/Entfernen von Deaktivierungskommentaren aus.
/// </summary>
internal static class MaintenanceCommand
{
    /// <summary>
    /// Versucht einen Wartungsmodus auszuführen. Gibt <c>null</c> zurück, wenn kein Wartungsmodus aktiv ist.
    /// </summary>
    internal static async Task<int?> TryRunAsync(LinterArgs args, CancellationToken ct = default, ILintConsole? console = null)
    {
        var c = console ?? LinterConsole.Instance;

        if (args.CreateBaselinePath != null)
        {
            return await CreateBaselineAsync(args, ct, c);
        }

        if (args.AddDisableAll)
        {
            return await AddDisableAllAsync(args, ct, c);
        }

        if (args.RemoveDisableAll)
        {
            return await RemoveDisableAllAsync(args, c);
        }

        return null;
    }

    private static async Task<int> AddDisableAllAsync(LinterArgs args, CancellationToken ct, ILintConsole c)
    {
        var config = ConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: true);
        if (config == null)
        {
            return 1;
        }

        LinterLogger.LogDisableAllInject(args.Verbose, args.TargetPath, c);

        string? rulesJsonContent = ConfigLoader.LoadRulesJsonContent(args.ConfigPath);
        var engine = new LinterEngine(config, rulesJsonContent);
        var violations = await engine.RunAsync(args.TargetPath, args.NoCache, args.CacheTtlMinutes, ct);
        var outputRoot = OutputRootResolver.Resolve(args.TargetPath);
        var violatingPaths = ViolationPathResolver.ResolveAbsolutePaths(violations, outputRoot);
        var result = DisableAllCommentInjector.InjectIntoFiles(violatingPaths);

        if (args.Verbose)
        {
            c.WriteLine($"[INFO]: Audit fand {violations.Count} Verstoesse in {result.CandidateFiles} Dateien.");
            c.WriteLine($"[INFO]: {result.ModifiedFiles} Dateien geaendert, {result.SkippedFiles} uebersprungen.");
        }

        c.WriteLine("OK");
        return 0;
    }

    private static async Task<int> RemoveDisableAllAsync(LinterArgs args, ILintConsole c)
    {
        LinterLogger.LogDisableAllRemove(args.Verbose, args.TargetPath, c);

        var result = await DisableAllCommentRemover.RemoveAsync(args.TargetPath);

        if (args.Verbose)
        {
            c.WriteLine($"[INFO]: {result.ModifiedFiles} von {result.ScannedFiles} Dateien bereinigt.");
        }

        c.WriteLine("OK");
        return 0;
    }

    private static async Task<int> CreateBaselineAsync(LinterArgs args, CancellationToken ct, ILintConsole c)
    {
        LinterLogger.LogBaselineCreate(args.Verbose, args.TargetPath, args.CreateBaselinePath!, c);

        using var catalog = await SourceFileCatalog.LoadAsync(args.TargetPath, ct);
        var outputRoot = OutputRootResolver.Resolve(args.TargetPath);

        var configPath = args.ConfigPath;
        if (string.IsNullOrWhiteSpace(configPath))
        {
            var targetDir = Directory.Exists(args.TargetPath)
                ? args.TargetPath
                : Path.GetDirectoryName(args.TargetPath);
            if (!string.IsNullOrEmpty(targetDir))
            {
                var candidate = Path.Combine(targetDir, "rules.json");
                if (File.Exists(candidate))
                {
                    configPath = candidate;
                }
            }
        }

        var config = ConfigLoader.TryLoadConfig(configPath, isRequired: false);
        var checksums = catalog.ComputeChecksums(outputRoot, config);

        BaselineWriter.Write(args.CreateBaselinePath!, checksums);

        if (args.Verbose)
        {
            c.WriteLine($"[INFO]: Baseline mit {checksums.Count} Dateien geschrieben.");
        }

        c.WriteLine("OK");
        return 0;
    }
}
