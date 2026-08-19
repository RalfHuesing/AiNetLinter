#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.DuplicateDetection;

/// <summary>
/// Baut aus Syntax und <see cref="SemanticModel"/> ein unveraenderliches Strukturprofil.
/// Identifier- und Literal-Werte fliessen nicht in den Vektor ein — nur Typen, Kontrollfluss,
/// Zieltypen und grobe Verhaltensmarker.
/// </summary>
internal static class StructureProfileExtractor
{
    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.MinimallyQualifiedFormat;
    private static readonly string[] IoTypePrefixes =
    [
        "System.Console", "System.IO", "System.Net", "System.Xml", "Microsoft.Win32",
    ];

    internal static MethodStructureProfile Extract(EligibleMethod method)
    {
        var features = new Dictionary<string, double>(StringComparer.Ordinal);
        var semanticModel = method.SemanticModel;
        var symbol = method.Symbol;

        Add(features, "ret:" + NormalizeType(symbol.ReturnType), StructureFeatureWeights.ReturnType);
        Add(features, "retkind:" + TypeKindLabel(symbol.ReturnType), StructureFeatureWeights.ReturnKind);

        foreach (var parameter in symbol.Parameters)
        {
            Add(features, "param:" + NormalizeType(parameter.Type), StructureFeatureWeights.ParameterType);
            Add(features, "paramkind:" + TypeKindLabel(parameter.Type), StructureFeatureWeights.ParameterKind);
        }

        var walker = new ProfileWalker(semanticModel, symbol.ContainingType);
        walker.Visit(method.Body);

        foreach (var (kind, count) in walker.ControlFlowCounts.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            Add(features, "cf:" + kind, StructureFeatureWeights.ControlFlow * count);
        }

        if (walker.ControlFlowSequence.Count > 0)
        {
            var sequence = string.Join(">", walker.ControlFlowSequence.Take(8));
            Add(features, "cflowseq:" + sequence, StructureFeatureWeights.ControlFlowSequence);
        }

        foreach (var target in walker.TargetTypes.OrderBy(t => t, StringComparer.Ordinal))
        {
            Add(features, "target:" + target, StructureFeatureWeights.TargetType);
        }

        foreach (var memberType in walker.MemberTypes.OrderBy(t => t, StringComparer.Ordinal))
        {
            Add(features, "member:" + memberType, StructureFeatureWeights.MemberType);
        }

        foreach (var literalClass in walker.LiteralClasses.OrderBy(t => t, StringComparer.Ordinal))
        {
            Add(features, "lit:" + literalClass, StructureFeatureWeights.LiteralClass);
        }

        var purity = walker.HasIo ? "io" : walker.MutatesInstanceState ? "mutates" : walker.UsesInstanceState ? "stateful" : "pure";
        Add(features, "purity:" + purity, StructureFeatureWeights.Purity);
        Add(features, "form:" + walker.ReturnForm, StructureFeatureWeights.ReturnForm);

        return new MethodStructureProfile(BuildSummary(symbol, walker), features);
    }

    private static void Add(Dictionary<string, double> features, string key, double weight) =>
        features[key] = features.GetValueOrDefault(key) + weight;

    private static string NormalizeType(ITypeSymbol type) =>
        type.ToDisplayString(TypeFormat);

    private static string TypeKindLabel(ITypeSymbol type) =>
        type.TypeKind.ToString().ToLowerInvariant();

