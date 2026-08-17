#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.DeadCode;

/// <summary>
/// Beherbergt Whitelist-Pruefungen fuer Symbole, die von find_dead_code grundsaetzlich nicht als Dead Code gemeldet werden.
/// </summary>
internal static class DeadCodeWhitelist
{
    private static readonly HashSet<string> WhitelistedAttributeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ModuleInitializerAttribute",
        "DllImportAttribute",
        "UnmanagedCallersOnlyAttribute",
        "FactAttribute",
        "TheoryAttribute",
        "TestAttribute",
        "TestMethodAttribute",
        "McpServerToolAttribute",
        "McpToolAttribute",
        "ExportAttribute",
        "ImportAttribute",
        "JSInvokableAttribute",
        "ParameterAttribute",
        "InjectAttribute",
        "JsonConstructorAttribute",
        "JsonConstructor",
        "BenchmarkAttribute",
        "Benchmark"
    };

    /// <summary>
    /// Prueft, ob ein Symbol gemaess Compiler-, Framework- und Konstruktor-Regeln gewhitelistet ist.
    /// </summary>
    internal static bool IsWhitelisted(ISymbol symbol, IMethodSymbol? entryPoint)
    {
        if (symbol.IsImplicitlyDeclared) return true;
        if (IsCompilerGeneratedName(symbol.Name)) return true;
        if (IsEntryPointSymbol(symbol, entryPoint)) return true;
        if (HasWhitelistedAttribute(symbol)) return true;
        if (IsSpecialMethodKind(symbol)) return true;
        if (IsUtilityClassConstructor(symbol)) return true;

        return false;
    }

    private static bool IsCompilerGeneratedName(string name)
    {
        return name.StartsWith('<') ||
               name.EndsWith("$", StringComparison.Ordinal) ||
               name.Equals("EqualityContract", StringComparison.Ordinal) ||
               name.Equals("<Clone>$", StringComparison.Ordinal);
    }

    private static bool IsEntryPointSymbol(ISymbol symbol, IMethodSymbol? entryPoint)
    {
        if (entryPoint is null) return false;
        if (SymbolEqualityComparer.Default.Equals(symbol, entryPoint)) return true;
        if (symbol is INamedTypeSymbol type && SymbolEqualityComparer.Default.Equals(type, entryPoint.ContainingType)) return true;

        return false;
    }

    private static bool HasWhitelistedAttribute(ISymbol symbol)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            var attrName = attr.AttributeClass?.Name;
            if (attrName != null && WhitelistedAttributeNames.Contains(attrName))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsSpecialMethodKind(ISymbol symbol)
    {
        if (symbol is not IMethodSymbol method) return false;

        return method.MethodKind switch
        {
            MethodKind.StaticConstructor => true,
            MethodKind.Destructor => true,
            MethodKind.PropertyGet => true,
            MethodKind.PropertySet => true,
            MethodKind.EventAdd => true,
            MethodKind.EventRemove => true,
            MethodKind.EventRaise => true,
            MethodKind.UserDefinedOperator => true,
            MethodKind.Conversion => true,
            _ => false
        };
    }

    /// <summary>
    /// Parameterlose private Konstruktoren in Klassen, deren sonstige Member ausschliesslich statisch sind,
    /// dienen der Verhinderung von Instanziierung (Utility-Pattern) und sind gewhitelistet.
    /// </summary>
    private static bool IsUtilityClassConstructor(ISymbol symbol)
    {
        if (symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor } ctor) return false;
        if (ctor.DeclaredAccessibility != Accessibility.Private || !ctor.Parameters.IsEmpty) return false;

        var containingType = ctor.ContainingType;
        if (containingType is null) return false;

        var otherMembers = containingType.GetMembers()
            .Where(m => m is not IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor })
            .ToList();

        return otherMembers.Count > 0 && otherMembers.All(m => m.IsStatic);
    }
}
