---
status: open
type: step-plan
task: codegraph-mcp
step: 001
title: "CLI-Einstiegspunkt --mcp-server + minimaler stdio-MCP-Server (Solution-Auswahl, kein Tool-Set)"
epic: EPIC-01
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-07-31T09:45:00Z
related_to: []
---

# Step 001: CLI-Einstiegspunkt --mcp-server + minimaler stdio-MCP-Server

## Bezug

- **Task:** `codegraph-mcp`
- **Epic:** `EPIC-01` aus `roadmap.md` — CLI-Einstiegspunkt & Server-Grundgerüst.
  Dieser Step deckt EPIC-01 **nicht vollständig** ab: er liefert das neue
  Flag, den neuen Command, das NuGet-Paket, die verschärfte
  Solution-Auswahl (Mehrdeutigkeits-Abbruch) und einen tatsächlich
  startenden, verbindbaren stdio-MCP-Server mit (noch) leerem Tool-Set.
  **Nicht** Teil dieses Steps (bleibt für einen Folge-Step innerhalb
  desselben Epics bzw. wandert in EPIC-02): resident gehaltene,
  zustandsvolle Server-Klasse mit Staleness-Cache/Thread-Sicherheit — das
  ist explizit EPIC-02. Die "Server bleibt bei Ladefehler am Leben und
  liefert strukturierte [ERROR]-Antworten"-Anforderung aus EPIC-01 ist erst
  ab dem ersten echten Tool sinnvoll prüfbar (kein Tool-Call ohne Tool) —
  dieser Step bereitet sie vor (Solution-Ladefehler crasht den Prozess
  nicht, wird geloggt), das vollständige Verhalten (Tool-Antwort im
  `[ERROR]`-Format bei Ladefehler) wird beim ersten Tool in EPIC-03
  verifiziert.
- **Konzept-Referenz:** `konzept.md` Muss-Haben "Neuer Ausführungsmodus",
  "Solution-Auswahl beim Start" (inkl. Mehrdeutigkeits-Abbruch),
  Abschnitt "Zielplattformen" (MCP-SDK statt eigenem Protokoll, kein
  DI-Container), "Wo im Projekt" (`AiNetLinter.csproj` neue
  `PackageReference`).

## Aktueller Projektzustand (JIT-Kontext)

- **CLI-Optionen-Pipeline** ist klar dreigeteilt und muss an allen drei
  Stellen synchron erweitert werden: `Cli/CliOptionFactory.cs` (erzeugt
  `Option<T>`), `Cli/CliOptions.cs` (Record mit allen `Option<T>`-Feldern
  + aufgelöstes `CliParsedArgs`), `Cli/CliCommandBuilder.cs` (`Build()`
  registriert die Option am `RootCommand`, `Parse()` liest den Wert aus
  dem `ParseResult`). `Program.ToLinterArgs` mappt `CliParsedArgs` auf das
  öffentliche `LinterArgs`-Objekt, das alle Commands bekommen. Vorbild für
  ein reines `bool`-Flag ohne Zusatzparameter: `SyncAgentRulesOnly`
  (`CreateSyncAgentRulesOnlyOption`, `-saro`-Kurzform, `Program.cs` Zeile
  119 `if (args.SyncAgentRulesOnly) return SyncAgentRulesCommand.Run(args);`
  — schneller Pfad **vor** der normalen `ValidateArgs`/Special-Command-Kette).
  `--mcp-server` braucht denselben schnellen Pfad: **vor**
  `ValidateArgs` (das für "normale" Läufe `--path` zwingend verlangt,
  aber `--mcp-server` `--path` optional erlaubt, siehe unten), aber
  **nach** dem `# Run: ...`-Header-Print in `Main` (der würde auf stdout
  landen und das MCP-JSON-RPC-Framing zerstören — siehe "Notes").
- **Kein bestehender Mehrdeutigkeits-Abbruch:** `SourceFileCatalog.FindSolutionFile`
  → `SearchInDirectory` (Zeile 262-269) wählt bei mehreren `.slnx`/`.sln`-
  Kandidaten stillschweigend `files[0]`. Das bleibt für den bestehenden
  CLI-Batch-Modus **unverändert** (Non-Goal: kein Umbau des bestehenden
  Verhaltens) — der MCP-Modus braucht eine **eigene**, strengere Auflösung
  vor dem Server-Start, keine Änderung an `FindSolutionFile` selbst.
