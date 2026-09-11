#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
    /// Duenner Wrapper auf die gebatchte Zuordnung (<see cref="FindTestsForSymbolsAsync"/>)
    /// mit genau einem Ziel — Signatur, Verhalten und Ergebnisform bleiben unveraendert.
    /// </summary>
    public static async Task<TestCoverageScannerResult> FindTestsForSymbolAsync(
        ISymbol targetSymbol,
        Solution solution,
        CancellationToken ct = default)
    {
        var batch = await FindTestsForSymbolsAsync([targetSymbol], solution, ct);
        var single = batch.Symbols[0];
        return new TestCoverageScannerResult(single.TotalMatchingTests, single.TestFiles);
    }

    /// <summary>Bereits geladener Testdokument-Kontext (Dokument, relativer Pfad, SyntaxRoot).</summary>
    private sealed record LoadedTestDocument(Document Document, string RelativePath, SyntaxNode Root);

    /// <summary>
    /// Gemeinsame Nachbearbeitung einer gematchten Testdatei (Kategorie, Klassenname,
    /// Projektverzeichnis) fuer den per-Symbol-Wrapper und den Batch-Durchlauf.
    /// </summary>
    private static TestFileCoverageResult BuildFileCoverageResult(
        LoadedTestDocument loadedDocument,
        string reason,
        List<string> matchingMethods,
        int matchingTestCount,
        int totalClassTests,
        IReadOnlyList<string> matchingClassNames)
    {
        var category = TestDetector.DetermineCategory(loadedDocument.Root, loadedDocument.RelativePath);
        var className = matchingClassNames.FirstOrDefault()
            ?? ExtractFirstTestClassName(loadedDocument.Root)
            ?? Path.GetFileNameWithoutExtension(loadedDocument.RelativePath);
        var projectDir = TestDetector.GetProjectDirectory(
            loadedDocument.Document.Project,
            loadedDocument.Document.Project.Solution.FilePath is { } slnPath ? Path.GetDirectoryName(slnPath) ?? "" : "");

        return new TestFileCoverageResult(
            FilePath: loadedDocument.RelativePath,
            TestClassName: className,
            Category: category,
            MatchReason: reason,
            TestMethods: matchingMethods,
            TotalClassTests: totalClassTests,
            ProjectDirectory: projectDir,
            EvidenceKind: TestCoverageMatchReasons.ToEvidenceKind(reason),
            Confidence: TestEvidenceKindNames.ToConfidence(TestCoverageMatchReasons.ToEvidenceKind(reason)),
            MatchingTestCount: matchingTestCount,
            TestClassNames: matchingClassNames
        );
    }

    private static (bool Matched, string Reason, List<string> MatchingMethods, int MatchingTests, int TotalClassTests, IReadOnlyList<string> MatchingClassNames) AnalyzeDocument(
        SyntaxNode root,
        SemanticModel semanticModel,
        ISymbol targetSymbol,
        string targetTypeName,
        string? targetMemberName)
    {
        var testMethods = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(TestDetector.IsTestMethod)
            .ToList();
        if (testMethods.Count == 0)
        {
            return (false, string.Empty, [], 0, 0, []);
        }

        var classNameMatches = MatchesAnyClassName(root, targetTypeName);
        if (targetMemberName is not null)
        {
            return AnalyzeMemberEvidence(new MemberEvidenceInput(
                root, testMethods, targetSymbol, targetTypeName, targetMemberName, semanticModel, classNameMatches));
        }

        return AnalyzeTypeEvidence(testMethods, root, targetSymbol, targetTypeName, semanticModel, classNameMatches);
    }

    private static (bool Matched, string Reason, List<string> MatchingMethods, int MatchingTests, int TotalClassTests, IReadOnlyList<string> MatchingClassNames) AnalyzeMemberEvidence(
        MemberEvidenceInput input)
    {
        var direct = input.TestMethods
            .Where(method => CallsTargetSymbol(method, input.TargetSymbol, input.SemanticModel))
            .Select(method => method.Identifier.Text)
            .ToList();
        if (direct.Count > 0)
        {
            var matchingMethods = input.TestMethods
                .Where(method => CallsTargetSymbol(method, input.TargetSymbol, input.SemanticModel))
                .ToList();
            return CreateEvidence(TestEvidenceKind.DirectInvocation, direct, direct.Count, input.TestMethods.Count,
                matchingMethods, input.TestMethods);
        }

        var explicitMember = input.TestMethods
            .Where(method => HasMemberCoverageComment(method, input.TargetTypeName, input.TargetMemberName))
            .Select(method => method.Identifier.Text)
            .ToList();
        if (explicitMember.Count > 0)
        {
            var matchingMethods = input.TestMethods
                .Where(method => HasMemberCoverageComment(method, input.TargetTypeName, input.TargetMemberName))
                .ToList();
            return CreateEvidence(TestEvidenceKind.ExplicitMemberCoverage, explicitMember, explicitMember.Count, input.TestMethods.Count,
                matchingMethods, input.TestMethods);
        }

        var nameMatches = input.TestMethods
            .Where(method => IsNamedAfterMember(method.Identifier.Text, input.TargetMemberName))
            .Select(method => method.Identifier.Text)
            .ToList();
        if (nameMatches.Count > 0)
        {
            var matchingMethods = input.TestMethods
                .Where(method => IsNamedAfterMember(method.Identifier.Text, input.TargetMemberName))
                .ToList();
            return CreateEvidence(TestEvidenceKind.MemberNameMatch, nameMatches, nameMatches.Count,
                input.TestMethods.Count, matchingMethods, input.TestMethods);
        }

        if (HasMemberCoverageComment(input.Root, input.TargetTypeName, input.TargetMemberName))
        {
            return CreateEvidence(TestEvidenceKind.ExplicitMemberCoverage, [], input.TestMethods.Count, input.TestMethods.Count,
                ExtractTestClassNames(input.Root));
        }

        var matchingClassNames = ExtractMatchingTestClassNames(input.Root, input.TargetTypeName);
        var matchingClassTests = CountMatchingTestClassMethods(input.Root, input.TargetTypeName);
        return input.ClassNameMatches && matchingClassTests > 0
            ? CreateEvidence(TestEvidenceKind.TypeNamingConvention, [], matchingClassTests, matchingClassTests,
                matchingClassNames)
            : (false, string.Empty, [], 0, input.TestMethods.Count, []);
    }

    private static (bool Matched, string Reason, List<string> MatchingMethods, int MatchingTests, int TotalClassTests, IReadOnlyList<string> MatchingClassNames) AnalyzeTypeEvidence(
        IReadOnlyList<MethodDeclarationSyntax> testMethods,
        SyntaxNode root,
        ISymbol targetSymbol,
        string targetTypeName,
        SemanticModel semanticModel,
        bool classNameMatches)
    {
        var directTypeUseCount = testMethods.Count(method => CallsOrUsesTargetType(method, targetSymbol, semanticModel));
        if (directTypeUseCount > 0)
        {
            return CreateEvidence(TestEvidenceKind.DirectTypeUse, [], directTypeUseCount, testMethods.Count,
                testMethods.Where(method => CallsOrUsesTargetType(method, targetSymbol, semanticModel)));
        }

        if (HasExplicitTypeCoverage(root, targetSymbol, targetTypeName, semanticModel))
        {
            return CreateEvidence(TestEvidenceKind.ExplicitTypeCoverage, [], testMethods.Count, testMethods.Count,
                ExtractTestClassNames(root));
        }

        var matchingClassNames = ExtractMatchingTestClassNames(root, targetTypeName);
        var matchingClassTests = CountMatchingTestClassMethods(root, targetTypeName);
        return classNameMatches && matchingClassTests > 0
            ? CreateEvidence(TestEvidenceKind.TypeNamingConvention, [], matchingClassTests, matchingClassTests,
                matchingClassNames)
            : (false, string.Empty, [], 0, testMethods.Count, []);
    }

    private static (bool Matched, string Reason, List<string> MatchingMethods, int MatchingTests, int TotalClassTests, IReadOnlyList<string> MatchingClassNames) CreateEvidence(
        TestEvidenceKind kind,
        List<string> methods,
        int matchingTests,
        int totalClassTests,
        IEnumerable<MethodDeclarationSyntax> matchingMethods,
        IReadOnlyList<MethodDeclarationSyntax>? allTestMethods = null)
    {
        var matchingMethodList = matchingMethods.ToList();
        var scopedTotal = allTestMethods is null
            ? totalClassTests
            : CountTestsInContainingClasses(allTestMethods, matchingMethodList);
        return (true, TestCoverageMatchReasons.For(kind), methods, matchingTests, scopedTotal,
            matchingMethodList
                .Select(GetContainingTestClassName)
                .Where(name => name is not null)
                .Select(name => name!)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList());
    }

    private static int CountTestsInContainingClasses(
        IReadOnlyList<MethodDeclarationSyntax> allTestMethods,
        IReadOnlyList<MethodDeclarationSyntax> matchingMethods)
    {
        var matchingClasses = matchingMethods
            .Select(method => method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault())
            .Where(declaration => declaration is not null)
            .ToHashSet();
        return allTestMethods.Count(method =>
            method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault() is { } declaration
            && matchingClasses.Contains(declaration));
    }

    private static (bool Matched, string Reason, List<string> MatchingMethods, int MatchingTests, int TotalClassTests, IReadOnlyList<string> MatchingClassNames) CreateEvidence(
        TestEvidenceKind kind,
        List<string> methods,
        int matchingTests,
        int totalClassTests,
        IReadOnlyList<string> matchingClassNames)
    {
        return (true, TestCoverageMatchReasons.For(kind), methods, matchingTests, totalClassTests,
            matchingClassNames.Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToList());
    }

    private static bool CallsOrUsesTargetType(
        MethodDeclarationSyntax method,
        ISymbol targetSymbol,
        SemanticModel semanticModel)
    {
        var targetType = targetSymbol as INamedTypeSymbol ?? targetSymbol.ContainingType;
        if (targetType is null) return false;

        foreach (var node in method.DescendantNodes())
        {
            if (NodeUsesTargetType(node, targetType, semanticModel))
            {
                return true;
            }
        }

        return false;
    }

    private static bool NodeUsesTargetType(SyntaxNode node, INamedTypeSymbol targetType, SemanticModel semanticModel)
    {
        if (node is BaseObjectCreationExpressionSyntax creation)
        {
            return CreationUsesTargetType(creation, targetType, semanticModel);
        }

        var targetOriginal = targetType.OriginalDefinition ?? targetType;
        if (node is InvocationExpressionSyntax invocation)
        {
            var symbol = semanticModel.GetSymbolInfo(invocation).Symbol;
            return symbol != null && SymbolEqualityComparer.Default.Equals(
                symbol.ContainingType?.OriginalDefinition ?? symbol.ContainingType,
                targetOriginal);
        }

        if (node is MemberAccessExpressionSyntax memberAccess)
        {
            var symbol = semanticModel.GetSymbolInfo(memberAccess).Symbol;
            return symbol != null && SymbolEqualityComparer.Default.Equals(
                symbol.ContainingType?.OriginalDefinition ?? symbol.ContainingType,
                targetOriginal);
        }

        return false;
    }

    private static bool CreationUsesTargetType(
        BaseObjectCreationExpressionSyntax creation,
        INamedTypeSymbol targetType,
        SemanticModel semanticModel)
    {
        var targetOriginal = targetType.OriginalDefinition ?? targetType;
        var symbol = semanticModel.GetSymbolInfo(creation).Symbol;
        if (symbol is IMethodSymbol ctor && SymbolEqualityComparer.Default.Equals(
            ctor.ContainingType?.OriginalDefinition ?? ctor.ContainingType,
            targetOriginal))
        {
            return true;
        }

        var type = semanticModel.GetTypeInfo(creation).Type;
        return SymbolEqualityComparer.Default.Equals(
            type?.OriginalDefinition ?? type,
            targetOriginal);
    }

    private static bool IsNamedAfterMember(string testMethodName, string targetMemberName)
    {
        return testMethodName.StartsWith(targetMemberName + "_", StringComparison.OrdinalIgnoreCase) ||
               testMethodName.Contains(targetMemberName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesAnyClassName(SyntaxNode root, string targetTypeName)
    {
        return root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(HasTestMethods)
            .Any(c => TestDetector.MatchesTestClassName(c.Identifier.Text, targetTypeName));
    }

    private static IReadOnlyList<string> ExtractTestClassNames(SyntaxNode root) =>
        root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(HasTestMethods)
            .Select(c => c.Identifier.Text)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyList<string> ExtractMatchingTestClassNames(SyntaxNode root, string targetTypeName) =>
        root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(HasTestMethods)
            .Where(c => TestDetector.MatchesTestClassName(c.Identifier.Text, targetTypeName))
            .Select(c => c.Identifier.Text)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    private static int CountMatchingTestClassMethods(SyntaxNode root, string targetTypeName) =>
        root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(TestDetector.IsTestMethod)
            .Count(method => method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault() is { } declaration
                && TestDetector.MatchesTestClassName(declaration.Identifier.Text, targetTypeName));

    private static bool HasTestMethods(ClassDeclarationSyntax declaration) =>
        declaration.DescendantNodes().OfType<MethodDeclarationSyntax>().Any(TestDetector.IsTestMethod);

    private static string? GetContainingTestClassName(MethodDeclarationSyntax method) =>
        method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.Text;

    private static bool HasExplicitTypeCoverage(
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

        if (HasNameofReference(root, targetTypeName)) return true;
        return ExtractCoversComments(root).Any(c => MatchesTypeCoverage(c, targetTypeName));
    }

    private static bool HasMemberCoverageComment(SyntaxNode root, string targetTypeName, string targetMemberName) =>
        ExtractCoversComments(root).Any(c => MatchesMemberCoverage(c, targetTypeName, targetMemberName));

    private static bool MatchesMemberCoverage(string covered, string targetTypeName, string targetMemberName) =>
        covered.EndsWith("." + targetMemberName, StringComparison.OrdinalIgnoreCase)
        && covered.Contains(targetTypeName, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesTypeCoverage(string covered, string targetTypeName) =>
        string.Equals(covered, targetTypeName, StringComparison.OrdinalIgnoreCase)
        || covered.EndsWith("." + targetTypeName, StringComparison.OrdinalIgnoreCase);

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
        SemanticModel semanticModel)
    {
        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var symbol = semanticModel.GetSymbolInfo(invocation).Symbol;
            if (symbol != null && SymbolEqualityComparer.Default.Equals(symbol, targetSymbol))
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
        TestCoverageMatchReasons.DirectMemberMatch => 0,
        TestCoverageMatchReasons.ExplicitMemberCoverage => 1,
        TestCoverageMatchReasons.MemberNameMatch => 2,
        TestCoverageMatchReasons.DirectTypeUsage => 3,
        TestCoverageMatchReasons.ExplicitCoversComment => 4,
        TestCoverageMatchReasons.DirectTypeofReference => 4,
        TestCoverageMatchReasons.NamingConventionMatch => 5,
        _ => 6
    };

    private sealed record MemberEvidenceInput(
        SyntaxNode Root,
        IReadOnlyList<MethodDeclarationSyntax> TestMethods,
        ISymbol TargetSymbol,
        string TargetTypeName,
        string TargetMemberName,
        SemanticModel SemanticModel,
        bool ClassNameMatches);

    [GeneratedRegex(@"//\s*(?:@covers|covers)\s+([\w\.]+)", RegexOptions.CultureInvariant)]
    private static partial Regex CoversRegex();
}

/// <summary>
/// Einheitliche Konstanten fuer Zuordnungsgruende von Testabdeckungen.
/// </summary>
public static class TestCoverageMatchReasons
{
    public const string DirectMemberMatch = "Direct Member Match / Invocation";
    public const string ExplicitMemberCoverage = "Explicit Member Coverage";
    public const string MemberNameMatch = "Member Name Match";
    public const string DirectTypeUsage = "Direct Type Usage / Invocation";
    public const string NamingConventionMatch = "Naming Convention Match";
    public const string ExplicitCoversComment = "Explicit @covers Comment";
    public const string DirectTypeofReference = "Direct typeof Reference";

    internal static string For(TestEvidenceKind kind) => kind switch
    {
        TestEvidenceKind.DirectInvocation => DirectMemberMatch,
        TestEvidenceKind.ExplicitMemberCoverage => ExplicitMemberCoverage,
        TestEvidenceKind.MemberNameMatch => MemberNameMatch,
        TestEvidenceKind.DirectTypeUse => DirectTypeUsage,
        TestEvidenceKind.ExplicitTypeCoverage => ExplicitCoversComment,
        TestEvidenceKind.TypeNamingConvention => NamingConventionMatch,
        _ => NamingConventionMatch,
    };

    internal static TestEvidenceKind ToEvidenceKind(string reason) => reason switch
    {
        DirectMemberMatch => TestEvidenceKind.DirectInvocation,
        ExplicitMemberCoverage => TestEvidenceKind.ExplicitMemberCoverage,
        MemberNameMatch => TestEvidenceKind.MemberNameMatch,
        DirectTypeUsage => TestEvidenceKind.DirectTypeUse,
        ExplicitCoversComment or DirectTypeofReference => TestEvidenceKind.ExplicitTypeCoverage,
        NamingConventionMatch => TestEvidenceKind.TypeNamingConvention,
        _ => TestEvidenceKind.TypeNamingConvention,
    };
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
    string? ProjectDirectory = null,
    TestEvidenceKind EvidenceKind = TestEvidenceKind.TypeNamingConvention,
    string Confidence = "low",
    int? MatchingTestCount = null,
    IReadOnlyList<string> TestClassNames = null!
);

/// <summary>
/// Ergebnis eines gebatchten Test-Zuordnungslaufs ueber alle Ziel-Symbole.
/// </summary>
/// <param name="Symbols">Je Ziel-Symbol (in Eingabereihenfolge) die zugeordneten Testdateien.</param>
/// <param name="DistinctTestFileCount">Solutionweite Dedup-Info: Anzahl verschiedener Testdateien
/// ueber alle Ziele hinweg (Pfadvergleich case-insensitive).</param>
/// <param name="DistinctTestFilePaths">Dieselben Pfade dedupliziert und deterministisch (ordinal) sortiert.</param>
public sealed record TestCoverageBatchScanResult(
    IReadOnlyList<TestCoverageBatchSymbolResult> Symbols,
    int DistinctTestFileCount,
    IReadOnlyList<string> DistinctTestFilePaths);

/// <summary>
/// Testzuordnung fuer ein einzelnes Ziel aus dem Batch-Durchlauf — feldgleich zur
/// per-Symbol-API (<see cref="TestCoverageScannerResult"/>), ergaenzt um die stabile Symbol-ID.
/// </summary>
public sealed record TestCoverageBatchSymbolResult(
    string SymbolId,
    int TotalMatchingTests,
    IReadOnlyList<TestFileCoverageResult> TestFiles);
