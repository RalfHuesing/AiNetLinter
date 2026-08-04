---
status: done (pending audit)
type: step-plan
task: codegraph-mcp-finish
step: 011
title: "Robuste McpLintConsole (B.6) + E2E-JSON-RPC-Framing-Test (B.6) + Opt-in --mcp-log Call-Log (B.7)"
epic: EPIC-06
estimated_risk: medium
step_type: single
items: []  # single, keine batch-Items
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-04
related_to: [step-010/step-review.md, step-009/fix-01/step-review.md]
---

# Step 011: Robuste McpLintConsole (B.6) + E2E-JSON-RPC-Framing-Test (B.6) + Opt-in --mcp-log Call-Log (B.7)

## Bezug

- **Task:** `codegraph-mcp-finish`
- **Epic:** `EPIC-06` aus `roadmap.md` — Robustheit & Observability für den
  MCP-Server (letzter offener Block des B-Clusters, B.6 + B.7).
- **Konzept-Referenz:** `Konzept.md` Z. 245-261 (Muss-Haben B, Punkte 6-7).
  B.6: „stdout strukturell als reiner Protokollkanal" — eigene
  `ILintConsole`-Implementierung für den MCP-Modus (leitet `WriteLine` nach
  stderr um) + E2E-Test, der jede stdout-Zeile als gültigen JSON-RPC-Frame
  verifiziert. B.7: Opt-in Call-Log `--mcp-log` (Zeitstempel, Tool,
  gekürzte Parameter, Ergebniszeilen, Trunkierung ja/nein, Dauer, Leermenge
  ja/nein), Default aus, Ablage neben `cache/`.
- **DoD-Referenz:** `Konzept.md` Z. 650-653 — „Alle sieben Punkte aus
  Muss-Haben B sind umgesetzt, reviewt, mit Integrationstest abgesichert".
  Nach step-011 sind B.1-B.7 abgehakt; DoD für B.6 + B.7: neue Komponenten
  in `Mcp/` (oder nahegelegener Namespace) + 1 Integration-Test für das
  JSON-RPC-Framing + Unit-Tests für die Call-Log-Mechanik + Doku-Updates
  in `Docs/agent-api.md`, `Docs/integration.md`, `Docs/configuration.md`.
