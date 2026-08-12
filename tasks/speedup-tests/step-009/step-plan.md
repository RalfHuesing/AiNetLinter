---
status: open
type: step-plan
task: speedup-tests
step: 009
corrects: step-008
title: "Korrektur: FilterMiniFidelityTests deckt IsTestProject-Diskrepanz auf statt sie wegzuassertieren"
epic: EPIC-2
estimated_risk: low
step_type: single
items: []
created_by: orchestrator
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-12
related_to: ["tasks/speedup-tests/step-008/step-review.md"]
---

# Step 009: Korrektur — FilterMiniFidelityTests deckt IsTestProject-Diskrepanz auf statt sie wegzuassertieren

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-2` aus `roadmap.md` — dieser Step korrigiert eine
  Test-Aussagekraft-Lücke aus step-008, ändert nichts am Epic-Fortschritt
  (EPIC-2 bleibt abgehakt).
- **Konzept-Referenz:** `konzept.md` Abschnitt zum strukturellen Formvergleich
  (Fidelity-Tests) — Zweck ist, echte Form-Drift zwischen Disk- und
  In-Memory-Welt aufzudecken, nicht bekannte Diskrepanzen zu verstecken.

## Aktueller Projektzustand (JIT-Kontext)

Mechanisches Korrektur-Transkript aus `tasks/speedup-tests/step-008/step-review.md`
(Finding 1, MAJOR, Ebene Plan-Erfüllung/Konzept-Treue) — keine eigene
Interpretation. Datei+Zeile sind exakt identifiziert, der Fix ist eine reine
Löschung (kein neuer Code).

Root-Cause (`RoslynTestSolutionFactory.CoreReferences` kontaminiert jedes
In-Memory-Projekt mit Testhost-xunit-Referenzen) ist bewusst NICHT Teil dieses
Korrektur-Steps — als `TD-005` (Priorität hoch, `auto_fixable: nein`) in
`tech-debt.md` festgehalten, braucht eine Architekturentscheidung.

## Intention

`FilterMiniFidelityTests.AssertTestProjectDetectionMatches` soll nur noch
Dimensionen prüfen, in denen Disk- und In-Memory-Welt tatsächlich
übereinstimmen sollen — nicht die bekannte, in `TD-005` dokumentierte
Fehlklassifikation des In-Memory-Produktionsprojekts als Testprojekt
bestätigen.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter.IntegrationTests/Platform/FilterMiniFidelityTests.cs` (Zeile 86-92)

- **Was:** Lösche die Kommentarzeilen 86-91 sowie die Zeile
  `Assert.True(TestProjectDetector.IsTestProject(GetProject(inMemory, "FilterMini")));`
  (Zeile 92) vollständig aus `AssertTestProjectDetectionMatches`. Die drei
  verbleibenden Assertions (disk `FilterMini` → `false`, disk
  `FilterMini.Tests` → `true`, in-memory `FilterMini.Tests` → `true`) bleiben
  unverändert bestehen.
- **Warum:** Behebt das MAJOR-Finding aus `step-008/step-review.md` — die
  gelöschte Assertion bestätigte eine bekannte Fehlklassifikation statt eine
  echte Formangleichung zu prüfen.

## Tests

- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~FilterMiniFidelityTests` — muss weiterhin grün sein (die drei verbleibenden Assertions ändern sich inhaltlich nicht)

## Definition of Done

- [ ] Zeilen 86-92 in `FilterMiniFidelityTests.cs` wie oben entfernt, sonst keine Änderung an der Datei
- [ ] `dotnet build` grün
- [ ] Genannter Testfilter grün
- [ ] Commit auf aktuellem Branch (Conventional Commit), Subject-Suffix `[speedup-tests]`
- [ ] `step-009/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- (keine zusätzlichen — reine Testkorrektur, keine neue Regelberührung gegenüber step-008)

## Bekannte Ausnahmen

- Keine.

## Notes

Scope-Disziplin (siehe `../../spec.md` §6.2.1): ausschliesslich das eine
MAJOR-Finding aus `step-008/step-review.md` beheben. `TD-003`, `TD-004` und
`TD-005` bleiben explizit außen vor — insbesondere `TD-005` (die eigentliche
Root-Cause in `RoslynTestSolutionFactory`) ist bewusst kein Teil dieser
Korrektur, sondern bleibt liegen, bis ein späterer Step in ihrer Nähe
vorbeikommt oder der Nutzer sie explizit als Epic aufnimmt.
