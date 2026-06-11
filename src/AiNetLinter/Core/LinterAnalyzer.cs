using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Configuration;
using AiNetLinter.Models;
using AiNetLinter.Metrics;
using AiNetLinter.Suppression;

namespace AiNetLinter.Core;

/// <summary>
/// Analysiert eine C#-Syntaxstruktur und findet Regelverstöße mit Semantik.
/// </summary>
public sealed partial class LinterAnalyzer : CSharpSyntaxWalker
{
    private readonly string _filePath;
    private readonly SyntaxTree _tree;
    private readonly SemanticModel _semanticModel;
    private readonly LinterConfig _config;
    private readonly List<RuleViolation> _violations = new();
    private string _currentNamespace = "";
    private readonly bool _isTestFile;

    public List<ClassInfo> Classes { get; } = new();

    internal LinterAnalyzer(string filePath, SemanticModel semanticModel, LinterConfig config, bool isTestFile)
        : base(SyntaxWalkerDepth.Node)
    {
        _filePath = filePath;
        _tree = semanticModel.SyntaxTree;
        _semanticModel = semanticModel;
        _config = config;
        _isTestFile = isTestFile;
    }

    /// <summary>
    /// Analysiert ein Dokument und gibt alle gefundenen Verstöße zurück.
    /// </summary>
    public static IReadOnlyCollection<RuleViolation> Analyze(string filePath, SemanticModel semanticModel, LinterConfig config, bool isTestFile = false)
    {
        var analyzer = new LinterAnalyzer(filePath, semanticModel, config, isTestFile);
        analyzer.RunAnalysis();
        return analyzer._violations;
    }

    internal IReadOnlyCollection<RuleViolation> Violations => _violations;

    internal void RunAnalysis()
    {
        CheckLineCount();
        CheckNullableEnable();
        Visit(_tree.GetRoot());
        FilterSuppressedViolations();
    }