- **`ILintConsole`/`LinterConsole`** (`Output/`) ist der bestehende
  Konsolen-Abstraktionslayer aller Commands (siehe `ImpactCommand.RunAsync`,
  `MapCommand.RunAsync` — beide `console ?? LinterConsole.Instance`
  Parameter für Testbarkeit). `McpServerCommand` sollte für die
  **eigene** Diagnose-Ausgabe (nicht das MCP-Protokoll selbst, das läuft
  über den SDK-Transport auf stdin/stdout) `Console.Error`/`ILintConsole`
  konsistent zu den bestehenden Commands nutzen — niemals `Console.Out`
  (siehe "Notes").
- **`LinterErrorFormatter`/`LinterErrorCodes`** (`Output/`) ist das
  bestehende `[ERROR]: {code}: {message}`-Format, auf das `konzept.md`
  explizit verweist ("gleiches Format wie bestehendes `[ERROR]`-Schema").
  Für den Mehrdeutigkeits-Abbruch dieses Steps wiederverwenden (neuer Code
  z. B. `AMBIGUOUS_SOLUTION` in `LinterErrorCodes`) statt einer Ad-hoc-
  Fehlermeldung.
- **Kein Vorbild für einen "endlos laufenden" Command:** alle bestehenden
  Commands (`ImpactCommand`, `MapCommand`, ...) sind kurzlebig (ein
  Request-Response-Zyklus, `Task<int>` kehrt zurück). `McpServerCommand`
  ist der erste Command, der den Prozess am Leben hält, bis stdin
  geschlossen wird / der Client die Verbindung trennt — das ist neu,
  keine bestehende Struktur zu duplizieren, aber die Rückgabekonvention
  (`Task<int> RunAsync(LinterArgs args, CancellationToken ct = default, ILintConsole? console = null)`)
  wird trotzdem beibehalten (Exit-Code nach Server-Ende, `ct` für
  `Console.CancelKeyPress`-Shutdown, der in `Program.cs` bereits verdrahtet
  ist).
