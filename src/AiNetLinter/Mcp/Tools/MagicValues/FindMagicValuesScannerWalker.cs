#nullable enable

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools.MagicValues;

/// <summary>
/// Parameter-Bundel fuer <see cref="MagicValueSyntaxWalker"/>. Fasst die acht Walker-Felder
/// (filePath/model/valueTypeFilter/categoryFilter/ignoreNumbers/includeSuppressed/
/// changedFiles/isTestPath/sink) zu einem <c>WalkerContext</c>-Record zusammen. Records mit
/// ≥ 6 Feldern sind explizit von <c>MaxConstructorDependencies: 5</c> ausgenommen (gilt nur
/// fuer Klassen-Konstruktoren, siehe <c>AiNetLinter.mdc</c>).
/// <see cref="ChangedFiles"/> ist <see langword="null"/>, wenn kein Git-Diff-Filter aktiv ist.
/// </summary>
internal sealed record MagicValueWalkerContext(
    string FilePath,
    SemanticModel? Model,
    MagicValueValueType? ValueTypeFilter,
    MagicValueCategory? CategoryFilter,
    IReadOnlySet<int> IgnoreNumbers,
    bool IncludeSuppressed,
    IReadOnlySet<string>? ChangedFiles,
    bool IsTestPath,
    List<RawMagicValue> Sink);

/// <summary>
/// Roslyn <see cref="CSharpSyntaxWalker"/>, der jedes <see cref="LiteralExpressionSyntax"/>
/// plus jedes statische <c>InterpolatedStringText</c>-Segment in <see cref="InterpolatedStringExpressionSyntax"/>
/// an <see cref="MagicValuesClassifier.Classify"/> uebergibt. Trivial-/Attribut-/Index-/Loop-
/// Filterung uebernimmt der Classifier; hier nur die Ast-Walk-Mechanik. Erkennt zusaetzlich
/// <c>enum_candidates</c> ueber if-else-Kaskaden / switch-Statements / switch-Expressions
/// (≥ 3 Vergleiche gegen denselben Identifier).
/// </summary>
internal sealed class MagicValueSyntaxWalker : CSharpSyntaxWalker
{
    private readonly MagicValueWalkerContext context;

    // Mutable Walker-State: enum_candidates-Erkennung sammelt die bereits klassifizierten
    // Literale hier, damit ProcessLiteral sie nicht doppelt meldet. Bewusst eine Instanz-
    // Variable statt im Context-Record, weil Records init-only-Properties haben.
    private readonly HashSet<LiteralExpressionSyntax> enumClassifiedLiterals = new();

    internal MagicValueSyntaxWalker(MagicValueWalkerContext context)
    {
        this.context = context;
    }

    public override void VisitLiteralExpression(LiteralExpressionSyntax node)
    {
        ProcessLiteral(node);
        base.VisitLiteralExpression(node);
    }

    /// <summary>Erkennt <c>enum_candidates</c> in if-else-Kaskaden: ≥ 3 Vergleiche gegen
    /// denselben Identifier-Token qualifizieren die Literale als Enum-Kandidaten. Die
    /// <c>base.Visit</c>-Reihenfolge stellt sicher, dass die Literale selbst trotzdem
    /// besucht werden (die Kaskaden-Erkennung laeuft VOR <see cref="ProcessLiteral"/>).</summary>
    public override void VisitIfStatement(IfStatementSyntax node)
    {
        DetectEnumCandidatesInCascades(node.Condition, node.Else);
        base.VisitIfStatement(node);
    }

    /// <summary>Erkennt <c>enum_candidates</c> in switch-Statements: ≥ 3 case-Labels mit
    /// LiteralExpression gegen denselben Switch-Expression-Identifier.</summary>
    public override void VisitSwitchStatement(SwitchStatementSyntax node)
    {
        DetectEnumCandidatesInSwitch(node.Expression, node.Sections);
        base.VisitSwitchStatement(node);
    }