- **Non-Goals (bewusst NICHT in diesem Step):**
  - Keine Änderung an den Tool-Implementierungen (`FindSymbolTool.cs`,
    `FindReferencesTool.cs`, …) selbst — B.6 + B.7 sind rein
    infrastrukturell (Server-Bootstrap + Dispatch-Wrapper).
  - Keine neuen Tools, keine Tool-Parameter-Änderungen, keine Doku- oder
    Config-Schema-Änderungen außerhalb der B.6-/B.7-Punkte.
  - Kein TD-Item (kein TD-001/006/008-Mitnahme) — der step-010-Review hat
    zwei MINOR-Beobachtungen offen gelassen (XML-Doc-Cleanup in
    `FindSymbolTool.cs:14-24`, `tech-debt.md`-Status für TD-005 + TD-007
    nicht auf „geschlossen" gesetzt), beide sind weiterhin KEIN
    EPIC-06-Scope und werden in `Bekannte Ausnahmen` für spätere Schritte
    vorgemerkt.
  - Keine Refactorings am bestehenden Cache- oder
    `McpCodeGraphServerOptions`-Aufbau — B.6/B.7 sind additiv
    (neue `McpLintConsole`, neue `McpCallLog`-Klasse,
    neuer CLI-Flag, neuer E2E-Test).
  - Kein Eingriff in `McpTestClient` (Subprozess-Testinfra bleibt
    unverändert — der E2E-Framing-Test nutzt eine eigene, schlanke
    Direkt-Subprozess-Hülle analog `McpTestClient.ConnectAsync`).

## Aktueller Projektzustand (JIT-Kontext)

Beim Code-Lesen für die Plan-Erstellung vorgefunden (Stand 2026-08-04):

### B.6 — Console-Architektur & JSON-RPC-Transport

- **`ILintConsole`** ist ein internes Interface in
  `src/AiNetLinter/Output/ILintConsole.cs:5-9` mit genau zwei Methoden:
  `void WriteLine(string message)` und `void WriteError(string message)`.
- **Einzige produktive Implementierung** ist `LinterConsole` in
  `src/AiNetLinter/Output/LinterConsole.cs:7-14` — ein interner
  Singleton (`LinterConsole.Instance`) mit `public void WriteLine(string)
  => Console.WriteLine(message)` (stdout!) und `WriteError` →
  `Console.Error.WriteLine`. **Hier liegt der strukturelle Leak**: jede
  `ILintConsole.WriteLine(...)`-Aufruf aus dem MCP-Server-Kontext landet
  auf stdout und würde das JSON-RPC-Framing zerstören. Aktuell wird das
  nur durch Disziplin vermieden (kein produktiver Aufrufer aus dem
  `McpCodeGraphServer`-Pfad macht `_console.WriteLine` — alle Meldungen
  gehen über `WriteError` nach stderr). Die strukturelle Absicherung
  fehlt.
- **In `McpServerCommand.RunAsync`**
  (`src/AiNetLinter/Commands/McpServerCommand.cs:31-65`) wird die
  `ILintConsole` per Parameter injiziert (Test-Pfad: `TestLintConsole`),
  Default ist `LinterConsole.Instance` (Z. 33). Die Übergabe an den
  Server läuft über `McpCodeGraphServerOptions.Console` (Z. 53).
- **Im Aufruf in `Program.cs:43`** (der `if (linterArgs.McpServer) return
  await McpServerCommand.RunAsync(...)`-Zweig) wird **keine** explizite
  Console übergeben — der Default greift und das ist genau der
  Leak-Pfad. Hier muss die `McpLintConsole.Instance` als dritter
  Parameter gesetzt werden.
- **JSON-RPC-Framing** wird vom `ModelContextProtocol`-NuGet-Paket
  übernommen. Der Transport ist `StdioServerTransport`
  (`McpServerCommand.cs:61`); die Frames sind newline-delimited JSON
  (verifiziert per `McpClient.CreateAsync` + `StdioClientTransport` in
  `McpTestClient.cs:74-79`, das die JSON-RPC-Antworten Zeile-für-Zeile
  liest — d. h. ein einzelner `Console.WriteLine(...)`-Leak wäre als
  nicht-JSON-Zeile auf dem stdout-Stream sichtbar).
- **Im SDK-Aufrufweg** kann der `StdioServerTransport` keine
  Custom-Streams annehmen (im Test-Pfad zeigt sich das: der SDK öffnet
  selbst `Console.OpenStandardInput/Output`). Für den E2E-Framing-Test
  muss daher eine **eigene Subprozess-Hülle** im Test geschrieben werden,
  die das Server-Process selbst startet (analog `McpTestClient.cs:67-79`)
  und die stdout-Bytes Zeile-für-Zeile abgreift, **bevor** sie ein
  potenziell nachgelagerter MCP-Parser sieht. Das ist der im Edge-Case
  der Aufgabenstellung genannte „First-Principles"-Test.
- **Bestehende Tests für die Console** (z. B. `McpServerCommandTests.cs:40
  ResolveSolutionPathOrError_TwoSlnxFiles_ReportsAmbiguousSolution`)
  nutzen den `TestLintConsole` (`src/AiNetLinter.Tests/Output/TestLintConsole.cs:9-19`)
  — ein in-memory-`ILintConsole` mit zwei `List<string>`-Captures für
  `Output` und `Errors`. Dieses Pattern ist die Vorlage für die
  B.6-Unit-Tests der `McpLintConsole` (Verhalten gegen
  in-memory-`TextWriter`).

### B.7 — CLI-Argumente, Cache-Ablage, Tool-Dispatch

- **CLI-Flag-Pattern**: `LinterArgs.cs` enthält die
  DTO-Properties (z. B. `McpServer` Z. 164), `CliOptions.cs` den
  `Option<...>`-Satz (Z. 10-48), `CliCommandBuilder.cs` die
  Wiring-Stellen (`Build()` Z. 12-30, `CreateOptions()` Z. 32-73,
  `Parse()` Z. 75-130), und `Program.cs:68-112` die
  `ToLinterArgs(...)`-Mapping-Stelle. Für `--mcp-log` muss **vier**
  Stellen erweitert werden: `LinterArgs.cs` (Property),
  `CliOptionFactory.cs` (Erzeugung), `CliOptions.cs` (Record-Feld),
  `CliCommandBuilder.cs` (Wiring + Mapping), `Program.cs`
  (Mapping zur `LinterArgs`-Property). Pattern exakt analog zum
  bestehenden `--agent-rules-path`-Flag (siehe `LinterArgs.cs:77`,
  `CliOptionFactory.cs:94-97`, `CliOptions.cs:27`,
  `CliCommandBuilder.cs:21 + 51 + 108`).
- **Cache-Ablage**: `AnalysisCacheManager.Load`
  (`src/AiNetLinter/Cache/AnalysisCacheManager.cs:36-51`) legt `cache/`
  unter `Path.Combine(exeDir, "cache")` an, mit einem pro-Lösung
  SHA-256-Hash-Präfix im Filename. Der Konzept-Wunsch „Ablage neben
  `cache/`" wird analog umgesetzt: `Path.Combine(exeDir, "mcp-log")` als
  Default-Ordner, Filename mit Solution-Name + Hash-Präfix + Timestamp
  zur Vermeidung von Kollisionen bei mehreren parallelen Server-Instanzen
  auf derselben Solution. **Wenn der Nutzer `--mcp-log` mit absolutem
  Pfad setzt, gewinnt der explizite Pfad 1:1** (analog B.1
  `rules.json`-Auto-Discovery: `args.ConfigPath` schlägt
  Auto-Discovery, siehe `McpServerCommand.cs:78-81`).
- **Tool-Dispatch-Hook-Punkt**: Die Tool-Registrierung läuft über drei
  zentrale Klassen (`SymbolGraphToolRegistrations.cs:23`,
  `FileStructureToolRegistrations.cs:26`,
  `AnalysisToolRegistrations.cs:29`), die jeweils `McpServerTool.Create(
  delegate, McpServerToolCreateOptions)` aufrufen. Der Dispatch wird vom
  SDK kontrolliert (es gibt keinen zentralen
  `OnToolCalled`-Callback im SDK). Der saubere Hook-Punkt ist daher
  **pro Tool** in den drei `*Registrations.cs`-Dateien: jeder
  Delegate-Wrapper wird in eine `async (...) => { ... }`-Lambda
  eingeschlossen, die (a) den Start-Zeitstempel + Tool-Name +
  Parameter-Kurzform an `McpCallLog.RecordStart(...)` übergibt, (b) das
  eigentliche Tool ausführt, (c) im `finally` `RecordEnd(...)` mit
  Dauer + Ergebniszeilen + Trunkierungs-/Leermenge-Flags aufruft. Das
  ist mechanisch (~3 Dateien × ~3-4 Wrapper = ~10 Lambda-Edits), aber
  strukturell sauber und ohne Eingriff in die Tool-Klassen selbst.
- **Trunkierungs-Erkennung** ist heute in `McpTruncation.cs:29-42`
  zentralisiert: Wenn die letzte angehängte Zeile mit `[N Treffer
  gesamt, M gezeigt —` beginnt, ist das Ergebnis trunkiert. Die
  Call-Log-Logik kann denselben String-Match nutzen, um das
  Trunkierungs-Flag zu setzen, ohne neue Felder in der Tool-API.
- **Forward-Looking-Marker** in `McpServerOptionsBuilder.cs:13`:
  `… kuenftige P0/P1-Erweiterungen (--mcp-log-State, rules.json
  Auto-Discovery-Hint, "laedt noch"-State) als additive With*-Methoden
  zu ermoeglichen …` — B.7 setzt einen Teil davon (Call-Log-State)
  jetzt um; der Hinweis im XML-Doc ist nach B.7-Implementierung
  konsistent oder muss minimal angepasst werden (Entscheidung des
  Coders; nicht im Scope dieses Plans erzwungen).

### Was wiederverwendet wird (statt neu gebaut)

- **`ILintConsole`**, **`TestLintConsole`**, **`LinterConsole`**: B.6
  fügt `McpLintConsole` als dritte Implementierung hinzu — kein
  Interface-Bruch, keine Test-Änderungen außer den neuen
  `McpLintConsole`-Tests.
- **`LinterArgs` + `CliOptions` + `CliCommandBuilder` + `Program.cs`**:
  B.7 folgt exakt dem bestehenden Pattern für `--agent-rules-path`.
- **`McpTruncation`**: Die Call-Log-Erkennung „wurde trunkiert?" nutzt
  den existierenden Meta-Zeilen-String, kein neuer Code zur
  Trunkierung.
- **`SubprocessConcurrencyGate` + `McpTestClient.ConnectAsync`-
  Pattern** (für den E2E-Framing-Test): B.6-E2E-Test spawnt
  `AiNetLinter.exe` als Subprozess und liest dessen stdout **roh** (vor
  dem SDK-Parser); die Gate-Nutzung verhindert Last-Flakes im
  Volllauf.

## Intention

Nach diesem Step sind die zwei letzten Punkte aus Muss-Haben B umgesetzt:
Der MCP-Server hat eine **strukturelle** stdout-Absicherung (spezifische
`McpLintConsole`, die `WriteLine` zwingend nach stderr umleitet — kein
versehentlicher Disziplin-Leak mehr aus wiederverwendeten CLI-Klassen
möglich) und einen **E2E-beweisbaren** Schutz (Integration-Test, der jede
stdout-Zeile einer Tool-Call-Sequenz als gültigen JSON-RPC-Frame
verifiziert). Zusätzlich existiert ein **opt-in Call-Log** (`--mcp-log
<pfad>`), das die tatsächliche Tool-Nutzung in der Praxis beobachtbar
macht und damit zukünftige Priorisierungen vom Markt-Benchmark auf
eigene Daten stützt. Ohne B.6 wäre ein einziger zukünftiger
`Console.WriteLine`-Call in einer Kernklasse ausreichend, um die
gesamte MCP-Session zu zerstören — diese strukturelle Lücke ist nach
dem Step geschlossen.

## Konkrete Änderungen

### B.6 — `McpLintConsole` (strukturelle stdout-Absicherung)

#### Datei 1: `src/AiNetLinter/Output/McpLintConsole.cs` (neu)

- **Was:** Neue `internal sealed class McpLintConsole : ILintConsole` mit
  analogem `Instance`-Singleton-Pattern wie `LinterConsole`. Implementiert
  `WriteLine(string message) => Console.Error.WriteLine(message)` und
  delegiert `WriteError` 1:1 an `Console.Error.WriteLine` (gleich wie
  `LinterConsole`, weil im MCP-Modus beide Kanäle strukturell auf stderr
  gehen müssen — ein versehentlicher `WriteLine` darf nicht auf stdout
  landen, ein `WriteError` ist semantisch ohnehin stderr).
- **Warum:** Strukturelle Garantie, dass im MCP-Modus
  `ILintConsole.WriteLine` niemals stdout berührt. Die existierende
  Disziplin („niemand ruft `_console.WriteLine` aus dem `Mcp/`-Pfad auf")
  wird damit von einer Architektur-Eigenschaft abgelöst. Datei liegt
  bewusst im `Output/`-Namespace neben `LinterConsole` und `ILintConsole`,
  damit alle drei Konsolen-Implementierungen am selben Ort discoverable
  sind.
- **Sichtbarkeit:** `internal sealed`, Pattern exakt wie `LinterConsole`
  (`LinterConsole.cs:7-14`).
- **Footprint:** minimal — eine Klasse + zwei Methoden + ein Singleton.
  Kein neuer Path-Override für `MaxAIContextFootprint` zu erwarten.

#### Datei 2: `src/AiNetLinter/Commands/McpServerCommand.cs:43` (oder umliegend)

- **Was:** Der Default-Fallback `var c = console ?? LinterConsole.Instance;`
  in `RunAsync` (Z. 33) bleibt bestehen (für die Test-Pfade, die eine
  eigene `TestLintConsole` injizieren). **In `Program.cs:43`** wird der
  MCP-Aufruf-Zweig um einen dritten Parameter ergänzt:
  `return await McpServerCommand.RunAsync(linterArgs, cts.Token,
  McpLintConsole.Instance);`. Begründung: Die Auswahl „welche Konsole
  passt zum Modus" ist eine **Aufrufstellen**-Entscheidung, nicht eine
  `McpServerCommand`-Eigenschaft — `McpServerCommand` bleibt
  mode-agnostisch (akzeptiert weiterhin `ILintConsole?` für Tests).
- **Warum:** So injiziert jeder MCP-Server-Start in Produktion die
  strukturelle Absicherung, ohne dass Tests ihre `TestLintConsole`
  aufgeben müssen. Der `Program.cs`-Patch ist 1 Zeile.
- **Risiko-Hinweis:** Wenn der Coder die Console-Wahl lieber in
  `McpServerCommand` selbst treffen möchte (z. B. via
  `McpServerCommandOptions`-Property), ist das eine akzeptierte
  Variante — die Plan-Vorgabe ist nur „**irgendwo zwischen Program.cs
  und `McpServerCommand.RunAsync`** wird im Produktionspfad die
  `McpLintConsole` aktiv". Die Variante via `Program.cs` ist die
  minimal-invasivste.

#### Datei 3: `src/AiNetLinter.Tests/Output/McpLintConsoleTests.cs` (neu)

- **Was:** 2-3 Unit-Tests, die das Verhalten von `McpLintConsole`
  verifizieren:
  - `McpLintConsole_WriteLine_RoutesToStderr` — direkter Test der
    `WriteLine`-Methode (z. B. via `Console.SetError(new StringWriter(...))`
    im Test-Setup).
  - `McpLintConsole_WriteError_RoutesToStderr` — symmetrischer Test
    für `WriteError`.
  - `McpLintConsole_Instance_ReturnsSameSingleton` — verifiziert die
    `Instance`-Property-Identität.
- **Warum:** Direkter, lokal testbarer Beweis, dass `McpLintConsole`
  das tut, was die Architektur von ihr verlangt. Komplementiert den
  E2E-Framing-Test (Datei 4), der das Verhalten nur indirekt (über die
  Framed-Bytes) verifiziert.
- **Kategorie:** `[Trait("Category", "Unit")]` — schnell, kein
  Subprozess.

#### Datei 4: `src/AiNetLinter.Tests/Mcp/McpServerCommandJsonRpcFramingTests.cs` (neu)

- **Was:** Ein Integration-Test, der eine vollständige Tool-Call-Sequenz
  gegen einen echten `AiNetLinter.exe --mcp-server`-Subprozess fährt und
  dabei **jede einzelne stdout-Zeile** als gültigen JSON-RPC-Frame
  verifiziert. Konkretes Vorgehen:
  1. Test spawnt `AiNetLinter.exe` als Subprozess mit
     `Process.Start`-API analog `McpTestClient.cs:65-79` (mit
     `SubprocessConcurrencyGate.AcquireAsync` zur Vermeidung von
     Last-Flakes).
  2. Test schreibt einen **gültigen JSON-RPC-Handshake** (initialize →
     notifications/initialized → tools/list → tools/call für mind. 2
     verschiedene Tools) zeilenweise auf `process.StandardInput`.
     Die genauen Frame-Inhalte werden einmalig als Konstanten im Test
     festgehalten (kein generischer MCP-Client nötig; das Test-Frame
     wird gegen den im `McpTestClient`-Pattern bewährten Initialize-
     Handshake gespiegelt).
  3. Test liest `process.StandardOutput` Zeile-für-Zeile, parst jede
     Zeile mit `JsonDocument.Parse` und prüft:
     - Parse erfolgreich (sonst Test-Fail mit Zeilennummer + Inhalt).
     - `root.GetProperty("jsonrpc").GetString() == "2.0"`.
  4. Test schließt stdin (EOF), wartet auf Prozess-Exit, prüft
     Exit-Code.
- **Kritisch:** Der Test darf den SDK-Parser **nicht** zwischen
  Subprozess und Assertions schalten — sonst würde der SDK einen
  JSON-Fehler bei einer geleakten Zeile schlucken. Der Test liest
  direkt vom `process.StandardOutput` und prüft die Zeilen selbst.
- **Test-Name:** `McpServerCommand_ToolCallSequence_AllStdoutLinesAreValidJsonRpcFrames`
  (Haupt-Test) + ggf. `McpServerCommand_HandshakeOnly_AllStdoutLinesAreValidJsonRpcFrames`
  (minimaler Smoke-Test nur mit initialize/initialized).
- **Warum:** Der Konzept-Punkt „ein einziger Leak zerstört das
  JSON-RPC-Framing der gesamten Session" ist nur dann wirklich
  abgesichert, wenn ein **echter Subprozess mit echten stdout-Bytes**
  verifiziert wird — nicht nur ein in-Memory-Capture der
  `ILintConsole`. Der Test dient als Regressions-Schutz für die
  Lebenszeit des Projekts.
- **Fixture-Nutzung:** Idealerweise eine neue kleine `XunitIClassFixture`
  (`McpServerSubprocessFixture` analog `SymbolGraphMcpFixture`), die
  EINMAL pro Testklasse einen Subprozess hochfährt (1 Connect +
  1-2 Tool-Calls); bei nur einem Test in der Klasse reicht ein
  inline-Spawn + `using` (kein Fixture-Overhead nötig).
- **Kategorie:** `[Trait("Category", "Integration")]` — Subprozess-Test,
  trägt zur Volllauf-Laufzeit bei. Erwarteter Overhead: ~3-5 s pro
  Volllauf (vergleichbar mit anderen E2E-Tests in
  `McpServerAllToolsE2ETests`).

#### Datei 5: Doku-Updates für B.6

- **`Docs/agent-api.md`** — neuer Unterabschnitt im `## MCP-Server-Modus`
  (Z. 213ff): „### stdout-Schutz". Erklärt in 2-3 Sätzen, dass
  `WriteLine` im MCP-Modus strukturell nach stderr umgeleitet wird
  (nicht als Disziplin, sondern als Architektur-Garantie), und dass
  ein unbeabsichtigter `Console.WriteLine` aus einer Kernklasse
  nicht das JSON-RPC-Framing zerstören kann, weil die zentrale
  `ILintConsole.WriteLine`-Implementierung im MCP-Modus (`McpLintConsole`)
  nur `Console.Error` schreibt. Verweist auf den E2E-Test als
  Regressions-Schutz.
- **`Docs/integration.md`** — kurzer Hinweis im `## MCP-Server
  registrieren`-Abschnitt (Z. 221ff), dass die registrierte `ainetlinter`
  -Binary stdout **niemals** für andere Zwecke als JSON-RPC nutzt
  (z. B. für CI-Log-Parsing, Debug-Ausgaben o. ä.).
- **Keine** Änderung an `Docs/configuration.md` nötig (B.6 ist kein
  Konfigurations-, sondern ein Architektur-Fix).

### B.7 — `--mcp-log` (Opt-in Call-Log)

#### Datei 6: `src/AiNetLinter/Cli/LinterArgs.cs` (Erweiterung um Z. ~165)

- **Was:** Neue Property `public string? McpLogPath { get; init; }` analog
  `AgentRulesPath` (Z. 77). `null` = Log deaktiviert (Default).
- **Warum:** Träger des CLI-Werts vom Parse-Result bis zur
  `McpServerCommand.RunAsync`-Auswertung.

#### Datei 7: `src/AiNetLinter/Cli/CliOptionFactory.cs` (Erweiterung um Z. ~240)

- **Was:** Neue Factory-Methode `internal static Option<string?>
  CreateMcpLogOption() => new("--mcp-log", "-mcp-log") { Description =
  "Optionaler Pfad fuer das MCP-Call-Log (JSONL-Format, ein Eintrag pro
  Zeile). Default: deaktiviert. Pfad-Aufloesung: absolut → wie angegeben;
  relativ → relativ zum Solution-Verzeichnis. Beispiel: --mcp-log
  ./.mcp-log/calls.log" }`.
- **Warum:** Pattern exakt wie `CreateAgentRulesPathOption()` (Z. 94-97).

#### Datei 8: `src/AiNetLinter/Cli/CliOptions.cs` (Erweiterung)

- **Was:** Neues Feld `Option<string?> McpLog` im Record (Z. 10-48,
  nach `McpServer`).
- **Warum:** Record-Felder müssen mit dem `Build()`-Argument-Satz und
  dem `Parse()`-GetValue-Satz konsistent sein.

#### Datei 9: `src/AiNetLinter/Cli/CliCommandBuilder.cs` (Erweiterung)

- **Was:** Drei Stellen: (a) `Build()` (Z. 12-30) — neue Option an
  `new RootCommand { …, options.McpLog, }` anhängen. (b)
  `CreateOptions()` (Z. 32-73) — neuen `CliOptionFactory.CreateMcpLogOption()`
  im Konstruktor-Aufruf ergänzen. (c) `Parse()` (Z. 75-130) — neue Zeile
  `McpLog: parseResult.GetValue(options.McpLog)` im `new
  CliParsedArgs(...)`-Aufruf, und neuer Eintrag `string? McpLog` im
  `CliParsedArgs`-Record.
- **Warum:** Standard-Wiring, exakt wie für `AgentRulesPath`.

#### Datei 10: `src/AiNetLinter/Program.cs:68-112` (`ToLinterArgs`)

- **Was:** Neue Zeile `McpLogPath = parsed.McpLog,` im `new
  LinterArgs { … }`-Initialisierer.
- **Warum:** Mapping vom geparsten CLI-Wert zur `LinterArgs`-Property.

#### Datei 11: `src/AiNetLinter/Mcp/McpCallLog.cs` (neu)

- **Was:** Neue `internal sealed class McpCallLog` mit folgenden
  Verantwortlichkeiten:
  - **Konstruktor**: nimmt `string logPath` (absoluter, vom Aufrufer
    aufgelöster Pfad), öffnet `StreamWriter` mit `append: true,
    Encoding.UTF8` (kein BOM).
  - **Methode `RecordStartAsync(string toolName, string args)`**:
    liefert ein `IAsyncDisposable`-Token (intern: `McpCallLogScope`).
    Das Token hält Start-Timestamp + Tool-Name + Args-String und
    übergibt im `DisposeAsync` den End-Zeitstempel + Ergebnis an
    `RecordEnd`.
  - **Methode `RecordEnd(CallToolResult result, TimeSpan duration,
    string args)`**: schreibt eine JSONL-Zeile in den StreamWriter mit
    den Konzept-Feldern:
    `{ "ts": "<ISO8601>", "tool": "<name>", "args": "<truncated>",
    "lines": <int>, "truncated": <bool>, "duration_ms": <double>,
    "empty": <bool> }`. `args` wird auf max. 200 Zeichen gekürzt (mit
    Ellipsis), `lines` ist die Anzahl der Text-Zeilen im
    `CallToolResult.Content[0].Text` (oder 0 bei
    `IsError == true` ohne Text-Content), `truncated` ist `true` wenn
    die letzte Zeile mit `[N Treffer gesamt, M gezeigt —` ODER `[N
    Dateien mit Textfund, M gezeigt —` beginnt (gleiche Strings wie
    `McpTruncation.cs:40` + `:66`), `empty` ist `true` wenn
    `lines == 0` und `IsError == false` (kein Fehler, aber auch kein
    Ergebnis).
  - **Methode `Dispose()`**: schließt den `StreamWriter`, löscht die
    Datei wenn 0 Einträge geschrieben wurden (kein leeres File
    hinterlassen).
  - **Thread-Safety**: ein internes `Lock _writeLock` umschließt die
    Schreib-Operation (mehrere parallele Tool-Calls möglich). Die
    `StreamWriter`-Instanz wird nicht zwischen Threads geteilt
    geschrieben — das `Lock` serialisiert.
- **Warum:** Eine eigene Klasse hält die Logik zentral und testbar; der
  Tool-Dispatch-Wrapper (Datei 12) wird dadurch trivial.
- **Kategorie-Hinweis:** Pfad-Auflösung in
  `McpServerCommand.RunAsync`: wenn `args.McpLogPath` `null`/leer →
  kein `McpCallLog` instanziiert (= Log deaktiviert, kein File I/O).
  Wenn nicht-leer: Auflösung analog `LinterArgs.ConfigPath` (Z. 76-78
  in `McpServerCommand.cs`): leerer String oder relatives Verzeichnis
  → relativ zur `solutionPath` (nicht zum `exeDir`, weil die Log-Datei
  zur Solution gehören soll und nicht in das Installations-Verzeichnis
  des Tools wandern soll). Default-Pfad, wenn Flag ohne Wert gesetzt
  (System.CommandLine unterstützt das via `Arity.ZeroOrOne` analog
  `CreateImpactOption` Z. 77-81): `<solutionDir>/.mcp-log/calls.log`.
- **Footprint:** Eine Klasse + 2-3 Methoden + ein internes `Scope`-Record
  (oder -Klasse). Wahrscheinlich kein neuer Path-Override für
  `MaxAIContextFootprint` nötig (Klasse ist klein); falls doch, dann
  `MaxLineCount`-Override wie bei den Tool-Klassen.

#### Datei 12: Drei `*Registrations.cs`-Dateien (Wrapper-Patch)

- **Was:** Jedes `McpServerTool.Create(delegate, options)` in
  `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` (4 Tools),
  `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` (3 Tools) und
  `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` (2 Tools) wird in
  eine `async (...) => { using var _ = log?.RecordStart(...); return
  await <original>(...); }`-Lambda gewrappt. Konkretes Beispiel für
  `find_symbol` (SymbolGraphToolRegistrations.cs:25-36):

  ```csharp
  // vorher (Z. 25-27):
  tools.Add(McpServerTool.Create(
      (string namePattern, string? kind = null, int maxResults = 50, CancellationToken ct = default) =>
          FindSymbolTool.ExecuteAsync(mcpState, namePattern, kind, maxResults, ct),
      new McpServerToolCreateOptions { Name = "find_symbol", ... }));

  // nachher:
  tools.Add(McpServerTool.Create(
      async (string namePattern, string? kind = null, int maxResults = 50, CancellationToken ct = default) =>
      {
          using var _ = callLog?.StartRecording("find_symbol", $"{namePattern}|{kind}|{maxResults}");
          return await FindSymbolTool.ExecuteAsync(mcpState, namePattern, kind, maxResults, ct);
      },
      new McpServerToolCreateOptions { Name = "find_symbol", ... }));
  ```

  Der `Register(tools, mcpState)`-Methodenkopf bekommt einen dritten
  Parameter: `McpCallLog? callLog = null` (analog zum
  `McpCodeGraphServerOptions.LoadFunc`-Pattern, das heute schon
  optionale Zusatz-Dienste einspeist).
- **Warum:** Sauberer, zentraler Hook-Punkt ohne Eingriff in die
  Tool-Klassen selbst. Der Coder kann die 9 Wrapper mechanisch anlegen.
- **Alternativen (vom Planer verworfen):**
  - **Zentrale Wrapper-Klasse mit `Delegate.DynamicInvoke`**: hässlich,
    verliert Typsicherheit, langsamer. Verworfen.
  - **Eingriff in `McpServerOptionsBuilder`**: das SDK bietet keinen
    OnToolCall-Hook, daher nicht möglich. Verworfen.
  - **Modifikation der Tool-Klassen (`FindSymbolTool.ExecuteAsync`
    etc.)**: würde 9 Dateien berühren statt 3, dringt in
    Tool-Logik ein, bläht jede Tool-Klasse um 2 Wrapper-Zeilen auf.
    Verworfen.
- **Footprint-Auswirkung:** Jede `Register`-Methode wächst um 9 * 2
  Zeilen (Wrapper-Lambda + Scope-Open), die Tool-Footprints selbst
  bleiben unverändert. Falls `Register`-Klassen das 60-Zeilen-Limit
  reißen (siehe `AiNetLinter.mdc`): ggf. Extraction einer privaten
  `BuildLoggedWrapper`-Hilfsmethode pro Datei. Voraussichtlich nicht
  nötig (Register-Methoden sind aktuell ~30-60 Zeilen, 9 × 2 = 18
  zusätzliche Zeilen — passt).

#### Datei 13: `McpServerOptionsFactory` / `McpServerCommand.RunAsync` (Verdrahtung)

- **Was:** In `McpServerCommand.RunAsync` (`Commands/McpServerCommand.cs:31-65`)
  wird nach der `McpCodeGraphServer`-Instanziierung (Z. 50-58) und vor
  der `McpServerOptionsFactory.Create(mcpState)`-Zeile (Z. 60) ein
  Block ergänzt:
  ```csharp
  McpCallLog? callLog = null;
  if (!string.IsNullOrWhiteSpace(args.McpLogPath))
  {
      var logPath = ResolveMcpLogPath(args.McpLogPath, solutionPath, c);
      callLog = new McpCallLog(logPath);
  }
  ```
  Der `callLog` wird an `McpServerOptionsFactory.Create(mcpState,
  callLog)` durchgereicht und in der Factory an jede
  `*Registrations.Register(tools, mcpState, callLog)` weitergegeben.
  Im `finally`/Dispose-Pfad wird `callLog?.Dispose()` aufgerufen.
- **Warum:** Verdrahtung Call-Log → Tool-Dispatch. Die
  `ResolveMcpLogPath`-Helfermethode lebt in `McpServerCommand` (analog
  `TryResolveRulesJsonPath` Z. 76-88) und ist `internal static` für
  Test-Sichtbarkeit.
- **Risiko-Hinweis:** Die `McpServerOptionsFactory.Create`-
  Methodensignatur ändert sich (`mcpState` → `mcpState, callLog = null`).
  Bestehende Tests, die `Create(mcpState)` direkt aufrufen, bleiben
  kompatibel (optionaler Parameter mit Default `null`). Die drei
  `*Registrations.Register`-Methodensignaturen ändern sich ebenfalls
  (`mcpState` → `mcpState, callLog = null`).

#### Datei 14: Drei Unit-Test-Dateien für B.7 (neu)

- **Was:**
  - **`McpCallLogTests.cs`** (`src/AiNetLinter.Tests/Mcp/McpCallLogTests.cs`):
    3-4 Unit-Tests, die das Verhalten der Log-Klasse isoliert
    verifizieren:
    - `RecordStart_ThenEnd_WritesJsonLineWithAllFields` — Start +
      End → genau eine JSONL-Zeile mit allen Konzept-Feldern.
    - `RecordEnd_TruncatedResult_SetsTruncatedTrue` — End mit
      einem Result, das mit der Trunkierungs-Meta-Zeile endet →
      `truncated: true`.
    - `RecordEnd_EmptyResult_SetsEmptyTrue` — End mit
      `IsError == false` und 0 Text-Zeilen → `empty: true`.
    - `Dispose_NoRecords_DeletesLogFile` — Log-File wird nicht
      angelegt wenn keine Calls recorded wurden.
  - **`McpServerCommandCallLogTests.cs`**
    (`src/AiNetLinter.Tests/Commands/McpServerCommandCallLogTests.cs`):
    1-2 Tests, die die Verdrahtung in `McpServerCommand.RunAsync`
    verifizieren:
    - `RunAsync_McpLogPathNotSet_CallLogIsNull` — kein Flag →
      kein `McpCallLog` instanziiert, Tool-Aufrufe laufen ohne
      Wrapper (oder Wrapper ist no-op).
    - `RunAsync_McpLogPathRelativeToSolution_CreatesLogFileInSolutionDir`
      — `--mcp-log ./.mcp-log/calls.log` → Datei wird unter
      `solutionDir/.mcp-log/calls.log` angelegt.
  - **Optional**: 1 In-Process-Integration-Test, der die
    Wrapper-Lambda direkt testet (Start → Tool-Call → End → JSONL
    enthält Tool-Name). Falls die Wrapper zu mechanisch sind, kann
    dieser Test entfallen.
- **Warum:** Strukturelle + funktionale Beweisführung, dass B.7 wie
  spezifiziert arbeitet (alle Konzept-Felder in der richtigen
  Reihenfolge, korrekte Trunkierungs-/Leermenge-Erkennung, korrekte
  Pfad-Auflösung).
- **Kategorien:** Alle drei Unit-Test-Dateien mit
  `[Trait("Category", "Unit")]` — keine Subprozesse, schnell.

#### Datei 15: Doku-Updates für B.7

- **`Docs/agent-api.md`** — neuer Unterabschnitt im
  `## MCP-Server-Modus`: „### Call-Log (opt-in)". Erklärt
  - Zweck: Beobachtung der tatsächlichen Tool-Nutzung in der Praxis.
  - Aufruf: `ainetlinter --mcp-server --mcp-log <pfad>` (oder
    `--mcp-log` ohne Wert → Default-Pfad `<solutionDir>/.mcp-log/calls.log`).
  - Format: JSONL, ein Eintrag pro Tool-Call, Felder
    `ts` / `tool` / `args` (gekürzt) / `lines` / `truncated` /
    `duration_ms` / `empty`.
  - Pfad-Auflösung: absolut → wie angegeben; relativ → relativ
    zum Solution-Verzeichnis.
  - Default: deaktiviert, kein File I/O.
  - Beispiel-Snippet (3-4 JSONL-Zeilen, wie sie aussehen).
  - Verweis auf den `--mcp-log`-Punkt in `Docs/configuration.md` für
    die formale Spec.
- **`Docs/integration.md`** — kurzer Hinweis im
  `## MCP-Server registrieren`-Abschnitt: „Für
  Production-Monitoring kann der registrierte `ainetlinter`-Aufruf um
  `--mcp-log <pfad>` ergänzt werden — siehe
  [Docs/agent-api.md#call-log-opt-in](agent-api.md#call-log-opt-in)
  für Format und Pfad-Auflösung."
- **`Docs/configuration.md`** — neuer Eintrag in der CLI-Optionen-
  Tabelle für `--mcp-log` (alias `-mcp-log`): Datentyp `string?`,
  Default `null`, Beschreibung analog der `--agent-rules-path`-
  Zeile. Verweis auf den Agent-API-Abschnitt für Format-Details.

## Tests

- [x] `McpLintConsole_WriteLine_RoutesToStderr` (`Output/McpLintConsoleTests.cs`,
      `[Trait("Category", "Unit")]`) — direkter Test der `WriteLine`-Umleitung
- [x] `McpLintConsole_WriteError_RoutesToStderr` (gleiche Datei,
      `Unit`) — symmetrisch für `WriteError`
- [x] `McpLintConsole_Instance_ReturnsSameSingleton` (gleiche Datei,
      `Unit`) — Singleton-Garantie
- [x] `McpServerCommand_ToolCallSequence_AllStdoutLinesAreValidJsonRpcFrames`
      (`Mcp/McpServerCommandJsonRpcFramingTests.cs`,
      `[Trait("Category", "Integration")]`) — E2E-Framing-Test mit
      echtem Subprozess
- [x] `McpCallLog_RecordStart_ThenEnd_WritesJsonLineWithAllFields`
      (`Mcp/McpCallLogTests.cs`, `Unit`)
- [x] `McpCallLog_RecordEnd_TruncatedResult_SetsTruncatedTrue`
      (gleiche Datei, `Unit`)
- [x] `McpCallLog_RecordEnd_EmptyResult_SetsEmptyTrue` (gleiche
      Datei, `Unit`)
- [x] `McpCallLog_Dispose_NoRecords_DeletesLogFile` (gleiche
      Datei, `Unit`)
- [x] `McpServerCommand_RunAsync_McpLogPathNotSet_CallLogIsNull`
      (`Commands/McpServerCommandCallLogTests.cs`, `Unit`)
- [x] `McpServerCommand_RunAsync_McpLogPathRelative_CreatesLogFileInSolutionDir`
      (gleiche Datei, `Unit`)
- [x] Bestehende Tests müssen ohne Änderung weiter grün laufen —
      insbesondere:
      - `McpServerAllToolsE2ETests` (9 Tool-E2E-Tests, alle weiter
        grün — der Wrapper ist eine reine Hinzufügung ohne
        Verhaltensänderung wenn `callLog == null`).
      - `McpTestClientRetryTests` + `McpServerAllToolsE2ETests`
        (Loading-Retry-Pfad unverändert).
      - `McpCodeGraphServerStalenessMtimeCacheTests` (B.5-Pfad
        unverändert).
      - `McpServerCommandLoadingStateTests` (Loading-State-Pfad
        unverändert).
      - Alle 1199+ bestehenden Tests grün (Stand step-010).
- [x] Volllauf `dotnet test AiNetLinter.slnx --no-build` muss am Ende
      grün sein (Definition of Done, analog jeder vorherige Step).

## Definition of Done

- [ ] Alle 15 Dateien oben umgesetzt (B.6: Datei 1-5; B.7: Datei 6-15)
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün:
      `dotnet build AiNetLinter.slnx` → 0 Warnungen, 0 Fehler
- [ ] Test-Command aus Tech-Stack-Notiz grün:
      `dotnet test AiNetLinter.slnx --no-build` → alle Tests
      (alt + neu) grün, keine TD-005-Regression
- [ ] Selbst-Lint grün:
      `dotnet run --project src\AiNetLinter -- --config rules.json
      --path .` → 0 Violations (oder dokumentierte, begründete
      PathOverride-Erweiterungen für die neuen Klassen)
- [ ] `Linter` selbst-lintet weiterhin alle Dateien im
      `AIContextFootprint`-Limit (≤ 2500) — falls `McpCallLog` +
      `McpLintConsole` PathOverrides brauchen, sind sie in `rules.json`
      mit Begründung pro Override dokumentiert
- [ ] Doku-Updates committet:
      `Docs/agent-api.md` (B.6-Absatz + B.7-Absatz),
      `Docs/integration.md` (B.6-Hinweis + B.7-Hinweis),
      `Docs/configuration.md` (B.7-CLI-Eintrag)
- [ ] `step-011/step-result.md` geschrieben mit
      Build/Test-Mess-Zahlen + Commit-Liste
- [ ] `status` in `step-plan.md` von `open` auf `done (pending audit)`
      gesetzt
- [ ] **Zwei Commits** (gemäß `spec.md` §10.3 + Konzept-Konvention für
      Code + Doku):
      1. Code-Commit: `feat: MCP-Server-stdout strukturell schützen
         (B.6) + Opt-in --mcp-log Call-Log (B.7) [codegraph-mcp-finish]`
      2. Doku-Commit: `docs: B.6 stdout-Schutz + B.7 Call-Log in
         agent-api/integration/configuration dokumentieren
         [codegraph-mcp-finish]`

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#1-grundprinzipien` —
  „Statische Kompilierung bevorzugen" (relevant: `McpLintConsole` +
  `McpCallLog` werden statisch kompiliert, keine Reflection / kein
  ALC), „Einfachheit vor Abstraktion" (relevant: schlanke 1-Zeilen-
  Lambda-Wrapper statt zentraler OnToolCall-Infrastruktur)
- `.agents/rules/AiNetLinterRichtlinien.mdc#2-architektur-verbote` —
  „Kein Dependency Injection Overhead" (relevant: `McpLintConsole`
  ist explizit KEIN DI-Container-Ersatz, sondern ein zweiter
  Singleton-Implementierungs-Punkt; `McpCallLog` wird per Parameter
  durchgereicht wie `LoadFunc` heute schon)
