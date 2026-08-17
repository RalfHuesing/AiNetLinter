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

# Schritt 2: Violations pruefen, auto-fixbare erkennen ([auto-fix] im Output)

# Schritt 3: Dry-Run des Auto-Fixers (--check kombiniert mit --fix simuliert, ohne Dateien zu schreiben)
ainetlinter --config rules.json --path ./src/MeinProjekt.slnx --fix --check

# Schritt 4: Fix anwenden
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
| `--check` | bool | Drift-Prüfung (exit 1 bei Abweichung). Kombiniert mit `--fix`: simuliert den Auto-Fixer, ohne Dateien zu schreiben (`[DRY-RUN]`-Ausgabe statt tatsächlicher Änderung) |
| `--add-disable-all` | bool | Fügt `// ainetlinter-disable all` in allen Dateien mit Verstößen ein |
| `--remove-disable-all` | bool | Entfernt alle `// ainetlinter-disable all`-Zeilen unter `--path` |
| `--debt-report` | bool | Tech-Debt-Report (Disable-all nach Ordner, wave-ready Kandidaten) |
| `--wave-ready` | bool | Zeigt nur Verstöße in Dateien ohne `// ainetlinter-disable all` |
| `--only-changed` | bool | Nur Verstöße in gegenüber der Baseline geänderten Dateien (erfordert `--baseline`) |
| `--git-since <ref>` | string | Beschränkt die Analyse auf seit `<ref>` geänderte Dateien (Git) |
| `--footprint <Klasse>` | string | Detaillierte AI-Context-Footprint-Auswertung für eine Klasse (Top-3-Abhängigkeiten) |
| `--no-cache` | bool | Deaktiviert den Analyse-Cache für diesen Lauf |
| `--cache-ttl <minuten>` | int | TTL für Cache-Bereinigung beim Programmstart (Standard 60, `0` = unbegrenzt) |
| `--mcp-server` | bool | Startet den stdio-basierten MCP-Server statt eines Lint-Laufs |
| `--mcp-log [pfad]` | string | Aktiviert das opt-in Call-Log im MCP-Server-Modus |
| `--list-rules` | bool | Alle Regeln auflisten (kein `--path` nötig) |
| `--describe-rule <RuleId>` | string | Eine Regel vollständig beschreiben |
| `--search-rules <Begriff>` | string | Regeln durchsuchen |
| `--docs <name>` / `-d <name>` | string | Integrierte Dokumentation ausgeben (Optionen: readme, agent-api, configuration, rationale, roadmap, rules-json; case-insensitive) |
| `--playbook <pfad>` | string | Repo-Playbook generieren |
| `--sync-agent-rules` | bool | `.agents/rules/AiNetLinter.mdc` im Rahmen eines Linter-Laufs aktualisieren |
| `--sync-agent-rules-only` | bool | Nur `.agents/rules/AiNetLinter.mdc` aktualisieren und Programm sofort beenden (schneller Pfad ohne Lint-Lauf) |
| `--agent-rules-path <pfad>` / `-arp <pfad>` | string | Custom-Pfad (.mdc-Datei oder Verzeichnis) für die Synchronisation der Agent-Regeln (Optional) |
| `--impact <typ>` | string | Impact-Analyse für einen Typ |
| `--debt-report` | bool | Tech-Debt-Report generieren |
| `--check` | bool | Drift-Prüfung (exit 1 bei Abweichung) |
| `--project <muster>` | string[] | Filtert die Analyse auf bestimmte Projektnamen (kommagetrennt, Glob-Muster erlaubt, z. B. `*.Core,*.Domain`) |
| `--exclude-project <muster>` | string[] | Schließt bestimmte Projekte aus (kommagetrennt, Glob-Muster erlaubt, z. B. `*.Tests`) |
| `--namespace <muster>` | string[] | Filtert die Analyse auf bestimmte C#-Namespaces (kommagetrennt, Glob-Muster erlaubt, z. B. `San.Auth*`) |
| `--exclude-namespace <muster>` | string[] | Schließt bestimmte Namespaces aus (kommagetrennt, Glob-Muster erlaubt, z. B. `*.Internal`) |
| `--exclude-tests` | bool | Shortcut, um alle automatisch erkannten Testprojekte auszublenden |
| `--tests-only` | bool | Shortcut, um ausschließlich Testprojekte zu analysieren |
| `--public-only` | bool | Blendet private und protected Member in Maps (wie skeleton) aus, um Token zu sparen |
| `--ignore-suppressions [sprachen...]` | string[] | Code-Unterdrückungen (`disable all` und inline `disable [Rule]`) umgehen (`all`, `cs`/`c#`, `razor`, `js`, `css`). Default: `all`. |

