#nullable enable

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using AiNetLinter.Cli;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Output;
using AiNetLinter.Scope;
using AiNetLinter.Suppression;

namespace AiNetLinter;

/// <summary>
/// Der CLI-Einstiegspunkt für den Linter.
/// </summary>
public static class Program
{
    private const string FixedCountMessageFormat = "[INFO]: {0} einfache Regelverstoesse wurden automatisch behoben.";
    private const string NoImpactCallSitesMessage = "Keine betroffenen Aufrufstellen gefunden.";
    private const string ImpactHeaderMessage = "# Semantische Diff-Impact-Analyse";
    private const string CallSitesFoundMessage = "Gefundene betroffene Aufrufstellen fuer geaenderte Signaturen:";

    /// <summary>
    /// Der Einstiegspunkt für die Ausführung der Linter-CLI.
    /// </summary>
    /// <param name="args">Die Befehlszeilenargumente.</param>
    /// <returns>Der Exit-Code des Programms (0 = Erfolg, 1 = Linter-Verstoesse, 2 = Fataler Fehler).</returns>
    public static async Task<int> Main(string[] args)
    {
        var (root, options) = CliCommandBuilder.Build();

        root.SetAction(async parseResult =>
        {
            try
            {
                var linterArgs = ToLinterArgs(CliCommandBuilder.Parse(parseResult, options));
                if (linterArgs.Format != "sarif")
                {
                    Console.WriteLine($"# Run: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                }
                return await ExecuteLinterAsync(linterArgs);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[FATAL ERROR]: Ein unerwarteter Fehler ist aufgetreten: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                return 2;
            }
        });

        return await root.Parse(args).InvokeAsync();
    }

    private static LinterArgs ToLinterArgs(CliCommandBuilder.ParsedArgs parsed)
    {
        return new LinterArgs
        {
            ConfigPath = parsed.ConfigPath,
            TargetPath = parsed.TargetPath,
            GraphPath = parsed.GraphPath,
            PlaybookPath = parsed.PlaybookPath,
            Format = parsed.Format,
            Verbose = parsed.Verbose,
            CreateBaselinePath = parsed.CreateBaselinePath,
            BaselinePath = parsed.BaselinePath,
            AddDisableAll = parsed.AddDisableAll,
            RemoveDisableAll = parsed.RemoveDisableAll,
            DebtReport = parsed.DebtReport,
            WaveReady = parsed.WaveReady,
            OnlyChanged = parsed.OnlyChanged,
            GitSince = parsed.GitSince,
            Fix = parsed.Fix,
            HasImpact = parsed.HasImpact,
            ImpactRef = parsed.ImpactRef,
        };
    }

    private static async Task<int> ExecuteLinterAsync(LinterArgs args)
    {
        var validationError = ValidateArgs(args);
        if (validationError.HasValue)
        {
            return validationError.Value;
        }

        var maintenanceExitCode = await TryRunMaintenanceModeAsync(args);
        if (maintenanceExitCode.HasValue)
        {
            return maintenanceExitCode.Value;
        }

        if (args.DebtReport)
        {
            return await RunDebtReportAsync(args);
        }

        if (args.HasImpact)
        {
            return await RunImpactAnalysisAsync(args);
        }

        return await RunAuditAsync(args);
    }

    private static int? ValidateArgs(LinterArgs args)
    {
        if (HasConflictingModeOptions(args))
        {
            Console.Error.WriteLine(
                "[ERROR]: Wartungsmodi (--create-baseline, --add-disable-all, --remove-disable-all) sind untereinander und mit --baseline nicht kombinierbar.");
            return 1;
        }

        if (args.OnlyChanged && args.BaselinePath == null)
        {
            Console.Error.WriteLine("[ERROR]: --only-changed erfordert --baseline.");
            return 1;
        }

        return null;
    }

    private static async Task<int> RunAuditAsync(LinterArgs args)
    {
        var config = LinterConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: true);
        if (config == null)
        {
            return 1;
        }

        LinterLogger.LogStart(args.Verbose, args.ConfigPath!, args.TargetPath);

        using var catalog = await SourceFileCatalog.LoadAsync(args.TargetPath);

        await GenerateOptionalOutputsAsync(catalog.Solution, args);

        var (currentCatalog, needsDispose) = await ApplyAutoFixIfNeededAsync(catalog, config, args);
        try
        {
            return await ExecuteAuditAsync(args, config, currentCatalog);
        }
        finally
        {
            if (needsDispose)
            {
                currentCatalog.Dispose();
            }
        }
    }

    private static async Task GenerateOptionalOutputsAsync(Solution solution, LinterArgs args)
    {
        if (args.GraphPath != null)
        {
            await TryGenerateCodegraphAsync(solution, args.GraphPath, args.Verbose);
        }

        if (args.PlaybookPath != null)
        {
            await TryGeneratePlaybookAsync(solution, args.PlaybookPath, args.Verbose);
        }
    }

    private static async Task<(SourceFileCatalog Catalog, bool NeedsDispose)> ApplyAutoFixIfNeededAsync(
        SourceFileCatalog catalog, LinterConfig config, LinterArgs args)
    {
        if (!args.Fix)
        {
            return (catalog, false);
        }

        var engine = new LinterEngine(config);
        var initialViolations = await engine.RunAsync(catalog);
        var fixedCount = await LinterAutoFixer.FixAsync(catalog.Solution, initialViolations, args.Verbose);
        if (fixedCount > 0)
        {
            Console.WriteLine(FixedCountMessageFormat, fixedCount);
            var reloaded = await SourceFileCatalog.LoadAsync(args.TargetPath);
            return (reloaded, true);
        }

        return (catalog, false);
    }

    private static async Task<int> ExecuteAuditAsync(LinterArgs args, LinterConfig config, SourceFileCatalog catalog)
    {
        if (args.BaselinePath != null)
        {
            return await AuditWithBaselineAsync(args, config, catalog);
        }

        return await AuditWithoutBaselineAsync(args, config, catalog);
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

    private static async Task TryGeneratePlaybookAsync(Solution solution, string playbookPath, bool verbose)
    {
        try
        {
            if (verbose)
            {
                Console.WriteLine($"[INFO]: Generiere Repo-Playbook unter: {playbookPath}");
            }
            await RepoPlaybookGenerator.GenerateAsync(solution, playbookPath, verbose);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR]: Fehler beim Generieren des Repo-Playbooks: {ex.Message}");
        }
    }

    private static async Task<int> RunDebtReportAsync(LinterArgs args)
    {
        LinterConfig? config = null;
        IReadOnlyCollection<Models.RuleViolation>? violations = null;

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

    private static async Task<int?> TryRunMaintenanceModeAsync(LinterArgs args)
    {
        if (args.CreateBaselinePath != null)
        {
            return await CreateBaselineAsync(args);
        }

        if (args.AddDisableAll)
        {
            return await AddDisableAllAsync(args);
        }

        if (args.RemoveDisableAll)
        {
            return await RemoveDisableAllAsync(args);
        }

        return null;
    }

    private static bool HasConflictingModeOptions(LinterArgs args)
    {
        var maintenanceModeCount = CountMaintenanceModes(args);
        return maintenanceModeCount > 1 ||
               args.BaselinePath != null && maintenanceModeCount > 0;
    }

    private static int CountMaintenanceModes(LinterArgs args)
    {
        int count = 0;
        if (args.CreateBaselinePath != null) count++;
        if (args.AddDisableAll) count++;
        if (args.RemoveDisableAll) count++;
        return count;
    }

    private static async Task<int> AddDisableAllAsync(LinterArgs args)
    {
        var config = LinterConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: true);
        if (config == null)
        {
            return 1;
        }

        LinterLogger.LogDisableAllInject(args.Verbose, args.TargetPath);

        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(args.TargetPath);
        var outputRoot = OutputRootResolver.Resolve(args.TargetPath);
        var violatingPaths = ViolatingFilePathResolver.ResolveAbsolutePaths(violations, outputRoot);
        var result = DisableAllCommentInjector.InjectIntoFiles(violatingPaths);

        if (args.Verbose)
        {
            Console.WriteLine(
                $"[INFO]: Audit fand {violations.Count} Verstoesse in {result.CandidateFiles} Dateien.");
            Console.WriteLine(
                $"[INFO]: {result.ModifiedFiles} Dateien geaendert, {result.SkippedFiles} uebersprungen.");
        }

        Console.WriteLine("OK");
        return 0;
    }

    private static async Task<int> RemoveDisableAllAsync(LinterArgs args)
    {
        LinterLogger.LogDisableAllRemove(args.Verbose, args.TargetPath);

        var result = await DisableAllCommentRemover.RemoveAsync(args.TargetPath);

        if (args.Verbose)
        {
            Console.WriteLine(
                $"[INFO]: {result.ModifiedFiles} von {result.ScannedFiles} Dateien bereinigt.");
        }

        Console.WriteLine("OK");
        return 0;
    }

    private static async Task<int> CreateBaselineAsync(LinterArgs args)
    {
        LinterLogger.LogBaselineCreate(args.Verbose, args.TargetPath, args.CreateBaselinePath!);

        using var catalog = await SourceFileCatalog.LoadAsync(args.TargetPath);
        var outputRoot = OutputRootResolver.Resolve(args.TargetPath);
        var checksums = catalog.ComputeChecksums(outputRoot);

        BaselineWriter.Write(args.CreateBaselinePath!, checksums);

        if (args.Verbose)
        {
            Console.WriteLine($"[INFO]: Baseline mit {checksums.Count} Dateien geschrieben.");
        }

        Console.WriteLine("OK");
        return 0;
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

        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(catalog);
        var filtered = BaselineViolationFilter.Filter(violations, comparison.ChangedFiles, outputRoot);

        if (comparison.HasAnyChange)
        {
            BaselineWriter.Write(args.BaselinePath!, currentChecksums);
            LinterLogger.LogBaselineUpdate(args.Verbose, comparison);
        }

        var scoped = ApplyScopeFilters(filtered, args, outputRoot, comparison.ChangedFiles);
        return WriteViolationsAndExit(scoped, args.Format, outputRoot, config);
    }

    private static async Task<int> AuditWithoutBaselineAsync(LinterArgs args, LinterConfig config, SourceFileCatalog catalog)
    {
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(catalog);
        var outputRoot = OutputRootResolver.Resolve(args.TargetPath);
        var scoped = ApplyScopeFilters(violations, args, outputRoot, onlyChangedFiles: []);
        return WriteViolationsAndExit(scoped, args.Format, outputRoot, config);
    }

    private static IReadOnlyCollection<Models.RuleViolation> ApplyScopeFilters(
        IReadOnlyCollection<Models.RuleViolation> violations,
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
        IReadOnlyCollection<Models.RuleViolation> violations,
        string format,
        string outputRoot,
        LinterConfig config)
    {
        if (format == "sarif")
        {
            SarifWriter.Write(violations, outputRoot, config);
            return violations.Count > 0 ? 1 : 0;
        }

        if (violations.Count > 0)
        {
            Console.WriteLine(ViolationTextFormatter.Format(violations, outputRoot, config));
            return 1;
        }

        Console.WriteLine("OK");
        return 0;
    }

    private static async Task<int> RunImpactAnalysisAsync(LinterArgs args)
    {
        using var catalog = await SourceFileCatalog.LoadAsync(args.TargetPath);
        var callSites = await DiffImpactAnalyzer.AnalyzeAsync(catalog.Solution, args.TargetPath, args.ImpactRef, args.Verbose);

        if (callSites.Count == 0)
        {
            Console.WriteLine(NoImpactCallSitesMessage);
        }
        else
        {
            Console.WriteLine(ImpactHeaderMessage);
            Console.WriteLine(CallSitesFoundMessage);
            foreach (var callSite in callSites)
            {
                Console.WriteLine(callSite);
            }
        }

        return 0;
    }
}
