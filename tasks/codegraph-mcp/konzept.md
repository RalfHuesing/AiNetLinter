---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: large
rules_dir: .agents/rules
last_updated: 2026-07-31
open_questions:
  - Exakte finale Tool-Namen/Parametrisierung (kann sich beim Planer im drift-loop noch verschieben)
  - Kaltstart-Zeit bei 160k LOC noch nicht gemessen — steuert, ob persistenter Cache Muss-Haben wird
  - Bestätigung: ModelContextProtocol-NuGet-Paket als neue externe Abhängigkeit akzeptiert
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

- Neuer Ausführungsmodus (Arbeitsname `--mcp-server`, finaler Flag-Name
  Sache des Planers), der einen stdio-MCP-Server startet statt eines
  Batch-Laufs.
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
- Tool-Set wie unten unter "Wie" beschrieben (8 Tools).
- Thread-sicherer Zugriff auf die gehaltene `Solution`/`Compilation` —
  nicht weil der drift-loop selbst parallelisiert (er ist laut Spec strikt
  seriell), sondern weil der Server auch außerhalb dieses einen Workflows
  genutzt werden können soll.
- Dokumentation: `Docs/agent-api.md` (neuer Abschnitt MCP-Modus),
  `Docs/integration.md` (Setup/Registrierung als MCP-Server),
  `Docs/ROADMAP.md`, `README.md`.
- Tests: Unit-Tests für die Staleness-Invalidierung, Integrationstests je
  Tool gegen eine Test-Solution (analog bestehender CLI-Integrationstests).

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

## Wie (grober Ansatz)

### Tool-Set (8 MCP-Tools)

| Tool | Input | Output | Basis (bestehender Code) |
| :--- | :--- | :--- | :--- |
| `find_symbol` | Name/Pattern (Substring/Glob), optionaler Kind-Filter (Klasse/Methode/Property/Interface) | Fundstellen: Datei:Zeile, Kind, Signatur, umschließender Typ | `SymbolFinder.FindDeclarationsAsync` (neu einzubinden) |
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

## Definition of Done / Erfolgskriterien

- `dotnet test` läuft vollständig grün.
- `ainetlinter --mcp-server --path <Solution>` startet einen stdio-MCP-Server,
  der sich von einem MCP-Client (z. B. Claude Code) verbinden lässt und alle
  8 Tools über `tools/list` meldet.
- Jedes der 8 Tools liefert für eine reale Test-Solution korrekte Ergebnisse
  (ein Integrationstest je Tool).
- Eine Änderung an einer Quelldatei zwischen zwei Tool-Calls wird beim
  nächsten Call, der diese Datei betrifft, korrekt erkannt (dedizierter
  Staleness-Test).
- Eine Solution mit Compile-Fehlern in einer Datei liefert für nicht
  betroffene Dateien weiterhin korrekte Antworten, für die betroffene Datei
  einen Warnhinweis statt eines Absturzes.
- Eine nicht ladbare Solution (z. B. kaputte `.slnx`) führt dazu, dass der
  Server startet, aber jeder Tool-Call einen strukturierten Fehler statt
  eines Crashs liefert.
- Der bestehende CLI-Batch-Modus (`--map`, `--impact`, regulärer Lint-Lauf)
  bleibt unverändert lauffähig (Regressionstest).
- Dokumentation aktualisiert: `Docs/agent-api.md`, `Docs/integration.md`,
  `Docs/ROADMAP.md`, `README.md`.
- Manueller Praxistest: Server gegen `San.smart.Planner.Platform` (~160k LOC)
  gestartet, mindestens 3 der 8 Tools live gegen die reale Solution
  ausprobiert und Ergebnis stichprobenartig verifiziert.

## Offene Punkte

- Exakte finale Tool-Namen/Parametrisierung — der Planer im drift-loop kann
  hier noch feinjustieren, die obige Tabelle ist der fachliche Vertrags-
  Rahmen, keine finale API-Signatur.
- Kaltstart-Zeit bei 160k LOC ist noch nicht gemessen. Stellt sich heraus,
  dass sie für interaktive Nutzung zu langsam ist, wird der persistente
  Cache (aktuell Nice-to-Have) zum Muss-Haben.
- Bestätigung ausstehend: `ModelContextProtocol`-NuGet-Paket als neue
  externe Abhängigkeit akzeptiert — bisher hat AiNetLinter nur
  Roslyn/System.CommandLine/CSS-/JS-Parser als Third-Party-Dependencies.
