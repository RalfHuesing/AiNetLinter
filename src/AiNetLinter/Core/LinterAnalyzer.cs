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
public sealed class LinterAnalyzer : CSharpSyntaxWalker
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
        // 1. Zeilenanzahl prüfen
        CheckLineCount();

        // 2. Syntax-Baum parsen und ablaufen
        var tree = CSharpSyntaxTree.ParseText(_fileContent);
        var root = tree.GetRoot();
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
        CheckForbiddenNamespace(node);
        base.VisitUsingDirective(node);
    }

    private void CheckForbiddenNamespace(UsingDirectiveSyntax node)
    {
        if (node.Name == null) return;
        var importedNamespace = node.Name.ToString();
        if (importedNamespace == null) return;

        foreach (var rule in _config.ForbiddenNamespaceDependencies)
        {
            if (IsViolation(rule, importedNamespace))
            {
                AddNamespaceViolation(node, importedNamespace);
            }
        }
    }

    private bool IsViolation(NamespaceRule rule, string importedNamespace)
    {
        if (rule.SourceNamespace == null || rule.TargetNamespace == null) return false;
        return _currentNamespace.StartsWith(rule.SourceNamespace) && 
               importedNamespace.StartsWith(rule.TargetNamespace);
    }

    private void AddNamespaceViolation(UsingDirectiveSyntax node, string importedNamespace)
    {
        _violations.Add(new RuleViolation
        {
            FilePath = _filePath,
            LineNumber = GetLineNumber(node),
            RuleName = "ForbiddenNamespaceDependency",
            Details = $"Der Namespace '{_currentNamespace}' darf nicht vom Namespace '{importedNamespace}' abhängen.",
            Guidance = "Entferne die Abhängigkeit oder nutze Abstraktion/Events statt direkter Kopplung."
        });
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        // Prüfen, ob die Klasse sealed ist (sofern konfiguriert)
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
        base.VisitClassDeclaration(node);
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        CheckValueObjectContract(node, node.Identifier.Text, isRecord: true);
        base.VisitRecordDeclaration(node);
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        CheckValueObjectContract(node, node.Identifier.Text, isRecord: false);
        base.VisitStructDeclaration(node);
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

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        // Parameter-Anzahl prüfen
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

        // Zyklomatische Komplexität prüfen
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

        // Kognitive Komplexität prüfen
        var cognitiveComplexity = ComplexityCalculator.GetCognitiveComplexity(node);
        if (cognitiveComplexity > _config.Metrics.MaxCognitiveComplexity)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = nameof(_config.Metrics.MaxCognitiveComplexity),
                Details = $"Die Methode '{node.Identifier.Text}' hat eine Kognitive Komplexität von {cognitiveComplexity} (erlaubt sind maximal {_config.Metrics.MaxCognitiveComplexity}).",
                Guidance = "Vereinfache verschachtelte Kontrollstrukturen (If-in-If etc.) und lagere Logik in flache Hilfsmethoden aus."
            });
        }

        base.VisitMethodDeclaration(node);
    }

    public override void VisitParameter(ParameterSyntax node)
    {
        // out-Parameter prüfen
        if (!_config.Global.AllowOutParameters && node.Modifiers.Any(SyntaxKind.OutKeyword))
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

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        // dynamic prüfen
        if (!_config.Global.AllowDynamic && node.Identifier.Text == "dynamic")
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

        base.VisitIdentifierName(node);
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
}
