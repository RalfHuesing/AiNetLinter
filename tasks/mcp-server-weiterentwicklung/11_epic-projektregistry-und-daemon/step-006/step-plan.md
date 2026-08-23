---
status: done (Korrektur ausstehend)
type: step-plan
task: 11_epic-projektregistry-und-daemon
step: 006
corrects: step-005
title: "Race-Interleavings in den Abnahmetests deterministisch verankern"
epic: EPIC-A
estimated_risk: medium
step_type: single
items: []
created_by: orchestrator
created_by_model: GPT-5
created_by_model_knowledge_cutoff: nicht deklariert
created_at: 2026-08-23T23:25:00+02:00
related_to: ["step-005/step-review.md", "step-005/step-plan.md", "step-005/step-result.md"]
---

# Step 006: Race-Interleavings in den Abnahmetests deterministisch verankern

## Bezug und Scope

- **Task:** `11_epic-projektregistry-und-daemon`
- **Epic:** EPIC-A
- **Korrekturquelle:** `step-005/step-review.md`, Findings 1–2.
- Dieser Step ist ein reiner Testkorrektur-Step. Die Produktionslogik aus
  `a50bff9a` bleibt unverändert, sofern kein minimaler, bereits vorhandener
  Test-Hook genutzt werden muss; Overview-, Loader-, Health- und sonstige
  Verträge sind nicht Scope.

## Korrekturen

### 1. Loading→Fault→Release ohne künstlichen Zusatz-Lease

- `ProductionColdLoad_BrokenSlnx_ReturnsOriginalLoadFailedContract` darf den
  `initialLease` nicht über Loading-Antwort, Fault und ersten Folgeaufruf
  offen halten.
- Der Produktions-Load wird mit einer deterministischen Barriere so geführt,
  dass der Initial-Lease nach dem Loading-Aufbau freigegeben ist und der Fault
  unmittelbar im relevanten Release-Interleaving sichtbar wird.
- Der Test beweist anschließend strikt die Sequenz: erster Folgeaufruf
  `PROJECT_LOAD_FAILED` mit Originalmeldung und Restore-/Retry-Hint; erst der
  zweite Folgeaufruf erzeugt eine neue Instanz bzw. startet einen frischen
  Retry. Ein zusätzlicher Busy-Lease darf die fehlerhafte Vorgängersequenz
  nicht künstlich verdecken.

### 2. Lookup→Reservation-Race und Loser-Disposal

- `Lease_AtomicLookupAndReservation_CreatesAndDisposesOnlyTheWinner` muss den
  früheren Vorzustand reproduzierbar ansteuern: ein erster Caller wird am
  bisherigen Lookup→Reservation-Interleaving kontrolliert angehalten, ein
  konkurrierender Publish wird abgeschlossen, danach läuft der erste Caller
  weiter.
- Der Test muss gegen die alte Step-004-Struktur zwei Creation-Pfade und den
  nicht publizierten Verlierer sichtbar machen können; gegen die atomare
  Step-005-Struktur müssen exakt ein Factory-/Load-Pfad, eine gemeinsame
  residente Instanz und genau eine Disposal des tatsächlich nicht publizierten
  Verlierers nachgewiesen werden.
- Verwende dafür ausschließlich einen bestehenden testbaren Seam oder einen
  minimalen test-only Hook; kein Warten auf `LoadTask`, keine globale
  Testserialisierung und keine Änderung des Registry-Lock-Vertrags. Factory-,
  Load- und Dispose-Zähler sowie der Other-Root-Lock-Hygiene-Anker bleiben
  explizit im Test.

## Akzeptanzkriterien

1. Der Kalt-Load-Test scheitert, wenn der Loading-Lease den FAILED-Marker
   fälschlich freigeben würde; er weist die Reihenfolge Fehlerantwort vor
   frischem Retry ohne zusätzlichen offenen Lease nach.
2. Der Dedupe-Test scheitert, wenn der Test erst nach eingetragener Reservation
   startet; sein kontrollierter Interleaving-Anker und die Zähler weisen
   Factory/Load/Dispose des Losers sowie Reference-Identity der Gewinnerinstanz
   nach.
3. Die Tests sind deterministisch, nicht zeitabhängig und lassen andere Roots
   während einer blockierten Creation bedienbar.
4. Es gibt keine Produktionsänderung außerhalb eines minimalen, ausdrücklich
   als test-only begründeten Seams; keine Scope-Ausweitung auf bereits
   abgenommene Overview-/Loader-/Health-Verträge.

## Tests und Verifikation

- Gezielte Iteration nur für die beiden betroffenen Tests und benachbarte
  Unit-/Integration-Contract-Tests; kein Stresslauf.
- Abschluss genau einmal: `dotnet build`,
  `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`,
  `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`.
- Vor jedem Commit MCP-Quality-Gates für geänderte Scopes ausführen:
  `get_violations`, `safeguard`, `metrics_lookup` sowie gezielt
  `get_impact`/`get_feature_context`.
- Drift-Audit bleibt einmalige Epic-Abschlussaktivität und wird hier nicht
  ausgeführt.

## Definition of Done

- [ ] Beide Test-Findings aus `step-005/step-review.md` sind deterministisch
  und regressionsstark behoben.
- [ ] Build und beide Nicht-Stress-Testprojekte sind genau einmal als
  Abschlusslauf grün; Stress bleibt unberührt.
- [ ] `step-006/step-result.md` dokumentiert Nachweise, Abweichungen und
  MCP-Gates; `codemap.md` ist pointer-artig gepflegt; dieser Plan steht danach
  auf `done (pending audit)`.
- [ ] Coder erstellt zwei gezielte Commits: Code/Tests, danach
  Doku/Artefakte. Keine Historienmanipulation und kein Push.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#1` — MCP-first-Symbol-/Impact-
  Prüfung für jeden Test-/Seam-Eingriff.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4` — xUnit-v3, deterministische
  Barrieren, gefilterte Iteration und genau ein Nicht-Stress-Abschlusslauf.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5` — explizite Vertragsassertions,
  Zero-Warning-Gate und keine künstliche globale Serialisierung.
