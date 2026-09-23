#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AiNetLinter.Mcp.Tools.Verify.DeadCode;

/// <summary>
/// Statische Scan-Pipeline fuer das Auffinden von unreferenziertem/totem Code in einer Roslyn-Solution.
/// </summary>
internal static partial class DeadCodeAdvisoryScanner
{
    public const int DefaultMaxResults = 50;

    /// <summary>
    /// Fuehrt den Dead-Code-Scan gemaess der uebergebenen Parameter ueber die Solution aus.
    /// </summary>
    public static async Task<DeadCodeScanResult> ScanAsync(
        Solution solution,
        DeadCodeAdvisoryOptions args,
        CancellationToken ct = default)
    {
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        var candidateDocuments = CollectCandidateDocuments(solution, solutionDir, args);
        var context = new DeadCodeScanContext(solution, solutionDir, args, candidateDocuments.Count);

        foreach (var projectGroup in candidateDocuments.GroupBy(d => d.Project))
        {
            if (ct.IsCancellationRequested) break;
            await ScanProjectAsync(projectGroup.Key, projectGroup, context, ct);
        }

        return BuildScanResult(context);
    }

    private static async Task ScanProjectAsync(
        Project project,
        IEnumerable<Document> documents,
        DeadCodeScanContext context,
        CancellationToken ct)
    {
        var compilation = await project.GetCompilationAsync(ct);
        if (compilation is null) return;

        context.RazorEvidenceIndex = await RazorGeneratedEvidenceIndex.CreateAsync(project, ct);

        var entryPoint = compilation.GetEntryPoint(ct);
        var hasInternalsVisibleTo = CheckInternalsVisibleTo(compilation.Assembly);

        if (context.Args.Mode is DeadCodeMode.Members or DeadCodeMode.Both)
        {
            foreach (var document in documents)
            {
                if (ct.IsCancellationRequested) break;
                await ScanDocumentAsync(document, entryPoint, hasInternalsVisibleTo, context, ct);
            }
        }

        if (context.Args.Mode is DeadCodeMode.Locals or DeadCodeMode.Both)
        {
            await DeadCodeAdvisoryDiagnosticsScanner.ScanProjectDiagnosticsAsync(documents, compilation, context, ct);
        }
    }

    private static async Task ScanDocumentAsync(
        Document document,
        IMethodSymbol? entryPoint,
        bool hasInternalsVisibleTo,
        DeadCodeScanContext context,
        CancellationToken ct)
    {
        var semanticModel = await document.GetSemanticModelAsync(ct);
        var syntaxRoot = await document.GetSyntaxRootAsync(ct);
        if (semanticModel is null || syntaxRoot is null) return;

        var declaredTypeNodes = syntaxRoot.DescendantNodes().Where(n => n is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax);
        foreach (var typeNode in declaredTypeNodes)
        {
            await ProcessTypeNodeAsync(typeNode, semanticModel, document, entryPoint, hasInternalsVisibleTo, context, ct);
        }
    }

    private static async Task ProcessTypeNodeAsync(
        SyntaxNode typeNode,
        SemanticModel semanticModel,
        Document document,
        IMethodSymbol? entryPoint,
        bool hasInternalsVisibleTo,
        DeadCodeScanContext context,
        CancellationToken ct)
    {
        if (semanticModel.GetDeclaredSymbol(typeNode, ct) is not INamedTypeSymbol typeSymbol) return;
        context.ScannedCount++;

        if (typeSymbol.ContainingType != null && context.DeadContainerTypes.Contains(typeSymbol.ContainingType))
        {
            return;
        }

        var isAlive = await EvaluateTypeLivenessAsync(typeSymbol, document, entryPoint, hasInternalsVisibleTo, context, ct);
        if (isAlive)
        {
            await ScanTypeMembersAsync(typeSymbol, document, typeNode, entryPoint, hasInternalsVisibleTo, context, ct);
        }
    }

