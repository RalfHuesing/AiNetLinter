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
        bool hasInternalsVisibleTo)
    {
        var kindStr = GetSymbolKindString(symbol);
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
            Accessibility: GetAccessibilityString(symbol.DeclaredAccessibility),
            Reason: "Keine relevante statische Nutzung im untersuchten Symbolumfang gefunden.",
            LimitsApplies: DetermineLimitsApplies(symbol, hasInternalsVisibleTo),
            Countercheck: Counterchecks(symbol),
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
        INamedTypeSymbol => ["Nutzung enthaltener Member und Entfern-Gruppe", "Aktivierung/Registrierung und externe Consumer"],
        _ => ["Aufrufer/Methodengruppen und Wrapper", "Vertragsdispatch, Registrierung und Reflection-Bindungen", "Dynamic", "Externe Consumer"]
    };

    private static DeadCodeScanCoverage BuildCoverage(DeadCodeScanContext context) => new(
        context.Args.RequestedScope ?? (context.Args.ScopeFiles is null ? "solution" : "changes"), context.Progress.ProcessedDocuments,
        context.DocumentsInScope - context.Progress.ProcessedDocuments, context.Progress.ElapsedMilliseconds,
        context.Progress.BudgetExpired ? "budget" : context.Progress.ReferencesComplete ? "finished" : string.Join(",", context.UsageIndex.CoverageGaps), context.Progress.ReferencesComplete,
        ChangesBasis: context.Progress.ChangesBasis);

    private static DeadCodeScanResult BuildScanResult(DeadCodeScanContext context)
    {
        var totalDead = context.DeadSymbols.Count;
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
            ByKind: context.ByKind,
            Status: !context.Progress.ReferencesComplete || context.Progress.BudgetExpired || context.Progress.ChangesBasis == "unavailable" ? "partial" : "complete",
            Cause: totalDead == 0
                ? $"Keine Kandidaten in den {context.DocumentsInScope} Dokumenten des angeforderten Scopes; kein globaler Clean-Claim."
                : "Statische Referenzsuche im angeforderten Scope.",
            ReturnedCandidates: paginatedSymbols.Count,
            TruncatedBy: isTruncated ? totalDead - paginatedSymbols.Count : 0,
            Next: new DeadCodeRecommendedNextAction(
                totalDead == 0 ? "countercheck" : action,
                totalDead == 0
                    ? "Reflection, DI, Generatoren, dynamic und externe Consumer pruefen."
                    : actionReason),
            ApiProtected: context.ApiProtectedCount,
            Coverage: BuildCoverage(context));
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
            IsTruncated: isTruncated);
    }
}
