#nullable enable

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
        if (_isTestFile) return;

        var methods = node.Members.OfType<MethodDeclarationSyntax>().ToList();
        var groups = methods.GroupBy(static m => m.Identifier.Text);
        foreach (var group in groups)
        {
            CheckMethodGroup(node, group.Key, group.ToList());
        }
    }

    private void CheckMethodGroup(TypeDeclarationSyntax node, string methodName, List<MethodDeclarationSyntax> groupMethods)
    {
        var count = groupMethods.Count;
        if (count > _config.Metrics.MaxMethodOverloads)
        {
            AddMaxOverloadsViolation(node, methodName, groupMethods[0], count);
        }

        if (_config.Global.PreventContextDependentOverloads && count > 1)
        {
            CheckPrimitiveOverloadConflicts(groupMethods);
        }
    }

    private void AddMaxOverloadsViolation(TypeDeclarationSyntax node, string methodName, MethodDeclarationSyntax firstMethod, int count)
    {
        _violations.Add(new RuleViolation
        {
            FilePath = _filePath,
            LineNumber = GetLineNumber(firstMethod),
            RuleName = "MaxMethodOverloads",
            Details = $"Der Typ '{node.Identifier.Text}' deklariert {count} Ueberladungen fuer die Methode '{methodName}' (erlaubt sind maximal {_config.Metrics.MaxMethodOverloads}).",
            Guidance = "Reduziere die Anzahl der Ueberladungen, indem du unterschiedliche, sprechende Methodennamen waehlst."
        });
    }

    private void CheckPrimitiveOverloadConflicts(List<MethodDeclarationSyntax> methodGroup)
    {
        for (int i = 0; i < methodGroup.Count; i++)
        {
            CheckSinglePrimitiveOverloadConflict(methodGroup, i);
        }
    }

    private void CheckSinglePrimitiveOverloadConflict(List<MethodDeclarationSyntax> methodGroup, int startIndex)
    {
        var methodA = methodGroup[startIndex];
        for (int j = startIndex + 1; j < methodGroup.Count; j++)
        {
            var methodB = methodGroup[j];
            if (ArePrimitiveOverloadConflicts(methodA, methodB))
            {
                AddPrimitiveOverloadConflictViolation(methodA, methodB);
            }
        }
    }

    private void AddPrimitiveOverloadConflictViolation(MethodDeclarationSyntax methodA, MethodDeclarationSyntax methodB)
    {
        _violations.Add(new RuleViolation
        {
            FilePath = _filePath,
            LineNumber = GetLineNumber(methodB),
            RuleName = "PreventContextDependentOverloads",
            Details = $"Die Methode '{methodB.Identifier.Text}' steht im Konflikt mit einer Überladung in Zeile {GetLineNumber(methodA)}. Beide unterscheiden sich nur in primitiven Typen.",
            Guidance = "Verwende explizite Methodennamen (z.B. 'ProcessInt' statt 'Process'), um Mehrdeutigkeiten für KI-Agenten zu vermeiden."
        });
    }

    private bool ArePrimitiveOverloadConflicts(MethodDeclarationSyntax a, MethodDeclarationSyntax b)
    {
        var paramsA = a.ParameterList.Parameters;
        var paramsB = b.ParameterList.Parameters;

        if (paramsA.Count != paramsB.Count) return false;

        return CheckPrimitiveDifferences(paramsA, paramsB);
    }

    private bool CheckPrimitiveDifferences(SeparatedSyntaxList<ParameterSyntax> paramsA, SeparatedSyntaxList<ParameterSyntax> paramsB)
    {
        var hasPrimitiveDiff = false;
        for (int i = 0; i < paramsA.Count; i++)
        {
            var diffResult = CompareParameterTypes(paramsA[i], paramsB[i]);
            if (diffResult == ParameterDiff.NotPrimitive) return false;
            if (diffResult == ParameterDiff.PrimitiveDiff)
            {
                hasPrimitiveDiff = true;
            }
        }
        return hasPrimitiveDiff;
    }

    private enum ParameterDiff
    {
        SameType,
        PrimitiveDiff,
        NotPrimitive
    }

    private ParameterDiff CompareParameterTypes(ParameterSyntax paramA, ParameterSyntax paramB)
    {
        var typeA = GetParameterType(paramA);
        var typeB = GetParameterType(paramB);
        if (typeA == null || typeB == null) return ParameterDiff.NotPrimitive;

        if (SymbolEqualityComparer.Default.Equals(typeA, typeB)) return ParameterDiff.SameType;

        return IsBothPrimitive(typeA, typeB) ? ParameterDiff.PrimitiveDiff : ParameterDiff.NotPrimitive;
    }

    private ITypeSymbol? GetParameterType(ParameterSyntax param)
    {
        if (param.Type == null) return null;
        return _semanticModel.GetTypeInfo(param.Type).Type;
    }

    private static bool IsBothPrimitive(ITypeSymbol a, ITypeSymbol b)
    {
        return IsPrimitiveType(a) && IsPrimitiveType(b);
    }

    private static bool IsPrimitiveType(ITypeSymbol symbol)
    {
        if (IsPrimitiveName(symbol.Name)) return true;
        return IsPrimitiveSpecialType(symbol.SpecialType);
    }

    private static readonly HashSet<string> PrimitiveNames = new(StringComparer.Ordinal)
    {
        "Int32", "Int64", "Int16", "String", "Boolean", 
        "Double", "Single", "Decimal", "Char", "Byte", "Guid"
    };

    private static readonly HashSet<SpecialType> PrimitiveSpecialTypes = new()
    {
        SpecialType.System_Int32,
        SpecialType.System_Int64,
        SpecialType.System_String,
        SpecialType.System_Boolean,
        SpecialType.System_Double,
        SpecialType.System_Single,
        SpecialType.System_Decimal,
        SpecialType.System_Char,
        SpecialType.System_Byte
    };

    private static bool IsPrimitiveName(string name) => PrimitiveNames.Contains(name);

    private static bool IsPrimitiveSpecialType(SpecialType specialType) => PrimitiveSpecialTypes.Contains(specialType);

    private void CheckNamespaceDirectoryMapping()
    {
        if (_isTestFile) return;

        var relativePath = GetRelativePath();
        if (string.IsNullOrEmpty(relativePath)) return;
        if (relativePath == ".") return;

        var pathParts = relativePath.Split(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);

        CheckDirectoryDepth(pathParts);
        CheckNamespaceMappingRule(pathParts, relativePath);
    }

    private string? GetRelativePath()
    {
        var fileDirectory = System.IO.Path.GetDirectoryName(_filePath);
        if (string.IsNullOrEmpty(fileDirectory)) return null;

        var projectDir = FindProjectDirectory(fileDirectory);
        if (string.IsNullOrEmpty(projectDir)) return null;

        return System.IO.Path.GetRelativePath(projectDir, fileDirectory);
    }

    private void CheckDirectoryDepth(string[] pathParts)
    {
        if (pathParts.Length <= _config.Metrics.MaxDirectoryDepth) return;

        _violations.Add(new RuleViolation
        {
            FilePath = _filePath,
            LineNumber = 1,
            RuleName = "MaxDirectoryDepth",
            Details = $"Die Dateitiefe betraegt {pathParts.Length} Ordner (erlaubt sind maximal {_config.Metrics.MaxDirectoryDepth} ab csproj).",
            Guidance = "Verflache die Projektstruktur und nutze Feature-Ordner statt tiefer Hierarchien, um KIs die Navigation zu erleichtern."
        });
    }

    private void CheckNamespaceMappingRule(string[] pathParts, string relativePath)
    {
        if (!_config.Global.EnforceNamespaceDirectoryMapping) return;

        var namespaceDeclaration = _tree.GetRoot().DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        if (namespaceDeclaration == null) return;

        var declaredNamespace = namespaceDeclaration.Name.ToString();

        // Relevante Pfad-Teile bestimmen (ignorierte Segmente entfernen)
        var ignoredSegments = _config.Global.NamespaceDirectoryMappingIgnorePathSegments
            ?? Array.Empty<string>();
        var relevantParts = pathParts
            .Where(p => !ignoredSegments.Contains(p, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (relevantParts.Length == 0) return; // Edge Case: Alle Segmente werden ignoriert -> kein Verstoß

        bool matches = _config.Global.NamespaceDirectoryMappingMode switch
        {
            "suffix-match" => MatchesSuffix(declaredNamespace, relevantParts,
                _config.Global.NamespaceDirectoryMappingRequiredTrailingSegments),
            "contains-all" => MatchesContainsAll(declaredNamespace, relevantParts),
            _ => MatchesExact(declaredNamespace, relevantParts) // "exact" (Default)
        };

        if (!matches)
        {
            var expectedSuffix = string.Join(".", relevantParts);
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(namespaceDeclaration),
                RuleName = "EnforceNamespaceDirectoryMapping",
                Details = $"Der Namespace '{declaredNamespace}' stimmt nicht mit dem " +
                          $"physischen Ordnerpfad '{relativePath}' ueberein " +
                          $"(Modus: {_config.Global.NamespaceDirectoryMappingMode}).",
                Guidance = $"Passe den Namespace an, sodass er '.{expectedSuffix}' enthaelt, " +
                           $"oder verschiebe die Datei."
            });
        }
    }

    private static bool MatchesExact(string ns, string[] parts)
    {
        var suffix = string.Join(".", parts);
        return ns.Equals(suffix, StringComparison.OrdinalIgnoreCase) || 
               ns.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesSuffix(string ns, string[] parts, int requiredTrailing)
    {
        if (parts.Length == 0) return true;
        var trailing = parts.TakeLast(Math.Min(requiredTrailing, parts.Length)).ToArray();
        var suffix = string.Join(".", trailing);
        return ns.Equals(suffix, StringComparison.OrdinalIgnoreCase) || 
               ns.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesContainsAll(string ns, string[] parts)
    {
        return parts.All(p => ns.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private static string FindProjectDirectory(string startDir)
    {
        var current = startDir;
        while (!string.IsNullOrEmpty(current))
        {
            if (System.IO.Directory.GetFiles(current, "*.csproj").Any())
            {
                return current;
            }
            current = System.IO.Path.GetDirectoryName(current);
        }
        return "";
    }
}
