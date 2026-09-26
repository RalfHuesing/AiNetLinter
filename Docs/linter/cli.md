# AiNetLinter — CLI-Referenz & Workflows

→ [Linter-Konfiguration](configuration.md) | [Linter-Projektintegration](integration.md) | [MCP-Server](../mcp/server.md) | [MCP-Tools](../mcp/tools.md) | [README](../../README.md)

---

## 1. Discovery-Commands

Regeln entdecken und dokumentierte Hintergründe abrufen (kein `--path` erforderlich):

```bash
# Alle Regeln als formatierte Tabelle auflisten:
ainetlinter --list-rules

# Eine Regel vollständig beschreiben (Warum, Alternativen, Auto-Fix):
ainetlinter --describe-rule <RuleId>
# Beispiel:
ainetlinter --describe-rule EnforceSealedClasses

# Regeln nach Stichwort durchsuchen (RuleId, Beschreibung, Intent):
ainetlinter --search-rules <Begriff>
# Beispiele:
ainetlinter --search-rules "komplexitaet"
ainetlinter --search-rules "sealed"
ainetlinter --search-rules "agent"

# Integrierte Dokumentation direkt auf stdout ausgeben:
ainetlinter --docs index
ainetlinter --docs cli
ainetlinter --docs configuration
ainetlinter --docs mcp-tools
```

---

## 2. Aufruf-Syntax

`AiNetLinter` wird als Windows .NET 10 Core CLI-Tool ausgeführt:

```bash
ainetlinter --config <Pfad-zur-ainetlinter-rules.json> --path <Pfad-zur-slnx-oder-Verzeichnis> [Optionen]
```

### Alle CLI-Flags

| Flag | Typ | Beschreibung |
| :--- | :--- | :--- |
| `-c`, `--config <pfad>` | string | Pfad zur `ainetlinter-rules.json` (erforderlich für Audit-Läufe; nicht nötig mit `--create-baseline`) |
| `-p`, `--path <pfad>` | string | Pfad zur Solution-Datei (`.sln` / `.slnx`) oder ein Verzeichnis (erforderlich) |
| `--fix` | bool | Automatische Behebung einfacher Verstöße (z. B. `sealed`, `readonly`, `#nullable enable`) über Roslyn-Syntaxbaum-Transformationen |
| `--baseline <pfad>` | string | Baseline-Datei für Ratchet-Modus. Bei erkannter Checksum-Abweichung wird die Datei automatisch neu geschrieben (kein separater Update-Befehl nötig) |
| `--create-baseline <pfad>` | string | Erzeugt eine Baseline-JSON mit SHA-256-Checksummen aller `.cs`- sowie Web-Dateien (CSS, JS, Razor) |
| `--only-changed` | bool | Nur Verstöße in gegenüber der Baseline geänderten Dateien (erfordert `--baseline`) |
| `--wave-ready` | bool | Zeigt nur Verstöße in Dateien ohne `// ainetlinter-disable all` |
| `--add-disable-all` | bool | Führt einen Audit-Lauf aus und fügt `// ainetlinter-disable all` in betroffenen C#-Dateien ein (erfordert `--config`) |
| `--remove-disable-all` | bool | Entfernt exakte `// ainetlinter-disable all`-Zeilen unter `--path` |
| `--no-cache` | bool | Deaktiviert den Analyse-Cache für diesen Lauf vollständig |
| `--cache-ttl <minuten>` | int | TTL für Cache-Bereinigung beim Programmstart (Standard `60`, `0` = unbegrenzt) |
| `--verbose` | bool | Detaillierte Protokollausgaben aktivieren |
| `--list-rules` | bool | Alle Regeln auflisten (kein `--path` nötig) |
| `--describe-rule <RuleId>` | string | Eine Regel vollständig beschreiben |
| `--search-rules <Begriff>` | string | Regeln durchsuchen |
| `--docs <name>` / `-d <name>` | string | Integrierte Dokumentation ausgeben (`index`, `cli`, `configuration`, `integration`, `mcp-tools`, `mcp-dead-code`, `mcp-server`, `mcp-integration`, `mcp-bootstrap`, `mcp-rule`, `readme`, `rationale`, `ainetlinter-rules-json`; case-insensitive). `index` liefert Einstieg und Folgeaufrufe. |
| `--mcp-server` | bool | Startet den ThinClient des stdio-basierten MCP-Servers statt eines Lint-Laufs |
| `--parent-pid <pid>` | int | Überwacht die Parent-PID im MCP-Modus; ohne Angabe automatische Ermittlung |
| `--mcp-project-ttl-minutes <minuten>` | decimal | Idle-TTL der Projektregistry (InvariantCulture, Standard `45` Minuten) |
| `--mcp-max-projects <anzahl>` | int | Maximale Zahl residenter Projekt-Keys (Standard `4`) |
| `--mcp-external-max-disk-bytes <bytes>` | long | Maximale externe Diskbelegung für Assembly-/Snapshot-Ressourcen |
| `--mcp-external-max-memory-bytes <bytes>` | long | Maximale externe Speicherbelegung für Assembly-/Snapshot-Ressourcen |
| `--mcp-external-max-parallel-operations <anzahl>` | int | Maximale parallele externe Creation-/Materialisierungsoperationen |
| `--mcp-external-max-resident-resources <anzahl>` | int | Maximale Anzahl residenter externer Assembly-/Snapshot-Ressourcen |
| `--mcp-external-idle-ttl-minutes <minuten>` | decimal | Idle-TTL externer Ressourcen (InvariantCulture) |
| `--daemon-start` | bool | Startet den internen Named-Pipe-Daemonpfad (nicht für externe Client-Registrierungen) |
| `--daemon-instance <id>` | string | Isoliert Named-Pipe-Endpunkt, Startup-Gate und MRU-State pro Daemon-Instanz (max. 32 Zeichen, lowercase) |
| `--mcp-daemon-idle-exit-minutes <minuten>` | decimal | Idle-Exit des internen DaemonHosts (Standard `10` Minuten) |

