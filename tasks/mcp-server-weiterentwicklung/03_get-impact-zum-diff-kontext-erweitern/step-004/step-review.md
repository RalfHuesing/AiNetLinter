---
status: done
type: step-review
task: 03_get-impact-zum-diff-kontext-erweitern
step: 004
epic: EPIC-3+EPIC-4
step_type: single
reviewed_by: kritiker
reviewed_by_model: stealth/ox-alpha
reviewed_by_model_knowledge_cutoff: unbekannt
reviewed_at: 2026-08-22T23:57:00+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 004: Testfundament, gebatchte Test-Zuordnung & recommendedTestCommands (EPIC-3+4)

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Korrektur-Step `step-<MMM>` angelegt (`corrects: step-004`)
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

Ein einziges MAJOR-Finding (Ebene 3, Logik): der neue deduplizierte
Testbefehl ist im Mehrklassenfall als Shell-Zeile nicht ausführbar — der
`|`-Verbundfilter ist nicht quotiert. Alles andere (alle vier dokumentierten
Abweichungen, Counter-Nachweise, Konzept-Treue, Rules, Gates) ist geprüft und
in Ordnung; der Fix ist rein mechanisch, daher ohne erneuten Planer-Aufruf
transkribierbar.

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
      (Verteilung auf Dateien wie in step-result „Abweichungen" 2–4
      dokumentiert; jede einzeln gegen den echten Guard/Regelkontext verifiziert)
- [x] Rules-Konformität: `.agents/rules/AiNetLinter.mdc#grenzwerte-produktion`
      und `.agents/rules/AiNetLinterRichtlinien.mdc#5-qualitätsdrift-prävention`
      / `#4-updates-tests` eingehalten
- [x] Logische Korrektheit: mit einer Ausnahme (Finding 1) korrekt;
      Counter-Beweis, Evidenztrennung, Dedup und Wrapper-Identität selbst
      gegen den Diff und die Tests nachgelesen
- [x] Konzept-Treue: Muss-Habens dieses Steps erfüllt, keine Non-Goal-
      Umsetzung, Bestandsverhalten unverändert
- [x] Build: selbst nachgeprüft, grün (0 Warnungen, 0 Fehler)
- [x] Tests: selbst nachgeprüft, grün (FastTests 1605/1605,
      IntegrationTests 348/348, je `Category!=Stress`)
- [x] CodeMap aktualisiert (stichprobenartig gegen den Diff: alle
      step-004-Module eingetragen, keine Lücke)

## Befund

### Plan-Erfüllung

Alle sechs „Konkreten Änderungen" umgesetzt, teils an abweichenden, im
Result begründeten und von mir verifizierten Orten:

1. **Batch-API** (`FindTestsForSymbolsAsync` +
   `TestCoverageBatchScanResult`/`…SymbolResult`) vorhanden; Kern liegt als
   partial `TestCoverageScanner` in `Core/TestCoverageBatchScan.cs`, Records am
   Ende von `TestCoverageScanner.cs`. Projekte/Dokumente werden genau einmal
   iteriert, SyntaxRoot+SemanticModel je Dokument genau einmal bezogen
   (`ScanDocumentAgainstTargetsAsync`), dann gegen alle Ziele gematcht.
   `AnalyzeDocument`/Evidenzkonstanten/Prioritäten unangetastet wiederverwendet.
   `FindTestsForSymbolAsync` ist dünner Wrapper mit `[symbol]`.
2. **`BuildRecommendedCommands`** private→internal, reine Weiterleitung auf
   `TestRecommendationBuilder.BuildDotNetTestCommands`; Dedup je Testprojekt
   über Klassenvereinigung, ordinal sortiert — siehe aber Finding 1 zum
   Befehlsformat.
3. **`DiffImpactCounters`** existiert (in `DiffImpactAnalysisModels.cs` statt
   eigener Datei, s. Abweichungen 3/4), optionaler `Counters`-Parameter am
   `DiffAnalysisRequest` (kein fünfter Positionsparameter), Null-Verhalten ohne
   Übergabe bestätigt.
4. **Fixture** (`ChangeContextScenarioFactory` + `ScenarioSymbols`) im TestKit:
   drei Projekte `App.Core` → `App` → `App.Tests`, public `PlaceAsync` mit
   Call-Sites (direkter Invocation-Test ruft `service.PlaceAsync()`), private
   `LogInternal` ohne externe Aufrufstellen, Hunk-Ranges, Symbol-Handles —
   plan-konform, nur Assembly abweichend (s. u., Ebene 4).
5. **Batch-Tests**: alle geforderten Nachweise vorhanden und aussagekräftig
   (Counter==1 bei zwei Zielen, Evidenzarten getrennt asserted, private Methode
   per Naming Convention, Wrapper≡Batch feldidentisch, Command-Dedup exakter
   String doppelt berechnet, zusätzlich Leerliste-ohne-Scan).
6. **OnceOnly-Test** in IntegrationTests: zusammengesetzter Lauf auf echtem
   Git-Mini-Workspace, `GitRuns==1 && TestSolutionScans==1` bei N=2, `LintRuns`
   bleibt 0 gepinnt.

