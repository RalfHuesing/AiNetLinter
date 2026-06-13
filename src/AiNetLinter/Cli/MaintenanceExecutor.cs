#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Output;
using AiNetLinter.Suppression;

namespace AiNetLinter.Cli;

/// <summary>
/// Führt Wartungsaktionen wie das Verwalten von Baselines und das Injizieren/Entfernen von Deaktivierungskommentaren aus.
/// </summary>
public static class MaintenanceExecutor
{
    /// <summary>
    /// Fügt allen Dateien mit Regelverstößen einen Deaktivierungskommentar hinzu.
    /// </summary>
    public static async Task<int> AddDisableAllAsync(LinterArgs args)
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

    /// <summary>
    /// Entfernt alle dateiweiten Deaktivierungskommentare aus Quellcodedateien.
    /// </summary>
    public static async Task<int> RemoveDisableAllAsync(LinterArgs args)
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

    /// <summary>
    /// Erzeugt eine Baseline-Sicherungsdatei für inkrementelle Prüfungen.
    /// </summary>
    public static async Task<int> CreateBaselineAsync(LinterArgs args)
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
}
