---
status: open
type: step-plan
task: 11_epic-projektregistry-und-daemon
step: 005
corrects: step-004
title: "FAILED-Freigabe und Registry-Reservation atomar absichern"
epic: EPIC-A
estimated_risk: high
step_type: single
items: []
created_by: orchestrator
created_by_model: GPT-5
created_by_model_knowledge_cutoff: nicht deklariert
created_at: 2026-08-23T22:35:00+02:00
related_to: ["step-004/step-review.md", "step-004/step-plan.md", "step-004/step-result.md"]
---

# Step 005: FAILED-Freigabe und Registry-Reservation atomar absichern

## Bezug und Scope

- **Task:** `11_epic-projektregistry-und-daemon`
- **Epic:** EPIC-A
- **Korrekturquelle:** `step-004/step-review.md`, Findings 1–2.
- Dieser Step ist der mechanische Korrekturpfad: Er bearbeitet ausschließlich
  die beiden exakt lokalisierten Race-Fenster aus dem Review. Overview-
  Rendering, Loader-Formatierung, Health-Snapshot-Semantik und sonstige
  Beobachtungen bleiben unverändert.

## Aktueller Zustand

- `ProjectRegistry.ReleaseEntry` setzt derzeit `FailureLeaseReleased` allein
  aufgrund des beim Release sichtbaren `LoadFailed`-Zustands. Ein Lease, das
  `Loading` beantwortet, kann dadurch nach einem zwischenzeitlichen Fault
  fälschlich die FAILED-Freigabe auslösen, bevor eine echte
  `PROJECT_LOAD_FAILED`-Antwort erzeugt wurde.
- Der Toolpfad baut die Loading-/LoadFailed-Antwort in
  `ProjectToolCall.ExecuteAsync` auf und gibt den Lease anschließend frei.
  Nur der tatsächliche LoadFailed-Antwortpfad darf künftig die eindeutige
  Freigabemarkierung setzen.
- `ProjectRegistry` führt Resident-Lookup und `ReserveCreation` in getrennten
  Lock-Abschnitten aus. Zwischen beiden Abschnitten kann ein anderer Caller
  publizieren und die Reservation entfernen; der pausierte Caller erzeugt
  dann einen zweiten Server. Der `raced`-Pfad entsorgt diesen Verlierer
  derzeit nicht sicher.

## Korrekturentscheidungen

### 1. FAILED-Marker nur durch die echte Fehlerantwort freigeben

- Führe für `ProjectLease`/`ProjectEntry` eine eindeutige, explizite
  Antwort-Markierung ein (z. B. `MarkLoadFailedResponseEmitted`), die nur
  unmittelbar vor dem Release im `LoadFailed`-Zweig von
  `ProjectToolCall.ExecuteAsync` gesetzt wird.
- `ReleaseEntry` darf die FAILED-Freigabe nicht mehr aus dem allgemeinen
  `LoadState` ableiten. Ein Lease, das `Loading` geliefert hat, setzt niemals
  die Markierung, auch wenn der Hintergrund-Load bis zum Release faulted.
- Solange kein Fehlerantwort-Lease markiert und freigegeben wurde, bleibt der
  FAILED-Entry resident und kann weder durch `FindAdoptable` noch durch den
  Eviction-Tick vorzeitig ersetzt werden. Nach der markierten Freigabe darf
  der nächste Aufruf genau einen frischen Retry starten.

### 2. Resident-Lookup und Reservation in einem Lock-Abschnitt koppeln

- Ersetze die getrennte Lookup-/Reserve-Folge durch einen atomaren Registry-
  Abschnitt: unter `gate` entweder den bestehenden Entry adoptieren oder die
  per-Key-Reservation eintragen. Der Abschnitt enthält weiterhin keinerlei
  Factory-Aufruf, IO, `LoadTask`-Warten oder Solution-Load.
- Factory-Kick-off und Publish bleiben außerhalb des Locks. Der Publish prüft
  den Reservation-Key erneut; ein bereits publizierter Gewinner wird von
  konkurrierenden Aufrufern adoptiert.
- Ein bereits erzeugter, nicht publizierter Verlierer wird nach Verlassen des
  Locks deterministisch disposed; er darf weder in `retired` verloren gehen
  noch seinen Hintergrund-Load neben der Gewinnerinstanz weiterführen.
