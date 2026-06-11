using System.CommandLine;
using System.Text.Json;
using AiNetLinter.Cli;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Output;
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
                return await ExecuteLinterAsync(ToLinterArgs(CliCommandBuilder.Parse(parseResult, options)));
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
        };
    }

    private static async Task<int> ExecuteLinterAsync(LinterArgs args)
    {
        if (HasConflictingModeOptions(args))
        {
            Console.Error.WriteLine("[ERROR]: --create-baseline und --add-disable-all dürfen nicht mit --baseline kombiniert werden.");
            return 1;
        }

        var maintenanceExitCode = await TryRunMaintenanceModeAsync(args);
        if (maintenanceExitCode.HasValue)
        {
            return maintenanceExitCode.Value;
        }

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

        return null;
    }

    private static bool HasConflictingModeOptions(LinterArgs args)
    {
        return args.BaselinePath != null && (args.CreateBaselinePath != null || args.AddDisableAll) ||
               args.CreateBaselinePath != null && args.AddDisableAll;
    }

    private static async Task<int> AddDisableAllAsync(LinterArgs args)
    {
        LogDisableAllInject(args.Verbose, args.TargetPath);

        var result = await DisableAllCommentInjector.InjectAsync(args.TargetPath);

        if (args.Verbose)
        {
            Console.WriteLine(
                $"[INFO]: {result.ModifiedFiles} von {result.TotalFiles} Dateien geändert, {result.SkippedFiles} übersprungen.");
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

        return WriteViolationsAndExit(filtered, args.Format, outputRoot);
    }

    private static async Task<int> AuditWithoutBaselineAsync(LinterArgs args, LinterConfig config)
    {
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(args.TargetPath);
        var outputRoot = OutputRootResolver.Resolve(args.TargetPath);
        return WriteViolationsAndExit(violations, args.Format, outputRoot);
    }

    private static int WriteViolationsAndExit(
        IReadOnlyCollection<Models.RuleViolation> violations,
        string format,
        string outputRoot)
    {
        if (format == "sarif")
        {
            SarifWriter.Write(violations, outputRoot);
            return violations.Count > 0 ? 1 : 0;
        }

        if (violations.Count > 0)
        {
            Console.WriteLine(ViolationTextFormatter.Format(violations, outputRoot));
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
            Console.WriteLine($"[INFO]: Erzeuge Baseline für: {targetPath}");
            Console.WriteLine($"[INFO]: Ausgabedatei: {baselinePath}");
        }
    }

    private static void LogDisableAllInject(bool verbose, string targetPath)
    {
        if (verbose)
        {
            Console.WriteLine($"[INFO]: Füge Disable-all-Kommentar ein unter: {targetPath}");
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
        Console.WriteLine($"[INFO]: Baseline aktualisiert: {changedCount} geändert, {removedCount} entfernt.");
    }

    private static LinterConfig? TryLoadConfig(string? configPath, bool isRequired)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            if (isRequired)
            {
                Console.Error.WriteLine("[ERROR]: --config ist erforderlich für den Audit-Lauf.");
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
            return JsonSerializer.Deserialize<LinterConfig>(content, options);
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
    }
}
