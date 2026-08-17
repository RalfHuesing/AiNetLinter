#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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

namespace AiNetLinter.Mcp.Tools.DeadCode;

/// <summary>
/// Statische Scan-Pipeline fuer das Auffinden von unreferenziertem/totem Code in einer Roslyn-Solution.
/// </summary>
public static class FindDeadCodeScanner
{
    public const int DefaultMaxResults = 50;

    /// <summary>
    /// Fuehrt den Dead-Code-Scan gemaess der uebergebenen Parameter ueber die Solution aus.
    /// </summary>
    public static async Task<DeadCodeScanResult> ScanAsync(
        Solution solution,
        FindDeadCodeArgs args,
        CancellationToken ct = default)
    {
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        var candidateDocuments = CollectCandidateDocuments(solution, solutionDir, args);
        var context = new DeadCodeScanContext(solution, solutionDir, args);

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

        var entryPoint = compilation.GetEntryPoint(ct);
        var hasInternalsVisibleTo = CheckInternalsVisibleTo(compilation.Assembly);

        foreach (var document in documents)
        {
            if (ct.IsCancellationRequested) break;
            await ScanDocumentAsync(document, entryPoint, hasInternalsVisibleTo, context, ct);
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

        var declaredTypeNodes = syntaxRoot.DescendantNodes().OfType<BaseTypeDeclarationSyntax>();
        foreach (var typeNode in declaredTypeNodes)
        {
            if (semanticModel.GetDeclaredSymbol(typeNode, ct) is not INamedTypeSymbol typeSymbol) continue;
            context.ScannedCount++;

            if (typeSymbol.ContainingType != null && context.DeadContainerTypes.Contains(typeSymbol.ContainingType))
            {
                continue;
            }

            var typeIsDead = await CheckAndRecordTypeAsync(typeSymbol, document, entryPoint, hasInternalsVisibleTo, context, ct);
            if (!typeIsDead)
            {
                await ScanTypeMembersAsync(typeSymbol, document, entryPoint, hasInternalsVisibleTo, context, ct);
            }
        }
    }

    private static async Task<bool> CheckAndRecordTypeAsync(
        INamedTypeSymbol typeSymbol,
        Document document,
        IMethodSymbol? entryPoint,
        bool hasInternalsVisibleTo,
        DeadCodeScanContext context,
        CancellationToken ct)
    {
        if (!ShouldCheckSymbol(typeSymbol, context.Args) || DeadCodeWhitelist.IsWhitelisted(typeSymbol, entryPoint))
        {
            return false;
        }

        var isDead = await IsSymbolUnreferencedAsync(typeSymbol, document, context.Solution, ct);
        if (!isDead) return false;

        if (typeSymbol.DeclaredAccessibility == Accessibility.Private)
        {
            context.DeadContainerTypes.Add(typeSymbol);
            AddDeadSymbol(context, typeSymbol, document, hasInternalsVisibleTo);
            return true;
        }

        AddDeadSymbol(context, typeSymbol, document, hasInternalsVisibleTo);
        return false;
    }

    private static async Task ScanTypeMembersAsync(
        INamedTypeSymbol typeSymbol,
        Document document,
        IMethodSymbol? entryPoint,
        bool hasInternalsVisibleTo,
        DeadCodeScanContext context,
        CancellationToken ct)
    {
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member.IsImplicitlyDeclared) continue;
            if (DeadCodeWhitelist.IsWhitelisted(member, entryPoint)) continue;
            if (!ShouldCheckMemberKind(member, context.Args.Kind)) continue;
            if (!MatchesAccessibilityFilter(member.DeclaredAccessibility, context.Args.Accessibility)) continue;

            var isDead = await IsSymbolUnreferencedAsync(member, document, context.Solution, ct);
            if (isDead)
            {
                AddDeadSymbol(context, member, document, hasInternalsVisibleTo);
            }
        }
    }

    private static async Task<bool> IsSymbolUnreferencedAsync(
        ISymbol symbol,
        Document declaringDocument,
        Solution solution,
        CancellationToken ct)
    {
        if (await HasReferencedInterfaceOrOverrideAsync(symbol, solution, ct))
        {
            return false;
        }

        if (symbol.DeclaredAccessibility == Accessibility.Private)
        {
            var declaringDocs = symbol.DeclaringSyntaxReferences
                .Select(r => solution.GetDocument(r.SyntaxTree))
                .OfType<Document>()
                .ToImmutableHashSet();

            var effectiveDocs = declaringDocs.IsEmpty ? ImmutableHashSet.Create(declaringDocument) : declaringDocs;
            var references = await SymbolFinder.FindReferencesAsync(symbol, solution, documents: effectiveDocs, cancellationToken: ct);
            return references.All(r => !r.Locations.Any());
        }
        else
        {
            var references = await SymbolFinder.FindReferencesAsync(symbol, solution, ct);
            return references.All(r => !r.Locations.Any());
        }
    }

    private static async Task<bool> HasReferencedInterfaceOrOverrideAsync(
        ISymbol symbol,
        Solution solution,
        CancellationToken ct)
    {
        if (symbol is IMethodSymbol method && method.IsOverride && method.OverriddenMethod != null)
        {
            var baseRefs = await SymbolFinder.FindReferencesAsync(method.OverriddenMethod, solution, ct);
            if (baseRefs.Any(r => r.Locations.Any())) return true;
        }
        else if (symbol is IPropertySymbol property && property.IsOverride && property.OverriddenProperty != null)
        {
            var baseRefs = await SymbolFinder.FindReferencesAsync(property.OverriddenProperty, solution, ct);
            if (baseRefs.Any(r => r.Locations.Any())) return true;
        }

        foreach (var ifaceMember in GetImplementedInterfaceMembers(symbol))
        {
            var ifaceRefs = await SymbolFinder.FindReferencesAsync(ifaceMember, solution, ct);
            if (ifaceRefs.Any(r => r.Locations.Any())) return true;
        }

        return false;
    }

    private static IEnumerable<ISymbol> GetImplementedInterfaceMembers(ISymbol symbol)
    {
        if (symbol.ContainingType is null) return [];
        if (symbol is IMethodSymbol method) return GetImplementedMethodMembers(method, symbol.ContainingType);
        if (symbol is IPropertySymbol prop) return GetImplementedPropertyMembers(prop, symbol.ContainingType);
        return [];
    }

    private static IEnumerable<ISymbol> GetImplementedMethodMembers(IMethodSymbol method, INamedTypeSymbol containingType)
    {
        foreach (var explicitImpl in method.ExplicitInterfaceImplementations)
        {
            yield return explicitImpl;
        }

        foreach (var iface in containingType.AllInterfaces)
        {
            foreach (var ifaceMember in iface.GetMembers().OfType<IMethodSymbol>())
            {
                if (SymbolEqualityComparer.Default.Equals(containingType.FindImplementationForInterfaceMember(ifaceMember), method))
                {
                    yield return ifaceMember;
                }
            }
        }
    }

    private static IEnumerable<ISymbol> GetImplementedPropertyMembers(IPropertySymbol prop, INamedTypeSymbol containingType)
    {
        foreach (var explicitImpl in prop.ExplicitInterfaceImplementations)
        {
            yield return explicitImpl;
        }

        foreach (var iface in containingType.AllInterfaces)
        {
            foreach (var ifaceMember in iface.GetMembers().OfType<IPropertySymbol>())
            {
                if (SymbolEqualityComparer.Default.Equals(containingType.FindImplementationForInterfaceMember(ifaceMember), prop))
                {
                    yield return ifaceMember;
                }
            }
        }
    }

    private static void AddDeadSymbol(
        DeadCodeScanContext context,
        ISymbol symbol,
        Document document,
        bool hasInternalsVisibleTo)
    {
        var kindStr = GetSymbolKindString(symbol);
        var accessibilityStr = GetAccessibilityString(symbol.DeclaredAccessibility);
        var confidenceStr = ClassifyConfidence(symbol, hasInternalsVisibleTo);

        if (context.Args.Confidence == DeadCodeConfidenceFilter.High && !confidenceStr.Equals("high", StringComparison.OrdinalIgnoreCase)) return;
        if (context.Args.Confidence == DeadCodeConfidenceFilter.Low && !confidenceStr.Equals("low", StringComparison.OrdinalIgnoreCase)) return;

        var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        var line = 1;
        var column = 1;
        var filePath = document.FilePath ?? "";

        if (syntaxRef != null)
        {
            var span = syntaxRef.GetSyntax().GetLocation().GetLineSpan();
            line = span.StartLinePosition.Line + 1;
            column = span.StartLinePosition.Character + 1;
            filePath = span.Path;
        }

        var relativePath = PathNormalizer.ToRelative(context.SolutionDir, filePath);
        var limitsApplies = DetermineLimitsApplies(symbol, hasInternalsVisibleTo);

        var reason = confidenceStr == "high"
            ? "Keine Referenzen innerhalb der Solution gefunden (privates/internes Element ohne Framework-Marker)."
            : "Keine Referenzen in der Solution gefunden (oeffentliche API oder moegliche Framework-Bindung).";

        var containerTypeName = symbol.ContainingType?.ToDisplayString() ?? symbol.ContainingNamespace?.ToDisplayString() ?? "";

        var entry = new DeadCodeEntry(
            Id: symbol.ToDisplayString(),
            Kind: kindStr,
            ContainerType: containerTypeName,
            SymbolName: symbol.Name,
            File: relativePath,
            Line: line,
            Column: column,
            Accessibility: accessibilityStr,
            Confidence: confidenceStr,
            Reason: reason,
            LimitsApplies: limitsApplies);

        context.DeadSymbols.Add(entry);

        if (context.ByKind.TryGetValue(kindStr, out var count))
            context.ByKind[kindStr] = count + 1;
        else
            context.ByKind[kindStr] = 1;
    }

    private static DeadCodeScanResult BuildScanResult(DeadCodeScanContext context)
    {
        var totalDead = context.DeadSymbols.Count;
        var highCount = context.DeadSymbols.Count(s => s.Confidence.Equals("high", StringComparison.OrdinalIgnoreCase));
        var lowCount = context.DeadSymbols.Count(s => s.Confidence.Equals("low", StringComparison.OrdinalIgnoreCase));

        var isTruncated = context.DeadSymbols.Count > context.Args.MaxResults;
        var paginatedSymbols = isTruncated ? context.DeadSymbols.Take(context.Args.MaxResults).ToList() : context.DeadSymbols;

        var summary = new DeadCodeSummary(
            ScannedSymbols: context.ScannedCount,
            TotalDead: totalDead,
            High: highCount,
            Low: lowCount,
            ByKind: context.ByKind);

        var recommendedAction = new DeadCodeRecommendedNextAction(
            Action: "ask_user",
            Reason: "Vor dem Loeschen von totem Code Rueckfrage halten (statische Heuristik; dynamische Framework-Bindungen koennen vorliegen).");

        return new DeadCodeScanResult(
            DeadSymbols: paginatedSymbols,
            Summary: summary,
            Limits: DeadCodeLimits.DefaultLimits,
            RecommendedNextAction: recommendedAction,
            IsTruncated: isTruncated);
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

    private static List<Document> CollectCandidateDocuments(Solution solution, string solutionDir, FindDeadCodeArgs args)
    {
        var docs = new List<Document>();
        foreach (var project in solution.Projects)
        {
            if (!project.SupportsCompilation) continue;
            if (!args.IncludeTests && TestProjectDetector.IsTestProject(project)) continue;

            foreach (var doc in project.Documents)
            {
                if (!SourceFileCatalog.IsValidDocument(doc, solutionDir)) continue;
                if (!ViolationScopeFilter.MatchesScope(doc.FilePath ?? "", project.Name, solutionDir, args.ScopeFilter)) continue;

                docs.Add(doc);
            }
        }
        return docs;
    }

    private static bool ShouldCheckSymbol(INamedTypeSymbol symbol, FindDeadCodeArgs args)
    {
        if (!MatchesKindFilter(symbol, args.Kind)) return false;
        if (!MatchesAccessibilityFilter(symbol.DeclaredAccessibility, args.Accessibility)) return false;
        return true;
    }

    private static bool MatchesKindFilter(ISymbol symbol, DeadCodeKindFilter kindFilter)
    {
        if (kindFilter == DeadCodeKindFilter.All) return true;

        return symbol switch
        {
            INamedTypeSymbol named => MatchesNamedTypeKind(named, kindFilter),
            IMethodSymbol => kindFilter is DeadCodeKindFilter.Method,
            IPropertySymbol => kindFilter is DeadCodeKindFilter.Property,
            IFieldSymbol => kindFilter is DeadCodeKindFilter.Field,
            IEventSymbol => kindFilter is DeadCodeKindFilter.Event,
            _ => false
        };
    }

    private static bool MatchesNamedTypeKind(INamedTypeSymbol symbol, DeadCodeKindFilter kindFilter)
    {
        if (kindFilter == DeadCodeKindFilter.Type) return true;
        if (kindFilter == DeadCodeKindFilter.Class && symbol.TypeKind == TypeKind.Class) return true;
        if (kindFilter == DeadCodeKindFilter.Delegate && symbol.TypeKind == TypeKind.Delegate) return true;
        return false;
    }

    private static bool ShouldCheckMemberKind(ISymbol member, DeadCodeKindFilter kindFilter)
    {
        if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary or MethodKind.Constructor })
            return MatchesKindFilter(member, kindFilter);
        if (member is IPropertySymbol or IFieldSymbol or IEventSymbol)
            return MatchesKindFilter(member, kindFilter);
        return false;
    }

    private static bool MatchesAccessibilityFilter(Accessibility accessibility, DeadCodeAccessibilityFilter filter)
    {
        return filter switch
        {
            DeadCodeAccessibilityFilter.All => true,
            DeadCodeAccessibilityFilter.Private => accessibility == Accessibility.Private,
            DeadCodeAccessibilityFilter.Internal => accessibility == Accessibility.Internal,
            DeadCodeAccessibilityFilter.Public => accessibility == Accessibility.Public,
            DeadCodeAccessibilityFilter.PrivateInternal => accessibility is Accessibility.Private or Accessibility.Internal,
            _ => true
        };
    }

    private static string GetSymbolKindString(ISymbol symbol)
    {
        return symbol switch
        {
            INamedTypeSymbol t => GetNamedTypeKindString(t.TypeKind),
            IMethodSymbol m => m.MethodKind == MethodKind.Constructor ? "constructor" : "method",
            IPropertySymbol => "property",
            IFieldSymbol => "field",
            IEventSymbol => "event",
            _ => "symbol"
        };
    }

    private static string GetNamedTypeKindString(TypeKind typeKind)
    {
        return typeKind switch
        {
            TypeKind.Class => "class",
            TypeKind.Struct => "struct",
            TypeKind.Interface => "interface",
            TypeKind.Enum => "enum",
            TypeKind.Delegate => "delegate",
            _ => "type"
        };
    }

    private static string GetAccessibilityString(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Private => "private",
            Accessibility.Internal => "internal",
            Accessibility.Public => "public",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "unknown"
        };
    }
}

internal sealed class DeadCodeScanContext(Solution solution, string solutionDir, FindDeadCodeArgs args)
{
    public Solution Solution { get; } = solution;
    public string SolutionDir { get; } = solutionDir;
    public FindDeadCodeArgs Args { get; } = args;
    public List<DeadCodeEntry> DeadSymbols { get; } = [];
    public Dictionary<string, int> ByKind { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<INamedTypeSymbol> DeadContainerTypes { get; } = new(SymbolEqualityComparer.Default);
    public int ScannedCount { get; set; }
}
