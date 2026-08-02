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

Neben dem CLI-Batch-Modus kann AiNetLinter auch als **stdio-basierter MCP-Server** gestartet werden, der die Roslyn-basierte Solution-Analyse als 9 granular abfragbare Tools für AI-Coding-Agenten bereitstellt. Server-Start, Tool-Verhalten, Trunkierungs-Format und Error-Reporting werden hier beschrieben. Setup- und Registrierungs-Anleitung: [Docs/integration.md#mcp-server-registrieren](integration.md#mcp-server-registrieren).

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

> Symbolgraph-Tools (find_symbol, find_references, get_impact, get_type_hierarchy, get_file_skeleton, get_violations) arbeiten ausschliesslich auf C#/.cs-Quellcode. Fuer Namen, die nur in .js, .razor, .cshtml, .xaml, .html oder .css vorkommen, ist search_pattern der passende Fallback. Struktur-Tools ohne C#-Beschraenkung: get_index_scope, get_hotspots.

Konsequenz für den Agent-Loop: 6 Tools sind C#-only (find_symbol, find_references, get_impact, get_type_hierarchy, get_file_skeleton, get_violations), 2 Tools sind Struktur-orientiert und nicht C#-beschränkt (get_index_scope, get_hotspots). `search_pattern` ist der vorgesehene Fallback für Treffer in `.js`/`.razor`/`.cshtml`/`.xaml`/`.html`/`.css` und ist selbst nicht C#-only.

### Die 9 Tools

| Tool | Input | Output | C#-only | Trunkierung |
| :--- | :--- | :--- | :---: | :---: |
| `find_symbol` | `namePattern` (Substring), `kind?` (Klasse/Methode/Property/Interface), `maxResults?` (Default 50) | Fundstellen als `Datei:Zeile - Kind: Signatur` | ja | ja |
| `find_references` | `symbolIdentifier` (Datei:Zeile:Spalte oder qualifizierter Name), `maxResults?` (Default 50) | Alle Aufrufstellen | ja | ja |
| `get_impact` | `gitRef?` (Git-Commit-Ref; leer = uncommittete Änderungen) **oder** `symbolIdentifier?` (exklusiv!), `maxResults?` (Default 50) | Betroffene Call-Sites | ja | ja |
| `get_type_hierarchy` | `typeIdentifier` (Datei:Zeile:Spalte oder qualifizierter Name) | Basisklassen, implementierte Interfaces, abgeleitete Typen | ja | nein |
| `get_file_skeleton` | `filePath` (relativ oder absolut) | Struktur-Skelett (Typen, Signaturen ohne Bodies) | ja | nein |
| `get_index_scope` | — | Dateityp-Aufschlüsselung der geladenen Solution | nein | nein |
| `get_hotspots` | `scopeFilter?` (Projekt-Name oder solution-relativer Pfad) | `.cs`-Dateien, die ihrem `MaxLineCount`-Limit nahekommen oder es überschreiten | nein | nein |
| `get_violations` | `scopeFilter?` (Projekt-Name oder solution-relativer Pfad) | Aktuelle Lint-Verstöße inkl. Regel-ID pro Eintrag | ja | nein |
| `search_pattern` | `pattern` (Text oder Regex), `isRegex?` (Default `false` = case-insensitive Substring), `maxResults?` (Default 50) | Treffer im Dateibestand (alle Dateitypen) | nein (Fallback) | ja |

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

### Compile-Fehler-Warnhinweis (EPIC-06)

Wenn die Solution Compile-Fehler in einzelnen Dateien hat, prependieren **8 von 9 Tools** einen aggregierten Warnhinweis vor das eigentliche Ergebnis:

```
Hinweis: N Dateien haben Compile-Fehler (M Errors gesamt) — Details siehe get_file_skeleton fuer die betroffenen Dateien.
```

`get_file_skeleton` nutzt stattdessen einen **datei-spezifischen** Warnhinweis für die angefragte Datei (mit den ersten 3 Diagnostic-IDs und Messages, weitere mit `+M weitere`). `get_violations` prependet keinen Compile-Warnhinweis — der Lint-Lauf liefert die Compile-Fehler selbst als Violations.

### Staleness-Invalidierung

`McpCodeGraphServer.GetCurrentSolution()` wird vor **jedem** Tool-Aufruf aufgerufen und prüft pro Document, ob die Datei auf der Platte neuer ist als der zuletzt gesehene `mtime`. Bei Abweichung wird der SHA-256-Hash verglichen, um reine `mtime`-Touchups (z. B. durch einen IDE-Save) zu ignorieren, und nur bei tatsächlicher Inhaltsänderung ein inkrementelles `WithDocumentText`-Update gefahren. **Es findet kein Komplett-Reload des MSBuildWorkspace statt.**

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
