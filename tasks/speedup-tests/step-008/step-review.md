---
status: done
type: step-review
task: speedup-tests
step: 008
epic: EPIC-2
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-12
verdict: issues
tech_debt_ids: [TD-005]
---

# Review Step 008: Testplattform-Fundament Teil 3 — FilterMini-Fixture (Disk + In-Memory-Spec + Fidelity-Test)

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Korrektur-Step `step-009` angelegt (`corrects: step-008`)
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (oder Baselines beachtet)

## Befund

### Plan-Erfüllung

Alle neun Dateien/Ordner aus „Konkrete Änderungen" wurden angelegt und entsprechen strukturell dem
Plan (Datei 1-7, 9 vollständig erfüllt). Datei 8 (`FilterMiniFidelityTests.cs`) ist **teilweise**
erfüllt: der strukturelle Formvergleich und die Verhaltensparität sind vorhanden, aber Punkt 3 des
Plans verlangt explizit, dass `TestProjectDetector.IsTestProject(...)` für das Produktionsprojekt
`FilterMini` „in beiden Welten übereinstimmend" `false` liefert. Der Coder hat diese eine Teil-
Assertion nicht wie geplant umgesetzt, sondern den Test so geändert, dass er das tatsächliche
(abweichende) In-Memory-Verhalten (`true`) als erwartet bestätigt — siehe Finding 1.

Die eigenständige Beurteilung der drei im Auftrag gestellten Fragen:

1. **Plattform-Scope-Problem aus step-006 oder step-008-Fehler?** Verifiziert per Lektüre von
   `src/AiNetLinter.TestKit/RoslynTestSolutionFactory.cs`: `CoreReferences` wird einmalig statisch
   aus `AppDomain.CurrentDomain.GetAssemblies()` gebaut und in `AddProject` **ungefiltert** an jedes
   Projekt gehängt; `ProjectSpec` bietet nur `AdditionalReferences` zum Hinzufügen, keinen Mechanismus
   zum Ausschließen der Kernreferenzen für ein einzelnes Projekt. `FilterMiniSolutionSpec` hätte diese
   Kontamination **nicht** durch eine andere (z. B. projektspezifische) Formulierung der Spec
   vermeiden können — das ist echtes step-006-Factory-Design, außerhalb des step-008-Scopes. Die
   Einschätzung des Coders ist zutreffend.
2. **Untergräbt die Anpassung den Sinn des Fidelity-Tests?** Ja, in dem einen Punkt. `konzept.md`
   Zeile 438-440 begründet den strukturellen Formvergleich explizit damit, dass er eine Formabweichung
   „früh und mit präziser Diagnose" auffangen soll. Hier liegt eine reale, bestätigte Formabweichung
   vor (Disk-`FilterMini`: `false`, In-Memory-`FilterMini`: `true`) — der Test wurde jedoch so
   angepasst, dass er diese Abweichung nicht mehr anzeigt, sondern das abweichende Verhalten als
   korrekt bestätigt (`Assert.True` statt `Assert.False`). Ein Leser des Tests sieht keinen Hinweis
   darauf, dass hier eine bekannte Plattformlücke „weggeprüft" statt aufgedeckt wird, außer über den
   Kommentar direkt daneben (der die Ursache zwar sauber erklärt, aber die Assertion trotzdem auf das
   fehlerhafte Ist-Verhalten statt auf einen dokumentierten Soll-Bruch legt).
3. **Reale, EPIC-4-relevante Tech-Debt?** Ja — siehe TD-005 unten, Priorität `hoch` (spätere
   Filtermatrix-Migration könnte auf dieser Referenzheuristik reale Assertions aufbauen und dabei
   fälschlich vom kontaminierten In-Memory-Verhalten ausgehen).

### Rules-Konformität

`AiNetLinter.mdc` (Methodenlänge, max. 4 Parameter, `sealed`-Pflicht): eingehalten — alle neuen
konkreten Klassen (`FilterMiniFidelityTests`, `Widget`, `Formatter`, `WidgetTests`) sind `sealed`,
alle Methoden kurz und unter dem Testprojekt-Limit von 100 Zeilen, keine Methode mit mehr als 4
Parametern. `AiNetLinterRichtlinien.mdc` §5 (keine Task-/Planungsartefakt-Referenzen in
Kommentaren): eingehalten — der erklärende Kommentar zur `CoreReferences`-Kontamination in
`FilterMiniFidelityTests.cs:86-91` vermeidet bewusst jede „step-NNN"-Referenz, im Gegensatz zu
TD-004. Keine Verstöße gefunden.

### Logische Korrektheit

Der Formvergleich (Projektnamen, Dokumentanzahl, Nullable-Kontext) sowie die Verhaltensparität
(`Widget.Describe()`-Rückgabetyp) sind korrekt implementiert und real gegen MSBuild verifiziert
(selbst nachvollzogen, siehe Build-/Test-Status). Der Dokumentanzahl-Filter (Ausschluss von
`obj`/`bin`-Pfadsegmenten) ist eine sinnvolle, im Plan nicht vorgesehene, aber sachlich richtige
Anpassung an SDK-generierte Build-Artefakte — keine Abweichung vom eigentlichen Testzweck, sondern
eine notwendige Präzisierung, kein Finding.

