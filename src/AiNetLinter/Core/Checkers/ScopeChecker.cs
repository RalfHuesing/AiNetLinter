#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.Checkers;

internal static class ScopeChecker
{
    private static readonly HashSet<string> PrimitiveNames = new(StringComparer.Ordinal)
    {
        "Int32", "Int64", "Int16", "String", "Boolean",
        "Double", "Single", "Decimal", "Char", "Byte", "Guid"
    };

    private static readonly HashSet<SpecialType> PrimitiveSpecialTypes = new()
    {
        SpecialType.System_Int32, SpecialType.System_Int64, SpecialType.System_String,
        SpecialType.System_Boolean, SpecialType.System_Double, SpecialType.System_Single,
        SpecialType.System_Decimal, SpecialType.System_Char, SpecialType.System_Byte
    };

    internal static void CheckMethodOverloads(TypeDeclarationSyntax node, CheckerContext ctx)
    {
        if (ctx.IsTestFile) return;

        var methods = node.Members.OfType<MethodDeclarationSyntax>().ToList();
        var groups = methods.GroupBy(static m => m.Identifier.Text);
        foreach (var group in groups)
            CheckMethodGroup(node, group.Key, group.ToList(), ctx);
    }

    internal static void CheckNamespaceDirectoryMapping(CheckerContext ctx)
    {
        if (ctx.IsTestFile) return;

        var relativePath = GetRelativePath(ctx.FilePath);
        if (string.IsNullOrEmpty(relativePath) || relativePath == ".") return;

        var pathParts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        CheckDirectoryDepth(pathParts, ctx);
        CheckNamespaceMappingRule(pathParts, relativePath, ctx);
    }

    private static void CheckMethodGroup(TypeDeclarationSyntax node, string methodName, List<MethodDeclarationSyntax> groupMethods, CheckerContext ctx)
    {
        var count = groupMethods.Count;
        if (count > ctx.Config.Metrics.MaxMethodOverloads)
        {
            ctx.ReportViolation(groupMethods[0], new ViolationDescription(
                nameof(ctx.Config.Metrics.MaxMethodOverloads),
                $"Der Typ '{node.Identifier.Text}' deklariert {count} Ueberladungen fuer die Methode '{methodName}' (erlaubt sind maximal {ctx.Config.Metrics.MaxMethodOverloads}).",
                $"Reduziere die Anzahl der Ueberladungen: Waehle unterschiedliche, sprechende Methodennamen oder fasse optionale Parameter in einem Parameter-Object zusammen (z. B. 'sealed record {methodName}Options(...)' mit Properties fuer alle variablen Teile)."));
        }

        if (ctx.Config.Global.PreventContextDependentOverloads && count > 1)
            CheckPrimitiveOverloadConflicts(groupMethods, ctx);
    }

    private static void CheckPrimitiveOverloadConflicts(List<MethodDeclarationSyntax> methodGroup, CheckerContext ctx)
    {
        for (int i = 0; i < methodGroup.Count; i++)
        {
            for (int j = i + 1; j < methodGroup.Count; j++)
            {
                if (ArePrimitiveOverloadConflicts(methodGroup[i], methodGroup[j], ctx))
                {
                    ctx.ReportViolation(methodGroup[j], new ViolationDescription(
                        nameof(ctx.Config.Global.PreventContextDependentOverloads),
                        $"Die Methode '{methodGroup[j].Identifier.Text}' steht im Konflikt mit einer Überladung in Zeile {SyntaxHelper.LineOf(methodGroup[i])}. Beide unterscheiden sich nur in primitiven Typen.",
                        "Verwende explizite Methodennamen (z.B. 'ProcessInt' statt 'Process'), um Mehrdeutigkeiten für KI-Agenten zu vermeiden."));
                }
            }
        }
    }

    private static bool ArePrimitiveOverloadConflicts(MethodDeclarationSyntax a, MethodDeclarationSyntax b, CheckerContext ctx)
    {
        var paramsA = a.ParameterList.Parameters;
        var paramsB = b.ParameterList.Parameters;
        if (paramsA.Count != paramsB.Count) return false;

        var hasPrimitiveDiff = false;
        for (int i = 0; i < paramsA.Count; i++)
        {
            var typeA = GetParameterType(paramsA[i], ctx);
            var typeB = GetParameterType(paramsB[i], ctx);
            if (typeA == null || typeB == null) return false;
            if (SymbolEqualityComparer.Default.Equals(typeA, typeB)) continue;
            if (!IsBothPrimitive(typeA, typeB)) return false;
            hasPrimitiveDiff = true;
        }
        return hasPrimitiveDiff;
    }

