using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Configuration;
using AiNetLinter.Models;

namespace AiNetLinter.Core;

/// <summary>
/// Domain-specific partial class file handling architectural rules such as namespace coupling, sealed classes, and Value Object contracts.
/// </summary>
public sealed partial class LinterAnalyzer : CSharpSyntaxWalker
{
    public override void VisitUsingDirective(UsingDirectiveSyntax node)
    {
        if (node.Name != null)
        {
            CheckForbiddenNamespaceString(node.Name.ToString(), node);
        }
        base.VisitUsingDirective(node);
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        CheckXmlDoc(node, node.Identifier.Text, "Klasse");
        CheckPascalCase(node.Identifier, "Klasse");
        CheckSealedClass(node);
        CheckValueObjectContract(node, node.Identifier.Text, isRecord: false);
        CheckMethodOverloads(node);
        CheckPrimaryConstructorDependencies(node);

        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol != null)
        {
            Classes.Add(new ClassInfo
            {
                Name = ResolveClassName(symbol, node.Identifier.Text),
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                MaxCognitiveComplexity = GetMaxMethodComplexity(node),
                Symbol = symbol,
                HasTestMethods = CheckForTestMethods(node),
                IsPartial = node.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)),
            });
        }

        base.VisitClassDeclaration(node);
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        CheckXmlDoc(node, node.Identifier.Text, "Record");
        CheckPascalCase(node.Identifier, "Record");
        CheckValueObjectContract(node, node.Identifier.Text, isRecord: true);
        CheckMethodOverloads(node);
        CheckPrimaryConstructorDependencies(node);
        base.VisitRecordDeclaration(node);
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        CheckXmlDoc(node, node.Identifier.Text, "Struct");
        CheckPascalCase(node.Identifier, "Struct");
        CheckValueObjectContract(node, node.Identifier.Text, isRecord: false);
        CheckMethodOverloads(node);
        CheckPrimaryConstructorDependencies(node);
        base.VisitStructDeclaration(node);
    }

    private void CheckSealedClass(ClassDeclarationSyntax node)
    {
        if (ShouldSkipSealedCheck(node)) return;

        _violations.Add(new RuleViolation
        {
            FilePath = _filePath,
            LineNumber = GetLineNumber(node),
            RuleName = nameof(_config.Global.EnforceSealedClasses),
            Details = $"Die Klasse '{node.Identifier.Text}' ist nicht als 'sealed' deklariert.",
            Guidance = "Fuege den 'sealed' Modifikator zur Klassendeklaration hinzu, um unkontrollierte Vererbung zu verhindern."
        });
    }

    private bool ShouldSkipSealedCheck(ClassDeclarationSyntax node)
    {
        if (!_config.Global.EnforceSealedClasses) return true;
        if (IsSealedOrStaticOrAbstract(node)) return true;

        bool isPartial = node.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
        return isPartial && _config.Global.AllowUnsealedPartialClasses;
    }

    private void CheckValueObjectContract(TypeDeclarationSyntax node, string name, bool isRecord)
    {
        if (!ShouldCheckValueObject(name)) return;

        if (!isRecord && !IsStructOrReadOnly(node))
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = nameof(_config.Global.EnforceValueObjectContracts),
                Details = $"Das Value Object '{name}' ist als 'class' deklariert.",
                Guidance = "Value Objects muessen als 'record' oder 'readonly struct' deklariert werden, um Unveraenderlichkeit zu garantieren."
            });
        }

        CheckValueObjectProperties(node, name);
    }

    private void CheckValueObjectProperties(TypeDeclarationSyntax node, string name)
    {
        foreach (var prop in node.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (prop.AccessorList != null && prop.AccessorList.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration)))
            {
                _violations.Add(new RuleViolation
                {
                    FilePath = _filePath,
                    LineNumber = GetLineNumber(prop),
                    RuleName = nameof(_config.Global.EnforceValueObjectContracts),
                    Details = $"Das Value Object '{name}' enthaelt eine veraenderbare Eigenschaft '{prop.Identifier.Text}' (hat einen 'set'-Accessor).",
                    Guidance = "Entferne den 'set'-Accessor und benutze get-only oder 'init' fuer Eigenschaften in Value Objects."
                });
            }
        }
    }

    private void CheckForbiddenNamespaceString(string? referencedNamespace, SyntaxNode node)
    {
        if (string.IsNullOrEmpty(referencedNamespace)) return;

        foreach (var rule in _config.ForbiddenNamespaceDependencies)
        {
            if (IsViolation(rule, referencedNamespace))
            {
                AddNamespaceViolation(node, referencedNamespace);
            }
        }
    }

    private bool IsViolation(NamespaceRule rule, string referencedNamespace)
    {
        if (rule.SourceNamespace == null || rule.TargetNamespace == null) return false;
        return _currentNamespace.StartsWith(rule.SourceNamespace) && 
               referencedNamespace.StartsWith(rule.TargetNamespace);
    }

    private void AddNamespaceViolation(SyntaxNode node, string referencedNamespace)
    {
        _violations.Add(new RuleViolation
        {
            FilePath = _filePath,
            LineNumber = GetLineNumber(node),
            RuleName = "ForbiddenNamespaceDependency",
            Details = $"Der Namespace '{_currentNamespace}' darf nicht vom Namespace '{referencedNamespace}' abhaengen (Referenz gefunden: '{node}').",
            Guidance = "Entferne die Abhaengigkeit oder nutze Abstraktion/Events statt direkter Kopplung."
        });
    }

    private bool ShouldCheckValueObject(string name)
    {
        if (!_config.Global.EnforceValueObjectContracts) return false;
        return name.EndsWith("ValueObject");
    }

    private static bool IsStructOrReadOnly(TypeDeclarationSyntax node)
    {
        if (node is StructDeclarationSyntax) return true;
        return node.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword));
    }

    private static string ResolveClassName(INamedTypeSymbol symbol, string identifierText) =>
        string.IsNullOrWhiteSpace(symbol.Name) ? identifierText : symbol.Name;

    public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
    {
        var prevNamespace = _currentNamespace;
        _currentNamespace = node.Name.ToString();
        base.VisitNamespaceDeclaration(node);
        _currentNamespace = prevNamespace;
    }

    public override void VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
    {
        _currentNamespace = node.Name.ToString();
        base.VisitFileScopedNamespaceDeclaration(node);
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (!_config.Global.AllowDynamic && IsDynamicType(node))
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = nameof(_config.Global.AllowDynamic),
                Details = "Die Verwendung des Typs 'dynamic' ist nicht gestattet.",
                Guidance = "Verwende stattdessen stark typisierte Schnittstellen, Klassen oder generische Typen."
            });
        }

        CheckForbiddenSymbolNamespace(node);
        base.VisitIdentifierName(node);
    }

    private void CheckForbiddenSymbolNamespace(IdentifierNameSyntax node)
    {
        var symbol = GetTargetSymbol(node);
        if (symbol == null) return;

        CheckSymbolNamespace(symbol, node);
    }

    private ISymbol? GetTargetSymbol(IdentifierNameSyntax node)
    {
        SyntaxNode target = node;
        while (target.Parent is NameSyntax || target.Parent is MemberAccessExpressionSyntax)
        {
            target = target.Parent;
        }

        return _semanticModel.GetSymbolInfo(target).Symbol ?? 
               _semanticModel.GetSymbolInfo(node).Symbol;
    }

    private void CheckSymbolNamespace(ISymbol symbol, SyntaxNode node)
    {
        if (symbol is INamedTypeSymbol typeSymbol)
        {
            CheckForbiddenNamespaceString(typeSymbol.ContainingNamespace?.ToDisplayString(), node);
            return;
        }
        
        var ns = symbol is INamespaceSymbol nsSymbol 
            ? nsSymbol.ToDisplayString() 
            : symbol.ContainingType?.ContainingNamespace?.ToDisplayString();
            
        CheckForbiddenNamespaceString(ns, node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        CheckMinimalApiAsParameters(node);
        base.VisitInvocationExpression(node);
    }

    private bool IsDynamicType(IdentifierNameSyntax node)
    {
        var typeInfo = _semanticModel.GetTypeInfo(node);
        return typeInfo.Type?.TypeKind == TypeKind.Dynamic;
    }
}
