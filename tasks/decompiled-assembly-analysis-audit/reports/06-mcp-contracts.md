# Linse 06 — MCP-Komposition, Schemas, Wire-Format, Fehler und Session-Generation

- Reviewstatus: Orchestrator-Fallback; kein unabhängiger Reviewer verfügbar (`collab spawn failed: agent thread limit reached`).
- Revision: `65c19468`; Produktionsquellen blieben seit der Audit-Baseline unverändert.
- MCP-Parameter: projektgebundene Abfragen mit `targetType=project`, `targetPath=<repo-root-redacted>`; Assembly-Probe mit `targetType=assembly`, `targetPath=<neutral-built-dll>`. Pfade und Quellenidentitäten sind redigiert.

## Abdeckung

Geprüft wurden `AssemblyAnalysisDispatcher`, `AnalysisTargetResolver`, `McpToolRegistrationOptions`, Assembly-/Symbolgraph-Registrierungen, Response-Enrichment, strukturierte DTOs, Fehlercodes, Status-/Completeness-Projektion sowie die Dokumentation in `Docs/agent-api.md` und `Docs/configuration.md`.

## Befund MCP-001

- Schweregrad: S1
- Umfang: U3 — strukturierte Assembly-Antworten und Diagnose-/Session-Summary
- Konfidenz: hoch
- Bereich: globales Wire-Budget
- Evidenz: `AssemblyAnalysisResponseLimits.cs:32-52` begrenzt die ausgewählten Diagnose-Samples auf `MaxDiagnosticBytes = 4 * 1024`. `InspectAssemblyTool.cs:84-99` schreibt dieselbe Sampleauswahl zusätzlich als top-level `diagnostics`, als `diagnosticsSummary` mit Root-/Transitiv-/Gesamt-Samples und als eigene Referenz-Session-Samples; `ProjectReferenceSessions` erzeugt je Session weitere begrenzte Samples. `Docs/configuration.md:35` beschreibt dagegen „4 KiB je Antwort“.
- Auswirkung: Das interne Samplebudget ist kein globales serialisiertes Antwortbudget. Schon ohne Referenz-Sessions wird der Text mehrfach im JSON wiederholt; mit bis zu 32 Sessions kann die strukturierte Antwort deutlich über 4 KiB Diagnoseinhalt liegen. Das konterkariert die Token-/Wire-Schranke und kann bei vielen Fehlern große MCP-Payloads erzeugen.
- Reproduktion: Die vorhandene Regression `AssemblyAnalysisDispatcherCapabilityTests.AssemblyRoute_StructuredContentUsesOneGlobalDiagnosticsBudget` prüft nur `UTF8.GetByteCount(string.Join("\n", payload.diagnostics))` in `:255-289`, nicht die serialisierte `structuredContent`-JSON-Größe. Ein Testfall mit Root-/Transitivdiagnosen und mehreren Sessiondiagnosen zeigt die Wiederholung in den genannten Feldern.
- Disposition: Für Folgeimplementierung zurückgestellt; Audit-only verbietet Produktionsänderungen. Empfohlene Folge: nach der finalen DTO-Projektion ein echtes globales Byte-/Tokenbudget anwenden oder Summary-Samples deduplizieren und die Testassertion auf die serialisierte Wire-Antwort verschieben.

## Vertragsprüfung ohne bestätigten Defekt

`AnalysisTargetResolver.cs:12-52` verlangt exakt `project` oder `assembly`, absolute Pfade und für Assemblys eine vorhandene `.dll`; die Dispatch-Route verwendet den kanonisierten Pfad. `AssemblyAnalysisToolRegistrations.cs:28-120` beschreibt und registriert die beiden Assembly-only-Tools als read-only. `SymbolGraphToolRegistrations.cs:49-138` führt `includeReferences=false` als Default und liefert die Assembly-spezifischen Varianten für `find_symbol`, `find_references` und `get_call_tree`.

Die Capability-Matrix in `Docs/agent-api.md:315-343` entspricht der Registrierung: `get_impact` bleibt projektgebunden, Assembly-Tools teilen Registry/Snapshot und Antworten tragen Herkunft, Generation, Status und Vollständigkeit. `AssemblyAnalysisResponse.Enrich` ergänzt diese Metadaten konsistent an Text und structured content. Fehlerfälle nutzen explizite `unsupported`, `invalid_argument`, `symbol_not_found` bzw. Compilation-/Recoverable-Pfade.

## Abdeckungsgrenze MCP-001

- Typ: Live-Wire-Abdeckung, kein zusätzlicher bestätigter Vertragsdefekt
- Schweregrad: S3
- Umfang: U3 — tatsächlich über JSON-RPC übertragene Antworten
- Konfidenz: mittel
- Evidenz: Tool-Registrierungs- und Dispatcher-Tests sind vorhanden. Die in diesem Lauf direkt abgefragten MCP-Antworten wurden als Toolresultate inspiziert; ein separater Roh-JSON-RPC-Payload-Messlauf für die maximale Diagnose-/Sessionkombination wurde nicht durchgeführt.
- Auswirkung: Die oben beschriebene Mehrfachserialisierung ist statisch belegt, ihre exakte maximale Bytezahl unter dem verwendeten MCP-Serializer bleibt offen.
- Reproduktion: Einen Fixture-Result mit maximalen Root-/Transitivdiagnosen und mehreren Referenz-Sessions erzeugen, `structuredContent` mit den Produktoptionen serialisieren und die UTF-8-Größe einschließlich aller Summary-Felder messen.
- Disposition: Als Folgeaufgabe mit MCP-001 verknüpft; keine Änderung im Audit.
