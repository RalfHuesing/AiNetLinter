#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AiNetLinter.Mcp.Tools.Common;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.FileStructure;

internal static partial class GetNamespaceTreeScanner
{
    private static (string Text, NamespaceTreePayload Payload) RenderNamespaceTree(
        NamespaceTreeScanParameters parameters,
        INamespaceSymbol startNs,
        HashSet<SyntaxTree> projectTrees)
    {
        var rootNodes = new List<NamespaceTreeNode>();
        var flatListForOutput = new List<(string DisplayName, int TypeCount, int Indent)>();
        var traverseContext = new NamespaceTreeTraverseContext(parameters, projectTrees, flatListForOutput);
        var hasExactRoot = !string.IsNullOrWhiteSpace(parameters.NamespacePrefix);

        AddExactRootToFlatOutput(parameters, startNs, projectTrees, flatListForOutput, hasExactRoot);

        CollectNamespaceTreeNodes(
            startNs,
            traverseContext,
            currentDepth: 1,
            resultNodes: rootNodes,
            currentIndent: hasExactRoot ? 1 : 0);

        rootNodes = CreateExactRootProjection(parameters, startNs, projectTrees, rootNodes, hasExactRoot);

        // The requested namespace is context, not an additional result.  Its root
        // node is still retained in the structured tree so exact queries are
        // navigable even when it is a leaf.
        var totalCount = flatListForOutput.Count - (hasExactRoot ? 1 : 0);
        var shownList = flatListForOutput.Take(parameters.MaxResults + (hasExactRoot ? 1 : 0)).ToList();
        var shownProjection = hasExactRoot
            ? TakeExactRootProjection(rootNodes, parameters.MaxResults)
            : NamespaceTreeProjection.Take(rootNodes, parameters.MaxResults);
        var truncated = totalCount > parameters.MaxResults;

        var sb = RenderNamespaceText(parameters, shownList);
        AppendNamespaceTreeSummary(sb, parameters, shownList, totalCount, truncated);

        var payload = new NamespaceTreePayload(
            SolutionName: null,
            Project: parameters.Project.Name,
            NamespacePrefix: parameters.NamespacePrefix,
            KindFilter: parameters.KindFilter,
            Depth: parameters.Depth,
            IncludeTypes: parameters.IncludeTypes,
            TotalCount: totalCount,
            ShownCount: shownProjection.Count,
            Truncated: truncated,
            Namespaces: shownProjection.Nodes,
            TruncatedBy: truncated ? ["maxResults"] : null,
            Next: truncated ? new NamespaceTreeNext("request_detail", "maxResults erhöhen oder depth/namespacePrefix verfeinern.") : null);

        return (sb.ToString(), payload);
    }

    private static void AddExactRootToFlatOutput(
        NamespaceTreeScanParameters parameters, INamespaceSymbol startNs, HashSet<SyntaxTree> projectTrees,
        List<(string DisplayName, int TypeCount, int Indent)> flatOutput, bool hasExactRoot)
    {
        if (!hasExactRoot) return;
        var directTypes = CollectMatchingSourceTypes(startNs, parameters, projectTrees);
        flatOutput.Add((startNs.ToDisplayString(), directTypes.Count, 0));
    }

    private static List<NamespaceTreeNode> CreateExactRootProjection(
        NamespaceTreeScanParameters parameters, INamespaceSymbol startNs, HashSet<SyntaxTree> projectTrees,
        List<NamespaceTreeNode> rootNodes, bool hasExactRoot)
    {
        if (!hasExactRoot) return rootNodes;
        var directTypes = CollectMatchingSourceTypes(startNs, parameters, projectTrees);
        return [new NamespaceTreeNode(startNs.ToDisplayString(), directTypes.Count,
            parameters.IncludeTypes ? directTypes.Select(type => ToTypeEntry(type, parameters.SolutionDir, projectTrees)).ToList() : null,
            rootNodes.Count > 0 ? rootNodes : null)];
    }

    private static List<INamedTypeSymbol> CollectMatchingSourceTypes(
        INamespaceSymbol ns, NamespaceTreeScanParameters parameters, HashSet<SyntaxTree> projectTrees) =>
        CollectSourceTypes(ns, projectTrees).Where(type => SymbolKindClassifier.MatchesTypeKind(type, parameters.KindFilter)).ToList();

    private static StringBuilder RenderNamespaceText(
        NamespaceTreeScanParameters parameters, List<(string DisplayName, int TypeCount, int Indent)> shownList)
    {
        var sb = new StringBuilder();
        var prefixTitle = string.IsNullOrWhiteSpace(parameters.NamespacePrefix) ? string.Empty : $" unter '{parameters.NamespacePrefix}'";
        sb.AppendLine($"# Namespaces in Projekt '{parameters.Project.Name}'{prefixTitle}:\n");
        if (shownList.Count == 0) sb.AppendLine("Keine Namespaces mit Typen gefunden.");
        else foreach (var item in shownList) sb.AppendLine($"{new string(' ', item.Indent * 2)}- {item.DisplayName} ({item.TypeCount} Typen)");
        return sb;
    }

    private static (IReadOnlyList<NamespaceTreeNode> Nodes, int Count) TakeExactRootProjection(
        IReadOnlyList<NamespaceTreeNode> rootNodes,
        int maxResults)
    {
        var root = rootNodes.Single();
        var children = root.SubNamespaces is null
            ? (Nodes: (IReadOnlyList<NamespaceTreeNode>)[], Count: 0)
            : NamespaceTreeProjection.Take(root.SubNamespaces, maxResults);
        return ([root with { SubNamespaces = children.Nodes.Count > 0 ? children.Nodes : null }], children.Count);
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
        return new TypeNodeEntry(t.Name, SymbolKindClassifier.DescribeNamedTypeKind(t), filePath, line, SymbolVisibilityResolver.ResolveVisibility(t));
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
