---
status: done
type: step-review
task: 11_epic-projektregistry-und-daemon
step: 005
epic: EPIC-A
step_type: single
reviewed_by: kritiker
reviewed_by_model: GPT-5
reviewed_by_model_knowledge_cutoff: nicht deklariert
reviewed_at: 2026-08-23T23:14:38+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 005: FAILED-Freigabe und Registry-Reservation atomar absichern

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — mindestens ein MAJOR-Finding; Korrektur-Step erforderlich (`corrects: step-005`)
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

- [x] Plan-Erfüllung: Produktionspfade und Testanker gegen Step-Plan, Step-Result, Step-004-Review, CodeMap, Roadmap und Konzept geprüft; die Implementierung entspricht den Korrekturentscheidungen, die beiden Abnahmeanker sind jedoch nicht regressionsstark genug.
- [x] Rules-Konformität: die in den Rules-Refs genannten Regeln aus `AiNetLinter.mdc#agent-resilience` sowie `AiNetLinterRichtlinien.mdc#1`, `#4` und `#5` geprüft; die MCP-Quality-Gates sind grün.
- [x] Logische Korrektheit: Loading→Fault→Release, FAILED-Marker, atomarer Lookup/Reservation-Abschnitt, Publish-Race, Loser-Disposal und andere Roots über MCP-Symbole, Bodies, Referenzen und Impact geprüft.
- [x] Konzept-Treue: A.4/A.7 und der A.8-Testkatalog zu genau einem residenten Server, FAILED-Marker/Retry, Lock-Hygiene und loser disposal abgeglichen; die Produktionslogik folgt dem Vertrag, die Testnachweise decken die geforderten Interleavings nicht ab.
- [x] Build: den grünen Abschlusslauf aus `step-005/step-result.md` verwendet, nicht wiederholt.
- [x] Tests: den dokumentierten grünen Nicht-Stress-Abschlusslauf verwendet, nicht wiederholt; zusätzlich die beiden betroffenen Tests gezielt ausgeführt (je 1/1 grün), ohne Stresslauf oder Drift-Audit.

## Befund

### Plan-Erfüllung

Die explizite FAILED-Antwortmarkierung ist in `ProjectLease` vorhanden und wird im `LoadFailed`-Zweig von `ProjectToolCall.ExecuteAsync` gesetzt; `ReleaseEntry` leitet die Freigabe nicht mehr allein aus `LoadState` ab. Lookup und Reservation liegen in `TryAdoptOrCreate` im selben kurzen Registry-Lock, während Definition/Factory/Load außerhalb des Locks bleiben; der bereits erzeugte, nicht publizierte Server wird im Race-Zweig in `retired` aufgenommen und von `Lease` außerhalb des Locks disposed. Die Definition-of-Done-Testanker sind trotzdem nicht erfüllt, weil beide neuen Tests die jeweils beanstandeten Vorzustände nicht deterministisch nachstellen und daher auch gegen die fehlerhafte Step-004-Version grün bleiben können.

### Rules-Konformität

Die Resilience-Regel ist eingehalten: kein `await`/Solution-Load liegt im Registry-Lock, und die gezielten MCP-Metriken melden alle geprüften Methoden innerhalb der Grenzwerte. `get_violations` meldet 0 Verstoesse für `src/AiNetLinter`, `src/AiNetLinter.FastTests` und `src/AiNetLinter.IntegrationTests`; `safeguard` für `src/AiNetLinter` meldet 10,00/10 bei Threshold 8,00. Die MCP-first-Symbol-/Impact-Prüfung und der gezielte Testlauf entsprechen den Workflow-Gates; es gibt kein separates Rules-Finding.

### Logische Korrektheit

Die Produktionspfade sind für die beiden Zielinterleavings korrekt strukturiert: ein Loading-Lease besitzt kein Fehlerantwort-Flag, ein echter `PROJECT_LOAD_FAILED`-Pfad markiert genau seinen Lease, und die Registry entfernt den FAILED-Entry erst bei markierter Freigabe und `InFlightCount == 0`. Die atomare Reservation teilt eine `Lazy`-Creation, publiziert nur einen Server und disposiert einen tatsächlich unterschiedlichen Publish-Verlierer nach dem Lock. Die Tests beweisen diese Eigenschaften jedoch nicht: Der Loading-Test hält einen zusätzlichen Lease offen, und der Reservation-Test pausiert erst während der Factory nach bereits eingetragener Reservation; ein nicht publizierter Verlierer wird überhaupt nicht erzeugt.

### Konzept-Treue (Ebene 4)

