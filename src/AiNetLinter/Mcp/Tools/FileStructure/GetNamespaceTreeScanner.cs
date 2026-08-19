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
internal static class GetNamespaceTreeScanner
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
            Types: typeEntries);

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

    private static (string Text, NamespaceTreePayload Payload) RenderNamespaceTree(
        NamespaceTreeScanParameters parameters,
        INamespaceSymbol startNs,
        HashSet<SyntaxTree> projectTrees)
    {
        var rootNodes = new List<NamespaceTreeNode>();
        var flatListForOutput = new List<(string DisplayName, int TypeCount, int Indent)>();
        var traverseContext = new NamespaceTreeTraverseContext(parameters, projectTrees, flatListForOutput);

        CollectNamespaceTreeNodes(
            startNs,
            traverseContext,
            currentDepth: 1,
            resultNodes: rootNodes,
            currentIndent: 0);

        var totalCount = flatListForOutput.Count;
        var shownList = flatListForOutput.Take(parameters.MaxResults).ToList();
        var truncated = totalCount > parameters.MaxResults;

        var sb = new StringBuilder();
        var prefixTitle = string.IsNullOrWhiteSpace(parameters.NamespacePrefix) ? string.Empty : $" unter '{parameters.NamespacePrefix}'";
        sb.AppendLine($"# Namespaces in Projekt '{parameters.Project.Name}'{prefixTitle}:\n");

        if (shownList.Count == 0)
        {
            sb.AppendLine("Keine Namespaces mit Typen gefunden.");
        }
        else
        {
            foreach (var item in shownList)
            {
                var indent = new string(' ', item.Indent * 2);
                sb.AppendLine($"{indent}- {item.DisplayName} ({item.TypeCount} Typen)");
            }
        }

        AppendNamespaceTreeSummary(sb, parameters, shownList, totalCount, truncated);

        var payload = new NamespaceTreePayload(
            SolutionName: null,
            Project: parameters.Project.Name,
            NamespacePrefix: parameters.NamespacePrefix,
            KindFilter: parameters.KindFilter,
            Depth: parameters.Depth,
            IncludeTypes: parameters.IncludeTypes,
            TotalCount: totalCount,
            ShownCount: shownList.Count,
            Truncated: truncated,
            Namespaces: rootNodes);

        return (sb.ToString(), payload);
    }

    private static void AppendNamespaceTreeSummary(
        StringBuilder sb,
        NamespaceTreeScanParameters parameters,
        List<(string DisplayName, int TypeCount, int Indent)> shownList,
        int totalCount,
        bool truncated)
    {
        if (truncated)
        {
            sb.AppendLine();
            sb.Append($"[{totalCount} Namespaces gesamt, {shownList.Count} gezeigt — depth reduzieren oder maxResults erhoehen]");
        }
        else
        {
            sb.AppendLine();
            var firstNs = shownList.FirstOrDefault().DisplayName;
            var nextHint = string.IsNullOrWhiteSpace(firstNs) ? parameters.Project.Name : firstNs;
            if (parameters.Depth <= 1 && shownList.Count == 1 && shownList[0].TypeCount > 0)
            {
                sb.Append($"Tipp: Nutze depth=2 fuer Unter-Namespaces oder get_namespace_tree(project=\"{parameters.Project.Name}\", namespacePrefix=\"{nextHint}\") fuer die direkten Typen.");
            }
            else
            {
                sb.Append($"Tipp: Nutze get_namespace_tree(project=\"{parameters.Project.Name}\", namespacePrefix=\"{nextHint}\") fuer die Typen.");
            }
        }
    }

    private static void CollectNamespaceTreeNodes(
        INamespaceSymbol ns,
        NamespaceTreeTraverseContext context,
        int currentDepth,
        List<NamespaceTreeNode> resultNodes,
        int currentIndent)
    {
        var candidateNamespaces = ns.IsGlobalNamespace
            ? FlattenToTopLevelMeaningfulNamespaces(ns, context.ProjectTrees)
            : ns.GetNamespaceMembers().Where(n => HasAnySourceTypesInHierarchy(n, context.ProjectTrees)).OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var subNs in candidateNamespaces)
        {
            var directTypes = CollectSourceTypes(subNs, context.ProjectTrees)
                .Where(t => SymbolKindClassifier.MatchesTypeKind(t, context.Parameters.KindFilter))
                .ToList();

            var subTreeNodes = new List<NamespaceTreeNode>();
            var typeEntries = context.Parameters.IncludeTypes
                ? directTypes.Select(t => ToTypeEntry(t, context.Parameters.SolutionDir, context.ProjectTrees)).ToList()
                : null;

            context.FlatOutput.Add((subNs.ToDisplayString(), directTypes.Count, currentIndent));

            if (currentDepth < context.Parameters.Depth)
            {
                CollectNamespaceTreeNodes(
                    subNs,
                    context,
                    currentDepth + 1,
                    subTreeNodes,
                    currentIndent + 1);
            }

            resultNodes.Add(new NamespaceTreeNode(
                Namespace: subNs.ToDisplayString(),
                TypeCount: directTypes.Count,
                Types: typeEntries,
                SubNamespaces: subTreeNodes.Count > 0 ? subTreeNodes : null));
        }
    }


    private static List<INamespaceSymbol> FlattenToTopLevelMeaningfulNamespaces(
        INamespaceSymbol globalNs,
        HashSet<SyntaxTree> projectTrees)
    {
        var result = new List<INamespaceSymbol>();
        foreach (var rootMember in globalNs.GetNamespaceMembers().Where(n => HasAnySourceTypesInHierarchy(n, projectTrees)))
        {
            var current = rootMember;
            while (!CollectSourceTypes(current, projectTrees).Any() && current.GetNamespaceMembers().Count(n => HasAnySourceTypesInHierarchy(n, projectTrees)) == 1)
            {
                current = current.GetNamespaceMembers().Single(n => HasAnySourceTypesInHierarchy(n, projectTrees));
            }
            result.Add(current);
        }
        return result.OrderBy(n => n.ToDisplayString(), StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static TypeNodeEntry ToTypeEntry(INamedTypeSymbol t, string solutionDir, HashSet<SyntaxTree> projectTrees)
    {
        var location = t.Locations.FirstOrDefault(l => l.IsInSource && l.SourceTree is not null && projectTrees.Contains(l.SourceTree))
            ?? t.Locations.FirstOrDefault(l => l.IsInSource);
        var filePath = location?.SourceTree?.FilePath is not null
            ? PathNormalizer.ToRelative(solutionDir, location.SourceTree.FilePath)
            : string.Empty;
        var line = (location?.GetLineSpan().StartLinePosition.Line ?? 0) + 1;
        return new TypeNodeEntry(t.Name, SymbolKindClassifier.DescribeNamedTypeKind(t, englishClass: false), filePath, line, SymbolVisibilityResolver.ResolveVisibility(t));
    }

    internal static bool HasAnySourceTypesInHierarchy(INamespaceSymbol ns, HashSet<SyntaxTree> projectTrees)
    {
        if (CollectSourceTypes(ns, projectTrees).Any()) return true;
        return ns.GetNamespaceMembers().Any(sub => HasAnySourceTypesInHierarchy(sub, projectTrees));
    }

    private static (int NamespaceCount, int TypeCount) CountNamespacesAndTypes(
        INamespaceSymbol globalNs,
        HashSet<SyntaxTree> projectTrees)
    {
        var nsCount = 0;
        var typeCount = 0;

        void Traverse(INamespaceSymbol ns)
        {
            var types = CollectSourceTypes(ns, projectTrees).ToList();
            if (types.Count > 0)
            {
                nsCount++;
                typeCount += types.Count;
            }

            foreach (var sub in ns.GetNamespaceMembers())
            {
                Traverse(sub);
            }
        }

        Traverse(globalNs);
        return (nsCount, typeCount);
    }

    private static IEnumerable<INamedTypeSymbol> CollectSourceTypes(
        INamespaceSymbol ns,
        HashSet<SyntaxTree> projectTrees)
    {
        return ns.GetTypeMembers()
            .Where(t => IsTypeInProjectTrees(t, projectTrees))
            .Where(t => !IsCompilerGenerated(t));
    }

    private static bool IsTypeInProjectTrees(INamedTypeSymbol t, HashSet<SyntaxTree> projectTrees)
    {
        foreach (var loc in t.Locations)
        {
            if (loc.IsInSource && loc.SourceTree is not null && projectTrees.Contains(loc.SourceTree))
            {
                return true;
            }
        }

        foreach (var syntaxRef in t.DeclaringSyntaxReferences)
        {
            if (syntaxRef.SyntaxTree is not null && projectTrees.Contains(syntaxRef.SyntaxTree))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCompilerGenerated(INamedTypeSymbol t)
    {
        if (t.IsImplicitlyDeclared) return true;
        var name = t.Name;
        if (name.StartsWith('<') ||
            name.EndsWith("$", StringComparison.Ordinal) ||
            name.Equals("EqualityContract", StringComparison.Ordinal) ||
            name.Equals("<Clone>$", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var attr in t.GetAttributes())
        {
            var attrName = attr.AttributeClass?.Name;
            if (attrName is "CompilerGeneratedAttribute" or "CompilerGenerated")
            {
                return true;
            }
        }

        return false;
    }

    internal static INamespaceSymbol? FindNamespace(INamespaceSymbol root, string? namespacePrefix)
    {
        if (string.IsNullOrWhiteSpace(namespacePrefix)) return root;

        var parts = namespacePrefix.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var current = root;

        foreach (var part in parts)
        {
            var next = current.GetNamespaceMembers().FirstOrDefault(n => n.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
            if (next is null) return null;
            current = next;
        }

        return current;
    }
}

