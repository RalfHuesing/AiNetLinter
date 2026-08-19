#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core;

/// <summary>
/// Scannt eine Roslyn-Solution nach Testdateien und Testmethoden, die ein bestimmtes
/// Ziel-Symbol abdecken (durch Namenskonventionen, [Fact]/[Theory]-Attribute,
/// typeof/nameof-Referenzen, @covers-Kommentare oder direkte Aufrufe).
/// Gemeinsamer residenter Kern fuer get_feature_context und get_test_context.
/// </summary>
public static partial class TestCoverageScanner
{
    /// <summary>
    /// Findet alle Testdateien und Testmethoden, die das angegebene Symbol abdecken.
    /// </summary>
    public static async Task<TestCoverageScannerResult> FindTestsForSymbolAsync(
        ISymbol targetSymbol,
        Solution solution,
        CancellationToken ct = default)
    {
        var targetType = targetSymbol is INamedTypeSymbol named ? named : targetSymbol.ContainingType;
        var targetTypeName = targetType?.Name ?? targetSymbol.Name;
        var targetMemberName = targetSymbol is not INamedTypeSymbol ? targetSymbol.Name : null;
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";

        var results = new List<TestFileCoverageResult>();
        var totalMatchingTests = 0;

        foreach (var project in solution.Projects)
        {
            if (ct.IsCancellationRequested) break;
            if (!TestDetector.IsTestProjectOrHasTestFiles(project)) continue;

            var projectResults = await ScanProjectDocumentsAsync(
                project, solutionDir, targetSymbol, targetTypeName, targetMemberName, ct);

            results.AddRange(projectResults);
            totalMatchingTests += projectResults.Sum(r => r.TestMethods.Count);
        }

        var sorted = results
            .OrderBy(r => GetMatchReasonPriority(r.MatchReason))
            .ThenBy(r => r.FilePath)
            .ToList();

        return new TestCoverageScannerResult(totalMatchingTests, sorted);
    }

    private static async Task<List<TestFileCoverageResult>> ScanProjectDocumentsAsync(
        Project project,
        string solutionDir,
        ISymbol targetSymbol,
        string targetTypeName,
        string? targetMemberName,
        CancellationToken ct)
    {
        var results = new List<TestFileCoverageResult>();

        foreach (var document in project.Documents)
        {
            if (ct.IsCancellationRequested) break;
            if (document.FilePath is null) continue;

            var relativePath = PathNormalizer.ToRelative(solutionDir, document.FilePath);
            if (!TestDetector.IsTestFile(relativePath)) continue;

            var result = await ProcessDocumentAsync(
                document, relativePath, targetSymbol, targetTypeName, targetMemberName, ct);

            if (result != null)
            {
                results.Add(result);
            }
        }

        return results;
    }

    private static async Task<TestFileCoverageResult?> ProcessDocumentAsync(
        Document document,
        string relativePath,
        ISymbol targetSymbol,
        string targetTypeName,
        string? targetMemberName,
        CancellationToken ct)
    {
        var syntaxRoot = await document.GetSyntaxRootAsync(ct);
        if (syntaxRoot is null) return null;

        var semanticModel = await document.GetSemanticModelAsync(ct);
        if (semanticModel is null) return null;

        var (fileMatches, reason, matchingMethods, totalClassTests) = AnalyzeDocument(
            syntaxRoot, semanticModel, targetSymbol, targetTypeName, targetMemberName);

        if (!fileMatches || matchingMethods.Count == 0) return null;

        var category = TestDetector.DetermineCategory(syntaxRoot, relativePath);
        var className = ExtractFirstTestClassName(syntaxRoot) ?? Path.GetFileNameWithoutExtension(relativePath);
        var projectDir = TestDetector.GetProjectDirectory(
            document.Project,
            document.Project.Solution.FilePath is { } slnPath ? Path.GetDirectoryName(slnPath) ?? "" : "");

        return new TestFileCoverageResult(
            FilePath: relativePath,
            TestClassName: className,
            Category: category,
            MatchReason: reason,
            TestMethods: matchingMethods,
            TotalClassTests: totalClassTests,
            ProjectDirectory: projectDir
        );
    }

