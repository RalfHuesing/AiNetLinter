---
status: done
type: step-review
task: metrics-tree
step: 003
epic: EPIC-02
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-08
verdict: approved
tech_debt_ids: []
---

# Review Step 003: Roslyn-Modi violation_density/complexity + Doku-Updates + Roadmap-Abschluss

## Verdict

- [x] **approved** — alle vier Prüfebenen ok

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (oder Baselines beachtet)

## Befund

Alle 11 geplanten Dateien umgesetzt, `metrics_tree` deckt jetzt alle 4 Konzept-Modi ab; Doku
(`Docs/agent-api.md`, `README.md`, `Docs/ROADMAP.md`, `tasks/features/05-roadmap.md`) stichprobenartig
gegen den tatsächlichen Code verifiziert und korrekt. Die drei im Coder-Bericht gemeldeten
Plan-Abweichungen sind alle mechanisch/zwingend, nicht Architektur-Ermessen (siehe unten). Selbst
reproduzierter Build + gezielter Testlauf grün; der Abschluss-Volllauf (1349 Tests) ist nachvollziehbar
dokumentiert und die Testmenge per `--list-tests` gegen denselben Filter selbst bestätigt (siehe
Build-/Test-Status).

### Plan-Erfüllung

Alle 11 „Konkrete Änderungen" (Dateien 1-11) umgesetzt und stichprobenartig verifiziert. `codemap.md`
wurde vor dem Doku-Commit aktualisiert (Pflicht erfüllt) und ist inhaltlich korrekt (Registrierungs-Umzug,
neue Datei, top-level Records, Verzeichnis-Verschiebung — alles nachvollziehbar mit Begründung
dokumentiert). Alle Tests aus dem Plan vorhanden (Sortierung beider Roslyn-Modi, Edge-Cases leerer Root/
Single-File/`depth=5`/methodenlose Datei, 2 Dogfooding-Tests, Tool-Count-Korrektur).

**Drei ungeplante, aber vom Coder gemeldete Abweichungen — geprüft, alle mechanisch:**

