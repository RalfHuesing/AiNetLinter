---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: large
rules_dir: .agents/rules
last_updated: 2026-08-03
open_questions: []
supersedes: tasks/codegraph-mcp-server, tasks/codegraph-mcp-next
---

# Konzept: AiNetLinter MCP-Codegraph-Server — Fertigstellung

## Ziel (Was)

Der `--mcp-server`-Modus von AiNetLinter (stdio-MCP-Server mit granular
abfragbaren Codegraph-/Lint-Tools für AI-Coding-Agenten) ist in seinem
ursprünglich geplanten Kernumfang (EPIC-01 bis EPIC-08, 9 Tools) fertig,
reviewt und gemergt. Dieser Task schließt das ab, was danach als
**verbindliches Scope** beschlossen, aber nicht fertig umgesetzt wurde —
sieben konkrete Server-Erweiterungen, eine unreviewte Coder-Einheit, ein
wachsender struktureller Tech-Debt-Block — und erweitert den Symbolgraphen
zusätzlich um drei Punkte aus dem separaten Ideen-Backlog
(`tasks/codegraph-mcp-next`), die der Nutzer bewusst **nicht** als optional
eingestuft haben will (siehe Entscheidung unten: "Erfahrungsgemäß werden
Nice-to-Have-Punkte von Agenten-Loops nie umgesetzt"). Nach Abschluss dieses
Tasks werden `tasks/codegraph-mcp-server` und `tasks/codegraph-mcp-next`
gelöscht — alles inhaltlich Relevante daraus steht ab hier.

## Warum / Kontext

- AiNetLinter läuft produktiv gegen sehr große Bestandscodebasen. Der
  MCP-Modus soll AI-Agenten granulare, semantisch präzise Tools statt
  `rg`/`grep`-Exploration geben (Kontext-/Token-Ersparnis, siehe
  Ursprungs-Recherche in der jetzt gelöschten `codegraph-mcp-server/konzept.md`,
  Kernaussage bleibt gültig: ~60 % geringere Kosten, ~69 % weniger Tokens bei
  Codegraph-Indexing vs. grep-Loops, Quelle: Anthony West, "Code Intelligence
  & Code-Graph Indexing for AI Agents", 2026).
- **Zwei Vorgänger-Ordner werden hier konsolidiert:**
  - `tasks/codegraph-mcp-server` — der Umsetzungs-Task (11 Einheiten via
    `dynamic-loop`, EPIC-01..08 approved, EPIC "P0/P1-Erweiterungen" nur
    teilweise umgesetzt, siehe unten).
  - `tasks/codegraph-mcp-next` — ein reiner Ideen-Backlog (P2, "später"),
    nie ein eigener Umsetzungs-Task, hing formal von `codegraph-mcp-server`
    ab (`depends_on`-Feld).
- **Warum jetzt und nicht einfach weiterlaufen lassen:** Der letzte
  Orchestrator-Lauf endete mitten in Einheit 011 ohne Kritiker-Review
  (User-Stopp). Seitdem liegen 6 unreviewte Commits lokal, inklusive einer
  Plan-Abweichung, die die projekteigene Architekturregel (`AIContextFootprint`
  ≤ 2500) an 13 Stellen im MCP-Modul per `PathOverride` auf 2700 lockert. Das
  ist der richtige Punkt für eine bewusste Bestandsaufnahme statt eines
  reflexhaften "Einheit 012 aufrufen".

## Bereits umgesetzt (Stand 2026-08-03, verifiziert gegen Code, nicht nur gegen Doku)

**Fertig, reviewt, gemergt (approved, in `main`):**

- EPIC-01 (CLI-Flag `--mcp-server` + Solution-Auswahl), EPIC-02
  (Resident-Server + lazy Staleness-Invalidierung über Hash/mtime pro
  bekanntem `Document`), EPIC-03 (5 Symbolgraph-Tools), EPIC-04 (alle 4
  Struktur-/Qualitäts-Tools inkl. `search_pattern`) — **alle 9 MCP-Tools
  sind vollständig implementiert und reviewt:** `get_index_scope`,
  `find_symbol`, `find_references`, `get_impact`, `get_type_hierarchy`,
  `get_file_skeleton`, `get_hotspots`, `get_violations` (inkl. Regel-ID pro
  Verstoß), `search_pattern`.
- EPIC-05 (Scope-Kommunikation via `initialize`-`instructions` + Miss-Hint
  in `find_symbol`), EPIC-06 (Compile-Fehler-Warnhinweis in allen 9 Tools
  statt Absturz, Server-Lifecycle-Robustheit), EPIC-07 (Test-Ausbau:
  Staleness, Mehrdeutigkeits-Abbruch, Cache-Isolation, CLI-Regression),
  EPIC-08 (Doku: `Docs/agent-api.md`, `Docs/integration.md` inkl.
  Tool-vs-`rg`-Priorisierungsempfehlung, `Docs/ROADMAP.md`, `README.md`).
- Trunkierung + `maxResults` (Default 50) in allen 4 Listen-Tools
  (`find_symbol`, `find_references`, `get_impact`, `search_pattern`),
  einheitliche Text-Meta-Zeile bei Kürzung, `McpTruncation.cs` als
  gemeinsamer Helper.
- Test-Infrastruktur umgebaut: `McpTestClient` (C#, `StdioClientTransport`)
  ersetzt alle Python-Dogfooding-Skripte, `Category=Unit`/`Category=Integration`
  als Testfilter, `McpLiveRepositoryTests` als laufendes Dogfooding gegen die
  eigene `AiNetLinter.slnx`.

**Codiert, aber nicht reviewt/nicht gepusht — Einheit 011 (Commits `4bcd5ab`,
`075a8a0`, `af41a6b`, `1201840`, `a530b4f`, `8a663c7`, lokal, 6 Commits):**

- **TD-009** (Konstruktor-Record): `McpCodeGraphServer` nimmt jetzt 1
  Parameter (`McpCodeGraphServerOptions`) statt 5 — verifiziert im Code
  ([McpCodeGraphServer.cs](src/AiNetLinter/Mcp/McpCodeGraphServer.cs),
  [McpCodeGraphServerOptions.cs](src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs)).
- **TD-014** (Factory-Aufteilung): `McpServerOptionsBuilder` (54 Z., Fluent-API)
  + schlanke `McpServerOptionsFactory` (52 Z.) — verifiziert im Code
  ([McpServerOptionsBuilder.cs](src/AiNetLinter/Mcp/McpServerOptionsBuilder.cs)).
- **TD-019** (Test-Flake): Retry-Loop in `McpTestClient.ConnectAsync` gegen
  parallele MCP-Init-Timeouts — verifiziert im Code.
- **Nicht durch einen Kritiker geprüft.** Build laut Coder-Bericht 0/0,
  Volllauf 1191/1191 — **nicht durch diese Session erneut nachgefahren**
  (lokaler Build schlägt aktuell mit `MSB3027`/Datei-Sperre fehl, siehe
  "Entdeckte Mängel/Redundanzen").
- **Plan-Abweichung, ebenfalls ungereviewt:** `rules.json` wurde um 9 neue
  `PathOverride: MaxAIContextFootprint 2700`-Einträge erweitert (zusätzlich
  zu den 4 bereits bestehenden = **13 Dateien insgesamt**, siehe
  "Entdeckte Mängel/Redundanzen").
- Working Tree: clean, **11 Commits lokal ohne Push** (8 davon
  `codegraph-mcp-server`-Einheiten 009-011, 2 externe
  `.agents/Agent-Scaffolding`-Squash-Merges, 1 `docs(rules)`-Commit von
  Ralf) — Stand `git log --oneline -1` = `59c2f5e`.

**Entscheidung dieses Konzepts (siehe "Scope > Muss-Haben" A):** Einheit 011
wird **so wie sie ist** review-abgeschlossen, die `PathOverride`-Erweiterung
wird als Pragmatik akzeptiert — der strukturelle Fix folgt separat als
eigene Muss-Haben-Einheit C, nicht als Voraussetzung für den 011-Abschluss.

**Noch nicht begonnen — sieben P0/P1-Erweiterungen, in `konzept.md` (jetzt
gelöscht) als "kein offener Punkt mehr" (Scope-Entscheidung) markiert, aber
**nie codiert** — verifiziert per Code-Grep, nicht nur laut `Docs/ROADMAP.md`,
das sie selbst korrekt als "Geplant" führt:** `rules.json`-Auto-Discovery,
Verzeichnis-Sweep für neue/gelöschte `.cs`-Dateien, generierte Last-Fixture,
Kaltstart-Entkopplung, Staleness-Sweep über Verzeichnis-`mtime`, stdout-Schutz
(eigene `ILintConsole`), Opt-in Call-Log (`--mcp-log`). Details siehe
"Scope > Muss-Haben" B.

## Scope

### Muss-Haben

**Alles in diesem Abschnitt ist Muss-Haben — bewusst kein separater
"Nice-to-Have"-Rang für die P2-Punkte (E).** Nutzer-Entscheidung: Punkte,
die nur als "optional" markiert sind, werden von Agenten-Loops erfahrungsgemäß
nie umgesetzt — deshalb werden auch die drei aus `codegraph-mcp-next`
übernommenen Erweiterungen hier als verbindlich geführt.

**A. Einheit 011 formal abschließen**

- Vor jedem Build/Test in dieser Einheit: offene `AiNetLinter.exe`/
  `testhost.exe`-Prozesse prüfen und ggf. beenden (siehe "Entdeckte
  Mängel/Redundanzen" — lokaler Build war zuletzt durch eine Datei-Sperre
  blockiert).
- Volllauf `dotnet test AiNetLinter.slnx --no-build` frisch fahren (nicht
  nur den Coder-Bericht aus `units/011/result.md` übernehmen).
- Kritiker-Review für die 6 lokalen 011-Commits nachholen (TD-009, TD-014,
  TD-019), **inklusive** der 9-Datei-`PathOverride`-Erweiterung in
  `rules.json` als akzeptiertem Pragmatik-Fix (Entscheidung s. o.).
  Schwerpunkt zusätzlich: A3-Nachweis für TD-019 ist nicht abschließend
  (Last-Test lief laut Coder-Bericht auch **ohne** Retry-Loop grün — der
  Flake aus Einheit 010 gilt als nicht deterministisch reproduzierbar,
  Retry-Logik ist Absicherung, kein bewiesener Fix; im Review als
  akzeptierte Restunschärfe vermerken, kein Blocker).
- Push-Entscheidung für die 11 lokalen Commits nach Kritiker-`approved`.

**B. Die sieben offenen P0/P1-Erweiterungen**, in dieser Reihenfolge
(Betriebsrisiko vor Komfort — Nutzer-Entscheidung: silent-falsche
Tool-Antworten zuerst beheben, dann erst mit belastbaren Zahlen gegen die
zeitbasierten Fixes arbeiten):

1. **`rules.json`-Auto-Discovery.** `ResolveConfig`/`ResolveMaxLineCount`
   ([McpServerCommand.cs:55-80](src/AiNetLinter/Commands/McpServerCommand.cs))
   laden `rules.json` **nur** bei explizit gesetztem `--config` — verifiziert,
   kein Auto-Discovery-Pfad im Code. Der MCP-Normalfall ist Registrierung
   durch den Host **ohne** manuell eingetragene Pfade
   (`args: ["--mcp-server"]`, siehe `Docs/integration.md`) — genau dann
   arbeitet `get_violations` still mit Default-Regeln statt der
   Projekt-`rules.json`, ohne jeden Hinweis im Tool-Output. Für ein Projekt
   mit angepassten Regeln liefert `get_violations` dann durchgehend
   irreführende Ergebnisse, bis jemand zufällig `--config` im
   Host-Config-Eintrag nachträgt. Fix: ohne `--config` neben der
   aufgelösten Solution-Datei nach `rules.json` suchen; keine gefunden →
   `[WARN]` auf stderr **und** Vermerk in der `get_violations`-Antwort
   selbst ("Basis: Default-Regeln, keine `rules.json` gefunden").
2. **Neu angelegte/gelöschte `.cs`-Dateien sichtbar machen.**
   `RefreshStaleDocuments()`
   ([McpCodeGraphServer.cs:119](src/AiNetLinter/Mcp/McpCodeGraphServer.cs))
   iteriert ausschließlich über beim Solution-Load bekannte `Document`s —
   verifiziert: kein `Directory.GetFiles`/Verzeichnis-Sweep im Code. Eine
   während der Session neu angelegte Klasse ist bis zum Server-Neustart
   unsichtbar, eine gelöschte Datei bleibt als Treffer bestehen — der
   Server antwortet nicht mit einem Fehler, sondern mit einer plausiblen
   Falschaussage ("keine Treffer" für tatsächlich existierenden, gerade neu
   erstellten Code), und neue Dateien sind im Agenten-Dev-Loop der
   Normalfall, nicht die Ausnahme. Fix: zusätzlicher Verzeichnis-Sweep,
   der `.cs`-Dateien ohne zugehöriges `Document` einhängt und Dokumente
   ohne existierende Datei entfernt. Bewusste Grenze:
   `<Compile Remove=...>`-Ausschlüsse werden nicht erkannt.
3. **Generierte Last-Fixture als Skalierungsnachweis.** Kein Fixture-
   Generator für synthetische Solutions (500/5.000 Dateien) im
   Test-Bestand gefunden. Alle bisherigen Performance-/Skalierungsaussagen
   (Begründung für die folgenden Punkte 4/5) sind unbelegt gegen die eigene,
   kleine `AiNetLinter.slnx` (~3.600 Zeilen). Fix: Test-Hilfsmittel, das
   eine synthetische Solution definierter Größe generiert, plus ein
   Messlauf für Kaltstart-Zeit und Tool-Call-Dauer je Tool. **Bewusst vor**
   Punkt 4/5 eingeplant, damit deren Umsetzung gegen echte Zahlen erfolgt,
   nicht gegen die ursprüngliche Annahme (San.smart.Planner.Platform,
   ~160k LOC) ohne eigenen Beleg.
4. **Kaltstart entkoppeln.** `McpServerCommand.RunAsync`
   ([McpServerCommand.cs:35](src/AiNetLinter/Commands/McpServerCommand.cs))
   wartet `TryLoadSolutionAsync` synchron ab, **bevor** der stdio-Transport
   aufgesetzt wird — bei großen Solutions blockiert das den
   `initialize`-Handshake (Dauer siehe Last-Fixture-Messlauf, Punkt 3).
   MCP-Hosts mit Startup-Timeout werten das als fehlgeschlagenen Server.
   Fix: Transport zuerst aufsetzen, Solution-Load als Hintergrund-Task;
   `McpCodeGraphServer` bekommt einen dritten Zustand "lädt noch" (aktuell
   nur binär `IsLoaded`), betroffene Tools antworten in diesem Zustand mit
   einer strukturierten Kurzantwort statt zu blockieren.
5. **Staleness-Sweep über Verzeichnis-`mtime` kurzschließen.**
   `RefreshStaleDocuments()` prüft aktuell `File.GetLastWriteTimeUtc` **für
   jede einzelne Datei jedes Projekts bei jedem** `GetCurrentSolution()`-
   Aufruf (verifiziert, kein Directory-Level-Shortcut im Code) — Ausmaß
   siehe Last-Fixture-Messlauf (Punkt 3). Fix: Verzeichnis-`mtime` je
   Projektverzeichnis cachen, unveränderte Verzeichnisse überspringen.
   Kombinierbar mit Punkt 2 (derselbe Sweep-Mechanismus).
6. **stdout strukturell als reiner Protokollkanal.** Aktuell keine
   MCP-spezifische `ILintConsole`-Implementierung im Code (verifiziert,
   nur `LinterConsole`/`Output`-Implementierungen für den CLI-Pfad) — der
   Schutz gegen einen stdout-Leak aus wiederverwendeten CLI-Komponenten
   (z. B. ein `Console.WriteLine` in einer Kernklasse) ist aktuell reine
   Disziplin, nicht strukturell erzwungen. Ein einziger Leak zerstört das
   JSON-RPC-Framing der gesamten Session. Fix: eigene `ILintConsole` für
   den MCP-Modus, die `WriteLine` nach stderr umleitet, plus ein E2E-Test,
   der jede stdout-Zeile einer Tool-Call-Sequenz als gültigen JSON-RPC-Frame
   verifiziert.
7. **Opt-in Call-Log (`--mcp-log`).** Kein `--mcp-log`-Flag im Code, keine
   Log-Datei-Logik. Ohne dieses Log gibt es keine eigene Datengrundlage,
   welche der Tools tatsächlich genutzt werden — alle Priorisierungen
   bleiben Markt-Benchmark-Vermutungen statt eigener Beobachtung. Fix:
   schlankes Call-Log (Zeitstempel, Tool, gekürzte Parameter,
   Ergebniszeilen, Trunkierung ja/nein, Dauer, Leermenge ja/nein), Default
   aus, Ablage neben `cache/`.

**C. Struktureller Tech-Debt-Fix: `ILinterEngineConfig`-Interface
(TD-008/TD-010)**

- **Befund, verschärft gegenüber dem letzten Stand in der gelöschten
  `tech-debt.md`:** `McpCodeGraphServer.Config` ist vom Typ `Config`
  (konkrete Klasse, ~1110 Zeilen transitiver `Configuration`-Namespace),
  nicht von einem schlanken Interface. Jede Tool-Klasse, die
  `McpCodeGraphServer` referenziert, zieht diesen kompletten Namespace in
  ihren `AIContextFootprint` (Limit 2500) mit. Verifiziert in `rules.json`:
  **13 Dateien** haben inzwischen `PathOverride: MaxAIContextFootprint 2700`
  — praktisch der gesamte aktive Kern des MCP-Moduls (9 der 9 Tool-Klassen,
  3 der 3 Registrar-Klassen, `AuditCommand.cs`). Der Selbst-Lint von
  AiNetLinter — das zentrale Verkaufsargument des Tools — greift für sein
  eigenes, komplexestes, am aktivsten weiterentwickeltes Modul faktisch
  nicht mehr ohne Sonderregel.
- **Warum Muss-Haben:** Jede der sieben Erweiterungen aus B erweitert mit
  hoher Wahrscheinlichkeit entweder `McpCodeGraphServer` selbst oder eine
  Tool-Klasse, die darauf zugreift — jede davon treibt eine bereits am
  2700er-Limit hängende Klasse weiter Richtung Bruch. Ohne den
  strukturellen Fix ist der wahrscheinliche Verlauf: PathOverride Nummer
  14, 15, 16 statt Ursachenbehebung. Zusätzlich löst P2-1 (E) ein weiteres
  Tool im ohnehin knappen `SymbolGraphToolRegistrations` aus (TD-011).
- **Umsetzung (unverändert aus dem alten Tech-Debt-Log, geschätzt 4-6h):**
  `internal interface ILinterEngineConfig`, das nur die von
  `LinterEngine`/den Tools tatsächlich benötigten Properties exportiert,
  `McpCodeGraphServer.Config` wird vom Interface-Typ statt der konkreten
  `Config`-Klasse. Reduziert den transitiven Footprint auf die tatsächlich
  genutzte Property-Menge. Reduziert im Idealfall die 13
  `PathOverride`-Einträge auf die tatsächlich verbleibenden Fälle (mit
  Begründung pro verbleibendem Override, siehe DoD).
- **Einordnung im Ablauf:** direkt nach A (011-Abschluss), vor B — damit B
  gegen den bereits entlasteten Footprint umgesetzt wird statt gegen den
  ohnehin schon knappen Stand.

**D. Restliche offene Tech-Debt-Einträge** (aus der gelöschten
`tech-debt.md`, unverändert offen, hier vollständig — nicht nur als ID —
übernommen):

- **TD-001** (niedrig): `AiNetLinter.csproj` zieht über das
  `ModelContextProtocol`-Paket transitiv `Microsoft.Extensions.AI.Abstractions`
  mit, aktuell ungenutzt. Bei Bedarf prüfen, ob eine gezieltere
  Paket-Referenz existiert.
- **TD-002** (niedrig): `McpServerCommandTests.cs` — der einzige
  Subprozess-basierte E2E-Test startet pro Lauf einen vollständigen
  `AiNetLinter.exe`-Prozess inkl. MSBuild-Registrierung, spürbar langsamer
  als In-Process-Tests. Bei weiteren Subprozess-Tests einen
  wiederverwendbaren Fixture-Prozess/In-Memory-Transport erwägen.
- **TD-004** (mittel): Wiederkehrender `AIContextFootprint`-Druck auf die
  drei Tool-Registrierungs-Sammelklassen (`SymbolGraphToolRegistrations`,
  `FileStructureToolRegistrations`, `AnalysisToolRegistrations`) — ca.
  11-15 Zeilen Zuwachs pro registriertem Tool in der jeweiligen Klasse
  selbst. Bei jedem neuen Tool (z. B. P2-1 unten) Footprint aller drei
  Klassen vorab prüfen.
- **TD-005** (mittel): `McpCodeGraphServer` als Parametertyp einer
  Tool-`ExecuteAsync`-Signatur zieht dessen Footprint transitiv in die
  Tool-Klasse. Etabliertes Gegenmuster ("dünner Dispatch + separate
  Scanner-/Formatter-Datei ohne `McpCodeGraphServer`-Abhängigkeit") muss bei
  jedem neuen Tool von Anfang an angewendet werden, nicht reaktiv.
- **TD-006** (niedrig):
  [GetIndexScopeScanner.cs](src/AiNetLinter/Mcp/Tools/GetIndexScopeScanner.cs)
  dupliziert `SafeEnumerateFiles`/`IsGeneratedPath` aus
  [WebFileCatalog.cs](src/AiNetLinter/Web/WebFileCatalog.cs) 1:1 statt sie
  wiederzuverwenden. Bei einem weiteren Dateisystem-Scan mit ähnlichem
  Ausschlussmuster (z. B. B.3, Last-Fixture-Generierung) einmalig in eine
  gemeinsame Hilfsklasse ziehen.
- **TD-007** (niedrig): `McpCodeGraphServer.TryApplyContentChange` hat 5
  Parameter (`Document, string, DateTime, FileState, ref Solution`), über
  `MaxMethodParameterCount` = 4 (aktuell durch
  `MaxMethodParameterCountForNonPublic: 6` toleriert, da `private`). Bei der
  nächsten `McpCodeGraphServer`-Änderung (z. B. B.2/B.5, die dieselbe
  Methode ohnehin anfassen) in einen Input-`record` ziehen.
- **TD-011** (niedrig, wird durch E.1 scharf): `SymbolGraphToolRegistrations`
  hatte zuletzt (Stand vor Einheit 011) nur 6 Zeilen Puffer bis zum
  2500-Limit. Ein fünftes Symbolgraph-Tool — genau das, was E.1
  (`get_symbol_body`) unten ist — braucht mit hoher Wahrscheinlichkeit eine
  vierte Symbolgraph-Registrar-Klasse. Wird mit E.1 in derselben Einheit
  gelöst, nicht separat offen gelassen (siehe DoD).

**E. Symbolgraph-Erweiterungen aus `codegraph-mcp-next`** (vom Nutzer
bewusst als Muss-Haben statt "später" eingestuft):

1. **`get_symbol_body` + stabile Symbol-IDs.** Zusammenhängendes Paar, kein
   Einzelfeature: `get_file_skeleton` liefert pro Member zusätzlich eine
   stabile ID (überlebt Zeilenverschiebungen durch Agent-Edits,
   disambiguiert Overloads wie `ProcessOrder(int)` vs.
   `ProcessOrder(OrderDto)`); ein neues Tool `get_symbol_body` akzeptiert
   sowohl diese ID als auch das bestehende `Datei:Zeile:Spalte`-Format
   (`SymbolIdentifierResolver` erweitern, kein zweiter Auflösungsweg) und
   liefert gezielt den Member-Body statt der ganzen Datei. Größter
   verbleibender Token-Hebel: Agent holt Skelett (günstig), dann gezielt
   15 Zeilen Body statt einer 500-Zeilen-Datei. Ausgabe hart begrenzen
   (`maxResults`-Mechanik, ein Body kann eine 800-Zeilen-Methode sein).
   Basis: `DocumentationCommentId.CreateDeclarationId`/
   `GetFirstSymbolForDeclarationId` (Microsoft.CodeAnalysis). **Löst
   TD-011 in derselben Einheit** (fünfte Symbolgraph-Registrar-Klasse
   einplanen, nicht reaktiv).
2. **Blast-Radius als `depth`-Parameter statt neues Tool.** Optionaler
   Parameter an `find_references`/`get_impact`, Default `depth = 1`
   (unverändertes heutiges Verhalten), fest verdrahtete Obergrenze (z. B. 3)
   statt frei wählbar, zusätzliches Knotenlimit unabhängig von `maxResults`
   (transitive Suche kann exponentiell wachsen, bevor überhaupt formatiert
   wird). Ab `depth > 1` aggregiert ausgeben ("37 Aufrufer in 12 Dateien,
   davon 9 in 3 Projekten", dann Top-N), nicht flach — sonst nächster
   Token-Brand. Bewusst kein neues Tool: mehr ähnliche Tools erhöhen die
   Wahrscheinlichkeit, dass das LLM zum falschen greift.
3. **DI-Registrierung als Zusatzzeile in `get_type_hierarchy`.** Kein
   eigenes Tool — reine Textsuche nach `AddScoped<IFoo`/`AddSingleton<IFoo`/
   `AddTransient<IFoo` als zusätzliche Zeile in der bestehenden
   `get_type_hierarchy`-Antwort, klar als heuristischer Fund gekennzeichnet
   (Factory-Registrierungen/Convention-based-Scanning werden bewusst nicht
   erkannt).

### Nice-to-Have (optional, spätere Iteration)

Bewusst kurz gehalten — siehe Entscheidung oben, dass "optional" faktisch
"wird nicht gemacht" bedeutet. Nur zwei Punkte bleiben hier, weil sie
**von den Ergebnissen aus B abhängen** und vorher nicht sinnvoll geplant
werden können:

- Konfigurierbare Tool-Auswahl (z. B. `get_violations` bei Bedarf
  abschaltbar) — erst relevant, falls sich Performance bei sehr großen
  Solutions als reales Problem zeigt (siehe B.3, Last-Fixture-Messlauf).
- Persistenter Cache über Server-Neustarts hinweg (z. B. Skeleton-Daten auf
  Disk) — erst sinnvoll planbar, sobald B.3 (Last-Fixture) belastbare
  Kaltstart-Zahlen liefert.

### Non-Goals (unverändert aus dem Vorgänger-Konzept, bewusst NICHT Teil davon)

- **Keine Editier-Tools** (kein `insert_after_symbol` o. ä.) — Editieren
  bleibt Aufgabe der Agenten-App, zweiter Schreibpfad auf demselben
  Git-Working-Tree wäre ein Risiko ohne Mehrwert.
- **Kein Embedding-/Vektor-basiertes Semantic-Search.**
- **Kein Ersatz des bestehenden CLI-Batch-Modus** (`--map`, `--impact`,
  regulärer Lint-Lauf) — bleibt unverändert bestehen.
- **Kein generischer Multi-Sprachen-Support**, **kein Cross-Language-
  Symbolgraph** — Symbolgraph-Tools bleiben `.cs`-only (siehe Miss-Hint/
  Scope-Kommunikation, bereits umgesetzt).
- **Kein Plugin-/Erweiterungssystem, kein `AssemblyLoadContext`** —
  konform `AiNetLinterRichtlinien.mdc` §1/§2.
- **Kein zweites, generisches Codegraph-Tool** (`get_call_tree`,
  Duplicate-Symbol-Drift-Warnung, Dead-Code-Detection, PageRank/Symbol-
  Centrality, `agent_hint`-Feld, `mcp_config`-Rauschfilterung,
  No-New-Violations-Ratchet, `get_active_rules`-Tool) — alle einzeln im
  Vorgänger-Konzept geprüft und verworfen, Begründungen bleiben gültig
  (Marktabdeckung bereits vorhanden, Risiko einer Fehlinterpretation durch
  den Agenten, oder redundant zu bereits vorhandenen Mechanismen).
- **Keine neuen Features außerhalb von A/B/C/D/E oben.** Dieser Task ist
  Fertigstellung + Tech-Debt + die drei explizit vom Nutzer verbindlich
  gemachten Symbolgraph-Erweiterungen — keine offene Feature-Erweiterungsrunde.

## Zielplattformen / Technischer Rahmen

Unverändert: .NET 10, gleiches Executable (`AiNetLinter.csproj`), offizielles
`ModelContextProtocol`-NuGet-Paket (stdio-Transport), kein DI-Container,
Wiederverwendung von `SourceFileCatalog`/`SymbolFinder`/`SkeletonMapBuilder`/
`HotspotMapBuilder`/`RuleRegistry`/`LinterEngine`. Keine Änderung an diesen
Grundsatzentscheidungen nötig — der gesamte Restumfang (A-E) baut auf der
bestehenden Architektur auf.

## Verworfene Alternativen

Unverändert aus dem Vorgänger-Konzept (dort ausführlich begründet, hier nur
die Kurzfassung, damit die Frage nicht erneut aufkommt): eigenes
JSON-RPC-Protokoll statt MCP-SDK, `FileSystemWatcher`-basierte statt lazy
Invalidierung, Editier-Tools nach Serena-Vorbild, generischer LSP-zu-MCP-
Nachbau, Embedding-Suche jetzt schon, zweiter Disk-Cache-Namespace,
persistenter Cache als Muss-Haben, zusätzliche Thread-Safety über das
bestehende `Lock` hinaus, `.csproj`/`.sln`-Hash-Invalidierung statt
Verzeichnis-Sweep, Duplicate-Symbol-Drift-Warnung, Dead-Code-Detection,
PageRank/Symbol-Centrality, `get_call_tree` als eigenes Tool statt
`depth`-Parameter (siehe E.2).

## Wo im Projekt

- [src/AiNetLinter/Commands/McpServerCommand.cs](src/AiNetLinter/Commands/McpServerCommand.cs) —
  Einstiegspunkt, Auto-Discovery-Fix (B.1) und Kaltstart-Fix (B.4) setzen hier an.
- [src/AiNetLinter/Mcp/McpCodeGraphServer.cs](src/AiNetLinter/Mcp/McpCodeGraphServer.cs) —
  resident gehaltene Solution, Staleness-Logik; Ansatzpunkt für B.2, B.5,
  TD-007, sowie den dritten "lädt noch"-Zustand aus B.4.
- [src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs](src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs),
  [src/AiNetLinter/Mcp/McpServerOptionsBuilder.cs](src/AiNetLinter/Mcp/McpServerOptionsBuilder.cs),
  [src/AiNetLinter/Mcp/McpServerOptionsFactory.cs](src/AiNetLinter/Mcp/McpServerOptionsFactory.cs) —
  aus Einheit 011 (Muss-Haben A), Ansatzpunkt für `--mcp-log`-State (B.7).
- [src/AiNetLinter/Mcp/Tools/](src/AiNetLinter/Mcp/Tools) — alle 9 Tool-Klassen
  + Scanner/Formatter-Begleitdateien; Ansatzpunkt für E.1
  (`GetSymbolBodyTool` neu) und E.3 (`GetTypeHierarchyTool`-Erweiterung).
- [src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs](src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs) —
  TD-011, wird durch E.1 akut, dort mitzulösen.
- [src/AiNetLinter/Configuration/](src/AiNetLinter/Configuration) —
  `Config`-Klasse, Ziel für den TD-008/TD-010-`ILinterEngineConfig`-Refactor (C).
- [rules.json](rules.json) — `PathOverrides`-Sektion (13 Einträge, siehe C),
  Ziel für B.1 (Auto-Discovery-Suche relativ zur Solution-Datei).
- [src/AiNetLinter.Tests/Mcp/McpTestClient.cs](src/AiNetLinter.Tests/Mcp/McpTestClient.cs),
  `src/AiNetLinter.Tests/Fixtures/*McpFixture.cs` — Basis für B.6
  (stdout-Framing-E2E-Test) und B.3 (Last-Fixture-Generator, neuer Ordner
  `src/AiNetLinter.Tests/Fixtures/LastFixture*` o. ä.).
- [Docs/agent-api.md](Docs/agent-api.md), [Docs/integration.md](Docs/integration.md),
  [Docs/ROADMAP.md](Docs/ROADMAP.md) — laufend nachziehen (ROADMAP.md
  Zeilen 478-493 bereits korrekt als "Geplant" für B geführt, wird bei
  Abschluss auf "Umgesetzt" verschoben; E.1-E.3 sind dort noch gar nicht
  gelistet, müssen ergänzt werden).

## Entdeckte Mängel/Redundanzen

- **Lokaler Build aktuell rot (Datei-Sperre, nicht Code-Fehler).**
  - **Gefunden:** `dotnet build AiNetLinter.slnx` schlägt mit `MSB3027`/`MSB3021`
    fehl — `AiNetLinter.dll` ist durch laufende `AiNetLinter.exe`- und
    `testhost.exe`-Prozesse gesperrt (mind. 1 `AiNetLinter.exe`, PID 35664,
    und 1 `testhost.exe`, PID 35908, zum Analysezeitpunkt noch aktiv;
    ursprünglich waren beim ersten Build-Versuch bis zu 8 `AiNetLinter.exe`-
    Prozesse gleichzeitig gesperrt-haltend).
  - **Bezug:** Deckt sich zeitlich mit dem in `state.md` dokumentierten
    User-Abbruch eines langlaufenden Coder-Aufrufs für Einheit 011
    ("Coder-Aufruf lief sehr lange... vermutlich vom User gecancelt").
  - **Vorschlag:** Vor jedem Build/Test in Muss-Haben A erst offene
    `AiNetLinter.exe`/`testhost.exe`-Prozesse prüfen und bei Bedarf beenden
    (jetzt Teil von A, siehe oben). Falls das öfter vorkommt: prüfen, ob
    `McpTestClient`/die `IAsyncDisposable`-Kette den Kind-Prozess bei
    Testabbruch/Cancellation zuverlässig beendet (kein bestätigter
    Code-Bug, nur eine wiederkehrende Beobachtung über zwei Sessions —
    bei erneutem Auftreten während dieses Tasks als eigenen Tech-Debt-
    Eintrag aufnehmen).
  - **Entscheidung:** übernommen ins Scope (→ Muss-Haben A, erster Schritt).
- **`rules.json`-PathOverride-Liste ist auf dem Weg, zur Regel statt zur
  Ausnahme zu werden.**
  - **Gefunden:** 13 von aktuell 24 Dateien im `Mcp`-Namespace (plus
    `AuditCommand.cs`) tragen `MaxAIContextFootprint: 2700` statt des
    projektweiten Defaults 2500 — mehr als die Hälfte des aktiven
    MCP-Moduls.
  - **Bezug:** `AiNetLinter.mdc` Zeile 15/28 (`AIContextFootprint` ≤ 2500,
    "Kopplung reduzieren; eigene Typen-Abhängigkeiten minimieren").
  - **Vorschlag:** siehe Muss-Haben C (`ILinterEngineConfig`).
  - **Entscheidung:** übernommen ins Scope (→ Muss-Haben C).
- **`get_index_scope`/`WebFileCatalog`-Duplikation** — bereits als TD-006
  erfasst (siehe Muss-Haben D), hier nur der Vollständigkeit halber
  verlinkt, kein zusätzlicher Fund.
- **Konzeptionelle Lücke zwischen Anspruch und Test-Nachweis bei B.4/B.5.**
  - **Gefunden:** Die Begründung für Kaltstart-Entkopplung (30-60s Blockade
    bei 160k LOC) und Staleness-`mtime`-Kurzschluss (spürbar bei
    vier-/fünfstelliger Dateizahl) stammt beides aus der ursprünglichen
    Projektmotivation (San.smart.Planner.Platform, ~160k LOC), nicht aus
    einer eigenen Messung gegen die tatsächlich vorhandene, kleine
    `AiNetLinter.slnx` (~3.600 Zeilen).
  - **Bezug:** kein Regelverstoß, sondern eine Beweislücke.
  - **Vorschlag:** B.3 (Last-Fixture) vor B.4/B.5 einplanen (bereits so in
    der Reihenfolge oben umgesetzt).
  - **Entscheidung:** übernommen ins Scope (→ Reihenfolge in Muss-Haben B).

## Wie (grober Ansatz)

Kein neuer Architektur-Ansatz nötig — reine Fortsetzung der bestehenden
Struktur (Resident-Server, lazy Invalidierung, Tool-Klasse +
Scanner/Formatter-Begleitdatei + Registrar-Eintrag pro Tool). Der Planer im
nachfolgenden Loop leitet aus diesem Konzept Einheiten ab; verbindliche
Reihenfolge (Entscheidung des Nutzers):

1. **Einheit "011-Abschluss"** (Muss-Haben A): Prozess-Bereinigung, Volllauf
   nachfahren, Kritiker-Review für die 6 bestehenden Commits, Push.
2. **Einheit "TD-008/010-Refactor"** (Muss-Haben C): `ILinterEngineConfig`,
   reduziert alle 13 PathOverrides auf ihren tatsächlichen Bedarf.
3. **Einheiten B.1 → B.2 → B.3 → B.4 → B.5 → B.6 → B.7** (Betriebsrisiko
   zuerst, siehe Muss-Haben B für die vollständige Begründung der
   Reihenfolge).
4. **Einheiten E.1 → E.2 → E.3** (Symbolgraph-Erweiterungen aus
   `codegraph-mcp-next`) zuletzt, da sie von einem bereits entlasteten
   Footprint (Schritt 2) profitieren und E.1 TD-011 mitlöst.

## Definition of Done / Erfolgskriterien

- Einheit 011 hat ein Kritiker-`approved`-Review, Volllauf frisch
  nachgefahren (nicht nur Coder-Bericht übernommen), 11 lokale Commits sind
  gepusht.
- `ILinterEngineConfig`-Refactor (C) umgesetzt: `rules.json`
  `PathOverride`-Liste ist auf die Fälle reduziert, die der strukturelle
  Fix nicht lösen kann (falls vorhanden, mit Begründung pro verbleibendem
  Override dokumentiert).
- Alle sieben Punkte aus Muss-Haben B sind umgesetzt, reviewt, mit
  Integrationstest abgesichert (analog dem bestehenden Testmuster:
  `Category=Unit` für Logik, `Category=Integration` für
  Subprozess-/E2E-Verhalten).
- Alle drei Punkte aus Muss-Haben E sind umgesetzt: `get_symbol_body` +
  stabile IDs in `get_file_skeleton`, `depth`-Parameter an
  `find_references`/`get_impact`, DI-Hinweis in `get_type_hierarchy` — je
  mit Integrationstest. TD-011 ist dabei mitgelöst (fünfte
  Symbolgraph-Registrar-Klasse, falls nötig), nicht separat offen.
- `Docs/ROADMAP.md` Zeilen 478-493 sind von "Geplant" auf den tatsächlichen
  Stand aktualisiert, E.1-E.3 sind neu ergänzt (kein Dokument, das Soll und
  Ist vermischt).
- Alle in D gelisteten Tech-Debt-Einträge (TD-001, TD-002, TD-004, TD-005,
  TD-006, TD-007) sind entweder geschlossen oder bewusst mit Begründung
  erneut zurückgestellt (kein stillschweigendes Verschwinden beim Löschen
  von `codegraph-mcp-server`).
- `dotnet build`/`dotnet test AiNetLinter.slnx --no-build` grün, 0
  Warnungen, keine durch diesen Task verursachte Regression im CLI-Batch-
  Modus (Regressionstest wie in EPIC-07 bereits etabliert).
- `tasks/codegraph-mcp-server/` und `tasks/codegraph-mcp-next/` sind
  gelöscht, ohne dass eine Nachfrage "wo stand das nochmal" nötig wird —
  dieses Dokument ist an der Stelle vollständig.

## Offene Punkte

Keine blockierenden offenen Punkte — alle vier Grundsatzentscheidungen
(Umgang mit Einheit 011, Reihenfolge von B, TD-008/010 als Muss-Haben,
E als Muss-Haben statt Nice-to-Have) sind vom Nutzer getroffen und oben
eingearbeitet.
