#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Maps.Skeleton;

/// <summary>
/// Extrahiert Typ-Skelette (Signaturen + Metadaten) aus einem C#-Syntaxbaum via SemanticModel.
/// </summary>
internal sealed class SkeletonSyntaxWalker : CSharpSyntaxWalker
{
    private readonly SemanticModel _semanticModel;
    private readonly string _relativePath;
    private readonly IReadOnlyCollection<string> _dependencySuffixes;
    private readonly List<SkeletonTypeInfo> _types = [];
    private string _currentNamespace = "";

    public IReadOnlyList<SkeletonTypeInfo> Types => _types;

    internal SkeletonSyntaxWalker(
        SemanticModel semanticModel,
        string relativePath,
        IReadOnlyCollection<string> dependencySuffixes)
        : base(SyntaxWalkerDepth.Node)
    {
        _semanticModel = semanticModel;
        _relativePath = relativePath;
        _dependencySuffixes = dependencySuffixes;
    }

    public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
    {
        var previous = _currentNamespace;
        _currentNamespace = node.Name.ToString();
        base.VisitNamespaceDeclaration(node);
        _currentNamespace = previous;
    }

    public override void VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
    {
        _currentNamespace = node.Name.ToString();
        base.VisitFileScopedNamespaceDeclaration(node);
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        if (IsNestedType(node)) return;
        _types.Add(BuildTypeInfo("class", node.Modifiers, node.Identifier.Text,
            node.TypeParameterList, node.BaseList, node.Members));
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        if (IsNestedType(node)) return;
        var kind = node.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? "record struct" : "record";
        _types.Add(BuildTypeInfo(kind, node.Modifiers, node.Identifier.Text,
            node.TypeParameterList, node.BaseList, node.Members));
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        if (IsNestedType(node)) return;
        _types.Add(BuildTypeInfo("interface", node.Modifiers, node.Identifier.Text,
            node.TypeParameterList, node.BaseList, node.Members));
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        if (IsNestedType(node)) return;
        _types.Add(BuildTypeInfo("struct", node.Modifiers, node.Identifier.Text,
            node.TypeParameterList, node.BaseList, node.Members));
    }

