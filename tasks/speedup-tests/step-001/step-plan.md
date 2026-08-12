---
status: done (pending audit)
type: step-plan
task: speedup-tests
step: 001
corrects: null
title: "Drei neue Testzielprojekte + gemeinsame Props + Config-Verträge auf neue Namen"
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

# Step 001: Drei neue Testzielprojekte + gemeinsame Props + Config-Verträge auf neue Namen

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-1` aus `roadmap.md` — Fundament. Dieser Step deckt nur den ersten sinnvoll
  abgeschlossenen Teil ab: die drei Zielprojekte als leere, aber arbeitsfähige Hüllen samt
  gemeinsamer Props-Datei, Solution-Wiring und die beiden produktiven Konfigurationsverträge
  `ProjectOverrides`/`TestProjectNameSuffixes`, die laut Leitplanke 0 sonst sofort falsch-rot
  greifen würden. Architekturguards, Migrationsledger, Legacy-Build-Gate, `InternalsVisibleTo`,
  Baseline-Messung und die Minimum Safety Envelope bleiben für einen oder mehrere Folge-Steps
  desselben Epics offen.
- **Konzept-Referenz:** `konzept.md` §„Grober Lösungsansatz" Punkt 1, Leitplanke 0 (Rückkopplung
  eigenes Repository), Leitplanke 8 Punkt 1 („Neue Zielprojekte und Guards werden zuerst
  arbeitsfähig aufgebaut"), Muss-Haben „Anpassung der produktiven Konfigurationsverträge".

## Aktueller Projektzustand (JIT-Kontext)

- `AiNetLinter.slnx` enthält heute genau zwei Projekte (`src/AiNetLinter.Tests`,
  `src/AiNetLinter`) unter einem einzigen `/src/`-Ordner. Es gibt noch keine
  `src/AiNetLinter.FastTests/`, `src/AiNetLinter.IntegrationTests/`, `src/AiNetLinter.TestKit/`
  und keine `tests/AiNetLinter.TestProject.props` — alle vier sind noch reine Platzhalter in
  `codemap.md`.
- `src/AiNetLinter.Tests/AiNetLinter.Tests.csproj` ist der einzige heutige Referenzpunkt für einen
  Testprojekt-Vertrag: `net10.0`, `Nullable`/`TreatWarningsAsErrors`, `RunSettingsFilePath` auf
  `../../.runsettings`, xUnit-v3-Pakete, sowie ein eigener `PackageReference Update` für
  `Microsoft.Build.Framework` auf `18.8.2` und `Microsoft.NET.StringTools` auf `18.8.2` — das
  überschreibt bewusst die `17.11.48`-Pin aus `src/Directory.Build.props`, die für alle Projekte
  unter `src/` (auch für `AiNetLinter.Tests`) automatisch gilt. Diese Test-spezifische Override
  darf nicht dreifach in den neuen Projekten kopiert werden, sondern gehört laut Leitplanke 0 in
  die neue gemeinsame `TestProject.props`.
- `rules.json` → `ProjectOverrides` enthält genau den Schlüssel `"*.Tests"`. Verifiziert in
  `src/AiNetLinter/Configuration/ProjectConfigResolver.cs` (`IsMatch`, Zeile 118-122): das Muster
  wird zu `^.*\.Tests$` übersetzt — ein literales `.Tests`-Suffix. `AiNetLinter.FastTests` und
  `AiNetLinter.IntegrationTests` matchen das **nicht** (sie enden auf `FastTests`/
  `IntegrationTests`, nicht auf `.Tests`), `AiNetLinter.TestKit` erst recht nicht.
- `rules.json` → `TestSentinel.TestProjectNameSuffixes` (Zeile 291-297) enthält
  `Tests, Test, IntegrationTests, Specs, Spec`. `TestProjectDetector.HasTestProjectNameSuffix`
  (`src/AiNetLinter/Core/TestProjectDetector.cs`, Zeile 42-53) prüft `EndsWith(suffix)` bzw.
  `EndsWith("." + suffix)` case-insensitive. `AiNetLinter.FastTests` matcht bereits über den
  Suffix `Tests`, `AiNetLinter.IntegrationTests` bereits über `Tests`/`IntegrationTests`.
  `AiNetLinter.TestKit` matcht **keinen** vorhandenen Suffix — muss ergänzt werden.
- `InternalsVisibleTo("AiNetLinter.Tests")` existiert genau einmal in
  `src/AiNetLinter/Core/LinterEngine.cs` Zeile 18. Keiner der drei neuen Projekte konsumiert in
  diesem Step `internal`-Produkttypen (sie bleiben in diesem Step leer bzw. bekommen nur die zwei
  unten beschriebenen, öffentlich testbaren Proof-Tests). Ein `InternalsVisibleTo`-Eintrag wird
  deshalb bewusst noch nicht ergänzt — er gehört in den Step, der ihn tatsächlich braucht (z. B.
  `RoslynTestSolutionFactory` in EPIC-2), sonst entsteht ein unbenutzter, nicht verifizierbarer
  Attributs-Eintrag.
- Es existiert noch kein Architekturguard, kein `test-migration-ledger.md` und kein
  Legacy-Build-Gate — dieser Step baut noch keines davon; das bleibt für den nächsten Step-Modus-
  Aufruf innerhalb desselben Epics.
- `src/AiNetLinter.Tests/xunit.runner.json` und `.runsettings` bleiben in diesem Step
  unverändert — profilfähiges TRX-Logging (Leitplanke 10) ist Teil der noch offenen
  Baseline-Messung, nicht dieses Fundament-Teilschritts.

## Intention

Die drei Zielprojekte existieren danach als arbeitsfähige, in `AiNetLinter.slnx` eingebundene
Hüllen mit einer expliziten gemeinsamen Props-Datei statt dreifach kopierter Einstellungen. Die
beiden produktiven Konfigurationsverträge, die laut Leitplanke 0 sofort und ohne fachlichen Grund
falsch-rot würden (`ProjectOverrides`, `TestProjectNameSuffixes`), werden im selben Step
angepasst — mit je einem Test, der die Auflösung für alle drei neuen Namen belegt. Damit ist die
Rückkopplungsgefahr aus Leitplanke 0 für diesen Teilbereich geschlossen, bevor im nächsten Step
tatsächlicher Testinhalt (TestKit-Builder, Architekturguards) hinzukommt.

## Konkrete Änderungen

### Datei 1: `tests/AiNetLinter.TestProject.props` (neu)

- **Was:** Neue, explizit zu importierende Props-Datei mit den gemeinsamen Einstellungen aller
  drei neuen Testprojekte: `TargetFramework net10.0`, `ImplicitUsings enable`, `Nullable enable`,
  `TreatWarningsAsErrors true`, `IsPackable false`, sowie den `PackageReference Update`-Block für
  `Microsoft.Build.Framework` (`18.8.2`) und `Microsoft.NET.StringTools` (`18.8.2`) 1:1 gespiegelt
  aus `src/AiNetLinter.Tests/AiNetLinter.Tests.csproj` (Zeile 39-46). Kein `RunSettingsFilePath`
  hier fest verdrahten, falls `AiNetLinter.TestKit` (reine Class Library, kein Testhost) die Datei
  ebenfalls importiert — stattdessen `RunSettingsFilePath` pro Test-ausführendem Projekt (FastTests,
  IntegrationTests) direkt im jeweiligen `.csproj` setzen, analog zum heutigen Vertrag.
- **Warum:** Leitplanke 0 verbietet explizit, das MSBuild-Paketpinning dreifach zu kopieren; eine
  unscoped `Directory.Build.props` wäre gefährlich, weil sie auch Produkt- oder
  Mini-Fixture-Projekte erfassen könnte. Eine explizit importierte Datei ist der verlangte
  Mittelweg.

### Datei 2: `src/AiNetLinter.FastTests/AiNetLinter.FastTests.csproj` (neu)

- **Was:** Neues SDK-Testprojekt, importiert `../../tests/AiNetLinter.TestProject.props` explizit
  (`<Import Project="..."/>`), setzt zusätzlich `RunSettingsFilePath` auf
  `$(MSBuildThisFileDirectory)../../.runsettings`, referenziert die xUnit-v3-Pakete
  (`Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, `xunit.v3.assert`, `xunit.v3.core`,
  `coverlet.collector`) analog zu `AiNetLinter.Tests.csproj`, sowie `ProjectReference` auf
  `../AiNetLinter/AiNetLinter.csproj` und (sobald Datei 4 existiert) auf
  `../AiNetLinter.TestKit/AiNetLinter.TestKit.csproj`.
