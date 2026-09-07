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

        var metrics = context.Options.IncludeMetrics
            ? MetricsLookupScanner.ScanSymbol(symbol, context.Config, solutionDir, ct, context.AssemblySymbolIdentity)
            : null;

        var callers = context.Options.IncludeCallers
            ? await CollectCallersAsync(symbol, context.Solution, context.Options.MaxCallers, ct)
            : null;

        var tests = context.Options.IncludeTests
            ? await CollectTestsAsync(symbol, context.Solution, context.Options.MaxTests, ct)
            : null;

        var violations = context.Options.IncludeViolations
            ? await CollectViolationsAsync(context.Solution, declaration, context.Config, context.Console, ct)
            : null;

        return new FeatureContextPayload(declaration, metrics, callers, tests, violations);
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
            isTruncated ? ["maxCallers"] : []);
    }

    private static async Task<TestCoverageReportDto> CollectTestsAsync(
        ISymbol symbol,
        Solution solution,
        int requestedMaxTests,
        CancellationToken ct)
    {
        var testResults = await TestCoverageScanner.FindTestsForSymbolAsync(symbol, solution, ct);
        var maxTests = Math.Clamp(requestedMaxTests, 1, MaxTestFilesLimit);
        var isTruncated = testResults.TestFiles.Count > maxTests;
        var testFiles = isTruncated ? testResults.TestFiles.Take(maxTests).ToList() : testResults.TestFiles;
        var truncationReasons = isTruncated ? new List<string> { "maxTests" } : [];
        var remainingMethods = MaxTestMethodsTotal;
        var displayedTestMethods = 0;
        var dtos = new List<TestFileCoverageDto>(testFiles.Count);

        foreach (var file in testFiles)
        {
            var totalMatchingMethods = file.TestMethods.Count;
            var take = Math.Min(Math.Min(totalMatchingMethods, MaxTestMethodsPerFile), remainingMethods);
            var methods = file.TestMethods.Take(take).ToList();
            if (take < totalMatchingMethods)
            {
                isTruncated = true;
                if (!truncationReasons.Contains("maxTestMethodsPerFile", StringComparer.Ordinal))
                {
                    truncationReasons.Add("maxTestMethodsPerFile");
                }
            }

            remainingMethods -= take;
            displayedTestMethods += take;
            dtos.Add(new TestFileCoverageDto(
                FilePath: file.FilePath,
                TestClassName: file.TestClassName,
                Category: file.Category,
                MatchReason: file.MatchReason,
                TestMethods: methods,
                TotalClassTests: file.TotalClassTests,
                TotalMatchingMethods: totalMatchingMethods));
        }

        if (testResults.TotalMatchingTests > MaxTestMethodsTotal)
        {
            isTruncated = true;
            if (!truncationReasons.Contains("maxTestMethodsTotal", StringComparer.Ordinal))
            {
                truncationReasons.Add("maxTestMethodsTotal");
            }
        }

        return new TestCoverageReportDto(
            testResults.TotalMatchingTests,
            testResults.TestFiles.Count,
            dtos,
            isTruncated,
            displayedTestMethods,
            truncationReasons);
    }

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
        ILinterEngineConfig config,
        ILintConsole? console,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(declaration.FilePath))
        {
            return new ViolationsReportDto(
                0, 0, [], false, FeatureContextStatus.NotApplicable,
                FeatureContextReasonCodes.SourceFileUnavailable);
        }

        if (DiffImpactAnalyzer.FindDocumentByPath(solution, declaration.FilePath) is null)
        {
            return new ViolationsReportDto(
                0, 0, [], false, FeatureContextStatus.Unavailable,
                FeatureContextReasonCodes.SourceFileUnavailable);
        }

        try
        {
            var concreteConfig = (Config)config;
            var engine = new LinterEngine(concreteConfig, rulesJsonContent: null, profiler: null, console: console);
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
                0, 0, [], false, FeatureContextStatus.Failed,
                FeatureContextReasonCodes.ViolationsScanFailed);
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
            isTruncated ? ["maxViolations"] : []);
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
    ILinterEngineConfig Config,
    ILintConsole? Console,
    FeatureContextOptions Options,
    AnalysisSymbolIdentity? AssemblySymbolIdentity = null
);
