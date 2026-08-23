#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Cli;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Lifetime;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RalfHuesing.Mcp.Observability;

namespace AiNetLinter.Commands;

/// <summary>
/// Startet einen stdio-basierten MCP-Server ohne eigenen Projektbezug in der Client-Konfiguration:
/// Jeder Tool-/Resource-Aufruf adressiert per absolutem <c>projectRoot</c> einen Lease-geschuetzten
/// Key der <see cref="ProjectRegistry"/>, dessen Instanzen lazy aus einer Definitionsdatei
/// (<c>ainetlinter.project.json</c> im Projektroot) entstehen. Laeuft, bis der Client die
/// Verbindung trennt oder das Cancellation-Token signalisiert wird.
/// </summary>
internal static class McpServerCommand
{
    /// <summary>
    /// Validiert die CLI-Argumente (inklusive hartem Cut fuer --path/--config im MCP-Modus),
    /// startet die Projektregistry und danach den MCP-Server mit dem registrierten Tool-Set.
    /// Der Solution-Load je Key laeuft im Hintergrund — der MCP-Transport antwortet auf
    /// <c>initialize</c> sofort, Tools waehrend des Loads reagieren mit
    /// <see cref="McpToolResults.Loading"/>.
    /// </summary>
    internal static async Task<int> RunAsync(LinterArgs args, CancellationToken ct = default, ILintConsole? console = null)
    {
        var c = console ?? LinterConsole.Instance;
        var validationError = args.Validate();
        if (validationError is not null)
        {
            c.WriteError(validationError);
            return 1;
        }

        await using var lifetime = McpServerLifetime.Start(args.ParentPid, ct, c.WriteError);
        await using var registry = new ProjectRegistry(new ProjectRegistryOptions(
            InstanceFactory: definition => CreateResidentInstance(definition, c),
            Clock: TimeProvider.System,
            MaxProjects: args.McpMaxProjects ?? ProjectRegistryDefaults.MaxProjects,
            IdleTtl: args.McpProjectTtlMinutes is { } minutes ? TimeSpan.FromMinutes((double)minutes) : default));

        var obsOptions = ResolveObservabilityOptions(args.McpLogPath, solutionPath: null);

        var services = new ServiceCollection();
        var builder = services.AddMcpServer();
        builder.WithObservability(obsOptions);

        using var serviceProvider = services.BuildServiceProvider();
        var serverOptions = serviceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        serverOptions.ServerInfo = new Implementation
        {
            Name = McpServerOptionsFactory.ServerName,
            Version = McpServerOptionsFactory.GetServerVersion(),
        };
        serverOptions.ServerInstructions = ServerInstructions.Text;
        serverOptions.ToolCollection = McpServerOptionsFactory.BuildToolCollection(registry, serviceProvider);
        serverOptions.ResourceCollection = McpServerOptionsFactory.BuildResourceCollection(registry);

        var transport = new StdioServerTransport(serverOptions);
        await using var server = McpServer.Create(transport, serverOptions, serviceProvider: serviceProvider);
        try
        {
            await server.RunAsync(lifetime.Token);
        }
        catch (OperationCanceledException) when (lifetime.Token.IsCancellationRequested)
        {
        }

        return 0;
    }

    /// <summary>
    /// Komposition des Factory-Delegaten je Key: Die Regeldatei wird streng geladen (eine lesbare,
    /// aber ungueltige rules.json scheitert deterministisch statt Default-Regeln einzusetzen);
    /// erst bei Erfolg entsteht eine Server-Instanz, deren Hintergrund-Load die Solution der
    /// Definition laedt. Dedupe und Lock-Hygiene liegen in der Registry bzw. im Instanzmuster.
    /// </summary>
    private static ProjectInstanceCreation CreateResidentInstance(ProjectDefinition definition, ILintConsole console) =>
        ProjectInstanceFactory.TryCreate(
            definition,
            baseOptions => ProjectInstanceCreation.Resident(new McpCodeGraphServer(new McpCodeGraphServerOptions
            {
                Catalog = null,
                Console = console,
                MaxLineCount = baseOptions.MaxLineCount,
                Config = baseOptions.Config,
                UsedDefaultConfig = false,
                ResolvedConfigPath = baseOptions.ResolvedConfigPath,
                LoadFunc = innerCt => TryLoadSolutionAsync(definition.SolutionPath, innerCt, console),
            })));

