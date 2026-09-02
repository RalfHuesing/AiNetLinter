#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis.References;

internal sealed class AssemblyReferenceSessionExpander(
    AssemblyReferenceExpansionNode root,
    CancellationToken cancellationToken)
{
    private readonly List<AssemblyReferenceSession> sessions = [];
    private readonly List<string> diagnostics = [];
    private readonly HashSet<string> visitedTargets = new(StringComparer.OrdinalIgnoreCase);
    private int nodeCount;

    internal async Task<AssemblyReferenceExpansion> BuildAsync()
    {
        foreach (var reference in OrderReferences(root.References))
        {
            await VisitAsync(root, reference, depth: 1).ConfigureAwait(false);
        }

        return new(sessions, diagnostics.Distinct(StringComparer.Ordinal).Take(100).ToList());
    }

    private async Task VisitAsync(
        AssemblyReferenceExpansionNode owner,
        AssemblyReferenceDto reference,
        int depth)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sessionReference = reference with { Depth = depth };
        if (TryAddNodeBoundary(sessionReference)
            || TryAddUnresolved(sessionReference)
            || TryAddTerminal(sessionReference)
            || TryAddDeduplicated(sessionReference))
        {
            return;
        }

        nodeCount++;
        await LeaseAndVisitChildAsync(owner, reference, sessionReference, depth).ConfigureAwait(false);
    }

    private bool TryAddNodeBoundary(AssemblyReferenceDto reference)
    {
        if (nodeCount < AssemblyReferenceResolver.MaxReferenceNodes) return false;

        var diagnostic = $"Die Referenz-Session-Expansion erreicht die Begrenzung von {AssemblyReferenceResolver.MaxReferenceNodes} Knoten.";
        AddSession(reference with { Resolved = false, ResolutionState = "node_limit", Diagnostic = diagnostic }, "partial", [diagnostic]);
        diagnostics.Add(diagnostic);
        return true;
    }

    private bool TryAddUnresolved(AssemblyReferenceDto reference)
    {
        if (reference.Resolved) return false;
        var referenceDiagnostics = DiagnosticOf(reference).DefaultIfEmpty(
            $"Die Referenz '{reference.Name}' ist nicht auflösbar.").ToList();
        AddSession(reference, reference.ResolutionState, referenceDiagnostics);
        diagnostics.AddRange(referenceDiagnostics);
        return true;
    }

    private bool TryAddTerminal(AssemblyReferenceDto reference)
    {
        if (reference.ResolutionState is not ("cycle" or "depth_limit" or "node_limit")) return false;
        var referenceDiagnostics = DiagnosticOf(reference).DefaultIfEmpty(
            $"Die Referenz-Session für '{reference.Name}' wurde wegen {reference.ResolutionState} beendet.").ToList();
        AddSession(reference, reference.ResolutionState, referenceDiagnostics);
        diagnostics.AddRange(referenceDiagnostics);
        return true;
    }

    private bool TryAddDeduplicated(AssemblyReferenceDto reference)
    {
        if (visitedTargets.Add(GetTargetKey(reference))) return false;
        AddSession(reference with { ResolutionState = "deduplicated" }, "deduplicated", DiagnosticOf(reference));
        return true;
    }

    private async Task LeaseAndVisitChildAsync(
        AssemblyReferenceExpansionNode owner,
        AssemblyReferenceDto reference,
        AssemblyReferenceDto sessionReference,
        int depth)
    {
        var child = await owner.OpenReferenceAsync(reference, cancellationToken).ConfigureAwait(false);
        if (child is null)
        {
            AddFailedSession(sessionReference);
            return;
        }

        AddSession(
            sessionReference,
            child.SessionStatus,
            child.Diagnostics,
            child);
        diagnostics.AddRange(child.Diagnostics);
        if (depth >= AssemblyReferenceResolver.MaxReferenceDepth)
        {
            AddDepthBoundary();
            return;
        }

        foreach (var childReference in OrderReferences(child.References))
        {
            await VisitAsync(child, childReference, depth + 1).ConfigureAwait(false);
        }
    }

    private void AddFailedSession(AssemblyReferenceDto reference)
    {
        var failure = DiagnosticOf(reference).DefaultIfEmpty(
            $"Referenz-Session für '{reference.Name}' konnte nicht eröffnet werden.").ToList();
        AddSession(reference, "partial", failure);
        diagnostics.AddRange(failure);
    }

    private void AddDepthBoundary()
    {
        diagnostics.Add($"Die Referenz-Session-Expansion erreicht die maximale Tiefe {AssemblyReferenceResolver.MaxReferenceDepth}.");
    }

    private void AddSession(
        AssemblyReferenceDto reference,
        string sessionStatus,
        IReadOnlyList<string> sessionDiagnostics,
        AssemblyReferenceExpansionNode? node = null)
    {
        sessions.Add(new(
            reference,
            node?.AssemblyPath ?? GetReferencePath(reference),
            node?.Identity,
            node?.Origin,
            node?.Completeness ?? AssemblySessionStatus.Partial.ToCompletenessLabel(),
            node?.SessionStatus ?? sessionStatus,
            sessionDiagnostics));
    }

    private static IReadOnlyList<string> DiagnosticOf(AssemblyReferenceDto reference) =>
        string.IsNullOrWhiteSpace(reference.Diagnostic)
            ? Array.Empty<string>()
            : [reference.Diagnostic];

    private static string GetTargetKey(AssemblyReferenceDto reference)
    {
        if (!string.IsNullOrWhiteSpace(reference.ResolvedPath)) return reference.ResolvedPath;
        if (!string.IsNullOrWhiteSpace(reference.SourceProjectPath)) return $"source:{reference.SourceProjectPath}";
        return string.Join('|', reference.Name, reference.Version, reference.Culture);
    }

    private static string GetReferencePath(AssemblyReferenceDto reference) =>
        reference.ResolvedPath
        ?? reference.SourceProjectPath
        ?? reference.Name;

    private static IEnumerable<AssemblyReferenceDto> OrderReferences(
        IEnumerable<AssemblyReferenceDto> references) =>
        references
            .OrderBy(reference => reference.Depth)
            .ThenBy(reference => reference.ResolutionState == "source_project" ? 0 : 1)
            .ThenBy(reference => reference.Name, StringComparer.Ordinal)
            .ThenBy(reference => reference.Version, StringComparer.Ordinal);
}
