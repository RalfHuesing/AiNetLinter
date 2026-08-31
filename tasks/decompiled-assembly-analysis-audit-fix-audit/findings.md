# Konsolidierte Tech-Debt- & Vertragsbefunde (MCP-Audit)

Dieses Dokument fasst sämtliche technischen Schulden, Vertragsabweichungen, Budget- und Serialisierungsprobleme zusammen, die im Rahmen des MCP-Live- und Vertragsaudits für Projekt- und Assembly-Ziele identifiziert wurden. Erfolgreiche oder unauffällige Prüfschritte sind hier bewusst ausgelassen; der Fokus liegt ausschließlich auf den konkreten Problemen, deren Code-Ursachen und Lösungsvorschlägen.

---

## 1. Klassifikations- & Prioritätsübersicht

| Master-ID | Original-IDs | Dringlichkeit | Umfang | Betroffene Tools / Komponenten | Kurzbeschreibung |
| :--- | :--- | :---: | :--- | :--- | :--- |
| **MF-001** | F-HEALTH-004, CT-ERR-001, RMT-001, AOM-001 | **P1** | Systemisch (Server) | Fast alle MCP-Tools (`get_server_health`, `get_file_tree`, `get_index_scope`, `get_feature_context`, `get_impact`, `get_test_context`, `search_pattern`, `get_hotspots`, `pattern_detect`, `get_violations`, `reload_config`, etc.) | Inkonsistente `isError`-Semantik und fehlendes `StructuredContent` bei regulären Fehlern und Unsupported-Targets (Text-only). |
| **MF-002** | EXT-001, SC-STRUCT-002 | **P1** | Systemisch (Projektionen) | `find_assembly_extensions`, `get_class_structure`, `inspect_assembly` | Stiller Datenverlust: `AssemblyAnalysisResponseBudgetCompactor` schneidet bei >4 KB Array-Einträge ab, ohne Zähler (`shownMemberCount`, `totalExtensions`) oder `truncated`-Flags zu aktualisieren. |
| **MF-003** | IA-004, SN-003, EXT-002, SB-002 | **P1** | Systemisch (Server / Tools) | `inspect_assembly`, `find_symbol`, `find_references`, `get_call_tree`, `find_assembly_extensions`, `get_server_health` | Fehlendes hartes Gesamt-Antwortbudget: Metadaten (Diagnosen, Referenzen, Session-Header) lassen Antworten trotz kleiner Slices auf 20k–100k+ Zeichen anschwellen. |
| **MF-004** | EXT-003 | **P1** | Lokal (Tool) | `find_assembly_extensions` | Der Parameter `receiverType` wird von der Tool-Logik entgegengenommen, aber bei der Suche komplett ignoriert. |
| **MF-005** | SC-STRUCT-001, CT-DG-001, SN-001 | **P1** | Mehrere Tools | `find_symbol`, `get_file_skeleton`, `dependency_graph`, `get_symbol_body` | Dekompilierte Pfade (z. B. `source/File.cs`) werden in Ad-hoc-Sessions nicht aufgelöst; `find_symbol` gibt keine generationsgebundene Folge-Symbol-ID aus. |
| **MF-006** | SB-001 | **P1** | Systemisch (Server) | `AssemblySourceSelectionOrchestrator`, `AssemblyAnalysisResponse` | Konfigurierte Quellenzuordnung führt live immer zu dekompiliertem Fallback; der genaue Fallback-Grund wird verschluckt und nirgends strukturiert ausgegeben. |
| **MF-007** | IA-006, EXT-004 | **P1** | Mehrere Tools | `inspect_assembly`, `find_assembly_extensions`, `get_class_structure` | Strukturierte Parameter-, Generics- und Constraint-Daten fehlen im `StructuredContent`; Signaturen liegen nur als unformatierter Textstring vor. |
| **MF-008** | SN-002, RMT-002 | **P1** | Mehrere Tools | `get_call_tree` (Root-only), `get_type_hierarchy`, `metrics_tree`, `reload_config` | Fehlende oder inkonsistente `StructuredContent`-Projektionen im Erfolgsfall (reine Text-Ausgaben ohne maschinenlesbare DTOs). |
| **MF-009** | F-HEALTH-001 | **P1** | Lokal (Tool) | `get_file_tree` | `treeDepth` wird bei der Verzeichnis-Traversierung ignoriert; stattdessen steuert nur `maxDepth` den Scan. |
| **MF-010** | F-HEALTH-003 | **P2** | Systemisch (Server) | `get_server_health` | Parameterloser globaler Health-Call expandiert alle residenten Session-Details unbegrenzt (>76 KB bei vielen Sessions). |
| **MF-011** | F-HEALTH-002 | **P2** | Lokal (Tool) | `get_file_tree` | `view="summary"` überträgt breite Verzeichnisstrukturen und ignoriert `maxResults`. |
| **MF-012** | CT-SES-001 | **P2** | Systemisch (Server) | `AssemblyAnalysisRegistry` | Nicht-kanonische Schreibweisen desselben Pfades erzeugen redundante Session-Generationen im Registry-Dictionary. |
| **MF-013** | IA-007 | **P2** | Lokal (Tool) | `inspect_assembly`, `find_assembly_extensions` | Kein explizites Root-Feld `metadataOnly: true` / Load-Attest im Response-Payload. |
| **MF-014** | AOM-002 | **P2** | Dokumentation | `Docs/agent-api.md` | Dokumentationswiderspruch: Zeile 195 schließt Assembly-Target für Health aus, während Zeilen 312, 342, 378 und der Code es unterstützen. |
| **MF-015** | RMT-003 | **P3** | Dokumentation | `Docs/integration.md`, `Docs/configuration.md` | Fehlende explizite Kleinlimit-/Progressive-Disclosure-Strategie für breite Listen-/Baum-Tools. |
| **ORCH-001** | ORCH-001 | **P2** | Prozess / Workflow | Git-Commit-Hygiene | Fremdpfad aus einem Sibling-Task wurde versehentlich in Fachcommit `f2e96682` aufgenommen. |