1. **`FileMetric`/`BuilderNode` top-level statt `internal nested`:** `rules.json` bestätigt
   `BanPublicNestedTypesAllowPrivate: true` — die Regel erlaubt nur `private nested` Typen, `internal
   nested` ist ein Error-Verstoß. Der Plan-Wortlaut („von `private` auf `internal` anheben") hätte
   also zwangsläufig gegen diese Regel verstoßen; die Top-Level-Extraktion ist die einzig
   regelkonforme Umsetzung derselben Absicht (Wiederverwendung durch `MetricsTreeRoslynScanner`).
   Kein Ermessen — mechanisch erzwungen.
2. **Verschiebung aller `MetricsTree*.cs` nach `Mcp/Tools/MetricsTree/`:** Selbst nachgeprüft per
   `git ls-tree` am Commit vor diesem Step: `src/AiNetLinter/Mcp/Tools/` hatte bereits **31** Einträge
   (Limit `MaxDirectoryChildren: 30`) — die neue `MetricsTreeRoslynScanner.cs` trieb das auf 32. Nach
   der Verschiebung: 28 Einträge in `Mcp/Tools/` (unter Limit). Die vorbestehende Verletzung war vor
   diesem Step nicht sichtbar, weil laut Test-Strategie dieses Tasks der verpflichtende Volllauf erst
   am Ende steht — plausibel, keine Nachlässigkeit dieses Steps. Verzeichniswahl (`MetricsTree/`) ist
   naheliegend und konsistent mit bestehenden Präzedenzfällen im Projekt (z. B. `Maps/Skeleton/`).
   Kein Ermessen jenseits der (unstrittigen) Namenswahl.
3. **Zwei `AIContextFootprint`-`PathOverride`-Anpassungen in `rules.json`:** Folgeeffekt aus (2), da
   `AnalysisToolRegistrations.cs`/`MetricsTreeTool.cs` durch den `metrics_tree`-Zuzug tatsächlich über
   ihre bisherigen Limits stiegen. Anwendung der bereits 10-fach etablierten Projekt-Konvention
   (`PathOverride` knapp über Ist-Wert) — keine neue Architekturentscheidung. Selbst per
   `get_violations` (voller Scope) nachgeprüft: aktuell 0 `AIContextFootprint`-Warnungen im
   Produktionscode.

Alle drei Abweichungen sind Konsequenzen des vom Plan selbst geforderten Abschluss-Volllaufs, nicht
Scope-Erweiterungen — korrekt im Step-Result dokumentiert, kein Finding.

### Rules-Konformität

`.agents/rules/AiNetLinter.mdc` (`MaxMethodParameterCount`, `AIContextFootprint`, `MaxLineCount`,
`MaxCyclomaticComplexity`/`MaxCognitiveComplexity`, Kurz-Stil) eingehalten — `get_violations`
(scopeFilter `MetricsTree`) bestätigt 0 Verstöße in den 8 relevanten Dateien; voller Solution-Scope
zeigt nur 3 vorbestehende, absichtliche Fixture-Verstöße (`AllowDynamic` in
`tests/Fixtures/DiRegistrationMini`), unverändert seit vor diesem Step. `MaxMethodParameterCount`-
Suppression auf `BuildNode` korrekt begründet (Why-Kommentar, kein Task-ID-Bezug,
`AiNetLinterRichtlinien.mdc` §5 eingehalten). `AiNetLinterRichtlinien.mdc` §1 Prüfpflicht bei
Doku-Änderungen: stichprobenartig verifiziert — die Aussage „9 von 14 Tools (inkl. `metrics_tree`)"
in `Docs/agent-api.md` stimmt exakt mit dem Code überein (`MetricsTreeTool.ExecuteAsync` ruft
`FindSymbolTool.BuildAggregateWarningAsync` für alle vier Modi auf).

### Logische Korrektheit

`ComputeSortValue`/`FormatDisplayLine` in `MetricsTreeScanner.cs` selbst gelesen: `violation_density`
sortiert absteigend nach `ViolationCount` (ungewichtet, wie in „Bekannte Ausnahmen" vermerkt),
`complexity` absteigend nach Ø CC (`SumCyclomatic / MethodCount`), `MaxCyclomatic`/`MaxCognitive`
korrekt per `Math.Max` (nicht Sum) aggregiert. `complexity`-Formatierung nutzt `CultureInfo.
InvariantCulture` (vermeidet den in „Beobachtungen" korrekt benannten, vorbestehenden
`FormatBytes`-Lokalisierungsbug). `MetricsTreeRoslynScannerTests.cs` gelesen: Tests sind
aussagekräftig (echte Sortier-Assertions per Text-Index-Vergleich, nicht nur „kein Crash"), Edge-Case
„Datei ohne Methoden" prüft explizit `Ø CC 0.0`/`max CC 0` statt nur Erfolg.

### Konzept-Treue (Ebene 4)

Alle 4 Muss-Haben-Modi umgesetzt, kein Non-Goal berührt (kein Mermaid/Graph-Output, kein
`method_count`), CLI-`--map`-Subcommands unangetastet (nicht im Diff). `tasks/features/05-roadmap.md`
korrekt und objektiv abgehakt — zwei Akzeptanzkriterien bewusst `[ ]` belassen mit Anmerkung (5+
Tests „1 pro Mode" bei nur 4 Modi; Doku „mit Beispielen pro Mode" nur als Tabellenzeile), exakt wie
von `AiNetLinterRichtlinien.mdc` §1 Objektivitätsregel gefordert — kein stillschweigendes Abhaken.
Umfang der Umsetzung deckt sich mit der Intention im Step-Plan, keine Scope-Über- oder -Unterschreitung.

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx                                                            → grün (0 Warnungen, 0 Fehler)
dotnet test --filter "FullyQualifiedName~MetricsTree|FullyQualifiedName~McpServerCommand" → grün (73 Tests, 0 Fehler)
get_violations (voller Scope)                                                             → 0 Warnungen/Fehler Produktionscode
                                                                                              (3 vorbestehende Fixture-Fehler unverändert)
```

Der Abschluss-Volllauf `dotnet test --filter Category!=Stress` selbst wurde **nicht** erneut
ausgeführt (>800s laut Projekt-Konvention, laut Auftrag nicht zwingend) — aber per `--list-tests`
gegen exakt denselben Filter selbst nachgeprüft: **1349** discoverte Tests, identisch mit der im
Step-Result gemeldeten Zahl. Zusammen mit grünem Build und grünem gezieltem Lauf plausibel.

## Sonstige Beobachtungen / MINOR / NITPICK

- Der im Step-Result dokumentierte Testbefehl `dotnet test --filter "FullyQualifiedName~MetricsTree|~McpServerCommand"`
  (zweite Filterbedingung ohne `FullyQualifiedName~`-Präfix) ist bei eigener Reproduktion ein
  **ungültiger** VSTest-Filter (`Ungültiges Format für TestCaseFilter`) — nicht der Befehl aus
  `step-plan.md` (dort korrekt mit `FullyQualifiedName~` vor beiden Bedingungen). Die gemeldete Zahl
  „102 Tests" lässt sich mit keinem plausiblen Filter reproduzieren; der tatsächliche, korrekte Befehl
  liefert 73 Tests. Reine Transkriptionsungenauigkeit im Report (Build/Tests sind nachweislich grün,
  keine Auswirkung auf die eigentliche Umsetzung) — kein Finding, aber für künftige Reports auf exakte
  Kommandozeilen-Übernahme achten.
- `ServerInstructions.cs` listet `metrics_tree` weiterhin nicht im `initialize`-Handshake-Text
  (vorbestehende Lücke seit EPIC-01, vom Coder korrekt als außerhalb des Datei-Scopes benannt) — nicht
  neu durch diesen Step, kein eigener Tech-Debt-Eintrag nötig (bereits in `step-result.md`
  „Beobachtungen" vermerkt, minimal).

## Tech-Debt-Update (kein neuer Eintrag)

- `TD-001` in `tasks/metrics-tree/tech-debt.md` aktualisiert: betroffene Dateien jetzt
  `AnalysisToolRegistrations.cs` + `Mcp/Tools/MetricsTree/MetricsTreeTool.cs` (verschoben aus
  `FileStructureToolRegistrations.cs`). Selbst per `get_violations` verifiziert: aktuell keine
  Warnungen, aber der Footprint-Druck ist **verlagert und tendenziell verschärft**, nicht behoben —
  beide Klassen liegen nur noch wenige Zeilen unter ihrem jeweils angehobenen `PathOverride`. Priorität
  bleibt `mittel`, Facade-Vorschlag weiterhin offen und gültig.

## Roadmap-Status

`tasks/metrics-tree/roadmap.md` zeigt EPIC-02 aktuell noch als **nicht** abgehakt (`[ ] EPIC-02 …
**In Arbeit → `step-003`**`) — der Doku-Commit dieses Steps hat diese Datei nicht angefasst
(Aktualisierung ist laut `../skills/kritiker/SKILL.md`/`../skills/planer/SKILL.md` Aufgabe des
Planers nach `approved`-Verdict, nicht des Coders). Mit diesem `approved`-Verdict ist EPIC-02
inhaltlich vollständig abgeschlossen — die formale Abhakung in `roadmap.md` steht noch aus und ist
der nächste Schritt des Orchestrators/Planers, danach ist `roadmap.md` vollständig abgehakt.
