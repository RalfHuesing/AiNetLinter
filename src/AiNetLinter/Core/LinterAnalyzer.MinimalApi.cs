#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;

namespace AiNetLinter.Core;

/// <summary>
/// Minimal-API-[AsParameters]-Prüfung (opt-in).
/// </summary>
public sealed partial class LinterAnalyzer
{
    private void CheckMinimalApiAsParameters(InvocationExpressionSyntax node)
    {
        if (!_config.Global.EnforceMinimalApiAsParameters || _isTestFile)
        {
            return;
        }

        if (!IsMinimalApiMapInvocation(node))
        {
            return;
        }

        foreach (var argument in node.ArgumentList.Arguments)
        {
            ReportMissingAsParameters(argument.Expression);
        }
    }

    private static bool IsMinimalApiMapInvocation(InvocationExpressionSyntax node)
    {
        if (node.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var methodName = memberAccess.Name.Identifier.Text;
        return methodName.StartsWith("Map", StringComparison.Ordinal) && methodName.Length > 3;
    }

    private void ReportMissingAsParameters(ExpressionSyntax expression)
    {
        var parameters = GetLambdaParameters(expression);
        if (parameters.Count <= _config.Metrics.MaxMethodParameterCount)
        {
            return;
        }

        foreach (var parameter in parameters)
        {
            if (!IsCompositeParameter(parameter) || HasAsParametersAttribute(parameter))
            {
                continue;
            }

            AddAsParametersViolation(parameter, parameters.Count);
        }
    }

    private void AddAsParametersViolation(ParameterSyntax parameter, int parameterCount)
    {
        _violations.Add(new RuleViolation
        {
            FilePath = _filePath,
            LineNumber = GetLineNumber(parameter),
            RuleName = nameof(_config.Global.EnforceMinimalApiAsParameters),
            Details = $"Minimal-API-Endpunkt mit {parameterCount} Parametern: Composite-Typ '{parameter.Type}' benoetigt [AsParameters].",
            Guidance = "Fuege [AsParameters] zum Composite-Parameter hinzu oder reduziere die Parameteranzahl.",
        });
    }

    private static IReadOnlyList<ParameterSyntax> GetLambdaParameters(ExpressionSyntax expression)
    {
        return expression switch
        {
            SimpleLambdaExpressionSyntax simple => [simple.Parameter],
            ParenthesizedLambdaExpressionSyntax paren => paren.ParameterList.Parameters.ToArray(),
            _ => [],
        };
    }

    private static bool IsCompositeParameter(ParameterSyntax parameter) =>
        parameter.Type is IdentifierNameSyntax or QualifiedNameSyntax;

    private bool HasAsParametersAttribute(ParameterSyntax parameter) =>
        parameter.AttributeLists.SelectMany(list => list.Attributes).Any(IsAsParametersAttribute);

    private bool IsAsParametersAttribute(AttributeSyntax attribute)
    {
        var symbol = _semanticModel.GetSymbolInfo(attribute).Symbol?.ContainingType;
        if (symbol == null)
        {
            return false;
        }

        return symbol.Name == "AsParametersAttribute" &&
               symbol.ContainingNamespace?.ToDisplayString() == "Microsoft.AspNetCore.Http";
    }
}
