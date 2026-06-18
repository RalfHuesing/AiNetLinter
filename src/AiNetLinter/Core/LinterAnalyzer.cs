#nullable enable

using System.Collections.Concurrent;
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

    internal void UseSharedFieldTrackers(ConcurrentDictionary<INamedTypeSymbol, FieldReadonlyTracker> trackers)
    {
        _ctx.SharedFieldTrackers = trackers;
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
        PreRegisterPrivateFields();
        Visit(_tree.GetRoot());
        StateChecker.CheckReadonlyFields(_ctx);
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
            ArchitectureChecker.CheckForbiddenNamespace(node.Name.ToString(), node, _ctx);
        ArchitectureChecker.CheckPhantomNamespace(node, _ctx);
        base.VisitUsingDirective(node);
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        if (_ctx.Config.FileFilters.SkipGeneratedCodeAttribute && ArchitectureChecker.IsGeneratedCode(node, _ctx))
            return;
        NamingChecker.CheckXmlDoc(node, node.Identifier.Text, "Klasse", _ctx);
        NamingChecker.CheckPascalCase(node.Identifier, "Klasse", _ctx);
        ArchitectureChecker.CheckSealedClass(node, _ctx);
        ArchitectureChecker.CheckValueObjectContract(node, node.Identifier.Text, isRecord: false, _ctx);
        ScopeChecker.CheckMethodOverloads(node, _ctx);
        StateChecker.CheckPrimaryConstructorDependencies(node, _ctx);
        ImmutabilityChecker.CheckClass(node, _ctx);
        WpfSeparationChecker.Check(node, _ctx);
        NestedTypesChecker.Check(node, _ctx);
        PublicMembersChecker.Check(node, node.Identifier.Text, _ctx);
        ArchitectureChecker.CollectClassInfo(node, _ctx);
        base.VisitClassDeclaration(node);
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        if (_ctx.Config.FileFilters.SkipGeneratedCodeAttribute && ArchitectureChecker.IsGeneratedCode(node, _ctx))
            return;
        NamingChecker.CheckXmlDoc(node, node.Identifier.Text, "Record", _ctx);
        NamingChecker.CheckPascalCase(node.Identifier, "Record", _ctx);
        ArchitectureChecker.CheckValueObjectContract(node, node.Identifier.Text, isRecord: true, _ctx);
        ScopeChecker.CheckMethodOverloads(node, _ctx);
        StateChecker.CheckPrimaryConstructorDependencies(node, _ctx);
        NestedTypesChecker.Check(node, _ctx);
        PublicMembersChecker.Check(node, node.Identifier.Text, _ctx);
        base.VisitRecordDeclaration(node);
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        if (_ctx.Config.FileFilters.SkipGeneratedCodeAttribute && ArchitectureChecker.IsGeneratedCode(node, _ctx))
            return;
        NamingChecker.CheckXmlDoc(node, node.Identifier.Text, "Struct", _ctx);
        NamingChecker.CheckPascalCase(node.Identifier, "Struct", _ctx);
        ArchitectureChecker.CheckValueObjectContract(node, node.Identifier.Text, isRecord: false, _ctx);
        ScopeChecker.CheckMethodOverloads(node, _ctx);
        StateChecker.CheckPrimaryConstructorDependencies(node, _ctx);
        NestedTypesChecker.Check(node, _ctx);
        PublicMembersChecker.Check(node, node.Identifier.Text, _ctx);
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
        BusinessLogicChecker.Check(node, _ctx);
        BoolParameterChecker.CheckMethod(node, _ctx);
        base.VisitMethodDeclaration(node);
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
        ScopeChecker.CheckVariableShadowing(node.Identifier, node, _ctx);
        base.VisitParameter(node);
    }

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        StateChecker.AnalyzePrivateField(node, _ctx);
        base.VisitFieldDeclaration(node);
    }

    public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        StateChecker.CheckParameterReassignment(node.Left, _ctx);
        StateChecker.RegisterFieldWrite(node.Left, _ctx);
        base.VisitAssignmentExpression(node);
    }

    public override void VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
    {
        if (node.IsKind(SyntaxKind.PostIncrementExpression) || node.IsKind(SyntaxKind.PostDecrementExpression))
        {
            StateChecker.CheckParameterReassignment(node.Operand, _ctx);
            StateChecker.RegisterFieldWrite(node.Operand, _ctx);
        }
        base.VisitPostfixUnaryExpression(node);
    }

    public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
    {
        if (node.IsKind(SyntaxKind.PreIncrementExpression) || node.IsKind(SyntaxKind.PreDecrementExpression))
        {
            StateChecker.CheckParameterReassignment(node.Operand, _ctx);
            StateChecker.RegisterFieldWrite(node.Operand, _ctx);
        }
        base.VisitPrefixUnaryExpression(node);
    }

    public override void VisitArgument(ArgumentSyntax node)
    {
        if (node.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword) || node.RefOrOutKeyword.IsKind(SyntaxKind.RefKeyword))
        {
            StateChecker.CheckParameterReassignment(node.Expression, _ctx);
            StateChecker.RegisterFieldWrite(node.Expression, _ctx);
        }
        base.VisitArgument(node);
    }

    public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
    {
        if (IsLocalVariable(node))
            ScopeChecker.CheckVariableShadowing(node.Identifier, node, _ctx);
        base.VisitVariableDeclarator(node);
    }

    public override void VisitForEachStatement(ForEachStatementSyntax node)
    {
        ScopeChecker.CheckVariableShadowing(node.Identifier, node, _ctx);
        base.VisitForEachStatement(node);
    }

    public override void VisitCatchDeclaration(CatchDeclarationSyntax node)
    {
        ScopeChecker.CheckVariableShadowing(node.Identifier, node, _ctx);
        base.VisitCatchDeclaration(node);
    }

    public override void VisitSingleVariableDesignation(SingleVariableDesignationSyntax node)
    {
        ScopeChecker.CheckVariableShadowing(node.Identifier, node, _ctx);
        base.VisitSingleVariableDesignation(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        MinimalApiChecker.Check(node, _ctx);
        ArchitectureChecker.CheckPhantomReflection(node, _ctx);
        TruncationChecker.Check(node, _ctx);
        base.VisitInvocationExpression(node);
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        bool hasNsRules = _ctx.Config.ForbiddenNamespaceDependencies?.Count > 0;
        bool hasDynamicCheck = !_ctx.Config.Global.AllowDynamic;
        if (hasDynamicCheck)
            ArchitectureChecker.CheckDynamic(node, _ctx);
        if (hasNsRules)
            ArchitectureChecker.CheckForbiddenSymbolNamespace(node, _ctx);
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

    public override void VisitLiteralExpression(LiteralExpressionSyntax node)
    {
        MagicValuesChecker.Check(node, _ctx);
        base.VisitLiteralExpression(node);
    }

    // --- Private helpers ---

    private void PreRegisterPrivateFields()
    {
        if (!_ctx.Config.Global.EnforceReadonlyFields) return;
        foreach (var field in _tree.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>())
            StateChecker.AnalyzePrivateField(field, _ctx);
    }

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
            _ctx.AddViolation(new RuleViolation
            {
                FilePath = _ctx.FilePath,
                LineNumber = 1,
                RuleName = nameof(_ctx.Config.Metrics.MaxLineCount),
                Details = $"Die Datei hat {lineCount} Zeilen (erlaubt sind maximal {_ctx.Config.Metrics.MaxLineCount}).",
                Guidance = "Teile die Datei in kleinere, logisch in sich geschlossene Klassen oder Vertical Slices auf."
            });
        }
    }

    private void CheckNullableEnable()
    {
        if (!_ctx.Config.Global.EnforceNullableEnable) return;
        if (_ctx.IsTestFile) return;

        var nullableContext = _ctx.SemanticModel.GetNullableContext(0);
        if (!nullableContext.HasFlag(NullableContext.Enabled))
        {
            _ctx.AddViolation(new RuleViolation
            {
                FilePath = _ctx.FilePath,
                LineNumber = 1,
                RuleName = nameof(_ctx.Config.Global.EnforceNullableEnable),
                Details = "Die Datei deklariert kein '#nullable enable' und hat keine global aktivierten Nullable-Pruefungen.",
                Guidance = "Fuege '#nullable enable' am Anfang der Datei hinzu, oder aktiviere Nullable global in der csproj/Directory.Build.props."
            });
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

    private static bool IsLocalVariable(VariableDeclaratorSyntax node)
    {
        var grandparent = node.Parent?.Parent;
        return grandparent is not FieldDeclarationSyntax && grandparent is not EventFieldDeclarationSyntax;
    }
}

public sealed record AnalyzerArgs(
    string FilePath,
    SemanticModel SemanticModel,
    LinterConfig Config,
    bool IsTestFile = false,
    string? ProjectName = null
);
