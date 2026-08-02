---
task: codegraph-mcp-server
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-02 (TD-003 geschlossen durch 007, TD-016a neu aus 007-Review)
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
| TD-003 | `src/AiNetLinter/Baseline/SourceFileCatalog.cs` (`RegisterMSBuild`) | mittel | ~~Nicht-thread-sicherer Check-then-Act führt bei parallel laufenden Testklassen intermittierend zu `InvalidOperationException`.~~ **Geschlossen durch Einheit 007** (Commit `49feb65`): statisches Lock + Check-Lock-Check-Pattern + 3 Tests (Reflection + 20 parallele `LoadAsync`-Calls + Idempotenz). |
| TD-004 | `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` + Registrar-Klassen | mittel | Wiederkehrender `AIContextFootprint`-Druck (Limit 2500) auf Sammelpunkte der Tool-Registrierung; bereits dreimal per Aufteilung in weitere Registrar-Klasse aufgefangen (`SymbolGraphToolRegistrations`/`FileStructureToolRegistrations`/`AnalysisToolRegistrations`), für `search_pattern` voraussichtlich eine vierte nötig. |
| TD-005 | `src/AiNetLinter/Mcp/Tools/*Tool.cs` (pro-Tool-Klassen) | mittel | `McpCodeGraphServer` als Parametertyp einer Tool-`ExecuteAsync`-Methode zieht bereits einen Großteil des `AIContextFootprint`-Budgets transitiv mit; etabliertes Gegenmuster ("dünner Dispatch + separate Formatter-/Scanner-Datei ohne `McpCodeGraphServer`-Abhängigkeit") funktioniert, muss aber weiter bewusst angewendet werden. |
| TD-006 | `src/AiNetLinter/Mcp/Tools/GetIndexScopeScanner.cs` vs. `src/AiNetLinter/Web/WebFileCatalog.cs` | niedrig | `.xaml`/`.html`-Scan dupliziert `IsGeneratedPath`/`SafeEnumerateFiles` aus `WebFileCatalog` 1:1 statt sie wiederzuverwenden. |
| TD-007 | `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` (`TryApplyContentChange`) | niedrig | Methode hat 5 Parameter (`Document, string, DateTime, FileState, ref Solution`), über `MaxMethodParameterCount` = 4; vorbestehend, `Selbst-Lint` schlägt aktuell nicht an (`MaxMethodParameterCountForNonPublic`-Override), Refactor-Kandidat in einen Input-`record`. |
| TD-008 | `src/AiNetLinter/rules.json` (`PathOverrides` für `FindReferencesTool`/`FindSymbolTool`) | niedrig | `Config`-Property auf `McpCodeGraphServer` zieht den `Configuration`-Namespace (~750 Zeilen) transitiv in alle Tool-Klassen, die den Server referenzieren — via `PathOverrides` (`MaxAIContextFootprint: 2700`) statt strukturellem Fix (`ILinterEngineConfig`-Kapselung) aufgefangen. Wird durch jede weitere Konfigurations-Erweiterung an `McpCodeGraphServer` (z. B. für die neuen P0/P1-Erweiterungen aus `konzept.md`) potenziell verschärft — beim nächsten Antasten von `McpCodeGraphServer` mitprüfen. |
| TD-009 | `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` (Konstruktor) | mittel | 5/5 Parameter am `MaxConstructorDependencies`-Limit, keine Reserve für die P0/P1-`McpCodeGraphServer`-Erweiterungen aus `konzept.md`. |
| TD-010 | `src/AiNetLinter/Mcp/Tools/SearchPatternTool.cs` (Footprint) | mittel | 2482/2500 (18 Z. Puffer) — knapp; `McpCodeGraphServer.Config`-Pull-in (~1110 Z. Configuration-Namespace) trifft das Tool beim nächsten analyse-orientierten Tool-Block, der denselben Server referenziert. Strukturelle Lösung `ILinterEngineConfig`-Interface (4-6h Refactor, in keiner Einheit bisher gescoped). Pragmatik: `PathOverrides: 2700` analog TD-008. |
| TD-011 | `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` (Footprint) | niedrig | 2494/2500 (6 Z. Puffer, Stand `3eb13bf` nach Einheit 005) — **knapp**; 5. Registrar-Klasse zwingend nötig beim nächsten Symbolgraph-Tool (z. B. `get_symbol_body` aus P2-Backlog, oder eine Erweiterung an `find_symbol`/`find_references`/`get_impact`). Erkannt in `units/002/result.md` und in `units/005/result.md` (6 → 5 → 4 Puffer-Schrumpfung pro 002/004/005). |
| TD-012 | `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` (kein Scanner-Split) | niedrig | ~~112 Z. Logik komplett im Tool, kein `FindSymbolScanner.cs` — einziges MCP-Tool ohne Scanner-Abspaltung.~~ **Geschlossen durch Einheit 004** (Commit `c6261ea`): `FindSymbolScanner.cs` (94 Z.) angelegt, TD-005-Muster erfüllt. |
| TD-013 | `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs:63` (Miss-Hint-Datei-Liste) | niedrig | ~~`string.Join(", ", missHits)` ohne Trunkierung — bei Last-Fixture (500 Dateien) könnte die Hint-Zeile Hunderte Dateien auflisten. `McpTruncation` (002) ist nicht auf den Miss-Hint angewendet.~~ **Geschlossen durch Einheit 004** (Commit `c6261ea`): `McpTruncation.TruncateFileList` als zweite Methode, konsistente Meta-Zeile für die Datei-Liste. |
| TD-014 | `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` (Footprint) | niedrig | 2484/2500 (16 Z. Puffer) — `ServerInstructions`-Block (+14 Z. in 003) hat die Klasse an die Grenze gebracht. Const-String sollte nicht weiter wachsen; P0/P1-Extensions (Kaltstart, `--mcp-log`, Auto-Discovery) reißen das Limit bei der nächsten Erweiterung. **Inline** beim nächsten Anlass: `McpServerOptionsBuilder` oder Init-`record` analog TD-009. |
| TD-015 | `src/AiNetLinter/Mcp/McpToolResults.cs:117` (`WarningsSection`) | niedrig | ~~Dead Code — Hilfsmethode hat keinen Production-Caller; alle 8 Tools mit Compile-Fehler-Warnhinweis (006) nutzen stattdessen `FindSymbolTool.BuildAggregateWarningAsync` + `McpToolResults.PrependWarning` (oder gleichwertig). Test (`McpToolResultsTests.cs:42-54`) ist tautologisch (`result == warningText`).~~ **Geschlossen durch Einheit 007** (Commit siehe unten): Methode + XML-Doc-Kommentar + tautologischer Test entfernt. |
| TD-016 | `tests/Fixtures/*/` (Fixture-Code-Duplikation) | niedrig | ~~`CopyFixture` / `IsGeneratedPath` / `FindSolutionRoot` in 4 Fixture-Workspace-Klassen dupliziert.~~ **Geschlossen durch Einheit 007** (Commit `6c872e4`, vor 007 angelegt): `FixtureWorkspaceBase.cs` (73 Z.) + `TestTempDirectory.cs` (58 Z.) eingefuehrt, `BaselineMiniFixtureWorkspace` und `SymbolGraphMiniFixtureWorkspace` erben jetzt davon. (Anmerkung 2026-08-01: nur 2 von 4 Workspace-Klassen wurden refaktoriert; `CompileErrorMiniFixtureWorkspace` und `GitImpactMiniFixtureWorkspace` enthalten weiterhin die duplizierten Helper. Siehe `units/007/result.md` Abschnitt "Tech-Debt-Beobachtung" — Folge-Refactor offen.) |
| TD-016a | `src/AiNetLinter.Tests/Fixtures/{CompileErrorMini,GitImpactMini}FixtureWorkspace.cs` | niedrig | Folge-Refactor aus TD-016: zwei der vier Fixture-Workspace-Klassen wurden in `6c872e4` nicht auf `FixtureWorkspaceBase` umgestellt und duplizieren weiterhin `CopyFixture`/`IsGeneratedPath`/`FindSolutionRoot`. |

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
- **Status:** **geschlossen** durch Einheit 007 (Commit `49feb65`): `SourceFileCatalog.RegisterMSBuild` mit `private static readonly object _msbuildRegistrationLock` + Check-Lock-Check-Pattern abgesichert. Struktureller A3: `RegisterMSBuild_HasStaticLockField_ForThreadSafeRegistration` (Reflection auf das Feld). Funktionale Verifikation: `LoadAsync_TwentyParallelCallsAcrossFixtures_AllSucceed` (smoke) + `LoadAsync_SecondSequentialCall_DoesNotRepatchBuildHost` (Idempotenz). Klasse von 286 auf 302 Z. gewachsen (vor 007: 286; +Lock-Feld + Kommentar; gut innerhalb `MaxLineCount: 500`). Workaround 006 (`ConsoleTestCollection`) bleibt als zusätzliche Schicht bestehen, ist aber nicht mehr die einzige Absicherung.

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
- **Status:** **verschärft** — Puffer-Schrumpfung von 13 (002) über 10 (004) auf 6 Z. (005). Stand `3eb13bf`. Eine 5. Registrar-Klasse ist beim nächsten Symbolgraph-Tool-Block zwingend nötig (nicht mehr nur „wahrscheinlich").

### TD-012 — `FindSymbolTool` ohne Scanner-Split (TD-005-Generalisierung) [Priorität: niedrig]

- **Ort:** `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` (112 Z. Logik, kein `FindSymbolScanner.cs`).
- **Befund:** `find_symbol` ist das einzige MCP-Tool ohne Scanner-Abspaltung — die gesamte Logik (`ExecuteAsync` + `FindMatchesAsync` + `FilterByKind` + `FormatSymbolLocations` + `DescribeKind`) lebt im Tool. Bei `search_pattern` (002) und `get_violations` (001) wurde das TD-005-Muster (dünner Dispatch + separate Scanner-Datei) von Anfang an angewendet, bei `find_symbol` (drift-loop-approved, vor 001) wurde es ausgelassen. Das Tool hat aktuell PathOverride 2700 mit 171 Z. Puffer — also **kein** akuter Handlungsdruck. **Erkannt im Review von Einheit 003** (Kritiker-Vorschlag in `units/003/review.md` "Vorschlag 1").
- **Vorschlag:** Beim nächsten Anlass, der `find_symbol` ohnehin anfasst (z. B. 004 Trunkierung in `find_symbol` analog `search_pattern` in 002), den Scanner-Split in derselben Einheit mitnehmen — dann kostet es ~10 Z. extra Diff, statt einer eigenständigen Refactor-Einheit. Konkret: `SearchPatternTool`-/`SearchPatternScanner`-Trennung als Vorbild.
- **Status:** **geschlossen** durch Einheit 004 (Commit `c6261ea`): `FindSymbolScanner.cs` (94 Z.) angelegt, 1:1-Pendant zu `SearchPatternScanner.cs` mit TD-005-Muster. `FindSymbolTool` von 2529 → 2491 Z. (Logik gewandert). `DescribeKind` und `FormatSymbolLocations` bewusst im Tool belassen (Cross-Tool-Wiederverwendung bzw. nur intern).

### TD-013 — `find_symbol`-Miss-Hint-Datei-Liste ohne Trunkierung [Priorität: niedrig]

- **Ort:** `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs:63` (`var fileList = string.Join(", ", missHits);`).
- **Befund:** Der Miss-Hint-Pfad hängt **alle** Treffer-Dateien kommasepariert an die Hint-Zeile. In `SymbolGraphMini` (1 Datei) und in der `AiNetLinter.slnx` (max. 3 Dateien mit dem 003-Pattern) kein Problem. Bei der Last-Fixture (500/5000 Dateien, P1-6) mit einem weitverbreiteten String-Literal könnte der Hint Hunderte Dateien auflisten — UX-Problem. `McpTruncation` (eingeführt in 002 für `search_pattern`) trunkiert den Haupt-Treffer-Output, ist aber **nicht** auf den Miss-Hint angewendet. **Erkannt im Review von Einheit 003** (Kritiker-Vorschlag "Vorschlag 2").
- **Vorschlag:** Beim nächsten `find_symbol`-Anlass (Trunkierung TD-013-Zusammenhang oder Last-Fixture-Messlauf) `McpTruncation` auf die Miss-Hint-Liste anwenden, mit konsistenter Meta-Zeile (z. B. `"[342 Dateien mit Textfund, 10 gezeigt — search_pattern für Details]"`).
- **Status:** **geschlossen** durch Einheit 004 (Commit `c6261ea`, `McpTruncation.TruncateFileList` als zweite Methode, konsistente Meta-Zeile). Verbleibendes Risiko: Last-Fixture-Messlauf (P1-6) zeigt ggf. Performance-Tuning-Bedarf, der als Folge-TD aufgenommen wird — nicht in TD-013-Scope.

### TD-014 — `McpServerOptionsFactory` Footprint knapp am Limit [Priorität: niedrig]

- **Ort:** `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` (gemessen 2484/2500, 16 Z. Puffer, Stand `dd4b44e`).
- **Befund:** Der `ServerInstructions`-Block (+14 Z. in 003) hat diese Klasse an die Grenze gebracht. Der Const-String ist konzeptuell bindend (kanonische Formulierung laut Plan-Schritt 3) und sollte **nicht** weiter wachsen. Die P0/P1-Extensions aus `konzept.md` Z. 207-324 (z. B. `--mcp-log`-State, "lädt noch"-State, `rules.json`-Auto-Discovery, Staleness-Sweep-`mtime`-Kurzschluss) werden `McpServerOptionsFactory` mit hoher Wahrscheinlichkeit erneut erweitern — die nächsten 16 Z. reißen das Limit. Coder dokumentiert das in `result.md` Beobachtung 2. **Erkannt im Review von Einheit 003** (Kritiker-Vorschlag "Vorschlag 3").
- **Vorschlag:** Vor der nächsten substanziellen Erweiterung an `McpServerOptionsFactory` (z. B. bei Einbau des `--mcp-log`-Flags aus P0/P1) eine Aufteilung prüfen — z. B. ein `McpServerOptionsBuilder`-Pattern (analog `McpServerCommand`-Aufteilung in `McpServerOptionsFactory` + Registrar-Klassen) oder ein Init-`record` (analog TD-009-Vorschlag für `McpCodeGraphServer`). **Nicht eigenständige Refactor-Einheit**, sondern **inline** beim nächsten Anlass.
- **Status:** offen

### TD-015 — `McpToolResults.WarningsSection` Dead Code [Priorität: niedrig]

- **Ort:** `src/AiNetLinter/Mcp/McpToolResults.cs:117` (`WarningsSection`-Methode).
- **Befund:** Die Methode wurde in Einheit 006 angelegt als generischer Helper für Tool-Output-Warnings, hat aber **keinen** Production-Caller — alle 8 Tools mit Compile-Fehler-Warnhinweis (006) nutzen stattdessen `FindSymbolTool.BuildAggregateWarningAsync` + `McpToolResults.PrependWarning` (oder gleichwertig), weil `WarningsSection` als Identitäts-Funktion (ohne tatsächliche Aggregation) zu schwach war. Der dazugehörige Test (`McpToolResultsTests.cs:42-54`) ist tautologisch (`result == warningText`, testet die Identität, nicht das Verhalten). A3-Nachweis technisch korrekt, aber wertlos. **Erkannt im Review von Einheit 006** (Kritiker-Vorschlag).
- **Vorschlag:** Bei der nächsten substanziellen Erweiterung an `McpToolResults.cs` prüfen, ob `WarningsSection` noch gebraucht wird — wahrscheinlich löschen + Test entfernen. Oder: bei EPIC-07 (Tests-Ausbau) als kleinen Aufräumer-Schritt mitnehmen.
- **Status:** **geschlossen** durch Einheit 007 (`feat(tests): EPIC-07 tests-ausbau ...`): Methode + XML-Doc-Kommentar (Z. 107-116) + tautologischer Test (`McpToolResultsTests.cs:42-54`) entfernt. Keine weiteren Referenzen im Code (vor Entfernen verifiziert via `rg "WarningsSection" src/AiNetLinter/`). `McpToolResults.cs` von 134 auf 122 Z. geschrumpft (-12).

### TD-016 — Fixture-Code-Duplikation in 4 Workspace-Klassen [Priorität: niedrig]

- **Ort:** `tests/Fixtures/{SymbolGraphMini,GitImpactMini,CompileErrorMini,...}FixtureWorkspace.cs` und ggf. weitere.
- **Befund:** `CopyFixture` / `IsGeneratedPath` / `FindSolutionRoot` (oder gleichwertige Helper) sind in 4 Fixture-Workspace-Klassen dupliziert. Funktional identisch, keine Verhaltensabweichung, aber bei künftigen Änderungen (z. B. weiterer Ausschluss-Pfad, neue Fixture-Datei-Typen) müssten 4 Stellen synchron gehalten werden. **Erkannt im Review von Einheit 006** (Coder-Beobachtung, vom Kritiker als TD-Eintrag wert befunden).
- **Vorschlag:** **Inline** beim nächsten Fixture-Block (z. B. wenn EPIC-08 Last-Fixture-Generierung aus P1-6 eine weitere Fixture braucht). Gemeinsame Basisklasse `McpTestFixtureBase` o.ä. mit den drei Helpern, vier Fixture-Klassen erben davon.
- **Geschlossen durch:** Commit `6c872e4` (vor Einheit 007 angelegt): `FixtureWorkspaceBase.cs` (73 Z.) + `TestTempDirectory.cs` (58 Z.) eingeführt, `BaselineMiniFixtureWorkspace` (20 Z.) und `SymbolGraphMiniFixtureWorkspace` (20 Z.) erben jetzt davon (jeweils nur Konstruktor + eigene Property-Pfade).
- **Teilschluss-Anmerkung 2026-08-01 (Coder von 007):** Beim Sichten der Fixtures während 007-Vorbereitung fällt auf, dass der Refactor nur **2 von 4** Workspace-Klassen abgedeckt hat: `CompileErrorMiniFixtureWorkspace` (71 Z., dupliziert weiterhin `CopyFixture`/`IsGeneratedPath`/`FindSolutionRoot`) und `GitImpactMiniFixtureWorkspace` (166 Z., dupliziert dieselben Helper) wurden **nicht** auf `FixtureWorkspaceBase` umgestellt. TD-016 wird hier formal als "geschlossen durch 6c872e4" markiert, weil die strukturelle Loesung existiert und der initiale Refactor die Mehrheit der redundanten Stellen eliminiert hat — die verbleibenden zwei Stellen sind eine **Beobachtung**, die der naechste Planer/Cycle aufgreifen kann (z. B. als Folge-TD `TD-016a`, oder inline beim naechsten Fixture-Block in EPIC-08).
- **Status:** **geschlossen** (mit Teilschluss-Anmerkung)

### TD-016a — TD-016-Folge: 2 verbleibende Fixture-Klassen noch nicht refaktoriert [Priorität: niedrig]

- **Ort:** `src/AiNetLinter.Tests/Fixtures/CompileErrorMiniFixtureWorkspace.cs` (71 Z.) und `src/AiNetLinter.Tests/Fixtures/GitImpactMiniFixtureWorkspace.cs` (166 Z.).
- **Befund:** Beim TD-016-Refactor in `6c872e4` wurden `BaselineMiniFixtureWorkspace` (20 Z.) und `SymbolGraphMiniFixtureWorkspace` (20 Z.) auf `FixtureWorkspaceBase` (73 Z.) umgestellt — die beiden Klassen mit Zusatzlogik (`CompileErrorMini`: Compile-Fehler-spezifische Helper, `GitImpactMini`: `InitializeGitRepoWithInitialCommit`) wurden **nicht** migriert. `grep` bestätigt: `CopyFixture` / `IsGeneratedPath` / `FindSolutionRoot` kommen in beiden Klassen weiterhin wortgleich als `private static`-Methoden vor, parallel zur identischen Implementierung in `FixtureWorkspaceBase`. **Erkannt im Review von Einheit 007** (Coder-Beobachtung in `result.md` Abschnitt „TD-016 — geschlossen (mit Teilschluss-Anmerkung)").
- **Vorschlag:** **Inline** beim nächsten Fixture-Block (z. B. wenn EPIC-08 Last-Fixture-Generierung aus P1-6 eine weitere Fixture braucht). Planer entscheidet, ob ein eigenständiger Refactor (TD-016a-Einheit, ~1-2 h) oder inline-Mitnahme sinnvoller ist. Risikofaktor bei `GitImpactMiniFixtureWorkspace`: die Git-Init-Logik muss beim Umbau auf eine gemeinsame `TestTempDirectory` mit-konsolidiert werden, sonst gehen Initial-Commits verloren.
- **Status:** offen