---

## 2. Detaillierte Befundbeschreibungen & Lösungsvorschläge

### MF-001: Inkonsistente Fehler- und `isError`-Klassifikation & fehlendes `StructuredContent`

- **Dringlichkeit:** P1
- **Umfang:** Systemisch (Server)
- **Original-IDs:** `F-HEALTH-004`, `CT-ERR-001`, `RMT-001`, `AOM-001`
- **Betroffene Tools:** `GetServerHealthTool`, `GetFileTreeTool`, `GetIndexScopeTool`, `GetFeatureContextTool`, `GetImpactTool`, `GetTestContextTool`, `SearchPatternTool`, `GetHotspotsTool`, `PatternDetectTool`, `GetViolationsTool`, `ReloadConfigTool`, `GetFileSkeletonTool`, `GetSymbolBodyTool`, `GetClassStructureTool`
- **Problem & Agentische Auswirkung:**
  Bei Anwendungsfehlern (`INVALID_ARGUMENT`, `RESOURCE_NOT_FOUND`, `CONFIG_NOT_FOUND`) und Unsupported-Targets (`ASSEMBLY_TARGET_UNSUPPORTED`) liefert der Server `isError=false` und gibt den Fehler ausschließlich als formatierten Textstring zurück. `StructuredContent` bleibt `null`. Ein automatisierter Agent kann nicht programmatisch (ohne fehleranfälliges Regex/Textparsing) zwischen einer regulären leeren Ergebnismenge, einem Formatierungs-Fallback und einem echten Validierungs- oder Unsupported-Fehler unterscheiden.
