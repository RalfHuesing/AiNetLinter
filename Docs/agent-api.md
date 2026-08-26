# AiNetLinter — Agent-API Referenz

Kompakte Referenz für AI-Agenten. Alle CLI-Flags, Workflows und das strukturierte Error-Format.

---

## Discovery-Commands

Regeln entdecken ohne Lint-Lauf (kein `--path` nötig):

```bash
# Alle Regeln als Markdown-Tabelle:
ainetlinter --list-rules

# Eine Regel vollständig beschreiben (Warum, Alternativen, Auto-Fix):
ainetlinter --describe-rule <RuleId>
# Beispiel:
ainetlinter --describe-rule EnforceSealedClasses

# Regeln nach Begriff durchsuchen (RuleId, Beschreibung, Intent):
ainetlinter --search-rules <Begriff>
# Beispiele:
ainetlinter --search-rules "komplexitaet"
ainetlinter --search-rules "sealed"
ainetlinter --search-rules "agent"

# Integrierte Dokumentation als Markdown ausgeben (z. B. Konfigurationsreferenz):
ainetlinter --docs configuration
```

---

## Lint-Workflows

### Schritt 1: Startkonfiguration holen
```bash
ainetlinter --docs rules-json > rules.json
```
Dumpt die eingebettete Default-Konfiguration — sofort einsatzbereit, lokal anpassbar.

### Workflow 1 — Lint + Fix

```bash
# Schritt 1: Lint-Lauf
ainetlinter --config rules.json --path ./src/MeinProjekt.slnx

# Schritt 2: Fix anwenden
ainetlinter --config rules.json --path ./src/MeinProjekt.slnx --fix
```

Auto-fixbare Regeln: `EnforceSealedClasses`, `EnforcePascalCase`, `EnforceNullableEnable`

### Workflow 2 — Baseline (Ratchet-Modus)

Friert bestehende Verstösse ein; nur neue/geänderte Dateien werden geprüft.

```bash
# Schritt 1: Baseline anlegen
ainetlinter --config rules.json --path ./src/ --create-baseline baseline.json

# Schritt 2: Lint mit Baseline (nur Neu-Verstösse)
ainetlinter --config rules.json --path ./src/ --baseline baseline.json
```

Bei Checksum-Abweichungen (z. B. nach Behebungen) schreibt derselbe Aufruf die `baseline.json` automatisch neu — kein separater Update-Befehl nötig.

---

## Alle CLI-Flags

| Flag | Typ | Beschreibung |
| :--- | :--- | :--- |
| `--config <pfad>` | string | Pfad zur `rules.json` (erforderlich für Audit) |
| `--path <pfad>` | string | Pfad zur `.slnx`/`.sln`/Verzeichnis |
| `--fix` | bool | Auto-Fixer aktivieren |
| `--baseline <pfad>` | string | Baseline-Datei für Ratchet-Modus. Bei erkannter Checksum-Abweichung wird die Datei automatisch neu geschrieben (kein separater Update-Befehl nötig) |
| `--create-baseline <pfad>` | string | Neue Baseline anlegen |
| `--verbose` | bool | Detaillierte Ausgabe aktivieren |
| `--add-disable-all` | bool | Fügt `// ainetlinter-disable all` in allen Dateien mit Verstößen ein |
| `--remove-disable-all` | bool | Entfernt alle `// ainetlinter-disable all`-Zeilen unter `--path` |
| `--wave-ready` | bool | Zeigt nur Verstöße in Dateien ohne `// ainetlinter-disable all` |
| `--only-changed` | bool | Nur Verstöße in gegenüber der Baseline geänderten Dateien (erfordert `--baseline`) |
| `--no-cache` | bool | Deaktiviert den Analyse-Cache für diesen Lauf |
| `--cache-ttl <minuten>` | int | TTL für Cache-Bereinigung beim Programmstart (Standard 60, `0` = unbegrenzt) |
| `--mcp-server` | bool | Startet den stdio-basierten MCP-Server statt eines Lint-Laufs |
| `--parent-pid <pid>` | int | Überwacht die Parent-PID im MCP-Modus; ohne Angabe automatische Ermittlung |
| `--mcp-project-ttl-minutes <minuten>` | decimal | Idle-TTL der Projektregistry (InvariantCulture, Standard 45 Minuten) |
| `--mcp-max-projects <anzahl>` | int | Maximale Zahl residenter Projekt-Keys (Standard 4) |
| `--daemon-start` | bool | Startet den internen Named-Pipe-Daemonpfad (nicht für externe Client-Registrierungen) |
| `--mcp-daemon-idle-exit-minutes <minuten>` | decimal | Idle-Exit des internen DaemonHosts (Standard 10 Minuten) |
| `--list-rules` | bool | Alle Regeln auflisten (kein `--path` nötig) |
| `--describe-rule <RuleId>` | string | Eine Regel vollständig beschreiben |
| `--search-rules <Begriff>` | string | Regeln durchsuchen |
| `--docs <name>` / `-d <name>` | string | Integrierte Dokumentation ausgeben (Optionen: readme, agent-api, configuration, rationale, roadmap, rules-json, mcp-bootstrap, mcp-rule; case-insensitive) |

## Strukturiertes Error-Format (L9)

Fehlermeldungen sind maschinenlesbar:

```
[ERROR]: <CODE>: <Kurzmeldung>
  context: <Datei oder Schritt>
  hint:    <umsetzbare Empfehlung>
```

### Error-Codes

| Code | Bedeutung |
| :--- | :--- |
| `CONFIG_REQUIRED` | `--config` fehlt (für Audit-Lauf) |
| `CONFIG_NOT_FOUND` | `rules.json` nicht gefunden |
| `CONFIG_INVALID` | `rules.json` nicht parsebar |
| `CONFIG_SMELL` | Konfigurationsgeruch (z. B. zu breite Ausnahmen) |
| `BASELINE_NOT_FOUND` | Baseline-Datei nicht gefunden |
| `BASELINE_INVALID` | Baseline-Datei nicht parsebar |
| `WORKSPACE_DIAGNOSTIC` | MSBuild-Fehler beim Laden des Workspaces |
| `PROJECT_NOT_RESTORED` | Projekt ohne frischen `dotnet restore` (`obj/project.assets.json` fehlt/veraltet) — einmal pro betroffenem Projekt statt tausender Phantom-Dependency-Folgefehler, siehe `rationale.md` §13 |
| `ANALYSIS_FAILED` | Analyse-Laufzeit-Fehler |
| `RESOURCE_NOT_FOUND` | Referenzierte Datei nicht gefunden |
| `DRIFT_DETECTED` | Generierter Inhalt weicht von gespeicherter Datei ab |

### Beispiel

```
[ERROR]: BASELINE_NOT_FOUND: Object reference not set
  context: baseline.json
  hint:    Baseline-Datei mit --create-baseline neu erzeugen.
```

---

## Violations-Output-Format

```markdown
# AiNetLinter - 3 violations

| Regel | Gesamt | Prod | Tests | Struktur |
|---|---:|---:|---:|:---:|
| EnforceSealedClasses | 2 | 2 | 0 | |
| MaxPartialClassFiles | 1 | 1 | 0 | ⚠ |

## Handlungsanweisung
...
**Auto-Fix verfuegbar** fuer markierte Violations [auto-fix]:
  `ainetlinter --path <pfad> --fix`

## Regellegende
### EnforceSealedClasses (2×)
**Warum:** ...
**Fix-Alternativen:** ...

## Violations nach Datei

### Produktion (1 Datei)

#### src/MyClass.cs
- Z.5 EnforceSealedClasses [auto-fix] — Klasse 'Foo' ist nicht sealed.
- Z.10 MaxPartialClassFiles [→ strukturell] — Auf 5 Dateien verteilt.
```

- `[auto-fix]` = automatisch mit `--fix` behebbar
- `[→ strukturell]` = struktureller Verstoß, Details im Abschnitt "Strukturelle Verstöße" gekürzt
- Violations nach Datei sortiert (alphabetisch), innerhalb nach Zeilennummer, aufgeteilt in Produktion und Tests
- Strukturelle Violations (MaxPartialClassFiles, AIContextFootprint) erscheinen zusätzlich im Abschnitt "Strukturelle Verstösse" mit mehrzeiligen Details

---

## MCP-Server-Modus