- `.agents/rules/AiNetLinterRichtlinien.mdc#3-build-and-test` —
  „Test-Logging & Fehlerdiagnose via `TestResults/latest.trx`"
  (relevant: bei B.6-E2E-Test-Failures wird TRX inspiziert, nicht
  blind re-laufen); „Vor jedem Build/Test offene `AiNetLinter.exe`/
  `testhost.exe`-Prozesse prüfen" (relevant: B.6-E2E-Test spawnt
  Subprozess, hinterlässt ggf. Hänger bei Test-Crash — Cleanup via
  `try/finally Dispose` und `using`)
- `.agents/rules/AiNetLinterRichtlinien.mdc#4-updates-and-tests` —
  „xUnit v3 Tests Pflicht" (alle neuen Tests sind xUnit v3), „MCP &
  Dogfood Testing über C#-Testinfra" (B.6-E2E-Test ist C#, kein
  Python), „Update-Pflicht für Docs/ROADMAP.md, Docs/configuration.md,
  README.md" (B.6 + B.7 erweitern agent-api.md + integration.md +
  configuration.md — `Docs/ROADMAP.md` ist die externe Roadmap und
  nicht die `tasks/.../roadmap.md`, nicht in scope), „Commit-Vorschlag
  Pflicht" (jede Coder-Antwort endet mit `### Commit-Vorschlag`-Block)
