# Ausführungs-Roadmap: decompiled-assembly-analysis-finish2

status: executing
current_epic: EPIC-C
correction_round: 2
recent_finding_signatures: TTL-TICK-NORMALIZATION-REGRESSION
cycle_state: correction-required
last_commit: 118ccb94
primary_task: Schließe die dekompilierte Assembly-Analyse mit begrenzten Pfaden, Ressourcenverträgen, Cross-Assembly-Navigation und belastbaren Regressionen ab.
tech_debt: siehe tech-debt.md

## Aktueller Stand

- EPIC-A: done, nach Korrekturrunde 1 `approved`.
- EPIC-B: residual P1 nach drei ernsthaften Versuchen; keine weitere
  automatische Schleife für dieses Finding.
- Die beim Resume vorgefundene, bereits committete Arbeit `118ccb94` behebt
  vier dokumentierte P2-Punkte und implementiert die zwei EPIC-E-Teilaufträge;
  sie wird als tatsächlicher Arbeitsstand erhalten und unabhängig geprüft.
- Nächster unabhängiger Arbeitsblock: die ausstehende EPIC-C-Folgeprüfung;
  EPIC-B bleibt bis zu einer späteren expliziten Wire-Form-/Payload-Budget-
  Entscheidung offen.

## EPIC-A — Stabile In-Memory- und Dateipfade

- Ziel: Assembly-Sessions für leere relative Pfade, generierte Dokumente,
  Parametermethoden und Stable-IDs über die vorgesehenen Assembly-Tools
  konsistent und fachlich begrenzt nutzbar machen.
- Abhängigkeiten: bestehende Assembly-Session-/Provider-Komposition und
  vorhandene Stable-ID-/Snapshot-Verträge.
- Betroffene Bereiche: Assembly-Analyse-Session, Symbol-/Datei-/Call-Tree-
  Tools, Resolver, Fixtures und Assembly-Regressionstests.
- Muss-/Akzeptanzkriterien: Kein gültiger Assembly-Aufruf scheitert am leeren
  physischen Basis- oder `relativeTo`-Pfad; generierte Dokumente sind über
  `get_file_skeleton` erreichbar oder liefern eine präzise Unsupported-Antwort;
  Parametermethoden und Stable-IDs sind konsistent; fremde/ungültige IDs werden
  kontrolliert als fachlicher Fehler ausgegeben.
- Verifikation: fokussierte Assembly-/Symbolgraph-Tests, synthetische Fixture,
  gezielter `get_violations`-Nachweis.
- Status: done

## EPIC-B — Begrenzte Diagnostik und Antwortverträge

- Ziel: `inspect_assembly`, `find_assembly_extensions` und `get_server_health`
  liefern strukturierte, begrenzte, vollständigkeitsmarkierte und lesbare
  Diagnose-/Referenzdaten.
- Abhängigkeiten: EPIC-A für stabile Assembly-Sessions.
- Betroffene Bereiche: Assembly- und Health-Modelle, Formatter, Tool-Handler,
  Wire-/E2E-Verträge und Größenbudget-Tests.
- Muss-/Akzeptanzkriterien: Counts, Samples, Completeness und Truncation sind
  maschinenlesbar; strukturierte und textuelle Antworten halten dieselben
  Grenzen ein; Health bleibt standardmäßig kompakt und Detaildaten sind nur
  explizit sowie begrenzt verfügbar; `complete` plus Diagnostics projiziert
  `partial` konsistent.
- Verifikation: Response- und E2E-Tests für Root-/transitive Diagnostics,
  Truncation und festgelegtes Payload-Budget; gezielter Violations-Check.
- Status: residual_open (P1; drei Versuche ausgeschöpft)

## EPIC-B-Restbefund nach drei Versuchen

- `DIAGNOSTICS-SAMPLE-BUDGET`: drei ernsthafte Implementierungs-/Korrektur-
  versuche ausgeschöpft; P1 bleibt im Wire-Shape bestehen.
- Status: nicht freigabefähig; keine weitere automatische Schleife für dieses
  einzelne Finding. Der Rest ist in `tech-debt.md` und `execution-log.md`
  vollständig mit Ursache, Evidenz und nächstem Ansatz dokumentiert.