Neben dem CLI-Batch-Modus kann AiNetLinter auch als **stdio-basierter MCP-Server** gestartet werden, der die Roslyn-basierte Solution-Analyse über granular abfragbare Tools für AI-Coding-Agenten bereitstellt. Server-Start, Tool-Verhalten, Trunkierungs-Format und Error-Reporting werden hier beschrieben. Setup- und Registrierungs-Anleitung: [Docs/integration.md#mcp-server-registrieren](integration.md#mcp-server-registrieren).

### Server-Lifecycle

Der Server läuft als stdio-Transport, gesteuert vom MCP-Host (Claude Code, Cursor, eigene Agent-Loops). Start:

```bash
ainetlinter --mcp-server                         # projectRoot kommt je Tool-Aufruf
ainetlinter --mcp-server --parent-pid <pid>       # optionale explizite Parent-PID
```

Bei Legacy-MCP `initialize` (Handshake) hält der Daemon mehrere Projekt-Keys
resident. Der registrierte `--mcp-server`-Prozess arbeitet dabei als ThinClient:
Er verbindet sich zuerst mit dem Named-Pipe-Daemon und startet genau einen
detached `--daemon-start`, falls kein Endpunkt erreichbar ist. Nach `hello` /
`welcome` werden stdio-Frames ohne MCP-SDK- oder JSON-RPC-Interpretation
weitergereicht; stdout bleibt ausschließlich MCP-Protokoll. Jeder
projektgebundene Tool-Aufruf erhält den absoluten Parameter
`projectRoot`; `get_server_health` darf diesen Filter weglassen. Im Root liegt
`ainetlinter.project.json` mit `solution` und `rules`:

```json
{
  "solution": "src/MeinProjekt.slnx",
  "rules": "rules.json"
}
```

Relative Pfade werden zur Definitionsdatei aufgelöst. Fehlt die Datei oder ist
ein Ziel ungültig, antwortet der adressierte Key mit einem deterministischen
Fehlervertrag statt mit geratenen Defaults. `--path` und `--config` sind im
MCP-Modus harte Fehler und bleiben dem Batch-Modus vorbehalten.

MCP `2026-07-28` verwendet stattdessen `server/discover`: Der Request enthält unter `params._meta` die Protokollversion sowie Client-Info und Client-Capabilities. Nach der Discovery müssen auch Folge-Requests wie `tools/list` diese Metadaten mitsenden. Beide Pfade liefern denselben globalen Instructions-Text.

Der MCP-Server ermittelt ohne zusätzliche Konfiguration die PID des aufrufenden Prozesses und überwacht dessen Lebenszeichen. Sobald der Parent-Prozess beendet oder nicht mehr erreichbar ist, wird der Server-CancellationToken ausgelöst und der Server beendet sich mit Exit-Code `0`. Wrapper-Skripte und Spezialumgebungen können die Ziel-PID mit `--parent-pid <pid>` explizit vorgeben. Die Überwachung verwendet unter Windows `NtQueryInformationProcess`, unter Linux `/proc/<pid>/stat` und unter macOS `getppid()`; ein `--idle-timeout` ist nicht Teil dieser Funktion.

Der interne Start `--daemon-start` startet den `DaemonHost` mit Named-Pipe-
Handshake, geteilter Projektregistry und einer MCP-Session je Pipe-Verbindung.
Nach standardmäßig 10 Minuten ohne Verbindungen, aktive Loads oder Warmups
beendet sich der Host; bis zu zwei zuletzt verwendete Projekte werden aus dem
MRU-Zustand vorgeladen. Ein einzelner unterbrochener, read-only Wire-Abschnitt
kann roh replayed werden; ein zweiter Fehler beendet die Session ohne Schleife.
Readiness und Pipe-Pump besitzen Hänger-Zeitlimits. `AINETLINTER_NO_DAEMON=1`
schaltet ausschließlich für Debugging auf den direkten In-Proc-Stdio-Pfad um.

### System-Log für MCP-Tool-Calls

Der tatsächliche MCP-SDK-Server schreibt standardmäßig genau ein abgeschlossenes
Tool-Call-Event in das bestehende Serilog-System-Log. Das Event enthält `ToolName`,
`DurationMs` und `IsError`; bei einem Fehler kommt `ErrorCode` hinzu. Im Daemon-Modus
trägt das Event zusätzlich `ConnectionId`. Argumente und Response-Payloads werden nicht
geloggt. Der ThinClient reicht die Wire-Frames nur durch und schreibt kein zusätzliches
Tool-Call-Event. Die Funktion kann in `appsettings.json` über `Logging:McpCallLogging`
deaktiviert werden; fehlt der Schlüssel, ist sie aktiviert. Ein anderer JSON-Typ als
Boolean ist ein harter Startfehler.

### Daemon-Pipe-Vertrag (Transport-Grundlage)

Die Pipe-/Handshake-Grundlage liegt unter `Mcp/Daemon/` und wird vom internen
`--daemon-start`-Pfad für den `DaemonHost` verwendet. Der MCP-SDK-Handshake
über `--mcp-server`/stdio bleibt nach außen unverändert; der ThinClient
verdrahtet Connect-or-Start und reicht die Nutzdaten nach dem Pipe-Handshake
opak weiter.

- Der Named-Pipe-Endpunkt lautet ausschließlich
  `ainetlinter.analyzer.v1.<username>` für den aktuellen Windows-Benutzer.
  Der Server erstellt ihn mit `PipeOptions.CurrentUserOnly`; dadurch ist der
  Pipe-Zugriff auf den aktuellen Benutzer begrenzt.
- Jede Pipe-Nachricht ist genau ein JSON-Objekt in einer einzelnen
  newline-delimited-Zeile. Leere, mehrzeilige, ungültige oder nicht-objektartige
  Frames werden abgewiesen. Nach dem Pipe-Level-Handshake werden die MCP-/JSON-
  RPC-Nutzdaten als validierte, aber nicht umgeschriebene Bytes weitergereicht.
- Die Pipe-Level-Nachrichten `hello`, `welcome` und `shutdown` verwenden
  Protokollversion `1`. `welcome` enthält `daemonVersion`,
  `executableVersion`, `processId` und die effektive `configuration` mit
  `maxProjects` und `idleExitMinutes`. `projectRoot` gehört nicht
  in diesen Handshake.
- Eine nicht unterstützte Protokollversion wird mit
  `PROTOCOL_VERSION_UNSUPPORTED` abgewiesen. Bei abweichender
  `executableVersion` darf die Zustandslogik bei null weiteren Verbindungen
  genau eine `shutdown`-Entscheidung erzeugen; bei konkurrierenden oder danach
  folgenden Verbindungen lautet der Fehler `VERSION_CONFLICT`. Damit löst ein
  Versionskonflikt keinen Ping-Pong-Neustart aus.
- Weicht die vom Client gemeldete Konfiguration von der effektiven
  Daemon-Konfiguration ab, wird ein strukturiertes
  `CONFIGURATION_DIVERGENCE`-Warnereignis höchstens einmal je
  Handshake-State-Machine ausgelöst. Es ändert weder `projectRoot` noch die
  Registry-Semantik.
- Jede `DaemonPipeConnection` besitzt ein eigenes Cancellation-Token.
  Disconnect bricht nur die in-flight Lese-/Schreibarbeit dieser Verbindung
  ab; andere Verbindungen und ihr gemeinsamer Warm-State werden vom Transport
  nicht verändert.

Vor jedem Tool-Aufruf prüft der Server per Datei-`mtime` + SHA-256-Hash, ob bekannte Quelldateien seit dem letzten Zugriff geändert wurden, und aktualisiert betroffene Dokumente **inkrementell** über `WithDocumentText` statt eines kompletten Workspace-Reloads.

Wenn ein Projekt-Key nicht geladen werden kann (Solution-Datei fehlt, MSBuild-
Fehler), bleibt der Server trotzdem verfügbar — der adressierte Tool-Call liefert
`PROJECT_LOAD_FAILED` mit Ursprungsmeldung und Restore-Hinweis statt eines Crashs.

### Scope-Hinweis (C#-only)

Der Server schickt bei Legacy-`initialize` und modernem `server/discover` denselben zentralen `ServerInstructions`-Text an den Agent. Er enthält nur globale Regeln: den `projectRoot`-Vertrag, den optionalen Verweis auf den einmaligen Bootstrap über `ainetlinter://agent-guide`, die C#-Symbolgraph-Grenze mit `search_pattern`-Fallback, die Sufficiency-/Truncation-Regel und die `isError`-Policy. Der vollständige Bootstrap wird nicht bei jeder Discovery übertragen. Die vollständigen Tool- und Parameterschemas bleiben in `tools/list`; der Projektstatus steht in der Overview-Resource.

Das Engineering-Budget für diesen globalen Text beträgt 2.557 UTF-8-Bytes und
wird durch Tests mit `Encoding.UTF8.GetByteCount` abgesichert; daraus wird keine
exakte Tokenersparnis abgeleitet.

### Tool-Annotations

`tools/list` enthält für jedes registrierte Tool die vier MCP-Hinweise
`readOnlyHint`, `destructiveHint`, `idempotentHint` und `openWorldHint`. Analyse-,
Symbol-, Metrik- und Health-Abfragen liefern `true/false/true/false`,
`reload_config` `false/false/true/false` und
`report_observability_feedback` `false/false/false/false` (jeweils in der genannten
Reihenfolge). Die Hints beschreiben erwartete Seiteneffekte und die geschlossene
Systemgrenze; sie sind keine Zugriffssteuerung und keine Sicherheitsgarantie und
ersetzen keine Berechtigungs- oder Pfadprüfung. Legacy-`initialize` und modernes `server/discover` übertragen für
`tools/list` dieselben Annotationen.

Die Annotationen vergrößern den gemessenen Legacy-`tools/list`-Payload von 20.836
auf 26.887 UTF-8-Bytes (Delta +6.051 Bytes, Baseline-Messung 2026-08-20; Messung
über `McpPayloadMeasurement`). Der moderne Payload beträgt in derselben Prüfung
27.034 UTF-8-Bytes. Daraus wird keine Tokenersparnis abgeleitet.

### Tool-Referenz

Für jedes projektgebundene Tool ist `projectRoot` der erste Pflichtparameter;
die folgenden Zeilen listen die jeweiligen fachlichen Zusatzparameter. Die
einzige Tool-Ausnahme ist `get_server_health` mit optionalem Filter.

| Tool | Input | Output | C#-only | Trunkierung |
| :--- | :--- | :--- | :--- | :---: |
| `get_namespace_tree` | `project?` (Projektname/Substring), `namespacePrefix?` (Start-Namespace), `depth?` (1-3, Default 1), `includeTypes?` (Default true), `kind?` (class/interface/record/struct/enum/all, Default all), `maxResults?` (Default 50, Cap 200) | Hierarchischer Namespace- und Typ-Baum (3 Zoom-Stufen: Solution-Overview, Namespaces, Typ-Liste mit Datei/Zeile/Sichtbarkeit) | ja | ja |
| `find_symbol` | `namePatterns` (Array von Namens-Mustern, max. 10 pro Call; auch fuer genau einen Namen), `kind?` (Klasse/Methode/Property/Interface), `maxResults?` (Default 50) | Fundstellen als `Datei:Zeile - Kind: Signatur` je Pattern; StructuredContent liefert immer `FindSymbolBatchDto` (`results: [{ namePattern, matches: [...] }]`) | ja | ja |
| `find_references` | `symbolIdentifier` (Datei:Zeile:Spalte, Datei:Zeile ohne Spalte oder qualifizierter Name), `maxResults?` (Default 50), `depth?` (Default 1, hard cap 3) | Alle Aufrufstellen; jede erfolgreiche Tiefe liefert `structuredContent.callSites` plus `completeness` mit Tiefe, Herkunft, besuchten Knoten und getrennten Trunkierungsgründen | ja | ja |
| `get_call_tree` | `symbolIdentifier` (wie `find_references`), `depth?` (Default 2, hard cap 5), `format?` (`ascii` Default oder `mermaid`), `topN?` (Default 10, Fan-Out-Kappung pro Ebene), `direction?` (`incoming` Default, `outgoing` oder `both`) | Echter Aufrufer- oder Aufgerufene-Baum (Eltern-Kind-Struktur) als ASCII-Baum oder Mermaid-`flowchart TD`; `incoming` fragt, wer das Symbol aufruft, `outgoing` fragt, welche Source-Symbole es aufruft, `both` liefert beide Richtungen abwechselnd, damit `topN` nicht eine Richtung vollständig aus der sichtbaren Ebene verdrängt; Traversierung hart begrenzt auf 250 Knoten | ja | ja |
| `get_impact` | `gitRef?` (Git-Commit-Ref; ohne jeden Parameter aufgerufen = Standardfall: uncommittete Änderungen) **oder** `symbolIdentifier?` (exklusiv!), `maxResults?` (Default 50), `depth?` (Default 1, hard cap 3; nur Symbol-Branch, im gesamten Git-Branch (callers UND change-context) wirkungslos), `detailLevel?` (`"callers"` Default oder `"change-context"`, case-insensitive; nur im Git-Diff-Modus, nie zusammen mit `symbolIdentifier`), `maxChangedSymbols?` (Default 20, Cap 100), `maxTestsPerSymbol?` (Default 10, Cap 50) | `callers` (Default): betroffene Call-Sites; der Symbol-Branch verwendet für jede Tiefe dieselbe `callSites`/`completeness`-Struktur wie `find_references`. `change-context`: strukturiertes Objekt mit geänderten Dateien und Symbolen, Call-Sites, statisch zugeordneten Tests, diffbezogenen Violations, empfohlenen `dotnet test`-Befehlen und Completeness-Metadaten (siehe Detailabschnitt unten) | ja | ja |
| `get_type_hierarchy` | `symbolIdentifier` (Datei:Zeile:Spalte, Datei:Zeile ohne Spalte oder qualifizierter Name), `maxResults?` (Default 50, nur für abgeleitete/implementierende Typen) | Basisklassen, implementierte Interfaces (untrunkiert), abgeleitete/implementierende Typen (trunkiert), heuristische DI-Registrierungen (letzte Sektion) | ja | ja (nur abgeleitete/implementierende Typen) |
| `dependency_graph` | `filePath?` (ganze Datei) **oder** `symbolIdentifier?` (ein Typ, engerer Scope, exklusiv!), `direction?` (`incoming`/`outgoing`/`both`, Default `both`), `depth?` (Default 1, hard cap 3, transitiv auf Datei-Ebene, hart begrenzt auf 150 besuchte Dateien), `maxResults?` (Default 50) | Datei-zu-Datei-Abhängigkeitskanten (annotiert mit den zugrunde liegenden Typnamen und Referenzzahl), abgeleitet aus echten `SemanticModel`-Typreferenzen statt `using`-Direktiven; optional Projekt-Referenzen des Zielprojekts | ja | ja |
| `get_file_skeleton` | `filePaths` (Array von Pfaden fuer Batch in 1 Turn; auch fuer genau eine Datei, relativ oder absolut) | Struktur-Skelett (Typen, Signaturen ohne Bodies, jeweils mit stabiler `id:` für `get_symbol_body`) | ja | nein |
| `get_class_structure` | `symbolIdentifier` (Pflicht: Typname, Datei:Zeile:Spalte oder DocCommentId), `sortBy?` (`lines` [Default], `kind`, `name`), `maxMembers?` (Default 50, Cap 200; bei Überschreitung Truncation-Meta-Zeile und `Truncated: true` im StructuredContent) | Tabellarische Übersicht über alle Member eines Typs (Kind, Name, Visibility, Start-/End-Zeile, Zeilenanzahl, Signatur); bei `record`-Typen werden die Parameter des Primary Constructors als eigene Zeilen (`Kind: PrimaryCtor-Param`) vor den restlichen Membern ausgegeben | ja | nein |
| `get_index_scope` | — | Dateityp-Aufschlüsselung der geladenen Solution | nein | nein |
| `get_hotspots` | `scopeFilter?` (Projekt-Name oder solution-relativer Pfad) | `.cs`-Dateien, die ihrem `MaxLineCount`-Limit nahekommen oder es überschreiten; `StructuredContent` enthält nur `critical`/`warning`-Dateien (kein `ok`-Eintrag pro Datei — das würde bei einer großen Solution die Antwort unnötig aufblähen) | nein | nein (Text-Report ist per Threshold ohnehin klein) |
| `metrics_tree` | `root?` (Teilbaum, Default Solution-Root), `mode` (`code_size`, `comment_density`, `violation_density`, `complexity`), `depth?` (1-5, Default 1), `topN?` (Default 10), `fileFilter?` (Regex auf den Pfad) | ASCII-Baum mit aggregierten Werten pro Verzeichnisknoten und sortierten Top-N-Kindern je Ebene — `code_size`/`comment_density` sind reiner Datei-Walk (LoC/Bytes bzw. Kommentar-Ratio), `violation_density`/`complexity` laufen über `LinterEngine` bzw. Roslyn-Syntaxbäume (Lint-Verstöße bzw. zyklomatische/kognitive Komplexität je Methode) | nein (zwei der vier Modi sind reiner Datei-Walk) | ja (Top-N pro Ebene) |
| `metrics_lookup` | `symbolIdentifiers` (Array von Symbol-IDs/Namen fuer Batch in 1 Turn; auch fuer genau ein Symbol) | Punktgenaue Metriken (Netto-LOC, zyklomatische/kognitive Komplexität, effektive Parameteranzahl, AI-Context-Footprint, Member-Counts) und Schwellwert-Abgleich gegen aktive `rules.json` für ein oder mehrere C#-Symbole; liefert lesbares Markdown mit Status-Badges (`[OK]`, `[WARN]`, `[VIOLATION]`) und stark typisiertes `MetricsLookupBatchDto` in `structuredContent` | ja | nein |
| `get_feature_context` | `symbol` (Pflicht: Typname, Methode, Property, Datei:Zeile oder DocCommentId), `includeCallers?` (Default `true`), `includeTests?` (Default `true`), `includeMetrics?` (Default `true`), `includeViolations?` (Default `true`), `maxCallers?` (Default 10, Cap 50), `maxTests?` (Default 10, Cap 50) | Composite One-Shot-Exploration für ein C#-Symbol vor Edits/Refactorings: bündelt 5 Dimensionen (Deklaration, Metriken & Budget, direkte Aufrufer, statische Test-Zuordnung und Linter-Violations) in einem einzigen Aufruf; liefert strukturiertes Markdown und typisiertes `FeatureContextPayload` in `structuredContent` | ja | ja |
| `get_test_context` | `symbol` (Pflicht: Typname, Methode, Datei:Zeile oder DocCommentId), `symbolIdentifier?` (Alias), `maxResults?` (Default 30, Cap 100) | Statische Test-Zuordnung für ein C#-Symbol: ermittelt zielgerichtet alle zugeordneten Testdateien, Testklassen, Testmethoden, Test-Kategorien (Unit/Integration), Zuordnungsgründe und direkt ausführbare `dotnet test` Filterbefehle; liefert strukturiertes Markdown und typisiertes `TestContextPayload` in `structuredContent` | ja | ja |
| `get_violations` | `projectRoot` (Pflicht), `scopeFilter?`, `maxResults?` (Default 50), `contextLines?` (0-5, Default 2), `includeSnippet?` (Default `false`) | Aktuelle Lint-Verstöße für den adressierten Projekt-Key inkl. Regel-ID und optionalen Quellcode-Snippets | ja | ja |
| `safeguard` | `scopeFilter?` (Projekt-Name oder solution-relativer Pfad), `minScore?` (Default 8.0), `maxViolations?` (Default 20) | Structured JSON (siehe unten): deterministischer 0-10-Quality-Score, Pass/Fail gegen `minScore`, Top-Violations, strukturierter Remediation-Hint | ja | nein |
| `pattern_detect` | `patterns?` (Default: alle 6 — god-class, async-void, long-method, public-without-doc, empty-catch, feature-envy), `scopeFilter?` (Projekt-Name oder solution-relativer Pfad), `maxResultsPerPattern?` (Default 20) | Structured JSON + Text: Lint-Verstöße nach Pattern-Kategorie gruppiert statt flacher Datei-Liste (siehe unten) | ja | ja (je Pattern) |
| `find_magic_values` | `scopeFilter?` (Projekt-Name oder Pfad-Substring), `valueType?` (`all` Default / `strings` / `numbers`), `categoryFilter?` (`all` Default / `config_candidates` / `constant_candidates` / `enum_candidates` / `nameof_candidates` / `localization_candidates` / `standard_candidates` / `security_candidates`), `minOccurrences?` (Default 1, auch Einzelvorkommen), `maxResults?` (Default 50), `ignoreNumbers?` (optional), `includeTests?` (Default false; filtert `/Tests/`, `/FastTests/` aus dem relativen Pfad), `includeSuppressed?` (Default false; wirksam via `SyntaxTrivia`-Auswertung am Literal), `changedOnly?` (Default false; nutzt `DiffImpactAnalyzer.RunGitDiff` + `ParseGitDiffHunks`, leere Diffs → 0 Dateien) | Strukturierte Funde (URLs, Pfade, Timeouts, Format-Strings, Schwellenwerte, HTTP-Statuscodes, Buffer-Konstanten, duplizierte `const`-Felder, enum-Kaskaden, `nameof`-Kandidaten, Security-Secrets, User-Facing-Exception-Messages) mit Ziel-Empfehlung (`appsettings.json`, `Constants.cs`, `StatusCodes.StatusXXX…`); alle 7 Heuristik-Kategorien aktiv (siehe unten) | ja | ja |
| `find_dead_code` | `accessibility?` (`private_internal` Default / `all` / `private` / `internal` / `public`), `confidence?` (`both` Default / `high` / `low`), `kind?` (`all` Default / `type` / `class` / `method` / `field` / `property` / `event` / `delegate`), `scopeFilter?`, `includeTests?` (Default `false`), `mode?` (`members` Default / `locals` / `both`), `maxResults?` (Default 50) | Statische Kandidaten für unreferenzierte Typen, Member, Felder, Events oder Locals mit Confidence-Stufe und ausgewiesenen Grenzen der Analyse, etwa bei Reflection, DI, Serializern und Routing | ja | ja |
| `get_symbol_body` | `symbolIdentifiers` (Array stabiler IDs/Namen/Dateizeilen fuer Batch in 1 Turn; auch fuer genau ein Symbol), `maxBodyLines?` (Default 80) | Markdown-Block mit Symbol-Body bzw. -Bodies, getrennt durch Divider, hart gekappt bei `maxBodyLines` mit Ellipse-Indikator | ja | nein (Body) |
| `search_pattern` | `pattern` (Text oder Regex), `isRegex?` (Default `false` = case-insensitive Substring), `maxResults?` (Default 50), `maxFiles?`, `contextLines?`, `maxResponseBytes?`, `scope?`, `includePatterns?`, `excludePatterns?`, `enrichCSharp?` (Default `false`) | Treffer im Dateibestand (alle Dateitypen) mit Match-Bereichen, optionalem Kontext und `completeness`; bei `enrichCSharp=true` zusätzlich `semantic` für sichtbare Treffer geladener C#-Dokumente | nein (Fallback) | ja |
| `reload_config` | `projectRoot` (Pflicht), `configPath?` (optional, Override für diesen Key) | Liest standardmäßig die `rules`-Datei des adressierten Keys neu ein; ein expliziter `configPath` ist ein Hot-Swap-Override. Vorher/Nachher-Zusammenfassung inkl. Delta bei aktivierten Regeln | nein | nein |
| `get_server_health` | `projectRoot?` (optionaler Key-Filter) | Health je Projekt-Key oder als Aggregation: LoadState, Solution/Config-Quelle, LastUsedUtc, Uptime, Refresh-/Staleness-Werte und LastGoodState/LastLoadError | nein | nein |
| `report_observability_feedback` | `feedbackType` (Pflicht), `title` (Pflicht), `description` (Pflicht), `relatedTool?`, `severity?` (Default `medium`), `expectedBehavior?`, `actualBehavior?`, `additionalContext?`, `projectRoot?` | Schreibt Fehlerberichte, unerwartete Ausgaben, False Positives oder Feature-Wünsche von KI-Agenten unbeschränkt ins System-Log zur Analyse (nicht für normale Leermengen wie nicht existierende Symbole); liefert Bestätigung und typisiertes DTO | ja | nein |
| `find_duplicates` | `mode?` (`clone` Default, `refactoring-drift` oder `structural`), `scopeType?` (`all` Default, `production`, `tests`), `minTokens?` (Default aus `rules.json`, 30), `similarityThreshold?` (`exact`/`near`/`fuzzy`, Default `fuzzy` — niedrigste noch angezeigte Stufe, bei `mode=clone` und `mode=structural`), `normalizeIdentifiers?` (Default `false`, nur `mode=clone`), `scopeDir?` (Default Solution-Root), `maxResults?` (Default 20), `helperSymbol?` (Datei:Zeile:Spalte, Datei:Zeile ohne Spalte, stabile DocumentationCommentId oder qualifizierter Name wie bei `find_references`; Pflicht bei `mode=refactoring-drift`, bei `mode=structural` ignoriert) | `mode=clone`: Token-basierte Code-Clone-Detection (Jaccard-N-Gram, Method-Granularität) als transitiv gruppierte Cluster (nicht isolierte Paare), gestaffelt nach exact/near/fuzzy-Ähnlichkeit (inkl. Top-Cluster-Übersicht bei >20 Treffern). `mode=refactoring-drift`: Methoden, die den per `helperSymbol` angegebenen Helper strukturell nachbauen statt ihn aufzurufen ("absence-of-calls"-Heuristik, Murphy-Hill 2005) — als Kandidaten (nicht Verstöße) gelistet, siehe Detail-Abschnitt unten. `mode=structural`: Erkennt semantisch ähnliche Hilfsmethoden anhand eines Roslyn-Strukturprofils und Cosine-Similarity (Typ-4/Intended Duplication), liefert manuell zu prüfende Kandidatencluster mit Strukturprofil-Kurzfassung — keine automatische `DuplicateCode`-Violation, eigene Cosine-Schwellwerte aus `rules.json` (`StructuralDuplicate*Threshold`) | ja | ja |

Die Testinformationen von `get_feature_context`, `get_test_context` und `get_impact` mit `detailLevel="change-context"` (`testAssociations`) sind eine **statische Test-Zuordnung**. Der Scanner führt keine instrumentierte Laufzeit-Coverage durch und liest keine Coverage-Dateien. Der Testbezug sagt daher nicht aus, ob ein Test den Zielpfad tatsächlich ausführt oder Assertions für diesen Pfad enthält.

### Structured Output

Neben dem in der Tabelle oben dokumentierten Text-Output liefern `get_namespace_tree`, `get_violations`, `get_class_structure`, `metrics_lookup`, `get_feature_context`, `get_test_context`, `get_hotspots`, `get_server_health`, `report_observability_feedback`, `get_index_scope`, `find_symbol`, `find_references` (alle erlaubten `depth`-Werte), `get_impact` (Symbol- und Git-Diff-Branch), `dependency_graph` (alle `depth`-Werte), `find_duplicates`, `find_magic_values` und `search_pattern` zusaetzlich ein `structuredContent`-Feld (MCP-Protokoll-Feature) mit denselben Daten als JSON — additiv, ohne den Text-Vertrag zu aendern. Clients, die nur den Text konsumieren, ignorieren das Feld einfach. `safeguard` (siehe unten) ist das Vorbild fuer dieses Muster. `find_references` und der Symbol-Branch von `get_impact` liefern bei jeder erlaubten Tiefe dieselbe strukturierte Transitivantwort; der Git-Diff-Branch von `get_impact` behaelt im Default `detailLevel="callers"` seine bestehende `CallSiteEntry`-Form, mit `detailLevel="change-context"` liefert er stattdessen ein eigenes Payload-Objekt (siehe Detailabschnitt unten).

**`search_pattern` — strukturierte Treffer und C#-Enrichment:** Die gemeinsame sichtbare Match-Liste
liefert `filePath`, 1-basierte `line`-/`matchRanges`-Positionen, unveränderten `lineText`, optional
`contextBefore`/`contextAfter`, `projectName` sowie `completeness`, `scope` und `snapshot`. Bei
`enrichCSharp=false` bleibt `semantic` nicht gesetzt. Bei `true` enthält es `kind`, `resolution`
und, wenn Roslyn eine stabile ID liefert, `symbolId`:

```json
{
  "filePath": "src/App/OrderService.cs",
  "line": 42,
  "matchRanges": [{ "column": 18, "length": 10 }],
  "lineText": "    return await PlaceAsync(order);",
  "projectName": "App",
  "semantic": {
    "kind": "symbol_reference",
    "resolution": "resolved",
    "symbolId": "M:App.OrderService.PlaceAsync"
  }
}
```

`kind` kann `declaration`, `symbol_reference`, `comment`, `string`, `code` oder `unknown` sein.
`resolution` kann `resolved`, `not_applicable`, `unknown`, `ambiguous` oder `unavailable` sein.
Kommentare und String-Literale werden nicht als Symbolreferenzen ausgegeben. Die Anreicherung nutzt
nur eindeutig zuordenbare Dokumente des residenten Roslyn-Snapshots; fehlende Dokumente oder ein
abweichender Snapshot-Zeilentext werden als `unavailable`, mehrdeutige Symbolkandidaten als
`ambiguous` sichtbar. Die lexikalische Treffer-, Scope- und Budgetauswahl sowie der Legacy-Text
bleiben unverändert. Bei Trunkierung oder `unavailable`/`ambiguous` sind Scope-Verfeinerung,
niedrigere Limits oder ein gezielter semantischer Folgeaufruf der vorgesehene nächste Schritt.

**`find_references` / `get_impact` (Symbol-Branch) — transitive Structured Response:** Beide Tools liefern ein JSON-Objekt mit deterministisch sortierten und deduplizierten Treffern. `filePath` ist solution-relativ mit Forward-Slashes; `depth` ist die Traversierungsstufe; `reachedFromSymbolId` ist die stabile `DocumentationCommentId` des in diesem Schritt untersuchten Symbols (bei fehlender ID ein deterministischer qualifizierter Anzeigename). Call-Sites, deren Aufrufer eine lokale Funktion ist, tragen in `reachedFromSymbolId` die eindeutige Sonderform `<ID des einschließenden Members>#lf:<Name>@<Zeile>:<Spalte>` — ohne diesen Sonderfall wuerde die Doc-ID der lokalen Funktion mit der ihres einschliessenden Members kollidieren; der String-Wert aenderte sich dadurch von der (geerbten, mehrdeutigen) Methoden-ID zu einer eindeutigen ID.

```json
{
  "callSites": [
    {
      "filePath": "src/App/OrderService.cs",
      "line": 42,
      "symbolName": "OrderService.PlaceAsync",
      "projectName": "App",
      "depth": 2,
      "reachedFromSymbolId": "M:App.OrderFacade.PlaceAsync"
    }
  ],
  "completeness": {
    "requestedDepth": 2,
    "effectiveDepth": 2,
    "visitedNodeCount": 8,
    "totalCallSiteCount": 14,
    "shownCallSiteCount": 14,
    "truncatedByMaxResults": false,
    "truncatedByNodeLimit": false,
    "depthWasClamped": false
  }
}
```

`totalCallSiteCount` zählt die ungekappte Menge innerhalb des Traversierungs-Hard-Caps; `shownCallSiteCount` zählt die tatsächlich in `callSites` enthaltenen Einträge. `truncatedByMaxResults`, `truncatedByNodeLimit` und `depthWasClamped` sind unabhängig voneinander und können gleichzeitig `true` sein. Die Textantwort wird aus derselben gezeigten Trefferliste formatiert und bleibt für Textclients kompatibel.

**`get_impact` (`detailLevel=change-context`) — Structured Output im Detail:** Der Git-Diff-Zweig liefert bei `detailLevel="change-context"` ein eigenes Payload-Objekt statt der `CallSiteEntry`-Liste des Default-Modus. `StructuredContent` liefert:

```json
{
  "mode": "gitDiff",
  "detailLevel": "change-context",
  "changedFiles": [
    { "filePath": "src/App/OrderService.cs", "ranges": [{ "startLine": 40, "lineCount": 8 }] }
  ],
  "changedSymbols": [
    {
      "documentationCommentId": "M:App.OrderService.PlaceAsync",
      "displayName": "OrderService.PlaceAsync",
      "kind": "Method",
      "accessibility": "Public",
      "projectName": "App",
      "filePath": "src/App/OrderService.cs",
      "startLine": 37,
      "endLine": 61
    }
  ],
  "callSites": [],
  "testAssociations": [
    {
      "symbolId": "M:App.OrderService.PlaceAsync",
      "filePath": "tests/App.Tests/OrderServiceTests.cs",
      "testMethods": ["PlaceAsync_ValidOrder_Persists"],
      "matchReason": "Direct Member Match / Invocation"
    }
  ],
  "violations": [
    { "filePath": "src/App/OrderService.cs", "lineNumber": 44, "ruleName": "...", "severity": "warning", "details": "..." }
  ],
  "recommendedTestCommands": ["dotnet test tests/App.Tests --filter FullyQualifiedName~OrderServiceTests"],
  "completeness": {
    "changedSymbolsTotal": 3,
    "changedSymbolsShown": 3,
    "symbolsTruncated": false,
    "callSitesTruncated": false,
    "testsTruncated": false
  }
}
```

Die Feldnamen sind vertraglich exakt (zentrale CamelCase-Policy, durch Vertragstests gepinnt); `accessibility` ist bewusst ein String (z. B. `"Public"`), keine Zahl. `callSites` verwendet dieselbe `TransitiveCallSiteEntry`-Struktur wie der transitive Abschnitt oben. `matchReason` traegt die getrennten Evidenzarten der statischen Zuordnung in ihren Literal-Formen — `"Direct Member Match / Invocation"`, `"Naming Convention Match"`, `"Explicit @covers Comment"`, `"Direct typeof Reference"` — priorisiert in dieser Reihenfolge.

Vertragsregeln:

- `detailLevel="change-context"` ist nur im Git-Diff-Modus zulaessig. Die Validierung ist case-insensitive; die Kombination mit `symbolIdentifier` und jeder unbekannte `detailLevel`-Wert liefern ein recoverable `INVALID_ARGUMENT` — im Kombinationsfall mit dem Hinweis, fuer den Kontext eines einzelnen Symbols `get_feature_context` zu nutzen. Weglassen, leer oder `"callers"` waehlt das bestehende Call-Site-Verhalten (Default).
- `maxChangedSymbols` (Default 20, Cap 100) und `maxTestsPerSymbol` (Default 10, Cap 50) werden geklemmt: Werte unter 1 laufen auf den jeweiligen Default zurueck, Werte ueber dem Cap auf den Cap. Die Symbol-Kappung sitzt im Analyzer-Kern nach der Symbolermittlung und VOR den teuren Call-Site-, Test- und Violations-Analysen; die Kappungsreihenfolge ist deterministisch (Projekt → Datei → Startzeile → Symbol-ID), weggekappte Symbole erscheinen nirgends in der Antwort, `completeness.changedSymbolsTotal` spiegelt die Zahl vor der Kappung.
- `maxResults` (Default 50) kappet in diesem Modus nur die Symbol-/Violation-Toplisten der Textantwort, nicht das strukturierte Objekt.
- Die Textantwort ist eine kompakte Zusammenfassung (Kennzahlen, Symbol- und Violation-Topliste, empfohlene Befehle). Bei vollstaendigem Ergebnis haengt der Sufficiency-Hinweis an, sonst eine Trunkierungs-Meta-Zeile mit den Kappungsgruenden.
- „Kein Git-Repository oder leerer Diff" ist kein Fehlerfall: das Tool liefert ein leeres, aber vertragsgueltiges Objekt (alle Listen leer, `completeness` mit `0`/`false`).
- `violations` sind bewusst kompakt — ohne Snippets oder Source-Ausschnitte.
- `recommendedTestCommands` ist dedupliziert: genau ein Befehl je betroffenem Testprojekt, dessen Filter die Vereinigung der Trefferklassen des Projekts enthaelt (nur aus den GEZEIGTEN Testtreffern gebaut).

**Dokumentierte Grenzen** des change-context-Modus:

- **Gelöschte Dateien** liefern keine Hunks — der Diff-Parser wertet `+++ /dev/null` nicht aus. Gelöschte Dateien erscheinen daher weder in `changedFiles` noch in `changedSymbols`; das ist eine dokumentierte Grenze, kein Fehlerfall.
- **Umbenennungen:** Mit Git-Rename-Detection landen die Hunks unter dem neuen Pfad; ohne Detection erscheinen Löschung und Neuanlage als getrennte Ereignisse — die Löschseite faellt unter dieselbe Grenze wie gelöschte Dateien.
- **`depth` ist im gesamten Git-Branch wirkungslos** (callers UND change-context); die Tiefe der gelieferten Call-Sites ergibt sich aus dem Traversal-Ergebnis, nicht aus dem Parameter.
- **Die stabile ID** (`documentationCommentId`, `testAssociations[].symbolId`) ist eine `DocumentationCommentId`; fehlt diese, greift ein deterministischer FullyQualified-Fallback. Lokale Funktionen erhalten die ID des einschliessenden Members plus das eindeutige Suffix `#lf:<Name>@<Zeile>:<Spalte>`.
- **Die Testinformationen sind eine statische Zuordnung** (siehe Notiz unter der Tool-Tabelle) — keine Laufzeit-Coverage, keine Coverage-Dateien.
- **Multi-Hunk-Container-Regel:** Die innerste Deklaration wird dateiweit ueber alle Hunks entschieden. Trifft ein Hunk einen Member und ein zweiter Hunk derselben Datei die Deklarationszeile des enthaltenen Typs, erscheint nur der Member.

**`safeguard` — Structured Output im Detail:** Der Score aggregiert deterministisch aus dem aktuellen Solution-Zustand Lint-Violations (gewichtet nach Severity), durchschnittliche Cognitive Complexity und AI-Context-Footprint über alle konkreten Klassen im Scope (relativ zu den `Metrics`-Limits aus `rules.json`) sowie einen Sealed-Klassen-Bonus (falls `EnforceSealedClasses` aktiv ist). Die Top-Einträge in `violations` enthalten jeweils Datei, Zeile, Regel, Severity, Details als Problemtext und konkrete Guidance. `totalViolationCount` zählt alle Violations vor der `maxViolations`-Auswahl; `shownViolationCount` zählt die ausgegebenen Top-Einträge; `violationsTruncated` ist `true`, wenn die Ausgabe wegen `maxViolations` gekürzt wurde. `StructuredContent` liefert:

```json
{
  "passed": true,
  "score": 10.0,
  "threshold": 8.0,
  "violations": [
    { "filePath": "...", "lineNumber": 42, "ruleName": "...", "details": "...", "severity": "warning", "guidance": "..." }
  ],
  "totalViolationCount": 1,
  "shownViolationCount": 1,
  "violationsTruncated": false,
  "remediation": {
    "topIssue": "...",
    "actionableSteps": ["..."],
    "documentationHint": "Docs/configuration.md"
  },
  "summary": "Safeguard-Score: 10.00/10 (Threshold 8.00) — PASS. 1 Verstoß, 178 Klassen analysiert."
}
```

Die Text-Antwort wiederholt die Top-Auswahl als `Top-Befunde` mit den Labels
`Problem`, `Datei`, `Zeile`, `Regel`, `Severity` und `Guidance`. Bei einer
Kürzung nennt die Summary zusätzlich `Top-Auswahl wegen maxViolations` und fordert
für die vollständige Liste zum Aufruf von `get_violations` auf.

`IsError` ist ausschließlich bei einer echten Malfunction `true` (LinterEngine-Fehler oder ein Projekt, das trotz `SupportsCompilation == true` auch nach internen Retries keine Compilation liefert) — ein normaler Score-Output mit `passed: false` ist kein Fehler, sondern das erwartete Quality-Gate-Ergebnis.

**`pattern_detect` — Structured Output im Detail:** Reine Aggregation bereits von der `LinterEngine` erzeugter Lint-Verstöße nach 6 Pattern-Kategorien — kein neuer Detection-Code. Unterstützte Patterns: `god-class` (`AIContextFootprint`/`MaxPublicMembersPerType`/`MaxLineCount`), `async-void` (`BanAsyncVoid`), `long-method` (`MaxMethodLineCount`/`MaxCyclomaticComplexity`/`MaxCognitiveComplexity`), `public-without-doc` (`EnforceXmlDocumentation`), `empty-catch` (`EnforceNoSilentCatch`), `feature-envy` (`AvoidExcessiveMiddleMen` — die nächste existierende Näherung, kein 1:1-Match zum klassischen Feature-Envy-Begriff). Die anderen 4 Patterns (`deep-nesting`, `disposable-not-disposed`, `static-state`, `magic-numbers`) sind bewusst **nicht** Teil dieser Version — sie haben keine existierende Erkennung und würden komplett neue Roslyn-Syntax-Walker mit eigenem False-Positive-Risiko erfordern (eigener, größerer Scope). `StructuredContent` liefert:

```json
{
  "patterns": [
    {
      "id": "god-class",
      "description": "...",
      "occurrences": 3,
      "items": [
        { "filePath": "...", "line": 42, "ruleName": "AIContextFootprint", "details": "..." }
      ]
    }
  ],
  "summary": { "patternsWithHits": 2, "totalOccurrences": 5 }
}
```

Eine Violation gehört immer zu genau einem Pattern (die 6 RuleId-Gruppen überschneiden sich nicht); trifft bei `god-class` mehr als eine Regel auf dieselbe Klasse zu, sind das separate Items (keine Dedupe-Logik, identisch zu `get_violations`). `items` ist je Pattern auf `maxResultsPerPattern` gekappt (Default 20), `occurrences` bleibt die volle Trefferzahl. Ist eine zugrunde liegende Regel (z. B. `BanAsyncVoid`) in `rules.json` deaktiviert, zeigt das zugehörige Pattern automatisch 0 Treffer — kein separater Ein-/Ausschalter in `pattern_detect` selbst (Config-Drift-Vermeidung).

**`dependency_graph` — Structured Output im Detail:** Knoten sind Dateien (Solution-relative Pfade), Kanten sind Datei-zu-Datei, annotiert mit den Typnamen, die den Übergang ausgelöst haben — abgeleitet aus echten `SemanticModel`-Typreferenzen (nicht nur `using`-Direktiven), gefiltert auf Typen, die in der geladenen Solution deklariert sind (BCL-/NuGet-Rauschen ausgeschlossen). `filePath` scannt die ganze Datei (Union aller darin deklarierten Typen), `typeIdentifier` scannt nur die Deklaration dieses einen Typs — enger als die ganze Datei. Ab `depth > 1` traversiert die BFS ausschließlich auf Datei-Ebene (kein Typ-Scope mehr ab Hop 2), zyklische Abhängigkeiten werden über ein Visited-Set abgefangen: eine bereits besuchte Datei wird nicht erneut expandiert, die schließende Kante bleibt aber im Ergebnis sichtbar. `StructuredContent` liefert:

```json
{
  "target": { "kind": "file", "path": "src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs", "typeName": null },
  "direction": "both",
  "edges": [
    { "from": "...", "to": "...", "direction": "outgoing", "typeNames": ["SymbolIdentifierResolver"], "referenceCount": 2 }
  ],
  "projectReferences": [ { "project": "AiNetLinter.IntegrationTests", "references": ["AiNetLinter", "AiNetLinter.TestKit"] } ],
  "truncated": false
}
```

`maxResults` kappt die angezeigten Kanten (Default 50); die Traversierung selbst ist unabhängig davon hart auf 150 besuchte Dateien begrenzt (Scan-Kosten-Grenze bei großen Solutions) — beide Kappungsarten setzen `truncated: true` und unterdrücken den Sufficiency-Hinweis. Projekt-Referenzen (`Project.ProjectReferences` des Zielprojekts) sind eine günstige Zusatz-Sicht, keine vollständige Projekt-Graph-Traversierung; NuGet-Vulnerability-Scanning ist bewusst nicht Teil dieses Tools.

**`find_duplicates` — Structured Output im Detail:** Token-basiertes Clone-Detection (CCFinder/Jaccard-N-Gram-Ansatz, Method-Granularität) über dieselbe `DuplicateDetectionEngine`, die auch der Linter-Checker `DuplicateCode` nutzt. Transitiv ähnliche Methoden (A~B, B~C) werden zu einem Cluster gruppiert statt als isolierte Paare gemeldet, gestaffelt nach `exact` (≥0.95), `near` (≥0.80) und `fuzzy` (≥0.65) Jaccard-Similarity — `similarityThreshold` bestimmt die niedrigste noch angezeigte Stufe (Default `fuzzy` zeigt alles). `StructuredContent` liefert:

```json
{
  "clusters": [
    {
      "bucket": "exact",
      "score": 1.0,
      "members": [
        { "filePath": "...", "line": 42, "signatureName": "MyNamespace.HandlerA.BuildOptions()", "tokenCount": 36 },
        { "filePath": "...", "line": 18, "signatureName": "MyNamespace.HandlerB.BuildOptions()", "tokenCount": 36 }
      ]
    }
  ],
  "summary": { "methodsScanned": 240, "totalClusters": 3, "shownClusters": 3, "truncated": false, "mode": "clone" }
}
```

`minTokens` filtert triviale Methoden (leere `Dispose`/`ToString`-Overrides) heraus; generierte, nicht zum Solution-Quellbereich gehörende sowie vom zentralen Source-Katalog ausgeschlossene Dateien werden nicht fingerprinted. Bei `mode=refactoring-drift` nennt ein abgelehnter Helper den tatsächlich ermittelten Grund (etwa Tokenzahl, Scope oder GeneratedCode) statt einer Liste möglicher Ausschlüsse. `normalizeIdentifiers` (Default `false`) schaltet die Erkennung umbenannter Klone (Type-2) an, indem Identifier-/Literal-Tokens vor dem Vergleich normalisiert werden. `scopeDir` grenzt auf einen Teilbereich ein (case-insensitiver Substring-Abgleich auf den Dateipfad, wie `scopeFilter` bei `get_violations`). `maxResults` kappt die gezeigten Cluster (Default 20, aus `rules.json` überschreibbar) — `truncated: true` unterdrückt den Sufficiency-Hinweis und ergänzt stattdessen eine Trunkierungs-Meta-Zeile.

**`find_duplicates mode=structural` — Structured Output im Detail:** Deterministisches Roslyn-Strukturprofil und Cosine-Similarity (keine Embeddings/Netzwerkzugriffe) zur Erkennung semantisch ähnlicher Hilfsmethoden mit unterschiedlichen Namen und Literalen (Typ-4/Intended Duplication). Das Profil enthält normalisierte Rückgabe-/Parametertypen, Kontrollfluss-Form, aufgelöste Zieltypen bei `switch`/Pattern-Interaktionen sowie grobe Verhaltensmarker (Purity, Literal-Klassen). `similarityThreshold` filtert über eigene Cosine-Schwellwerte aus `rules.json` (`StructuralDuplicateExactThreshold`/`NearThreshold`/`FuzzyThreshold`, Standard 0.90/0.80/0.70) — unabhängig von den Jaccard-`DuplicateCode*Threshold`-Werten. Ergebnisse sind manuell zu prüfende Kandidatencluster, keine automatischen Verstöße. `helperSymbol` wird ignoriert. Kleiner Helper oft nur mit `minTokens` unter dem Lint-Default 30 sichtbar. `StructuredContent` liefert dasselbe Schema wie `mode=clone`, ergänzt um `structureProfile` je Mitglied:

```json
{
  "clusters": [
    {
      "bucket": "near",
      "score": 0.87,
      "members": [
        { "filePath": "src/Tools/GetClassStructureTool.cs", "line": 42, "signatureName": "GetClassStructureTool.GetTypeKindDescription(INamedTypeSymbol)", "tokenCount": 18, "structureProfile": "ret=string; params=INamedTypeSymbol; cf=switch-expr; targets=TypeKind; lits=string; pure; form=switch" },
        { "filePath": "src/Tools/GetNamespaceTreeScanner.cs", "line": 77, "signatureName": "GetNamespaceTreeScanner.DescribeTypeKind(INamedTypeSymbol)", "tokenCount": 16, "structureProfile": "ret=string; params=INamedTypeSymbol; cf=switch-expr; targets=TypeKind; lits=string; pure; form=switch" }
      ]
    }
  ],
  "summary": { "methodsScanned": 312, "totalClusters": 4, "shownClusters": 4, "truncated": false, "mode": "structural" }
}
```

Ergebnisse sind Prüfempfehlungen, keine automatischen Verstöße — `DuplicateCodeChecker`/`safeguard` bleiben auf dem tokenbasierten Verhalten; die höhere False-Positive-Unsicherheit semantischer Ähnlichkeit fließt nicht als Lint-Gate-Verletzung ein.

**`find_duplicates mode=refactoring-drift` — Structured Output im Detail:** Eigenes Response-Schema (nicht `clusters`/`bucket`) — findet Methoden, die strukturell einem per `helperSymbol` benannten Helfer `H` ähneln (Jaccard-Score ≥ `near`-Schwellwert aus `rules.json`, `DuplicateCodeNearThreshold`), ihn aber nachweislich nicht aufrufen ("absence-of-calls"-Heuristik, Murphy-Hill 2005). `helperSymbol` wird wie bei `find_references` aufgelöst (Datei:Zeile:Spalte, Datei:Zeile ohne Spalte, stabile DocumentationCommentId oder qualifizierter Name); löst der Identifikator nicht auf ein Symbol oder mehrdeutig auf, liefert das Tool denselben `SYMBOL_NOT_FOUND`/`AMBIGUOUS_SYMBOL`-Fehler wie `find_references`. Nur gewöhnliche Methoden/lokale Funktionen sind als Helfer zulässig (Konstruktoren/Properties/Felder werden von der zugrunde liegenden Engine nicht fingerprinted) — `similarityThreshold` wird in diesem Modus ignoriert. `StructuredContent` liefert:

```json
{
  "candidates": [
    { "filePath": "...", "line": 42, "signatureName": "MyNamespace.DriftedA.Build()", "tokenCount": 36, "score": 1.0 }
  ],
  "summary": { "helperSymbol": "MyNamespace.OptionsHelper.BuildDefault()", "methodsScanned": 240, "totalCandidates": 2, "shownCandidates": 2, "truncated": false }
}
```

Feldname bewusst `candidates`, nicht `violations` — False-Positive-Budget ist höher als bei `mode=clone` (Ziel < 25 %), weil strukturelle Ähnlichkeit nicht zwingend Refactoring-Drift bedeutet (z. B. mehrere legitime, ähnlich aufgebaute `Dispose()`-Implementierungen). Text und `StructuredContent` benennen das Ergebnis konsistent als Kandidaten zur manuellen Prüfung, nie als automatisch gemeldete Verstöße — anders als `mode=clone` fließt dieser Modus **nicht** in `DuplicateCodeChecker`/`safeguard` ein (On-Demand-only, kein Lint-Gate).

**`find_magic_values` — Structured Output im Detail:** On-Demand-Audit ueber alle `.cs`-Dokumente der Solution. Klassifiziert Literale nach fachlichen Refactoring-Zielen (`config_candidates` fuer URLs/Pfade/Connection-Strings/Timeouts, `constant_candidates` fuer Format-Strings/Schwellenwerte und duplizierte `const`-Felder, `enum_candidates` fuer if-else-/switch-Kaskaden mit ≥ 3 Vergleichen gegen denselben Identifier, `nameof_candidates` fuer String-Literale, die exakt einem Symbol-Namen im Scope entsprechen, `localization_candidates` fuer User-Facing Exception-Messages > 15 Zeichen, `standard_candidates` fuer HTTP-Statuscodes + kontextgebundene Buffer-Konstanten, `security_candidates` fuer hartcodierte Secrets/Credentials via Name- oder Praefix-Heuristik). Trivial-/Attribut-/Index-/Loop-/GetHashCode-Filter verhindern false positives; `ignoreNumbers` ergaenzt die Trivial-Liste um projektspezifische Zahlen (z. B. 24/60/360/1000). `localization_candidates` liefert in der Praxis selten Treffer (heuristisch auf Exception-Konstruktoren mit Message > 15 Zeichen beschraenkt) — Trefferquote ist abhaengig vom Codebase-Stil. `StructuredContent` liefert:

```json
{
  "magicValues": [
    {
      "filePath": "src/AiNetLinter/Api/Controllers/UsersController.cs",
      "line": 42,
      "column": 25,
      "valueType": "string",
      "value": "https://api.example.com/v1",
      "category": "config_candidates",
      "recommendation": "appsettings.json (ApiSettings/BaseUrl o. ae.)",
      "contextHint": "URL-Literal",
      "occurrences": 1
    }
  ],
  "summary": {
    "total": 17,
    "shownOccurrences": 17,
    "byCategoryConfig": 12,
    "byCategoryConstant": 4,
    "byCategoryStandard": 1
  }
}
```

`occurrences` zaehlt identische Literale in derselben Datei (Aggregation ueber `(category, value, filePath)`-Tupel); `minOccurrences` (Default 1 — auch Einzelvorkommen) filtert unterhalb der Schwelle. `byCategory*`-Felder aggregieren ueber alle Kategorie-Treffer (vor `maxResults`-Trunkierung). `truncated: true` wird in der Text-Antwort via `McpTruncation`-Meta-Zeile signalisiert, `summary.shownOccurrences < summary.total` ist die korrespondierende `StructuredContent`-Signalisierung.

**Suppression-Sonderfall (bewusste Ausnahme):** `find_magic_values` unterstuetzt Suppression ueber `// ainetlinter-disable MagicValues` (oder `/* ainetlinter-disable MagicValues */`), allerdings bewusst pro Fundstelle via `SyntaxTrivia` (Leading + Trailing) statt ueber den dateiweiten `SuppressionScanner`. Abweichung von der sonst projektweiten Suppression-Semantik ist gewollt: bei dutzenden Magic-Value-Funden pro Datei waere ein dateiweiter Disable-Kommentar nutzlos (ein einzelner Kommentar wuerde alle Funde der Datei stumm schalten). Diese feinere Granularitaet ist eine bewusste Ausnahme und nicht als Inkonsistenz misszuverstehen — die Knoten-Auswertung am `LiteralExpressionSyntax` laesst sich performant im selben AST-Walk miterledigen. Implementierte Granularitaet: `SingleLineCommentTrivia` und `MultiLineCommentTrivia` mit exaktem Substring `ainetlinter-disable MagicValues` (Block-Kommentare werden ebenfalls ausgewertet, solange der Heuristik-Pfad sauber bleibt). `includeSuppressed: false` ist der wirksame Default; `includeSuppressed: true` zeigt auch stummgeschaltete Funde (kein Heuristik-Unterschied). Andere Regel-Namen (z. B. `// ainetlinter-disable SomeOtherRule`) und dateiweite `// ainetlinter-disable all`-Semantik werden nicht ausgewertet.

Beispiel-Aufruf (JSON-RPC über stdio):

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "find_symbol",
    "arguments": {
      "namePatterns": ["LinterEngine"],
      "maxResults": 5
    }
  }
}
```

### Trunkierungs-Format

Vier Listen-Tools (`find_symbol`, `find_references`, `get_impact`, `search_pattern`) respektieren den `maxResults`-Parameter (Default 50) und hängen bei Überschreitung eine **einheitliche Meta-Zeile** an die Ausgabe an. Zwei semantisch unterschiedliche Meta-Zeilen existieren:

**Listen-Trunkierung** (Treffer-Liste, `McpTruncation.cs:40`):

```
[N Treffer gesamt, M gezeigt — Pattern verfeinern oder maxResults erhöhen]
```

**Datei-Listen-Trunkierung** (Miss-Hint-Fallback in `find_symbol` bei 0 C#-Treffern, `McpTruncation.cs:66`):

```
[N Dateien mit Textfund, M gezeigt — search_pattern fuer Details]
```

Beide Meta-Zeilen sind wortwörtlich aus `src/AiNetLinter/Mcp/McpTruncation.cs` übernommen — der Code ist die Source of Truth.

Ausnahme: der `get_impact`-Zweig `detailLevel="change-context"` respektiert `maxResults` ebenfalls (Default 50), kappet damit aber nur die Symbol-/Violation-Toplisten seiner Textzusammenfassung und haengt bei Trunkierung statt dieser einheitlichen Meta-Zeile eine eigene `[Teilergebnis: …]`-Zeile an (siehe Detailabschnitt oben).

### Miss-Hint (find_symbol Fallback)

Wenn `find_symbol` mit einem Pattern ohne C#-Treffer aufgerufen wird, liefert das Tool eine trunkierte Datei-Liste der Nicht-C#-Treffer mit der Datei-Listen-Meta-Zeile (siehe oben). Empfohlener Folge-Schritt: `search_pattern` mit demselben Pattern aufrufen.

### Resources für Agenten

Für eine neue Integration ist die direkte Resource `ainetlinter://agent-guide`
der erste Einstieg. Sie ist ohne `projectRoot` und damit auch ohne vorhandene
`ainetlinter.project.json` lesbar. Ihr Inhalt entspricht der eingebetteten
Bootstrap-Dokumentation `Docs/mcp-bootstrap.md` und enthält anschließend die
separat eingebettete dauerhafte `AiNetLinter-McpWorkflow.mdc`. Sie beschreibt
Solution-/Regeldatei-Ermittlung, `ainetlinter.project.json`, MCP-Registrierung
und das Kopieren der Regeldatei nach `.agents/rules` oder `.cursor/rules`.
Abruf: `resources/read` mit `{"uri": "ainetlinter://agent-guide"}`. Offline
stehen `ainetlinter --docs mcp-bootstrap` für den Bootstrap und
`ainetlinter --docs mcp-rule` für die dauerhafte Regel zur Verfügung.

