#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace AiNetLinter.Core.DuplicateDetection;

/// <summary>Deterministische Cosine-Similarity auf Sparse-Feature-Vektoren (keine Embeddings).</summary>
internal static class StructureSimilarity
{
    internal static double Cosine(IReadOnlyDictionary<string, double> a, IReadOnlyDictionary<string, double> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0.0;

        var (smaller, larger) = a.Count <= b.Count ? (a, b) : (b, a);
        double dot = 0.0;
        foreach (var (key, value) in smaller)
        {
            if (larger.TryGetValue(key, out var other))
            {
                dot += value * other;
            }
        }

        var magnitudeA = Magnitude(a);
        var magnitudeB = Magnitude(b);
        if (magnitudeA == 0.0 || magnitudeB == 0.0) return 0.0;
        return dot / (magnitudeA * magnitudeB);
    }

    private static double Magnitude(IReadOnlyDictionary<string, double> vector) =>
        Math.Sqrt(vector.Values.Sum(v => v * v));
}
