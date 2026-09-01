#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis.References;

internal delegate Task<AssemblyAnalysisLeaseResult> AssemblyReferenceLeaseFactory(
    AssemblyReferenceDto reference,
    CancellationToken cancellationToken);

internal sealed class AssemblyAnalysisLease : IDisposable, IAssemblyBodyContext
{
    private readonly AssemblyAnalysisEntry entry;
    private readonly AssemblyReferenceLeaseFactory? referenceLeaseFactory;
    private readonly object referenceGate = new();
    private readonly List<AssemblyAnalysisLease> referenceLeases = [];
    private Task<AssemblyReferenceExpansion>? referenceExpansionTask;
    private AssemblyReferenceExpansion? referenceExpansion;
    private int disposed;

    internal AssemblyAnalysisLease(
        AssemblyAnalysisEntry entry,
        string canonicalPath,
        McpCodeGraphServer server,
        AssemblyContext context,
        AssemblyReferenceLeaseFactory? referenceLeaseFactory = null)
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

    Solution? IAssemblyBodyContext.Solution => Server.GetCurrentSolution();

    AnalysisSymbolIdentity? IAssemblyBodyContext.AssemblySymbolIdentity => Server.AssemblySymbolIdentity;

    bool IAssemblyBodyContext.IsDecompiled => Context.Origin.IsDecompiled;

    Task<AssemblyBodyResolution> IAssemblyBodyContext.ResolveBodyAsync(
        ISymbol symbol,
        int maxBodyLines,
        CancellationToken cancellationToken) =>
        ResolveBodyAsync(symbol, maxBodyLines, cancellationToken);

    internal Task<AssemblyBodyResolution> ResolveBodyAsync(
        ISymbol symbol,
        int maxBodyLines,
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref disposed) != 0)
        {
            return Task.FromResult(new AssemblyBodyResolution(
                null, "unavailable", Context.Origin.ContentMode, "Der Assembly-Lease ist nicht mehr gültig."));
        }

        if (!Context.Origin.IsDecompiled || Context.BodyResolver is null)
        {
            return Task.FromResult(new AssemblyBodyResolution(
                null, "source", "source", "Source-backed Symbole verwenden den Roslyn-Body."));
        }

        return Context.BodyResolver(symbol, maxBodyLines, cancellationToken);
    }

    internal IReadOnlyList<AssemblyReferenceSession> ReferenceSessions =>
        referenceExpansion?.Sessions ?? Array.Empty<AssemblyReferenceSession>();

    internal IReadOnlyList<string> ReferenceExpansionDiagnostics =>
        referenceExpansion?.Diagnostics ?? Array.Empty<string>();

    /// <summary>
    /// Liefert eine nicht-besitzende Momentaufnahme der vom Root-Lease eröffneten Child-Leases.
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
        var expander = new AssemblyReferenceSessionExpander(this, cancellationToken, RegisterReferenceLease);
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

    internal static IReadOnlyList<string> DiagnosticOf(AssemblyReferenceDto reference) =>
        string.IsNullOrWhiteSpace(reference.Diagnostic)
            ? Array.Empty<string>()
            : [reference.Diagnostic];

    internal static string GetTargetKey(AssemblyReferenceDto reference)
    {
        if (!string.IsNullOrWhiteSpace(reference.ResolvedPath)) return reference.ResolvedPath;
        if (!string.IsNullOrWhiteSpace(reference.SourceProjectPath)) return $"source:{reference.SourceProjectPath}";
        return string.Join('|', reference.Name, reference.Version, reference.Culture);
    }

    private void DisposeReferenceLeases()
    {
        AssemblyAnalysisLease[] leases;
        lock (referenceGate)
        {
            leases = [.. referenceLeases];
            referenceLeases.Clear();
        }

        for (var index = leases.Length - 1; index >= 0; index--)
        {
            leases[index].Dispose();
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
