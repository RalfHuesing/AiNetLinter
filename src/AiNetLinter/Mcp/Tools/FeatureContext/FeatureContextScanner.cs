#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Tools.MetricsLookup;
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
        Solution solution,
        ILinterEngineConfig config,
        ILintConsole? console,
        FeatureContextOptions options,
        CancellationToken ct)
    {
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";

        // 1. Symbol-Deklaration
        var declaration = ExtractDeclaration(symbol, solutionDir);

        // 2. Metriken
        MetricsLookupResultDto? metrics = null;
        if (options.IncludeMetrics)
        {
            metrics = MetricsLookupScanner.ScanSymbol(symbol, config, solutionDir, ct);
        }

        // 3. Callers
        CallersReportDto? callers = null;
        if (options.IncludeCallers)
        {
            var allCallers = await DiffImpactAnalyzer.FindCallSiteEntriesAsync(symbol, solution);
            var maxCallers = Math.Clamp(options.MaxCallers, 1, 50);
            var isTruncated = allCallers.Count > maxCallers;
            var callersList = isTruncated ? allCallers.Take(maxCallers).ToList() : allCallers;
            callers = new CallersReportDto(allCallers.Count, callersList, isTruncated);
        }

        // 4. Tests
        TestCoverageReportDto? tests = null;
        if (options.IncludeTests)
        {
            var testResults = await TestCoverageScanner.FindTestsForSymbolAsync(symbol, solution, ct);
            var maxTests = Math.Clamp(options.MaxTests, 1, 50);
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

            tests = new TestCoverageReportDto(testResults.TotalMatchingTests, testResults.TestFiles.Count, dtos, isTruncated);
        }

        // 5. Violations
        ViolationsReportDto? violations = null;
        if (options.IncludeViolations)
        {
            violations = await CollectViolationsAsync(solution, declaration, config, console, ct);
        }

        return new FeatureContextPayload(declaration, metrics, callers, tests, violations);
    }

    private static SymbolDeclarationDto ExtractDeclaration(ISymbol symbol, string solutionDir)
    {
        var loc = symbol.Locations.FirstOrDefault(l => l.IsInSource);
        var filePath = "";
        var startLine = 0;
        var endLine = 0;

        if (loc?.SourceTree != null)
        {
            filePath = PathNormalizer.ToRelative(solutionDir, loc.SourceTree.FilePath);
            var lineSpan = loc.GetLineSpan();
            startLine = lineSpan.StartLinePosition.Line + 1;
            endLine = lineSpan.EndLinePosition.Line + 1;
        }

        var lineCount = endLine >= startLine ? endLine - startLine + 1 : 0;
        var name = symbol.ToDisplayString();
        var kind = symbol.Kind.ToString();
        var accessibility = symbol.DeclaredAccessibility.ToString().ToLowerInvariant();
        var containerType = symbol.ContainingType?.Name;

        string? returnType = null;
        var parameters = new List<string>();

        if (symbol is IMethodSymbol method)
        {
            returnType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            parameters = method.Parameters
                .Select(p => $"{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}")
                .ToList();
        }
        else if (symbol is IPropertySymbol prop)
        {
            returnType = prop.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        }
        else if (symbol is IFieldSymbol field)
        {
            returnType = field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        }

        string? docCommentId = null;
        try
        {
            docCommentId = DocumentationCommentId.CreateDeclarationId(symbol);
        }
        catch
        {
            // Ignorieren falls nicht unterstuetzt
        }

        return new SymbolDeclarationDto(
            Name: name,
            Kind: kind,
            Accessibility: accessibility,
            FilePath: filePath,
            StartLine: startLine,
            EndLine: endLine,
            LineCount: lineCount,
            ContainerType: containerType,
            ReturnType: returnType,
            Parameters: parameters,
            DocCommentId: docCommentId
        );
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
            var engine = new LinterEngine(
                config: concreteConfig,
                rulesJsonContent: null,
                profiler: null,
                console: console,
                args: null);

            var allViolations = await engine.RunAsync(solution, noCache: true, cacheTtlMinutes: 0, ct);
            var normalizedTarget = PathNormalizer.NormalizeSeparators(declaration.FilePath);

            var fileViolations = allViolations
                .Where(v =>
                {
                    var normalizedV = PathNormalizer.NormalizeSeparators(v.FilePath);
                    return normalizedV.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase) ||
                           normalizedV.EndsWith("/" + normalizedTarget, StringComparison.OrdinalIgnoreCase) ||
                           normalizedTarget.EndsWith("/" + normalizedV, StringComparison.OrdinalIgnoreCase);
                })
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
        catch
        {
            return new ViolationsReportDto(0, 0, [], false);
        }
    }
}
