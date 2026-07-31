---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: large
rules_dir: .agents/rules
last_updated: 2026-07-31  # aktualisiert: Dogfooding statt externem Praxistest (siehe "Entdeckte Mängel/Redundanzen")
open_questions: []
---

# Konzept: AiNetLinter als stdio-MCP-Codegraph-Server

## Ziel (Was)

AiNetLinter bekommt einen zusätzlichen Ausführungsmodus: einen stdio-basierten
MCP-Server, der die bereits vorhandene Roslyn-Solution-Analyse (Impact-Analyse,
Skeleton-/Hotspot-/Kopplungs-Maps, Lint-Regeln) nicht mehr nur als einmaligen
CLI-Batch-Report ausliefert, sondern als granular abfragbare MCP-Tools für
AI-Coding-Agenten in großen .NET-Codebasen (100k+ LOC). Ziel: weniger
grep/rg-Explorationsrunden und weniger verbrannte Tokens pro Agenten-Task,
ohne den bestehenden CLI-Modus zu ersetzen.

## Warum / Kontext

- AiNetLinter läuft produktiv gegen sehr große Bestandscodebasen (Beispiel:
  `San.smart.Planner.Platform`, ~160k LOC).
- Agenten-Loops (z. B. `.cursor/Agent-Scaffolding/dev-loop/drift-loop` im
  Zielprojekt) nutzen zur Code-Exploration aktuell `rg`/`grep` — textbasiert,
  mit False Positives (Treffer in Strings/Kommentaren/gleichnamigen Symbolen
  anderswo), die der Agent erst durch zusätzliche Lese-Runden disambiguieren
  muss. Jede dieser Runden kostet Kontext/Tokens.
- Recherche bestätigt die Ausgangs-Hypothese, statt sie nur zu unterstellen:
  eine 2026-07-Re-Validierung über sieben Benchmark-Repos misst **~60 %
  geringere Kosten, ~69 % weniger Tokens** bei Codegraph-Indexing vs.
  grep-basierten Agenten-Loops (siehe Recherche-Quelle unten).
- AiNetLinter hat die fachliche Grundlage dafür bereits an Bord: ein volles
  Roslyn-Solution-Modell (`SourceFileCatalog`), semantische Impact-Analyse
  (`DiffImpactAnalyzer` via `SymbolFinder`), sowie Skeleton-/Hotspot-/
  Kopplungs-Maps — aktuell aber ausschließlich als CLI-Batch-Dump: ein
  Prozess pro Aufruf, der die komplette `MSBuildWorkspace` jedes Mal neu lädt.
