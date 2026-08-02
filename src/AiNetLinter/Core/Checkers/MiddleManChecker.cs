#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.Checkers;

internal static class MiddleManChecker
{
    internal static void Check(ClassDeclarationSyntax node, CheckerContext ctx)
    {
        if (ShouldSkipClass(node, ctx)) return;

        var includePrivate = ctx.Config.Global.MiddleManIncludePrivateMembers;

        // Alle relevanten Methoden und Properties ermitteln
        var members = node.Members
            .Where(m => IsRelevantMember(m, includePrivate))
            .ToList();

        if (members.Count < ctx.Config.Global.MiddleManMinMemberCount) return;

        int forwardingCount = 0;
        foreach (var member in members)
        {
            if (IsPureForwarder(member, node, ctx))
            {
                forwardingCount++;
            }
        }

        double ratio = (double)forwardingCount / members.Count;
        double maxRatio = ctx.Config.Global.MaxMiddleManForwardingRatio;

        if (ratio > maxRatio)
        {
            var className = node.Identifier.Text;
            ctx.ReportViolation(node, new ViolationDescription(
                "AvoidExcessiveMiddleMen",
                $"Die Klasse '{className}' hat ein hohes Weiterleitungs-Verhältnis ({ratio:P0} > {maxRatio:P0}).",
                $"Diese Klasse fungiert mit {forwardingCount} von {members.Count} Weiterleitungen primär als 'Middle Man' und erhöht die Indirektionstiefe. " +
                "Erwägen Sie, die Klasse aufzuteilen, Logik zu konsolidieren oder die Aufrufe direkt an die Collaborators zu richten."));
        }
    }

    private static bool ShouldSkipClass(ClassDeclarationSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.AvoidExcessiveMiddleMen) return true;

