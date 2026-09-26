#nullable enable

using AiNetLinter.Mcp.Tools.FileStructure;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.Verify.DeadCode;

/// <summary>
/// Auswahl der im Dead-Code-Advisory unterstuetzten Typen und Methoden.
/// </summary>
internal static class DeadCodeFilters
{
    internal static bool ShouldCheckMember(ISymbol member) =>
        member is IMethodSymbol { MethodKind: MethodKind.Ordinary } method && !IsContractMethod(method);

    private static bool IsContractMethod(IMethodSymbol method)
    {
        if (method.ContainingType.TypeKind == TypeKind.Interface
            || method.IsAbstract
            || method.IsOverride
            || method.ExplicitInterfaceImplementations.Length > 0)
            return true;

        return method.ContainingType.AllInterfaces
            .SelectMany(contract => contract.GetMembers().OfType<IMethodSymbol>())
            .Any(contract => SymbolEqualityComparer.Default.Equals(
                method.ContainingType.FindImplementationForInterfaceMember(contract), method));
    }

    internal static string GetSymbolKindString(ISymbol symbol) =>
        symbol switch
        {
            INamedTypeSymbol t => t.TypeKind switch
            {
                TypeKind.Class => "class",
                TypeKind.Struct => "struct",
                TypeKind.Interface => "interface",
                TypeKind.Enum => "enum",
                TypeKind.Delegate => "delegate",
                _ => "type"
            },
            IMethodSymbol => "method",
            _ => nameof(symbol)
        };

    internal static string GetAccessibilityString(Accessibility accessibility) =>
        SymbolVisibilityResolver.ResolveVisibility(accessibility);
}
