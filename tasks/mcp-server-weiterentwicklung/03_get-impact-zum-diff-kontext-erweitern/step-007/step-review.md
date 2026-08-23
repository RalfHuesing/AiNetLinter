---
status: done
type: step-review
task: 03_get-impact-zum-diff-kontext-erweitern
step: 007
epic: EPIC-5
step_type: single
reviewed_by: kritiker
reviewed_by_model: stealth/ox-alpha
reviewed_by_model_knowledge_cutoff: unbekannt
reviewed_at: 2026-08-23T10:20:00+02:00
verdict: approved
tech_debt_ids: [TD-003]
---

# Review Step 007: Solutionweite Violations & diffbezogene Filterung (interne Stufe)

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Korrektur-Step `step-<MMM>` angelegt (`corrects: step-<NNN>`)
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (oder Baselines beachtet)

## Befund

Alle vier Plan-Datei-Änderungen (Helper-Extract, neue Stufe, XML-Doc,
Tests) sind umgesetzt, alle 8 Test-Checklistenpunkte durch die 7 neuen
FastTests und den erweiterten Integrationstest abgedeckt, Commit und
CodeMap passen.

### Plan-Erfüllung

Alle fünf Dateien des Plans im Commit `8bc3e919` enthalten; die drei
dokumentierten Abweichungen (DiffPathContext-Bündel, ScenarioFactory-
Fixture mit EnforceSealedClasses, Solution-unterhalb-Repo-Fixture) sind
regelbzw. testbarkeitsbegründet und schmaler als der Plan, nicht
anderweitig; `codemap.md` trägt alle step-007-Stellen (Stufe, Helper,
Counter-Doc, beide Testdateien).

### Rules-Konformität

`AiNetLinter.mdc` #Grenzwerte eingehalten (Methoden ≤60 Zeilen, Filter
exakt 4 Parameter via `DiffPathContext`-Record ab der 5-Parameter-Grenze,
`sealed`, `#nullable enable`, `Mcp/Tools/Analysis` weiter unter 30
Dateien); `AiNetLinterRichtlinien.mdc` #Qualitätsdrift-Prävention
eingehalten (Engine-Beschaffung nur noch in `RunSolutionLintAsync`,
Malfunction-Muster statt Exceptions, Zero-Warning, Kommentare ohne
Task-/Step-Referenzen) sowie #Updates-&-Tests (xUnit v3 mit
`Category=Unit`, keine Serialisierungs-Collection, keine dateibasierten
Fixtures nötig).

### Logische Korrektheit

Die Filterregel bildet §Filterregeln exakt ab: Hunk-Treffer (halboffen
`[Start, Start+LineCount)`, damit matcht `LineCount=0` nie — konsistent
mit dem HunkRange-XML-Doc) ODER inklusive Deklarationsspanne GEZEIGTER
Symbole; die Nachbar-Violation derselben Datei bleibt draußen (unit-
 wie stage-level gepinnt, AuditLogger-Analogie); Dedup ist über das
Where strukturell garantiert und zusätzlich per Test gezählt; Sortierung
FilePath→Zeile→Regel ordinal case-insensitive. „Lint genau einmal" ist
belegt: ein Inkrement unmittelbar vor genau einem `RunSolutionLintAsync`,
Skip-empty ohne Lauf/Inkrement, Tripel-Nachweis `GitRuns==1 &&
TestSolutionScans==1 && LintRuns==1` über dasselbe Counters-Objekt im
Integrationstest. Bestandsverhalten von `get_violations` unverändert:
reiner Methoden-Extract mit identischen Argumenten; der einzige
strukturelle Unterschied ((Config)-Downcast jetzt innerhalb try/catch)
ist praktisch unerreichbar, da `ILinterEngineConfig` projektweit
ausschließlich von `Config` implementiert wird und der Tool-Aufrufer
immer einen konkreten Config-Snapshot übergibt.

### Konzept-Treue (Ebene 4)

§Filterregeln sind EXAKT umgesetzt (Hunk ∪ Spanne gezeigter Symbole,
sonst nichts — kein zweites ungescoptes `get_violations`),
§Performance-Regel „Linter genau einmal" erfüllt und instrumentiert
nachweisbar; EPIC-6-Grenze respektiert (`GetImpactTool`/`GetImpactInput`
und Registrierungen unberührt, kein Tool-Kontakt, keine Kappung/Caps in
der Stufe); `ShownSymbols` als explizite Eingabe setzt die Konzept-
Semantik „GEZEIGTE Symbole vor teuren Folgeanalysen" korrekt um.

### Build-/Test-Status

```
dotnet build                                                          → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress       → grün (1612 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (348 Tests, 0 Fehler)
```

## Tech-Debt-Einträge aus diesem Review

- `TD-003` (Volltext vom Orchestrator/Nutzer in `tech-debt.md` nachzutragen — Schreiben
  war in diesem Auftrag explizit untersagt) — [Priorität: niedrig]
  [Auto-Fixable: nein]: `MetricsTreeRoslynScanner.ComputeViolationDensityMetricsAsync`
  (`src/AiNetLinter/Mcp/Tools/MetricsTree/MetricsTreeRoslynScanner.cs`, Z. 70–79) baut die
  LinterEngine identisch selbst ((Config)-Downcast + Konstruktor + `RunAsync(noCache:true)`),
  obwohl seit step-007 `GetViolationsScanner.RunSolutionLintAsync` die gemeinsame
  Beschaffungsstelle für den MCP-Pfad ist; Umstellung wäre Einzeiler, erfordert aber die
  Entscheidung, ob MetricsTree auf den Analysis-Namespace koppeln darf bzw. wohin der Helper
  langfristig gehört — Architektur-Ermessen, daher nicht auto-fixable. Vom Coder gemeldet,
  vom Kritiker gegen Code bestätigt; liegt außerhalb des step-007-Scopes (Plan schloss den
  Helper bewusst auf get_violations + neue Stufe).
