#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.MetricsLookup;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Models;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.FeatureContext;

/// <summary>
/// Koordiniert die Aggregation der fuenf Feature-Kontext-Dimensionen fuer ein Roslyn-Symbol.
/// </summary>
internal static class FeatureContextScanner
{
    private const int MaxCallersLimit = 50;
    private const int MaxTestFilesLimit = 50;
    private const int MaxTestMethodsPerFile = 50;
    private const int MaxTestMethodsTotal = 200;
    private const int MaxViolations = 50;

    internal static async Task<FeatureContextPayload> ScanAsync(
        ISymbol symbol,
        FeatureContextScanContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var solutionDir = Path.GetDirectoryName(context.Solution.FilePath) ?? "";
        var declaration = ExtractDeclaration(symbol, solutionDir, context.AssemblySymbolIdentity);

        var metrics = CollectMetrics(symbol, context, solutionDir, ct);
        var callers = context.Options.IncludeCallers
            ? await CollectCallersAsync(symbol, context.Solution, context.Options.MaxCallers, ct)
            : null;
        var tests = context.Options.IncludeTests
            ? await CollectTestsAsync(symbol, context.Solution, context.Options.MaxTests, ct)
            : null;
        var violations = context.Options.IncludeViolations
            ? await CollectViolationsAsync(context.Solution, declaration, context.Config, context.Console, ct)
            : null;

        return BuildPayload(context, declaration, metrics, callers, tests, violations);
    }

    private static MetricsLookupResultDto? CollectMetrics(
        ISymbol symbol,
        FeatureContextScanContext context,
        string solutionDir,
        CancellationToken ct) =>
        context.Options.IncludeMetrics && context.Config is not null
            ? MetricsLookupScanner.ScanSymbol(symbol, context.Config, solutionDir, ct, context.AssemblySymbolIdentity)
            : null;

    private static FeatureContextPayload BuildPayload(
        FeatureContextScanContext context,
        SymbolDeclarationDto declaration,
        MetricsLookupResultDto? metrics,
        CallersReportDto? callers,
        StaticTestContextReportDto? tests,
        ViolationsReportDto? violations)
    {
        var metricsStatus = context.Options.IncludeMetrics
            ? context.Config is null ? FeatureContextStatus.NotConfigured : FeatureContextStatus.Complete
            : null;
        var completeness = ResolveCompleteness(metricsStatus, callers, tests, violations);
        var nextStep = tests?.NextStep
            ?? callers?.NextStep
            ?? violations?.NextStep
            ?? (metricsStatus == FeatureContextStatus.NotConfigured
                ? "Abschnitt metrics: ainetlinter-rules.json bereitstellen und den Metrik-Abschnitt erneut abfragen."
                : null);
        return new FeatureContextPayload(
            declaration, metrics, callers, tests, violations,
            metricsStatus, completeness, nextStep);
    }

    internal static string ResolveCompleteness(
        string? metricsStatus,
        CallersReportDto? callers,
        StaticTestContextReportDto? tests,
        ViolationsReportDto? violations)
    {
        var statuses = new[]
        {
            metricsStatus,
            callers?.Completeness,
            tests?.Completeness,
            violations?.Status,
        };

        if (statuses.Contains(FeatureContextStatus.Error, StringComparer.Ordinal)) return FeatureContextStatus.Partial;
        if (statuses.Contains(FeatureContextStatus.NotDecidable, StringComparer.Ordinal)) return FeatureContextStatus.NotDecidable;
        if (statuses.Contains(FeatureContextStatus.NotConfigured, StringComparer.Ordinal)) return FeatureContextStatus.NotConfigured;
        if (statuses.Contains(FeatureContextStatus.NotApplicable, StringComparer.Ordinal)) return FeatureContextStatus.NotApplicable;

        if (callers?.Completeness == FeatureContextStatus.Truncated
            || tests?.Completeness == FeatureContextStatus.Truncated
            || violations?.Status == FeatureContextStatus.Truncated
        )
        {
            return FeatureContextStatus.Truncated;
        }

        return callers?.Completeness == FeatureContextStatus.Partial
                || tests?.Completeness == FeatureContextStatus.Partial
                || violations?.Status == FeatureContextStatus.Partial
            ? FeatureContextStatus.Partial
            : FeatureContextStatus.Complete;
    }