---

## 3. Lint-Workflows

### Startkonfiguration holen
```bash
ainetlinter --docs ainetlinter-rules-json > ainetlinter-rules.json
```
Dumpt die eingebettete Default-Konfiguration — sofort einsatzbereit und lokal anpassbar.

### Workflow 1 — Lint + Fix

```bash
# Schritt 1: Lint-Lauf
ainetlinter --config ainetlinter-rules.json --path ./src/MeinProjekt.slnx

# Schritt 2: Auto-Fixer anwenden
ainetlinter --config ainetlinter-rules.json --path ./src/MeinProjekt.slnx --fix
```

Der Auto-Fixer verarbeitet die Regel-IDs `EnforceSealedClasses`, `EnforceReadonlyFields` und `EnforceNullableEnable`. Er schreibt betroffene Quelldateien und analysiert danach erneut. PascalCase-Umbenennungen sind nicht implementiert; nicht jeder intern unterstützte Fix besitzt einen aktiven Regelproduzenten.

### Baseline: veränderlicher Checksum-Filter

```sh
ainetlinter --path ./MyApp.slnx --create-baseline ainetlinter-baseline.json
ainetlinter --config ainetlinter-rules.json --path ./MyApp.slnx --baseline ainetlinter-baseline.json
```

`--baseline` meldet nur Verstöße in neuen/geänderten Dateien. Bei Abweichungen schreibt der Lauf die gesamte Baseline automatisch neu, auch wenn Verstöße verbleiben. Ein unveränderter Folgelauf kann deshalb bestehen. Das ist kein dauerhaftes Gate gegen bereits bekannte Verstöße. `--only-changed` erfordert `--baseline`; der Baseline-Lauf filtert bereits ohne dieses Zusatzflag.

Baseline: JSON mit `version: 1` und `files` als Map relativer Pfade auf SHA-256-Checksummen. Pfade verwenden `/`; Basis ist das Verzeichnis von `--path` bzw. der übergebenen Solution. `--create-baseline`, `--add-disable-all` und `--remove-disable-all` sind untereinander und mit `--baseline` exklusiv.

### Workflow 3 — Wellen-Workflow (Wave-Ready)

Für schrittweise Freischaltung von Code über Auskommentierung:

```bash
# Schritt 1: Audit ausführen und alle Dateien mit Verstößen per Bulk markieren
ainetlinter --config ainetlinter-rules.json --path ./MeinProjekt.slnx --add-disable-all

# Schritt 2: Nur bereits freigeschaltete Dateien (ohne Disable-all) im CI/Entwicklungsbetrieb prüfen
ainetlinter --config ainetlinter-rules.json --path ./MeinProjekt.slnx --wave-ready

# Schritt 3: Exakte Markierungen unter dem gesamten Ziel entfernen
ainetlinter --path ./MeinProjekt.slnx --remove-disable-all
```

---

## Agentische Nutzung

Für Symbolnavigation und das Source-Gate siehe [MCP-Werkzeugwahl und Verträge](../mcp/tools.md). Dead-Code-Advisories sind separate Prüfkandidaten; sie beeinflussen den Gate-Status nicht.

## 5. Exit-Codes

