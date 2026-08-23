---
status: done
type: step-review
task: 11_epic-projektregistry-und-daemon
step: 004
epic: EPIC-A
step_type: single
reviewed_by: kritiker
reviewed_by_model: GPT-5
reviewed_by_model_knowledge_cutoff: nicht deklariert
reviewed_at: 2026-08-23T23:59:00+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 004: Produktions-Kalt-Load, Erstzugriffs-Dedupe und leasegeschuetzte Overview korrigieren

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — mindestens ein MAJOR-Finding; Korrektur-Step erforderlich (`corrects: step-004`)
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

- [x] Plan-Erfüllung: die geänderten Produktionspfade, Testanker und die drei Korrekturverträge gegen Step-Plan, Step-Result, Step-003-Review, CodeMap, Roadmap und Konzept geprüft.
- [x] Rules-Konformität: die im Step-Plan referenzierten Rules-Refs sowie `get_violations`, `safeguard` und die Metrikbudgets geprüft.
- [x] Logische Korrektheit: Kalt-Load-Fehlerverkettung, FAILED-Lifetime, Reservation/Publish-Rennen, andere Roots und Overview-Rendering semantisch über den AiNetLinter-MCP geprüft.
- [x] Konzept-Treue: A.4 (kanonischer Key, exakt eine Erstinstanz, Lease-Lifetime), A.5 (Fehlerverträge) und A.7 (FAILED-Marker/Retry und Health-Snapshot-Semantik) abgeglichen.
- [x] Build: den im `step-result.md` dokumentierten grünen Abschlusslauf verwendet, nicht wiederholt.
- [x] Tests: die im `step-result.md` dokumentierten grünen Nicht-Stress-Gates und die gezielten Korrekturanker verwendet, nicht wiederholt.

## Befund

### Plan-Erfüllung

Die Originalexception wird im produktiven `TryLoadSolutionAsync` nach dem Warn-Log propagiert, der gemeinsame `PROJECT_LOAD_FAILED`-Descriptor enthält Meldung, Solution-Kontext und Retry-Hint, und die Overview hält ihren Lease über Snapshot und Rendering; diese Teile sind durch gezielte MCP-Symbolprüfungen und die dokumentierten Tests belegt. Die exakte Erstzugriffs-Dedupe und die garantierte Loading→Failed→Retry-Sequenz sind jedoch wegen der unten beschriebenen Race-Fenster nicht vollständig erfüllt.

### Rules-Konformität

Die relevanten Produktions-, FastTests- und IntegrationTests-Sichten melden jeweils 0 Violations; `safeguard` für `src/AiNetLinter` ist 10,00/10 PASS. Die geprüften geänderten Typen und Methoden liegen innerhalb der dokumentierten LOC-, Komplexitäts-, Parameter- und AI-Context-Grenzen; aus den Rules-Refs ergibt sich kein separates Finding.

### Logische Korrektheit

Die Reservation verhindert das im Step-003-Review beanstandete Factory-im-Lock und die deterministisch synchronisierte Reservation-Barriere ist sinnvoll. Sie ist aber nicht atomar mit dem vorgelagerten Resident-Lookup, und der FAILED-Freigabezustand unterscheidet nicht zwischen einer Loading-Antwort und einer tatsächlich erzeugten `PROJECT_LOAD_FAILED`-Antwort; beide Lücken brechen die geforderten Nebenläufigkeitsverträge in zulässigen Interleavings.

### Konzept-Treue (Ebene 4)

Die Umsetzung folgt dem Konzept bei leasegeschütztem Overview-Rendering, konsistenten Root-/Loader-/LoadFailed-Texten und unveränderter Health-Snapshot-Aggregation. A.4/A.7 sind dennoch nicht vollständig erfüllt: Ein kanonischer Root kann bei einem Lookup-/Reservation-Rennen mehr als eine Factory und mehr als einen Hintergrund-Load erhalten, und der FAILED-Marker kann vor der vorgeschriebenen Fehlerantwort freigegeben werden.

### Build-/Test-Status

Die Ergebnisse stammen aus `step-004/step-result.md`; der vollständige Nicht-Stress-Stack wurde gemäß Auftrag nicht erneut ausgeführt.

```text
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (1680 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (351 Tests, 0 Fehler)
```

## Findings (nur bei `issues`)

1. `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs:304` und `src/AiNetLinter/Mcp/Projects/ProjectToolCall.cs:39-40` — **[MAJOR] [Logik/Konzept-Treue]** `ReleaseEntry` setzt `FailureLeaseReleased` für jedes Lease frei, dessen Server beim Release bereits `LoadFailed` ist. Der Toolpfad kann in Zeile 39 zunächst `Loading` beantworten und den Lease direkt danach freigeben; faultet der Hintergrund-Load zwischen Zustandsprüfung und diesem Release, wird dieser Loading-Lease wie eine Fehlerantwort behandelt. Der nächste Aufruf erfüllt dann `FindAdoptable`-Voraussetzungen, entfernt den FAILED-Entry und startet einen frischen Load, ohne zuvor `PROJECT_LOAD_FAILED` mit Originalmeldung auszugeben. Der Produktionsregressionstest hält mit `initialLease` in `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandContractTests.cs:85-111` absichtlich einen zusätzlichen Lease offen und deckt dieses echte Handler-Interleaving daher nicht ab. **Fix:** Den Lease-/Registry-Vertrag um eine eindeutige Markierung ergänzen, die nur der tatsächlich erzeugte `LoadFailed`-Antwortpfad vor dem Release setzt; ein Lease, das `Loading` zurückgibt, darf `FailureLeaseReleased` nicht setzen. Einen deterministischen Test ergänzen, der den Load unmittelbar vor dem Release des Loading-Leases faultet und anschließend zwingend erst `PROJECT_LOAD_FAILED` und danach bei einem weiteren Aufruf eine neue Instanz erwartet.

2. `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs:140-146,161-174,202-205` — **[MAJOR] [Logik/Konzept-Treue]** Der Resident-Lookup und die Reservation sind getrennte Lock-Abschnitte: Nach `FindAdoptable` kann ein Caller vor `ReserveCreation` pausieren. Publiziert ein anderer Caller in diesem Fenster die Instanz und entfernt die Reservation, sieht der pausierte Caller in `ReserveCreation` keinen Eintrag mehr, erzeugt eine zweite Factory/Server-/Load-Kette und trifft erst in `PublishCreation` auf `raced`. Dieser Race-Zweig adoptiert zwar den Gewinner, fügt `attempt.Creation.Server` aber nicht zu `retired` hinzu; der Verlierer bleibt somit samt Hintergrund-Load unentsorgt. Die vorhandene Barriere in `src/AiNetLinter.FastTests/Mcp/Projects/ProjectRegistryTests.cs:118-122` startet den zweiten Caller erst während eine Reservation bereits existiert und prüft dieses Lookup→Reserve-Fenster nicht. **Fix:** Resident-Prüfung und Reservation unter demselben `gate`-Abschnitt atomar koppeln bzw. `ReserveCreation` vor dem Erzeugen nochmals auf einen inzwischen publizierten Entry prüfen; jeder bereits erzeugte, nicht publizierte Loser muss nach dem Lock sicher außerhalb des Locks disposed werden. Einen deterministischen Test mit einer Barriere zwischen `FindAdoptable` und `ReserveCreation` sowie exakter Factory-/Load-/Dispose-Zählung ergänzen.
