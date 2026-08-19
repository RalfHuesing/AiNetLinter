---
status: done
type: step-review
task: markdown-builder
step: 003
epic: EPIC-02
step_type: single
reviewed_by: kritiker
reviewed_by_model: MiniMax-M3
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-19
verdict: approved
tech_debt_ids: []
---

# Review Step 003: EPIC-02 Welle 1 — HotspotSectionFormatter löschen + ListRulesCommand + GetSymbolBodyTool

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Korrektur-Step `step-<MMM>` angelegt (`corrects: step-003`)
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (oder Baselines beachtet)

## Befund

### Plan-Erfüllung

Alle fünf Plan-Bereiche umgesetzt (HotspotSectionFormatter gelöscht, `GetHotspotsScanner.AppendHotspotSection` Z.127–153 mit `MarkdownBuilder` + `Table(t => ...)`-Callback, `HotspotMapBuilder.AppendHotspotSection` Z.87–113 dupliziert, `ListRulesCommand.ListAll` mit `MarkdownTableBuilder` + 5 Spalten, `GetSymbolBodyTool` mit `Heading(3)`+`BlankLine`+`Line`+`BlankLine`+`CodeBlock`+`.TrimEnd()`); 11/11 `GetHotspotsToolTests` + 8/8 `ListRulesCommandTests` + 6/6 `GetSymbolBodyToolTests` (Prio-4/6/10 verriegelt) + 22/22 `McpLiveRepositoryTests` (MCP-Token-Vertrag) bestätigen Byte-Stabilität; CodeMap im Doc-Commit `77ac9992` aktualisiert (HotspotSectionFormatter-Eintrag entfernt, vier Callsites als „umgebaut (EPIC-02, step-003)" markiert); Code-Commit `107b2682` trägt Suffix `[markdown-builder]` + `Refs: tasks/markdown-builder/step-003`-Trailer + Pflicht-`### Commit-Vorschlag`-Block; drei dokumentierte Abweichungen (`using AiNetLinter.Output;` bleibt nötig für `MarkdownBuilder`/`ColumnAlign` in beiden Hotspot-Dateien, `ainetlinter-disable DuplicateCode`-Marker in Scanner-Methode, Doc-XML-Kreuzverweis von `HotspotSectionFormatter` auf Schicht-Trennung-Begründung umgeschrieben) sind alle nachvollziehbar begründet und in `step-result.md` dokumentiert.

### Rules-Konformität

`internal static` für die beiden `AppendHotspotSection`-Helper (statische Helper-Klassen, kein `sealed` nötig); `#nullable enable` an Z.1 in allen vier geänderten Dateien; `MaxMethodLineCount: 60` deutlich unterschritten (`AppendHotspotSection` je 27 Z. inkl. Signatur, `ListAll`-Block ~14 Z., `GetSymbolBodyTool`-Migration ~10 Z.); `MaxMethodParameterCount: 4` exakt erreicht (Plan hatte das gesehen, „zulässig am Limit"); `MaxLineCount: 500` in allen Dateien klar eingehalten; xUnit v3-Test-Klassen-Konventionen nicht betroffen (keine neuen Tests); keine `// step-003` / `// EPIC-02` / Task-IDs in Code-Kommentaren (per `grep` über `src/` und `tasks/` verifiziert, nur Doku-/Task-Files referenzieren die Namen — erlaubt); sparsame Kommentare — der `ainetlinter-disable`-Marker enthält die Schicht-Trennung-Begründung inline in derselben Zeile (projektübliche Konvention aus `Docs/configuration.md` Z.385–389); Zero-Warning-Direktive eingehalten (Build 0/0).

### Logische Korrektheit

Byte-Stabilität `GetSymbolBodyTool`: `.TrimEnd()` ist zwingend und korrekt — `MarkdownBuilder.CodeBlock` (`src/AiNetLinter/Output/MarkdownBuilder.cs:145`) emittiert nach `\`\`\`` immer ein `\n`, das Original aber nicht; `.TrimEnd()` entfernt genau dieses eine Zeichen, semantisch sicher (kein anderes trailing whitespace im Original, Body endet auf `body + "\n\`\`\`"`). Duplikation Scanner vs. Map: Side-by-side-Vergleich beider `AppendHotspotSection`-Bodies zeigt **byte-identische** Builder-Logik, einziger Unterschied ist der Input-Typ (`HotspotFileInfo` vs. `StructureFileInfo`); Sortierung in beiden via `OrderByDescending(x => x.Lines).ThenBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)` korrekt im Lambda; `mb.Line("Keine.")`-Fall für leere File-Listen in beiden Methoden korrekt; Trailing-`sb.AppendLine()` (Scanner Z.152, Map Z.112) repliziert das Original-`sb.AppendLine()` (ehem. Z.43 in `HotspotSectionFormatter.cs`) für die Leerzeile vor dem nächsten Block — byte-stabil. Coder-Abweichungen: #1 Import bleibt nötig (statisch verifiziert: `MarkdownBuilder`/`ColumnAlign` werden jetzt in der neuen Methode referenziert); #2 Disable-Marker sauber, `DuplicateCode`-Regel erkennt den Jaccard-1.00-Cluster, Konvention „Marker in einer der beteiligten Dateien" greift (Scanner = Mcp.Tools-Layer, konsistent); #3 Doc-Kreuzverweis-Umschreibung ist die einzig saubere Lösung gegen stale `cref` auf gelöschte Datei, enthält keine Step-/EPIC-Referenz.

### Konzept-Treue (Ebene 4)

Prio 4 (Hotspot-Löschung) exakt wie `konzept.md` §3 Prio 4 spezifiziert: `Output/HotspotSectionFormatter.cs` ersatzlos entfernt, beide Aufrufer reinkarnieren die Logik lokal mit `MarkdownBuilder` (Schicht-Trennung-Duplikation vom Konzept explizit vorgesehen, daher **kein** Tech-Debt-Fund); Prio 6 (`ListRulesCommand`) exakt wie Konzept §3 Prio 6: `MarkdownTableBuilder` mit 5 Spalten `RuleId`/`Bezeichnung`/`Intent`/`Severity`/`Auto-Fix`, kein Alignment, `autoFix` als fertiger String `"ja (--fix)"`/`"-"`; Prio 10 (`GetSymbolBodyTool`) exakt wie Konzept §3 Prio 10: `Heading(3)` + `BlankLine` + optional `Line("id: ...")` + `BlankLine` + `CodeBlock("csharp", body)`, `.TrimEnd()` ist die saubere Lösung für den byte-stabilen MCP-Token-Vertrag (6/6 Fast-Tests + 22/22 `McpLiveRepositoryTests` grün); nicht umgebaute Sonderfälle respektiert (`SkeletonMarkdownRenderer` Z.71–82, `ViolationMarkdownFormatter.AppendViolationItem` Z.249–270, beide aus EPIC-01 als „nicht umbauen" markiert und in step-003 nicht angefasst); EPIC-02-Prio 5/7/8/9 (Generators + MetricsLookupFormatter) sind **korrekt** NICHT in diesem Step umgesetzt — gehören laut `roadmap.md` in step-004/step-005 (Welle 2 + 3), kein Scope-Drift.

### Build-/Test-Status

```
dotnet build                                                                                                            → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress                                                         → grün (1428 Tests, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~GetHotspotsToolTests|FullyQualifiedName~ListRulesCommandTests|FullyQualifiedName~GetSymbolBodyToolTests → grün (25/25)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress                                                   → grün (321 Tests, 0 Fehler, 2 m 8 s)
dotnet run -- --config rules.json --path .                                                                              → grün (Dogfood sauber, OK)
```
