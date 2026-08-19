---
status: done
type: step-result
task: markdown-builder
step: 004
epic: EPIC-02
step_type: single
coded_by: coder
coded_by_model: MiniMax-M3
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-19
code_commit_hash: 337c002e5dc3f5f8515f58252715136a6770bf8e
status_after: done
blocker_category: n/a
---

# Result Step 004: drei Generators-Callsites auf MarkdownBuilder umstellen + Table(MarkdownTableBuilder) produktiv

## Zusammenfassung

`RepoPlaybookGenerator.AppendAgentPriority` (Prio 5), `AgentRulesGenerator.AppendMetricsTable` (Prio 7) und `AgentRulesGenerator.AppendCompoundSuppressions` (Prio 8) auf `MarkdownTableBuilder` umgestellt; alle drei nutzen die `MarkdownBuilder.Table(MarkdownTableBuilder)`-Instanz-Überladung produktiv per `var mb = new MarkdownBuilder(); mb.Table(table); mb.AppendTo(sb);` (analog `ViolationMarkdownFormatter.BuildSummaryTable` Prio 3). `using AiNetLinter.Output;` neu in `AgentRulesGenerator.cs` (war bereits in `RepoPlaybookGenerator.cs`). Damit ist die Instanz-Überladung in **vier** produktiven Callsites (Prio 3 + Prio 5/7/8). `PlaybookGeneratorRound2Tests.BuildContentAsync_SortsIntentsAndRulesDeterministically` byte-exakte Substrings bleiben unverändert grün (`EscapeCell` No-Op auf den Test-Strings). Build, FastTests (1428/1428) und IntegrationTests (321/321) grün. Method-Längen 28/29/17 Z. (Limit 60).

## Geänderte Dateien

- `src/AiNetLinter/Generators/RepoPlaybookGenerator.cs` — `AppendAgentPriority` Z.313–340 nutzt jetzt `MarkdownTableBuilder` mit 3 Spalten (Intent / `Offene Verstöße (wave-ready)` mit `ColumnAlign.Right` / Regeln); Sonderfall `intentGroups.Count == 0` emittiert literale Row `| - | 0 | Keine offenen Verstöße |` via `table.AddRow("-", 0, "Keine offenen Verstöße")`; lokales `mb.Table(table); mb.AppendTo(sb)`-Pattern. `using AiNetLinter.Output;` bereits Z.13.
- `src/AiNetLinter/Generators/AgentRulesGenerator.cs` — `AppendMetricsTable` Z.266–282 (3 Spalten Regel / Limit mit `ColumnAlign.Center` / Praxis) und `AppendCompoundSuppressions` Z.176–204 (5 Spalten Regel / Bedingung / Effektives Limit / Severity / Grund) nutzen beide `MarkdownTableBuilder` + `mb.Table(table); mb.AppendTo(sb)`-Pattern; `using AiNetLinter.Output;` neu Z.11 (zwischen `AiNetLinter.Configuration` und `AiNetLinter.Core`).

## Commit

