#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;

namespace AiNetLinter.Core;

/// <summary>
/// Domain-specific partial class file handling safety rules such as truncation checks for I/O operations.
/// </summary>
public sealed partial class LinterAnalyzer : CSharpSyntaxWalker
{
    private void CheckTruncationHandling(InvocationExpressionSyntax node)
    {
        if (!_config.Global.RequireExplicitTruncationHandling) return;
        if (_config.Global.AllowedEmptyReads) return;
        if (_isTestFile) return;

        var symbol = _semanticModel.GetSymbolInfo(node).Symbol;
        if (symbol == null) return;

        CheckTruncationSymbol(node, symbol);
    }

    private void CheckTruncationSymbol(InvocationExpressionSyntax node, ISymbol symbol)
    {
        var containingType = symbol.ContainingType;
        var typeName = containingType != null ? containingType.Name : "";
        var methodName = symbol.Name;

        if (IsReadOperation(typeName, methodName))
        {
            CheckTruncationGuard(node, methodName, typeName);
        }
    }

    private void CheckTruncationGuard(InvocationExpressionSyntax node, string methodName, string typeName)
    {
        if (IsGuardOrCheckPresentForInvocation(node)) return;

        _violations.Add(new RuleViolation
        {
            FilePath = _filePath,
            LineNumber = GetLineNumber(node),
            RuleName = "RequireExplicitTruncationHandling",
            Details = $"Der I/O-Leseaufruf '{methodName}' von '{typeName}' besitzt keine unmittelbare Validierung der Laenge oder Vollstaendigkeit (Truncation-Schutz).",
            Guidance = "Prüfe die Anzahl gelesener Bytes/Zeichen (z.B. '> 0' oder 'Length') oder ob die Rückgabe leer ist (z.B. string.IsNullOrEmpty). Beispiel:\n" +
                       "var json = await response.Content.ReadAsStringAsync(cancellationToken);\n" +
                       "if (string.IsNullOrEmpty(json))\n" +
                       "{\n" +
                       "    throw new InvalidOperationException(\"Response body was empty.\");\n" +
                       "}"
        });
    }

    private static bool IsReadOperation(string typeName, string methodName)
    {
        if (typeName == "File")
            return IsFileReadMethod(methodName);

        if (typeName == "HttpClient")
            return IsHttpClientReadMethod(methodName);

        if (IsStreamOrReader(typeName))
            return IsStreamReadMethod(methodName);

        return false;
    }

    private static bool IsFileReadMethod(string methodName) =>
        methodName.StartsWith("Read") || methodName.StartsWith("Open");

    private static bool IsHttpClientReadMethod(string methodName) =>
        methodName == "GetStringAsync" || methodName == "GetByteArrayAsync";

    private static bool IsStreamReadMethod(string methodName) =>
        methodName.StartsWith("Read") || methodName == "CopyToAsync" || methodName == "ReadLine";

    private static bool IsStreamOrReader(string typeName) =>
        typeName.Contains("Stream")
        || typeName == "HttpContent"
        || typeName == "TextReader"
        || typeName == "BinaryReader";

    private static bool IsGuardOrCheckPresentForInvocation(InvocationExpressionSyntax invocation)
    {
        if (IsInsideCondition(invocation)) return true;
        var declarator = FindEnclosingVariableDeclarator(invocation);
        return declarator != null && IsVariableCheckedInBlock(declarator);
    }

    private static VariableDeclaratorSyntax? FindEnclosingVariableDeclarator(SyntaxNode node)
    {
        var current = node.Parent;
        while (current != null && current is not StatementSyntax)
        {
            if (current is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator })
                return declarator;
            current = current.Parent;
        }
        return null;
    }

    private static bool IsVariableCheckedInBlock(VariableDeclaratorSyntax declarator)
    {
        var varName = declarator.Identifier.Text;
        var enclosingBlock = declarator.Ancestors().OfType<BlockSyntax>().FirstOrDefault();
        if (enclosingBlock == null) return false;

        var references = enclosingBlock.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(id => id.Identifier.Text == varName && id.SpanStart > declarator.Span.End);

        return references.Any(IsInsideCondition);
    }

    private static bool IsInsideCondition(SyntaxNode node)
    {
        var current = node.Parent;
        while (current != null && current is not BlockSyntax)
        {
            if (IsConditionNode(current)) return true;
            current = current.Parent;
        }
        return false;
    }

    private static bool IsConditionNode(SyntaxNode node)
    {
        return node is IfStatementSyntax || 
               node is BinaryExpressionSyntax || 
               node is ConditionalExpressionSyntax ||
               node is SwitchStatementSyntax;
    }
}
