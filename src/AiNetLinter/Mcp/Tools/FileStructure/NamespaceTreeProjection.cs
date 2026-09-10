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
