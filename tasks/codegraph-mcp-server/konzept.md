---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: large
rules_dir: .agents/rules
last_updated: 2026-08-01
open_questions: []
supersedes: tasks/codegraph-mcp, tasks/codegraph-mcp-next  # beide Ordner wurden hierher konsolidiert und gelöscht/entschlankt, siehe "Bereits umgesetzt"
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
- Agenten-Loops (z. B. `.agents/Agent-Scaffolding/dev-loop` im Zielprojekt)
  nutzen zur Code-Exploration aktuell `rg`/`grep` — textbasiert, mit False
  Positives (Treffer in Strings/Kommentaren/gleichnamigen Symbolen anderswo),
  die der Agent erst durch zusätzliche Lese-Runden disambiguieren muss. Jede
  dieser Runden kostet Kontext/Tokens.
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

## Bereits umgesetzt (Stand 2026-08-01)

Dieser Task führt zwei Vorgänger-Ordner zusammen: `tasks/codegraph-mcp`
(Umsetzung über `drift-loop`, dann teilweise über `dynamic-loop` fortgeführt)
und `tasks/codegraph-mcp-next` (Konzept-Verfeinerung ohne eigenen Code). Beide
Ordner existieren nicht mehr als eigenständige Tasks — der `drift-loop`-Ordner
wurde nach Übernahme des hier relevanten Inhalts gelöscht (Git-Historie bleibt
erhalten), der `next`-Ordner wurde auf die noch offenen P2-Punkte entschlankt
(`tasks/codegraph-mcp-next/Konzept.md`, jetzt reiner Backlog für später).

**Fertig, reviewt (`approved`), reale Commits:**

- **EPIC-01 — CLI-Einstiegspunkt & Server-Grundgerüst.** `--mcp-server`-Flag,
  `Commands/McpServerCommand.cs`, `ModelContextProtocol`-NuGet-Paket,
  Solution-Auswahl über `--path` mit Mehrdeutigkeits-Abbruch. Commit `3ae6230`.
- **EPIC-02 — Resident-Server & Staleness-Invalidierung.** Zustandshaltende
  `McpCodeGraphServer`-Klasse, Hash/mtime-Cache pro Datei, lazy Prüfung,
  Thread-sicherer Zugriff. Commit `81cf007`.
- **EPIC-03 — Symbolgraph-Tools (5 von 9).** `find_symbol` (Commit `9d6cecc`,
  inkl. fix-01), `find_references` (`a9e91ed`), `get_impact` (`8db5f4b`, inkl.
  fix-01 — behob echten stdio-Subprozess-Hang), `get_file_skeleton`
  (`c125511`), `get_type_hierarchy` (`22e8410`, inkl. fix-01 — behob
  stillschweigendes Entfernen externer Basistypen).
- **EPIC-04 — Struktur-/Qualitäts-Tools, 2 von 4.** `get_index_scope`
  (`6624312`), `get_hotspots` (`995500e` Code, `71779a4` Review).

**Codiert, aber Review nicht abgeschlossen — erste Einheit dieses Tasks:**

- **`get_violations`** (drittes EPIC-04-Tool): vollständig umgesetzt, Code
  1:1 wie ursprünglich geplant (siehe historischer Plan-Commit-Verlauf),
  Build/Test laut Coder grün (1088/1088, 0 Warnungen), Dogfooding gegen die
  eigene `AiNetLinter.slnx` dokumentiert (0 Violations, konsistent mit CLI).
  Code-Commit `e63176d` (Format weicht von der sonstigen Commit-Konvention
  ab — extern zusammengeführt, kein History-Rewrite laut Skill-Regel).
  **Der Kritiker-Review dazu wurde nie abgeschlossen** (Subagenten-Abbruch
  bei Initialisierung, kein inhaltliches Finding). Das ist der erste
  Arbeitsschritt für diesen Task: Review nachholen, **nicht neu coden**.

**Noch nicht begonnen:**

- **EPIC-04, Rest:** `search_pattern` (viertes/letztes EPIC-04-Tool) —
  Text-/Regex-Fallback über den Solution-Dateibestand, für alles, was kein
  C#-Symbol ist.