- `.agents/rules/AiNetLinterRichtlinien.mdc#5-qualitätsdrift-prävention` —
  „Zero-Warning-Direktive" (relevant: `<TreatWarningsAsErrors>true`
  muss halten), „Result-Pattern bevorzugen" (relevant: `McpCallLog`
  verwendet kein Exception-basiertes Logging, sondern
  `StartRecording` → `IDisposable`-Token-Pattern), „Symptom-Fixing
  verboten" (relevant: B.6 ist **strukturelles** Fixen, nicht
  Symptom-Unterdrückung), **„Verboten: Jede Referenz auf Task-/
  Planungsartefakte"** (relevant: kein `// siehe step-011` /
  `// B.6` / `// EPIC-06` in Code-Kommentaren; **die XML-Doc-Stelle
  in `McpServerOptionsBuilder.cs:13` mit dem `--mcp-log`-Hinweis ist
  eine forward-looking Erwartung, kein Planungsartefakt — die darf
  bleiben**; nach B.7-Implementierung kann der Coder sie auf
  Wunsch präzisieren, das ist aber nicht erzwungen), **„Verboten:
  Refactoring-Historie"** (relevant: `McpLintConsole` und `McpCallLog`
  enthalten keine „war früher X"-Marker)
- `.agents/rules/AiNetLinter.mdc` (Kurz-Stil) — `sealed` für
  `McpLintConsole` + `McpCallLog`, Methoden ≤ 60 Zeilen (Lambda-
  Wrapper sind 4-5 Zeilen, kein Problem), `#nullable enable` pro
  Datei (alle neuen Dateien tragen das `#nullable enable`-Pragma
  in Z. 1, exakt wie der projektweite Standard), `MaxLineCount`-Limit
  für `McpCallLog.cs` (~80-100 Zeilen realistisch, im Limit),
  `MaxMethodParameterCount: 4` für alle neuen Methoden (keine
  Methode mit 5+ Parametern — wo nötig, Input-`record`,
  siehe Vorlage `McpCodeGraphServerOptionsFromParameters` für
  gleiches Pattern in Schritt 3/8)