    private static ITypeSymbol? GetParameterType(ParameterSyntax param, CheckerContext ctx)
    {
        if (param.Type == null) return null;
        return ctx.SemanticModel.GetTypeInfo(param.Type).Type;
    }

    private static bool IsBothPrimitive(ITypeSymbol a, ITypeSymbol b) =>
        IsPrimitiveType(a) && IsPrimitiveType(b);

    private static bool IsPrimitiveType(ITypeSymbol symbol) =>
        PrimitiveNames.Contains(symbol.Name) || PrimitiveSpecialTypes.Contains(symbol.SpecialType);

    private static void CheckDirectoryDepth(string[] pathParts, CheckerContext ctx)
    {
        if (pathParts.Length <= ctx.Config.Metrics.MaxDirectoryDepth) return;

        ctx.ReportViolationAtLine(1, new ViolationDescription(
            nameof(ctx.Config.Metrics.MaxDirectoryDepth),
            $"Die Dateitiefe betraegt {pathParts.Length} Ordner (erlaubt sind maximal {ctx.Config.Metrics.MaxDirectoryDepth} ab csproj).",
            "Verflache die Projektstruktur und nutze Feature-Ordner statt tiefer Hierarchien, um KIs die Navigation zu erleichtern."));
    }

    private static void CheckNamespaceMappingRule(string[] pathParts, string relativePath, CheckerContext ctx)
    {
        if (!ctx.Config.Global.EnforceNamespaceDirectoryMapping) return;

        var namespaceDeclaration = ctx.SemanticModel.SyntaxTree.GetRoot().DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        if (namespaceDeclaration == null) return;

        var declaredNamespace = namespaceDeclaration.Name.ToString();
        var ignoredSegments = ctx.Config.Global.NamespaceDirectoryMappingIgnorePathSegments ?? Array.Empty<string>();
        var relevantParts = pathParts
            .Where(p => !ignoredSegments.Contains(p, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (relevantParts.Length == 0) return;

        bool matches = ctx.Config.Global.NamespaceDirectoryMappingMode switch
        {
            "suffix-match" => MatchesSuffix(declaredNamespace, relevantParts, ctx.Config.Global.NamespaceDirectoryMappingRequiredTrailingSegments),
            "contains-all" => MatchesContainsAll(declaredNamespace, relevantParts),
            _ => MatchesExact(declaredNamespace, relevantParts)
        };

        if (!matches)
        {
            var expectedSuffix = string.Join(".", relevantParts);
            ctx.ReportViolation(namespaceDeclaration, new ViolationDescription(
                nameof(ctx.Config.Global.EnforceNamespaceDirectoryMapping),
                $"Der Namespace '{declaredNamespace}' stimmt nicht mit dem physischen Ordnerpfad '{relativePath}' ueberein (Modus: {ctx.Config.Global.NamespaceDirectoryMappingMode}).",
                $"Passe den Namespace an, sodass er '.{expectedSuffix}' enthaelt, oder verschiebe die Datei."));
        }
    }

    private static bool MatchesExact(string ns, string[] parts)
    {
        var suffix = string.Join(".", parts);
        return ns.Equals(suffix, StringComparison.OrdinalIgnoreCase) ||
               ns.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesSuffix(string ns, string[] parts, int requiredTrailing)
    {
        if (parts.Length == 0) return true;
        var trailing = parts.TakeLast(Math.Min(requiredTrailing, parts.Length)).ToArray();
        var suffix = string.Join(".", trailing);
        return ns.Equals(suffix, StringComparison.OrdinalIgnoreCase) ||
               ns.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesContainsAll(string ns, string[] parts) =>
        parts.All(p => ns.Contains(p, StringComparison.OrdinalIgnoreCase));

    private static string? GetRelativePath(string filePath)
    {
        var fileDirectory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(fileDirectory)) return null;
        var projectDir = FindProjectDirectory(fileDirectory);
        if (string.IsNullOrEmpty(projectDir)) return null;
        return Path.GetRelativePath(projectDir, fileDirectory);
    }

    private static string FindProjectDirectory(string startDir)
    {
        var current = startDir;
        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.GetFiles(current, "*.csproj").Any()) return current;
            current = Path.GetDirectoryName(current);
        }
        return "";
    }
}
