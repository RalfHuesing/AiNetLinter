#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.Checkers;

internal static class DynamicTypeChecker
{
    internal static void Check(IdentifierNameSyntax node, CheckerContext ctx)
    {
        if (ctx.Config.Global.AllowDynamic) return;
        var typeInfo = ctx.SemanticModel.GetTypeInfo(node);
        if (typeInfo.Type?.TypeKind != TypeKind.Dynamic) return;

        ctx.ReportViolation(node,
            nameof(ctx.Config.Global.AllowDynamic),
            "Die Verwendung des Typs 'dynamic' ist nicht gestattet.",
            "Verwende stattdessen stark typisierte Schnittstellen, Klassen oder generische Typen.");
    }
}
