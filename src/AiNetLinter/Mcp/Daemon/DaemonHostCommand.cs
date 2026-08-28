#nullable enable

using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.Logging;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Serilog;

namespace AiNetLinter.Mcp.Daemon;

internal static class DaemonHostCommand
{
    internal static async Task<int> RunAsync(
        LinterArgs args,
        CancellationToken cancellationToken = default,
        ILintConsole? console = null)
    {
        var daemonConsole = console ?? LinterConsole.Instance;
        var validationError = args.Validate();
        if (validationError is not null)
        {
            daemonConsole.WriteError(validationError);
            return 1;
        }

        var maxProjects = args.McpMaxProjects ?? DaemonProtocol.DefaultMaxProjects;
        var idleMinutes = args.McpDaemonIdleExitMinutes ?? DaemonProtocol.DefaultIdleExitMinutes;
        var mru = new MruStateStore(new MruStateStoreOptions(
            MruStateStore.GetFilePath(args.DaemonInstance),
            TimeProvider.System,
            daemonConsole.WriteError,
            MaxProjects: maxProjects));
        var projectRegistry = new ProjectRegistry(new ProjectRegistryOptions(
            definition => McpServerCommand.CreateResidentInstance(definition, daemonConsole),
            TimeProvider.System,
            maxProjects,
            args.McpProjectTtlMinutes is { } ttl ? TimeSpan.FromMinutes((double)ttl) : default));
        using var assemblyComposition = AssemblyAnalysisHostComposition.Create();
        var registry = new DaemonRegistryAdapter(projectRegistry);
        var host = new DaemonHost(new DaemonHostOptions(
            registry,
            mru,
            new DaemonPipeTransport(daemonInstance: args.DaemonInstance),
            TimeProvider.System,
            TimeSpan.FromMinutes((double)idleMinutes),
            new EffectiveDaemonConfiguration(maxProjects, idleMinutes),
            daemonConsole,
            SessionRunner: CreateSessionRunner(projectRegistry, assemblyComposition)));

        await using var activeHost = host;
        Log.Information("Daemon: Host startet (IdleExit={IdleExitMinutes} Min, MaxProjects={MaxProjects})", idleMinutes, maxProjects);
        var exitCode = await activeHost.RunAsync(cancellationToken).ConfigureAwait(false);
        Log.Information("Daemon: Host beendet, ExitCode={ExitCode}", exitCode);
        return exitCode;
    }

    private static Func<DaemonPipeConnection, Task> CreateSessionRunner(
        ProjectRegistry registry,
        AssemblyAnalysisHostComposition assemblyComposition) =>
        connection => RunMcpSessionAsync(connection, registry, assemblyComposition);

    internal static async Task RunMcpSessionAsync(
        DaemonPipeConnection connection,
        ProjectRegistry registry,
        AssemblyAnalysisHostComposition assemblyComposition)
    {
        var services = new ServiceCollection();
        var runtimeContext = connection.RuntimeContext;
        Serilog.Log.Debug("Daemon: MCP-Session startet (ConnectionId={ConnectionId})", runtimeContext?.ConnectionId);
        var serverBuilder = services.AddMcpServer();
        McpCallLoggingFilter.Configure(serverBuilder, runtimeContext?.ConnectionId);
        await using var serviceProvider = services.BuildServiceProvider();
        var serverOptions = serviceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        serverOptions.ServerInfo = new Implementation
        {
            Name = McpServerOptionsFactory.ServerName,
            Version = McpServerOptionsFactory.GetServerVersion(),
        };
        serverOptions.ServerInstructions = ServerInstructions.Text;
        serverOptions.ToolCollection = McpServerOptionsFactory.BuildToolCollection(
            registry,
            runtimeContext,
            assemblyComposition);
        serverOptions.ResourceCollection = McpServerOptionsFactory.BuildResourceCollection(registry);

        var transport = new StreamServerTransport(connection.Stream, connection.Stream);
        await using var server = McpServer.Create(transport, serverOptions, serviceProvider: serviceProvider);
        try
        {
            await server.RunAsync(connection.CancellationToken).ConfigureAwait(false);
            Serilog.Log.Debug("Daemon: MCP-Session normal beendet (ConnectionId={ConnectionId})", runtimeContext?.ConnectionId);
        }
        catch (OperationCanceledException) when (connection.CancellationToken.IsCancellationRequested)
        {
            Serilog.Log.Information("Daemon: MCP-Session abgebrochen (ConnectionId={ConnectionId}, Ursache=ConnectionCancellation)", runtimeContext?.ConnectionId);
        }
        catch (Exception exception)
        {
            Serilog.Log.Error(exception, "Daemon: MCP-Session mit Ausnahme beendet (ConnectionId={ConnectionId})", runtimeContext?.ConnectionId);
            throw;
        }
    }
}
