using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;

namespace AiNetLinter.Core;

/// <summary>
/// Domain-specific partial class file handling scope rules such as variable shadowing and method overload limits.
/// </summary>
public sealed partial class LinterAnalyzer : CSharpSyntaxWalker
{
    public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
    {
        if (IsLocalVariable(node))
        {
            CheckVariableShadowing(node.Identifier, node);
        }
        base.VisitVariableDeclarator(node);
    }

    public override void VisitForEachStatement(ForEachStatementSyntax node)
    {
        CheckVariableShadowing(node.Identifier, node);
        base.VisitForEachStatement(node);
    }

    public override void VisitCatchDeclaration(CatchDeclarationSyntax node)
    {
        CheckVariableShadowing(node.Identifier, node);
        base.VisitCatchDeclaration(node);
    }

    public override void VisitSingleVariableDesignation(SingleVariableDesignationSyntax node)
    {
        CheckVariableShadowing(node.Identifier, node);
        base.VisitSingleVariableDesignation(node);
    }

    public override void VisitParameter(ParameterSyntax node)
    {
        CheckOutParameter(node);
        CheckVariableShadowing(node.Identifier, node);
        base.VisitParameter(node);
    }

    private void CheckVariableShadowing(SyntaxToken identifier, SyntaxNode node)
    {
        if (!_config.Global.EnforceNoVariableShadowing) return;
        var name = identifier.Text;
        if (string.IsNullOrEmpty(name)) return;

        var selfSymbol = _semanticModel.GetDeclaredSymbol(node);
        var symbols = _semanticModel.LookupSymbols(node.SpanStart, name: name);
        var shadowed = symbols.FirstOrDefault(s => !SymbolEqualityComparer.Default.Equals(s, selfSymbol) && IsShadowedSymbol(s));

        if (shadowed != null)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = "EnforceNoVariableShadowing",
                Details = $"Die Variable oder der Parameter '{name}' verdeckt ein Feld, eine Eigenschaft oder einen aeusseren Parameter '{shadowed.ToDisplayString()}'.",
                Guidance = "Benenne die Variable oder den Parameter um, um Namenskonflikte und Verwirrung bei KI-Agenten zu vermeiden."
            });
        }
    }

    private static bool IsShadowedSymbol(ISymbol symbol) =>
        symbol is IFieldSymbol or IPropertySymbol or IParameterSymbol or ILocalSymbol;

    private static bool IsLocalVariable(VariableDeclaratorSyntax node)
    {
        var grandparent = node.Parent?.Parent;
        return grandparent is not FieldDeclarationSyntax && grandparent is not EventFieldDeclarationSyntax;
    }

    private void CheckMethodOverloads(TypeDeclarationSyntax node)
    {
        var methods = node.Members.OfType<MethodDeclarationSyntax>();
        foreach (var group in methods.GroupBy(static m => m.Identifier.Text))
        {
            var count = group.Count();
            if (count > _config.Metrics.MaxMethodOverloads)
            {
                _violations.Add(new RuleViolation
                {
                    FilePath = _filePath,
                    LineNumber = GetLineNumber(group.First()),
                    RuleName = "MaxMethodOverloads",
                    Details = $"Der Typ '{node.Identifier.Text}' deklariert {count} Ueberladungen fuer die Methode '{group.Key}' (erlaubt sind maximal {_config.Metrics.MaxMethodOverloads}).",
                    Guidance = "Reduziere die Anzahl der Methodenueberladungen, indem du unterschiedliche Methodennamen waehlst oder optionale Parameter verwendest."
                });
            }
        }
    }
}
