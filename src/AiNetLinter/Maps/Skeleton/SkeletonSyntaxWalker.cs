#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Core;

namespace AiNetLinter.Maps.Skeleton;

/// <summary>
/// Extrahiert Typ-Skelette (Signaturen + Metadaten) aus einem C#-Syntaxbaum via SemanticModel.
/// </summary>
internal sealed class SkeletonSyntaxWalker : CSharpSyntaxWalker
{
    private readonly SemanticModel _semanticModel;
    private readonly string _relativePath;
    private readonly List<SkeletonTypeInfo> _types = [];
    private string _currentNamespace = "";
    private readonly IReadOnlyList<string> _includeNamespaces;
    private readonly IReadOnlyList<string> _excludeNamespaces;
    private readonly bool _publicOnly;

    public IReadOnlyList<SkeletonTypeInfo> Types => _types;

    internal SkeletonSyntaxWalker(
        SemanticModel semanticModel,
        string relativePath,
        IReadOnlyList<string> includeNamespaces,
        IReadOnlyList<string> excludeNamespaces,
        bool publicOnly)
        : base(SyntaxWalkerDepth.Node)
    {
        _semanticModel = semanticModel;
        _relativePath = relativePath;
        _includeNamespaces = includeNamespaces;
        _excludeNamespaces = excludeNamespaces;
        _publicOnly = publicOnly;
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
        if (IsNestedType(node) || !IsNamespaceAllowed()) return;
        _types.Add(BuildTypeInfo("class", node));
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        if (IsNestedType(node) || !IsNamespaceAllowed()) return;
        var kind = node.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? "record struct" : "record";
        _types.Add(BuildTypeInfo(kind, node));
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        if (IsNestedType(node) || !IsNamespaceAllowed()) return;
        _types.Add(BuildTypeInfo("interface", node));
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        if (IsNestedType(node) || !IsNamespaceAllowed()) return;
        _types.Add(BuildTypeInfo("struct", node));
    }

    public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        if (IsNestedType(node) || !IsNamespaceAllowed()) return;
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

    private SkeletonTypeInfo BuildTypeInfo(string typeKind, TypeDeclarationSyntax node)
    {
        var fullName = node.Identifier.Text + (node.TypeParameterList?.ToString() ?? "");
        var baseTypes = node.BaseList != null ? ": " + node.BaseList.Types.ToString() : null;
        var memberInfos = ExtractMembers(node, node.Members);

        if (node is RecordDeclarationSyntax recordDecl && recordDecl.ParameterList != null)
        {
            foreach (var param in recordDecl.ParameterList.Parameters)
            {
                var propType = param.Type?.ToString() ?? "object";
                var propName = param.Identifier.Text;
                var accessor = typeKind == "record struct" ? "{ get; set; }" : "{ get; init; }";
                var sig = $"public {propType} {propName} {accessor}";
                memberInfos.Add(new SkeletonMemberInfo(MemberKind.Property, NormalizeWhitespace(sig), null));
            }
        }

        return new SkeletonTypeInfo(
            _currentNamespace,
            typeKind,
            BuildModifiers(node.Modifiers),
            fullName,
            baseTypes,
            _relativePath,
            memberInfos);
    }

    private bool IsNamespaceAllowed()
    {
        return NamespaceFilter.IsNamespaceAllowed(_currentNamespace, _includeNamespaces, _excludeNamespaces);
    }

    private static SyntaxTokenList GetModifiers(MemberDeclarationSyntax member)
    {
        return member switch
        {
            FieldDeclarationSyntax f => f.Modifiers,
            ConstructorDeclarationSyntax c => c.Modifiers,
            PropertyDeclarationSyntax p => p.Modifiers,
            MethodDeclarationSyntax m => m.Modifiers,
            EventFieldDeclarationSyntax e => e.Modifiers,
            _ => default
        };
    }

    private static bool HasPublicOrInternalModifier(SyntaxTokenList modifiers)
    {
        return modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword) || mod.IsKind(SyntaxKind.InternalKeyword));
    }

    private static bool IsExplicitInterfaceImplementation(MemberDeclarationSyntax member)
    {
        if (member is MethodDeclarationSyntax method)
        {
            return method.ExplicitInterfaceSpecifier != null;
        }
        if (member is PropertyDeclarationSyntax prop)
        {
            return prop.ExplicitInterfaceSpecifier != null;
        }
        return false;
    }

    private static bool IsPublicOrInternal(MemberDeclarationSyntax member, SyntaxNode parent)
    {
        if (parent is InterfaceDeclarationSyntax)
        {
            return true;
        }

        var modifiers = GetModifiers(member);
        if (HasPublicOrInternalModifier(modifiers))
        {
            return true;
        }

        return IsExplicitInterfaceImplementation(member);
    }

    private List<SkeletonMemberInfo> ExtractMembers(SyntaxNode parent, SyntaxList<MemberDeclarationSyntax> members)
    {
        var result = new List<SkeletonMemberInfo>();

        foreach (var member in members)
        {
            if (_publicOnly && !IsPublicOrInternal(member, parent))
            {
                continue;
            }

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
        var uses = ExtractUsedDependencies(bodyNode, context);

        var parts = new List<string>();
        if (throws.Count > 0) parts.Add("Throws: " + string.Join(", ", throws));
        if (uses.Count > 0)   parts.Add("Uses: " + string.Join(", ", uses));

        return parts.Count > 0 ? string.Join("; ", parts) : null;
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

    private IReadOnlyList<string> ExtractUsedDependencies(SyntaxNode body, SyntaxNode context)
    {
        var containingType = _semanticModel.GetDeclaredSymbol(context)?.ContainingType;
        if (containingType == null) return [];

        var dependencyNames = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var identifier in body.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            var symbol = _semanticModel.GetSymbolInfo(identifier).Symbol;
            if (symbol is not (IFieldSymbol or IPropertySymbol)) continue;
            if (!SymbolEqualityComparer.Default.Equals(symbol.ContainingType, containingType)) continue;

            dependencyNames.Add(symbol.Name);
        }

        return [.. dependencyNames];
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