    private static async Task<CallersReportDto> CollectCallersAsync(
        ISymbol symbol,
        Solution solution,
        int requestedMaxCallers,
        CancellationToken ct)
    {
        var allCallers = await DiffImpactAnalyzer.FindCallSiteEntriesAsync(symbol, solution, ct);
        var orderedCallers = allCallers
            .OrderBy(c => PathNormalizer.NormalizeSeparators(c.FilePath), StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Line)
            .ThenBy(c => c.ProjectName, StringComparer.Ordinal)
            .ThenBy(c => c.CallerMemberName ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(c => c.SymbolName, StringComparer.Ordinal)
            .ToList();
        var maxCallers = Math.Clamp(requestedMaxCallers, 1, MaxCallersLimit);
        var isTruncated = orderedCallers.Count > maxCallers;
        var callersList = isTruncated ? orderedCallers.Take(maxCallers).ToList() : orderedCallers;
        return new CallersReportDto(
            orderedCallers.Count,
            callersList,
            isTruncated,
            isTruncated ? ["maxCallers"] : [],
            Completeness: orderedCallers.Count == 0
                ? FeatureContextStatus.Empty
                : isTruncated ? FeatureContextStatus.Truncated : FeatureContextStatus.Complete,
            NextStep: isTruncated
                ? "Abschnitt impact: maxCallers erhöhen und die statischen Call-Sites erneut abfragen."
                : null);
    }

    private static async Task<StaticTestContextReportDto> CollectTestsAsync(
        ISymbol symbol,
        Solution solution,
        int requestedMaxTests,
        CancellationToken ct)
    {
        var testResults = await TestCoverageScanner.FindTestsForSymbolAsync(symbol, solution, ct);
        var maxTests = Math.Clamp(requestedMaxTests, 1, MaxTestFilesLimit);
        var isTruncated = testResults.TestFiles.Count > maxTests;
        var testFiles = isTruncated ? testResults.TestFiles.Take(maxTests).ToList() : testResults.TestFiles;
        var projection = ProjectTestCandidates(testFiles, isTruncated);

        return new StaticTestContextReportDto(
            testResults.TotalMatchingTests,
            testResults.TestFiles.Count,
            projection.Files,
            projection.IsTruncated,
            projection.DisplayedTestMethods,
            projection.TruncatedBy,
            Completeness: testResults.TotalMatchingTests == 0
                ? FeatureContextStatus.Empty
                : projection.IsTruncated ? FeatureContextStatus.Truncated : FeatureContextStatus.Complete,
            EvidenceBoundary: FeatureContextSemantics.StaticTestCandidates,
            NextStep: projection.IsTruncated
                ? "Abschnitt testContext: maxTests erhöhen und die statischen Testkandidaten erneut abfragen."
                : null);
    }

    private static TestCandidateProjection ProjectTestCandidates(
        IReadOnlyList<TestFileCoverageResult> testFiles,
        bool isTruncated)
    {
        var reasons = isTruncated ? new List<string> { "maxTests" } : [];
        var remainingMethods = MaxTestMethodsTotal;
        var displayedMethods = 0;
        var projectedFiles = new List<StaticTestCandidateFileDto>(testFiles.Count);
        var methodsAfterPerFileCaps = testFiles.Sum(file => Math.Min(file.TestMethods.Count, MaxTestMethodsPerFile));

        foreach (var file in testFiles)
        {
            var totalMethods = file.TestMethods.Count;
            var take = Math.Min(Math.Min(totalMethods, MaxTestMethodsPerFile), remainingMethods);
            var takeWithoutPerFileCap = Math.Min(totalMethods, remainingMethods);
            if (take < takeWithoutPerFileCap && !reasons.Contains("maxTestMethodsPerFile", StringComparer.Ordinal))
            {
                isTruncated = true;
                reasons.Add("maxTestMethodsPerFile");
            }

            remainingMethods -= take;
            displayedMethods += take;
            projectedFiles.Add(new StaticTestCandidateFileDto(
                file.FilePath,
                file.TestClassName,
                file.Category,
                file.MatchReason,
                file.TestMethods.Take(take).ToList(),
                file.TotalClassTests,
                totalMethods));
        }

        if (methodsAfterPerFileCaps > MaxTestMethodsTotal && !reasons.Contains("maxTestMethodsTotal", StringComparer.Ordinal))
        {
            isTruncated = true;
            reasons.Add("maxTestMethodsTotal");
        }

        return new TestCandidateProjection(projectedFiles, displayedMethods, isTruncated, reasons);
    }

    private sealed record TestCandidateProjection(
        IReadOnlyList<StaticTestCandidateFileDto> Files,
        int DisplayedTestMethods,
        bool IsTruncated,
        IReadOnlyList<string> TruncatedBy);

    private static SymbolDeclarationDto ExtractDeclaration(
        ISymbol symbol,
        string solutionDir,
        AnalysisSymbolIdentity? assemblyIdentity)
    {
        var (filePath, startLine, endLine) = ExtractLocation(symbol, solutionDir);
        var lineCount = endLine >= startLine ? endLine - startLine + 1 : 0;
        var (returnType, parameters) = ExtractTypeAndParameters(symbol);
        var docCommentId = assemblyIdentity?.Format(
            symbol.TryGetDocCommentId() ?? CallGraphTraversal.GetStableSymbolId(symbol))
            ?? symbol.TryGetDocCommentId();

        return new SymbolDeclarationDto(
            Name: symbol.ToDisplayString(),
            Kind: symbol.Kind.ToString(),
            Accessibility: symbol.DeclaredAccessibility.ToString().ToLowerInvariant(),
            FilePath: filePath,
            StartLine: startLine,
            EndLine: endLine,
            LineCount: lineCount,
            ContainerType: symbol.ContainingType?.Name,
            ReturnType: returnType,
            Parameters: parameters,
            DocCommentId: docCommentId
        );
    }

    private static (string FilePath, int StartLine, int EndLine) ExtractLocation(ISymbol symbol, string solutionDir)
    {
        var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef != null)
        {
            var syntax = syntaxRef.GetSyntax();
            var lineSpan = syntax.GetLocation().GetLineSpan();
            var filePath = PathNormalizer.ToRelative(solutionDir, lineSpan.Path);
            var start = lineSpan.StartLinePosition.Line + 1;
            var end = lineSpan.EndLinePosition.Line + 1;
            return (filePath, start, end);
        }

        var loc = symbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (loc?.SourceTree != null)
        {
            var filePath = PathNormalizer.ToRelative(solutionDir, loc.SourceTree.FilePath);
            var lineSpan = loc.GetLineSpan();
            var start = lineSpan.StartLinePosition.Line + 1;
            var end = lineSpan.EndLinePosition.Line + 1;
            return (filePath, start, end);
        }

        return ("", 0, 0);
    }

