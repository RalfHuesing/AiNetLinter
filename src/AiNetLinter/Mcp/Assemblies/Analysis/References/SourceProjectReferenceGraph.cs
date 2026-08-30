#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis.References;

internal sealed class SourceProjectReferenceGraph(
    Solution solution,
    IReadOnlyList<AssemblyReferenceDto> existingReferences)
{
    private readonly List<AssemblyReferenceDto> references = [];
    private readonly List<AssemblySessionDiagnostic> diagnostics = [];
    private readonly HashSet<string> assemblyNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<ProjectId> visitedProjects = [];
    private readonly HashSet<ProjectId> activeProjects = [];
    private int expandedNodes;

    internal SourceProjectReferenceResolution Resolve(Project rootProject)
    {
        visitedProjects.Add(rootProject.Id);
        activeProjects.Add(rootProject.Id);
        expandedNodes = 1;
        Visit(rootProject, depth: 0);
        return new(references, assemblyNames, diagnostics);
    }

    private void Visit(Project project, int depth)
    {
        foreach (var projectReference in project.ProjectReferences.OrderBy(
                     reference => reference.ProjectId.ToString(),
                     StringComparer.Ordinal))
        {
            ProcessReference(projectReference, depth + 1);
        }
    }

    private void ProcessReference(ProjectReference projectReference, int depth)
    {
        var child = solution.GetProject(projectReference.ProjectId);
        var name = child?.AssemblyName ?? child?.Name ?? projectReference.ProjectId.ToString();
        var knownReference = existingReferences.FirstOrDefault(reference =>
            string.Equals(reference.Name, name, StringComparison.OrdinalIgnoreCase));
        var decision = Decide(child, depth);
        if (child is not null) assemblyNames.Add(name);

        AddReference(name, knownReference, child, depth, decision);
        if (decision.State is not "source_project" || child is null) return;

        expandedNodes++;
        visitedProjects.Add(child.Id);
        activeProjects.Add(child.Id);
        Visit(child, depth);
        activeProjects.Remove(child.Id);
    }

    private ReferenceDecision Decide(Project? child, int depth)
    {
        if (child is null)
        {
            return new(false, "missing", "Die Source-Project-Referenz ist in der gemappten Solution nicht vorhanden.");
        }

        if (depth > AssemblyReferenceResolver.MaxReferenceDepth)
        {
            return new(false, "depth_limit", $"überschreitet die maximale Referenztiefe {AssemblyReferenceResolver.MaxReferenceDepth}.");
        }

        if (activeProjects.Contains(child.Id))
        {
            return new(true, "cycle", "Zyklische Source-Project-Referenz erkannt.");
        }

        if (visitedProjects.Contains(child.Id))
        {
            return new(true, "deduplicated", null);
        }

        return expandedNodes >= AssemblyReferenceResolver.MaxReferenceNodes
            ? new(false, "node_limit", $"Die Source-Project-Referenzauflösung erreicht die Begrenzung von {AssemblyReferenceResolver.MaxReferenceNodes} Projekten.")
            : new(true, "source_project", null);
    }

    private void AddReference(
        string name,
        AssemblyReferenceDto? knownReference,
        Project? child,
        int depth,
        ReferenceDecision decision)
    {
        var diagnostic = CreateDiagnostic(name, decision);
        references.Add(new AssemblyReferenceDto(
            name,
            knownReference?.Version ?? "0.0.0.0",
            knownReference?.Culture ?? "neutral",
            decision.Resolved,
            null,
            decision.State,
            depth,
            diagnostic,
            child?.FilePath));
        if (diagnostic is not null)
        {
                diagnostics.Add(new(
                    decision.State is "cycle" ? "assembly-reference-cycle" : AssemblyReferenceResolver.BoundaryDiagnosticCode,
                diagnostic,
                AssemblyDiagnosticSeverity.Warning));
        }
    }

    private static string? CreateDiagnostic(string name, ReferenceDecision decision) =>
        decision.Diagnostic is null
            ? null
            : decision.State switch
            {
                "missing" => $"Source-Project-Referenz '{name}' ist in der gemappten Solution nicht vorhanden.",
                "cycle" => $"Zyklische Source-Project-Referenz erkannt: '{name}'.",
                "depth_limit" => $"Source-Project-Referenz '{name}' {decision.Diagnostic}",
                _ => $"Source-Project-Referenz '{name}': {decision.Diagnostic}",
            };

    private sealed record ReferenceDecision(bool Resolved, string State, string? Diagnostic);
}
