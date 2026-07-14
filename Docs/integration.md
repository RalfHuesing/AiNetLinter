# AiNetLinter — Projekt-Integration (Schritt-für-Schritt)

AiNetLinter ist ein CLI-Linter für .NET-Projekte, der C#-Code (und optional CSS, JavaScript, Razor) auf AI-Lesbarkeit und Strukturqualität prüft — Metriken wie Methodenlänge, zyklomatische Komplexität, Kopplung und semantische Benennung, optimiert für LLM-Agenten als Entwicklungspartner. Das Tool läuft als eigenständige `.exe`, erzeugt Markdown-Reports und synchronisiert Coding-Regeln direkt in Cursor/Claude-Regelwerke.

Diese Anleitung richtet sich an AI-Agenten, die AiNetLinter als Quality-Gate in ein bestehendes .NET-Projekt integrieren sollen.

**Ziel:** AiNetLinter läuft als automatisierter Test im bestehenden Testprojekt. Neue Regelverstösse in geänderten Dateien blockieren den Build (Ratchet-Prinzip). Cursor/Claude-Regeln werden automatisch synchronisiert.

---

## Voraussetzungen

- `AiNetLinter.exe` ist auf dem Entwicklungsrechner installiert (z. B. unter `C:\Daten\AiNetLinter-win-x64\AiNetLinter.exe`)
- Ein bestehendes .NET-Testprojekt ist vorhanden (xUnit, NUnit, MSTest — egal)
- Die Solution hat eine `.sln`- oder `.slnx`-Datei im Root-Verzeichnis

---

## Schritt 1: Verzeichnisstruktur anlegen

Integriere AiNetLinter **als Unterverzeichnis im bestehenden Testprojekt** — kein neues `.csproj` anlegen.

```
<TestProjekt>/
  AiNetLinter/
    docs/          ← versionierte Tool-Dokumentation (Schritt 2)
    rules/         ← Konfigurationsdateien (Schritt 3 + 6)
    output/        ← Lint-Reports (gitignored, Schritt 4)
```

Pfad-Empfehlung: `<SolutionName>.Tests/AiNetLinter/`

---

## Schritt 2: Tool-Dokumentation versionieren

Dumpe die eingebetteten Docs in das `docs/`-Unterverzeichnis. **Wichtig (Windows):** Verwende `cmd /c`-Umleitung mit `>` — nicht `Set-Content` oder `Out-File`, da diese ein BOM einfügen oder die Kodierung ändern und dadurch Zeichensalat erzeugen.

```cmd
cmd /c "AiNetLinter.exe --docs readme        > <TestProjekt>\AiNetLinter\docs\AiNetLinter-readme.md"
cmd /c "AiNetLinter.exe --docs agent-api     > <TestProjekt>\AiNetLinter\docs\AiNetLinter-agent-api.md"
cmd /c "AiNetLinter.exe --docs configuration > <TestProjekt>\AiNetLinter\docs\AiNetLinter-configuration.md"
```

Diese Dateien versionieren — sie geben dem Agenten Kontext ohne Netz-Zugriff.

---

## Schritt 3: Startkonfiguration anlegen

Dumpe die eingebettete Default-Konfiguration als Ausgangspunkt:

```cmd
cmd /c "AiNetLinter.exe --docs rules-json > <TestProjekt>\AiNetLinter\rules\<projektname>.rules.json"
```

Die erzeugte `rules.json` enthält alle Schalter mit sinnvollen Defaults. **Noch nicht anpassen** — das passiert in Schritt 8 nach dem ersten echten Lauf.

---

## Schritt 4: .gitignore anlegen

Der `output/`-Ordner enthält Lint-Reports und wird nicht versioniert:

```
# In <TestProjekt>/AiNetLinter/.gitignore (neu anlegen):
output/
```

---

## Schritt 5: Test anlegen

Lege einen einzelnen Test im bestehenden Testprojekt an. Der Test startet `AiNetLinter.exe` als Prozess und prüft den Exit-Code.

**Pseudocode (framework-unabhängig):**

```
TEST "LintReport wird erzeugt und ist grün":

  1. exePath = Pfad zu AiNetLinter.exe auflösen
     (z. B. aus Umgebungsvariable oder hartem Pfad)

  2. WENN exePath nicht existiert:
       Test überspringen (SkipUnless / Assume.That / Inconclusive)
       — nicht fehlschlagen, damit CI ohne lokales Tool grün bleibt

  3. Argumente zusammensetzen:
       --config  <Pfad zur rules.json>
       --path    <Solution-Root>
       --baseline <Pfad zur Baseline-JSON>   ← nach Schritt 6 verfügbar
       --sync-cursor-rules                   ← synchronisiert .cursor/rules/AiNetLinter.mdc

  4. Prozess starten, stdout + stderr lesen, auf Exit warten

  5. Report in output/<stem>.md schreiben (UTF-8)

  6. WENN exitCode != 0: Test fehlschlagen mit Hinweis auf Report-Pfad
     WENN exitCode == 0: sicherstellen dass .cursor/rules/AiNetLinter.mdc existiert
```

**Hinweis Schritt 2 (Tool nicht vorhanden):** In xUnit heisst das `Assert.SkipUnless`, in NUnit `Assume.That(condition)`, in MSTest `Assert.Inconclusive()`. Wähle die für das Projekt passende Variante — das Ziel ist dasselbe: Test wird als "übersprungen" markiert, nicht als Fehler.

**Exit-Codes:**
- `0` = alles grün (bzw. nur bekannte Verstösse in der Baseline)
- `1` = neue Verstösse in geänderten Dateien → Test schlägt fehl
- `≥ 2` = fataler Fehler (Konfiguration, fehlende Dateien) → Test schlägt fehl

