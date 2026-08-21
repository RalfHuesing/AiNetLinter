---
status: done
type: step-review
task: 04_repositoryweite-hybridsuche-und-kontextbudget
step: 003
epic: EPIC-04
step_type: single
reviewed_by: kritiker
reviewed_by_model: GPT-5 (Codex)
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-21
verdict: issues
tech_debt_ids: [TD-003-001]
---

# Review Step 003: Opt-in C#-Roslyn-Enrichment und MCP-Vertrag synchronisieren

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — mindestens ein MAJOR-Finding; Korrektur-Step erforderlich
- [ ] **blocked** — kein Infrastruktur- oder Entscheidungsblocker

## Geprüft

- [ ] Plan-Erfüllung: weitgehend umgesetzt, aber die Cancellation-Implementierung verletzt eine harte Plan-Notiz.
- [x] Rules-Konformität: Step-Code, Größenbudgets, Exception-/Catch-Politik und Testregeln geprüft.
- [ ] Logische Korrektheit: Default-, MCP- und Roslyn-Normalpfade sind korrekt; der Cancellation-Fallback ist fehlerhaft.
- [ ] Konzept-Treue: die harte Grenze „keine zweite Trefferenumeration“ wird im Cancellation-Pfad überschritten.
- [x] Build: selbst nachgeprüft, grün.
- [x] Tests: selbst nachgeprüft, beide vollständigen Non-Stress-Gates grün.

## Befund

### Plan-Erfüllung

Die additive `enrichCSharp=false`-Kompatibilität ist durch Records, Legacy-Formatter, `GetFilesWithHits`, StructuredContent-Grundform, Limits, Completeness und die direkten/SDK-/Raw-Wire-Tests nachvollziehbar umgesetzt. Der Enricher arbeitet auf der bereits sichtbaren Match-Liste, ordnet solution-relative kanonische Pfade gegen `Solution.Projects`/`Document` zu, prüft den Snapshot-Zeilentext, verarbeitet MatchRanges an Roslyn-Positionen und verwendet die geforderten Kategorien, Resolutionszustände sowie `TryGetDocCommentId()`; Kommentare/Strings werden nicht als Symbolreferenzen ausgegeben. Die Registrierung bindet `enrichCSharp=false` korrekt, Overview/Instructions/README/Docs sind grundsätzlich synchronisiert, und das Instructions-UTF-8-Budget ist eingehalten. Nicht erfüllt ist die Plan-/Konzeptgrenze zur einmaligen Trefferenumeration, weil der Tool-Fallback bei einer während der Anreicherung ausgelösten Cancellation den lexikalischen Scanner erneut startet.

### Rules-Konformität

Die neuen Enricher-Records sind immutable, der neue Produktionscode bleibt unter `MaxLineCount`, `MaxMethodLineCount`, `AIContextFootprint` und den Komplexitätsgrenzen; `get_violations` meldet für den Enricher- und Tool-Scope 0 Verstöße. Der Snapshot-Load-Catch ist nicht still, sondern schreibt eine Warnung nach `Trace` und liefert den vorgesehenen `unavailable`-Fallback; Cancellation wird nicht verschluckt. Die xUnit-v3-/TestKit-Regeln und die Testparallelität wurden durch die Änderung nicht verletzt. Der vollständige projektinterne Lint-Lauf meldete nur den bereits vorhandenen, gitignorierten `temp`-Ordner als `MaxDirectoryChildren` (46 Einträge), nicht Step-Code.

### Logische Korrektheit

Die Normalpfade liefern stabile Deklarations-/Referenz-IDs, bewahren Legacy-Text, Reihenfolge, Scope-/Datei-/Kontext-/Antwortbudget-Auswahl und StructuredContent-Objektform; Roslyn-Ausfälle werden recoverable modelliert. Die MatchRange-zu-Syntax-Abbildung und der Disk-/Snapshot-Zeilentextvergleich verhindern Scheinsicherheit im üblichen Pfad. Die Cancellation-Behandlung ist jedoch nicht korrekt: Wird nach der lexikalischen Auswahl während `EnrichAsync` abgebrochen, fängt `SearchPatternTool` die Operation ab und ruft `SearchPatternScanner.Scan(...)` in Zeile 60 nochmals auf. Dadurch wird die Dateisystem-/Trefferenumeration doppelt ausgeführt, obwohl die gemeinsame sichtbare Match-Liste die einzige Quelle sein soll; außerdem werden bereits ermittelte Ergebnisse verworfen und ein unnötiger zweiter Scan unter dem bereits abgebrochenen Token gestartet. Die vorhandene Testsuite prüft Cancellation nur direkt am lexikalischen Scanner, nicht diesen Enrichment-Abbruchpfad.

