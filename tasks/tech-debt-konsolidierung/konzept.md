---
task: tech-debt-konsolidierung
type: konzept
status: in-progress
created: 2026-08-05
description: Konsolidierung aller offenen Tech-Debt-Punkte aus den vergangenen Tasks (codegraph-mcp-finish, mcp-call-logging-fuer-agenten-analyse, verbesserungen-mcp)
---

# Konzept: Konsolidierung offener Tech Debt (AiNetLinter)

Dieses Konzept bündelt alle verbleibenden, offenen Tech-Debt-Einträge aus den bisherigen Tasks (`codegraph-mcp-finish`, `mcp-call-logging-fuer-agenten-analyse`, `verbesserungen-mcp`), damit diese geordnet nachverfolgt und in zukünftigen Refactoring-Schritten abgearbeitet werden können.

---

## Übersicht der offenen Tech-Debt-Punkte

| NEUE ID | URSPRÜNGLICHE ID | TASK-QUELLE | BEREICH / DATEIEN | PRIO | KURZFASSUNG |
|---|---|---|---|---|---|
| **TD-001** | TD-001 | `verbesserungen-mcp` | `src/AiNetLinter.Tests/Mcp/Tools/*ToolTests.cs` | mittel | Regex `Dateien?` in Aggregat-Warnung-Tests matcht nur Plural. |
| **TD-002** | TD-002 | `verbesserungen-mcp` | `src/AiNetLinter/Mcp/Tools/GetIndexScopeScanner.cs` | niedrig | `FormatBreakdown` hartkodiert „Dateien" auch bei 1 Datei („1 Dateien"). |
| **TD-003** | TD-003 | `verbesserungen-mcp` | `src/AiNetLinter.Tests` | mittel | Voller `dotnet test`-Lauf bricht in Sandbox unter extremer Paralelllast intermittierend ab. |
| **TD-004** | TD-004 | `verbesserungen-mcp` | `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs` | niedrig | Zerrissener/abgebrochener XML-Doc-Kommentar an `ExecuteAsync`. |
| **TD-005** | TD-005 | `verbesserungen-mcp` | `rules.json`, `src/AiNetLinter.Tests/CliIntegrationTests.cs` | mittel | Footprint-Schwellwert 2800 für `AnalysisToolRegistrations.cs` knapp; `CliIntegrationTests` reagiert fragil auf globale Violations. |
| **TD-006** | TD-011 | `codegraph-mcp-finish` | `src/AiNetLinter/Mcp/*ToolRegistrations.cs` | mittel | Footprint-Druck auf 3 Tool-Registrar-Sammelklassen. |

---

## Detaillierte Beschreibungen

