---
status: done
type: step-result
task: 11_epic-projektregistry-und-daemon
step: 005
epic: EPIC-A
step_type: single
coded_by: coder
coded_by_model: GPT-5
coded_by_model_knowledge_cutoff: nicht deklariert
coded_at: 2026-08-23T23:05:00+02:00
code_commit_hash: a50bff9a
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 005: FAILED-Freigabe und Registry-Reservation atomar absichern

## Ergebnis

Beide Findings aus `step-004/step-review.md` sind behoben:

- `ProjectLease` führt eine explizite Markierung für die tatsächlich erzeugte
  `PROJECT_LOAD_FAILED`-Antwort. Nur der LoadFailed-Zweig von
  `ProjectToolCall.ExecuteAsync` markiert unmittelbar vor dem Lease-Release;
  ein Loading-Lease kann die FAILED-Freigabe nicht mehr allein auslösen.
- Resident-Lookup und per-Key-Reservation sind in einem kurzen Registry-Lock
  gekoppelt. Factory, IO, LoadTask und Solution-Load bleiben außerhalb des
  Locks. Ein nicht publizierter Creation-Verlierer wird nach dem Lock genau
  einmal über `retired` disposed; die Gewinnerinstanz wird nicht berührt.

Die deterministischen Tests decken den Fault zwischen Loading-Antwort und
Release sowie das atomare Creation-Interleaving ab. Der Registry-Test weist
exakt einen Factory-Aufruf, einen Load und eine spätere Server-Disposal aus;
der bestehende Other-Root-Lock-Hygiene-Anker blieb erhalten. Der Overview-Code
und die übrigen ausdrücklich ausgenommenen Verträge wurden nicht geändert.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Projects/ProjectLease.cs`
- `src/AiNetLinter/Mcp/Projects/ProjectEntry.cs`
- `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs`
- `src/AiNetLinter/Mcp/Projects/ProjectToolCall.cs`
- `src/AiNetLinter.FastTests/Mcp/Projects/ProjectRegistryTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Projects/ProjectRegistryTestDoubles.cs`
- `src/AiNetLinter.FastTests/Mcp/OverviewResourceLeaseContractTests.cs`
- `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandContractTests.cs`

## Abweichungen und Nachweise

Die fachliche Umsetzung weicht nicht vom Step-Plan ab. Der gemeinsame
Registry-Test-Harness wurde wegen des MCP-MaxLineCount-Gates in die neue Datei
`ProjectRegistryTestDoubles.cs` ausgelagert. Der bestehende Overview-Vertragstest
wurde auf die explizite Tool-Antwortmarkierung ausgerichtet; die
Overview-Produktion bleibt unverändert.

Gezielte Nachweise:

- Registry-/Lease-Unit-Slice: 15/15 grün.
- `McpServerCommandContractTests`: 16/16 grün.
- Abschluss-Build: 0 Warnungen, 0 Fehler.
- Abschluss-FastTests: 1681/1681 grün.
- Abschluss-IntegrationTests: 351/351 grün.
- Stress-Tests und Drift-Audit: nicht ausgeführt, wie beauftragt.

## MCP-Quality-Gates

Vor dem Code-Commit auf dem endgültigen C#-Stand:

- `get_violations`: 0 Violations in `src/AiNetLinter` (630 Dateien),
  `src/AiNetLinter.FastTests` (195 Dateien) und
  `src/AiNetLinter.IntegrationTests` (98 Dateien).
- `safeguard`: 10,00/10, Threshold 8,00, PASS; 0 Top-Verstöße.
- `metrics_lookup`: 11/11 angeforderte geänderte Produktions-/Test-Symbole
  aufgelöst; alle LineCount-, Komplexitäts-, Parameter-,
  AIContextFootprint- und PublicMember-Gates OK.
- Die semantische Impact-/Feature-Prüfung erfolgte MCP-first; zusätzliche
  Root-/Loader-/Health-Änderungen waren nicht erforderlich.

## Commits

1. `a50bff9a` — `fix: MCP-Races atomar absichern [11_epic-projektregistry-und-daemon]`
   mit `Refs: tasks/mcp-server-weiterentwicklung/11_epic-projektregistry-und-daemon/step-005`.
2. Der Doku-/Artefakt-Commit folgt nach diesem Ergebnis- und Codemap-Update.

Kein Push und keine Historienmanipulation; der fremde untracked Step-13-Ordner
wurde nicht angefasst oder gestaged.
