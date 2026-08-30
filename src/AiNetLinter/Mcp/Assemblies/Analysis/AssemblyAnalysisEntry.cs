#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed class AssemblyAnalysisEntry : IAsyncDisposable
{
    private readonly object gate = new();
    private readonly IDisposable? lifetime;
    private readonly TaskCompletionSource<object?> leaseDrain = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? disposeTask;
    private int leaseCount;
    private bool closing;

    internal AssemblyAnalysisEntry(
        string canonicalPath,
        McpCodeGraphServer server,
        AssemblyContext context,
        IDisposable? lifetime)
    {
        CanonicalPath = canonicalPath;
        Server = server;
        Context = context;
        this.lifetime = lifetime;
    }

    internal static AssemblyAnalysisEntry Create(
        string canonicalPath,
        Microsoft.CodeAnalysis.Solution solution,
        AssemblyContext context,
        IDisposable? lifetime) =>
        new(
            canonicalPath,
            CreateReadOnlyServer(solution, context),
            context,
            lifetime);

    internal string CanonicalPath { get; }
    internal McpCodeGraphServer Server { get; }
    internal AssemblyContext Context { get; }
    internal string ContentHash => Context.Origin.ContentHash;

    internal bool Matches(AssemblyFingerprint fingerprint) =>
        string.Equals(ContentHash, fingerprint.Sha256, StringComparison.OrdinalIgnoreCase);

    internal bool TryAcquireLease(out AssemblyAnalysisLease? lease)
    {
        lock (gate)
        {
            if (closing)
            {
                lease = null;
                return false;
            }

            leaseCount++;
            lease = new(this, CanonicalPath, Server, Context);
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
    private int disposed;

    internal AssemblyAnalysisLease(
        AssemblyAnalysisEntry entry,
        string canonicalPath,
        McpCodeGraphServer server,
        AssemblyContext context)
    {
        this.entry = entry;
        CanonicalPath = canonicalPath;
        Server = server;
        Context = context;
    }

    internal string CanonicalPath { get; }
    internal McpCodeGraphServer Server { get; }
    internal AssemblyContext Context { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0) entry.ReleaseLease();
    }
}

internal sealed record AssemblyAnalysisLeaseResult(
    AssemblyAnalysisLease? Lease,
    CallToolResult? Error);
