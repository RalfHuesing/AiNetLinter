---
status: active
task: magic-values-in-mcp
derived_from: konzept.md
created_at: 2026-08-14T20:36:21+02:00
last_updated: 2026-08-15T17:21:00+02:00
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
---

# Roadmap: magic-values-in-mcp

Grober Anker, kein Detailplan — Detail-Steps entstehen erst JIT im
Step-Modus des Planers, siehe `../spec.md` §7.2. Diese Datei wird
laufend angepasst (Epics abgehakt, ergänzt, umformuliert oder als
obsolet markiert) — kein starres Vorab-Dokument.

## Tech-Stack-Notiz

- **Stack:** C# / .NET 10, Roslyn AST (`SyntaxWalker` & `SemanticModel`), xUnit v3.
- **Build-Command:** `dotnet build`
- **Test-Command (Pflicht-Gate):**
  - `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
  - `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
- **Lint/Doku-Sync-Command:** `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`
- **Code-Style-Kurzfassung:** `sealed` für konkrete Klassen; Methoden ≤ 60 Zeilen; ab 5 Parametern Input-`record`; `#nullable enable` am Dateianfang; kein leeres `catch`; kein `dynamic`; `out` nur in `Try*`-Methoden; `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`; xUnit v3; keine `[Collection(...)]`-Zwangsserialisierung; `TestCategory` korrekt setzen (Unit/Component/Integration/Dogfood/Performance, **nicht** Stress für nicht-lastintensive Tests); MCP-Server `ainetlinter` für Symbol-/Violation-Abfragen statt `rg`/`grep` (Dogfooding).
- **Commit-Konventionen:** Conventional Commits auf Deutsch, imperativ, Subject ≤ 72 Zeichen, Suffix `[magic-values-in-mcp]`, Trailer `Refs: tasks/magic-values-in-mcp/step-NNN`. Commit-Vorschlag-Pflicht am Antwortende.
- **Shell:** PowerShell 7, `git --no-pager`, kein `sed -i`, keine Bash-Kettung.
- **Tests-Verzeichnis (KORREKTUR zum Konzept):** Konzept §„Wo im Projekt" nennt `src/AiNetLinter.Tests/Mcp/Tools/` — dieses Verzeichnis existiert nicht (Legacy-Projekt `AiNetLinter.Tests` wurde in Commit `7a596b` quarantäniert und aufgelöst). Tatsächliche Pfade:
  - Unit/Component-Tests: `src/AiNetLinter.FastTests/Mcp/Tools/FindMagicValuesScannerTests.cs` mit `[Trait("Category", "Unit")]` oder `[Trait("Category", "Component")]`.
  - Integration-Tests: `src/AiNetLinter.IntegrationTests/Mcp/Tools/FindMagicValuesToolTests.cs` mit `[Trait("Category", "Integration")]`.
- **Konzept-Diskrepanz `Wo im Projekt` ↔ Realität:** Konzept nicht ändern (Nutzer-Eigentum) — Korrektur wird über die CodeMap an den Coder weitergegeben.

## Regel-Index

Pro Datei in `.agents/rules/**` — Kurzbeschreibung, kein Volltext. Der
Step-Modus-Planer liest diesen Index und dann gezielt nur die 1-2 Dateien,
die zum aktuellen Step passen.

- `.agents/rules/AiNetLinterRichtlinien.mdc` — verbindliche Architektur-, Workflow-, Kommentar- und Commit-Leitplanken für das gesamte AiNetLinter-Projekt (z. B. keine DI, monolithisch, Windows/PowerShell, xUnit v3, Commit-Vorschlag-Pflicht, Verbot von Task-Artefakt-Verweisen in Code-Kommentaren).
- `.agents/rules/AiNetLinter.mdc` — automatisch generierte Übersicht der aktiven Linter-Regeln und Code-Qualitäts-Grenzwerte (Sealed, MaxLineCount, EnforceNullableEnable, etc.), gesynct via `--sync-agent-rules-only`.

