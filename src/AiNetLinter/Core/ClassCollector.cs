using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Metrics;

namespace AiNetLinter.Core;

/// <summary>
/// Hilfs-Walker zum Sammeln aller deklarierten Klassen und deren maximalen Komplexitäten.
/// </summary>
internal sealed class ClassCollector : CSharpSyntaxWalker
{
    public List<ClassInfo> Classes { get; } = new();
    private readonly string _filePath;
    private readonly List<string> _usings = new();

    public ClassCollector(string filePath)
    {
        _filePath = filePath;
    }

    public override void VisitUsingDirective(UsingDirectiveSyntax node)
    {
        if (node.Name != null)
        {
            _usings.Add(node.Name.ToString());
        }
        base.VisitUsingDirective(node);
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var maxComplexity = GetMaxMethodComplexity(node);
        var baseClass = GetBaseClass(node);

        Classes.Add(new ClassInfo
        {
            Name = node.Identifier.Text,
            FilePath = _filePath,
            LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            MaxCognitiveComplexity = maxComplexity,
            BaseClass = baseClass,
            Namespace = GetNamespace(node),
            Usings = _usings.ToArray(),
            HasTestMethods = CheckForTestMethods(node)
        });

        base.VisitClassDeclaration(node);
    }

    private static string? GetNamespace(ClassDeclarationSyntax node)
    {
        var parent = node.Parent;
        while (parent != null)
        {
            if (parent is BaseNamespaceDeclarationSyntax ns)
            {
                return ns.Name.ToString();
            }
            parent = parent.Parent;
        }
        return null;
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

    private static string? GetBaseClass(ClassDeclarationSyntax node)
    {
        if (node.BaseList == null) return null;
        if (node.BaseList.Types.Count == 0) return null;
        return node.BaseList.Types[0].Type.ToString();
    }
}

internal sealed class ClassInfo
{
    public required string Name { get; init; }
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required int MaxCognitiveComplexity { get; init; }
    public string? BaseClass { get; init; }
    public string? Namespace { get; init; }
    public required IReadOnlyList<string> Usings { get; init; }
    public required bool HasTestMethods { get; init; }
    public string FullyQualifiedName => string.IsNullOrEmpty(Namespace) ? Name : $"{Namespace}.{Name}";
}