---

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

Neben dem CLI-Batch-Modus kann AiNetLinter auch als **stdio-basierter MCP-Server** gestartet werden, der die Roslyn-basierte Solution-Analyse als 20 granular abfragbare Tools für AI-Coding-Agenten bereitstellt. Server-Start, Tool-Verhalten, Trunkierungs-Format und Error-Reporting werden hier beschrieben. Setup- und Registrierungs-Anleitung: [Docs/integration.md#mcp-server-registrieren](integration.md#mcp-server-registrieren).

### Server-Lifecycle

Der Server läuft als stdio-Transport, gesteuert vom MCP-Host (Claude Code, Cursor, eigene Agent-Loops). Start:

```bash
ainetlinter --mcp-server            # sucht .sln/.slnx im aktuellen Verzeichnis
ainetlinter --mcp-server --path <Datei/Verzeichnis>   # explizite Ziel-Solution
```

Bei `initialize` (Handshake) lädt der Server die Solution einmal via `MSBuildWorkspace` und hält sie über die gesamte Prozesslaufzeit **resident** — Tool-Calls laden die Solution nicht neu. Der Cold-Start (Solution-Load) skaliert mit der Solution-Größe; Tool-Calls arbeiten gegen den resident geladenen Workspace und benötigen keinen erneuten Solution-Load.

Vor jedem Tool-Aufruf prüft der Server per Datei-`mtime` + SHA-256-Hash, ob bekannte Quelldateien seit dem letzten Zugriff geändert wurden, und aktualisiert betroffene Dokumente **inkrementell** über `WithDocumentText` statt eines kompletten Workspace-Reloads.

Wenn beim Start keine Solution geladen werden kann (Solution-Datei fehlt, MSBuild-Fehler), startet der Server trotzdem — jeder Tool-Call liefert dann einen `SOLUTION_NOT_LOADED`-Fehler statt eines Crashs.

### Scope-Hinweis (C#-only)

Der Server schickt beim `initialize`-Handshake folgenden zentralen `ServerInstructions`-Text an den Agent:

> Symbolgraph-Tools (find_symbol, find_references, get_call_tree, get_impact, get_type_hierarchy, dependency_graph, get_file_skeleton, get_class_structure, get_violations, safeguard, pattern_detect, find_magic_values, get_symbol_body, find_duplicates) arbeiten ausschliesslich auf C#/.cs-Quellcode. Fuer Namen, die nur in .js, .razor, .cshtml, .xaml, .html oder .css vorkommen, ist search_pattern der passende Fallback. Struktur-Tools ohne C#-Beschraenkung: get_index_scope, get_hotspots.

Konsequenz für den Agent-Loop: 14 Tools sind C#-only (find_symbol, find_references, get_call_tree, get_impact, get_type_hierarchy, dependency_graph, get_file_skeleton, get_class_structure, get_violations, safeguard, pattern_detect, find_magic_values, get_symbol_body, find_duplicates), 2 Tools sind Struktur-orientiert und nicht C#-beschränkt (get_index_scope, get_hotspots). `search_pattern` ist der vorgesehene Fallback für Treffer in `.js`/`.razor`/`.cshtml`/`.xaml`/`.html`/`.css` und ist selbst nicht C#-only.

### Die 20 Tools

| Tool | Input | Output | C#-only | Trunkierung |
| :--- | :--- | :--- | :---: | :---: |
| `find_symbol` | `namePattern` (Substring), `kind?` (Klasse/Methode/Property/Interface), `maxResults?` (Default 50) | Fundstellen als `Datei:Zeile - Kind: Signatur` | ja | ja |
| `find_references` | `symbolIdentifier` (Datei:Zeile:Spalte, Datei:Zeile ohne Spalte oder qualifizierter Name), `maxResults?` (Default 50), `depth?` (Default 1, hard cap 3; >1 = transitive Aufrufstellen, aggregiert) | Alle Aufrufstellen | ja | ja |
| `get_call_tree` | `symbolIdentifier` (wie `find_references`), `depth?` (Default 2, hard cap 5), `format?` (`ascii` Default oder `mermaid`), `topN?` (Default 10, Fan-Out-Kappung pro Ebene) | Echter Caller-Baum (Eltern-Kind-Struktur) als ASCII-Baum oder Mermaid-`flowchart TD`; Traversierung hart begrenzt auf 250 Knoten | ja | ja |
| `get_impact` | `gitRef?` (Git-Commit-Ref; ohne jeden Parameter aufgerufen = Standardfall: uncommittete Änderungen) **oder** `symbolIdentifier?` (exklusiv!), `maxResults?` (Default 50), `depth?` (Default 1, hard cap 3; nur Symbol-Branch, Git-Branch ignoriert) | Betroffene Call-Sites | ja | ja |
| `get_type_hierarchy` | `typeIdentifier` (Datei:Zeile:Spalte, Datei:Zeile ohne Spalte oder qualifizierter Name), `maxResults?` (Default 50, nur für abgeleitete/implementierende Typen) | Basisklassen, implementierte Interfaces (untrunkiert), abgeleitete/implementierende Typen (trunkiert), heuristische DI-Registrierungen (letzte Sektion) | ja | ja (nur abgeleitete/implementierende Typen) |
| `dependency_graph` | `filePath?` (ganze Datei) **oder** `typeIdentifier?` (ein Typ, engerer Scope, exklusiv!), `direction?` (`incoming`/`outgoing`/`both`, Default `both`), `depth?` (Default 1, hard cap 3, transitiv auf Datei-Ebene, hart begrenzt auf 150 besuchte Dateien), `maxResults?` (Default 50) | Datei-zu-Datei-Abhängigkeitskanten (annotiert mit den zugrunde liegenden Typnamen und Referenzzahl), abgeleitet aus echten `SemanticModel`-Typreferenzen statt `using`-Direktiven; optional Projekt-Referenzen des Zielprojekts | ja | ja |
| `get_file_skeleton` | `filePath` (relativ oder absolut) | Struktur-Skelett (Typen, Signaturen ohne Bodies, jeweils mit stabiler `id:` für `get_symbol_body`) | ja | nein |
| `get_class_structure` | `symbol` (Pflicht: Typname, Datei:Zeile:Spalte oder DocCommentId), `sortBy?` (`lines` [Default], `kind`, `name`), `maxMembers?` (Default 50, Cap 200; bei Überschreitung Truncation-Meta-Zeile und `Truncated: true` im StructuredContent) | Tabellarische Übersicht über alle Member eines Typs (Kind, Name, Visibility, Start-/End-Zeile, Zeilenanzahl, Signatur); bei `record`-Typen werden die Parameter des Primary Constructors als eigene Zeilen (`Kind: PrimaryCtor-Param`) vor den restlichen Membern ausgegeben | ja | nein |
| `get_index_scope` | — | Dateityp-Aufschlüsselung der geladenen Solution | nein | nein |
| `get_hotspots` | `scopeFilter?` (Projekt-Name oder solution-relativer Pfad) | `.cs`-Dateien, die ihrem `MaxLineCount`-Limit nahekommen oder es überschreiten; `StructuredContent` enthält nur `critical`/`warning`-Dateien (kein `ok`-Eintrag pro Datei — das würde bei einer großen Solution die Antwort unnötig aufblähen) | nein | nein (Text-Report ist per Threshold ohnehin klein) |
| `metrics_tree` | `root?` (Teilbaum, Default Solution-Root), `mode` (`code_size`, `comment_density`, `violation_density`, `complexity`), `depth?` (1-5, Default 1), `topN?` (Default 10), `fileFilter?` (Regex auf den Pfad) | ASCII-Baum mit aggregierten Werten pro Verzeichnisknoten und sortierten Top-N-Kindern je Ebene — `code_size`/`comment_density` sind reiner Datei-Walk (LoC/Bytes bzw. Kommentar-Ratio), `violation_density`/`complexity` laufen über `LinterEngine` bzw. Roslyn-Syntaxbäume (Lint-Verstöße bzw. zyklomatische/kognitive Komplexität je Methode) | nein (zwei der vier Modi sind reiner Datei-Walk) | ja (Top-N pro Ebene) |
| `get_violations` | `scopeFilter?` (Projekt-Name oder solution-relativer Pfad), `maxResults?` (Default 50), `contextLines?` (0-5, Default 2), `includeSnippet?` (Default `false`) | Aktuelle Lint-Verstöße inkl. Regel-ID pro Eintrag; optional Quellcode-Snippets via `includeSnippet=true` mit `contextLines` (Snippet zeigt `contextLines` Zeilen davor + verletzende Zeile + `contextLines` Zeilen danach); prependet eine Header-Zeile `Basis: Default-Regeln, keine rules.json gefunden`, wenn der Server ohne `--config` gestartet wurde und keine `rules.json` neben der Solution-Datei findet | ja | ja |
| `safeguard` | `scopeFilter?` (Projekt-Name oder solution-relativer Pfad), `minScore?` (Default 8.0), `maxViolations?` (Default 20) | Structured JSON (siehe unten): deterministischer 0-10-Quality-Score, Pass/Fail gegen `minScore`, Top-Violations, strukturierter Remediation-Hint | ja | nein |
| `pattern_detect` | `patterns?` (Default: alle 6 — god-class, async-void, long-method, public-without-doc, empty-catch, feature-envy), `scopeFilter?` (Projekt-Name oder solution-relativer Pfad), `maxResultsPerPattern?` (Default 20) | Structured JSON + Text: Lint-Verstöße nach Pattern-Kategorie gruppiert statt flacher Datei-Liste (siehe unten) | ja | ja (je Pattern) |
| `find_magic_values` | `scopeFilter?` (Projekt-Name oder Pfad-Substring), `valueType?` (`all` Default / `strings` / `numbers`), `categoryFilter?` (`all` Default / `config_candidates` / `constant_candidates` / `enum_candidates` / `nameof_candidates` / `localization_candidates` / `standard_candidates` / `security_candidates`), `minOccurrences?` (Default 1, auch Einzelvorkommen), `maxResults?` (Default 50), `ignoreNumbers?` (optional), `includeTests?` (Default false; filtert `/Tests/`, `/FastTests/` aus dem relativen Pfad), `includeSuppressed?` (Default false; wirksam via `SyntaxTrivia`-Auswertung am Literal), `changedOnly?` (Default false; nutzt `DiffImpactAnalyzer.RunGitDiff` + `ParseGitDiffHunks`, leere Diffs → 0 Dateien) | Strukturierte Funde (URLs, Pfade, Timeouts, Format-Strings, Schwellenwerte, HTTP-Statuscodes, Buffer/Zeit-Konstanten, duplizierte `const`-Felder, enum-Kaskaden, `nameof`-Kandidaten, Security-Secrets, User-Facing-Exception-Messages) mit Ziel-Empfehlung (`appsettings.json`, `Constants.cs`, `StatusCodes.StatusXXX…`); alle 7 Heuristik-Kategorien aktiv (siehe unten) | ja | ja |
| `get_symbol_body` | `identifier` (stabile DocumentationCommentId, Datei:Zeile:Spalte, Datei:Zeile ohne Spalte oder qualifizierter Name), `maxBodyLines?` (Default 80) | Markdown-Block mit Symbol-Body, hart gekappt bei `maxBodyLines` mit Ellipse-Indikator | ja | nein (Body) |
| `search_pattern` | `pattern` (Text oder Regex), `isRegex?` (Default `false` = case-insensitive Substring), `maxResults?` (Default 50) | Treffer im Dateibestand (alle Dateitypen) | nein (Fallback) | ja |
| `reload_config` | `configPath?` (Default: zuletzt geladener Pfad bzw. frische Auto-Discovery neben der Solution) | Liest die `rules.json` zur Laufzeit neu ein, ohne Server-Neustart; Vorher/Nachher-Zusammenfassung inkl. Delta bei aktivierten Regeln | nein | nein |
| `get_server_health` | — | LoadState, geladene Solution/Config-Quelle, Uptime, Anzahl Solution-Refreshes seit Start, Call-Log-Aggregation (falls `--mcp-log` aktiv) | nein | nein |
| `find_duplicates` | `mode?` (`clone` Default oder `refactoring-drift`), `scopeType?` (`all` Default, `production`, `tests`), `minTokens?` (Default aus `rules.json`, 30), `similarityThreshold?` (`exact`/`near`/`fuzzy`, Default `fuzzy` — niedrigste noch angezeigte Stufe, nur `mode=clone`), `normalizeIdentifiers?` (Default `false`), `scopeDir?` (Default Solution-Root), `maxResults?` (Default 20), `helperSymbol?` (Datei:Zeile:Spalte, Datei:Zeile ohne Spalte, stabile DocumentationCommentId oder qualifizierter Name wie bei `find_references`; Pflicht bei `mode=refactoring-drift`) | `mode=clone`: Token-basierte Code-Clone-Detection (Jaccard-N-Gram, Method-Granularität) als transitiv gruppierte Cluster (nicht isolierte Paare), gestaffelt nach exact/near/fuzzy-Ähnlichkeit (inkl. Top-Cluster-Übersicht bei >20 Treffern). `mode=refactoring-drift`: Methoden, die den per `helperSymbol` angegebenen Helper strukturell nachbauen statt ihn aufzurufen ("absence-of-calls"-Heuristik, Murphy-Hill 2005) — als Kandidaten (nicht Verstöße) gelistet, siehe Detail-Abschnitt unten | ja | ja |

### Structured Output

Neben dem in der Tabelle oben dokumentierten Text-Output liefern `get_violations`, `get_class_structure`, `get_hotspots`, `get_server_health`, `get_index_scope`, `find_symbol`, `find_references` (nur `depth=1`), `get_impact` (Symbol- und Git-Diff-Branch, jeweils `depth=1`), `dependency_graph` (alle `depth`-Werte), `find_duplicates` und `find_magic_values` zusaetzlich ein `structuredContent`-Feld (MCP-Protokoll-Feature) mit denselben Daten als JSON — additiv, ohne den Text-Vertrag zu aendern. Clients, die nur den Text konsumieren, ignorieren das Feld einfach. `safeguard` (siehe unten) ist das Vorbild fuer dieses Muster. Bei `find_references`/`get_impact` mit `depth>1` bleibt `structuredContent` bewusst leer, weil die transitive Traversierung intern keine strukturierten Zwischendaten haelt — `dependency_graph` haelt seine BFS-Kanten dagegen durchgehend als strukturierte `DependencyEdge`-Records (siehe unten), daher bleibt `structuredContent` dort auch bei `depth>1` gefuellt.

**`safeguard` — Structured Output im Detail:** Der Score aggregiert drei Komponenten deterministisch aus dem aktuellen Solution-Zustand — Lint-Violations (gewichtet nach Severity), durchschnittliche Cognitive Complexity und AI-Context-Footprint über alle konkreten Klassen im Scope (relativ zu den `Metrics`-Limits aus `rules.json`), sowie ein Sealed-Klassen-Bonus (falls `EnforceSealedClasses` aktiv ist). `StructuredContent` liefert:

```json
{
  "passed": true,
  "score": 10.0,
  "threshold": 8.0,
  "violations": [
    { "filePath": "...", "lineNumber": 42, "ruleName": "...", "details": "...", "severity": "warning", "guidance": "..." }
  ],
  "remediation": {
    "topIssue": "...",
    "actionableSteps": ["..."],
    "documentationHint": "Docs/configuration.md"
  },
  "summary": "Safeguard-Score: 10.00/10 (Threshold 8.00) — PASS. 0 Top-Verstoesse, 178 Klassen analysiert."
}
```

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
  "summary": { "methodsScanned": 240, "totalClusters": 3, "shownClusters": 3, "truncated": false }
}
```

`minTokens` filtert triviale Methoden (leere `Dispose`/`ToString`-Overrides) heraus; `bin/`, `obj/`, `.ainetlinter/` und `tests/Fixtures/`-Verzeichnisse sowie Methoden mit `[GeneratedCode]`-Attribut sind fest ausgeschlossen. `normalizeIdentifiers` (Default `false`) schaltet die Erkennung umbenannter Klone (Type-2) an, indem Identifier-/Literal-Tokens vor dem Vergleich normalisiert werden. `scopeDir` grenzt auf einen Teilbereich ein (case-insensitiver Substring-Abgleich auf den Dateipfad, wie `scopeFilter` bei `get_violations`). `maxResults` kappt die gezeigten Cluster (Default 20, aus `rules.json` überschreibbar) — `truncated: true` unterdrückt den Sufficiency-Hinweis und ergänzt stattdessen eine Trunkierungs-Meta-Zeile.

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

**`find_magic_values` — Structured Output im Detail:** On-Demand-Audit ueber alle `.cs`-Dokumente der Solution. Klassifiziert Literale nach fachlichen Refactoring-Zielen (`config_candidates` fuer URLs/Pfade/Connection-Strings/Timeouts, `constant_candidates` fuer Format-Strings/Schwellenwerte und duplizierte `const`-Felder, `enum_candidates` fuer if-else-/switch-Kaskaden mit ≥ 3 Vergleichen gegen denselben Identifier, `nameof_candidates` fuer String-Literale, die exakt einem Symbol-Namen im Scope entsprechen, `localization_candidates` fuer User-Facing Exception-Messages > 15 Zeichen, `standard_candidates` fuer HTTP-Statuscodes + Buffer-/Zeit-Konstanten, `security_candidates` fuer hartcodierte Secrets/Credentials via Name- oder Praefix-Heuristik). Trivial-/Attribut-/Index-/Loop-/GetHashCode-Filter verhindern false positives; `ignoreNumbers` ergaenzt die Trivial-Liste um projektspezifische Zahlen (z. B. 24/60/360/1000). `localization_candidates` liefert in der Praxis selten Treffer (heuristisch auf Exception-Konstruktoren mit Message > 15 Zeichen beschraenkt) — Trefferquote ist abhaengig vom Codebase-Stil. `StructuredContent` liefert:

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
      "namePattern": "LinterEngine",
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

### Miss-Hint (find_symbol Fallback)

Wenn `find_symbol` mit einem Pattern ohne C#-Treffer aufgerufen wird, liefert das Tool eine trunkierte Datei-Liste der Nicht-C#-Treffer mit der Datei-Listen-Meta-Zeile (siehe oben). Empfohlener Folge-Schritt: `search_pattern` mit demselben Pattern aufrufen.

### Resource `ainetlinter://overview`

