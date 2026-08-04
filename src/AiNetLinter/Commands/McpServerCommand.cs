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

/// <summary>
/// Startet einen stdio-basierten MCP-Server (Model Context Protocol) fuer die aufgeloeste Solution.
/// Laeuft, bis der Client die Verbindung trennt oder das Cancellation-Token signalisiert wird.
/// </summary>
internal static class McpServerCommand
{
    /// <summary>
    /// Loest die Ziel-Solution auf, laedt sie (bester Versuch, kein Absturz bei Fehlschlag) und
    /// startet danach den MCP-Server mit dem in diesem Step registrierten Tool-Set (aktuell
    /// nur <c>find_symbol</c>, siehe <see cref="McpServerOptionsFactory"/>). Der Solution-Load
    /// laeuft im Hintergrund — der MCP-Transport antwortet auf <c>initialize</c> sofort, Tools
    /// waehrend des Loads reagieren mit <see cref="McpToolResults.Loading"/>.
    /// </summary>
    internal static async Task<int> RunAsync(LinterArgs args, CancellationToken ct = default, ILintConsole? console = null)
    {
        var c = console ?? LinterConsole.Instance;
        var solutionPath = ResolveSolutionPathOrError(args.TargetPath, c);
        if (solutionPath is null) return 1;

        var resolvedConfigPath = TryResolveRulesJsonPath(args.ConfigPath, solutionPath);
        if (resolvedConfigPath is null && string.IsNullOrWhiteSpace(args.ConfigPath))
        {
            var solutionDir = Path.GetDirectoryName(solutionPath);
            c.WriteError($"[WARN]: Keine rules.json neben der Solution gefunden ({solutionDir}); get_violations laeuft mit Default-Regeln.");
        }

        // LoadFunc deferriert den synchronen Wait auf MSBuildWorkspace.OpenSolutionAsync in
        // einen Hintergrund-Task, den McpCodeGraphServer selbst startet. So blockiert der
        // MCP-Transport-Handshake nicht mehr auf der Loesungs-Ladezeit.
        var maxLineCount = ResolveMaxLineCount(args, resolvedConfigPath);
        var config = ResolveConfig(args, resolvedConfigPath);
        var usedDefaultConfig = resolvedConfigPath is null;
        using var mcpState = new McpCodeGraphServer(new McpCodeGraphServerOptions
        {
            Catalog = null,
            Console = c,
            MaxLineCount = maxLineCount,
            Config = config,
            UsedDefaultConfig = usedDefaultConfig,
            ResolvedConfigPath = resolvedConfigPath,
            LoadFunc = innerCt => TryLoadSolutionAsync(solutionPath, innerCt, c),
        });

        McpCallLog? callLog = null;
        try
        {
            callLog = TryCreateCallLog(args.McpLogPath, solutionPath);

            var serverOptions = McpServerOptionsFactory.Create(mcpState, callLog);
            var transport = new StdioServerTransport(serverOptions);
            await using var server = McpServer.Create(transport, serverOptions);
            await server.RunAsync(ct);
        }
        finally
        {
            if (callLog is not null) await callLog.DisposeAsync();
        }
        return 0;
    }

    /// <summary>
    /// Loest den konfigurierten <c>--mcp-log</c>-Pfad auf und instanziiert bei Bedarf ein
    /// <see cref="McpCallLog"/>. Absoluter Pfad gewinnt 1:1; ein leerer oder relativer Pfad wird
    /// gegen das Solution-Verzeichnis aufgeloest (analog zu <c>cache/</c> neben der Solution,
    /// damit das Log in der Solution-Wurzel landet, nicht im Installations-Verzeichnis des
    /// Tools). Liefert <see langword="null"/>, wenn das Flag nicht gesetzt ist.
    /// </summary>
    internal static McpCallLog? TryCreateCallLog(string? mcpLogPath, string solutionPath)
    {
        if (string.IsNullOrWhiteSpace(mcpLogPath)) return null;

        var resolved = ResolveMcpLogPath(mcpLogPath, solutionPath);
        return new McpCallLog(resolved);
    }

    /// <summary>
    /// Aufloesung des Call-Log-Pfads: absolut -> wie angegeben; relativ -> relativ zum
    /// Solution-Verzeichnis (nicht zum exeDir, damit die Log-Datei zur Solution gehoert
    /// und nicht in das Installations-Verzeichnis des Tools wandert). GetFullPath normalisiert
    /// zusaetzlich Separatoren, damit das Ergebnis konsistent fuer Tests und Konsumenten ist.
    /// </summary>
    internal static string ResolveMcpLogPath(string mcpLogPath, string solutionPath)
    {
        if (Path.IsPathRooted(mcpLogPath)) return mcpLogPath;
        var solutionDir = Path.GetDirectoryName(solutionPath) ?? string.Empty;
        return Path.GetFullPath(Path.Combine(solutionDir, mcpLogPath));
    }