    public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        if (IsNestedType(node)) return;
        var members = node.Members
            .Select(m => new SkeletonMemberInfo(MemberKind.Field, m.Identifier.Text, null))
            .ToList();
        _types.Add(new SkeletonTypeInfo(
            _currentNamespace,
            "enum",
            BuildModifiers(node.Modifiers),
            node.Identifier.Text,
            null,
            _relativePath,
            members));
    }

    // ── Private Helpers ──────────────────────────────────────────────────────

    private static bool IsNestedType(SyntaxNode node) =>
        node.Parent is TypeDeclarationSyntax;

    private SkeletonTypeInfo BuildTypeInfo(
        string typeKind,
        SyntaxTokenList modifiers,
        string name,
        TypeParameterListSyntax? typeParams,
        BaseListSyntax? baseList,
        SyntaxList<MemberDeclarationSyntax> members)
    {
        var fullName = name + (typeParams?.ToString() ?? "");
        var baseTypes = baseList != null ? ": " + baseList.Types.ToString() : null;
        var memberInfos = ExtractMembers(members);

        return new SkeletonTypeInfo(
            _currentNamespace,
            typeKind,
            BuildModifiers(modifiers),
            fullName,
            baseTypes,
            _relativePath,
            memberInfos);
    }

    private List<SkeletonMemberInfo> ExtractMembers(SyntaxList<MemberDeclarationSyntax> members)
    {
        var result = new List<SkeletonMemberInfo>();

        foreach (var member in members)
        {
            var info = member switch
            {
                FieldDeclarationSyntax f       => BuildFieldInfo(f),
                ConstructorDeclarationSyntax c => BuildConstructorInfo(c),
                PropertyDeclarationSyntax p    => BuildPropertyInfo(p),
                MethodDeclarationSyntax m      => BuildMethodInfo(m),
                EventFieldDeclarationSyntax e  => BuildEventInfo(e),
                _                              => null,
            };

            if (info != null) result.Add(info);
        }

        return result;
    }

    private static SkeletonMemberInfo BuildFieldInfo(FieldDeclarationSyntax node)
    {
        var sig = node.ToString().Trim().TrimEnd(';') + ";";
        sig = NormalizeWhitespace(sig);
        return new SkeletonMemberInfo(MemberKind.Field, sig, null);
    }

    private static SkeletonMemberInfo BuildPropertyInfo(PropertyDeclarationSyntax node)
    {
        var accessors = node.AccessorList != null
            ? "{ " + string.Join(" ", node.AccessorList.Accessors.Select(a => a.Keyword.Text + ";")) + " }"
            : "=> /* computed */";
        var sig = $"{BuildModifiers(node.Modifiers, node.Parent)} {node.Type} {node.Identifier.Text} {accessors}";
        return new SkeletonMemberInfo(MemberKind.Property, NormalizeWhitespace(sig), null);
    }

    private SkeletonMemberInfo BuildConstructorInfo(ConstructorDeclarationSyntax node)
    {
        var paramList = FormatParameters(node.ParameterList);
        var sig = $"{BuildModifiers(node.Modifiers, node.Parent)} {node.Identifier.Text}({paramList})";
        var meta = ExtractMethodMeta(node.Body, node.ExpressionBody, node);
        return new SkeletonMemberInfo(MemberKind.Constructor, NormalizeWhitespace(sig), meta);
    }

    private SkeletonMemberInfo BuildMethodInfo(MethodDeclarationSyntax node)
    {
        var typeParams = node.TypeParameterList?.ToString() ?? "";
        var paramList = FormatParameters(node.ParameterList);
        var sig = $"{BuildModifiers(node.Modifiers, node.Parent)} {node.ReturnType} {node.Identifier.Text}{typeParams}({paramList})";
        var meta = ExtractMethodMeta(node.Body, node.ExpressionBody, node);
        var kind = ClassifyMethodKind(node.Modifiers, node.Parent);
        return new SkeletonMemberInfo(kind, NormalizeWhitespace(sig), meta);
    }

    private static SkeletonMemberInfo BuildEventInfo(EventFieldDeclarationSyntax node)
    {
        var sig = NormalizeWhitespace(node.ToString().Trim());
        return new SkeletonMemberInfo(MemberKind.Event, sig, null);
    }

    private string? ExtractMethodMeta(
        BlockSyntax? body,
        ArrowExpressionClauseSyntax? exprBody,
        SyntaxNode context)
    {
        SyntaxNode? bodyNode = body ?? (SyntaxNode?)exprBody;
        if (bodyNode == null) return null;

        var throws = ExtractThrowTypes(bodyNode);
        var uses = ExtractUsedDependencyTypes(bodyNode, context);

        var parts = new List<string>();
        if (throws.Count > 0) parts.Add("Throws: " + string.Join(", ", throws));
        if (uses.Count > 0)   parts.Add("Uses: " + string.Join(", ", uses));

        return parts.Count > 0 ? string.Join(" | ", parts) : null;
    }

    private IReadOnlyList<string> ExtractThrowTypes(SyntaxNode body)
    {
        var types = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var node in body.DescendantNodes())
        {
            BaseObjectCreationExpressionSyntax? creation = node switch
            {
                ThrowStatementSyntax ts  => ts.Expression as BaseObjectCreationExpressionSyntax,
                ThrowExpressionSyntax te => te.Expression as BaseObjectCreationExpressionSyntax,
                _                        => null,
            };

            if (creation == null) continue;

            var typeInfo = _semanticModel.GetTypeInfo(creation);
            var typeName = typeInfo.Type?.Name;
            if (!string.IsNullOrEmpty(typeName))
                types.Add(typeName);
        }

        return [.. types];
    }

    private IReadOnlyList<string> ExtractUsedDependencyTypes(SyntaxNode body, SyntaxNode context)
    {
        var containingType = _semanticModel.GetDeclaredSymbol(context)?.ContainingType;
        if (containingType == null) return [];

        var typeNames = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var identifier in body.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            var symbol = _semanticModel.GetSymbolInfo(identifier).Symbol;
            if (symbol is not (IFieldSymbol or IPropertySymbol)) continue;
            if (!SymbolEqualityComparer.Default.Equals(symbol.ContainingType, containingType)) continue;

            var typeName = symbol switch
            {
                IFieldSymbol f    => f.Type.Name,
                IPropertySymbol p => p.Type.Name,
                _                 => null,
            };

            if (!string.IsNullOrEmpty(typeName) && IsDependencyType(typeName))
                typeNames.Add(typeName);
        }

        return [.. typeNames];
    }

    private bool IsDependencyType(string typeName)
    {
        if (typeName.Length >= 2 && typeName[0] == 'I' && char.IsUpper(typeName[1]))
            return true;

        foreach (var suffix in _dependencySuffixes)
        {
            if (typeName.EndsWith(suffix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static MemberKind ClassifyMethodKind(SyntaxTokenList modifiers, SyntaxNode? parent)
    {
        if (parent is InterfaceDeclarationSyntax)
            return MemberKind.PublicMethod;

        foreach (var mod in modifiers)
        {
            if (mod.IsKind(SyntaxKind.PublicKeyword))    return MemberKind.PublicMethod;
            if (mod.IsKind(SyntaxKind.InternalKeyword))  return MemberKind.InternalMethod;
        }
        return MemberKind.PrivateMethod;
    }

    private static string BuildModifiers(SyntaxTokenList modifiers, SyntaxNode? parent = null)
    {
        var text = modifiers.ToString().Trim();
        if (string.IsNullOrEmpty(text))
        {
            if (parent is InterfaceDeclarationSyntax)
                return "";
            return "private";
        }
        return text;
    }

    private static string FormatParameters(ParameterListSyntax paramList) =>
        string.Join(", ", paramList.Parameters.Select(p => NormalizeWhitespace(p.ToString())));

    private static string NormalizeWhitespace(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text.Trim(), @"\s+", " ");
}
