---
status: done (pending audit)
type: step-plan
task: 11_epic-projektregistry-und-daemon
step: 014
corrects: step-013
title: "Step-013-Korrektur: fehlende Contract-Nachweise (F1) und erreichbare Timeout-Diagnostik (F2)"
epic: EPIC-B
estimated_risk: medium
step_type: single
items: []
created_by: orchestrator
created_by_model: stealth/ox-alpha (openrouter)
created_by_model_knowledge_cutoff: nicht deklariert
created_at: 2026-08-24T12:45:00+02:00
related_to:
  - step-013/step-review.md
---

# Step 014: Step-013-Korrektur — fehlende Contract-Nachweise (F1) und erreichbare Timeout-Diagnostik (F2)

> **Charakter dieses Plans:** Mechanisches Transkript der Findings aus
> `step-013/step-review.md` (Verdict `issues`) gemäß `../spec.md` §6.2.1 —
> kein Planer-Aufruf, kein Ermessensspielraum. Es wird bewusst nichts
> ergänzt oder umformuliert; die Fix-Anweisungen stehen wörtlich im Review.

## Bezug

- **Task:** `11_epic-projektregistry-und-daemon`
- **Epic:** EPIC-B aus `roadmap.md` — Korrektur im Abschluss-Cluster
  (ThinClient/Pump/Testkatalog); Epic-Zuordnung von `step-013` übernommen.
- **Konzept-Referenz:** `Konzept.md` B.2 (Retry/Hänger), B.3 (Hänger-Schutz),
  B.6 (Testkatalog) — unverändert gegenüber `step-013`.

## Aktueller Projektzustand (JIT-Kontext)

Transkript — der maßgebliche Zustand steht im Review (`step-013/step-review.md`)
und im Result (`step-013/step-result.md`). Betroffene Symbole laut Finding:

- `src/AiNetLinter/Mcp/Daemon/DaemonBytePump.cs:146` (gegenüber `:149-150`)
- `ThinClientProxy.ReportPumpFailure` (Diagnosekanal des Hänger-Falls)
- `src/AiNetLinter.FastTests/Mcp/Daemon/` und
  `src/AiNetLinter.IntegrationTests/Mcp/Daemon/` (fehlende Nachweisdateien)

## Intention

Die vom Review als MAJOR nachgewiesenen Lücken schließen: die fünf im
Step-013-Testkatalog geforderten, aber fehlenden Contract-Nachweise ergänzen
(F1) und den unerreichbaren Timeout-Diagnosezweig in der Pump so korrigieren,
dass der Hänger-Fall eine unterscheidbare Signatur trägt (F2). Damit werden
die in step-013 angekreuzten DoD-Zeilen tatsächlich belegt.

## Konkrete Änderungen

### F2 — `src/AiNetLinter/Mcp/Daemon/DaemonBytePump.cs:146` (gegenüber `:149-150`)

- **Was (wörtlich aus dem Review):** Den reinen Idle-Timeout-Fall vor dem
  Null-Zweig erkennen (`linked.IsCancellationRequested &&
  !callerToken.IsCancellationRequested && inputFailure/outputFailure sind
  OperationCanceledException` → `TimeoutException` liefern).
- **Warum:** Der `TimeoutException`-Zweig ist derzeit unerreichbar — beide
  Pump-Tasks werden bei Erreichen des Idle-Limits über denselben linked Token
  gemeinsam gecancelt, sodass Zeile 146 zuerst greift und das Hänger-Ereignis
  über `ReportPumpFailure` als „unbekannter Pipe-Fehler" erscheint. Das
  Akzeptanzkriterium „Retry, Hänger, Konflikt und Restart sind unterscheidbar"
  ist auf dem Diagnosekanal verletzt.
- **Absicherung:** Im neuen Hänger-Contract (siehe F1, Nachweis 3) auf die
  Timeout-Signatur assertieren.

### F1 — fünf fehlende Contract-Nachweise (FastTests bzw. IntegrationTests/Mcp/Daemon/)

Wörtlich aus dem Review (Belegsuche über beide Suiten: `ReplayFrame|
DaemonBytePump|ThinClientProxy|PumpIdle|ReplayWindow` trifft ausschließlich
`ThinClientContractTests.BytePump_ForwardsOpaqueFramesWithoutJsonInterpretation`,
das keinen Replay-/Retry-Pfad berührt):

1. **Genau-ein Replay** nach Rohframe-Abschluss ohne Antwort (AK 4): Fenster
   gesetzt → Antwort löscht das Fenster (`Take() == null`); erneuter Lauf
   schreibt die ReplayFrame zuerst. Auf `DaemonBytePump`-Ebene ohne Seams
   unit-testbar (Streams injizieren, `DaemonPumpOptions(…, ReplayFrame)`).
2. **Zweiter Rohfehler ohne dritte Runde** (AK 4): zweiter Abbruch →
   `Completed=false`, Exit ≠ 0, kein Loop; Proxy-Seite über Integration mit
   kontrolliertem Pipe-Abbruch oder über einen minimalen internen Test-Seam am
   Retry-Fenster absichern.
