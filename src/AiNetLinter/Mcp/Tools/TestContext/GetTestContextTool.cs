#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.TestContext;

/// <summary>
/// MCP-Tool <c>get_test_context</c>: Ermittelt zielgerichtet alle Test-Dateien, Test-Klassen,
/// Test-Methoden und Kategorien fuer ein gegebenes Produktions-Symbol.
/// </summary>
internal static class GetTestContextTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state,
        TestContextOptions options,
        CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var targetSymbol = options.EffectiveSymbol;
        if (string.IsNullOrWhiteSpace(targetSymbol))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbol' (oder 'symbolIdentifier') fehlt oder ist leer.",
                hint: "symbol angeben: z. B. \"Namespace.Klasse\", \"Namespace.Klasse.Methode\", \"Datei.cs:42\" oder DocCommentId.");
        }

        try
        {
            var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(solution, targetSymbol, ct);
            if (error is not null) return error;
            if (symbol is null) return McpToolResults.SymbolNotFound(targetSymbol);

            var testResults = await TestCoverageScanner.FindTestsForSymbolAsync(symbol, solution, ct);
            var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
            var targetFilePath = ExtractFilePath(symbol, solutionDir);

            var maxResults = Math.Clamp(options.MaxResults, 1, 100);
            var isTruncated = testResults.TestFiles.Count > maxResults;
            var testFiles = isTruncated ? testResults.TestFiles.Take(maxResults).ToList() : testResults.TestFiles;
            var recommendedCommands = BuildRecommendedCommands(testResults.TestFiles);
            var isUntested = testResults.TotalMatchingTests == 0 || testResults.TestFiles.Count == 0;

            var payload = new TestContextPayload(
                TargetSymbol: symbol.ToDisplayString(),
                TargetKind: symbol.Kind.ToString(),
                TargetFilePath: targetFilePath,
                TotalMatchingTests: testResults.TotalMatchingTests,
                TotalTestFiles: testResults.TestFiles.Count,
                TestFiles: testFiles,
                RecommendedTestCommands: recommendedCommands,
                IsUntested: isUntested,
                IsTruncated: isTruncated
            );

            var markdown = TestContextFormatter.FormatReport(payload);
            return McpToolResults.Text(markdown, payload);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in get_test_context: {ex.Message}",
                context: targetSymbol);
        }
    }

    private static string ExtractFilePath(ISymbol symbol, string solutionDir)
    {
        var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef != null)
        {
            var lineSpan = syntaxRef.GetSyntax().GetLocation().GetLineSpan();
            return PathNormalizer.ToRelative(solutionDir, lineSpan.Path);
        }

        var loc = symbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (loc?.SourceTree != null)
        {
            return PathNormalizer.ToRelative(solutionDir, loc.SourceTree.FilePath);
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> BuildRecommendedCommands(IReadOnlyList<TestFileCoverageResult> testFiles)
    {
        var commands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in testFiles)
        {
            var project = DeduceProjectDir(file.FilePath);
            var className = file.TestClassName;
            if (!string.IsNullOrWhiteSpace(className))
            {
                commands.Add($"dotnet test {project} --filter FullyQualifiedName~{className}");
            }
        }

        return commands.OrderBy(c => c).ToList();
    }

    private static string DeduceProjectDir(string filePath)
    {
        var normalized = PathNormalizer.NormalizeSeparators(filePath);
        if (normalized.Contains(".IntegrationTests/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("src/AiNetLinter.IntegrationTests/", StringComparison.OrdinalIgnoreCase))
        {
            return "src/AiNetLinter.IntegrationTests";
        }

        if (normalized.Contains(".FastTests/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("src/AiNetLinter.FastTests/", StringComparison.OrdinalIgnoreCase))
        {
            return "src/AiNetLinter.FastTests";
        }

        var parts = normalized.Split('/');
        if (parts.Length > 2 && (parts[0].Equals("src", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("tests", StringComparison.OrdinalIgnoreCase)))
        {
            return $"{parts[0]}/{parts[1]}";
        }

        return "src/AiNetLinter.FastTests";
    }
}
