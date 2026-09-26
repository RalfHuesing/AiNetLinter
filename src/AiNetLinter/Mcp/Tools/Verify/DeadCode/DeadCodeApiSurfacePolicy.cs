#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AiNetLinter.Configuration;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.Verify.DeadCode;

internal static class DeadCodeApiSurfacePolicy
{
    internal static bool IsKnown(string? value) => value is "closed_solution" or "external_library";

    internal static bool IsExternallyVisible(ISymbol symbol)
    {
        if (!IsExternallyVisibleAccessibility(symbol.DeclaredAccessibility)) return false;

        for (var containingType = symbol.ContainingType; containingType is not null; containingType = containingType.ContainingType)
        {
            if (!IsExternallyVisibleAccessibility(containingType.DeclaredAccessibility)) return false;
        }

        return symbol is not INamedTypeSymbol || IsTypeChainExternallyVisible((INamedTypeSymbol)symbol);
    }

    internal static IReadOnlyList<DeadCodeApiSurfaceIssue> FindUnconfiguredProjects(
        Solution solution,
        IReadOnlySet<string>? scopeFiles,
        Config config)
    {
        var solutionDir = System.IO.Path.GetDirectoryName(solution.FilePath) ?? "";
        var options = new DeadCodeAdvisoryOptions(ScopeFiles: scopeFiles, Config: config);
        var projects = DeadCodeAdvisoryScanner.CollectCandidateDocumentsForPolicy(solution, solutionDir, options)
            .Select(document => document.Project)
            .DistinctBy(project => project.Id)
            .OrderBy(project => project.Name, StringComparer.Ordinal)
            .ToList();

        var issues = new List<DeadCodeApiSurfaceIssue>();
        foreach (var project in projects)
        {
            var resolved = ProjectConfigResolver.ResolveForProject(project.Name, config);
            var value = resolved.DeadCode?.DefaultApiSurface;
            if (value is "closed_solution" or "external_library") continue;

            var matchedOverride = ProjectConfigResolver.FindProjectOverride(project.Name, config);
            var fieldPath = matchedOverride?.Value.DeadCode?.ApiSurface is not null
                ? $"ProjectOverrides.{matchedOverride.Value.Key}.DeadCode.ApiSurface"
                : "DeadCode.DefaultApiSurface";
            issues.Add(new(project.Name, fieldPath, value));
        }

        return issues;
    }

    internal static bool HasMissingFriend(ISymbol symbol, Solution solution)
    {
        if (symbol.DeclaredAccessibility is not (Accessibility.Internal or Accessibility.ProtectedOrInternal)) return false;
        var friends = symbol.ContainingAssembly.GetAttributes().Where(attribute =>
            attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.InternalsVisibleToAttribute")
            .Select(attribute => attribute.ConstructorArguments.FirstOrDefault().Value as string).OfType<string>();
        return friends.Any(friend => !solution.Projects.Any(project => project.AssemblyName == friend.Split(',')[0].Trim()));
    }

    private static bool IsTypeChainExternallyVisible(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (!IsExternallyVisibleAccessibility(current.DeclaredAccessibility)) return false;
        }

        return true;
    }

    private static bool IsExternallyVisibleAccessibility(Accessibility accessibility) =>
        accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal;
}

internal sealed record DeadCodeApiSurfaceIssue(string ProjectName, string FieldPath, string? Value);
