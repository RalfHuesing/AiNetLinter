# Audit-Bericht: Epic 05 — Navigation und fachliche Query-Korrektheit

## Scope und Evidenz

### Untersuchte Komponenten und Verträge

- **Symbolgraph-Werkzeuge:**
  - `find_symbol`: `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindSymbolTool.cs`, `FindSymbolTool.cs`.
  - `find_references`: `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindReferencesTool.cs`, `FindReferencesTool.cs`.
  - `get_call_tree`: `src/AiNetLinter/Mcp/Tools/CallTree/`.
  - `get_type_hierarchy`: `src/AiNetLinter/Mcp/Tools/SymbolGraph/GetTypeHierarchyTool.cs`.
  - `dependency_graph`: `src/AiNetLinter/Mcp/Tools/DependencyGraph/`.
  - `get_symbol_body`: `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs`.
  - `get_namespace_tree`: `src/AiNetLinter/Mcp/Tools/FileStructure/GetNamespaceTreeTool.cs`.
  - `get_file_skeleton`: `src/AiNetLinter/Mcp/Tools/FileStructure/GetFileSkeletonTool.cs`.
  - `get_class_structure`: `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs`.
  - `metrics_lookup` & `metrics_tree`: `src/AiNetLinter/Mcp/Tools/MetricsLookup/`, `src/AiNetLinter/Mcp/Tools/MetricsTree/`.
- **Live-MCP-Abfragen:**
  - Ausführung aller oben genannten Werkzeuge gegen `LOCAL-01` und `LOCAL-02` mit und ohne `includeReferences`.

---

## Befunde

### 1. Bugs

#### FINDING-EPIC05-01: Falscher Sufficiency-Hinweis bei `find_references` auf signature-only dekompilierten Snapshots

- **Kategorie:** Bug
- **Priorität:** P2
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindReferencesTool.cs` (Zeilen 68–71)
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/TransitiveCallGraphFormatter.cs`
- **Soll-Ist-Abweichung:**
  In dekompilierten Sessions werden standardmäßig nur Member-Signaturen ohne Rümpfe dekompiliert (`contentMode=decompiledSignatureOnly`).
  Wenn `find_references` für ein Symbol in einer solchen Session aufgerufen wird, findet Roslyn naturgemäß keine Aufrufstellen innerhalb von Methodenrümpfen.
  `TransitiveCallGraphFormatter.IsComplete` prüft nur, ob keine Trunkierungsgrenzen erreicht wurden, und stuft 0 Funde als "vollständig" ein. Daraufhin fügt `McpSufficiencyHints.Append` folgenden Hinweis an:
  `[HINWEIS]: Diese Daten sind vollstaendig fuer den angefragten Scope — kein zusaetzliches Read/Grep noetig.`
- **Evidenz:**
  - Live-Aufruf von `find_references` auf `LOCAL-01` lieferte:
    ```
    Keine Aufrufstellen gefunden fuer '...'
    [HINWEIS]: Diese Daten sind vollstaendig fuer den angefragten Scope — kein zusaetzliches Read/Grep noetig.
    ```
  - Dies ist sachlich falsch: Es wurden schlicht keine Methodenrümpfe analysiert, weshalb nicht behauptet werden darf, dass das Ergebnis vollständig sei.
- **Auswirkung:**
  KI-Agenten leiten aus dem Hinweis fälschlicherweise ab, dass das Symbol im untersuchten Code nirgendwo verwendet wird (False Negative mit hoher behaupteter Konfidenz).
- **Empfehlung:**
  Bei dekompilierten Sessions im Modus `decompiledSignatureOnly` darf kein Sufficiency-Hinweis ausgegeben werden; stattdessen sollte ein Hinweis erfolgen:
  `Hinweis: In dekompilierten Signature-Only-Sessions werden Methodenrümpfe nicht auf Aufrufe durchsucht.`
- **Abgrenzung:** Semantischer Fehler in der Ergebnis-Projektion und Sufficiency-Kennzeichnung.

#### FINDING-EPIC05-02: `get_namespace_tree` gibt für Assemblies irreführenden `# Solution Overview`-Header aus

- **Kategorie:** Bug
- **Priorität:** P3
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Tools/FileStructure/GetNamespaceTreeTool.cs` (Zeilen 54, 85–110)
- **Soll-Ist-Abweichung:**
  Wird `get_namespace_tree` mit `targetType='assembly'` aufgerufen, liefert die oberste Ebene folgenden Markdown-Header:
  `# Solution Overview: Solution (1 Projekte)`
  `Tipp: Nutze get_namespace_tree(project="<ProjektName>") fuer die Namespaces eines Projekts.`
