#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed record AssemblyAnalysisEntryCreateParameters(
    string CanonicalPath,
    Microsoft.CodeAnalysis.Solution Solution,
    AssemblyContext Context,
    IDisposable? Lifetime,
    ExternalResourceLease? ResourceLease = null);

internal sealed class AssemblyAnalysisEntry : IAsyncDisposable
{
    private readonly object gate = new();
    private readonly IDisposable? lifetime;
    private readonly ExternalResourceLease? resourceLease;
    private readonly TaskCompletionSource<object?> leaseDrain = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private DateTime lastUsedUtc = DateTime.UtcNow;
    private Task? disposeTask;
    private int leaseCount;
    private bool closing;

    internal AssemblyAnalysisEntry(
        string canonicalPath,
        McpCodeGraphServer server,
        AssemblyContext context,
        IDisposable? lifetime,
        ExternalResourceLease? resourceLease = null)
    {
        CanonicalPath = canonicalPath;
        Server = server;
        Context = context;
        this.lifetime = lifetime;
        this.resourceLease = resourceLease;
    }

    internal static AssemblyAnalysisEntry Create(AssemblyAnalysisEntryCreateParameters parameters) =>
        new(
            parameters.CanonicalPath,
            CreateReadOnlyServer(parameters.Solution, parameters.Context),
            parameters.Context,
            parameters.Lifetime,
            parameters.ResourceLease);

    internal string CanonicalPath { get; }
    internal McpCodeGraphServer Server { get; }
    internal AssemblyContext Context { get; }
    internal string ContentHash => Context.Origin.ContentHash;

    internal bool Matches(AssemblyFingerprint fingerprint) =>
        string.Equals(ContentHash, fingerprint.Sha256, StringComparison.OrdinalIgnoreCase);

    internal bool TryAcquireLease(out AssemblyAnalysisLease? lease)
    {
        return TryAcquireLease(null, out lease);
    }

    internal bool TryAcquireLease(
        Func<AssemblyReferenceDto, CancellationToken, Task<AssemblyAnalysisLeaseResult>>? referenceLeaseFactory,
        out AssemblyAnalysisLease? lease)
    {
        lock (gate)
        {
            if (closing)
            {
                lease = null;
                return false;
            }

            leaseCount++;
            lastUsedUtc = DateTime.UtcNow;
            lease = new(this, CanonicalPath, Server, Context, referenceLeaseFactory);
            return true;
        }
    }

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource<object?>? completion = null;
        Task? drain = null;
        lock (gate)
        {
            if (disposeTask is null)
            {
                closing = true;
                completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                disposeTask = completion.Task;
                drain = leaseCount == 0 ? Task.CompletedTask : leaseDrain.Task;
            }
        }

        if (completion is not null)
        {
            _ = DisposeAfterDrainAsync(drain!, completion);
        }

        return new(disposeTask!);
    }

    private async Task DisposeAfterDrainAsync(
        Task drain,
        TaskCompletionSource<object?> completion)
    {
        try
        {
            await drain.ConfigureAwait(false);
            var failures = new List<Exception>();
            try
            {
                await Server.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            try
            {
                resourceLease?.Dispose();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            try
            {
                if (lifetime is IAsyncDisposable asyncLifetime)
                {
                    await asyncLifetime.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    lifetime?.Dispose();
                }
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            if (failures.Count > 0)
            {
                completion.TrySetException(failures.Count == 1 ? failures[0] : new AggregateException(failures));
            }
            else
            {
                completion.TrySetResult(null);
            }
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    internal void ReleaseLease()
    {
        lock (gate)
        {
            if (leaseCount == 0) return;
            leaseCount--;
            if (closing && leaseCount == 0)
            {
                leaseDrain.TrySetResult(null);
            }
        }
    }

    internal bool IsIdle(DateTime now, TimeSpan idleTtl)
    {
        lock (gate)
        {
            return !closing && leaseCount == 0 && now - lastUsedUtc > idleTtl;
        }
    }

    private static McpCodeGraphServer CreateReadOnlyServer(
        Microsoft.CodeAnalysis.Solution solution,
        AssemblyContext context) =>
        new(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(
            Catalog: null,
            Config: new Config
            {
                Global = new GlobalConfig(),
                Metrics = new MetricsConfig(),
            },
            ReadOnlySolutionSnapshot: solution,
            AssemblySymbolIdentity: new AnalysisSymbolIdentity(
                context.Origin.ContentHash,
                context.Generation))));
}

internal sealed class AssemblyAnalysisLease : IDisposable
{
    private readonly AssemblyAnalysisEntry entry;
    private readonly Func<AssemblyReferenceDto, CancellationToken, Task<AssemblyAnalysisLeaseResult>>? referenceLeaseFactory;
    private int disposed;

    internal AssemblyAnalysisLease(
        AssemblyAnalysisEntry entry,
        string canonicalPath,
        McpCodeGraphServer server,
        AssemblyContext context,
        Func<AssemblyReferenceDto, CancellationToken, Task<AssemblyAnalysisLeaseResult>>? referenceLeaseFactory = null)
    {
        this.entry = entry;
        CanonicalPath = canonicalPath;
        Server = server;
        Context = context;
        this.referenceLeaseFactory = referenceLeaseFactory;
    }

    internal string CanonicalPath { get; }
    internal McpCodeGraphServer Server { get; }
    internal AssemblyContext Context { get; }

    internal Task<AssemblyAnalysisLeaseResult> LeaseReferenceAsync(
        AssemblyReferenceDto reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (referenceLeaseFactory is null)
        {
            return Task.FromResult(new AssemblyAnalysisLeaseResult(
                null,
                McpToolResults.Recoverable(
                    LinterErrorCodes.AnalysisFailed,
                    "Für diesen Assembly-Lease ist keine Referenzauflösung verfügbar.")));
        }

        if (!reference.Resolved
            || string.IsNullOrWhiteSpace(reference.ResolvedPath)
            || !Context.References.Any(candidate =>
                string.Equals(candidate.Name, reference.Name, StringComparison.Ordinal)
                && string.Equals(candidate.Version, reference.Version, StringComparison.Ordinal)
                && string.Equals(candidate.Culture, reference.Culture, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.ResolvedPath, reference.ResolvedPath, StringComparison.OrdinalIgnoreCase)
                && candidate.Depth == reference.Depth))
        {
            return Task.FromResult(new AssemblyAnalysisLeaseResult(
                null,
                McpToolResults.Recoverable(
                    LinterErrorCodes.AnalysisFailed,
                    $"Die Referenz '{reference.Name}' ist nicht als analysierbares Ziel aufgelöst.")));
        }

        return referenceLeaseFactory(reference, cancellationToken);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0) entry.ReleaseLease();
    }
}

internal sealed record AssemblyAnalysisLeaseResult(
    AssemblyAnalysisLease? Lease,
    CallToolResult? Error);
