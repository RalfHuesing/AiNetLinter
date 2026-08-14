#nullable enable

using System.Collections.Generic;
using AiNetLinter.Core;

namespace AiNetLinter.Mcp.Tools.PatternDetect;

/// <summary>
/// Statische Pattern-ID → RuleId(s)-Zuordnung für <c>pattern_detect</c>. Deckt 6 Patterns ab,
/// die bereits über vollwertige, produktive Linter-Regeln/Checker verfügen. Die anderen 3
/// (deep-nesting, disposable-not-disposed, static-state) haben keine existierende Erkennung
/// und würden komplett neue Roslyn-Syntax-Walker mit eigenem False-Positive-Risiko erfordern
/// — bewusst zurückgestellt (analog zum <c>method_count</c>-Präzedenzfall bei <c>metrics_tree</c>).
/// Magic-Value-Funde sind nicht Teil von <c>pattern_detect</c> — sie werden vom separaten
/// On-Demand-Audit-Tool <c>find_magic_values</c>
/// (<see cref="Mcp.Tools.MagicValues.FindMagicValuesTool"/>) abgedeckt, das dieselbe Domäne
/// (Literale/Schwellenwerte/URLs/...) ohne Bindung an die <see cref="LinterEngine"/>-Checker
/// klassifiziert. Reine Aggregation über bereits von der <see cref="LinterEngine"/> erzeugte
/// <see cref="AiNetLinter.Models.RuleViolation"/>-Objekte — kein neuer Detection-Code.
///
/// Jede Violation gehört zu genau einem Pattern (die 6 RuleId-Gruppen überschneiden sich nicht).
/// Bei <c>god-class</c> können mehrere Regeln (AIContextFootprint/MaxPublicMembersPerType/
/// MaxLineCount) auf derselben Klasse gleichzeitig treffen — das sind trotzdem separate Items
/// in der Trefferliste, keine Dedupe-Logik (identisch zu <c>get_violations</c>).
/// </summary>
internal static class PatternCatalog
{
    /// <summary>
    /// "AvoidExcessiveMiddleMen" hat keine <see cref="LinterRuleIds"/>-Konstante (analog zu
    /// <c>RuleRegistry.General.cs</c>, das dieselbe RuleId ebenfalls als Literal führt) —
    /// deshalb hier ebenfalls als Literal statt via nameof().
    /// </summary>
    private const string AvoidExcessiveMiddleMenRuleId = "AvoidExcessiveMiddleMen";

    internal static readonly IReadOnlyList<PatternDefinition> Patterns =
    [
        new PatternDefinition(
            "god-class",
            "Klassen mit zu grossem AI-Context-Footprint, zu vielen Public-Members oder zu vielen Zeilen.",
            [LinterRuleIds.AIContextFootprint, LinterRuleIds.MaxPublicMembersPerType, LinterRuleIds.MaxLineCount]),
        new PatternDefinition(
            "async-void",
            "async void Methoden oder Local Functions statt async Task — Exceptions koennen nicht awaited/gefangen werden.",
            [LinterRuleIds.BanAsyncVoid]),
        new PatternDefinition(
            "long-method",
            "Methoden mit zu vielen Zeilen oder zu hoher zyklomatischer/kognitiver Komplexitaet.",
            [LinterRuleIds.MaxMethodLineCount, LinterRuleIds.MaxCyclomaticComplexity, LinterRuleIds.MaxCognitiveComplexity]),
        new PatternDefinition(
            "public-without-doc",
            "Oeffentliche Member ohne XML-Dokumentationskommentar.",
            [LinterRuleIds.EnforceXmlDocumentation]),
        new PatternDefinition(
            "empty-catch",
            "Catch-Bloecke, die eine Exception stillschweigend verschlucken.",
            [LinterRuleIds.EnforceNoSilentCatch]),
        new PatternDefinition(
            "feature-envy",
            "Klassen, die ueberwiegend Aufrufe an ein anderes Objekt weiterleiten (Middle-Man — " +
            "die naechste existierende Naeherung, kein 1:1-Match zum klassischen Feature-Envy-Begriff).",
            [AvoidExcessiveMiddleMenRuleId]),
    ];
}

/// <summary>Einzelner Pattern-Katalogeintrag: stabile <paramref name="Id"/> (Tool-Parameter-Wert),
/// deutsche Kurzbeschreibung und die zugeordneten <see cref="LinterRuleIds"/>-Werte.</summary>
internal sealed record PatternDefinition(string Id, string Description, IReadOnlyList<string> RuleIds);
