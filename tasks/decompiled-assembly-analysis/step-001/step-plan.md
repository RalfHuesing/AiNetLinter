---
status: done (pending audit)
type: step-plan
task: decompiled-assembly-analysis
step: 001
corrects: null
title: "Einheitlichen Analysis-Target-Vertrag und Dispatch umstellen"
epic: EPIC-01
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-28T11:23:07+02:00
related_to: []
---

# Step 001: Einheitlichen Analysis-Target-Vertrag und Dispatch umstellen

## Bezug

Dieser Step bedient EPIC-01 „Einheitlicher Analyse-Target-Vertrag und gemeinsame Dispatch-Grenze“. Er setzt die Konzeptteile zum harten MCP-Vertrag mit `targetType` und `targetPath`, zur zentralen Auflösung und zum gemeinsamen Dispatch um. Die residenten Assembly-Sessions, Decompilation und Source-Mappings bleiben dem nachfolgenden EPIC-02/03 vorbehalten.

## Aktueller Projektzustand (JIT-Kontext)

- Die MCP-Registrierungen der projektbezogenen Roslyn-Tools verwenden derzeit durchgehend `projectRoot` und rufen `ProjectToolCall.ExecuteAsync` direkt auf. Die gemeinsame Projekt-Lease-, Loading-, Failure- und Degraded-Logik ist in `src/AiNetLinter/Mcp/Projects/ProjectToolCall.cs` und `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs` konzentriert.
- `src/AiNetLinter/Mcp/AssemblyAnalysis/AssemblyAnalysisToolRegistrations.cs` besitzt dagegen einen separaten Vertrag mit `assemblyPath` und optionalem Consumer-`projectRoot`; die eigentlichen Services arbeiten teilweise ohne Projektkontext. Eine `AnalysisTarget`-, `AnalysisTargetResolver`- oder `AnalysisToolCall`-Struktur existiert noch nicht.
- `get_server_health` ist bereits die vorgesehene Ausnahme für einen optionalen Zielbezug: ohne Ziel liefert es den Daemon-Gesamtstatus. Ressourcen und Overview-/Rules-URIs verwenden weiterhin projektbezogene `projectRoot`-Parameter, sind aber nicht Teil des Tool-Call-Vertrags.
- Die Test-Fixtures `McpProcessHost`, `McpRawWireTestHarness` und mehrere MCP-Vertragstests injizieren bzw. erwarten heute `projectRoot`. Der bestehende 29-Tool-Inventarvertrag und die Projekt-Lifecycle-Tests müssen deshalb gemeinsam mit der Produktionsregistrierung migriert werden.
- Die CodeMap deckt die betroffenen MCP-, Test- und Dokumentationsbereiche bereits ab. Es wurde kein zusätzlicher taskrelevanter Bereich gefunden; sie muss für diesen Step nicht erweitert werden. `tech-debt.md` ist nicht vorhanden und wird als leerer Index behandelt.

## Intention

Einen einzigen, strikt validierten Zielvertrag für alle projekt-/Roslyn-bezogenen MCP-Tools einführen:

```json
{
  "targetType": "project" | "assembly",
  "targetPath": "absoluter, kanonischer Pfad"
}
```

Die Registrierung soll nur noch den gemeinsamen Dispatcher kennen. Dieser löst das Ziel genau einmal auf und führt für Projektziele die bestehende Registry-/Lease-Lifecycle-Logik unverändert weiter. Assembly-Ziele erhalten in diesem Step bereits denselben Wire-Vertrag und denselben Dispatch-Einstieg; die residenten Assembly-Sessions und dekompilierten Roslyn-Modelle werden erst in EPIC-02 angeschlossen.

## Konkrete Änderungen

### 1. Target-Modell, Validierung und gemeinsamer Dispatcher

