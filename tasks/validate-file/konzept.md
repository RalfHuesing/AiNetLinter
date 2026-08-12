---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: medium
rules_dir: .agents/rules
last_updated: 2026-08-12
open_questions: []
---

# Konzept: `validate_file` — Kompakte Post-Edit-Validierung

## Ziel (Was)

Ein neues MCP-Server-Tool `validate_file`, das nach einem Code-Edit in **einem kompakten Call** sowohl echte Roslyn-Compiler-Fehler als auch AiNetLinter-Lint-Diagnostics für genau diese Datei liefert — inklusive kurzer, handlungsorientierter `nextSteps`-Hinweise. Schließt die fehlende Kern-Primitive für den Verifikations-Loop jeder Agent-Session: nach jedem Edit prüfen, ob etwas kaputt ist, ohne einen vollen Solution-weiten `get_violations`-Call oder einen externen `dotnet build` zu brauchen.

Rohe Ausgangsidee (bereits vorhanden, noch nicht umgesetzt): `tasks/features/03-market-research.md` §5.1 F1.

## Warum / Kontext

Aus `tasks/features/08-prioritaet-agentische-programmierung.md` (P1, Priorisierungs-Runde 2026-08-12): Post-Edit-Verifikation ist die **häufigste Einzelaktion** in jeder agentischen Coding-Session, in jedem Projekt — und fehlt aktuell im 18-Tool-Set als kompaktes Primitiv:
- `get_violations` ist solution-weit gedacht; `scopeFilter` ist ein Substring-Match ohne echten Single-File-Modus.
- Compiler-Fehler (echte `CS...`-Diagnostics) werden aktuell nirgends als eigenständige, strukturierte Daten ausgegeben — nur als knapper Text-Anhang in anderen Tools (siehe Bestandscode-Analyse).
- Ein Agent, der gerade eine Datei editiert hat, will in einem Call wissen: "Kompiliert das noch? Verstößt es gegen unsere Regeln? Was mache ich als Nächstes?" — nicht drei separate Tools kombinieren müssen.

Passt zum eigenen Leitbild (`tasks/features/05-roadmap.md` §0): AiNetLinter als "Verifikations-Schicht ... Gatekeeper zwischen AI-Output und CI-Merge", nicht nur Solution-weites Audit-Werkzeug.

## Erkenntnisse aus der `src/`-Bestandscode-Analyse

1. **Compiler-Fehler werden schon geholt, aber nicht strukturiert ausgegeben.**
   - `McpCompileDiagnostics.GetErrorsByFileAsync` (`src/AiNetLinter/Mcp/Tools/McpCompileDiagnostics.cs:27`) läuft bereits über `solution.Projects` → `Project.GetCompilationAsync()` → filtert `DiagnosticSeverity.Error`. Aktuell genutzt von `GetFileSkeletonTool.cs:46-48`, `FindSymbolTool.cs`, `GetIndexScopeScanner.cs` — aber nur als **Kurztext-Warnung** (`FormatFileWarning`, max. 3 Diagnostics, kein Line/Col, `Diagnostic.Location.GetLineSpan()` wird nirgends aufgerufen).
   - Für `validate_file` heißt das: **Beschaffung wiederverwenden, strukturierte Extraktion (ruleId/severity/line/col/message) neu bauen.**
2. **`get_violations`-Scope ist Substring, kein Single-File-Modus.**
   - `ViolationScopeFilter.MatchesScope` (`src/AiNetLinter/Mcp/Tools/Analysis/ViolationScopeFilter.cs:43-50`) prüft `projectName.Contains(scopeFilter) || relativePath.Contains(scopeFilter)`. Ein voller Pfad funktioniert faktisch, matcht aber potenziell auch `FooExtended.cs` bei `scopeFilter="Foo.cs"`. Die Filter-/Sortier-Infrastruktur (`FilterAndSortViolations`, `BuildFileToProjectMap`) ist 1:1 wiederverwendbar — `validate_file` braucht aber einen **exakten Pfadabgleich**, keinen Substring-Match.
3. **Kein bestehender "nextSteps"-Mechanismus — `McpSufficiencyHints`/`McpDrillDownHints` sind etwas anderes.**
   - Beide Klassen hängen nur generische, fachlose Meta-Hinweise an ("Daten vollständig, kein Read/Grep nötig" bzw. "zeigt Ebene 1-N"). Kein Fachinhalt.
   - Der inhaltlich passende Baustein ist `SafeguardScanner.BuildRemediation` mit seiner `RuleHints`-Lookup-Tabelle (`SafeguardScanner.cs:308-333`) — echte, regelspezifische Freitext-Empfehlungen. Das ist der eigentliche Wiederverwendungs-Kandidat für `nextSteps`, nicht die Hints-Klassen.
