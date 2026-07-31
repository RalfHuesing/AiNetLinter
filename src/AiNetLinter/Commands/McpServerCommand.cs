#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Cli;
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
    /// nur <c>find_symbol</c>, siehe <see cref="McpServerOptionsFactory"/>).
    /// </summary>
    internal static async Task<int> RunAsync(LinterArgs args, CancellationToken ct = default, ILintConsole? console = null)
    {
        var c = console ?? LinterConsole.Instance;
        var solutionPath = ResolveSolutionPathOrError(args.TargetPath, c);
        if (solutionPath is null) return 1;

        var catalog = await TryLoadSolutionAsync(solutionPath, ct, c);
        using var mcpState = new McpCodeGraphServer(catalog, c);

        var serverOptions = McpServerOptionsFactory.Create(mcpState);
        var transport = new StdioServerTransport(serverOptions);
        await using var server = McpServer.Create(transport, serverOptions);
        await server.RunAsync(ct);
        return 0;
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
