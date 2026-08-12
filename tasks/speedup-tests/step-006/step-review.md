---
status: done
type: step-review
task: speedup-tests
step: 006
epic: EPIC-2
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-12
verdict: approved
tech_debt_ids: []
---

# Review Step 006: Testplattform-Fundament Teil 1 — RoslynTestSolutionFactory und PreparedSolutionFixture

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Korrektur-Step `step-<MMM>` angelegt (`corrects: step-<NNN>`)
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` (referenzierte Dateien) eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün

## Befund

### Plan-Erfüllung

Alle sechs Dateien (2 neue TestKit-Klassen, Assembly-Fixture-Registrierung, 2 neue
Vertragstestklassen, 1 Migration) exakt wie im Plan spezifiziert umgesetzt; `codemap.md` korrekt
nachgezogen. Der vom Coder berichtete Verzicht auf einen vollen `Category!=Stress`-Lauf ist
**plankonform, kein Finding**: der Step-Plan selbst legt im „Tests"- und „Definition of
Done"-Abschnitt explizit die gefilterten Läufe als Soll-Zustand fest („kein voller
`Category!=Stress`-Lauf für diesen Step … Gezielte Test-Filter oben grün (kein Vollauf nötig laut
Konzept §7)"), gedeckt durch `roadmap.md` Tech-Stack-Notiz Zeile 31-36 („nie der volle
`Category!=Stress`-Lauf pro Step … Voller Lauf … nur … am Task-Ende") und `konzept.md` §7 Zeile
566-569. Das engt die in `coder/SKILL.md` Schritt 5 generisch geforderte „ein vollständiger Lauf
des Test-Commands aus der Tech-Stack-Notiz" bewusst auf den in der Tech-Stack-Notiz selbst
dokumentierten Sparsam-Verifikation-Vertrag ein — kein Widerspruch, da die Tech-Stack-Notiz diese
Einschränkung selbst enthält und der Step-Plan sie explizit übernimmt.

### Rules-Konformität

`AiNetLinterRichtlinien.mdc` §4 „Testsuite-Parallelität bewahren": eingehalten — echte
xUnit-v3-Assembly-Fixture (`[assembly: AssemblyFixture(typeof(PreparedSolutionFixture))]`,
`src/AiNetLinter.FastTests/Platform/PreparedSolutionAssemblyFixture.cs`) statt
zwangsserialisierender Collection. `AiNetLinter.mdc`/`rules.json`-Override für
`AiNetLinter.TestKit`: `#nullable enable` überall gesetzt, `sealed` auf konkreten Typen
(`PreparedSolutionFixture`, Records `ProjectSpec`/`RoslynTestSolution`), `MaxMethodParameterCount`
(4) durch den 7-Parameter-Record `ProjectSpec` nicht verletzt — genau der in `AiNetLinter.mdc`
Zeile 22 vorgesehene Ausweg „Ab Überschreitung: `record` als Parameter-Object", nicht dessen
Verstoß.

### Logische Korrektheit

`RoslynTestSolutionFactory.CoreReferencesLazy` (`Lazy<T>` mit Default-Threading-Modus
`ExecutionAndPublication`) und `PreparedSolutionFixture.scenarios`
(`ConcurrentDictionary<string, Lazy<RoslynTestSolution>>.GetOrAdd`) sind das lehrbuchhaft korrekte
Double-Checked-Lazy-Muster: `GetOrAdd` kann den `valueFactory`-Delegate zwar mehrfach parallel
aufrufen, der hier aber nur ein neues, noch nicht ausgewertetes `Lazy<T>`-Wrapper-Objekt erzeugt
(keine Seiteneffekte) — nur der tatsächliche Gewinner-Wrapper wird gespeichert und von allen
Aufrufern über dessen `.Value` gelesen, wodurch die eigentliche `factory` (Solution-Aufbau)
nachweislich höchstens einmal pro Szenario läuft. Der zugehörige
`PreparedSolutionFixtureTests`-Thread-Safety-Test (16 parallele `Task.Run`-Aufrufe,
`Interlocked.Increment`-Zähler) bestätigt das mechanisch und lief beim eigenen Nachvollzug grün.
Migration von `LinterEngineSolutionAnalysisTests.cs`: reines Infrastruktur-Refactoring, Assertions
und Testklasse unverändert, nur der Solution-Aufbau wurde auf `RoslynTestSolutionFactory` +
`using` umgestellt — keine stillschweigende Verhaltensänderung.

### Konzept-Treue (Ebene 4)

Deckt `konzept.md` §2 vollständig für die in diesem Teil-Step vorgesehenen zwei Bausteine ab:
deklarativer Mehrprojekt-Builder, einmalig gecachter Referenzsatz, lazy pro-Szenario
materialisierende, thread-sichere Assembly-Fixture, write-once-Workspace-Disziplin (dokumentiert in
den XML-Doc-Kommentaren beider Klassen). `MsBuildFixtureHost`/`IsolatedFixtureLease` und
`FilterMini` bleiben wie im Plan explizit als „Bekannte Ausnahmen" begründet ausgeklammert — kein
Scope-Zuwachs, keine Lücke gegenüber einem für diesen Step zugesagten Muss-Haben-Punkt.

### xUnit-v3-API-Verifikation

Coder-Behauptung („`Xunit.AssemblyFixtureAttribute` existiert in 3.2.2 genau wie in der offiziellen
Doku") per Reflektion gegen die tatsächlich wiederhergestellte
`xunit.v3.extensibility.core/3.2.2`-DLL selbst nachvollzogen: `Xunit.AssemblyFixtureAttribute`
(konkrete Attribut-Klasse) sowie `Xunit.v3.IAssemblyFixtureAttribute` (Interface) sind real
vorhanden und werden in `PreparedSolutionAssemblyFixture.cs` korrekt verwendet — kein Fallback
nötig, Behauptung bestätigt.

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx → grün, 0 Warnungen/Fehler, 5 Projekte
dotnet test src/AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~LinterEngineSolutionAnalysisTests|FullyQualifiedName~RoslynTestSolutionFactoryTests|FullyQualifiedName~PreparedSolutionFixtureTests|FullyQualifiedName~FastTestsDependencyGuardTests|FullyQualifiedName~TestCategoryProfileGuardTests" → grün (12 Tests, 0 Fehler)
```

## Sonstige Beobachtungen / MINOR / NITPICK

- `PreparedSolutionFixture.GetOrCreate` liefert bei einer fehlschlagenden `factory` keine eigene,
  szenario-bezogene diagnostische Kontextinformation (konzept.md §2 Zeile 360-363: „der
  Fixture-Aufbau meldet die Ursache selbst diagnostisch (welches Szenario, welcher Baustein)") —
  `Lazy<T>` mit `ExecutionAndPublication` cacht und wirft die Original-Exception unverändert erneut.
  Da die zugrunde liegenden Factory-Exceptions (z. B. `RoslynTestSolutionFactory`s
  `InvalidOperationException` mit Projektnamen) bereits aussagekräftig sind und der Szenarioname dem
  aufrufenden Test ohnehin bekannt ist, bleibt das kosmetisch — kein MAJOR, kein Blocker.
