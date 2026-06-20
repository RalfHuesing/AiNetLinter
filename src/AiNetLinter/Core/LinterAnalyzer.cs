#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Configuration;
using AiNetLinter.Core.Checkers;
using AiNetLinter.Metrics;
using AiNetLinter.Models;
using AiNetLinter.Suppression;

namespace AiNetLinter.Core;

public sealed class LinterAnalyzer : CSharpSyntaxWalker
{
    private readonly CheckerContext _ctx;
    private readonly SyntaxTree _tree;

    public List<ClassInfo> Classes => _ctx.Classes;
    public List<PartialClassPart> PartialClassParts => _ctx.PartialClassParts;

    internal LinterAnalyzer(string filePath, SemanticModel semanticModel, LinterConfig config, bool isTestFile, string? projectName = null)
        : base(SyntaxWalkerDepth.Node)
    {
        _ctx = new CheckerContext(filePath, config, semanticModel, isTestFile, projectName);
        _tree = semanticModel.SyntaxTree;
    }

    public static IReadOnlyCollection<RuleViolation> Analyze(AnalyzerArgs args)
    {
        var analyzer = new LinterAnalyzer(args.FilePath, args.SemanticModel, args.Config, args.IsTestFile, args.ProjectName);
        analyzer.RunAnalysis();
        return analyzer._ctx.Violations;
    }

    public static IReadOnlyCollection<RuleViolation> Analyze(string filePath, SemanticModel semanticModel, LinterConfig config)
        => Analyze(new AnalyzerArgs(filePath, semanticModel, config));

    public static IReadOnlyCollection<RuleViolation> Analyze(string filePath, SemanticModel semanticModel, LinterConfig config, bool isTestFile)
        => Analyze(new AnalyzerArgs(filePath, semanticModel, config, isTestFile));

    internal IReadOnlyCollection<RuleViolation> Violations => _ctx.Violations;

    internal void RunAnalysis()
    {
        CheckLineCount();
        CheckNullableEnable();
        ScopeChecker.CheckNamespaceDirectoryMapping(_ctx);
        Visit(_tree.GetRoot());
        FilterSuppressedViolations();
    }

    // --- Visit* dispatchers ---

    public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
    {
        var prev = _ctx.CurrentNamespace;
        _ctx.CurrentNamespace = node.Name.ToString();
        base.VisitNamespaceDeclaration(node);
        _ctx.CurrentNamespace = prev;
    }

    public override void VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
    {
        _ctx.CurrentNamespace = node.Name.ToString();
        base.VisitFileScopedNamespaceDeclaration(node);
    }

