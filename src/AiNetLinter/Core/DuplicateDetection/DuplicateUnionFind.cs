#nullable enable

namespace AiNetLinter.Core.DuplicateDetection;

/// <summary>
/// Minimaler Union-Find (Path-Compression, ohne Union-by-Rank). Methodenzahlen pro Solution sind
/// klein genug, dass Rank-Optimierung keinen messbaren Unterschied macht. Geteilt zwischen
/// tokenbasierter Cluster-Bildung und struktureller Kandidatenclusterung.
/// </summary>
internal sealed class DuplicateUnionFind
{
    private readonly int[] _parent;

    internal DuplicateUnionFind(int size)
    {
        _parent = new int[size];
        for (var i = 0; i < size; i++) _parent[i] = i;
    }

    internal int Find(int x)
    {
        while (_parent[x] != x)
        {
            _parent[x] = _parent[_parent[x]];
            x = _parent[x];
        }
        return x;
    }

    internal void Union(int a, int b)
    {
        var rootA = Find(a);
        var rootB = Find(b);
        if (rootA != rootB) _parent[rootA] = rootB;
    }
}
