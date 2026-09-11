#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>
/// Projektion gemeinsamer sichtbarer Namespace-Knoten fuer Text- und Structured-Ausgaben.
/// </summary>
internal static class NamespaceTreeProjection
{
    internal static int CountVisibleEntries(NamespaceTreePayload payload)
    {
        if (payload.Projects is not null) return payload.Projects.Count;
        if (payload.Types is not null) return payload.Types.Count;
        return payload.Namespaces?.Sum(CountEntries) ?? 0;
    }

    /// <summary>
    /// Zählt die sichtbaren fachlichen Einheiten einer Namespace-Antwort.
    /// Ein exakter Namespace-Root ist Kontext; nur seine gezeigten Children
    /// sind Namespace-Ergebnisse und zählen deshalb in <c>shownCount</c>.
    /// </summary>
    internal static int VisibleEntryCount(NamespaceTreePayload payload)
    {
        if (payload.Projects is not null || payload.Types is not null)
        {
            return CountVisibleEntries(payload);
        }

        if (!string.IsNullOrWhiteSpace(payload.NamespacePrefix)
            && payload.Namespaces is [var exactRoot])
        {
            return (exactRoot.Types?.Count ?? 0) + (exactRoot.SubNamespaces?.Sum(CountEntries) ?? 0);
        }

        return CountVisibleEntries(payload);
    }

    internal static (IReadOnlyList<NamespaceTreeNode> Nodes, int Count) Take(
        IReadOnlyList<NamespaceTreeNode> nodes,
        int maxEntries)
    {
        var result = new List<NamespaceTreeNode>();
        var taken = 0;
        foreach (var node in nodes)
        {
            if (taken >= maxEntries) break;

            var remaining = maxEntries - taken - 1;
            var sub = node.SubNamespaces is null || remaining <= 0
                ? null
                : Take(node.SubNamespaces, remaining).Nodes;
            result.Add(node with { SubNamespaces = sub is { Count: > 0 } ? sub : null });
            taken += 1 + (sub?.Sum(CountEntries) ?? 0);
        }

        return (result, taken);
    }

    internal static IReadOnlyList<NamespaceTreeNode> RemoveLast(
        IReadOnlyList<NamespaceTreeNode> nodes,
        int visibleCount)
    {
        return Take(nodes, Math.Max(0, visibleCount - 1)).Nodes;
    }

    internal static int CountEntries(NamespaceTreeNode node) =>
        1 + (node.SubNamespaces?.Sum(CountEntries) ?? 0);
}
