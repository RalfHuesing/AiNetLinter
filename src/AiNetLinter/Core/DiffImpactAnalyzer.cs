#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using AiNetLinter.Output;

namespace AiNetLinter.Core;

/// <summary>
/// Analysiert das Git-Diff und findet alle Call-Sites in der Solution, die von geaenderten Methodensignaturen betroffen sind.
/// </summary>
public sealed class DiffImpactAnalyzer
{
    private const string GitCommand = "git";
    private const string FilePathPrefix = "+++ b/";
    private const string HunkPrefix = "@@ ";

    /// <summary>
    /// Führt die semantische Diff-Impact-Analyse aus und gibt eine Liste der betroffenen Aufrufstellen zurück.
    /// </summary>
    /// <param name="solution">Die geladene Roslyn-Solution.</param>
    /// <param name="targetPath">Der Zielpfad des Projekts/der Solution.</param>
    /// <param name="gitSinceRef">Der Git-Commit-Verweis (z. B. HEAD~1) oder null/leer für uncommitteten Code.</param>
    /// <param name="verbose">Aktiviert detailliertes Protokoll-Logging.</param>
    /// <returns>Eine Liste von formatierten Aufrufstellen (Call-Sites).</returns>
    public static async Task<List<string>> AnalyzeAsync(Solution solution, string targetPath, string? gitSinceRef, bool verbose)
    {
        var repoRoot = FindGitRoot(targetPath);
        if (repoRoot == null)
        {
            LogGitWarning(verbose);
            return [];
        }

        var diffOutput = RunGitDiff(repoRoot, gitSinceRef);
        if (string.IsNullOrEmpty(diffOutput))
        {
            return [];
        }

        var hunks = ParseGitDiffHunks(diffOutput);
        var changedSymbols = await GetChangedSymbolsFromHunksAsync(solution, repoRoot, hunks);

        return await FindAllCallSitesAsync(changedSymbols, solution);
    }

    private static void LogGitWarning(bool verbose)
    {
        if (verbose)
        {
            Console.WriteLine("[WARNING]: Kein Git-Repository gefunden.");
        }
    }