- Nächste Ausführung: mit dem unabhängigen nächsten Arbeitspaket EPIC-C
  fortfahren; der Abschluss darf EPIC-B erst als vollständig werten, wenn der
  Restbefund später ausdrücklich behoben oder mit Nutzerentscheidung behandelt
  wurde.

- Evidenz: Commit `337ebe90`, Folge-Review vom 2026-08-31 und
  `TD-EPIC-B-005`/`TD-EPIC-B-010` in `tech-debt.md`.
- P2-Restpunkte (`maxDiagnosticBytes`, registrierter Health-Detail-E2E) sind
  separat als `accepted-deferred` dokumentiert; der Registry-Footprint bleibt
  als Projekt-Dead-Debt außerhalb dieses EPICs.

## EPIC-C — Ressourcen, Konfiguration und Lebensdauer

- Ziel: Disk-, Memory-, Parallelitäts-, Resident- und Idle-TTL-Limits über das
  vorhandene External-Source-/MCP-Settings-Modell und CLI-Overrides validiert
  konfigurieren und in Registry-/Snapshot-Lebenszyklen wirksam machen.
- Abhängigkeiten: EPIC-A; keine Vermischung von Projekt- und Assembly-Registry.
- Betroffene Bereiche: Optionsmodell, Loader/CLI, Registry, Leases,
  Materialisierung, Creation-Barrier und Ressourcen-/Race-Fixtures.
- Muss-/Akzeptanzkriterien: LRU/TTL und Kapazitätsverhalten sind unter Last
  nachvollziehbar; Acquire/Eviction schützt aktive Leases; Materialisierung
  ist budget- bzw. rollback-sicher; Producer- und Consumer-Cancellation sowie
  Dispose während der Erzeugung sind deterministisch.
- Verifikation: fokussierte TTL/LRU/Capacity/Lease/Race/Creation-Barrier-
  Tests; hohe Last nur in gezielten Stress-Tests; gezielter Violations-Check.
- Status: in_progress (Korrekturrunde 3: Tick-Normalisierungsregression)

## EPIC-C-Review — Korrektur umgesetzt, Folge-Review ausstehend

- Subagent: `01a05585-ab4e-7652-b391-96fd81ed6d95`; 43 Arbeitsbaum-Einträge,
  ohne Agenten-Commit.
- Ergebnis: gemeinsame validierte Disk-/Memory-/Parallelitäts-/Resident- und
  Idle-TTL-Limits, JSON-/CLI-/Daemon-/ThinClient-Verdrahtung, LRU/TTL/
  Capacity, Lease-Schutz, Parallelitätsslots, rollback-sichere Materialisierung
  sowie deterministische Creation-Cancellation-/Dispose-Verträge.
- Verifikation laut Rollenbericht: Build 0/0; EPIC-C-Fokus Fast 129/129,
  Integration 4/4; vollständige Nicht-Stress-FastTests 2256 bestanden,
  2 Skips; Integration 372/374. `get_violations` nur der bekannte
  `AssemblyAnalysisRegistry`-Footprint.
- Review: Korrekturrunde 1 hat vier ursprüngliche EPIC-C-Befunde behoben. Der
  Folge-Review gegen `017797d4` meldete ein P1-Race bei idle Assembly-LRU und
  einen P2-Rest beim Producer-Join; Korrekturrunde 2 hat beide bearbeitet.
  Der unabhängige Folge-Review gegen `dc002543` läuft. Das ausgeschöpfte
  EPIC-B-Finding bleibt unverändert.

## EPIC-C-Korrekturrunde 1

- Scope: `TD-EPIC-C-001` bis `TD-EPIC-C-005` — Source-Eviction/Reservation,
  Assembly-LRU-Owner-Lease, ThinClient-Handshake, Registrierungsfenster und
  Producer-Cancellation-Join.
- Regel: ein begründeter Versuch je Finding; Polling-Timeouts führen nicht zu
  einem Interrupt.

## EPIC-C-Korrektur Runde 1 — abgeschlossen, Review ausstehend

