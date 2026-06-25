#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Metrics;
using AiNetLinter.Models;

namespace AiNetLinter.Core.Checkers;

internal static class ClassInfoCollector
{
    internal static void Collect(TypeDeclarationSyntax node, CheckerContext ctx)
    {
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(node);
        if (symbol == null) return;

        var footprintResult = AIContextFootprintCalculator.CalculateDetailed(
            symbol,
            ctx.Config.Metrics.FootprintIgnoreNamespacePrefixes,
            ctx.Config.Metrics.FootprintIgnoreTypeNames);

        ctx.Classes.Add(new ClassInfo
        {
            Name = string.IsNullOrWhiteSpace(symbol.Name) ? node.Identifier.Text : symbol.Name,
            FilePath = ctx.FilePath,
            LineNumber = node.LineOf(),
            MaxCognitiveComplexity = ComplexityChecker.GetMaxMethodComplexity(node),
            InheritanceDepth = InheritanceDepthChecker.GetInheritanceDepth(symbol, ctx),
            AIContextFootprint = footprintResult.TotalLines,
            AIContextFootprintDetails = footprintResult.TopDependencies,
            HasTestMethods = TestAttributeDetector.CheckForTestMethods(node, ctx),
            IsPartial = node.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)),
            IsStatic = node.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)),
            BaseTypeNames = GetBaseTypeNames(symbol),
            ProjectName = ctx.ProjectName
        });
    }

    private static IReadOnlyCollection<string> GetBaseTypeNames(INamedTypeSymbol? symbol)
    {
        if (symbol == null) return Array.Empty<string>();
        var names = new List<string>();

        var current = symbol.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            names.Add(current.Name);
            current = current.BaseType;
        }

        foreach (var iface in symbol.AllInterfaces)
            names.Add(iface.Name);

        return names.AsReadOnly();
    }
}