### Konzept-Treue (Ebene 4)

Neue Suchtools, RAG/Ranking, Cursor-/Session-State und eine neue Solution-Ladung wurden nicht eingeführt; `rg` bleibt erlaubt und die öffentliche Dokumentation behauptet keine Tokenersparnis. Die optionale Snapshot-Anreicherung bleibt im vorgesehenen Scope. Die explizite harte Grenze aus Konzept/Plan/Notes — keine zweite Trefferenumeration und keine Veränderung der lexikalischen Sichtbarkeitsauswahl — ist im Cancellation-Fallback dennoch verletzt und verhindert ein `approved`-Verdict.

## Build-/Test-Status

```text
dotnet build → grün (0 Fehler, 0 Warnungen)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (1560 Tests, 0 Fehler, 0 übersprungen)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (338 Tests, 0 Fehler, 0 übersprungen)
dotnet run --project src/AiNetLinter -- --config rules.json --path . → Exit 0; 1 vorhandene MaxDirectoryChildren-Violation nur unter gitignoriertem temp
git diff --check → sauber
```

Zusätzlich waren die im Step-Result genannten fokussierten Scanner-/MCP-/Contract-/Raw-Wire-/Overview-/Options-/Dokumentationsprüfungen im Code-Commit bereits grün; die beiden vollständigen Gates wurden für dieses Review erneut ausgeführt.

## Findings

1. `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternTool.cs:58-61` — **[MAJOR] [Logik/Plan/Konzept]** Der `OperationCanceledException`-Fallback startet mit `SearchPatternScanner.Scan(scannerParameters with { EnrichCSharp = false })` eine zweite Dateisystem- und Trefferenumeration, obwohl Step-Plan und Konzept ausdrücklich die bereits sichtbare Match-Liste als einzige Quelle und keine zweite Trefferenumeration verlangen. Das tritt genau dann auf, wenn Cancellation nach dem erfolgreichen lexikalischen Scan während der Roslyn-Anreicherung eintritt; der Pfad verwirft die erste Scanantwort und kann trotz abgebrochenem Token erneut arbeiten. **Fix:** Den bereits erzeugten `lexicalResult`-Payload bis zum Tool zurückreichen und bei abgebrochener Anreicherung nur dessen vorhandene Matches ohne Semantic-Felder sowie unveränderte Cancellation-/Completeness-Metadaten verwenden; alternativ den Abbruchpfad im Enricher recoverable auf der vorhandenen Liste modellieren. Einen Regressionstest ergänzen, der Cancellation nach der lexikalischen Auswahl auslöst und eine zweite Enumeration ausschließt.

## Tech-Debt-Einträge aus diesem Review

- `TD-003-001` — `OverviewResourceRegistration.ToolSummaries` nennt bei `search_pattern` zwar `enrichCSharp=true`, aber nicht die Snapshot-/`unavailable`-/`ambiguous`-Grenze und den vorgesehenen Folgeweg bei Trunkierung; `auto_fixable: ja`; Scope: `src/AiNetLinter/Mcp/OverviewResourceRegistration.cs`/Overview-Discovery-Text. Dies ist eine nicht-blockierende Doku-Vervollständigung, da Toolbeschreibung, ServerInstructions und öffentliche Docs die Details bereits enthalten.

## Reviewpfad

- Konzept: `tasks/mcp-agenten-effizienz/04_repositoryweite-hybridsuche-und-kontextbudget.md`
- Plan/Ergebnis: `tasks/mcp-agenten-effizienz/04_repositoryweite-hybridsuche-und-kontextbudget/step-003/step-plan.md`, `step-003/step-result.md`
- Vorreview: `tasks/mcp-agenten-effizienz/04_repositoryweite-hybridsuche-und-kontextbudget/step-002/step-review.md`
- Regeln: `.agents/rules/AiNetLinterRichtlinien.mdc`, `.agents/rules/AiNetLinter.mdc`
- Commits: Code `8252e232`, Dokumentation/Step-Artefakte `a7fd6794`