Nach dem Bootstrap liefert die Status-Resource `ainetlinter://overview` unter
`ainetlinter://overview?projectRoot=<url-encoded>` bei jedem `resources/read`
eine frisch erzeugte kurze Statuskarte: Projektroot,
geladene Solution, verwendete Regelquelle und nächste Einstiegspunkte. Die
vollständigen Tool- und Parameterschemas stehen in `tools/list`. Beispiel:
`{"uri": "ainetlinter://overview?projectRoot=C%3A%2Frepos%2Fmein-projekt"}`.

Die Resource `ainetlinter://rules{?projectRoot}` liefert für denselben adressierten
Projekt-Key bei jedem `resources/read` eine frisch aus dessen atomarem Config-Snapshot
generierte Markdown-Karte. Sie enthält die Konfigurationsherkunft (eingebaute Defaults
oder aufgelöster `rules.json`-Pfad), aktive und deaktivierte Regeln sowie die effektiven
Metrik-Schwellwerte. Projekt- und Pfad-Overrides werden als vorhandene Muster ausgewiesen;
die konkrete Anwendung erfolgt weiterhin pro Roslyn-Projekt bzw. Datei. Beispiel:
`{"uri": "ainetlinter://rules?projectRoot=C%3A%2Frepos%2Fmein-projekt"}`.
Die Ausgabe spiegelt auch Änderungen wider, die über `reload_config` in denselben
residenten Projekt-Key geladen wurden.

