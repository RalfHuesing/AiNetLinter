# Epic 1 — kompakte Code-Map

## Primäre Einstiegspunkte

- Öffentliche Registrierung: src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs:15-131
  - AssemblyAnalysisToolRegistrations.Register
  - inspect_assembly-Dispatch: Zeilen 27-66
  - find_assembly_extensions-Dispatch: Zeilen 89-115
- Sammlungseinbindung: src/AiNetLinter/Mcp/Composition/McpServerToolCollectionFactory.cs:10-28
  - McpServerToolCollectionFactory.Create
  - AssemblyAnalysisToolRegistrations.Register wird in Zeile 20 genau einmal aufgerufen.
- Gemeinsame Tooloptionen: src/AiNetLinter/Mcp/Tools/McpToolRegistrationOptions.cs:7-75
  - AssemblyTool
  - ProjectTargetContract, ReadOnlyTargetContract, AssemblyTargetContract
- Target- und Routinggrenze:
  - src/AiNetLinter/Mcp/AnalysisTargetResolver.cs:11-81 — Resolve, ResolveOptional, TargetType-/Pfadvalidierung
  - src/AiNetLinter/Mcp/AnalysisToolCall.cs:113-179 — AssemblyAnalysisDispatcher.ExecuteAsync und Enrichment

## Betroffene Symbole und Responsepfade

- src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyTool.cs:10-26
  - inspect_assembly-Ausführung und Eingabemodell
- src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs:10-26
  - Extension-Ausführung und Eingabemodell
- src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisService.cs:15-387
  - AssemblyAnalysisService; Defaults und Analyseoberfläche
- src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs:17-151
  - AssemblyAnalysisResponse.Enrich und FitsResponseBudget
- src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponseLimits.cs:12-19
  - Diagnose-, Referenz-, Session- und Responsebudgets
- src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponseLimits.Budget.cs:15-128
  - Bounded-Projektion und Budgetprüfung
- src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/Responses/InspectAssemblyResponseBuilder.cs:1-100
- src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/Responses/FindAssemblyExtensionsResponseBuilder.cs:1-80
- AssemblyAnalysisToolSupport: Eingabevalidierung, Workspace- und Lease-Grenze

## Aufrufer und Abhängigkeiten

- McpServerToolCollectionFactory.Create
  → AssemblyAnalysisToolRegistrations.Register
  → AnalysisToolCall.CreateTargetRoute / AssemblyAnalysisDispatcher.ExecuteAsync
  → AnalysisTargetResolver.Resolve
  → Assembly-Registry/Session und metadata-only Analyse
  → Response-Builder und AssemblyAnalysisResponse.Enrich
  → Structured-/Text-Response mit Bounded Budgets.
- inspect_assembly entscheidet in AssemblyAnalysisToolRegistrations.Register, Zeilen 61-64, den kontextabhängigen Default für ExpandAssemblyReferences.
- find_assembly_extensions setzt in derselben Datei, Zeile 113, ExpandAssemblyReferences fest auf true.
- Referenznavigation bleibt über includeReferences und die gemeinsame Assembly-Session begrenzt; AssemblyAnalysisResponse ergänzt Origin-/Trust-/Status-/Completeness-Signale.

## Relevante Tests und Dokumentation

- FastTests:
  - src/AiNetLinter.FastTests/Mcp/Wiring/WiringToolCollectionContractTests.cs:25-118 — Toolzahl, Pflichtfelder, Legacy-Feld-Ausschluss und Capability-Beschreibungen.
  - dieselbe Datei:149-205 — Annotationen und Read-only-Profil der Assembly-Werkzeuge.
- IntegrationTests:
  - src/AiNetLinter.IntegrationTests/Mcp/Tools/McpServerAssemblyHealthE2ETests.cs:28-140 — Standalone Assembly-Aufrufe, Health und Schemafelder.
  - src/AiNetLinter.IntegrationTests/Mcp/Daemon/DaemonHostMcpProcessContractTests.cs:18-89 — Daemon-List/Call-Vertrag.
  - src/AiNetLinter.FastTests/Mcp/Daemon/DaemonHostMcpContractTests.cs:168-195 — List/Call-Parameter.
  - src/AiNetLinter.IntegrationTests/Mcp/McpToolAnnotationsWireTests.cs:17-67 — Wire-Annotationen.
- Dokumentation:
  - Docs/agent-api.md:308-348,355-360,389-482 — Targetvertrag, Parameter, Metadata, Budget und Progressive Disclosure.
  - Docs/configuration.md:32-35 — MCP-Discovery und Assembly-Kurzvertrag.
  - Docs/mcp-bootstrap.md:61-74 — Capability- und Targetmodell.
  - README.md:19,28-42 — öffentliche Assembly-Discoverability.
- Lokale Prüffall-Matrix:
  - temp/decompiled-assembly-audit-examples.md — nur für Label-/Pfadauflösung; konkrete Identitäten bleiben redigiert und werden nicht in versionierte Artefakte übernommen.

## Verifizierte Invarianten, Risiken und Unsicherheiten

- Assembly-Analyse bleibt metadata-only/read-only; Zielcode wird nicht geladen oder ausgeführt.
- targetType und targetPath sind Pflicht; Assembly-Targets erfordern absolute vorhandene .dll- oder .exe-Pfade.
- Fehler für falsche Targets oder ungültige Pfade sind recoverable und benennen die relevante Eingabe.
- Response-Metadaten machen Herkunft, Snapshot, Vertrauen, Generation, Status, Vollständigkeit, Fallback und Trunkierung sichtbar.
- Response-Budgets begrenzen Text und Structured Content; der Codewert und die Dokumentation sind derzeit nicht konsistent.
- find_assembly_extensions besitzt keinen öffentlichen includeReferences-Parameter, expandiert aber intern Referenzen.
- Ungefiltertes inspect_assembly expandiert standardmäßig im Kontext ohne Typ-/Memberfilter; gefilterte Abfragen bleiben im Root-Kontext.
- Die exakte Roh-JSON-Schemaform des tools/list-Aufrufs konnte über die verfügbare Tooloberfläche nicht separat gelesen werden; die callable Signatur und Registrierungsimplementierung sind belegt.
- Abschlussprüfung: keine Builds oder Tests gemäß Konzept-Non-Goal-Vertrag.
