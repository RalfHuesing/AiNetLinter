---
status: done
type: step-result
task: 03_get-impact-zum-diff-kontext-erweitern
step: 007
epic: EPIC-5
step_type: single
coded_by: coder
coded_by_model: stealth/ox-alpha
coded_by_model_knowledge_cutoff: unbekannt
coded_at: 2026-08-23T10:10:00+02:00
code_commit_hash: 8bc3e919
status_after: done
blocker_category: n/a
---

# Result Step 007: Solutionweite Violations & diffbezogene Filterung (interne Stufe)

## Zusammenfassung

Der neue interne `DiffViolationScanner` (`Mcp/Tools/Analysis`) führt den
Linter pro Aufruf GENAU EINMAL solutionweit aus und filtert das Ergebnis rein
diffbezogen: eine Violation bleibt, wenn ihre Zeile in einem geänderten Hunk
ODER in der Deklarationsspanne eines GEZEIGTEN Symbols liegt — andere
Violations derselben Datei bleiben außen vor; Doppelbedingung liefert genau
einen Eintrag; Ausgabe sortiert FilePath→Zeile→Regel analog zur Scope-Sortierung.
Die drei Pfadsemantiken (Hunks repo-root-relativ, Symbol-Einträge
solution-relativ, Violations absolut) werden zentral im Filter über
`GetFullPath`/`Combine` auf vergleichbare Absolutpfade normalisiert und ordinal
case-insensitive verglichen. Die LinterEngine-Beschaffung steckt jetzt im
gemeinsamen Helper `GetViolationsScanner.RunSolutionLintAsync` (geteilt von
`get_violations` und der neuen Stufe); `DiffImpactCounters.LintRuns` hat seine
einzige Produktions-Inkrement-Stelle unmittelbar vor dem Lauf (Skip-empty:
keine Hunks UND keine gezeigten Symbole → kein Lint, kein Inkrement). Der
zusammengesetzte Integrationstest weist das volle Konzept-Tripel nach:
GitRuns==1 && TestSolutionScans==1 && LintRuns==1.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Tools/Analysis/DiffViolationScanner.cs` (neu) —
  Stufe `CollectAsync(DiffViolationScanRequest)` mit Skip-empty-Guard,
  LintRuns-Inkrement vor dem einen Lauf, Malfunction-Muster bei non-OCE-
  Exception; pure `FilterDiffRelevantViolations` inkl. zentraler
  Pfadnormalisierung; Records `DiffViolationScanRequest`,
  `DiffViolationScanResult`, `DiffPathContext`.
- `src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs` —
  Lint-Beschaffung ((Config)-Downcast + `new LinterEngine(...)` +
  `RunAsync(solution, noCache:true, 0, ct)`) in internal
  `RunSolutionLintAsync` extrahiert; `BuildViolationsTextAsync` delegiert —
  Verhalten von `get_violations` unverändert.
- `src/AiNetLinter/Core/DiffImpactAnalysisModels.cs` — nur XML-Doc am
  `DiffImpactCounters`/`LintRuns` aktualisiert (Inkrement-Stellen benannt,
  veralteter „folgt noch"-Text entfernt).
- `src/AiNetLinter.FastTests/Mcp/Tools/Analysis/DiffViolationFilterTests.cs`
  (neu) — 7 Unit-Tests: Hunk-Ein-/Ausschluss samt Randwerten (erster/letzter
  Strich inklusive), Spannen-Treffer ohne Hunk, `LineCount=0` matcht nie,
  Pfadsemantik/Trenner/Case-Toleranz, Dedup + deterministische Sortierung;
  Stage: echte `LinterEngine` auf `ChangeContextScenarioFactory`-Solution →
  Treffer im Hunk dabei, `AuditLogger.cs` komplett außen vor, `LintRuns==1`;
  leerer Input → kein Lint, `LintRuns==0`, keine Malfunction.
- `src/AiNetLinter.IntegrationTests/Core/DiffImpactAnalyzerOnceOnlyTests.cs`
  — ruft nach Git- und Batch-Stufe die Violations-Stufe mit DEMSELBEN
  Counters-Objekt auf (echte Workspace-Hunks/-Symbole, Ad-hoc-Config) und
  assertet das Tripel; die alte `LintRuns==0`-Pin-Assertion samt
  Begründungskommentar entfernt.

## Commit

- **Code-Commit-Hash:** `8bc3e919`
- **Message:**
  ```
  feat: Diff-Violations-Stufe [03_get-impact-zum-diff-kontext-erweitern]

  Der neue DiffViolationScanner fuehrt den Linter genau einmal solutionweit
  aus und filtert das Ergebnis rein diffbezogen ... (Body gekürzt)

  Refs: tasks/mcp-server-weiterentwicklung/03_get-impact-zum-diff-kontext-erweitern/step-007
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier
  drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build                                                          → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress       → grün (1612 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (348 Tests, 0 Fehler)
```

Dogfood-Lint (`dotnet run --project src/AiNetLinter -- --config rules.json
--path ./AiNetLinter.slnx`) → OK. Zusatzchecks via MCP nach
`reload_config` (Server-Index kannte die neue Datei vorher nicht):
`metrics_lookup` auf `CollectAsync`/`FilterDiffRelevantViolations`/
`RunSolutionLintAsync`/`BuildViolationsTextAsync`/Typ — alle Schwellwerte OK
(Footprint des neuen Typs 283 ≤ 2500); `find_duplicates` (clone/near,
Produktion) → 0 Cluster bei 1326 Methoden; `find_magic_values` im Scope
Mcp/Tools/Analysis → nur Bestandsfunde in SearchPattern*/Format-Strings,
keine in meinen Dateien; `find_dead_code` (Scope Analysis) → 0.

Schnelliteration während der Entwicklung: nur
`FullyQualifiedName~DiffViolation` (7 Tests) bzw. der OnceOnly-Test einzeln.

## Abweichungen vom Plan

1. **Filter-Signatur um ein Pfad-Bündel erweitert (Regelkonformität):** die
   Plan-Skizze rief `FilterDiffRelevantViolations(...)` mit 5 Parametern
   (violations, changedFiles, shownSymbols, RepositoryRoot, SolutionDir).
   Ab 5 Parametern schreibt `.agents/rules/AiNetLinter.mdc` ein Input-Record
   vor — die beiden Basisverzeichnisse stecken deshalb im kleinen
   `readonly record struct DiffPathContext(RepositoryRoot, SolutionDir)`
   (gleiche Datei), die Filtermethode hat exakt 4 Parameter
   (`metrics_lookup` bestätigt). Semantik wie geplant.
2. **Stage-Test-Fixture nutzt die vorhandene Szenario-Factory statt einer
   neuen Ad-hoc-Solution:** der Plan nannte als Muster „Ad-hoc-Config auf
   In-Memory-Solution" mit Beispiel `MetricsConfig { MaxLineCount = … }`;
   konkret umgesetzt mit `ChangeContextScenarioFactory.CreateScenario()` +
   `GlobalConfig { EnforceSealedClasses = true }` — deterministische
   Violation auf bekannter Zeile (Klassendeklaration, Zeile 3 von
   OrderService.cs, gepinnt) UND zugleich der Konzept-Kernfall auf Stufen-
   Ebene (AuditLogger.cs-Analogie: gleichartige Violation einer Datei ohne
   Hunk bleibt komplett außen vor). Die Factory ist laut CodeMap ausdrücklich
   Grundlage für „die folgenden Steps".
3. **Sortier-Erwartung im Dedup-Test an ordinale Realität angepasst:** erste
   Testfassung hatte repo/solution als disjunkte Bäume modelliert — ein
   Doppelmatch (Hunk UND Spanne) auf EINER Datei ist dabei konstruktiv
   unmöglich; das Fixture modelliert jetzt realistisch (Solution-Verzeichnis
   unterhalb der Repo-Wurzel). Kein Produktionscode betroffen.

## Beobachtungen

- **`MetricsTreeRoslynScanner.ComputeViolationDensityMetricsAsync` baut die
  LinterEngine identisch selbst** ((Config)-Downcast + Konstruktor-Aufruf +
  `RunAsync(noCache:true)`, eigener Hinweiskommentar darauf). Der Plan-Scope
  für den Helper war explizit GetViolationsScanner + neue Stufe — MetricsTree
  habe ich NICHT angerührt. Falls der Kritiker DRY-Nachschub will: dort auf
  `RunSolutionLintAsync` umstellen wäre ein Einzeiler-Zug.
- **Residenter MCP-Server braucht einen Refresh nach neuen Dateien:** der
  laufende `ainetlinter` (Uptime >1 h) kannte die neu angelegte Datei im
  Symbolgraph nicht (find_symbol leer, Compile-Fehler-Hinweis aus der
  Testprojekt-Sicht); `reload_config` stößt glücklicherweise auch
  `ReloadSolutionAsync` an und behebt das ohne Server-Neustart. Für künftige
  Coder-Steps merken: erst `reload_config`, dann Zusatzchecks.
- **EnforceSealedClasses meldet auf der Klassendeklarations-Zeile** (Zeile 3
  bei namespace+Leerzeile+class) — für künftige Stage-Tests mit dieser Regel
  als deterministische Zeilenanker nutzbar (jetzt im Test gepinnt).
- TD-001 (FastTests-Server-Ergonomie) blieb unberührt — meine FastTests
  kommen ohne McpCodeGraphServer aus (pure Funktion + direkte Engine).

## Bekannte Unschärfen

- **Pfadnormalisierung nutzt `Path.GetFullPath`** (Windows): normalisiert
  Trenner und `..`-Segmente, aber NICHT Groß-/Klein­schreibung — die Case-
  Toleranz steckt bewusst in den Dictionary-/Ordering-Compara­rern
  (OrdinalIgnoreCase, wie Bestandscode). Nicht-Dateisystem-absolute Pfade
  (UNC ok, Linux-Layout unter Windows nicht) sind nicht extra abgesichert —
  Zielplattform ist Windows/pwsh laut Richtlinien.
- **Symbol-Einträge mit leerem Solution-Verzeichnis** (reine In-Memory-
  Solutions ohne `Solution.FilePath`): relative Symbol-Pfade können dann
  nicht aufgelöst werden und matchen nie — für EPIC-6 irrelevant (dort liegt
  immer eine geladene Datei-Solution vor), per XML-Doc nicht extra
  ausgewiesen.
- **Byte-Identität von `get_violations`** ist nicht durch Alt/Neu-Diff
  belegt, sondern strukturell begründet (reiner Methoden-Extract, gleiche
  Argumente) plus alle unangetastet grünen GetViolationsTool-/
  SearchPattern-/Dogfood-Tests.
- Der Stage-Test pinnt die Violations-Zeile 3 an den Fixture-Quelltext der
  ScenarioFactory — verschiebt jemand dort die Klassendeklaration, schlägt
  der Test lautstark (bewusst kein dynamisches Berechnen, Muster wie
  step-004-Bodyzeilen-Konstanten).
