---
status: done (pending re-audit)
type: step-plan
task: speedup-tests
step: 018
corrects: null
title: "Kumulative Doku-Korrektur: MCP-Read-only-Snapshot-Super-Step mit 23 Klassen"
epic: EPIC-4
estimated_risk: low
step_type: batch
items:
  - id: item-01
    title: "Step-018-Baseline und Recovery 1 bis 6 auditierbar rekonstruieren"
    source: "Git-Historie a6cc275..f0dbacc; step-review.md Finding 1"
  - id: item-02
    title: "23 Klassen, Snapshot-Seam und produktive Snapshot-Anpassungen kumulativ dokumentieren"
    source: "e864407; ae5aa73; 0846624; f0dbacc"
  - id: item-03
    title: "Recovery 6 als letzten Fuenf-Dateien-Teilschnitt abgrenzen"
    source: "Recovery-6-Plan; f0dbacc; Critic-Nachweis 62 Vertraege"
  - id: item-04
    title: "Step-Result und State ohne pauschale 1:1-Behauptung korrigieren"
    source: "step-review.md MAJOR item-04"
created_by: planer
created_by_model: gpt-5.6-sol
created_by_model_knowledge_cutoff: nicht ausgewiesen
created_at: 2026-08-13
related_to:
  - step-006
  - step-015
  - step-016
  - step-017
  - step-018/step-review.md
---

# Step 018 - Kumulative Doku-Korrektur des MCP-Read-only-Snapshot-Super-Steps

## Zweck dieser offenen Korrekturrunde

Der technische Stand wird nicht veraendert. Diese Runde korrigiert ausschliesslich die
Audit-Dokumentation nach dem Critic-Verdict `issues`: Der zuletzt gespeicherte Recovery-6-Plan
beschrieb nur den finalen Fuenf-Dateien-Schnitt, waehrend `f0dbacc` den gesamten seit `e864407`
angesammelten Recovery-Working-Tree mit 40 `src`-Dateien committen musste. Der aktuelle
`step-result.md` stellt diesen Gesamtcommit deshalb unzutreffend als „Plan 1:1 umgesetzt“ dar.

Der korrigierte Plan ist bewusst ein **kumulativer Audit-Plan fuer denselben step-018**. Er ersetzt
nicht die historische Entwicklung und behauptet nicht, alle spaeteren Entscheidungen seien schon
im ersten Plan enthalten gewesen. Recovery 6 bleibt der letzte, eng begrenzte Teilschnitt.

Offen fuer den Doku-Coder sind nur `step-result.md` und die Abschlussstatus-Synchronisation. Keine
C#-, TestKit-, Projekt-, Ledger- oder Testaenderung; keine Tests und kein neuer Codecommit.

## Verbindliche Commit- und Artefaktbasis

- `a6cc275`: initialer Step-018-Plan fuer zwei Duplicate-Detection-Toolklassen.
- `880f6bc`: Nutzerkonforme Erweiterung zum 24-Klassen-Super-Step.
- `e864407`: Roh-Renames der 24 Klassen plus Blocker-Ergebnis; noch keine fertige Migration.
- `e6b3000`: plattenfreie Neuplanung auf 20 FastTests-Klassen und vier vorwaertsgerichtete
  Rueck-Moves.
- `ae5aa73`: nach gruenem Build und 78 roten Tests beschlossene interne
  `ReadOnlySolutionSnapshot`-Seam; Ziel wieder 23 Klassen, nur Suppression Legacy.
- `0846624` / `6f223ca`: Recovery-4-Praezisierung und Start fuer die verbleibenden 42 Fehler,
  Spec-/Call-Site-Korrekturen und Gates.
- Recovery 5: kein eigener Plancommit; der uncommittierte Working Tree schloss die funktionalen
  Gates (126/126, Build 0/0, 253/253, Live 8/8, Suppression 1/1, Guards 3/3, Ledger/Legacy 5/5).
  Als einziger offener Befund blieb der statische 23-Scope-Guard in fuenf Klassen. Dieser belegte
  Zustand war der Eingang fuer Recovery 6.
- Recovery 6: letzter mechanischer Teilschnitt in genau fuenf FastTests-Klassen; 62 vorhandene
  Testmethoden blieben erhalten und die letzten Temp-/Catalog-Helper entfielen.
- `f0dbacc`: **Sammel-Codecommit** des gesamten bis dahin uncommittierten Recovery-Stands,
  einschliesslich Recovery 6; 40 `src`-Dateien, 857 Additionen und 831 Loeschungen laut
  Commitstatistik.