    private static (bool Matched, string Reason, List<string> MatchingMethods, int TotalTests) AnalyzeDocument(
        SyntaxNode root,
        SemanticModel semanticModel,
        ISymbol targetSymbol,
        string targetTypeName,
        string? targetMemberName)
    {
        var testMethods = FindTestMethods(root, targetSymbol, targetMemberName, semanticModel);
        if (testMethods.Count == 0)
        {
            return (false, string.Empty, [], 0);
        }

        var classNameMatches = MatchesAnyClassName(root, targetTypeName);
        var hasCovers = HasCoversComment(root, targetTypeName);
        var hasTypeof = HasTypeofReference(root, targetSymbol, targetTypeName, semanticModel);

        return SelectMatchingMethodsAndReason(
            testMethods, targetMemberName, classNameMatches, hasCovers, hasTypeof);
    }

    private static List<(string Name, bool IsDirectMatch)> FindTestMethods(
        SyntaxNode root,
        ISymbol targetSymbol,
        string? targetMemberName,
        SemanticModel semanticModel)
    {
        var list = new List<(string Name, bool IsDirectMatch)>();
        var allMethods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in allMethods)
        {
            if (!TestDetector.IsTestMethod(method)) continue;

            var methodName = method.Identifier.Text;
            var isMethodMatch = targetMemberName != null &&
                (IsNamedAfterMember(methodName, targetMemberName) ||
                 CallsTargetSymbol(method, targetSymbol, targetMemberName, semanticModel));

            list.Add((methodName, isMethodMatch));
        }