### stdout-Schutz (strukturelle JSON-RPC-Absicherung)

Im MCP-Server-Modus ist `stdout` der Transport-Kanal des JSON-RPC-Protokolls. Bereits ein einziger `Console.WriteLine(...)`-Call aus irgendeiner wiederverwendeten CLI-Klasse wuerde das Framing der gesamten Session zerstoeren, weil die naechste JSON-RPC-Zeile von einem nicht-JSON-Leak praefixiert waere und der MCP-Host den Frame nicht mehr parsen kann.

Der Schutz ist **strukturell**, nicht ueber Disziplin geloest: im MCP-Modus wird statt `LinterConsole` die `McpLintConsole`-Implementierung aktiviert (in `Program.cs` als expliziter Parameter an `McpServerCommand.RunAsync` uebergeben), die `ILintConsole.WriteLine(...)` zwingend nach `stderr` umleitet. Ein unbeabsichtigter `Console.WriteLine`-Call in einer Tool-Implementierung oder einem Helper wuerde weiterhin ein Leak sein, aber der zentrale `ILintConsole`-Pfad ist abgesichert.

Regressions-Schutz: E2E-Framing-Tests in `McpServerCommandJsonRpcFramingTests` spawnen `AiNetLinter.exe` als Subprozess und schreiben Legacy-`initialize` beziehungsweise modernes `server/discover` mit anschließendem `tools/list` manuell auf stdin. Sie prüfen **jede** Zeile auf stdout als gültigen JSON-RPC-Frame (`jsonrpc == "2.0"`), vergleichen Instructions und Toolnamen mit der registrierten Collection und messen Zeichen sowie UTF-8-Bytes. Kein SDK-Parser zwischen Subprozess und Assertions — ein zukünftiger Leak würde als nicht-JSON-Zeile sichtbar.

