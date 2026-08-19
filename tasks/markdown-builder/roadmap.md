---
status: active
task: markdown-builder
derived_from: konzept.md
created_at: 2026-08-19
last_updated: 2026-08-19
updated_at_step: step-003
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
---

# Roadmap: markdown-builder

Grober Anker, kein Detailplan — Detail-Steps entstehen erst JIT im
Step-Modus des Planers, siehe `../../.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md` §7.2. Diese Datei wird
laufend angepasst (Epics abgehakt, ergänzt, umformuliert oder als
obsolet markiert) — kein starres Vorab-Dokument.

## Tech-Stack-Notiz

Aus dem Projekt abgeleitet, einmalig hier (nicht pro Step neu):

- **Build-Command:** `dotnet build` (alle vier Projekte der `AiNetLinter.slnx`, `TreatWarningsAsErrors = true` — keine Warnungen erlaubt).
- **Test-Commands (Abschluss-Gate):**
  - `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
  - `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
  - Beide MÜSSEN grün sein, bevor der Task als abgeschlossen gilt.
- **Schnelle Iteration (während Entwicklung):** `dotnet test src/AiNetLinter.FastTests --filter Category=Unit` (bzw. `Category=Component`, <10s).
- **Stress-Kategorie (nur manuell):** `dotnet test src/AiNetLinter.IntegrationTests --filter Category=Stress` — niemals automatisch im Volllauf; betrifft 16-fach-parallele Server-Subprozesse (~150s).
- **Lint-Command:** keiner extern — AiNetLinter ist selbst Dogfood-Linter; Sync der generierten Regeln via `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`.
- **Code-Style-Kurzfassung** (aus `AiNetLinter.mdc` + `AiNetLinterRichtlinien.mdc`):
  - `sealed` für konkrete Klassen, `#nullable enable` am Dateianfang.
  - Methoden ≤60 Zeilen (≤150 mit CC≤3 & CogC≤5 Compound-Suppression), `MaxMethodParameterCount: 4` → Input-`record` ab 5 Parametern.
  - xUnit v3 Tests, `[Trait("Category", "Unit"|"Component"|"Integration"|"Dogfood"|"Performance"|"Stress")]`.
  - Kein `dynamic`, `out` nur in `Try*`, kein leeres `catch`, `Result<T>` bevorzugt, sparsame Kommentare (kein Task-/Step-Bezug wie `TD-005`).
  - Testklassen ohne `sealed`, `*Tests` mit `MaxMethodLineCount: 100`.
  - Schicht-Trennung: `Maps →` darf nicht `Mcp.Tools` referenzieren (siehe Konzept Prio 4 Hotspot-Duplikation).
  - MCP-Server `ainetlinter` ist für C#-Symbol-/Violation-Queries bevorzugt zu nutzen statt `rg`/`grep` (Dogfooding).
- **Commit-Konventionen:**
  - Conventional Commits auf Deutsch, imperativ (z. B. `feat:`, `fix:`, `docs:`, `chore:`, `refactor:`).
  - **Pflicht-Suffix `[markdown-builder]`** im Subject jedes Task-Commits (auch in Code-Commits), Kurzname = `tasks/markdown-builder` → `markdown-builder`.
  - Commit-Body trägt `Refs: tasks/markdown-builder/step-NNN`-Trailer (siehe Konzept §10.3).
  - Jede Antwort mit Datei-Änderungen schließt mit `### Commit-Vorschlag`-Block ab (reiner Commit-Text, keine Shell-Befehle).
  - **Kein** `git commit --amend`, **kein** `rebase`, **kein** Force-Push; `step-result.md`-Hashes werden dadurch ungültig.
- **Sprache:** Code/Identifier Englisch, Meldungen/Doku-User-Text Deutsch.

## Regel-Index

Ein Eintrag pro Datei in `<rules_dir>/**` — Kurzbeschreibung, kein Volltext. Zweck: Der Step-Modus-Planer ist pro Aufruf eine frische, isolierte Session ohne Erinnerung an diesen Roadmap-Modus-Aufruf — er kann `<rules_dir>/**` nicht bei jedem Step neu komplett lesen (Kosten), liest aber diesen Index (steht ja schon hier in `roadmap.md`) und dann gezielt nur die 1-2 Dateien, die zum aktuellen Step passen, siehe `spec.md` §7.2 / `skills/planer/SKILL.md` Schritt 4a.