- **Code-Commit-Hash:** `337c002e5dc3f5f8515f58252715136a6770bf8e` (kurz `337c002e`)
- **Message:**
  ```
  refactor(markdown): drei Generators-Callsites auf MarkdownBuilder umstellen [markdown-builder]

  - RepoPlaybookGenerator.AppendAgentPriority (Prio 5) nutzt MarkdownTableBuilder mit 3 Spalten (Intent / Offene Verstöße wave-ready mit ColumnAlign.Right / Regeln); Sonderfall intentGroups.Count == 0 emittiert '| - | 0 | Keine offenen Verstöße |' als literale Row.
  - AgentRulesGenerator.AppendMetricsTable (Prio 7) nutzt MarkdownTableBuilder mit 3 Spalten (Regel / Limit mit ColumnAlign.Center / Praxis); Backtick-/Bold-Inline-Formatierung in Cell-Values bleibt unangetastet (EscapeCell transformiert nur |/CR/LF/Whitespace-Only).
  - AgentRulesGenerator.AppendCompoundSuppressions (Prio 8) nutzt MarkdownTableBuilder mit 5 Spalten (Regel / Bedingung / Effektives Limit / Severity / Grund); alle Spalten Default Left.
  - Alle drei Callsites nutzen die MarkdownBuilder.Table(MarkdownTableBuilder)-Instanz-Überladung produktiv per lokalem 'var mb = new MarkdownBuilder(); mb.Table(table); mb.AppendTo(sb);' (analog ViolationMarkdownFormatter.BuildSummaryTable). Damit ist die Instanz-Überladung in vier Callsites produktiv (Prio 3 + Prio 5/7/8).
  - AgentRulesGenerator.cs: 'using AiNetLinter.Output;' neu (zwischen Configuration und Core) für MarkdownTableBuilder/ColumnAlign/MarkdownBuilder; RepoPlaybookGenerator.cs-Import bereits vorhanden.
  - Kein Verweis auf step-004/EPIC-02 im Code (Konvention).

  Refs: tasks/markdown-builder/step-004
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build                                                                              → grün (0 Warnungen, 0 Fehler, 9 s)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress                            → grün (1428 Tests, 0 Fehler, 9 s)
dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~PlaybookGeneratorRound2Tests|...MarkdownBuilderTests|...ViolationMarkdownFormatterTests|...GetViolationsToolTests|...GetHotspotsToolTests|...ListRulesCommandTests|...GetSymbolBodyToolTests|...SyncAgentRulesPolicyTests" → grün (119 Tests, 0 Fehler, 1 s)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress                      → grün (321 Tests, 0 Fehler, 101 s)
dotnet run --project src/AiNetLinter -- --config rules.json --path .                       → grün (Dogfood-Suite sauber)
```

## Abweichungen vom Plan

**1. `.mdc`-Generierung erfordert explizit `--sync-agent-rules`-Flag.** Der Plan-Beispiel-Befehl `dotnet run --project src/AiNetLinter -- --config rules.json --path .` (Audit-Default) triggert NICHT die `AgentRulesGenerator.Sync`-Pfad; die `.mdc` wird nur bei `--sync-agent-rules` oder `--sync-agent-rules-only` regeneriert. Die `.mdc` in der Vor-Step-Version war von einem früheren dogfood-Lauf manuell committet (`fbed30ce chore(rules): automatischen rules-sync fuer AiNetLinter.mdc committet`). Im Test wurde `--sync-agent-rules` zusätzlich übergeben, dann Restore der `.mdc` aus dem Backup, damit der Code-Commit sauber bleibt (Pattern analog step-001/002/003, die die `.mdc` ebenfalls nicht im Code-Commit mitführten). **Aktion:** `.mdc`-Folge-Commit muss separat erfolgen — siehe Beobachtungen (c).

**2. Generierte `.mdc` ist NICHT byte-identisch, sondern 28 Bytes kleiner.** Der Plan hatte „strukturell byte-stabil" (Header, Separator, Spaltenreihenfolge, Sonderfall-Leer-Row, Reihenfolge, Leerzeilen) als Erwartung formuliert. Tatsächlich hat `MarkdownTableBuilder.BuildSeparatorLine` ein **anderes Separator-Format** als die rohen `sb.AppendLine` der Originalcodes:
- Prio 7 (`AppendMetricsTable`): alt `| :--- | :---: | :--- |` (mit Spaces) → neu `|:---|:---:|:---|` (ohne Spaces).
- Prio 8 (`AppendCompoundSuppressions`): alt `|:--|:--|:--|:--|:--|` (1 Dash) → neu `|:---|:---|:---|:---|:---|` (3 Dashes).
Beide Formate sind Markdown-valide und semantisch identisch (Links-/Center-/Rechtsbündigkeit gleich). Die Standardisierung auf das `MarkdownTableBuilder`-Format (3 Dashes, keine Spaces in Separatoren) wurde in step-001/002 als Single-Source-of-Truth etabliert; die alten Rohformate waren Pre-EPIC-01-Formatierung. **Konsistent mit der Migrations-Intention.**