---

## Schritt 6: Ersten Lauf durchführen und Baseline erzeugen

Führe AiNetLinter **einmalig ohne Baseline** aus, um das Ist-Inventar zu sehen:

```cmd
AiNetLinter.exe --config <rules.json> --path <solution-root>
```

Dieser Lauf zeigt alle aktuellen Verstösse. Es ist normal, dass ein bestehendes Projekt viele Verstösse hat — die Baseline friert sie ein, sodass nur **neue** Verstösse im geänderten Code den Test blockieren.

Baseline erzeugen und versionieren:

```cmd
AiNetLinter.exe --config <rules.json> --path <solution-root> --create-baseline <TestProjekt>\AiNetLinter\rules\<projektname>-baseline.json
```

Die erzeugte `<projektname>-baseline.json` **in git einchecken**. Sie ist der Ratchet: Dateien die sich nicht ändern, werden nicht geprüft.

---

## Schritt 7: Cursor/Claude Rules synchronisieren

`--sync-cursor-rules` (bereits im Test-Aufruf aus Schritt 5 enthalten) erzeugt automatisch:

- `.cursor/rules/AiNetLinter.mdc` — Metriken und aktive Regeln aus der `rules.json`

Diese Datei macht die konfigurierten Grenzwerte für Cursor und Claude Code direkt sichtbar, ohne dass der Agent eine extra Datei lesen muss. **Versioniere diese Datei.**

Optional: Playbook erzeugen (Repo-Statistik, Migrations-Status):

```
--playbook .cursor/rules/playbook.mdc
```

Drift-Check in CI (ohne Datei zu schreiben):

- Nur Cursor-Regeln prüfen (schneller Pfad ohne Lint-Lauf):
  ```cmd
  AiNetLinter.exe --config <rules.json> --path <solution-root> --sync-cursor-rules-only --check
  ```
- Kombinierter Lauf (Linter-Prüfung + Cursor-Regeln prüfen):
  ```cmd
  AiNetLinter.exe --config <rules.json> --path <solution-root> --sync-cursor-rules --check
  ```

Exit 1 wenn `.cursor/rules/AiNetLinter.mdc` veraltet ist oder (im kombinierten Lauf) Code-Verstöße vorliegen.

---

## Schritt 8: rules.json an das Projekt anpassen

Nach dem ersten Lauf gibt es typischerweise **False Positives** — Verstösse die strukturell korrekt sind, aber gegen eine Standardregel verstossen. Diese Phase erfordert Abstimmung mit dem Projektverantwortlichen.

**Vorgehen:**

1. Voll-Inventar analysieren (ohne `--baseline`):
   ```cmd
   AiNetLinter.exe --config <rules.json> --path <solution-root> > output\voll-inventar.md
   ```

2. Muster identifizieren — welche Regeln produzieren systematisch False Positives?

3. Pro Anpassung folgende Felder in der README (oder einer Governance-Datei) dokumentieren:

   | Feld | Inhalt |
   |---|---|
   | **Problem** | Welche Regel, warum False Positive |
   | **Ist-Daten** | Anzahl Verstösse, betroffene Pfade |
   | **Scope** | Global / `ProjectOverrides` / `PathOverrides` — engster sinnvoller Pfad |
   | **Wertwahl** | Konkreter Wert mit Bezug zu Ist-Daten (kein willkürlicher Puffer) |
   | **Alternative verworfen** | Warum nicht Code-Fix, Suppression oder engeres Limit |
   | **Prod-Schutz** | Produktionscode bleibt weiter unter globalem Limit |

4. Anpassung in `rules.json` vornehmen, danach Baseline neu erzeugen.

**Verboten:** Limits anheben oder Regeln abschalten, **nur** damit der Test grün wird — ohne dokumentiertes False Positive.

**Verfügbare Exemption-Mechanismen** (Details: `--docs configuration`):
- `ProjectOverrides` — andere Grenzwerte für Test-Projekte
- `PathOverrides` — Pfad-spezifische Ausnahmen (engster Scope)
- Typ-/Prefix-Exemptions — z. B. `FootprintIgnoreTypeNames`, `ConstructorDependencyIgnoreTypePrefixes`
- `// ainetlinter-disable all` — Einzel-Datei supprimieren (als temporäres Hilfsmittel, nicht als Dauerlösung)

---

## Ergebnis-Übersicht

| Was | Wo | Versioniert? |
|---|---|---|
| Startkonfiguration | `AiNetLinter/rules/<projektname>.rules.json` | Ja |
| Baseline (Ratchet) | `AiNetLinter/rules/<projektname>-baseline.json` | Ja |
| Tool-Dokumentation | `AiNetLinter/docs/*.md` | Ja |
| Cursor/Claude-Regeln | `.cursor/rules/AiNetLinter.mdc` | Ja |
| Lint-Reports | `AiNetLinter/output/` | **Nein** (gitignored) |

---

## Weiterführende Dokumentation

```cmd
AiNetLinter.exe --docs readme          ← Schnellstart, Feature-Übersicht
AiNetLinter.exe --docs configuration   ← Vollständige Config-Referenz, alle Felder
AiNetLinter.exe --docs agent-api       ← Alle CLI-Flags, Workflows, Fehlerformat
AiNetLinter.exe --docs rules-json      ← Default-Konfiguration als JSON
AiNetLinter.exe --list-rules           ← Alle Regeln als Tabelle
AiNetLinter.exe --describe-rule <Id>   ← Eine Regel vollständig erklären
```

---

> [AiNetLinter](https://github.com/RalfHuesing/AiNetLinter) — Quellcode, Changelog und Issues auf GitHub.
