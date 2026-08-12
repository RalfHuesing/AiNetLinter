---
status: done
type: step-plan
task: speedup-tests
step: 014
corrects: step-013
title: "Korrektur: Namespace-Glob-Vertrag selektiv kalibrieren"
epic: EPIC-4
estimated_risk: low
step_type: single
items: []
created_by: orchestrator
created_by_model: "gpt-5.6-sol"
created_by_model_knowledge_cutoff: "nicht ausgewiesen"
created_at: 2026-08-12
related_to: [step-013/step-review.md]
---

# Step 014: Korrektur: Namespace-Glob-Vertrag selektiv kalibrieren

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-4`
- **Korrektur:** `step-013`, Finding 1 in `step-013/step-review.md`

## Aktueller Projektzustand (JIT-Kontext)

`src/AiNetLinter.FastTests/Maps/Skeleton/SkeletonMapFilterTests.cs:126-138` verwendet
`IncludeNamespaces = ["FilterMini.*"]`. Das Muster trifft `FilterMini.Core`, `FilterMini.Utils`
und `FilterMini.Tests.Core`; der Fall würde deshalb auch bei ignoriertem Include-Filter grün bleiben.

## Intention

Der vorhandene Namespace-Glob-Fall muss wieder nachweisen, dass ein Namespace-Glob andere
Namespaces ausschließt. Die Fallzahl von 18 bleibt erhalten.

## Konkrete Änderungen

### `src/AiNetLinter.FastTests/Maps/Skeleton/SkeletonMapFilterTests.cs:126-138`

- **Was:** Den Fall mit einem selektiven Glob `FilterMini.Tests.*` kalibrieren und sowohl den
  erwarteten Treffer als auch die ausgeschlossenen Produktions-Namespaces assertieren. Die
  Fallzahl 18 beibehalten.
- **Warum:** Ein ignorierter Include-Namespace-Filter darf den Test nicht bestehen.

## Tests

- [ ] `dotnet build src/AiNetLinter.FastTests` → grün.
- [ ] `dotnet test src/AiNetLinter.FastTests --no-build --filter FullyQualifiedName~SkeletonMapFilterTests` → 18 Tests grün.

## Definition of Done

- [ ] Der selektive Glob belegt positiven Treffer und negative Ausschlüsse.
- [ ] Die Filtermatrix umfasst weiterhin 18 Fälle.
- [ ] Die gezielten Build-/Testbefehle sind grün.
- [ ] Commit auf aktuellem Branch mit Suffix `[speedup-tests]`.
- [ ] `step-014/step-result.md` geschrieben.
- [ ] `status` auf `done (pending audit)` gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §4/§5 — Assertions nicht abschwächen und Ursache im Testvertrag beheben.

## Bekannte Ausnahmen

Keine.

## Notes

Nur Finding 1 aus `step-013/step-review.md` umsetzen; keine Fixture-, Produkt- oder sonstigen
Filteränderungen.