- Neue interne, immutable Werttypen in `src/AiNetLinter/Mcp/AnalysisTarget.cs` und eine zentrale Auflösung in `src/AiNetLinter/Mcp/AnalysisTargetResolver.cs` einführen. Die Struktur soll mindestens die Zielart `Project`/`Assembly`, den kanonischen Pfad sowie den unveränderten Eingangsrequest mit nullable Rohwerten abbilden.
- Die Validierung an einer Stelle bündeln: beide Felder sind erforderlich, nur die exakten Wire-Werte `project` und `assembly` sind zulässig, `targetPath` muss absolut sein und wird vor dem Registry-Zugriff kanonisiert. Für Assembly-Ziele zusätzlich eine vorhandene Datei mit `.dll`-Endung verlangen; Verzeichnisse, relative Pfade und andere Dateitypen müssen als reproduzierbarer, recoverable Argumentfehler enden. Ein Projektziel darf weiterhin seine vorhandenen Registry-Fehler (`PROJECT_NOT_INITIALIZED`, ungültige Definition usw.) liefern.
- Keine Dual-Dispatch- oder Migrationslogik einbauen: `projectRoot`, `assemblyPath` und ein optionaler Consumer-Projektpfad werden nicht mehr als alternative Tool-API interpretiert. Für fehlerhafte direkte Dispatcher-Aufrufe dieselbe Argumentvalidierung wie im Schema erzwingen.
- `src/AiNetLinter/Mcp/AnalysisToolCall.cs` als gemeinsame Dispatch-Grenze ergänzen. Nach der Auflösung soll der Projektzweig die vorhandene `ProjectToolCall`-Lifecycle-Logik (Lease, Loading, LoadFailed, Degraded-Header) wiederverwenden. Der Assemblyzweig darf in diesem Step nur einen expliziten, klar begrenzten Adapter für die bereits vorhandenen Assembly-Metadaten-Services anbieten; keine `Assembly.Load`, keinen `AssemblyLoadContext`, keine Reflection-Ausführung und keine neue Session-/Cache-Infrastruktur hinzufügen.
- Für allgemeine projektbasierte Tools, die in diesem Step noch keine Assembly-Fähigkeit besitzen, einen stabilen recoverable „Assembly-Ziel für dieses Tool noch nicht unterstützt“-Fehler über den gemeinsamen Dispatcher liefern. Die spezialisierten `inspect_assembly`-/`find_assembly_extensions`-Services dürfen ihre bisherige Ausgabe zunächst über den neuen Assembly-Zweig weiterliefern, jedoch ohne Consumer-`projectRoot`; EPIC-02 ersetzt diesen Adapter später durch die residente Assembly-Session.
- `get_server_health` über den zentralen Target-Resolver anbinden: kein Ziel bleibt der Aggregatmodus, ein vollständiges Projektziel fragt genau diesen Projektsnapshot ab, ein halb ausgefülltes Ziel ist ein Argumentfehler. Ein Assemblyziel erhält bis zur Assembly-Registry einen expliziten „noch nicht unterstützt“-Fehler. `report_observability_feedback` bleibt als nicht zielgebundener Maintenance-Call unverändert.

### 2. Alle betroffenen MCP-Registrierungen auf den Vertrag umstellen