- **Code-Referenzen:**
  - [`src/AiNetLinter/Mcp/McpToolResults.cs:66-74`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/McpToolResults.cs#L66-L74) (`BuildResult` erzeugt keine `StructuredContent`-Daten für Fehler)
  - [`src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs:65-81`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs#L65-L81) (`Unsupported` baut Text-only Block)
  - [`src/AiNetLinter/Output/LinterErrorFormatter.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Output/LinterErrorFormatter.cs)
- **Lösungsvorschlag:**
  1. Typisiertes Fehler-DTO definieren:
     ```csharp
     public sealed record McpErrorPayload(
         string Code,
         string Message,
         string? Context,
         string? Hint,
         string? TargetType,
         bool IsRecoverable);
     ```
  2. `McpToolResults.BuildResult` so anpassen, dass bei Fehlern und Unsupported-Rückgaben immer dieses DTO als `StructuredContent` mitgegeben wird.
  3. `AssemblyAnalysisResponse.Unsupported` mit typisiertem Payload ausstatten.

---

### MF-002: Stiller Datenverlust & Drift zwischen Text, Zählern und `StructuredContent`

- **Dringlichkeit:** P1
- **Umfang:** Systemisch (Projektionen)
- **Original-IDs:** `EXT-001`, `SC-STRUCT-002`
- **Betroffene Tools:** `find_assembly_extensions`, `get_class_structure`, `inspect_assembly`
- **Problem & Agentische Auswirkung:**
  In [`AssemblyAnalysisResponse.cs:41`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs#L41) wird für jedes angereicherte Assembly-Ergebnis [`AssemblyAnalysisResponseLimits.EnsureStructuredContentBudget`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs#L35) aufgerufen, welches [`AssemblyAnalysisResponseBudgetCompactor.Compact(node, MaxDiagnosticBytes)`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseBudgetCompactor.cs#L30) mit einer harten 4-KiB-Grenze (`MaxDiagnosticBytes = 4096`) ausführt.
  Wenn der Payload 4 KiB übersteigt, löscht `Compact` stillschweigend Array-Einträge aus `extensions`, `members`, `types`, synchronisiert jedoch weder `shownMemberCount`, `totalMemberCount`, `totalExtensions` noch das `truncated`-Flag.
  *Evidenz:* `find_assembly_extensions` meldet im Text 124 Extensions (`truncated=false`), liefert im JSON aber nur 1–2 Einträge. `get_class_structure` meldet 21 Member (`shownMemberCount=21`, `truncated=false`), das JSON-Array enthält jedoch nur 17 Member.
- **Code-Referenzen:**
  - [`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseBudgetCompactor.cs:20-22, 35-46, 241-250`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseBudgetCompactor.cs#L20-L22) (`OptionalPropertyNames`, `TryRemoveOptionalProperty`)
  - [`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs:18, 35-48`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs#L18) (`MaxDiagnosticBytes = 4 * 1024`)
  - [`src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs:41-43`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs#L41-L43)
- **Lösungsvorschlag:**
  1. `AssemblyAnalysisResponseBudgetCompactor` muss primär Diagnostik und Metadaten kürzen, nicht die angeforderten Nutzdaten (Members, Extensions, Types).
  2. Falls Nutzdaten-Arrays aus Budgetgründen gekürzt werden müssen, MÜSSEN die Zähler (`shownMemberCount`, `shownCount`) atomar synchronisiert und `truncated = true` mit `truncatedBy: ["responseBudget"]` gesetzt werden.
  3. Die 4-KiB-Grenze (`MaxDiagnosticBytes`) darf nicht blind auf den gesamten Fachdaten-Payload angewendet werden.

---

### MF-003: Fehlendes hartes Gesamt-Antwortbudget (Metadaten-Explosion)

- **Dringlichkeit:** P1
- **Umfang:** Systemisch (Server / Tools)
- **Original-IDs:** `IA-004`, `SN-003`, `EXT-002`, `SB-002`
- **Betroffene Tools:** `inspect_assembly`, `find_symbol`, `find_references`, `get_call_tree`, `find_assembly_extensions`, `get_server_health`
- **Problem & Agentische Auswirkung:**
  Parameter wie `maxResults`, `maxMembers`, `topN` begrenzen nur die gefilterten Fachdatenzeilen. Begleitende Metadatenblöcke (Diagnose-Zusammenfassungen, 32 Referenz-Einträge, 32 Referenz-Sessions, Namespace-Listen) werden unabhängig davon in voller Länge formatiert. Dadurch erreichen Antworten bei `maxResults=1` oder `maxResults=1000` zwischen 20 KB und 102 KB Textgröße.
- **Code-Referenzen:**
  - [`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs:19-21, 113-145`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs#L19-L21) (`MaxReferences = 32`, `MaxReferenceSessions = 32`)
  - [`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs:104-126`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs#L104-L126)
  - [`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyFormatter.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyFormatter.cs)
- **Lösungsvorschlag:**
  1. Progressive Disclosure für Metadaten: Referenzen und Referenz-Sessions standardmäßig nur als Aggregatzahlen ausgeben (`shownCount: 0, totalCount: 45`).
  2. Vollständige Detaillisten nur bei expliziten Flags (z. B. `includeReferences=true` oder `includeDiagnostics=true`) rendern.
  3. Harte Obergrenze für Text-Content mit klarer Truncation-Markierung (`[Antwort wegen globalem Budget gekürzt]`).

---

### MF-004: `receiverType`-Filter in `find_assembly_extensions` unwirksam

- **Dringlichkeit:** P1
- **Umfang:** Lokal (Tool)
- **Original-IDs:** `EXT-003`
- **Betroffene Tools:** `find_assembly_extensions`
- **Problem & Agentische Auswirkung:**
  In [`FindAssemblyExtensionsTool.cs:62-64`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs#L62-L64) wird `AssemblyExtensionSearchOptions` nur mit `ExtensionName` und `Namespace` instanziiert; `arguments.ReceiverType` wird nicht übergeben. In [`AssemblyAnalysisService.FindExtensions`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs#L102-L120) findet keinerlei Filterung nach dem Receiver-Typ statt.
  *Evidenz:* Ein passender `receiverType` und ein absichtlich unmöglicher Typ (`Receiver_404`) liefern exakt dieselben 124 Treffer.
- **Code-Referenzen:**
  - [`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs:62-64`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs#L62-L64)
  - [`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs:102-120`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs#L102-L120)
  - [`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs:19-26, 42-45`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs#L19-L26)
- **Lösungsvorschlag:**
  1. `AssemblyExtensionSearchOptions` um `string? ReceiverType` erweitern.
  2. In `FindAssemblyExtensionsTool.BuildResult` den Wert `arguments.ReceiverType` übergeben:
     ```csharp
     var selection = AssemblyAnalysisService.FindExtensions(
         context,
         new AssemblyExtensionSearchOptions(arguments.ExtensionName, arguments.Namespace, arguments.ReceiverType, maxResults));
     ```
  3. In `AssemblyAnalysisService.FindExtensions` den Filter anwenden:
     ```csharp
     .Where(pair => MatchesReceiver(pair.Method, options.ReceiverType))
     ```

---

### MF-005: Pfad- und Folge-ID-Ketten für dekompilierte Assemblys instabil

- **Dringlichkeit:** P1
- **Umfang:** Mehrere Tools
- **Original-IDs:** `SC-STRUCT-001`, `CT-DG-001`, `SN-001`
- **Betroffene Tools:** `find_symbol`, `get_file_skeleton`, `dependency_graph`, `get_symbol_body`
- **Problem & Agentische Auswirkung:**
  1. [`SolutionDocumentPathResolver.Find`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/Documents/SolutionDocumentPathResolver.cs#L16) scheitert bei relativen dekompilierten Pfaden (z. B. `source/File.cs`), da `solution.FilePath` in Ad-hoc-Workspaces keinen Directory-Pfad liefert und `Path.GetFullPath` gegen das Prozess-CWD statt gegen das temporäre Dekompilierungsverzeichnis auflöst.
  2. [`FindSymbolTool`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/SymbolGraph/FindSymbolTool.cs#L153) gibt für Assembly-Ziele relative Dateipfade und Signaturen aus, formatiert jedoch keine generationsgebundene Symbol-ID (`assembly:<hash>:<generation>:<symbolId>`). Folgeaufrufe an `get_symbol_body` schlagen daher fehl, wenn der Agent selbst konstruierte `M:`-Präfixe übergibt.
- **Code-Referenzen:**
  - [`src/AiNetLinter/Core/Documents/SolutionDocumentPathResolver.cs:16-55`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/Documents/SolutionDocumentPathResolver.cs#L16-L55)
  - [`src/AiNetLinter/Mcp/Tools/SymbolGraph/FindSymbolTool.cs:145-155`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/SymbolGraph/FindSymbolTool.cs#L145-L155) (`FormatSymbolLocationEntries`)
  - [`src/AiNetLinter/Mcp/Tools/FileStructure/GetFileSkeletonTool.cs:90-105`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FileStructure/GetFileSkeletonTool.cs#L90-L105)
- **Lösungsvorschlag:**
  1. In `SolutionDocumentPathResolver` den Basispfad der Dokumente im Roslyn-Projekt prüfen (z. B. gemeinsamer Root aller `project.Documents`), um relative Pfade wie `source/File.cs` und Dateinamen robust zuzuordnen.
  2. In `FindSymbolTool` für Assembly-Ziele die generationgebundene ID über `AnalysisSymbolIdentity.Format(symbolId)` in `SymbolLocationEntry` integrieren.

---

### MF-006: Konfigurierte Source-Zuordnung führt immer zu dekompiliertem Fallback

- **Dringlichkeit:** P1
- **Umfang:** Systemisch (Server)
- **Original-IDs:** `SB-001`
- **Betroffene Tools:** `AssemblySourceSelectionOrchestrator`, `AssemblyAnalysisResponse`, alle Assembly-Tools
- **Problem & Agentische Auswirkung:**
  Trotz hinterlegter Quellenzuordnung (`ExternalSourceMapping`) und vorhandener Checkout-Artefakte wird live immer `origin=decompiled`, `sourcePath=none` gemeldet. Schlägt das Mapping fehl (z. B. Assembly-Name stimmt nicht exakt überein oder Mapping-Count != 1), ruft [`AssemblySourceSelectionOrchestrator.ResolveAsync`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblySourceSelectionOrchestrator.cs#L75-L82) einfach `CreateScope()` ohne Diagnosen auf. Der Client erfährt nicht, warum der Source-backed-Modus nicht aktiviert wurde.
- **Code-Referenzen:**
  - [`src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblySourceSelectionOrchestrator.cs:70-90`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblySourceSelectionOrchestrator.cs#L70-L90)
  - [`src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs:83-88`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs#L83-L88)
- **Lösungsvorschlag:**
  1. In `AssemblySourceSelectionOrchestrator.ResolveAsync` bei Nicht-Treffern strukturierte Diagnosen erfassen (`no-mapping-matched`, `multiple-mappings-ambiguous`, `source-solution-invalid`, `provider-unavailable`).
  2. Den Fallback-Grund in `AssemblyResponseMetadata` aufnehmen und im Header/StructuredContent anzeigen (`fallbackReason=mapping-not-found`).

---

### MF-007: Strukturierte Parameter-, Generics- und Constraint-Daten fehlen

- **Dringlichkeit:** P1 / P2
- **Umfang:** Mehrere Tools
- **Original-IDs:** `IA-006`, `EXT-004`
- **Betroffene Tools:** `inspect_assembly`, `find_assembly_extensions`, `get_class_structure`
- **Problem & Agentische Auswirkung:**
  In den Ergebnisobjekten von Member- und Extension-Tools fehlen separate strukturierte Parameter-, Generic- und Constraint-Felder im `StructuredContent` für diverse Symbol-Arten bzw. werden bei der Serialisierung/Budget-Compacting entfernt. Signaturen müssen per String-Parsing zerlegt werden, was fehleranfällig ist.
- **Code-Referenzen:**
  - [`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs:122-167, 194-212`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs#L122-L167)
  - [`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs:72-97, 107-117`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs#L72-L97)
- **Lösungsvorschlag:**
  1. Konsistentes DTO-Schema mit `parameters: [{ name, type, refKind, isOptional, defaultValue }]`, `genericParameters: []` und `constraints: []` garantieren.
  2. Sicherstellen, dass diese Felder nicht vom Compactor gelöscht werden, wenn das Ergebnis innerhalb des Limits liegt.

---

### MF-008: Inkonsistente `StructuredContent`-Projektionen im Erfolgsfall

- **Dringlichkeit:** P1 / P2
- **Umfang:** Mehrere Tools
- **Original-IDs:** `SN-002`, `RMT-002`
- **Betroffene Tools:** `get_call_tree` (Root-only), `get_type_hierarchy`, `metrics_tree`, `reload_config`
- **Problem & Agentische Auswirkung:**
  - `get_call_tree` liefert bei `includeReferences=false` nur Text/Mermaid; das `StructuredContent` ist leer.
  - `get_type_hierarchy`, `metrics_tree` und `reload_config` liefern rein textuelle Ausgaben ohne strukturierte DTOs. Automatisierte Workflows können Hierarchien und Metriken nicht direkt maschinell verarbeiten.
- **Code-Referenzen:**
  - [`src/AiNetLinter/Mcp/Tools/CallTree/GetCallTreeTool.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/CallTree/GetCallTreeTool.cs)
  - [`src/AiNetLinter/Mcp/Tools/SymbolGraph/GetTypeHierarchyTool.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/SymbolGraph/GetTypeHierarchyTool.cs)
  - [`src/AiNetLinter/Mcp/Tools/Metrics/MetricsTreeTool.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/Metrics/MetricsTreeTool.cs)
  - [`src/AiNetLinter/Mcp/Tools/ServerMaintenance/ReloadConfigTool.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/ServerMaintenance/ReloadConfigTool.cs)
- **Lösungsvorschlag:**
  1. `GetCallTreeTool` so erweitern, dass auch bei Root-only ein `CallTreeDto` im `StructuredContent` enthalten ist.
  2. `GetTypeHierarchyPayload`, `MetricsTreePayload` und `ReloadConfigPayload` als DTOs implementieren und via `McpToolResults.Text(text, payload)` zurückgeben.

---

### MF-009: `treeDepth` wird in `get_file_tree` wire-seitig ignoriert

- **Dringlichkeit:** P1
- **Umfang:** Lokal (Tool)
- **Original-IDs:** `F-HEALTH-001`
- **Betroffene Tools:** `get_file_tree`
- **Problem & Agentische Auswirkung:**
  In [`FileStructureToolRegistrations.cs:57-58`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Registration/FileStructureToolRegistrations.cs#L57-L58) werden `maxDepth` (Default `null`) und `treeDepth` (Default `2`) registriert. In [`GetFileTreeScanner.cs:34`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeScanner.cs#L34) wird jedoch ausschließlich `input.MaxDepth` an `FileSystemWalkOptions.ForFileTree` übergeben.
  *Evidenz:* Aufrufe mit `treeDepth=0, 1, 2, 3` liefern identische Tiefen bis Level 6; `maxDepth` steuert die Tiefe dagegen korrekt.
- **Code-Referenzen:**
  - [`src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeScanner.cs:34`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeScanner.cs#L34)
  - [`src/AiNetLinter/Mcp/Registration/FileStructureToolRegistrations.cs:57-81`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Registration/FileStructureToolRegistrations.cs#L57-L81)
  - [`src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeInput.cs:13-14`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeInput.cs#L13-L14)
- **Lösungsvorschlag:**
  1. In `GetFileTreeScanner.Scan`:
     ```csharp
     var effectiveDepth = input.MaxDepth ?? (input.TreeDepth > 0 ? input.TreeDepth : null);
     var options = FileSystemWalkOptions.ForFileTree(effectiveDepth, cancellationToken);
     ```
  2. Im Tool-Schema bereinigen oder `treeDepth` und `maxDepth` eindeutig als Aliasse behandeln.

---

### MF-010: Globaler Default-Health wächst ungebunden mit Session-Anzahl

- **Dringlichkeit:** P2
- **Umfang:** Systemisch (Server)
- **Original-IDs:** `F-HEALTH-003`
- **Betroffene Tools:** `get_server_health`
- **Problem & Agentische Auswirkung:**
  In [`GetServerHealthResponseBuilder.cs:38-43`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthResponseBuilder.cs#L38-L43) iteriert die globale Health-Generierung über sämtliche residenten Assembly-Sessions und hängt jeweils vollständige Formatierungsblöcke an. Bei 100+ erwärmten Sessions explodiert die Antwortgröße auf >76 KB.
- **Code-Referenzen:**
  - [`src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthResponseBuilder.cs:38-52`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthResponseBuilder.cs#L38-L52)
  - [`src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthProjection.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthProjection.cs)
- **Lösungsvorschlag:**
  1. Im parameterlosen globalen Health standardmäßig eine aggregierte Zusammenfassung ausgeben (`totalAssemblies: 107, activeSessions: 32, totalDiagnostics: 1240`).
  2. Die vollständige Session-Liste nur ausgeben, wenn explizit `includeSessions=true` oder ein konkretes `targetPath` übergeben wurde.

---

### MF-011: Root-`summary` in `get_file_tree` ist nicht kompakt und ignoriert `maxResults`

- **Dringlichkeit:** P2
- **Umfang:** Lokal (Tool)
- **Original-IDs:** `F-HEALTH-002`
- **Betroffene Tools:** `get_file_tree`
- **Problem & Agentische Auswirkung:**
  `view="summary"` liefert zwar keine Dateieinträge (`files: []`), überträgt aber die vollständige Liste aller Verzeichnisse (z. B. 186 Verzeichnisse, 24 KiB) und ignoriert `maxResults`.
- **Code-Referenzen:**
  - [`src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeScanner.cs:98-102, 137-156`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeScanner.cs#L98-L102)
- **Lösungsvorschlag:**
  1. In `view="summary"` standardmäßig nur Top-Level-Verzeichnisse oder Verzeichnisaggregate ausgeben.
  2. Bei Bedarf eine separate Begrenzung für Verzeichnisse einführen (`maxDirectories`).

---

### MF-012: Reparse- und Pfad-Schreibweisen erzeugen redundante Session-Generationen

- **Dringlichkeit:** P2
- **Umfang:** Systemisch (Server)
- **Original-IDs:** `CT-SES-001`
- **Betroffene Tools:** `AssemblyAnalysisRegistry`
- **Problem & Agentische Auswirkung:**
  Nicht-kanonische Schreibweisen desselben Pfades (z. B. Reparse-Pfade, 8.3-Dateinamen, redundante Slashes) erzeugen im Dictionary separate Registry-Einträge und neue Generationen für denselben physischen DLL-Inhalt.
- **Code-Referenzen:**
  - [`src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.cs:114`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.cs#L114)
- **Lösungsvorschlag:**
  1. Pfad vor der Key-Bildung im Dictionary vollständig über `Path.GetFullPath` und ggf. Normalisierung auf kanonische Schreibweise bringen.

---

### MF-013: `metadataOnly` / Load-State nur indirekt observierbar

- **Dringlichkeit:** P2
- **Umfang:** Lokal (Tool-Vertrag)
- **Original-IDs:** `IA-007`
- **Betroffene Tools:** `inspect_assembly`, `find_assembly_extensions`
- **Problem & Agentische Auswirkung:**
  Das Root-Payload enthält kein explizites boolesches Flag `metadataOnly: true`. Agents müssen das metadata-only Verhalten indirekt aus `origin=decompiled` und `status=partial` ableiten.
- **Code-Referenzen:**
  - [`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs:119-134`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs#L119-L134) (`InspectAssemblyPayload`)
  - [`src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs:92-106`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs#L92-L106)
- **Lösungsvorschlag:**
  1. `bool MetadataOnly { get; } = true` im `AssemblyResponseMetadata` und in den Payloads ergänzen.

---

### MF-014: Dokumentationswiderspruch bzgl. `get_server_health` mit Assembly-Target

- **Dringlichkeit:** P2
- **Umfang:** Dokumentation
- **Original-IDs:** `AOM-002`
- **Betroffene Dokumente:** `Docs/agent-api.md`
- **Problem & Agentische Auswirkung:**
  In [`Docs/agent-api.md:195`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/Docs/agent-api.md#L195) steht: "`get_server_health` kann diesen Target-Block weglassen oder einen vollständigen Projekt-Target-Block erhalten." In den Zeilen 312, 342 und 378 wird jedoch korrekt beschrieben, dass auch `targetType="assembly"` übergeben werden kann.
- **Code-Referenzen:**
  - [`Docs/agent-api.md:195`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/Docs/agent-api.md#L195) vs. [`Docs/agent-api.md:312, 342, 378`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/Docs/agent-api.md#L312)
- **Lösungsvorschlag:**
  1. Satz in Zeile 195 anpassen: "`get_server_health` kann diesen Target-Block weglassen oder einen vollständigen Projekt- bzw. Assembly-Target-Block erhalten."

---

### MF-015: Fehlende explizite Kleinlimit-Strategie in der Dokumentation

- **Dringlichkeit:** P3
- **Umfang:** Dokumentation
- **Original-IDs:** `RMT-003`
- **Betroffene Dokumente:** `Docs/integration.md`, `Docs/configuration.md`
- **Problem & Agentische Auswirkung:**
  In den Leitfäden fehlt ein expliziter Hinweis für breite Abfragen (`pattern_detect`, `get_violations`, `get_namespace_tree`, `metrics_tree`), initial kleine Limits (1–2) zu wählen, um Kontext-Budgets von LLMs zu schonen.
- **Lösungsvorschlag:**
  1. Abschnitt zur Progressive-Disclosure-Strategie in `Docs/integration.md` schärfen.

---

### ORCH-001: Fremdpfad in Fachbericht-Commit `f2e96682`

- **Dringlichkeit:** P2
- **Umfang:** Prozess / Orchestrierung
- **Original-IDs:** `ORCH-001`
- **Problem & Auswirkung:**
  Im Commit `f2e96682` wurde versehentlich eine Datei aus einem Sibling-Task mitgestaged.
- **Lösungsvorschlag:**
  1. Git-Staging-Befehle immer mit expliziten Dateipfaden ausführen (`git add tasks/.../file.md`), kein `git add .`.

---

## 3. Empfohlene Refactoring-Reihenfolge

1. **Paket 1: Protokoll- & Serialisierungsintegrität (P1)**
   - Behebung von MF-002 (Kompaktierungs-Drift & Datenverlust in `AssemblyAnalysisResponseBudgetCompactor`).
   - Behebung von MF-004 (`receiverType`-Filter in `FindAssemblyExtensionsTool` implementieren).
   - Behebung von MF-009 (`treeDepth`-Weiterleitung in `GetFileTreeScanner`).
   - Behebung von MF-005 (Pfadnormalisierung in `SolutionDocumentPathResolver` und generationsgebundene Symbol-ID in `FindSymbolTool`).

2. **Paket 2: Fehler- und Antwortbudget-Vertrag (P1)**
   - Behebung von MF-001 (Einheitliches Fehler-DTO in `McpToolResults` für `StructuredContent`).
   - Behebung von MF-003 (Hartes Text-/Metadaten-Budget für Assembly-Tools).
   - Behebung von MF-006 (Diagnostik und Fallback-Gründe in `AssemblySourceSelectionOrchestrator`).
   - Behebung von MF-008 (`StructuredContent` für `get_call_tree`, `get_type_hierarchy`, `metrics_tree`, `reload_config`).

3. **Paket 3: Schema- & Aggregationsbereinigung (P2/P3)**
   - Behebung von MF-010 (Globales Health-Aggregat).
   - Behebung von MF-011 (Summary-Kompaktierung).
   - Behebung von MF-012 (Pfad-Kanonisierung in Registry).
   - Behebung von MF-013 & MF-014 (Dokumentationskorrektur und `metadataOnly`-Flag).