### Compile-Fehler-Warnhinweis

Wenn die Solution Compile-Fehler in einzelnen Dateien hat, prependieren **9 von 15 Tools** (inkl. `metrics_tree`) einen aggregierten Warnhinweis vor das eigentliche Ergebnis. `pattern_detect` prependet diesen Warnhinweis bewusst nicht (Pattern 1:1 von `get_violations` übernommen, siehe unten):

```
Hinweis: 1 Datei hat Compile-Fehler (M Errors gesamt) — Details siehe get_file_skeleton fuer die betroffenen Dateien.
Hinweis: N Dateien haben Compile-Fehler (M Errors gesamt) — Details siehe get_file_skeleton fuer die betroffenen Dateien.
```

Bei genau einer betroffenen Datei wechselt die Zeile in den Singular (`1 Datei hat`), bei mehreren bleibt es beim Plural (`N Dateien haben`).

`get_file_skeleton` nutzt stattdessen einen **datei-spezifischen** Warnhinweis für die angefragte Datei (mit den ersten 3 Diagnostic-IDs und Messages, weitere mit `+M weitere`). `get_violations` und `pattern_detect` prependen keinen Compile-Warnhinweis **und** surfacen Compile-Fehler auch nicht als eigene Violations/Pattern-Treffer — der zugrunde liegende Lint-Lauf ignoriert sie schlicht. Wer wissen will, ob Compile-Fehler vorliegen, muss eines der anderen 9 Tools nutzen (z. B. `get_index_scope` fuer den aggregierten oder `get_file_skeleton` fuer den datei-spezifischen Warnhinweis).

