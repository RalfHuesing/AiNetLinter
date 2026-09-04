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
    private readonly IAssemblySourceSelectionSnapshotRegistry registry;
    private readonly ExternalResourceRegistry resources;
    private readonly ExternalResourceRegistry sourceResources;
    private readonly IAsyncDisposable sourceOrchestrator;
    private readonly IAssemblySourceSelectionResolver orchestrator;
    private readonly IAssemblySourceResolver registryResolver;
    private readonly IAssemblyAnalysisRegistry sessions;
    private readonly object lifecycleGate = new();
    private Task? disposalTask;
    private int disposed;

    internal AssemblyAnalysisHostComposition(
        AssemblyAnalysisHostConfiguration configuration,
        IExternalSourceProvider provider,
        AssemblyAnalysisHostDependencies dependencies)
    {
        ConfigurationResult = configuration;
        Provider = provider;
        sourceOrchestrator = dependencies.SourceOrchestrator;
        orchestrator = dependencies.Orchestrator;
        registryResolver = dependencies.RegistryResolver;
        registry = dependencies.Registry;
        resources = dependencies.Resources;
        sourceResources = dependencies.SourceResources;
        sessions = dependencies.Sessions;
    }

    internal AssemblyAnalysisHostConfiguration ConfigurationResult { get; }

    internal IExternalSourceProvider Provider { get; }

    internal IAssemblySourceSelectionSnapshotRegistry Registry => registry;

    internal ExternalResourceRegistry Resources => resources;

    internal ExternalResourceRegistry SourceResources => sourceResources;

    internal IAssemblyAnalysisRegistry Sessions
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
        IExternalSourceCredentialResolver? credentialResolver = null,
        ExternalResourceRegistryOverrides? resourceOverrides = null)
        => Create(new AssemblyAnalysisHostCreationParameters(
            settingsPath,
            provider,
            credentialResolver,
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
            await sourceOrchestrator.DisposeAsync().ConfigureAwait(false);
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

        try
        {
            sourceResources.Dispose();
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
    IReadOnlyList<ExternalSourceConfigurationDiagnostic>? AssemblyAnalysisDiagnostics = null);

internal sealed class AssemblyAnalysisHostDependencies
{
    internal required IAsyncDisposable SourceOrchestrator { get; init; }

    internal required IAssemblySourceSelectionResolver Orchestrator { get; init; }

    internal required IAssemblySourceResolver RegistryResolver { get; init; }

    internal required IAssemblySourceSelectionSnapshotRegistry Registry { get; init; }

    internal required ExternalResourceRegistry Resources { get; init; }

    internal required ExternalResourceRegistry SourceResources { get; init; }

    internal required IAssemblyAnalysisRegistry Sessions { get; init; }
}

internal sealed record AssemblyAnalysisHostCreationParameters(
    string? SettingsPath = null,
    IExternalSourceProvider? Provider = null,
    IExternalSourceCredentialResolver? CredentialResolver = null,
    ExternalResourceRegistryOverrides? ResourceOverrides = null,
    string? DaemonProfile = null);

internal sealed record AssemblyAnalysisDefaultProviderCreationParameters(
    ExternalSourceConfigurationLoadResult ConfigurationResult,
    IExternalSourceCredentialResolver? CredentialResolver,
    ExternalResourceRegistry SourceResources,
    IExternalSourceSnapshotResourceCoordinator ResourceCoordinator,
    string? DaemonProfile);

internal static class AssemblyAnalysisHostFactory
{
    internal static AssemblyAnalysisHostComposition Create(
        AssemblyAnalysisHostCreationParameters parameters)
    {
        var configurationResult = ExternalSourceConfigurationLoader.Load(parameters.SettingsPath);
        var assemblyConfigurationResult = AssemblyAnalysisConfigurationLoader.Load(parameters.SettingsPath);
        var configuredResources = configurationResult.Configuration?.CacheOptions.ResourceOptions
            ?? ExternalSourceResourceOptions.Default;
        var resourceOptions = ExternalResourceRegistryOptionsFactory.Create(
            configuredResources,
            parameters.ResourceOverrides);
        var sourceResources = new ExternalResourceRegistry(resourceOptions);
        var registry = new SourceSnapshotRegistry(sourceResources);
        var resources = new ExternalResourceRegistry(resourceOptions);
        var sourceProvider = parameters.Provider ?? AssemblyAnalysisHostProviderFactory.CreateDefaultProvider(
            new AssemblyAnalysisDefaultProviderCreationParameters(
                configurationResult,
                parameters.CredentialResolver,
                sourceResources,
                registry,
                parameters.DaemonProfile));
        var sourceConfiguration = new AssemblySourceSelectionConfiguration(
            configurationResult.Succeeded,
            configurationResult.Configuration?.Mappings ?? [],
            configurationResult.Diagnostics,
            configurationResult.Configuration?.SourceMode ?? ExternalSourceSourceMode.SourcePreferred);
        var providerCoordinator = new AssemblySourceProviderCoordinator(sourceProvider, registry);
        var sourceOrchestrator = new AssemblySourceSelectionOrchestrator(
            sourceConfiguration,
            providerCoordinator);
        var assemblyDecompilationConfiguration = CreateDecompilationConfiguration(
            assemblyConfigurationResult,
            parameters.DaemonProfile);
        var sessions = CreateRegistry(
            sourceOrchestrator,
            resources,
            assemblyDecompilationConfiguration,
            parameters.DaemonProfile);
        return new AssemblyAnalysisHostComposition(
            new AssemblyAnalysisHostConfiguration(
                configurationResult.Succeeded,
                assemblyConfigurationResult.Options,
                assemblyConfigurationResult.Diagnostics),
            sourceProvider,
            new AssemblyAnalysisHostDependencies
            {
                SourceOrchestrator = sourceOrchestrator,
                Orchestrator = sourceOrchestrator,
                RegistryResolver = sourceOrchestrator,
                Registry = registry,
                Resources = resources,
                SourceResources = sourceResources,
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
        IAssemblySourceResolver sourceOrchestrator,
        ExternalResourceRegistry resources,
        AssemblyDecompilationConfiguration decompilationConfiguration,
        string? daemonProfile) =>
        new(
            sourceOrchestrator,
            null,
            resources,
            null,
            new AssemblyAnalysisRegistryRuntimeOptions(
                decompilationConfiguration,
                daemonProfile is null
                    ? null
                    : AiNetLinter.Mcp.Daemon.DaemonInstanceId.Normalize(daemonProfile)));
}

internal static class AssemblyAnalysisHostProviderFactory
{
    internal static IExternalSourceProvider CreateDefaultProvider(
        AssemblyAnalysisDefaultProviderCreationParameters parameters)
    {
        var cacheOptions = parameters.ConfigurationResult.Configuration?.CacheOptions
            ?? ExternalSourceCacheOptions.Default;
        var cacheConstruction = ExternalSourceRepositoryCacheOptionsFactory.Create(cacheOptions, parameters.DaemonProfile);
        cacheOptions = new ExternalSourceCacheOptions(
            cacheConstruction.CacheRoot,
            cacheOptions.RefreshInterval,
            cacheOptions.ResourceOptions);
        var stagingRoot = Path.Combine(
            cacheConstruction.CacheRoot,
            ExternalSourceRepositoryCacheContract.CheckoutDirectoryName);
        var transport = new GiteaGitRepositoryTransport(parameters.CredentialResolver);
        var acquirer = ExternalSourceRepositoryAcquirerFactory.CreateConfigured(
            transport,
            stagingRoot,
            cacheOptions,
            cacheConstruction.CreateRefreshPolicy());
        return new GiteaExternalSourceProvider(
            acquirer,
            new ExternalSourceSnapshotMaterializer(parameters.SourceResources, parameters.ResourceCoordinator));
    }
}