    /// <summary>Erkennt <c>enum_candidates</c> in switch-Expressions: ≥ 3 SwitchExpressionArms
    /// mit konstantem Pattern gegen dasselbe Switch-Expression.</summary>
    public override void VisitSwitchExpression(SwitchExpressionSyntax node)
    {
        DetectEnumCandidatesInSwitchExpression(node.GoverningExpression, node.Arms);
        base.VisitSwitchExpression(node);
    }

    private void DetectEnumCandidatesInCascades(ExpressionSyntax condition, ElseClauseSyntax? elseClause)
    {
        // Sammelt (IdentifierName, LiteralExpression) aus einer if-else-Kaskade.
        // Pro Identifier-Name zaehlen wir, wie oft ein Literal dagegen verglichen wird.
        // Bei >= 3 Treffern klassifizieren wir jedes Literal als enum_candidate.
        var comparisons = CollectIdentifierLiteralComparisonsInto(
            condition, new Dictionary<string, List<LiteralExpressionSyntax>>());
        AppendComparisonsFromElseChain(elseClause, comparisons);
        ClassifyEnumCandidates(comparisons);
    }

    private void AppendComparisonsFromElseChain(
        ElseClauseSyntax? elseClause,
        Dictionary<string, List<LiteralExpressionSyntax>> comparisons)
    {
        var current = elseClause;
        while (current is not null)
        {
            if (current.Statement is IfStatementSyntax nestedIf)
            {
                CollectIdentifierLiteralComparisonsInto(nestedIf.Condition, comparisons);
                current = nestedIf.Else;
            }
            else
            {
                break;
            }
        }
    }

    private void DetectEnumCandidatesInSwitch(
        ExpressionSyntax governingExpression,
        SyntaxList<SwitchSectionSyntax> sections)
    {
        var identifier = ExtractIdentifierName(governingExpression);
        if (identifier is null) return;

        var literals = new List<LiteralExpressionSyntax>();
        foreach (var section in sections)
        {
            foreach (var label in section.Labels)
            {
                if (label is CaseSwitchLabelSyntax caseLabel
                    && caseLabel.Value is LiteralExpressionSyntax literal)
                {
                    literals.Add(literal);
                }
            }
        }

        if (literals.Count >= 3)
        {
            ClassifyEnumCandidates(new Dictionary<string, List<LiteralExpressionSyntax>>
            {
                [identifier] = literals,
            });
        }
    }

    private void DetectEnumCandidatesInSwitchExpression(
        ExpressionSyntax governingExpression,
        SeparatedSyntaxList<SwitchExpressionArmSyntax> arms)
    {
        var identifier = ExtractIdentifierName(governingExpression);
        if (identifier is null) return;

        var literals = new List<LiteralExpressionSyntax>();
        foreach (var arm in arms)
        {
            if (arm.Pattern is ConstantPatternSyntax constantPattern
                && constantPattern.Expression is LiteralExpressionSyntax literal)
            {
                literals.Add(literal);
            }
        }

        if (literals.Count >= 3)
        {
            ClassifyEnumCandidates(new Dictionary<string, List<LiteralExpressionSyntax>>
            {
                [identifier] = literals,
            });
        }
    }

    private Dictionary<string, List<LiteralExpressionSyntax>> CollectIdentifierLiteralComparisonsInto(
        ExpressionSyntax expression,
        Dictionary<string, List<LiteralExpressionSyntax>> sink)
    {
        if (expression is BinaryExpressionSyntax binary
            && binary.IsKind(SyntaxKind.EqualsExpression))
        {
            TryAddComparison(binary.Left, binary.Right, sink);
            TryAddComparison(binary.Right, binary.Left, sink);
        }
        else if (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            CollectIdentifierLiteralComparisonsInto(parenthesized.Expression, sink);
        }
        else if (expression is BinaryExpressionSyntax logical
            && (logical.IsKind(SyntaxKind.LogicalAndExpression) || logical.IsKind(SyntaxKind.LogicalOrExpression)))
        {
            CollectIdentifierLiteralComparisonsInto(logical.Left, sink);
            CollectIdentifierLiteralComparisonsInto(logical.Right, sink);
        }

        return sink;
    }