### Staleness-Invalidierung

`McpCodeGraphServer.GetCurrentSolution()` wird vor **jedem** Tool-Aufruf aufgerufen und prüft pro Document, ob die Datei auf der Platte neuer ist als der zuletzt gesehene `mtime`. Bei Abweichung wird der SHA-256-Hash verglichen, um reine `mtime`-Touchups (z. B. durch einen IDE-Save) zu ignorieren, und nur bei tatsächlicher Inhaltsänderung ein inkrementelles `WithDocumentText`-Update gefahren. **Es findet kein Komplett-Reload des MSBuildWorkspace statt.**

Zusätzlich laufen pro Refresh zwei Erweiterungen:

- **Verzeichnis-Sweep** hängt `.cs`-Dateien, die seit dem Solution-Load neu auf der Platte angelegt wurden, automatisch via `Solution.AddDocument` ein (Filter: `*.cs`, `IsGeneratedPath`-Ausschluss, neues Document landet im ersten passenden Nicht-Test-Projekt bzw. Fallback erstes Projekt). So liefert `find_symbol` auch für gerade erstellten Code Treffer, statt stillschweigend „keine Treffer".
- **Document-Removal** entfernt Documents, deren Datei zwischenzeitlich von der Platte gelöscht wurde, aus dem Solution-Modell (`Solution.RemoveDocument`). So liefert `find_symbol` keine Geister-Treffer auf nicht mehr existente Dateien.