Die Implementierung folgt dem A.4/A.7-Vertrag zu residenter Instanz, FAILED-Marker, deterministischer Retry-Reihenfolge und lockfreier Factory-/Load-Phase. Gegen A.8 fehlen belastbare Nachweise für das ausdrücklich verlangte Loading→Fault→Release-Interleaving sowie für das Lookup→Reservation-Rennen mit exakter Factory-/Load-/Dispose-Zählung; damit ist die Korrektur im Step-Scope noch nicht abnahmefähig.

### Build-/Test-Status

Die Abschlussläufe stammen aus `step-005/step-result.md` und wurden gemäß Auftrag nicht wiederholt:

```text
dotnet build → grün (0 Warnungen, 0 Fehler; Nachweis aus step-result.md)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (1681 Tests, 0 Fehler; Nachweis aus step-result.md)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (351 Tests, 0 Fehler; Nachweis aus step-result.md)
```

Gezielte Stichproben:

```text
dotnet test src/AiNetLinter.FastTests --no-build --filter FullyQualifiedName~Lease_AtomicLookupAndReservation_CreatesAndDisposesOnlyTheWinner → grün (1 Test, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --no-build --filter FullyQualifiedName~ProductionColdLoad_BrokenSlnx_ReturnsOriginalLoadFailedContract → grün (1 Test, 0 Fehler)
```

## MCP-Quality-Gates

- `get_violations`: 0 Verstoesse in den drei geprüften Projekt-Scopes.
- `safeguard`: 10,00/10 für `src/AiNetLinter`, Threshold 8,00, PASS.
- `metrics_lookup`: alle 10 geprüften Produktions-/Test-Symbole innerhalb der dokumentierten LOC-, Komplexitäts- und Parametergrenzen.
- `get_feature_context`, `get_symbol_body`, `find_references` und `get_impact`: FAILED-Markierung, Release-Aufruf, Reservation/Publish-Aufrufer und Testzuordnungen semantisch aufgelöst; keine weiteren Scope-Auswirkungen gefunden.

## Findings (nur bei `issues`)

1. `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandContractTests.cs:98` (Freigabe erst in Zeile 124) — **[MAJOR] [Plan/Logik/Konzept-Treue]** `ProductionColdLoad_BrokenSlnx_ReturnsOriginalLoadFailedContract` hält `initialLease` über den Loading-Aufruf, den Load-Fehler und den ersten Fehlerantwort-Aufruf offen. Dadurch bleibt `InFlightCount` beim Release des Loading-Leases mindestens 1; selbst die fehlerhafte Step-004-Implementierung, die den FAILED-Marker aus `LoadState` ableitet, hätte an diesem Punkt wegen des Busy-Guards nicht freigegeben. Der Test kann daher das geforderte Loading→Fault→Release-Rennen und die zwingende Reihenfolge „nächster Aufruf `PROJECT_LOAD_FAILED`, erst danach frischer Retry“ nicht widerlegen. **Fix:** Den initialen Lease nach dem Start des Background-Loads vor dem Loading-`ProjectToolCall` freigeben und den Loadübergang mit einer deterministischen Barriere so koordinieren, dass der Fehler unmittelbar im Release-Interleaving sichtbar wird; anschließend explizit den ersten Folgeaufruf auf `PROJECT_LOAD_FAILED` und erst den zweiten auf eine neue Serverinstanz prüfen.

2. `src/AiNetLinter.FastTests/Mcp/Projects/ProjectRegistryTests.cs:157-164` — **[MAJOR] [Plan/Logik/Konzept-Treue]** `Lease_AtomicLookupAndReservation_CreatesAndDisposesOnlyTheWinner` startet den zweiten Caller erst, nachdem `factoryEntered` aus der Factory signalisiert wurde; zu diesem Zeitpunkt ist die Reservation bereits eingetragen. Der Test prüft damit nur das bestehende Single-Flight-Warten und würde auch mit dem Step-004-Code grün bleiben. Außerdem erzeugt er bei `InstancesCreated == 1` keinen nicht publizierten Creation-Verlierer und verifiziert folglich weder dessen genau einmalige Disposal noch, dass die residente Gewinnerinstanz unangetastet bleibt. **Fix:** Einen deterministischen Race-Anker am bisherigen Lookup→Reservation-Fenster ergänzen, der den ersten Caller dort pausiert, den konkurrierenden Publish und danach die Fortsetzung des ersten Callers kontrolliert; der Test muss gegen die Step-004-Version zwei Creation-Pfade sichtbar machen, gegen Step-005 genau einen Factory-/Load-Pfad, eine gemeinsame residente Instanz und die exakt einmalige Disposal des tatsächlich nicht publizierten Verlierers (sowie weiterhin Servicefähigkeit eines anderen Roots) nachweisen.
