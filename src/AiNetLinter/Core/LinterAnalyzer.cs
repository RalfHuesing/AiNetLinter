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
    private readonly string? _projectName;
    private readonly HashSet<IFieldSymbol> _privateFieldsToAnalyze = new(SymbolEqualityComparer.Default);
    private readonly HashSet<IFieldSymbol> _fieldsModifiedOutsideConstructor = new(SymbolEqualityComparer.Default);

    public List<ClassInfo> Classes { get; } = new();
    public List<PartialClassPart> PartialClassParts { get; } = new();

    internal LinterAnalyzer(string filePath, SemanticModel semanticModel, LinterConfig config, bool isTestFile, string? projectName = null)
        : base(SyntaxWalkerDepth.Node)
    {
        _filePath = filePath;
        _tree = semanticModel.SyntaxTree;
        _semanticModel = semanticModel;
        _config = config;
        _isTestFile = isTestFile;
        _projectName = projectName;
    }

    /// <summary>
    /// Analysiert ein Dokument und gibt alle gefundenen Verstöße zurück.
    /// </summary>
    /// <param name="args">Die Argumente für die Analyse.</param>
    /// <returns>Eine Liste aller gefundenen Regelverstöße.</returns>
    public static IReadOnlyCollection<RuleViolation> Analyze(AnalyzerArgs args)
    {
        var analyzer = new LinterAnalyzer(args.FilePath, args.SemanticModel, args.Config, args.IsTestFile, args.ProjectName);
        analyzer.RunAnalysis();
        return analyzer._violations;
    }

    /// <summary>
    /// Analysiert ein Dokument und gibt alle gefundenen Verstöße zurück.
    /// </summary>
    /// <param name="filePath">Der Dateipfad.</param>
    /// <param name="semanticModel">Das semantische Modell.</param>
    /// <param name="config">Die Konfiguration.</param>
    /// <returns>Eine Liste aller gefundenen Regelverstöße.</returns>
    public static IReadOnlyCollection<RuleViolation> Analyze(string filePath, SemanticModel semanticModel, LinterConfig config)
    {
        return Analyze(new AnalyzerArgs(filePath, semanticModel, config));
    }

    /// <summary>
    /// Analysiert ein Dokument und gibt alle gefundenen Verstöße zurück.
    /// </summary>
    /// <param name="filePath">Der Dateipfad.</param>
    /// <param name="semanticModel">Das semantische Modell.</param>
    /// <param name="config">Die Konfiguration.</param>
    /// <param name="isTestFile">Gibt an, ob es sich um ein Test-Dokument handelt.</param>
    /// <returns>Eine Liste aller gefundenen Regelverstöße.</returns>
    public static IReadOnlyCollection<RuleViolation> Analyze(string filePath, SemanticModel semanticModel, LinterConfig config, bool isTestFile)
    {
        return Analyze(new AnalyzerArgs(filePath, semanticModel, config, isTestFile));
    }

    internal IReadOnlyCollection<RuleViolation> Violations => _violations;

    internal void RunAnalysis()
    {
        CheckLineCount();
        CheckNullableEnable();
        CheckNamespaceDirectoryMapping();
        Visit(_tree.GetRoot());
        CheckReadonlyFields();
        FilterSuppressedViolations();
    }

    private void CheckLineCount()
    {
        var text = _tree.GetText();
        var lineCount = text.Lines.Count;

        if (_config.Metrics.AggregatePartialClassLineCount && HasPartialRootDeclarations())
        {
            RecordPartialClassParts(lineCount);
            return;
        }

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

    private bool HasPartialRootDeclarations()
    {
        return _tree.GetRoot().DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Any(t => t.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));
    }

    private void RecordPartialClassParts(int fileLineCount)
    {
        foreach (var typeDecl in _tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            if (!typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
            {
                continue;
            }

            var symbol = _semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
            if (symbol == null)
            {
                continue;
            }

            PartialClassParts.Add(new PartialClassPart(
                symbol.ToDisplayString(),
                _filePath,
                GetLineNumber(typeDecl),
                fileLineCount));
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
                Details = "Die Datei deklariert kein '#nullable enable' und hat keine global aktivierten Nullable-Pruefungen.",
                Guidance = "Fuege '#nullable enable' am Anfang der Datei hinzu, oder aktiviere Nullable global in der csproj/Directory.Build.props."
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

    private void CheckReadonlyFields()
    {
        if (!_config.Global.EnforceReadonlyFields) return;

        foreach (var field in _privateFieldsToAnalyze)
        {
            CheckSingleFieldReadonly(field);
        }
    }

    private void CheckSingleFieldReadonly(IFieldSymbol field)
    {
        if (_fieldsModifiedOutsideConstructor.Contains(field)) return;

        var syntaxRef = field.DeclaringSyntaxReferences.FirstOrDefault();
        var syntaxNode = syntaxRef?.GetSyntax();
        var lineNumber = syntaxNode != null ? GetLineNumber(syntaxNode) : 1;

        _violations.Add(new RuleViolation
        {
            FilePath = _filePath,
            LineNumber = lineNumber,
            RuleName = "EnforceReadonlyFields",
            Details = $"Das private Feld '{field.Name}' wird nur im Konstruktor oder Initialisierer zugewiesen, ist aber nicht als 'readonly' deklariert.",
            Guidance = "Fuege den 'readonly' Modifikator zum Feld hinzu, um unabsichtliche Modifikationen zu verhindern."
        });
    }
}

/// <summary>
/// Argumente für die Ausführung der Analyse über LinterAnalyzer.
/// </summary>
public sealed record AnalyzerArgs(
    string FilePath,
    SemanticModel SemanticModel,
    LinterConfig Config,
    bool IsTestFile = false,
    string? ProjectName = null
);