- Der bestehende Single-Flight-/Reservation-Vertrag, die Kanonisierung und
  die Bedienbarkeit anderer Roots bleiben erhalten. Kein negatives Caching
  und kein synchrones Warten auf den Solution-Load.

## Akzeptanzkriterien

1. Ein deterministischer Test faultet den Load unmittelbar zwischen der
   Loading-Antwort und dem Release dieses Leases. Der nächste Aufruf liefert
   zwingend zuerst `PROJECT_LOAD_FAILED` mit Originalmeldung und Hint; erst
   der darauffolgende Retry erzeugt eine neue Instanz.
2. Ein Loading-Lease kann niemals allein `FailureLeaseReleased` setzen; ein
   echter LoadFailed-Antwortpfad setzt die Markierung genau einmal vor seinem
   Release. Mehrere laufende Fehlerantworten behalten den Entry bis zum
   letzten markierten Release.
3. Ein deterministischer Test pausiert einen Caller zwischen Resident-Lookup
   und der bisherigen Reservation-Stelle. Der Test weist nach, dass dieser
   Interleaving-Punkt nach der Korrektur nicht mehr zwei Factory-Aufrufe,
   Server oder Background-Loads erzeugt.
4. Jeder erzeugte, nicht publizierte Reservation-Verlierer wird genau einmal
   außerhalb des Registry-Locks disposed; keine fremde residente Instanz wird
   dabei disposed. Factory-, Load- und Dispose-Zähler sind im Test exakt.
5. Ein anderer kanonischer Root bleibt während einer blockierten Creation oder
   eines Background-Loads leasebar; Registry-Lock und Solution-Load bleiben
   getrennt.

## Tests und Verifikation

- Gezielte Iteration: betroffene Unit-/Contract-Tests per Testnamefilter bzw.
  `Category=Unit`; keine globale Testserialisierung und keine Stress-Tests.
- Abschluss genau einmal nach erfolgreicher gezielter Iteration:
  `dotnet build`,
  `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`,
  `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`.
- Vor jedem Commit AiNetLinter-MCP-Quality-Gates für die geänderten Scopes:
  `get_violations`, `safeguard`, `metrics_lookup` und bei Bedarf
  `get_impact`/`get_feature_context`.
- Keine Drift-Audit-Ausführung in diesem Step; sie bleibt einmalige
  Epic-Abschlussaktivität.

## Definition of Done

- [ ] Beide Findings aus `step-004/step-review.md` sind mit Code und
  deterministischen Race-Tests behoben.
- [ ] Loading-Lease und LoadFailed-Antwort-Lease sind unterscheidbar; der
  FAILED-Marker wird ausschließlich durch die echte Fehlerantwort freigegeben.
- [ ] Resident-Lookup/Reservation ist atomar, Factory/IO bleibt außerhalb des
  Locks, und jeder Creation-Verlierer wird sicher entsorgt.
- [ ] Build und beide Nicht-Stress-Testprojekte sind genau einmal als
  Abschlusslauf grün; Stress bleibt unberührt.
- [ ] `step-005/step-result.md` ist mit Abweichungen, Nachweisen und
  MCP-Gates geschrieben; `codemap.md` gepflegt; dieser Plan steht danach auf
  `done (pending audit)`.
- [ ] Coder erstellt zwei gezielte Commits: zuerst Code/Tests, danach
  Doku/Artefakte. Keine Historienmanipulation und kein Push.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#agent-resilience` — kein Blocking auf
  `Task`-Loads; Registry-Lock darf keine IO-/Await-Phase umfassen.
- `.agents/rules/AiNetLinterRichtlinien.mdc#1` — C#-Symbole und Impact vor
  Änderungen über den projektgebundenen AiNetLinter-MCP prüfen.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4` — deterministische xUnit-v3-
  Tests, gezielte Iteration und genau ein vollständiger Nicht-Stress-Lauf.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5` — explizite Result-/Fehler-
  verträge, Zero-Warning-Gate und keine Artefakt-IDs in Produktionskommentaren.

## Ausdrückliche Nicht-Scope-Punkte

- Keine Änderungen am Overview-Lease, am gemeinsamen LoadFailed-Descriptor,
  an Root-/Loader-Texten oder an der Health-Snapshot-Aggregation.
- Keine Roadmap-/Meilenstein-Doku-, Drift-Audit- oder Epic-B-Abschlussarbeit;
  diese bleiben nach erfolgreicher Korrektur dem regulären Epic-Abschlussstep
  vorbehalten.
