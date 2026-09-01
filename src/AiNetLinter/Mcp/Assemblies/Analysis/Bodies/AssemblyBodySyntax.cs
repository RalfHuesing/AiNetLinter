#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Assemblies.Analysis.Bodies;

internal static class AssemblyBodySyntax
{
    internal static bool HasUnavailableMember(ISymbol symbol) =>
        symbol switch
        {
            IMethodSymbol method => method.IsAbstract || HasExternModifier(method),
            IPropertySymbol property => HasNoBody(property),
            IEventSymbol eventSymbol => eventSymbol.AddMethod?.IsAbstract == true
                || eventSymbol.RemoveMethod?.IsAbstract == true,
            _ => false,
        };

    internal static bool HasNoBody(IPropertySymbol property) =>
        property.GetMethod?.IsAbstract == true
        || property.SetMethod?.IsAbstract == true
        || HasExternModifier(property.GetMethod)
        || HasExternModifier(property.SetMethod);

    internal static bool HasExternModifier(ISymbol? symbol) =>
        symbol?.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<MemberDeclarationSyntax>()
            .Any(member => member.Modifiers.Any(SyntaxKind.ExternKeyword)) == true;
}