- `.agents/rules/AiNetLinter.mdc` (Grenzwerte) — `MaxAIContextFootprint
  ≤ 2500` für alle neuen Klassen; bei `McpCallLog.cs` ggf. ein
  PathOverride in `rules.json` mit Begründung
- `.agents/rules/AiNetLinter.mdc` (Checker) — `EnforceNoSilentCatch`
  in `McpCallLog.Dispose` (beim Stream-Schließen + File-Delete können
  IO-Exceptions auftreten, die explizit zu behandeln sind — der
  Konzept-Vorschlag „Datei löschen wenn leer" braucht ein
  `try/catch (IOException)`-Pattern mit Logging auf stderr via
  `Console.Error.WriteLine`); `BanAsyncVoid` in `McpCallLogScope`-
  Pattern (verwendet `IAsyncDisposable`, nicht `async void`)

## Bekannte Ausnahmen

- **`step-010`-Review-MINOR #1** (XML-Doc-Cleanup in
  `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs:14-24`,
  „…damit diese Klasse eigener `c>AIContextFootprint`…") — NICHT in
  diesem Step. Begründung: außerhalb EPIC-06-Scope, der step-010-Coder
  hat den Loading-Check in der Datei gesetzt, der XML-Doc-Cleanup
  wurde im Plan Z. 562-563 als „Aufräumen erlaubt" markiert, aber
  nicht erzwungen. Wird in einem späteren Schritt (EPIC-07 oder
  beliebiger nächster Touch-Punkt in `FindSymbolTool.cs`) erledigt.
  Aufwand: 1 Min. Der Coder darf ihn im Zuge der B.7-Wrapper-Patch-
  Arbeit an `FindSymbolTool.cs:25-36` mitnehmen (gleiche Datei),
  wenn der Wrapper-Patch die Datei ohnehin öffnet — das ist
  „Aufräumen erlaubt" und nicht erzwungen.