    public override void VisitUsingDirective(UsingDirectiveSyntax node)
    {
        if (node.Name != null)
            NamespaceCouplingChecker.CheckForbiddenNamespace(node.Name.ToString(), node, _ctx);
        PhantomDependencyChecker.CheckPhantomNamespace(node, _ctx);
        base.VisitUsingDirective(node);
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        if (_ctx.Config.FileFilters.SkipGeneratedCodeAttribute && GeneratedCodeDetector.IsGenerated(node, _ctx))
            return;
        NamingChecker.CheckXmlDoc(node, node.Identifier.Text, "Klasse", _ctx);
        NamingChecker.CheckPascalCase(node.Identifier, "Klasse", _ctx);
        SealedClassChecker.Check(node, _ctx);
        ValueObjectChecker.Check(node, node.Identifier.Text, isRecord: false, _ctx);
        ScopeChecker.CheckMethodOverloads(node, _ctx);
        StateChecker.CheckPrimaryConstructorDependencies(node, _ctx);
        ImmutabilityChecker.CheckClass(node, _ctx);
        WpfSeparationChecker.Check(node, _ctx);
        NestedTypesChecker.Check(node, _ctx);
        PublicMembersChecker.Check(node, node.Identifier.Text, _ctx);
        ClassInfoCollector.Collect(node, _ctx);
        base.VisitClassDeclaration(node);
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        if (_ctx.Config.FileFilters.SkipGeneratedCodeAttribute && GeneratedCodeDetector.IsGenerated(node, _ctx))
            return;
        NamingChecker.CheckXmlDoc(node, node.Identifier.Text, "Record", _ctx);
        NamingChecker.CheckPascalCase(node.Identifier, "Record", _ctx);
        ValueObjectChecker.Check(node, node.Identifier.Text, isRecord: true, _ctx);
        ScopeChecker.CheckMethodOverloads(node, _ctx);
        StateChecker.CheckPrimaryConstructorDependencies(node, _ctx);
        NestedTypesChecker.Check(node, _ctx);
        PublicMembersChecker.Check(node, node.Identifier.Text, _ctx);
        ClassInfoCollector.Collect(node, _ctx);
        base.VisitRecordDeclaration(node);
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        if (_ctx.Config.FileFilters.SkipGeneratedCodeAttribute && GeneratedCodeDetector.IsGenerated(node, _ctx))
            return;
        NamingChecker.CheckXmlDoc(node, node.Identifier.Text, "Struct", _ctx);
        NamingChecker.CheckPascalCase(node.Identifier, "Struct", _ctx);
        ValueObjectChecker.Check(node, node.Identifier.Text, isRecord: false, _ctx);
        ScopeChecker.CheckMethodOverloads(node, _ctx);
        StateChecker.CheckPrimaryConstructorDependencies(node, _ctx);
        NestedTypesChecker.Check(node, _ctx);
        PublicMembersChecker.Check(node, node.Identifier.Text, _ctx);
        ClassInfoCollector.Collect(node, _ctx);
        base.VisitStructDeclaration(node);
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        NamingChecker.CheckXmlDoc(node, node.Identifier.Text, "Interface", _ctx);
        NamingChecker.CheckPascalCase(node.Identifier, "Interface", _ctx);
        base.VisitInterfaceDeclaration(node);
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        NamingChecker.CheckXmlDoc(node, node.Identifier.Text, "Methode", _ctx);
        NamingChecker.CheckPascalCase(node.Identifier, "Methode", _ctx);
        var isPublic = node.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
        NamingChecker.CheckSemanticNaming(node.ParameterList, isPublic, _ctx, node.Identifier.Text);
        ComplexityChecker.CheckMethod(node, _ctx);
        BoolParameterChecker.CheckMethod(node, _ctx);
        AsyncVoidChecker.CheckMethod(node, _ctx);
        base.VisitMethodDeclaration(node);
    }

