#nullable enable

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using AiNetLinter.Logging;

namespace AiNetLinter.Mcp.Daemon;

internal sealed class DaemonMcpSession
{
    private readonly Func<DaemonRuntimeContext?, McpServerPrimitiveCollection<McpServerTool>> toolCollectionFactory;
    private readonly Func<McpServerResourceCollection> resourceCollectionFactory;

    internal DaemonMcpSession(
        Func<DaemonRuntimeContext?, McpServerPrimitiveCollection<McpServerTool>> toolCollectionFactory,
        Func<McpServerResourceCollection> resourceCollectionFactory)
    {
        ArgumentNullException.ThrowIfNull(toolCollectionFactory);
        ArgumentNullException.ThrowIfNull(resourceCollectionFactory);
        this.toolCollectionFactory = toolCollectionFactory;
        this.resourceCollectionFactory = resourceCollectionFactory;
    }

    internal async Task RunAsync(DaemonPipeConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

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
        serverOptions.ToolCollection = toolCollectionFactory(runtimeContext);
        serverOptions.ResourceCollection = resourceCollectionFactory();

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
