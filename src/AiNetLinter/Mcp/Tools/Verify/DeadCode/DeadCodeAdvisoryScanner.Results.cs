#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using AiNetLinter.Output;

namespace AiNetLinter.Mcp.Tools.Verify.DeadCode;

internal static partial class DeadCodeAdvisoryScanner
{
    private static void AddDeadSymbol(
        DeadCodeScanContext context,
        ISymbol symbol,
        Document document,
        bool hasInternalsVisibleTo,
        DeadCodeAdvisoryScanner.SymbolReferenceAnalysis referenceAnalysis)
    {
        var kindStr = GetSymbolKindString(symbol);
        var accessibilityStr = GetAccessibilityString(symbol.DeclaredAccessibility);
        var assessment = RazorGeneratedEvidenceIndex.AssessMember(
            RazorGeneratedEvidenceStatus.NotComponent,
            ClassifyConfidence(symbol, hasInternalsVisibleTo));

        if (context.Args.Confidence == DeadCodeConfidenceFilter.High && !assessment.Confidence.Equals("high", StringComparison.OrdinalIgnoreCase)) return;
        if (context.Args.Confidence == DeadCodeConfidenceFilter.Low && !assessment.Confidence.Equals("low", StringComparison.OrdinalIgnoreCase)) return;

        var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        var line = 1;
        var column = 1;
        var filePath = document.FilePath ?? "";

        if (syntaxRef != null)
        {
            var span = syntaxRef.GetSyntax().GetLocation().GetLineSpan();
            line = span.StartLinePosition.Line + 1;
            column = span.StartLinePosition.Character + 1;
            filePath = span.Path;
        }

        var relativePath = PathNormalizer.ToRelative(context.SolutionDir, filePath);
        var containerTypeName = symbol.ContainingType?.ToDisplayString() ?? symbol.ContainingNamespace?.ToDisplayString() ?? "";
        var entry = new DeadCodeEntry(
            Id: symbol.ToDisplayString(),
            Kind: kindStr,
            ContainerType: containerTypeName,
            SymbolName: symbol.Name,
            File: relativePath,
            Line: line,
            Column: column,
            Accessibility: accessibilityStr,
            Confidence: assessment.Confidence,
            Reason: DescribeUsage(referenceAnalysis, symbol),
            LimitsApplies: DetermineLimitsApplies(symbol, hasInternalsVisibleTo),
            Countercheck: Counterchecks(symbol),
            Usage: referenceAnalysis.TestReferenceCount > 0 ? "test_only" : "unreferenced",
            TestReferences: referenceAnalysis.TestReferenceCount,
            InternalSymbolIdentifier: context.Args.HandoffIdentity?.FormatHandoff(symbol, document.Project.Id),
            ProjectName: document.Project.Name,
            Priority: ReviewPriority(symbol, context.Progress));

        context.DeadSymbols.Add(entry);
        if (context.ByKind.TryGetValue(kindStr, out var count)) context.ByKind[kindStr] = count + 1;
        else context.ByKind[kindStr] = 1;
    }

    private static int ReviewPriority(ISymbol symbol, DeadCodeScanProgress progress)
    {
        if (progress.PriorTargets.Contains(DeadCodeUsageIndex.Key(symbol))) return 0;
        return symbol is INamedTypeSymbol ? 1 : 2;
    }

    private static IReadOnlyList<string> Counterchecks(ISymbol symbol) => symbol switch
    {
        IFieldSymbol or IPropertySymbol => ["Produktive Leser/Schreibstellen", "Reflection-Auswahl und Daten-/Markupvertrag"],
        INamedTypeSymbol => ["Nutzung enthaltener Member und Entfern-Gruppe", "Aktivierung/Registrierung und externe Consumer"],
        _ => ["Aufrufer/Methodengruppen und Wrapper", "Vertragsdispatch, Registrierung und Reflection-Bindungen", "Dynamic", "Externe Consumer"]
    };

    private static string DescribeUsage(SymbolReferenceAnalysis usage, ISymbol symbol)
    {
        if (symbol is IFieldSymbol or IPropertySymbol)
            return $"no_production_read; writes={usage.WriteReferenceCount}; testReads={usage.TestReferenceCount}";
        return usage.TestReferenceCount > 0
            ? $"Keine produktiven statischen Referenzen; {usage.TestReferenceCount} Testreferenz(en) gefunden."
            : "Keine relevante produktive Nutzung im untersuchten Symbolumfang gefunden.";
    }

    private static DeadCodeScanCoverage BuildCoverage(DeadCodeScanContext context) => new(
        context.Args.RequestedScope ?? (context.Args.ScopeFiles is null ? "solution" : "changes"), context.Progress.ProcessedDocuments,
        context.DocumentsInScope - context.Progress.ProcessedDocuments, context.Progress.ElapsedMilliseconds,
        context.Progress.BudgetExpired ? "budget" : context.Progress.ReferencesComplete ? "finished" : string.Join(",", context.UsageIndex.CoverageGaps), context.Progress.ReferencesComplete,
        ChangesBasis: context.Progress.ChangesBasis);

