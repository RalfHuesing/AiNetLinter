---
status: done (Korrektur ausstehend)
type: step-plan
task: speedup-tests
step: 002
corrects: null
title: "Migrationsledger, Architekturguards und Baseline-Messung"
epic: EPIC-1
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-12
related_to: []
---

# Step 002: Migrationsledger, Architekturguards und Baseline-Messung

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-1` aus `roadmap.md` — Fundament. Step-001 hat die drei
  Zielprojekte, die gemeinsame `TestProject.props` und die beiden
  produktiven Konfigurationsverträge (`ProjectOverrides`,
  `TestProjectNameSuffixes`) geliefert. Offen sind laut `roadmap.md`-Notiz
  noch: Architekturguards, Ledger, Legacy-Build-Gate, Baseline-Messung,
  `InternalsVisibleTo` und Minimum Safety Envelope. Dieser Step deckt
  **Ledger, Architekturguards und Baseline-Messung** ab — die restlichen
  drei Punkte bleiben bewusst für einen Folge-Step (siehe „Notes" unten,
  Begründung der Reihenfolge).
- **Konzept-Referenz:** `konzept.md` Leitplanke 0 (Rückkopplung
  Selbstlint/Sentinel), Leitplanke 6 (Kategorisierung als
  Architekturvertrag, inkl. Unterabschnitt „Was die Guards wirklich
  können" und „Runner- und Prozessparallelität"), Leitplanke 8
  (Strangler-Migration, Unterabschnitt „Zwei Mechanismen, die das Ledger
  von Dokumentation zu Schutz machen" — hier nur der Ledger-Teil, nicht
  das Build-Gate), Leitplanke 10 (Messmethodik).

## Aktueller Projektzustand (JIT-Kontext)

- `tasks/speedup-tests/test-migration-ledger.md` existiert noch nicht
  (verifiziert). `tech-debt.md` existiert ebenfalls noch nicht — kein
  Fehler, nur noch keine Einträge im Task bisher.
- `src/AiNetLinter.Tests/` enthält 222 `.cs`-Dateien, davon 183 mit
  mindestens einem `[Trait("Category", ...)]` (210 Vorkommen `Unit`, 43
  `Integration`, 1 `Stress`). Die restlichen ~39 Dateien sind Fixtures,
  Helper oder reine Infrastruktur ohne eigene Testklasse — für den
  Ledger-Konsistenzguard zählen nur tatsächliche Testklassen (Klassen mit
  mindestens einer `[Fact]`/`[Theory]`-Methode), nicht jede `.cs`-Datei.
- `src/AiNetLinter.Tests/Architecture/ArchitectureTests.cs` ist trotz des
  Namens **kein** Architekturguard, sondern ein reiner
  `LinterAnalyzer`-Regeltest (verifiziert: prüft
  `EnforceSealedClasses`/`AllowDynamic`/etc. gegen synthetischen
  Quelltext). Die neuen Guards werden bewusst als eigene Klassen an
  anderer Stelle angelegt, nicht dort angehängt (Leitplanke 6 warnt
  explizit vor dieser Verwechslung).
- `src/AiNetLinter.FastTests/` und `src/AiNetLinter.IntegrationTests/`
  enthalten nach Step-001 je zwei bzw. eine Proof-Testklasse, sonst noch
  keine eigene Unterstruktur für Architektur-/Guard-Tests.
- `[assembly: InternalsVisibleTo("AiNetLinter.Tests")]` existiert bis
  heute nur einmal, in `src/AiNetLinter/Core/LinterEngine.cs`
  (verifiziert per Grep). Dieser Step braucht keinen neuen Eintrag: die
  Ledger- und Guard-Tests konsumieren ausschließlich öffentliche APIs
  bzw. reflektieren über bereits geladene Assemblies, keine internen
  Produkttypen.
- `.runsettings` schreibt heute fix `TestResults/latest.trx`
  (verifiziert). Für die Baseline-Messung reicht ein Override per
  `--logger "trx;LogFileName=<name>.trx"` auf der Kommandozeile — der
  globale `.runsettings`-Default für den täglichen Diagnoseworkflow
  (`AiNetLinterRichtlinien.mdc` §3) wird in diesem Step **nicht**
  angetastet; die volle Umstellung auf pro-Profil-`LogFileName` ist
  laut `konzept.md` Leitplanke 10 ohnehin erst mit der Aktualisierung von
  `AGENTS.md`/der Diagnoseregel fällig (spätes Epic, nicht hier).
- `AGENTS.md` §2 nennt die heutigen produktiven Kommandos
  (`--filter Category=Unit`, `--filter Category!=Stress`) — das sind die
  „heutigen, semantisch gleichwertigen Profile" aus Leitplanke 10, gegen
  die jetzt gemessen wird. Es gibt noch keine separate `Dogfood`- oder
  `Component`-Kategorie im Bestand; die Baseline misst deshalb genau die
  drei heute real existierenden, im Alltag genutzten Profile
  (`Unit`, `Integration` exklusive Stress, Build), nicht mehr.
- `src/AiNetLinter/Core/Checkers/PhantomDependencyChecker.cs` existiert
  bereits als semantisches (SemanticModel-basiertes) Deny-Pattern für
  verbotene Abhängigkeiten im Produktcode — als Referenzmuster für „wie
  sieht eine Deny-Liste im Bestand aus" nützlich, aber nicht direkt
  wiederverwendbar: die neuen Guards prüfen kompilierte
  Assembly-/Metadaten-Referenzen bzw. geladene Laufzeit-Assemblies einer
  **Test**-Assembly, kein Roslyn-`SemanticModel` von Produktquelltext.

## Intention

Nach diesem Step existiert ein maschinell geprüftes Sicherheitsnetz für
die anstehende Migration, bevor der erste fachliche Test verschoben
wird: ein vollständiges Migrationsledger mit Konsistenzguard (jede
Legacy-Testklasse ist erfasst, kein stiller Statusdrift möglich), zwei
Architekturguard-Ebenen in `AiNetLinter.FastTests`/`AiNetLinter.TestKit`
(statische Deny-Liste über kompilierte Metadaten + Laufzeitcheck auf
geladene MSBuild-Assemblies) sowie ein Kategorien-/Profilguard je
Ziel-Assembly. Zusätzlich liegt eine reproduzierbare Vorher-Baseline
(Median über mindestens drei Läufe, Build getrennt von Testzeit) vor, auf
die sich der spätere Vorher-/Nachher-Nachweis stützen kann. Damit ist die
Migration ab dem nächsten Kohorten-Step gegen die drei in
Konzeptfehler 2/3/16 benannten Risiken (verfrühte Quarantäne, Ledger ohne
laufenden Schutz, dokumentationsartiges Ledger) abgesichert, ohne bereits
selbst produktiven Testcode zu verschieben.

## Konkrete Änderungen

### Datei 1: `tasks/speedup-tests/test-migration-ledger.md` (neu)

- **Was:** Versioniertes Migrationsledger nach dem in `konzept.md`
  Leitplanke 8 beschriebenen Schema. Kopf: kurze Statuslegende
  (`pending`/`migrated`/`consolidated`/`removed-trivial`) und die
  Konsistenzregeln, die der Guard (Datei 2) durchsetzt. Danach eine
  Tabelle mit einer Zeile pro heutiger Legacy-Testklasse (ermittelt aus
  den 183 kategorisierten Dateien plus jeder weiteren Datei mit
  mindestens einer `[Fact]`/`[Theory]`-Methode): Quelldatei, Testklasse,
  Produktbereich (aus dem Verzeichnis ableitbar, z. B. `Cli`, `Mcp`,
  `Baseline`, `Configuration`, `Maps`, `Suppression`, `Cache`,
  `Diagnostics`, `Output`, `FalsePositives`, `Metrics`, `Web`,
  `Core`/`Commands`), Status (initial durchgehend `pending`), gezielter
  Legacy-Filter (z. B. `FullyQualifiedName~<Klassenname>` oder ein
  engerer Namespace-Filter, wo mehrere Klassen zusammengehören), neuer
  Abdeckungsort (leer bei `pending`). Für `migrated`/`consolidated`/
  `removed-trivial` sind die tieferen Pflichtfelder aus dem Muss-Haben
  (Produktbereich/Risiko, Erfolgs-/Negativ-/Fehlerfall, Evidenz) erst ab
  dem Kohorten-Step fällig, in dem die jeweilige Zeile den Status
  wechselt — die Initialbefüllung in diesem Step bleibt bewusst auf
  Inventar-Ebene (Konsistenzguard prüft genau das, siehe Datei 2), nicht
  auf vollständiger Vertragsdokumentation für alle ~250 Klassen auf
  einmal.
- **Warum:** Muss-Haben „versioniertes Migrationsledger" +
  Konzeptfehler 16 („Ein Ledger, das nur ein Dokument ist, driftet").
  Ohne vollständiges Inventar zum Start kann der Konsistenzguard in
  Datei 2 nicht sinnvoll prüfen, ob jede Legacy-Klasse erfasst ist.

### Datei 2: `src/AiNetLinter.IntegrationTests/Migration/TestMigrationLedgerConsistencyTests.cs` (neu)

- **Was:** `[Trait("Category", "Integration")]`-Testklasse, die (a) das
  Ledger aus Datei 1 parst, (b) über Reflection auf der bereits
  geladenen `AiNetLinter.Tests`-Testassembly (oder gleichwertig: durch
  Scannen der `.cs`-Dateien nach Klassen mit `[Fact]`/`[Theory]`, falls
  ein direkter Assembly-Load zu MSBuild-Testprojekt-Startproblemen
  führt — Umsetzungsdetail bleibt beim Coder) jede tatsächliche
  Legacy-Testklasse ermittelt und gegen das Ledger abgleicht. Prüft die
  vier in Leitplanke 8 genannten Fehlerfälle: (a) Testklasse ohne
  Ledger-Eintrag, (b) `migrated`/`consolidated`-Eintrag, dessen
  Legacy-Klasse noch existiert, (c) Eintrag, dessen neuer Abdeckungsort
  nicht existiert, (d) `removed-trivial` ohne Begründungstext.
- **Warum:** Konzeptfehler 16 — der Guard macht das Ledger zu einer
  geprüften Invariante statt einer Absichtserklärung.

### Datei 3: `src/AiNetLinter.FastTests/Architecture/FastTestsDependencyGuardTests.cs` (neu)

- **Was:** `[Trait("Category", "Unit")]`-Testklasse (reine
  Metadaten-/Reflection-Prüfung, kein MSBuild/Prozess nötig) mit
  mindestens zwei Prüfungen: (1) statische Deny-Liste über die
  kompilierten Metadaten-Referenzen von `AiNetLinter.FastTests.dll` und
  `AiNetLinter.TestKit.dll` (Metadaten-Reader oder gleichwertig, nicht
  Quelltext-Grep) gegen mindestens `Microsoft.Build.*`,
  `Microsoft.CodeAnalysis.MSBuild.*`, `MSBuildWorkspace`,
  `SourceFileCatalog.LoadAsync`, `System.Diagnostics.Process`; (2) ein
  Laufzeitcheck (z. B. über eine Assembly-Fixture, deren Disposal am
  Ende des Fast-Laufs `AppDomain.CurrentDomain.GetAssemblies()` gegen
  dieselbe Deny-Liste prüft), dass keine dieser verbotenen Assemblies
  während des tatsächlichen Testlaufs geladen wurde.
- **Warum:** Konzeptfehler 5 + Leitplanke 6 Unterabschnitt „Was die
  Guards wirklich können" — Projekttrennung allein verhindert teure
  Fast-Tests nicht, weil die Produktreferenz MSBuild-Typen transitiv
  erreichbar macht.

### Datei 4: `src/AiNetLinter.FastTests/Architecture/TestCategoryProfileGuardTests.cs` (neu)

- **Was:** `[Trait("Category", "Unit")]`-Test, der über alle Testklassen
  in `AiNetLinter.FastTests` reflektiert und sicherstellt, dass jede
  genau einen gültigen Kategorie-Trait aus `{Unit, Component}` besitzt
  (Hilfs-/Fixtureklassen ohne `[Fact]`/`[Theory]` ausgenommen).

### Datei 5: `src/AiNetLinter.IntegrationTests/Architecture/TestCategoryProfileGuardTests.cs` (neu)

- **Was:** Gleiches Prinzip wie Datei 4, aber für
  `AiNetLinter.IntegrationTests`: jede Testklasse besitzt genau einen
  gültigen Kategorie-Trait aus `{Integration, Dogfood, Performance,
  Stress}`.
- **Warum:** Leitplanke 6 „Kategorisierung als Architekturvertrag" —
  „Jede Testklasse besitzt genau ein gültiges Laufprofil" wird für beide
  neuen Assemblies getrennt durchgesetzt statt cross-assembly geladen zu
  werden (vermeidet fragile Sibling-Assembly-Pfad-Logik zwischen zwei
  Testprojekten mit potenziell unterschiedlichem Build-Zustand).

### Datei 6: `tasks/speedup-tests/baseline-measurement.md` (neu)

- **Was:** Dokumentierte Baseline-Messung nach `konzept.md`
  Leitplanke 10: (1) `dotnet build AiNetLinter.slnx` einmal separat
  zeitgestoppt; (2) `dotnet test --filter Category=Unit --no-build`
  dreimal hintereinander mit je eigenem
  `--logger "trx;LogFileName=baseline-unit-runN.trx"`, Wall Clock +
  aggregierte Testzeit aus jeder TRX gelesen; (3)
  `dotnet test --filter Category!=Stress --no-build` (heutiges
  Abschlussgate, enthält Unit+Integration) analog dreimal mit eigenem
  `LogFileName`. Ergebnis: Median + Streuung je Profil, Rohdaten-Tabelle,
  Maschinen-/Umgebungskontext, Hinweis auf Ausreißer/Fremdlast statt
  stillem Entfernen. Stress-Kategorie bleibt bewusst außen vor
  (Leitplanke 10 verlangt Dogfood/Performance/Stress getrennt, und
  Stress läuft laut `AGENTS.md` §2 ohnehin nie automatisch) — sie wird
  erst bei der Abschlussverifikation gemessen.
- **Warum:** Muss-Haben „Vorher-/Nachher-Messung" + Leitplanke 10 —
  ohne Baseline vor dem ersten Refactoring ist der spätere relative
  Vergleich nicht belastbar. Dies ist eine bewusste, einmalige Ausnahme
  von der sonst sparsamen Step-Verifikation (Leitplanke 7): der volle
  `Category!=Stress`-Lauf wird hier absichtlich mehrfach ausgeführt,
  weil er selbst das Messobjekt ist, nicht ein Nebeneffekt-Check.

## Tests

- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~TestMigrationLedgerConsistencyTests` — neuer Ledger-Guard grün gegen das neu angelegte, initial vollständig `pending`-Ledger.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~FastTestsDependencyGuardTests` — neuer Deny-Listen-/Laufzeit-Guard grün.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~TestCategoryProfileGuardTests` — Kategorienguard FastTests grün.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~TestCategoryProfileGuardTests` — Kategorienguard IntegrationTests grün.
- [ ] `dotnet build AiNetLinter.slnx` grün (0 Warnungen/Fehler, weiterhin 5 Projekte).
- [ ] Baseline-Messläufe aus Datei 6 tatsächlich ausgeführt und die resultierenden Zahlen in `baseline-measurement.md` eingetragen (kein Platzhaltertext).

Am Ende dieses Steps zusätzlich die im Konzept vorgesehene Abschnittsgrenze:
`dotnet test --filter Category!=Stress` (heutiges Abschlussgate) grün —
das ist ohnehin Teil der Baseline-Messläufe aus Datei 6, muss also nicht
doppelt separat gefahren werden.

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün
- [ ] Test-Command aus Tech-Stack-Notiz grün (siehe „Tests" oben — gezielte Filter, kein reflexartiger Volllauf außer der bewusst vorgesehenen Baseline-Messung selbst)
- [ ] Ledger-Konsistenzguard (Datei 2) tatsächlich rot, wenn testweise eine Legacy-Klasse ohne Ledger-Eintrag simuliert wird (kurz verifizieren, dann zurücksetzen — Nachweis, dass der Guard nicht nur grün, weil er nichts prüft)
- [ ] Commit auf aktuellem Branch (Conventional Commit)
- [ ] `step-002/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §3 — TRX-Diagnose/Logging-Verhalten, relevant für die `--logger`-Overrides der Baseline-Messläufe (Datei 6), ohne den globalen `.runsettings`-Default zu ändern.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 — Testsuite-Parallelität bewahren: die vier neuen Guard-Testklassen (Dateien 2-5) dürfen nicht ungeprüft in eine zwangsserialisierende Collection gepackt werden; falls der Laufzeitcheck in Datei 3 eine Ausführungsreihenfolge braucht (z. B. „am Ende des Fast-Laufs"), ist das über eine begründete Assembly-Fixture zu lösen, nicht über pauschale Collection-Serialisierung der ganzen Assembly.

## Bekannte Ausnahmen

- Keine bekannten flaky Tests in diesem Step.

## Notes

- **Warum Minimum Safety Envelope, Legacy-Build-Gate (technischer Anker)
  und `InternalsVisibleTo` bewusst nicht in diesem Step stecken:**
  Laut `konzept.md` Leitplanke 8 Vertrag Punkt 2 vor 3 muss die Minimum
  Safety Envelope (repräsentative Tests für Config laden,
  Solution analysieren, CLI-Adapter, MCP-Handshake) stehen, **bevor**
  das Legacy-Projekt quarantiniert wird. Der technische Anker
  „Legacy-Build-Gate" bekommt seine eigentliche Schutzfunktion erst in
  dem Moment relevant, in dem das Legacy-Projekt nicht mehr im normalen
  Gate mitläuft (vorher läuft es ohnehin bei jedem `Category!=Stress`
  mit — der Build-Gate-Fall „niemand merkt Bitrot" tritt erst nach der
  Quarantäne ein). Beides zusammen mit der Quarantäne selbst in einem
  Step zu planen ist die sauberere Schnittgrenze, statt hier ein
  Bau-Gate ohne aktuellen Schutzzweck vorzuziehen. `InternalsVisibleTo`
  wird erst gebraucht, sobald `AiNetLinter.TestKit` tatsächlich interne
  Produkttypen berührt (frühestens `RoslynTestSolutionFactory` in
  EPIC-2) — ein vorsorglicher Eintrag ohne Konsumenten wäre unbelegter
  Vorgriff.
- Der nächste Step (voraussichtlich `step-003`) baut damit die Minimum
  Safety Envelope, das Legacy-Build-Gate und die tatsächliche
  Quarantäne-Ausführungsfilterung auf — erst danach ist `EPIC-1`
  vollständig abgedeckt.
- Für die Bereichszuordnung im Ledger (Datei 1) reicht die
  Verzeichnisstruktur unter `src/AiNetLinter.Tests/` als erste
  Näherung; wo ein Verzeichnis fachlich uneinheitlich ist (z. B.
  `Core/` mischt mehrere Bereiche), darf der Coder feiner aufteilen —
  wichtig ist, dass jede Zeile einen nachvollziehbaren, nicht
  zufälligen Bereich trägt.
