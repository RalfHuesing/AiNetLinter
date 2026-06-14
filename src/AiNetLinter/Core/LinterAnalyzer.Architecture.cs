#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Configuration;
using AiNetLinter.Models;
using AiNetLinter.Metrics;

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
            CheckPhantomNamespace(node);
        }
        base.VisitUsingDirective(node);
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        if (_config.FileFilters.SkipGeneratedCodeAttribute && IsGeneratedCode(node))
        {
            return;
        }

        CheckXmlDoc(node, node.Identifier.Text, "Klasse");
        CheckPascalCase(node.Identifier, "Klasse");
        CheckSealedClass(node);
        CheckValueObjectContract(node, node.Identifier.Text, isRecord: false);
        CheckMethodOverloads(node);
        CheckPrimaryConstructorDependencies(node);
        CheckClassImmutability(node);

        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol != null)
        {
            var footprintResult = AIContextFootprintCalculator.CalculateDetailed(symbol);
            Classes.Add(new ClassInfo
            {
                Name = ResolveClassName(symbol, node.Identifier.Text),
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                MaxCognitiveComplexity = GetMaxMethodComplexity(node),
                InheritanceDepth = GetInheritanceDepth(symbol),
                AIContextFootprint = footprintResult.TotalLines,
                AIContextFootprintDetails = footprintResult.TopDependencies,
                HasTestMethods = CheckForTestMethods(node),
                IsPartial = node.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)),
                IsStatic = node.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)),
                BaseTypeNames = GetBaseTypeNames(symbol),
                ProjectName = _projectName,
            });
        }

        base.VisitClassDeclaration(node);
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        if (_config.FileFilters.SkipGeneratedCodeAttribute && IsGeneratedCode(node))
        {
            return;
        }

        CheckXmlDoc(node, node.Identifier.Text, "Record");
        CheckPascalCase(node.Identifier, "Record");
        CheckValueObjectContract(node, node.Identifier.Text, isRecord: true);
        CheckMethodOverloads(node);
        CheckPrimaryConstructorDependencies(node);
        base.VisitRecordDeclaration(node);
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        if (_config.FileFilters.SkipGeneratedCodeAttribute && IsGeneratedCode(node))
        {
            return;
        }

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
        if (isPartial && _config.Global.AllowUnsealedPartialClasses) return true;

        if (HasExemptSuffix(node.Identifier.Text)) return true;

        return false;
    }

    private bool HasExemptSuffix(string className)
    {
        var suffixes = _config.Global.SealedClassExemptSuffixes;
        if (suffixes == null || suffixes.Count == 0) return false;
        return suffixes.Any(s => className.EndsWith(s, StringComparison.OrdinalIgnoreCase));
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
        return NamespaceMatches(_currentNamespace, rule.SourceNamespace) && 
               NamespaceMatches(referencedNamespace, rule.TargetNamespace);
    }

    private static bool NamespaceMatches(string ns, string pattern)
    {
        if (string.IsNullOrEmpty(ns) || string.IsNullOrEmpty(pattern)) return false;

        if (pattern.Contains('*'))
        {
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(ns, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return ns.StartsWith(pattern, StringComparison.OrdinalIgnoreCase);
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

    private static IReadOnlyCollection<string> GetBaseTypeNames(INamedTypeSymbol? symbol)
    {
        if (symbol == null) return Array.Empty<string>();
        var names = new List<string>();

        var current = symbol.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            names.Add(current.Name);
            current = current.BaseType;
        }

        foreach (var iface in symbol.AllInterfaces)
            names.Add(iface.Name);

        return names.AsReadOnly();
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
        bool hasNamespaceRules = _config.ForbiddenNamespaceDependencies != null && _config.ForbiddenNamespaceDependencies.Count > 0;
        bool hasDynamicCheck = !_config.Global.AllowDynamic;

        if (hasNamespaceRules || hasDynamicCheck)
        {
            CheckIdentifierRules(node, hasNamespaceRules, hasDynamicCheck);
        }

        base.VisitIdentifierName(node);
    }

    private void CheckIdentifierRules(IdentifierNameSyntax node, bool hasNamespaceRules, bool hasDynamicCheck)
    {
        if (hasDynamicCheck && IsDynamicType(node))
        {
            ReportDynamicViolation(node);
        }

        if (hasNamespaceRules)
        {
            CheckForbiddenSymbolNamespace(node);
        }
    }

    private void ReportDynamicViolation(IdentifierNameSyntax node)
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
        CheckPhantomReflection(node);
        CheckTruncationHandling(node);
        base.VisitInvocationExpression(node);
    }

    private void CheckPhantomNamespace(UsingDirectiveSyntax node)
    {
        if (!_config.Global.DetectAndBanPhantomDependencies) return;
        if (_isTestFile) return;

        if (node.Name != null)
        {
            var symbolInfo = _semanticModel.GetSymbolInfo(node.Name);
            if (symbolInfo.Symbol == null)
            {
                _violations.Add(new RuleViolation
                {
                    FilePath = _filePath,
                    LineNumber = GetLineNumber(node),
                    RuleName = "DetectAndBanPhantomDependencies",
                    Details = $"Der importierte Namespace '{node.Name}' kann nicht aufgeloest werden. Ist die NuGet-Abhaengigkeit in der csproj deklariert?",
                    Guidance = "Entferne das using-Statement oder fuege die entsprechende Projektreferenz/.csproj-Abhaengigkeit hinzu."
                });
            }
        }
    }

    private void CheckPhantomReflection(InvocationExpressionSyntax node)
    {
        if (!_config.Global.DetectAndBanPhantomDependencies) return;
        if (_isTestFile) return;

        var symbol = _semanticModel.GetSymbolInfo(node).Symbol;
        if (symbol == null) return;

        CheckPhantomReflectionSymbol(node, symbol);
    }

    private void CheckPhantomReflectionSymbol(InvocationExpressionSyntax node, ISymbol symbol)
    {
        var containingTypeSymbol = symbol.ContainingType;
        var containingType = containingTypeSymbol != null ? containingTypeSymbol.ToDisplayString() : "";
        var methodName = symbol.Name;

        if (IsForbiddenReflectionCall(containingType, methodName))
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = "DetectAndBanPhantomDependencies",
                Details = $"Die Verwendung von dynamischer Reflection '{containingType}.{methodName}' ist fuer KI-Lesbarkeit nicht gestattet.",
                Guidance = "Verwende statische Typ-Ausdruecke wie 'typeof(MyClass)' oder Generics, um die Compile-Zeit-Sicherheit zu wahren."
            });
        }
    }

    private static bool IsForbiddenReflectionCall(string containingType, string methodName)
    {
        if (IsTypeGetType(containingType, methodName)) return true;
        if (IsAssemblyLoad(containingType, methodName)) return true;
        return IsActivatorCreate(containingType, methodName);
    }

    private static bool IsTypeGetType(string containingType, string methodName)
    {
        return containingType == "System.Type" && methodName == "GetType";
    }

    private static bool IsAssemblyLoad(string containingType, string methodName)
    {
        if (!containingType.StartsWith("System.Reflection.Assembly")) return false;
        return methodName.StartsWith("Load") || methodName.StartsWith("LoadFrom");
    }

    private static bool IsActivatorCreate(string containingType, string methodName)
    {
        return containingType == "System.Activator" && methodName == "CreateInstance";
    }

    private bool IsDynamicType(IdentifierNameSyntax node)
    {
        var typeInfo = _semanticModel.GetTypeInfo(node);
        return typeInfo.Type?.TypeKind == TypeKind.Dynamic;
    }

    private int GetInheritanceDepth(INamedTypeSymbol symbol)
    {
        int depth = 0;
        var current = symbol.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            if (!IsFrameworkBaseType(current))
            {
                depth++;
            }
            if (depth > 20) return depth;
            current = current.BaseType;
        }

        return depth;
    }

    private bool IsFrameworkBaseType(INamedTypeSymbol symbol)
    {
        var prefixes = _config.Metrics.InheritanceDepthFrameworkPrefixes;
        if (prefixes == null || prefixes.Count == 0) return false;

        var ns = symbol.ContainingNamespace?.ToDisplayString();
        if (string.IsNullOrEmpty(ns)) return false;

        foreach (var prefix in prefixes)
        {
            var normalizedPrefix = prefix.EndsWith('.') ? prefix.Substring(0, prefix.Length - 1) : prefix;
            if (ns.Equals(normalizedPrefix, StringComparison.OrdinalIgnoreCase) ||
                ns.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private bool IsGeneratedCode(TypeDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol == null) return false;

        return symbol.GetAttributes().Any(a =>
            a.AttributeClass?.Name == "GeneratedCodeAttribute" ||
            a.AttributeClass?.Name == "GeneratedCode");
    }
}
