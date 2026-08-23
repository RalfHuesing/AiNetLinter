---
status: done
type: step-result
task: 11_epic-projektregistry-und-daemon
step: 003
epic: EPIC-A
step_type: single
coded_by: coder
coded_by_model: GPT-5 (Orchestrator continuation after Coder interruption)
coded_by_model_knowledge_cutoff: nicht deklariert
coded_at: 2026-08-23T20:31:00+02:00
code_commit_hash: ccf7b33a
status_after: done
blocker_category: n/a
---

# Result Step 003: MCP-Wiring auf die Projektregistry

## Zusammenfassung

Das MCP-Wiring läuft jetzt über die ProjectRegistry: projektgebundene Tools
halten ihren Lease über den vollständigen asynchronen Aufruf, Health bleibt
aggregierbar und Overview nutzt das adressierbare URI-Template. Der MCP-Start
ist auf `--mcp-server` ohne `--path`/`--config` umgestellt; Definitionsdatei,
Registry-Flags, Fehlerverträge und zweistufiger Load-/Refresh-Zustand sind
verdrahtet und getestet. Die Integrationstest-Harnesses verwenden ebenfalls
Definitionsdateien und übergeben `projectRoot`.

## Geänderte Dateien

- `src/AiNetLinter/Commands/McpServerCommand.cs` und `src/AiNetLinter/Mcp/*ToolRegistrations.cs` — Registry-Komposition und Lease-Wiring aus den WIP-Commits fertiggestellt.
- `src/AiNetLinter/Mcp/Projects/*` und `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` — Root-Guard, Registry-Race-Entsorgung sowie Load-/Refresh-Zustand vervollständigt.
- `src/AiNetLinter/Cli/CliOptionFactory.cs` — invariant geparste Registry-Flags und harter MCP-Argumentvertrag.
- `src/AiNetLinter.FastTests/Mcp/**` — Contract-, Registry-, Overview- und Zustandsverträge einschließlich hängesicherer Cancellation-Fixtures.
- `src/AiNetLinter.IntegrationTests/Mcp/**` — MCP-Prozess-/Raw-Wire-Harness auf Definitionsdatei, Arbeitsverzeichnis und `projectRoot` migriert; Hard-Cut und fehlende Solution geprüft.
- `ainetlinter.project.json`, `.mcp.json`, `AGENTS.md`, `README.md`, `Docs/{agent-api,configuration,integration}.md` — eigene Repo-Registrierung und Fachvertrag dokumentiert.
- `tasks/.../codemap.md` — Step-003-Bereiche und Test-/Migrationsanker aktualisiert.

## Commit

- **Code-Commit-Hash:** `ccf7b33a`
- **Message:**
  ```
  fix: MCP-Registry anbinden [11_epic-projektregistry-und-daemon]
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit nach diesem Step-Result.

## Build-/Test-Output

```text
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (1678 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (350 Tests, 0 Fehler)
dotnet run --project src/AiNetLinter -- --sync-agent-rules-only → grün, AiNetLinter.mdc bereits aktuell
```

Zusätzliche MCP-Gates: `get_violations` für `src/AiNetLinter` meldet 0
Verstöße; `safeguard` meldet 10,00/10 mit 0 Top-Verstößen. Der aktuelle
`get_feature_context`-Check bestätigt für `ProjectRegistry.InsertResident`
und `ProjectToolCall.ExecuteAsync` Metriken innerhalb aller Budgets und 0
Datei-/Symbolverletzungen.

## Abweichungen vom Plan

- Die vorhandenen MCP-Integrationstests mussten als fachlich notwendige
  Migrationskorrektur auf den neuen Start-/Definitionsvertrag umgestellt
  werden; der alte Broken-Slnx-Test prüft nun deterministisch
  `SOLUTION_NOT_FOUND` aus der Definitionsdatei. Die alte Batch-Auflösung
  bleibt unverändert.
- Der vollständige Nicht-Stress-Stack wurde nach der letzten Registry-
  Lock-Hygiene-Korrektur erneut ausgeführt und ist grün.
- Nicht in diesem Step: `Docs/ROADMAP.md`, Overview-Liveprüfung in Hermes/
  Claude Code, §D.4-Wiederöffnungsvermerk und drift-audit; diese Punkte sind
  wie geplant step-004 zugeordnet.

## Beobachtungen

- Die resident gestartete MCP-Instanz war während der frühen Prüfung zunächst
  ein älterer Snapshot; nach Staleness-Refresh lieferten die semantischen
  Gates aktuelle Symbol-/Metrikdaten.
- Die externe Hermes-Datei `C:\Users\Ralf\AppData\Local\hermes\config.yaml`
  wurde außerhalb des Repositories auf ausschließlich `--mcp-server` für
  `ainetlinter` reduziert; sie ist daher nicht Bestandteil des Git-Commits.

## Bekannte Unschärfen

- Eine echte Host-Liveprüfung des Overview-URI-Template ist bewusst offen und
  wird im Abschluss-Step geprüft; die In-Memory- und Raw-Wire-Verträge sind
  durch den aktuellen Testlauf abgedeckt.
