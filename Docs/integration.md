# AiNetLinter — Projekt-Integration (Schritt-für-Schritt)

AiNetLinter ist ein CLI-Linter für .NET-Projekte, der C#-Code (und optional CSS, JavaScript, Razor) auf AI-Lesbarkeit und Strukturqualität prüft — Metriken wie Methodenlänge, zyklomatische Komplexität, Kopplung und semantische Benennung, optimiert für LLM-Agenten als Entwicklungspartner. Das Tool läuft als eigenständige `.exe`, erzeugt Markdown-Reports und synchronisiert Coding-Regeln direkt in LLM-Agenten-Regelwerke.

Diese Anleitung richtet sich an AI-Agenten, die AiNetLinter als Quality-Gate in ein bestehendes .NET-Projekt integrieren sollen.

**Ziel:** AiNetLinter läuft als automatisierter Test im bestehenden Testprojekt. Neue Regelverstösse in geänderten Dateien blockieren den Build (Ratchet-Prinzip). Agent-Regeln werden automatisch synchronisiert.

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
       --sync-agent-rules                   ← synchronisiert .agents/rules/AiNetLinter.mdc (Pfad anpassbar über --agent-rules-path)

   4. Prozess starten, stdout + stderr lesen, auf Exit warten

   5. Report in output/<stem>.md schreiben (UTF-8)

   6. WENN exitCode != 0: Test fehlschlagen mit Hinweis auf Report-Pfad
      WENN exitCode == 0: sicherstellen dass .agents/rules/AiNetLinter.mdc existiert
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

## Schritt 7: Agent Rules (.agents / .cursor) synchronisieren

`--sync-agent-rules` (bereits im Test-Aufruf aus Schritt 5 enthalten) erzeugt automatisch:

- `.agents/rules/AiNetLinter.mdc` (Default-Pfad) — Metriken und aktive Regeln aus der `rules.json`

Diese Datei macht die konfigurierten Grenzwerte für AI-Agenten direkt sichtbar, ohne dass der Agent eine extra Datei lesen muss. **Versioniere diese Datei.**

Für andere Agent-Hosts (z. B. Cursor: `.cursor/rules/AiNetLinter.mdc`) den Zielpfad über `--agent-rules-path <Verzeichnis-oder-Datei>` setzen — Default ist ausschliesslich `.agents/rules/AiNetLinter.mdc`, es wird kein zweiter Pfad automatisch mitgeschrieben.

Optional: Playbook erzeugen (Repo-Statistik, Migrations-Status):

```
--playbook .agents/rules/playbook.md
```

Drift-Check in CI (ohne Datei zu schreiben):

- Nur Agent-Regeln prüfen (schneller Pfad ohne Lint-Lauf):
  ```cmd
  AiNetLinter.exe --config <rules.json> --path <solution-root> --sync-agent-rules-only --check
  ```
- Kombinierter Lauf (Linter-Prüfung + Agent-Regeln prüfen):
  ```cmd
  AiNetLinter.exe --config <rules.json> --path <solution-root> --sync-agent-rules --check
  ```

Exit 1 wenn `.agents/rules/AiNetLinter.mdc` veraltet ist oder (im kombinierten Lauf) Code-Verstöße vorliegen.

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
| Agent-Regeln | `.agents/rules/AiNetLinter.mdc` | Ja |
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

## MCP-Server registrieren

