# 07 – Schema & Documentation

## Ziel

Die öffentliche Agentenbeschreibung soll den implementierten Vertrag exakt, kompakt und ohne gefährliche Bootstrap-/Capability-Missverständnisse wiedergeben.

## Scope

- verbleibende JSON-`required`-/Enum-/Alias-/Description-Lücken;
- konsistente Error-Codes und Recovery-Hints;
- Server-Instructions, Resources, README, `Docs/agent-api.md` und MCP-Workflow-Regeln;
- klare Trennung zwischen einmaligem Integrations-Bootstrap und normaler Analyse;
- keine Doku, die gewünschte oder entfernte Features als aktiv darstellt.

## Ausgangsbefunde

`tool-discovery`, `resource-agent-guide`, `resource-overview`, `resource-rules`, `get_server_health`, `reload_config`, `combo-resources-health`.

## Voraussichtliche Quellbereiche

`Registration/*`, `ServerInstructions`, `McpRegistrationInstructions`, Resource-Formatter, `Docs/agent-api.md`, `Docs/integration.md`, README und `.agents/rules/AiNetLinter-McpWorkflow.mdc`.

## Abhängigkeiten

Nachgelagert zu den fachlichen Entscheidungen aus 01–06; paketbezogene Dokuänderungen bleiben in den jeweiligen Paketen.

## Abnahme

- `tools/list`/Einzelschema, Runtime-Fehler und Referenzdoku beschreiben denselben Vertrag;
- ein bereits integriertes Projekt wird nicht in einen Bootstrap geleitet;
- Resource- und Health-Fehler unterscheiden Query-, Target- und Integrationsprobleme;
- globale Instructions bleiben klein genug und verweisen nur auf notwendige Details.
