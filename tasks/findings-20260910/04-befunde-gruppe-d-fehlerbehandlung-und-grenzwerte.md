# Gruppe D: Fehlerbehandlung & Grenzwerte — Befundbericht

## Geprüfte Szenarien
1. Fehlendes Pflichtfeld (`targetPath` weglassen)
2. Nicht-existenter `targetPath`
3. Falscher Argumenttyp (String statt Array bei `namePatterns`)
4. Ungültiger Enum-Wert (`kind="unknownKind"`)
5. Leerer String für Pflichtfeld (`targetPath=""`)
6. `targetPath` zeigt auf ein Verzeichnis statt auf eine Datei
7. Grenzwerte: `maxResults=0` oder negative Limits
8. Unbekannte/unerwartete Argumente (`foo="bar"`)
9. Nicht verwaltete Binärdatei (`FALSE-01`)

---

### [Positiv / Best Practice] Exzellente Vorab-Validierung
Die Validierung im AiNetLinter MCP-Server gehört zu den robustesten Implementierungen im MCP-Ökosystem:
1. **JSON-Feldpfade**: Alle Fehler nennen den genauen Feldpfad (`fieldPath: $.targetPath`, `fieldPath: $.namePatterns`, `fieldPath: $.maxResults`), was Agenten die automatische Korrektur enorm erleichtert.
2. **Keine Stacktraces**: Kein einziger Fehlerfall leakt interne Exceptions oder Stacktraces. Alle Fehler werden kontrolliert als `McpToolResults.InvalidArgument` oder domänenspezifische Fehlercodes zurückgegeben.
3. **Schutz vor Phantom-Parametern**: Übergebene Parameter, die nicht im Schema existieren, werden sofort mit `Unbekanntes Argument: <name>` und `fieldPath: $.<name>` abgewiesen ([TargetPathToolRegistrationOptions.cs:66](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Registration/TargetPathToolRegistrationOptions.cs#L66)).

---

### [Minor] Befund F-08: Asymmetrie bei Cross-Target Fehlercodes
- **Tool(s)**: Alle zielgebundenen Tools
- **Quellcode**:
  - [AnalysisToolCall.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/AnalysisToolCall.cs)
  - [AssemblyAnalysisDispatcher.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisDispatcher.cs)
- **Evidenz**:
  Fall 1: Assembly-Target (`LOCAL-01`) an Source-Tool (`get_violations`) übergeben:
  ```text
  [ERROR]: ASSEMBLY_TARGET_UNSUPPORTED: Dieses Tool unterstützt das Assembly-Ziel nicht.
  operationStatus: unsupported
  code: ASSEMBLY_TARGET_UNSUPPORTED
  completeness: unsupported
  next: refine_scope — Ein Tool verwenden, das die Target-Herkunft unterstützt.
  ```
  Fall 2: Source-Target (`AiNetLinter.slnx`) an Assembly-Tool (`inspect_assembly`) übergeben:
  ```text
  [ERROR]: INVALID_ARGUMENT: Dieses Tool unterstützt kein Projekt-Ziel.
  operationStatus: invalid_argument
  code: INVALID_ARGUMENT
  completeness: not_applicable
  next: request_detail — targetPath auf eine vorhandene .dll/.exe-Datei setzen...
  ```
- **Problem**:
  Beide Fälle sind semantisch identisch: Ein Target mit der falschen Herkunft wurde übergeben.
  - Fall 1 liefert `ASSEMBLY_TARGET_UNSUPPORTED` mit `operationStatus: unsupported` und `completeness: unsupported`.
  - Fall 2 liefert `INVALID_ARGUMENT` mit `operationStatus: invalid_argument` und `completeness: not_applicable`.
  Für einen Agenten, der Fehler programmgesteuert auswertet, ist diese Diskrepanz schwer nachvollziehbar.
- **Reproduktion**:
  `get_violations` mit `.dll` aufrufen vs. `inspect_assembly` mit `.slnx` aufrufen.
- **Empfehlung**:
  Einheitlichen Fehlercode einführen, z. B. `SOURCE_TARGET_UNSUPPORTED` (analog zu `ASSEMBLY_TARGET_UNSUPPORTED`) oder beide Fälle einheitlich mit `operationStatus: unsupported` und passendem Hint behandeln.

---

### [Minor] Befund F-09: `FALSE-01` meldet `next: request_detail` trotz "Keine Wiederholung nötig"
- **Tool(s)**: `get_server_health`, `inspect_assembly`
- **Quellcode**:
  - [AssemblyAnalysisToolSupport.cs:70](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupport.cs#L70)
  - [McpNavigationProjection.cs:107](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Registration/McpNavigationProjection.cs#L107)
- **Evidenz**:
  Aufruf mit einer nativen Windows-EXE (`FALSE-01`):
  ```text
  [ERROR]: INVALID_ASSEMBLY: Die Datei enthält keine .NET-Metadaten. Hinweis: verwaltete .NET-.dll oder .exe mit IL erforderlich.
    hint: Keine Wiederholung nötig: Das Ziel ist keine verwaltete .NET-Assembly mit IL; eine passende .dll oder .exe mit .NET-Metadaten angeben.

  ## Navigation
  - operationStatus: `invalid_assembly`
  - result: available=`false`, code=`INVALID_ASSEMBLY`
  - completeness: `not_applicable`
  - next: `request_detail` — Keine Wiederholung nötig: Das Ziel ist keine verwaltete .NET-Assembly mit IL...
  ```
- **Problem**:
  Im Text steht ausdrücklich `"Keine Wiederholung nötig"`. Im maschinenlesbaren Navigation-Feld steht aber `next: request_detail`.
  Ein Agent, der maschinenlesbar `next == "request_detail"` auswertet, wird versucht sein, den Aufruf mit mehr Details oder anderem Paging zu wiederholen, obwohl die Datei prinzipiell inkompatibel ist.
- **Reproduktion**:
  Beliebiges Assembly-Tool mit nativer Executable (`FALSE-01`) aufrufen.
- **Empfehlung**:
  Bei nicht behebbaren Formatfehlern wie `INVALID_ASSEMBLY` sollte `next: "none"` oder `next: "switch_target"` signalisiert werden, nicht `request_detail`.