    private static async Task<bool> EvaluateTypeLivenessAsync(
        INamedTypeSymbol typeSymbol,
        Document document,
        IMethodSymbol? entryPoint,
        bool hasInternalsVisibleTo,
        DeadCodeScanContext context,
        CancellationToken ct)
    {
        if (!context.ScannedTypes.Add(typeSymbol))
        {
            return !context.DeadContainerTypes.Contains(typeSymbol);
        }

        var isDead = await CheckAndRecordTypeAsync(typeSymbol, document, entryPoint, hasInternalsVisibleTo, context, ct);
        return !isDead;
    }

    private static async Task<bool> CheckAndRecordTypeAsync(
        INamedTypeSymbol typeSymbol,
        Document document,
        IMethodSymbol? entryPoint,
        bool hasInternalsVisibleTo,
        DeadCodeScanContext context,
        CancellationToken ct)
    {
        if (!ShouldCheckSymbol(typeSymbol, context.Args)
            || DeadCodeWhitelist.IsWhitelisted(typeSymbol, entryPoint)
            || DeadCodeSuppression.IsSuppressed(typeSymbol))
        {
            return false;
        }

        var referenceAnalysis = await AnalyzeReferencesAsync(typeSymbol, context.Solution, ct);
        if (referenceAnalysis.IsUndecidable)
        {
            context.UndecidableCount++;
            return false;
        }

        var isDead = referenceAnalysis.IsDeadCandidate;
        if (!isDead) return false;

        if (typeSymbol.IsStatic)
        {
            foreach (var member in typeSymbol.GetMembers())
            {
                if (member.IsImplicitlyDeclared) continue;
                var memberIsDead = (await AnalyzeReferencesAsync(member, context.Solution, ct)).IsDeadCandidate;
                if (!memberIsDead)
                {
                    return false;
                }
            }
        }

        if (typeSymbol.DeclaredAccessibility == Accessibility.Private)
        {
            context.DeadContainerTypes.Add(typeSymbol);
            AddDeadSymbol(context, typeSymbol, document, hasInternalsVisibleTo, referenceAnalysis);
            return true;
        }

        AddDeadSymbol(context, typeSymbol, document, hasInternalsVisibleTo, referenceAnalysis);
        return false;
    }

    private static async Task ScanTypeMembersAsync(
        INamedTypeSymbol typeSymbol,
        Document document,
        SyntaxNode typeNode,
        IMethodSymbol? entryPoint,
        bool hasInternalsVisibleTo,
        DeadCodeScanContext context,
        CancellationToken ct)
    {
        foreach (var member in typeSymbol.GetMembers())
        {
            await ProcessMemberAsync(member, document, typeNode, entryPoint, hasInternalsVisibleTo, context, ct);
        }
    }

    private static async Task ProcessMemberAsync(
        ISymbol member,
        Document document,
        SyntaxNode typeNode,
        IMethodSymbol? entryPoint,
        bool hasInternalsVisibleTo,
        DeadCodeScanContext context,
        CancellationToken ct)
    {
        if (member.IsImplicitlyDeclared) return;
        if (!member.DeclaringSyntaxReferences.Any(r => typeNode.Span.Contains(r.Span))) return;
        if (DeadCodeWhitelist.IsWhitelisted(member, entryPoint)) return;
        if (DeadCodeSuppression.IsSuppressed(member)) return;
        if (!ShouldCheckMemberKind(member, context.Args.Kind)) return;
        if (!MatchesAccessibilityFilter(member.DeclaredAccessibility, context.Args.Accessibility)) return;

        var referenceAnalysis = await AnalyzeReferencesAsync(member, context.Solution, ct);
        if (referenceAnalysis.IsUndecidable)
        {
            context.UndecidableCount++;
            return;
        }

        if (referenceAnalysis.IsDeadCandidate)
        {
            AddDeadSymbol(context, member, document, hasInternalsVisibleTo, referenceAnalysis);
        }
    }

