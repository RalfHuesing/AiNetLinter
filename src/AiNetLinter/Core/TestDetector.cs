#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core;

/// <summary>
/// Zentraler Service zur Erkennung und Klassifizierung von Test-Artefakten (Projekte, Dateien, Klassen, Methoden, Kategorien).
/// Single Source of Truth für alle Test-Heuristiken im AiNetLinter.
/// </summary>
public static class TestDetector
{
    #region Patterns & Heuristics Constants

    private static readonly string[] TestPathSegments =
    [
        ".Tests/", ".UnitTests/", ".FastTests/", ".IntegrationTests/", ".ComponentTests/",
        ".TestKit/", ".Specs/", "/tests/", "/test/", "/specs/"
    ];

    private static readonly string[] TestFileSuffixes =
    [
        "Tests.cs", "Test.cs", "Spec.cs", "Specs.cs"
    ];

    private static readonly string[] TestPathPrefixes =
    [
        "tests/", "test/"
    ];

    private static readonly string[] TestKeywords =
    [
        "xunit", "nunit", "testplatform", "unittesting", "mstest",
        "fluentassertions", "shouldly", "moq", "nsubstitute", "bogus"
    ];

    private static readonly string[] DefaultTestProjectNameSuffixes =
    [
        "Tests", "Test", "UnitTests", "UnitTest", "IntegrationTests", "IntegrationTest",
        "FastTests", "ComponentTests", "TestKit", "Specs", "Spec"
    ];