        // Statische Klassen werden ignoriert
        if (node.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))) return true;

        // Abstrakte Klassen werden ignoriert
        if (node.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword))) return true;

        var className = node.Identifier.Text;

        // Suffix-Ausnahme prüfen
        if (HasExemptSuffix(className, ctx)) return true;

        // Basisklassen-Ausnahme prüfen
        if (HasExemptBaseType(node, ctx)) return true;

        return false;
    }

    private static bool IsRelevantMember(MemberDeclarationSyntax member, bool includePrivate)
    {
        if (member is not (MethodDeclarationSyntax or PropertyDeclarationSyntax))
        {
            return false;
        }

        // Explizite Interface-Implementierungen ausschließen, da sie erzwungen sind
        if (member is MethodDeclarationSyntax method && method.ExplicitInterfaceSpecifier != null)
        {
            return false;
        }
        if (member is PropertyDeclarationSyntax prop && prop.ExplicitInterfaceSpecifier != null)
        {
            return false;
        }

        return includePrivate || IsNonPrivate(member);
    }

    private static bool IsNonPrivate(MemberDeclarationSyntax member)
    {
        var modifiers = member.Modifiers;
        if (modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)))
        {
            return false;
        }

        // Standardmäßig ohne Access-Modifier ist private
        if (!modifiers.Any(m =>
            m.IsKind(SyntaxKind.PublicKeyword) ||
            m.IsKind(SyntaxKind.InternalKeyword) ||
            m.IsKind(SyntaxKind.ProtectedKeyword)))
        {
            return false;
        }

        return true;
    }

    private static bool HasExemptSuffix(string className, CheckerContext ctx)
    {
        var suffixes = ctx.Config.Global.MiddleManExemptSuffixes;
        if (suffixes == null || suffixes.Count == 0) return false;
        return suffixes.Any(s => className.EndsWith(s, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPureForwarder(MemberDeclarationSyntax member, ClassDeclarationSyntax classNode, CheckerContext ctx)
    {
        var expr = GetForwardExpression(member);
        if (expr is null) return false;

        expr = UnwrapExpressions(expr);

        if (expr is InvocationExpressionSyntax invocation)
        {
            var target = UnwrapExpressions(invocation.Expression);
            return IsForwardingToExternal(target, classNode, ctx);
        }

        if (expr is MemberAccessExpressionSyntax memberAccess)
        {
            return IsForwardingToExternal(memberAccess, classNode, ctx);
        }

        return false;
    }

    private static ExpressionSyntax? GetForwardExpression(MemberDeclarationSyntax member)
    {
        if (member is MethodDeclarationSyntax method)
        {
            return GetMethodForwardExpression(method);
        }
        if (member is PropertyDeclarationSyntax prop)
        {
            return GetPropertyForwardExpression(prop);
        }
        return null;
    }

    private static ExpressionSyntax? GetMethodForwardExpression(MethodDeclarationSyntax method)
    {
        if (method.ExpressionBody is not null)
        {
            return method.ExpressionBody.Expression;
        }

        if (method.Body is not null && method.Body.Statements.Count == 1)
        {
            var stmt = method.Body.Statements[0];
            if (stmt is ReturnStatementSyntax ret)
            {
                return ret.Expression;
            }
            if (stmt is ExpressionStatementSyntax est)
            {
                return est.Expression;
            }
        }

        return null;
    }

    private static ExpressionSyntax? GetPropertyForwardExpression(PropertyDeclarationSyntax prop)
    {
        if (prop.ExpressionBody is not null)
        {
            return prop.ExpressionBody.Expression;
        }

        if (prop.AccessorList is not null && prop.AccessorList.Accessors.Count == 1)
        {
            var accessor = prop.AccessorList.Accessors[0];
            if (accessor.IsKind(SyntaxKind.GetAccessorDeclaration))
            {
                if (accessor.ExpressionBody is not null)
                {
                    return accessor.ExpressionBody.Expression;
                }
                if (accessor.Body?.Statements.Count == 1 && accessor.Body.Statements[0] is ReturnStatementSyntax ret)
                {
                    return ret.Expression;
                }
            }
        }

        return null;
    }

    private static ExpressionSyntax UnwrapExpressions(ExpressionSyntax expr)
    {
        while (true)
        {
            if (expr is ParenthesizedExpressionSyntax paren)
            {
                expr = paren.Expression;
            }
            else if (expr is AwaitExpressionSyntax awaitExpr)
            {
                expr = awaitExpr.Expression;
            }
            else if (expr is CastExpressionSyntax castExpr)
            {
                expr = castExpr.Expression;
            }
            else
            {
                break;
            }
        }
        return expr;
    }

    private static bool IsForwardingToExternal(ExpressionSyntax target, ClassDeclarationSyntax classNode, CheckerContext ctx)
    {
        var classSymbol = ctx.SemanticModel.GetDeclaredSymbol(classNode);
        if (classSymbol == null) return false;

        var symbolInfo = ctx.SemanticModel.GetSymbolInfo(target);
        var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
        if (symbol == null) return false;

        var containingType = symbol.ContainingType;
        if (containingType != null)
        {
            if (SymbolEqualityComparer.Default.Equals(containingType, classSymbol))
            {
                return false; // Lokale Methode/Property
            }

            var baseType = classSymbol.BaseType;
            while (baseType != null)
            {
                if (SymbolEqualityComparer.Default.Equals(containingType, baseType))
                {
                    return false; // Geerbte Methode/Property
                }
                baseType = baseType.BaseType;
            }
        }

        return true;
    }

    private static bool HasExemptBaseType(ClassDeclarationSyntax node, CheckerContext ctx)
    {
        var exemptTypes = ctx.Config.Global.MiddleManExemptBaseTypes;
        if (exemptTypes == null || exemptTypes.Count == 0) return false;

        var symbol = ctx.SemanticModel.GetDeclaredSymbol(node);
        if (symbol == null) return false;

        var current = symbol.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            if (exemptTypes.Contains(current.Name, StringComparer.OrdinalIgnoreCase)) return true;
            current = current.BaseType;
        }

        foreach (var iface in symbol.AllInterfaces)
            if (exemptTypes.Contains(iface.Name, StringComparer.OrdinalIgnoreCase)) return true;

        return false;
    }
}
