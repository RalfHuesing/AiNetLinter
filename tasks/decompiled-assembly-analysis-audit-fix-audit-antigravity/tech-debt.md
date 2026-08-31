# Tech Debt & Architektur-Notizen: AiNetLinter MCP-Server

## 1. Übersicht

Dieses Dokument erfasst strukturelle Beobachtungen, Grenzfälle und technische Schulden, die während des Live-Audits des AiNetLinter MCP-Servers (v1.0.157) identifiziert wurden.

---

## 2. Erfasste Punkte

### TD-001: Unkonditionale Referenzprojektion im Assembly-Text-Formatter
- **Komponente:** `src/AiNetLinter/Mcp/Assemblies/Formatting/` / `InspectAssemblyResponseFormatter`
- **Kontext:** Der Text-Formatter projiziert aktuell den gesamten Referenzgraph der Lease, ohne zu prüfen, ob der aufrufende Kontext nur an einem einzelnen Typ/Member interessiert ist.
- **Lösungspfad:** Dem Formatter den `InspectAssemblyRequest` übergeben und bei vorhandenen Filtern (`typeName`, `memberName`, `namespace`) auf den `SummaryOnly`-Pfad für Referenzen umschalten.

### TD-002: Fehlendes global serialisiertes Diagnose-Budget in Symbolgraph-Assembly-Tools
- **Komponente:** `FindReferencesTool`, `GetCallTreeTool`
- **Kontext:** Im Gegensatz zu `GetServerHealthResponseBuilder`, der `maxDiagnostics` beachtet und deckelt, iterieren die Symbolgraph-Assembly-Handler über alle `ReferenceExpansionDiagnostics` und rendern jede einzelne Zeile als `[Assembly-Diagnostic]`.
- **Lösungspfad:** Gemeinsames Hilfsmittel `McpDiagnosticFormatter.FormatLimited(...)` mit festem Textlimit (z. B. 5 Zeilen) für alle Assembly-Tools verwenden.

### TD-003: Heuristischer Namensabgleich bei fehlender Consumer-Compilation in `find_assembly_extensions`
- **Komponente:** `FindAssemblyExtensionsTool` / `AssemblyExtensionsScanner`
- **Kontext:** Roslyns `ClassifiesAsExtensionMethod` liefert im Standalone-Assembly-Modus ohne Consumer-Projekt immer `not_decidable`. Das Tool gibt daraufhin alle Extension-Kandidaten aus, ohne den Parametertyp des ersten Arguments (`this T receiver`) gegen den angeforderten `receiverType` zu filtern.
- **Lösungspfad:** Zweistufiger Filter: Erst semantische Prüfung; wenn `not_decidable`, Fallback auf syntaktischen Typnamenabgleich (z. B. `receiverParamType.Name.Equals(requestedTypeName, OrdinalIgnoreCase)`).
