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
            var suggestedTestPath = isUntested
                ? SuggestTestFilePath(symbol, targetFilePath, solution, solutionDir)
                : null;

            var payload = new TestContextPayload(
                TargetSymbol: symbol.ToDisplayString(),
                TargetKind: symbol.Kind.ToString(),
                TargetFilePath: targetFilePath,
                TotalMatchingTests: testResults.TotalMatchingTests,
                TotalTestFiles: testResults.TestFiles.Count,
                TestFiles: testFiles,
                RecommendedTestCommands: recommendedCommands,
                IsUntested: isUntested,
                IsTruncated: isTruncated,
                SuggestedTestFilePath: suggestedTestPath
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
            var className = file.TestClassName;
            if (string.IsNullOrWhiteSpace(className)) continue;

            if (!string.IsNullOrWhiteSpace(file.ProjectDirectory) && file.ProjectDirectory != ".")
            {
                commands.Add($"dotnet test {file.ProjectDirectory} --filter FullyQualifiedName~{className}");
            }
            else
            {
                commands.Add($"dotnet test --filter FullyQualifiedName~{className}");
            }
        }

        return commands.OrderBy(c => c).ToList();
    }

    private static string SuggestTestFilePath(
        ISymbol symbol,
        string targetFilePath,
        Solution solution,
        string solutionDir)
    {
        var symbolName = symbol.Name.Split('.').Last().Split(':').First();
        var preferredTestProj = FindPreferredTestProject(solution);

        if (preferredTestProj != null)
        {
            var testProjDir = GetProjectDirectory(preferredTestProj, solutionDir);
            var subDir = ExtractSourceSubdirectory(solution, targetFilePath, solutionDir);
            return FormatSuggestedPath(testProjDir, subDir, symbolName);
        }

        return BuildFallbackTestPath(targetFilePath, symbolName);
    }

    private static Project? FindPreferredTestProject(Solution solution)
    {
        var testProjects = solution.Projects
            .Where(p => TestProjectDetector.IsTestProject(p))
            .ToList();

        return testProjects.FirstOrDefault(p =>
            p.Name.Contains("Unit", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Fast", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Spec", StringComparison.OrdinalIgnoreCase)) ?? testProjects.FirstOrDefault();
    }

    private static string ExtractSourceSubdirectory(Solution solution, string targetFilePath, string solutionDir)
    {
        var sourceProj = solution.Projects.FirstOrDefault(p =>
            p.Documents.Any(d => d.FilePath != null &&
                PathNormalizer.ToRelative(solutionDir, d.FilePath).Equals(targetFilePath, StringComparison.OrdinalIgnoreCase)));

        if (sourceProj != null)
        {
            var sourceProjDir = GetProjectDirectory(sourceProj, solutionDir);
            if (!string.IsNullOrEmpty(sourceProjDir) &&
                targetFilePath.StartsWith(sourceProjDir + "/", StringComparison.OrdinalIgnoreCase))
            {
                var inner = targetFilePath[(sourceProjDir.Length + 1)..];
                var dir = Path.GetDirectoryName(inner)?.Replace('\\', '/') ?? string.Empty;
                return dir == "." ? string.Empty : dir;
            }
        }

        var fallbackDir = Path.GetDirectoryName(targetFilePath)?.Replace('\\', '/') ?? string.Empty;
        return fallbackDir == "." ? string.Empty : fallbackDir;
    }

    private static string FormatSuggestedPath(string testProjDir, string subDir, string symbolName)
    {
        if (string.IsNullOrEmpty(testProjDir))
        {
            return string.IsNullOrEmpty(subDir)
                ? $"{symbolName}Tests.cs"
                : $"{subDir}/{symbolName}Tests.cs";
        }

        return string.IsNullOrEmpty(subDir)
            ? $"{testProjDir}/{symbolName}Tests.cs"
            : $"{testProjDir}/{subDir}/{symbolName}Tests.cs";
    }

    private static string BuildFallbackTestPath(string targetFilePath, string symbolName)
    {
        if (targetFilePath.StartsWith("src/", StringComparison.OrdinalIgnoreCase))
        {
            var parts = targetFilePath.Split('/');
            if (parts.Length > 2)
            {
                var srcProj = parts[1];
                var sub = string.Join('/', parts.Skip(2).Take(parts.Length - 3));
                var testFolder = $"tests/{srcProj}.Tests";
                return string.IsNullOrEmpty(sub)
                    ? $"{testFolder}/{symbolName}Tests.cs"
                    : $"{testFolder}/{sub}/{symbolName}Tests.cs";
            }
        }

        var dir = Path.GetDirectoryName(targetFilePath)?.Replace('\\', '/') ?? string.Empty;
        if (dir == ".") dir = string.Empty;
        return string.IsNullOrEmpty(dir)
            ? $"tests/{symbolName}Tests.cs"
            : $"tests/{dir}/{symbolName}Tests.cs";
    }

    private static string GetProjectDirectory(Project project, string solutionDir)
    {
        if (!string.IsNullOrEmpty(project.FilePath))
        {
            var rel = PathNormalizer.ToRelative(solutionDir, Path.GetDirectoryName(project.FilePath)!);
            return rel == "." ? string.Empty : rel;
        }

        var firstDoc = project.Documents.FirstOrDefault(d => d.FilePath != null);
        if (firstDoc?.FilePath != null)
        {
            var relDoc = PathNormalizer.ToRelative(solutionDir, firstDoc.FilePath);
            var dir = Path.GetDirectoryName(relDoc)?.Replace('\\', '/') ?? string.Empty;
            return dir == "." ? string.Empty : dir;
        }

        return string.Empty;
    }
}