    private static (string? ReturnType, IReadOnlyList<string> Parameters) ExtractTypeAndParameters(ISymbol symbol)
    {
        if (symbol is IMethodSymbol method)
        {
            var returnType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            var parameters = method.Parameters
                .Select(p => $"{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}")
                .ToList();
            return (returnType, parameters);
        }

        if (symbol is IPropertySymbol prop)
        {
            return (prop.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), []);
        }

        if (symbol is IFieldSymbol field)
        {
            return (field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), []);
        }

        return (null, []);
    }


    private static async Task<ViolationsReportDto> CollectViolationsAsync(
        Solution solution,
        SymbolDeclarationDto declaration,
        ILinterEngineConfig? config,
        ILintConsole? console,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (config is null)
        {
            return new ViolationsReportDto(
                0, 0, [], false, FeatureContextStatus.NotConfigured,
                FeatureContextReasonCodes.RulesNotConfigured,
                [],
                "Abschnitt violations: ainetlinter-rules.json bereitstellen und den Lint-Abschnitt erneut abfragen.");
        }

        if (string.IsNullOrEmpty(declaration.FilePath))
        {
            return new ViolationsReportDto(
                0, 0, [], false, FeatureContextStatus.NotApplicable,
                FeatureContextReasonCodes.SourceFileUnavailable);
        }

        if (DiffImpactAnalyzer.FindDocumentByPath(solution, declaration.FilePath) is null)
        {
            return new ViolationsReportDto(
                0, 0, [], false, FeatureContextStatus.NotDecidable,
                FeatureContextReasonCodes.SourceFileUnavailable);
        }

        try
        {
            var concreteConfig = (Config)config;
            var engine = new LinterEngine(concreteConfig, configContent: null, profiler: null, console: console);
            var allViolations = await engine.RunAsync(solution, noCache: true, cacheTtlMinutes: 0, ct);

            return FilterViolationsForFile(allViolations, declaration);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new ViolationsReportDto(
                0, 0, [], false, FeatureContextStatus.Partial,
                FeatureContextReasonCodes.ViolationsScanFailed,
                [],
                "Abschnitt violations: den Lint-Abschnitt erneut anfordern und den Workspace-Fehler prüfen.");
        }
    }

    private static ViolationsReportDto FilterViolationsForFile(
        IEnumerable<RuleViolation> allViolations,
        SymbolDeclarationDto declaration)
    {
        var normalizedTarget = PathNormalizer.NormalizeSeparators(declaration.FilePath);
        var fileViolations = allViolations
            .Where(v => IsMatchingFilePath(v.FilePath, normalizedTarget))
            .OrderBy(v => PathNormalizer.NormalizeSeparators(v.FilePath), StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => v.LineNumber)
            .ThenBy(v => v.RuleName, StringComparer.Ordinal)
            .ThenBy(v => v.Details, StringComparer.Ordinal)
            .ToList();

        var items = fileViolations.Select(v => new ViolationItemDto(
            RuleId: v.RuleName,
            Message: v.Details,
            Line: v.LineNumber,
            IsDirectlyOnSymbol: v.LineNumber >= declaration.StartLine && v.LineNumber <= declaration.EndLine
        )).ToList();

        var violationsOnSymbol = items.Count(i => i.IsDirectlyOnSymbol);
        var isTruncated = items.Count > MaxViolations;
        var displayItems = isTruncated ? items.Take(MaxViolations).ToList() : items;

        return new ViolationsReportDto(
            fileViolations.Count,
            violationsOnSymbol,
            displayItems,
            isTruncated,
            isTruncated ? FeatureContextStatus.Truncated : FeatureContextStatus.Complete,
            null,
            isTruncated ? ["maxViolations"] : [],
            isTruncated
                ? "Abschnitt violations: maxViolations verfeinern und die Datei erneut prüfen."
                : null);
    }

    private static bool IsMatchingFilePath(string filePath, string normalizedTarget)
    {
        var normalizedV = PathNormalizer.NormalizeSeparators(filePath);
        return normalizedV.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase) ||
               normalizedV.EndsWith("/" + normalizedTarget, StringComparison.OrdinalIgnoreCase) ||
               normalizedTarget.EndsWith("/" + normalizedV, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Kontext-Parameter fuer die FeatureContext-Scan-Ausfuehrung.
/// </summary>
internal sealed record FeatureContextScanContext(
    Solution Solution,
    ILinterEngineConfig? Config,
    ILintConsole? Console,
    FeatureContextOptions Options,
    AnalysisSymbolIdentity? AssemblySymbolIdentity = null
);