Beide Pfade sind „best-effort": `<Compile Remove=…>`-Ausschlüsse aus `.csproj` werden bewusst nicht gelesen — csproj-Parsing würde den MCP-Server unnötig komplex machen.

### Symbolgraph-Erweiterungen

Drei neue Features erweitern den Symbolgraph um praxisrelevante Hebel:

#### `get_symbol_body` und stabile Symbol-IDs (E.1)

`get_symbol_body` liefert den Source-Body eines oder mehrerer C#-Symbole per stabiler
`DocumentationCommentId` (z. B. `M:AiNetLinter.Mcp.Tools.GetSymbolBodyTool.ExecuteAsync`)
oder per klassischem `Datei:Zeile:Spalte`-Format (Fallback ohne Spalte:
`Datei:Zeile` — bei genau einem quelltext-eigenen Symbol auf der Zeile wird
dieses aufgeloest, bei mehreren liefert das Tool `AMBIGUOUS_SYMBOL` mit
Kandidatenliste analog zur Namensauflösung). 

**Batch-Support:** Über `symbolIdentifiers: ["M:...1", "M:...2"]` können mehrere Symbol-Bodies
in einem **einzigen Turn** geladen werden — spart massiv Roundtrips und Tool-Framing-Overhead.
`maxBodyLines` kappt hart je Symbol (Default 80), die Ausgabe enthaelt einen Ellipse-Indikator plus
Voll-Laengen-Hinweis am Ende. Token-Budget: 15 Zeilen Body statt 500
Zeilen Datei.

`get_file_skeleton` rendert pro Member zusaetzlich einen `id:...`-Marker
in derselben `DocumentationCommentId`-Notation. Damit kann der Agent:

1. `get_file_skeleton` aufrufen, alle relevanten Members + stabile IDs einsammeln.
2. `get_symbol_body` mit einer oder mehreren ausgewaehlten IDs aufrufen (`symbolIdentifiers`), um nur die Bodys genau dieser Member in 1 Turn zu laden.

Die ID ueberlebt Zeilenverschiebungen (solange der Symbol-FQN stabil
bleibt — Refactorings, die den FQN aendern, generieren eine neue ID, by
Design). Overloads werden ueber die voll-qualifizierte Parameter-Signatur
in der ID disambiguiert (`ProcessOrder(int)` vs.
`ProcessOrder(OrderDto)` bekommen unterschiedliche IDs).