    private void CheckLineCount()
    {
        var text = _tree.GetText();
        var lineCount = text.Lines.Count;
        if (lineCount > _config.Metrics.MaxLineCount)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = 1,
                RuleName = nameof(_config.Metrics.MaxLineCount),
                Details = $"Die Datei hat {lineCount} Zeilen (erlaubt sind maximal {_config.Metrics.MaxLineCount}).",
                Guidance = "Teile die Datei in kleinere, logisch in sich geschlossene Klassen oder Vertical Slices auf."
            });
        }
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

    private void CheckValueObjectContract(TypeDeclarationSyntax node, string name, bool isRecord)
    {
        if (!ShouldCheckValueObject(name))
        {
            return;
        }

        if (!isRecord && !IsStructOrReadOnly(node))
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = nameof(_config.Global.EnforceValueObjectContracts),
                Details = $"Das Value Object '{name}' ist als 'class' deklariert.",
                Guidance = "Value Objects müssen als 'record' oder 'readonly struct' deklariert werden, um Unveränderlichkeit zu garantieren."
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
                    Details = $"Das Value Object '{name}' enthält eine veränderbare Eigenschaft '{prop.Identifier.Text}' (hat einen 'set'-Accessor).",
                    Guidance = "Entferne den 'set'-Accessor und benutze get-only oder 'init' für Eigenschaften in Value Objects."
                });
            }
        }
    }

    private void CheckXmlDoc(SyntaxNode node, string name, string kind)
    {
        if (ShouldSkipXmlDoc(node)) return;

        if (!HasXmlDocumentation(node))
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = nameof(_config.Global.EnforceXmlDocumentation),
                Details = $"Das öffentliche Element '{name}' ({kind}) hat keine XML-Dokumentation (/// <summary>).",
                Guidance = "Füge ein XML-Dokumentationskommentar hinzu, um die Absicht des Elements zu beschreiben."
            });
        }
    }

    private bool ShouldSkipXmlDoc(SyntaxNode node)
    {
        if (!_config.Global.EnforceXmlDocumentation) return true;
        if (_isTestFile) return true;
        if (node is MethodDeclarationSyntax method && method.Modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword)))
        {
            return true;
        }
        return !IsInPublicContext(node);
    }

    private static bool IsInPublicContext(SyntaxNode node)
    {
        var current = node;
        while (current != null)
        {
            if (!IsPublic(current))
            {
                return false;
            }
            current = current.Parent;
        }
        return true;
    }

    private static bool IsPublic(SyntaxNode node)
    {
        if (node is BaseTypeDeclarationSyntax typeDecl)
        {
            return typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
        }
        if (node is MethodDeclarationSyntax method)
        {
            return method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
        }
        return true;
    }

    private static bool HasXmlDocumentation(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia();
        return trivia.Any(t => 
            t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || 
            t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
    }

    private void CheckPascalCase(SyntaxToken identifier, string kind)
    {
        if (ShouldSkipPascalCase()) return;

        var name = identifier.Text;
        if (string.IsNullOrEmpty(name)) return;
        if (!char.IsUpper(name[0]))
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                RuleName = nameof(_config.Global.EnforcePascalCase),
                Details = $"Der Name '{name}' ({kind}) ist nicht in PascalCase geschrieben.",
                Guidance = "Ändere den ersten Buchstaben des Namens in einen Großbuchstaben."
            });
        }
    }

    private bool ShouldSkipPascalCase()
    {
        if (!_config.Global.EnforcePascalCase) return true;
        return _isTestFile;
    }

    private void CheckSemanticNaming(ParameterListSyntax parameterList, bool isPublicMethod)
    {
        if (ShouldSkipSemanticNaming(isPublicMethod)) return;

        var genericNames = GetForbiddenNames();
        foreach (var param in parameterList.Parameters)
        {
            CheckParameterSemantic(param, genericNames);
        }
    }

    private bool ShouldSkipSemanticNaming(bool isPublicMethod)
    {
        if (!_config.Global.EnforceSemanticNaming) return true;
        if (!isPublicMethod) return true;
        return _isTestFile;
    }

    private static HashSet<string> GetForbiddenNames()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "data", "temp", "obj", "val", "tmp", "item", "param"
        };
    }

    private void CheckParameterSemantic(ParameterSyntax param, HashSet<string> genericNames)
    {
        var name = param.Identifier.Text;
        if (genericNames.Contains(name))
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(param),
                RuleName = nameof(_config.Global.EnforceSemanticNaming),
                Details = $"Der Parameter '{name}' in einer öffentlichen Methode hat einen generischen, nicht-semantischen Namen.",
                Guidance = "Verwende einen aussagekräftigen Parameternamen, der die Absicht und den Typ des Parameters beschreibt."
            });
        }
    }

    private static bool IsSealedOrStaticOrAbstract(ClassDeclarationSyntax node)
    {
        return node.Modifiers.Any(m => 
            m.IsKind(SyntaxKind.SealedKeyword) || 
            m.IsKind(SyntaxKind.StaticKeyword) || 
            m.IsKind(SyntaxKind.AbstractKeyword));
    }

    private static int GetLineNumber(SyntaxNode node)
    {
        return node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    }

    private void CheckNullableEnable()
    {
        if (!_config.Global.EnforceNullableEnable) return;
        if (_isTestFile) return;

        var nullableContext = _semanticModel.GetNullableContext(0);
        var isEnabled = nullableContext.HasFlag(NullableContext.Enabled);

        if (!isEnabled)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = 1,
                RuleName = nameof(_config.Global.EnforceNullableEnable),
                Details = "Die Datei deklariert kein '#nullable enable' und hat keine global aktivierten Nullable-Prüfungen.",
                Guidance = "Füge '#nullable enable' am Anfang der Datei hinzu, oder aktiviere Nullable global in der csproj/Directory.Build.props."
            });
        }
    }

    private int GetMaxMethodComplexity(ClassDeclarationSyntax node)
    {
        var max = 0;
        foreach (var method in node.Members.OfType<MethodDeclarationSyntax>())
        {
            max = Math.Max(max, ComplexityCalculator.GetCognitiveComplexity(method));
        }
        return max;
    }

    private bool CheckForTestMethods(ClassDeclarationSyntax node)
    {
        return node.Members.OfType<MethodDeclarationSyntax>()
            .SelectMany(m => m.AttributeLists)
            .SelectMany(al => al.Attributes)
            .Any(IsTestAttribute);
    }

    private bool IsTestAttribute(AttributeSyntax attr)
    {
        var symbol = _semanticModel.GetSymbolInfo(attr).Symbol;
        var attrType = symbol?.ContainingType;
        if (attrType == null) return false;

        var ns = attrType.ContainingNamespace?.ToDisplayString();
        if (ns == null) return false;

        return ns.StartsWith("Xunit", StringComparison.OrdinalIgnoreCase) ||
               ns.StartsWith("NUnit", StringComparison.OrdinalIgnoreCase) ||
               ns.StartsWith("Microsoft.VisualStudio.TestTools.UnitTesting", StringComparison.OrdinalIgnoreCase);
    }

    private void FilterSuppressedViolations()
    {
        var activeViolations = _violations
            .Where(v => !IsSuppressed(v.RuleName ?? "", v.LineNumber))
            .ToList();
        _violations.Clear();
        _violations.AddRange(activeViolations);
    }

    private bool IsSuppressed(string ruleName, int lineNumber)
    {
        return SuppressionEvaluator.IsSuppressed(_tree.GetText().ToString(), ruleName, lineNumber);
    }
}
