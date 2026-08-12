---
status: done
type: step-review
task: speedup-tests
step: 002
epic: EPIC-1
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-12
verdict: issues
tech_debt_ids: [TD-001, TD-002, TD-003]
---

# Review Step 002: Migrationsledger, Architekturguards und Baseline-Messung

## Verdict

- [ ] **approved**
- [x] **issues** — Korrektur-Step nötig (kann als flacher, mechanischer Anhang zu step-002 erfolgen)
- [ ] **blocked**

## Geprüft

- [x] Plan-Erfüllung: alle sechs „Konkrete Änderungen" umgesetzt; ein explizites DoD-Kriterium fehlt (siehe Finding 1)
- [x] Rules-Konformität: `AiNetLinterRichtlinien.mdc` §3/§4 eingehalten
- [x] Logische Korrektheit: Guard-/Ledger-Logik durch Codelesen nachvollzogen, korrekt
- [x] Konzept-Treue: passt zu `konzept.md` (Leitplanke 0/6/8/10), keine Non-Goals verletzt
- [x] Build: Coder-Report plausibel, nicht selbst neu ausgeführt (Sandbox blockiert `dotnet`-Aufrufe in dieser Review-Session, s. u.)
- [x] Tests: Coder-Report plausibel + Guard-Logik durch Codelesen/Mutations-Gedankenexperiment nachvollzogen

## Befund

### Plan-Erfüllung

Alle sechs „Konkrete Änderungen" (Ledger, Konsistenzguard, statischer Deny-Guard, Laufzeit-Guard,
zwei Kategorien-/Profilguards, Baseline-Messung) existieren wie im Plan beschrieben und sind im
Commit `cd1c80f` enthalten (verifiziert per `git show --stat`). Fünf der sechs „Tests"-Punkte und
alle DoD-Punkte bis auf einen sind erfüllt bzw. plausibel — offen ist das DoD-Kriterium „Guard
tatsächlich rot bei simulierter Lücke" (siehe Finding 1 unten): weder `step-result.md` noch
`baseline-measurement.md` dokumentieren, dass dieser Nachweis geführt wurde.

Die drei vom Coder selbst als „besonders prüfenswert" markierten Punkte wurden eigenständig
nachvollzogen:

1. **Literal-Kollision mit `FilterCliIntegrationTests`:** verifiziert. `ProjectConfigResolver`
   übersetzt `"*.Tests"` zu `^.*\.Tests$` — `AiNetLinter.IntegrationTests` matcht das nicht (endet
   auf `nTests`, nicht `.Tests`), wird also von der Selbstlint-Ausnahme in
   `FilterCliIntegrationTests.cs` nicht erfasst. Der neue Code in
   `TestMigrationLedgerConsistencyTests.cs:32` setzt den Legacy-Pfad tatsächlich per
   `string.Concat("src/AiNetLinter", ".Tests")` zusammen statt als durchgehendes Literal — Fix
   nachvollzogen und korrekt.
