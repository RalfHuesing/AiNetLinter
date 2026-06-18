#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Metrics;
using AiNetLinter.Models;

namespace AiNetLinter.Core.Checkers;

internal static class ArchitectureChecker
{
    internal static void CollectClassInfo(ClassDeclarationSyntax node, CheckerContext ctx)
    {
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(node);
        if (symbol == null) return;

        var footprintResult = AIContextFootprintCalculator.CalculateDetailed(
            symbol,
            ctx.Config.Metrics.FootprintIgnoreNamespacePrefixes,
            ctx.Config.Metrics.FootprintIgnoreTypeNames);

        ctx.Classes.Add(new ClassInfo
        {
            Name = string.IsNullOrWhiteSpace(symbol.Name) ? node.Identifier.Text : symbol.Name,
            FilePath = ctx.FilePath,
            LineNumber = SyntaxHelper.LineOf(node),
            MaxCognitiveComplexity = ComplexityChecker.GetMaxMethodComplexity(node),
            InheritanceDepth = GetInheritanceDepth(symbol, ctx),
            AIContextFootprint = footprintResult.TotalLines,
            AIContextFootprintDetails = footprintResult.TopDependencies,
            HasTestMethods = CheckForTestMethods(node, ctx),
            IsPartial = node.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)),
            IsStatic = node.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)),
            BaseTypeNames = GetBaseTypeNames(symbol),
            ProjectName = ctx.ProjectName
        });
    }

    internal static void CheckSealedClass(ClassDeclarationSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.EnforceSealedClasses) return;
        if (IsSealedOrStaticOrAbstract(node)) return;
        if (node.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)) && ctx.Config.Global.AllowUnsealedPartialClasses) return;
        if (HasExemptSuffix(node.Identifier.Text, ctx)) return;

        ctx.AddViolation(new RuleViolation
        {
            FilePath = ctx.FilePath,
            LineNumber = SyntaxHelper.LineOf(node),
            RuleName = nameof(ctx.Config.Global.EnforceSealedClasses),
            Details = $"Die Klasse '{node.Identifier.Text}' ist nicht als 'sealed' deklariert.",
            Guidance = "Fuege den 'sealed' Modifikator zur Klassendeklaration hinzu, um unkontrollierte Vererbung zu verhindern."
        });
    }

    internal static void CheckValueObjectContract(TypeDeclarationSyntax node, string name, bool isRecord, CheckerContext ctx)
    {
        if (!ctx.Config.Global.EnforceValueObjectContracts) return;
        if (!name.EndsWith("ValueObject")) return;

        if (!isRecord && !IsStructOrReadOnly(node))
        {
            ctx.AddViolation(new RuleViolation
            {
                FilePath = ctx.FilePath,
                LineNumber = SyntaxHelper.LineOf(node),
                RuleName = nameof(ctx.Config.Global.EnforceValueObjectContracts),
                Details = $"Das Value Object '{name}' ist als 'class' deklariert.",
                Guidance = $"Ersetze 'class' durch 'record' (z. B. 'public sealed record {name}(string Value)') oder 'readonly struct'. Records erzwingen Wert-Semantik und sind ohne zusaetzlichen Code unveraenderlich."
            });
        }

        foreach (var prop in node.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (prop.AccessorList != null && prop.AccessorList.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration)))
            {
                ctx.AddViolation(new RuleViolation
                {
                    FilePath = ctx.FilePath,
                    LineNumber = SyntaxHelper.LineOf(prop),
                    RuleName = nameof(ctx.Config.Global.EnforceValueObjectContracts),
                    Details = $"Das Value Object '{name}' enthaelt eine veraenderbare Eigenschaft '{prop.Identifier.Text}' (hat einen 'set'-Accessor).",
                    Guidance = "Entferne den 'set'-Accessor und benutze get-only oder 'init' fuer Eigenschaften in Value Objects."
                });
            }
        }
    }

    internal static void CheckForbiddenNamespace(string? referencedNamespace, SyntaxNode node, CheckerContext ctx)
    {
        if (string.IsNullOrEmpty(referencedNamespace)) return;

        foreach (var rule in ctx.Config.ForbiddenNamespaceDependencies)
        {
            if (rule.SourceNamespace == null || rule.TargetNamespace == null) continue;
            if (!NamespaceMatches(ctx.CurrentNamespace, rule.SourceNamespace)) continue;
            if (!NamespaceMatches(referencedNamespace, rule.TargetNamespace)) continue;

            ctx.AddViolation(new RuleViolation
            {
                FilePath = ctx.FilePath,
                LineNumber = SyntaxHelper.LineOf(node),
                RuleName = "ForbiddenNamespaceDependency",
                Details = $"Der Namespace '{ctx.CurrentNamespace}' darf nicht vom Namespace '{referencedNamespace}' abhaengen (Referenz gefunden: '{node}').",
                Guidance = "Entferne die Abhaengigkeit oder nutze Abstraktion/Events statt direkter Kopplung."
            });
        }
    }

    internal static void CheckPhantomNamespace(UsingDirectiveSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.DetectAndBanPhantomDependencies) return;
        if (ctx.IsTestFile) return;
        if (node.Name == null) return;

        var symbolInfo = ctx.SemanticModel.GetSymbolInfo(node.Name);
        if (symbolInfo.Symbol == null)
        {
            ctx.AddViolation(new RuleViolation
            {
                FilePath = ctx.FilePath,
                LineNumber = SyntaxHelper.LineOf(node),
                RuleName = "DetectAndBanPhantomDependencies",
                Details = $"Der importierte Namespace '{node.Name}' kann nicht aufgeloest werden. Ist die NuGet-Abhaengigkeit in der csproj deklariert?",
                Guidance = "Entferne das using-Statement oder fuege die entsprechende Projektreferenz/.csproj-Abhaengigkeit hinzu."
            });
        }
    }

    internal static void CheckDynamic(IdentifierNameSyntax node, CheckerContext ctx)
    {
        if (ctx.Config.Global.AllowDynamic) return;
        var typeInfo = ctx.SemanticModel.GetTypeInfo(node);
        if (typeInfo.Type?.TypeKind != TypeKind.Dynamic) return;

        ctx.AddViolation(new RuleViolation
        {
            FilePath = ctx.FilePath,
            LineNumber = SyntaxHelper.LineOf(node),
            RuleName = nameof(ctx.Config.Global.AllowDynamic),
            Details = "Die Verwendung des Typs 'dynamic' ist nicht gestattet.",
            Guidance = "Verwende stattdessen stark typisierte Schnittstellen, Klassen oder generische Typen."
        });
    }

    internal static void CheckForbiddenSymbolNamespace(IdentifierNameSyntax node, CheckerContext ctx)
    {
        SyntaxNode target = node;
        while (target.Parent is NameSyntax || target.Parent is MemberAccessExpressionSyntax)
            target = target.Parent;

        var symbol = ctx.SemanticModel.GetSymbolInfo(target).Symbol ?? ctx.SemanticModel.GetSymbolInfo(node).Symbol;
        if (symbol == null) return;

        string? ns;
        if (symbol is INamedTypeSymbol typeSymbol)
        {
            CheckForbiddenNamespace(typeSymbol.ContainingNamespace?.ToDisplayString(), node, ctx);
            return;
        }

        ns = symbol is INamespaceSymbol nsSymbol
            ? nsSymbol.ToDisplayString()
            : symbol.ContainingType?.ContainingNamespace?.ToDisplayString();

        CheckForbiddenNamespace(ns, node, ctx);
    }

    internal static void CheckPhantomReflection(InvocationExpressionSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.DetectAndBanPhantomDependencies) return;
        if (ctx.IsTestFile) return;

        var symbol = ctx.SemanticModel.GetSymbolInfo(node).Symbol;
        if (symbol == null) return;

        var containingType = symbol.ContainingType?.ToDisplayString() ?? "";
        var methodName = symbol.Name;

        if (!IsForbiddenReflectionCall(containingType, methodName)) return;

        ctx.AddViolation(new RuleViolation
        {
            FilePath = ctx.FilePath,
            LineNumber = SyntaxHelper.LineOf(node),
            RuleName = "DetectAndBanPhantomDependencies",
            Details = $"Die Verwendung von dynamischer Reflection '{containingType}.{methodName}' ist fuer KI-Lesbarkeit nicht gestattet.",
            Guidance = "Verwende statische Typ-Ausdruecke wie 'typeof(MyClass)' oder Generics, um die Compile-Zeit-Sicherheit zu wahren."
        });
    }

    internal static bool IsGeneratedCode(TypeDeclarationSyntax node, CheckerContext ctx)
    {
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(node);
        if (symbol == null) return false;
        return symbol.GetAttributes().Any(a =>
            a.AttributeClass?.Name == "GeneratedCodeAttribute" ||
            a.AttributeClass?.Name == "GeneratedCode");
    }

    private static bool IsForbiddenReflectionCall(string containingType, string methodName)
    {
        if (containingType == "System.Type" && methodName == "GetType") return true;
        if (containingType.StartsWith("System.Reflection.Assembly") && (methodName.StartsWith("Load") || methodName.StartsWith("LoadFrom"))) return true;
        return containingType == "System.Activator" && methodName == "CreateInstance";
    }

    internal static bool IsSealedOrStaticOrAbstract(ClassDeclarationSyntax node) =>
        node.Modifiers.Any(m => m.IsKind(SyntaxKind.SealedKeyword) || m.IsKind(SyntaxKind.StaticKeyword) || m.IsKind(SyntaxKind.AbstractKeyword));

    private static bool HasExemptSuffix(string className, CheckerContext ctx)
    {
        var suffixes = ctx.Config.Global.SealedClassExemptSuffixes;
        if (suffixes == null || suffixes.Count == 0) return false;
        return suffixes.Any(s => className.EndsWith(s, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsStructOrReadOnly(TypeDeclarationSyntax node)
    {
        if (node is StructDeclarationSyntax) return true;
        return node.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword));
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

    private static int GetInheritanceDepth(INamedTypeSymbol symbol, CheckerContext ctx)
    {
        int depth = 0;
        var current = symbol.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            if (!IsFrameworkBaseType(current, ctx)) depth++;
            if (depth > 20) return depth;
            current = current.BaseType;
        }
        return depth;
    }

    private static bool IsFrameworkBaseType(INamedTypeSymbol symbol, CheckerContext ctx)
    {
        var prefixes = ctx.Config.Metrics.InheritanceDepthFrameworkPrefixes;
        if (prefixes == null || prefixes.Count == 0) return false;
        var ns = symbol.ContainingNamespace?.ToDisplayString();
        if (string.IsNullOrEmpty(ns)) return false;

        foreach (var prefix in prefixes)
        {
            var normalized = prefix.EndsWith('.') ? prefix.Substring(0, prefix.Length - 1) : prefix;
            if (ns.Equals(normalized, StringComparison.OrdinalIgnoreCase) || ns.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool CheckForTestMethods(ClassDeclarationSyntax node, CheckerContext ctx) =>
        node.Members.OfType<MethodDeclarationSyntax>()
            .SelectMany(m => m.AttributeLists)
            .SelectMany(al => al.Attributes)
            .Any(attr => IsTestAttribute(attr, ctx));

    private static bool IsTestAttribute(AttributeSyntax attr, CheckerContext ctx)
    {
        var symbol = ctx.SemanticModel.GetSymbolInfo(attr).Symbol;
        var attrType = symbol?.ContainingType;
        if (attrType == null) return false;
        var ns = attrType.ContainingNamespace?.ToDisplayString();
        if (ns == null) return false;
        return ns.StartsWith("Xunit", StringComparison.OrdinalIgnoreCase)
            || ns.StartsWith("NUnit", StringComparison.OrdinalIgnoreCase)
            || ns.StartsWith("Microsoft.VisualStudio.TestTools.UnitTesting", StringComparison.OrdinalIgnoreCase);
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
}
