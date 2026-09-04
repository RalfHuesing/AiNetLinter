#nullable enable

using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.Logging;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Composition;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
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
        await using var assemblyComposition = AssemblyAnalysisHostComposition.Create(
            new AssemblyAnalysisHostCreationParameters(
                ResourceOverrides: new ExternalResourceRegistryOverrides(
                    args.McpExternalMaxDiskBytes,
                    args.McpExternalMaxMemoryBytes,
                    args.McpExternalMaxParallelOperations,
                    args.McpExternalMaxResidentResources,
                    args.McpExternalIdleTtlMinutes),
                DaemonProfile: args.DaemonInstance));
        var session = new DaemonMcpSession(
            runtimeContext => McpServerToolCollectionFactory.Build(
                projectRegistry,
                AnalysisToolCall.CreateTargetRoute(
                    ProjectAnalysisDispatcher.CreateRoute(projectRegistry),
                    AssemblyAnalysisDispatcher.CreateRoute(assemblyComposition.Sessions)),
                runtimeContext,
                assemblyComposition.Sessions),
            () => McpServerResourceCollectionFactory.Build(projectRegistry));
        var registry = new DaemonRegistryAdapter(projectRegistry);
        var host = new DaemonHost(new DaemonHostOptions(
            registry,
            mru,
            new DaemonPipeTransport(daemonInstance: args.DaemonInstance),
            TimeProvider.System,
            TimeSpan.FromMinutes((double)idleMinutes),
            CreateDaemonConfiguration(assemblyComposition, maxProjects, idleMinutes),
            daemonConsole,
            SessionRunner: session.RunAsync,
            DaemonProfile: DaemonInstanceId.Normalize(args.DaemonInstance)));

        await using var activeHost = host;
        Log.Information("Daemon: Host startet (IdleExit={IdleExitMinutes} Min, MaxProjects={MaxProjects})", idleMinutes, maxProjects);
        var exitCode = await activeHost.RunAsync(cancellationToken).ConfigureAwait(false);
        Log.Information("Daemon: Host beendet, ExitCode={ExitCode}", exitCode);
        return exitCode;
    }

    private static EffectiveDaemonConfiguration CreateDaemonConfiguration(
        AssemblyAnalysisHostComposition composition,
        int maxProjects,
        decimal idleMinutes)
    {
        var externalHealth = composition.Resources.Health;
        var externalIdleMinutes = composition.Resources.IdleTtl.Ticks
            / (decimal)TimeSpan.TicksPerMinute;
        return new(
            maxProjects,
            idleMinutes,
            externalHealth.MaxDiskBytes,
            externalHealth.MaxMemoryBytes,
            externalHealth.MaxParallelOperations,
            externalHealth.MaxResidentResources,
            externalIdleMinutes);
    }
}