        return list;
    }

    private static bool IsNamedAfterMember(string testMethodName, string targetMemberName)
    {
        return testMethodName.StartsWith(targetMemberName + "_", StringComparison.OrdinalIgnoreCase) ||
               testMethodName.Contains(targetMemberName, StringComparison.OrdinalIgnoreCase);
    }

    private static (bool Matched, string Reason, List<string> MatchingMethods, int TotalTests) SelectMatchingMethodsAndReason(
        List<(string Name, bool IsDirectMatch)> testMethods,
        string? targetMemberName,
        bool classNameMatches,
        bool hasCovers,
        bool hasTypeof)
    {
        if (targetMemberName != null)
        {
            var directMatches = testMethods.Where(m => m.IsDirectMatch).Select(m => m.Name).ToList();
            if (directMatches.Count > 0)
            {
                return (true, TestCoverageMatchReasons.DirectMemberMatch, directMatches, testMethods.Count);
            }
        }

        var allNames = testMethods.Select(m => m.Name).ToList();
        if (classNameMatches)
        {
            return (true, TestCoverageMatchReasons.NamingConventionMatch, allNames, testMethods.Count);
        }
        if (hasCovers)
        {
            return (true, TestCoverageMatchReasons.ExplicitCoversComment, allNames, testMethods.Count);
        }
        if (hasTypeof)
        {
            return (true, TestCoverageMatchReasons.DirectTypeofReference, allNames, testMethods.Count);
        }

        return (false, string.Empty, [], testMethods.Count);
    }

    private static bool MatchesAnyClassName(SyntaxNode root, string targetTypeName)
    {
        return root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Any(c => TestDetector.MatchesTestClassName(c.Identifier.Text, targetTypeName));
    }

    private static bool HasCoversComment(SyntaxNode root, string targetTypeName)
    {
        return ExtractCoversComments(root).Any(c => string.Equals(c, targetTypeName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasTypeofReference(
        SyntaxNode root,
        ISymbol targetSymbol,
        string targetTypeName,
        SemanticModel semanticModel)
    {
        foreach (var typeOf in root.DescendantNodes().OfType<TypeOfExpressionSyntax>())
        {
            var symbol = semanticModel.GetSymbolInfo(typeOf.Type).Symbol;
            var targetContainingType = targetSymbol.ContainingType ?? targetSymbol;
            if (symbol != null && (symbol.Name == targetTypeName || SymbolEqualityComparer.Default.Equals(symbol, targetContainingType)))
            {
                return true;
            }

            var typeText = typeOf.Type.ToString();
            if (typeText == targetTypeName || typeText.EndsWith("." + targetTypeName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return HasNameofReference(root, targetTypeName);
    }

    private static bool HasNameofReference(SyntaxNode root, string targetTypeName)
    {
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is IdentifierNameSyntax { Identifier.Text: "nameof" } &&
                invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is { } argExpr)
            {
                var argText = argExpr.ToString();
                if (argText == targetTypeName || argText.EndsWith("." + targetTypeName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool CallsTargetSymbol(
        MethodDeclarationSyntax method,
        ISymbol targetSymbol,
        string targetMemberName,
        SemanticModel semanticModel)
    {
        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var symbol = semanticModel.GetSymbolInfo(invocation).Symbol;
            if (symbol != null && SymbolEqualityComparer.Default.Equals(symbol, targetSymbol))
            {
                return true;
            }

            var text = invocation.Expression.ToString();
            if (text.EndsWith("." + targetMemberName, StringComparison.Ordinal) ||
                text.Equals(targetMemberName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> ExtractCoversComments(SyntaxNode root)
    {
        var list = new List<string>();
        foreach (var trivia in root.DescendantTrivia())
        {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                var match = CoversRegex().Match(trivia.ToString());
                if (match.Success)
                {
                    list.Add(match.Groups[1].Value.Trim());
                }
            }
        }
        return list;
    }

    private static string? ExtractFirstTestClassName(SyntaxNode root)
    {
        return root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.DescendantNodes().OfType<MethodDeclarationSyntax>().Any(TestDetector.IsTestMethod))
            ?.Identifier.Text;
    }

    private static int GetMatchReasonPriority(string reason) => reason switch
    {
        TestCoverageMatchReasons.DirectMemberMatch => 1,
        TestCoverageMatchReasons.NamingConventionMatch => 2,
        TestCoverageMatchReasons.ExplicitCoversComment => 3,
        TestCoverageMatchReasons.DirectTypeofReference => 4,
        _ => 5
    };

    [GeneratedRegex(@"//\s*(?:@covers|covers)\s+([\w\.]+)", RegexOptions.CultureInvariant)]
    private static partial Regex CoversRegex();
}

/// <summary>
/// Einheitliche Konstanten fuer Zuordnungsgruende von Testabdeckungen.
/// </summary>
public static class TestCoverageMatchReasons
{
    public const string DirectMemberMatch = "Direct Member Match / Invocation";
    public const string NamingConventionMatch = "Naming Convention Match";
    public const string ExplicitCoversComment = "Explicit @covers Comment";
    public const string DirectTypeofReference = "Direct typeof Reference";
}

/// <summary>
/// Einheitliche Konstanten fuer Test-Kategorien.
/// </summary>
public static class TestCategories
{
    public const string Unit = "Unit";
    public const string Integration = "Integration";
    public const string Component = "Component";
}

/// <summary>
/// Ergebnis des TestCoverageScanner-Laufs.
/// </summary>
public sealed record TestCoverageScannerResult(
    int TotalMatchingTests,
    IReadOnlyList<TestFileCoverageResult> TestFiles
);

/// <summary>
/// Zugeordnete Testdatei mit Details.
/// </summary>
public sealed record TestFileCoverageResult(
    string FilePath,
    string TestClassName,
    string Category,
    string MatchReason,
    IReadOnlyList<string> TestMethods,
    int TotalClassTests,
    string? ProjectDirectory = null
);
