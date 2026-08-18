#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.Checkers;

/// <summary>
/// Prueft, ob eine Klasse ueber Basisklasse oder Interface von einer konfigurierten Ausnahmeliste
/// erfasst wird — zentral statt separat in <see cref="ImmutabilityChecker"/> (Exempt-Basistypen fuer
/// <c>EnforceExplicitStateImmutability</c>) und <see cref="MiddleManChecker"/> (Exempt-Basistypen fuer
/// <c>AvoidExcessiveMiddleMen</c>) dupliziert — beide Regeln nutzen dieselbe Basistyp-/Interface-Kette,
/// nur die konfigurierte Ausnahmeliste unterscheidet sich.
/// </summary>
internal static class ExemptBaseTypeResolver
{
    internal static bool HasExemptBaseType(ClassDeclarationSyntax node, CheckerContext ctx, IReadOnlyCollection<string>? exemptTypes)
    {
        if (exemptTypes == null || exemptTypes.Count == 0) return false;

        var symbol = ctx.SemanticModel.GetDeclaredSymbol(node);
        if (symbol == null) return false;

        var current = symbol.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            if (exemptTypes.Contains(current.Name, StringComparer.OrdinalIgnoreCase)) return true;
            current = current.BaseType;
        }

        foreach (var iface in symbol.AllInterfaces)
            if (exemptTypes.Contains(iface.Name, StringComparer.OrdinalIgnoreCase)) return true;

        return false;
    }

    internal static bool HasExemptSuffix(string name, IReadOnlyCollection<string>? suffixes)
    {
        if (suffixes == null || suffixes.Count == 0) return false;
        return suffixes.Any(s => name.EndsWith(s, StringComparison.OrdinalIgnoreCase));
    }
}
