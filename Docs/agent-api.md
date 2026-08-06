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

# Codebase-Landkarten generieren:
ainetlinter --map vocabulary --path <pfad>
ainetlinter --map structure --path <pfad>
ainetlinter --map hotspots --path <pfad> [--config <rules.json>]
ainetlinter --map skeleton --path <pfad>
```

### Eval-Befehle (Assembled Audit Prompts)

Generieren vollständige, sofort nutzbare LLM-Audit-Prompts inkl. Evidenz. Erfordern `--path`.

```bash
ainetlinter --list-evals
ainetlinter --eval naming-drift        --path <pfad> [--spec <pfad>...]
ainetlinter --eval architecture-intent --path <pfad> [--spec <pfad>...]
```

**Prompt-Aufbau:** Jede per `--spec` übergebene Datei wird in einen XML-Container
eingebettet (`<doc name="DATEINAME">…</doc>`), sodass Heading-Hierarchien und
`---`-Trennzeichen in Spec-Dateien nicht mit dem Template-Rahmen kollidieren.
Der `{{SPEC}}`-Block im Template ist mit `<specs>…</specs>` ummantelt.

**Token-Warnung:** Überschreitet der assemblierte Prompt ~15.000 Tokens
(Schätzung: `Zeichenanzahl / 4`), gibt das Tool eine Warnung auf `stderr` aus.
Der Prompt wird trotzdem auf `stdout` ausgegeben.

**Output-Format:** Beide Templates enden mit einem Pflicht-Abschnitt der das
Modell zu einer P1/P2/P3-Empfehlungstabelle (Spalten: Priorität, Befund,
Empfehlung, Aufwand) zwingt.

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

# Schritt 3: Dry-Run des Auto-Fixers
ainetlinter --config rules.json --path ./src/MeinProjekt.slnx --fix --dry-run

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

# Schritt 3: Baseline aktualisieren nach Behebungen
ainetlinter --config rules.json --path ./src/ --update-baseline baseline.json
```

---

## Alle CLI-Flags

| Flag | Typ | Beschreibung |
| :--- | :--- | :--- |
| `--config <pfad>` | string | Pfad zur `rules.json` (erforderlich für Audit) |
| `--path <pfad>` | string | Pfad zur `.slnx`/`.sln`/Verzeichnis |
| `--fix` | bool | Auto-Fixer aktivieren |
| `--dry-run` | bool | Fix simulieren, keine Dateien schreiben |
| `--baseline <pfad>` | string | Baseline-Datei für Ratchet-Modus |
| `--create-baseline <pfad>` | string | Neue Baseline anlegen |
| `--update-baseline <pfad>` | string | Baseline nach Behebungen aktualisieren |
| `--verbose` | bool | Detaillierte Ausgabe aktivieren |
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
| `--map <typ>` | string | Codebase-Landkarte generieren (`vocabulary`, `structure`, `hotspots`, `skeleton`) |
| `--eval <name>` | string | Assemblierten Eval-Prompt ausgeben (`naming-drift`, `architecture-intent`) |
| `--list-evals` | bool | Verfügbare Eval-Typen auflisten |
| `--spec <pfad>` | string[] | Spezifikationsquelle für `--eval`: Datei oder Verzeichnis (erste Ebene, nur .md). Mehrfach angebbar. |
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

