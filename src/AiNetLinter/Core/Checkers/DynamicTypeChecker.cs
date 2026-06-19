#nullable enable

using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;

namespace AiNetLinter.Core.Checkers;

internal static class DynamicTypeChecker
{
    internal static void Check(IdentifierNameSyntax node, CheckerContext ctx)
    {
        if (ctx.Config.Global.AllowDynamic) return;
        var typeInfo = ctx.SemanticModel.GetTypeInfo(node);
        if (typeInfo.Type?.TypeKind != TypeKind.Dynamic) return;

        ctx.AddViolation(new RuleViolation
        {
            FilePath = ctx.FilePath,
            LineNumber = SyntaxHelper.LineOf(node),
            RuleName = nameof(ctx.Config.Global.AllowDynamic),
            Details = "Die Verwendung des Typs 'dynamic' ist nicht gestattet.",
            Guidance = "Verwende stattdessen stark typisierte Schnittstellen, Klassen oder generische Typen."
        });
    }
}
