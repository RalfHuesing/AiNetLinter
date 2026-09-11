---
status: ready
execution_mode: autonomous
open_questions: []
---

# Konzept: Restrukturierung & Verschlankung der MCP-Test-Suite

## 1. Executive Summary & Problemstellung

Die aktuelle Test-Suite von **AiNetLinter** weist im Bereich des MCP-Servers (Model Context Protocol) ein ausgeprägtes **Ice-Cream-Cone-Antipattern** (umgekehrte Testpyramide) auf:

Für elementare Prüfungen (wie Parameter-Validierungen, Zahlen-Grenzwerte, Formatierungsstrings oder Tool-Auflistungen) wird in Dutzenden von Tests die **komplette MCP-stdio-Interaktion über echte Betriebssystem-Subprozesse (`AiNetLinter.exe --mcp-server`)** ausgeführt.

### Konsequenzen des Ist-Zustands
1. **Extremer Ressourcen- und Zeitverbrauch**: Das Spawnen separater OS-Prozesse, das Aufbauen von stdin/stdout-Pipes, das Laden von MSBuild/Roslyn-Workspaces und das Hin- und Herschicken von JSON-RPC-Nachrichten erzeugen massive CPU- und I/O-Last.
2. **Workaround-Infrastruktur im Testprojekt**: Wegen der Prozessflut mussten Hilfskonstrukte wie [`SubprocessLifetimeBudget`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Platform/McpProcessHost.cs) (globale Semaphore) und überlange Timeouts von 60 bis 180 Sekunden ([`DefaultCallTimeout = TimeSpan.FromSeconds(180)`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/Platform/McpProcessHost.cs#L32)) eingeführt werden, um Runner-Abbrüche und Deadlocks unter Volllast zu verhindern.
3. **Flakiness-Risiko**: Subprozess-I/O und Pipe-Drains sind unter Last anfällig für TaskCanceledExceptions und Race Conditions.
4. **Hohe Redundanz**: Dieselbe Fachlogik wird oft doppelt getestet: einmal schnell in-memory in `FastTests` und nochmals langsam über Stdio in `IntegrationTests`.

---

## 2. Detaillierte Befunde & Evidenz im Code

### Befund 1: Reine Parameter-, Schema- und Feldpfad-Validierung im Stdio-Subprozess

In [`McpServerArgumentValidationE2ETests.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/Tools/McpServerArgumentValidationE2ETests.cs) (über 500 Zeilen) sowie den zugehörigen Teil-Dateien ([`FieldPaths.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/Tools/McpServerArgumentValidationE2ETests.FieldPaths.cs), [`D003.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/Tools/McpServerArgumentValidationE2ETests.D003.cs), [`A002.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/Tools/McpServerArgumentValidationE2ETests.A002.cs)):

- **Art der Tests**: Es werden über 30 einzelne Fakten/Theorien ausgeführt, die über `_fixture.Client.CallToolAsync(...)` per Stdio-RPC an einen externen Prozess prüfen:
  - `maxResults = 0` $\rightarrow$ `INVALID_ARGUMENT` ("maxResults muss mindestens 1 sein.")
  - `namePatterns = []` oder `[""]` $\rightarrow$ `INVALID_ARGUMENT` mit `fieldPath: "$.namePatterns"`
  - `kind = "trait"` $\rightarrow$ `INVALID_ARGUMENT`
  - Unbekannte Parameternamen (z. B. `wrongParam`, `anotherUnknownField`) $\rightarrow$ `INVALID_ARGUMENT`
  - Fehlende Pflichtfelder (`symbolIdentifier`, `targetPath`) bei `get_call_tree`, `get_type_hierarchy`, `find_references`
- **Ursache/Wirkung**: Diese Validierungen stehen direkt im C#-Code der jeweiligen Tools (z. B. `FindSymbolTool.cs`, `DeadCodeTool.cs`) oder im Dispatcher. Sie benötigen weder einen laufenden Subprozess noch Stdio-Pipes oder geladene Roslyn-Kataloge.
- **Redundanz**: In [`FindSymbolToolTests.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.FastTests/Mcp/Tools/SymbolGraph/FindSymbolToolTests.cs#L44-L60) in `AiNetLinter.FastTests` existieren für dieselben Fälle bereits direkte In-Memory-Tests via `FindSymbolTool.ExecuteAsync(...)`, die in Mikrosekunden durchlaufen.

### Befund 2: Triviale String-Vergleiche, Hilfetexte und Truncation-Marker als E2E-Tests

- **Feedback-String**: [`McpServerToolContractE2ETests.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/Tools/McpServerToolContractE2ETests.cs#L73-L88) führt `ReportObservabilityFeedback_ValidCall_ReturnsConfirmation` über Stdio aus. Das Tool `report_observability_feedback` formatiert lediglich einen String (`[INFO]: Feedback ... erfolgreich protokolliert.`). Dafür wird ein voller Subprozess-Roundtrip gefahren.
- **Ein-Test-Dateien für Truncation und Hints**:
  - [`McpServerCommandMissHintTests.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandMissHintTests.cs): 37 Zeilen, eine einzige Testmethode, um per Stdio zu prüfen, ob der Text `"Hinweis: kein C#-Symbol, aber Textfund"` ausgegeben wird.
  - [`McpServerCommandFindReferencesTests.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandFindReferencesTests.cs): 31 Zeilen, eine einzige Testmethode, um per Stdio zu prüfen, ob `maxResults = 2` den Text `"2 gezeigt"` enthält.
  - [`McpServerCommandFindSymbolTests.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandFindSymbolTests.cs): 46 Zeilen, 2 Tests für Truncation (`"2 gezeigt"`) und Batch-Miss-Hint.

### Befund 3: Redundante Tool-Registrierungstests via Subprozess

- [`McpHandshakeToolRegistrationTests.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/McpHandshakeToolRegistrationTests.cs): Startet `AiNetLinter.exe --mcp-server` als eigenen Subprozess via `StdioClientTransport`, verbindet den Client und prüft `client.ListToolsAsync()` darauf, ob `find_symbol`, `get_violations`, etc. vorhanden sind und ob in der Beschreibung von `get_file_tree` bestimmte Parameter dokumentiert sind.
- **Redundanz**: In [`WiringToolCollectionContractTests.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.FastTests/Mcp/Wiring/WiringToolCollectionContractTests.cs) in `AiNetLinter.FastTests` wird die gesamte Tool-Collection (`McpServerToolCollectionFactory.Build`) in-memory instantiiert und alle 33 Tools, InputSchemas und Annotations werden feingranular geprüft – ohne jeden Subprozess.

### Befund 4: Mehrfache Ad-hoc-Subprozess-Starts für Mini-Szenarien

Anstatt die langlebige Shared-Assembly-Fixture ([`ReadOnlyMcpHostFixture`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/Platform/ReadOnlyMcpHostFixture.cs)) zu nutzen, starten mehrere Testmethoden jeweils ihren **eigenen separaten Subprozess**:

- In [`McpServerCommandContractTests.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandContractTests.cs#L186-L203):
  - `RunAsync_ValidFixture_GetImpactWithGitRefReturnsCallSite` und `RunAsync_ValidFixture_GetImpactWithoutGitRefUncommittedReturnsCallSite`: Beide instanziieren ein temporäres Git-Verzeichnis ([`GitImpactMiniFixtureWorkspace`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Fixtures/FixtureWorkspaces.cs#L56)), rufen `McpProcessHost.StartAsync()` auf (neuer OS-Prozess `AiNetLinter.exe`), warten auf Initialisierung und prüfen dann lediglich `Assert.Contains("CalculatorCaller.cs", text)`.
- In [`McpServerCommandGetImpactTests.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandGetImpactTests.cs#L78-L110):
  - `RunAsync_ValidFixture_GetImpactGitBranchWithMaxResultsTruncates` und `RunAsync_ChangeContextCapsAboveContractAreClampedByHandler`: Zwei weitere vollständige Subprozess-Starts auf `GitImpactMiniFixtureWorkspace`, um Truncation ("2 gezeigt") und Clamping von `maxChangedSymbols` zu prüfen.
- In [`McpServerCommandErrorHandlingTests.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandErrorHandlingTests.cs#L29-L100):
  - Zwei separate Subprozesse (`AiNetLinter.exe`) mit bis zu 60s Timeout und 30 Retries, nur um zu prüfen, dass ein ungültiger Pfad `[ERROR]: INVALID_ARGUMENT` liefert bzw. Compile-Fehler das Linter-Tool nicht blockieren.

*Gegenbeispiel*: In [`GetImpactToolIntegrationTests.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/Tools/SymbolGraph/GetImpactToolIntegrationTests.cs) wird genau dieses GitImpact-Szenario bereits sauber **in-process** via `GetImpactTool.ExecuteAsync(state, ...)` getestet – völlig ohne OS-Prozess-Overhead!

---

## 3. Schützenswerter Kern: Wo Stdio-E2E-Tests unverzichtbar sind

Eine Verschlankung darf nicht die Sicherheit des Gesamtsystems gefährden. Es gibt spezifische Aspekte, die **ausschließlich** durch echte Subprozesse mit Stdio-Pipes abgesichert werden können:

1. **JSON-RPC-Framing & Stderr-Disziplin** ([`McpServerCommandJsonRpcFramingTests.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandJsonRpcFramingTests.cs)):
   - Verifiziert auf roher Byte-Ebene, dass **jede** Zeile auf stdout valides JSON-RPC 2.0 ist. Ein einziges versehentliches `Console.WriteLine` irgendwo in Core, Roslyn oder Third-Party-Libs würde das Framing der MCP-Session zerstören. Dieser Test ist als echter E2E-Wire-Test essenziell und bleibt erhalten.
2. **Prozess-Lifecycle, CLI-Flags & Signal-Handling** ([`McpServerLifetimeTests.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/McpServerLifetimeTests.cs), [`McpServerCommandAmbiguityE2ETests.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandAmbiguityE2ETests.cs)):
   - Prüft das Beenden bei Pipe-Close/EOF und den Exit-Code bei verbotenen CLI-Kombinationen (z. B. `--path` im MCP-Modus).
3. **Daemon-IPC & Multiprozess-Warmth** ([`ThinClientMcpProcessContractTests.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/Daemon/ThinClientMcpProcessContractTests.cs)):
   - Prüft echtes IPC-Verhalten zwischen Daemon-Prozess und Thin-Clients über Named Pipes / Sockets.
4. **Schlanker E2E-Smoke-Durchstich**:
   - 1 bis 2 fokussierte Smoke-Tests über die geteilte Fixture ([`ReadOnlyMcpHostFixture`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/Platform/ReadOnlyMcpHostFixture.cs)), die nachweisen, dass der Gesamtpfad (Binary $\rightarrow$ Stdio $\rightarrow$ MCP-Dispatcher $\rightarrow$ Roslyn $\rightarrow$ Antwort) nach dem Build funktionsfähig ist.

---

## 4. Ziel-Architektur & Lösungsansatz

### 4.1 Pyramiden-Sanierung nach Schichten

```
▲ E2E / Stdio-Wire (IntegrationTests):
│  - Nur echter Wire-Vertrag: Framing, stderr-Disziplin, CLI-Abbruch, Daemon-IPC.
│  - 1-2 repräsentative Smoke-Calls über Shared-Fixture.
┼─────────────────────────────────────────────────────────────────────────────
│ In-Process Integration (IntegrationTests):
│  - Roslyn- und Git-Integration gegen reale Workspaces (SourceFileCatalog).
│  - Aufruf via Tool.ExecuteAsync(...) oder Dispatcher in-process.
┼─────────────────────────────────────────────────────────────────────────────
▼ Unit / Komponente (FastTests):
   - Sämtliche Parameter-, Schema- und Feldpfad-Validierungen (INVALID_ARGUMENT).
   - Tool-Registrierung, Beschreibungen und Annotationen.
   - Textformatierungen, Truncation-Limits ("2 gezeigt"), Fehler-Codes.
```

### 4.2 Konkrete Maßnahmen

#### Maßnahme 1: Verlagerung aller Argument-Validierungen in `AiNetLinter.FastTests`
- Alle Tests aus `McpServerArgumentValidationE2ETests` (ungültige Bounds, leere Patterns, fehlende Pflichtfelder, unknown parameters) werden als schnelle In-Memory-Tests auf `Tool.ExecuteAsync` oder auf dem gemeinsamen Dispatcher/Validator in `FastTests` sichergestellt.
- Die über 500 Zeilen Stdio-Tests in `IntegrationTests/Mcp/Tools/McpServerArgumentValidationE2ETests*` werden daraufhin radikal bereinigt. Es verbleibt maximal ein einziger Test, der beweist, dass ein ungültiges Argument über Stdio das erwartete MCP-Fehlerformat erzeugt.

#### Maßnahme 2: Konsolidierung der Zersplitterten Stdio-Testklassen
- Die Klassen [`McpServerCommandMissHintTests.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandMissHintTests.cs), [`McpServerCommandFindReferencesTests.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandFindReferencesTests.cs) und [`McpServerCommandFindSymbolTests.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandFindSymbolTests.cs) werden aufgelöst:
  - Die Formatierungs- und Truncation-Assertions wandern in die jeweiligen `FastTests` (z. B. `FindReferencesToolTests.cs`, `FindSymbolToolTests.cs`).
  - Redundante Stdio-Aufrufe entfallen ersatzlos.

#### Maßnahme 3: Beseitigung redundanter Subprozess-Starts
- In `McpServerCommandContractTests.cs` und `McpServerCommandGetImpactTests.cs`:
  - Keine isolierten `McpProcessHost.StartAsync()`-Aufrufe mehr für einfache GitImpact- oder ChangeContext-Truncation-Tests.
  - Die GitImpact-Logik ist bereits in `GetImpactToolIntegrationTests.cs` in-process abgedeckt. Die verbleibenden Fälle werden dort als in-process Tests ergänzt.
- `McpHandshakeToolRegistrationTests.cs` entfällt, da `WiringToolCollectionContractTests.cs` in `FastTests` dieselbe Invariante vollständig abdeckt.

#### Maßnahme 4: Entlastung der Testinfrastruktur
- Nach dem Wegfall von ca. 50–70 unnötigen Stdio-RPCs und mehreren Ad-hoc-Prozess-Spawns kann das globale Prozessbudget und die extremen Timeouts (180s / 60s) auf normale Werte zurückgeführt werden.
- Die Ausführungszeit des Gates `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` sinkt signifikant.

---

## 5. Phasenplan für die spätere Umsetzung

| Phase | Fokus | Konkrete Schritte |
|---|---|---|
| **Phase 1: Validierung & Unit-Deckung** | `FastTests` | Fehlende In-Memory-Tests für Argumentvalidierung aller Tools in `FastTests` ergänzen (falls nicht schon vorhanden). |
| **Phase 2: E2E-Validierung entschlacken** | `IntegrationTests` | `McpServerArgumentValidationE2ETests` auf 1–2 Smoke-Fälle reduzieren; Partial-Dateien (`.FieldPaths.cs`, `.D003.cs`, `.A002.cs`) auflösen. |
| **Phase 3: Formatierungs- & Fragment-Klassen auflösen** | `IntegrationTests` | `MissHintTests`, `FindReferencesTests`, `FindSymbolTests`, `HandshakeToolRegistrationTests` in FastTests überführen und E2E-Klassen entfernen. |
| **Phase 4: Ad-hoc Subprozesse eliminieren** | `IntegrationTests` | Separate `McpProcessHost.StartAsync()`-Calls in `ContractTests` und `GetImpactTests` auf In-Process-Integrationstests umstellen. |
| **Phase 5: Verifikation & Cleanup** | Gates & Doku | Vollständiger Lauf beider Testprojekte; Messung der Laufzeitverbesserung; Anpassung von Timeouts. |

---

## 6. Erfolgskriterien

1. **Testabdeckung bleibt 100 % erhalten**: Keine fachliche Assertion (weder Parameterfehler, Truncation noch Fehler-Code) geht verloren; sie wird lediglich auf die korrekte, schnellere Test-Ebene verschoben.
2. **Drastische Reduktion von OS-Prozessen**: Die Anzahl gestarteter `AiNetLinter.exe`-Prozesse pro Testlauf sinkt um mindestens 50–70 %.
3. **Reduzierte Gate-Laufzeit & Robustheit**: `AiNetLinter.IntegrationTests` läuft deutlich schneller und ohne Timeout-Gefahr durch.
4. **Klares Test-Konzept**: Klare Trennung zwischen Wire-Protokoll-Tests (Stdio/Subprozess) und fachlichen Tool-Tests (In-Process/In-Memory).