4. **`codeHealth`-Score existiert nur solution-weit aggregiert.**
   - `SafeguardScanner.BuildScoreResult` (`SafeguardScanner.cs:156-195`) mittelt CC/Footprint/Sealed-Anteil über **alle** Klassen im Scope (`classes.Average(...)`). Auf eine Einzeldatei mit 1-2 Klassen angewandt, wäre das Ergebnis statistisch viel volatiler als ein Solution-Mittelwert — keine reine Wiederverwendung, sondern eine neu zu kalibrierende Berechnung (andere Gewichte/Threshold als `DefaultMinScoreThreshold = 8.0`).
5. **CLI-Batch-Modus liefert keine wiederverwendbare Einzeldatei-Logik.**
   - `LinterAutoFixer.cs` ist reine Syntax-Fix-Logik für 3 triviale Regeln, kein Report-Baustein. `--check` ist nur der Dry-Run von `--fix`, kein eigenständiger Validierungsmodus. Der normale CLI-Report ist solution-weit. `GetViolationsScanner` nutzt aber laut eigenem Kommentar (`GetViolationsScanner.cs:23-27`) bewusst dieselbe `LinterEngine` wie der CLI-Batch-Lauf — dieselbe Konsistenz-Garantie gilt für `validate_file`.
6. **Namenskonvention passt, keine Kollision.**
   - `validate_file` folgt dem `verb_noun`-Muster (wie `reload_config`). Am nächsten kommen `get_violations` (Lint, aber solution-weit, keine Compiler-Errors/codeHealth/nextSteps) und `get_file_skeleton` (datei-fokussiert, hat schon eine Kurzwarnung, aber liefert Struktur statt Diagnostics). Keine Überschneidung, die `validate_file` überflüssig machen würde.
7. **`readOnly`/`idempotent`-Annotationen sind ein Erstanwendungsfall.**
   - Repo-weiter Grep nach `Annotations|ReadOnlyHint|IdempotentHint` in `src/AiNetLinter/Mcp/**` liefert null Treffer. Alle bestehenden `McpServerTool.Create(...)`-Aufrufe (in allen `*ToolRegistrations.cs`) setzen nur `Name`/`Description`. Die rohe Idee (F1) will `readOnly: true`/`idempotent: true` — das wäre **neu einzuführen**, kein zitierbares Bestandsmuster.

## Scope

### Muss-Haben

- **Kompakter Response pro Datei:** Compiler-Errors (strukturiert: ruleId/severity/line/col/message, Erweiterung von `McpCompileDiagnostics`) + Lint-Violations (via bestehende `GetViolationsScanner`-Infrastruktur, aber mit exaktem Pfadabgleich statt Substring) in einem Aufruf.
- **`summary`-Block:** Anzahl Errors/Warnings/Info — **kein** `codeHealth`-Score. Ein 0-10-Score wäre bei Dateien mit 1-2 Klassen statistisch zu volatil (siehe Bestandscode-Analyse Punkt 4); Score-Bewertung bleibt Aufgabe von `safeguard` (dort sind Mittelwerte über einen größeren Scope stabil).
- **`nextSteps`:** abgeleitet aus `SafeguardScanner.RuleHints` (`SafeguardScanner.cs:308-333`) — bestehende regelspezifische Lookup-Tabelle wiederverwenden/erweitern statt einer zweiten, parallelen Hint-Tabelle im Projekt. Bei Bedarf um Compiler-Fehler-spezifische Hints ergänzen (`RuleHints` deckt aktuell nur Lint-Regeln ab, keine `CS*`-Diagnostics).
- **Exakter Pfadabgleich:** `file`-Parameter, exakter Match — kein Substring-Fallverwechslungsrisiko wie bei `scopeFilter` (siehe Bestandscode-Analyse Punkt 2).
- **`changedOnly`-Parameter (Batch via Git-Diff):** Analog zu `find_magic_values` — Wiederverwendung derselben Diff-Scanning-Logik aus `get_impact`. Mit `changedOnly=true` werden alle aktuell per Git-Diff geänderten Dateien in einem Call validiert, ohne dass der Agent sie einzeln aufzählen muss (deckt den realistischeren Fall nach einem Multi-File-Edit ab). Mindestens eines von `file`/`changedOnly` muss gesetzt sein.
- **Einheitliche Response-Form (Objekt-Wrapper mit Array):** Immer `{ Files: [...] }` — auch bei genau einer validierten Datei ein Array mit einem Element, nie zwei unterschiedliche Response-Formen je nach Aufrufform (`file` vs. `changedOnly`). Vermeidet Sonderfall-Handling beim Aufrufer und folgt derselben Objekt-Wrapper-Konvention wie `get_violations` (`{ Violations: [...] }`).
- **Kappung:** `maxResults` (Default 50, analog `McpTruncation`-Konvention der Nachbar-Tools) schützt pro Datei vor großen Dumps bei stark verletzten Dateien.
- **MCP-Tool-Annotationen `readOnly: true` / `idempotent: true`:** Erstanwendungsfall im Projekt (siehe Bestandscode-Analyse Punkt 7) — bewusst nur für dieses eine Tool, kein Nachziehen der übrigen 19 Tools in diesem Konzept (das wäre ein eigener, kleiner Folge-Task).

