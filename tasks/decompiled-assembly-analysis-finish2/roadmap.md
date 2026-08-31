# Ausführungs-Roadmap: decompiled-assembly-analysis-finish2

status: executing
current_epic: EPIC-B
correction_round: 0
cycle_state: normal
last_commit: b0ebc8b4
primary_task: Schließe die dekompilierte Assembly-Analyse mit begrenzten Pfaden, Ressourcenverträgen, Cross-Assembly-Navigation und belastbaren Regressionen ab.
tech_debt: siehe tech-debt.md

## Aktueller Stand

- EPIC-A: done, nach Korrekturrunde 1 `approved`.
- EPIC-B: in_progress, Implementierung seit Baseline `b0ebc8b4` abgeschlossen;
  Review steht aus.
- Korrekturrunde: 0 für EPIC-B.

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
- Status: in_progress

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
- Status: open

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
- Status: open

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
