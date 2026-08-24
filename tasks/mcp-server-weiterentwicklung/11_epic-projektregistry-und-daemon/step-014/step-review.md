---
status: done
type: step-review
task: 11_epic-projektregistry-und-daemon
step: 014
epic: EPIC-B
step_type: single
reviewed_by: kritiker
reviewed_by_model: stealth/ox-alpha (openrouter)
reviewed_by_model_knowledge_cutoff: nicht deklariert
reviewed_at: 2026-08-24T13:05:00+02:00
verdict: approved
tech_debt_ids: [TD-008]
---

# Review Step 014: Step-013-Korrektur — fehlende Contract-Nachweise (F1) und erreichbare Timeout-Diagnostik (F2)

## Verdict

- [x] **approved** — alle vier Prüfebenen ok; F1 und F2 sind vollständig geschlossen
- [ ] **issues**
- [ ] **blocked**

## Geprüft

- [x] Plan-Erfüllung: beide Commits vollständig im Diff gelesen (`683a3e4f` Code/Tests, `26898fba` Result/Codemap/Status), Abgleich gegen die wörtlichen Fix-Anweisungen aus `step-013/step-review.md`
- [x] Rules-Konformität: nur die im Plan zitierten Dateien (`AiNetLinter.mdc`, `AiNetLinterRichtlinien.mdc` §3/§4/§5)
- [x] Logische Korrektheit: `DaemonBytePump.ReadFailure` und der Seam-Umbau von `ThinClientProxy` Zeile für Zeile nachvollzogen; alle fünf neuen Nachweisdateien gegen die geforderten Verträge geprüft
- [x] Konzept-Treue: `Konzept.md` B.2/B.3/B.6 inkl. AK-5-Entscheidung (Option A) gegen die Umsetzung geprüft
- [x] Build: selbst nachgeprüft — `dotnet build` → 0 Warnungen, 0 Fehler (TreatWarningsAsErrors erfüllt)
- [x] Tests: gezielte Filterläufe als Stichprobe (alle fünf Nachweise + F2-Signatur + Architektur-Guard); kein Vollstack-Rerun, keine Stress-Kategorie, kein Drift-Audit

## Befund

Alle fünf geforderten Contract-Nachweise existieren mit harten, nicht abgeschwächten Assertions und sind in eigenen Läufen grün; F2 ist exakt nach Review-Vorschrift umgesetzt und durch Nachweis 3 abgesichert; der Vollstack-Befund (4. `Process.Start`-Stelle → `StandInProcess` in der guard-whitelisteden Harness-Datei) ist regelkonform, ohne den Guard anzutasten, und die Coder-Abweichungen (Seam-Umfang, N2 in-proc) bleiben innerhalb der vom Plan explizit erlaubten Alternativen.

### Plan-Erfüllung

Vollständig: F2 gemäß wörtlicher Fix-Anweisung (`pumpCancelled && inputTask.IsCanceled && outputTask.IsCanceled → TimeoutException`, vor dem Null-Fall, Caller-Cancel zuvor ausgeschlossen und unattributiert — das `IsCanceled`-Paar ist wegen `ObserveAsync` (OCE → null) die exakte Beobachtungsäquivalenz der vorgeschriebenen OCE-Bedingung); F1 mit allen fünf Nachweisen (N1 Pump-Level Replay-Fenster/-Reset/-Vorrang, N2 zweiter Rohfehler Exit 2 mit genau zwei signaturhaltigen `[WARN]` und genau zwei Verbindungen, N3 Kill des echten Welcome-PID-Stellvertreters + genau ein unterscheidbares Ereignis mit F2-Signatur, N4 echter Zwei-Prozess-Lauf mit identischem RefreshCount, strikt gewachsener Uptime und geteiltem Key, N5 Connect-or-Start-Transitions inkl. konkurrierender Starter am Mock-Pipe).

### Rules-Konformität

Eingehalten: Records/Klassen `sealed`, Methoden kurz, kein leeres `catch`, `TestTempDirectory`, keine zwangsserialisierende Collection, Zero-Warning-Build bestätigt.

### Logische Korrektheit

Die neue Timeout-Bedingung kann den echten Rohfehler nicht verschlucken (Faulted-Tasks fallen durch zu `inputFailure ?? outputFailure`), der Default-Pfad verhält sich unverändert, und die N2/N3-Skripte treiben Rohabbrch/Schweigen deterministisch statt timingabhängig.

### Konzept-Treue (Ebene 4)

Kein Non-Goal verletzt und B.6 damit belegt; die AK-5-Frage ist gemäß Nutzerentscheid (Option A: stderr-[WARN]-Ereignisse mit F2-Signaturen sind das geforderte Ereignis, kein zusätzlicher Sink) entschieden — die Konzept-Dokumentation folgt separat durch den Orchestrator und ist kein Mangel dieses Steps.

### Build-/Test-Status

```
dotnet build                                                                                          → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter ~ThinClientPumpContractTests|~ThinClientConnectOrStartTests   → grün (10/10)
dotnet test src/AiNetLinter.IntegrationTests --filter ~ThinClientProxySessionContractTests|~RunnerAndProcessCallsites… → grün (3/3)
dotnet test src/AiNetLinter.IntegrationTests --filter ~ThinClientsSharedWarmthProcessContractTests    → grün (1/1, 1 m 43 s)
dotnet test src/AiNetLinter.IntegrationTests --filter ~TwoDaemonProcessesOnOneEndpoint…|~ProductionColdLoad_BrokenSlnx… → grün (2/2, Stichprobe der ehemals kontaminierten Tests)
```

Nach den eigenen Läufen blieben keine AiNetLinter-Prozesse zurück.

## Sonstige Beobachtungen / MINOR / NITPICK

- `src/AiNetLinter.IntegrationTests/Mcp/Platform/McpRawWireTestHarness.cs:247` — [MINOR] [Rules] Neuer Kommentar referenziert „Konzept B.6“ (Task-Artefakt-Referenz, Richtlinien §5); Testcode, daher nicht verdict-wirksam. **Hinweis:** Beim nächsten Berühren dieser Datei ID-frei umschreiben.
- `step-014/step-plan.md` — [MINOR] [Plan] Tests-/DoD-Checkboxen wurden nicht angekreuzt (nur `status: done (pending audit)` gesetzt), anders als die step-013-Konvention; die Belege stehen vollständig im Result.
- `step-014/step-result.md` (Geänderte Dateien) — [NITPICK] Tippfehler „AiNetLiner.IntegrationTests“ (2×, fehlendes „t“).
- Coder-Beobachtung „Completed=true trotz Caller-Cancel-Race“ bestätigt, aber folgenlos (beide Wege Exit 0); N4-Laufzeit (~1,7 min) ist für einen echten Roslyn-Zwei-Prozess-Lauf angemessen und dokumentiert.
- Bewertung der offenen Coder-Punkte: Der Seam (`RunSessionAsync`/`ThinClientSessionOptions`) ist größer als das Plan-Minimum, aber jede Injektionsstelle (Connect, Spawn, Idle-Timeout, Stdio) ist durch einen der fünf geforderten Nachweise zwingend; die Alternative (Impostor am echten Endpunkt) war in step-013 bereits als Interferenzklasse ausgeschlossen — kein Rückbau, keine Scope-Überschreitung.

## Tech-Debt-Einträge aus diesem Review

- `TD-008` (siehe `tech-debt.md`) — Überlebende Fremd-Daemons am benutzergebundenen Endpunkt sind eine empirisch belegte, suite-weite Flakiness-Quelle; suite-weites Cleanup/Gating-Fixture fehlt (Priorität mittel).
