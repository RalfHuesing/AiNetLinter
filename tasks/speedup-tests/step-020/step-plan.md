---
status: done (pending audit)
type: step-plan
task: speedup-tests
step: 020
corrects: step-019
title: "Korrektur: doppelten Find-Symbol-No-Match-Vertrag konsolidieren"
epic: EPIC-4
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: gpt-5.6-sol
created_by_model_knowledge_cutoff: nicht ausgewiesen
created_at: 2026-08-13
related_to:
  - step-019/step-review.md
---

# Step 020: Doppelten Find-Symbol-No-Match-Vertrag konsolidieren

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-4` aus `roadmap.md`; die Implementierung ist vorhanden, aber wegen des
  Step-019-Verdicts `issues` noch nicht final approved.
- **Korrigiert:** `step-019`, ausschließlich Finding 1 aus Review-Commit `af61a93`.

## Aktueller Projektzustand

`FindSymbolFileAdapterTests.cs:37-44` und `:97-104` nutzen dieselbe bereits einmal pro Klasse
geladene `SymbolGraphMini`-Solution, rufen beide direkt
`FindSymbolScanner.FindMatchesAndFormat(fixture.Solution, "DoesNotExistXyzBlub123", null, 50)` auf
und pruefen dieselben zwei Aussagen: Plain-No-Match vorhanden, Miss-Hint nicht vorhanden. Die
zweite Methode besitzt lediglich ein `Tool`-Praefix; sie prueft weder `FindSymbolTool.ExecuteAsync`
noch einen anderen Dispatch-, Fehler- oder Formatvertrag. Auch die historische Tool-Testmethode
rief direkt den Scanner auf.

Der reale Step-019-Schnitt umfasst daher 20 historische Methoden, aber nur 19 einzigartige
Vertraege: elf FastTests-Vertraege und acht einzigartige Integration-Dateifallback-Vertraege. Die
Korrektur benoetigt keine Produkt-, Fixture-, Kategorien-, Ledgerstatus- oder Architekturarbeit.

TD-006 bis TD-010 aus `tech-debt.md` bleiben unveraendert offen und außerhalb dieses Fixes:
Kategorie-Trait-Auslesung, Skeleton-Testkonfiguration, Fast-/Legacy-Helfer,
Integration-Fixture-Lifecycle und Workspace-Kopie werden weder refaktoriert noch umklassifiziert.

## Intention

Die irrefuehrend benannte redundante Methode wird entfernt und der verbleibende Test benennt den
tatsaechlich ausgefuehrten Scanner-No-Match-Vertrag. Result, Ledger, CodeMap, Roadmap und Task-State
weisen anschließend transparent aus, dass 20 historische Methoden semantisch auf 19 einzigartige
Zielvertraege konsolidiert wurden, ohne fachliche Abdeckung zu verlieren.

## Konkrete Änderungen

### `src/AiNetLinter.IntegrationTests/Mcp/Tools/FindSymbolFileAdapterTests.cs`

- `FindMatchesAndFormat_ToolNoCsMatchAndNoNonCsHit_ReturnsPlainNoMatchText` vollständig entfernen.
- `FindMatchesAndFormat_NoCsMatchAndNoNonCsHit_ReturnsPlainNoMatchText` mit seinen beiden
  Assertions unverändert als einzige Quelle dieses Scannervertrags behalten. Der Name ist bereits
  ehrlich; nur falls bei der mechanischen Umsetzung eine Umbenennung nötig wird, muss sie weiterhin
  `FindSymbolScanner.FindMatchesAndFormat` und Plain-No-Match ausdruecken, nie Tool-Dispatch.
- Keine weitere Testmethode, Fixture oder Assertion anfassen. Insbesondere die ähnlichen, aber
  eigenständigen Kindfilter-, Pattern- und positiven Miss-Hint-Vertraege bleiben bestehen.

### Audit- und Migrationsartefakte

- `step-019/step-result.md`: Zusammenfassung, Dateiliste, Abweichungen und Beobachtungen auf
  **20 historische Methoden → 19 einzigartige Vertraege** korrigieren; Zielaufteilung **elf Fast +
  acht Integration**. Die bisherige Begründung, das exakte Paar bilde zwei historische
  Klassenvertraege und müsse getrennt bleiben, ausdrücklich als durch Review widerlegt ersetzen.
- `test-migration-ledger.md`: Beide bestehenden Find-Symbol-Zeilen und ihre maschinell gueltigen
  Zielpfade/Status unverändert lassen. Direkt bei der Find-Symbol-Kohorte eine kurze
  Ledger-Coverage-Notiz ergänzen: Die zwei historischen Plain-No-Match-Methoden werden durch den
  einen verbleibenden `FindSymbolFileAdapterTests`-Scannervertrag semantisch konsolidiert; insgesamt
  20 historische Methoden, 19 einzigartige Vertraege. Keine Zusatzsyntax in der Pfadspalte, da der
  Guard dort einen reinen existierenden Pfad erwartet.
- `codemap.md`: den Planungszeiger auf den realen step-020-Stand aktualisieren und den
  Integration-Anteil von neun Methoden auf acht einzigartige Vertraege berichtigen.
- `roadmap.md`: Step 019 plus Korrektur step-020 als EPIC-4-Grenzabschluss dokumentieren, aber das
  Epic bis zum Re-Audit nicht als final approved ausgeben.
- `task-state.md`: step-019 als `issues→pending re-audit (via step-020)` und step-020 nach Umsetzung
  als `done (pending audit)` mit korrekten Code-/Doku-Hashes ausweisen. Task bleibt `executing`.
- `step-020/step-result.md`: ausschließlich Entfernung, semantische Konsolidierung,
  Dokumentationskorrekturen und tatsächlich ausgeführte enge Gates berichten.

## Tests

- [ ] Betroffene Zielklasse kompilieren und ausführen:
  `dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~FindSymbolFileAdapterTests"`
  — erwartet acht statt neun grüne Tests.