    private static string BuildSummary(IMethodSymbol symbol, ProfileWalker walker)
    {
        var sb = new StringBuilder();
        sb.Append("ret=").Append(NormalizeType(symbol.ReturnType));
        sb.Append("; params=").Append(string.Join(",", symbol.Parameters.Select(p => NormalizeType(p.Type))));
        if (walker.ControlFlowCounts.Count > 0)
        {
            sb.Append("; cf=").Append(string.Join(",", walker.ControlFlowCounts.Keys.OrderBy(k => k, StringComparer.Ordinal)));
        }
        if (walker.TargetTypes.Count > 0)
        {
            sb.Append("; targets=").Append(string.Join(",", walker.TargetTypes.OrderBy(t => t, StringComparer.Ordinal)));
        }
        if (walker.LiteralClasses.Count > 0)
        {
            sb.Append("; lits=").Append(string.Join(",", walker.LiteralClasses.OrderBy(t => t, StringComparer.Ordinal)));
        }
        var purity = walker.HasIo ? "io" : walker.MutatesInstanceState ? "mutates" : walker.UsesInstanceState ? "stateful" : "pure";
        sb.Append("; ").Append(purity);
        sb.Append("; form=").Append(walker.ReturnForm);
        return sb.ToString();
    }

    private sealed class ProfileWalker : CSharpSyntaxWalker
    {
        private readonly SemanticModel _semanticModel;
        private readonly INamedTypeSymbol? _containingType;
        private bool _seenReturnSwitch;
        private bool _seenReturnIf;
        private bool _seenThrow;

        internal ProfileWalker(SemanticModel semanticModel, INamedTypeSymbol? containingType)
            : base(SyntaxWalkerDepth.Node)
        {
            _semanticModel = semanticModel;
            _containingType = containingType;
        }

        internal Dictionary<string, int> ControlFlowCounts { get; } = new(StringComparer.Ordinal);
        internal List<string> ControlFlowSequence { get; } = [];
        internal HashSet<string> TargetTypes { get; } = new(StringComparer.Ordinal);
        internal HashSet<string> MemberTypes { get; } = new(StringComparer.Ordinal);
        internal HashSet<string> LiteralClasses { get; } = new(StringComparer.Ordinal);
        internal bool MutatesInstanceState { get; private set; }
        internal bool UsesInstanceState { get; private set; }
        internal bool HasIo { get; private set; }
        internal string ReturnForm =>
            _seenReturnSwitch ? "switch" : _seenReturnIf ? "if" : _seenThrow ? "throw" : "direct";

        public override void Visit(SyntaxNode? node)
        {
            if (node is null) return;
            RecordControlFlow(node);
            RecordTargets(node);
            RecordLiterals(node);
            RecordPurity(node);
            base.Visit(node);
        }

        private static readonly Dictionary<SyntaxKind, string> ControlFlowSyntaxKinds = new()
        {
            [SyntaxKind.IfStatement] = "if",
            [SyntaxKind.SwitchStatement] = "switch",
            [SyntaxKind.SwitchExpression] = "switch-expr",
            [SyntaxKind.ForStatement] = "for",
            [SyntaxKind.ForEachStatement] = "foreach",
            [SyntaxKind.WhileStatement] = "while",
            [SyntaxKind.DoStatement] = "do",
            [SyntaxKind.TryStatement] = "try",
            [SyntaxKind.CatchClause] = "catch",
            [SyntaxKind.ReturnStatement] = "return",
            [SyntaxKind.ThrowStatement] = "throw",
            [SyntaxKind.ConditionalExpression] = "ternary",
            [SyntaxKind.IsPatternExpression] = "is-pattern",
            [SyntaxKind.UsingStatement] = "using",
            [SyntaxKind.LockStatement] = "lock",
            [SyntaxKind.YieldReturnStatement] = "yield",
            [SyntaxKind.YieldBreakStatement] = "yield",
        };

        private void RecordControlFlow(SyntaxNode node)
        {
            if (!ControlFlowSyntaxKinds.TryGetValue(node.Kind(), out var label)) return;
            ControlFlowCounts[label] = ControlFlowCounts.GetValueOrDefault(label) + 1;
            if (ControlFlowSequence.Count < 8) ControlFlowSequence.Add(label);
        }

