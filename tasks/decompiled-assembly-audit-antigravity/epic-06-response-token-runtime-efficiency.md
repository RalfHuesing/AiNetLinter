# Audit-Bericht: Epic 06 — Response-, Token- und Laufzeiteffizienz

## Scope und Evidenz

### Untersuchte Komponenten und Verträge

- **Response-Limits & Budget-Logik:** `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs`, `AssemblyAnalysisResponseLimits.Budget.cs`.
- **Markdown-Formatierung:** `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyFormatter.cs`.
- **DTO-Projektion:** `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/Responses/InspectAssemblyResponseBuilder.cs`, `FindAssemblyExtensionsResponseBuilder.cs`.
- **Live-MCP-Abfragen:**
  - `inspect_assembly` auf `LOCAL-01`, `LOCAL-02` und `LOCAL-03` zur Analyse des Trimming- und Trunkierungsverhaltens unter dem 8-KB-Budget (`MaxResponseBytes = 8192`).

---

## Befunde

### 1. Bugs

In dieser Kategorie liegt kein harter Vertragsbruch oder Absturz vor. Die iterative Trimming-Schleife in `AssemblyAnalysisResponseLimits.Budget.cs` arbeitet deterministisch und terminiert zuverlässig unterhalb von `MaxResponseBytes`.

---

### 2. Optimierungen

#### FINDING-EPIC06-01: Ungefilterte Namespace-Listen verdrängen Typen und Member im 8-KB-Budget

- **Kategorie:** Optimierung
- **Priorität:** P2
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyFormatter.cs` (Zeilen 45–60)
  - `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs`
- **Soll-Ist-Abweichung:**
  `InspectAssemblyFormatter` listet alle öffentlichen Namespaces der Ziel-Assembly ungekürzt auf. Bei großen Assemblies (wie `LOCAL-03` mit 64 Namespaces) belegt allein dieser Namespace-Block ~2,5 KB der maximal erlaubten 8 KB.
  Weil das Gesamt-Markdown dadurch das 8-KB-Limit überschreitet, kürzt `ProjectResponseBudget` in der Folge zuerst Member-Details und anschließend ganze Typen weg:
  ```
  - `Namespace.TypeName` (class, Public, Member 0 von 42 gezeigt (gekürzt: responseBudget))
  ```
- **Evidenz:**
  - Live-Ausgabe bei `LOCAL-03`: 64 Namespaces aufgelistet, aber für fast alle angezeigten Typen wurden die Member vollständig auf 0 gekürzt.
  - Bei `LOCAL-01`: Nur 7 von 48 Typen gezeigt, davon Typen 2–7 mit 0 Membern.
  - Bei `LOCAL-02`: Nur 4 von 48 Typen gezeigt, davon Typen 2–4 mit 0 Membern.
- **Auswirkung:**
  Der Agent erhält zwar eine vollständige Namespace-Liste, verliert aber die eigentlich relevanteren Typ- und Methodensignaturen der analysierten DLL/EXE.
- **Empfehlung:**
  Namespaces bei Überschreiten einer Schwelle (z. B. >10 Namespaces) im Markdown zusammenfassen/kürzen (z. B. `Top-10 Namespaces und 54 weitere`), damit das verbleibende Token-Budget für Typ- und Member-Signaturen genutzt werden kann.
- **Abgrenzung:** Token- und Informationsdichte-Optimierung.

#### FINDING-EPIC06-02: Hoher Token-Footprint durch parallele Markdown- und JSON-DTO-Payloads

- **Kategorie:** Optimierung
- **Priorität:** P2
- **Größe:** M
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/Responses/InspectAssemblyResponseBuilder.cs`
  - `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyTool.cs`
- **Soll-Ist-Abweichung:**
  `inspect_assembly` und `find_assembly_extensions` liefern gleichzeitig eine bis zu 8 KB große Markdown-Textdarstellung (`content[0].text`) und ein vollständiges typisiertes DTO (`structuredContent`). Da viele LLM-Clients beide Teile in den Prompt aufnehmen, verdoppelt sich der Token-Verbrauch pro Turn.
- **Evidenz:**
  - `InspectAssemblyTool.cs` liefert `McpToolResults.Text(text, structuredContent)`.
- **Auswirkung:**
  Erhöhter Token-Verbrauch und Kontextfenster-Belastung bei Agenten-Interaktionen.
- **Empfehlung:**
  Prüfen, ob Markdown-Text kompakter gestaltet werden kann, wenn `structuredContent` aktiviert ist, oder Progressive-Disclosure-Flags bereitgestellt werden können.
- **Abgrenzung:** Effizienz- und Token-Budget-Optimierung.

---

### 3. Missing Features

#### FINDING-EPIC06-03: Fehlender `compact`-Modus für schnelle Typen-Übersicht

- **Kategorie:** Missing Feature
- **Priorität:** P3
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs`
  - `.gemini/antigravity-ide/mcp/AiNetLinter/inspect_assembly.json`
- **Soll-Ist-Abweichung:**
  In `inspect_assembly` kann zwar `maxMembers=0` übergeben werden, um Member auszublenden, es gibt jedoch keinen expliziten Modus `view: 'summary' | 'types' | 'members'`, wie er beispielsweise in `get_file_tree` (`view: 'summary' | 'tree' | 'files'`) vorbildlich gelöst ist.
- **Evidenz:**
  - Parameterliste von `inspect_assembly`: `maxResults`, `maxMembers`, `publicOnly`, etc., aber keine High-Level-View-Steuerung.
- **Auswirkung:**
  Agenten müssen Parameterkombinationen (`maxMembers=0`, `includeReferences=false`) manuell wählen, um eine schnelle, token-effiziente Typenübersicht zu erhalten.
- **Empfehlung:**
  Einführung eines `view`- oder `compact`-Parameters in `inspect_assembly`.
- **Abgrenzung:** Komfort- und Progressive-Disclosure-Erweiterung.

---

## Offene Unsicherheiten

1. **Client-Verhalten bei Structured Content:** Einige Agenten-Clients nutzen primär den Markdown-Text, andere nur `structuredContent`; eine Reduktion des Textteils muss daher schrittweise erfolgen.
