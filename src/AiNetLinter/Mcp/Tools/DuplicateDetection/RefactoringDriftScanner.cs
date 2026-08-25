#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Core.DuplicateDetection;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.DuplicateDetection;

/// <summary>
/// Duenner Adapter zwischen <c>find_duplicates</c>s <c>mode="refactoring-drift"</c>-Zweig (Teil C)
/// und <see cref="DuplicateDetectionEngine.FindSimilarToAsync"/>. Loest das <c>helperSymbol</c>-
/// Argument ueber <see cref="FindReferencesTool.ResolveSymbolAsync"/> auf (wiederverwendet,
/// dieselbe Formaterkennung wie <c>find_references</c>/<c>get_impact</c>), ermittelt
/// <c>Callers(H)</c> ueber <see cref="DiffImpactAnalyzer.FindCallSiteEntriesAsync"/> plus einer
/// Positions-Aufloesung auf die jeweils umschliessende Methode (<see cref="SemanticModel.GetEnclosingSymbol"/>
/// — <see cref="DiffImpactAnalyzer.FindCallSiteEntriesAsync"/> liefert nur Datei:Zeile der
/// Aufrufstelle selbst, nicht die Identitaet der aufrufenden Methode), und delegiert die eigentliche
/// Aehnlichkeitssuche an die Engine. Keine Text-/JSON-Formatierung hier (macht
/// <see cref="DuplicateDetectionTool"/>), analog <see cref="DuplicateDetectionScanner"/>.
/// </summary>
internal static class RefactoringDriftScanner
{
    internal static async Task<(RefactoringDriftScanResultForTool? Result, CallToolResult? Error)> ScanAsync(
        Solution solution, GlobalConfig config, DuplicateDetectionInput input, CancellationToken ct)
    {
        var (symbol, resolveError) = await FindReferencesTool.ResolveSymbolAsync(solution, input.HelperSymbol!, ct);
        if (resolveError is not null) return (null, resolveError);

        if (symbol is not IMethodSymbol { MethodKind: MethodKind.Ordinary or MethodKind.LocalFunction } helper)
        {
            return (null, McpToolResults.InvalidArgument(
                $"helperSymbol '{input.HelperSymbol}' muss eine gewoehnliche Methode oder lokale Funktion sein " +
                $"(aufgeloest zu {DescribeKind(symbol!)}) — die Duplicate-Detection-Engine arbeitet nur auf " +
                "Method-/Local-Function-Koerpern (Teil A), Konstruktoren/Properties/Felder/Operatoren werden " +
                "nicht fingerprinted.",
                hint: "helperSymbol auf eine Methode oder lokale Funktion zeigen lassen."));
        }

        var options = DuplicateDetectionScanner.BuildOptions(config, input);

        var callSites = await DiffImpactAnalyzer.FindCallSiteEntriesAsync(helper, solution);
        var callers = await ResolveCallerMethodSymbolsAsync(solution, callSites, ct);

        var scanResult = await RefactoringDriftDetector.FindSimilarToAsync(solution, helper, callers, options, ct);
        if (scanResult is null)
        {
            var eligibility = await DuplicateMethodCollector.GetEligibilityAsync(solution, helper, options, ct);
            return (null, McpToolResults.InvalidArgument(
                DescribeFingerprintIneligibility(input.HelperSymbol!, options, eligibility),
                hint: DescribeFingerprintHint(eligibility.Eligibility)));
        }

        var effectiveMax = Math.Max(1, input.MaxResults ?? config.DuplicateCodeMaxResults);
        var candidates = scanResult.Candidates;
        var shown = candidates.Count <= effectiveMax ? candidates : candidates.Take(effectiveMax).ToList();
        var truncated = candidates.Count > effectiveMax;

        return (new RefactoringDriftScanResultForTool(
            helper.ToDisplayString(), shown, candidates.Count, scanResult.MethodsScanned, truncated), null);
    }

    private static string DescribeKind(ISymbol symbol) => symbol switch
    {
        IMethodSymbol m => $"Methode mit MethodKind={m.MethodKind}",
        IPropertySymbol => "Property",
        IFieldSymbol => "Feld",
        _ => symbol.Kind.ToString(),
    };

    private static string DescribeFingerprintIneligibility(
        string helperSymbol, DuplicateDetectionOptions options, MethodFingerprintEligibilityResult eligibility) =>
        eligibility.Eligibility switch
        {
            MethodFingerprintEligibility.TooFewTokens =>
                $"helperSymbol '{helperSymbol}' hat nur {eligibility.TokenCount} Body-Token; minTokens={options.MinTokens} ist nicht erreicht.",
            MethodFingerprintEligibility.TooFewTokensForNgrams =>
                $"helperSymbol '{helperSymbol}' hat nur {eligibility.TokenCount} Body-Token; ngramSize={options.NgramSize} ist nicht erreichbar.",
            MethodFingerprintEligibility.GeneratedCode =>
                $"helperSymbol '{helperSymbol}' ist mit GeneratedCode markiert und wird nicht fingerprinted.",
            MethodFingerprintEligibility.OutsideScope =>
                $"helperSymbol '{helperSymbol}' liegt ausserhalb von scopeDir und wird nicht fingerprinted.",
            MethodFingerprintEligibility.OutsideScopeType =>
                $"helperSymbol '{helperSymbol}' passt nicht zum gewaehlten scopeType und wird nicht fingerprinted.",
            MethodFingerprintEligibility.PermanentlyExcludedPath =>
                $"helperSymbol '{helperSymbol}' liegt in einem bewusst ausgeschlossenen Analysepfad und wird nicht fingerprinted.",
            MethodFingerprintEligibility.SourceFileExcluded =>
                $"helperSymbol '{helperSymbol}' liegt nicht in einer analysierbaren Solution-Quelldatei.",
            _ =>
                $"helperSymbol '{helperSymbol}' konnte nicht zu einem ausreichend grossen Fingerprint aufgeloest werden.",
        };

