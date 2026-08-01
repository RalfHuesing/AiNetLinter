---
task: codegraph-mcp-server
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-01
carried_forward_from: tasks/codegraph-mcp (drift-loop, gelöscht — siehe konzept.md "Bereits umgesetzt")
---

# Tech-Debt-Log: codegraph-mcp-server

Append-only. Jeder Eintrag ist eine während eines Reviews beobachtete, aber
bewusst **nicht** gefixte Auffälligkeit außerhalb des Scopes der jeweiligen
Einheit (Architektur, Anti-Pattern, Duplikation, Konsistenz).

**Priorität ist reine Sortierhilfe für den Menschen, kein Auslöser.**
Bewusst `hoch`/`mittel`/`niedrig` (deutsch) statt `CRITICAL`/`MAJOR`/`MINOR`,
um jede Verwechslung mit blockierenden Findings in einem Review auszuschließen
— kein Eintrag hier führt automatisch zu einer neuen Arbeitseinheit. Das
entscheidet ausschließlich der Nutzer.

TD-001 bis TD-007 stammen aus den `drift-loop`-Reviews des Vorgänger-Tasks
(`tasks/codegraph-mcp`, inzwischen gelöscht, Git-Historie bleibt erhalten).
Alle sind unverändert **offen** — der Umzug in diesen Ordner ist keine
inhaltliche Neubewertung.

## Index

| ID | Bereich / Datei | Priorität | Kurzfassung |
|---|---|---|---|
| TD-001 | `AiNetLinter.csproj` (`ModelContextProtocol`-Paket) | niedrig | Transitive `Microsoft.Extensions.AI.Abstractions`-Abhängigkeit ungenutzt mitgezogen. |
| TD-002 | `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` | niedrig | End-to-End-Test startet echten Subprozess (`AiNetLinter.exe`), spürbar länger als Unit-Tests. |
| TD-003 | `src/AiNetLinter/Baseline/SourceFileCatalog.cs` (`RegisterMSBuild`) | mittel | Nicht-thread-sicherer Check-then-Act führt bei parallel laufenden Testklassen intermittierend zu `InvalidOperationException`. |
| TD-004 | `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` + Registrar-Klassen | mittel | Wiederkehrender `AIContextFootprint`-Druck (Limit 2500) auf Sammelpunkte der Tool-Registrierung; bereits dreimal per Aufteilung in weitere Registrar-Klasse aufgefangen (`SymbolGraphToolRegistrations`/`FileStructureToolRegistrations`/`AnalysisToolRegistrations`), für `search_pattern` voraussichtlich eine vierte nötig. |
| TD-005 | `src/AiNetLinter/Mcp/Tools/*Tool.cs` (pro-Tool-Klassen) | mittel | `McpCodeGraphServer` als Parametertyp einer Tool-`ExecuteAsync`-Methode zieht bereits einen Großteil des `AIContextFootprint`-Budgets transitiv mit; etabliertes Gegenmuster ("dünner Dispatch + separate Formatter-/Scanner-Datei ohne `McpCodeGraphServer`-Abhängigkeit") funktioniert, muss aber weiter bewusst angewendet werden. |
| TD-006 | `src/AiNetLinter/Mcp/Tools/GetIndexScopeScanner.cs` vs. `src/AiNetLinter/Web/WebFileCatalog.cs` | niedrig | `.xaml`/`.html`-Scan dupliziert `IsGeneratedPath`/`SafeEnumerateFiles` aus `WebFileCatalog` 1:1 statt sie wiederzuverwenden. |
| TD-007 | `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` (`TryApplyContentChange`) | niedrig | Methode hat 5 Parameter (`Document, string, DateTime, FileState, ref Solution`), über `MaxMethodParameterCount` = 4; vorbestehend, `Selbst-Lint` schlägt aktuell nicht an (`MaxMethodParameterCountForNonPublic`-Override), Refactor-Kandidat in einen Input-`record`. |
| TD-008 | `src/AiNetLinter/rules.json` (`PathOverrides` für `FindReferencesTool`/`FindSymbolTool`) | niedrig | `Config`-Property auf `McpCodeGraphServer` zieht den `Configuration`-Namespace (~750 Zeilen) transitiv in alle Tool-Klassen, die den Server referenzieren — via `PathOverrides` (`MaxAIContextFootprint: 2700`) statt strukturellem Fix (`ILinterEngineConfig`-Kapselung) aufgefangen. Wird durch jede weitere Konfigurations-Erweiterung an `McpCodeGraphServer` (z. B. für die neuen P0/P1-Erweiterungen aus `konzept.md`) potenziell verschärft — beim nächsten Antasten von `McpCodeGraphServer` mitprüfen. |
| TD-009 | `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` (Konstruktor) | mittel | 5/5 Parameter am `MaxConstructorDependencies`-Limit, keine Reserve für die P0/P1-`McpCodeGraphServer`-Erweiterungen aus `konzept.md`. |
| TD-010 | `src/AiNetLinter/Mcp/Tools/SearchPatternTool.cs` (Footprint) | mittel | 2482/2500 (18 Z. Puffer) — knapp; `McpCodeGraphServer.Config`-Pull-in (~1110 Z. Configuration-Namespace) trifft das Tool beim nächsten analyse-orientierten Tool-Block, der denselben Server referenziert. Strukturelle Lösung `ILinterEngineConfig`-Interface (4-6h Refactor, in keiner Einheit bisher gescoped). Pragmatik: `PathOverrides: 2700` analog TD-008. |
| TD-011 | `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` (Footprint) | niedrig | 2487/2500 (13 Z. Puffer) — knapp; 5. Registrar-Klasse wahrscheinlich nötig beim nächsten Symbolgraph-Tool, das dazukommt. Erkannt im `units/002/result.md` (Anhang Footprint-Messung). |