- **`step-010`-Review-MINOR #2** (`tech-debt.md` TD-005 + TD-007 Status
  nicht auf „geschlossen" gesetzt) — NICHT in diesem Step.
  Begründung: EPIC-06 ist Robustheit/Observability, keine
  Tech-Debt-Verwaltungs-Hygiene. Wird im **nächsten Schritt, der
  `tech-debt.md` ohnehin anfasst** (z. B. EPIC-07) erledigt.
  Aufwand: 2 Min. (zwei Status-Zeilen + zwei `closed_by: step-011`-
  Einträge).
- **TD-008** (ehemalige 6-Parameter-Signatur in
  `GetViolationsScanner.cs:192`) — NICHT in diesem Step (kein
  Berührungspunkt, `GetViolationsScanner` wird in step-011 nicht
  angefasst).
- **TD-006** (BOM-Diskrepanz in `.agents/rules/AiNetLinter.mdc`) —
  NICHT in diesem Step (kein Code-Schaden, reine Hygiene; der
  step-011-Coder fasst die Datei nicht an).
- **TD-001** (abgerissene XML-Doc-Kommentare in 3 Mcp-Testklassen) —
  NICHT in diesem Step (keine Berührungspunkte; die drei Klassen
  werden in step-011 nicht geöffnet).