- Markt-Check (bewusst durchgeführt, um Doppelarbeit zu vermeiden): generische
  LSP-zu-MCP-Bridges existieren bereits breit und etabliert — Serena
  (~24.000★, wrappt Language-Server generisch inkl. C#), CodeGraph
  (codegraph-ai, 42+ MCP-Tools, 38 Sprachen), codegraph (colbymchenry,
  auto-sync), agent-lsp. Ein Nachbau eines generischen Codegraphen wäre reine
  Doppelarbeit ohne Differenzierung. Der Wert von AiNetLinter liegt in der
  bereits vorhandenen, .NET-spezifischen Tiefenanalyse (Impact, Hotspots,
  Kopplung, eigene Lint-Regeln) — granular abfragbar statt als Volltext-Dump.
  Das ist die Nische, die die generischen Tools nicht abdecken.

**Quelle Token-Zahlen:** Anthony West, "Code Intelligence & Code-Graph
Indexing for AI Agents" (2026), https://anthonywest.co.uk/research/code-intelligence-indexing-2026-openai

## Scope

### Muss-Haben

- Neuer Ausführungsmodus **`--mcp-server`** (Name bestätigt), der einen
  stdio-MCP-Server startet statt eines Batch-Laufs.
- **Solution-Auswahl beim Start:** `ainetlinter --mcp-server --path <Datei-
  oder-Verzeichnis>` — gleiche `--path`-Semantik wie bei allen anderen
  Commands (Datei direkt, oder Verzeichnis mit Auto-Suche nach `.sln`/
  `.slnx`, bestehende Logik aus `SourceFileCatalog.FindSolutionFile`).
  Fehlt `--path`, wird das aktuelle Arbeitsverzeichnis verwendet — das ist
  der Normalfall für die Registrierung als MCP-Server: der Host (z. B.
  Claude Code) startet den Prozess mit `cwd` = Projekt-Root, ohne dass der
  Nutzer den Pfad manuell in der Server-Config eintragen muss (analog zur
  Registrierung anderer stdio-MCP-Server pro Projekt). **Verschärfung
  gegenüber der bestehenden CLI-Logik:** findet die Verzeichnissuche mehr
  als eine `.sln`/`.slnx`-Datei, bricht der Server-Start mit einer klaren
  Fehlermeldung ab (Kandidaten benannt) statt wie bisher `files[0]`
  stillschweigend zu wählen — bei einem Batch-Lauf fällt eine falsch
  gewählte Solution sofort auf, bei einem resident laufenden Server würde
  sie sonst die komplette Session unbemerkt falsch beantworten.
- Server lädt die Solution **einmal** beim Start (`SourceFileCatalog.LoadAsync`)
  und hält sie resident im Speicher für die gesamte Session — kein Neuladen
  der `MSBuildWorkspace` pro Tool-Call.
- **Lazy Staleness-Invalidierung:** vor jeder Tool-Antwort wird für die
  betroffene(n) Datei(en) Hash/mtime gegen einen Cache geprüft; bei
  Abweichung inkrementelles Update des betroffenen `Document` in der
  `Solution` (kein Komplett-Reload).
- **Fehlerbehandlung ohne Absturz:**
  - Solution lädt gar nicht → jeder Tool-Call liefert eine strukturierte
    Fehlerantwort (gleiches Format wie bestehendes `[ERROR]`-Schema aus
    `Docs/agent-api.md`), Server bleibt am Leben.
  - Solution lädt, aber einzelne Dateien/Projekte haben Compile-Fehler →
    Tools liefern für nicht betroffene Bereiche weiterhin korrekte Antworten,
    für betroffene Bereiche einen Warnhinweis (Roslyns bestehende Toleranz
    gegenüber fehlerhaftem Code wird genutzt, nicht neu gebaut).
- **Explizite Scope-Kommunikation (C#-only für den Symbolgraph):** Die
  Roslyn-basierten Tools (`find_symbol`, `find_references`, `get_impact`,
  `get_type_hierarchy`, `get_file_skeleton`) decken ausschließlich `.cs`
  ab. Das wird nicht stillschweigend vorausgesetzt, sondern aktiv
  kommuniziert: jede Tool-`description` benennt die Grenze explizit
  ("nur C#/.cs, kein JavaScript/Razor-Markup/WPF-XAML/HTML/CSS"), zusätzlich
  trägt die `initialize`-Antwort des Servers (`instructions`-Feld, vom
  `ModelContextProtocol`-SDK unterstützt) denselben Hinweis einmal zentral.
  Begründung: ohne das würde ein Agent bei gemischtem Code (JS, Blazor,
  WPF — reale Projektzusammensetzung) nach einer JS-Funktion suchen,
  „nicht gefunden" für „existiert nicht" statt für „falsches Tool" halten
  und unnötig viele Anfragen verbrauchen — genau der Fehlerfall, den der
  Server eigentlich vermeiden soll.
- **Miss-Hint statt stiller Leermenge:** Findet `find_symbol` (typischer
  Einstiegspunkt „wo ist X") keinen C#-Treffer, macht der Server
  zusätzlich einen einfachen Text-Fallback (`search_pattern`-Mechanik) über
  die vom Graph **nicht** abgedeckten Dateitypen (`.js`, `.razor`,
  `.cshtml`, `.xaml`, `.html`, `.css`) im Solution-Verzeichnis. Gibt es dort
  einen Treffer, meldet der Server explizit „kein C#-Symbol, aber
  Texttreffer in `<Datei>` (nicht Teil des Graphs)" statt einer bloßen
  Leermeldung.
- **`get_violations` umgeht den bestehenden Disk-Cache
  (`AnalysisCacheManager`) und rechnet direkt gegen die resident gehaltene
  `Compilation`.** Begründung: der Disk-Cache existiert, um Re-Compilation
  zwischen unabhängigen CLI-Prozessstarts zu vermeiden — ein resident
  laufender Server hat dieses Problem nicht, er kompiliert nie neu. Der
  eigentliche Grund, warum das hier explizit festgehalten wird: mehrere
  gleichzeitig laufende Prozesse gegen dieselbe Solution (siehe "Wie" /
  Cache-Isolation) würden sich sonst dieselbe Cache-Datei teilen, ohne
  dass `AnalysisCacheManager` dafür eine prozessübergreifende Sperre hat.
- Tool-Set wie unten unter "Wie" beschrieben (9 Tools).
- Thread-sicherer Zugriff auf die gehaltene `Solution`/`Compilation` —
  nicht weil der drift-loop selbst parallelisiert (er ist laut Spec strikt
  seriell), sondern weil der Server auch außerhalb dieses einen Workflows
  genutzt werden können soll.
- Dokumentation: `Docs/agent-api.md` (neuer Abschnitt MCP-Modus),
  `Docs/integration.md` (Setup/Registrierung als MCP-Server),
  `Docs/ROADMAP.md`, `README.md`.
- Tests: Unit-Tests für die Staleness-Invalidierung, Integrationstests je
  Tool gegen eine Test-Solution (analog bestehender CLI-Integrationstests).
- **Dogfooding pro Tool-Step gegen die eigene `AiNetLinter.slnx`:** Jeder
  Step, der eines der 9 Tools neu einführt oder in seiner Kernlogik
  wesentlich ändert, verifiziert es zusätzlich zu den automatisierten
  Fixture-Tests **einmal ad-hoc gegen die reale AiNetLinter-Solution
  selbst** (Coder startet den gebauten Server wie im bestehenden
  E2E-Testmuster, aber mit `--path` auf das Repo-Root statt einer
  Mini-Fixture, und ruft das Tool mit einer echten Abfrage auf — z. B.
  `find_symbol` nach einem tatsächlich existierenden Klassennamen). Kein
  zusätzlicher Step/Task dafür nötig — die Prüfung ist Teil des ohnehin
  laufenden Tool-Steps, dokumentiert unter einem eigenen Abschnitt
  „Dogfooding" in `step-result.md` (Aufruf, Kurzergebnis, Auffälligkeiten).
  Ersetzt keine automatisierten Tests, ergänzt sie um einen Realismus-Check
  gegen echten, gewachsenen Code, den Mini-Fixtures strukturell nicht
  leisten können (echte Namenskollisionen, echte Kommentare/Strings als
  potenzielle False Positives, echte Dateigrößen).

### Nice-to-Have (optional, spätere Iteration)

- Konfigurierbare Tool-Auswahl (z. B. `get_violations` bei Bedarf abschaltbar,
  falls sich das bei sehr großen Solutions als Performance-Faktor erweist).
- Persistenter Cache über Server-Neustarts hinweg (z. B. Skeleton-Daten auf
  Disk), um die Kaltstart-Zeit bei 160k LOC zu verkürzen — erst relevant,
  sobald die tatsächliche Kaltstart-Zeit gemessen ist (siehe offene Punkte).

### Non-Goals (bewusst NICHT Teil davon)

- **Keine Editier-Tools** (kein `insert_after_symbol`, `replace_symbol_body`
  o. ä., wie Serena sie hat). Editieren bleibt Aufgabe der Agenten-App
  (Claude Code etc.) — ein zweiter Schreibpfad auf demselben Git-Working-Tree
  wäre ein Konflikt-/Risiko-Faktor ohne Mehrwert für AiNetLinters eigentliche
  Stärke (Analyse, nicht Code-Transformation).
- **Kein Embedding-/Vektor-basiertes Semantic-Search** in dieser Iteration —
  eigenes Subsystem (Embedding-Modell, Vektor-Store), kein Teil des
  bestehenden Roslyn-Kerns. Bei belegtem Bedarf spätere Iteration.
- **Kein Ersatz des bestehenden CLI-Batch-Modus** (`--map`, `--impact`,
  regulärer Lint-Lauf). Der wird aktiv von anderen Projekten/Pipelines
  genutzt und bleibt unverändert bestehen — dieser MCP-Modus ist eine
  Ergänzung, kein Umbau.
- **Kein generischer Multi-Sprachen-Support.** Bleibt .NET/Roslyn-spezifisch,
  wie der Rest von AiNetLinter.
- **Kein Cross-Language-Symbolgraph.** Der Symbolgraph (`find_symbol`,
  `find_references`, `get_impact`, `get_type_hierarchy`, `get_file_skeleton`)
  bleibt auf `.cs` beschränkt — kein Verknüpfen von C#-Methoden mit
  JS-Aufrufen aus Blazor-Interop, kein XAML-Bindings-Graph, keine
  Razor-Markup-Struktur. Bewusste Grenze, kein Versehen — siehe Muss-Haben
  "Explizite Scope-Kommunikation" und "Miss-Hint" dafür, dass diese Grenze
  dem Agenten nicht erst durch Trial-and-Error auffällt.
- **Kein Plugin-/Erweiterungssystem, kein `AssemblyLoadContext`** für den
  MCP-Modus — läuft als zusätzlicher Modus im selben monolithischen
  Executable (siehe "Verworfene Alternativen" zur Begründung gegenüber
  `AiNetLinterRichtlinien.mdc` §1/§2).

## Zielplattformen / Technischer Rahmen

- .NET 10, gleiches Executable (`AiNetLinter.csproj`) — kein separates
  Projekt, keine separate Assembly. Neuer Modus als weiteres CLI-Flag,
  strukturell wie bestehende Commands im `Commands/`-Ordner.
- **MCP-Protokoll:** offizielles `ModelContextProtocol`-NuGet-Paket (C#-SDK)
  für stdio-Transport, JSON-RPC-Framing und Capability-Handshake — statt
  eigenem Protokoll-Handrolling. Begründung: Protokoll-Details (Framing,
  Handshake) sind Boilerplate ohne fachlichen Mehrwert; das SDK abzubilden
  ist konsistent mit der Projekt-Maxime "Einfachheit vor Abstraktion nur wo
  echter Mehrwert entsteht" (`AiNetLinterRichtlinien.mdc` §1).
- **Kein DI-Container** (konform `AiNetLinterRichtlinien.mdc` §2) —
  Server-Zustand (gehaltene Solution, Staleness-Cache) über eine einzelne
  zustandshaltende Klasse, direkt instanziiert im Hostprozess, wie der Rest
  des Codes bereits statische Klassen/direkte Instanziierung nutzt
  (`MapCommand`, `ImpactCommand` als Vorbild).
- **Wiederverwendung statt Neubau:** `SourceFileCatalog` (Solution laden und
  aktualisieren — `WithUpdatedSolution` existiert bereits für In-Memory-
  Updates nach Auto-Fix, direkt wiederverwendbar für Staleness-Invalidierung),
  die `SymbolFinder`-Nutzung aus `DiffImpactAnalyzer`, `SkeletonMapBuilder`/
  `HotspotMapBuilder`, `RuleRegistry`/`LinterEngine`.

## Verworfene Alternativen

- **Separates Tool/Projekt statt Modus im selben Executable:** verworfen —
  widerspricht `AiNetLinterRichtlinien.mdc` §1 ("Monolithisch & schlank
  bleiben", "Kein Plugin-System"). Ein zusätzlicher CLI-Modus im selben
  Executable erreicht dasselbe fachliche Ziel, ohne mit dieser bestehenden
  Architektur-Leitplanke zu brechen.
- **Eigenes JSON-RPC/stdio-Protokoll statt offiziellem MCP-SDK:** verworfen
  — reine Boilerplate ohne Mehrwert, unnötiges Fehlerrisiko bei
  Protokoll-Details.
- **FileSystemWatcher-basierte Invalidierung statt lazy Hash-Check:**
  verworfen für diese erste Iteration — Event-Reihenfolge/Race-Conditions
  zwischen Watcher-Callback und einer laufenden Tool-Antwort sind ein
  zusätzliches Fehlerrisiko. Lazy Check zum Query-Zeitpunkt ist einfacher
  korrekt zu bekommen, kostet nur beim ersten Query nach einer Änderung
  minimal mehr Zeit.
- **Editier-Tools nach Serena-Vorbild:** verworfen, siehe Non-Goals — zwei
  konkurrierende Schreibpfade auf demselben Git-Working-Tree sind ein
  Risiko ohne Mehrwert für AiNetLinters Kernkompetenz.
- **Generischer Nachbau eines LSP-zu-MCP-Codegraphen (à la Serena/CodeGraph):**
  verworfen — Markt ist bereits gut bedient, kein Differenzierungsvorteil.
  AiNetLinters Wert liegt in den bereits vorhandenen linter-eigenen Analysen.
- **Embedding-basierte semantische Suche gleich mit einführen:** verworfen
  für diese Iteration — eigenes Subsystem, das den Scope erheblich
  vergrößert, ohne dass der Bedarf dafür schon belegt ist (siehe Non-Goals).
- **Eigener zweiter Disk-Cache-Namespace für den MCP-Modus** (z. B. eigenes
  Präfix, damit CLI und MCP-Server sich nicht in die Quere kommen, aber
  beide trotzdem einen Disk-Cache nutzen): verworfen zugunsten von "MCP-Modus
  nutzt gar keinen Disk-Cache" — der resident gehaltene Compilation-Zustand
  erfüllt den Zweck des Disk-Caches (Vermeidung von Re-Compile zwischen
  Prozessstarts) bereits vollständig; ein zweiter Namespace wäre zusätzliche
  Komplexität ohne zusätzlichen Nutzen.
- **Persistenter Cache/Kaltstart-Optimierung als Muss-Haben:** verworfen für
  diese Iteration — Kaltstart-Zeit bei 160k LOC ist ein Fixkosten-Faktor
  (MSBuild/Roslyn-Ladezeit), der sich ohnehin nur einmal pro Server-Session
  amortisiert und nutzerseitig als hinnehmbar bestätigt wurde. Bleibt
  Nice-to-Have, falls sich das in der Praxis anders zeigt.

## Wo im Projekt

- [Program.cs](src/AiNetLinter/Program.cs) — Einstiegspunkt, Dispatch auf den
  neuen Modus.
- [Cli/CliOptions.cs](src/AiNetLinter/Cli/CliOptions.cs),
  [Cli/CliOptionFactory.cs](src/AiNetLinter/Cli/CliOptionFactory.cs),
  [Cli/LinterArgs.cs](src/AiNetLinter/Cli/LinterArgs.cs) — neue Option für
  den MCP-Modus, analog zu bestehenden Flags wie `--map`.
- [Commands/](src/AiNetLinter/Commands) — neuer Command als Einstieg in den
  Server-Modus, analog `MapCommand.cs`/`ImpactCommand.cs`.
- [Baseline/SourceFileCatalog.cs](src/AiNetLinter/Baseline/SourceFileCatalog.cs)
  — Solution laden/aktualisieren; Basis für Resident-Betrieb.
  `WithUpdatedSolution` existiert bereits für In-Memory-Updates (aktuell für
  Auto-Fix genutzt) — direkt wiederverwendbar für die Staleness-Invalidierung,
  kein Neubau nötig.
- [Core/DiffImpactAnalyzer.cs](src/AiNetLinter/Core/DiffImpactAnalyzer.cs) —
  bestehende `SymbolFinder`-Nutzung (`FindReferencesAsync`), Vorbild/Basis
  für die Tools `find_references`/`get_impact`.
- [Maps/Skeleton/SkeletonMapBuilder.cs](src/AiNetLinter/Maps/Skeleton/SkeletonMapBuilder.cs),
  [Maps/HotspotMapBuilder.cs](src/AiNetLinter/Maps/HotspotMapBuilder.cs) —
  Basis für `get_file_skeleton`/`get_hotspots`, granularer Ausschnitt statt
  Whole-Repo-Dump.
- [Core/RuleRegistry.cs](src/AiNetLinter/Core/RuleRegistry.cs),
  [Core/LinterEngine.cs](src/AiNetLinter/Core/LinterEngine.cs) — Basis für
  `get_violations`.
- [Web/WebFileCatalog.cs](src/AiNetLinter/Web/WebFileCatalog.cs) — enumeriert
  bereits heute JS-/CSS-/Razor-Dateien für die Web-Checker; Basis für
  `get_index_scope`, kein neuer Datei-Scan nötig.
- [Core/Checkers/WpfSeparationChecker.cs](src/AiNetLinter/Core/Checkers/WpfSeparationChecker.cs)
  — zeigt die bestehende Grenze auf: WPF/XAML wird nur indirekt über
  C#-Code-Behind geprüft, nie die `.xaml`-Datei selbst — Beleg für den
  oben dokumentierten Non-Goal "kein Cross-Language-Symbolgraph".
- [Docs/agent-api.md](Docs/agent-api.md), [Docs/integration.md](Docs/integration.md)
  — Doku-Ergänzung.
- [AiNetLinter.csproj](src/AiNetLinter/AiNetLinter.csproj) — neue
  `PackageReference` für `ModelContextProtocol`.

## Entdeckte Mängel/Redundanzen

- **Kein Neubau der Symbol-/Solution-Logik nötig**
  - **Gefunden:** `SourceFileCatalog.WithUpdatedSolution` (Zeile 66-69) und
    die `SymbolFinder`-Nutzung in `DiffImpactAnalyzer` (`FindCallSitesAsync`,
    Zeile 281-302) decken bereits genau die Mechanik ab, die ein resident
    laufender MCP-Server für Staleness-Updates bzw. Referenz-Suche braucht.
  - **Bezug:** kein Regelverstoß, sondern Wiederverwendungs-Chance —
    passend zu `AiNetLinterRichtlinien.mdc` §1 ("Einfachheit vor Abstraktion").
  - **Vorschlag:** beide Bausteine direkt wiederverwenden statt eine
    parallele zweite Roslyn-Zugriffsschicht für den MCP-Modus zu bauen.
  - **Entscheidung:** übernommen ins Scope (siehe "Zielplattformen" und
    "Wo im Projekt" oben).
- **Architektur-Spannung "Monolithisch bleiben" vs. neuer Server-Modus**
  - **Gefunden:** `AiNetLinterRichtlinien.mdc` §1/§2 verbietet Plugin-Systeme,
    `AssemblyLoadContext` und DI-Container.
  - **Bezug:** ein MCP-Server-Modus *könnte* danach klingen, würde die Regel
    aber nicht verletzen, solange er als weiterer Modus im selben
    Executable ohne dynamisches Laden/DI-Container umgesetzt wird.
  - **Vorschlag:** explizit als Randbedingung festhalten (siehe "Zielplattformen"
    und "Verworfene Alternativen"), damit der Planer im drift-loop nicht
    versehentlich ein Plugin-System oder einen DI-Container einführt.
  - **Entscheidung:** übernommen — als Rahmenbedingung in Scope/Zielplattformen
    festgehalten, kein Non-Goal nötig, da kein tatsächlicher Konflikt bei
    korrekter Umsetzung.
- **Vorbestehende Cache-Race zwischen zwei parallelen CLI-Lint-Läufen**
  - **Gefunden:** bei der Prüfung der Nutzer-Frage zu Multi-Instanz-Cache
    (`Cache/AnalysisCacheManager.cs`, Zeile 82-88 `SaveIfDirty`): zwei
    gleichzeitige `ainetlinter`-Lint-Läufe gegen dieselbe Solution mit
    denselben `rules.json` teilen sich dieselbe Cache-Datei
    (`{solutionName}-{hash8}-{buildTimestamp}.json`); es gibt keine
    prozessübergreifende Datei-Sperre, `SaveIfDirty()` überschreibt komplett
    — letzter Schreiber gewinnt, der andere Prozess verliert seine in dieser
    Session gesammelten Cache-Einträge stillschweigend.
  - **Bezug:** kein `rules_dir`-Regelverstoß, sondern ein bereits heute
    bestehendes Verhalten, unabhängig vom MCP-Server. Keine falschen
    Lint-Ergebnisse dadurch (Cache-Treffer bleiben checksum-validiert), nur
    verminderte Cache-Wirksamkeit bei paralleler Nutzung.
  - **Vorschlag:** eigenes, von diesem Task unabhängiges Ticket/Task, falls
    parallele CLI-Läufe auf derselben Solution in der Praxis tatsächlich
    vorkommen (aktuell nicht belegt).
  - **Entscheidung:** bewusst **nicht** in diesen Task übernommen — der
    MCP-Server selbst umgeht das Problem für sich (siehe Muss-Haben/"Wie"),
    die CLI-interne Race bleibt unangetastetes Bestandsverhalten außerhalb
    des hier definierten Scopes.
- **`get_index_scope` braucht keinen neuen Datei-Scan**
  - **Gefunden:** `WebFileCatalog.Collect` (siehe "Wo im Projekt") liefert
    bereits die Dateiliste, aus der die Web-Checker (JS/CSS/Razor) ihre
    eigene Abdeckung ableiten — exakt die Grundlage, die `get_index_scope`
    für die Dateityp-Aufschlüsselung braucht.
  - **Bezug:** kein Regelverstoß, Wiederverwendungs-Chance analog zum
    ersten Fund oben.
  - **Vorschlag:** `get_index_scope` direkt auf `SourceFileCatalog.GetSourceFiles`
    + `WebFileCatalog.Collect` aufbauen statt eigener Dateisystem-Traversierung.
  - **Entscheidung:** übernommen ins Scope (siehe Tool-Tabelle unter "Wie").
- **Iteratives Agenten-Dogfooding statt einmaligem externen Praxistest**
  - **Gefunden:** Der ursprüngliche DoD-Punkt sah einen einmaligen
    "manuellen Praxistest" gegen `San.smart.Planner.Platform` (~160k LOC)
    am Task-Ende vor. Nutzer-Nachfrage (Chat, 2026-07-31) + eigene Prüfung
    ergaben zwei Probleme damit: (1) "manuell" bedeutete faktisch, dass kein
    Subagent dieses Kriterium selbst verifizieren konnte — es hätte immer
    auf einen Nutzer-Bericht ganz am Ende gewartet, statt Probleme früh zu
    finden. (2) Die tatsächlich vorhandene Solution unter
    `C:\Daten\Entwicklung\SAN\San.smart.Planner.Platform\San.smart.Planner.Platform.slnx`
    hat in diesem Checkout nur ~3.600 Zeilen C# (nicht ~160k) — der
    ursprünglich erhoffte Skalierungsnachweis (Kaltstart/Verhalten bei
    einer sehr großen Solution) wäre mit dieser konkreten Solution ohnehin
    nicht einlösbar gewesen.
  - **Bezug:** kein Regelverstoß, sondern eine Verbesserung des
    Verifikationsmechanismus selbst — passend zum Nutzer-Wunsch, Coder-
    /Kritiker-Agenten das direkt selbst nachprüfen zu lassen, statt auf
    einen externen, agentenseitig unzugänglichen Nachweis zu warten, und
    ohne dafür zusätzliche Mini-Steps/Tasks einzuführen.
  - **Vorschlag:** externe Solution als Testziel komplett streichen.
    Stattdessen: jeder Tool-Step dogfoodet das jeweils gebaute Tool
    ad-hoc gegen die eigene, real gewachsene `AiNetLinter.slnx` (immer
    verfügbar, ausreichend komplex für echte Namenskollisionen/Edge-Cases,
    kein Zugriffsproblem, keine Diskrepanz zwischen behaupteter und
    tatsächlicher Größe).
  - **Entscheidung:** übernommen — siehe "Muss-Haben" (neue Dogfooding-
    Zeile) und "Definition of Done" (angepasste Zeile). Ein Skalierungstest
    bei sehr großen externen Solutions (100k+ LOC) bleibt eine offene, nicht
    in diesem Task verfolgte Fragestellung, falls der Nutzer dafür künftig
    eine passende Solution identifiziert.

## Wie (grober Ansatz)

### Tool-Set (9 MCP-Tools)

| Tool | Input | Output | Basis (bestehender Code) |
| :--- | :--- | :--- | :--- |
| `get_index_scope` | keins | Dateityp-Aufschlüsselung der Solution: `.cs` (voll vom Graph abgedeckt) vs. `.js`/`.razor`/`.xaml`/`.html`/`.css` (nicht abgedeckt, jeweils mit Anzahl) — Orientierung, bevor der Agent überhaupt sucht | `SourceFileCatalog.GetSourceFiles`/`WebFileCatalog.Collect` (bereits vorhanden, liefern schon heute die Dateiliste für die Web-Checker) |
| `find_symbol` | Name/Pattern (Substring/Glob), optionaler Kind-Filter (Klasse/Methode/Property/Interface) | Fundstellen: Datei:Zeile, Kind, Signatur, umschließender Typ. Kein Treffer → Text-Fallback über nicht-C#-Dateitypen, siehe Muss-Haben „Miss-Hint" | `SymbolFinder.FindDeclarationsAsync` (neu einzubinden) |
| `find_references` | Symbol-Identifikator (Datei:Zeile:Spalte oder qualifizierter Name) | Alle Aufrufstellen: Datei:Zeile, aufrufender Kontext, Projekt | `DiffImpactAnalyzer.FindCallSitesAsync` (bereits vorhanden) |
| `get_impact` | Git-Ref (optional) oder Symbol direkt | Betroffene Call-Sites geänderter Signaturen | `DiffImpactAnalyzer.AnalyzeAsync` (bereits vorhanden, `--impact`) |
| `get_type_hierarchy` | Typ-Identifikator | Basisklassen, abgeleitete Klassen, Interface-Implementierer | `SymbolFinder.FindDerivedClassesAsync`/`FindImplementationsAsync` (neu einzubinden) |
| `get_file_skeleton` | Dateipfad (relativ) | Struktur-Skelett dieser einen Datei (Signaturen ohne Bodies) | `SkeletonMapBuilder`, granularer statt Whole-Repo (`--map skeleton`) |
| `get_hotspots` | Optionaler Namespace-/Projekt-Filter | Kopplungs-/Hotspot-Kennzahlen | `HotspotMapBuilder` (bereits vorhanden, `--map hotspots`) |
| `get_violations` | Datei- oder Symbol-Scope | Aktuelle Lint-Verstöße in diesem Scope | `RuleRegistry`/`LinterEngine`, scoped statt Solution-weit |
| `search_pattern` | Regex/Text-Pattern | Textstellen im Solution-Dateibestand | Fallback-Notausstieg für Fälle, die kein Symbol sind (z. B. Config-Werte, Kommentare) — bewusst mit an Bord, damit der Agent nicht komplett auf `rg` verzichten muss, wenn reine Textsuche tatsächlich richtig ist |

Bewusst **keine** Tools zum Schreiben/Ändern von Code (siehe Non-Goals).

### Server-Betrieb

1. Start: `ainetlinter --mcp-server --path <Solution>` lädt die Solution
   einmal via `SourceFileCatalog.LoadAsync` und hält sie resident für die
   gesamte Prozesslaufzeit.
2. Jeder Tool-Call prüft zunächst lazy, ob die von ihm betroffene(n)
   Datei(en) sich seit dem letzten bekannten Stand geändert haben
   (Hash/mtime-Vergleich); bei Abweichung inkrementelles Update über das
   bestehende `WithUpdatedSolution`-Muster statt komplettem Reload.
3. Fehlerfälle (Solution lädt nicht / einzelne Datei kompiliert nicht)
   liefern eine strukturierte Fehlerantwort statt eines Absturzes, im
   bestehenden `[ERROR]`-Format aus `Docs/agent-api.md`.
4. Der bestehende CLI-Batch-Modus bleibt vollständig unverändert und läuft
   parallel zum neuen Server-Modus weiter (kein Killswitch, keine
   Migration bestehender Nutzung).

### Cache-Isolation zwischen mehreren Prozessen

Frage aus der Konzeptrunde: Gibt es ein Problem mit dem bestehenden
Disk-Cache (`AnalysisCacheManager`), wenn mehrere MCP-Server-Instanzen für
unterschiedliche Projekte laufen, während parallel weiterhin normale
`ainetlinter`-Lint-Läufe (auf demselben oder einem anderen Projekt)
passieren können? Geprüft (`Cache/AnalysisCacheManager.cs`):

- **Unterschiedliche Solutions kollidieren nie:** der Cache-Dateiname wird
  aus `SHA256(solutionPath + rulesJsonContent)` gebildet — jede Solution
  bekommt eine eigene Datei im gemeinsamen `cache/`-Verzeichnis neben der
  `.exe`. Beliebig viele MCP-Server-Instanzen für unterschiedliche
  Projekte sind unproblematisch, unabhängig von Lint-Läufen auf anderen
  Projekten.
- **Dieselbe Solution + derselbe MCP-Server:** kein Thema, da `get_violations`
  laut Muss-Haben oben den Disk-Cache gar nicht erst anfasst.
- **Dieselbe Solution, MCP-Server + gleichzeitiger CLI-Lint-Lauf:** ohne die
  obige Entscheidung (`get_violations` ohne Disk-Cache) hätten beide
  Prozesse dieselbe Cache-Datei geöffnet — `AnalysisCacheManager` hat keine
  prozessübergreifende Sperre, `SaveIfDirty()` überschreibt die Datei
  komplett (`File.WriteAllText`), letzter Schreiber gewinnt. Durch den
  Disk-Cache-Bypass im MCP-Modus tritt dieser Fall nicht mehr auf: der
  CLI-Lint-Lauf bleibt alleiniger Schreiber seiner Cache-Datei.
- **Vorbestehendes, nicht zu diesem Task gehörendes Risiko:** zwei
  gleichzeitige CLI-Lint-Läufe (ganz ohne MCP) gegen **dieselbe** Solution
  mit denselben `rules.json` teilen sich schon heute dieselbe Cache-Datei
  ohne Cross-Prozess-Sperre — das ist ein bestehendes Verhalten, unabhängig
  von diesem Feature, siehe "Entdeckte Mängel/Redundanzen".

## Definition of Done / Erfolgskriterien

- `dotnet test` läuft vollständig grün.
- `ainetlinter --mcp-server --path <Solution>` startet einen stdio-MCP-Server,
  der sich von einem MCP-Client (z. B. Claude Code) verbinden lässt und alle
  9 Tools über `tools/list` meldet.
- Jedes der 9 Tools liefert für eine reale Test-Solution korrekte Ergebnisse
  (ein Integrationstest je Tool).
- `get_index_scope` liefert für eine Test-Solution mit gemischtem Code (C#,
  JS, Razor, XAML, CSS) eine korrekte Dateityp-Aufschlüsselung.
- Eine Anfrage nach einem Namen, der nur in einer `.js`/`.razor`/`.xaml`-Datei
  vorkommt, liefert die explizite Miss-Hint-Meldung ("kein C#-Symbol, aber
  Texttreffer in `<Datei>`, nicht Teil des Graphs") statt einer stillen
  Leermenge.
- Eine Änderung an einer Quelldatei zwischen zwei Tool-Calls wird beim
  nächsten Call, der diese Datei betrifft, korrekt erkannt (dedizierter
  Staleness-Test).
- Eine Solution mit Compile-Fehlern in einer Datei liefert für nicht
  betroffene Dateien weiterhin korrekte Antworten, für die betroffene Datei
  einen Warnhinweis statt eines Absturzes.
- Eine nicht ladbare Solution (z. B. kaputte `.slnx`) führt dazu, dass der
  Server startet, aber jeder Tool-Call einen strukturierten Fehler statt
  eines Crashs liefert.
- Ein Zielverzeichnis mit mehreren `.sln`/`.slnx`-Kandidaten und ohne
  explizites `--path` auf eine konkrete Datei führt zu einem Start-Abbruch
  mit klarer Fehlermeldung (Kandidaten benannt) statt einer stillschweigend
  falschen Solution-Auswahl.
- Zwei MCP-Server-Instanzen für unterschiedliche Solutions laufen parallel
  ohne Cache-Datei-Kollision (unterschiedliche Hash-Präfixe, siehe "Wie" /
  Cache-Isolation).
- Ein MCP-Server und ein gleichzeitiger CLI-Lint-Lauf auf **derselben**
  Solution laufen ohne Cache-Datei-Konflikt (MCP-Modus schreibt/liest den
  Disk-Cache nicht).
- Der bestehende CLI-Batch-Modus (`--map`, `--impact`, regulärer Lint-Lauf)
  bleibt unverändert lauffähig (Regressionstest).
- Dokumentation aktualisiert: `Docs/agent-api.md`, `Docs/integration.md`,
  `Docs/ROADMAP.md`, `README.md`.
- Kontinuierliches Dogfooding (blockierend, siehe Muss-Haben): jedes der 9
  Tools wurde in seinem jeweiligen Step mindestens einmal agentenseitig
  gegen die eigene `AiNetLinter.slnx`-Solution aufgerufen (nicht nur gegen
  Fixtures) — dokumentiert im jeweiligen `step-result.md`, Abschnitt
  „Dogfooding". Ersetzt den früher vorgesehenen einmaligen externen
  Praxistest (siehe „Entdeckte Mängel/Redundanzen" für die Begründung).

## Offene Punkte

*Keine blockierenden offenen Punkte.* Einzige bewusst nicht hier
festgelegte Größe: die exakte finale Tool-Namen/Parametrisierung — das ist
laut Konzept-Workflow explizit Sache des Planers im drift-loop (keine
Datei-/Signatur-genaue Implementierungsdetails auf Konzept-Ebene), die
Tabelle unter "Wie" ist der fachliche Vertrags-Rahmen dafür.
