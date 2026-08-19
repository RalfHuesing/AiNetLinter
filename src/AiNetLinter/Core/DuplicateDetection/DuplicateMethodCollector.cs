#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.DuplicateDetection;

/// <summary>
/// Sammelt zulaessige Methoden/lokale Funktionen fuer Duplicate-Detection. Clone-Erkennung,
/// Refactoring-Drift und strukturelle Drift nutzen dieselbe Eligibility, damit Scope-,
/// Generated-Code- und Token-Filter nicht auseinanderlaufen.
/// </summary>
internal static class DuplicateMethodCollector
{
    internal static async Task<List<EligibleMethod>> CollectAsync(
        Solution solution, DuplicateDetectionOptions options, CancellationToken ct)
    {
        var solutionDir = System.IO.Path.GetDirectoryName(solution.FilePath) ?? "";
        var result = new List<EligibleMethod>();

        foreach (var project in solution.Projects)
        {
            if (!project.SupportsCompilation) continue;
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is null) continue;

            foreach (var document in project.Documents)
            {
                ct.ThrowIfCancellationRequested();
                if (!IsEligibleDocument(document, solutionDir, options)) continue;
                await CollectDocumentAsync(document, compilation, options, result, ct);
            }
        }

        return result;
    }

    private static bool IsEligibleDocument(Document document, string solutionDir, DuplicateDetectionOptions options)
    {
        if (string.IsNullOrEmpty(document.FilePath)) return false;
        if (!SourceFileCatalog.IsValidDocument(document, solutionDir)) return false;
        var path = document.FilePath;
        if (IsPermanentlyExcludedPath(path)) return false;
        if (!PathNormalizer.MatchesScope(path, options.PathScopeFilter)) return false;
        return MatchesScopeType(document, path, options.ScopeType);
    }

    private static bool MatchesScopeType(Document document, string path, string? scopeType)
    {
        if (string.IsNullOrEmpty(scopeType) || string.Equals(scopeType, "all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var isTest = PathNormalizer.IsTestFile(path) ||
                     document.Project.Name.EndsWith("Tests", StringComparison.OrdinalIgnoreCase) ||
                     document.Project.Name.EndsWith(".TestKit", StringComparison.OrdinalIgnoreCase);

        return string.Equals(scopeType, "production", StringComparison.OrdinalIgnoreCase) ? !isTest : isTest;
    }

    private static bool IsPermanentlyExcludedPath(string path)
    {
        var normalized = PathNormalizer.NormalizeSeparators(path);
        return normalized.Contains("/.ainetlinter/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/tests/fixtures/", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task CollectDocumentAsync(
        Document document, Compilation compilation, DuplicateDetectionOptions options,
        List<EligibleMethod> result, CancellationToken ct)
    {
        var syntaxTree = await document.GetSyntaxTreeAsync(ct);
        if (syntaxTree is null) return;
        var root = await syntaxTree.GetRootAsync(ct);
        var semanticModel = compilation.GetSemanticModel(syntaxTree);

        foreach (var candidate in FindCandidateMethods(root))
        {
            ct.ThrowIfCancellationRequested();
            var method = TryBuildEligible(candidate, document.FilePath!, semanticModel, options);
            if (method is not null) result.Add(method);
        }
    }

    private static IEnumerable<(SyntaxNode Declaration, SyntaxNode Body)> FindCandidateMethods(SyntaxNode root)
    {
        foreach (var node in root.DescendantNodes())
        {
            if (node is MethodDeclarationSyntax method)
            {
                var body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
                if (body is not null) yield return (method, body);
            }
            else if (node is LocalFunctionStatementSyntax localFunction)
            {
                var body = (SyntaxNode?)localFunction.Body ?? localFunction.ExpressionBody;
                if (body is not null) yield return (localFunction, body);
            }
        }
    }

    private static EligibleMethod? TryBuildEligible(
        (SyntaxNode Declaration, SyntaxNode Body) candidate, string filePath, SemanticModel semanticModel,
        DuplicateDetectionOptions options)
    {
        var symbol = semanticModel.GetDeclaredSymbol(candidate.Declaration) as IMethodSymbol;
        if (symbol is null || IsGenerated(symbol)) return null;

        var tokens = candidate.Body.DescendantTokens().ToList();
        if (tokens.Count < options.MinTokens) return null;

        var lineNumber = candidate.Declaration.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        return new EligibleMethod(
            filePath, lineNumber, symbol.ToDisplayString(), tokens.Count,
            candidate.Declaration, candidate.Body, symbol, semanticModel);
    }

    private static bool IsGenerated(IMethodSymbol symbol) =>
        HasGeneratedCodeAttribute(symbol) || (symbol.ContainingType is { } t && HasGeneratedCodeAttribute(t));

    private static bool HasGeneratedCodeAttribute(ISymbol symbol) =>
        symbol.GetAttributes().Any(a =>
            a.AttributeClass?.Name is "GeneratedCodeAttribute" or "GeneratedCode");
}