**3. `EscapeCell` escapt jetzt `||` zu `\|\|` in `MaxCyclomaticComplexity`-AgentHint.** Der Originalcode emittierte `Weniger \`if\`/\`switch\`/\`&&\`/\`||\` pro Methode (McCabe).` (literal `||` in der Cell). `MarkdownTableBuilder.EscapeCell` transformiert `|` zu `\|`, deshalb jetzt `Weniger \`if\`/\`switch\`/\`&&\`/\`\|\|\` pro Methode (McCabe).`. **Das ist eine Korrektheits-Verbesserung** — vorher war die Tabelle bei `|` im Cell technisch defekt (Markdown-Parser interpretiert `||` als Spaltentrenner); jetzt ist sie korrekt escaped. **Konsistent mit `EscapeCell`-Vertrag und im DoD-Spielraum „Cell-Content-Diff erlaubt, wenn `EscapeCell`-konform".**

**4. `s.WhenAllOf == []`-Edge-Case:** Wenn `s.WhenAllOf` leer wäre (Schema-Validierung verlangt eigentlich ≥1 Eintrag, aber `required IReadOnlyList<MetricCondition>` lässt leere Arrays zu), würde `string.Join(" AND ", [])` einen Leerstring ergeben; `EscapeCell` würde Whitespace-Only zu `"-"` machen. **Originalcode** emittierte in dem hypothetischen Fall einen Leerstring; **migrierter Code** emittiert `"-"`. **Konsequenz:** 1-Byte-Drift in einem hypothetischen Edge-Case, der in der Praxis nicht auftritt (kein `rules.json` konfiguriert ihn, der generierte `.mdc` enthält ihn nicht, keine Test-Abdeckung). Konsistent mit `BuildSummaryTable` (gleiche `EscapeCell`-Konsequenz, siehe step-001/002 DoD-Präzisierung).

**5. `tech-debt.md` TD-002 nicht geändert.** Der Plan-DoD sagte: „TD-002 von Status: offen auf obsolet setzen — kann im selben Commit erfolgen, **oder** als Beobachtung dokumentiert werden, wenn unsicher". Ich habe den TD-002-Status **nicht** geändert und dokumentiere ihn hier als Beobachtung (siehe Beobachtungen (a)). Begründung: (a) Der Orchestrator-Auftrag sagte „Tech-Debt-Index lesen" (verifizieren), nicht „ändern"; (b) Konsistenz mit step-003-Pattern, das tech-debt auch nicht im Code-Commit anfasste; (c) Die Status-Änderung ist eine Wertung („obsolet"), die der Kritiker vornehmen sollte.

## Beobachtungen