    public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        AsyncVoidChecker.CheckLocalFunction(node, _ctx);
        base.VisitLocalFunctionStatement(node);
    }

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        NamingChecker.CheckPascalCase(node.Identifier, "Eigenschaft", _ctx);
        base.VisitPropertyDeclaration(node);
    }

    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        StateChecker.CheckConstructorDependencies(node, _ctx);
        BoolParameterChecker.CheckConstructor(node, _ctx);
        base.VisitConstructorDeclaration(node);
    }

    public override void VisitParameter(ParameterSyntax node)
    {
        StateChecker.CheckOutParameter(node, _ctx);
        base.VisitParameter(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        MinimalApiChecker.Check(node, _ctx);
        PhantomDependencyChecker.CheckPhantomReflection(node, _ctx);
        BlockingTaskChecker.CheckInvocation(node, _ctx);
        LinqChainLengthChecker.Check(node, _ctx);
        base.VisitInvocationExpression(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        BlockingTaskChecker.CheckMemberAccess(node, _ctx);
        base.VisitMemberAccessExpression(node);
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        bool hasNsRules = _ctx.Config.ForbiddenNamespaceDependencies?.Count > 0;
        bool hasDynamicCheck = !_ctx.Config.Global.AllowDynamic;
        if (hasDynamicCheck)
            DynamicTypeChecker.Check(node, _ctx);
        if (hasNsRules)
            NamespaceCouplingChecker.CheckForbiddenSymbolNamespace(node, _ctx);
        base.VisitIdentifierName(node);
    }

    public override void VisitCatchClause(CatchClauseSyntax node)
    {
        ControlFlowChecker.CheckCatch(node, _ctx);
        base.VisitCatchClause(node);
    }

    public override void VisitThrowStatement(ThrowStatementSyntax node)
    {
        ControlFlowChecker.CheckThrow(node, _ctx);
        base.VisitThrowStatement(node);
    }

    public override void VisitThrowExpression(ThrowExpressionSyntax node)
    {
        ControlFlowChecker.CheckThrow(node, _ctx);
        base.VisitThrowExpression(node);
    }

    // --- Private helpers ---

    private void CheckLineCount()
    {
        var lineCount = _tree.GetText().Lines.Count;
        var hasPartials = _tree.GetRoot().DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Any(t => t.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));

        if (hasPartials)
        {
            foreach (var typeDecl in _tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (!typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword))) continue;
                var symbol = _ctx.SemanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                if (symbol == null) continue;
                _ctx.PartialClassParts.Add(new PartialClassPart(
                    symbol.ToDisplayString(),
                    _ctx.FilePath,
                    typeDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    lineCount));
            }
        }

        if (hasPartials && _ctx.Config.Metrics.AggregatePartialClassLineCount) return;

        if (lineCount > _ctx.Config.Metrics.MaxLineCount)
        {
            _ctx.ReportViolationAtLine(1, new ViolationDescription(
                nameof(_ctx.Config.Metrics.MaxLineCount),
                $"Die Datei hat {lineCount} Zeilen (erlaubt sind maximal {_ctx.Config.Metrics.MaxLineCount}).",
                BuildFileLineLimitGuidance()));
        }
    }

    private string BuildFileLineLimitGuidance()
    {
        var methods = _tree.GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>().ToList();

        if (methods.Count == 0)
            return "Teile die Datei in kleinere, logisch in sich geschlossene Klassen oder Vertical Slices auf.";

        var totalCc = methods.Sum(m => ComplexityCalculator.GetCyclomaticComplexity(m));
        var avgCc = (double)totalCc / methods.Count;

        if (avgCc <= 2.0)
            return $"Die Datei ist strukturell flach (Ø CC={avgCc:F1} über {methods.Count} Methoden) — wahrscheinlich Konstanten, Mappings oder Builder-Code. " +
                   "Extrahiere thematisch zusammengehörende Gruppen in eigene Klassen (z. B. XyzMappings, XyzConstants).";

        return $"Die Datei ist lang UND komplex (Ø CC={avgCc:F1} über {methods.Count} Methoden). " +
               "Teile sie nach Single-Responsibility in kleinere, fokussierte Klassen auf.";
    }

    private void CheckNullableEnable()
    {
        if (!_ctx.Config.Global.EnforceNullableEnable) return;
        if (_ctx.IsTestFile) return;

        var nullableContext = _ctx.SemanticModel.GetNullableContext(0);
        if (!nullableContext.HasFlag(NullableContext.Enabled))
        {
            _ctx.ReportViolationAtLine(1, new ViolationDescription(
                nameof(_ctx.Config.Global.EnforceNullableEnable),
                "Die Datei deklariert kein '#nullable enable' und hat keine global aktivierten Nullable-Pruefungen.",
                "Fuege '#nullable enable' am Anfang der Datei hinzu, oder aktiviere Nullable global in der csproj/Directory.Build.props."));
        }
    }

    private void FilterSuppressedViolations()
    {
        var fileContent = _tree.GetText().ToString();
        var active = _ctx.Violations
            .Where(v => !SuppressionEvaluator.IsSuppressed(fileContent, v.RuleName ?? "", v.LineNumber))
            .ToList();
        _ctx.ReplaceViolations(active);
    }
}

public sealed record AnalyzerArgs(
    string FilePath,
    SemanticModel SemanticModel,
    LinterConfig Config,
    bool IsTestFile = false,
    string? ProjectName = null
);
