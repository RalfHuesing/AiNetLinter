#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools.Verify.DeadCode;

/// <summary>Begrenzt die bekannte Reflectionmenge anhand unmittelbar zugeordneter Auswahlbedingungen.</summary>
internal sealed class DeadCodeReflectionSelection(DeadCodeBindingCall context)
{
    internal bool? Includes(ISymbol member)
    {
        var loop = context.Call.Ancestors().OfType<ForEachStatementSyntax>()
            .FirstOrDefault(node => node.Expression.Span.Contains(context.Call.Span));
        if (loop is null) return HasOperation(context.Call.Ancestors().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault());
        var conditions = loop.Statement.DescendantNodesAndSelf().OfType<IfStatementSyntax>().ToArray();
        if (conditions.Length == 0) return HasOperation(loop.Statement);
        if (conditions.Length != 1 || conditions[0].Else is not null) return null;
        var condition = conditions[0];
        if (condition.Statement.DescendantNodesAndSelf().OfType<ContinueStatementSyntax>().Any()) return null;
        return IncludesCondition(member, condition);
    }

    private bool? IncludesCondition(ISymbol member, IfStatementSyntax condition)
    {
        if (condition.Condition is not InvocationExpressionSyntax call
            || context.Source.Model.GetSymbolInfo(call).Symbol is not IMethodSymbol method
            || method.Name != "IsDefined" || method.ContainingNamespace.ToDisplayString() != "System.Reflection") return null;
        if (call.ArgumentList.Arguments.FirstOrDefault()?.Expression is not TypeOfExpressionSyntax attributeType) return null;
        var attribute = context.Source.Model.GetTypeInfo(attributeType.Type).Type;
        if (attribute is null) return null;
        return member.GetAttributes().Any(value => SymbolEqualityComparer.Default.Equals(value.AttributeClass, attribute))
            ? HasOperation(condition.Statement) : false;
    }

    private bool? HasOperation(SyntaxNode? region)
    {
        if (region is null) return null;
        foreach (var call in region.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            if (context.Source.Model.GetSymbolInfo(call).Symbol is not IMethodSymbol method) continue;
            if (method.ContainingNamespace.ToDisplayString() == "System.Reflection"
                && method.Name is "GetValue" or "SetValue" or "Invoke") return true;
        }
        return null;
    }
}
