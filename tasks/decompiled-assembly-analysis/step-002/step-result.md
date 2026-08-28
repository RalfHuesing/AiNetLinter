---
status: done (pending audit)
type: step-result
task: decompiled-assembly-analysis
step: 002
epic: EPIC-01
step_type: single
coded_by: coder
coded_by_model: gpt-5
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-28T12:29:35+02:00
code_commit_hash: 7cbc6d45
status_after: done
blocker_category: n/a
---

# Result Step 002: MCP-Workflow-Regel auf den neuen Target-Vertrag synchronisieren

## Zusammenfassung

Die dauerhaft angewandte MCP-Workflow-Regel beschreibt jetzt den einheitlichen
`targetType`-/absoluten-`targetPath`-Vertrag. Die optionale Target-Ausnahme ist
auf `get_server_health` begrenzt; Feedback bleibt targetlos und
`projectRoot` wird nur noch bei den Resource-URIs genannt. Der bestehende
Agent-Guide-Vertragstest prüft zusätzlich den metadata-only Assembly-Scope und
die exakte Gleichheit mit der eingebetteten Workflow-Regel.

## Geänderte Dateien

- `.agents/rules/AiNetLinter-McpWorkflow.mdc` — Target-, Resource-URI- und metadata-only Assembly-Vertrag synchronisiert.
- `Docs/configuration.md` — veralteten optionalen Consumer-Kontext aus der Assembly-Beschreibung entfernt.
- `src/AiNetLinter.FastTests/Mcp/McpAgentGuideRegistrationTests.cs` — eingebettete Workflow-Regel und Bootstrap-/Regel-Vertrag gegen denselben Inhalt geprüft.

## Commit

- **Code-Commit-Hash:** `7cbc6d45`
- **Message:**
  ```
  fix(doku): Target-Regel synchronisieren [decompiled-assembly-analysis]

  Refs: tasks/decompiled-assembly-analysis/step-002
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit folgt.

## Build-/Test-Output

- `dotnet build` — grün, 0 Warnungen, 0 Fehler.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` — grün, 1.857 Tests, 0 Fehler.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` — grün, 360 Tests, 0 Fehler.

## Abweichungen vom Plan

Keine — der Korrekturumfang wurde 1:1 umgesetzt. Die bereits vorhandene
Einbettung in `src/AiNetLinter/AiNetLinter.csproj` blieb unverändert; der
Vertragstest prüft die daraus gelesene Ressource direkt gegen den
Agent-Guide-Abschnitt.

## Beobachtungen

Keine außerhalb des vorgegebenen Korrekturumfangs. Es wurde kein
Tech-Debt-Eintrag angelegt und `codemap.md` nicht verändert, weil keine neue
oder strukturell geänderte CodeMap-Architektur entstanden ist.

## Bekannte Unschärfen

Keine bekannten Unschärfen im Korrekturumfang. Die Assembly-Registry bleibt
entsprechend dem Review weiterhin ein späterer Scope; dieser Step dokumentiert
ausschließlich den bereits implementierten metadata-only Spezialtool-Vertrag.