Neben den 20 Tools stellt der Server eine MCP-Resource bereit — ein bei jedem `resources/read` frisch generiertes Markdown-Dokument mit zwei Teilen:

1. Kurzbeschreibung aller 20 Tools (ein Satz je Tool, keine Parameter-Details — die liefert `tools/list`).
2. Aktueller Server-Status: Pfad der geladenen Solution (oder Loading-/LoadFailed-Hinweis) und die tatsaechlich verwendete Regel-Quelle — entweder der Pfad der geladenen `rules.json` oder ein expliziter Hinweis, dass der Server mit eingebauten Default-Regeln laeuft (kein `rules.json` gefunden).

Gedacht als schneller Einstiegspunkt fuer einen Agenten, der den Server noch nicht kennt — der `initialize`-Handshake weist in `ServerInstructions` explizit auf die Resource hin. Abruf: `resources/read` mit `{"uri": "ainetlinter://overview"}`.

### stdout-Schutz (strukturelle JSON-RPC-Absicherung)

Im MCP-Server-Modus ist `stdout` der Transport-Kanal des JSON-RPC-Protokolls. Bereits ein einziger `Console.WriteLine(...)`-Call aus irgendeiner wiederverwendeten CLI-Klasse wuerde das Framing der gesamten Session zerstoeren, weil die naechste JSON-RPC-Zeile von einem nicht-JSON-Leak praefixiert waere und der MCP-Host den Frame nicht mehr parsen kann.

