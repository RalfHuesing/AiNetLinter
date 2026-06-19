#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Core.Checkers;

internal static class InheritanceDepthChecker
{
    internal static int GetInheritanceDepth(INamedTypeSymbol symbol, CheckerContext ctx)
    {
        int depth = 0;
        var current = symbol.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            if (!IsFrameworkBaseType(current, ctx)) depth++;
            if (depth > 20) return depth;
            current = current.BaseType;
        }
        return depth;
    }

    private static bool IsFrameworkBaseType(INamedTypeSymbol symbol, CheckerContext ctx)
    {
        var prefixes = ctx.Config.Metrics.InheritanceDepthFrameworkPrefixes;
        if (prefixes == null || prefixes.Count == 0) return false;
        var ns = symbol.ContainingNamespace?.ToDisplayString();
        if (string.IsNullOrEmpty(ns)) return false;

        foreach (var prefix in prefixes)
        {
            var normalized = prefix.EndsWith('.') ? prefix.Substring(0, prefix.Length - 1) : prefix;
            if (ns.Equals(normalized, StringComparison.OrdinalIgnoreCase) || ns.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
