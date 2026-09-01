# Audit-Bericht: Epic 01 — Öffentliche MCP-Verträge und Discoverability

## Scope und Evidenz

### Untersuchte Komponenten und Verträge

- **Tool-Registrierungen:** `src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs`, `AnalysisToolRegistrations.cs`, `FileStructureToolRegistrations.cs`, `SymbolGraphToolRegistrations.cs`, `SymbolBodyToolRegistrations.cs`.
- **Zentraler Dispatch:** `src/AiNetLinter/Mcp/AnalysisToolCall.cs` (`ExecuteRouted`, `UnsupportedAssemblyTarget`).
- **MCP-Tool-Schemas:** JSON-Definitionen unter `.gemini/antigravity-ide/mcp/AiNetLinter/` für `inspect_assembly`, `find_assembly_extensions`, `find_symbol`, `find_references`, `get_call_tree`, `get_type_hierarchy`, `dependency_graph`, `get_symbol_body`, `get_namespace_tree`, `get_file_skeleton`, `get_class_structure`, `metrics_tree`, `metrics_lookup`.
- **Dokumentation:** `Docs/agent-api.md`, `Docs/integration.md`, `Docs/configuration.md`, `instructions.md`.
- **Live-MCP-Abfragen:**
  - `inspect_assembly` auf `LOCAL-01`, `LOCAL-02`, `LOCAL-03`, `FALSE-01`.
  - `find_assembly_extensions` auf `LOCAL-01`.
  - Abfragen mit `targetType='assembly'` auf `get_file_tree`, `get_violations`, `get_impact` zur Prüfung der Fehlerreaktion (`ASSEMBLY_TARGET_UNSUPPORTED`).

---

## Befunde

### 1. Bugs

#### FINDING-EPIC01-01: Diskrepanz zwischen MCP-Server-Instruktionen und tatsächlicher Capability-Matrix

- **Kategorie:** Bug
- **Priorität:** P2
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `C:\Users\Ralf\.gemini\antigravity-ide\mcp\AiNetLinter\instructions.md` (Zeile 1)
  - `src/AiNetLinter/Mcp/ServerInstructions.cs`
  - `src/AiNetLinter/Mcp/AnalysisToolCall.cs` (Zeilen 88–105)
- **Soll-Ist-Abweichung:**
  In `instructions.md` wird KI-Agenten verbindlich vorgegeben:
  > *"JEDEM zielgebundenen Tool-Aufruf sind targetType und targetPath beizufuegen: targetType='project' fuer eine Source-Solution oder targetType='assembly' fuer eine lokale .dll- oder .exe-Datei; targetPath ist absolut."*
  
  Tatsächlich unterstützen jedoch nur 13 von 27 registrierten MCP-Tools das `targetType='assembly'`. Bei den übrigen 14 Tools (z. B. `get_file_tree`, `get_violations`, `safeguard`, `search_pattern`, `pattern_detect`, `find_magic_values`, `find_dead_code`, `get_feature_context`, `get_test_context`, `get_impact`, `find_duplicates`) führt die Übergabe von `targetType='assembly'` deterministisch zum Fehlercode `ASSEMBLY_TARGET_UNSUPPORTED`.
- **Evidenz:**
  - Live-Aufruf von `get_file_tree` mit `targetType='assembly'` liefert:
    `[ERROR]: ASSEMBLY_TARGET_UNSUPPORTED: Dieses Tool unterstützt das Assembly-Ziel nicht.`
  - Code in `AnalysisToolCall.cs` (Zeile 87–90):
    ```csharp
    if (resolution.Target!.TargetType == AnalysisTargetType.Assembly)
    {
        return UnsupportedAssemblyTarget(resolution.Target.CanonicalPath);
    }
    ```
  - `Docs/agent-api.md` dokumentiert zwar in Tabelle 3.1 die Aufteilung, die MCP-Systeminstruktion (`instructions.md`) behauptet jedoch Allgemeingültigkeit.
- **Auswirkung:**
  Agenten, die der Server-Instruktion folgen, versuchen nicht-unterstützte Tools mit `targetType='assembly'` aufzurufen, was zu vermeidbaren Turn-Verlusten und Fehlversuchen führt.
- **Empfehlung:**
  `instructions.md` und `ServerInstructions.cs` präzisieren, dass `targetType='assembly'` nur für die Assembly- und Symbolgraph-/Strukturwerkzeuge unterstützt wird, während Linter-, Audit- und Git-Tools `targetType='project'` erfordern.
- **Abgrenzung:** Dokumentations-/Instruktions-Diskrepanz im öffentlichen MCP-Vertrag.

---

### 2. Optimierungen

#### FINDING-EPIC01-02: `find_assembly_extensions` erzwingt immer teure Referenz-Expansion ohne Opt-out