Der Schutz ist **strukturell**, nicht ueber Disziplin geloest: im MCP-Modus wird statt `LinterConsole` die `McpLintConsole`-Implementierung aktiviert (in `Program.cs` als expliziter Parameter an `McpServerCommand.RunAsync` uebergeben), die `ILintConsole.WriteLine(...)` zwingend nach `stderr` umleitet. Ein unbeabsichtigter `Console.WriteLine`-Call in einer Tool-Implementierung oder einem Helper wuerde weiterhin ein Leak sein, aber der zentrale `ILintConsole`-Pfad ist abgesichert.

Regressions-Schutz: ein E2E-Framing-Test in `McpServerCommandJsonRpcFramingTests` spawnt `AiNetLinter.exe` als Subprozess, schreibt `initialize` + `tools/list` + `tools/call`-Frames manuell auf stdin und prueft **jede** Zeile auf stdout als gueltigen JSON-RPC-Frame (`jsonrpc == "2.0"`). Kein SDK-Parser zwischen Subprozess und Assertions — ein zukuenftiger Leak wuerde als nicht-JSON-Zeile sichtbar.

### Call-Log (opt-in)

Opt-in-Beobachtung der tatsaechlichen Tool-Nutzung in der Praxis, default deaktiviert (kein File I/O). Aktivierung ueber das Flag `--mcp-log <pfad>` (oder kurz `-mcp-log`).

