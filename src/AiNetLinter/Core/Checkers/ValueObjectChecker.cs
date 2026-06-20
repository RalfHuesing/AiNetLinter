#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.Checkers;

internal static class ValueObjectChecker
{
    internal static void Check(TypeDeclarationSyntax node, string name, bool isRecord, CheckerContext ctx)
    {
        if (!ctx.Config.Global.EnforceValueObjectContracts) return;
        if (!name.EndsWith("ValueObject")) return;

        if (!isRecord && !IsStructOrReadOnly(node))
        {
            ctx.ReportViolation(node, new ViolationDescription(
                nameof(ctx.Config.Global.EnforceValueObjectContracts),
                $"Das Value Object '{name}' ist als 'class' deklariert.",
                $"Ersetze 'class' durch 'record' (z. B. 'public sealed record {name}(string Value)') oder 'readonly struct'. Records erzwingen Wert-Semantik und sind ohne zusaetzlichen Code unveraenderlich."));
        }

        foreach (var prop in node.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (prop.AccessorList != null && prop.AccessorList.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration)))
            {
                ctx.ReportViolation(prop, new ViolationDescription(
                    nameof(ctx.Config.Global.EnforceValueObjectContracts),
                    $"Das Value Object '{name}' enthaelt eine veraenderbare Eigenschaft '{prop.Identifier.Text}' (hat einen 'set'-Accessor).",
                    "Entferne den 'set'-Accessor und benutze get-only oder 'init' fuer Eigenschaften in Value Objects."));
            }
        }
    }

    private static bool IsStructOrReadOnly(TypeDeclarationSyntax node)
    {
        if (node is StructDeclarationSyntax) return true;
        return node.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword));
    }
}