2. **Last-abhängige Flakiness in `McpServerCommandJsonRpcFramingTests`:** verifiziert als
   plausibel dokumentiert (Baseline-Rohdaten in `baseline-measurement.md` zeigen den Fehlschlag
   nur in Lauf 2 von 3, mit nachvollziehbarer Erklärung „isoliert sofort grün"). Zu Recht nicht
   selbst gefixt (Root Cause vor step-002) — als Tech-Debt aufgenommen, siehe TD-001.
3. **`.agents/rules/AiNetLinter.mdc`-Drift zurückgesetzt statt committet:** verifiziert per
   `git log -- .agents/rules/AiNetLinter.mdc` (letzter Commit `57923de`, weder in `cd1c80f` noch in
   `c5d4b10` enthalten) und `git status`/`git diff HEAD` (sauber, keine lokale Abweichung mehr). Die
   Behauptung, dass diese Datei step-002 nicht verändert und lokal korrekt zurückgesetzt wurde,
   stimmt. Der zugrunde liegende Root Cause (Datei seit step-001 nicht neu synchronisiert, zeigt
   noch den alten Override-Schlüssel `*.Tests` statt `*Tests`) ist real und wurde als
   Tech-Debt aufgenommen, siehe TD-003.

### Rules-Konformität

- `AiNetLinterRichtlinien.mdc` §3 (TRX-Diagnose/Logging): eingehalten.
  `baseline-measurement.md` nutzt ausschließlich `--logger "trx;LogFileName=..."`-Overrides pro
  Lauf, der globale `.runsettings`-Default bleibt unangetastet (kein `.runsettings`-Eintrag im
  Commit-Diff von `cd1c80f`/`c5d4b10`).
- `AiNetLinterRichtlinien.mdc` §4 (Testsuite-Parallelität): eingehalten.
  `FastTestsDependencyGuardTests` trägt `[Collection("FastTestsRuntimeDependencyGuard")]` —
  ausschließlich diese Klasse und die Fixture teilen sich die Collection, nicht die gesamte
  `AiNetLinter.FastTests`-Assembly; beide `TestCategoryProfileGuardTests`-Klassen und die
  Ledger-Konsistenztests tragen kein Collection-Attribut, laufen also weiterhin parallel. Die
  Begründung für die eine bewusste Serialisierung steht als XML-Doc-Kommentar an der Fixture-Klasse.

### Logische Korrektheit

- `TestMigrationLedgerConsistencyTests`: alle vier in Leitplanke 8 geforderten Fehlerfälle sind als
  eigene `[Fact]`-Methoden 1:1 abgebildet (fehlender Eintrag via `Except`-Mengendifferenz auf
  Klassennamen; migrated/consolidated mit noch existierender Quelldatei via `File.Exists`;
  migrated/consolidated ohne existierenden neuen Ort; removed-trivial ohne Begründungstext). Die
  Scan-Logik erkennt Testklassen korrekt über `[Fact]`/`[Theory]`-Methoden auf Roslyn-Syntaxebene,
  nicht per Datei-Zählung — deckt sich mit der Vorgabe im JIT-Kontext des Plans.
- `FastTestsDependencyGuardTests`: liest `AssemblyRef`/`TypeRef`/`MemberRef` direkt aus der
  kompilierten PE-Datei via `System.Reflection.Metadata`, deckt alle fünf im Plan genannten
  Deny-Kategorien ab (Microsoft.Build.*, MSBuildWorkspace-Namespace/-Typname,
  `SourceFileCatalog.LoadAsync` als MemberRef, `System.Diagnostics.Process`). Sauber getrennt vom
  Laufzeitcheck in der Fixture, die dieselbe Deny-Liste gegen tatsächlich geladene
  `AppDomain`-Assemblies prüft.
- Beide Kategorien-/Profilguards reflektieren korrekt nur über die eigene Assembly, schließen
  Hilfsklassen ohne `[Fact]`/`[Theory]` korrekt aus (z. B. bleibt
  `FastTestsRuntimeDependencyGuardFixture` unberücksichtigt) und prüfen „genau ein gültiger Trait"
  (nicht 0, nicht >1) wie im Plan gefordert.
- Eigene Verifikation der Ledger-Logik: testweise einen Ledger-Eintrag (`ArchitectureTests`)
  entfernt und wieder zurückgesetzt (`git diff` danach leer). Das eigentliche Ausführen von
  `dotnet test` wurde in dieser Review-Session vom Sandbox-Classifier blockiert (sowohl über Bash
  als auch PowerShell) — kein Projekt-/Code-Problem, sondern eine Tool-Berechtigungsgrenze dieser
  Review-Umgebung. Die Assertion-Logik (`missing = actual.Except(ledger); Assert.True(missing.Count
  == 0, ...)`) ist aber eindeutig: ein entfernter, aber weiterhin real existierender Klassenname
  landet zwingend in `missing`. Hohe Zuversicht durch Codelesen, aber **kein** tatsächlich
  ausgeführter Nachweis — siehe Finding 1.

### Konzept-Treue (Ebene 4)

Kein Non-Goal umgesetzt (keine Testreduktion, kein globales Caching, kein geteilter
`MSBuildWorkspace`). Die im „Notes"-Abschnitt des Plans begründete Scope-Grenze (Legacy-Build-Gate,
Minimum Safety Envelope, `InternalsVisibleTo` bewusst nicht in diesem Step) ist konsistent mit
Leitplanke 8 Punkt 2-vor-3 und wurde in `roadmap.md`/`codemap.md` (Doku-Commit `c5d4b10`)
nachvollziehbar fortgeschrieben. Die Baseline-Messung folgt Leitplanke 10 vollständig: Build von
Testzeit getrennt, Median über drei Läufe, Ausreißer dokumentiert statt entfernt, Kennzahlen ohne
Testanzahl im Nenner, Dogfood explizit als „noch nicht anwendbar" markiert statt stillschweigend
übergangen.

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx (Coder-Report)                                            → grün, 20,47 s, 0 Warnungen/Fehler, 5 Projekte
dotnet test .../TestMigrationLedgerConsistencyTests (Coder-Report)                       → grün (4 Tests)
dotnet test .../FastTestsDependencyGuardTests (Coder-Report)                             → grün (2 Tests)
dotnet test .../TestCategoryProfileGuardTests × 2 (Coder-Report)                         → grün (je 1 Test)
dotnet test --filter Category=Unit / Category!=Stress (Baseline, Coder-Report)           → grün (1 Ausreißer dokumentiert, s. o.)
```

Eigene Ausführung von `dotnet build`/`dotnet test` in dieser Review-Session war durch den
Sandbox-Classifier blockiert (Bash und PowerShell gleichermaßen); Verifikation erfolgte über
Code-/Commit-Lesen statt Neu-Lauf. Kein Hinweis auf eine falsch-grüne Behauptung gefunden.

## Findings

1. `tasks/speedup-tests/step-002/step-result.md` (fehlender Abschnitt) — [MAJOR] [Plan-Erfüllung/DoD]
   Das in `step-plan.md` DoD explizit geforderte Kriterium „Ledger-Konsistenzguard (Datei 2)
   tatsächlich rot, wenn testweise eine Legacy-Klasse ohne Ledger-Eintrag simuliert wird (kurz
   verifizieren, dann zurücksetzen)" ist in `step-result.md` nirgends dokumentiert (weder im
   Abschnitt „Build-/Test-Output" noch „Beobachtungen" noch „Bekannte Unschärfen"). Ohne diesen
   Nachweis ist laut Plan nicht belegt, dass der Guard nicht nur grün ist, weil er nichts prüft.
   **Fix:** In `src/AiNetLinter.IntegrationTests/Migration/test-migration-ledger.md` (bzw. direkt in
   der Arbeitskopie von `tasks/speedup-tests/test-migration-ledger.md`) testweise eine Zeile für
   eine tatsächlich existierende Legacy-Testklasse entfernen (z. B. die `ArchitectureTests`-Zeile),
   `dotnet test src/AiNetLinter.IntegrationTests --filter
   FullyQualifiedName~TestMigrationLedgerConsistencyTests` ausführen und den erwarteten roten
   `AllLegacyTestClasses_HaveLedgerEntry`-Fehlschlag beobachten, die Zeile danach wieder herstellen
   (`git diff` muss danach leer sein) und erneut grün laufen lassen. Ergebnis (rot mit
   Fehlermeldung, dann wieder grün) in `step-result.md` unter „Build-/Test-Output" oder
   „Beobachtungen" in 2-3 Sätzen dokumentieren. Rein mechanischer Nachtrag, keine Code-Änderung an
   Produktions- oder Testcode nötig.

## Tech-Debt-Einträge aus diesem Review

- `TD-001` (siehe `tech-debt.md`) — last-abhängige Flakiness in `McpServerCommandJsonRpcFramingTests`, bereits vor step-002 bestehend.
- `TD-002` (siehe `tech-debt.md`) — Selbstlint-Testglob `ExcludeProjects=["*.Tests"]` deckt `AiNetLinter.FastTests`/`AiNetLinter.IntegrationTests` nicht ab, fragil gegen künftige `"AiNetLinter.Tests"`-Literale in den neuen Projekten.
- `TD-003` (siehe `tech-debt.md`) — `.agents/rules/AiNetLinter.mdc` seit step-001 nicht neu synchronisiert, zeigt veralteten `ProjectOverrides`-Schlüssel.