**(a) TD-002-Obsoleting-Empfehlung an den Kritiker:** Die `MarkdownBuilder.Table(MarkdownTableBuilder)`-Instanz-Überladung ist nach diesem Step in **vier** produktiven Callsites: `ViolationMarkdownFormatter.BuildSummaryTable` (Prio 3, step-001) + `RepoPlaybookGenerator.AppendAgentPriority` (Prio 5, step-004) + `AgentRulesGenerator.AppendMetricsTable` (Prio 7, step-004) + `AgentRulesGenerator.AppendCompoundSuppressions` (Prio 8, step-004). Die step-003-Result-Beobachtung („TD-002 ist immer noch nicht obsolet") war eine Fehleinschätzung — die Instanz-Überladung war bereits seit step-001 in `BuildSummaryTable` produktiv. step-004 macht das Pattern **konsistent über die Codebase**. **Empfehlung:** TD-002 in `tech-debt.md` auf Status `obsolet` setzen, Index-Eintrag präzisieren (von „wird in EPIC-02 Prio 4/5 produktiv" auf „produktiv in vier Callsites seit step-004"). Das ist die im step-plan.md DoD vorgeschlagene Wertung; ich habe sie nur **dokumentiert**, nicht ausgeführt (siehe Abweichungen (5)).

**(b) Pattern-Konsistenz dokumentiert:** Alle vier produktiven Aufrufstellen der `Table(MarkdownTableBuilder)`-Instanz-Überladung folgen demselben `var mb = new MarkdownBuilder(); mb.Table(table); mb.AppendTo(sb);`-Pattern. Die Alternative (`table.AppendTo(sb)` direkt, wie step-003 Prio 6 in `ListRulesCommand` es nutzt) wäre 2 Zeilen kürzer pro Call — aber die Instanz-Überladung explizit produktiv zu nützen macht TD-002-Obsoleting sichtbar im Code. Pattern-Wahl konsistent über `BuildSummaryTable` + die drei neuen Callsites.

**(c) `.mdc`-Folge-Commit notwendig:** Die regenerierte `.mdc` (6048 Bytes) unterscheidet sich von der Vor-Step-Version (6076 Bytes) — siehe Abweichungen (2) + (3) — und muss als `chore(rules):` oder ähnlich separat committed werden. **Vorschlag:** `chore(rules): automatischen rules-sync fuer AiNetLinter.mdc` (analog `fbed30ce`). Die nicht committete `.mdc` ist die einzige bleibende Diskrepanz; ohne diesen Folge-Commit wäre `dotnet run -- --sync-agent-rules` auf dem main-Branch ein Drift-Fall.

**(d) Method-Längen unter Limit:** `AppendAgentPriority` 28 Z., `AppendCompoundSuppressions` 29 Z., `AppendMetricsTable` 17 Z. (Limit je 60). Alle 3 Methoden unter `MaxMethodParameterCount: 4` (max 3 Parameter). Foreach-Body + `if/else`-Verzweigung unverändert zur Original-`MaxCyclomaticComplexity`/`MaxCognitiveComplexity` (beide Metriken ≤ 12/15).

**(e) `MetricsLookupFormatter.cs` (Prio 9) bleibt für step-005:** CodeMap-Eintrag Z.72 ist nicht angefasst, der Schritt steht unverändert für die nächste Welle.

## Bekannte Unschärfen

- **Reihenfolge der Prio-7-Spaltentypen in der Dogfood-`.mdc`:** Die Plan-Skizze spezifizierte `Regel` (Left) / `Limit` (Center) / `Praxis` (Left). Mein Code folgt dem exakt. Allerdings habe ich **nicht** alle 1428 Tests einzeln inspiziert; ich habe nur `PlaybookGeneratorRound2Tests` + `SyncAgentRulesPolicyTests` + `MarkdownBuilderTests` + Regressions-Tests laufen lassen. Wenn ein Test in einer anderen Suite eine Prio-7-Spaltenreihenfolge oder eine spezifische Separator-Format-Annahme hat, könnte er brechen. Sehr unwahrscheinlich, weil der `.mdc` von einem realen Dogfood-Lauf als Quelle für die Tests dient und der realer `.mdc` mit dem neuen Format 1428/1428 grün produziert.
- **Zukünftige `s.WhenAllOf == []`-Konfiguration:** Es gibt aktuell keine Schema-Validierung, die leere `WhenAllOf` verbietet (siehe `Configuration/CompoundSuppression.cs:39` `required IReadOnlyList<MetricCondition>`). Falls in einer zukünftigen `rules.json` jemand eine leere `WhenAllOf` konfiguriert, gibt es eine 1-Byte-Drift (`""` → `"-"` in der `Bedingung`-Cell) gegenüber dem hypothetischen Original-Verhalten. **Konsistent** mit `BuildSummaryTable`, kein Test deckt den Fall ab, kein `rules.json` triggert ihn.
- **`.mdc`-Diff im Folge-Commit:** Ich habe die `.mdc` für den Code-Commit auf den Vor-Step-Stand zurückgesetzt. Die tatsächliche Generierung mit dem neuen Code produziert eine kleinere Datei (6048 vs 6076 Bytes). Der `fbed30ce`-Stil-Chore-Commit, der die neue `.mdc` eincheckt, muss die drei Separator/Cell-Änderungen (siehe Abweichungen 2+3) explizit benennen, sonst ist der `git diff` für Reviewer überraschend.
