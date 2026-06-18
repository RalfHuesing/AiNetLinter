#nullable enable

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Cli;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Metrics;
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
        Console.OutputEncoding = Encoding.UTF8;
        var (root, options) = CliCommandBuilder.Build();

        root.SetAction(async parseResult =>
        {
            try
            {
                var linterArgs = ToLinterArgs(CliCommandBuilder.Parse(parseResult, options));
                if (!linterArgs.Readme && linterArgs.Format != "sarif")
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
            Format = parsed.Output.Format,
            Verbose = parsed.Output.Verbose,
            GraphPath = parsed.Output.GraphPath,
            PlaybookPath = parsed.Output.PlaybookPath,
            CreateBaselinePath = parsed.Baseline.CreateBaselinePath,
            BaselinePath = parsed.Baseline.BaselinePath,
            OnlyChanged = parsed.Baseline.OnlyChanged,
            AddDisableAll = parsed.Maintenance.AddDisableAll,
            RemoveDisableAll = parsed.Maintenance.RemoveDisableAll,
            WaveReady = parsed.Scope.WaveReady,
            GitSince = parsed.Scope.GitSince,
            DebtReport = parsed.DebtReport,
            Fix = parsed.Fix,
            HasImpact = parsed.Impact.HasImpact,
            ImpactRef = parsed.Impact.ImpactRef,
            SyncCursorRules = parsed.SyncCursorRules,
            Check = parsed.Check,
            NoCache = parsed.NoCache,
            CacheTtlMinutes = parsed.CacheTtlMinutes,
            Footprint = parsed.Footprint,
            Readme = parsed.Readme,
        };
    }

    private static async Task<int> ExecuteLinterAsync(LinterArgs args)
    {
        if (args.Readme)
        {
            return RunPrintReadme();
        }

        var validationError = ValidateArgs(args);
        if (validationError.HasValue)
        {
            return validationError.Value;
        }

        if (args.Check && args.PlaybookPath != null)
        {
            return await RunPlaybookCheckAsync(args);
        }

        // Schneller Pfad: --sync-cursor-rules ohne --playbook.
        // Wenn --playbook ebenfalls gesetzt ist, fällt der Aufruf durch zu RunAuditAsync,
        // das beide Ausgaben via GenerateOptionalOutputsAsync erzeugt.
        if (args.SyncCursorRules && args.PlaybookPath == null)
        {
            return RunSyncCursorRules(args);
        }

        if (args.Footprint != null)
            return await FootprintExecutor.RunAsync(args);

        var maintenanceExitCode = await TryRunMaintenanceModeAsync(args);
        if (maintenanceExitCode.HasValue)
        {
            return maintenanceExitCode.Value;
        }

        if (args.DebtReport)
        {
            return await DebtReportExecutor.RunDebtReportAsync(args);
        }

        if (args.HasImpact)
        {
            return await ImpactExecutor.RunImpactAnalysisAsync(args);
        }

        return await RunAuditAsync(args);
    }

    private static int? ValidateArgs(LinterArgs args)
    {
        var error = args.Validate();
        if (error != null)
        {
            Console.Error.WriteLine(error);
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
            var exitCode = WriteViolationsAndExit(scoped, args.Format, outputRoot, config);

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

    private static async Task GenerateOptionalOutputsAsync(
        Solution solution,
        LinterArgs args,
        LinterConfig config,
        IReadOnlyCollection<Models.RuleViolation>? violations = null)
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

    private static string? LoadRulesJsonContent(string? configPath)
    {
        if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
        {
            return null;
        }
        return File.ReadAllText(configPath, Encoding.UTF8);
    }



    private static async Task<int> RunPlaybookCheckAsync(LinterArgs args)
    {
        var config = LinterConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: false);

        using var catalog = await SourceFileCatalog.LoadAsync(args.TargetPath);
        var generatedContent = await RepoPlaybookGenerator.BuildContentAsync(catalog.Solution, new PlaybookOptions(args.Verbose, config, args.ConfigPath ?? "rules.json"));

        if (!File.Exists(args.PlaybookPath))
        {
            Console.Error.WriteLine($"[ERROR]: Die Playbook-Datei '{args.PlaybookPath}' existiert nicht.");
            return 1;
        }

        var existingContent = await File.ReadAllTextAsync(args.PlaybookPath!, Encoding.UTF8);
        if (generatedContent == existingContent)
        {
            Console.WriteLine("[OK]: Playbook ist aktuell.");
            return 0;
        }

        Console.Error.WriteLine("[ERROR]: Drift erkannt! Das generierte Playbook stimmt nicht mit der Datei auf der Festplatte überein.");
        return 1;
    }

    private static async Task<int?> TryRunMaintenanceModeAsync(LinterArgs args)
    {
        if (args.CreateBaselinePath != null)
        {
            return await MaintenanceExecutor.CreateBaselineAsync(args);
        }

        if (args.AddDisableAll)
        {
            return await MaintenanceExecutor.AddDisableAllAsync(args);
        }

        if (args.RemoveDisableAll)
        {
            return await MaintenanceExecutor.RemoveDisableAllAsync(args);
        }

        return null;
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
        AiNetLinter.Diagnostics.PerformanceProfiler.Instance.StartPhase("OutputWriting");
        try
        {
            if (violations.Count == 0)
            {
                if (format != "sarif") Console.WriteLine("OK");
                else SarifWriter.Write(violations, outputRoot, config);
                return 0;
            }

            var hasError = RuleMetadataRegistry.HasErrorSeverity(violations, config);

            if (format == "sarif")
            {
                SarifWriter.Write(violations, outputRoot, config);
            }
            else
            {
                Console.WriteLine(ViolationTextFormatter.Format(violations, outputRoot, config));
            }

            return hasError ? 1 : 0;
        }
        finally
        {
            AiNetLinter.Diagnostics.PerformanceProfiler.Instance.StopPhase("OutputWriting");
        }
    }

    private static int RunSyncCursorRules(LinterArgs args)
    {
        var config = LinterConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: true);
        if (config == null)
        {
            return 1;
        }

        string baseDir = ResolveBaseDirectory(args.TargetPath);
        var cursorRulesDir = Path.Combine(baseDir, ".cursor", "rules");
        var mdcPath = Path.Combine(cursorRulesDir, "AiNetLinter.mdc");

        var content = CursorRulesGenerator.GenerateContent(config, args.ConfigPath ?? "rules.json");

        if (args.Check)
        {
            if (!File.Exists(mdcPath))
            {
                Console.Error.WriteLine($"[ERROR]: Die Datei '{mdcPath}' existiert nicht.");
                return 1;
            }

            var existing = File.ReadAllText(mdcPath, Encoding.UTF8);
            if (existing != content)
            {
                Console.Error.WriteLine("[ERROR]: Drift erkannt! Die generierten Cursor-Regeln stimmen nicht mit der Datei auf der Festplatte überein.");
                return 1;
            }

            Console.WriteLine("[OK]: Cursor-Regeln sind aktuell.");
            return 0;
        }
        else
        {
            if (!Directory.Exists(cursorRulesDir))
            {
                Directory.CreateDirectory(cursorRulesDir);
            }

            if (File.Exists(mdcPath) && File.ReadAllText(mdcPath, Encoding.UTF8) == content)
            {
                Console.WriteLine($"[INFO]: Cursor-Regeldatei ist bereits aktuell (kein Schreibzugriff): {mdcPath}");
                return 0;
            }

            File.WriteAllText(mdcPath, content, Encoding.UTF8);
            Console.WriteLine($"[INFO]: Cursor-Regeldatei erfolgreich synchronisiert unter: {mdcPath}");
            return 0;
        }
    }

    private static int RunPrintReadme()
    {
        string[] parts = ["README.md", "Docs/configuration.md"];
        foreach (var name in parts)
        {
            using var stream = typeof(Program).Assembly.GetManifestResourceStream(name);
            if (stream == null)
            {
                Console.Error.WriteLine($"[ERROR]: '{name}' wurde nicht als eingebettete Ressource gefunden.");
                return 1;
            }
            using var reader = new StreamReader(stream, Encoding.UTF8);
            Console.WriteLine(reader.ReadToEnd());
        }
        return 0;
    }

    private static string ResolveBaseDirectory(string targetPath)
    {
        if (Directory.Exists(targetPath))
        {
            return targetPath;
        }
        if (File.Exists(targetPath))
        {
            return Path.GetDirectoryName(targetPath) ?? targetPath;
        }
        return targetPath;
    }

}
