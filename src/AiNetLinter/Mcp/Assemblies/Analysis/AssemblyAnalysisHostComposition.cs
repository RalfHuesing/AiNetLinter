#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed class AssemblyAnalysisHostComposition : IAsyncDisposable
{
    private readonly ExternalResourceRegistry resources;
    private readonly IAssemblyAnalysisRegistry sessions;
    private readonly object lifecycleGate = new();
    private Task? disposalTask;
    private int disposed;

    internal AssemblyAnalysisHostComposition(
        AssemblyAnalysisHostConfiguration configuration,
        AssemblyAnalysisHostDependencies dependencies)
    {
        ConfigurationResult = configuration;
        resources = dependencies.Resources;
        sessions = dependencies.Sessions;
    }

    internal AssemblyAnalysisHostConfiguration ConfigurationResult { get; }

    internal ExternalResourceRegistry Resources => resources;

    internal IAssemblyAnalysisRegistry Sessions
    {
        get
        {
            ThrowIfDisposed();
            return sessions;
        }
    }

    internal bool IsDisposed => Volatile.Read(ref disposed) != 0;

    internal static AssemblyAnalysisHostComposition Create(
        string? settingsPath = null,
        ExternalResourceRegistryOverrides? resourceOverrides = null)
        => Create(new AssemblyAnalysisHostCreationParameters(
            settingsPath,
            resourceOverrides));

    internal static AssemblyAnalysisHostComposition Create(
        AssemblyAnalysisHostCreationParameters parameters)
        => AssemblyAnalysisHostFactory.Create(parameters);

    public ValueTask DisposeAsync() => new(StartDispose());

    private Task StartDispose()
    {
        lock (lifecycleGate)
        {
            disposalTask ??= DisposeCoreAsync();
            return disposalTask;
        }
    }

    private async Task DisposeCoreAsync()
    {
        Interlocked.Exchange(ref disposed, 1);
        var failures = new List<Exception>();
        try
        {
            await sessions.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        try
        {
            resources.Dispose();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        DisposeFailureAggregator.ThrowIfAny(failures);
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(AssemblyAnalysisHostComposition));
        }
    }
}

internal sealed record AssemblyAnalysisHostConfiguration(
    bool Succeeded,
    AssemblyAnalysisConfigurationOptions? AssemblyAnalysis = null,
    IReadOnlyList<AssemblyAnalysisConfigurationDiagnostic>? AssemblyAnalysisDiagnostics = null);

internal sealed class AssemblyAnalysisHostDependencies
{
    internal required ExternalResourceRegistry Resources { get; init; }

    internal required IAssemblyAnalysisRegistry Sessions { get; init; }
}

internal sealed record AssemblyAnalysisHostCreationParameters(
    string? SettingsPath = null,
    ExternalResourceRegistryOverrides? ResourceOverrides = null,
    string? DaemonProfile = null);

internal static class AssemblyAnalysisHostFactory
{
    internal static AssemblyAnalysisHostComposition Create(
        AssemblyAnalysisHostCreationParameters parameters)
    {
        var assemblyConfigurationResult = AssemblyAnalysisConfigurationLoader.Load(parameters.SettingsPath);
        var resourceOptions = ExternalResourceRegistryOptionsFactory.Create(
            parameters.ResourceOverrides);
        var resources = new ExternalResourceRegistry(resourceOptions);
        var assemblyDecompilationConfiguration = CreateDecompilationConfiguration(
            assemblyConfigurationResult,
            parameters.DaemonProfile);
        var sessions = CreateRegistry(
            resources,
            assemblyDecompilationConfiguration,
            parameters.DaemonProfile);
        return new AssemblyAnalysisHostComposition(
            new AssemblyAnalysisHostConfiguration(
                assemblyConfigurationResult.Succeeded,
                assemblyConfigurationResult.Options,
                assemblyConfigurationResult.Diagnostics),
            new AssemblyAnalysisHostDependencies
            {
                Resources = resources,
                Sessions = sessions,
            });
    }

    private static AssemblyDecompilationConfiguration CreateDecompilationConfiguration(
        AssemblyAnalysisConfigurationLoadResult configurationResult,
        string? daemonProfile)
    {
        var cacheRoot = configurationResult.Options.CacheRoot;
        if (daemonProfile is not null)
        {
            cacheRoot += "." + AiNetLinter.Mcp.Daemon.DaemonInstanceId.Normalize(daemonProfile);
        }

        return new(
            new AssemblyDecompilationOptions(Timeout: configurationResult.Options.DecompilationTimeout),
            cacheRoot,
            configurationResult.Options.ResponseBudgetBytes);
    }

    private static AssemblyAnalysisRegistry CreateRegistry(
        ExternalResourceRegistry resources,
        AssemblyDecompilationConfiguration decompilationConfiguration,
        string? daemonProfile) =>
        new(
            null,
            resources,
            null,
            new AssemblyAnalysisRegistryRuntimeOptions(
                decompilationConfiguration,
                daemonProfile is null
                    ? null
                    : AiNetLinter.Mcp.Daemon.DaemonInstanceId.Normalize(daemonProfile)));
}