Das einzige logische Problem ist die in „Plan-Erfüllung"/Frage 2 beschriebene
`AssertTestProjectDetectionMatches`-Assertion für In-Memory-`FilterMini`
(`FilterMiniFidelityTests.cs:92`): sie bestätigt eine bekannte Fehlklassifikation als korrekt, statt
sie entweder als Soll-Bruch offen zu lassen oder den Test insoweit gar nicht erst zu behaupten.

### Konzept-Treue (Ebene 4)

`konzept.md` Zeile 434-440 verlangt vom strukturellen Formvergleich ausdrücklich, dass er
Formabweichungen zwischen Disk- und In-Memory-Welt aufdeckt, weil „jede darauf aufbauende
Component-Assertion wertlos" wäre, sollte die Form abweichen. Die aktuelle Umsetzung tut für eine
bekannte, reale Formabweichung das Gegenteil: sie erklärt die Abweichung im Kommentar, asserted aber
das abweichende (fehlerhafte) Verhalten als Erwartung. Damit ist der Fidelity-Test in diesem einen
Punkt nicht mehr die vom Konzept geforderte Frühwarnung, sondern eine nachträgliche Rationalisierung
eines bekannten Fehlers. Das ist ein Muss-Haben-Punkt aus dem Plan (der wiederum konzept.md Zeile
438-440 umsetzt), der nicht wie gefordert erfüllt ist → MAJOR.

### Build-/Test-Status

Alle vier im Plan verlangten Commands selbst nachgeprüft, ausnahmslos grün:

```
dotnet build AiNetLinter.slnx                                                                                 → grün, 0 Fehler/Warnungen
dotnet test src/AiNetLinter.IntegrationTests --no-build --filter FullyQualifiedName~FilterMiniFidelityTests   → grün (1 Test)
dotnet test src/AiNetLinter.FastTests --no-build --filter FullyQualifiedName~FastTestsDependencyGuardTests    → grün (2 Tests)
dotnet test src/AiNetLinter.IntegrationTests --no-build --filter FullyQualifiedName~TestCategoryProfileGuardTests → grün (1 Test)
```

## Findings

1. `src/AiNetLinter.IntegrationTests/Platform/FilterMiniFidelityTests.cs:86-92` — [MAJOR]
   [Plan-Erfüllung / Konzept-Treue] `AssertTestProjectDetectionMatches` asserted
   `Assert.True(TestProjectDetector.IsTestProject(GetProject(inMemory, "FilterMini")))` — bestätigt
   die bekannte Fehlklassifikation des Produktionsprojekts als Testprojekt in der In-Memory-Welt
   (verursacht durch die in TD-005 dokumentierte `RoslynTestSolutionFactory.CoreReferences`-
   Kontamination) statt sie als Formabweichung offen zu lassen. Widerspricht Plan-Datei-8-Punkt-3
   („muss für `FilterMini.Tests` `true`, für `FilterMini` `false` sein, in beiden Welten
   übereinstimmend") und dem in `konzept.md` Zeile 438-440 begründeten Zweck des strukturellen
   Formvergleichs (Formabweichungen früh und präzise aufdecken statt sie wegzuprüfen). **Fix:**
   Zeilen 86-92 (den erklärenden Kommentar und die `Assert.True(...)`-Zeile für
   `GetProject(inMemory, "FilterMini")`) ersatzlos entfernen — die Methode behauptet dann keine
   Aussage mehr über `IsTestProject` des In-Memory-Produktionsprojekts, statt eine falsche Aussage
   als erwartet zu bestätigen. Die übrigen drei Assertions (Disk-`FilterMini`: `false`,
   Disk-/In-Memory-`FilterMini.Tests`: `true`) bleiben unverändert bestehen und decken weiterhin
   echte Fidelity ab. Der zugrunde liegende Root-Cause-Fix an `RoslynTestSolutionFactory` selbst ist
   **nicht** Teil dieses Korrektur-Steps — bereits als TD-005 (Priorität `hoch`) erfasst, da eine
   Architektur-Entscheidung nötig ist (kein mechanischer, ermessensfreier Fix möglich).

## Tech-Debt-Einträge aus diesem Review

- `TD-005` (siehe `tech-debt.md`) — `RoslynTestSolutionFactory.CoreReferences` kontaminiert jedes
  In-Memory-Projekt mit Testhost-xunit-Referenzen, wodurch `TestProjectDetector.IsTestProject` jedes
  In-Memory-Produktionsprojekt fälschlich als Testprojekt erkennt; Priorität `hoch` wegen möglicher
  Verfälschung der künftigen EPIC-4-Filtermatrix-Migration.