    private static void TryAddComparison(
        ExpressionSyntax maybeIdentifier,
        ExpressionSyntax maybeLiteral,
        Dictionary<string, List<LiteralExpressionSyntax>> sink)
    {
        if (maybeIdentifier is IdentifierNameSyntax identifier
            && maybeLiteral is LiteralExpressionSyntax literal
            && literal.Kind() is SyntaxKind.StringLiteralExpression or SyntaxKind.NumericLiteralExpression)
        {
            var name = identifier.Identifier.ValueText;
            if (string.IsNullOrEmpty(name)) return;
            if (!sink.TryGetValue(name, out var list))
            {
                list = new List<LiteralExpressionSyntax>();
                sink[name] = list;
            }
            list.Add(literal);
        }
    }

    private static string? ExtractIdentifierName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText,
        ParenthesizedExpressionSyntax p => ExtractIdentifierName(p.Expression),
        _ => null,
    };

    private void ClassifyEnumCandidates(Dictionary<string, List<LiteralExpressionSyntax>> comparisons)
    {
        const int EnumThreshold = 3;
        foreach (var pair in comparisons)
        {
            if (pair.Value.Count < EnumThreshold) continue;
            var identifierName = ToPascalCase(pair.Key);
            var recommendation = $"enum {identifierName} {{ ... }}";
            foreach (var literal in pair.Value)
            {
                if (enumClassifiedLiterals.Contains(literal)) continue;
                enumClassifiedLiterals.Add(literal);
                var lineSpan = literal.GetLocation().GetLineSpan();
                var line = lineSpan.StartLinePosition.Line + 1;
                var column = lineSpan.StartLinePosition.Character + 1;
                var value = literal.Token.ValueText;
                var valueType = literal.Kind() == SyntaxKind.NumericLiteralExpression
                    ? MagicValueValueType.Number
                    : MagicValueValueType.String;
                var classification = new MagicValueClassification(
                    true,
                    MagicValueCategory.EnumCandidates,
                    recommendation,
                    $"Diskretes Set gleicher Identifier-Vergleiche ({pair.Value.Count}x gegen '{pair.Key}')");
                context.Sink.Add(new RawMagicValue(
                    context.FilePath, line, column, valueType, value, classification));
            }
        }
    }

    private static string ToPascalCase(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return identifier;
        return char.IsUpper(identifier[0]) ? identifier : char.ToUpperInvariant(identifier[0]) + identifier.Substring(1);
    }

    public override void VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
    {
        // Beispiel: statische Text-Segmente in $"...{x}..." werden
        // durch den MagicValuesClassifier klassifiziert. Dynamische Segmente ({x}) werden
        // NICHT ausgewertet — das wuerde eine Laufzeit-Aufloesung erfordern, die fuer ein
        // On-Demand-Audit zu teuer und semantisch fragwuerdig waere. Wir synthetisieren
        // fuer jedes InterpolatedStringTextSyntax einen LiteralExpressionSyntax-Knoten und
        // reichen ihn durch den existierenden ProcessLiteral-Pfad — damit greifen URL/Path/
        // Format-String/Connection-String/Header-Id-Heuristiken ohne doppelte Logik. Die
        // synthetischen Knoten haben kein Parent (nicht im SyntaxTree), daher feuern die
        // Parent-Pfad-basierten Filter (Attribut/GetHashCode/Index/Loop) auf ihnen nicht —
        // das ist akzeptabel, weil die statischen Fragmente in diesen Kontexten ohnehin
        // keine Heuristik treffen wuerden. Auch die per-Symbol-Kontext-Heuristiken
        // (ClassifyNameofCandidate ueber FindScopeRoot, ClassifySecurityCandidate ueber
        // ResolveSurroundingName, ClassifyLocalizationCandidate ueber ArgumentSyntax-Kette,
        // HasDisableComment ueber Vorfahren-Walk) sind defensiv implementiert: ihre
        // Schleifen iterieren `for (var current = literal.Parent; current is not null;
        // current = current.Parent)`, terminieren also bei synthetic.Parent == null
        // automatisch ohne NullReferenceException.
        foreach (var content in node.Contents)
        {
            if (content is not InterpolatedStringTextSyntax text) continue;
            var textValue = text.TextToken.ValueText;
            if (string.IsNullOrEmpty(textValue)) continue;
            var synthetic = SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(textValue, textValue));
            ProcessLiteral(synthetic, node.GetLocation());
        }

        base.VisitInterpolatedStringExpression(node);
    }

    private void ProcessLiteral(LiteralExpressionSyntax node, Location? location = null)
    {
        if (!IsInScope(node.Kind())) return;

        // enum_candidates-Skip: ein Literal, das bereits in einer if-else-/switch-Kaskade
        // als enum-Kandidat klassifiziert wurde, darf nicht doppelt (z. B. als
        // ConfigCandidates via Connection-String-Heuristik) gemeldet werden.
        if (enumClassifiedLiterals.Contains(node)) return;

        var (valueType, value) = ExtractValue(node);
        if (value is null) return;

        var classification = MagicValuesClassifier.Classify(
            node, context.Model, context.IgnoreNumbers, new MagicValueClassifierOptions(
                IncludeTests: false,
                IncludeSuppressed: context.IncludeSuppressed,
                IsTestPath: context.IsTestPath));
        if (!classification.IsMagic) return;

        if (context.CategoryFilter is not null && classification.Category != context.CategoryFilter.Value) return;

        // location-Override fuer synthetische Literale aus interpolierten Strings: die
        // echte Quellcode-Position steht am InterpolatedStringExpressionSyntax-Knoten,
        // nicht am synthetischen Literal (das per Default-Location (0,0) liefert).
        var effectiveLocation = location ?? node.GetLocation();
        var lineSpan = effectiveLocation.GetLineSpan();
        var line = lineSpan.StartLinePosition.Line + 1;
        var column = lineSpan.StartLinePosition.Character + 1;

        context.Sink.Add(new RawMagicValue(context.FilePath, line, column, valueType, value, classification));
    }

    private bool IsInScope(SyntaxKind kind) => kind switch
    {
        SyntaxKind.StringLiteralExpression or SyntaxKind.Utf8StringLiteralExpression
            => context.ValueTypeFilter is null or MagicValueValueType.String,
        SyntaxKind.CharacterLiteralExpression => false, // char-Literale nicht gemeldet (CWE-787 / Encoding-Risiken, hier out-of-scope)
        SyntaxKind.NumericLiteralExpression
            => context.ValueTypeFilter is null or MagicValueValueType.Number,
        _ => false,
    };

    private static (MagicValueValueType, string?) ExtractValue(LiteralExpressionSyntax node)
    {
        switch (node.Kind())
        {
            case SyntaxKind.StringLiteralExpression:
            case SyntaxKind.Utf8StringLiteralExpression:
                return (MagicValueValueType.String, node.Token.ValueText);
            case SyntaxKind.NumericLiteralExpression:
            {
                if (node.Token.Value is null) return (MagicValueValueType.Number, null);
                // InvariantCulture: locale-unabhaengige Repraesentation (z. B. "0.19" statt
                // "0,19" auf de-DE), damit Tests und JSON-Output stabil sind.
                return (MagicValueValueType.Number, Convert.ToString(node.Token.Value, System.Globalization.CultureInfo.InvariantCulture));
            }
            default:
                return (MagicValueValueType.String, null);
        }
    }
}