- Subagent: `01a055f3-1938-7f71-b288-7acdb9b00d76`; kein Agenten-Commit.
- Ergebnis: Source-Eviction/Identity-Deduplizierung und atomare Reservation-
  zu-Resident-Lease-Überführung, idle-only Assembly-LRU mit geschützter
  Analyse-Lease, rückwärtskompatibler ThinClient-Handshake, Reservation bis
  Registrierung sowie cancellation-aware Checkout und deterministischer
  Producer-Join/Host-Dispose.
- Verifikation laut Rollenbericht: Build 0/0; EPIC-C-Fokus Fast 51/51,
  Integration 7/7; vollständige FastTests 2263 bestanden, 2 Skips;
  Integration 373 bestanden mit den zwei bekannten externen
  Beschreibungstextfehlern. Review gegen den Korrekturstand steht aus.

## EPIC-C-Folge-Review — Restbefunde

- `TD-EPIC-C-001` und `TD-EPIC-C-004`: `fixed` bestätigt.
- `TD-EPIC-C-002`: P1 bleibt wegen fehlender atomarer Revalidierung zwischen
  Idle-Prüfung und Retirement offen.
- `TD-EPIC-C-003`: ursprünglicher P1 behoben; Präzisionsvergleich von
  TimeSpan-Ticks gegen Rohwerte als P2-Rest.
- `TD-EPIC-C-005`: ursprünglicher P2 weitgehend behoben; deterministischer
  Producer-Join bleibt wegen Dictionary-Entfernung vor `Complete()` offen.
- Zusätzliche P2-Testlücke: direkter Materializer-plus-Registry-E2E-Pfad.

## EPIC-C-Korrekturrunde 2

- Scope: `TD-EPIC-C-002` (P1, atomare Lease-Revalidierung zwischen Idle-
  Prüfung und Retirement) und `TD-EPIC-C-008` (P2, Creation-Join erst nach
  `Complete()`); `TD-EPIC-C-006`/`007` bleiben bewusst `accepted-deferred`.
- Regel: zweiter begründeter Versuch für die betroffenen Findings;
  Polling-Timeouts führen nicht zu einem Interrupt.

## EPIC-C-Korrektur Runde 2 — abgeschlossen, Review ausstehend

- Subagent: `01a05647-1568-72c3-b948-2250a4c241d8`; kein Agenten-Commit.
- Ergebnis: atomare Retirement-Ownership mit Registry-/Entry-Lock und
  geschützter aktiver Lease; `creation.Complete()` vor Entfernung aus dem
  Producer-Join-Set; deterministische Race-/Dispose-Regressionen.
- Verifikation laut Rollenbericht: Build 0/0; fokussierte FastTests 57/57,
  Integration 7/7; vollständige FastTests 2265 bestanden, 2 Skips;
  Integration 373/375 mit nur den bekannten Beschreibungstext-Verträgen
  `ambiguous` und `sortBy`; Änderungs-/Test-Impact 0 Violations.
- Review: frischer unabhängiger Folge-Review gegen `dc002543` läuft; EPIC-C
  wird erst danach als `done` markiert.

## EPIC-C-Folge-Review — Korrekturstand `dc002543`

- Rolle: unabhängiger Reviewer; keine Codeänderung und kein Agenten-Commit.
- Prüffokus: atomare Retirement-Ownership, aktive Lease-Sicherheit,
  Producer-Join nach `Complete()`, Regressionen und MCP-Violations.
- Übergabe: `approved` nur bei keinem belegten P0/P1; sonst begründete weitere
  Korrekturrunde je betroffenem Finding.
- Warteverhalten: Polling-Timeouts führen nicht zu einem Interrupt.

## EPIC-C-Folge-Review — Korrekturstand `017797d4`

- Rolle: unabhängiger Reviewer; keine Codeänderung und kein Agenten-Commit.
- Prüffokus: alle fünf `TD-EPIC-C-001` bis `TD-EPIC-C-005`, aktive Lease-
  Sicherheit, Reservation-/Registry-Atomizität, Daemon-Handshake,
  Cancellation-Join, Regressionen und MCP-Violations.
