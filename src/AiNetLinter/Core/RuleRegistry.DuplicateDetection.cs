#nullable enable

using AiNetLinter.Configuration;

namespace AiNetLinter.Core;

internal static partial class RuleRegistry
{
    private static RuleMetadata[] BuildDuplicateDetectionRules() =>
    [
        new(
            RuleId: LinterRuleIds.DuplicateCode,
            DisplayName: "Duplicate Code",
            GetShortDescription: c => $"Methode ist ein fast identischer Klon einer anderen Methode (min. {c.Global.DuplicateCodeMinTokens} Tokens, Jaccard >= {c.Global.DuplicateCodeExactThreshold:F2}).",
            Warum: "Code-Duplikation entsteht, wenn eine bereits existierende Loesung nicht wiedergefunden wird — bei autonomer agentischer Entwicklung ein systematisches Muster, weil der Agent den vorhandenen Code nicht vollstaendig ueberblickt. Anders als die meisten Regeln hier ist dies ein Kandidaten-Befund, kein hartes Anti-Pattern: manuelle/LLM-Bewertung vor einer Konsolidierung bleibt noetig (siehe tasks/features/07-drift-audit-ideen.md).",
            Alternativen:
            [
                "**Gemeinsame Methode extrahieren**: Die geteilte Logik in eine wiederverwendbare Methode/Klasse auslagern und von allen beteiligten Stellen aufrufen.",
                "**find_duplicates(mode=\"refactoring-drift\")**: Pruefen, ob bereits ein zentraler Helper existiert, der stattdessen aufgerufen werden sollte.",
                "**Gezielt unterdruecken**: Falls die Aehnlichkeit beabsichtigt ist (strukturell gleiche, aber fachlich unterschiedliche Methoden) — '// ainetlinter-disable DuplicateCode' statt die Regel global zu deaktivieren."
            ],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "info",
            AgentHint: "Fast identische Methode gefunden — konsolidieren oder Aehnlichkeit bewusst begruenden.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.EnableDuplicateCodeCheck,
            IsMetric: false,
            IncludeInAgentRules: true,
            ConfigKeyHint: "rules.json → Global.DuplicateCode* (MinTokens, ExactThreshold, MaxResults, ...) | find_duplicates-MCP-Tool fuer near/fuzzy-Kandidaten"
        ),
    ];
}