- `.agents/rules/AiNetLinter.mdc` — Auto-generiert aus `rules.json` (via `--sync-agent-rules-only`): harte Code-Qualitäts-Metriken (MaxLineCount 500, MaxMethodLineCount 60/100-override, MaxMethodParameterCount 4, MaxCyclomaticComplexity 12, MaxCognitiveComplexity 15, AIContextFootprint 2500, Sealed-Pflicht, kein dynamic/out-außer-Try, Nullable-Enable, Compound-Suppressions).
- `.agents/rules/AiNetLinterRichtlinien.mdc` — Manuell gepflegte Architektur-/Workflow-/Verhaltensregeln: monolithisch/kein Plugin/kein DI-Container/kein ALC, PowerShell-7-Pflicht, Git `--no-pager`, Zero-Warning-Direktive, Commit-Pflichtblock, MCP-Dogfooding, Doku-Objektivität (keine unbelegten Superlative, nur Implementiertes dokumentieren, Pflicht zur Code-Verifikation).

## Epics

Ein Epic = grober Cluster mehrerer Steps, kein einzelner Step. Epics sind bewusst **groß** gehalten (Ralf-Vorgabe 2026-08-19: „sinnvoll große Code Steps, keine Mini oder Micro Steps") — der Step-Modus-Planer zerlegt jedes Epic in 3–5 committbare, in einer Review-Runde prüfbare Steps.

- [x] **EPIC-01: MarkdownBuilder-Foundation + Bug-Fix-Callsites umstellen** — Bündelt Konzept-Schritte 2 + 3 + 4 + 5 + 6 (Builder-Klasse anlegen, Testklasse anlegen, Callsites mit aktiven Escaping-Bugs umstellen). Konzept-Referenz: §2 (API), §3 Prio 1–3, §4 (Teststrategie), §8 Schritte 2–6. Abgeschlossen durch step-001 (`fc603681`) + step-002 (`b1a39ab1`, beide `approved`): `MarkdownBuilder.cs` 167 Z. mit `ColumnAlign` / `MarkdownTableBuilder` (inkl. `BuildHeaderLine` / `BuildSeparatorLine` / `BuildRowLine` + `FormatRow`-Helper) / `MarkdownBuilder` (fluent: `Heading` / `BlankLine` / `Line` / `BulletList` / `CodeBlock` / `Table(Action<>)` / `Table(MarkdownTableBuilder)` / `AppendTo` / `Build`), `MarkdownBuilderTests` 30/30, drei Bug-Fix-Callsites (`GetClassStructureTool` Prio 1, `GetViolationsScanner` Prio 2, `ViolationMarkdownFormatter.BuildSummaryTable` Prio 3) migriert und byte-stabil verriegelt.
  - *Ziel 1:* `src/AiNetLinter/Output/MarkdownBuilder.cs` neu — `ColumnAlign` Enum, `MarkdownTableBuilder` (mit beiden Table-Überladungen `Action<>` und Instanz-Übergabe) und `MarkdownBuilder` (fluent: `Heading`, `BlankLine`, `Line`, `BulletList`, `CodeBlock`, `Table`); Escaping `|` → `\|`, `\r`/`\n` → Leerzeichen, leer/whitespace → `"-"`.
  - *Ziel 2:* `src/AiNetLinter.FastTests/Output/MarkdownBuilderTests.cs` neu (≥22 Testfälle aus Konzept §4).
  - *Ziel 3:* Drei Callsites mit aktiven Bugs umstellen — `GetClassStructureTool.cs` (Bug: `v.Signature` mit `|` zerschießt Tabelle, `FormatMemberRow` wird toter Code), `GetViolationsScanner.cs` (Bug: `v.Details` unescaped + Snippet-Block in derselben Schleife), `ViolationMarkdownFormatter.cs` `BuildSummaryTable` (bedingte `hasStructural`-Spalte). Eingerückter Code-Block in `AppendViolationItem` bleibt absichtlich unverändert.
  - *Definition of Done:* Builder + Tests grün, `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` grün, `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` grün, `FormatMemberRow` gelöscht, `ViolationMarkdownFormatterTests` weiterhin grün, Diff im Markdown-Output **strukturell byte-stabil** (Header, Separator, Spaltenreihenfolge, Reihenfolge, Leerzeilen unverändert) und im Cell-Content **`EscapeCell`-konform** (leer/whitespace Cells emittieren `-`, Pipes werden `\|`); kein User-sichtbarer Drift in der dokumentierten Struktur.
- [x] **EPIC-02: Verbleibende Callsites umstellen + HotspotSectionFormatter entfernen** — Bündelt Konzept-Schritte 7 + 8 + 9 + 10 + 11 + 12 (Hotspot-Cleanup + die sechs übrigen, seit Konzept-Stand neu hinzugekommenen oder übersehenen Callsites). Konzept-Referenz: §3 Prio 4–10, §5 Mängel-Liste, §8 Schritte 7–12. Abgeschlossen durch step-003 (`107b2682`) + step-004 (`337c002e`) + step-005 (alle `approved`): `HotspotSectionFormatter` gelöscht, alle 10 Callsites (`GetClassStructureTool` Prio 1, `GetViolationsScanner` Prio 2, `ViolationMarkdownFormatter.BuildSummaryTable` Prio 3, `GetHotspotsScanner`/`HotspotMapBuilder` Prio 4, `RepoPlaybookGenerator` Prio 5, `ListRulesCommand` Prio 6, `AgentRulesGenerator.AppendMetricsTable` Prio 7, `AgentRulesGenerator.AppendCompoundSuppressions` Prio 8, `MetricsLookupFormatter` Prio 9, `GetSymbolBodyTool` Prio 10) vollständig und byte-stabil auf `MarkdownBuilder`/`MarkdownTableBuilder` migriert. TD-001 und TD-002 vollständig behoben/obsolet.
  - *Ziel 1:* `HotspotSectionFormatter.cs` (44 Zeilen) löschen — beide Aufrufer (`GetHotspotsScanner.cs`, `HotspotMapBuilder.cs`) bekommen je eine private `AppendHotspotSection`-Methode mit `MarkdownBuilder`. Duplikation wird akzeptiert und in Tech-Debt-Log aufgenommen (Schicht-Trennung `Maps → Mcp.Tools` verbietet gemeinsamen Helper).
  - *Ziel 2:* Sechs weitere Callsites umstellen — `RepoPlaybookGenerator.cs AppendAgentPriority` (Prio 5, „leer"-Sonderfall), `ListRulesCommand.cs ListAll` (Prio 6), `AgentRulesGenerator.cs AppendMetricsTable` (Prio 7) und `AppendCompoundSuppressions` (Prio 8, neu im Konzept aufgenommen), `MetricsLookupFormatter.cs` `Format` + `FormatMethodDetails` + `FormatTypeDetails` + `FormatPropertyDetails` (Prio 9, neu, vier Pattern-Arten in einer Methode, Signatur-Wechsel `StringBuilder sb` → `MarkdownBuilder mb`), `GetSymbolBodyTool.cs` (Prio 10, neu, single-line Code-Block).
  - *Ziel 3:* Drei Sonderfälle dokumentiert **nicht** umbauen: `SkeletonMarkdownRenderer.cs` (line-by-line Code-Block), `ViolationMarkdownFormatter.cs AppendViolationItem` (2-Space-eingerückter Code-Block), `ViolationMarkdownFormatter.cs AppendViolationItem` Snippet-Block.
  - *Definition of Done:* Alle 10 Callsites migriert, `HotspotSectionFormatter.cs` aus Solution entfernt, beide Test-Suites grün, bestehende MCP-Tests (`McpServerCommand*Tests`, `McpLiveRepositoryTests`) grün — die Output-Bytes von `metrics_lookup` und `get_symbol_body` MÜSSEN weiterhin identisch sein (Token-Vertrag gegenüber Agenten).
