#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
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

    internal static IReadOnlyList<DeadCodeApiSurfaceIssue> ValidateApiSurface(
        Solution solution,
        IReadOnlySet<string>? scopeFiles,
        Config config) => DeadCodeApiSurfacePolicy.FindUnconfiguredProjects(solution, scopeFiles, config);

    internal static List<Document> CollectCandidateDocumentsForPolicy(
        Solution solution,
        string solutionDir,
        DeadCodeAdvisoryOptions args) => CollectCandidateDocuments(solution, solutionDir, args);

    /// <summary>
    /// Fuehrt den Dead-Code-Scan gemaess der uebergebenen Parameter ueber die Solution aus.
    /// </summary>
    public static async Task<DeadCodeScanResult> ScanAsync(
        Solution solution,
        DeadCodeAdvisoryOptions args,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        var context = new DeadCodeScanContext(solution, solutionDir, args, 0);
        var config = args.Config?.DeadCode ?? new DeadCodeConfig();
        var seconds = args.SolutionBudget ? config.SolutionBudgetSeconds : config.VerifyBudgetSeconds;
        context.Progress.BudgetMilliseconds = Math.Max(0, (long)seconds) * 1000;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (seconds <= 0) budget.Cancel();
        else budget.CancelAfter(TimeSpan.FromSeconds(seconds));
        var candidateDocuments = CollectCandidateDocuments(solution, solutionDir, args);
        context.DocumentsInScope = candidateDocuments.Count;
        try
        {
            budget.Token.ThrowIfCancellationRequested();
            await DeadCodeChangeScope.ExpandAsync(context, budget.Token);
            candidateDocuments = CollectCandidateDocuments(solution, solutionDir, context.Args);
            context.DocumentsInScope = candidateDocuments.Count;
            context.UsageIndex = await DeadCodeUsageIndex.CreateAsync(solution, budget.Token, args.Config);
            new DeadCodeIndirectUsage(context.UsageIndex).Collect(budget.Token);
            await new DeadCodeMarkupUsage(context.UsageIndex).CollectAsync(solution, budget.Token, args.Config);
            context.Progress.ReferencesComplete = context.UsageIndex.CoverageGaps.Count == 0;
            if (!context.Progress.ReferencesComplete) return BuildScanResult(context);
            foreach (var projectGroup in candidateDocuments.GroupBy(document => document.Project))
            {
                await ScanProjectAsync(projectGroup.Key, projectGroup, context, budget.Token);
                if (context.Progress.BudgetExpired) break;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && budget.IsCancellationRequested)
        {
            context.Progress.BudgetExpired = true;
        }
        ct.ThrowIfCancellationRequested();
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
                ct.ThrowIfCancellationRequested();
                if (context.Progress.ElapsedMilliseconds >= context.Progress.BudgetMilliseconds)
                {
                    context.Progress.BudgetExpired = true;
                    return;
                }
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
            ct.ThrowIfCancellationRequested();
            await ProcessTypeNodeAsync(typeNode, semanticModel, document, entryPoint, hasInternalsVisibleTo, context, ct);
        }
        context.Progress.ProcessedDocuments++;
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
            || DeadCodeSuppression.IsSuppressed(typeSymbol)
            || typeSymbol.GetMembers().Any(DeadCodeWhitelist.IsCompilerRoot))
        {
            return false;
        }

        if (IsApiProtected(typeSymbol, document, context))
        {
            context.ApiProtectedCount++;
            return false;
        }

        var referenceAnalysis = await AnalyzeReferencesAsync(typeSymbol, context, ct);
        if (referenceAnalysis.IsUndecidable)
        {
            RecordUncertainty(typeSymbol, document, context);
            return false;
        }

        var isDead = referenceAnalysis.IsDeadCandidate;
        if (!isDead) return false;

        if (context.Args.Confidence == DeadCodeConfidenceFilter.High && ClassifyConfidence(typeSymbol, hasInternalsVisibleTo) != "high") return false;
        context.DeadContainerTypes.Add(typeSymbol);
        AddDeadSymbol(context, typeSymbol, document, hasInternalsVisibleTo, referenceAnalysis);
        return true;
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
        if (!member.DeclaringSyntaxReferences.Any(r => r.SyntaxTree == typeNode.SyntaxTree && typeNode.Span.Contains(r.Span))) return;
        if (!context.ScannedMembers.Add(member)) return;
        if (DeadCodeWhitelist.IsWhitelisted(member, entryPoint)) return;
        if (DeadCodeSuppression.IsSuppressed(member)) return;
        if (!ShouldCheckMemberKind(member, context.Args.Kind)) return;
        if (!MatchesAccessibilityFilter(member.DeclaredAccessibility, context.Args.Accessibility)) return;
        context.ScannedCount++;
        if (IsApiProtected(member, document, context))
        {
            context.ApiProtectedCount++;
            return;
        }

        var referenceAnalysis = await AnalyzeReferencesAsync(member, context, ct);
        if (referenceAnalysis.IsUndecidable)
        {
            RecordUncertainty(member, document, context);
            return;
        }

        if (referenceAnalysis.IsDeadCandidate)
        {
            AddDeadSymbol(context, member, document, hasInternalsVisibleTo, referenceAnalysis);
        }
    }

    private static bool IsApiProtected(ISymbol symbol, Document document, DeadCodeScanContext context)
    {
        if (context.Args.Config is null) return false;
        var config = ProjectConfigResolver.ResolveForProject(document.Project.Name, context.Args.Config);
        return string.Equals(config.DeadCode?.DefaultApiSurface, "external_library", StringComparison.Ordinal)
            && (DeadCodeApiSurfacePolicy.IsExternallyVisible(symbol)
                || symbol.ContainingType is { } type && DeadCodeApiSurfacePolicy.IsExternallyVisible(type)
                && GetImplementedInterfaceMembers(symbol).Any(DeadCodeApiSurfacePolicy.IsExternallyVisible));
    }

    private static async Task<SymbolReferenceAnalysis> AnalyzeReferencesAsync(
        ISymbol symbol,
        DeadCodeScanContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (DeadCodeApiSurfacePolicy.HasMissingFriend(symbol, context.Solution))
            context.UsageIndex.MarkUnknown(symbol, "friend_consumer_missing");
        var references = symbol is INamedTypeSymbol type
            ? GetRelatedReferenceSymbols(symbol).Concat(type.GetMembers().SelectMany(GetRelatedReferenceSymbols))
            : GetRelatedReferenceSymbols(symbol);
        var analyses = references.Select(reference => context.UsageIndex.Analyze(reference,
            symbol is INamedTypeSymbol ? DeadCodeUsageIndex.Key(symbol) : null)).ToArray();
        return await Task.FromResult(new SymbolReferenceAnalysis(
            analyses.Any(analysis => analysis.Production),
            analyses.Any(analysis => analysis.Unknown),
            analyses.Sum(analysis => analysis.Tests),
            analyses.Sum(analysis => analysis.Writes)));
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

    private readonly record struct SymbolReferenceAnalysis(bool HasProductionReference, bool HasUnknownReference, int TestReferenceCount, int WriteReferenceCount = 0)
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
        return project.SupportsCompilation && (args.IncludeTests || DeadCodeProjectRole.Resolve(project, args.Config) != "test");
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
