#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>
/// Reine Scan- und Formatierungs-Logik fuer <c>get_namespace_tree</c> entlang 3 Zoom-Stufen.
/// </summary>
internal static partial class GetNamespaceTreeScanner
{
    internal static bool IsValidKind(string? kind) => SymbolKindClassifier.IsValidTypeKind(kind);

    internal static async Task<HashSet<SyntaxTree>> GetProjectSyntaxTreesAsync(
        Project project,
        string? solutionDir,
        CancellationToken ct)
    {
        var validDocs = project.Documents.Where(d => SourceFileCatalog.IsValidDocument(d, solutionDir)).ToList();
        var trees = await Task.WhenAll(validDocs.Select(d => d.GetSyntaxTreeAsync(ct)));
        return trees.Where(t => t is not null).Select(t => t!).ToHashSet();
    }

    /// <summary>
    /// Stufe 1: Solution-Ueberblick ueber alle Projekte.
    /// </summary>
    internal static async Task<(string Text, NamespaceTreePayload Payload)> ScanSolutionProjectsAsync(
        Solution solution, CancellationToken ct)
    {
        var solutionName = Path.GetFileName(solution.FilePath) ?? "Solution";
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        var projectEntries = new List<ProjectOverviewEntry>();

        foreach (var project in solution.Projects)
        {
            var projectType = ProjectTypeClassifier.Classify(project);
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is null)
            {
                projectEntries.Add(new ProjectOverviewEntry(project.Name, projectType, 0, 0));
                continue;
            }

            var projectTrees = await GetProjectSyntaxTreesAsync(project, solutionDir, ct);
            var (nsCount, typeCount) = CountNamespacesAndTypes(compilation.GlobalNamespace, projectTrees);
            projectEntries.Add(new ProjectOverviewEntry(project.Name, projectType, nsCount, typeCount));
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# Solution Overview: {solutionName} ({projectEntries.Count} Projekte)\n");
        foreach (var p in projectEntries)
        {
            sb.AppendLine($"- {p.ProjectName} (Typ: {p.ProjectType}, {p.NamespaceCount} Namespaces, {p.TypeCount} Typen)");
        }

        sb.AppendLine();
        sb.Append("Tipp: Nutze get_namespace_tree(project=\"<ProjektName>\") fuer die Namespaces eines Projekts.");

        var payload = new NamespaceTreePayload(
            SolutionName: solutionName,
            Project: null,
            NamespacePrefix: null,
            KindFilter: null,
            Depth: 1,
            IncludeTypes: true,
            TotalCount: projectEntries.Count,
            ShownCount: projectEntries.Count,
            Truncated: false,
            Projects: projectEntries);

        return (sb.ToString(), payload);
    }

    /// <summary>
    /// Stufe 2 & 3: Namespaces oder Typen eines Zielprojekts abfragen.
    /// </summary>
    internal static async Task<(string Text, NamespaceTreePayload Payload)> ScanProjectNamespacesAsync(
        NamespaceTreeScanParameters parameters,
        CancellationToken ct)
    {
        var compilation = await parameters.Project.GetCompilationAsync(ct);
        if (compilation is null)
        {
            var emptyPayload = new NamespaceTreePayload(
                SolutionName: null,
                Project: parameters.Project.Name,
                NamespacePrefix: parameters.NamespacePrefix,
                KindFilter: parameters.KindFilter,
                Depth: parameters.Depth,
                IncludeTypes: parameters.IncludeTypes,
                TotalCount: 0,
                ShownCount: 0,
                Truncated: false);
            return ($"Projekt '{parameters.Project.Name}' konnte nicht kompiliert werden.", emptyPayload);
        }

        var projectTrees = await GetProjectSyntaxTreesAsync(parameters.Project, parameters.SolutionDir, ct);
        var startNamespace = FindNamespace(compilation.GlobalNamespace, parameters.NamespacePrefix);
        if (startNamespace is null && !string.IsNullOrWhiteSpace(parameters.NamespacePrefix))
        {
            var emptyPayload = new NamespaceTreePayload(
                SolutionName: null,
                Project: parameters.Project.Name,
                NamespacePrefix: parameters.NamespacePrefix,
                KindFilter: parameters.KindFilter,
                Depth: parameters.Depth,
                IncludeTypes: parameters.IncludeTypes,
                TotalCount: 0,
                ShownCount: 0,
                Truncated: false);
            return ($"Namespace '{parameters.NamespacePrefix}' wurde im Projekt '{parameters.Project.Name}' nicht gefunden.", emptyPayload);
        }

        var targetNs = startNamespace ?? compilation.GlobalNamespace;

        if (!string.IsNullOrWhiteSpace(parameters.NamespacePrefix) && parameters.IncludeTypes && parameters.Depth <= 1)
        {
            return RenderNamespaceTypes(parameters, targetNs, projectTrees);
        }

        return RenderNamespaceTree(parameters, targetNs, projectTrees);
    }

