using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Configuration;
using AiNetLinter.Models;

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
        // Einfaches Zeilenzählen anhand von Zeilenumbrüchen
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

        base.VisitClassDeclaration(node);
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

        // TODO: Komplexitäts-Checks hier integrieren, sobald der ComplexityCalculator implementiert ist.

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
