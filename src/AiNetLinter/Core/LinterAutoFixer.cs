#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;
using AiNetLinter.Output;

namespace AiNetLinter.Core;

/// <summary>
/// Behebt einfache Linter-Verstoesse automatisiert ueber Roslyn Syntax-Transformationen.
/// </summary>
internal sealed class LinterAutoFixer
{
    private const string SealedRule   = LinterRuleIds.EnforceSealedClasses;
    private const string ReadonlyRule  = "EnforceReadonlyFields";
    private const string NullableRule  = LinterRuleIds.EnforceNullableEnable;

    /// <summary>
    /// Fuehrt die automatische Korrektur fuer unterstuetzte Regeln auf der Solution aus.
    /// </summary>
    internal static async Task<(int FixedCount, Solution UpdatedSolution)> FixAsync(
        Solution solution,
        IReadOnlyCollection<RuleViolation> violations,
        FixOptions options,
        ILintConsole? console = null)
    {
        var baseTypes = await CollectBaseTypesAsync(solution);
        var context = new FixContext(baseTypes, options.Verbose, options.DryRun, console ?? ConsoleLintConsole.Instance);
        var currentSolution = solution;
        int fixedCount = 0;

        var violationsByFile = violations
            .Where(v => IsFixable(v.RuleName))
            .GroupBy(v => v.FilePath, StringComparer.OrdinalIgnoreCase);

        foreach (var group in violationsByFile)
        {
            var (updatedSolution, count) = await TryFixFileAsync(currentSolution, group.Key, group.ToList(), context);
            if (count > 0)
            {
                currentSolution = updatedSolution;
                fixedCount += count;
            }
        }

        return (fixedCount, currentSolution);
    }