- `0`: Erfolg (Keine Regelverstöße gefunden).
- `1`: Regelverstöße oder erwarteter Fehler, etwa fehlende/ungültige Konfiguration, Baseline oder unbekanntes Dokument. Zur Unterscheidung stdout/stderr lesen.
- `2`: Unbehandelter Laufzeitfehler, Abbruch oder bestimmter MCP-Startfehler. CLI-Validierungsfehler liefern `1`; jeden Nichtnull-Code als Fehlschlag behandeln.

---

## 6. Ausgabeformate

Alle Dateipfade in der Ausgabe sind **relativ zum `--path`-Argument** (Verzeichnis bzw. übergeordnetes Verzeichnis bei `.sln`/`.slnx`), mit Forward-Slashes.

### Strukturiertes Error-Format (L9)

Fehlermeldungen sind maschinenlesbar strukturiert:

```
[ERROR]: <CODE>: <Kurzmeldung>
  context: <Datei oder Schritt>
  hint:    <umsetzbare Empfehlung>
```

#### Error-Codes

| Code | Bedeutung |
| :--- | :--- |
| `CONFIG_REQUIRED` | `--config` fehlt (für Audit-Lauf) |
| `CONFIG_NOT_FOUND` | `ainetlinter-rules.json` nicht gefunden |
| `CONFIG_INVALID` | `ainetlinter-rules.json` nicht parsebar |
| `CONFIG_SMELL` | Konfigurationsgeruch (z. B. zu breite Ausnahmen) |
| `BASELINE_NOT_FOUND` | Baseline-Datei nicht gefunden |
| `BASELINE_INVALID` | Baseline-Datei nicht parsebar |
| `WORKSPACE_DIAGNOSTIC` | MSBuild-Fehler beim Laden des Workspaces |
| `PROJECT_NOT_RESTORED` | Projekt ohne frischen `dotnet restore` (`obj/project.assets.json` fehlt/veraltet) — einmal pro betroffenem Projekt statt tausender Phantom-Dependency-Folgefehler, siehe [Restore-Erkennung](../rationale.md#restore-erkennung) |
| `ANALYSIS_FAILED` | Analyse-Laufzeit-Fehler |
| `RESOURCE_NOT_FOUND` | Referenzierte Datei nicht gefunden |
| `DRIFT_DETECTED` | Generierter Inhalt weicht von gespeicherter Datei ab |

#### Beispiel

```
[ERROR]: BASELINE_NOT_FOUND: Object reference not set
  context: baseline.json
  hint:    Baseline-Datei mit --create-baseline neu erzeugen.
```

### Violations-Output-Format

Der Linter erzeugt standardmäßig einen detaillierten Markdown-Report auf stdout:

```markdown
# AiNetLinter - 3 violations

| Regel | Gesamt | Prod | Tests | Struktur |
|---|---:|---:|---:|:---:|
| EnforceSealedClasses | 2 | 2 | 0 | |
| MaxPartialClassFiles | 1 | 1 | 0 | ⚠ |

## Handlungsanweisung
...
**Auto-Fix verfuegbar** fuer markierte Violations [auto-fix]:
  `ainetlinter --path <pfad> --config ainetlinter-rules.json --fix`

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
- `[→ strukturell]` = struktureller Verstoß, Details im Abschnitt "Strukturelle Verstöße"
- Violations sind alphabetisch nach Datei und innerhalb nach Zeilennummer sortiert, unterteilt in Produktion und Tests.

---

## 7. Build & Deployment

Um das Tool als eigenständiges, plattformspezifisches CLI-Tool für Windows zu kompilieren:

```bash
dotnet publish src/AiNetLinter/AiNetLinter.csproj -c Release -r win-x64 --self-contained true -o ./publish
```

> [!IMPORTANT]
> **MSBuild-Abhängigkeiten (BuildHost-Ordner):**
> `MSBuildWorkspace` benötigt externe Host-Prozesse. Nach dem Publish müssen zwingend die beiden Ordner `BuildHost-netcore/` und `BuildHost-net472/` im selben Verzeichnis wie `AiNetLinter.exe` liegen. Siehe [Linter-Projektintegration](integration.md#voraussetzungen-und-dateien).

Implementierungsbelege: [CLI-Optionen](../../src/AiNetLinter/Cli/CliOptionFactory.cs), [Validierung](../../src/AiNetLinter/Cli/LinterArgs.cs), [Audit und Baseline](../../src/AiNetLinter/Commands/AuditCommand.cs), [Auto-Fixer](../../src/AiNetLinter/Core/LinterAutoFixer.cs). Dokument-Aliase: `linter-cli`, `linter-config`, `linter-integration`, `agent-api` (= `mcp-tools`).
