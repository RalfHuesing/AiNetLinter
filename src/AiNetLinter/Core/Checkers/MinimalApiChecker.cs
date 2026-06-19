#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.Checkers;

internal static class MinimalApiChecker
{
    internal static void Check(InvocationExpressionSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.EnforceMinimalApiAsParameters || ctx.IsTestFile) return;
        if (!IsMinimalApiMapInvocation(node)) return;

        foreach (var argument in node.ArgumentList.Arguments)
        {
            ReportMissingAsParameters(argument.Expression, ctx);
        }
    }

    private static bool IsMinimalApiMapInvocation(InvocationExpressionSyntax node)
    {
        if (node.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        var methodName = memberAccess.Name.Identifier.Text;
        return methodName.StartsWith("Map", StringComparison.Ordinal) && methodName.Length > 3;
    }

    private static void ReportMissingAsParameters(ExpressionSyntax expression, CheckerContext ctx)
    {
        var parameters = GetLambdaParameters(expression);
        if (parameters.Count <= ctx.Config.Metrics.MaxMethodParameterCount) return;

        foreach (var parameter in parameters)
        {
            if (!IsCompositeParameter(parameter) || HasAsParametersAttribute(parameter, ctx)) continue;
            AddViolation(parameter, parameters.Count, ctx);
        }
    }

    private static void AddViolation(ParameterSyntax parameter, int parameterCount, CheckerContext ctx) =>
        ctx.ReportViolation(parameter,
            nameof(ctx.Config.Global.EnforceMinimalApiAsParameters),
            $"Minimal-API-Endpunkt mit {parameterCount} Parametern: Composite-Typ '{parameter.Type}' benoetigt [AsParameters].",
            "Fuege [AsParameters] zum Composite-Parameter hinzu oder reduziere die Parameteranzahl.");

    private static IReadOnlyList<ParameterSyntax> GetLambdaParameters(ExpressionSyntax expression) =>
        expression switch
        {
            SimpleLambdaExpressionSyntax simple => [simple.Parameter],
            ParenthesizedLambdaExpressionSyntax paren => paren.ParameterList.Parameters.ToArray(),
            _ => []
        };

    private static bool IsCompositeParameter(ParameterSyntax parameter) =>
        parameter.Type is IdentifierNameSyntax or QualifiedNameSyntax;

    private static bool HasAsParametersAttribute(ParameterSyntax parameter, CheckerContext ctx) =>
        parameter.AttributeLists
            .SelectMany(list => list.Attributes)
            .Any(attr => IsAsParametersAttribute(attr, ctx));

    private static bool IsAsParametersAttribute(AttributeSyntax attribute, CheckerContext ctx)
    {
        var symbol = ctx.SemanticModel.GetSymbolInfo(attribute).Symbol?.ContainingType;
        if (symbol == null) return false;
        return symbol.Name == "AsParametersAttribute" &&
               symbol.ContainingNamespace?.ToDisplayString() == "Microsoft.AspNetCore.Http";
    }
}