- **Warum:** Zielprojekt für Unit-/Component-Tests aus Konzept §1; muss von Anfang an
  arbeitsfähig sein (baubar, testbar), auch wenn dieser Step nur zwei Proof-Tests enthält.

### Datei 3: `src/AiNetLinter.IntegrationTests/AiNetLinter.IntegrationTests.csproj` (neu)

- **Was:** Analoges SDK-Testprojekt für Integration/Dogfood/Performance/Stress. Importiert
  dieselbe `TestProject.props`, referenziert dieselben xUnit-v3-Pakete, `ProjectReference` auf
  `../AiNetLinter/AiNetLinter.csproj` und `../AiNetLinter.TestKit/AiNetLinter.TestKit.csproj`.
  Eigenes `xunit.runner.json` mit `parallelizeAssembly: false` (identisch zum heutigen Wert in
  `src/AiNetLinter.Tests/xunit.runner.json`) — bewusste Vorwegnahme der in Leitplanke 6
  vorgeschriebenen getrennten Runner-Politik pro Assembly, auch wenn die feineren
  Parallelitätsbudgets erst in einem späteren Step folgen.
- **Warum:** Zielprojekt für die teuren Ebenen aus Konzept §1; Runner-Trennung ab dem ersten Tag
  verhindert, dass später niemand merkt, welche Assembly welche Runner-Datei tatsächlich nutzt.