- **`ModelContextProtocol`-NuGet-Paket ist im Repo noch nicht referenziert**
  (geprüft: kein Treffer in `AiNetLinter.csproj` oder sonstwo im Code,
  nur in `konzept.md`/`roadmap.md` erwähnt). Recherche zur aktuellen
  SDK-API (siehe Quellen unten) zeigt zwei Nutzungsmuster:
  1. **Generic-Host-Muster** (`Host.CreateEmptyApplicationBuilder(...)`,
     `builder.Services.AddMcpServer().WithStdioServerTransport()...`) —
     das ist der in Beispielen/Doku dominante Weg, nutzt aber intern
     `Microsoft.Extensions.DependencyInjection` (`IServiceCollection`).
  2. **Low-Level-Muster** ohne Host/DI: `StdioServerTransport` hat einen
     Konstruktor, der direkt `McpServerOptions` (+ optional
     `ILoggerFactory`) annimmt, kombiniert mit `McpServerFactory.Create(...)`
     liefert das einen `IMcpServer`, den man direkt `RunAsync()`-t — ganz
     ohne `IServiceCollection`/Generic-Host.
  - **Entscheidung für diesen Step:** Muster 2 (Low-Level, ohne
    Generic-Host/DI) verwenden — `AiNetLinterRichtlinien.mdc` §2 verbietet
    "DI-Container Overhead" ausdrücklich ("Nutze statische Klassen oder
    direkte Instanziierung"), und `konzept.md` "Zielplattformen" bestätigt
    "Kein DI-Container ... Server-Zustand ... über eine einzelne
    zustandshaltende Klasse, direkt instanziiert". Der Generic-Host-Weg
    des SDKs würde dem widersprechen, obwohl er in der Doku prominenter
    ist — das ist eine bewusste Abweichung vom "Standard-Beispiel" des
    SDKs zugunsten der Projekt-Leitplanke. Falls sich beim Implementieren
    zeigt, dass `McpServerFactory`/die Low-Level-API in der tatsächlich
    aufgelösten Paketversion anders heißt oder fehlt: kurz in
    `step-result.md` dokumentieren, welche tatsächliche API genutzt wurde
    und warum (kein Rules-Verstoß, wenn die grundsätzliche Linie "kein
    IServiceCollection/Host-Builder für AiNetLinters eigenen Code"
    eingehalten bleibt).
  - **Quellen (Web-Recherche, nicht Teil des Repos):**
    https://github.com/modelcontextprotocol/csharp-sdk (offizielles Repo),
    https://modelcontextprotocol.github.io/csharp-sdk/api/ModelContextProtocol.Server.StdioServerTransport.html
    (API-Referenz `StdioServerTransport`).

## Intention

Nach diesem Step lässt sich `ainetlinter --mcp-server [--path <Pfad>]`
starten: das Programm löst die Ziel-Solution nach derselben `--path`-
Semantik wie alle anderen Commands auf (Datei direkt, Verzeichnis mit
Auto-Suche, Default = aktuelles Arbeitsverzeichnis, wenn `--path` fehlt),
bricht bei mehreren gefundenen `.sln`/`.slnx`-Kandidaten mit einer klaren,
alle Kandidaten benennenden Fehlermeldung ab, lädt die eindeutig
aufgelöste Solution einmalig, und startet danach einen stdio-MCP-Server
(offizielles SDK), der sich von einem MCP-Client verbinden lässt und über
`tools/list` ein (noch) leeres Tool-Array meldet. Schlägt das Laden der
Solution fehl, crasht der Prozess nicht — der Server startet trotzdem
(Zustand "Solution nicht verfügbar" wird nur geloggt, noch nicht als
Tool-Fehlerantwort ausgeliefert, da es noch keine Tools gibt). Das ist die
Grundlage, auf der EPIC-02 (resident gehaltener Zustand, Staleness) und
EPIC-03 (die eigentlichen 9 Tools) aufbauen.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/AiNetLinter.csproj`

- **Was:** Neue `<PackageReference Include="ModelContextProtocol" Version="<aktuelle stabile Version>" />`
  in der bestehenden `<ItemGroup>` mit den anderen `PackageReference`-
  Einträgen (Zeile 12-20) ergänzen. Coder ermittelt die aktuelle stabile
  NuGet-Version zum Implementierungszeitpunkt (z. B. via
  `dotnet add package ModelContextProtocol` im Projektverzeichnis, das
  löst automatisch die neueste passende Version auf und trägt sie ein).
- **Warum:** `konzept.md` "Zielplattformen" — offizielles C#-SDK statt
  eigenem Protokoll-Handrolling.

### Datei 2: `src/AiNetLinter/Cli/CliOptionFactory.cs`

- **Was:** Neue Methode `CreateMcpServerOption()`, analog zu
  `CreateSyncAgentRulesOnlyOption()`:
  ```csharp
  internal static Option<bool> CreateMcpServerOption() => new("--mcp-server")
  {
      Description = "Startet einen stdio-basierten MCP-Server (Model Context Protocol) statt eines Batch-Laufs. --path optional, Default: aktuelles Arbeitsverzeichnis.",
  };
  ```
- **Warum:** Konsistent mit bestehendem Muster für reine Bool-Flags ohne
  Zusatzwert.

### Datei 3: `src/AiNetLinter/Cli/CliOptions.cs`

- **Was:** Neues Feld `Option<bool> McpServer` an `CliOptions`-Record
  anhängen (nach `IgnoreSuppressions`, letztes Feld). Neues Feld
  `bool McpServer` an `CliParsedArgs`-Record anhängen (gleiche Position).
- **Warum:** Durchreichen des geparsten Flags bis `Program.cs`.

### Datei 4: `src/AiNetLinter/Cli/CliCommandBuilder.cs`

- **Was:**
  - `Build()`: `options.McpServer` in die `RootCommand`-Initialisierer-Liste
    aufnehmen (letztes Element).
  - `CreateOptions()`: `CliOptionFactory.CreateMcpServerOption()` als
    letztes Konstruktor-Argument an `new CliOptions(...)` anhängen.
  - `Parse()`: `McpServer: parseResult.GetValue(options.McpServer)` als
    letztes Argument an `new CliParsedArgs(...)` anhängen.
- **Warum:** Vollständige Verdrahtung durch die bestehende dreistufige
  Options-Pipeline (siehe "Aktueller Projektzustand").

### Datei 5: `src/AiNetLinter/Cli/LinterArgs.cs`

- **Was:** Neue Property `public bool McpServer { get; init; }` ergänzen
  (mit XML-Doc-Kommentar analog zu den bestehenden Properties, z. B. nahe
  `SyncAgentRulesOnly`).
  Zusätzlich `Validate()`/`HasStandaloneCommand()` anpassen: `--mcp-server`
  gehört wie `Docs`/`MapType`/etc. zu den Fällen, die **kein** `--path`
  zwingend erfordern (Default = aktuelles Arbeitsverzeichnis laut
  `konzept.md`) — `HasStandaloneCommand()` um `|| McpServer` erweitern.
- **Warum:** `konzept.md` Muss-Haben "Fehlt `--path`, wird das aktuelle
  Arbeitsverzeichnis verwendet". Die Pfadauflösung selbst (Default auf
  cwd) passiert **nicht** in `LinterArgs`, sondern in `McpServerCommand`
  (siehe Datei 7) — hier wird nur verhindert, dass die generische
  `--path`-Pflichtprüfung greift.

### Datei 6: `src/AiNetLinter/Output/LinterErrorCodes.cs`

- **Was:** Neue Konstante `internal const string AmbiguousSolution = "AMBIGUOUS_SOLUTION";`
  ergänzen.
- **Warum:** Für den Mehrdeutigkeits-Abbruch in `McpServerCommand`
  wiederverwendbar, statt eine Ad-hoc-Zeichenkette zu verwenden — konsistent
  mit den bestehenden Codes (`ConfigRequired`, `ResourceNotFound`, ...).

### Datei 7: `src/AiNetLinter/Commands/McpServerCommand.cs` (neu)

- **Was:** Neuer `internal static class McpServerCommand` mit
  `internal static async Task<int> RunAsync(LinterArgs args, CancellationToken ct = default, ILintConsole? console = null)`:
  1. **Pfadauflösung:** `--path` fehlt → aktuelles Arbeitsverzeichnis
     (`Directory.GetCurrentDirectory()`) verwenden, sonst `args.TargetPath`.
  2. **Mehrdeutigkeits-Abbruch:** eigene, kleine private Hilfsmethode
     (z. B. `ResolveSolutionPathOrError`), die — anders als
     `SourceFileCatalog.FindSolutionFile`/`SearchInDirectory` — bei
     `File.Exists(path)` direkt die Datei nimmt, bei `Directory.Exists(path)`
     **alle** `.sln`+`.slnx`-Kandidaten sammelt und:
     - 0 Treffer → `[ERROR]`-Ausgabe (`ResourceNotFound` oder neuer Code,
       Planer überlässt die Detailwahl dem Coder) über `console`/`Console.Error`,
       Rückgabe `1`, **kein** Server-Start.
     - 1 Treffer → diesen Pfad verwenden, weiter zu Schritt 3.
     - ≥2 Treffer → `[ERROR]`-Ausgabe mit `LinterErrorCodes.AmbiguousSolution`,
       `context` listet alle gefundenen Kandidaten-Pfade, `hint` verweist
       auf `--path <konkrete-Datei>`, Rückgabe `1`, **kein** Server-Start.
     - **Bewusst keine Änderung an `SourceFileCatalog.FindSolutionFile`
       selbst** — die bestehende CLI-Logik (`files[0]`-Fallback) bleibt für
       alle anderen Commands unverändert (siehe "Aktueller Projektzustand").
  3. **Solution laden:** `SourceFileCatalog.LoadAsync(resolvedPath, ct)`
     in einem `try`/`catch` — bei Fehlschlag: Fehler über
     `Console.Error`/`console` loggen (nicht crashen, kein leeres `catch`
     — siehe `AiNetLinter.mdc` "Kein leeres catch"), Server **trotzdem**
     starten (leerer Tool-Katalog kann so oder so nichts über den
     Solution-Zustand aussagen, das strukturierte `[ERROR]`-pro-Tool-Call-
     Verhalten kommt erst mit dem ersten Tool in EPIC-03).
  4. **MCP-Server starten:** `StdioServerTransport` + `McpServerOptions`
     (Low-Level-API ohne Generic-Host/DI, siehe "Aktueller Projektzustand"
     für die Begründung) mit `ServerInfo` (Name z. B. `"ainetlinter"`,
     Version aus `AiNetLinter.csproj` `<Version>` oder einer Konstante),
     (noch) keine registrierten Tools (leeres `Capabilities.Tools`/keine
     `WithTools*`-Aufrufe — Tool-Set kommt in EPIC-03). Server per
     `RunAsync(ct)` laufen lassen, bis der Client trennt oder `ct`
     signalisiert wird (`Console.CancelKeyPress` ist in `Program.cs`
     bereits verdrahtet).
  5. Rückgabe `0` nach regulärem Server-Ende (Client hat Verbindung
     getrennt) bzw. beim `OperationCanceledException`-Pfad, den `Program.cs`
     bereits global abfängt.
- **Warum:** Neuer Ausführungsmodus, EPIC-01. Struktur orientiert an
  `ImpactCommand`/`MapCommand` (gleiche Signatur-Konvention), aber mit dem
  neuen Aspekt "läuft, bis der Client trennt" statt Request-Response.

### Datei 8: `src/AiNetLinter/Program.cs`

- **Was:**
  - In `ToLinterArgs`: `McpServer = parsed.McpServer,` ergänzen.
  - In `Main`, **vor** dem `# Run: ...`-Header-Print (Zeile 40-45): wenn
    `linterArgs.McpServer` gesetzt ist, den Header **nicht** ausgeben
    (stdout muss für den MCP-Client sauber bleiben, siehe "Notes") und
    direkt `return await McpServerCommand.RunAsync(linterArgs, cts.Token);`
    aufrufen — **vor** `ExecuteLinterAsync`/`ValidateArgs`, analog zum
    bestehenden `SyncAgentRulesOnly`-Fast-Path in `ExecuteLinterAsync`
    (Zeile 119), aber hier eine Ebene höher in `Main` selbst, weil auch
    der Header-Print vermieden werden muss, der in `Main` und nicht in
    `ExecuteLinterAsync` passiert.
- **Warum:** Dispatch auf den neuen Modus, ohne das bestehende stdout-
  Verhalten für alle anderen Modi zu verändern.

## Tests

- [ ] `McpServerCommandTests.cs` (neu, `src/AiNetLinter.Tests/`, Vorbild:
      Struktur bestehender Integrationstests wie `ProgramTests.cs`/
      `DiffImpactAnalyzerTests.cs`):
  - Mehrdeutigkeits-Abbruch: Verzeichnis mit zwei `.slnx`-Dateien (Test-
    Fixture, z. B. unter `tests/Fixtures/` ein neues Mini-Fixture-Paar
    anlegen oder — falls einfacher — zwei leere `.slnx`-Dateien in einem
    temporären Verzeichnis erzeugen) → `RunAsync` liefert `1`, Fehlerausgabe
    enthält `AMBIGUOUS_SOLUTION` und beide Dateinamen.
  - Kein `.sln`/`.slnx` gefunden → `RunAsync` liefert `1`, strukturierte
    `[ERROR]`-Ausgabe, kein Absturz/keine Exception nach außen.
  - `--path` fehlt → aktuelles Arbeitsverzeichnis wird als Basis für die
    Auflösung verwendet (Test kann das über einen temporären
    `Directory.SetCurrentDirectory`-Wechsel + ein einzelnes Solution-
    Fixture prüfen, oder — falls `McpServerCommand` die Pfadauflösung als
    separat testbare, reine Funktion exponiert (bevorzugt, siehe "Notes")
    — direkt gegen diese Funktion testen).
  - Erfolgreicher Start + Verbindung: Server mit einer validen Test-
    Solution (z. B. `tests/Fixtures/BaselineMini/` falls dort eine `.sln`/
    `.slnx` liegt, sonst kleinstes geeignetes bestehendes Fixture) starten,
    über einen MCP-Client (SDK bietet i. d. R. einen `McpClientFactory`/
    In-Memory- oder Pipe-Transport für Tests — Coder recherchiert die
    testfreundlichste Variante der SDK-Version) `initialize` + `tools/list`
    aufrufen, Tool-Array ist leer, kein Fehler.
  - Solution lädt nicht (kaputte `.slnx`) → Server-Start crasht nicht
    (Prozess/Aufruf kehrt kontrolliert zurück bzw. Server läuft trotzdem,
    je nach gewählter Teststrategie).
- [ ] Bestehende Tests bleiben grün (`dotnet test AiNetLinter.slnx`) —
      insbesondere `ProgramTests.cs`, da `Program.cs`/`LinterArgs.cs`
      angefasst werden.

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt (Dateien 1-8)
- [ ] `dotnet build AiNetLinter.slnx` grün, keine neuen Warnungen
      (`TreatWarningsAsErrors`)
- [ ] `dotnet test AiNetLinter.slnx` grün (neue + bestehende Tests)
- [ ] `ainetlinter --mcp-server --path <Solution>` lässt sich manuell
      starten und mit einem MCP-Client verbinden (mind. `tools/list` liefert
      leeres Array) — falls das im Rahmen dieses Steps nicht praktikabel
      manuell verifizierbar ist (kein MCP-Client zur Hand), genügt der
      automatisierte Test dafür als Ersatz, aber das sollte in
      `step-result.md` explizit vermerkt werden.
- [ ] Commit auf aktuellem Branch (Conventional Commit, Englisch, Suffix
      `[codegraph-mcp]`, siehe Tech-Stack-Notiz in `roadmap.md`)
- [ ] `step-001/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)`
      gesetzt
- [ ] `### Commit-Vorschlag`-Abschnitt am Ende der Coder-Antwort
      (`AiNetLinterRichtlinien.mdc` §4)

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §1/§2 — kein Plugin-System,
  kein `AssemblyLoadContext`, **kein DI-Container**: direkt maßgeblich für
  die Wahl der Low-Level-MCP-SDK-API ohne `IServiceCollection`/Generic-Host
  (siehe "Aktueller Projektzustand"). §3 (PowerShell/`git --no-pager`) für
  alle Shell-Interaktionen des Coders. §4 Commit-Vorschlag-Pflicht,
  Doku-Update-Pflicht bei Feature-Änderungen (Doku-Updates selbst sind
  EPIC-08, aber `Docs/ROADMAP.md`-Eintrag "in Arbeit" für diesen neuen
  Modus ist an dieser Stelle optional, kein Muss — Planer entscheidet
  bewusst, das nicht in diesen Step zu ziehen, um ihn nicht unnötig zu
  vergrößern; volle Doku-Pflicht bleibt EPIC-08). §5 Zero-Warning-Direktive.
- `.agents/rules/AiNetLinter.mdc` — `#nullable enable` in allen neuen
  Dateien, `sealed` wo zutreffend (Achtung: `McpServerCommand` ist wie
  `ImpactCommand`/`MapCommand` eine `internal static class` — `sealed`
  gilt nicht für statische Klassen), Methoden ≤60 Zeilen (die
  `RunAsync`-Hauptmethode in `McpServerCommand` wahrscheinlich in mehrere
  private Hilfsmethoden aufteilen, analog zu `MapCommand.RunAsync` +
  `ResolveMaxLineCount`/`ReportUnknownType`), kein leeres `catch` beim
  Solution-Ladefehler, max. 4 Parameter/max. 1 `bool`-Parameter (`RunAsync`-
  Signatur mit `LinterArgs args, CancellationToken ct, ILintConsole? console`
  ist bereits das etablierte 3-Parameter-Muster, nicht anfassen).

## Bekannte Ausnahmen

- Keine.

## Code-Skizze (optional)

```csharp
// McpServerCommand.cs — grobe Form, kein exaktes SDK-API-Zitat
// (Coder verifiziert die exakte API der tatsächlich aufgelösten Paketversion)

internal static async Task<int> RunAsync(LinterArgs args, CancellationToken ct = default, ILintConsole? console = null)
{
    var c = console ?? LinterConsole.Instance;
    var pathOrError = ResolveSolutionPathOrError(args.TargetPath, c);
    if (pathOrError is null) return 1; // Fehler wurde bereits ausgegeben

    SourceFileCatalog? catalog = null;
    try
    {
        catalog = await SourceFileCatalog.LoadAsync(pathOrError, ct);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[WARN]: MCP-Server startet ohne geladene Solution: {ex.Message}");
    }

    var transport = new StdioServerTransport(new McpServerOptions
    {
        ServerInfo = new Implementation { Name = "ainetlinter", Version = "1.0.78" },
        // Capabilities.Tools bleibt leer — EPIC-03 fuegt Tools hinzu.
    });

    await using var server = McpServerFactory.Create(transport, /* options */ null!);
    await server.RunAsync(ct);
    return 0;
}
```

## Notes

- **stdout ist für den Coder tabu außerhalb des MCP-Transports.** Das
  gesamte JSON-RPC-Framing des MCP-Protokolls läuft über stdin/stdout —
  jede zusätzliche `Console.WriteLine`/`c.WriteLine`-Ausgabe (wie der
  bestehende `# Run: ...`-Header in `Program.cs` oder `ILintConsole`-
  Aufrufe in `McpServerCommand`) würde das Framing zerstören. Diagnose/
  Fehlermeldungen aus `McpServerCommand` **ausschließlich** über
  `Console.Error` (nicht `ILintConsole`/`LinterConsole.Instance`, falls
  diese intern auf `Console.Out` schreibt — Coder verifiziert das kurz an
  `Output/LinterConsole.cs`, bevor er sie im MCP-Pfad verwendet; im
  Zweifel direkt `Console.Error.WriteLine` statt `ILintConsole` für alles,
  was **vor** dem eigentlichen Server-Start passiert, wie den
  Mehrdeutigkeits-Abbruch).
- **Pfadauflösung als separat testbare Funktion:** Damit die Tests für
  den Mehrdeutigkeits-Abbruch nicht zwingend einen echten Prozessstart/
  MSBuild-Solution-Load brauchen, sollte die reine Kandidaten-Ermittlung
  (Verzeichnis scannen, 0/1/≥2-Fälle unterscheiden) als eigene, von
  `RunAsync` unabhängig aufrufbare (`internal static`) Methode ohne I/O
  jenseits `Directory.GetFiles`/`File.Exists` stehen — das macht sie ohne
  echten MSBuild-Workspace testbar.
- **Kein `[McpServerToolType]`/`WithToolsFromAssembly()` in diesem Step**
  — das ist genau der Teil, der DI/Assembly-Scanning nahelegt und laut
  Recherche eng an das Generic-Host-Muster gekoppelt ist. EPIC-03 klärt,
  wie die 9 Tools registriert werden (ggf. manuell über
  `McpServerOptions.Capabilities.Tools.ToolCollection` oder eine
  vergleichbare Low-Level-API, statt Attribut-basiertem Assembly-Scan) —
  bewusst nicht in diesem Step vorentschieden, da noch kein Tool existiert,
  an dem sich das Muster verifizieren ließe.
- **`San.smart.Planner.Platform`-Praxistest (EPIC-09) ist hier nicht
  Scope** — dieser Step wird nur gegen kleine Test-Fixtures verifiziert.
- Sollte sich beim Implementieren zeigen, dass die aktuelle
  `ModelContextProtocol`-Paketversion die Low-Level-API
  (`StdioServerTransport`-Konstruktor mit `McpServerOptions`,
  `McpServerFactory.Create`) nicht wie recherchiert anbietet: kurz
  dokumentieren (in `step-result.md`, Abschnitt "Abweichungen vom Plan"),
  welche tatsächliche API genutzt wurde. Das ist **kein** automatischer
  Blocker, solange die Grundlinie "keine `IServiceCollection`/kein
  Generic-Host-Builder in AiNetLinters eigenem Code" eingehalten bleibt.
