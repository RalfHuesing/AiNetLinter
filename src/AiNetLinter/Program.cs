using System.CommandLine;
using System.Text.Json;
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
    /// <summary>
    /// Der Einstiegspunkt für die Ausführung der Linter-CLI.
    /// </summary>
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
        };
    }

    private static async Task<int> ExecuteLinterAsync(LinterArgs args)
    {
        var modeError = ValidateModeOptions(args);
        if (modeError.HasValue)
        {
            return modeError.Value;
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

        var onlyChangedError = ValidateOnlyChangedOption(args);
        if (onlyChangedError.HasValue)
        {
            return onlyChangedError.Value;
        }

        return await RunAuditAsync(args);
    }

    private static int? ValidateModeOptions(LinterArgs args)
    {
        if (!HasConflictingModeOptions(args))
        {
            return null;
        }

        Console.Error.WriteLine(
            "[ERROR]: Wartungsmodi (--create-baseline, --add-disable-all, --remove-disable-all) sind untereinander und mit --baseline nicht kombinierbar.");
        return 1;
    }

    private static int? ValidateOnlyChangedOption(LinterArgs args)
    {
        if (!args.OnlyChanged || args.BaselinePath != null)
        {
            return null;
        }

        Console.Error.WriteLine("[ERROR]: --only-changed erfordert --baseline.");
        return 1;
    }

    private static async Task<int> RunAuditAsync(LinterArgs args)
    {
        var config = TryLoadConfig(args.ConfigPath, isRequired: true);
        if (config == null)
        {
            return 1;
        }

        LogStart(args.Verbose, args.ConfigPath!, args.TargetPath);

        if (args.BaselinePath != null)
        {
            return await AuditWithBaselineAsync(args, config);
        }

        return await AuditWithoutBaselineAsync(args, config);
    }

    private static async Task<int> RunDebtReportAsync(LinterArgs args)
    {
        LinterConfig? config = null;
        IReadOnlyCollection<Models.RuleViolation>? violations = null;

        if (!string.IsNullOrWhiteSpace(args.ConfigPath))
        {
            config = TryLoadConfig(args.ConfigPath, isRequired: false);
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
        var config = TryLoadConfig(args.ConfigPath, isRequired: true);
        if (config == null)
        {
            return 1;
        }

        LogDisableAllInject(args.Verbose, args.TargetPath);

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
        LogDisableAllRemove(args.Verbose, args.TargetPath);

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
        LogBaselineCreate(args.Verbose, args.TargetPath, args.CreateBaselinePath!);

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

    private static async Task<int> AuditWithBaselineAsync(LinterArgs args, LinterConfig config)
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

        using var catalog = await SourceFileCatalog.LoadAsync(args.TargetPath);
        var outputRoot = OutputRootResolver.Resolve(args.TargetPath);
        var currentChecksums = catalog.ComputeChecksums(outputRoot);
        var comparison = BaselineComparer.Compare(storedBaseline, currentChecksums);

        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(catalog);
        var filtered = BaselineViolationFilter.Filter(violations, comparison.ChangedFiles, outputRoot);

        if (comparison.HasAnyChange)
        {
            BaselineWriter.Write(args.BaselinePath!, currentChecksums);
            LogBaselineUpdate(args.Verbose, comparison);
        }

        var scoped = ApplyScopeFilters(filtered, args, outputRoot, comparison.ChangedFiles);
        return WriteViolationsAndExit(scoped, args.Format, outputRoot, config);
    }

    private static async Task<int> AuditWithoutBaselineAsync(LinterArgs args, LinterConfig config)
    {
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(args.TargetPath);
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

    private static void LogStart(bool verbose, string configPath, string targetPath)
    {
        if (verbose)
        {
            Console.WriteLine($"[INFO]: Lade Konfiguration von: {configPath}");
            Console.WriteLine($"[INFO]: Analysiere Ziel-Pfad: {targetPath}");
        }
    }

    private static void LogBaselineCreate(bool verbose, string targetPath, string baselinePath)
    {
        if (verbose)
        {
            Console.WriteLine($"[INFO]: Erzeuge Baseline fuer: {targetPath}");
            Console.WriteLine($"[INFO]: Ausgabedatei: {baselinePath}");
        }
    }

    private static void LogDisableAllInject(bool verbose, string targetPath)
    {
        if (verbose)
        {
            Console.WriteLine($"[INFO]: Audit und Disable-all-Injection unter: {targetPath}");
        }
    }

    private static void LogDisableAllRemove(bool verbose, string targetPath)
    {
        if (verbose)
        {
            Console.WriteLine($"[INFO]: Entferne Disable-all-Kommentare unter: {targetPath}");
        }
    }

    private static void LogBaselineUpdate(bool verbose, BaselineComparisonResult comparison)
    {
        if (!verbose)
        {
            return;
        }

        var changedCount = comparison.ChangedFiles.Count;
        var removedCount = comparison.RemovedFiles.Count;
        Console.WriteLine($"[INFO]: Baseline aktualisiert: {changedCount} geaendert, {removedCount} entfernt.");
    }

    private static LinterConfig? TryLoadConfig(string? configPath, bool isRequired)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            if (isRequired)
            {
                Console.Error.WriteLine("[ERROR]: --config ist erforderlich fuer den Audit-Lauf.");
            }

            return null;
        }

        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine($"[ERROR]: Die Konfigurationsdatei wurde nicht gefunden: {configPath}");
            return null;
        }

        var config = LoadConfig(configPath);
        if (config == null)
        {
            Console.Error.WriteLine("[ERROR]: Die Konfigurationsdatei konnte nicht deserialisiert werden.");
        }

        return config;
    }

    private static LinterConfig? LoadConfig(string configPath)
    {
        try
        {
            var content = File.ReadAllText(configPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var config = JsonSerializer.Deserialize<LinterConfig>(content, options);
            return config is null ? null : LinterConfigNormalizer.Normalize(config);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"[ERROR]: Ungueltige Konfiguration in '{configPath}': {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load config: {ex.Message}");
            return null;
        }
    }

    private sealed class LinterArgs
    {
        public string? ConfigPath { get; init; }
        public required string TargetPath { get; init; }
        public string? GraphPath { get; init; }
        public required string Format { get; init; }
        public required bool Verbose { get; init; }
        public string? CreateBaselinePath { get; init; }
        public string? BaselinePath { get; init; }
        public bool AddDisableAll { get; init; }
        public bool RemoveDisableAll { get; init; }
        public bool DebtReport { get; init; }
        public bool WaveReady { get; init; }
        public bool OnlyChanged { get; init; }
        public string? GitSince { get; init; }
    }
}