        private void RecordTargets(SyntaxNode node)
        {
            switch (node)
            {
                case SwitchExpressionSyntax switchExpr:
                    AddType(TargetTypes, _semanticModel.GetTypeInfo(switchExpr.GoverningExpression).Type);
                    if (switchExpr.GoverningExpression is MemberAccessExpressionSyntax member)
                    {
                        AddType(TargetTypes, _semanticModel.GetTypeInfo(member.Name).Type);
                    }
                    _seenReturnSwitch = true;
                    break;
                case SwitchStatementSyntax switchStmt:
                    AddType(TargetTypes, _semanticModel.GetTypeInfo(switchStmt.Expression).Type);
                    if (switchStmt.Expression is MemberAccessExpressionSyntax stmtMember)
                    {
                        AddType(TargetTypes, _semanticModel.GetTypeInfo(stmtMember.Name).Type);
                    }
                    _seenReturnSwitch = true;
                    break;
                case IsPatternExpressionSyntax pattern:
                    AddType(TargetTypes, _semanticModel.GetTypeInfo(pattern.Expression).Type);
                    break;
                case ReturnStatementSyntax { Expression: ConditionalExpressionSyntax }:
                    _seenReturnIf = true;
                    break;
                case IfStatementSyntax:
                    _seenReturnIf = true;
                    break;
                case ThrowStatementSyntax:
                    _seenThrow = true;
                    break;
            }
        }

        private void RecordLiterals(SyntaxNode node)
        {
            if (node is not LiteralExpressionSyntax literal) return;
            var kind = literal.Kind() switch
            {
                SyntaxKind.StringLiteralExpression or SyntaxKind.CharacterLiteralExpression => "string",
                SyntaxKind.NumericLiteralExpression => "number",
                SyntaxKind.TrueLiteralExpression or SyntaxKind.FalseLiteralExpression => "bool",
                SyntaxKind.NullLiteralExpression => "null",
                _ => null,
            };
            if (kind is not null) LiteralClasses.Add(kind);
        }

        private void RecordPurity(SyntaxNode node)
        {
            CheckStateMutation(node);
            CheckIoInvocation(node);
            CheckMemberAccess(node);
        }

        private void CheckStateMutation(SyntaxNode node)
        {
            var target = node switch
            {
                AssignmentExpressionSyntax assignment => assignment.Left,
                PrefixUnaryExpressionSyntax prefix => prefix.Operand,
                PostfixUnaryExpressionSyntax postfix => postfix.Operand,
                _ => null,
            };
            if (target is not null && RefersToInstanceMember(target)) MutatesInstanceState = true;
        }

        private void CheckIoInvocation(SyntaxNode node)
        {
            if (node is not InvocationExpressionSyntax invocation) return;
            var invoked = _semanticModel.GetSymbolInfo(invocation).Symbol;
            if (invoked?.ContainingType is { } containing && IsIoType(containing))
            {
                HasIo = true;
            }
        }

        private void CheckMemberAccess(SyntaxNode node)
        {
            if (node is not MemberAccessExpressionSyntax access) return;
            var accessed = _semanticModel.GetSymbolInfo(access).Symbol;
            if (accessed is IFieldSymbol field)
            {
                AddType(MemberTypes, field.Type);
                if (RefersToInstanceMember(access)) UsesInstanceState = true;
            }
            else if (accessed is IPropertySymbol property)
            {
                AddType(MemberTypes, property.Type);
                if (RefersToInstanceMember(access)) UsesInstanceState = true;
            }
        }

        private bool RefersToInstanceMember(SyntaxNode node)
        {
            var symbol = _semanticModel.GetSymbolInfo(node).Symbol;
            if (symbol is null || symbol.IsStatic) return false;
            if (symbol is not (IFieldSymbol or IPropertySymbol)) return false;
            return SymbolEqualityComparer.Default.Equals(symbol.ContainingType, _containingType);
        }

        private static bool IsIoType(INamedTypeSymbol type)
        {
            var display = type.ToDisplayString();
            foreach (var prefix in IoTypePrefixes)
            {
                if (display.StartsWith(prefix, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static void AddType(HashSet<string> set, ITypeSymbol? type)
        {
            if (type is null || type.TypeKind == TypeKind.Error) return;
            set.Add(type.ToDisplayString(TypeFormat));
        }
    }
}
