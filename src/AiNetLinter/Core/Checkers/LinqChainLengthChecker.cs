#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.Checkers;

internal static class LinqChainLengthChecker
{
    internal static void Check(InvocationExpressionSyntax node, CheckerContext ctx)
    {
        var limit = ctx.Config.Metrics.MaxLinqChainLength;
        if (limit <= 0) return;

        // Nur die Wurzel einer Kette prüfen (nicht jeden inneren Knoten)
        if (IsNestedLinqCall(node)) return;

        var chainLength = CountLinqChain(node, ctx.Config.Metrics.LinqMethodNames);
        if (chainLength <= limit) return;

        ctx.ReportViolation(node,
            LinterRuleIds.MaxLinqChainLength,
            $"LINQ-Kette hat {chainLength} Methoden (erlaubt: {limit}).",
            BuildGuidance(chainLength, limit));
    }

    /// <summary>
    /// Zählt die LINQ-Methoden in einer Kette.
    /// Walk: Ausgehend vom äußersten Aufruf die Expression-Kette nach innen.
    /// </summary>
    private static int CountLinqChain(
        InvocationExpressionSyntax root,
        IReadOnlyCollection<string> linqNames)
    {
        var count = 0;
        SyntaxNode current = root;

        while (current is InvocationExpressionSyntax invocation
            && invocation.Expression is MemberAccessExpressionSyntax access)
        {
            var name = access.Name.Identifier.Text;
            if (!IsLinqMethod(name, linqNames)) break;

            count++;
            current = access.Expression;
        }

        return count;
    }

    /// <summary>
    /// Prüft ob dieser Knoten selbst ein innerer LINQ-Aufruf ist
    /// (d.h. ob er als Expression in einem äußeren InvocationExpression vorkommt).
    /// Wenn ja, wird er übersprungen — nur die äußerste Kette zählt.
    /// </summary>
    private static bool IsNestedLinqCall(InvocationExpressionSyntax node)
    {
        if (node.Parent is MemberAccessExpressionSyntax parentAccess
            && parentAccess.Expression == node
            && parentAccess.Parent is InvocationExpressionSyntax)
            return true;
        return false;
    }

    private static bool IsLinqMethod(string name, IReadOnlyCollection<string> linqNames) =>
        linqNames.Contains(name, StringComparer.Ordinal);

    private static string BuildGuidance(int actual, int limit) =>
        $"Eine LINQ-Kette mit {actual} Methoden erzeugt sequenzielle kognitive Last, die weder " +
        $"zyklomatische noch kognitive Komplexitaet erfasst. " +
        $"Alternativen: (1) Zwischenergebnis in benannte Variable extrahieren und Kette aufteilen. " +
        $"(2) Komplex-Teile in private Methoden mit sprechenden Namen auslagern " +
        $"(z. B. 'FilterActiveOrders()', 'RankByRevenue()'). " +
        $"(3) Query-Syntax statt Method-Syntax fuer mehrstufige Abfragen verwenden wenn lesbarkeit wichtiger ist als Kompaktheit.";
}
