#nullable enable

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>
/// Ein Eintrag in der Projekt-Übersicht für Stufe 1 von <c>get_namespace_tree</c>.
/// </summary>
public sealed record ProjectOverviewEntry(
    string ProjectName,
    string ProjectType,
    int NamespaceCount,
    int TypeCount);

/// <summary>
/// Ein konkreter C#-Typ für Stufe 3 von <c>get_namespace_tree</c>.
/// </summary>
public sealed record TypeNodeEntry(
    string Name,
    string Kind,
    string FilePath,
    int Line,
    string Visibility);

/// <summary>
/// Ein Knoten im hierarchischen Namespace-Baum für Stufe 2 von <c>get_namespace_tree</c>.
/// </summary>
public sealed record NamespaceTreeNode(
    string Namespace,
    int TypeCount,
    IReadOnlyList<TypeNodeEntry>? Types = null,
    IReadOnlyList<NamespaceTreeNode>? SubNamespaces = null);

/// <summary>
/// Eingabeargumente für <see cref="GetNamespaceTreeTool.ExecuteAsync"/>.
/// </summary>
public sealed record GetNamespaceTreeInput(
    string? Project = null,
    string? NamespacePrefix = null,
    int Depth = 1,
    bool IncludeTypes = true,
    string? Kind = "all",
    int MaxResults = 50);

public sealed record NamespaceTreeScanParameters(
    Project Project,
    string? NamespacePrefix,
    int Depth,
    bool IncludeTypes,
    string? KindFilter,
    int MaxResults,
    string SolutionDir);

public sealed record NamespaceTreePayload(
    string? SolutionName,
    string? Project,
    string? NamespacePrefix,
    string? KindFilter,
    int Depth,
    bool IncludeTypes,
    int TotalCount,
    int ShownCount,
    bool Truncated,
    IReadOnlyList<ProjectOverviewEntry>? Projects = null,
    IReadOnlyList<NamespaceTreeNode>? Namespaces = null,
    IReadOnlyList<TypeNodeEntry>? Types = null);
