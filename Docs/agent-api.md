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
| `--sync-cursor-rules` | bool | `.cursor/rules/AiNetLinter.mdc` aktualisieren |
| `--impact <typ>` | string | Impact-Analyse für einen Typ |
| `--debt-report` | bool | Tech-Debt-Report generieren |
| `--check` | bool | Drift-Prüfung (exit 1 bei Abweichung) |

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
