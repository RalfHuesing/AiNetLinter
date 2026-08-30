#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Snapshots;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed class AssemblyAnalysisHostComposition : IAsyncDisposable
{
    private readonly SourceSnapshotRegistry registry;
    private readonly IAssemblySourceSelectionResolver orchestrator;
    private readonly IAssemblySourceResolver registryResolver;
    private readonly AssemblyAnalysisRegistry sessions;
    private readonly object lifecycleGate = new();
    private Task? disposalTask;
    private int disposed;

    private AssemblyAnalysisHostComposition(
        ExternalSourceConfigurationLoadResult configurationResult,
        IExternalSourceProvider provider,
        SourceSnapshotRegistry registry)
    {
        ConfigurationResult = configurationResult;
        Provider = provider;
        this.registry = registry;
        var sourceOrchestrator = new AssemblySourceSelectionOrchestrator(
            configurationResult,
            provider,
            registry);
        orchestrator = sourceOrchestrator;
        registryResolver = sourceOrchestrator;
        sessions = new AssemblyAnalysisRegistry(registryResolver);
    }

    internal ExternalSourceConfigurationLoadResult ConfigurationResult { get; }

    internal IExternalSourceProvider Provider { get; }

    internal SourceSnapshotRegistry Registry => registry;

    internal AssemblyAnalysisRegistry Sessions
    {
        get
        {
            ThrowIfDisposed();
            return sessions;
        }
    }

    internal IAssemblySourceSelectionResolver Orchestrator
    {
        get
        {
            ThrowIfDisposed();
            return orchestrator;
        }
    }

    internal bool IsDisposed => Volatile.Read(ref disposed) != 0;

    internal static AssemblyAnalysisHostComposition Create(
        string? settingsPath = null,
        IExternalSourceProvider? provider = null,
        IExternalSourceCredentialResolver? credentialResolver = null)
    {
        var configurationResult = ExternalSourceConfigurationLoader.Load(settingsPath);
        var sourceProvider = provider ?? CreateDefaultProvider(
            configurationResult,
            credentialResolver);
        var registry = new SourceSnapshotRegistry();
        return new AssemblyAnalysisHostComposition(configurationResult, sourceProvider, registry);
    }

    private static IExternalSourceProvider CreateDefaultProvider(
        ExternalSourceConfigurationLoadResult configurationResult,
        IExternalSourceCredentialResolver? credentialResolver)
    {
        var cacheOptions = configurationResult.Configuration?.CacheOptions
            ?? ExternalSourceCacheOptions.Default;
        var cacheConstruction = ExternalSourceRepositoryCacheOptionsFactory.Create(cacheOptions);
        var stagingRoot = Path.Combine(
            cacheConstruction.CacheRoot,
            ExternalSourceRepositoryCacheContract.CheckoutDirectoryName);
        var transport = new GiteaGitRepositoryTransport(credentialResolver);
        var acquirer = ExternalSourceRepositoryAcquirerFactory.CreateConfigured(
            transport,
            stagingRoot,
            cacheOptions,
            cacheConstruction.CreateRefreshPolicy());
        return new GiteaExternalSourceProvider(
            acquirer,
            new ExternalSourceSnapshotMaterializer());
    }

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
            registry.Dispose();
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