#### `depth`-Parameter fuer `find_references` / `get_impact` (E.2)

Beide Tools haben einen optionalen `depth`-Parameter (Default 1, hard
cap 3). `depth = 1` liefert direkte Aufrufstellen; `depth > 1`
loest transitive Aufrufstellen ueber `SymbolFinder.FindReferencesAsync`
und aggregiert sie zu derselben strukturierten `callSites`/`completeness`-
Antwortform. Die Eintraege werden vor der `maxResults`-Kappung dedupliziert
und deterministisch nach Tiefe, Pfad, Zeile und Symbolname sortiert.
`completeness` trennt `maxResults`, das Knotenlimit von 200 besuchten
Symbolen und einen auf 3 gekappten Depth-Wert. Text und StructuredContent
werden aus einer gemeinsamen Aggregation erzeugt.

**Verhaltenskorrektur bei `depth > 1` (nicht nur additive Erweiterung):**
Die Kinder-Expansion enqueued je Referenzlocation das einschliessende
Aufrufer-Member statt der referenzierten Definition. `depth > 1` liefert
seit dieser Korrektur echte mehrstufige Aufruferketten (`A → B → C`) mit
korrekter `Depth`-/`reachedFromSymbolId`-Zuordnung statt faktisch nur
Override-/Interface-Expansion; lokale Funktionen erscheinen dabei als
Reached-From-Knoten mit eindeutigen `#lf:`-IDs. Die Korrektur aendert
Bestandsausgaben und betrifft `find_references` UND den `get_impact`-
Symbol-Branch.

`get_impact` ignoriert `depth` im gesamten Git-Branch (callers und
change-context — es gibt keine Symboltiefe fuer `gitRef`-basierte
Diff-Analyse).

#### DI-Registrierungs-Hinweis in `get_type_hierarchy` (E.3)

`get_type_hierarchy` haengt eine zusaetzliche Sektion

```
DI-Registrierungen (heuristisch, Convention-/Factory-basiertes Scanning nicht abgedeckt):
AddScoped: IReporter, ConsoleReporter (src/Di/Program.cs:9) — AddScoped<IReporter, ConsoleReporter>
...
```

an, sobald die Heuristik mindestens eine Registrierung findet. Die
Heuristik scant alle `.cs`-Dateien per `\b`-Word-Boundary-Regex auf
`AddScoped<...>`, `AddSingleton<...>`, `AddTransient<...>` und filtert
auf Treffer, deren Typ-Parameter den voll-qualifizierten Namen des
Hierarchie-Typs enthalten. Convention-basierte und Factory-basierte
Registrierungen werden bewusst nicht über Reflection erkannt. Bei
0 Treffern wird die Sektion weggelassen.

Wenn die Definitionsdatei auf keine gültige Regeldatei zeigt, wird der Projekt-Key
nicht mit Default-Regeln angelegt. Der Fehler enthält den betroffenen Pfad und
die Bauanleitung; ein `reload_config`-Fehler bleibt recoverable und lässt die
aktive Config unverändert.

Eine fehlende oder ungültige Definition ist über `PROJECT_NOT_INITIALIZED`,
`PROJECT_DEFINITION_INVALID`, `SOLUTION_NOT_FOUND`, `RULES_NOT_FOUND` oder
`RULES_INVALID` sichtbar; es gibt keine Nachbarsuche und keinen stillen Default-
Fallback.

### Error-Reporting

Fehlermeldungen folgen dem bestehenden strukturierten Format auf `stderr` und im Tool-Response-Text:

```
[ERROR]: <CODE>: <Kurzmeldung>
  context: <Datei oder Schritt>
  hint:    <umsetzbare Empfehlung>
```

### Error-Codes im MCP-Kontext

| Code | Bedeutung im MCP-Kontext |
| :--- | :--- |
| `CONFIG_REQUIRED` | `--config` fehlt (für `get_violations`) |
| `CONFIG_NOT_FOUND` | `rules.json` nicht gefunden |
| `CONFIG_INVALID` | `rules.json` nicht parsebar |
| `CONFIG_SMELL` | Konfigurationsgeruch (z. B. zu breite Ausnahmen) |
| `BASELINE_NOT_FOUND` | Baseline-Datei nicht gefunden |
| `BASELINE_INVALID` | Baseline-Datei nicht parsebar |
| `WORKSPACE_DIAGNOSTIC` | Roslyn/MSBuild-Compile-Fehler (auch Defensiv-Wrapper der Tools) |
| `PROJECT_NOT_RESTORED` | Projekt ohne frischen `dotnet restore` — `get_violations`/`safeguard`/`pattern_detect`/`metrics_tree` melden dafür eine Diagnose pro Projekt statt tausender Phantom-Dependency-Folgefehler (`DetectAndBanPhantomDependencies` wird für dieses Projekt unterdrückt), siehe `rationale.md` §13 |
| `ANALYSIS_FAILED` | Analyse-Laufzeit-Fehler |
| `RESOURCE_NOT_FOUND` | Datei/Solution-Pfad nicht gefunden (Server-Start oder `get_file_skeleton`) |
| `DRIFT_DETECTED` | Generierter Inhalt weicht von gespeicherter Datei ab |
| `SYMBOL_NOT_FOUND` | `symbolIdentifier` / `typeIdentifier` löst zu keinem Symbol auf |
| `AMBIGUOUS_SYMBOL` | `symbolIdentifier` löst zu mehreren Symbolen auf (Kandidaten in `context`) |
| `INVALID_ARGUMENT` | Leeres Pattern, ungültige Regex, exklusive Parameter verletzt (`get_impact`), Pflichtparameter fehlt/falsch benannt |
| `PROJECT_ROOT_REQUIRED` | Projektgebundener Tool-Aufruf ohne `projectRoot` |
| `PROJECT_ROOT_INVALID` | `projectRoot` ist kein absoluter Pfad |
| `PROJECT_NOT_INITIALIZED` | Keine `ainetlinter.project.json` im adressierten Projektroot; Antwort enthält ein kopierfähiges Template |
| `PROJECT_DEFINITION_INVALID` | Definitionsdatei ist ungültig oder enthält nicht die Pflichtfelder `solution` und `rules` |
| `SOLUTION_NOT_FOUND` | Die Solution aus der Definitionsdatei existiert nicht |
| `RULES_NOT_FOUND` | Die Regeldatei aus der Definitionsdatei existiert nicht |
| `RULES_INVALID` | Die Regeldatei ist lesbar, aber nicht gültig; es werden keine Default-Regeln geladen |
| `PROJECT_LOAD_FAILED` | Kalt-Load eines Projekt-Keys fehlgeschlagen; der nächste Aufruf versucht den Load erneut |

### Verhalten bei fehlendem oder falsch benanntem Pflichtparameter

Jedes Tool mit einem Pflicht-Identifikator/-Pfad-Parameter (`find_symbol.namePatterns`,
`find_references`/`get_call_tree.symbolIdentifier`, `get_type_hierarchy.typeIdentifier`,
`get_symbol_body.symbolIdentifiers`, `get_file_skeleton.filePaths`, `metrics_lookup.symbolIdentifiers`,
`search_pattern.pattern`, `metrics_tree.mode`, `find_duplicates`-`mode=refactoring-drift`s `helperSymbol`) deklariert diesen
Parameter auf SDK-Ebene als optional (Default `null`), damit ein fehlender oder falsch benannter
Parameter im JSON-RPC-Aufruf (z. B. `symbolIdentifier` statt des von `get_type_hierarchy`
erwarteten `typeIdentifier`) nicht schon vor Erreichen des Tool-Codes an der Argument-Bindung
scheitert. Der Tool-Code selbst prüft den Parameter danach explizit auf `null`/leer und liefert bei
Verletzung ein reguläres `[ERROR]: INVALID_ARGUMENT`-Ergebnis (`isError = false`, siehe
Error-Codes-Tabelle) mit einem Hint, der den korrekten Parameternamen und das erwartete Format
nennt — kein Server-Crash und keine rohe SDK-Fehlermeldung. Die je Tool bewusst unterschiedlichen
Parameternamen (semantisch passend zum jeweiligen Identifikator-Typ) bleiben davon unberührt.

### Verhalten bei nicht-ladbarer Solution

Schlägt der Kalt-Load eines Projekt-Keys fehl, bleibt der Transport verfügbar.
Der adressierte Tool-Aufruf liefert `PROJECT_LOAD_FAILED` mit Ursprungsmeldung
und Restore-Hinweis; der FAILED-Marker wird nicht negativ gecacht. Ein späterer
Aufruf versucht den Key erneut. Ein Fehler beim inkrementellen Refresh lässt den
letzten guten Stand resident; Antworten tragen bis zur Heilung einen `[WARN]`-
Kopf und Health meldet `LastGoodStateUtc` sowie `LastLoadError`.

### Drei-Zustands-Lifecycle des MCP-Servers

Der Server-Start entkoppelt den MCP-Transport-Handshake vom Solution-Load: `initialize` antwortet sofort, der eigentliche `MSBuildWorkspace.OpenSolutionAsync`-Aufruf läuft im Hintergrund. Dadurch gibt es drei unterscheidbare Zustände, die sich semantisch klar trennen:

| Zustand | Erkennbar an | Reaktion für den Agent |
| :--- | :--- | :--- |
| **Loading** (transient) | `[INFO]: Server laedt die Solution noch. ...` (kein `isError`) | Kurz warten und erneut versuchen (Polling im Sekunden-Takt). Echte Tool-Ergebnisse erscheinen, sobald der Load abgeschlossen ist. |
| **Loaded** (regulär) | Volle Tool-Antworten, `[ERROR]: ...` nur bei tatsächlichen Problemen | Normale Workflow-Schritte ausführen. |
| **LoadFailed** (für diesen Key) | `[ERROR]: PROJECT_LOAD_FAILED: ...` | Solution-/Build-Ursache prüfen und denselben Projekt-Key erneut aufrufen. |

Der `Loading`-Zustand ist bewusst **kein** Fehler (`isError == false`), weil der Tool-Aufruf nicht falsch war — der Server braucht nur wenige Sekunden für den ersten Solution-Load. MCP-Hosts (Claude Desktop, eigene Test-Harness) erkennen den Info-Text und können den Aufruf nach kurzer Pause wiederholen.

---

## Vollständige Rule-ID-Tabelle

Aktuelle Regel-Liste abrufen:

```bash
ainetlinter --list-rules
```

Auto-fixbare Regeln (`--fix`):
- `EnforceSealedClasses` — `sealed` für konkrete Klassen
- `EnforcePascalCase` — PascalCase für öffentliche Bezeichner
- `EnforceNullableEnable` — `#nullable enable` am Dateianfang

---

> [AiNetLinter](https://github.com/RalfHuesing/AiNetLinter) — Quellcode, Changelog und Issues auf GitHub.