    private static async Task<SymbolReferenceAnalysis> AnalyzeReferencesAsync(
        ISymbol symbol,
        Solution solution,
        CancellationToken ct)
    {
        var referenceSymbols = GetRelatedReferenceSymbols(symbol).ToArray();
        var seenLocations = new HashSet<(DocumentId DocumentId, Microsoft.CodeAnalysis.Text.TextSpan Span)>();
        var hasProductionReference = false;
        var hasUnknownReference = false;
        var testReferenceCount = 0;

        foreach (var referenceSymbol in referenceSymbols)
        {
            var references = await SymbolFinder.FindReferencesAsync(referenceSymbol, solution, ct);
            var declarations = referenceSymbol.DeclaringSyntaxReferences
                .Select(reference => (reference.SyntaxTree, reference.Span))
                .ToHashSet();

            foreach (var location in references.SelectMany(reference => reference.Locations))
            {
                if (location.Location.SourceTree is { } sourceTree
                    && declarations.Contains((sourceTree, location.Location.SourceSpan)))
                {
                    continue;
                }

                if (location.Document is { } referenceDocument
                    && !seenLocations.Add((referenceDocument.Id, location.Location.SourceSpan)))
                {
                    continue;
                }

                switch (ClassifyReferenceRole(location.Document))
                {
                    case ReferenceRole.Production:
                        hasProductionReference = true;
                        break;
                    case ReferenceRole.Test:
                        testReferenceCount++;
                        break;
                    default:
                        hasUnknownReference = true;
                        break;
                }
            }
        }

        return new SymbolReferenceAnalysis(
            HasProductionReference: hasProductionReference,
            HasUnknownReference: hasUnknownReference,
            TestReferenceCount: testReferenceCount);
    }

    private static IEnumerable<ISymbol> GetRelatedReferenceSymbols(ISymbol symbol)
    {
        yield return symbol;

        if (symbol is IMethodSymbol method)
        {
            for (var overriddenMethod = method.OverriddenMethod; overriddenMethod is not null; overriddenMethod = overriddenMethod.OverriddenMethod)
            {
                yield return overriddenMethod;
            }
        }
        else if (symbol is IPropertySymbol property)
        {
            for (var overriddenProperty = property.OverriddenProperty; overriddenProperty is not null; overriddenProperty = overriddenProperty.OverriddenProperty)
            {
                yield return overriddenProperty;
            }
        }

        foreach (var interfaceMember in GetImplementedInterfaceMembers(symbol))
        {
            yield return interfaceMember;
        }
    }

    private static ReferenceRole ClassifyReferenceRole(Document? document)
    {
        if (document?.Project is null || !document.Project.SupportsCompilation || document.FilePath is null)
        {
            return ReferenceRole.Unknown;
        }

        if (TestDetector.IsTestProject(document.Project) || TestDetector.IsTestFile(document.FilePath))
        {
            return ReferenceRole.Test;
        }

        return ReferenceRole.Production;
    }

    private enum ReferenceRole { Production, Test, Unknown }

    private readonly record struct SymbolReferenceAnalysis(bool HasProductionReference, bool HasUnknownReference, int TestReferenceCount)
    {
        public bool IsUndecidable => !HasProductionReference && HasUnknownReference;
        public bool IsDeadCandidate => !HasProductionReference && !HasUnknownReference;
    }

    private static IEnumerable<ISymbol> GetImplementedInterfaceMembers(ISymbol symbol)
    {
        if (symbol.ContainingType is null) return [];
        if (symbol is IMethodSymbol method) return GetImplementedInterfaceMembers(method, method.ExplicitInterfaceImplementations, symbol.ContainingType);
        if (symbol is IPropertySymbol prop) return GetImplementedInterfaceMembers(prop, prop.ExplicitInterfaceImplementations, symbol.ContainingType);
        return [];
    }