### MCP-Tool Schnittstellen-Spezifikation (`validate_file`)

```json
{
  "name": "validate_file",
  "description": "Liefert fuer eine Datei (oder alle per Git-Diff geaenderten Dateien) kompakte Compiler-Errors und AiNetLinter-Lint-Diagnostics samt Handlungsempfehlungen in einem Call.",
  "annotations": { "readOnly": true, "idempotent": true },
  "parameters": {
    "file": "Optional. Exakter Pfad einer einzelnen zu validierenden Datei. Mindestens eines von file/changedOnly ist Pflicht.",
    "changedOnly": "boolean (Default: false). Validiert alle per Git-Diff geaenderten Dateien statt einer einzelnen (Wiederverwendung der get_impact-Diff-Logik).",
    "maxResults": "Maximale Anzahl Diagnostics pro Datei (Default: 50). Schuetzt vor Context-Window-Ueberlauf bei stark verletzten Dateien."
  },
  "response_shape": "{ Files: [ { file, summary: { errors, warnings, info }, diagnostics: [ { ruleId, severity, line, col, message } ], nextSteps: [ string ] } ] }"
}
```

### Nice-to-Have (Zwischenspeicher — vor `status: ready` aufgelöst)

*Keine offenen Nice-to-Have-Punkte — alle vier Diskussionspunkte dieser Runde wurden direkt in Muss-Haben oder Non-Goals entschieden.*

### Non-Goals (bewusst NICHT Teil davon)

- **Kein Ersatz für `dotnet build`/`dotnet test`:** ergänzt die Verifikation, ersetzt sie nicht — konsistent mit der README-Positionierung ("kein Ersatz für Compiler oder Tests").
- **Keine Redundanz zu `get_violations`:** `get_violations` bleibt das solution-weite Audit-Tool; `validate_file` ist explizit der schnelle Einzeldatei-Check nach einem Edit.
- **Kein automatisches Fixen:** liefert Diagnose + Empfehlung, keine Code-Änderung (Agent/`--fix` bleiben die Ausführenden).
- **Kein `codeHealth`-Score pro Datei:** bewusst weggelassen (siehe Muss-Haben) — zu volatil bei kleinen Dateien, bleibt Aufgabe von `safeguard`.
- **Kein Nachziehen der `readOnly`/`idempotent`-Annotationen bei den übrigen 19 Tools:** eigener, kleiner Folge-Task, nicht Teil dieses Konzepts.
- **Kein explizites `files: string[]`-Array für eine frei wählbare Liste:** `changedOnly` deckt den Batch-Fall (alle geänderten Dateien) ab; eine dritte, unabhängige Liste beliebiger Dateien wäre Scope-Creep ohne belegten Bedarf.

## Zielplattformen / Technischer Rahmen

- **Stack:** C# / .NET 10, Roslyn `Compilation.GetDiagnostics()` + bestehende `LinterEngine`/`GetViolationsScanner`.
- **Integration:** AiNetLinter MCP-Server (`src/AiNetLinter`), gleiche Registrierungs-Infrastruktur wie alle bestehenden Analysis-Tools.

## Verworfene Alternativen

- **`McpSufficiencyHints`/`McpDrillDownHints` für `nextSteps` wiederverwenden:** verworfen — beide liefern nur generische, fachlose Meta-Hinweise (Vollständigkeit/Pagination), keine inhaltlichen Handlungsempfehlungen. `SafeguardScanner.RuleHints` ist stattdessen die gewählte Vorlage (siehe Muss-Haben).
- **Eigener `codeHealth`-Score pro Datei (neu kalibriert):** verworfen — bei 1-2 Klassen pro Datei statistisch zu volatil, hätte eigene Gewichte/Threshold gebraucht ohne echten Mehrwert gegenüber reiner Diagnostics-Zählung.
- **Freies `files: string[]`-Array für beliebige Dateilisten:** verworfen zugunsten von `changedOnly` (Git-Diff-basiert) — deckt den tatsächlichen Anwendungsfall (Multi-File-Edit) ab, ohne dass der Aufrufer Pfade selbst zusammenstellen muss.