## Einträge

### TD-001 — Ungenutzte transitive `Microsoft.Extensions.AI.Abstractions`-Abhängigkeit [Priorität: niedrig]

- **Ort:** `src/AiNetLinter/AiNetLinter.csproj` — `PackageReference Include="ModelContextProtocol"`.
- **Befund:** Zieht `Microsoft.Extensions.AI.Abstractions` transitiv mit (SDK-Features wie `SampleAsync`/`IChatClient`), aktuell ungenutzt.
- **Vorschlag:** Bei Bedarf prüfen, ob eine gezieltere Paket-Referenz existiert (nur Server statt vollem SDK).
- **Status:** offen

### TD-002 — Subprozess-basierter E2E-Test ohne Fixture-Pool [Priorität: niedrig]

- **Ort:** `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs`.
- **Befund:** Der einzige echte End-to-End-Test startet pro Testlauf einen vollständigen Subprozess inkl. MSBuildLocator-Registrierung und Solution-Load — spürbar langsamer als reine In-Process-Tests.
- **Vorschlag:** Bei weiteren Subprozess-basierten MCP-Integrationstests (EPIC-07) einen gemeinsamen, wiederverwendbaren Fixture-Prozess bzw. In-Memory-Transport erwägen.
- **Status:** offen

### TD-003 — Race Condition in `SourceFileCatalog.RegisterMSBuild` bei paralleler Testausführung [Priorität: mittel]

- **Ort:** `src/AiNetLinter/Baseline/SourceFileCatalog.cs`, `RegisterMSBuild()` — nicht-thread-sicherer Check-then-Act (`if (!MSBuildLocator.IsRegistered)`).
- **Befund:** Führt bei parallel laufenden Testklassen, die `SourceFileCatalog.LoadAsync` erstmalig aufrufen, intermittierend zu `InvalidOperationException` (beobachtet, reproduziert als Timing-Flake — ein direkt anschließender Lauf war grün).
- **Vorschlag:** `RegisterMSBuild()` mit statischem Lock absichern (Check-Lock-Check) und/oder betroffene Testklassen in eine gemeinsame, nicht-parallele xUnit-Collection stecken. Vor weiteren MCP-Integrationstests (EPIC-07) angehen, da die Kollisionswahrscheinlichkeit mit jeder weiteren parallelen Testklasse steigt.
- **Status:** offen

### TD-004 — Wiederkehrender `AIContextFootprint`-Druck auf Tool-Registrierungs-Sammelpunkte [Priorität: mittel]

- **Ort:** `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` + `SymbolGraphToolRegistrations.cs`/`FileStructureToolRegistrations.cs`/`AnalysisToolRegistrations.cs`.
- **Befund:** Bereits ab dem ersten Tool musste die Registrierung aus `McpServerCommand.cs` ausgelagert werden, um `AIContextFootprint` (Limit 2500) nicht zu reißen. Seitdem wiederholt beobachtet: Faktor ist die **eigene Zeilenzahl der jeweiligen Sammelklasse selbst** (zählt in den Footprint mit), nicht nur die Body-Aufrufe der Tool-Klassen. Trend ~11-15 Zeilen Zuwachs pro `tools.Add(...)`-Eintrag. Bereits zweimal durch Aufteilung in eine weitere Registrar-Klasse aufgefangen; `FileStructureToolRegistrations` lag beim dritten Tool bei 2492/2500 (4 Zeilen Puffer) vor der Auslagerung in `AnalysisToolRegistrations`.
- **Vorschlag:** Für `search_pattern` (letztes EPIC-04-Tool) Footprint der drei bestehenden Registrar-Klassen vorab prüfen; bei Bedarf eine vierte Registrar-Klasse einplanen statt reaktiv nach gerissenem Limit.
- **Status:** offen