    private static string? FindGitRoot(string startPath)
    {
        var current = File.Exists(startPath) ? Path.GetDirectoryName(startPath) : startPath;
        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.Exists(Path.Combine(current, ".git")))
            {
                return current;
            }
            current = Path.GetDirectoryName(current);
        }
        return null;
    }

    private static string? RunGitDiff(string repoRoot, string? gitSinceRef)
    {
        var args = string.IsNullOrEmpty(gitSinceRef) ? "diff -U0 -- *.cs" : $"diff -U0 {gitSinceRef} -- *.cs";
        var startInfo = new ProcessStartInfo
        {
            FileName = GitCommand,
            Arguments = args,
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo);
        if (process == null) return null;

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0 ? output : null;
    }

    internal static Dictionary<string, List<int>> ParseGitDiffHunks(string gitDiffOutput)
    {
        var result = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        var lines = gitDiffOutput.Split('\n');
        string? currentFile = null;

        foreach (var line in lines)
        {
            currentFile = ProcessDiffLine(line, currentFile, result);
        }

        return result;
    }

    private static string? ProcessDiffLine(string line, string? currentFile, Dictionary<string, List<int>> result)
    {
        if (line.StartsWith(FilePathPrefix, StringComparison.Ordinal))
        {
            return line.Substring(FilePathPrefix.Length).Trim().Replace('/', Path.DirectorySeparatorChar);
        }

        if (currentFile != null && line.StartsWith(HunkPrefix, StringComparison.Ordinal))
        {
            ParseHunkLine(line, currentFile, result);
        }

        return currentFile;
    }

    internal static void ParseHunkLine(string line, string currentFile, Dictionary<string, List<int>> result)
    {
        if (!TryExtractHunkRange(line, out var startLine, out var count))
        {
            return;
        }

        var list = GetOrCreateLineList(currentFile, result);
        for (int i = 0; i < count; i++)
        {
            list.Add(startLine + i);
        }
    }

    private static bool TryExtractHunkRange(string line, out int startLine, out int count)
    {
        startLine = 0;
        count = 0;

        var parts = line.Split(' ');
        if (parts.Length < 3) return false;

        var plusPart = parts[2];
        if (!plusPart.StartsWith('+')) return false;

        var numbers = plusPart.Substring(1).Split(',');
        if (!int.TryParse(numbers[0], out startLine)) return false;

        count = 1;
        if (numbers.Length > 1)
        {
            _ = int.TryParse(numbers[1], out count);
        }

        return true;
    }

    private static List<int> GetOrCreateLineList(string currentFile, Dictionary<string, List<int>> result)
    {
        if (!result.TryGetValue(currentFile, out var list))
        {
            list = new List<int>();
            result[currentFile] = list;
        }
        return list;
    }

    private static Document? FindDocumentByPath(Solution solution, string filePath)
    {
        foreach (var project in solution.Projects)
        {
            var doc = project.Documents.FirstOrDefault(d => string.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
            if (doc != null) return doc;
        }
        return null;
    }

    private static async Task<List<ISymbol>> GetChangedSymbolsFromHunksAsync(
        Solution solution, string repoRoot, Dictionary<string, List<int>> hunks)
    {
        var changedSymbols = new List<ISymbol>();
        foreach (var pair in hunks)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(repoRoot, pair.Key));
            var document = FindDocumentByPath(solution, absolutePath);
            if (document == null) continue;

            var symbols = await GetChangedSymbolsAsync(document, pair.Value);
            changedSymbols.AddRange(symbols);
        }
        return changedSymbols;
    }

    private static async Task<List<string>> FindAllCallSitesAsync(List<ISymbol> symbols, Solution solution)
    {
        var result = new List<string>();
        foreach (var symbol in symbols.Distinct<ISymbol>(SymbolEqualityComparer.Default))
        {
            var callSites = await FindCallSitesAsync(symbol, solution);
            result.AddRange(callSites);
        }
        return result;
    }

    private static async Task<List<ISymbol>> GetChangedSymbolsAsync(Document document, List<int> changedLines)
    {
        var root = await document.GetSyntaxRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        if (root == null || semanticModel == null) return [];

        var symbols = new List<ISymbol>();
        
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        AddChangedSymbols(methods, semanticModel, changedLines, symbols);

        var constructors = root.DescendantNodes().OfType<ConstructorDeclarationSyntax>();
        AddChangedSymbols(constructors, semanticModel, changedLines, symbols);

        return symbols;
    }

    private static void AddChangedSymbols(
        IEnumerable<SyntaxNode> nodes, SemanticModel semanticModel, List<int> changedLines, List<ISymbol> symbols)
    {
        foreach (var node in nodes)
        {
            var symbol = GetValidChangedSymbol(node, semanticModel, changedLines);
            if (symbol != null)
            {
                symbols.Add(symbol);
            }
        }
    }

    private static ISymbol? GetValidChangedSymbol(SyntaxNode node, SemanticModel semanticModel, List<int> changedLines)
    {
        if (!IntersectsWithChangedLines(node, changedLines))
        {
            return null;
        }

        var symbol = semanticModel.GetDeclaredSymbol(node);
        if (symbol == null || !IsPublicOrInternal(symbol))
        {
            return null;
        }

        return symbol;
    }

    private static bool IntersectsWithChangedLines(SyntaxNode node, List<int> changedLines)
    {
        var span = node.GetLocation().GetLineSpan();
        var start = span.StartLinePosition.Line + 1;
        var end = span.EndLinePosition.Line + 1;

        foreach (var line in changedLines)
        {
            if (line >= start && line <= end) return true;
        }
        return false;
    }

    private static bool IsPublicOrInternal(ISymbol symbol)
    {
        var accessibility = symbol.DeclaredAccessibility;
        return accessibility == Accessibility.Public ||
               accessibility == Accessibility.Internal ||
               accessibility == Accessibility.Protected ||
               accessibility == Accessibility.ProtectedOrInternal;
    }

    private static async Task<List<string>> FindCallSitesAsync(ISymbol symbol, Solution solution)
    {
        var callSites = new List<string>();
        var references = await SymbolFinder.FindReferencesAsync(symbol, solution);

        foreach (var reference in references)
        {
            foreach (var location in reference.Locations)
            {
                var lineSpan = location.Location.GetLineSpan();
                var filePath = lineSpan.Path;
                var line = lineSpan.StartLinePosition.Line + 1;

                var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
                var relativePath = PathNormalizer.ToRelative(outputRoot, filePath);

                callSites.Add($"{relativePath}:{line} - Aufruf von '{symbol.ContainingType?.Name}.{symbol.Name}' in Projekt '{location.Document.Project.Name}'");
            }
        }

        return callSites;
    }
}
