#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Providers;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;

namespace AiNetLinter.TestKit;

internal sealed class AssemblyAnalysisRecordingProvider : IExternalSourceProvider
{
    private readonly Queue<ExternalSourceProviderResult>? results;
    private readonly Func<ExternalSourceMapping, CancellationToken, ExternalSourceProviderResult>? callback;

    internal AssemblyAnalysisRecordingProvider(params ExternalSourceProviderResult[] results)
    {
        this.results = new Queue<ExternalSourceProviderResult>(results);
    }

    internal AssemblyAnalysisRecordingProvider(
        Func<ExternalSourceMapping, CancellationToken, ExternalSourceProviderResult> callback)
    {
        this.callback = callback;
    }

    internal int CallCount { get; private set; }

    internal ExternalSourceMapping? Mapping { get; private set; }

    internal CancellationToken CancellationToken { get; private set; }

    public ValueTask<ExternalSourceProviderResult> ResolveAsync(
        ExternalSourceMapping mapping,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        Mapping = mapping;
        CancellationToken = cancellationToken;
        if (callback is not null)
        {
            return ValueTask.FromResult(callback(mapping, cancellationToken));
        }

        return ValueTask.FromResult(results!.Dequeue());
    }
}

internal sealed class AssemblyAnalysisAcquisitionFailureProvider : IExternalSourceProvider
{
    private readonly ExternalSourceRepositoryAcquisitionResult acquisition;

    internal AssemblyAnalysisAcquisitionFailureProvider(
        ExternalSourceRepositoryAcquisitionResult acquisition)
    {
        this.acquisition = acquisition;
    }

    public ValueTask<ExternalSourceProviderResult> ResolveAsync(
        ExternalSourceMapping mapping,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(
            ExternalSourceProviderFailureProjection.FromUnavailableAcquisition(acquisition));
    }
}
