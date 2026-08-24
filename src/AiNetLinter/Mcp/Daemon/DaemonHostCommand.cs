#nullable enable

using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RalfHuesing.Mcp.Observability;

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
            MruStateStore.DefaultFilePath,
            TimeProvider.System,
            daemonConsole.WriteError,
            MaxProjects: maxProjects));
        var projectRegistry = new ProjectRegistry(new ProjectRegistryOptions(
            definition => McpServerCommand.CreateResidentInstance(definition, daemonConsole),
            TimeProvider.System,
            maxProjects,
            args.McpProjectTtlMinutes is { } ttl ? TimeSpan.FromMinutes((double)ttl) : default));
        var registry = new DaemonRegistryAdapter(projectRegistry);
        var host = new DaemonHost(new DaemonHostOptions(
            registry,
            mru,
            new DaemonPipeTransport(),
            TimeProvider.System,
            TimeSpan.FromMinutes((double)idleMinutes),
            new EffectiveDaemonConfiguration(maxProjects, idleMinutes, args.McpLogPath ?? DaemonProtocol.DefaultLogTarget),
            daemonConsole,
            SessionRunner: CreateSessionRunner(projectRegistry)));

        await using var activeHost = host;
        return await activeHost.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Func<DaemonPipeConnection, Task> CreateSessionRunner(
        ProjectRegistry registry) =>
        connection => RunMcpSessionAsync(connection, registry);

    internal static async Task RunMcpSessionAsync(
        DaemonPipeConnection connection,
        ProjectRegistry registry)
    {
        var services = new ServiceCollection();
        services.AddMcpServer().WithObservability(
            McpServerCommand.ResolveObservabilityOptions(null, null));
        await using var serviceProvider = services.BuildServiceProvider();
        var serverOptions = serviceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        serverOptions.ServerInfo = new Implementation
        {
            Name = McpServerOptionsFactory.ServerName,
            Version = McpServerOptionsFactory.GetServerVersion(),
        };
        serverOptions.ServerInstructions = ServerInstructions.Text;
        serverOptions.ToolCollection = McpServerOptionsFactory.BuildToolCollection(registry, serviceProvider);
        serverOptions.ResourceCollection = McpServerOptionsFactory.BuildResourceCollection(registry);

        var transport = new StreamServerTransport(connection.Stream, connection.Stream);
        await using var server = McpServer.Create(transport, serverOptions, serviceProvider: serviceProvider);
        await server.RunAsync(connection.CancellationToken).ConfigureAwait(false);
    }
}