Commit `7b3b0284` passend (Conventional Commit, Task-Ref); Doku-Commit getrennt.

### Rules-Konformität

Gegen die vom Plan referenzierten Regeln geprüft — eingehalten:

- Grenzwerte: größte neue Produktionsdatei 333 Zeilen (`TestCoverageScanner.cs`,
  <500); Methoden laut `metrics_lookup` max. 27 LOC / CC 8 / 3 effektive
  Parameter (`RunAnalysisAsync`, `ScanDocumentAgainstTargetsAsync`) — alle OK.
  `sealed`/static überall, `#nullable enable` in jeder neuen Datei, kein neues
  leeres `catch`. Partial-Split `TestCoverageScanner` = genau 2 Dateien
  (= Limit `MaxPartialClassFiles`).
- `MaxDirectoryChildren=30`: `src/AiNetLinter/Core` enthält exakt 30 Einträge —
  die dokumentierte Konsolidierung (Abweichung 3) war zwingend, nicht Kosmetik.
- DRY: Wrapper statt zweiter Logik, ein Command-Builder für Tool und Batch-Antwort.
- Zero-Warning durch eigenen Build bestätigt.
- Keine Task-ID-/Step-Referenzen in Codekommentaren (Suche nach
  `step-\d+|EPIC-\d|TD-\d\d\d` über `src/**/*.cs`: 0 Treffer).
- xUnit v3, keine Serialisierungs-Collection (nur `[Trait]`),
  `TestTempDirectory` für den Mini-Git-Workspace verwendet.

### Logische Korrektheit

