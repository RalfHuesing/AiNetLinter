#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Core.DuplicateDetection;

/// <summary>
/// Refactoring-Drift-Detection ("absence-of-calls"-Heuristik, Murphy-Hill 2005).
/// Findet Methoden, deren Körper strukturell einem gegebenen Helper <c>H</c> ähnelt, die <c>H</c> aber nicht aufrufen.
/// </summary>
internal static class RefactoringDriftDetector
{
    /// <summary>
    /// Sucht Methoden, die strukturell ähnlich zu <paramref name="helper"/> sind (Jaccard-Score
    /// ≥ <see cref="DuplicateDetectionOptions.NearThreshold"/>) und nicht in
    /// <paramref name="callers"/> enthalten sind.
    /// </summary>
    internal static async Task<RefactoringDriftScanResult?> FindSimilarToAsync(
        Solution solution, IMethodSymbol helper, IReadOnlyCollection<ISymbol> callers,
        DuplicateDetectionOptions options, CancellationToken ct)
    {
        var fingerprints = await DuplicateDetectionEngine.CollectFingerprintsAsync(solution, options, ct);

        var helperFingerprint = fingerprints.FirstOrDefault(
            f => SymbolEqualityComparer.Default.Equals(f.Symbol, helper));
        if (helperFingerprint is null) return null;

        var callerSet = new HashSet<ISymbol>(callers, SymbolEqualityComparer.Default);

        var candidates = fingerprints
            .Where(f => !ReferenceEquals(f, helperFingerprint))
            .Where(f => !callerSet.Contains(f.Symbol))
            .Select(f => new RefactoringDriftCandidate(
                f.FilePath, f.LineNumber, f.SignatureName, f.TokenCount,
                DuplicateDetectionEngine.ComputeJaccard(helperFingerprint.NgramHashes, f.NgramHashes)))
            .Where(c => c.Score >= options.NearThreshold)
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.FilePath, System.StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.LineNumber)
            .ToList();

        return new RefactoringDriftScanResult(candidates, fingerprints.Count);
    }
}