    private static string DescribeFingerprintHint(MethodFingerprintEligibility eligibility) => eligibility switch
    {
        MethodFingerprintEligibility.TooFewTokens or MethodFingerprintEligibility.TooFewTokensForNgrams =>
            "minTokens senken oder einen umfangreicheren Helper waehlen.",
        MethodFingerprintEligibility.OutsideScope or MethodFingerprintEligibility.OutsideScopeType =>
            "scopeDir und scopeType pruefen oder einen passenden Helper waehlen.",
        _ => "Den Quellpfad und die Eignung des Helper-Symbols pruefen.",
    };

    /// <summary>
    /// <see cref="DiffImpactAnalyzer.FindCallSiteEntriesAsync"/> liefert je Aufrufstelle nur
    /// Datei:Zeile des Aufrufs selbst (<see cref="CallSiteEntry.SymbolName"/> ist immer der Name
    /// des aufgerufenen Symbols <c>H</c>, nicht der aufrufenden Methode — siehe Doc-Kommentar dort).
    /// Diese Methode loest pro Aufrufstelle zusaetzlich die umschliessende Methode auf (Positions-
    /// Resolution ueber <see cref="SemanticModel.GetEnclosingSymbol(int, CancellationToken)"/> am
    /// Zeilenanfang — exakte Spalte wird nicht gebraucht, weil der Aufruf ohnehin innerhalb der
    /// Text-Spanne der umschliessenden Methode liegt), und normalisiert Lambda-/anonyme-Funktions-
    /// Symbole auf die naechste "echte" Methode/lokale Funktion (das Einzige, was
    /// <see cref="DuplicateDetectionEngine"/> ueberhaupt fingerprinted) hoch, damit die
    /// Caller-Identitaet mit den Fingerprint-Identitaeten der Engine uebereinstimmt.
    /// </summary>
    private static async Task<HashSet<ISymbol>> ResolveCallerMethodSymbolsAsync(
        Solution solution, IReadOnlyList<CallSiteEntry> callSites, CancellationToken ct)
    {
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        var result = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var callSite in callSites)
        {
            ct.ThrowIfCancellationRequested();
            var method = await ResolveEnclosingMethodAsync(solution, solutionDir, callSite, ct);
            if (method is not null) result.Add(method);
        }
        return result;
    }

    private static async Task<IMethodSymbol?> ResolveEnclosingMethodAsync(
        Solution solution, string solutionDir, CallSiteEntry callSite, CancellationToken ct)
    {
        var absolutePath = Path.GetFullPath(Path.Combine(solutionDir, callSite.FilePath));
        var document = DiffImpactAnalyzer.FindDocumentByPath(solution, absolutePath);
        if (document is null) return null;

        var text = await document.GetTextAsync(ct);
        var semanticModel = await document.GetSemanticModelAsync(ct);
        if (text is null || semanticModel is null || callSite.Line < 1 || callSite.Line > text.Lines.Count)
        {
            return null;
        }

        var position = text.Lines[callSite.Line - 1].Start;
        var enclosing = semanticModel.GetEnclosingSymbol(position, ct);
        return NormalizeToFingerprintedMethod(enclosing);
    }

    /// <summary>Wandert von einem beliebigen umschliessenden Symbol (z. B. einer Lambda/anonymen
    /// Funktion) ueber <see cref="ISymbol.ContainingSymbol"/> nach oben, bis eine gewoehnliche
    /// Methode oder lokale Funktion erreicht ist — die einzigen Deklarationsarten, die
    /// <see cref="DuplicateDetectionEngine"/> ueberhaupt fingerprinted (siehe dortiges
    /// <c>FindCandidateMethods</c>). Liefert <see langword="null"/>, wenn keine solche Methode in
    /// der Kette existiert (z. B. Aufruf auf Typ-/Namespace-Ebene, Feld-Initializer).</summary>
    private static IMethodSymbol? NormalizeToFingerprintedMethod(ISymbol? symbol)
    {
        var current = symbol;
        while (current is IMethodSymbol method)
        {
            if (method.MethodKind is MethodKind.Ordinary or MethodKind.LocalFunction) return method;
            current = method.ContainingSymbol;
        }
        return null;
    }
}
