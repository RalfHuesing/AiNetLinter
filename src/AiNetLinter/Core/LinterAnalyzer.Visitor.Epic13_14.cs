using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;

namespace AiNetLinter.Core;

/// <summary>
/// Syntax-Walker-Implementierung für Epic 13 & Epic 14 (Scope- & Zustands-Leitplanken sowie Kopplung & Semantik).
/// </summary>
public sealed partial class LinterAnalyzer : CSharpSyntaxWalker
{
    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        CheckConstructorDependencies(node);
        base.VisitConstructorDeclaration(node);
    }

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

    public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        CheckParameterReassignment(node.Left);
        RegisterFieldWrite(node.Left);
        base.VisitAssignmentExpression(node);
    }

    public override void VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
    {
        if (node.IsKind(SyntaxKind.PostIncrementExpression) || node.IsKind(SyntaxKind.PostDecrementExpression))
        {
            CheckParameterReassignment(node.Operand);
            RegisterFieldWrite(node.Operand);
        }
        base.VisitPostfixUnaryExpression(node);
    }

    public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
    {
        if (node.IsKind(SyntaxKind.PreIncrementExpression) || node.IsKind(SyntaxKind.PreDecrementExpression))
        {
            CheckParameterReassignment(node.Operand);
            RegisterFieldWrite(node.Operand);
        }
        base.VisitPrefixUnaryExpression(node);
    }

    public override void VisitArgument(ArgumentSyntax node)
    {
        if (node.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword) || node.RefOrOutKeyword.IsKind(SyntaxKind.RefKeyword))
        {
            CheckParameterReassignment(node.Expression);
            RegisterFieldWrite(node.Expression);
        }
        base.VisitArgument(node);
    }

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        AnalyzePrivateFields(node);
        base.VisitFieldDeclaration(node);
    }

    public override void VisitLiteralExpression(LiteralExpressionSyntax node)
    {
        CheckMagicValue(node);
        base.VisitLiteralExpression(node);
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

    private void CheckPrimaryConstructorDependencies(TypeDeclarationSyntax node)
    {
        if (node.ParameterList == null) return;
        var count = node.ParameterList.Parameters.Count;
        if (count > _config.Metrics.MaxConstructorDependencies)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = "MaxConstructorDependencies",
                Details = $"Der Primaerkonstruktor hat {count} Parameter (erlaubt sind maximal {_config.Metrics.MaxConstructorDependencies}).",
                Guidance = "Reduziere die Anzahl der Abhaengigkeiten, indem du den Typ in kleinere Klassen aufteilst."
            });
        }
    }

    private void CheckConstructorDependencies(ConstructorDeclarationSyntax node)
    {
        var count = node.ParameterList.Parameters.Count;
        if (count > _config.Metrics.MaxConstructorDependencies)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = "MaxConstructorDependencies",
                Details = $"Der Konstruktor hat {count} Parameter (erlaubt sind maximal {_config.Metrics.MaxConstructorDependencies}).",
                Guidance = "Reduziere die Anzahl der Abhaengigkeiten (Constructor Injection), indem du die Klasse in kleinere Services aufteilst."
            });
        }
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

    private void CheckParameterReassignment(ExpressionSyntax expression)
    {
        if (!_config.Global.EnforceReadonlyParameters) return;

        var symbol = _semanticModel.GetSymbolInfo(expression).Symbol;
        if (symbol is IParameterSymbol parameter && parameter.RefKind != RefKind.Out)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(expression),
                RuleName = "EnforceReadonlyParameters",
                Details = $"Der Parameter '{parameter.Name}' wird innerhalb der Methode neu zugewiesen.",
                Guidance = "Behandle Parameter als readonly. Nutze stattdessen eine lokale Variable, um den geaenderten Wert zu speichern."
            });
        }
    }

    private void RegisterFieldWrite(ExpressionSyntax expression)
    {
        if (!_config.Global.EnforceReadonlyFields) return;

        var symbol = _semanticModel.GetSymbolInfo(expression).Symbol as IFieldSymbol;
        if (symbol == null || !_privateFieldsToAnalyze.Contains(symbol)) return;

        if (!IsInsideConstructorOfDeclaringType(expression, symbol.ContainingType))
        {
            _fieldsModifiedOutsideConstructor.Add(symbol);
        }
    }

    private bool IsInsideConstructorOfDeclaringType(SyntaxNode node, INamedTypeSymbol declaringType)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (IsConstructorOf(current, declaringType)) return true;
            if (current is TypeDeclarationSyntax or NamespaceDeclarationSyntax or CompilationUnitSyntax) return false;
            current = current.Parent;
        }
        return false;
    }

    private bool IsConstructorOf(SyntaxNode node, INamedTypeSymbol declaringType)
    {
        if (node is not ConstructorDeclarationSyntax constructorDecl) return false;
        var symbol = _semanticModel.GetDeclaredSymbol(constructorDecl);
        return symbol != null && SymbolEqualityComparer.Default.Equals(symbol.ContainingType, declaringType);
    }

    private void CheckMagicValue(LiteralExpressionSyntax node)
    {
        if (!_config.Global.EnforceNoMagicValues) return;
        if (IsMagicValue(node))
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = "EnforceNoMagicValues",
                Details = $"Magischer Wert '{node.ToString()}' im Code gefunden.",
                Guidance = "Deklariere den Wert als 'const' oder 'static readonly' Feld, oder nutze ein 'enum', um die Semantik explizit zu benennen."
            });
        }
    }

    private bool IsMagicValue(LiteralExpressionSyntax node)
    {
        if (!IsTargetLiteral(node)) return false;
        if (IsExceptionValue(node)) return false;
        if (IsConstDeclaration(node)) return false;
        return IsInsideBody(node);
    }

    private static bool IsTargetLiteral(LiteralExpressionSyntax node)
    {
        var kind = node.Kind();
        return kind == SyntaxKind.NumericLiteralExpression || kind == SyntaxKind.StringLiteralExpression;
    }

    private static bool IsExceptionValue(LiteralExpressionSyntax node)
    {
        var kind = node.Kind();
        if (kind == SyntaxKind.StringLiteralExpression) return node.Token.ValueText == "";
        if (kind == SyntaxKind.NumericLiteralExpression) return IsExceptionNumeric(node.Token.Value);
        return false;
    }

    private static bool IsExceptionNumeric(object? value)
    {
        if (value == null) return false;
        try
        {
            var d = System.Convert.ToDouble(value);
            return d == 0.0 || d == 1.0;
        }
        catch (System.Exception ignored)
        {
            _ = ignored;
            return false;
        }
    }

    private static bool IsInsideBody(SyntaxNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (IsBodyContainer(current)) return true;
            current = current.Parent;
        }
        return false;
    }

    private static bool IsBodyContainer(SyntaxNode node)
    {
        if (node is not BlockSyntax and not ArrowExpressionClauseSyntax) return false;
        var p = node.Parent;
        return p is MethodDeclarationSyntax or ConstructorDeclarationSyntax or AccessorDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax;
    }

    private static bool IsConstDeclaration(LiteralExpressionSyntax node)
    {
        var parent = node.Parent;
        while (parent != null)
        {
            var (isConst, shouldBreak) = IsConstStatement(parent);
            if (isConst) return true;
            if (shouldBreak) break;
            parent = parent.Parent;
        }
        return false;
    }

    private static (bool IsConst, bool ShouldBreak) IsConstStatement(SyntaxNode node)
    {
        var shouldBreak = node is BlockSyntax or MethodDeclarationSyntax or ConstructorDeclarationSyntax;
        if (node is LocalDeclarationStatementSyntax localDecl)
        {
            return (localDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)), shouldBreak);
        }
        if (node is FieldDeclarationSyntax fieldDecl)
        {
            return (fieldDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)), shouldBreak);
        }
        return (false, shouldBreak);
    }

    private void AnalyzePrivateFields(FieldDeclarationSyntax node)
    {
        if (!_config.Global.EnforceReadonlyFields) return;
        if (!node.Modifiers.Any(SyntaxKind.PrivateKeyword)) return;

        foreach (var variable in node.Declaration.Variables)
        {
            RegisterPrivateFieldSymbol(variable);
        }
    }

    private void RegisterPrivateFieldSymbol(VariableDeclaratorSyntax variable)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
        if (symbol == null || symbol.IsReadOnly || symbol.IsConst) return;

        _privateFieldsToAnalyze.Add(symbol);
    }
}
