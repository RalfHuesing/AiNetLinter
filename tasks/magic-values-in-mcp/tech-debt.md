---
task: magic-values-in-mcp
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-14T21:55:00+02:00
---

# Tech-Debt-Log: magic-values-in-mcp

Append-only. Jeder Eintrag ist eine vom Kritiker während eines
Step-Reviews beobachtete, aber bewusst **nicht** gefixte Auffälligkeit
außerhalb des Scopes des jeweiligen Steps (Architektur, Anti-Pattern,
Duplikation, Konsistenz) — siehe `../spec.md` §8.3/§9.

**Priorität ist reine Sortierhilfe für den Menschen, kein Auslöser.**
Bewusst `hoch`/`mittel`/`niedrig` (deutsch) statt `CRITICAL`/`MAJOR`/
`MINOR`, um jede Verwechslung mit den blockierenden Findings in
`step-review.md` auszuschließen — kein Eintrag hier führt automatisch zu
einem eigenen Korrektur-Step oder einem neuen Epic. Das entscheidet
grundsätzlich der Nutzer (manuell, z. B. durch Ergänzen eines Epics in
`roadmap.md` mit Verweis auf die Tech-Debt-ID).

**`auto_fixable` (`ja`/`nein`, siehe `../spec.md` §9.1) ist die einzige
Ausnahme:** rein mechanische, entscheidungsfreie Fixes ohne
Architektur-Ermessen dürfen vom Planer opportunistisch an einen ohnehin
laufenden Step angehängt werden (§10.6) — kein eigener Step, kein
eigener Sweep. Default bei Unsicherheit ist `nein`.

## Index

| ID | Bereich / Datei | Priorität | Auto-Fixable | Kurzfassung |
|---|---|---|---|---|
| TD-001 | `Mcp/OverviewResourceRegistration.cs` + 3 Test-Dateien | mittel | nein | Tool-Count muss bei jedem Tool-Add an drei Stellen manuell synchronisiert werden — zentrale Konstante fehlt. |
| TD-002 | `src\AiNetLinter\Mcp\Tools\MagicValues\MagicValuesStringHeuristics.cs` | niedrig | nein | `localization_candidates`-Heuristik deckt nur Exception-Konstruktoren ab; UI-Prompts/Logins fehlen (Caller-Type-Heuristik nicht umgesetzt). |

## Einträge

### TD-001 — Tool-Count-Drift über drei Test-Dateien [Priorität: mittel] [Auto-Fixable: nein]

- **Gefunden in:** step-001 (Kritiker-Review vom 2026-08-14)
- **Ort:** `src/AiNetLinter.FastTests/Mcp/OverviewResourceRegistrationTests.cs:BuildOverviewText_ListsAllNineteenTools`, `src/AiNetLinter.IntegrationTests/Mcp/McpDocumentationSmokeTests.cs:AgentApi_CountsCsharpOnlyToolsCorrectly`, `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandContractTests.cs:RunAsync_ValidFixture_ServerRespondsWithNineteenTools` (Hardcoded-Magic-Numbers `19`/`13`/`19`).
- **Befund:** Beim Hinzufügen eines neuen MCP-Tools müssen drei voneinander unabhängige Test-Dateien mit-aktualisiert werden, damit sie den neuen Tool-Count akzeptieren. Keine zentrale Source-of-Truth — die Tests kennen den Soll-Count nicht aus `McpServerOptionsFactory` oder `OverviewResourceRegistration.ToolSummaries.Count`, sondern aus hartkodierten Literalen. Bei einem Schritt mit schlichter Ergänzung (z. B. EPIC-2-`security_candidates`) ist das ein weiterer Drift-Punkt, der leicht vergessen wird.
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von step-001; die drei Test-Dateien sind Bestand und der Refactor würde zentrale Konstanten-Sichtbarkeit + Test-API-Design (z. B. `internal const int CurrentToolCount`) berühren, was eine eigene Planer-/Nutzer-Entscheidung verdient.
- **Vorschlag:** `internal const int CurrentToolCount = N;` in `McpServerOptionsFactory` oder `OverviewResourceRegistration` exportieren; die drei Tests darauf umstellen. Analog für den C#-only-Subset (`McpDocumentationSmokeTests`-`12`/`13`-Konstante). Konsolidierung kann im selben Epic wie das nächste Tool-Add erfolgen.
- **Auto-Fixable:** nein — berührt Test-API-Design und Konstanten-Sichtbarkeit, ist nicht rein mechanisch.
- **Status:** offen

### TD-002 — `localization_candidates` deckt nur Exception-Konstruktoren ab (UI-Prompts/Logins fehlen) [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-003 (Coder-Result vom 2026-08-15)
- **Ort:** `src\AiNetLinter\Mcp\Tools\MagicValues\MagicValuesStringHeuristics.ClassifyLocalizationCandidate` — die Heuristik matcht nur String-Literale, die (a) Argument in einem Exception-Konstruktor sind und (b) eine effektive Länge > 15 Zeichen haben.
- **Befund:** Konzept §Muss-Haven nennt „User-Facing Nachrichtentexte in Exceptions, UI-Prompts oder Logins". EPIC-2 setzt nur den Exception-Teil um. UI-Prompts (z. B. `ShowDialog("Bitte bestätigen Sie...")`) und Login-Flows (z. B. `Console.WriteLine("Enter password:")`) würden ebenfalls `localization_candidates` sein, brauchen aber eine Caller-Type-Heuristik (z. B. UI-Framework-Imports, `Console.WriteLine`-Symbol-Name, `ILogger`-Wrapper).
- **Warum nicht sofort gefixt:** Caller-Type-Erkennung ist Architektur-Ermessen (welche UI-Frameworks? WPF/WinUI/Blazor/Console? projektspezifisch konfigurierbar?). Wäre eigener Planer-Aufruf wert. Pragmatik mit Exception-Konstruktor + Längenschwelle ist die kleinste, false-positive-ärmste Variante und entspricht dem in `agent-api.md` dokumentierten Verhalten.
- **Vorschlag:** Folge-Task: Caller-Type-Heuristik mit projektspezifischer Konfiguration (welche Methoden/Namespaces/Frameworks lösen `localization_candidates` aus). Konfigurationsdatei (z. B. `rules.json`-Erweiterung um `localizationCallerTypes: ["ShowDialog", "Console.WriteLine", ...]`) als Mechanismus.
- **Auto-Fixable:** nein — Caller-Type-Set und Konfigurationsformat sind Architektur-Entscheidungen.
- **Status:** offen
