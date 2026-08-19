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
    private static readonly HashSet<string> TestAttributeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Fact", "FactAttribute",
        "Theory", "TheoryAttribute",
        "Test", "TestAttribute",
        "TestMethod", "TestMethodAttribute",
        "TestCase", "TestCaseAttribute"
    };

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
            var isTestProject = IsTestProjectOrHasTestFiles(project);
            if (!isTestProject) continue;

            foreach (var document in project.Documents)
            {
                if (ct.IsCancellationRequested) break;
                if (document.FilePath is null) continue;

                var relativePath = PathNormalizer.ToRelative(solutionDir, document.FilePath);
                if (!PathNormalizer.IsTestFile(relativePath)) continue;

                var syntaxRoot = await document.GetSyntaxRootAsync(ct);
                if (syntaxRoot is null) continue;

                var semanticModel = await document.GetSemanticModelAsync(ct);
                if (semanticModel is null) continue;

                var (fileMatches, reason, matchingMethods, totalClassTests) = AnalyzeDocument(
                    syntaxRoot, semanticModel, targetSymbol, targetTypeName, targetMemberName, relativePath);

                if (fileMatches && matchingMethods.Count > 0)
                {
                    var category = DetermineCategory(syntaxRoot, relativePath);
                    results.Add(new TestFileCoverageResult(
                        FilePath: relativePath,
                        TestClassName: ExtractFirstTestClassName(syntaxRoot) ?? Path.GetFileNameWithoutExtension(relativePath),
                        Category: category,
                        MatchReason: reason,
                        TestMethods: matchingMethods,
                        TotalClassTests: totalClassTests
                    ));
                    totalMatchingTests += matchingMethods.Count;
                }
            }
        }

        var sorted = results
            .OrderBy(r => GetMatchReasonPriority(r.MatchReason))
            .ThenBy(r => r.FilePath)
            .ToList();

        return new TestCoverageScannerResult(totalMatchingTests, sorted);
    }

    private static bool IsTestProjectOrHasTestFiles(Project project)
    {
        if (project.Name.Contains("Test", StringComparison.OrdinalIgnoreCase)) return true;
        return project.Documents.Any(d => d.FilePath != null && PathNormalizer.IsTestFile(d.FilePath));
    }

    private static (bool Matched, string Reason, List<string> MatchingMethods, int TotalTests) AnalyzeDocument(
        SyntaxNode root,
        SemanticModel semanticModel,
        ISymbol targetSymbol,
        string targetTypeName,
        string? targetMemberName,
        string relativePath)
    {
        var testMethods = new List<(MethodDeclarationSyntax Syntax, string Name, bool IsDirectMatch)>();
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
        var coversComments = ExtractCoversComments(root);

        var hasClassLevelCovers = coversComments.Any(c => string.Equals(c, targetTypeName, StringComparison.OrdinalIgnoreCase));
        var classNameMatches = classDeclarations.Any(c => MatchesClassName(c.Identifier.Text, targetTypeName));
        var hasClassLevelTypeof = false;

        foreach (var typeOf in root.DescendantNodes().OfType<TypeOfExpressionSyntax>())
        {
            var symbol = semanticModel.GetSymbolInfo(typeOf.Type).Symbol;
            if (symbol != null && (symbol.Name == targetTypeName || SymbolEqualityComparer.Default.Equals(symbol, targetSymbol.ContainingType ?? targetSymbol)))
            {
                hasClassLevelTypeof = true;
                break;
            }

            var typeText = typeOf.Type.ToString();
            if (typeText == targetTypeName || typeText.EndsWith("." + targetTypeName, StringComparison.Ordinal))
            {
                hasClassLevelTypeof = true;
                break;
            }
        }

        if (!hasClassLevelTypeof)
        {
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is IdentifierNameSyntax { Identifier.Text: "nameof" } &&
                    invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is { } argExpr)
                {
                    var argText = argExpr.ToString();
                    if (argText == targetTypeName || argText.EndsWith("." + targetTypeName, StringComparison.Ordinal))
                    {
                        hasClassLevelTypeof = true;
                        break;
                    }
                }
            }
        }

        var allMethodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
        foreach (var method in allMethodDeclarations)
        {
            if (!IsTestMethod(method)) continue;

            var methodName = method.Identifier.Text;
            var isMethodMatch = false;

            if (targetMemberName != null)
            {
                if (methodName.StartsWith(targetMemberName + "_", StringComparison.OrdinalIgnoreCase) ||
                    methodName.Contains(targetMemberName, StringComparison.OrdinalIgnoreCase))
                {
                    isMethodMatch = true;
                }
                else if (CallsTargetSymbol(method, targetSymbol, targetMemberName, semanticModel))
                {
                    isMethodMatch = true;
                }
            }

            testMethods.Add((method, methodName, isMethodMatch));
        }

        if (testMethods.Count == 0)
        {
            return (false, string.Empty, [], 0);
        }

        var matchingMethodNames = new List<string>();
        string reason;

        if (targetMemberName != null)
        {
            var directMatches = testMethods.Where(m => m.IsDirectMatch).Select(m => m.Name).ToList();
            if (directMatches.Count > 0)
            {
                matchingMethodNames = directMatches;
                reason = "Direct Member Match / Invocation";
            }
            else if (classNameMatches)
            {
                matchingMethodNames = testMethods.Select(m => m.Name).ToList();
                reason = "Naming Convention Match";
            }
            else if (hasClassLevelCovers)
            {
                matchingMethodNames = testMethods.Select(m => m.Name).ToList();
                reason = "Explicit @covers Comment";
            }
            else if (hasClassLevelTypeof)
            {
                matchingMethodNames = testMethods.Select(m => m.Name).ToList();
                reason = "Direct typeof Reference";
            }
            else
            {
                return (false, string.Empty, [], testMethods.Count);
            }
        }
        else
        {
            if (classNameMatches)
            {
                matchingMethodNames = testMethods.Select(m => m.Name).ToList();
                reason = "Naming Convention Match";
            }
            else if (hasClassLevelCovers)
            {
                matchingMethodNames = testMethods.Select(m => m.Name).ToList();
                reason = "Explicit @covers Comment";
            }
            else if (hasClassLevelTypeof)
            {
                matchingMethodNames = testMethods.Select(m => m.Name).ToList();
                reason = "Direct typeof Reference";
            }
            else
            {
                return (false, string.Empty, [], testMethods.Count);
            }
        }

        return (true, reason, matchingMethodNames, testMethods.Count);
    }

    private static bool MatchesClassName(string testClassName, string targetTypeName)
    {
        if (testClassName.Equals(targetTypeName + "Tests", StringComparison.OrdinalIgnoreCase) ||
            testClassName.Equals(targetTypeName + "Test", StringComparison.OrdinalIgnoreCase) ||
            testClassName.Equals("Test" + targetTypeName, StringComparison.OrdinalIgnoreCase) ||
            testClassName.StartsWith(targetTypeName + "Tests", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsTestMethod(MethodDeclarationSyntax method)
    {
        if (method.AttributeLists.Count == 0) return false;
        foreach (var attrList in method.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = attr.Name.ToString();
                var simpleName = name.Split('.').Last();
                if (TestAttributeNames.Contains(simpleName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool CallsTargetSymbol(
        MethodDeclarationSyntax testMethod,
        ISymbol targetSymbol,
        string targetMemberName,
        SemanticModel semanticModel)
    {
        foreach (var invocation in testMethod.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is IdentifierNameSyntax id && id.Identifier.Text == targetMemberName)
            {
                return true;
            }
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.Text == targetMemberName)
            {
                return true;
            }

            var symbol = semanticModel.GetSymbolInfo(invocation).Symbol;
            if (symbol != null && (SymbolEqualityComparer.Default.Equals(symbol, targetSymbol) ||
                                   symbol.Name == targetMemberName))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> ExtractCoversComments(SyntaxNode root)
    {
        var list = new List<string>();
        var text = root.GetText().ToString();
        foreach (Match match in CoversRegex().Matches(text))
        {
            list.Add(match.Groups[1].Value.Trim());
        }
        return list;
    }

    private static string? ExtractFirstTestClassName(SyntaxNode root)
    {
        return root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.Text;
    }

    private static string DetermineCategory(SyntaxNode root, string relativePath)
    {
        foreach (var attr in root.DescendantNodes().OfType<AttributeSyntax>())
        {
            var name = attr.Name.ToString();
            if (name.Contains("Trait", StringComparison.OrdinalIgnoreCase) && attr.ArgumentList != null)
            {
                var args = attr.ArgumentList.Arguments;
                if (args.Count >= 2 && args[0].ToString().Contains("Category", StringComparison.OrdinalIgnoreCase))
                {
                    var cat = args[1].ToString().Trim('"');
                    if (!string.IsNullOrWhiteSpace(cat)) return cat;
                }
            }
        }

        var normalized = PathNormalizer.NormalizeSeparators(relativePath);
        if (normalized.Contains(".FastTests/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/Unit/", StringComparison.OrdinalIgnoreCase))
        {
            return "Unit";
        }
        if (normalized.Contains(".IntegrationTests/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/Integration/", StringComparison.OrdinalIgnoreCase))
        {
            return "Integration";
        }

        return "Unit";
    }

    private static int GetMatchReasonPriority(string reason) => reason switch
    {
        "Direct Member Match / Invocation" => 1,
        "Naming Convention Match" => 2,
        "Explicit @covers Comment" => 3,
        "Direct typeof Reference" => 4,
        _ => 5
    };

    [GeneratedRegex(@"//\s*(?:@covers|covers)\s+([\w\.]+)", RegexOptions.CultureInvariant)]
    private static partial Regex CoversRegex();
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
    int TotalClassTests
);
