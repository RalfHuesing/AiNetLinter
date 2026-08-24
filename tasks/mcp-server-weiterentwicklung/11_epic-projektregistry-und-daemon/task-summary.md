---
task: 11_epic-projektregistry-und-daemon
completed_at: 2026-08-24T13:25:00+02:00
final_status: done  # done | aborted
total_iterations: 15
total_commits: 66
total_epics: 2
total_tech_debt_entries: 10
---

# Task Summary: 11_epic-projektregistry-und-daemon

## Ergebnis

Beide Epics sind vollständig umgesetzt und der getestete Code-Stand ist seit dem
step-015-Vollstack unverändert (`7a1431d9` Code; danach nur Doku-/Task-Commits
`9e12a0a3`, `124b6c2f`). EPIC-A liefert die transportneutrale Projektregistry mit
Definitionsdatei `ainetlinter.project.json`, absoluter `projectRoot`-Pflicht, harten
Fehlerverträgen, TTL/LRU-Eviction mit Busy-Guard/Pending-Adoption und dem
zweistufigen Zustandsvertrag; das eigene Repo (Definitionsdatei, AGENTS.md-Ritual,
`.mcp.json`) ist migriert. EPIC-B verlagert die Registry in den Daemon
(Named-Pipe-Transport mit Handshake/Versionsvergleich, DaemonHost mit Idle-Exit und
MRU-Warmup, ThinClient mit Connect-or-Start, genau-ein-Replay, Hänger-Schutz und
Escape-Ventil); Health/Observability weisen Modus/Verbindungen/PID/Keys aus. Die
Nutzerentscheidung „Option A" vom 2026-08-24 (stderr-[WARN]-Ereignisse als AK-5-
Ereignis) ist im Konzept verankert (`41169b72`, Konzept B.3/B.6) und in der
Umsetzung (unterscheidbare Ereignis-Signaturen) abgebildet.

## Roadmap-Status

Beide Epics (`EPIC-A`, `EPIC-B`) sind abgehakt; kein Epic ist obsolet. Alle
Subitems A.1–A.9 und B.1–B.7 tragen inline `[x] erledigt → step-NNN`-Annotationen
mit Step-Nachweisen. Kosmetischer Rest: mehrere EPIC-A-Subitem-Zeilen stehen noch
auf `- [ ]`, obwohl Parent-Epic abgehakt und Umsetzung belegt ist (EPIC-B-Subitems
durchgängig `[x]`) — reiner Checkbox-Drift, kein offener Umfang.

## Steps-Übersicht

| Step | Epic | Status | Title | Commit | Notiz |
|------|------|--------|-------|--------|-------|
| step-001 | EPIC-A | done | Registry-Grundlage: Definitionsdatei, Loader, Fehlerverträge | `e0b25033` | approved |
| step-002 | EPIC-A | done | Registry-Kern: Lease, Entry, Eviction, FAILED-Marker | `a80ec821` | approved |
| step-003 | EPIC-A | done | MCP-Wiring auf die Projektregistry | `ccf7b33a` | issues → korrigiert in step-004 |
| step-004 | EPIC-A | done | Kalt-Load, Erstzugriffs-Dedupe, leasegeschützte Overview | `2ed8bcc0` | issues → korrigiert in step-005 |
| step-005 | EPIC-A | done | FAILED-Freigabe und Reservation atomar | `a50bff9a` | issues → korrigiert in step-006 |
| step-006 | EPIC-A | done | Race-Interleavings deterministisch verankern | `05b2e157` | issues → korrigiert in step-007 |
| step-007 | EPIC-A | done | Originalfehler und Creation-Loser im Testvertrag | `73695524` | approved (schließt 003–006-Kette) |
| step-008 | EPIC-A | done | EPIC-A-Abschluss: Live-Prüfung, Drift-Audit, Meilenstein-Doku | `3c01d78a` | approved |
| step-009 | EPIC-B | done | Transport-/Handshake-Grundlage | `a6a6c40d` | approved |
| step-010 | EPIC-B | done | DaemonHost-Lifecycle, Idle-Exit, MRU-Warmup | `424a781b` | issues → korrigiert in step-011/012 |
| step-011 | EPIC-B | done | DaemonHost-Korrektur (Exklusivität, MRU, Registration-Race) | `1c7ee714` | issues → korrigiert in step-012 |
| step-012 | EPIC-B | done | Direkte Prozess-Contracts (Doppelstart, Host/MCP-Pipe) | `ffb60157` | approved |
| step-013 | EPIC-B | done | ThinClient: Connect-or-Start, Pump, Retry/Hänger, Health, Migration | `b9605ea5` | issues → korrigiert in step-014 |
| step-014 | EPIC-B | done | F1/F2-Korrektur: fünf Contract-Nachweise + Timeout-Signatur | `683a3e4f` | approved |
| step-015 | EPIC-B | done | Task-weites Drift-Audit (Duplicates/Magic Values/Dead Code) | `7a1431d9` | approved |