    private static IEnumerable<ISymbol> GetImplementedInterfaceMembers<TSymbol>(
        TSymbol member,
        System.Collections.Immutable.ImmutableArray<TSymbol> explicitImplementations,
        INamedTypeSymbol containingType)
        where TSymbol : class, ISymbol
    {
        foreach (var explicitImpl in explicitImplementations)
        {
            yield return explicitImpl;
        }

        foreach (var iface in containingType.AllInterfaces)
        {
            foreach (var ifaceMember in iface.GetMembers().OfType<TSymbol>())
            {
                if (SymbolEqualityComparer.Default.Equals(containingType.FindImplementationForInterfaceMember(ifaceMember), member))
                {
                    yield return ifaceMember;
                }
            }
        }
    }

    private static string ClassifyConfidence(ISymbol symbol, bool hasInternalsVisibleTo)
    {
        if (symbol.DeclaredAccessibility == Accessibility.Private) return "high";
        if (symbol.DeclaredAccessibility == Accessibility.Internal && !hasInternalsVisibleTo) return "high";
        return "low";
    }

    private static IReadOnlyList<string> DetermineLimitsApplies(ISymbol symbol, bool hasInternalsVisibleTo)
    {
        var limits = new List<string>();

        if (symbol.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected)
        {
            limits.Add("publicApiSurface");
            limits.Add("reflection");
        }

        if (hasInternalsVisibleTo && symbol.DeclaredAccessibility == Accessibility.Internal)
        {
            limits.Add("internalsVisibleTo");
        }

        if (symbol is IPropertySymbol)
        {
            limits.Add("jsonSerializer");
            limits.Add("optionsBinding");
        }

        if (GetImplementedInterfaceMembers(symbol).Any())
        {
            limits.Add("interfaceImplementation");
        }

        return limits;
    }

    private static bool CheckInternalsVisibleTo(IAssemblySymbol assembly)
    {
        return assembly.GetAttributes().Any(a =>
            a.AttributeClass?.Name.Equals("InternalsVisibleToAttribute", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static List<Document> CollectCandidateDocuments(Solution solution, string solutionDir, DeadCodeAdvisoryOptions args)
    {
        var docs = new List<Document>();
        foreach (var project in solution.Projects)
        {
            if (!ShouldScanProject(project, args)) continue;
            AddCandidateDocuments(project, solutionDir, args, docs);
        }
        return docs;
    }

    private static bool ShouldScanProject(Project project, DeadCodeAdvisoryOptions args)
    {
        return project.SupportsCompilation && (args.IncludeTests || !TestDetector.IsTestProject(project));
    }

    private static void AddCandidateDocuments(
        Project project,
        string solutionDir,
        DeadCodeAdvisoryOptions args,
        List<Document> documents)
    {
        foreach (var document in project.Documents)
        {
            if (IsCandidateDocument(document, project.Name, solutionDir, args)) documents.Add(document);
        }
    }

    private static bool IsCandidateDocument(
        Document document,
        string projectName,
        string solutionDir,
        DeadCodeAdvisoryOptions args)
    {
        if (!SourceFileCatalog.IsValidDocument(document, solutionDir)) return false;
        if (args.ScopeFiles is not null
            && (document.FilePath is null || !args.ScopeFiles.Contains(Path.GetFullPath(document.FilePath)))) return false;
        return ViolationScopeFilter.MatchesScope(document.FilePath ?? "", projectName, solutionDir, args.ScopeFilter);
    }

    private static bool ShouldCheckSymbol(INamedTypeSymbol symbol, DeadCodeAdvisoryOptions args) =>
        DeadCodeFilters.ShouldCheckSymbol(symbol, args);

    private static bool ShouldCheckMemberKind(ISymbol member, DeadCodeKindFilter kindFilter) =>
        DeadCodeFilters.ShouldCheckMemberKind(member, kindFilter);

    private static bool MatchesAccessibilityFilter(Accessibility accessibility, DeadCodeAccessibilityFilter filter) =>
        DeadCodeFilters.MatchesAccessibilityFilter(accessibility, filter);

    internal static string GetSymbolKindString(ISymbol symbol) =>
        DeadCodeFilters.GetSymbolKindString(symbol);

    private static string GetAccessibilityString(Accessibility accessibility) =>
        DeadCodeFilters.GetAccessibilityString(accessibility);
}