- [ ] Danach ohne erneuten Build ausschließlich Kategorien-, Ledger- und Legacy-Gate prüfen:
  `dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~TestCategoryProfileGuardTests|FullyQualifiedName~TestMigrationLedgerConsistencyTests|FullyQualifiedName~LegacyProjectBuildGateTests"`
- [ ] Kein Component-Grenzgate: Es ändert sich ausschließlich eine Integration-Testduplikation;
  FastTests, Produktcode und Component-Verhalten bleiben unberührt.
- [ ] Kein vollständiger Fast-/Integration-/`Category!=Stress`- oder Stresslauf.

## Definition of Done

- [ ] Exakt eine redundante Methode ist entfernt; der verbleibende Name beschreibt ehrlich den
  direkt aufgerufenen Scanner-No-Match-Vertrag.
- [ ] Acht einzigartige Integration-Dateifallback-Vertraege bleiben grün; kein Fehler-, Negativ-,
  Kindfilter-, Format- oder Miss-Hint-Vertrag ging verloren.
- [ ] Result, Ledger, CodeMap, Roadmap und Task-State verwenden konsistent die Aussage
  **20 historische Methoden → 19 einzigartige Vertraege (elf Fast + acht Integration)**.
- [ ] TD-006 bis TD-010 bleiben unverändert und werden nicht als behoben dargestellt.
- [ ] Die beiden engen Gates und `git --no-pager diff --check` sind grün; kein Voll-/Stresslauf.
- [ ] Commit auf aktuellem Branch als Conventional Commit auf Deutsch mit Suffix
  `[speedup-tests]`; kein Push.
- [ ] `step-020/step-result.md` geschrieben; Planstatus `done (pending audit)`.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitaetsdrift-Praevention` — keine redundanten
  oder irrefuehrend benannten Tests, keine Assertions abschwaechen.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — engster betroffener Testfilter;
  keine unnoetige Ausweitung oder Serialisierung.

## Bekannte Ausnahmen

- TD-006, TD-007, TD-008, TD-009 und TD-010 sind bekannte, nicht auto-fixable Beobachtungen aus
  dem Step-019-Review. Sie sind kein Scope und kein Abnahmehindernis fuer diesen Fix.

## Notes

- Keine zweite Konsolidierung anhand ähnlicher Namen vornehmen. Das Review beanstandet exakt das
  byte-/verhaltensgleiche Paar an den damaligen Zeilen 37-44 und 97-104.
- Keine historischen Testmethoden künstlich umetikettieren: Die Konsolidierung betrifft zwei
  Methoden, die beide direkt denselben Scanner aufriefen.