## Wo im Projekt

- **Compiler-Diagnostics-Erweiterung:** `src/AiNetLinter/Mcp/Tools/McpCompileDiagnostics.cs` — Beschaffung wiederverwenden, strukturierte Extraktion ergänzen.
- **Lint-Diagnostics-Wiederverwendung:** `src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs`, `ViolationScopeFilter.cs` — exakten Pfad-Match-Modus ergänzen.
- **`nextSteps`-Vorlage:** `src/AiNetLinter/Mcp/Tools/Safeguard/SafeguardScanner.cs:308-333` (`RuleHints`-Lookup-Tabelle).
- **Tool-Registrierung + Annotationen:** `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` — hier auch `readOnly`/`idempotent` erstmals am `McpServerTool.Create(...)`-Aufruf setzen (bislang nirgends im Projekt verwendet, siehe Bestandscode-Analyse Punkt 7).
- **`changedOnly`-Diff-Logik zum Wiederverwenden:** `src/AiNetLinter/Mcp/Tools/SymbolGraph/GetImpactTool.cs` — dieselbe Git-Diff-Scanning-Logik wie bei `find_magic_values` (`tasks/magic-values-in-mcp/konzept.md`).
- **Tests:** `src/AiNetLinter.Tests/Mcp/Tools/` — Tool+Scanner-Testpaar analog `SafeguardToolTests.cs`/`SafeguardScannerTests.cs`.

## Entdeckte Mängel/Redundanzen

- **Keine echten Mängel gefunden.** Alle relevanten Bausteine (Compiler-Diagnostics-Beschaffung, Violation-Scanning, Rule-Hints) existieren bereits als wiederverwendbare Bausteine, nur nicht in der für `validate_file` nötigen Form kombiniert/strukturiert — kein Neubau von Grund auf nötig, siehe Bestandscode-Analyse oben.

## Wie (grober Ansatz)

1. `McpCompileDiagnostics` um eine Methode erweitern, die für **eine** Datei strukturierte Diagnostics (inkl. `Location.GetLineSpan()`) statt eines Kurztexts liefert.
2. `GetViolationsScanner`/`ViolationScopeFilter` um einen exakten Einzeldatei-Modus ergänzen (kein Substring-Match).
3. Zielliste bestimmen: entweder die eine Datei aus `file`, oder bei `changedOnly=true` alle per Git-Diff geänderten Dateien (Wiederverwendung der `get_impact`-Diff-Logik).
4. Für jede Zieldatei beide Diagnostics-Quellen zusammenführen, mit `summary` (Errors/Warnings/Info-Zählung, kein Score) und `McpTruncation`-Kappung bei `maxResults`.
5. `nextSteps` aus `SafeguardScanner.RuleHints` ableiten, um Compiler-Diagnostics-Hints ergänzen.
6. Ergebnisse als `{ Files: [...] }` zusammenfassen (auch bei nur einer Datei), Tool mit `readOnly`/`idempotent`-Annotationen in `AnalysisToolRegistrations.cs` registrieren.

## Definition of Done / Erfolgskriterien

- `validate_file` liefert für `file` (Einzeldatei) UND `changedOnly=true` (alle Git-Diff-geänderten Dateien) strukturierte Compiler-Errors UND Lint-Violations in einem Call, jeweils als `{ Files: [...] }`.
- Exakter Pfadabgleich verwechselt keine Datei mit einer anderen (z. B. `Foo.cs` vs. `FooExtended.cs`).
- `summary` enthält Errors/Warnings/Info-Zählung, keinen `codeHealth`-Score.
- `nextSteps` liefert für mindestens die Fälle "Lint-Regelverstoß" (via `RuleHints`) und "Compiler-Error" sinnvolle Freitext-Hinweise.
- `maxResults`-Kappung greift pro Datei bei stark verletzten Dateien zuverlässig.
- Tool ist mit `readOnly: true`/`idempotent: true` registriert.
- Tests in `src/AiNetLinter.Tests/Mcp/Tools/` bestätigen Compiler- und Lint-Diagnostics-Erkennung, `changedOnly`-Batch-Verhalten und Kappung.

## Offene Punkte

*Keine offenen Punkte.*