    private static DeadCodeScanResult BuildScanResult(DeadCodeScanContext context)
    {
        var totalDead = context.DeadSymbols.Count;
        var highCount = context.DeadSymbols.Count(s => s.Confidence.Equals("high", StringComparison.OrdinalIgnoreCase));
        var lowCount = context.DeadSymbols.Count(s => s.Confidence.Equals("low", StringComparison.OrdinalIgnoreCase));
        var isTruncated = totalDead > context.Args.MaxResults;
        var paginatedSymbols = context.DeadSymbols.OrderBy(entry => entry.Priority).ThenBy(entry => entry.File, StringComparer.Ordinal)
            .ThenBy(entry => entry.Line).Take(context.Args.MaxResults).ToList();
        var action = isTruncated ? "continue" : "countercheck";
        var actionReason = isTruncated
            ? "maxResults erhoehen oder Scope verfeinern; Gegenpruefung nach dem vollstaendigen Ergebnis."
            : "Reflection, DI, Generatoren, dynamic und externe Consumer pruefen.";
        var summary = new DeadCodeSummary(
            DocumentsInScope: context.DocumentsInScope,
            ScannedSymbols: context.ScannedCount,
            TotalDead: totalDead,
            High: highCount,
            Low: lowCount,
            ByKind: context.ByKind,
            Status: !context.Progress.ReferencesComplete || context.Progress.BudgetExpired || context.Progress.ChangesBasis == "unavailable" ? "partial" : "complete",
            Cause: totalDead == 0
                ? $"Keine Kandidaten in den {context.DocumentsInScope} Dokumenten des angeforderten Scopes; kein globaler Clean-Claim."
                : "Statische Referenzsuche im angeforderten Scope.",
            Confidence: isTruncated ? "medium" : "high",
            ReturnedCandidates: paginatedSymbols.Count,
            TruncatedBy: isTruncated ? totalDead - paginatedSymbols.Count : 0,
            Next: new DeadCodeRecommendedNextAction(
                totalDead == 0 ? "countercheck" : action,
                totalDead == 0
                    ? "Reflection, DI, Generatoren, dynamic und externe Consumer pruefen."
                    : actionReason),
            Undecidable: context.UndecidableCount,
            ApiProtected: context.ApiProtectedCount,
            Coverage: BuildCoverage(context),
            UndecidableReasons: context.Progress.UncertainSymbols.Values.GroupBy(entry => entry.Reason)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal));
        var recommendedAction = totalDead == 0
            ? new DeadCodeRecommendedNextAction(
                Action: "ask_user",
                Reason: "Kandidaten manuell gegen Reflection, DI, Generatoren, dynamic und externe Consumer gegenpruefen; keine Loeschentscheidung.")
            : new DeadCodeRecommendedNextAction(action, actionReason);

        return new DeadCodeScanResult(
            DeadSymbols: paginatedSymbols,
            Summary: summary,
            Limits: DeadCodeLimits.DefaultLimits,
            RecommendedNextAction: recommendedAction,
            IsTruncated: isTruncated,
            UndecidableSymbols: context.Progress.UncertainSymbols.Values.ToArray());
    }
    private static void RecordUncertainty(ISymbol symbol, Document document, DeadCodeScanContext context)
    {
        var key = DeadCodeUsageIndex.Key(symbol);
        if (context.Progress.UncertainSymbols.ContainsKey(key)) return;
        var reasons = context.UsageIndex.UnknownReasons.TryGetValue(key, out var values)
            ? string.Join(",", values.Order(StringComparer.Ordinal)) : "reference_role_or_binding";
        var location = symbol.Locations.FirstOrDefault(location => location.IsInSource)?.GetLineSpan();
        var entry = new DeadCodeEntry(
            symbol.ToDisplayString(), GetSymbolKindString(symbol), symbol.ContainingType?.ToDisplayString() ?? "",
            symbol.Name, PathNormalizer.ToRelative(context.SolutionDir, document.FilePath ?? ""),
            (location?.StartLinePosition.Line ?? 0) + 1, (location?.StartLinePosition.Character ?? 0) + 1,
            GetAccessibilityString(symbol.DeclaredAccessibility), "", reasons, [], ResultType: "undecidable",
            Countercheck: [reasons], Usage: "undecidable",
            InternalSymbolIdentifier: context.Args.HandoffIdentity?.FormatHandoff(symbol, document.Project.Id),
            ProjectName: document.Project.Name, Priority: 3);
        context.Progress.UncertainSymbols.Add(key, entry);
        context.UndecidableCount++;
    }
}
