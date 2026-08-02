#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Cli;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Output;
using ModelContextProtocol.Server;

namespace AiNetLinter.Commands;

internal static class McpServerCommand
{
    internal static async Task<int> RunAsync(LinterArgs args, CancellationToken ct = default, ILintConsole? console = null)
    {
        var c = console ?? LinterConsole.Instance;
        var solutionPath = ResolveSolutionPathOrError(args.TargetPath, c);
        if (solutionPath is null) return 1;

        var catalog = await TryLoadSolutionAsync(solutionPath, ct, c);
        using var mcpState = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            catalog, c, ResolveMaxLineCount(args), ResolveConfig(args)));

        var serverOptions = McpServerOptionsFactory.Create(mcpState);
        var transport = new StdioServerTransport(serverOptions);
        await using var server = McpServer.Create(transport, serverOptions);
        await server.RunAsync(ct);
        return 0;
    }

    /// <summary>
    /// Loest den konfigurierten Zeilen-Grenzwert auf — identische Logik wie
    /// <see cref="MapCommand"/>s private Hilfsmethode gleichen Namens (1:1-Uebernahme statt
    /// Sichtbarkeitsanhebung einer 6-Zeilen-Methode ueber Projektgrenzen). Bei gesetztem
    /// <see cref="LinterArgs.ConfigPath"/> wird <c>rules.json</c> geladen (best effort), sonst der
    /// <see cref="MetricsConfig"/>-Default verwendet — derselbe Grenzwert, den auch ein CLI-Lint-Lauf
    /// auf derselben Solution respektieren wuerde. <see langword="internal"/> statt <c>private</c>,
    /// damit die Config-Verdrahtung direkt testbar ist.
    /// </summary>
    internal static int ResolveMaxLineCount(LinterArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.ConfigPath))
            return new MetricsConfig().MaxLineCount;

        var config = ConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: false);
        return config?.Metrics.MaxLineCount ?? new MetricsConfig().MaxLineCount;
    }

    /// <summary>
    /// Loest die vollstaendige Linter-<see cref="Config"/> auf — identische Logik wie
    /// <see cref="ResolveMaxLineCount"/>, nur die Entitaet ist groesser (wird fuer
    /// <c>get_violations</c>/<see cref="McpCodeGraphServer.Config"/> gebraucht). Bei gesetztem
    /// <see cref="LinterArgs.ConfigPath"/> wird <c>rules.json</c> geladen (best effort), sonst der
    /// <see cref="Config"/>-Default verwendet — dieselbe Config, die auch ein CLI-Lint-Lauf
    /// auf derselben Solution respektieren wuerde. <see langword="internal"/> statt
    /// <c>private</c>, damit die Config-Verdrahtung direkt testbar ist.
    /// </summary>
    internal static Config ResolveConfig(LinterArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.ConfigPath))
            return new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() };

        return ConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: false)
            ?? new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() };
    }

    internal static string? ResolveSolutionPathOrError(string targetPath, ILintConsole console)
    {
        var basePath = string.IsNullOrEmpty(targetPath) ? Directory.GetCurrentDirectory() : targetPath;

        if (File.Exists(basePath)) return basePath;

        if (!Directory.Exists(basePath))
        {
            console.WriteError(LinterErrorFormatter.Format(
                LinterErrorCodes.ResourceNotFound,
                $"Pfad nicht gefunden: {basePath}"));
            return null;
        }

        var candidates = FindSolutionCandidates(basePath);
        return candidates.Count switch
        {
            0 => ReportNoSolutionFound(basePath, console),
            1 => candidates[0],
            _ => ReportAmbiguousSolution(candidates, console),
        };
    }

    private static IReadOnlyList<string> FindSolutionCandidates(string directory)
    {
        return Directory.GetFiles(directory, "*.slnx")
            .Concat(Directory.GetFiles(directory, "*.sln"))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? ReportNoSolutionFound(string directory, ILintConsole console)
    {
        console.WriteError(LinterErrorFormatter.Format(
            LinterErrorCodes.ResourceNotFound,
            $"Keine .sln oder .slnx Datei im Verzeichnis gefunden: {directory}",
            hint: "Pfad direkt auf eine konkrete Solution-Datei zeigen lassen (--path <Datei>)."));
        return null;
    }

    private static string? ReportAmbiguousSolution(IReadOnlyList<string> candidates, ILintConsole console)
    {
        console.WriteError(LinterErrorFormatter.Format(
            LinterErrorCodes.AmbiguousSolution,
            "Mehrere Solution-Dateien gefunden, Auswahl ist nicht eindeutig.",
            context: string.Join(", ", candidates),
            hint: "Konkrete Solution-Datei ueber --path <Datei> angeben."));
        return null;
    }

    internal static async Task<SourceFileCatalog?> TryLoadSolutionAsync(string solutionPath, CancellationToken ct, ILintConsole console)
    {
        try
        {
            var catalog = await SourceFileCatalog.LoadAsync(solutionPath, ct);
            if (catalog.HasLoadingErrors)
            {
                console.WriteError($"[WARN]: Solution mit Workspace-Diagnosen geladen: {solutionPath}");
            }

            return catalog;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            console.WriteError($"[WARN]: MCP-Server startet ohne geladene Solution ({solutionPath}): {ex.Message}");
            return null;
        }
    }
}