### TD-001 — Fehlerhafte `Dateien?`-Regex in Aggregat-Warnung-Tests maskiert Singular-Fall
- **Quelle:** `verbesserungen-mcp` (TD-001)
- **Betroffene Dateien:** `src/AiNetLinter.Tests/Mcp/Tools/GetIndexScopeToolTests.cs:107`, `GetHotspotsToolTests.cs:109` u. a.
- **Befund:** Die Regex `\d+\s+Dateien?\s+haben` matcht grammatikalisch nur den Plural („N Dateien", da sich das `?` nur auf das `n` bezieht). Wenn ein Workspace genau 1 Datei mit Compile-Fehlern enthält, scheitert der Test an der Assertion, obwohl die Produktionslogik (`FormatAggregateWarning`) sauber zwischen „Datei" und „Dateien" unterscheidet.
- **Handlungsempfehlung:** Test-Assertions vereinheitlichen auf `\d+\s+Datei(en)?\s+haben` oder eine gemeinsame Assertion-Hilfsmethode einführen.

### TD-002 — `GetIndexScopeScanner.FormatBreakdown` pluralisiert „Datei" nie
- **Quelle:** `verbesserungen-mcp` (TD-002)
- **Betroffene Dateien:** `src/AiNetLinter/Mcp/Tools/GetIndexScopeScanner.cs:85-93`
- **Befund:** In `FormatBreakdown` ist für alle Datei-Typen (`.cs`, `.css`, `.razor` etc.) fest der String `"Dateien"` hinterlegt (z. B. `$"{csCount} Dateien"`). Bei genau einer Datei wird `"1 Dateien"` statt `"1 Datei"` ausgegeben.
- **Handlungsempfehlung:** Eine grammatikalische Unterscheidung (Singular/Plural) analog zu `McpCompileDiagnostics.FormatAggregateWarning` ergänzen.

### TD-003 — Intermittierende Testhost-Abstürze bei vollem `dotnet test`-Lauf unter Paralelllast
- **Quelle:** `verbesserungen-mcp` (TD-003)
- **Betroffene Dateien:** `src/AiNetLinter.Tests` (Gesamt-Testsuite)
- **Befund:** Beim Ausführen der gesamten Testsuite (`dotnet test`) kommt es in manchen Sandbox-Umgebungen bei stark parallelen MSBuildWorkspace- und Subprozess-Tests (z. B. `McpTestClientParallelTests`) vereinzelt zu einem Absturz des Testhost-Prozesses („Der Testhostprozess ist abgestürzt").
- **Handlungsempfehlung:** Gezielte Identifizierung von Ressourcen-Spitzen; ggf. Begrenzung der Parallelität über Semaphoren (`SubprocessConcurrencyGate`) oder Timeouts justieren.

### TD-004 — Zerrissener XML-Doc-Kommentar an `FindReferencesTool.ExecuteAsync`
- **Quelle:** `verbesserungen-mcp` (TD-004)
- **Betroffene Dateien:** `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs:27-35`
- **Befund:** Der XML-Doc-Kommentar bricht mitten im Gedanken ab („…Stellt dem Aufrufstellen-Output einen\nDateien hat…").
- **Handlungsempfehlung:** Den XML-Doc-Kommentar an `ExecuteAsync` lesbar und grammatikalisch korrekt vervollständigen.

### TD-005 — `AIContextFootprint`-Schwellwert 2800 für `AnalysisToolRegistrations.cs` knapp
- **Quelle:** `verbesserungen-mcp` (TD-005)
- **Betroffene Dateien:** `rules.json` (PathOverride) und `src/AiNetLinter.Tests/CliIntegrationTests.cs`
- **Befund:** Der Schwellwert `MaxAIContextFootprint: 2800` für `AnalysisToolRegistrations.cs` bietet wenig Puffer. Kleine Erweiterungen an transitiv abhängigen Klassen (wie `McpCodeGraphServer.cs`) lösen sofort eine Linter-Violation aus, was den `CliIntegrationTest` zum Fehlschlagen bringt.
- **Handlungsempfehlung:** Schwellwert in `rules.json` moderat anheben (z. B. auf 2820) oder `CliIntegrationTests` so anpassen, dass nur spezifische Output-Abschnitte statt der gesamten Solution geprüft werden.

### TD-006 — Footprint-Druck auf 3 Tool-Registrar-Sammelklassen
- **Quelle:** `codegraph-mcp-finish` (TD-011)
- **Betroffene Dateien:** `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs`, `FileStructureToolRegistrations.cs`, `AnalysisToolRegistrations.cs`
- **Befund:** Die Registrar-Klassen wachsen bei jedem neuen MCP-Tool weiter an. Eine naive Vererbung würde das Dispatcher-Pattern verfälschen und den Footprint sogar erhöhen.
- **Handlungsempfehlung:** Prüfung von kategoriespezifischen Helper-Klassen (z. B. gemeinsamer Lambda-Body-Helper für Call-Log-Dispatching), um den Footprint der Registrars schlank zu halten.

### TD-006 — Status nach Umsetzungsversuch (tech-debt-konsolidierung)
- **Ergebnis:** **Nicht lösbar mit aktueller MCP-SDK-Version.**
- **Befund aus Implementierungsversuch:** Das MCP-SDK (`ModelContextProtocol.Server.McpServerTool.Create`) verwendet die **Lambda-Parameternamen direkt als JSON-Property-Namen** des Tool-Input-Schemas. Sobald ein Helper die Caller-Lambdas umschließt, gehen die semantisch wichtigen Namen (`filePath`, `scopeFilter`, `symbolIdentifier`, `maxResults`, `depth`, …) verloren. Das resultierende Schema bricht alle Live-Tests (`McpLiveRepositoryTests`, `McpServerAllToolsE2ETests`, `McpServerCommandTests`).
- **Workaround-Versuche (alle verworfen):**
  - Helper mit konkreten Overloads (0-arg, 1-string, 1-string?, string+int, string+int+int): Lambdas müssen im Helper neu deklariert werden, Parameternamen gehen verloren.
  - `McpServerToolCreateOptions` bietet nur `OutputSchema`, kein `InputSchema` — keine Möglichkeit, das Schema unabhängig zu setzen.
  - `Expression<Func<…>>`-basierte Reflection: würde starke Typisierung oder Signatur-Korrektheit kosten.
- **Möglicher Zukunfts-Lösungsansatz:** Migration auf `AIFunctionMcpServerTool.Create(Delegate, options)` (Delegate direkt, kein Schema-Verlust) — dann muss die `if (callLog is null)`-Verzweigung jedoch in den Delegate-Body zurückwandern, was den Helper-Charakter wieder aufhebt.
- **Folge:** TD-006 bleibt als **dokumentierte, nicht lösbare Tech-Schuld** bestehen. Die `if (callLog is null) … else …`-Boilerplate bleibt 10× im Code. Sollte bei zukünftigem SDK-Upgrade erneut evaluiert werden.