    private static (string Text, NamespaceTreePayload Payload) RenderNamespaceTypes(
        NamespaceTreeScanParameters parameters,
        INamespaceSymbol ns,
        HashSet<SyntaxTree> projectTrees)
    {
        var allTypes = CollectSourceTypes(ns, projectTrees)
            .Where(t => SymbolKindClassifier.MatchesTypeKind(t, parameters.KindFilter))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalCount = allTypes.Count;
        var shownTypes = allTypes.Take(parameters.MaxResults).ToList();
        var truncated = totalCount > parameters.MaxResults;

        var typeEntries = shownTypes
            .Select(t => ToTypeEntry(t, parameters.SolutionDir, projectTrees))
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"# Typen in Namespace '{parameters.NamespacePrefix}' (Projekt: {parameters.Project.Name}):\n");

        if (typeEntries.Count == 0)
        {
            sb.AppendLine("Keine Typen gefunden.");
        }
        else
        {
            foreach (var t in typeEntries)
            {
                sb.AppendLine($"- {t.Name} ({t.Kind}) — {t.FilePath}:{t.Line}");
            }
        }

        if (truncated)
        {
            sb.AppendLine();
            sb.Append($"[{totalCount} Typen gesamt, {shownTypes.Count} gezeigt — maxResults erhoehen]");
        }

        AppendSubNamespaceHint(sb, ns, parameters.NamespacePrefix, projectTrees);

        var payload = new NamespaceTreePayload(
            SolutionName: null,
            Project: parameters.Project.Name,
            NamespacePrefix: parameters.NamespacePrefix,
            KindFilter: parameters.KindFilter,
            Depth: 1,
            IncludeTypes: true,
            TotalCount: totalCount,
            ShownCount: shownTypes.Count,
            Truncated: truncated,
            Types: typeEntries,
            Namespaces:
            [
                new NamespaceTreeNode(
                    ns.ToDisplayString(),
                    totalCount,
                    typeEntries,
                    DirectSubNamespaces(ns, projectTrees)),
            ],
            TruncatedBy: truncated ? ["maxResults"] : null,
            Next: truncated ? new NamespaceTreeNext("request_detail", "maxResults erhöhen oder namespacePrefix/kind verfeinern.") : null);

        return (sb.ToString(), payload);
    }

    private static void AppendSubNamespaceHint(
        StringBuilder sb,
        INamespaceSymbol ns,
        string? namespacePrefix,
        HashSet<SyntaxTree> projectTrees)
    {
        var directSubNamespaces = ns.GetNamespaceMembers()
            .Where(sub => HasAnySourceTypesInHierarchy(sub, projectTrees))
            .OrderBy(sub => sub.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (directSubNamespaces.Count == 0) return;

        sb.AppendLine();
        var examples = string.Join(", ", directSubNamespaces.Take(3).Select(s => s.ToDisplayString()));
        if (directSubNamespaces.Count > 3) examples += ", ...";
        sb.Append($"[Hinweis: Unter '{namespacePrefix}' existieren {directSubNamespaces.Count} weitere Sub-Namespaces ({examples}) — nutze depth=2 oder includeTypes=false fuer den Namespace-Baum]");
    }

    private static IReadOnlyList<NamespaceTreeNode>? DirectSubNamespaces(
        INamespaceSymbol ns,
        HashSet<SyntaxTree> projectTrees)
    {
        var nodes = ns.GetNamespaceMembers()
            .Where(sub => HasAnySourceTypesInHierarchy(sub, projectTrees))
            .OrderBy(sub => sub.Name, StringComparer.OrdinalIgnoreCase)
            .Select(sub => new NamespaceTreeNode(
                sub.ToDisplayString(),
                CollectSourceTypes(sub, projectTrees).Count()))
            .ToList();
        return nodes.Count == 0 ? null : nodes;
    }

}
