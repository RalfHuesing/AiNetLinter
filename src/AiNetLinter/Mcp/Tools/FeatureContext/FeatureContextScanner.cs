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
    internal static async Task<FeatureContextPayload> ScanAsync(
        ISymbol symbol,
        FeatureContextScanContext context,
        CancellationToken ct)
    {
        var solutionDir = Path.GetDirectoryName(context.Solution.FilePath) ?? "";
        var declaration = ExtractDeclaration(symbol, solutionDir, context.AssemblySymbolIdentity);

        var metrics = context.Options.IncludeMetrics
            ? MetricsLookupScanner.ScanSymbol(symbol, context.Config, solutionDir, ct, context.AssemblySymbolIdentity)
            : null;

        var callers = context.Options.IncludeCallers
            ? await CollectCallersAsync(symbol, context.Solution, context.Options.MaxCallers)
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
        int requestedMaxCallers)
    {
        var allCallers = await DiffImpactAnalyzer.FindCallSiteEntriesAsync(symbol, solution);
        var maxCallers = Math.Clamp(requestedMaxCallers, 1, 50);
        var isTruncated = allCallers.Count > maxCallers;
        var callersList = isTruncated ? allCallers.Take(maxCallers).ToList() : allCallers;
        return new CallersReportDto(allCallers.Count, callersList, isTruncated);
    }

    private static async Task<TestCoverageReportDto> CollectTestsAsync(
        ISymbol symbol,
        Solution solution,
        int requestedMaxTests,
        CancellationToken ct)
    {
        var testResults = await TestCoverageScanner.FindTestsForSymbolAsync(symbol, solution, ct);
        var maxTests = Math.Clamp(requestedMaxTests, 1, 50);
        var isTruncated = testResults.TestFiles.Count > maxTests;
        var testFiles = isTruncated ? testResults.TestFiles.Take(maxTests).ToList() : testResults.TestFiles;
        var dtos = testFiles.Select(f => new TestFileCoverageDto(
            FilePath: f.FilePath,
            TestClassName: f.TestClassName,
            Category: f.Category,
            MatchReason: f.MatchReason,
            TestMethods: f.TestMethods,
            TotalClassTests: f.TotalClassTests
        )).ToList();

        return new TestCoverageReportDto(testResults.TotalMatchingTests, testResults.TestFiles.Count, dtos, isTruncated);
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
        if (string.IsNullOrEmpty(declaration.FilePath))
        {
            return new ViolationsReportDto(0, 0, [], false);
        }

        try
        {
            var concreteConfig = (Config)config;
            var engine = new LinterEngine(concreteConfig, rulesJsonContent: null, profiler: null, console: console);
            var allViolations = await engine.RunAsync(solution, noCache: true, cacheTtlMinutes: 0, ct);

            return FilterViolationsForFile(allViolations, declaration);
        }
        catch (Exception ignored)
        {
            _ = ignored;
            return new ViolationsReportDto(0, 0, [], false);
        }
    }

    private static ViolationsReportDto FilterViolationsForFile(
        IEnumerable<RuleViolation> allViolations,
        SymbolDeclarationDto declaration)
    {
        var normalizedTarget = PathNormalizer.NormalizeSeparators(declaration.FilePath);
        var fileViolations = allViolations
            .Where(v => IsMatchingFilePath(v.FilePath, normalizedTarget))
            .OrderBy(v => v.LineNumber)
            .ToList();

        var items = fileViolations.Select(v => new ViolationItemDto(
            RuleId: v.RuleName,
            Message: v.Details,
            Line: v.LineNumber,
            IsDirectlyOnSymbol: v.LineNumber >= declaration.StartLine && v.LineNumber <= declaration.EndLine
        )).ToList();

        var violationsOnSymbol = items.Count(i => i.IsDirectlyOnSymbol);
        var isTruncated = items.Count > 50;
        var displayItems = isTruncated ? items.Take(50).ToList() : items;

        return new ViolationsReportDto(fileViolations.Count, violationsOnSymbol, displayItems, isTruncated);
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
