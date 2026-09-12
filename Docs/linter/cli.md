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
| `--add-disable-all` | bool | Führt einen Audit-Lauf aus und fügt `// ainetlinter-disable all` in allen Dateien mit Verstößen ein (erfordert `--config`) |
| `--remove-disable-all` | bool | Entfernt exakte `// ainetlinter-disable all`-Zeilen unter `--path` |
| `--no-cache` | bool | Deaktiviert den Analyse-Cache für diesen Lauf vollständig |
| `--cache-ttl <minuten>` | int | TTL für Cache-Bereinigung beim Programmstart (Standard `60`, `0` = unbegrenzt) |
| `--verbose` | bool | Detaillierte Protokollausgaben aktivieren |
| `--list-rules` | bool | Alle Regeln auflisten (kein `--path` nötig) |
| `--describe-rule <RuleId>` | string | Eine Regel vollständig beschreiben |
| `--search-rules <Begriff>` | string | Regeln durchsuchen |
| `--docs <name>` / `-d <name>` | string | Integrierte Dokumentation ausgeben (`cli`, `configuration`, `integration`, `mcp-tools`, `mcp-server`, `mcp-integration`, `mcp-bootstrap`, `mcp-rule`, `readme`, `rationale`, `ainetlinter-rules-json`; case-insensitive) |
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

Automatisch behebbare Regeln: `EnforceSealedClasses`, `EnforcePascalCase`, `EnforceNullableEnable`.

### Workflow 2 — Inkrementelle Migration (Baseline / Ratchet)

**Use-Case:** Bestehende („alte") Projekte mit vielen Verstößen schrittweise auf Qualitätsstandard bringen — ohne Big-Bang-Refactoring und ohne Git-Integration.

```bash
# Schritt 1: Baseline anlegen (alle aktuellen Dateien per SHA-256 einfrieren)
ainetlinter --path ./src/MeinProjekt.slnx --create-baseline ainetlinter-baseline.json

# Schritt 2: Baseline ins Repository committen
git add ainetlinter-baseline.json && git commit -m "chore: add ainetlinter baseline"

# Schritt 3: Regulärer Lauf / CI — nur Verstöße in geänderten Dateien melden
ainetlinter --config ainetlinter-rules.json --path ./src/MeinProjekt.slnx --baseline ainetlinter-baseline.json
```

**Semantik:**

| Zustand | Verhalten |
| :--- | :--- |
| Checksumme identisch mit Baseline | Datei unverändert → Verstöße werden **nicht** gemeldet |
| Checksumme abweichend oder Datei neu | Datei wurde angefasst → Verstöße werden **gemeldet** |
| Irgendeine Abweichung erkannt | Gesamte Baseline-Datei wird automatisch neu geschrieben |

**Weicher Ratchet:** Nach einem Lauf mit geänderten Dateien werden die neuen Checksummen eingefroren — auch wenn noch Verstöße bestehen. Um weitere Verbesserungen zu erzwingen, die Datei erneut bearbeiten.

**Baseline-Format** (relative Pfade mit Forward-Slashes, Basis: `--path`):
```json
{
  "version": 1,
  "files": {
    "src/MyApp/Program.cs": "a1b2c3d4e5f6...",
    "src/MyApp/styles.css": "e7f8a9b0c1d2..."
  }
}
```

### Workflow 3 — Wellen-Workflow (Wave-Ready)

Für schrittweise Freischaltung von Code über Auskommentierung:

```bash
# Schritt 1: Audit ausführen und alle Dateien mit Verstößen per Bulk markieren
ainetlinter --config ainetlinter-rules.json --path ./MeinProjekt.slnx --add-disable-all

# Schritt 2: Nur bereits freigeschaltete Dateien (ohne Disable-all) im CI/Entwicklungsbetrieb prüfen
ainetlinter --config ainetlinter-rules.json --path ./MeinProjekt.slnx --wave-ready

# Schritt 3: Nach Behebung der Verstöße die Markierung gezielt wieder entfernen
ainetlinter --path ./MeinProjekt.slnx --remove-disable-all
```

---

## 4. Integration durch LLM / Agenten

### Workflow für Agenten

1. **Vor einer Codeänderung:** Kontext aus dem Projekt holen (via MCP-Tools oder CLI `--list-rules` / `--describe-rule`).
2. **Nach einer Codeänderung:** Linter ausführen:
   ```bash
   ainetlinter --path . --config ainetlinter-rules.json
   ```
3. **Verstöße interpretieren** (anhand `RuleMetadata.intent`):
   - `intent: agent-context` — Komplexitäts-/Größenverstoß → direkt beheben
   - `intent: agent-resilience` — `EnforceNoSilentCatch` → Priorität hoch
   - `intent: test-coverage` — `StaticTestSentinel` → Test hinzufügen oder Exemption prüfen
   - `intent: architecture` — Namespace-/Vererbungsverstoß → nur mit Rücksprache beheben
4. **Suppression bei unvermeidbaren Verstößen:**
   ```csharp
   // ainetlinter-disable EnforceNoSilentCatch
   catch (Exception) { }
   ```

### Zwei-Stufen-Modell

| Profil | Zweck | Wann aktivieren |
| :--- | :--- | :--- |
| `platform-default` | Produktiv — Agenten beheben Verstöße direkt | Regulärer Entwicklungsbetrieb |
| `platform-ai-strict` | Zielrichtung — zeigt den Sollzustand | Code-Reviews, Architektur-Audits |

---

## 5. Exit-Codes

- `0`: Erfolg (Keine Regelverstöße gefunden).
- `1`: Regelbrüche wurden identifiziert und ausgegeben.
- `2`: Fataler Fehler (z. B. IO-Exception, MSBuildWorkspace-Ladefehler, CLI-Syntaxfehler).

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
| `PROJECT_NOT_RESTORED` | Projekt ohne frischen `dotnet restore` (`obj/project.assets.json` fehlt/veraltet) — einmal pro betroffenem Projekt statt tausender Phantom-Dependency-Folgefehler, siehe `rationale.md` §13 |
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
> `MSBuildWorkspace` benötigt externe Host-Prozesse. Nach dem Publish müssen zwingend die beiden Ordner `BuildHost-netcore/` und `BuildHost-net472/` im selben Verzeichnis wie `AiNetLinter.exe` liegen. Siehe [Linter-Projektintegration](integration.md#msbuild-abhangigkeiten-buildhost-ordner).