Neben dem CLI-Batch-Modus kann AiNetLinter auch als **stdio-basierter MCP-Server** gestartet werden, der die Roslyn-basierte Solution-Analyse als 13 granular abfragbare Tools für AI-Coding-Agenten bereitstellt. Server-Start, Tool-Verhalten, Trunkierungs-Format und Error-Reporting werden hier beschrieben. Setup- und Registrierungs-Anleitung: [Docs/integration.md#mcp-server-registrieren](integration.md#mcp-server-registrieren).

### Server-Lifecycle

Der Server läuft als stdio-Transport, gesteuert vom MCP-Host (Claude Code, Cursor, eigene Agent-Loops). Start:

```bash
ainetlinter --mcp-server            # sucht .sln/.slnx im aktuellen Verzeichnis
ainetlinter --mcp-server --path <Datei/Verzeichnis>   # explizite Ziel-Solution
```

Bei `initialize` (Handshake) lädt der Server die Solution einmal via `MSBuildWorkspace` und hält sie über die gesamte Prozesslaufzeit **resident** — Tool-Calls laden die Solution nicht neu. Der Cold-Start (Solution-Load) kann bei großen Solutions spürbar dauern, danach sind Tool-Calls schnell.

Vor jedem Tool-Aufruf prüft der Server per Datei-`mtime` + SHA-256-Hash, ob bekannte Quelldateien seit dem letzten Zugriff geändert wurden, und aktualisiert betroffene Dokumente **inkrementell** über `WithDocumentText` statt eines kompletten Workspace-Reloads.

Wenn beim Start keine Solution geladen werden kann (Solution-Datei fehlt, MSBuild-Fehler), startet der Server trotzdem — jeder Tool-Call liefert dann einen `SOLUTION_NOT_LOADED`-Fehler statt eines Crashs.

### Scope-Hinweis (C#-only)

Der Server schickt beim `initialize`-Handshake folgenden zentralen `ServerInstructions`-Text an den Agent:

> Symbolgraph-Tools (find_symbol, find_references, get_impact, get_type_hierarchy, get_file_skeleton, get_violations, safeguard, get_symbol_body) arbeiten ausschliesslich auf C#/.cs-Quellcode. Fuer Namen, die nur in .js, .razor, .cshtml, .xaml, .html oder .css vorkommen, ist search_pattern der passende Fallback. Struktur-Tools ohne C#-Beschraenkung: get_index_scope, get_hotspots.

Konsequenz für den Agent-Loop: 8 Tools sind C#-only (find_symbol, find_references, get_impact, get_type_hierarchy, get_file_skeleton, get_violations, safeguard, get_symbol_body), 2 Tools sind Struktur-orientiert und nicht C#-beschränkt (get_index_scope, get_hotspots). `search_pattern` ist der vorgesehene Fallback für Treffer in `.js`/`.razor`/`.cshtml`/`.xaml`/`.html`/`.css` und ist selbst nicht C#-only.

### Die 13 Tools

| Tool | Input | Output | C#-only | Trunkierung |
| :--- | :--- | :--- | :---: | :---: |
| `find_symbol` | `namePattern` (Substring), `kind?` (Klasse/Methode/Property/Interface), `maxResults?` (Default 50) | Fundstellen als `Datei:Zeile - Kind: Signatur` | ja | ja |
| `find_references` | `symbolIdentifier` (Datei:Zeile:Spalte oder qualifizierter Name), `maxResults?` (Default 50), `depth?` (Default 1, hard cap 3; >1 = transitive Aufrufstellen, aggregiert) | Alle Aufrufstellen | ja | ja |
| `get_impact` | `gitRef?` (Git-Commit-Ref; ohne jeden Parameter aufgerufen = Standardfall: uncommittete Änderungen) **oder** `symbolIdentifier?` (exklusiv!), `maxResults?` (Default 50), `depth?` (Default 1, hard cap 3; nur Symbol-Branch, Git-Branch ignoriert) | Betroffene Call-Sites | ja | ja |
| `get_type_hierarchy` | `typeIdentifier` (Datei:Zeile:Spalte oder qualifizierter Name) | Basisklassen, implementierte Interfaces, abgeleitete Typen, heuristische DI-Registrierungen (letzte Sektion) | ja | nein |
| `get_file_skeleton` | `filePath` (relativ oder absolut) | Struktur-Skelett (Typen, Signaturen ohne Bodies, jeweils mit stabiler `id:` für `get_symbol_body`) | ja | nein |
| `get_index_scope` | — | Dateityp-Aufschlüsselung der geladenen Solution | nein | nein |
| `get_hotspots` | `scopeFilter?` (Projekt-Name oder solution-relativer Pfad) | `.cs`-Dateien, die ihrem `MaxLineCount`-Limit nahekommen oder es überschreiten | nein | nein |
| `get_violations` | `scopeFilter?` (Projekt-Name oder solution-relativer Pfad) | Aktuelle Lint-Verstöße inkl. Regel-ID pro Eintrag; prependet eine Header-Zeile `Basis: Default-Regeln, keine rules.json gefunden`, wenn der Server ohne `--config` gestartet wurde und keine `rules.json` neben der Solution-Datei findet | ja | nein |
| `safeguard` | `scopeFilter?` (Projekt-Name oder solution-relativer Pfad), `minScore?` (Default 8.0), `maxViolations?` (Default 20) | Structured JSON (siehe unten): deterministischer 0-10-Quality-Score, Pass/Fail gegen `minScore`, Top-Violations, strukturierter Remediation-Hint | ja | nein |
| `get_symbol_body` | `identifier` (stabile DocumentationCommentId oder Datei:Zeile:Spalte oder qualifizierter Name), `maxBodyLines?` (Default 80) | Markdown-Block mit Symbol-Body, hart gekappt bei `maxBodyLines` mit Ellipse-Indikator | ja | nein (Body) |
| `search_pattern` | `pattern` (Text oder Regex), `isRegex?` (Default `false` = case-insensitive Substring), `maxResults?` (Default 50) | Treffer im Dateibestand (alle Dateitypen) | nein (Fallback) | ja |
| `reload_config` | `configPath?` (Default: zuletzt geladener Pfad bzw. frische Auto-Discovery neben der Solution) | Liest die `rules.json` zur Laufzeit neu ein, ohne Server-Neustart; Vorher/Nachher-Zusammenfassung inkl. Delta bei aktivierten Regeln | nein | nein |
| `get_server_health` | — | LoadState, geladene Solution/Config-Quelle, Uptime, Anzahl Solution-Refreshes seit Start, Call-Log-Aggregation (falls `--mcp-log` aktiv) | nein | nein |

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

Beide Meta-Zeilen sind wortwörtlich aus `src/AiNetLinter/Mcp/McpTruncation.cs` übernommen — der Code ist die Source of Truth, nicht das Konzept.

### Miss-Hint (find_symbol Fallback)

Wenn `find_symbol` mit einem Pattern ohne C#-Treffer aufgerufen wird, liefert das Tool eine trunkierte Datei-Liste der Nicht-C#-Treffer mit der Datei-Listen-Meta-Zeile (siehe oben). Empfohlener Folge-Schritt: `search_pattern` mit demselben Pattern aufrufen.

### Resource `ainetlinter://overview`

Neben den 12 Tools stellt der Server eine MCP-Resource bereit — ein bei jedem `resources/read` frisch generiertes Markdown-Dokument mit zwei Teilen:

1. Kurzbeschreibung aller 12 Tools (ein Satz je Tool, keine Parameter-Details — die liefert `tools/list`).
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

### Compile-Fehler-Warnhinweis (EPIC-06)

Wenn die Solution Compile-Fehler in einzelnen Dateien hat, prependieren **8 von 12 Tools** einen aggregierten Warnhinweis vor das eigentliche Ergebnis:

```
Hinweis: 1 Datei hat Compile-Fehler (M Errors gesamt) — Details siehe get_file_skeleton fuer die betroffenen Dateien.
Hinweis: N Dateien haben Compile-Fehler (M Errors gesamt) — Details siehe get_file_skeleton fuer die betroffenen Dateien.
```

Bei genau einer betroffenen Datei wechselt die Zeile in den Singular (`1 Datei hat`), bei mehreren bleibt es beim Plural (`N Dateien haben`).

`get_file_skeleton` nutzt stattdessen einen **datei-spezifischen** Warnhinweis für die angefragte Datei (mit den ersten 3 Diagnostic-IDs und Messages, weitere mit `+M weitere`). `get_violations` prependet keinen Compile-Warnhinweis **und** surfaced Compile-Fehler auch nicht als eigene Violations — der Lint-Lauf ignoriert sie schlicht. Wer wissen will, ob Compile-Fehler vorliegen, muss eines der anderen 8 Tools nutzen (z. B. `get_index_scope` fuer den aggregierten oder `get_file_skeleton` fuer den datei-spezifischen Warnhinweis).

### Staleness-Invalidierung

`McpCodeGraphServer.GetCurrentSolution()` wird vor **jedem** Tool-Aufruf aufgerufen und prüft pro Document, ob die Datei auf der Platte neuer ist als der zuletzt gesehene `mtime`. Bei Abweichung wird der SHA-256-Hash verglichen, um reine `mtime`-Touchups (z. B. durch einen IDE-Save) zu ignorieren, und nur bei tatsächlicher Inhaltsänderung ein inkrementelles `WithDocumentText`-Update gefahren. **Es findet kein Komplett-Reload des MSBuildWorkspace statt.**

Zusätzlich laufen pro Refresh zwei Erweiterungen:

- **Verzeichnis-Sweep** hängt `.cs`-Dateien, die seit dem Solution-Load neu auf der Platte angelegt wurden, automatisch via `Solution.AddDocument` ein (Filter: `*.cs`, `IsGeneratedPath`-Ausschluss, neues Document landet im ersten passenden Nicht-Test-Projekt bzw. Fallback erstes Projekt). So liefert `find_symbol` auch für gerade erstellten Code Treffer, statt stillschweigend „keine Treffer".
- **Document-Removal** entfernt Documents, deren Datei zwischenzeitlich von der Platte gelöscht wurde, aus dem Solution-Modell (`Solution.RemoveDocument`). So liefert `find_symbol` keine Geister-Treffer auf nicht mehr existente Dateien.

Beide Pfade sind „best-effort": `<Compile Remove=…>`-Ausschlüsse aus `.csproj` werden bewusst nicht gelesen (Konzept-Vorgabe).

### Symbolgraph-Erweiterungen (EPIC-08)

Drei neue Features erweitern den Symbolgraph um praxisrelevante Hebel:

#### `get_symbol_body` und stabile Symbol-IDs (E.1)

`get_symbol_body` liefert den Source-Body eines C#-Symbols per stabiler
`DocumentationCommentId` (z. B. `M:AiNetLinter.Mcp.Tools.GetSymbolBodyTool.ExecuteAsync`)
oder per klassischem `Datei:Zeile:Spalte`-Format. `maxBodyLines` kappt
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
cap 3). `depth = 1` liefert direkte Aufrufstellen wie bisher. `depth > 1`
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
Registrierungen werden bewusst NICHT erkannt (Konzept-Vorgabe). Bei
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
| `ANALYSIS_FAILED` | Analyse-Laufzeit-Fehler |
| `RESOURCE_NOT_FOUND` | Datei/Solution-Pfad nicht gefunden (Server-Start oder `get_file_skeleton`) |
| `DRIFT_DETECTED` | Generierter Inhalt weicht von gespeicherter Datei ab |
| `AMBIGUOUS_SOLUTION` | Mehrere `.sln`/`.slnx` im `cwd` ohne `--path` |
| `SOLUTION_NOT_LOADED` | Server startete ohne geladene Solution; Tool-Calls liefern diesen Fehler |
| `SYMBOL_NOT_FOUND` | `symbolIdentifier` / `typeIdentifier` löst zu keinem Symbol auf |
| `AMBIGUOUS_SYMBOL` | `symbolIdentifier` löst zu mehreren Symbolen auf (Kandidaten in `context`) |
| `INVALID_ARGUMENT` | Leeres Pattern, ungültige Regex, exklusive Parameter verletzt (`get_impact`) |

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