- **Kein TD-Item** ist im EPIC-06-Scope → keine TD-Mitnahme in
  diesem Step (Begründung pro TD im Tech-Debt-Index bzw. im Plan
  Z. 4/5 oben).
- **Forward-Looking-Marker in `McpServerOptionsBuilder.cs:13`** mit
  dem Hinweis auf `--mcp-log` als künftige Erweiterung: nach
  step-011 ist dieser Marker **inhaltlich korrekt** (B.7 hat es
  umgesetzt) und kann vom Coder **optional** präzisiert werden
  (z. B. „Call-Log-State umgesetzt in step-011, weitere
  Erweiterungen denkbar"). Wenn der Coder den Kommentar präzisiert,
  muss er darauf achten, **keine Planungsartefakt-Referenz**
  (Schritt-Nummer, EPIC-ID) einzufügen (§5-Verbot). Andernfalls
  bleibt der Kommentar unverändert.

## Code-Skizze (optional)

### `McpLintConsole.cs` (komplette Datei)

```csharp
#nullable enable

using System;

namespace AiNetLinter.Output;

/// <summary>
/// MCP-spezifische <see cref="ILintConsole"/>-Implementierung, die <c>WriteLine</c> zwingend
/// nach <c>stderr</c> umleitet. Hintergrund: im MCP-Server-Modus ist <c>stdout</c> der
/// Transport-Kanal des JSON-RPC-Protokolls — ein einziger <c>Console.WriteLine</c>-Call
/// aus einer wiederverwendeten CLI-Klasse wuerde das Framing der gesamten Session
/// zerstoeren. Diese Implementierung macht den Schutz strukturell, nicht ueber Disziplin.
/// Singleton-Pattern analog <see cref="LinterConsole"/>.
/// </summary>
internal sealed class McpLintConsole : ILintConsole
{
    internal static readonly McpLintConsole Instance = new();

    private McpLintConsole() { }

    public void WriteLine(string message) => Console.Error.WriteLine(message);
    public void WriteError(string message) => Console.Error.WriteLine(message);
}
```

### `McpCallLog.cs` (Skizze der Kern-API)

```csharp
#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp;

internal sealed class McpCallLog : IAsyncDisposable
{
    private readonly StreamWriter _writer;
    private readonly string _logPath;
    private readonly Lock _writeLock = new();
    private int _entryCount;
    private bool _disposed;

    internal McpCallLog(string logPath)
    {
        _logPath = logPath;
        var dir = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        _writer = new StreamWriter(new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.Read),
                                   new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    internal McpCallLogScope StartRecording(string toolName, string args)
    {
        return new McpCallLogScope(this, toolName, args, Stopwatch.StartNew());
    }

    private void RecordEnd(McpCallLogScope scope, CallToolResult result)
    {
        scope.Stopwatch.Stop();
        var argsTruncated = scope.Args.Length > 200
            ? scope.Args[..200] + "..."
            : scope.Args;
        var text = ExtractText(result);
        var lines = text is null ? 0 : text.Split('\n').Length;
        var truncated = text is not null && (
            text.Contains("[N Treffer gesamt, M gezeigt —") ||
            text.Contains("[N Dateien mit Textfund, M gezeigt —"));
        var empty = result.IsError != true && lines == 0;

        var entry = new
        {
            ts = DateTime.UtcNow.ToString("O"),
            tool = scope.ToolName,
            args = argsTruncated,
            lines,
            truncated,
            duration_ms = scope.Stopwatch.Elapsed.TotalMilliseconds,
            empty,
        };
        var json = JsonSerializer.Serialize(entry);
        lock (_writeLock)
        {
            if (_disposed) return;
            _writer.WriteLine(json);
            _writer.Flush();
            _entryCount++;
        }
    }

    private static string? ExtractText(CallToolResult result)
    {
        if (result.Content is not { Count: > 0 }) return null;
        return result.Content[0] is TextContentBlock t ? t.Text : null;
    }

    public async ValueTask DisposeAsync()
    {
        lock (_writeLock)
        {
            if (_disposed) return;
            _disposed = true;
        }
        await _writer.DisposeAsync();
        if (_entryCount == 0)
        {
            try { File.Delete(_logPath); }
            catch (IOException) { /* Log-Delete-Fehler ist kein Blocker */ }
        }
    }
}

internal sealed class McpCallLogScope : IAsyncDisposable
{
    internal string ToolName { get; }
    internal string Args { get; }
    internal Stopwatch Stopwatch { get; }
    private readonly McpCallLog _log;
    private CallToolResult? _result;

    internal McpCallLogScope(McpCallLog log, string toolName, string args, Stopwatch sw)
    {
        _log = log;
        ToolName = toolName;
        Args = args;
        Stopwatch = sw;
    }

    public ValueTask DisposeAsync()
    {
        // Result wird vom Wrapper via SetResult geliefert, BEVOR Dispose aufgerufen wird.
        if (_result is { } r) _log.RecordEnd(this, r);
        return ValueTask.CompletedTask;
    }
}
```

### Wrapper-Pattern in `SymbolGraphToolRegistrations.cs` (Beispiel)

```csharp
// vorher (Z. 25-27):
tools.Add(McpServerTool.Create(
    (string namePattern, string? kind = null, int maxResults = 50, CancellationToken ct = default) =>
        FindSymbolTool.ExecuteAsync(mcpState, namePattern, kind, maxResults, ct),
    new McpServerToolCreateOptions { Name = "find_symbol", ... }));

// nachher (Wrapper-Pattern):
tools.Add(McpServerTool.Create(
    async (string namePattern, string? kind = null, int maxResults = 50, CancellationToken ct = default) =>
    {
        if (callLog is null)
            return await FindSymbolTool.ExecuteAsync(mcpState, namePattern, kind, maxResults, ct);
        using var scope = callLog.StartRecording("find_symbol", $"{namePattern}|{kind}|{maxResults}");
        var result = await FindSymbolTool.ExecuteAsync(mcpState, namePattern, kind, maxResults, ct);
        scope.Complete(result);
        return result;
    },
    new McpServerToolCreateOptions { Name = "find_symbol", ... }));
```

(`scope.Complete(result)` ist eine kleine Hilfsmethode, die das
Result im Scope hält, damit `DisposeAsync` es an `RecordEnd`
weitergeben kann — sauberer als `Stopwatch` + Result im Scope
zwei Mal zu setzen.)

## Notes

- **Scope-Disziplin**: EPIC-07 (TD-001/002/004/005/006/007) und
  EPIC-08 (E.1-E.3) sind **nicht** im Scope dieses Steps, auch wenn
  der Coder bei der Wrapper-Patch-Arbeit an `Mcp/SymbolGraphToolRegistrations.cs`
  oder `Mcp/FileStructureToolRegistrations.cs` oder `Mcp/AnalysisToolRegistrations.cs`
  zufällig über TD-Marker stolpert. Deren Schließung gehört in die
  jeweiligen Epics.
- **Doku-Commits-Strategie**: zwei Commits (Code + Doku), gemäß
  `spec.md` §10.3 + der etablierten Konvention aus step-007/step-008
  /step-009/step-010. Reihenfolge: erst Code-Commit, dann Doku-Commit
  (Doku-Commit kann eigenständig reviewt werden, falls das sinnvoll
  erscheint — Orchestrator entscheidet).
- **Keine TD-Mitnahme**: begründet im Plan-Schritt 4 (Risiko-Einschätzung)
  + oben in „Bekannte Ausnahmen". Die zwei step-010-MINOR-Items
  bleiben offen und werden in einem späteren Schritt erledigt (nicht
  in step-011).
- **B.6-E2E-Test als Regressions-Schutz**: der Test ist bewusst
  minimal gehalten (initialize + 1-2 tool-calls, nicht alle 9 Tools),
  um die Volllauf-Laufzeit nicht unnötig zu verlängern. Falls
  zukünftige Schritte neue Tools hinzufügen, ist der Test trotzdem
  ausreichend, weil er das **strukturelle** Verhalten prüft (kein
  Tool-spezifisches Leak), nicht die Tool-spezifische Korrektheit.
- **B.7-Pfad-Auflösung**: die Entscheidung „relativ zum
  Solution-Verzeichnis, nicht zum exeDir" ist eine
  Planer-Empfehlung, kein Zwang. Falls der Coder eine andere
  Auflösung bevorzugt (z. B. „relativ zum exeDir, weil cache/ auch
  dort liegt"), ist das akzeptabel — der Konzept-Wunsch „Ablage
  neben `cache/`" lässt beide Lesarten zu. Entscheidung im
  step-result dokumentieren.
- **McpCallLogScope.Complete-Result-Pattern**: der Planer schlägt
  dieses Pattern vor, damit `RecordEnd` das `CallToolResult` mit
  Zeilenzahl + Trunkierung + Leermenge auswerten kann. Eine
  Alternative wäre `McpCallLogScope` mit `internal CallToolResult?
  Result { get; set; }` und ein direkter Property-Set im Wrapper —
  semantisch identisch, nur Schreibweise anders. Beide sind §5-konform.
- **Wrapper-Overhead bei deaktiviertem Log**: der `if (callLog is
  null)`-Fast-Path im Wrapper ist absichtlich — wenn B.7 nicht
  aktiv ist, soll der Wrapper **keinen** zusätzlichen Allokations-
  Overhead verursachen (kein `McpCallLogScope`-Objekt, kein
  `Stopwatch.StartNew()`). Damit bleibt der Default-Pfad
  verhaltens- und performance-identisch zum Stand vor step-011.
- **PathOverride-Pflege**: nach Implementation muss der Coder
  `dotnet run --project src\AiNetLinter -- --config rules.json
  --path .` laufen lassen, prüfen ob neue Klassen (`McpLintConsole`,
  `McpCallLog`, ggf. `McpCallLogScope`) `MaxAIContextFootprint`-
  PathOverrides brauchen, und diese ggf. mit Begründung in
  `rules.json` ergänzen. Falls `McpCallLog` (mit
  `ModelContextProtocol.Protocol.CallToolResult` + `System.Text.Json`
  + `System.IO` als transitive Typen) das Limit reißt, ist das
  erwartet und kein Befund — ein PathOverride mit Begründung
  „JSONL-Writer + SDK-Result-Type als transitiver Footprint" ist
  ausreichend.
- **Tech-Stack-Konformität**: `McpCallLog` nutzt
  `System.Text.Json` (projektweit verfügbar, kein neues Paket
  nötig) und `ModelContextProtocol.Protocol.CallToolResult` (bereits
  referenziert). Keine neuen NuGet-Pakete erforderlich.
- **Commit-Subject-Konvention** (Erinnerung für den Coder): beide
  Commits tragen das Suffix `[codegraph-mcp-finish]` (gilt für
  Code- **und** Doku-Commits, gemäß `roadmap.md` Tech-Stack-Notiz
  Commit-Konventionen).
