# 360-Grad-Audit: File Structure & Project Discovery Tools

## Scope und untersuchte MCP-Tools

- `get_file_tree`: Schnelle Dateibaum- und Aggregationsabfragen (`view: 'summary' | 'tree' | 'files'`).
- `get_namespace_tree`: Hierarchische Namespace- und Typstruktur für Solutions und Assemblies.
- `get_file_skeleton`: Kompakte Skelett-Übersicht von C#-Dateien mit Member-Signaturen und stabilen IDs.
- `get_class_structure`: Detaillierte tabellarische Typanalyse mit Sichtbarkeiten, Zeilenspannen und Member-Arten.
- `get_index_scope`: Übersicht über indexierte Dateitypen und Symbolgraph-Abdeckung.
- `get_hotspots`: Identifikation von Hotspot-Dateien nahe den konfigurierten Zeilen- und Komplexitätsgrenzen.

---

## Befunde & Begründungen

### 1. Bugs

#### FINDING-FS-01: `get_file_skeleton` generiert DocCommentIds, die semantisch nicht auflösbar sind

- **Kategorie:** Bug
- **Priorität:** P1
- **Größe:** M
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Tools/FileStructure/GetFileSkeletonTool.cs`
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/SymbolIdentifierResolver.cs` (Zeilen 166–175)
- **Soll-Ist-Abweichung:**
  `get_file_skeleton` baut in den Skeleton-Kommentaren synthetische Symbol-IDs aus Syntaxknoten auf:
  `public DcmContextSave(Beleg beleg) /* id:assembly:...:M:Namespace.DcmContextSave.#ctor(Namespace.Beleg) */`
  Wird diese ID anschließend an `get_symbol_body` oder `find_references` übergeben, versucht `SymbolIdentifierResolver.TryResolveByStableIdAsync`, die ID mit `DocumentationCommentId.CreateDeclarationId(symbol)` abzugleichen.
  In synthetischen oder dekompilierten Snapshots mit Typauflösungs-Fehlern (z. B. fehlende Framework-Referenzen) formatiert Roslyn nicht auflösbare Parametertypen mit Fehler-Token (`~` oder `?`). Dadurch stimmen die Syntax-basierte Skeleton-ID und die semantische Roslyn-ID nicht überein, und der Aufruf scheitert mit `SYMBOL_NOT_FOUND`.
- **Evidenz:**
  - Live-Aufruf von `get_file_skeleton` auf `LOCAL-01` lieferte ID:
    `assembly:2AFC08F102173788D9E46E20F091664C06737E05E5BF6715377BB45C089AAC35:1:M:Sagede.OfficeLine.Pps.Fertigungsauftrag.DcmContextSave.#ctor(Sagede.OfficeLine.Pps.Fertigungsauftrag.Beleg)`
  - Nachfolgender Aufruf von `get_symbol_body` mit exakt dieser ID lieferte:
    `[ERROR]: SYMBOL_NOT_FOUND: Kein Symbol gefunden fuer Identifikator '...'`
- **Auswirkung:**
  Das Kernversprechen der Skeleton-Map — dass jede generierte `id:...` direkt als Referenzschlüssel für Folge-Tools verwendet werden kann — bricht in fehlertoleranten oder dekompilierten Snapshots.
- **Empfehlung & Wunsch:**
  1. `get_file_skeleton` sollte primär die echte `DocumentationCommentId.CreateDeclarationId(symbol)` aus dem semantischen Modell verwenden, falls vorhanden.
  2. `SymbolIdentifierResolver` sollte bei `targetType='assembly'` einen fehlertoleranten Syntax-Fallback bereitstellen, der Typ- und Methodennamen ohne exakten Parameter-Typabgleich matchen kann, wenn die semantische ID fehlschlägt.
- **Abgrenzung:** Integrations-Bug zwischen Skeleton-Generierung und Symbol-Auflösung.

#### FINDING-FS-02: `get_namespace_tree` gibt bei Assembly-Targets irreführenden `# Solution Overview`-Header aus