- `5fb77c1`: bisheriger Dokuabschluss; wegen der zu engen Plan-/Resultdarstellung Gegenstand dieser
  Korrekturrunde.
- `3609160` / `6d8acc1`: Critic-Review `issues` und korrigierte Reviewzahlen.

Die Recovery-Nummern 1 bis 3 waren in den damaligen Planfrontmattern nicht einzeln benannt. Die
Zuordnung unten ist daher eine Rekonstruktion aus Commitfolge, Task-State-Uebergaengen, Blocker-
Result und den nachfolgenden Plan-Eingangszustaenden; sie darf im Result nicht als getrennte
Codecommit-Serie ausgegeben werden.

## Kumulative Baseline und Recovery-Historie

| Phase | Belegter Inhalt | Ergebnis / Planabweichung |
|---|---|---|
| Baseline | `a6cc275` plante 2 Duplicate-Detection-Toolklassen; `880f6bc` erweiterte vor Ausfuehrung auf 24 kompatibel angenommene Klassen. | Der verbindliche Step-Scope wurde zum Super-Step; der Zweiklassenplan war damit obsolet. |
| Recovery 1 | `e864407` sicherte die 24 Roh-Renames. Legacy-Baseline 243/243, danach Build mit 26 Compilefehlern. | Kein fertiger Codeabschluss; der Blocker erforderte neue Fixture-/Pfadspezifikation. |
| Recovery 2 | `e6b3000` plante 20 plattenfreie Klassen, vier Rueck-Moves und deklarative SymbolGraph-/CompileError-/DI-/Faulting-Specs. Die Ausfuehrung brachte den Build auf gruen, der 20er Lauf blieb mit 142/220 gruen und 78 rot. | Die Annahme „keine Produkt-Seam“ erwies sich fuer pfadtragende Server-Snapshots als falsch. |
| Recovery 3 | `ae5aa73` plante die interne Snapshot-Seam, `VirtualProjectDirectory`, einen kanonischen Snapshot-Kontext, drei Wiederaufnahmen und nur Suppression als Legacy-Rueck-Move. | Legitime Scope-Erweiterung auf Produkt/TestKit; Zielstand wurde 23 migrierte Klassen. |
| Recovery 4 | `0846624`/`6f223ca` begrenzten weitere Produktarbeit und planten die mechanische Schliessung der noch 42 Fehler: Catalog-Call-Sites, CompileError/DI/Faulting, Seamtests und drei Wiederaufnahmen. | Produkt-Seam war bereits im Working Tree; danach blieben nur noch Test-/Fixture-/Gatearbeiten. |
| Recovery 5 | Uncommittierte Fortsetzung schloss alle funktionalen Gates und den Ledger auf 23 `migrated`; Suppression blieb Legacy/`pending`. | Einziger Rest war der statische Guard in fuenf roh uebernommenen Tests. Kein separater Commit vorhanden. |
| Recovery 6 | Fuenf Klassen wurden auf `RoslynTestSolutionFactory`/`McpInMemoryTestContext` und virtuelle Faulting-Snapshots umgestellt. | Dieser **Teilschnitt** wurde 1:1 umgesetzt: 62 Testnamen und Assertions erhalten, statischer Guard null. Er ist aber nicht der gesamte Inhalt von `f0dbacc`. |

## Kumulativer technischer Scope von Step 018

### 23 migrierte historische Testklassen

Der finale Ledger- und FastTests-Stand umfasst exakt diese 23 Klassen:

1. `LinterAnalyzerArchitectureRuleTests`
2. `LinterAnalyzerTests`
3. `CallGraphTraversalTests`
4. `DependencyGraphScannerTests`
5. `DependencyGraphToolTests`
6. `DiRegistrationHeuristicsTests`
7. `DuplicateDetectionToolRefactoringDriftTests`
8. `DuplicateDetectionToolTests`
9. `FindReferencesToolTests`
10. `GetCallTreeToolTests`
11. `GetFileSkeletonToolTests`
12. `GetHotspotsToolTests`
13. `GetSymbolBodyToolTests`
14. `GetTypeHierarchyToolTests`
15. `GetViolationsToolTests`
16. `McpToolResultsTests`
17. `MetricsTreeRoslynScannerTests`
18. `MetricsTreeToolTests`
19. `PatternDetectScannerTests`
20. `PatternDetectToolTests`
21. `SafeguardScannerTests`
22. `SafeguardToolTests`
23. `SymbolIdentifierResolverTests`

