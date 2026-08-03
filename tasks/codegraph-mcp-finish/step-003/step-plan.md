---
status: done
type: step-plan
task: codegraph-mcp-finish
step: 003
title: "Testsuite-Performance — Core/-Testordner sub-gliedern + MaxDirectoryChildren aktivieren (F.3)"
epic: EPIC-01
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-03
related_to: ["step-002"]
---

# Step 003: Core/-Testordner sub-gliedern + MaxDirectoryChildren aktivieren (F.3)

## Bezug

- **Task:** `codegraph-mcp-finish`
- **Epic:** `EPIC-01` aus `roadmap.md` — Testsuite-Performance (Block F).
  F.1 (step-001) und F.2 (step-002) sind approved. F.3 ist der nächste
  offene Teilpunkt, F.4–F.6 bleiben für spätere Steps offen.
- **Konzept-Referenz:** `Konzept.md` Muss-Haben F, Punkt 3: „`Core/`-Testordner
  sub-gliedern, danach `MaxDirectoryChildren` aktivieren." Non-Goal:
  „Keine Änderung an Testinhalten/Assertions" (reines
  Datei-Organisations-Refactoring, kein Verhaltens- oder Assert-Änderung).

## Aktueller Projektzustand (JIT-Kontext)

- `src/AiNetLinter.Tests/Core/` enthält aktuell **49 Test-Dateien**, davon
  **42 direkt im Wurzelverzeichnis** und **7 bereits in einem
  Unterordner `Core/Checkers/`** (`MaxBoolParameterCountTests.cs`,
  `MaxConstructorDependenciesTests.cs`, `MaxInheritanceDepthTests.cs`,
  `MaxPublicMembersPerTypeTests.cs`, `MaxSwitchArmsTests.cs`,
  `NamespaceDirectoryMappingTests.cs`, `NestedTypesCheckerTests.cs`).
  **Wichtig:** Dieser Unterordner existiert bereits — eine frühere
  Bearbeitung hat mit der Sub-Gliederung schon begonnen. Dieser Step
  **erweitert** `Core/Checkers/` um weitere Checker-Tests, statt eine
  zweite, konkurrierende Struktur daneben aufzubauen (siehe „Konkrete
  Änderungen" unten).
- Das Testprojekt hat bereits eine etablierte themenbasierte
  Ordnerstruktur auf oberster Ebene (`Baseline/`, `Cli/`, `Commands/`,
  `Fixtures/`, `Mcp/`, **`Metrics/`**, `Suppression/`, `Web/`, `Cache/`,
  `Architecture/`, `Configuration/`, `Diagnostics/`, `Evals/`,
  `FalsePositives/`, `Maps/`, `Output/`). Besonders relevant:
  **`src/AiNetLinter.Tests/Metrics/`** existiert bereits (4 Dateien:
  `CognitiveComplexityGuidanceTests.cs`, `CognitiveComplexityWalkerTests.cs`,
  `MaxDirectoryChildrenTests.cs`, `MethodLineCounterTests.cs`) — das ist
  der vorgesehene Ort für Metrik-/Grenzwert-Infrastruktur-Tests, nicht
  `Core/`. Drei Dateien aus `Core/` gehören inhaltlich dorthin (siehe
  unten) — sie werden in dieses **bestehende** Verzeichnis verschoben,
  nicht in ein neues `Core/Metrics/` dupliziert.
- `AiNetLinter.mdc` (Regel-Index, siehe Rules-Refs) gruppiert die
  aktiven Checker-Regeln in vier Kategorien: `agent-resilience`,
  `architecture`, `test-coverage`, `general` — plus eine separate
  Metriken-Tabelle (`agent-context`-Intent im `RuleRegistry.cs`,
  z. B. `MaxLineCount`, `MaxMethodParameterCount`, `AIContextFootprint`,
  `MaxDirectoryChildren` selbst). Der bereits bestehende
  `Core/Checkers/`-Ordner mischt beide (z. B. `MaxBoolParameterCountTests`
  ist eine Metrik, `NamespaceDirectoryMappingTests` eine
  `architecture`-Regel) — das ist die bereits etablierte Konvention
  (ein Ordner pro „testet genau eine Checker-/Regel-Klasse 1:1", nicht
  nach Intent-Kategorie getrennt) und wird in diesem Step **unverändert
  fortgeführt**, nicht neu aufgeteilt.
- `EnforceNamespaceDirectoryMapping` (Regel, aktiv, siehe
  `AiNetLinter.mdc`) erzwingt, dass der Namespace einer Datei ihrem
  Verzeichnispfad entspricht — jede verschobene Datei **muss** ihre
  `namespace`-Zeile entsprechend anpassen, sonst schlägt der
  Build/Self-Lint fehl.
- `MaxDirectoryChildren` ist projektweit auf `0` (deaktiviert,
  `rules.json:161`) — die Check-Logik selbst
  (`PostAnalysisChecks.RunMaxDirectoryChildrenCheck`,
  `src/AiNetLinter/Core/PostAnalysisChecks.cs:291-334`) existiert bereits
  vollständig und ist bereits gegen `MaxDirectoryChildrenTests.cs`
  getestet — dieser Step aktiviert nur den Schwellwert, baut keine neue
  Logik.
- **Verzeichnis-Sweep vor der Grenzwert-Wahl (wichtig, um keine neue,
  außerhalb des Scopes liegende Verletzung zu erzeugen):**
  `MaxDirectoryChildren` gilt projektweit, nicht nur für Tests. Aktuelle
  Kind-Zahlen der größten Verzeichnisse (ermittelt per
  `Get-ChildItem`, ohne `bin`/`obj`):
  `src/AiNetLinter.Tests/Core` = 43 (wird durch diesen Step aufgelöst),
  **`src/AiNetLinter/Core/Checkers` = 28** (Produktionscode, außerhalb
  des Scopes von Block F — Non-Goal „keine Änderung an Testinhalten"
  schließt implizit auch keine ungeplante Produktionscode-Umgliederung
  ein), `src/AiNetLinter.Tests` = 23, `src/AiNetLinter/Core` = 21,
  `src/AiNetLinter` = 21, `src/AiNetLinter/Mcp/Tools` = 17,
  `src/AiNetLinter/Configuration` = 15, `src/AiNetLinter.Tests/Commands` = 15.
  Ein Grenzwert **unter 28** würde sofort `src/AiNetLinter/Core/Checkers`
  verletzen — eine Datei außerhalb des Scopes dieses Steps, deren
  Aufteilung nicht Teil von Block F ist. Deshalb: Grenzwert **30**
  (oberes Ende der in `Docs/ROADMAP.md`/`Docs/configuration.md`
  dokumentierten Empfehlung „20–30 für Mittelklasse-Projekte" — kein
  neuer Wert, keine Abweichung von der bereits dokumentierten Spanne).
  Die bestehende `MaxDirectoryChildrenExemptNames`-Liste (`Migrations`,
  `Generated`, `wwwroot`, `obj`, `bin`, `.git`) deckt bereits alle
  Build-Output-/Fremdcode-Verzeichnisse ab — keine Ergänzung nötig,
  sofern der Sweep unten keine weitere Überraschung zeigt.

## Intention

Der 49-Datei-Flachordner `Core/` erschwert Navigation/Agent-Discovery
(größter Einzelordner im Testprojekt) und verhindert, dass die
`MaxDirectoryChildren`-Regel projektweit sinnvoll aktiviert werden kann
(Konzept-Vorgabe: Sub-Gliederung **vor** Aktivierung, damit der Selbst-Lint
nicht sofort auf sich selbst schlägt). Nach diesem Step: `Core/` ist in
themenbasierte Unterordner gegliedert (bestehendes `Checkers/` erweitert,
bestehendes `Metrics/` erweitert, Rest bleibt als Engine-/Infrastruktur-Tests
in `Core/`), jede Datei behält exakt ihren Testinhalt (keine Assertion-
Änderung, reine Datei-/Namespace-Verschiebung), und `MaxDirectoryChildren`
ist mit Grenzwert 30 aktiv — verifiziert gegen einen frischen Lint-/Testlauf,
der keine neuen Verstöße außerhalb des Scopes dieses Steps zeigt.

## Konkrete Änderungen

### Verschiebung A: 20 Dateien nach `src/AiNetLinter.Tests/Core/Checkers/` (bestehender Ordner, wird erweitert)

Jede Datei: **verschieben** (nicht kopieren) von `Core/<Datei>.cs` nach
`Core/Checkers/<Datei>.cs`, `namespace AiNetLinter.Tests.Core;` →
`namespace AiNetLinter.Tests.Core.Checkers;`, keine sonstige Änderung am
Dateiinhalt (Testkörper, Assertions, `using`-Direktiven unverändert außer
falls der `namespace`-Wechsel eine `using AiNetLinter.Tests.Core;` in einer
anderen Datei nötig macht — prüfen, ob eine der bewegten Klassen von einer
anderen Testdatei referenziert wird, bevor verschoben wird).

Betroffene Dateien (Auswahlkriterium: testet 1:1 eine einzelne
Checker-/Regel-Implementierungsklasse, analog zu den bereits in
`Checkers/` liegenden 7 Dateien):

- `AsciiIdentifiersTests.cs` (testet `EnforceAsciiIdentifiers`)
- `AsyncVoidCheckerTests.cs` (`BanAsyncVoid`)
- `BlockingTaskCheckerTests.cs` (`BanBlockingTaskAccess`)
- `CouplingSemanticTests.cs`
- `DynamicTypeCheckerTests.cs`
- `LinqChainLengthCheckerTests.cs` (`MaxLinqChainLength`)
- `MethodParameterCountAccessibilityTests.cs`
- `MethodParameterCountIgnoreTypePrefixesTests.cs`
- `MethodParameterCountOverrideTests.cs`
- `MiddleManCheckerTests.cs` (`AvoidExcessiveMiddleMen`)
- `NamespaceCouplingCheckerTests.cs`
- `NamingCheckerTests.cs` (`EnforcePascalCase`/`EnforceSemanticNaming`)
- `PhantomDependencyCheckerTests.cs` (`DetectAndBanPhantomDependencies`)
- `SealedClassCheckerTests.cs` (`EnforceSealedClasses`)
- `SilentCatchAllowedTypesTests.cs` (`EnforceNoSilentCatch`-Ausnahmen)
- `SwitchDispatcherDetectorTests.cs`
- `UiFileSeparationCheckerTests.cs`
- `ValueObjectCheckerTests.cs` (`EnforceValueObjectContracts`)
- `WpfCodeBehindTests.cs`
- `MaxPartialClassFilesTests.cs` (`MaxPartialClassFiles`)

### Verschiebung B: 3 Dateien nach `src/AiNetLinter.Tests/Metrics/` (bestehender Ordner, wird erweitert)

Analog zu A, aber Zielnamespace `AiNetLinter.Tests.Metrics` (bereits
etablierter Namespace der 4 vorhandenen Dateien dort — vor dem
Verschieben eine der bestehenden Dateien öffnen und den exakten
Namespace übernehmen).

- `AIContextFootprintDeduplicationTests.cs`
- `FileLimitGuidanceTests.cs`
- `PostAnalysisChecksPathOverrideTests.cs`

### Verbleibend in `src/AiNetLinter.Tests/Core/` (19 Dateien, keine Verschiebung, kein Namespace-Wechsel)

Reine Engine-/Infrastruktur-Tests ohne 1:1-Bezug zu einer einzelnen
Checker-Klasse — bleiben unverändert liegen:
`ClassInfoCollectorTests.cs`, `CompoundSuppressionEvaluatorTests.cs`,
`CompoundSuppressionIntegrationTests.cs`, `ControlFlowResilienceTests.cs`,
`NamespaceFilterTests.cs`, `NullCoalescingInitializerClassifierTests.cs`,
`PlaybookGeneratorRound2Tests.cs`, `ResultPatternNamespaceTests.cs`,
`RuleRegistryTests.cs`, `ScopeImmutabilityTests.cs`,
`StaticTestSentinelExemptionTests.cs`, `TestCoverageResolverTests.cs`,
`TestProjectDetectorSuffixTests.cs`, `ViolationDescriptionTests.cs`,
`AutoFixerTests.cs`, `DiffImpactAnalyzerTests.cs`, `LinterAnalyzerTests.cs`,
`LinterEngineCacheTests.cs`, `LinterEngineTests.cs`.

**Ergebnis-Zielbild:** `Core/` = 19 Dateien, `Core/Checkers/` = 27 Dateien,
`Metrics/` = 7 Dateien — alle drei unter dem neuen Grenzwert 30.

**Hinweis zur Zuordnung oben:** Die Kategorisierung basiert auf
Dateiname/Testklassen-Konvention, nicht auf vollständigem Lesen jeder
einzelnen Datei. Bei einzelnen der oben ambigen Fälle (insbesondere
`CouplingSemanticTests`, `SwitchDispatcherDetectorTests`,
`NullCoalescingInitializerClassifierTests`, `ControlFlowResilienceTests`,
`StaticTestSentinelExemptionTests`): beim Öffnen der Datei kurz
verifizieren, welche Produktionsklasse tatsächlich getestet wird (per
`using`-Zeilen / getesteter Typ). Weicht der tatsächliche Testinhalt von
der Einordnung oben ab (z. B. eine Datei testet doch keine 1:1-Checker-
Klasse, sondern Engine-Infrastruktur), die Datei stattdessen gemäß dem
oben beschriebenen Kriterium einordnen und die Abweichung kurz unter
„Abweichungen vom Plan" im `step-result.md` vermerken — das ist
Verifikation der Plan-Prämisse, keine eigenständige Architekturentscheidung.

### Datei: `rules.json` (Zeile ~161)

- **Was:** `"MaxDirectoryChildren": 0` → `"MaxDirectoryChildren": 30`.
  `MaxDirectoryChildrenExemptNames` unverändert lassen, **außer** ein
  frischer Sweep (siehe Tests unten) zeigt ein weiteres Verzeichnis
  außerhalb des Scopes dieses Steps mit mehr als 30 Einträgen — dann
  dieses Verzeichnis statt einer erneuten Grenzwert-Anhebung gezielt in
  die Exempt-Liste aufnehmen (mit Begründung im Commit) oder, falls das
  passiert, den Fund unter „Abweichungen vom Plan" dokumentieren statt
  eigenmächtig eine Produktionscode-Umgliederung vorzunehmen.
- **Warum:** Konzept-Vorgabe („im Anschluss an die Sub-Gliederung
  aktivieren"), Grenzwert 30 vermeidet eine sofortige, außerhalb des
  Scopes liegende Verletzung in `src/AiNetLinter/Core/Checkers` (28
  Einträge) — siehe „Aktueller Projektzustand".

### Datei: `.agents/rules/AiNetLinter.mdc` (auto-generiert)

- **Was:** Nach der `rules.json`-Änderung
  `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`
  ausführen, damit die Regeldatei den neuen `MaxDirectoryChildren`-Wert
  (`30` statt `0`) widerspiegelt (Tabellen-Zeile in „Grenzwerte
  (Produktion)").
- **Warum:** Tech-Stack-Notiz (`roadmap.md`) — Regeldatei wird
  automatisch aus `rules.json` synchronisiert, keine manuelle Bearbeitung.

### Datei: `Docs/configuration.md` (Zeile ~107, `MaxDirectoryChildren`-Beispielwert)

- **Was:** Prüfen, ob dort der alte Default `0` als Beispiel für den
  aktuellen Projektwert (nicht nur als generisches Beispiel für einen
  deaktivierten Zustand) dargestellt ist; falls ja, auf den neuen
  aktiven Wert `30` bzw. einen Hinweis „im eigenen Projekt aktiv"
  aktualisieren. Falls die Stelle nur ein generisches Konfigurations-
  Beispiel ist (Default-Doku für Nutzer, nicht Live-Wert dieses
  Projekts): unverändert lassen, kurz im `step-result.md` vermerken
  warum keine Änderung nötig war.
- **Warum:** `AiNetLinterRichtlinien.mdc` §4 „Update-Pflicht" — Doku bei
  Konfig-Änderungen ohne Aufforderung mitziehen.

## Tests

- [ ] `dotnet build AiNetLinter.slnx` — grün, 0 Warnungen (verifiziert
      insbesondere, dass keine der 23 verschobenen Dateien einen
      `EnforceNamespaceDirectoryMapping`-Verstoß erzeugt).
- [ ] `dotnet test --filter Category=Unit` — grün, gleiche Testanzahl wie
      vor dem Step (reine Verschiebung, keine Testinhalte geändert).
- [ ] `dotnet test AiNetLinter.slnx --no-build` (Volllauf) — grün, **und**
      `TestResults/latest.trx`/Konsolen-Output auf neue
      `MaxDirectoryChildren`-Verstöße prüfen (Selbst-Lint des eigenen
      Repos läuft im Rahmen der bestehenden Testsuite mit, z. B.
      `McpLiveRepositoryTests`/`RuleRegistryTests` o. ä. — falls dabei ein
      unerwarteter Verzeichnis-Treffer auftaucht, siehe Hinweis zu
      `MaxDirectoryChildrenExemptNames` oben).
- [ ] Manueller Sweep: `Get-ChildItem` (ohne `bin`/`obj`) auf alle
      Projektverzeichnisse mit >30 Einträgen nach der Umstellung — muss
      leer sein (bzw. jeder verbleibende Treffer ist ein bereits vor
      diesem Step bekanntes, exemptiertes Verzeichnis).
- [ ] Vor jedem Build/Test: offene `AiNetLinter.exe`/`testhost.exe`-Prozesse
      prüfen und ggf. beenden (Tech-Stack-Notiz, bekannte
      Datei-Sperren-Falle).

## Definition of Done

- [ ] Alle 23 Dateien verschoben (20 → `Core/Checkers/`, 3 → `Metrics/`),
      Namespace jeweils angepasst, kein Inhalt/keine Assertion geändert.
- [ ] `rules.json`: `MaxDirectoryChildren` = 30, `AiNetLinter.mdc`
      neu synchronisiert.
- [ ] `Docs/configuration.md` geprüft/ggf. aktualisiert.
- [ ] Build-Command aus Tech-Stack-Notiz grün, 0 Warnungen.
- [ ] Test-Command aus Tech-Stack-Notiz grün (Unit + Volllauf).
- [ ] Kein neuer `MaxDirectoryChildren`-Verstoß außerhalb bereits
      bekannter/exemptierter Verzeichnisse.
- [ ] Commit auf aktuellem Branch (Conventional Commit, Suffix
      `[codegraph-mcp-finish]`).
- [ ] `step-003/step-result.md` geschrieben.
- [ ] `status` in `step-plan.md` von `in_progress` auf
      `done (pending audit)` gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — `EnforceNamespaceDirectoryMapping`
  (Abschnitt „architecture": Namespace muss Verzeichnispfad entsprechen —
  jede Verschiebung braucht die passende `namespace`-Zeile),
  `MaxDirectoryChildren`-Zeile in „Grenzwerte (Produktion)" (wird durch
  diesen Step von `0` auf `30` geändert und muss nach dem
  `--sync-agent-rules-only`-Lauf den neuen Wert zeigen).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 „Update-Pflicht" —
  `Docs/configuration.md`/`rules.json` bei Konfig-Änderungen ohne
  Aufforderung mitziehen; §5 Kommentar-Konventionen (falls beim
  Verschieben ein XML-Doc-Kommentar berührt wird: keine
  Task-/Planungsartefakt-Referenzen wie `step-003`/`F.3` im Code selbst).

## Bekannte Ausnahmen

- Keine bekannten flaky Tests in diesem Step-Scope.

## Notes

- **Bestehende Struktur wiederverwenden, nicht duplizieren** (Kern des
  JIT-Ansatzes, siehe „Aktueller Projektzustand"): `Core/Checkers/` und
  `Metrics/` existieren bereits — dieser Step füllt sie auf, baut keine
  Parallelstruktur wie `Core/Checkers2/` oder `Core/Metrics/` daneben.
- **Bewusst keine weitere Unterteilung von `Checkers/` nach
  `agent-resilience`/`architecture`/`general`** (obwohl `Konzept.md`
  diese Kategorien als Vorbild nennt): Der bereits etablierte,
  vorgefundene Ordner-Zuschnitt („ein Ordner pro 1:1-Checker-Test",
  ohne weitere Intent-Unterteilung) wird fortgeführt, statt eine zweite,
  konkurrierende Taxonomie einzuführen. 27 Dateien in `Checkers/` bleiben
  unter dem neuen Grenzwert 30 — sollte sich beim Umsetzen zeigen, dass
  die tatsächliche Zahl (z. B. durch die Verifikation der ambigen Fälle
  oben) doch über 30 steigt, ist eine weitere Unterteilung nach
  Intent-Kategorie die vorgesehene Rückfalloption (kurz im
  `step-result.md` begründen).
- **Kein Fix für `src/AiNetLinter/Core/Checkers` (28 Einträge,
  Produktionscode).** Das ist eine bewusste Scope-Grenze: Block F ist
  laut `Konzept.md`-Non-Goal reines Testsuite-Refactoring, keine
  Produktionscode-Umgliederung. Der Grenzwert 30 ist explizit so
  gewählt, dass dieses Verzeichnis **nicht** neu als Verstoß auftaucht.
  Sollte ein künftiger Step (außerhalb dieses Tasks oder in Muss-Haben
  B/C, die ohnehin `Mcp/`-Dateien anfassen) diesen Ordner weiter
  wachsen lassen, ist das kein Gegenstand dieses Steps.
- **`baselineAfter`-Tote-Variable (TD-002) ist nicht Teil dieses Steps** —
  bleibt laut Nutzer-Entscheidung offen, keine Verschiebung von
  `WebBaselineTests.cs` in diesem Step (liegt ohnehin in `Baseline/`,
  nicht `Core/`).
- F.4 (Test-Data-Builder), F.5 (`#nullable enable`-Randmitnahme), F.6
  (Laufzeitmessung dokumentieren) bleiben für Folge-Steps offen — F.6
  insbesondere braucht einen Volllauf-Vergleich **nach** F.1-F.5, nicht
  schon hier.
