---
task: decompiled-assembly-analysis
type: follow-up-strategy
created_at: 2026-08-28T16:55:05+02:00
---

# Strategie für kontextbegrenzte Folge-Tasks

## Entscheidung

Die bisherige Vorgabe „größere Code-Pakete“ bleibt bestehen, wird aber als
größere vertikale Pakete innerhalb eines Epics interpretiert. Ein Agentenlauf
soll nicht gleichzeitig mehrere eigenständige Fachverträge, komplette
Lifecycle-Modelle und alle Folge-Epics bearbeiten.

## Split-Gate vor dem Coder

Der Planer teilt einen Step vor dem Coder, wenn mindestens eine Grenze
überschritten wird:

- mehr als ein primärer Fachvertrag oder mehr als zwei eng gekoppelte Verträge;
- mehr als drei unmittelbar betroffene Schichten;
- mehr als acht Akzeptanzkriterien;
- mehr als zwölf zentrale Dateien im initialen Leseumfang.

Das ist kein Mini-Paket-Sweep: Jeder Teil bleibt ein vertikaler, testbarer
Vertrag mit Implementierung, Adapter und Tests. Gemeinsame DRY-, MagicValues-
und DeadCode-Funde werden weiterhin in das passende Paket integriert.

## Kontext-Handoff

Jeder Step-Plan enthält:

- `context_budget.read_first` — die zentralen Dateien und Symbole;
- `context_budget.read_on_demand` — nur bei konkretem Bedarf zu ladende Dateien;
- `context_budget.out_of_scope` — bewusst nicht zu öffnende Bereiche;
- Invarianten, Risiken und den nächsten sicheren Einstiegspunkt.

Der Coder und der Kritiker starten mit diesem Handoff und laden keine
vollständige Solution pauschal. MCP-Symbol-, Referenz- und Impact-Abfragen
bleiben der bevorzugte Weg für C#-Semantik; Textsuche wird gezielt eingesetzt.

## Fortsetzungsregel bei Compact

Wenn ein Agent vor dem Abschluss in einen Kontext-Compact läuft, bleiben
Step-Plan, Handoff, Worklog (falls der Step länger als eine Sitzung läuft) und
bereits erzeugte Commits die Quelle für die Fortsetzung. Der Orchestrator
schließt den betroffenen Agenten und startet immer einen neuen Agenten für
denselben Rollenabschnitt. Kein bestehender Sub-Agent wird wiederverwendet.

## Empfohlene Epic-Schnitte

- EPIC-03: Mapping-Vertrag/Validierung und Snapshot-Auflösung als getrennte
  vertikale Pakete;
- EPIC-04: Gitea-Client/Auth/Fehlersemantik und Refresh/Cache/atomare
  Veröffentlichung als getrennte Pakete;
- EPIC-05: transitive Referenzen/Health und Capability-Routing als getrennte
  Pakete;
- EPIC-06: Dokumentation/Verträge und finale Verifikation als getrennte
  Pakete.

Der jeweilige Planer prüft diese Schnittlinie gegen den tatsächlichen Code und
darf sie begründet anpassen. Ein vollständiger Epic-Umbau bleibt jedoch ein
Signal zum Splitten, nicht zum Erhöhen des Kontextbudgets.