- **Kategorie:** Optimierung
- **Priorität:** P2
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs` (Zeile 113)
  - `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs`
- **Soll-Ist-Abweichung:**
  In `AssemblyAnalysisToolRegistrations.cs` ist beim Registrieren von `find_assembly_extensions` die Eigenschaft `ExpandAssemblyReferences: true` fest eincodiert. Es existiert im Tool-Schema kein Parameter `includeReferences` (wie bei `inspect_assembly` oder `find_symbol`), um die Referenzexpansion abzuwählen.
- **Evidenz:**
  - Code in `AssemblyAnalysisToolRegistrations.cs` (Zeile 113):
    ```csharp
    new AnalysisToolDispatch(
        AssemblySessionCall: lease => FindAssemblyExtensionsTool.ExecuteAsync(...),
        ExpandAssemblyReferences: true)
    ```
  - Bei `LOCAL-01` öffnete der Aufruf von `find_assembly_extensions` im Hintergrund Dutzende Referenz-Sessions (im Testfall 90 Sessions resident), obwohl der Aufrufer eventuell nur die in der Root-Assembly definierten Extension-Methoden analysieren wollte.
- **Auswirkung:**
  Hoher Overhead bei Speicherverbrauch, Decompilation-Laufzeit und CPU-Last bei jeder Ausführung von `find_assembly_extensions`.
- **Empfehlung:**
  Parameter `includeReferences` (Default `false` oder `true` mit explizitem Opt-out) in das Schema und den Dispatcher von `find_assembly_extensions` aufnehmen.
- **Abgrenzung:** Effizienz- und Kontrolloptimierung.

#### FINDING-EPIC01-03: Dynamischer Default für `includeReferences` in `inspect_assembly` führt zu `null` im JSON-Schema

- **Kategorie:** Optimierung
- **Priorität:** P3
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs` (Zeilen 41, 61–64)
  - `.gemini/antigravity-ide/mcp/AiNetLinter/inspect_assembly.json`
- **Soll-Ist-Abweichung:**
  `includeReferences` hat im C#-Parameter den Typ `bool? includeReferences = null`. Im JSON-Schema wird `"includeReferences":{"default":null,"type":["boolean","null"]}` generiert. Der tatsächliche Default ist kontextabhängig (ohne Type-/Member-Filter `true`, mit Filter `false`).
- **Evidenz:**
  - Registrierung in `AssemblyAnalysisToolRegistrations.cs`:
    ```csharp
    ExpandAssemblyReferences: includeReferences ??
        (string.IsNullOrWhiteSpace(typeName)
         && string.IsNullOrWhiteSpace(memberName)
         && (memberNames is null || memberNames.All(string.IsNullOrWhiteSpace)))
    ```
  - Die dynamische Logik ist im Freitext der Toolbeschreibung erklärt, im JSON-Schema für LLM-Tool-Calling-Validatoren aber als `null` deklariert.
- **Auswirkung:**
  Geringe Verwirrung bei Schema-basierten Validatoren; Agenten müssen die Textbeschreibung parsen, um das Default-Verhalten zu verstehen.
- **Empfehlung:**
  Dokumentation im Schema belassen, aber in `Docs/agent-api.md` klar hervorheben, dass `null` für automatische Heuristik steht.
- **Abgrenzung:** Dokumentations-/Schema-Präzisierung.

---

### 3. Missing Features

#### FINDING-EPIC01-04: Fehlender Parameter `includeReferences` in `find_assembly_extensions`

- **Kategorie:** Missing Feature
- **Priorität:** P2
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs` (Zeilen 91–116)
  - `.gemini/antigravity-ide/mcp/AiNetLinter/find_assembly_extensions.json`
- **Soll-Ist-Abweichung:**
  Während alle anderen assemblyfähigen Werkzeuge (`inspect_assembly`, `find_symbol`, `find_references`, `get_call_tree`) eine explizite `includeReferences`-Steuerung anbieten, fehlt dieser Parameter in `find_assembly_extensions` vollständig im Schema.
- **Evidenz:**
  - Vergleich der Schemas von `inspect_assembly.json` (enthält `includeReferences`) und `find_assembly_extensions.json` (enthält nur `receiverType`, `extensionName`, `namespace`, `maxResults`, `targetType`, `targetPath`).
- **Auswirkung:**
  Agenten können nicht wählen, ob sie eine schnelle Root-Suche nach Extension-Methoden durchführen oder den gesamten Referenzbaum einbeziehen wollen.
- **Empfehlung:**
  Ergänzung des Parameters `includeReferences: boolean = false` in `find_assembly_extensions`.
- **Abgrenzung:** Fehlende Parametrisierung im öffentlichen Tool-Vertrag.

---

## Offene Unsicherheiten

1. **Schema-Evolution:** Eine Änderung von Default-Werten oder Parametern in `find_assembly_extensions` muss rückwärtskompatibel bleiben, damit bestehende Client-Aufrufe ohne `includeReferences` nicht brechen.