```bash
ainetlinter --mcp-server --mcp-log ./.mcp-log/calls.log
ainetlinter --mcp-server --mcp-log  # Default-Pfad: <exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl
```

Format: JSONL, ein Eintrag pro Tool-Call. Felder pro Zeile:

| Feld | Typ | Bedeutung |
| :--- | :--- | :--- |
| `ts` | string (ISO 8601) | UTC-Zeitstempel des Call-Beginns |
| `tool` | string | Tool-Name (z. B. `find_symbol`) |
| `args` | string | Kurzform der Argumente, max. 200 Zeichen + `...` |
| `lines` | number | Anzahl Text-Zeilen im Tool-Result |
| `truncated` | bool | `true` wenn Trunkierungs-Meta-Zeile erkannt |
| `duration_ms` | number | Dauer des Tool-Aufrufs in Millisekunden |
| `empty` | bool | `true` wenn `lines == 0` und kein Fehler |

Beispiel-Snippet:

```json
{"ts":"2026-08-04T11:23:45.123Z","tool":"find_symbol","args":"Greeter|null|50","lines":3,"truncated":false,"duration_ms":12.4,"empty":false}
{"ts":"2026-08-04T11:23:46.456Z","tool":"get_index_scope","args":"","lines":7,"truncated":false,"duration_ms":1.2,"empty":false}
```

