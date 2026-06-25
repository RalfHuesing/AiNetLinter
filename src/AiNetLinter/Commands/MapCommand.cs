#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Configuration;
using AiNetLinter.Maps;
using AiNetLinter.Output;

namespace AiNetLinter.Commands;

/// <summary>
/// Generiert Codebase-Landkarten für Drift-Erkennung und Eval-Prompts.
/// </summary>
internal static class MapCommand
{
    internal static Task<int> RunAsync(
        LinterArgs args,
        CancellationToken ct = default,
        ILintConsole? console = null)
    {
        var c = console ?? ConsoleLintConsole.Instance;

        if (string.IsNullOrWhiteSpace(args.TargetPath))
        {
            c.WriteError(LinterErrorFormatter.Format(
                LinterErrorCodes.ConfigRequired,
                "--path fehlt für --map",
                context: $"--map {args.MapType}",
                hint: "Pfad zur Solution oder zum Verzeichnis mit --path angeben."));
            return Task.FromResult(1);
        }

        var mapType = args.MapType?.ToLowerInvariant();

        var exitCode = mapType switch
        {
            "vocabulary" => VocabularyMapBuilder.Build(args.TargetPath, c),
            "structure"  => StructureMapBuilder.Build(args.TargetPath, ResolveMaxLineCount(args), c),
            "hotspots"   => HotspotMapBuilder.Build(args.TargetPath, ResolveMaxLineCount(args), c),
            _ => ReportUnknownType(mapType, c)
        };

        return Task.FromResult(exitCode);
    }

    private static int ResolveMaxLineCount(LinterArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.ConfigPath))
            return new MetricsConfig().MaxLineCount;

        var config = LinterConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: false);
        return config?.Metrics.MaxLineCount ?? new MetricsConfig().MaxLineCount;
    }

    private static int ReportUnknownType(string? mapType, ILintConsole c)
    {
        c.WriteError(LinterErrorFormatter.Format(
            LinterErrorCodes.ResourceNotFound,
            $"Unbekannter Map-Typ '{mapType}'",
            context: $"--map {mapType}",
            hint: "Gültige Typen: vocabulary, structure, hotspots"));
        return 1;
    }
}