Alle 15 Steps sind fachlich abgeschlossen; die sieben `issues`-Verdicts wurden
jeweils durch Folge-Steps geschlossen und dort `approved`.

## Globale Audit-Befunde (Kritiker, Modus `global`)

### Konzept erfüllt?

Ja — Muss-Habens aus A.1–A.9 und B.1–B.7 sind im Endstand nachweisbar, gestützt
auf die Step-Reviews und eigene Stichproben am aktuellen Code:

- **A.3 harter Cut:** `LinterArgs.cs:281/287` lehnen `--path`/`--config` im
  MCP-Modus mit hartem Fehler ab; Batch-Optionen existieren unverändert weiter.
  Flag-Validierung (`--mcp-project-ttl-minutes`, `--mcp-max-projects`,
  `--mcp-daemon-idle-exit-minutes`) aktiv.
- **A.2/A.6/A.9 Migration:** `ainetlinter.project.json` im Repo-Root vorhanden;
  AGENTS.md-Abschnitt „AiNetLinter-MCP: Initialisierung" vorhanden;
  Repo-`.mcp.json` auf `command + --mcp-server` reduziert.
- **A.4/F6:** projectRoot-/Definitionsdatei-Vertrag einmalig in
  `ServerInstructions.Text`; `MaxUtf8Bytes = 2_557` unverändert eingehalten.
- **B.2/B.3/B.5/B.6:** Handshake-/Versionslogik, genau-ein-Replay
  (`ThinClientProxy.MaximumRetries = 1`), unterscheidbare stderr-[WARN]-Signaturen,
  Escape `AINETLINTER_NO_DAEMON=1`, Health-Felder — alles im aktuellen Stand
  verifiziert; die fünf step-014-Nachweise schließen den B.6-Testkatalog.
- **AK-5 „Option A":** Konzept B.3/B.6 verankert (`41169b72`), Umsetzung
  signaturhaltig, vom step-015-Diff unberührt.
- **Non-Goals respektiert:** Kein HTTP/TCP/Auth, kein DI/Service, keine neuen
  Tools (26er-Vertrag laut step-008-Liveprüfung), Batch-Pipeline unberührt, kein
  FileSystemWatcher, keine Deprecationsschichten.
- Wiederöffnungsvermerke §D.4 (EPIC-A) und §C.5 (Daemon-Status) stehen in
  `tasks/mcp-server-weiterentwicklung/90_bewusst-nicht-umsetzen/Konzept.md`.
- Doku-Sammlung abgedeckt: `Docs/agent-api.md` (projectRoot/Definitionsdatei),
  `Docs/configuration.md` (neue Flags inkl. `--mcp-daemon-idle-exit-minutes`),
  `Docs/integration.md` (Abschnitt „Daemon-Transport: interner Hostpfad" mit
  ThinClient/Escape/Health), `Docs/ROADMAP.md` (EPIC-A-Meilenstein),
  `README.md` (Nutzungsmodus/Daemon), `00_uebersicht` Zeile 11 gepflegt.

Keine CRITICAL-/MAJOR-Findings auf Task-Ebene. MINOR-Beobachtungen: (1) Roadmap-
Checkbox-Drift bei EPIC-A-Subitems (siehe oben); (2) die Hermes-`config.yaml`-
Registrierung ist formal korrekt reduziert (`command + --mcp-server`), steht aber
aktuell auf `enabled: false` — Nutzer-Toggle, kein Migrationsdefekt, da das Repo-
`.mcp.json` den Endstand nutzt.

### Seiteneffekte / Regressionen

- `dotnet build` (Gesamtprojekt, heute): **grün, 0 Warnungen, 0 Fehler** —
  eigener Sanity-Lauf dieses Audits.
- Testbeleg ohne Rerun (Effizienzvorgabe): Der vollständige Nicht-Stress-Stack
  lief in step-015 auf **identischem Codestand**: FastTests 1726/1726 grün;
  IntegrationTests 357/359 mit 2 klassifizierten TD-008-Kontaminationen
  (`DaemonHostProcessContractTests`, `DaemonHostMcpProcessContractTests` — beide
  isoliert 1/1 grün). Die Klassifikation ist dokumentiert, mechanisch plausibel
  (statisches Endpoint-Semaphor wirft exakt `OperationCanceledException`) und vom
  step-015-Review akzeptiert. Stress wurde nie ausgeführt; kein Drift-Audit-Rerun.

### Rules-Konformität (Stichproben)

Vertieft an drei Steps, jeweils gegen den aktuellen Stand statt nur die Reviews:

- **step-003 (harter Cut):** MCP-Modus verwirft `--path`/`--config` deterministisch
  mit Bauanleitung im Fehlertext; Batch-Pfad unangetastet (`CliOptionFactory`
  bleibt Batch-seitig). Regelkonform (Zero-Warning, Result-/harte-Fehler-Verträge).