`SuppressionScannerTests` wurde im Verlauf als echter `ScanFile`-/Dateivertrag erkannt, liegt
weiter im Legacy-Projekt und bleibt im Ledger `pending`. `f0dbacc` aenderte dort nur
`#nullable enable`; dies ist zu nennen, aber keine Migration.

### FastTests-Infrastruktur und Snapshot-Vertraege

Step 018 lieferte fuenf deklarative/ownerhaltende Fixtures unter
`src/AiNetLinter.FastTests/Fixtures/`, `CompileErrorHeaderAssertions`, die neue Testklasse
`McpCodeGraphServerReadOnlySnapshotTests` sowie einen Factory-Vertrag fuer
`VirtualProjectDirectory`. Die Specs bilden SymbolGraph, CompileError plural/singular, DI und den
werfenden TextLoader mit virtuellen Pfaden ab; `McpInMemoryTestContext` besitzt den Workspace und
erzeugt Server ueber den Snapshot-Zweig.

### Legitime Produkt-Seam und produktive Snapshot-Anpassungen

`f0dbacc` enthaelt sieben Produktdateien. Sie duerfen in Plan und Result nicht verschwiegen oder
als Recovery-6-Fuenfdateienscope dargestellt werden:

- **Explizit durch Recovery 3 geplant:**
  - `McpCodeGraphServerOptions.cs`: interne optionale `ReadOnlySolutionSnapshot`-Option.
  - `McpCodeGraphServer.cs`: disjunkter Snapshot-Zustand, kein Staleness-Refresh,
    `RefreshCount == 0`; Catalog-/LoadFunc-Livepfad bleibt Default.
- **Im Recovery-Verlauf fuer residente virtuelle Dokumente erforderlich geworden:**
  - `ScopeChecker.cs`: beendet die Projektsuche an nicht existenten virtuellen Verzeichnissen.
  - `WalkedFile.cs`: traegt das zugehoerige Roslyn-`Document`.
  - `SolutionFileWalker.cs`: liest bevorzugt residenten `SourceText` und behaelt nur fuer den
    Livepfad den Dateifallback.
  - `GetHotspotsScanner.cs` und `MetricsTreeScanner.cs`: verwenden den dokumenttragenden
    `WalkedFile`-Leseweg.

Die letzten fuenf Anpassungen waren im engen Recovery-6-Plan nicht enthalten. Im korrigierten
Result sind sie als waehrend der frueheren Recovery-Ausfuehrung hinzugekommene, fachlich passende
Produktanpassungen auszuweisen, nicht nachtraeglich als 1:1-Inhalt von Recovery 6 umzudeuten.

### TestKit

`RoslynTestSolutionFactory.cs` erhielt den optionalen `ProjectSpec.VirtualProjectDirectory`-Wert.
Diese additive Pfadfidelitaet war ab Recovery 3 geplant und wird durch den erweiterten
`RoslynTestSolutionFactoryTests`-Vertrag abgesichert.

## Exakter `f0dbacc`-Scope fuer das Result

Der Doku-Coder gruppiert die 40 `src`-Dateien auditierbar:

- 31 FastTests-Dateien: 23 migrierte Zielklassen plus 5 Fixtures,
  `CompileErrorHeaderAssertions`, Snapshot-Seam-Tests und Factory-Tests.
- 7 Produktdateien: die oben beschriebene Server-Seam und die residenten Dokumenttext-Anpassungen.
- 1 TestKit-Datei: `RoslynTestSolutionFactory.cs`.
- 1 Legacy-Datei: `SuppressionScannerTests.cs`, nur Nullable-Aktivierung.

Der Roh-Move `e864407` ist als vorangegangener Step-018-Codezwischenstand separat zu nennen.
`f0dbacc` ist der finale Codecommit, aber nicht der einzige technische Commit der Step-Historie.

## Belegte Gates

Die vorhandenen Result-/Review-Belege werden unveraendert uebernommen, nicht neu ausgefuehrt:

- Recovery-6-Fuenfklassen: 62/62 gruen.
- Critic-Audit inklusive drei Snapshot-Seam-Vertraegen: 65/65 gruen.
- Enger Recovery-Gate: 126/126 gruen.
- `dotnet build`: 0 Warnungen, 0 Fehler.
- 23 Klassen plus Snapshot-Seam/Factory: 253/253 gruen.
- Legacy-Live-Refresh: 8/8 gruen.
- Legacy-Suppression: 1/1 gruen.
- FastTests Dependency-/Category-Guards: 3/3 gruen.
- Integration Ledger-/Legacy-Gates: 5/5 gruen.
- Statischer 23-Scope-Guard: null Treffer; Ledger exakt 23 `migrated`, Suppression `pending`.

