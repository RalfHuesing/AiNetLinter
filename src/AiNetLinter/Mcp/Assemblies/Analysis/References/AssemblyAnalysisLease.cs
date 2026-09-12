#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Handoffs;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis.References;

internal delegate Task<AssemblyAnalysisLeaseResult> AssemblyReferenceLeaseFactory(
    AssemblyReferenceDto reference,
    CancellationToken cancellationToken);

internal sealed record AssemblyReferenceLeaseContext(
    AssemblyReferenceLeaseFactory? Factory,
    Action<AssemblyAnalysisEntry>? OnReferenceLeaseReleased);

internal sealed class AssemblyAnalysisLease : IDisposable, IAssemblyBodyContext
{
    private readonly AssemblyAnalysisEntry entry;
    private readonly AssemblyReferenceLeaseFactory? referenceLeaseFactory;
    private readonly object referenceGate = new();
    private readonly List<AssemblyAnalysisLease> referenceLeases = [];
    private readonly Dictionary<string, Task<AssemblyAnalysisLeaseResult>> navigationReferenceLeaseTasks = new(StringComparer.Ordinal);
    private readonly Action<AssemblyAnalysisEntry>? onReferenceLeaseReleased;
    private Task<AssemblyReferenceExpansion>? referenceExpansionTask;
    private AssemblyReferenceExpansion? referenceExpansion;
    private int disposed;

    internal AssemblyAnalysisLease(
        AssemblyAnalysisEntry entry,
        string canonicalPath,
        ISolutionStateProvider server,
        AssemblyContext context,
        AssemblyReferenceLeaseContext referenceContext)
    {
        this.entry = entry;
        CanonicalPath = canonicalPath;
        Server = server;
        Context = context;
        referenceLeaseFactory = referenceContext.Factory;
        onReferenceLeaseReleased = referenceContext.OnReferenceLeaseReleased;
    }

    internal string CanonicalPath { get; }
    internal ISolutionStateProvider Server { get; }
    internal AssemblyContext Context { get; }

    Solution? IAssemblyBodyContext.Solution => Server.GetCurrentSolution();

    AnalysisSymbolIdentity? IAssemblyBodyContext.AssemblySymbolIdentity => Server.AssemblySymbolIdentity;

    AssemblyOrigin? IAssemblyBodyContext.Origin => Context.Origin;

    internal IReadOnlyList<AssemblyReferenceSession> ReferenceSessions =>
        referenceExpansion?.Sessions ?? Array.Empty<AssemblyReferenceSession>();

    internal IReadOnlyList<string> ReferenceExpansionDiagnostics =>
        referenceExpansion?.Diagnostics ?? Array.Empty<string>();

    /// <summary>
    /// Liefert eine nicht-besitzende Momentaufnahme der direkt von diesem Lease eröffneten Child-Leases.
    /// Der Aufrufer darf diese Leases nur innerhalb der Lebensdauer dieses Leases verwenden; die
    /// Freigabe bleibt ausschließlich beim Root-Lease, damit Cross-Assembly-Navigation keine
    /// unabhängige Resident-Lifetime erzeugt.
    /// </summary>
    internal IReadOnlyList<AssemblyAnalysisLease> ReferenceLeasesSnapshot()
    {
        lock (referenceGate)
        {
            return referenceLeases.ToList();
        }
    }

    internal Task<AssemblyReferenceExpansion> ExpandReferencesAsync(CancellationToken cancellationToken = default)
    {
        lock (referenceGate)
        {
            return referenceExpansionTask ??= ExpandReferencesCoreAsync(cancellationToken);
        }
    }

    internal Task<AssemblyAnalysisLeaseResult> LeaseReferenceAsync(
        AssemblyReferenceDto reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (referenceLeaseFactory is null) return Task.FromResult(ReferenceLeaseUnavailable());
        if (!CanLeaseReference(reference)) return Task.FromResult(ReferenceLeaseRejected(reference));
        return referenceLeaseFactory(reference, cancellationToken);
    }

    /// <summary>
    /// Öffnet ausschließlich die direkte Referenz, deren kanonischer Handoff-Target-Token passt.
    /// Die Suchmodus-Komponente des Keys verhindert, dass eine Owner-only-Öffnung eine spätere
    /// Closure als bereits aufgebaut erscheinen lässt.
    /// </summary>
    internal Task<AssemblyAnalysisLeaseResult?> LeaseNavigationOwnerAsync(
        SymbolHandoffIdentifier handoff,
        string searchMode,
        CancellationToken cancellationToken)
    {
        var reference = Context.References.FirstOrDefault(candidate => MatchesHandoffTarget(candidate, handoff));
        if (reference is null) return Task.FromResult<AssemblyAnalysisLeaseResult?>(null);

        var cacheKey = $"{handoff.TargetToken}|{searchMode}";
        Task<AssemblyAnalysisLeaseResult> task;
        lock (referenceGate)
        {
            if (!navigationReferenceLeaseTasks.TryGetValue(cacheKey, out task!))
            {
                task = OpenNavigationOwnerAsync(reference);
                navigationReferenceLeaseTasks.Add(cacheKey, task);
            }
        }

        return AwaitNavigationOwnerAsync(task, cancellationToken);
    }