- Übergabe: `approved` nur bei keinem belegten P0/P1; sonst frische zweite
  Korrekturrunde für die betroffenen Findings.
- Warteverhalten: Polling-Timeouts führen nicht zu einem Interrupt.

## EPIC-D — Mehrere Assemblies und tolerante Analyse

- Ziel: Begrenzte, ausdrücklich anforderbare Referenzexpansion für `find_symbol`
  und Call-Tree sowie tolerante, herkunftssichere `find_references`-Antworten
  über Referenzgrenzen implementieren.
- Abhängigkeiten: EPIC-C für Session-/Lease-Limits, EPIC-B für Antwortstatus.
- Betroffene Bereiche: Assembly-Referenzauflösung, Dispatcher/Resolver,
  Symbol- und Call-Tree-Traversierung, Referenzdiagnostik und synthetische
  Mehr-DLL-Fixtures.
- Muss-/Akzeptanzkriterien: `includeReferences=false` bleibt unverändert;
  `true` ist explizit, begrenzt und mit Assembly-Herkunft; Call-Tree und
  `find_references` kennzeichnen Partialität/Diagnostics statt falscher
  Vollständigkeit; fehlende, zyklische und beschädigte Referenzen destabilisieren
  weder MCP noch Resident-Bestand.
- Verifikation: Root-Assembly mit zwei Referenz-DLLs, exklusiv referenzierter
  Typ, fehlende/zyklische/beschädigte Metadaten, Tiefen-/Anzahl-Limits,
  tolerante Fehlerfälle und Live-Szenarien; gezielter Violations-Check.
- Status: open (Teilimplementierung aus `118ccb94` wird nach EPIC-D geprüft)

## EPIC-E — Abgegrenzte Befunde, Regressionen und Abschluss

- Ziel: Klassen-/Member-Filter und definierte In-Memory-Metriken abschließen,
  historische Befunde sichtbar abarbeiten, sichere scope-nahe Qualitätsfunde
  behandeln und alle Abschlussverträge konservieren.
- Abhängigkeiten: EPIC-A bis EPIC-D.
- Betroffene Bereiche: Structure-/Metrics-Tools, historische Assembly-Tests,
  Dokumentation bei tatsächlichen Vertragsänderungen und betroffene Testdateien.
- Muss-/Akzeptanzkriterien: große Antworten sind serverseitig begrenzt und
  filterbar; SourceText-Metriken sind definiert oder als `unknown` markiert;
  alle historischen Befunde sind umgesetzt, regressionsgesichert, bereinigt
  oder mit Evidenz als Tech Debt fortgeführt; keine impliziten Änderungen am
  Projektpfad.
- Verifikation: explizite Konzept-Checkliste, vollständiger Build, vollständige
  Nicht-Stress-Testläufe, Assembly-Live-Wiederholung und Abschluss-Audit.
- Status: open

## Abschluss-Checkliste aus Konzept.md

- [ ] Synthetische Multi-DLL-Fixtures einschließlich fehlender, zyklischer und
  beschädigter/unvollständiger Metadaten
- [ ] Leere In-Memory-Pfade, generierte Dokumente, Parametermethoden und
  unbekannte Stable-IDs
- [ ] Begrenzte Root-/transitive Diagnostics, Counts, Samples, Completeness,
  Truncation und festgelegtes Payload-Budget
- [ ] Resident-Limit, TTL/LRU, Parallelität, Lease und konkurrierende Eviction
- [ ] Race-Entscheidung ohne Use-after-dispose oder Lease-Leak
- [ ] Provider-Join, wartender Consumer-Abbruch, Producer-Abbruch und Dispose
  während Erzeugung
- [ ] `find_symbol` mit `includeReferences=false` und `true`
- [ ] Tolerante, partielle `find_references`-Antwort mit Diagnostics
- [ ] Positive Routing-, Hierarchie- und Metrics-Regressionen
- [ ] Historische Befunde und übernommene Tech-Debt-Punkte triagiert
- [ ] `dotnet build`
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
- [ ] Synthetische Assembly-Live-Szenarien wiederholt
- [ ] Gezielter MCP-Abschluss-Audit ausgeführt
