#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Core.DuplicateDetection;

/// <summary>
/// Teil C — Refactoring-Drift-Detection ("absence-of-calls"-Heuristik, Murphy-Hill 2005 "How We
/// Refactor, and How We Know It"), siehe <c>tasks/features/07-drift-audit-ideen.md</c> §C und
/// <c>tasks/features/05-roadmap.md</c> "Teil C". Findet Methoden, deren Koerper strukturell einem
/// gegebenen Helper <c>H</c> aehnelt, die <c>H</c> aber nicht aufrufen — Kandidat dafuer, dass der
/// Helper "ausgewickelt" statt aufgerufen wurde. Baut bewusst NICHT auf einer zweiten
/// Tokenisierungs-/Jaccard-Pipeline auf, sondern wiederverwendet <see cref="CollectFingerprintsAsync"/>
/// und <see cref="ComputeJaccard"/> aus der Teil-A-Haelfte dieser <c>partial class</c> — "1 gegen
/// alle" statt "alle gegen alle" (guenstiger als der volle Cluster-Scan aus <see cref="ScanAsync"/>,
/// weil keine Kanten zwischen den Nicht-Helper-Kandidaten selbst gebraucht werden).
/// </summary>
internal static partial class DuplicateDetectionEngine
{
    /// <summary>
    /// Sucht Methoden, die strukturell aehnlich zu <paramref name="helper"/> sind (Jaccard-Score
    /// ≥ <see cref="DuplicateDetectionOptions.NearThreshold"/>) und nicht in
    /// <paramref name="callers"/> enthalten sind. <paramref name="callers"/> wird vom Aufrufer
    /// (<c>RefactoringDriftScanner</c>) ermittelt — Aufrufer-Aufloesung ist Symbolgraph-Logik
    /// (<see cref="Microsoft.CodeAnalysis.FindSymbols.SymbolFinder"/>/<c>DiffImpactAnalyzer</c>),
    /// nicht Teil der Token-CPD-Engine.
    /// <para/>
    /// Liefert <see langword="null"/>, wenn <paramref name="helper"/> nicht unter den regulaeren
    /// <see cref="CollectFingerprintsAsync"/>-Fingerprints auftaucht — d. h. wenn er dieselben
    /// False-Positive-Filter wie Teil A nicht besteht (zu kurz fuer <see cref="DuplicateDetectionOptions.MinTokens"/>,
    /// <c>[GeneratedCode]</c>, in einem permanent ausgeschlossenen Verzeichnis, oder kein
    /// Methoden-/Local-Function-Body vorhanden). Der Aufrufer uebersetzt das in eine
    /// Handlungsanleitung (z. B. <c>minTokens</c> senken), statt hier eine Exception zu werfen —
    /// das ist ein erwartbarer/recoverable Fall, keine Malfunction.
    /// </summary>
    internal static async Task<RefactoringDriftScanResult?> FindSimilarToAsync(
        Solution solution, IMethodSymbol helper, IReadOnlyCollection<ISymbol> callers,
        DuplicateDetectionOptions options, CancellationToken ct)
    {
        var fingerprints = await CollectFingerprintsAsync(solution, options, ct);

        var helperFingerprint = fingerprints.FirstOrDefault(
            f => SymbolEqualityComparer.Default.Equals(f.Symbol, helper));
        if (helperFingerprint is null) return null;

        var callerSet = new HashSet<ISymbol>(callers, SymbolEqualityComparer.Default);

        var candidates = fingerprints
            .Where(f => !ReferenceEquals(f, helperFingerprint))
            .Where(f => !callerSet.Contains(f.Symbol))
            .Select(f => new RefactoringDriftCandidate(
                f.FilePath, f.LineNumber, f.SignatureName, f.TokenCount,
                ComputeJaccard(helperFingerprint.NgramHashes, f.NgramHashes)))
            .Where(c => c.Score >= options.NearThreshold)
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.FilePath, System.StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.LineNumber)
            .ToList();

        return new RefactoringDriftScanResult(candidates, fingerprints.Count);
    }
}