**Pfad-Aufloesung:** absoluter Pfad → wie angegeben; relativer Pfad → relativ zum Solution-Verzeichnis (analog zu `cache/` neben der Solution). Default bei `--mcp-log` ohne Wert: `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl` (lokales Server-Datum; `<solutionName>` ist der Dateiname der Solution ohne Extension). Wenn keine Solution auflösbar ist, bricht der Server mit Fehlermeldung auf stderr und Exit-Code 1 ab, es wird keine Log-Datei angelegt. Leere Logs (kein Tool-Call aufgezeichnet) werden beim Server-Shutdown automatisch geloescht.

**Error-Schema (Tool-Handler-Exceptions):** Unbehandelte Exceptions in Tool-Handlern werden in derselben JSONL-Datei als zusaetzliche Zeile mit `level=error` persistiert. Die Felder `ts`, `tool` und `args` sind identisch zum Call-Schema; statt `lines`/`truncated`/`duration_ms`/`empty` traegt der Eintrag:

| Feld | Typ | Bedeutung |
| :--- | :--- | :--- |
| `level` | string | Immer `"error"` fuer diese Zeilen |
| `error_type` | string | Exception-Typ-Name ohne Namespace (z. B. `InvalidOperationException`) |
| `error_message` | string | `Exception.Message` |
| `stack_trace` | string | Stack-Trace, gekappt auf 4 KB + `...`-Marker bei Ueberschreitung |