AiNetLinter kann als **stdio-basierter MCP-Server** gestartet werden, um die Roslyn-basierte Solution-Analyse als 18 granular abfragbare Tools für AI-Coding-Agenten bereitzustellen (Claude Code, Cursor, eigene Agent-Loops). Vollständige Tool-Referenz, Trunkierungs-Format und Error-Codes: [Docs/agent-api.md#mcp-server-modus](agent-api.md#mcp-server-modus).

### Registrierung im MCP-Host

Standard-`mcpServers`-Block (Claude Code, Cursor und andere MCP-Hosts mit gleicher JSON-Spec):

```json
{
  "mcpServers": {
    "ainetlinter": {
      "command": "ainetlinter",
      "args": ["--mcp-server"]
    }
  }
}
```

Der Pfad zur `ainetlinter`-Exe wird vom MCP-Host über `PATH` aufgelöst (oder über den host-spezifischen Wrapper wie `.cursor/mcp.json` / `.mcp.json`). **Kein expliziter Pfad-Parameter nötig** — der Server sucht beim Start die Ziel-Solution selbst.

**`args: ["--mcp-server"]` ohne `--config` ist die empfohlene Registrierung.** Der Server sucht automatisch nach `rules.json` neben der aufgelösten Solution-Datei (`McpServerCommand.TryResolveRulesJsonPath`). Wird keine gefunden, läuft er mit den `Config`-Default-Regeln und signalisiert das in `get_violations` durch eine sichtbare Header-Zeile `Basis: Default-Regeln, keine rules.json gefunden` (siehe [Docs/agent-api.md](agent-api.md#default-config-markierung-in-get_violations)).

**stdout-Schutz:** der registrierte `ainetlinter`-Prozess nutzt `stdout` **ausschliesslich** für JSON-RPC. Andere Verwendungen (CI-Log-Parsing, Debug-Ausgaben via `Console.WriteLine`, Pipe-Redirect auf `tee`, o. ä.) wuerden das JSON-RPC-Framing zerstoeren und sind nicht zulaessig. Status- und Fehlerausgaben gehen auf `stderr` (siehe [Docs/agent-api.md#stdout-schutz-strukturelle-json-rpc-absicherung](agent-api.md#stdout-schutz-strukturelle-json-rpc-absicherung)).

**Opt-in Call-Log:** fuer Production-Monitoring kann der registrierte `ainetlinter`-Aufruf um `--mcp-log <pfad>` ergaenzt werden — siehe [Docs/agent-api.md#call-log-opt-in](agent-api.md#call-log-opt-in) fuer Format und Pfad-Aufloesung. Default: deaktiviert, kein File I/O.

### cwd-Verhalten

Der Server läuft im `cwd` des Host-Prozesses. Mit `args: ["--mcp-server"]` (ohne `--path`) sucht er im `cwd` nach genau einer `.sln`- oder `.slnx`-Datei und lädt sie. **Empfehlung:** MCP-Server pro Projekt registrieren, nicht global, damit das `cwd` zum jeweiligen Projekt-Root passt und keine Mehrdeutigkeit entsteht. Die `rules.json`-Auto-Discovery läuft unabhängig vom `cwd` des Host-Prozesses — sie erfolgt relativ zur **aufgelösten** Solution-Pfad-Komponente, nicht zum `cwd`.

### Start-Sequenz: entkoppelter initialize-Handshake

Der MCP-Transport-Handshake (`initialize`) antwortet **sofort** — die Lösung wird parallel im Hintergrund geladen. Damit erkennen Hosts mit kurzem Startup-Timeout den Server zuverlässig als „bereit", ohne auf die `MSBuildWorkspace.OpenSolutionAsync`-Latenz warten zu müssen. Tool-Calls, die während des Hintergrund-Loads eintreffen, erhalten einen Loading-Info-Text (`[INFO]: Server laedt die Solution noch. ...`, kein Fehler); sobald der Load abgeschlossen ist, liefern dieselben Tools reguläre Ergebnisse. Vollständige Beschreibung der drei Zustände (`Loading` / `Loaded` / `LoadFailed`) und der Retry-Empfehlung für Agent-Loops: [Docs/agent-api.md](agent-api.md#drei-zustands-lifecycle-des-mcp-servers).

### Mehrdeutigkeit: mehrere Solutions im cwd

Liegen im `cwd` mehrere `.sln`- oder `.slnx`-Dateien, bricht der Server-Start mit einem `[ERROR]: AMBIGUOUS_SOLUTION`-Fehler ab. Abhilfe: explizit `--path <Datei>` in den `args` setzen:

```json
{
  "mcpServers": {
    "ainetlinter": {
      "command": "ainetlinter",
      "args": ["--mcp-server", "--path", "./src/MeinProjekt.slnx"]
    }
  }
}
```

`--path` akzeptiert sowohl eine direkte Solution-Datei (relativ oder absolut) als auch ein Verzeichnis, in dem dann ebenfalls nach genau einer `.sln`/`.slnx` gesucht wird. `0` Kandidaten führen analog zu `[ERROR]: RESOURCE_NOT_FOUND`.

### Tool-vs-`rg`-Empfehlung für Agent-Loops

Wenn der MCP-Server registriert ist, sollten Agent-Loops **folgende Reihenfolge** einhalten:

1. **Zuerst** symbolische Tools: `find_symbol` (Symbol lokalisieren), `get_file_skeleton` (Strukturüberblick), `get_symbol_body` (Body eines Symbols per stabiler ID), `find_references` / `get_impact` (Aufrufstellen, optional mit `depth`-Parameter für transitive Aggregation), `get_type_hierarchy` (Vererbung inkl. heuristischer DI-Registrierungs-Hinweise), `get_violations` (Lint-Stand). Diese Tools liefern **semantisch präzise, getypte** Ergebnisse — keine String-Suche, keine False Positives.
2. **Nur wenn das nicht reicht** (Nicht-C#-Dateien wie `.json`/`.yml`/`.md`/`.razor`/`.xaml`/`.html`/`.css` oder reine Konfigurations-/Kommentar-/String-Suche): `search_pattern` mit `isRegex=false` (Default, case-insensitive Substring) oder `isRegex=true` für komplexere Muster.
3. **Niemals** `rg` / `grep` für **C#-Symbole** (Klassen-, Methoden-, Property-Namen). Diese Tools durchsuchen Strings und Kommentare mit, produzieren False Positives in gleichnamigen Symbolen anderswo und liefern keine Typ-/Signatur-Information.

Konkret:

- Klassennamen suchen → `find_symbol(namePattern: "MyClass", kind: "Klasse")`
- Methoden-Aufrufer finden → `find_references(symbolIdentifier: "MyClass.MyMethod")` oder `get_impact(symbolIdentifier: ...)`
- Konfigwert in `.json` finden → `search_pattern(pattern: "MySetting")` (oder direkt `rg`, das ist hier äquivalent)
- TODO-Kommentare listen → `search_pattern(pattern: "TODO", isRegex: false)` (oder `rg "TODO"`)
- Lint-Stand einer Datei → `get_violations(scopeFilter: "src/MeinProjekt/Service.cs")`

### Erstorientierung: Resource `ainetlinter://overview`

Ein Agent, der den Server zum ersten Mal sieht, kann per `resources/read` (`{"uri": "ainetlinter://overview"}`) einen kurzen Ueberblick abrufen: alle Tools in einem Satz sowie den aktuellen Server-Status (geladene Solution, tatsaechlich verwendete `rules.json` oder Hinweis auf Default-Regeln). Details: [Docs/agent-api.md#resource-ainetlinteroverview](agent-api.md#resource-ainetlinteroverview).

### Mehrere parallele Server-Instanzen

Pro Solution ein eigener Server-Prozess — die Cache-Isolation zwischen verschiedenen Solutions ist SHA-256-basiert (Implementierung in `AnalysisCacheManager`), der Nutzer braucht nichts zu konfigurieren. Ein gleichzeitiger CLI-Lint-Lauf auf derselben Solution kollidiert nicht mit dem MCP-Server-Cache, weil `get_violations` den Disk-Cache umgeht.

---

> [AiNetLinter](https://github.com/RalfHuesing/AiNetLinter) — Quellcode, Changelog und Issues auf GitHub.
