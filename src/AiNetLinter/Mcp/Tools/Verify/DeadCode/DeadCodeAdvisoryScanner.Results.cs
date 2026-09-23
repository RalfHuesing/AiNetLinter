#nullable enable

using System;
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
        var accessibilityStr = GetAccessibilityString(symbol.DeclaredAccessibility);
        var assessment = RazorGeneratedEvidenceIndex.AssessMember(
            context.RazorEvidenceIndex.GetStatus(symbol, document),
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
            Reason: assessment.Reason,
            LimitsApplies: DetermineLimitsApplies(symbol, hasInternalsVisibleTo),
            Countercheck: assessment.Countercheck);

        context.DeadSymbols.Add(entry);
        if (context.ByKind.TryGetValue(kindStr, out var count)) context.ByKind[kindStr] = count + 1;
        else context.ByKind[kindStr] = 1;
    }

    private static DeadCodeScanResult BuildScanResult(DeadCodeScanContext context)
    {
        var totalDead = context.DeadSymbols.Count;
        var highCount = context.DeadSymbols.Count(s => s.Confidence.Equals("high", StringComparison.OrdinalIgnoreCase));
        var lowCount = context.DeadSymbols.Count(s => s.Confidence.Equals("low", StringComparison.OrdinalIgnoreCase));
        var isTruncated = totalDead > context.Args.MaxResults;
        var paginatedSymbols = isTruncated ? context.DeadSymbols.Take(context.Args.MaxResults).ToList() : context.DeadSymbols;
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
            Status: isTruncated ? "truncated" : totalDead == 0 ? "empty" : "checked",
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
                    : actionReason));
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
