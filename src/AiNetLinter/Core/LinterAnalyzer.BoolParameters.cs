#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;

namespace AiNetLinter.Core;

/// <summary>
/// Prüft MaxBoolParameterCount: zu viele bool-Parameter in öffentlichen Methoden und Konstruktoren.
/// </summary>
public sealed partial class LinterAnalyzer : CSharpSyntaxWalker
{
    internal void CheckBoolParameterCountForMethod(MethodDeclarationSyntax node)
    {
        bool isPrivateOrProtected = IsPrivateOrProtected(node.Modifiers);
        CheckBoolParameterCount(node.ParameterList, node.Identifier.Text, node, isPrivateOrProtected);
    }

    internal void CheckBoolParameterCountForConstructor(ConstructorDeclarationSyntax node)
    {
        bool isPrivateOrProtected = IsPrivateOrProtected(node.Modifiers);
        CheckBoolParameterCount(node.ParameterList, node.Identifier.Text, node, isPrivateOrProtected);
    }

    private void CheckBoolParameterCount(
        ParameterListSyntax paramList,
        string memberName,
        SyntaxNode node,
        bool isPrivateOrProtected)
    {
        var limit = _config.Metrics.MaxBoolParameterCount;
        if (limit <= 0) return;

        if (isPrivateOrProtected && _config.Metrics.MaxBoolParameterCountAllowPrivate) return;

        var exemptPrefixes = _config.Metrics.MaxBoolParameterCountExemptMethodPrefixes;
        foreach (var prefix in exemptPrefixes)
        {
            if (memberName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return;
        }

        var boolCount = CountBoolParameters(paramList);
        if (boolCount > limit)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = nameof(_config.Metrics.MaxBoolParameterCount),
                Details = $"'{memberName}' hat {boolCount} bool-Parameter (erlaubt: {limit}). Bool-Parameter sind an der Call-Site opak: 'DoWork(true, false)' trägt keine semantische Information.",
                Guidance = "Fasse die bool-Parameter in ein Parameter-Object zusammen: 'sealed record WorkOptions(bool EnableX, bool EnableY)' — damit werden Call-Sites selbsterklärend."
            });
        }
    }

    private int CountBoolParameters(ParameterListSyntax paramList)
    {
        var count = 0;
        foreach (var param in paramList.Parameters)
        {
            if (param.Type != null && IsBoolType(param.Type))
                count++;
        }
        return count;
    }

    private bool IsBoolType(TypeSyntax type)
    {
        var typeInfo = _semanticModel.GetTypeInfo(type);
        var t = typeInfo.Type;
        if (t == null) return false;

        if (t.SpecialType == SpecialType.System_Boolean) return true;

        if (t is INamedTypeSymbol named
            && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1
            && named.TypeArguments[0].SpecialType == SpecialType.System_Boolean)
            return true;

        return false;
    }

    private static bool IsPrivateOrProtected(SyntaxTokenList modifiers) =>
        modifiers.Any(SyntaxKind.PrivateKeyword) || modifiers.Any(SyntaxKind.ProtectedKeyword);
}