    /// <summary>
    /// Loest den effektiven <c>rules.json</c>-Pfad auf: bei explizit gesetztem
    /// <see cref="LinterArgs.ConfigPath"/> wird dieser 1:1 zurueckgegeben (die Existenzpruefung
    /// uebernimmt spaeter <see cref="ConfigLoader.TryLoadConfig"/>), sonst wird neben der
    /// aufgeloesten Solution-Datei nach <c>rules.json</c> gesucht. Liefert <see langword="null"/>,
    /// wenn weder explizit noch per Auto-Discovery ein Pfad gefunden wurde — der Aufrufer faellt
    /// in diesem Fall auf die Config-Defaults zurueck und signalisiert das per [WARN] auf stderr
    /// bzw. Header-Zeile in <c>get_violations</c>.
    /// </summary>
    internal static string? TryResolveRulesJsonPath(string? configPath, string solutionPath)
    {
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            return configPath;
        }

        var solutionDir = Path.GetDirectoryName(solutionPath);
        if (string.IsNullOrEmpty(solutionDir)) return null;

        var candidate = Path.Combine(solutionDir, "rules.json");
        return File.Exists(candidate) ? candidate : null;
    }

    /// <summary>
    /// Loest den konfigurierten Zeilen-Grenzwert auf — bei gesetztem <paramref name="resolvedConfigPath"/>
    /// wird die zugehoerige <c>rules.json</c> geladen (best effort), sonst der
    /// <see cref="MetricsConfig"/>-Default verwendet — derselbe Grenzwert, den auch ein CLI-Lint-Lauf
    /// auf derselben Solution respektieren wuerde. <paramref name="resolvedConfigPath"/> wird von
    /// <see cref="RunAsync"/> aus <see cref="TryResolveRulesJsonPath"/> durchgereicht, damit
    /// Auto-Discovery und explizites <c>--config</c> strukturell gleich behandelt werden.
    /// <see langword="internal"/> statt <c>private</c>, damit die Config-Verdrahtung direkt testbar ist.
    /// </summary>
    internal static int ResolveMaxLineCount(LinterArgs args, string? resolvedConfigPath = null)
    {
        var path = resolvedConfigPath ?? args.ConfigPath;
        if (string.IsNullOrWhiteSpace(path))
            return new MetricsConfig().MaxLineCount;

        var config = ConfigLoader.TryLoadConfig(path, isRequired: false);
        return config?.Metrics.MaxLineCount ?? new MetricsConfig().MaxLineCount;
    }

    /// <summary>
    /// Loest die vollstaendige Linter-<see cref="Config"/> auf — bei gesetztem
    /// <paramref name="resolvedConfigPath"/> wird die zugehoerige <c>rules.json</c> geladen (best
    /// effort), sonst der <see cref="Config"/>-Default verwendet — dieselbe Config, die auch ein
    /// CLI-Lint-Lauf auf derselben Solution respektieren wuerde. <paramref name="resolvedConfigPath"/>
    /// wird von <see cref="RunAsync"/> aus <see cref="TryResolveRulesJsonPath"/> durchgereicht, damit
    /// Auto-Discovery und explizites <c>--config</c> strukturell gleich behandelt werden.
    /// <see langword="internal"/> statt <c>private</c>, damit die Config-Verdrahtung direkt testbar ist.
    /// </summary>
    internal static Config ResolveConfig(LinterArgs args, string? resolvedConfigPath = null)
    {
        var path = resolvedConfigPath ?? args.ConfigPath;
        if (string.IsNullOrWhiteSpace(path))
            return new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() };

        return ConfigLoader.TryLoadConfig(path, isRequired: false)
            ?? new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() };
    }

    /// <summary>
    /// Loest den Ziel-Solution-Pfad auf (Datei direkt, Verzeichnis mit Auto-Suche, Default = cwd).
    /// Bricht bei 0 oder &gt;=2 gefundenen Kandidaten mit einer strukturierten [ERROR]-Ausgabe ab
    /// und liefert dann <see langword="null"/>. Reine Funktion ohne MSBuild/Solution-Load, daher
    /// unabhaengig von <see cref="RunAsync"/> testbar.
    /// </summary>
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

    /// <summary>
    /// Laedt die Solution best-effort. Schlaegt das Laden fehl, wird nur geloggt (Console.Error) und
    /// <see langword="null"/> geliefert — der Server startet trotzdem, der Aufrufer haelt den
    /// geladenen <see cref="SourceFileCatalog"/> resident (siehe <see cref="RunAsync"/>).
    /// </summary>
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
