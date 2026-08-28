#nullable enable

using System;
using System.Threading;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed class AssemblyAnalysisHostComposition : IDisposable
{
    private readonly SourceSnapshotRegistry registry;
    private readonly AssemblySourceSelectionOrchestrator orchestrator;
    private int disposed;

    private AssemblyAnalysisHostComposition(
        ExternalSourceConfigurationLoadResult configurationResult,
        IExternalSourceProvider provider,
        SourceSnapshotRegistry registry)
    {
        ConfigurationResult = configurationResult;
        Provider = provider;
        this.registry = registry;
        orchestrator = new AssemblySourceSelectionOrchestrator(
            configurationResult,
            provider,
            registry);
    }

    internal ExternalSourceConfigurationLoadResult ConfigurationResult { get; }

    internal IExternalSourceProvider Provider { get; }

    internal SourceSnapshotRegistry Registry => registry;

    internal AssemblySourceSelectionOrchestrator Orchestrator
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
        IExternalSourceProvider? provider = null)
    {
        var configurationResult = ExternalSourceConfigurationLoader.Load(settingsPath);
        var sourceProvider = provider ?? new UnavailableExternalSourceProvider();
        var registry = new SourceSnapshotRegistry();
        return new AssemblyAnalysisHostComposition(configurationResult, sourceProvider, registry);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        registry.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(AssemblyAnalysisHostComposition));
        }
    }
}