## Offene Doku-Coder-Arbeit

### `step-result.md`

1. Titel und Zusammenfassung auf den kumulativen 23-Klassen-Snapshot-Super-Step umstellen;
   Recovery 6 als letzten Fuenf-Dateien-Teilschnitt separat nennen.
2. Im Commitabschnitt `e864407` als Roh-Move-Zwischenstand, `f0dbacc` als finalen Sammel-
   Codecommit und den spaeteren Dokucommit getrennt ausweisen.
3. „Geaenderte Dateien“ nach der 31/7/1/1-Aufteilung oben dokumentieren und die sieben
   Produktdateien namentlich auffuehren.
4. Eine kurze Recovery-Tabelle Baseline/1–6 mit denselben Evidenzgrenzen wie in diesem Plan
   aufnehmen. Recovery 5 ausdruecklich als uncommittierten Zwischenstand ohne eigenen Plancommit
   kennzeichnen.
5. Abschnitt „Abweichungen vom Plan“ ehrlich korrigieren:
   - Gesamt-Step und `f0dbacc` **nicht** 1:1 zum letzten Recovery-6-Plan;
   - Recovery-6-Fuenfklassenschnitt fuer sich 1:1 umgesetzt;
   - Snapshot-Seam/`VirtualProjectDirectory` waren ab Recovery 3 geplant;
   - die fuenf weiteren Produktdateien waren fruehere Recovery-Erweiterungen und muessen als
     solche offengelegt werden;
   - Suppression wurde entgegen dem 24er Ausgangsplan nicht migriert, sondern bewusst Legacy
     belassen.
6. „Beobachtungen“ und „Bekannte Unschaerfen“ nicht mehr mit „Keine“ fuellen: festhalten, dass
   `f0dbacc` mehrere uncommittierte Recovery-Phasen aggregiert und einzelne Hunks deshalb nur ueber
   Plan-/State-/TRX-Historie, nicht ueber getrennte Codecommits, einer Recovery zugeordnet werden.
7. Frontmatter nach der Doku-Korrektur auf `done (pending re-audit)` setzen; keine Gatezahl oder
   Behauptung erfinden.

### Abschlussstatus

Nach der Resultkorrektur `step-plan.md` und `task-state.md` wieder auf `done (pending re-audit)`
setzen, Coded-Stand `e864407 -> f0dbacc`, Review `issues -> re-audit ausstehend` und Code-/Doku-
Commits getrennt sichtbar halten. Danach `git --no-pager diff --check` und ausschliesslich einen
Dokucommit erstellen.

## State-Artefakt-Entscheidung

- **`task-state.md`: anzupassen.** Diese Runde ist offen; Titel und Commitkette muessen den
  kumulativen Step statt nur Recovery 6 zeigen.
- **`codemap.md`: minimal anzupassen.** Der vorhandene 23/Suppression-Pointer ist richtig. Der
  Servereintrag wird von `planning` auf den realen step-018-Stand gesetzt und der residente
  Dokumenttext-Pfad ergaenzt.
- **`test-migration-ledger.md`: nicht anzupassen.** Die 23 Zielpfade entsprechen dem realen
  Bestand und die Suppression-Zeile ist korrekt `pending`; 5/5 Konsistenzgate ist belegt.
- **`roadmap.md` und `tech-debt.md`: nicht anzupassen.** Der Critic-Fund betrifft nur die
  Auditdarstellung desselben Steps, kein neues Epic und keinen Tech-Debt-Eintrag.

## Stop-Kriterien und Abnahme

- Keine Code-, Test-, Ledger- oder Gate-Aenderung.
- Keine nachtraegliche Behauptung, der gesamte Sammelcommit sei 1:1 aus Recovery 6 entstanden.
- „1:1“ nur fuer den belegten Fuenfklassenschnitt mit 62 erhaltenen Vertraegen verwenden.
- Alle Zahlen und Commitzuordnungen muessen aus den oben benannten Artefakten stammen.
- Falls der Doku-Coder eine technische Inkonsistenz statt einer Dokumentationsluecke findet:
  stoppen und `blocked` melden; keine Codekorrektur in dieser Runde.
- Abnahme durch erneuten Critic-Audit von Plan, Result, Task-State, CodeMap, Ledger und den
  Commits `e864407`, `f0dbacc`, `5fb77c1` plus neuem Dokucommit.

## MCP-/Recherche-Entscheidung

Kein MCP-Aufruf erforderlich. Der Befund ist commit- und artefaktbezogen; `git show`, die
historischen Planversionen, Review, Ledger und CodeMap sind die primaeren Quellen.
