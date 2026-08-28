---
task: decompiled-assembly-analysis
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-28T19:37:58+02:00
---

# Tech-Debt-Log: decompiled-assembly-analysis

Append-only. Dieser Log enthält Architektur-, Anti-Pattern- und
Duplikationsbeobachtungen außerhalb des jeweiligen Step-Scopes.

## Index

| ID | Bereich / Datei | Priorität | Auto-Fixable | Kurzfassung |
|---|---|---|---|---|
| TD-001 | `ExternalSourceMappingValidator` / `SourceSnapshotIdentity` | niedrig | nein | Identische private Drive-Path-Prüfung ist über zwei Vertragsgrenzen dupliziert. |

## Einträge

### TD-001 — Gemeinsame Drive-Path-Prüfung prüfen [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-008 (Kritiker-Review vom 2026-08-28)
- **Ort:** `src/AiNetLinter/Configuration/ExternalSourceMappingValidator.cs:374-375`; `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotModels.cs:157-158`
- **Befund:** Der Exact-DRY-Audit findet dieselbe private Funktion `value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':'` zweimal; jede Kopie wird jeweils einmal im eigenen Pfadnormalisierungsvertrag verwendet.
- **Warum nicht sofort gefixt:** Die Methoden liegen außerhalb des Resolvercodes und in den bereits abgeschlossenen Konfigurations- bzw. Snapshot-Identitätsverträgen. Eine gemeinsame Ablage würde mindestens beide Vertragsgrenzen und wahrscheinlich die bestehende `PathNormalizer`-API berühren.
- **Vorschlag:** Bei einer ohnehin anstehenden, vertraglich passenden Pfadnormalisierung prüfen, ob ein gemeinsamer interner Helper ohne neue öffentliche API und ohne Semantikänderung fachlich sinnvoll ist.
- **Auto-Fixable:** nein — die geeignete gemeinsame Ablage und der zulässige Vertragszuschnitt erfordern Architektur-Ermessen.
- **Status:** offen
