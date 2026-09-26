#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools.Verify.DeadCode;

/// <summary>Solutionweiter Referenzindex eines unveränderlichen Advisory-Snapshots.</summary>
internal sealed class DeadCodeUsageIndex
{
    private readonly Dictionary<string, List<Usage>> usages = new(StringComparer.Ordinal);
    internal List<DeadCodeUsageDocument> Documents { get; } = [];
    internal List<INamedTypeSymbol> SourceTypes { get; } = [];
    internal Dictionary<string, HashSet<string>> UnknownReasons { get; } = new(StringComparer.Ordinal);
    internal HashSet<string> CoverageGaps { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, List<DeadCodeGenericBinding>> TypeArguments { get; } = new(StringComparer.Ordinal);

    internal static string Key(ISymbol symbol) =>
        symbol.ContainingAssembly?.Identity.Name + ":" + symbol.OriginalDefinition.GetDocumentationCommentId();

    internal static async Task<DeadCodeUsageIndex> CreateAsync(Solution solution, CancellationToken ct, AiNetLinter.Configuration.Config? config = null)
    {
        var index = new DeadCodeUsageIndex();
        foreach (var project in solution.Projects.Where(project => project.SupportsCompilation))
        {
            var generated = await project.GetSourceGeneratedDocumentsAsync(ct);
            foreach (var document in project.Documents.Concat<Document>(generated))
            {
                ct.ThrowIfCancellationRequested();
                var model = await document.GetSemanticModelAsync(ct);
                var root = await document.GetSyntaxRootAsync(ct);
                if (model is null || root is null) { index.CoverageGaps.Add("semantic_document_unavailable"); continue; }
                var role = GetRole(document, config);
                var source = new DeadCodeUsageDocument(document, model, root, role);
                index.Documents.Add(source);
                index.IndexDocument(source, ct);
            }
        }
        return index;
    }

    internal static string GetRole(Document document, AiNetLinter.Configuration.Config? config = null)
    {
        var role = DeadCodeProjectRole.Resolve(document.Project, config);
        return role == "production" && document.FilePath is null ? "unknown" : role;
    }

    private void IndexDocument(DeadCodeUsageDocument source, CancellationToken ct)
    {
        IndexUnknownDeclarations(source, ct);
        foreach (var declaration in source.Root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            if (source.Model.GetDeclaredSymbol(declaration, ct) is INamedTypeSymbol type) SourceTypes.Add(type);
        foreach (var name in source.Root.DescendantNodes().OfType<SimpleNameSyntax>())
        {
            ct.ThrowIfCancellationRequested();
            var symbol = source.Model.GetSymbolInfo(name, ct).Symbol;
            if (symbol is IMethodSymbol { ReducedFrom: { } reduced }) symbol = reduced;
            if (symbol is not (INamedTypeSymbol or IMethodSymbol or IPropertySymbol or IFieldSymbol)) continue;
            var owner = source.Model.GetEnclosingSymbol(name.SpanStart, ct);
            var ownerType = owner as INamedTypeSymbol ?? owner?.ContainingType;
            var write = IsWrite(name);
            Add(symbol, source.Role, ownerType, write);
            if (symbol.ContainingType is { } type && !SymbolEqualityComparer.Default.Equals(type, ownerType))
                Add(type, source.Role, ownerType, false);
        }
        foreach (var creation in source.Root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            if (source.Model.GetSymbolInfo(creation, ct).Symbol is IMethodSymbol constructor)
                Add(constructor, source.Role);
        IndexTypeArguments(source, ct);
    }

    private void IndexUnknownDeclarations(DeadCodeUsageDocument source, CancellationToken ct)
    {
        if (source.Role != "unknown") return;
        foreach (var declaration in source.Root.DescendantNodes())
        {
            var symbol = source.Model.GetDeclaredSymbol(declaration, ct);
            if (symbol is INamedTypeSymbol or IMethodSymbol or IFieldSymbol or IPropertySymbol)
                MarkUnknown(symbol, "declaration_role");
        }
    }

    private void IndexTypeArguments(DeadCodeUsageDocument source, CancellationToken ct)
    {
        foreach (var call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (source.Model.GetSymbolInfo(call, ct).Symbol is not IMethodSymbol method) continue;
            for (var i = 0; i < method.TypeArguments.Length; i++)
            {
                if (method.TypeArguments[i] is not INamedTypeSymbol type) continue;
                var key = Key(method.OriginalDefinition) + ":" + method.TypeParameters[i].Name;
                if (!TypeArguments.TryGetValue(key, out var types)) TypeArguments[key] = types = [];
                types.Add(new(type, source.Role));
            }
        }
    }

    private static bool IsWrite(SimpleNameSyntax name)
    {
        SyntaxNode node = name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? access : name;
        return node.Parent is AssignmentExpressionSyntax assignment && assignment.Left == node
            && assignment.RawKind == (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.SimpleAssignmentExpression
            || node.Parent is ArgumentSyntax argument && argument.RefKindKeyword.ValueText == "out";
    }

    internal void Add(ISymbol symbol, string role, INamedTypeSymbol? owner = null, bool write = false)
    {
        var key = Key(symbol);
        if (!usages.TryGetValue(key, out var entries)) usages[key] = entries = [];
        entries.Add(new(role, owner is null ? null : Key(owner), write));
    }

    internal void MarkUnknown(ISymbol symbol, string reason)
    {
        Add(symbol, "unknown");
        var key = Key(symbol);
        if (!UnknownReasons.TryGetValue(key, out var reasons)) UnknownReasons[key] = reasons = new(StringComparer.Ordinal);
        reasons.Add(reason);
    }

    internal DeadCodeUsageAnalysis Analyze(ISymbol symbol, string? ignoredOwner = null)
    {
        if (!usages.TryGetValue(Key(symbol), out var entries)) return new(false, false, 0, 0);
        var relevant = entries.Where(entry => entry.Owner is null || entry.Owner != ignoredOwner).Where(entry => symbol is not INamedTypeSymbol || entry.Owner != Key(symbol)).ToArray();
        var readers = relevant.Where(entry => !entry.Write).ToArray();
        return new(readers.Any(entry => entry.Role == "production"), readers.Any(entry => entry.Role == "unknown"),
            readers.Count(entry => entry.Role == "test"), relevant.Count(entry => entry.Write));
    }

    private sealed record Usage(string Role, string? Owner, bool Write);
}

internal sealed record DeadCodeUsageDocument(Document Document, SemanticModel Model, SyntaxNode Root, string Role);
internal readonly record struct DeadCodeUsageAnalysis(bool Production, bool Unknown, int Tests, int Writes)
{
    internal bool HasKnownReference => Production || Tests > 0;
}

internal sealed record DeadCodeGenericBinding(INamedTypeSymbol Type, string Role);