Beispiel-Snippet:

```json
{"ts":"2026-08-05T09:14:22.011Z","tool":"get_file_skeleton","args":"./src/Foo.cs","level":"error","error_type":"InvalidOperationException","error_message":"simuliertes Hot-Reload-Race in get_file_skeleton","stack_trace":"   at AiNetLinter.Mcp.Tools.FileStructureToolRegistrations.HandleGetFileSkeleton(String path) in FileStructureToolRegistrations.cs:line 142\n   at AiNetLinter.Mcp.Tools.FileStructureToolRegistrations.ExecuteCallAsync(String tool, JsonElement args, McpCallLog log) in FileStructureToolRegistrations.cs:line 67\n..."}
```

Der Wrapper ist ein **Fast-Path**: ohne Flag laeuft der Tool-Dispatch ohne Overhead (kein `McpCallLogScope`-Objekt, kein `Stopwatch.StartNew()`). Siehe `Docs/configuration.md` fuer die formale CLI-Option-Spec.

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

`get_symbol_body` liefert den Source-Body eines C#-Symbols per stabiler
`DocumentationCommentId` (z. B. `M:AiNetLinter.Mcp.Tools.GetSymbolBodyTool.ExecuteAsync`)
oder per klassischem `Datei:Zeile:Spalte`-Format (Fallback ohne Spalte:
`Datei:Zeile` — bei genau einem quelltext-eigenen Symbol auf der Zeile wird
dieses aufgeloest, bei mehreren liefert das Tool `AMBIGUOUS_SYMBOL` mit
Kandidatenliste analog zur Namensauflösung). `maxBodyLines` kappt
hart (Default 80), die Ausgabe enthaelt einen Ellipse-Indikator plus
Voll-Laengen-Hinweis am Ende. Token-Budget: 15 Zeilen Body statt 500
Zeilen Datei.

`get_file_skeleton` rendert pro Member zusaetzlich einen `id:...`-Marker
in derselben `DocumentationCommentId`-Notation. Damit kann der Agent:

1. `get_file_skeleton` aufrufen, alle relevanten Members + stabile IDs einsammeln.
2. `get_symbol_body` mit einer ausgewaehlten ID aufrufen, nur den Body dieses Members holen.

Die ID ueberlebt Zeilenverschiebungen (solange der Symbol-FQN stabil
bleibt — Refactorings, die den FQN aendern, generieren eine neue ID, by
Design). Overloads werden ueber die voll-qualifizierte Parameter-Signatur
in der ID disambiguiert (`ProcessOrder(int)` vs.
`ProcessOrder(OrderDto)` bekommen unterschiedliche IDs).

#### `depth`-Parameter fuer `find_references` / `get_impact` (E.2)

Beide Tools haben einen optionalen `depth`-Parameter (Default 1, hard
cap 3). `depth = 1` liefert direkte Aufrufstellen. `depth > 1`
loest transitive Aufrufstellen ueber `SymbolFinder.FindReferencesAsync`
und aggregiert sie zu einer Top-N-Antwort mit explizitem `depth`-Marker
in der Trunkierungs-Meta-Zeile. Separates Knotenlimit (200) verhindert
exponentielle Explosion bei grossen Symbolgraphen.

`get_impact` ignoriert `depth` im Git-Branch (es gibt keine Symboltiefe
fuer `gitRef`-basierte Diff-Analyse).

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