3. **Ping-/Hänger-Timeout → `TerminateIdentifiedDaemon` + genau ein Ereignis**
   (AK 5, Plan-Ausnahme „Stellvertreterprozess"): Deterministischer Kern auf
   Pump-Ebene (winziges `PumpIdleTimeout`, stummer Stream → Timeout-Signatur
   nach F2-Fix assertieren); die Kill-/Restart-Entscheidung des Proxys braucht
   dafür einen kleinen testbaren Seam (z. B. Pump-Optionen/Timeout injizierbar)
   oder einen engen Integrationslauf — Architekturmehrheit liegt beim
   Korrektur-Step, kein Rückbau bestehender Verträge.
4. **Zwei ThinClients teilen die Daemon-Registry** (DoD-Zeile B.6,
   Shared-Warmth über RefreshCount/Keys): als enger Zwei-Prozess-Lauf über den
   Raw-Wire-Harness (`noDaemon: false`, kurzer Idle-Exit, gemeinsame Fixture).
5. **Connect-or-Start-Transitions/konkurrierende Starter am Mock-Pipe**
   (B.6 Unit): dedizierter Unit-Test statt nur indirekter Abdeckung über den
   kalten Integrationslauf.

**Review-Fazit (übernommen):** Bis zu diesen Nachweisen bleibt die DoD-Aussage
„durch Unit-/Integration-Contracts belegt" für genau-ein Retry, zweiter
Rohfehler, Ping-Hänger-Schutz und Shared-Warmth unhaltbar.

## Tests

- [ ] Nachweis 1: Genau-ein Replay (AK 4, Pump-Level-Contract)
- [ ] Nachweis 2: Zweiter Rohfehler ohne dritte Runde (AK 4)
- [ ] Nachweis 3: Hänger-Timeout → Kill + genau ein unterscheidbares Ereignis (AK 5, mit F2-Signatur)
- [ ] Nachweis 4: Zwei ThinClients teilen die Daemon-Registry / Shared-Warmth (B.6)
- [ ] Nachweis 5: Connect-or-Start-Transitions/konkurrierende Starter am Mock-Pipe (B.6)
- [ ] Bestehende ThinClient-/Daemon-/Host-Contracts bleiben grün (Regression)

## Definition of Done

- [ ] F2 korrigiert und durch Nachweis 3 abgesichert
- [ ] Alle fünf Nachweise vorhanden und grün
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün (0 Warnungen, 0 Fehler)
- [ ] Test-Command aus Tech-Stack-Notiz grün — kompletter Nicht-Stress-Stack
      genau EINMAL vor Step-Abschluss; Entwicklung selbst mit gefilterten
      Läufen (`Category=Unit` bzw. gezielte Filter); `Category=Stress` nie
- [ ] MCP-Quality-Gates vor jedem Commit (`get_violations`, `safeguard`);
      drift-audit NICHT erneut ausführen (in step-013 für EPIC-B erledigt)
- [ ] Commit auf aktuellem Branch (Conventional Commit, Deutsch, imperativ,
      Suffix `[11_epic-projektregistry-und-daemon]`)
- [ ] `step-014/step-result.md` geschrieben
- [ ] `status` in diesem `step-plan.md` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §3 (Testkategorien, TRX-Diagnose),
  §4 (kein Symptom-Fixing — Assertions nicht abschwächen, Ursache beheben),
  §5 (Zero-Warning)
- `.agents/rules/AiNetLinter.mdc` (aktive Grenzwerte: `sealed`, Footprint ≤2500,
  Methoden ≤60 Zeilen, Options-Records ab 5 Parametern)

## Bekannte Ausnahmen

- `ProjectRegistryTests.Lease_AtomicLookupAndReservation_CreatesAndDisposesOnlyTheWinner`
  ist ein timingabhängiger EPIC-A-Bestandstest unter Volllast (laut Review
  kein Step-013-Defekt; gezielt dreifach grün). Fällt er im einmaligen
  Vollstack-Lauf, während er isoliert grün läuft, ist das kein
  Step-014-Fehler — im Result dokumentieren, nicht als Regression werten.

## Notes

- **Ausdrücklich NICHT Teil dieses Korrektursteps** (laut Review
  Nutzerentscheid, kein Blocker): der Konzept-Entscheidungsbedarf zum
  AK-5-„Call-Log-Ereignis“ beim SDK-freien ThinClient. Die Meldungen bleiben
  stderr-[WARN]-Ereignisse; es wird KEIN Observability-Sink gebaut und kein
  Konzept rückgebaut.
- Optionale, laut Review nur opportunistische Schärfungen (keine DoD-Pflicht,
  nur wenn nebenbei möglich): All-Zeilen-JSON-Stdout-Assertion zusätzlich für
  einen `noDaemon: false`-Lauf; gezielter Catch einer
  `ThinClientVersionConflictException` statt des generischen FATAL-Catch
  (`Program.cs:61-65`).
- TD-007 (Abdeckungsasymmetrie Legacy-/Daemon-Pfad) ist reine Beobachtung —
  hier nichts dagegen unternehmen.
