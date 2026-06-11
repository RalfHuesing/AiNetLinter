using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Configuration;
using AiNetLinter.Models;
using AiNetLinter.Metrics;

namespace AiNetLinter.Core;

/// <summary>
/// Analysiert eine C#-Syntaxstruktur und findet Regelverstöße.
/// </summary>
public sealed partial class LinterAnalyzer : CSharpSyntaxWalker
{
    private readonly string _filePath;
    private readonly string _fileContent;
    private readonly LinterConfig _config;
    private readonly List<RuleViolation> _violations = new();
    private string _currentNamespace = "";

    private LinterAnalyzer(string filePath, string fileContent, LinterConfig config)
        : base(SyntaxWalkerDepth.Node)
    {
        _filePath = filePath;
        _fileContent = fileContent;
        _config = config;
    }

    /// <summary>
    /// Analysiert eine Datei und gibt alle gefundenen Verstöße zurück.
    /// </summary>
    public static IReadOnlyCollection<RuleViolation> Analyze(string filePath, string fileContent, LinterConfig config)
    {
        var analyzer = new LinterAnalyzer(filePath, fileContent, config);
        analyzer.RunAnalysis();
        return analyzer._violations;
    }

    private void RunAnalysis()
    {
        CheckLineCount();

        var tree = CSharpSyntaxTree.ParseText(_fileContent);
        var root = tree.GetRoot();
        
        CheckNullableEnable(root);
        
        Visit(root);
    }

    private void CheckLineCount()
    {
        var lineCount = _fileContent.Split('\n').Length;
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
        if (IsTestFile()) return true;
        if (node is MethodDeclarationSyntax method && method.Modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword)))
        {
            return true;
        }
        return !AnalyzerHelpers.IsInPublicContext(node);
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
        return IsTestFile();
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
        return IsTestFile();
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

    private bool IsTestFile()
    {
        return AnalyzerHelpers.IsTestFile(_filePath);
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

    private void CheckNullableEnable(SyntaxNode root)
    {
        if (ShouldSkipNullable()) return;

        var hasNullableEnable = root.DescendantNodes(descendIntoTrivia: true)
            .OfType<NullableDirectiveTriviaSyntax>()
            .Any(d => d.SettingToken.IsKind(SyntaxKind.EnableKeyword));

        if (!hasNullableEnable)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = 1,
                RuleName = nameof(_config.Global.EnforceNullableEnable),
                Details = "Die Datei deklariert kein '#nullable enable'.",
                Guidance = "Füge '#nullable enable' am Anfang der Datei hinzu, um Compile-Time-Nullprüfungen zu aktivieren."
            });
        }
    }

    private bool ShouldSkipNullable()
    {
        if (!_config.Global.EnforceNullableEnable) return true;
        if (IsTestFile()) return true;
        return AnalyzerHelpers.IsNullableEnabledGlobally(_filePath);
    }
}
