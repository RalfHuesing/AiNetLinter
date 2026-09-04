# Roadmap: 03-cross-assembly-navigation

Status: executing
Current Epic: EPIC-02
Letzter Commit: bb636742 (EPIC-01 abgeschlossen)
Blocker: keine

## Primäraufgabe
Cross-Assembly-Navigation und Typauflösung im MCP-Server

## Epics

### EPIC-01: Performance Short-Circuit für Test-Scans bei Fremd-Assemblies
- **Ziel**: Bei `targetType=assembly` in `get_impact` und `get_assembly_context` unnötige Test-Referenz-Scans sofort abbrechen, wenn die Assembly keine Testframework-Referenzen enthält.
- **Abhängigkeiten**: Keine (Quick-Win, beschleunigt nachfolgende Testläufe).
- **Betroffene Bereiche**: `TestDetector.cs`, `TestCoverageBatchScan.cs`, `TestCoverageAssemblyShortCircuitTests.cs`.
- **Muss-Kriterien**: Erkennung von Testframework-Referenzen (`xunit.*`, `nunit.*`, `MSTest.*`, `Microsoft.VisualStudio.TestPlatform.*`); Short-Circuiting auf leere Test-Menge in < 3s; bestehende Tests für Test-Assemblies unberührt.
- **Verifikation**: FastTests für Short-Circuiting bei Assembly ohne Testreferenzen vs. Assembly mit Testreferenzen; `get_violations`.
- **Status**: done

### EPIC-02: `search_assembly` Deklarations- & Symbolart-Filter
- **Ziel**: `search_assembly` um `declarationOnly` (`boolean`, Default `false`) und `kind` (`method`, `type`, `property`) erweitern.
- **Abhängigkeiten**: Keine.
- **Betroffene Bereiche**: `AssemblySearchTool.cs`, `AssemblyAnalysisToolRegistrations.cs`, FastTests.
- **Muss-Kriterien**: `declarationOnly=true` filtert Treffer in Strings, Kommentaren und XML-Docs heraus; `kind` schränkt auf Symbolart ein; Default-Verhalten unverändert.
- **Verifikation**: FastTests mit dekompilierter Test-Assembly; `get_violations`.
- **Status**: open

### EPIC-03: MCP-Tool `resolve_type_origin`
- **Ziel**: Eigenständiges Tool zur schnellen Auflösung von Typnamen zu definierender Assembly und absolutem DLL-Pfad über Roslyn `Compilation.References`.
- **Abhängigkeiten**: Keine.
- **Betroffene Bereiche**: `ResolveTypeOriginTool.cs`, Tool-Registrierung, FastTests.
- **Muss-Kriterien**: Ermittelt Assembly-Name, absoluten Pfad der DLL auf der Festplatte, vollqualifizierten Typnamen und Symbol-Kind; `SYMBOL_NOT_FOUND` mit durchsuchten Referenzen bei Nichterfolg; Antwortzeit < 100ms.
- **Verifikation**: FastTests mit InMemory-Workspace und Referenz-Metadaten; `get_violations`.
- **Status**: open

### EPIC-04: Outgoing Cross-Assembly Call-Leaves in `get_call_tree` mit BCL-Filterung
- **Ziel**: Cross-Assembly-Aufrufe in `OutgoingCallScanner` nicht verwerfen, sondern als externe Referenz-Blätter mit `[ref: Assembly]` ausweisen; BCL-Rauschfilter via `includeBcl` (Default `false`).
- **Abhängigkeiten**: Keine.
- **Betroffene Bereiche**: `OutgoingCallScanner.cs`, `GetCallTreeTool.cs`, `AssemblyGetCallTreeTool.cs`, FastTests.
- **Muss-Kriterien**: `[ref: <Assembly>] <Typ>.<Member>` für Calls in referenzierte Assemblies; Unterdrückung von `System.*`/`Microsoft.NETCore.*` wenn `includeBcl=false`; optionales Einblenden bei `includeBcl=true`.
- **Verifikation**: FastTests für Outgoing-Call-Trees mit Cross-Assembly-Referenzen und BCL-Filter; `get_violations`.
- **Status**: open

### EPIC-05: MCP-Tool `find_implementations` (Project & Assembly)
- **Ziel**: Eigenständiges Tool zum Auffinden konkreter Implementierungen von Interfaces und abstrakten Klassen/Methoden.
- **Abhängigkeiten**: Keine.
- **Betroffene Bereiche**: `FindImplementationsTool.cs`, Tool-Registrierungen (`project` & `assembly`), FastTests.
- **Muss-Kriterien**: Dualer Zielvertrag (`targetType=project|assembly`); liefert Klasse, Methode, Datei- und Zeilenposition sowie Status (`concrete`, `abstract`, `virtual`).
- **Verifikation**: FastTests für Schnittstellen-Implementierungen und Overrides in Project und Assembly; `get_violations`.
- **Status**: open

### EPIC-06: MCP-Registrierung, Toolschemas, Dokumentations-Sync & Abschlussverifikation
- **Ziel**: Registrierung aller neuen Tools in `tools/list` und `.gemini/antigravity-ide/mcp/AiNetLinter/`, Dokumentations-Sync, Gesamtabschlussprüfung.
- **Abhängigkeiten**: EPIC-01 bis EPIC-05.
- **Betroffene Bereiche**: ToolRegistrations, `Docs/configuration.md`, `.agents/rules/AiNetLinter-McpWorkflow.mdc`, IntegrationTests.
- **Muss-Kriterien**: E2E-Integrationstests über MCP-Client; Sync von Agent-Rules; alle Gates grün.
- **Verifikation**: `dotnet build`, `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`, `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`, `audit`-Skill.
- **Status**: open

## Abschluss-Checkliste (aus Konzept.md)
- [ ] `resolve_type_origin` antwortet deterministisch in < 100 ms mit Assembly-Name und Dateipfad.
- [ ] `get_call_tree(direction="outgoing")` zeigt Fremd-Assembly-Calls als `[ref: <Assembly>]` ohne BCL-Rauschen.
- [ ] `find_implementations` findet konkrete Implementierungen/Overrides in Quellcode und Assemblies mit Zeilenangaben.
- [ ] `search_assembly` liefert mit `declarationOnly=true` keine Treffer in XML-Docs oder Kommentaren.
- [x] `get_impact` und `get_assembly_context` schließen bei Fremd-Assemblies ohne Testreferenzen in < 3 s ab.
- [ ] FastTests (`Category!=Stress`) und IntegrationTests (`Category!=Stress`) laufen warnungs- und fehlerfrei.
- [ ] Dokumentation & MCP-Schemas synchronisiert (`Docs/configuration.md`, `.agents/rules/AiNetLinter-McpWorkflow.mdc`, `AiNetLinter.mdc`).
