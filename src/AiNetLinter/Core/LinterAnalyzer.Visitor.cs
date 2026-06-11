using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Configuration;
using AiNetLinter.Models;
using AiNetLinter.Metrics;

namespace AiNetLinter.Core;

/// <summary>
/// Syntax-Walker-Implementierung für LinterAnalyzer (Besucher-Methoden).
/// </summary>
public sealed partial class LinterAnalyzer : CSharpSyntaxWalker
{
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

    public override void VisitUsingDirective(UsingDirectiveSyntax node)
    {
        if (node.Name != null)
        {
            CheckForbiddenNamespaceString(node.Name.ToString(), node);
        }
        base.VisitUsingDirective(node);
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
            Details = $"Der Namespace '{_currentNamespace}' darf nicht vom Namespace '{referencedNamespace}' abhängen (Referenz gefunden: '{node}').",
            Guidance = "Entferne die Abhängigkeit oder nutze Abstraktion/Events statt direkter Kopplung."
        });
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        CheckXmlDoc(node, node.Identifier.Text, "Klasse");
        CheckPascalCase(node.Identifier, "Klasse");

        if (_config.Global.EnforceSealedClasses && !IsSealedOrStaticOrAbstract(node))
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = nameof(_config.Global.EnforceSealedClasses),
                Details = $"Die Klasse '{node.Identifier.Text}' ist nicht als 'sealed' deklariert.",
                Guidance = "Füge den 'sealed' Modifikator zur Klassendeklaration hinzu, um unkontrollierte Vererbung zu verhindern."
            });
        }

        CheckValueObjectContract(node, node.Identifier.Text, isRecord: false);

        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol != null)
        {
            Classes.Add(new ClassInfo
            {
                Name = symbol.Name,
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
        base.VisitRecordDeclaration(node);
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        CheckXmlDoc(node, node.Identifier.Text, "Struct");
        CheckPascalCase(node.Identifier, "Struct");
        CheckValueObjectContract(node, node.Identifier.Text, isRecord: false);
        base.VisitStructDeclaration(node);
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        CheckXmlDoc(node, node.Identifier.Text, "Interface");
        CheckPascalCase(node.Identifier, "Interface");
        base.VisitInterfaceDeclaration(node);
    }

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        CheckPascalCase(node.Identifier, "Eigenschaft");
        base.VisitPropertyDeclaration(node);
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        CheckXmlDoc(node, node.Identifier.Text, "Methode");
        CheckPascalCase(node.Identifier, "Methode");
        
        bool isPublicMethod = node.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
        CheckSemanticNaming(node.ParameterList, isPublicMethod);

        var paramCount = node.ParameterList.Parameters.Count;
        if (paramCount > _config.Metrics.MaxMethodParameterCount)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = nameof(_config.Metrics.MaxMethodParameterCount),
                Details = $"Die Methode '{node.Identifier.Text}' hat {paramCount} Parameter (erlaubt sind maximal {_config.Metrics.MaxMethodParameterCount}).",
                Guidance = "Kapsle die Parameter in einen C# record (Parameter Object)."
            });
        }

        CheckMethodComplexities(node);
        CheckMethodLineCount(node);

        base.VisitMethodDeclaration(node);
    }

    private void CheckMethodLineCount(MethodDeclarationSyntax node)
    {
        var codeLineCount = MethodLineCounter.GetCodeLineCount(node);
        if (codeLineCount == 0)
        {
            return;
        }

        if (codeLineCount > _config.Metrics.MaxMethodLineCount)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = nameof(_config.Metrics.MaxMethodLineCount),
                Details = $"Die Methode '{node.Identifier.Text}' hat {codeLineCount} Codezeilen (erlaubt sind maximal {_config.Metrics.MaxMethodLineCount}, ohne Kommentare und Leerzeilen).",
                Guidance = "Lagere logische Abschnitte in kleinere Hilfsmethoden aus (Extract Method), um den Code für KI-Agenten besser editierbar zu halten."
            });
        }
    }

    private void CheckMethodComplexities(MethodDeclarationSyntax node)
    {
        var cyclomaticComplexity = ComplexityCalculator.GetCyclomaticComplexity(node);
        if (cyclomaticComplexity > _config.Metrics.MaxCyclomaticComplexity)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = nameof(_config.Metrics.MaxCyclomaticComplexity),
                Details = $"Die Methode '{node.Identifier.Text}' hat eine Zyklomatische Komplexität von {cyclomaticComplexity} (erlaubt sind maximal {_config.Metrics.MaxCyclomaticComplexity}).",
                Guidance = "Teile die Methode in kleinere Hilfsmethoden auf und reduziere Verzweigungen (ifs, Schleifen, logische Ketten)."
            });
        }

        var cognitiveComplexity = ComplexityCalculator.GetCognitiveComplexity(node);
        if (cognitiveComplexity > _config.Metrics.MaxCognitiveComplexity)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = nameof(_config.Metrics.MaxCognitiveComplexity),
                Details = $"Die Methode '{node.Identifier.Text}' hat eine Kognitive Komplexität von {cognitiveComplexity} (erlaubt sind maximal {_config.Metrics.MaxCognitiveComplexity}).",
                Guidance = CognitiveComplexityGuidance.Build(
                    node,
                    cognitiveComplexity,
                    _config.Metrics.MaxCognitiveComplexity),
            });
        }
    }

    public override void VisitParameter(ParameterSyntax node)
    {
        if (ShouldReportOutParameter(node))
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = nameof(_config.Global.AllowOutParameters),
                Details = $"Der Parameter '{node.Identifier.Text}' verwendet das verbotene 'out'-Schlüsselwort.",
                Guidance = "Verwende C#-Tuples oder Records für mehrere Rückgabewerte."
            });
        }

        base.VisitParameter(node);
    }

    private bool ShouldReportOutParameter(ParameterSyntax node)
    {
        if (_config.Global.AllowOutParameters)
        {
            return false;
        }

        if (!node.Modifiers.Any(SyntaxKind.OutKeyword))
        {
            return false;
        }

        return !IsAllowedTryPatternOut(node);
    }

    private bool IsAllowedTryPatternOut(ParameterSyntax node)
    {
        if (!_config.Global.AllowTryPatternOutParameters)
        {
            return false;
        }

        if (node.Parent?.Parent is not MethodDeclarationSyntax method)
        {
            return false;
        }

        if (!method.Identifier.Text.StartsWith("Try", StringComparison.Ordinal))
        {
            return false;
        }

        return method.ReturnType is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.BoolKeyword };
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

    public override void VisitCatchClause(CatchClauseSyntax node)
    {
        if (!_config.Global.EnforceNoSilentCatch || _isTestFile)
        {
            base.VisitCatchClause(node);
            return;
        }

        if (IsSwallowed(node) && !IsAllowedCancellationCatch(node))
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = nameof(_config.Global.EnforceNoSilentCatch),
                Details = "Stummes Abfangen (Silent Swallowing) einer Exception erkannt.",
                Guidance = "Wirf die Exception erneut (throw;) oder protokolliere sie, um Fehler im agentischen Loop sichtbar zu machen."
            });
        }

        base.VisitCatchClause(node);
    }

    private bool IsAllowedCancellationCatch(CatchClauseSyntax node)
    {
        if (!_config.Global.AllowCancellationShutdownCatch)
        {
            return false;
        }

        if (node.Declaration?.Type is not IdentifierNameSyntax { Identifier.Text: "OperationCanceledException" })
        {
            return false;
        }

        return node.Filter != null;
    }

    private static bool IsSwallowed(CatchClauseSyntax node)
    {
        if (node.Block.Statements.Count == 0)
        {
            return true;
        }

        var hasThrow = node.Block.DescendantNodes().OfType<ThrowStatementSyntax>().Any();
        var hasInvoke = node.Block.DescendantNodes().OfType<InvocationExpressionSyntax>().Any();
        return !hasThrow && !hasInvoke;
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
