#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using AiNetLinter.Baseline;
using AiNetLinter.Cli;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Models;
using AiNetLinter.Output;
using AiNetLinter.Scope;
using AiNetLinter.Suppression;

namespace AiNetLinter.Commands;

/// <summary>
/// Führt den Standard-Lint-Audit (mit und ohne Baseline) aus.
/// </summary>
internal static class AuditCommand
{
    private const string FixedCountMessageFormat = "[INFO]: {0} einfache Regelverstoesse wurden automatisch behoben.";

    /// <summary>
    /// Einstiegspunkt für den Audit-Befehl.
    /// </summary>
    internal static async Task<int> RunAsync(LinterArgs args)
    {
        var config = LinterConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: true);
        if (config == null)
        {
            return 1;
        }

        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.Initialize(config.Global.EnablePerformanceProfiling);
        LinterLogger.LogStart(args.Verbose, args.ConfigPath!, args.TargetPath);

        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.StartPhase("WorkspaceLoading");
        using var catalog = await SourceFileCatalog.LoadAsync(args.TargetPath);
        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.StopPhase("WorkspaceLoading");

        if (args.BaselinePath != null)
            return await RunAuditWithBaselineAsync(args, config, catalog);

        // Optimierter Pfad ohne Baseline (Analyse läuft einmalig vor optionalen Ausgaben)
        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.StartPhase("AutoFix");
        var (currentCatalog2, needsDispose2) = await ApplyAutoFixIfNeededAsync(catalog, config, args);
        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.StopPhase("AutoFix");
        try
        {
            AiNetLinter.Diagnostics.PerformanceProfiler.Instance.StartPhase("DocumentAnalysis");
            string? rulesJsonContent = LoadRulesJsonContent(args.ConfigPath);
            var engine = new LinterEngine(config, rulesJsonContent);
            var violations = await engine.RunAsync(currentCatalog2, args.NoCache, args.CacheTtlMinutes);
            AiNetLinter.Diagnostics.PerformanceProfiler.Instance.StopPhase("DocumentAnalysis");

            AiNetLinter.Diagnostics.PerformanceProfiler.Instance.StartPhase("OptionalOutputs");
            await GenerateOptionalOutputsAsync(currentCatalog2.Solution, args, config, violations);
            AiNetLinter.Diagnostics.PerformanceProfiler.Instance.StopPhase("OptionalOutputs");

            var outputRoot = OutputRootResolver.Resolve(args.TargetPath);
            var scoped = ApplyScopeFilters(violations, args, outputRoot, onlyChangedFiles: []);
            var exitCode = WriteViolationsAndExit(scoped, outputRoot, config);

            AiNetLinter.Diagnostics.PerformanceProfiler.Instance.WriteReport(args.TargetPath, currentCatalog2.Solution.FilePath, args.ConfigPath);
            return exitCode;
        }
        finally
        {
            if (needsDispose2) currentCatalog2.Dispose();
        }
    }

    private static async Task<int> RunAuditWithBaselineAsync(LinterArgs args, LinterConfig config, SourceFileCatalog catalog)
    {
        // Baseline-Pfad bleibt unverändert, um Regressionsrisiken zu vermeiden.
        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.StartPhase("OptionalOutputs");
        await GenerateOptionalOutputsAsync(catalog.Solution, args, config);
        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.StopPhase("OptionalOutputs");

        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.StartPhase("AutoFix");
        var (currentCatalog, needsDispose) = await ApplyAutoFixIfNeededAsync(catalog, config, args);
        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.StopPhase("AutoFix");
        try
        {
            var exitCode = await AuditWithBaselineAsync(args, config, currentCatalog);
            AiNetLinter.Diagnostics.PerformanceProfiler.Instance.WriteReport(args.TargetPath, currentCatalog.Solution.FilePath, args.ConfigPath);
            return exitCode;
        }
        finally
        {
            if (needsDispose) currentCatalog.Dispose();
        }
    }

    private static async Task<int> AuditWithBaselineAsync(LinterArgs args, LinterConfig config, SourceFileCatalog catalog)
    {
        BaselineFile storedBaseline;
        try
        {
            storedBaseline = BaselineReader.Read(args.BaselinePath!);
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException)
        {
            Console.Error.WriteLine($"[ERROR]: {ex.Message}");
            return 1;
        }

        var outputRoot = OutputRootResolver.Resolve(args.TargetPath);
        var currentChecksums = catalog.ComputeChecksums(outputRoot);
        var comparison = BaselineComparer.Compare(storedBaseline, currentChecksums);

        string? rulesJsonContent = null;
        if (!string.IsNullOrEmpty(args.ConfigPath) && File.Exists(args.ConfigPath))
        {
            rulesJsonContent = await File.ReadAllTextAsync(args.ConfigPath, Encoding.UTF8);
        }

        var engine = new LinterEngine(config, rulesJsonContent);
        var violations = await engine.RunAsync(catalog, args.NoCache, args.CacheTtlMinutes);
        var filtered = BaselineViolationFilter.Filter(violations, comparison.ChangedFiles, outputRoot);

        if (comparison.HasAnyChange)
        {
            BaselineWriter.Write(args.BaselinePath!, currentChecksums);
            LinterLogger.LogBaselineUpdate(args.Verbose, comparison);
        }

        var scoped = ApplyScopeFilters(filtered, args, outputRoot, comparison.ChangedFiles);
        return WriteViolationsAndExit(scoped, outputRoot, config);
    }

    internal static async Task GenerateOptionalOutputsAsync(
        Solution solution,
        LinterArgs args,
        LinterConfig config,
        IReadOnlyCollection<RuleViolation>? violations = null)
    {
        if (args.GraphPath != null)
        {
            await TryGenerateCodegraphAsync(solution, args.GraphPath, args.Verbose);
        }

        if (args.PlaybookPath != null)
        {
            await TryGeneratePlaybookAsync(solution, args.PlaybookPath, new PlaybookOptions(args.Verbose, config, args.ConfigPath ?? "rules.json", violations));
        }

        if (args.SyncCursorRules)
        {
            TrySyncCursorRules(args.TargetPath, config, args.Verbose);
        }
    }

    private static void TrySyncCursorRules(string targetPath, LinterConfig config, bool verbose)
    {
        try
        {
            if (verbose)
            {
                Console.WriteLine("[INFO]: Synchronisiere Cursor-Regeln (.mdc)...");
            }
            CursorRulesGenerator.Sync(targetPath, config, verbose);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR]: Fehler beim Synchronisieren der Cursor-Regeln: {ex.Message}");
        }
    }

    private static async Task<(SourceFileCatalog Catalog, bool NeedsDispose)> ApplyAutoFixIfNeededAsync(
        SourceFileCatalog catalog, LinterConfig config, LinterArgs args)
    {
        if (!args.Fix)
        {
            return (catalog, false);
        }

        string? rulesJsonContent = null;
        if (!string.IsNullOrEmpty(args.ConfigPath) && File.Exists(args.ConfigPath))
        {
            rulesJsonContent = await File.ReadAllTextAsync(args.ConfigPath, Encoding.UTF8);
        }

        var engine = new LinterEngine(config, rulesJsonContent);
        var initialViolations = await engine.RunAsync(catalog, args.NoCache, args.CacheTtlMinutes);
        var (fixedCount, updatedSolution) = await LinterAutoFixer.FixAsync(
            catalog.Solution, initialViolations, new FixOptions(args.Verbose, args.Check));

        if (args.Check)
        {
            if (fixedCount > 0)
            {
                Console.WriteLine($"[DRY-RUN]: {fixedCount} einfache Regelverstoesse wuerden automatisch behoben.");
            }
            return (catalog, false);
        }

        if (fixedCount > 0)
        {
            Console.WriteLine(FixedCountMessageFormat, fixedCount);
            return (catalog.WithUpdatedSolution(updatedSolution), true);
        }

        return (catalog, false);
    }

    private static async Task TryGenerateCodegraphAsync(Solution solution, string graphPath, bool verbose)
    {
        try
        {
            if (verbose)
            {
                Console.WriteLine($"[INFO]: Generiere Codegraph unter: {graphPath}");
            }
            await CodegraphGenerator.GenerateAsync(solution, graphPath);
            if (verbose)
            {
                Console.WriteLine("[INFO]: Codegraph erfolgreich generiert.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR]: Fehler beim Generieren des Codegraphen: {ex.Message}");
        }
    }

    private static async Task TryGeneratePlaybookAsync(
        Solution solution,
        string playbookPath,
        PlaybookOptions options)
    {
        try
        {
            if (options.Verbose)
            {
                Console.WriteLine($"[INFO]: Generiere Repo-Playbook unter: {playbookPath}");
            }
            await RepoPlaybookGenerator.GenerateAsync(solution, playbookPath, options);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR]: Fehler beim Generieren des Repo-Playbooks: {ex.Message}");
        }
    }

    internal static string? LoadRulesJsonContent(string? configPath)
    {
        if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
        {
            return null;
        }
        return File.ReadAllText(configPath, Encoding.UTF8);
    }

    private static IReadOnlyCollection<RuleViolation> ApplyScopeFilters(
        IReadOnlyCollection<RuleViolation> violations,
        LinterArgs args,
        string outputRoot,
        IReadOnlyCollection<string> onlyChangedFiles)
    {
        var gitFiles = args.GitSince != null
            ? GitChangedFilesResolver.ResolveSince(args.TargetPath, args.GitSince)
            : [];

        var changedFiles = args.OnlyChanged ? onlyChangedFiles : [];

        return ViolationScopeFilter.Apply(violations, new ViolationScopeOptions
        {
            WaveReady = args.WaveReady,
            GitChangedFiles = gitFiles,
            OnlyChangedFiles = changedFiles,
        }, outputRoot);
    }

    private static int WriteViolationsAndExit(
        IReadOnlyCollection<RuleViolation> violations,
        string outputRoot,
        LinterConfig config)
    {
        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.StartPhase("OutputWriting");
        try
        {
            if (violations.Count == 0)
            {
                Console.WriteLine("OK");
                return 0;
            }

            var hasError = RuleMetadataRegistry.HasErrorSeverity(violations, config);
            Console.WriteLine(ViolationMarkdownFormatter.Format(violations, outputRoot, config));
            return hasError ? 1 : 0;
        }
        finally
        {
            AiNetLinter.Diagnostics.PerformanceProfiler.Instance.StopPhase("OutputWriting");
        }
    }
}