    private async Task<AssemblyAnalysisLeaseResult> OpenNavigationOwnerAsync(AssemblyReferenceDto reference)
    {
        var result = await LeaseReferenceAsync(reference, CancellationToken.None).ConfigureAwait(false);
        if (result.Lease is not null) RegisterReferenceLease(result.Lease);
        return result;
    }

    private static async Task<AssemblyAnalysisLeaseResult?> AwaitNavigationOwnerAsync(
        Task<AssemblyAnalysisLeaseResult> task,
        CancellationToken cancellationToken) =>
        await task.WaitAsync(cancellationToken).ConfigureAwait(false);

    private static bool MatchesHandoffTarget(AssemblyReferenceDto reference, SymbolHandoffIdentifier handoff) =>
        handoff.Origin == SymbolHandoffOrigin.Assembly
        && SymbolHandoffToken.TryCreateTarget(
            reference.ResolvedPath ?? reference.SourceProjectPath ?? string.Empty,
            out var targetToken)
        && string.Equals(targetToken, handoff.TargetToken, StringComparison.Ordinal);

    private bool CanLeaseReference(AssemblyReferenceDto reference) =>
        reference.Resolved
        && (reference.ResolutionState == "source_project" || !string.IsNullOrWhiteSpace(reference.ResolvedPath))
        && Context.References.Any(candidate => IsMatchingReference(candidate, reference));

    private static bool IsMatchingReference(AssemblyReferenceDto candidate, AssemblyReferenceDto reference) =>
        string.Equals(candidate.Name, reference.Name, StringComparison.Ordinal)
        && string.Equals(candidate.Version, reference.Version, StringComparison.Ordinal)
        && string.Equals(candidate.Culture, reference.Culture, StringComparison.OrdinalIgnoreCase)
        && string.Equals(candidate.ResolvedPath, reference.ResolvedPath, StringComparison.OrdinalIgnoreCase)
        && string.Equals(candidate.SourceProjectPath, reference.SourceProjectPath, StringComparison.OrdinalIgnoreCase)
        && (string.Equals(candidate.ResolutionState, reference.ResolutionState, StringComparison.Ordinal)
            || (!string.Equals(reference.ResolutionState, "source_project", StringComparison.Ordinal)
                && !string.Equals(candidate.ResolutionState, "source_project", StringComparison.Ordinal)))
        && candidate.Depth == reference.Depth;

    private static AssemblyAnalysisLeaseResult ReferenceLeaseUnavailable() =>
        new(
            null,
            McpToolResults.Recoverable(
                LinterErrorCodes.AnalysisFailed,
                "Für diesen Assembly-Lease ist keine Referenzauflösung verfügbar."));

    private static AssemblyAnalysisLeaseResult ReferenceLeaseRejected(AssemblyReferenceDto reference) =>
        new(
            null,
            McpToolResults.Recoverable(
                LinterErrorCodes.AnalysisFailed,
                $"Die Referenz '{reference.Name}' ist nicht als analysierbares Ziel aufgelöst."));

    private async Task<AssemblyReferenceExpansion> ExpandReferencesCoreAsync(CancellationToken cancellationToken)
    {
        var expander = new AssemblyReferenceSessionExpander(CreateReferenceExpansionNode(), cancellationToken);
        try
        {
            referenceExpansion = await expander.BuildAsync().ConfigureAwait(false);
            return referenceExpansion;
        }
        catch
        {
            DisposeReferenceLeases();
            throw;
        }
    }

    private void RegisterReferenceLease(AssemblyAnalysisLease lease)
    {
        lock (referenceGate)
        {
            referenceLeases.Add(lease);
        }
    }

    private AssemblyReferenceExpansionNode CreateReferenceExpansionNode() =>
        new(
            CanonicalPath,
            Context.Identity,
            Context.Origin,
            Context.Status.ToCompletenessLabel(),
            Context.Status.ToString().ToLowerInvariant(),
            Context.Diagnostics,
            Context.References,
            OpenReferenceExpansionNodeAsync);

    private async Task<AssemblyReferenceExpansionNode?> OpenReferenceExpansionNodeAsync(
        AssemblyReferenceDto reference,
        CancellationToken cancellationToken)
    {
        var result = await LeaseReferenceAsync(reference, cancellationToken).ConfigureAwait(false);
        if (result.Lease is null) return null;

        var child = result.Lease;
        RegisterReferenceLease(child);
        return child.CreateReferenceExpansionNode();
    }

    private void DisposeReferenceLeases()
    {
        AssemblyAnalysisLease[] leases;
        lock (referenceGate)
        {
            leases = [.. referenceLeases];
            referenceLeases.Clear();
            navigationReferenceLeaseTasks.Clear();
        }

        for (var index = leases.Length - 1; index >= 0; index--)
        {
            leases[index].Dispose();
            onReferenceLeaseReleased?.Invoke(leases[index].entry);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            DisposeReferenceLeases();
            entry.ReleaseLease();
        }
    }
}

internal sealed record AssemblyAnalysisLeaseResult(
    AssemblyAnalysisLease? Lease,
    CallToolResult? Error);