- **Counter-Beweis (Kernanforderung „kein N-mal-Scan"):** Inkrement-Stellen sind
  korrekt platziert — `GitRuns` unmittelbar vor dem einzigen `RunGitDiff` im
  gemeinsamen `RunAnalysisAsync` (instrumentierte Läufe gehen denselben Pfad),
  `TestSolutionScans` einmal je Batch-Aufruf nach Leerprüfung. Tests pinnen
  `TestSolutionScans==1` bei zwei Zielen (FastTests UND Integration) sowie
  `GitRuns==1`; das ist ein echter Nachweis, kein Tautologietest.
- Leere Zielliste → kein Scan, kein Inkrement: war nicht explizit geregelt,
  sinnvoll entschieden und per Test gepinnt.
- Evidenzarten, Prioritäts-Sortierung und Ergebnisfelder des Wrappers sind
  ausdrucksgleich zum Altcode; Identität ist durch den Feldvergleichstest plus
  alle unangetastet grünen Bestandstests belegt (kein Alt/Neu-Diff —
  ausreichend, transparent im Result dokumentiert).
- **Finding 1 (das einzige):** der deduplizierte Mehrklassen-Befehl ist nicht
  direkt ausführbar — Details unten.

### Konzept-Treue (Ebene 4)

- Muss-Habens dieses Steps erfüllt: gebatchte Zuordnung ohne vollständigen
  Scan pro Symbol (Counter-Beweis), deduplizierte Befehle je Testprojekt (Form
  vertraglich, Quoting siehe Finding 1), Fixture ≥2 Produktionsprojekte + 1
  Testprojekt mit privater call-site-freier Methode, direkte Invocation und
  Namenskonvention als getrennte Evidenzarten. Non-Goals nicht berührt (keine
  Testausführung, kein neues MCP-Tool, Caps bleiben EPIC-6).
- Bestandsverhalten unverändert: `callers`-Pfad läuft über denselben
  `RunAnalysisAsync`, nur um internal erweitert; alle 1605 FastTests inklusive
  der Scanner-/Tool-Bestände grün.
- **Die vier dokumentierten Abweichungen — alle verifiziert und akzeptabel:**
  1. *Fixture im TestKit, OnceOnly-Test in IntegrationTests:* Die Begründung
     ist real — `FastTestsDependencyGuardTests` scannt die Metadaten von
     FastTests.dll **und** TestKit.dll und verbietet TypeRefs auf
     `System.Diagnostics.Process` (Zeile 77); der GitRuns-Nachweis braucht
     einen echten Git-Subprozess über den Analyzer-Pfad. Das Konzept sagt
     nichts zur Assembly-Zugehörigkeit von Fixture/Test — Platzierung ist
     Implementierungsdetail, der Nachweis läuft weiterhin im Gate
     (`Category!=Stress`). Akzeptabel.
  2. *Git-Workspace in `FixtureWorkspaces.cs`:* Real —
     `McpProcessArchitectureGuardTests` zählt `Process.Start(`-Callsites
     dateiweise (`Assert.Equal(3)`) und pinnt die Owner-Dateien inkl.
     `Fixtures/FixtureWorkspaces.cs`; eine neue Process-haltige Datei hätte den
     Guard gerissen. `RunGit`→`FixtureGit`-Extraktion verhält sich identisch
     (Pragma mitgezogen). Korrekt gehandhabt.
  3. *Datei-Konsolidierung:* Core-Verzeichnis exakt 30 Kinder — ohne
     Konsolidierung wäre `MaxDirectoryChildren` gerissen worden. Gewählte
     Zielorte (Records beim Scanner, Counter bei den Analysemodellen, Builder
     im TestContext-Ordner — vom Plan ausdrücklich erlaubt) sind sachlich
     naheliegend.
  4. *Counter-Design:* int-Felder + `Interlocked` statt Count*-Methoden
     (MiddleMen-Gefahr real), interner Einstieg `FindTestsForSymbolsCoreAsync`
     (eine öffentliche Counters-Überladung wäre wegen des optionalen `ct`
     CS0121-mehrdeutig), `RunAnalysisAsync` internal statt neuer
     Wrapper-Einstiegspunkte — jeweils konsistent mit den Rules und sauber
     XML-dokumentiert.
- Command-Formatwechsel je-Projekt-statt-je-Klasse ist vertraglich gewollt
  (Konzept-Must-have); der einzige Bestands-Assert dazu ist ein Einzelklassen-
  Substring und bleibt zu Recht grün.

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx                                          → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress        → grün (1605 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (348 Tests, 0 Fehler)
```

Dogfood selbst nachgeprüft: `metrics_lookup` über sechs neue/geänderte Symbole
(Batch-API, Wrapper, Scan-Kern, Builder, `DiffImpactCounters`,
`RunAnalysisAsync`) — alle Schwellwerte OK. `find_duplicates`: Solution-weit
nur das bekannte Test-Grundrauschen; scoped auf Produktion und TestKit keine
Cluster aus step-004-Dateien.

## Findings

1. `src/AiNetLinter/Mcp/Tools/TestContext/TestRecommendationBuilder.cs:62-65` —
   [MAJOR] [Logische Korrektheit] Der gebaute Befehl enthält bei ≥2 Treffer-
   klassen im selben Projekt einen **unquotierten `|`**
   (`dotnet test App.Tests --filter FullyQualifiedName~A|FullyQualifiedName~B`).
   Als Shell-Zeile deuten cmd, PowerShell und bash das `|` als Pipe — der zum
   Kopieren gedachte Befehl zerbricht dann in `dotnet test … ~A` plus einem
   nicht existierenden Kommando `~B`. Das widerspricht dem eigenen Vertrag des
   neuen Codes: das XML-Doc verspricht „direkt ausführbare dotnet test-Befehle",
   die Tool-Beschreibung (`AnalysisToolRegistrations.cs`, „kopierbare dotnet
   test Filterbefehle") dasselbe, und der Step-Plan nennt die Ausführbarkeit
   explizit als Formatvorlagen-Qualität. Der Einzelklassenfall (alter
   Bestands-Assert) ist korrekt; der Defekt trifft genau den neuen
   Dedup-Normalfall. **Fix (mechanisch):** in `BuildCommand` den kompletten
   Filterwert in doppelte Anführungszeichen setzen, sobald mehr als eine Klasse
   vereint wird — also `$"--filter \"{filter}\""` nur im Mehr-Klassen-Zweig
   (oder äquivalent: `filter` bei `classNames.Count > 1` mit `"` wrappen).
   Doppelte Anführungszeichen funktionieren in cmd, PowerShell und bash.
   Einzelklassen-Ausgabe unverändert lassen, damit die Bestands-Asserts
   (`GetTestContextToolTests.cs:162,347`) ohne Anpassung grün bleiben. Den
   Erwartungs-String in
   `src/AiNetLinter.FastTests/Core/TestCoverageBatchScannerTests.cs:105-109`
   um die Quotes ergänzen.

## Sonstige Beobachtungen / MINOR / NITPICK

- `GetTestContextTool.BuildRecommendedCommands` ist jetzt reiner Forwarder —
  plangemäß so vorgesehen, steht aber philosophisch nahe an
  `AvoidExcessiveMiddleMen`. Wenn EPIC-6 direkt auf `TestRecommendationBuilder`
  geht, kann der Forwarder entfallen; Entscheidung gehört dort hin, nicht in
  diesen Step.
- Die synthetischen Hunk-Zeilenkonstanten (`PlaceAsyncBodyLine/
  LogInternalBodyLine = 7`) koppeln an die Fixture-Quelltexte; Verschieben der
  Methodenzeilen schlägt die Hunk-Tests lautstark. Bewusst so gewählt, ok.
- Match-Schleife O(Ziele) je Dokument: Parse/Semantikmodell passieren exakt
  einmal je Dokument — die Konzept-Anforderung („höchstens einmal parsen/
  semantisch auswerten") ist wörtlich erfüllt; der Match selbst ist billig
  (String-/Symbolvergleiche auf geteiltem Root/Model). Ein Ein-Pass-Match gegen
  alle Ziele würde die bewährte Evidenzlogik umbauen; Performancegewinn erst bei
  sehr vielen Zielen (Cap 100). Bewertung: **ok**, kein Tech-Debt-Wert — die
  Form ist vom Plan so vorgegeben und bewusst abgewogen.