- Die Tool-Lambdas und Beschreibungen in `src/AiNetLinter/Mcp/Tools/Analysis/AnalysisToolRegistrations.cs`, `src/AiNetLinter/Mcp/Tools/SymbolGraph/SymbolGraphToolRegistrations.cs`, `src/AiNetLinter/Mcp/Tools/FileStructure/FileStructureToolRegistrations.cs`, `src/AiNetLinter/Mcp/Tools/SymbolBody/SymbolBodyToolRegistrations.cs` und `src/AiNetLinter/Mcp/Tools/DuplicateDetection/DuplicateDetectionToolRegistrations.cs` von `projectRoot` auf die beiden Target-Felder umstellen und ausschließlich den gemeinsamen Dispatcher verwenden.
- `src/AiNetLinter/Mcp/Tools/ServerMaintenance/ServerMaintenanceToolRegistrations.cs` für `reload_config` auf ein erforderliches Projektziel umstellen und `get_server_health` mit optionalem, aber paarweise validiertem Target registrieren. Den Feedback-Call nicht künstlich in den Target-Vertrag aufnehmen.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolRegistrations.cs` auf `targetType`/`targetPath` umstellen. `assemblyPath` und der optionale Consumer-`projectRoot` dürfen nicht im Schema verbleiben; beide Assembly-Tools müssen über `AnalysisToolCall` laufen.
- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` und `src/AiNetLinter/Mcp/ServerInstructions.cs` an die neue Anforderungssprache anpassen. Die Anzahl und Reihenfolge des bestehenden Tool-Inventars bleibt unverändert.
- Die internen Dateisystem- und Ressourcenpfade dürfen weiterhin einen kanonischen Projektpfad nutzen, sofern sie ihn erst aus der Projekt-Lease beziehen. `ProjectResourceLease`, Overview- und Rules-Resource-URIs werden in diesem Step nicht in einen fiktiven Tool-Target-Vertrag umgebaut.

### 3. Vertragstests, Dispatch-Tests und Test-Fixtures migrieren

- Neue Fast-Tests für den Resolver und den Dispatcher ergänzen, vorzugsweise unter `src/AiNetLinter.FastTests/Mcp/AnalysisTargetResolverTests.cs` und `src/AiNetLinter.FastTests/Mcp/AnalysisToolCallTests.cs`. Abdecken: gültiges Projekt-/Assemblyziel, fehlende oder ungültige Typen, relative Pfade, fehlende Pfade, Directory-als-DLL, falsche Endung, Assembly ohne Projektdefinition sowie Weitergabe des kanonischen Projektpfads an die bestehende Registry.
- `src/AiNetLinter.FastTests/Mcp/WiringContractTests.cs`, `McpServerOptionsFactoryTests.cs`, `SymbolGraphToolRegistrationsTests.cs` und `GetServerHealthToolTests.cs` auf die neuen Schemas, Beschreibungen, Resolver-/Dispatcher-Aufrufe und Health-Varianten umstellen. Der Test muss ausdrücklich sicherstellen, dass Projekt-/Roslyn-Tools `targetType` und `targetPath` verlangen, Assembly-Tools keine Legacy-Felder mehr führen, Health beide Target-Felder optional behandelt und die nicht zielgebundenen Ausnahmen erhalten bleiben.
- Die bisherigen direkten `ProjectToolCall`-Verwendungen in `WiringContractTests.cs` und den betroffenen MCP-Integrationstests auf den neuen Dispatcher mit einem `AnalysisTargetRequest("project", root)` umstellen; die bestehenden Loading-, Failure-, Reload- und Degraded-Assertions bleiben als Regressionsschutz erhalten.
- `src/AiNetLinter.IntegrationTests/Mcp/McpServerAllToolsE2ETests.cs`, `McpHandshakeToolRegistrationTests.cs` und `McpServerCommandContractTests.cs` auf die neuen Wire-Argumente und Assembly-Zielaufrufe migrieren. Die E2E-Schemaverifikation soll sowohl den harten neuen Vertrag als auch das unveränderte Tool-Inventar prüfen.
- `src/AiNetLinter.IntegrationTests/Mcp/Platform/McpProcessHost.cs` inklusive `McpProcessTarget` so anpassen, dass die Fixture standardmäßig ein Projektziel injiziert und ein bereits explizit gesetztes Assemblyziel nicht überschreibt. `RepositoryMcpHostFixture.cs` und alle manuellen Wire-Aufrufe in `McpRawWireTestHarness.cs` sowie `McpServerCommandErrorHandlingTests.cs` entsprechend aktualisieren. Health-Aggregat und Feedback dürfen weiterhin ohne Ziel gesendet werden.

### 4. Öffentliche MCP-Dokumentation synchronisieren