### TD-005 — `McpCodeGraphServer`-Parameter lässt Tool-Klassen kaum eigenen `AIContextFootprint`-Spielraum [Priorität: mittel]

- **Ort:** `src/AiNetLinter/Mcp/Tools/*Tool.cs` — jede Klasse mit `ExecuteAsync(McpCodeGraphServer state, ...)`-Signatur.
- **Befund:** `McpCodeGraphServer` zieht über `SourceFileCatalog`/Config-Klassen bereits einen erheblichen Teil des 2500-Zeilen-Limits transitiv mit — `AIContextFootprintCalculator` zählt dabei auch die eigene Dateilänge der Zielklasse. Mehrfach beobachtet, dass genau dadurch eine Tool-Klasse (nicht die Registrierung) das Limit reißt. Etabliertes Gegenmuster: „dünner Dispatch + separate Formatter-/Scanner-Datei ohne `McpCodeGraphServer`-Abhängigkeit" — funktioniert konsequent, wenn von Anfang an angewendet, nicht erst reaktiv.
- **Vorschlag:** Für neue Tools (`search_pattern`) und für jede der neuen Erweiterungen aus `konzept.md`, die `McpCodeGraphServer` weiter aufwerten (z. B. `--mcp-log`-Zustand, „lädt noch"-Zustand), das Muster von Anfang an anwenden, nicht nachträglich.
- **Status:** offen

### TD-006 — `.xaml`/`.html`-Scan dupliziert `WebFileCatalog`-Hilfsmethoden statt sie wiederzuverwenden [Priorität: niedrig]

- **Ort:** `src/AiNetLinter/Mcp/Tools/GetIndexScopeScanner.cs` (`SafeEnumerateFiles`, `IsGeneratedPath`) gegenüber `src/AiNetLinter/Web/WebFileCatalog.cs` (wortgleiche Logik).
- **Befund:** Funktional identisch, keine Verhaltensabweichung, aber zwei Stellen, die bei künftigen Änderungen (z. B. weitere Ausschluss-Verzeichnisse) synchron gehalten werden müssten. Bewusst in Kauf genommen im ursprünglichen Step, kein Versehen.
- **Vorschlag:** Falls ein weiterer Dateisystem-Scan mit ähnlichem Ausschluss-Muster nötig wird (z. B. für die Last-Fixture-Generierung aus `konzept.md`), `SafeEnumerateFiles`/`IsGeneratedPath` einmalig in eine gemeinsame interne Hilfsklasse ziehen statt ein drittes Mal zu duplizieren.
- **Status:** offen

### TD-007 — `McpCodeGraphServer.TryApplyContentChange` mit 5 Parametern über `MaxMethodParameterCount` [Priorität: niedrig]

- **Ort:** `src/AiNetLinter/Mcp/McpCodeGraphServer.cs`, `TryApplyContentChange(Document, string, DateTime, FileState, ref Solution)`.
- **Befund:** 5 Parameter, `MaxMethodParameterCount` ist projektweit auf 4 gesetzt; Selbst-Lint schlägt aktuell nicht an, weil die Methode `private` ist und `MaxMethodParameterCountForNonPublic: 6` greift. Vorbestehend, nicht durch einen einzelnen Step verursacht.
- **Vorschlag:** Bei einem der nächsten Schritte, die `McpCodeGraphServer` ohnehin anfassen (z. B. Kaltstart-Entkopplung oder Verzeichnis-Sweep aus `konzept.md`), die 5 Parameter in einen Input-`record` ziehen — kombinierbar mit dem TD-003-Locking-Fix, da beide dieselbe Klasse betreffen.
- **Status:** offen

### TD-008 — `PathOverrides` als Footprint-Regression-Fix statt strukturellem Refactor [Priorität: niedrig]

- **Ort:** `rules.json` (`PathOverrides` für `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs`/`FindSymbolTool.cs`, `MaxAIContextFootprint: 2700`).
- **Befund:** Das Hinzufügen der `Config`-Property auf `McpCodeGraphServer` zog den `Configuration`-Namespace (~750 Zeilen) transitiv in alle Tool-Klassen mit `McpCodeGraphServer`-Referenz — `FindReferencesTool`/`FindSymbolTool` sprangen dadurch von ~1768 auf ~2519/2518 Zeilen. Mit `PathOverrides` pragmatisch aufgefangen (Precedent: `AuditCommand.cs`), nicht strukturell gelöst.
- **Vorschlag:** Eine bessere langfristige Lösung wäre ein `internal interface ILinterEngineConfig` o. ä., das nur die von `LinterEngine` benötigten Properties exportiert — geschätzt 4-6h-Refactor, lohnt sich erst, wenn `McpCodeGraphServer` durch weitere Konfigurations-Erweiterungen (siehe TD-005) noch mehr Tool-Klassen in dieselbe Footprint-Nähe zieht.
- **Status:** offen

### TD-009 — `McpCodeGraphServer`-Konstruktor mit 5 Parametern am `MaxConstructorDependencies`-Limit [Priorität: mittel]

- **Ort:** `src/AiNetLinter/Mcp/McpCodeGraphServer.cs:30-46` (Konstruktor mit 5 Parametern: `SourceFileCatalog?, ILintConsole?, int, Config?, ILintConsole?`).
- **Befund:** Die Parameterzahl deckt sich exakt mit dem `MaxConstructorDependencies: 5`-Limit aus `rules.json` (siehe `AiNetLinter.mdc` Z. 27) — der Selbst-Lint schlägt derzeit nicht an, weil der Wert **erreicht** ist, nicht überschritten. **Die Reserve ist weg.** Die `konzept.md` P0/P1-Erweiterungen ("`--mcp-log`", "Kaltstart entkoppeln", "Staleness-Sweep Verzeichnis-`mtime`", "`rules.json`-Auto-Discovery" u. a., Z. 207-324) werden `McpCodeGraphServer` in den nächsten Schritten mit hoher Wahrscheinlichkeit erneut erweitern — die erste sechste Dependency reißt das Limit und damit den Build.
- **Vorschlag:** Bei der nächsten Erweiterung an `McpCodeGraphServer` den Konstruktor auf ein Input-`record` umstellen (analog zum Vorschlag in TD-007 für `TryApplyContentChange`). Konkret: ein `internal sealed record McpCodeGraphServerOptions(SourceFileCatalog? Catalog, ILintConsole Console, int MaxLineCount, Config Config)` (oder vergleichbar). Dadurch wachsen zukünftige Konfigurations-Erweiterungen am `record` (additive Property), nicht an der Parameterliste. **Erkannt im Review von Einheit 001** (Kritiker-Vorschlag in `units/001/review.md` Abschnitt "Tech-Debt-Vorschlag").
- **Status:** offen

### TD-010 — `SearchPatternTool`-Footprint knapp am `AIContextFootprint`-Limit [Priorität: mittel]

- **Ort:** `src/AiNetLinter/Mcp/Tools/SearchPatternTool.cs` (gemessen 2482/2500, 18 Z. Puffer, Stand `28e6e58`).
- **Befund:** Der `McpCodeGraphServer.Config`-Property-Pull-in zieht den `Configuration`-Namespace (~1110 Z.) transitiv in alle Tool-Klassen mit `McpCodeGraphServer`-Referenz — derselbe Mechanismus wie bei `FindSymbolTool`/`FindReferencesTool` (TD-008, dort bereits durch `PathOverrides: 2700` pragmatisch aufgefangen). `SearchPatternTool` hat aktuell 18 Z. Puffer; jede künftige Erweiterung am `Configuration`-Namespace (z. B. Properties für die P0/P1-`McpCodeGraphServer`-Erweiterungen aus `konzept.md` Z. 207-324) treibt das Tool mit hoher Wahrscheinlichkeit über 2500 und reißt das Build.
- **Vorschlag:** Beim nächsten analyse-orientierten Tool-Block, der `McpCodeGraphServer` referenziert: entweder `PathOverrides: 2700` analog TD-008 setzen (Pragmatik, etabliertes Precedent) ODER den ohnehin nötigen `ILinterEngineConfig`-Refactor (TD-008-Vorschlag) endlich angehen, der TD-008/TD-010 gemeinsam strukturell löst. **Strukturelle Schließung allein TD-010-spezifisch nicht möglich** — der Refactor ist eine TD-008/TD-010-Investition.
- **Status:** offen

### TD-011 — `SymbolGraphToolRegistrations`-Footprint knapp [Priorität: niedrig]

- **Ort:** `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` (gemessen 2487/2500, 13 Z. Puffer, Stand `28e6e58`).
- **Befund:** Die 4. Registrar-Klasse (`AnalysisToolRegistrations`) wurde in Einheit 002 nicht nötig (TD-004-Vorhersage widerlegt), aber die Symbolgraph-Registrar-Klasse selbst ist jetzt am Limit (13 Z. Puffer). Sobald ein weiteres Symbolgraph-Tool dazukommt (z. B. `get_symbol_body` aus `konzept.md` P2-Backlog, oder eine Erweiterung an `find_symbol`/`find_references`/`get_impact`), ist eine 5. Registrar-Klasse wahrscheinlich.
- **Vorschlag:** Beim nächsten Symbolgraph-Tool-Block die Footprints aller drei existierenden Registrar-Klassen re-messen (Planer-Pflicht-Check, siehe `units/002/plan.md` Check 4 als Vorbild) und ggf. eine 5. Registrar-Klasse einplanen.
- **Status:** offen