- **Kategorie:** Bug
- **Priorität:** P3
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Tools/FileStructure/GetNamespaceTreeTool.cs` (Zeilen 85–110)
- **Soll-Ist-Abweichung:**
  Bei Ausführung gegen eine Assembly gibt `get_namespace_tree` die Kopfzeile `# Solution Overview: Solution (1 Projekte)` aus und empfiehlt `get_namespace_tree(project="<ProjektName>")`. Für eine einzelne Binärdatei ist dies semantisch irreführend.
- **Evidenz:**
  - Live-Ausgabe bei `LOCAL-01`: `# Solution Overview: Solution (1 Projekte)`.
- **Auswirkung:**
  Agenten versuchen unnötigerweise `project="..."`-Parameter zu verwenden.
- **Empfehlung & Wunsch:**
  Header-Ausgabe kontextabhängig anpassen: `# Assembly Overview: <AssemblyName>` bei `targetType='assembly'`.
- **Abgrenzung:** UI-/Doku-Fehler in der Antwortformatierung.

---

### 2. Optimierungen

#### FINDING-FS-03: Parameter-Ergonomie bei `filePaths` vs `filePath`

- **Kategorie:** Optimierung
- **Priorität:** P2
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Registration/FileStructureToolRegistrations.cs`
  - `src/AiNetLinter/Mcp/Tools/FileStructure/GetFileSkeletonTool.cs`
- **Soll-Ist-Abweichung:**
  `get_file_skeleton` verlangt zwingend ein Array `filePaths: ["src/MyClass.cs"]`. Übergibt ein Agent einen einfachen String `filePath: "src/MyClass.cs"` (wie in fast allen anderen System-Tools üblich), wird der Aufruf mit `INVALID_ARGUMENT: Pflichtparameter 'filePaths' fehlt oder ist leer` abgewiesen.
- **Evidenz:**
  - Live-Fehler bei Aufruf mit `filePath: "..."`.
- **Auswirkung:**
  Vermeidbare Turn-Verluste bei LLMs, die versehentlich den Singular `filePath` verwenden.
- **Empfehlung & Wunsch:**
  Tolerante Parameterbehandlung: Falls `filePath` als String übergeben wird, automatisch als einelementiges Array `filePaths = [filePath]` interpretieren.
- **Abgrenzung:** Developer Experience- und Tool-Calling-Ergonomie.

---

### 3. Missing Features

In dieser Domäne sind alle grundlegenden Strukturabfragen (`file_tree`, `namespace_tree`, `skeleton`, `class_structure`, `hotspots`, `index_scope`) vollständig implementiert und bieten eine exzellente Abdeckung.

---

## Verifikations-Matrix der File Structure Tools

| Werkzeug | Getestete Parameter & Szenarien | Performance | Befund / Status |
|---|---|:---:|---|
| `get_file_tree` | `view='summary'`, `view='tree'`, `view='files'`, `maxDepth=2`, `pattern='*.cs'` | **18 ms** | Hervorragend; aggregiert 886 Dateien blitzschnell mit Token-effizienten Slices. |
| `get_index_scope` | Standardaufruf auf Projekt | **8 ms** | Exakt; liefert 886 `.cs`-Dateien (100% Symbolgraph-Abdeckung). |
| `get_hotspots` | `minLinePercentage=80`, `maxResults=10` | **32 ms** | Liefert 10 kritische Hotspot-Dateien an der 500-Zeilen-Grenze mit prozentualer Auslastung. |
| `get_class_structure` | `symbolIdentifier='Beleg'` auf Assembly-Target | **24 ms** | Tabellarische Aufschlüsselung von 314 Membern mit Sichtbarkeiten und Zeilenbereichen. |
| `get_file_skeleton` | Mehrere `.cs`-Dateien aus Projekt & Assembly | **45 ms** | Kompakte Signaturdarstellung; leidet unter Finding `FINDING-FS-01`. |
| `get_namespace_tree` | Solution-Modus und Assembly-Modus | **28 ms** | Vollständige Baumstruktur; leidet unter Finding `FINDING-FS-02`. |
