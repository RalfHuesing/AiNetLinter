# Code-Map: Einheitlicher Roslyn-Analysepfad

Diese Karte ist eine kompakte Navigationshilfe für den Task
`decompiled-assembly-analysis-finish`; sie ist keine vollständige
Repository-Dokumentation. Beziehungen werden von den Rollen gegen den
aktuellen Working Tree und die AiNetLinter-MCP-Abfragen verifiziert.

## Primäraufgabe

Den einheitlichen Roslyn-Analysepfad für dekompilierte Assembly-Analyse
einschließlich Source-Truth, Sessions, Ressourcen, MCP-Capabilities und
Abschlussverifikation fertigstellen.

## Bekannte Einstiegspunkte

- `src/AiNetLinter/Mcp/AnalysisToolCall.cs` — MCP-Dispatcher und gemeinsamer
  Analysepfad.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/` — Assembly-Targetauflösung,
  Context-/Session-Aufbau und Inspect-/Analyse-Tools.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/` — Resolver, Session, Registry,
  Source-Selection und Ressourcen-Lifecycle.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/` — Mapping, Provider,
  Snapshot-, Cache- und Attestation-Lifecycle.
- `src/AiNetLinter.FastTests/Mcp/` und
  `src/AiNetLinter.IntegrationTests/` — fachbezogene Unit-, Component- und
  MCP-/Integrationstests.

## Epic-Zuordnung

- Epic 1: gemeinsamer Target-, Session- und Roslyn-Route.
- Epic 2: External Source-of-Truth, Trust, Attestation und Cachegenerationen.
- Epic 3: transitive Assembly-Referenzen sowie getrennte externe Ressourcen.
- Epic 4: Capability-Matrix, Host-Integration und End-to-End-Verträge.
- Epic 5: Dokumentation und Abschluss-Gates.

## Abschluss-Suchpunkte

- MCP-Semantik: `get_feature_context`, `find_symbol`, `find_references`,
  `get_impact`, `get_violations`, `safeguard`.
- Qualitätsaudit: `find_duplicates`, `find_dead_code`, `find_magic_values`.
- Abschlussdokumente: `README.md`, `Docs/agent-api.md`,
  `Docs/integration.md`, `Docs/configuration.md`, `Docs/ROADMAP.md`,
  `Docs/rationale.md`.