### Datei 4: `src/AiNetLinter.TestKit/AiNetLinter.TestKit.csproj` (neu)

- **Was:** Neues SDK-Class-Library-Projekt (`Microsoft.NET.Sdk`, kein `OutputType Exe`), importiert
  `../../tests/AiNetLinter.TestProject.props`, referenziert `../AiNetLinter/AiNetLinter.csproj`.
  **Keine** xUnit-Paketreferenz (Leitplanke 11 / Konzept-Korrektur 11: „Das TestKit erhält nicht
  allein zur Erkennung eine fachlich unnötige xUnit-Abhängigkeit"). Kein `.cs`-Inhalt in diesem
  Step — das Projekt bleibt bewusst leer, bis EPIC-2 die ersten Builder/Fixtures liefert; ein
  leeres SDK-Projekt baut valide.
- **Warum:** Arbeitsfähige Hülle für die künftige gemeinsame Testplattform (Konzept §2), ohne
  vorzeitig Inhalt vorwegzunehmen, den EPIC-2 erst plant.

### Datei 5: `AiNetLinter.slnx` (Zeile 1-6)

- **Was:** Die drei neuen `.csproj`-Dateien unter demselben `/src/`-Ordner-Knoten wie die
  bestehenden zwei Projekte ergänzen.
- **Warum:** Ohne Solution-Eintrag baut/testet `dotnet build`/`dotnet test` auf Solution-Ebene die
  neuen Projekte nicht mit — und laut Leitplanke 0 verändert genau dieser Eintrag auch die
  Eingabe von Dogfood/Selbstlint/`StaticTestSentinel` (bewusst, siehe Notes).

### Datei 6: `rules.json` (Abschnitt `ProjectOverrides`, Zeile 393-397+)

- **Was:** Den bestehenden Schlüssel `"*.Tests"` durch `"*Tests"` ersetzen (ohne führenden Punkt —
  matcht dann `AiNetLinter.Tests`, `AiNetLinter.FastTests` und `AiNetLinter.IntegrationTests`
  gleichermaßen über `ProjectConfigResolver.IsMatch`). Zusätzlich einen zweiten Schlüssel
  `"AiNetLinter.TestKit"` mit demselben Override-Inhalt (`Global.EnforceSealedClasses: false`,
  `Metrics.MaxMethodLineCount: 100`, unverändertem `MaxPublicMembersPerTypeExemptSuffixes`-Block)
  ergänzen, da `TestKit` nicht auf `Tests` endet und `*Tests` es nicht erfasst.
- **Warum:** Ohne diese Anpassung gelten für den neuen Testcode laut Leitplanke 0 die vollen
  Produktionsregeln (`EnforceSealedClasses`, `MaxMethodLineCount` 60 statt 100 etc.) und der
  Selbstlint wird ohne fachlichen Grund rot, sobald die neuen Projekte in der Solution liegen.

### Datei 7: `rules.json` (Abschnitt `TestSentinel.TestProjectNameSuffixes`, Zeile 291-297)

- **Was:** `"TestKit"` als weiteren Eintrag in die Suffix-Liste aufnehmen
  (`Tests, Test, IntegrationTests, Specs, Spec, TestKit`).
- **Warum:** `TestProjectDetector.HasTestProjectNameSuffix` erkennt `AiNetLinter.TestKit` sonst
  nicht als Testprojekt — mit Konsequenzen für `StaticTestSentinel`s Abdeckungsindex, sobald das
  TestKit eigene Typen bekommt.

### Datei 8: `src/AiNetLinter.FastTests/Configuration/ProjectOverrideResolutionTests.cs` (neu)

- **Was:** Neue Unit-Testklasse (`[Trait("Category", "Unit")]`), die `ConfigLoader`/
  `ProjectConfigResolver.ResolveForProject(...)` für die drei Projektnamen `AiNetLinter.FastTests`,
  `AiNetLinter.IntegrationTests`, `AiNetLinter.TestKit` gegen die tatsächlich aus `rules.json`
  geladene globale Config aufruft und `Global.EnforceSealedClasses == false` sowie
  `Metrics.MaxMethodLineCount == 100` erwartet (Theory mit den drei Namen als `InlineData`). Vorbild
  für das Laden von `rules.json` als `Config`-Objekt: bestehender Ladepfad in
  `src/AiNetLinter/Configuration/` (z. B. `ConfigLoader`/`RulesJsonLoader` — im Coding-Step exakt
  gegen den vorhandenen Lademechanismus verifizieren, nicht neu erraten).
- **Warum:** Erfüllt direkt die Konzept-DoD-Zeile „Die neuen Testprojekte lösen nachweislich auf
  den Test-`ProjectOverride` auf … ein Test belegt beides" (erster Teil).

### Datei 9: `src/AiNetLinter.FastTests/Core/TestProjectDetectorSuffixTests.cs` (neu)

- **Was:** Neue Unit-/Component-Testklasse (`[Trait("Category", "Component")]`, da ein
  `AdhocWorkspace` zum Erzeugen eines benannten `Project`-Objekts nötig ist — kein Solution-Load
  von Platte, kein `SourceFileCatalog.LoadAsync`, damit laut Konzept-Tabelle §1 zulässig auf
  Component-Ebene). Erzeugt je einen `Project` mit den Namen `AiNetLinter.FastTests`,
  `AiNetLinter.IntegrationTests`, `AiNetLinter.TestKit` (ohne xUnit-Metadatenreferenz, damit
  ausschließlich der Namens-Suffix-Fallback greift) und ruft `TestProjectDetector.IsTestProject`
  mit der aus `rules.json` geladenen `TestProjectNameSuffixes`-Liste auf; erwartet `true` für alle
  drei.
- **Warum:** Erfüllt den zweiten Teil derselben DoD-Zeile („… und werden von
  `TestProjectDetector` als Testprojekte erkannt; ein Test belegt beides").

### Datei 10: `src/AiNetLinter.IntegrationTests/Configuration/ProjectOverrideRealSolutionTests.cs` (neu)

- **Was:** Ein einzelner Integrationstest (`[Trait("Category", "Integration")]`), der die echte
  `AiNetLinter.slnx` einmal via `SourceFileCatalog.LoadAsync` lädt (Muster aus einem bestehenden
  Integrationstest wie `src/AiNetLinter.Tests/Cli/CliIntegrationTests.cs` übernehmen) und für die
  drei neuen Projektnamen sowohl `ProjectConfigResolver.ResolveForProject` als auch
  `TestProjectDetector.IsTestProject` (jetzt mit echten Metadatenreferenzen aus der geladenen
  Solution, nicht nur Namens-Fallback) prüft.
- **Warum:** Component-Tests mit synthetischem `AdhocWorkspace`-Projekt beweisen nur den
  Namens-Fallback-Pfad; ein echter Solution-Load ist der einzige Nachweis, dass die drei neuen
  Projekte auch mit ihren tatsächlichen Metadatenreferenzen (xUnit-Pakete) korrekt erkannt werden
  und dieselbe Fidelity zwischen In-Memory- und MSBuild-Welt gilt, die Leitplanke 4 später generell
  verlangt.

## Tests

- [ ] `dotnet build` (volle Solution) — muss grün sein, inklusive der drei neuen Projekte.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~ProjectOverrideResolutionTests|FullyQualifiedName~TestProjectDetectorSuffixTests` — beide neuen Proof-Tests grün.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~ProjectOverrideRealSolutionTests` — neuer Integrationstest grün.
- [ ] Repräsentativer Legacy-Konsument gezielt mitlaufen lassen, um sicherzustellen, dass die
  `rules.json`-Änderung das bestehende `*.Tests`-Verhalten nicht bricht:
  `dotnet test --filter FullyQualifiedName~ArchitectureTests` (bestehende Klasse in
  `src/AiNetLinter.Tests/Architecture/ArchitectureTests.cs`, prüft `LinterAnalyzer`-Regeln inkl.
  Test-Override-Pfad).

Kein Volllauf (`Category!=Stress`) in diesem Step — Konzept §7 „Sparsame Verifikation": nur die
direkt betroffenen neuen Tests plus ein gezielter Legacy-Repräsentant.

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün
- [ ] Test-Command aus Tech-Stack-Notiz grün (gezielt, siehe „Tests" oben — kein Volllauf)
- [ ] Commit auf aktuellem Branch (Conventional Commit, Deutsch, `[speedup-tests]`-Suffix)
- [ ] `step-001/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §3 (Windows/PowerShell-Tooling, `dotnet test`/TRX-
  Diagnose) — Build-/Testkommandos in diesem Step laufen unter PowerShell 7, TRX-Diagnose bei
  Fehlern über `TestResults/latest.trx`.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 (Testsuite-Parallelität bewahren) — die neuen
  Testklassen werden **nicht** in eine zwangsserialisierende Collection gepackt; nur die neue
  `AiNetLinter.IntegrationTests`-Assembly selbst bekommt `parallelizeAssembly: false` (Assembly-
  Ebene, kein Collection-Zwang für einzelne Klassen).
- `.agents/rules/AiNetLinter.mdc` (Projekt-Overrides `*.Tests`) — wird durch diesen Step selbst auf
  `*Tests`/`AiNetLinter.TestKit` erweitert; die generierte `.mdc`-Datei synchronisiert sich beim
  nächsten `AiNetLinter --sync-agent-rules`-Lauf automatisch aus `rules.json`, wird in diesem Step
  nicht von Hand angefasst.

## Bekannte Ausnahmen

- Keine.

## Notes

- **Leitplanke 0 bewusst nur teilweise geschlossen:** Dieser Step schließt die
  `ProjectOverrides`/`TestProjectNameSuffixes`-Lücke, **nicht** `InternalsVisibleTo` (noch kein
  Bedarf, siehe „Aktueller Projektzustand") und **nicht** das Dogfood-/Selbstlint-Kostenwachstum
  durch fünf statt zwei Solution-Projekte — Letzteres wird laut Konzept getrennt vorher/nachher
  gemessen (Baseline-Step folgt) und ist kein Fehlschlagskriterium für diesen Step.
- **`AiNetLinter.Tests.csproj` bleibt unangetastet.** Die neue `TestProject.props` wird nicht
  rückwirkend vom Legacy-Projekt importiert — das würde das Strangler-Prinzip verletzen (Altprojekt
  ist Migrationsquelle, keine Zielarchitektur) und unnötiges Risiko für die laufende Legacy-Suite
  erzeugen. Die Duplikation zwischen Legacy-`.csproj` und neuer `TestProject.props` ist für die
  Dauer der Migration bewusst in Kauf genommen.
- **Kein Architekturguard in diesem Step.** Die Deny-Liste gegen `Microsoft.Build.*`/Prozessstarts
  in `AiNetLinter.FastTests` (Konzept §6 „Was die Guards wirklich können") ist ausdrücklich Teil
  eines Folge-Steps, nicht dieses Fundament-Teilschritts — in diesem Step referenziert
  `AiNetLinter.FastTests` noch keine verbotene Infrastruktur, es gibt also (noch) nichts zu
  bewachen.
- **`ConfigLoader`-Ladepfad vor dem Schreiben von Datei 8 exakt im Code verifizieren** — der Planer
  hat den genauen Klassennamen/Methode zum Laden von `rules.json` in eine `Config`-Instanz nicht
  abschließend nachgelesen; im Coding-Step kurz in `src/AiNetLinter/Configuration/` nachsehen
  (`rg`/MCP `find_symbol` statt Raten), damit der Test denselben Pfad nutzt wie die Produktion.