- **Evidenz:**
  - Live-Ausgabe bei `LOCAL-01`:
    ```
    # Solution Overview: Solution (1 Projekte)
    - AssemblyName (Typ: Lib, 2 Namespaces, 46 Typen)
    Tipp: Nutze get_namespace_tree(project="<ProjektName>") fuer die Namespaces eines Projekts.
    ```
  - Für eine einzelne DLL/EXE ist der Begriff "Solution" und der Tipp zur Projekt-Navigation irreführend.
- **Auswirkung:**
  Agenten erhalten unpassenden Kontext und versuchen eventuell `project="..."`-Parameter zu setzen.
- **Empfehlung:**
  Header und Tipps abhängig vom Kontext anpassen (z. B. `# Assembly Overview: <AssemblyName>` bei `targetType='assembly'`).
- **Abgrenzung:** Formatierungs- und Doku-Fehler.

---

### 2. Optimierungen

#### FINDING-EPIC05-03: `find_symbol` ohne `includeReferences` begrenzt Navigation auf Root-Snapshot

- **Kategorie:** Optimierung
- **Priorität:** P2
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindSymbolTool.cs`
- **Soll-Ist-Abweichung:**
  `find_symbol` hat als Default `includeReferences=false`. Wenn ein Entwickler nach einem Symbol sucht, das aus einer referenzierten DLL stammt (z. B. eine Basisklasse oder ein Interface), findet der Standardaufruf 0 Treffer ohne Hinweis, dass das Symbol in den referenzierten Assemblies vorhanden sein könnte.
- **Evidenz:**
  - Live-Aufruf nach externen Schnittstellen liefert 0 Treffer; erst mit explizitem `includeReferences: true` wird die Referenzauflösung aktiviert.
- **Auswirkung:**
  Zusätzliche Iterationsrunden für den Agenten bei der Suche nach Basistypen.
- **Empfehlung:**
  Wenn bei `includeReferences=false` 0 Treffer gefunden werden, einen Hinweis im Ergebnis ausgeben:
  `Tipp: Bei targetType='assembly' kann 'includeReferences=true' gesetzt werden, um auch Referenz-Assemblies zu durchsuchen.`
- **Abgrenzung:** UX- und Discoverability-Optimierung.

---

### 3. Missing Features

#### FINDING-EPIC05-04: Fehlende `get_impact`-Unterstützung für Assembly-Targets

- **Kategorie:** Missing Feature
- **Priorität:** P2
- **Größe:** M
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Registration/SymbolGraphToolRegistrations.cs` (Zeilen 143–163)
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/GetImpactTool.cs`
- **Soll-Ist-Abweichung:**
  `get_impact` unterstützt aktuell nur `targetType='project'`. Der Symbol-Modus (`get_impact` mit `symbolIdentifier`) analysiert Typ- und Aufrufhierarchien, wird für `targetType='assembly'` aber pauschal mit `ASSEMBLY_TARGET_UNSUPPORTED` abgewiesen.
- **Evidenz:**
  - Live-Aufruf von `get_impact` mit `symbolIdentifier` auf `LOCAL-01` scheitert mit `ASSEMBLY_TARGET_UNSUPPORTED`.
  - In `SymbolGraphToolRegistrations.cs` setzt `AddGetImpact` im `AnalysisToolDispatch` kein `AssemblySessionCall`.
- **Auswirkung:**
  Agenten können die Auswirkungsanalyse für ein Symbol in einer Assembly nicht über `get_impact` abfragen, sondern müssen manuell `find_references`, `get_type_hierarchy` und `get_call_tree` kombinieren.
- **Empfehlung:**
  Unterstützung von `AssemblySessionCall` für den `symbolIdentifier`-Modus von `get_impact` nachrüsten.
- **Abgrenzung:** Funktionale Lücke im Symbolgraph-Werkzeugkasten.

---

## Offene Unsicherheiten

1. **Call-Graph-Traversierungstiefe:** Bei `includeReferences=true` kann der Aufrufergraph über mehrere referenzierte Assemblies tief anwachsen; die harte Begrenzung auf 250 Knoten schützt vor Endlosschleifen.
