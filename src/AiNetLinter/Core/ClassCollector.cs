using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Metrics;

namespace AiNetLinter.Core;

/// <summary>
/// Hilfs-Walker zum Sammeln aller deklarierten Klassen und deren maximalen Komplexitäten mit Semantik.
/// </summary>
internal sealed class ClassCollector : CSharpSyntaxWalker
{
    public List<ClassInfo> Classes { get; } = new();
    private readonly string _filePath;
    private readonly SemanticModel _semanticModel;

    public ClassCollector(string filePath, SemanticModel semanticModel)
    {
        _filePath = filePath;
        _semanticModel = semanticModel;
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol != null)
        {
            var maxComplexity = GetMaxMethodComplexity(node);
            Classes.Add(new ClassInfo
            {
                Name = symbol.Name,
                FilePath = _filePath,
                LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                MaxCognitiveComplexity = maxComplexity,
                Symbol = symbol,
                HasTestMethods = CheckForTestMethods(node)
            });
        }

        base.VisitClassDeclaration(node);
    }

    private static bool CheckForTestMethods(ClassDeclarationSyntax node)
    {
        return node.Members.OfType<MethodDeclarationSyntax>()
            .SelectMany(m => m.AttributeLists)
            .SelectMany(al => al.Attributes)
            .Any(attr => attr.Name != null && IsTestAttribute(attr.Name.ToString()));
    }

    private static bool IsTestAttribute(string name)
    {
        return name.Contains("Fact") || name.Contains("Theory") || name.Contains("Test");
    }

    private static int GetMaxMethodComplexity(ClassDeclarationSyntax node)
    {
        var max = 0;
        foreach (var method in node.Members.OfType<MethodDeclarationSyntax>())
        {
            max = Math.Max(max, ComplexityCalculator.GetCognitiveComplexity(method));
        }
        return max;
    }
}

internal sealed class ClassInfo
{
    public required string Name { get; init; }
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required int MaxCognitiveComplexity { get; init; }
    public required INamedTypeSymbol Symbol { get; init; }
    public required bool HasTestMethods { get; init; }
}