    private static bool IsFixable(string? ruleName)
    {
        return ruleName == SealedRule ||
               ruleName == ReadonlyRule ||
               ruleName == NullableRule;
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

    private static async Task<HashSet<INamedTypeSymbol>> CollectBaseTypesAsync(Solution solution)
    {
        var baseTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation == null) continue;

            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = await syntaxTree.GetRootAsync();
                CollectBasesFromRoot(root, semanticModel, baseTypes);
            }
        }
        return baseTypes;
    }

    private static void CollectBasesFromRoot(SyntaxNode root, SemanticModel semanticModel, HashSet<INamedTypeSymbol> baseTypes)
    {
        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classDecls)
        {
            var symbol = semanticModel.GetDeclaredSymbol(classDecl);
            if (symbol?.BaseType != null)
            {
                baseTypes.Add(symbol.BaseType.OriginalDefinition);
            }
        }
    }

    private static async Task<(Solution Solution, int FixedCount)> TryFixFileAsync(
        Solution solution,
        string filePath,
        List<RuleViolation> fileViolations,
        FixContext context)
    {
        var document = FindDocumentByPath(solution, filePath);
        if (document?.FilePath == null) return (solution, 0);

        var (updatedDoc, fixedCount) = await FixDocumentAsync(document, fileViolations, context.BaseTypes);
        var oldText = await document.GetTextAsync();
        var newText = await updatedDoc.GetTextAsync();
        if (oldText.ToString() == newText.ToString()) return (solution, 0);

        if (context.DryRun)
        {
            context.Console.WriteLine($"[DRY-RUN]: Würde {fixedCount} Fix(es) anwenden auf: {document.Name}");
            return (solution, fixedCount);
        }

        await File.WriteAllTextAsync(document.FilePath, newText.ToString(), newText.Encoding ?? Encoding.UTF8);
        LogFixApplied(document.Name, context.Verbose, context.Console);

        return (updatedDoc.Project.Solution, fixedCount);
    }

    private static void LogFixApplied(string docName, bool verbose, ILintConsole console)
    {
        if (verbose)
        {
            console.WriteLine($"[INFO]: Automatischer Fix angewendet auf: {docName}");
        }
    }

    private static async Task<(Document Document, int FixedCount)> FixDocumentAsync(
        Document document,
        List<RuleViolation> fileViolations,
        HashSet<INamedTypeSymbol> baseTypes)
    {
        var semanticModel = await document.GetSemanticModelAsync();
        var root = await document.GetSyntaxRootAsync();
        if (semanticModel == null) return (document, 0);
        if (root == null) return (document, 0);

        var (newRoot, nullableCount) = ApplyNullableFix(root, fileViolations);
        var (newRootSealed, sealedCount) = ApplySealedFix(newRoot, semanticModel, baseTypes, fileViolations);
        var (finalRoot, readonlyCount) = ApplyReadonlyFix(newRootSealed, semanticModel, fileViolations);

        return (document.WithSyntaxRoot(finalRoot), nullableCount + sealedCount + readonlyCount);
    }

    private static (SyntaxNode Root, int Count) ApplyNullableFix(SyntaxNode root, List<RuleViolation> violations)
    {
        if (!violations.Any(v => v.RuleName == NullableRule))
        {
            return (root, 0);
        }

        var nullableRoot = ApplyNullableEnableFix(root);
        if (nullableRoot != root)
        {
            return (nullableRoot, 1);
        }

        return (root, 0);
    }

    private static (SyntaxNode Root, int Count) ApplySealedFix(
        SyntaxNode root, SemanticModel semanticModel, HashSet<INamedTypeSymbol> baseTypes, List<RuleViolation> violations)
    {
        if (violations.Any(v => v.RuleName == SealedRule))
        {
            return ApplySealedClassesFix(root, semanticModel, baseTypes);
        }
        return (root, 0);
    }

    private static (SyntaxNode Root, int Count) ApplyReadonlyFix(
        SyntaxNode root, SemanticModel semanticModel, List<RuleViolation> violations)
    {
        var readonlyViolations = violations.Where(v => v.RuleName == ReadonlyRule).ToList();
        if (readonlyViolations.Count > 0)
        {
            return ApplyReadonlyFieldsFix(root, semanticModel, readonlyViolations);
        }
        return (root, 0);
    }

    private static SyntaxNode ApplyNullableEnableFix(SyntaxNode root)
    {
        var firstToken = root.GetFirstToken(includeZeroWidth: true);
        var leadingTrivia = firstToken.LeadingTrivia;

        if (leadingTrivia.Any(t => t.IsKind(SyntaxKind.NullableDirectiveTrivia)))
        {
            return root;
        }

        var nullableTriviaList = SyntaxFactory.ParseLeadingTrivia("#nullable enable\r\n");
        var newLeadingTrivia = nullableTriviaList.AddRange(leadingTrivia);
        var newFirstToken = firstToken.WithLeadingTrivia(newLeadingTrivia);
        return root.ReplaceToken(firstToken, newFirstToken);
    }

    private static (SyntaxNode Root, int Count) ApplySealedClassesFix(
        SyntaxNode root,
        SemanticModel semanticModel,
        HashSet<INamedTypeSymbol> baseTypes)
    {
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Where(c => CanSealClass(c, semanticModel, baseTypes))
            .ToList();

        if (classes.Count == 0) return (root, 0);

        var newRoot = root.ReplaceNodes(classes, (oldNode, newNode) => AddSealedModifier(newNode));
        return (newRoot, classes.Count);
    }

    private static bool CanSealClass(ClassDeclarationSyntax classDecl, SemanticModel semanticModel, HashSet<INamedTypeSymbol> baseTypes)
    {
        var modifiers = classDecl.Modifiers;
        if (modifiers.Any(m => m.IsKind(SyntaxKind.SealedKeyword) || 
                               m.IsKind(SyntaxKind.StaticKeyword) || 
                               m.IsKind(SyntaxKind.AbstractKeyword)))
        {
            return false;
        }

        var symbol = semanticModel.GetDeclaredSymbol(classDecl);
        if (symbol == null) return false;

        return !baseTypes.Contains(symbol.OriginalDefinition);
    }

    private static ClassDeclarationSyntax AddSealedModifier(ClassDeclarationSyntax classDecl)
    {
        var sealedToken = SyntaxFactory.Token(
            SyntaxFactory.TriviaList(),
            SyntaxKind.SealedKeyword,
            SyntaxFactory.TriviaList(SyntaxFactory.Space));

        if (classDecl.Modifiers.Count == 0)
        {
            return classDecl.WithModifiers(SyntaxFactory.TokenList(sealedToken));
        }

        return classDecl.WithModifiers(classDecl.Modifiers.Add(sealedToken));
    }

    private static (SyntaxNode Root, int Count) ApplyReadonlyFieldsFix(
        SyntaxNode root,
        SemanticModel semanticModel,
        List<RuleViolation> violations)
    {
        var fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>()
            .Where(f => ShouldMakeFieldReadonly(f, semanticModel, violations))
            .ToList();

        if (fields.Count == 0) return (root, 0);

        var newRoot = root.ReplaceNodes(fields, (oldNode, newNode) => AddReadonlyModifier(newNode));
        var count = fields.Sum(f => CountViolatingVariables(f, semanticModel, violations));

        return (newRoot, count);
    }

    private static int CountViolatingVariables(FieldDeclarationSyntax field, SemanticModel semanticModel, List<RuleViolation> violations)
    {
        return field.Declaration.Variables.Count(v => IsViolatingField(v, semanticModel, violations));
    }

    private static bool ShouldMakeFieldReadonly(FieldDeclarationSyntax fieldDecl, SemanticModel semanticModel, List<RuleViolation> violations)
    {
        if (HasReadonlyOrConstModifier(fieldDecl))
        {
            return false;
        }

        return fieldDecl.Declaration.Variables.Any(v => IsViolatingField(v, semanticModel, violations));
    }

    private static bool HasReadonlyOrConstModifier(FieldDeclarationSyntax fieldDecl)
    {
        return fieldDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword) || m.IsKind(SyntaxKind.ConstKeyword));
    }

    private static bool IsViolatingField(VariableDeclaratorSyntax variable, SemanticModel semanticModel, List<RuleViolation> violations)
    {
        var symbol = semanticModel.GetDeclaredSymbol(variable);
        if (symbol == null) return false;

        return violations.Any(v => v.LineNumber == GetLineNumber(variable) && v.Details.Contains($"'{symbol.Name}'"));
    }

    private static FieldDeclarationSyntax AddReadonlyModifier(FieldDeclarationSyntax fieldDecl)
    {
        var readonlyToken = SyntaxFactory.Token(
            SyntaxFactory.TriviaList(),
            SyntaxKind.ReadOnlyKeyword,
            SyntaxFactory.TriviaList(SyntaxFactory.Space));

        return fieldDecl.WithModifiers(fieldDecl.Modifiers.Add(readonlyToken));
    }

    private static int GetLineNumber(SyntaxNode node)
    {
        return node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    }

    private sealed record FixContext(
        HashSet<INamedTypeSymbol> BaseTypes,
        bool Verbose,
        bool DryRun,
        ILintConsole Console);
}

/// <summary>
/// Optionen für <see cref="LinterAutoFixer.FixAsync"/>.
/// </summary>
internal sealed record FixOptions(bool Verbose, bool DryRun = false);