- `README.md`, `Docs/agent-api.md`, `Docs/integration.md` und `Docs/mcp-bootstrap.md` von „`projectRoot` bei jedem Tool“ auf den Target-Vertrag umstellen. Die Dokumentation muss Projektziel, Assemblyziel, Health-Aggregat, die nicht zielgebundene Feedback-Ausnahme und die weiterhin projektbezogenen Resource-URIs klar unterscheiden.
- Die Assembly-Dokumentation darf in diesem Step nur die tatsächlich vorhandenen spezialisierten Assembly-Funktionen und deren neuen Zielvertrag beschreiben; residenter Decompiler-/Source-Mapping-Support wird erst dokumentiert, wenn EPIC-02/03 implementiert ist. Keine Konfigurations- oder `rules.json`-Änderung vornehmen.

## Tests

Während der Umsetzung:

- `dotnet test src/AiNetLinter.FastTests --filter Category=Unit`

Vor Abschluss des Steps zusätzlich:

- `dotnet build`
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`

## Definition of Done

- Alle projekt-/Roslyn-bezogenen Tool-Schemas verwenden den harten Vertrag `targetType` + `targetPath`; `projectRoot` und `assemblyPath` sind keine parallelen Tool-Argumente mehr.
- Jede betroffene Registrierung ruft den gemeinsamen Target-Dispatcher auf. Die vorhandene Projekt-Registry mit Lease-, Lade-, Fehler- und Degraded-Verhalten bleibt regressionsfrei.
- `inspect_assembly` und `find_assembly_extensions` besitzen den neuen Assembly-Zielvertrag und laufen ebenfalls durch den gemeinsamen Dispatcher; kein Consumer-Projektpfad wird mehr als verstecktes zweites Ziel akzeptiert. Allgemeine Assembly-Ziele liefern bis zur nächsten Epic-Erweiterung einen expliziten, recoverable Nicht-Unterstützt-Fehler.
- Health-Aggregat, projektbezogener Health-Snapshot, Feedback-Ausnahme und Resource-URIs sind eindeutig und getestet voneinander abgegrenzt.
- Resolver-, Schema-, Wire-, Fixture- und E2E-Tests prüfen die positiven und negativen Vertragsfälle sowie die Projektregressionen. Build und beide Nicht-Stress-Abschlussläufe sind grün.
- Keine neue Assembly-Lade-/Ausführungsroute, keine residenten Assembly-Sessions und keine Source-Mapping-Implementierung wurden vorgezogen.

## Rules-Refs

- `.agents/rules/AiNetLinter-McpWorkflow.mdc` — absolute `projectRoot`-Vorgabe für MCP-Aufrufe im Entwicklungsworkflow, semantische MCP-Abfragen für C# und Trennung von MCP-/Textsuche.
- `.agents/rules/AiNetLinter.mdc` — C#-Qualitäts-, Immutability-, Fehlerbehandlungs-, Methodenlängen- und Warnungsregeln für die neuen Target-Werttypen und den Dispatcher.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — Architekturgrenzen, Tests, Dokumentationssynchronisation, Sicherheitsregeln gegen Assembly-Laden/Reflection sowie Abschlussverifikation.

## Bekannte Ausnahmen

- Dieser Step führt den gemeinsamen Vertrag und den Dispatch-Einstieg ein, aber nicht die in EPIC-02 geplante residente Assembly-Analyse. Der Zwischenadapter für die zwei bestehenden spezialisierten Assembly-Services ist deshalb bewusst klein und darf keine neue parallele Infrastruktur bilden.
- `get_server_health` ohne Target und `report_observability_feedback` bleiben zielungebunden. Resource-URIs behalten ihren projektbezogenen `projectRoot`-Teil, weil sie nicht über die Tool-Call-Schemas laufen.

## Code-Skizze (optional)

```text
MCP Tool Call
  -> AnalysisTargetRequest
  -> AnalysisTargetResolver
  -> AnalysisToolCall
       -> project: ProjectRegistry / ProjectLease / bestehende ProjectToolCall-Lifecycle
       -> assembly: begrenzter bestehender Spezial-Adapter
```
