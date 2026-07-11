#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using AiNetLinter.Baseline;
using AiNetLinter.Cli;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Diagnostics;
using AiNetLinter.Generators;
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

    // Bündelt gemeinsame Parameter für private Audit-Methoden.
    private sealed record AuditRunContext(LinterArgs Args, Config Config, IPerformanceProfiler Profiler, ILintConsole Console);

    /// <summary>
    /// Einstiegspunkt für den Audit-Befehl.
    /// </summary>
    internal static async Task<int> RunAsync(LinterArgs args, CancellationToken ct = default, ILintConsole? console = null)
    {
        var c = console ?? LinterConsole.Instance;
        var config = ConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: true);
        if (config == null) return 1;

        IPerformanceProfiler profiler = config.Global.EnablePerformanceProfiling
            ? new PerformanceProfiler(true)
            : NullPerformanceProfiler.Instance;

        var ctx = new AuditRunContext(args, config, profiler, c);
        LinterLogger.LogStart(args.Verbose, args.ConfigPath!, args.TargetPath, c);

        profiler.StartPhase("WorkspaceLoading");
        using var catalog = await SourceFileCatalog.LoadAsync(args.TargetPath, ct);
        profiler.StopPhase("WorkspaceLoading");

        if (args.BaselinePath != null)
            return await RunAuditWithBaselineAsync(ctx, catalog, ct);

        // Optimierter Pfad ohne Baseline (Analyse läuft einmalig vor optionalen Ausgaben)
        profiler.StartPhase("AutoFix");
        var (currentCatalog2, needsDispose2) = await ApplyAutoFixIfNeededAsync(catalog, ctx, ct);
        profiler.StopPhase("AutoFix");
        try
        {
            profiler.StartPhase("DocumentAnalysis");
            string? rulesJsonContent = ConfigLoader.LoadRulesJsonContent(args.ConfigPath);
            var engine = new LinterEngine(config, rulesJsonContent, profiler, c, args);
            var violations = await engine.RunAsync(currentCatalog2, args.NoCache, args.CacheTtlMinutes, ct);
            profiler.StopPhase("DocumentAnalysis");

            profiler.StartPhase("OptionalOutputs");
            await GenerateOptionalOutputsAsync(currentCatalog2.Solution, ctx, violations);
            profiler.StopPhase("OptionalOutputs");

            var outputRoot = OutputRootResolver.Resolve(args.TargetPath);
            var scoped = ApplyScopeFilters(violations, args, outputRoot, onlyChangedFiles: []);
            var exitCode = WriteViolationsAndExit(scoped, outputRoot, ctx);

            profiler.WriteReport(args.TargetPath, currentCatalog2.Solution.FilePath, args.ConfigPath);
            return exitCode;
        }
        finally
        {
            if (needsDispose2) currentCatalog2.Dispose();
        }
    }

    private static async Task<int> RunAuditWithBaselineAsync(AuditRunContext ctx, SourceFileCatalog catalog, CancellationToken ct)
    {
        var profiler = ctx.Profiler;
        // Baseline-Pfad bleibt unverändert, um Regressionsrisiken zu vermeiden.
        profiler.StartPhase("OptionalOutputs");
        await GenerateOptionalOutputsAsync(catalog.Solution, ctx);
        profiler.StopPhase("OptionalOutputs");

        profiler.StartPhase("AutoFix");
        var (currentCatalog, needsDispose) = await ApplyAutoFixIfNeededAsync(catalog, ctx, ct);
        profiler.StopPhase("AutoFix");
        try
        {
            var exitCode = await AuditWithBaselineAsync(ctx, currentCatalog, ct);
            profiler.WriteReport(ctx.Args.TargetPath, currentCatalog.Solution.FilePath, ctx.Args.ConfigPath);
            return exitCode;
        }
        finally
        {
            if (needsDispose) currentCatalog.Dispose();
        }
    }

    private static async Task<int> AuditWithBaselineAsync(AuditRunContext ctx, SourceFileCatalog catalog, CancellationToken ct)
    {
        var (args, config, profiler, c) = ctx;

        BaselineFile storedBaseline;
        try
        {
            storedBaseline = BaselineReader.Read(args.BaselinePath!);
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException)
        {
            var code = ex is FileNotFoundException ? LinterErrorCodes.BaselineNotFound : LinterErrorCodes.BaselineInvalid;
            c.WriteError(LinterErrorFormatter.Format(code, ex.Message,
                context: args.BaselinePath,
                hint: "Baseline-Datei mit --create-baseline neu erzeugen."));
            return 1;
        }

        var outputRoot = OutputRootResolver.Resolve(args.TargetPath);
        var currentChecksums = catalog.ComputeChecksums(outputRoot, config, args);
        var comparison = BaselineComparer.Compare(storedBaseline, currentChecksums);

        string? rulesJsonContent = ConfigLoader.LoadRulesJsonContent(args.ConfigPath);

        var engine = new LinterEngine(config, rulesJsonContent, profiler, c, args);
        var violations = await engine.RunAsync(catalog, args.NoCache, args.CacheTtlMinutes, ct);
        var filtered = BaselineViolationFilter.Filter(violations, comparison.ChangedFiles, outputRoot);

        if (comparison.HasAnyChange)
        {
            BaselineWriter.Write(args.BaselinePath!, currentChecksums);
            LinterLogger.LogBaselineUpdate(args.Verbose, comparison, c);
        }

        var scoped = ApplyScopeFilters(filtered, args, outputRoot, comparison.ChangedFiles);
        return WriteViolationsAndExit(scoped, outputRoot, ctx);
    }

    private static async Task GenerateOptionalOutputsAsync(
        Solution solution,
        AuditRunContext ctx,
        IReadOnlyCollection<RuleViolation>? violations = null)
    {
        var (args, config, _, c) = ctx;

        if (args.PlaybookPath != null)
        {
            await TryGeneratePlaybookAsync(solution, args.PlaybookPath, new PlaybookOptions(args.Verbose, config, args.ConfigPath ?? "rules.json", violations), c);
        }

        if (args.SyncCursorRules)
        {
            TrySyncCursorRules(ctx);
        }
    }

    private static void TrySyncCursorRules(AuditRunContext ctx)
    {
        var (args, config, _, c) = ctx;
        try
        {
            if (args.Verbose)
            {
                c.WriteLine("[INFO]: Synchronisiere Cursor-Regeln (.mdc)...");
            }
            CursorRulesGenerator.Sync(new CursorRulesSyncOptions(
                args.TargetPath,
                config,
                args.Verbose,
                args.ConfigPath ?? "rules.json",
                args.CursorRulesPath));
        }
        catch (Exception ex)
        {
            c.WriteError($"[ERROR]: Fehler beim Synchronisieren der Cursor-Regeln: {ex.Message}");
        }
    }

    private static async Task<(SourceFileCatalog Catalog, bool NeedsDispose)> ApplyAutoFixIfNeededAsync(
        SourceFileCatalog catalog, AuditRunContext ctx, CancellationToken ct)
    {
        var (args, config, profiler, c) = ctx;

        if (!args.Fix) return (catalog, false);

        string? rulesJsonContent = ConfigLoader.LoadRulesJsonContent(args.ConfigPath);

        var engine = new LinterEngine(config, rulesJsonContent, profiler, c, args);
        var initialViolations = await engine.RunAsync(catalog, args.NoCache, args.CacheTtlMinutes, ct);
        var (fixedCount, updatedSolution) = await LinterAutoFixer.FixAsync(
            catalog.Solution, initialViolations, new FixOptions(args.Verbose, args.Check), c);

        if (args.Check)
        {
            if (fixedCount > 0)
            {
                c.WriteLine($"[DRY-RUN]: {fixedCount} einfache Regelverstoesse wuerden automatisch behoben.");
            }
            return (catalog, false);
        }

        if (fixedCount > 0)
        {
            c.WriteLine(string.Format(FixedCountMessageFormat, fixedCount));
            return (catalog.WithUpdatedSolution(updatedSolution), true);
        }

        return (catalog, false);
    }

    private static async Task TryGeneratePlaybookAsync(
        Solution solution,
        string playbookPath,
        PlaybookOptions options,
        ILintConsole c)
    {
        try
        {
            if (options.Verbose)
            {
                c.WriteLine($"[INFO]: Generiere Repo-Playbook unter: {playbookPath}");
            }
            await RepoPlaybookGenerator.GenerateAsync(solution, playbookPath, options);
        }
        catch (Exception ex)
        {
            c.WriteError($"[ERROR]: Fehler beim Generieren des Repo-Playbooks: {ex.Message}");
        }
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
        AuditRunContext ctx)
    {
        var (_, config, profiler, c) = ctx;
        profiler.StartPhase("OutputWriting");
        try
        {
            if (violations.Count == 0)
            {
                c.WriteLine("OK");
                return 0;
            }

            var hasError = RuleMetadataRegistry.HasErrorSeverity(violations, config);
            c.WriteLine(ViolationMarkdownFormatter.Format(violations, outputRoot, config));
            return hasError ? 1 : 0;
        }
        finally
        {
            profiler.StopPhase("OutputWriting");
        }
    }
}