Wenn der Server ohne `--config` gestartet wurde **und** keine `rules.json` neben der aufgelösten Solution-Datei findet, läuft er mit den `Config`-Defaults. `get_violations` prependet in diesem Fall vor den eigentlichen Lint-Output eine sichtbare Header-Zeile:

```
Basis: Default-Regeln, keine rules.json gefunden
```

Zusätzlich erscheint beim Server-Start ein `[WARN]: Keine rules.json neben der Solution gefunden (…)` auf `stderr`. **Empfehlung an den Agent-Loop:** beim Auftauchen dieser Header-Zeile den Nutzer darauf hinweisen, dass die Lint-Ergebnisse nicht aus der projekteigenen `rules.json` stammen — entweder `args: ["--mcp-server", "--config", "<pfad>"]` setzen oder `rules.json` neben der Solution-Datei anlegen.

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
| `AMBIGUOUS_SOLUTION` | Mehrere `.sln`/`.slnx` im `cwd` ohne `--path` |
| `SOLUTION_NOT_LOADED` | Server startete ohne geladene Solution; Tool-Calls liefern diesen Fehler |
| `SYMBOL_NOT_FOUND` | `symbolIdentifier` / `typeIdentifier` löst zu keinem Symbol auf |
| `AMBIGUOUS_SYMBOL` | `symbolIdentifier` löst zu mehreren Symbolen auf (Kandidaten in `context`) |
| `INVALID_ARGUMENT` | Leeres Pattern, ungültige Regex, exklusive Parameter verletzt (`get_impact`), Pflichtparameter fehlt/falsch benannt |

### Verhalten bei fehlendem oder falsch benanntem Pflichtparameter

Jedes Tool mit einem Pflicht-Identifikator/-Pfad-Parameter (`find_symbol.namePattern`,
`find_references`/`get_call_tree.symbolIdentifier`, `get_type_hierarchy.typeIdentifier`,
`get_symbol_body.identifier`, `get_file_skeleton.filePath`, `search_pattern.pattern`,
`metrics_tree.mode`, `find_duplicates`-`mode=refactoring-drift`s `helperSymbol`) deklariert diesen
Parameter auf SDK-Ebene als optional (Default `null`), damit ein fehlender oder falsch benannter
Parameter im JSON-RPC-Aufruf (z. B. `symbolIdentifier` statt des von `get_type_hierarchy`
erwarteten `typeIdentifier`) nicht schon vor Erreichen des Tool-Codes an der Argument-Bindung
scheitert. Der Tool-Code selbst prüft den Parameter danach explizit auf `null`/leer und liefert bei
Verletzung ein reguläres `[ERROR]: INVALID_ARGUMENT`-Ergebnis (`isError = false`, siehe
Error-Codes-Tabelle) mit einem Hint, der den korrekten Parameternamen und das erwartete Format
nennt — kein Server-Crash und keine rohe SDK-Fehlermeldung. Die je Tool bewusst unterschiedlichen
Parameternamen (semantisch passend zum jeweiligen Identifikator-Typ) bleiben davon unberührt.

### Verhalten bei nicht-ladbarer Solution

Schlägt der `SourceFileCatalog.LoadAsync` beim Server-Start fehl, wird nur ein `[WARN]: MCP-Server startet ohne geladene Solution (...)` auf `stderr` geschrieben, der Server startet trotzdem und jeder Tool-Call liefert einen `SOLUTION_NOT_LOADED`-Fehler (siehe Error-Codes-Tabelle). Der Server stürzt nicht ab.

### Drei-Zustands-Lifecycle des MCP-Servers

Der Server-Start entkoppelt den MCP-Transport-Handshake vom Solution-Load: `initialize` antwortet sofort, der eigentliche `MSBuildWorkspace.OpenSolutionAsync`-Aufruf läuft im Hintergrund. Dadurch gibt es drei unterscheidbare Zustände, die sich semantisch klar trennen:

| Zustand | Erkennbar an | Reaktion für den Agent |
| :--- | :--- | :--- |
| **Loading** (transient) | `[INFO]: Server laedt die Solution noch. ...` (kein `isError`) | Kurz warten und erneut versuchen (Polling im Sekunden-Takt). Echte Tool-Ergebnisse erscheinen, sobald der Load abgeschlossen ist. |
| **Loaded** (regulär) | Volle Tool-Antworten, `[ERROR]: ...` nur bei tatsächlichen Problemen | Normale Workflow-Schritte ausführen. |
| **LoadFailed** (terminal) | `[ERROR]: SOLUTION_NOT_LOADED: ...` | Server-Log auf `[WARN]`-Zeilen prüfen, Pfad/Config korrigieren, Server neu starten. |

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