## Epics

- [x] **EPIC-1: `find_magic_values` — Tool-Core, Basis-Klassifizierung & Doku-Sync.** Roslyn-`SyntaxWalker` (LiteralExpression, Raw String Literal, statische Text-Segmente in `InterpolatedStringExpressionSyntax`), `SemanticModel` für Parameternamen/Zuweisungsvariablen, Trivial-Filterung (`0`/`1`/`-1`/`""`/`" "`/`"\n"`/`true`/`false`/`null`, Index/Loop, Attribut, `GetHashCode()`-Sonderfall), Konfigurations- und Konstanten-Kategorien (URLs, Pfade, Timeouts, Schwellenwerte, Format-Strings), Trunkierung via `McpTruncation` (Default 50), `StructuredContent` als Objekt-Wrapper `{ MagicValues: [...] }` (kein nacktes Array), Registrierung in `AnalysisToolRegistrations.cs`, Eintrag in `Docs/agent-api.md` Tool-Tabelle (wird damit Tool Nr. 19), PatternCatalog-Kommentar zu „magic-numbers" aktualisiert, `Docs/ROADMAP.md`-Eintrag. Bezug: `konzept.md` §Muss-Haven (Basis-Block: vollständige Erfassung, `minOccurrences=1`, `maxResults`, StructuredContent, Ziel-Fokus C#), §„Wo im Projekt", §„Definition of Done" (Tool-Registrierung, Trunkierung, DoD-Punkte 1-7). → **Abgehakt durch step-001 (done) + step-002 (Korrektur für `VisitInterpolatedStringExpression`-No-op, approved).**

- [x] **EPIC-2: Erweiterte Heuristiken, Eingrenzung, Suppression & Doku-Abschluss.** `nameof_candidates` (Scope-Vergleich gegen Parameter/Member/Typ-Namen), `enum_candidates` (Switch/If-Kaskaden, AST-Vergleich von Identifiern gegen Literale in Verzweigungen), `standard_candidates` (HTTP-Statuscodes → `StatusCodes.StatusXXXNotFound`), `security_candidates` (Namens-Heuristik `password`/`secret`/`apiKey`/`token`/`connectionString` kombiniert mit nicht-trivialem String-Literal als Wert, plus Präfix-Muster `AKIA…`/`sk-…`; **kein Entropie-Algorithmus** — bewusst Non-Goal), Erkennung duplizierter `private const`/`internal const`-Felder über Klassengrenzen hinweg (Aggregation `FieldDeclarationSyntax` mit `const`-Modifier, Ziel-Empfehlung „Hochstufung in gemeinsame Konstanten-Klasse" — Bestandsfund im Projekt: `WarnThreshold = 0.80` in `HotspotMapBuilder.cs:23` und `GetHotspotsScanner.cs:27`), Suppression via `SyntaxTrivia` (Leading/Trailing Trivia am jeweiligen `SyntaxNode`, **nicht** dateiweite `SuppressionScanner`-Semantik), `changedOnly`-Parameter (bool, Default false; semantisch äquivalent zu „leerer `gitRef` = uncommittete Änderungen" — Diff-Logik aus `DiffImpactAnalyzer.ParseGitDiffHunks` wiederverwenden, eigene Set-Semantik statt Duplikat des `get_impact`-`gitRef`-Strings), `includeTests` (Default false), `includeSuppressed` (Default false), Suppression-Sonderfall-Hinweis (pro-Fundstelle statt dateiweit) in `Docs/agent-api.md`. Tests für alle neuen Heuristiken, Suppression-Granularität und `changedOnly`. Bezug: `konzept.md` §Muss-Haven (Suppression-Block, Diff-Scope-Block, restliche Kategorien), §„Verworfene Alternativen" (Suppression-Begründung), §„Definition of Done" (DoD-Punkte 8-12). → **Abgehakt durch step-003 (approved).**