- **EPIC-05:** Scope-Kommunikation (Tool-`description` + `initialize`-
  `instructions`-Feld benennen die C#-only-Grenze explizit) und Miss-Hint
  (`find_symbol` ohne C#-Treffer meldet Textfunde in nicht abgedeckten
  Dateitypen statt stiller Leermenge).
- **EPIC-06:** Robustheit bei Compile-/Solution-Fehlern — Audit aller 9 Tools
  auf den strukturierten `[ERROR]`-Pfad statt Absturz.
- **EPIC-07:** Tests — Staleness-Invalidierung, Integrationstests je Tool,
  Miss-Hint, Mehrdeutigkeits-Abbruch, Cache-Isolation, CLI-Regression.
- **EPIC-08:** Dokumentation — `Docs/agent-api.md`, `Docs/integration.md`,
  `Docs/ROADMAP.md`, `README.md`.

**Bekannte, noch offene Tech-Debt-Einträge** (aus den Kritiker-Reviews der
fertigen Steps, unverändert gültig, siehe `tech-debt.md` in diesem Ordner):
TD-001 (ungenutzte transitive Abhängigkeit), TD-002 (Subprozess-E2E-Test ohne
Fixture-Pool), TD-003 (Race Condition in `SourceFileCatalog.RegisterMSBuild`),
TD-004/TD-005 (`AIContextFootprint`-Druck auf Registrar-/Tool-Klassen, aktuell
gehandhabtes, wiederkehrendes Muster), TD-006 (Datei-Scan-Duplikation
`GetIndexScopeScanner` vs. `WebFileCatalog`), TD-007 (`TryApplyContentChange`
mit 5 Parametern).

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
  nicht weil ein Agenten-Loop selbst parallelisiert, sondern weil der
  Server auch außerhalb eines einzelnen Workflows genutzt werden können soll.
- Dokumentation: `Docs/agent-api.md` (neuer Abschnitt MCP-Modus),
  `Docs/integration.md` (Setup/Registrierung als MCP-Server),
  `Docs/ROADMAP.md`, `README.md`.
- Tests: Unit-Tests für die Staleness-Invalidierung, Integrationstests je
  Tool gegen eine Test-Solution (analog bestehender CLI-Integrationstests).
- **Dogfooding pro Tool-Step gegen die eigene `AiNetLinter.slnx`:** Jeder
  Step, der eines der 9 Tools neu einführt oder in seiner Kernlogik
  wesentlich ändert, verifiziert es zusätzlich zu den automatisierten
  Fixture-Tests **einmal ad-hoc gegen die reale AiNetLinter-Solution
  selbst**, dokumentiert unter einem eigenen Abschnitt „Dogfooding" im
  jeweiligen Ergebnisprotokoll der Einheit. Ersetzt keine automatisierten
  Tests, ergänzt sie um einen Realismus-Check gegen echten, gewachsenen
  Code, den Mini-Fixtures strukturell nicht leisten können (echte
  Namenskollisionen, echte Kommentare/Strings als potenzielle False
  Positives, echte Dateigrößen). Hat sich bereits zweimal ausgezahlt (siehe
  "Bereits umgesetzt": stdio-Hang-Fix bei `get_impact`, externe-Basistyp-Fix
  bei `get_type_hierarchy`).

### Erweiterungen ins Scope (übernommen aus dem Folge-Konzept, P0+P1 — kein offener Punkt mehr)

Diese Punkte stammen aus der Konzept-Verfeinerung in
`tasks/codegraph-mcp-next` (jetzt entschlankt, siehe
`../codegraph-mcp-next/Konzept.md` für den verbleibenden P2-Rest). Sie waren
dort bereits vollständig entschieden (keine offenen Fragen, keine
Nutzer-Vorbehalte) — hier ins verbindliche Scope dieses Tasks übernommen,
damit sie nicht als loses Zweitdokument verloren gehen.

- **Trunkierung + `maxResults` für alle Listen-Tools.** `find_symbol`,
  `find_references`, `get_impact`, `search_pattern` geben aktuell (bzw.
  würden geben) unbegrenzt viele Zeilen aus — das Gegenteil des
  Projektziels. Optionaler Parameter `maxResults` (Default 50) an jedem
  Listen-Tool, Limit wirkt auf **Ausgabezeilen**, nicht auf Symbole (wegen
  `partial`-Typen). Ein gemeinsamer Trunkierungs-Helper gehört neben
  `Mcp/McpToolResults.cs`. Abgeschnittene Antworten enden mit einer
  Meta-Zeile, die den nächsten Zug nahelegt (siehe nächster Punkt fürs
  Format). DoD-Kriterium: jedes Listen-Tool liefert bei einer generischen
  Anfrage gegen die Last-Fixture (siehe P1-6 unten) eine Antwort unter der
  konfigurierten Zeilengrenze.
- **Ausgabeformat verbindlich: Text, nicht JSON.** Alle Tools bleiben bei
  Plain-Text-Zeilen über `McpToolResults.Text` (so bereits durchgängig
  umgesetzt) — kein Wechsel zu JSON für Trunkierungs-Metadaten oder sonst
  etwas. Grund: token-günstiger, von LLMs zuverlässig lesbar, konsistent
  mit dem bestehenden `[ERROR]`-Textformat aus `LinterErrorFormatter`.
  Feste Meta-Zeile bei Trunkierung, sinngemäß: `[342 Treffer gesamt, 50
  gezeigt — Pattern verfeinern oder maxResults erhöhen]`. Gehört als
  verbindliche Format-Regel in `Docs/agent-api.md` (EPIC-08).
- **Regel-ID in der `get_violations`-Ausgabe.** Jeder gemeldete Verstoß
  trägt seine Regel-ID/seinen Regelnamen — der Agent hat den zugehörigen
  Regeltext über die ohnehin geladene `.agents/rules/AiNetLinter.mdc`
  bereits im Kontext. Löst dadurch die gesamte `rules.json`-Verzahnungsfrage
  (kein `agent_hint`-Feld, keine `mcp_config`-Filterung, kein
  `get_active_rules`-Tool nötig — siehe "Bewusst gestrichen" unter
  "Verworfene Alternativen").
- **Neu angelegte/gelöschte `.cs`-Dateien sichtbar machen.**
  `RefreshStaleDocuments()` (`Mcp/McpCodeGraphServer.cs`) iteriert aktuell
  nur über die beim Serverstart bekannten `Document`s — eine danach neu
  angelegte Klasse ist für den Server bis zum Neustart unsichtbar, eine
  gelöschte Datei bleibt fälschlich als Treffer bestehen. Gefährlich, weil
  der Server nicht mit einem Fehler antwortet, sondern mit einer
  plausiblen Lüge („keine Treffer" für tatsächlich existierenden Code) —
  und neue Dateien sind im Agenten-Dev-Loop der Normalfall. Fix:
  zusätzlicher Verzeichnis-Sweep, der `.cs`-Dateien ohne zugehöriges
  `Document` über die Roslyn-Solution-API einhängt (Projekt-Zuordnung über
  längsten gemeinsamen Pfad-Präfix) und Dokumente ohne existierende Datei
  entfernt. Bewusste Grenze: `<Compile Remove=...>`-Ausschlüsse werden
  nicht erkannt (kein voller `.csproj`-Parser) — als bekannte Einschränkung
  dokumentieren, nicht lösen. Test-Kriterium: eigene Testfälle für „Datei
  angelegt" und „Datei gelöscht" zusätzlich zum bestehenden „Datei
  geändert"-Fall (EPIC-07).
- **`rules.json`-Auto-Discovery statt stiller Default-Regeln.** Fehlt
  `--config`, sucht der Server aktuell gar nicht nach `rules.json` und
  arbeitet still mit Default-Werten — `get_violations` prüft dann gegen
  Default-Regeln statt der Projekt-Konfiguration, ohne jeden Hinweis. Fix:
  ohne `--config` neben der aufgelösten Solution-Datei nach `rules.json`
  suchen; wird keine gefunden, `[WARN]` auf stderr **und** ein Vermerk in
  der `get_violations`-Antwort selbst („Basis: Default-Regeln, keine
  `rules.json` gefunden") — der Agent sieht das Server-Log nicht.
- **Kaltstart entkoppeln.** `McpServerCommand.RunAsync` wartet aktuell
  `TryLoadSolutionAsync` vollständig ab, bevor der stdio-Transport überhaupt
  aufgesetzt wird — bei 160k LOC blockiert das 30-60s vor dem ersten
  `initialize`-Handshake, was MCP-Hosts mit Startup-Timeout als
  fehlgeschlagen werten kann. Fix: Transport zuerst aufsetzen, Solution-Load
  als Hintergrund-Task; `McpCodeGraphServer` bekommt einen dritten Zustand
  „lädt noch", Tools antworten in diesem Zustand mit einer strukturierten
  `[ERROR]`-artigen Kurzantwort („Solution wird noch geladen (seit N s) —
  in Kürze erneut versuchen") statt zu blockieren. Bewusst kein
  Timeout-Warten. Der `instructions`-Text aus EPIC-05 erwähnt diesen
  Zustand einmal, damit er nicht als „Server kaputt" gelesen wird.
- **Staleness-Sweep über Verzeichnis-`mtime` kurzschließen.**
  `RefreshStaleDocuments()` prüft aktuell `File.GetLastWriteTimeUtc` für
  jede Datei jedes Projekts bei **jedem** Tool-Call — bei Datei-Auflagen im
  vier- bis fünfstelligen Bereich potenziell spürbar (Netzlaufwerk,
  Virenscanner-Interception). Fix: Verzeichnis-`mtime` je Projektverzeichnis
  cachen, unveränderte Verzeichnisse komplett überspringen — derselbe
  Mechanismus deckt zusätzlich den Datei-Sweep aus dem vorherigen Punkt ab,
  deshalb beide zusammen umsetzen.
- **stdout strukturell als reiner Protokollkanal.** Im stdio-MCP-Modus ist
  stdout der JSON-RPC-Kanal — eine einzelne Textzeile dorthin (z. B. ein
  `Console.WriteLine` in einer wiederverwendeten CLI-Komponente wie
  `DiffImpactAnalyzer`) zerstört das Framing und die Session. Aktuell nur
  durch Disziplin (`verbose: false`-Aufrufe) vermieden, nicht strukturell.
  Fix: eigene `ILintConsole`-Implementierung für den MCP-Modus, die auch
  `WriteLine` nach stderr leitet, verdrahtet in `McpServerCommand`. Dazu ein
  E2E-Test, der eine Abfolge echter Tool-Calls gegen den Serverprozess fährt
  und assertiert, dass jede stdout-Zeile ein gültiger JSON-RPC-Frame ist
  (gehört fachlich zu EPIC-07, wird aber erst durch diesen Punkt notwendig).
- **Generierte Last-Fixture als Skalierungsnachweis.** Der ursprüngliche
  externe Praxistest (siehe EPIC-09, gestrichen) ist ersatzlos entfallen,
  Dogfooding gegen die eigene `AiNetLinter.slnx` (~3.600 Zeilen) kann die
  Skalierungsfrage strukturell nicht beantworten — alle Kernbegründungen des
  Projekts sind Aussagen über große Solutions, und die beiden vorherigen
  Punkte (Kaltstart, Staleness-Sweep) sind Effekte, die unterhalb einiger
  tausend Dateien unsichtbar bleiben. Fix: ein Test-Hilfsmittel, das eine
  synthetische Solution definierter Größe (z. B. 500 / 5.000 Dateien mit
  realistischen Referenzketten) generiert, plus ein Messlauf, der
  Kaltstart-Zeit und Dauer je Tool-Call protokolliert. Agentenseitig
  reproduzierbar, kein externes Repo nötig.
- **Opt-in Call-Log als Datenbasis für künftige Priorisierung.** Es gibt
  aktuell keinerlei eigene Messung, welche der 9 Tools tatsächlich genutzt
  werden, welche Leermengen liefern, wie oft dieselbe Frage doppelt gestellt
  wird — alle bisherigen Priorisierungen (inklusive der Punkte in diesem
  Abschnitt) stammen aus Markt-Benchmarks, nicht aus eigenen Daten. Fix:
  schlankes Call-Log (Zeitstempel, Tool-Name, gekürzte Parameter,
  Ergebniszeilen, trunkiert ja/nein, Dauer, Leermenge ja/nein), eine Zeile
  pro Call, Ablage neben `cache/` mit demselben Solution-Hash im
  Dateinamen (Isolationslogik existiert bereits in `AnalysisCacheManager`).
  Per Flag `--mcp-log` opt-in, Default aus — sonst ungefragter Schreibzugriff
  im Projektverzeichnis des Nutzers. Auswertung bewusst nicht automatisieren.
- **Registrierungs-/Umstellungsempfehlung für externe Agenten-Loops
  dokumentieren.** Ein Server, den niemand aufruft, hat auch nichts zu
  messen (siehe vorheriger Punkt). Fix ist reine Dokumentation, kein Code:
  Registrierung des Servers in `Docs/integration.md` (ohnehin EPIC-08)
  **plus** eine explizite Empfehlung, in welcher Reihenfolge ein
  Agenten-Loop im Zielprojekt die Tools gegenüber `rg`/`grep` bevorzugen
  sollte (erst `find_symbol`/`get_file_skeleton`, `rg` nur für Nicht-Symbole
  wie Konfigwerte/Kommentare/Nicht-C#-Dateien) — gilt nur, wenn der Server im
  jeweiligen Zielprojekt überhaupt registriert ist.

### Nice-to-Have (optional, spätere Iteration)

- Konfigurierbare Tool-Auswahl (z. B. `get_violations` bei Bedarf abschaltbar,
  falls sich das bei sehr großen Solutions als Performance-Faktor erweist).
- Persistenter Cache über Server-Neustarts hinweg (z. B. Skeleton-Daten auf
  Disk), um die Kaltstart-Zeit bei 160k LOC zu verkürzen — erst relevant,
  sobald die tatsächliche Kaltstart-Zeit gemessen ist (siehe Last-Fixture
  oben unter "Erweiterungen").
- Die verbleibenden P2-Punkte aus der Konzept-Verfeinerung
  (`get_symbol_body` + stabile Symbol-IDs, `depth`-Parameter für
  Blast-Radius, DI-Registrierungs-Zeile in `get_type_hierarchy`) — siehe
  `../codegraph-mcp-next/Konzept.md`, bewusst außerhalb dieses Tasks, da
  echter Mehrwert, aber ohne entsteht kein Schaden, und die Priorisierung
  gegenüber den P0/P1-Punkten oben eindeutig war.

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
- **Kein zweites, generisches Codegraph-Tool** (`get_call_tree`,
  Duplicate-Symbol-Drift-Warnung, Dead-Code-Detection, PageRank/Symbol-
  Centrality) — siehe "Verworfene Alternativen" für die Einzelbegründungen.

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
  (MSBuild/Roslyn-Ladezeit); der Kaltstart-**Blockade**-Effekt (kein
  `initialize`-Handshake während des Ladens) ist dagegen ins Scope
  übernommen (siehe "Erweiterungen ins Scope" / Kaltstart entkoppeln) — das
  ist ein anderes Problem als die reine Ladedauer.
- **Thread-Safety/`SemaphoreSlim` zusätzlich zum bestehenden `Lock`:**
  verworfen — bereits erledigt. `McpCodeGraphServer` hält ein `Lock`,
  `GetCurrentSolution()` gibt eine immutable `Solution` heraus, die
  Roslyn-Arbeit läuft danach lockfrei korrekt parallel. Ein zweiter
  Synchronisationsmechanismus wäre Schaden, kein Zugewinn.
- **`.csproj`/`.sln`-Hash-Invalidierung:** verworfen — aufgegangen im
  Verzeichnis-Sweep für neue/gelöschte `.cs`-Dateien (siehe "Erweiterungen
  ins Scope"); das reale Problem sind neue/gelöschte Dateien, nicht
  geänderte Projektdateien.
- **Duplicate-Symbol-Drift-Warnung** ("mehr als 1 Treffer bei gleichem
  Namensmuster" als automatische Warnung): verworfen — in echtem C# der
  Regelfall (Overloads, Interface+Impl, `partial`, Test-Doubles, generische
  Varianten), überwiegend Rauschen, trainiert den Agenten, Warnungen zu
  überlesen. Drift-Erkennung gehört als Linterregel mit scharfer Definition,
  nicht als Nebeneffekt einer Suchabfrage.
- **Dead-Code-Detection:** verworfen — nicht nur nutzlos, sondern riskant:
  „0 Referenzen" gilt nur ohne Reflection, DI-Registrierung, Serialisierung
  und XAML-Bindings — genau der vorliegende Codebestand. Ein Agent liest
  einen solchen Befund leicht als Löschauftrag.
- **PageRank/Symbol-Centrality:** verworfen — ein Agent mit konkretem Task
  braucht keine repo-weite Wichtigkeits-Rangliste; `HotspotMapBuilder`
  deckt den realen Bedarf bereits ab.
- **`agent_hint`-Feld in `rules.json`:** verworfen — erledigt durch die
  Regel-ID in `get_violations` (siehe "Erweiterungen ins Scope"), den
  Regeltext hat der Agent bereits über `AiNetLinter.mdc` im Kontext.
- **`mcp_config`-Rauschfilterung** (konfigurierbares Ausblenden bestimmter
  Regeln in `get_violations`): verworfen — `rules.json` ist bereits zu
  groß, und Verstecken schafft verdeckte Tech-Debt statt sie sichtbar zu
  halten.
- **No-New-Violations-Ratchet:** verworfen — Duldungs-Pattern schleppt
  Tech-Debt über Jahre mit, statt sie sichtbar zu machen.
- **`get_active_rules`-Tool:** verworfen — redundant zu
  `.agents/rules/AiNetLinter.mdc` (`--sync-agent-rules-only`).
- **`get_call_tree` als eigenes Tool:** verworfen — dieselbe Frage wie
  Blast-Radius-Traversal (siehe `../codegraph-mcp-next/Konzept.md`, P2-4),
  gelöst durch einen `depth`-Parameter an bestehenden Tools statt ein
  zweites, ähnliches Tool. Je mehr ähnliche Tools ein Server anbietet, desto
  häufiger greift das LLM zum falschen.

## Wo im Projekt

- [Program.cs](src/AiNetLinter/Program.cs) — Einstiegspunkt, Dispatch auf den
  neuen Modus.
- [Cli/CliOptions.cs](src/AiNetLinter/Cli/CliOptions.cs),
  [Cli/CliOptionFactory.cs](src/AiNetLinter/Cli/CliOptionFactory.cs),
  [Cli/LinterArgs.cs](src/AiNetLinter/Cli/LinterArgs.cs) — neue Option für
  den MCP-Modus, analog zu bestehenden Flags wie `--map`.
- [Commands/](src/AiNetLinter/Commands) — Server-Modus-Einstieg
  (`McpServerCommand.cs`), analog `MapCommand.cs`/`ImpactCommand.cs`.
- [Baseline/SourceFileCatalog.cs](src/AiNetLinter/Baseline/SourceFileCatalog.cs)
  — Solution laden/aktualisieren; Basis für Resident-Betrieb.
  `WithUpdatedSolution` existiert bereits für In-Memory-Updates (aktuell für
  Auto-Fix genutzt) — direkt wiederverwendbar für die Staleness-Invalidierung.
- [Core/DiffImpactAnalyzer.cs](src/AiNetLinter/Core/DiffImpactAnalyzer.cs) —
  bestehende `SymbolFinder`-Nutzung (`FindReferencesAsync`), Basis für
  `find_references`/`get_impact`.
- [Maps/Skeleton/SkeletonMapBuilder.cs](src/AiNetLinter/Maps/Skeleton/SkeletonMapBuilder.cs),
  [Maps/HotspotMapBuilder.cs](src/AiNetLinter/Maps/HotspotMapBuilder.cs) —
  Basis für `get_file_skeleton`/`get_hotspots`.
- [Core/RuleRegistry.cs](src/AiNetLinter/Core/RuleRegistry.cs),
  [Core/LinterEngine.cs](src/AiNetLinter/Core/LinterEngine.cs) — Basis für
  `get_violations`.
- [Web/WebFileCatalog.cs](src/AiNetLinter/Web/WebFileCatalog.cs) — enumeriert
  bereits JS-/CSS-/Razor-Dateien für die Web-Checker; Basis für
  `get_index_scope`.
- [Mcp/McpCodeGraphServer.cs](src/AiNetLinter/Mcp/McpCodeGraphServer.cs) —
  zentrale Resident-Server-Klasse (Solution, Staleness-Cache, `Config`,
  `Console`) — Ansatzpunkt für Kaltstart-Entkopplung, Verzeichnis-Sweep,
  stdout-Schutz (siehe "Erweiterungen ins Scope").
- [Mcp/McpToolResults.cs](src/AiNetLinter/Mcp/McpToolResults.cs) —
  geteiltes Ergebnis-Boilerplate, Ansatzpunkt für den Trunkierungs-Helper.
- [Mcp/Tools/](src/AiNetLinter/Mcp/Tools) — pro-Tool-Klassen, drei
  Registrar-Klassen (`SymbolGraphToolRegistrations`,
  `FileStructureToolRegistrations`, `AnalysisToolRegistrations`) — für
  `search_pattern` voraussichtlich eine vierte (siehe `tech-debt.md`).
- [Docs/agent-api.md](Docs/agent-api.md), [Docs/integration.md](Docs/integration.md)
  — Doku-Ergänzung.
- [AiNetLinter.csproj](src/AiNetLinter/AiNetLinter.csproj) — bereits um
  `ModelContextProtocol` ergänzt.

## Entdeckte Mängel/Redundanzen

- **Kein Neubau der Symbol-/Solution-Logik nötig** — bereits umgesetzt:
  `SourceFileCatalog.WithUpdatedSolution` und die `SymbolFinder`-Nutzung in
  `DiffImpactAnalyzer` wurden direkt wiederverwendet, keine parallele zweite
  Roslyn-Zugriffsschicht gebaut.
- **Architektur-Spannung "Monolithisch bleiben" vs. neuer Server-Modus** —
  geprüft und aufgelöst: ein weiterer Modus im selben Executable ohne
  dynamisches Laden/DI-Container verletzt `AiNetLinterRichtlinien.mdc` §1/§2
  nicht.
- **Vorbestehende Cache-Race zwischen zwei parallelen CLI-Lint-Läufen**
  (`Cache/AnalysisCacheManager.cs`, `SaveIfDirty`): bewusst außerhalb dieses
  Tasks — der MCP-Server umgeht das Problem für sich selbst (Disk-Cache-
  Bypass), die CLI-interne Race bleibt unangetastetes Bestandsverhalten,
  unabhängig von diesem Feature.
- **`get_index_scope` brauchte keinen neuen Datei-Scan** — bereits umgesetzt
  auf Basis von `SourceFileCatalog.GetSourceFiles` + `WebFileCatalog.Collect`.
- **Iteratives Agenten-Dogfooding statt einmaligem externen Praxistest** —
  bereits umgesetzt (siehe Muss-Haben "Dogfooding pro Tool-Step"); ersetzt
  den ursprünglich vorgesehenen einmaligen externen Praxistest gegen
  `San.smart.Planner.Platform`, der an der tatsächlichen Größe dieser
  Solution in diesem Checkout (~3.600 statt ~160k Zeilen) gescheitert wäre.
  Die dadurch weiterhin offene Skalierungsfrage wird jetzt durch die
  generierte Last-Fixture adressiert (siehe "Erweiterungen ins Scope").

## Wie (grober Ansatz)

### Tool-Set (9 MCP-Tools)

| Tool | Input | Output | Basis (bestehender Code) | Status |
| :--- | :--- | :--- | :--- | :--- |
| `get_index_scope` | keins | Dateityp-Aufschlüsselung der Solution | `SourceFileCatalog.GetSourceFiles`/`WebFileCatalog.Collect` | fertig |
| `find_symbol` | Name/Pattern, optionaler Kind-Filter | Fundstellen inkl. Miss-Hint-Fallback | `SymbolFinder.FindDeclarationsAsync` | fertig |
| `find_references` | Symbol-Identifikator | Alle Aufrufstellen | `DiffImpactAnalyzer.FindCallSitesAsync` | fertig |
| `get_impact` | Git-Ref oder Symbol | Betroffene Call-Sites | `DiffImpactAnalyzer.AnalyzeAsync` | fertig |
| `get_type_hierarchy` | Typ-Identifikator | Basis-/abgeleitete Typen | `SymbolFinder.FindDerivedClassesAsync`/`FindImplementationsAsync` | fertig |
| `get_file_skeleton` | Dateipfad | Struktur-Skelett einer Datei | `SkeletonMapBuilder` | fertig |
| `get_hotspots` | Optionaler Filter | Kopplungs-/Hotspot-Kennzahlen | `HotspotMapBuilder` | fertig |
| `get_violations` | Datei-/Symbol-Scope | Aktuelle Lint-Verstöße | `RuleRegistry`/`LinterEngine` | codiert, Review offen |
| `search_pattern` | Regex/Text-Pattern | Textstellen im Dateibestand | Fallback für Nicht-Symbol-Fälle | offen |

Bewusst **keine** Tools zum Schreiben/Ändern von Code (siehe Non-Goals).

### Server-Betrieb

1. Start: `ainetlinter --mcp-server --path <Solution>` lädt die Solution
   einmal via `SourceFileCatalog.LoadAsync` und hält sie resident für die
   gesamte Prozesslaufzeit. Transport/Handshake stehen dabei unabhängig vom
   Ladezustand sofort bereit (siehe "Erweiterungen ins Scope" / Kaltstart).
2. Jeder Tool-Call prüft zunächst lazy, ob die von ihm betroffene(n)
   Datei(en) sich seit dem letzten bekannten Stand geändert haben
   (Hash/mtime-Vergleich, über Verzeichnis-`mtime` kurzgeschlossen); bei
   Abweichung inkrementelles Update über das bestehende `WithUpdatedSolution`-
   Muster statt komplettem Reload.
3. Fehlerfälle (Solution lädt nicht / einzelne Datei kompiliert nicht)
   liefern eine strukturierte Fehlerantwort statt eines Absturzes, im
   bestehenden `[ERROR]`-Format aus `Docs/agent-api.md`.
4. Der bestehende CLI-Batch-Modus bleibt vollständig unverändert und läuft
   parallel zum neuen Server-Modus weiter (kein Killswitch, keine
   Migration bestehender Nutzung).

### Cache-Isolation zwischen mehreren Prozessen

- **Unterschiedliche Solutions kollidieren nie:** der Cache-Dateiname wird
  aus `SHA256(solutionPath + rulesJsonContent)` gebildet — jede Solution
  bekommt eine eigene Datei im gemeinsamen `cache/`-Verzeichnis neben der
  `.exe`.
- **Dieselbe Solution + derselbe MCP-Server:** kein Thema, da `get_violations`
  den Disk-Cache gar nicht erst anfasst.
- **Dieselbe Solution, MCP-Server + gleichzeitiger CLI-Lint-Lauf:** durch
  den Disk-Cache-Bypass im MCP-Modus bleibt der CLI-Lint-Lauf alleiniger
  Schreiber seiner Cache-Datei.
- **Vorbestehendes, nicht zu diesem Task gehörendes Risiko:** zwei
  gleichzeitige CLI-Lint-Läufe (ganz ohne MCP) gegen **dieselbe** Solution
  mit denselben `rules.json` teilen sich schon heute dieselbe Cache-Datei
  ohne Cross-Prozess-Sperre — bestehendes Verhalten, unabhängig von diesem
  Feature, siehe "Entdeckte Mängel/Redundanzen".

## Definition of Done / Erfolgskriterien

**Aus dem Ursprungs-Scope (teilweise bereits erfüllt, siehe "Bereits umgesetzt"):**

- `dotnet test` läuft vollständig grün.
- `ainetlinter --mcp-server --path <Solution>` startet einen stdio-MCP-Server,
  der sich von einem MCP-Client verbinden lässt und alle 9 Tools über
  `tools/list` meldet.
- Jedes der 9 Tools liefert für eine reale Test-Solution korrekte Ergebnisse
  (ein Integrationstest je Tool) — inkl. `get_violations` (Review muss
  `approved` sein, ohne dass der Code dafür neu geschrieben werden musste)
  und `search_pattern` (neu zu bauen).
- `get_index_scope` liefert für eine Test-Solution mit gemischtem Code (C#,
  JS, Razor, XAML, CSS) eine korrekte Dateityp-Aufschlüsselung.
- Eine Anfrage nach einem Namen, der nur in einer `.js`/`.razor`/`.xaml`-Datei
  vorkommt, liefert die explizite Miss-Hint-Meldung statt einer stillen
  Leermenge.
- Eine Änderung an einer Quelldatei zwischen zwei Tool-Calls wird beim
  nächsten Call, der diese Datei betrifft, korrekt erkannt.
- Eine Solution mit Compile-Fehlern in einer Datei liefert für nicht
  betroffene Dateien weiterhin korrekte Antworten, für die betroffene Datei
  einen Warnhinweis statt eines Absturzes.
- Eine nicht ladbare Solution führt dazu, dass der Server startet, aber
  jeder Tool-Call einen strukturierten Fehler statt eines Crashs liefert.
- Ein Zielverzeichnis mit mehreren `.sln`/`.slnx`-Kandidaten ohne explizites
  `--path` auf eine konkrete Datei führt zu einem Start-Abbruch mit klarer
  Fehlermeldung statt einer stillschweigend falschen Solution-Auswahl.
- Zwei MCP-Server-Instanzen für unterschiedliche Solutions laufen parallel
  ohne Cache-Datei-Kollision.
- Ein MCP-Server und ein gleichzeitiger CLI-Lint-Lauf auf **derselben**
  Solution laufen ohne Cache-Datei-Konflikt.
- Der bestehende CLI-Batch-Modus bleibt unverändert lauffähig
  (Regressionstest).
- Dokumentation aktualisiert: `Docs/agent-api.md`, `Docs/integration.md`,
  `Docs/ROADMAP.md`, `README.md`.
- Kontinuierliches Dogfooding: jedes der 9 Tools wurde in seiner jeweiligen
  Einheit mindestens einmal agentenseitig gegen die eigene `AiNetLinter.slnx`
  aufgerufen (nicht nur gegen Fixtures).

**Aus den übernommenen Erweiterungen (neu, siehe "Erweiterungen ins Scope"):**

- Jedes Listen-Tool (`find_symbol`, `find_references`, `get_impact`,
  `search_pattern`) trunkiert bei generischer Anfrage gegen die
  Last-Fixture unter einer definierten Zeilengrenze, mit `maxResults`-
  Parameter (Default 50) und einheitlicher Trunkierungs-Meta-Zeile.
- `get_violations` gibt für jeden Verstoß die Regel-ID aus.
- Eine während der Session **neu angelegte** bzw. **gelöschte** `.cs`-Datei
  wird beim nächsten betroffenen Tool-Call korrekt sichtbar bzw. entfernt
  (dedizierte Tests für beide Fälle, zusätzlich zum bestehenden
  Änderungs-Test).
- Ohne `--config` wird `rules.json` neben der Solution automatisch gefunden;
  fehlt sie, erscheint ein `[WARN]` auf stderr **und** ein Vermerk in der
  `get_violations`-Antwort.
- Der Server beantwortet `initialize`/`tools/list` auch während eines noch
  laufenden Solution-Kaltstarts; betroffene Tools liefern in dieser Zeit
  eine strukturierte „lädt noch"-Antwort statt zu blockieren.
- Der Staleness-Sweep überspringt unveränderte Projektverzeichnisse über
  einen `mtime`-Kurzschluss, verifiziert durch einen Performance-/
  Zähl-Test gegen die Last-Fixture.
- Ein E2E-Test fährt eine Abfolge realer Tool-Calls und bestätigt, dass
  jede stdout-Zeile ein gültiger JSON-RPC-Frame ist (kein Leck durch
  wiederverwendete CLI-Komponenten).
- Eine generierte Last-Fixture (mind. 500 Dateien) existiert als
  Test-Hilfsmittel, ein Messlauf für Kaltstart-Zeit und Tool-Call-Dauer
  wurde mindestens einmal ausgeführt und dokumentiert.
- `--mcp-log` aktiviert ein opt-in Call-Log, Default ist aus; ohne das Flag
  entsteht keine Log-Datei.
- `Docs/integration.md` enthält eine dokumentierte Empfehlung zur
  Tool-vs-`rg`-Priorisierung für Agenten-Loops, die den Server registrieren.

## Offene Punkte

*Keine blockierenden offenen Punkte.* Nächster konkreter Arbeitsschritt:
Kritiker-Review für den bereits vorhandenen `get_violations`-Code (Commit
`e63176d`) nachholen — kein Neu-Code, siehe "Bereits umgesetzt". Die exakte
finale Tool-Namen/Parametrisierung der neuen Erweiterungen (z. B. genaue
Feldnamen im Trunkierungs-Meta-Format) ist bewusst nicht hier
festgelegt — das ist Sache der Planungs-Einheit im Loop, keine
Konzept-Ebenen-Entscheidung.
