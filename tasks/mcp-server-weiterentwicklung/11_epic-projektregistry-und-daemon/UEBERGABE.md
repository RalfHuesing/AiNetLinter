---
type: uebergabe
created_at: 2026-08-23T18:45:00+02:00
from_model: stealth/ox-alpha (openrouter)
reason: Nutzerstop — Modellwechsel; wiederholte Provider-Störungen bei Subagenten-Läufen
---

# Übergabe: Task 11_epic-projektregistry-und-daemon (Stand 2026-08-23, ~18:45 MEZ)

## Wo wir stehen

drift-loop läuft gemäß `.agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md`
(Orchestrator-Rolle: Plant JIT je Step, genau EIN Subagent gleichzeitig, Orchestrator
committet nur Task-Doku). `task-state.md`: Status `executing`,
`current_step: step-003 (in_progress)`, total_steps 3.

| Step | Inhalt | Status |
|---|---|---|
| step-001 | Definitionsdatei, Loader, Fehlerverträge (loader-seitig A.5), Config-Materialisierung | **approved**, Code `e0b25033` |
| step-002 | Registry-Kern: Entry/Lease/LeaseResult/Registry, Eviction TTL/LRU, Busy-Guard, Pending-Adoption, FAILED-Marker, TimeProvider | **approved**, Code `a80ec821` |
| step-003 | MCP-Wiring auf die Projektregistry: Tool-Leases (`projectRoot` Pflicht), harter Cut `--path`/`--config`, Health pro Key, reload_config je Key, Overview-URI-Template, ServerInstructions ≤2557 Bytes, Tests, Repo-Migration | **in_progress — WIP im Arbeitsbaum, jetzt als WIP-Commit gesichert** |

Geplant war außerdem: step-004 = Abschluss Epic A (drift-audit einmal pro Epic,
Live-Verifikation Overview inkl. Tool-Rückfallplan laut Review 5, Meilenstein-Doku,
§D.4-Vermerk in `90_bewusst-nicht-umsetzen/Konzept.md`). Danach Epic B (Daemon).

## Der WIP-Commit (step-003-Zwischenstand)

Enthalten (24 geänderte Dateien + 1 neue):
alle 6 Tool-Registrations + `OverviewResourceRegistration`, `McpServerOptionsFactory`,
`McpServerCommand`, `McpCodeGraphServer`, `LinterArgs` (u. a. `ValidateMcpMode`
angelegt), `CliOptions`/`CliOptionFactory`/`CliCommandBuilder`, `ServerInstructions`,
`GetServerHealthTool`/`-Models`, `ReloadConfigTool`, `Program.cs`, die
`Projects/`-Klassen (ErrorCodes/Lease/Registry/Entry/InstanceFactory) sowie NEU
`Mcp/Projects/ProjectToolCall.cs`.

**Zustand bei Sicherung:** NICHT grün — letzte Messung ~55 Compile-Fehler (normaler
Multi-File-Zwischenstand mitten im Refactoring). Build/Tests/Gates wurden NICHT
erfolgreich durchlaufen. Der WIP-Commit ist eine ehrliche Sicherung, KEIN
fertiger Step.

**Fehlt laut step-plan.md (zu verifizieren, dann ergänzen):**
- Der komplette Testumfang (Contract-Tests `tools/list` required `projectRoot`,
  Root-Guards PROJECT_ROOT_REQUIRED/_INVALID, harter-Cut-Tests, Budget-Test,
  Integration-Routing je Key, async-Lease-Lifetime-Nachweis R2/A)
- Repo-Migration: `ainetlinter.project.json` im Repo-Root, `.mcp.json` + Hermes
  `config.yaml` (C:\Users\Ralf\AppData\Local\hermes\config.yaml — NUR den
  ainetlinter-Eintrag anfassen!) auf `command + --mcp-server` reduziert,
  AGENTS.md-Abschnitt „AiNetLinter-MCP: Initialisierung", Docs-Ausschnitte
  (agent-api/configuration/integration/README), mdc-Sync via
  `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`
- Ggf. Restarbeiten: ServerInstructions-Budget-Rechnung (≤2557 UTF-8-Bytes,
  dokumentieren!), Vollständigkeit Health-Aggregation/reload_config je Key/
  Overview-URI-Template — alles gegen `step-003/step-plan.md` prüfen

## Das Problem (warum es hier stoppte)

Sechs Coder-Subagenten-Läufe für step-003 starben an **Provider-Störungen**
(HTTP 429 bzw. leere Responses nach internen Retries; stealth/ox-alpha via
openrouter). Muster: der Abbruch kam jeweils, wenn der Subagent-Kontext groß war
(Ende Recherche-/Lese-Phase oder mitten in der Implementierung) — Läufe 1–2 sofort
(429), Läufe 3–4 nach 25–50 Calls ohne/nur triviale Änderungen, Lauf 5 nach ~80
Calls MIT dem obigen substantiellen Teilstand. Lauf 6 (Fortsetzungs-Runde) lief ~17
Minuten und wurde vom Nutzer hart gestoppt.

**Kein Workflow-Problem:** Loop blieb konsistent, kein Halbststand außer dem
dokumentierten WIP; alle Commits bis step-002 sind grün und reviewed. Es ist ein
Infrastruktur-/Provider-Thema dieses Modells bei großen Subagent-Kontexten.

## Wie der Nachfolger weitermacht (Empfehlung)

1. Als Orchestrator gemäß drift-loop `orchestrator.md` fortsetzen; Nutzer-Vorgaben
   (Effizienz, große Steps, MCP-Dogfooding) stehen in `task-state.md`.
2. step-003 **vollenden**: WIP-Commit gegen `step-003/step-plan.md` verifizieren
   (`git --no-pager show <wip-hash>` + `dotnet build`), Fehlendes ergänzen, Gates
   (`dotnet build` warnungsfrei; FastTests + IntegrationTests je
   `Category!=Stress` EINMAL), Quality-Gates über den ainetlinter-MCP-Server
   (`get_violations`/`safeguard`/`metrics_lookup`) VOR den Commits, dann die zwei
   Pflicht-Commits des Coders (Code; Doku mit step-result.md, codemap-Pflege,
   Plan-Status auf `done (pending audit)`).
   - Alternativ, falls der WIP zu verworren erscheint: Plan teilen (Wiring vs.
     Migration/Tests) — aber der Teilstand ist bereits weitgehend vollständig
     angelegt, Vollenden ist der kürzere Weg.
3. Danach normal weiter im Loop: Kritiker (Modus step) → Review-Commit → Planer
   für step-004 → Epic-B-Steps.
4. Tech-Debt-Stand: TD-001/TD-003 sind Auftragsinhalt von step-003 (nach Umsetzung
   durch den Kritiker auf erledigt setzen lassen); TD-002/004/005 bleiben offen (Epic B).

## Umgebung (verifiziert 2026-08-23)

- ainetlinter-MCP-Server LÄUFT (v1.0.125, LoadState Loaded) — aber als ALTVERSION;
  nach Code-Änderungen refreshed er per Staleness (normal), Neustart nicht nötig.
- Parallele Agent-Arbeit im Repo möglich (zuletzt `32f302a0`, Konzept 12) — immer
  nur eigene Step-Dateien staggen, nie `-A`/`.`.
- Shell: git-bash unter Windows (POSIX-Syntax, kein PowerShell); `git --no-pager`;
  native Tools mit C:/-Pfaden.