    /// <summary>
    /// Loest die <see cref="McpObservabilityOptions"/> anhand des CLI-Wertes <paramref name="mcpLogPath"/> auf.
    /// Default (<c>null</c>): Observability und Feedback-Tool sind aktiv, LogDirectory ist null (schreibt
    /// in Standardordner %LOCALAPPDATA%\RalfHuesing\McpObservability\ainetlinter\).
    /// Bei Werten wie "off", "false", "disabled", "none" wird Observability komplett deaktiviert.
    /// Bei einem Pfad wird das angegebene Verzeichnis verwendet (relativ zum Solution-Pfad oder absolut).
    /// </summary>
    internal static McpObservabilityOptions ResolveObservabilityOptions(string? mcpLogPath, string? solutionPath)
    {
        var serverVersion = McpServerOptionsFactory.GetServerVersion();

        if (IsObservabilityDisabledKeyword(mcpLogPath))
        {
            return new McpObservabilityOptions
            {
                Enabled = false,
                EnableToolCallLogging = false,
                EnableFeedbackTool = false,
                EnableResponseLogging = false,
                ServerName = McpServerOptionsFactory.ServerName,
                ServerVersion = serverVersion,
                LogDirectory = null,
            };
        }

        var logDirectory = string.IsNullOrWhiteSpace(mcpLogPath)
            ? null
            : ResolveMcpLogPath(mcpLogPath.Trim(), solutionPath ?? string.Empty);

        return new McpObservabilityOptions
        {
            Enabled = true,
            EnableToolCallLogging = true,
            EnableFeedbackTool = true,
            EnableResponseLogging = true,
            ServerName = McpServerOptionsFactory.ServerName,
            ServerVersion = serverVersion,
            LogDirectory = logDirectory,
        };
    }

    private static bool IsObservabilityDisabledKeyword(string? mcpLogPath)
    {
        if (mcpLogPath is null) return false;
        var trimmed = mcpLogPath.Trim();
        return trimmed.Equals("off", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("false", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("disabled", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("none", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Aufloesung des expliziten Log-Verzeichnisses: absolut -> wie angegeben; relativ -> relativ zum
    /// Solution-Verzeichnis (nicht zum exeDir, damit die Log-Dateien zur Solution gehoeren).
    /// </summary>
    internal static string ResolveMcpLogPath(string mcpLogPath, string solutionPath)
    {
        if (Path.IsPathRooted(mcpLogPath)) return Path.GetFullPath(mcpLogPath);
        var solutionDir = Path.GetDirectoryName(solutionPath) ?? string.Empty;
        return Path.GetFullPath(Path.Combine(solutionDir, mcpLogPath));
    }

    /// <summary>
    /// Loest den konfigurierten Zeilen-Grenzwert auf — bei gesetztem <paramref name="resolvedConfigPath"/>
    /// wird die zugehoerige <c>rules.json</c> geladen (best effort), sonst der
    /// <see cref="MetricsConfig"/>-Default verwendet — derselbe Grenzwert, den auch ein CLI-Lint-Lauf
    /// auf derselben Solution respektieren wuerde. Das Laden der Regeldatei inklusive Defaults
    /// delegiert an <see cref="AiNetLinter.Mcp.Projects.ProjectInstanceFactory.MaterializeRules"/>,
    /// denselben geteilten Kern wie der Registry-Pfad.
    /// </summary>
    internal static int ResolveMaxLineCount(LinterArgs args, string? resolvedConfigPath = null) =>
        ProjectInstanceFactory.MaterializeRules(
            resolvedConfigPath ?? args.ConfigPath,
            isRequired: false).MaxLineCount;

    /// <summary>
    /// Loest die vollstaendige Linter-<see cref="Config"/> auf — bei gesetztem
    /// <paramref name="resolvedConfigPath"/> wird die zugehoerige <c>rules.json</c> geladen (best
    /// effort), sonst der <see cref="Config"/>-Default verwendet — dieselbe Config, die auch ein
    /// CLI-Lint-Lauf auf derselben Solution respektieren wuerde. Das Laden der Regeldatei inklusive
    /// Defaults delegiert an
    /// <see cref="AiNetLinter.Mcp.Projects.ProjectInstanceFactory.MaterializeRules"/>, denselben
    /// geteilten Kern wie der Registry-Pfad.
    /// </summary>
    internal static Config ResolveConfig(LinterArgs args, string? resolvedConfigPath = null) =>
        ProjectInstanceFactory.MaterializeRules(
            resolvedConfigPath ?? args.ConfigPath,
            isRequired: false).Config;

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