    private static readonly HashSet<string> TestAttributeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Fact", "FactAttribute",
        "Theory", "TheoryAttribute",
        "Test", "TestAttribute",
        "TestMethod", "TestMethodAttribute",
        "TestCase", "TestCaseAttribute"
    };

    private static readonly string[] ClassNameAffixes =
    [
        "Tests", "Test", "Specs", "Spec", "IntegrationTests", "UnitTests", "ComponentTests", "FastTests"
    ];

    private static readonly string[] IntegrationPathMarkers =
    [
        "/Integration/", ".IntegrationTests/", "/IntegrationTests/", "/E2E/",
        "/EndToEnd/", "/Functional/", "/Performance/", "/Stress/"
    ];

    private static readonly string[] ComponentPathMarkers =
    [
        "/Component/", ".ComponentTests/", "/ComponentTests/"
    ];

    #endregion

    #region Project Detection

    /// <summary>
    /// Prüft, ob ein Roslyn-Projekt ein Testprojekt ist (anhand von Metadaten-Referenzen, Projektnamen oder Dateipfad).
    /// </summary>
    public static bool IsTestProject(Project project, IReadOnlyList<string>? testProjectNameSuffixes = null)
    {
        foreach (var reference in project.MetadataReferences)
        {
            if (IsTestReference(reference.Display))
            {
                return true;
            }
        }

        var suffixes = testProjectNameSuffixes ?? DefaultTestProjectNameSuffixes;
        if (HasTestProjectNameSuffix(project.Name, suffixes))
        {
            return true;
        }

        if (project.FilePath is { } path)
        {
            var fileName = Path.GetFileName(path);
            if (IsTestFile(fileName) || IsTestFile(path))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Prüft, ob ein Projekt entweder ein Testprojekt ist oder Testdateien enthält.
    /// </summary>
    public static bool IsTestProjectOrHasTestFiles(Project project, IReadOnlyList<string>? testProjectNameSuffixes = null)
    {
        if (IsTestProject(project, testProjectNameSuffixes)) return true;
        return project.Documents.Any(d => d.FilePath != null && IsTestFile(d.FilePath));
    }

    /// <summary>
    /// Findet alle Testprojekte einer Solution.
    /// </summary>
    public static IReadOnlyList<Project> FindTestProjects(Solution solution, IReadOnlyList<string>? testProjectNameSuffixes = null)
    {
        return solution.Projects
            .Where(p => IsTestProject(p, testProjectNameSuffixes))
            .ToList();
    }

    /// <summary>
    /// Findet das bevorzugte Testprojekt (Unit / Fast / Spec bevorzugt) einer Solution.
    /// </summary>
    public static Project? FindPreferredTestProject(Solution solution, IReadOnlyList<string>? testProjectNameSuffixes = null)
    {
        var testProjects = FindTestProjects(solution, testProjectNameSuffixes);
        return testProjects.FirstOrDefault(p =>
            p.Name.Contains("Unit", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Fast", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Spec", StringComparison.OrdinalIgnoreCase)) ?? testProjects.FirstOrDefault();
    }

    /// <summary>
    /// Ermittelt den relativen Verzeichnispfad eines Projekts zur Solution.
    /// </summary>
    public static string GetProjectDirectory(Project project, string solutionDir)
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

    private static bool HasTestProjectNameSuffix(string projectName, IReadOnlyList<string> suffixes)
    {
        foreach (var suffix in suffixes)
        {
            if (projectName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                || projectName.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase)
                || projectName.Contains("." + suffix + ".", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsTestReference(string? display)
    {
        if (string.IsNullOrEmpty(display)) return false;

        foreach (var keyword in TestKeywords)
        {
            if (display.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region File & Path Detection

    /// <summary>
    /// Prüft, ob ein Dateipfad (relativ oder absolut) eine Testdatei darstellt.
    /// </summary>
    public static bool IsTestFile(string relativeOrAbsoluteFilePath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsoluteFilePath)) return false;
        var normalized = PathNormalizer.NormalizeSeparators(relativeOrAbsoluteFilePath);

        if (TestPathPrefixes.Any(p => normalized.StartsWith(p, StringComparison.OrdinalIgnoreCase))) return true;
        if (TestFileSuffixes.Any(s => normalized.EndsWith(s, StringComparison.OrdinalIgnoreCase))) return true;
        return TestPathSegments.Any(seg => normalized.Contains(seg, StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Class & Method Detection

    /// <summary>
    /// Prüft, ob eine Methode ein Testattribut (xUnit, NUnit, MSTest) besitzt.
    /// </summary>
    public static bool IsTestMethod(MethodDeclarationSyntax method)
    {
        if (method.AttributeLists.Count == 0) return false;
        foreach (var attrList in method.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var simpleName = attr.Name.ToString().Split('.').Last();
                if (TestAttributeNames.Contains(simpleName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Prüft, ob ein Testklassenname dem Namensschema für ein Ziel-Symbol entspricht.
    /// </summary>
    public static bool MatchesTestClassName(string testClassName, string targetTypeName)
    {
        if (testClassName.Equals("Test" + targetTypeName, StringComparison.OrdinalIgnoreCase)) return true;
        return ClassNameAffixes.Any(affix =>
            testClassName.Equals(targetTypeName + affix, StringComparison.OrdinalIgnoreCase) ||
            testClassName.StartsWith(targetTypeName + affix, StringComparison.OrdinalIgnoreCase) ||
            testClassName.EndsWith(targetTypeName + affix, StringComparison.OrdinalIgnoreCase) ||
            testClassName.Contains(targetTypeName + affix, StringComparison.OrdinalIgnoreCase))
            || (testClassName.StartsWith(targetTypeName, StringComparison.OrdinalIgnoreCase)
                && ClassNameAffixes.Any(affix => testClassName.EndsWith(affix, StringComparison.OrdinalIgnoreCase)));
    }

    #endregion

    #region Category & Trait Detection

    /// <summary>
    /// Bestimmt die Testkategorie (Unit, Integration, Component) einer Testdatei anhand von Attributen und Pfadkonventionen.
    /// </summary>
    public static string DetermineCategory(SyntaxNode root, string relativePath)
    {
        var traitCategory = ExtractTraitCategory(root);
        if (!string.IsNullOrWhiteSpace(traitCategory))
        {
            return traitCategory;
        }

        return DeduceCategoryFromPath(relativePath);
    }

    /// <summary>
    /// Extrahiert die Test-Kategorie aus Attributen ([Trait("Category", ...)], [Category(...)], [TestCategory(...)]).
    /// </summary>
    public static string? ExtractTraitCategory(SyntaxNode root)
    {
        foreach (var attr in root.DescendantNodes().OfType<AttributeSyntax>())
        {
            var cat = ExtractCategoryFromAttribute(attr);
            if (!string.IsNullOrWhiteSpace(cat)) return cat;
        }

        return null;
    }

    /// <summary>
    /// Extrahiert die Test-Kategorie aus einem einzelnen Attribut.
    /// </summary>
    public static string? ExtractCategoryFromAttribute(AttributeSyntax attr)
    {
        if (attr.ArgumentList == null || attr.ArgumentList.Arguments.Count == 0) return null;
        var name = attr.Name.ToString();
        var args = attr.ArgumentList.Arguments;

        if (name.Contains("Trait", StringComparison.OrdinalIgnoreCase) && args.Count >= 2)
        {
            if (args[0].ToString().Contains("Category", StringComparison.OrdinalIgnoreCase))
            {
                return args[1].ToString().Trim('"', ' ');
            }
        }

        if (name.EndsWith("Category", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("CategoryAttribute", StringComparison.OrdinalIgnoreCase))
        {
            return args[0].ToString().Trim('"', ' ');
        }

        return null;
    }

    /// <summary>
    /// Leitet die Testkategorie aus dem relativen Pfad anhand standardisierter Pfadmarker ab.
    /// </summary>
    public static string DeduceCategoryFromPath(string relativePath)
    {
        var normalized = PathNormalizer.NormalizeSeparators(relativePath);

        if (IntegrationPathMarkers.Any(m => normalized.Contains(m, StringComparison.OrdinalIgnoreCase)))
        {
            return TestCategories.Integration;
        }

        if (ComponentPathMarkers.Any(m => normalized.Contains(m, StringComparison.OrdinalIgnoreCase)))
        {
            return TestCategories.Component;
        }

        return TestCategories.Unit;
    }

    #endregion
}