- **step-013/014 (ThinClient-Fachlichkeit):** F2-Fix nachvollzogen
  (`DaemonBytePump.cs:148–151` — erreichbarer Timeout-Zweig mit Hänger-Signatur);
  genau-ein-Retry (`MaximumRetries = 1`), unterscheidbare [WARN]-Signaturen
  (Replay vs. kein weiterer Retry vs. Konfigurationsdivergenz), Escape-Ventil
  vorhanden. step-014-Review bestätigte zusätzlich sealed/no-empty-catch/
  keine Serialisierungs-Collections/TestTempDirectory über alle neuen Dateien.
- **step-001/-002 (Registry-Grundlagen):** Review-Dokumentation zu Grenzwerten
  (`get_violations` 0 Verstöße, `safeguard` 10,00/10, Metrikbudgets) konsistent;
  TD-001–TD-005 zeigen, dass der Kritiker-Kanal tatsächlich genutzt wurde.

Keine Rules-Verletzung gefunden, die den Task-Abschluss berührt.

## Tech-Debt-Zusammenfassung

Aggregation aus dem Index oben in `tech-debt.md` (Volltext bleibt dort,
Pointer-Prinzip):

- **Hoch:** 0 Einträge
- **Mittel:** 3 Einträge — `TD-001` (stummer Default-Fallback bei defekter rules.json
  im Registry-Pfad), `TD-004` (Soft-Cap über MaxProjects bei nur-busy Register),
  `TD-008` (überlebende Fremd-Daemons als suite-weite Flakiness-Quelle am
  benutzergebundenen Pipe-Endpunkt)
- **Niedrig:** 7 Einträge — `TD-002`, `TD-003`, `TD-005`, `TD-006`, `TD-007`,
  `TD-009`, `TD-010`

Alle 10 Einträge sind `auto_fixable: nein` und Status `offen`. Hinweis (keine
Entscheidung vorwegnehmend): TD-008 ist die einzige mittel-Eintragsquelle
bleibender Vollstack-Unruhe (2 kontaminierte Integrationstests im letzten Lauf);
TD-010 ist ein abgrenzbarer Doku-Fix.

## Offene Punkte

- [ ] TD-007/TD-008/TD-009/TD-010 sind dem Nutzer zur Entscheidung vorbehalten
      (Auftragsvorgabe) — Empfehlungsliste unten.
- [ ] TD-001/TD-004 (Priorität mittel) warten seit EPIC-A auf eine
      Konzept-/Kapazitätsentscheidung des Nutzers.
- [ ] Die zwei Integrationsausfälle im letzten Vollstack bleiben an TD-008
      gekoppelt, bis ein suite-weites Cleanup/Gating existiert (isoliert jeweils
      grün).
- [ ] Kosmetik: Roadmap-Checkboxen der EPIC-A-Subitems nachziehen; Hermes-
      Registrierung `enabled`-Zustand nach Nutzerentscheid setzen.

## Empfehlungen

1. **TD-008** als ersten Infrastruktur-Step eines Folge-Tasks priorisieren
   (suite-weites Cleanup-/Gating-Fixture für den benutzergebundenen Pipe-Endpunkt),
   bevor weitere Daemon-Pfad-Tests entstehen.
2. **TD-010** als kleinen Doku-Pflicht-Step erledigen (Tabellenzeile
   `AMBIGUOUS_SOLUTION` in `Docs/agent-api.md:834` an Realverhalten anpassen).
3. **TD-007** (Abdeckungsasymmetrie Escape- vs. Daemon-Pfad) bei der nächsten
   Testinfrastruktur-Runde zusammen mit TD-008 betrachten.
4. **TD-001/TD-004** als Konzeptnachtrag entscheiden (Fehlervertrag für defekte
   Regeldateien; Kapazitätsvertrag bei vollem Register), falls der Daemon-Multi-
   Projektbetrieb produktiv scharf geschaltet wird.
5. **TD-009** in einen späteren Refactoring-Step legen (zentrale Toolnamensquelle).

## Statistik

- **Anzahl Epics:** 2, davon abgehakt: 2
- **Anzahl Steps:** 15
- **Davon approved:** 8 (Endstand: alle 15 fachlich abgeschlossen, 7 issue-Steps
  durch Folge-Approvals geschlossen)
- **Davon blocked:** 0
- **Anzahl Commits:** 66 (task-getaggt, inkl. Plan-/Result-/Review-/Konzept-Commits)
- **Anzahl Tech-Debt-Einträge:** 10 (davon `auto_fixable: ja`: 0)
- **Davon Korrektur-Steps:** 7 (längste `corrects`-Kette: 4 / 3 —
  step-003→004→005→006→007; unter Nutzaufsicht zu approved-Endstand aufgelöst)
- **Laufzeit:** 2026-08-23T12:48 (+02:00) bis 2026-08-24T13:25 (+02:00)
  ≈ 24,5 Stunden über zwei Kalendertage
