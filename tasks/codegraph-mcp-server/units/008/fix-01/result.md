---
unit: 008
fix_round: 01
task: codegraph-mcp-server
workflow: dynamic-loop
type: result
created_by: coder
created_at: 2026-08-02
trigger: units/008/review.md (F-001 MAJOR + F-002 MINOR)
trigger_plan: units/008/fix-01/plan.md (Commit 96f1029)
---

# Result Einheit 008 / fix-01 — F-001 Doku-Drift-Korrektur in `Docs/agent-api.md:238`

## Summary

Der MAJOR-F-001 aus `units/008/review.md:53-112` ist behoben: die
C#-only-Zählung in `Docs/agent-api.md:238` stimmt jetzt wortwörtlich mit
der Tabelle (Z. 242-252), dem zitierten `ServerInstructions`-Block
(`McpServerOptionsFactory.cs:26-31`) und der Realität (6 C#-only-Tools)
überein. `search_pattern` ist aus der C#-only-Aufzählung herausgenommen
und als eigener Fallback-Satz formuliert; die zwei nicht-C#-beschränkten
Tools (`get_index_scope`, `get_hotspots`) sind explizit in der Klammer
genannt. Mitgenommen wurden außerdem der MINOR-F-002 (A3-Block-Symmetrie
in `units/008/result.md`) und ein optionaler 4. A3-Wortlaut-Test in
`McpDocumentationSmokeTests.cs`, der den **korrigierten** Doku-Wortlaut
gegen Drift absichert (echter A3-Pfad: Doku manipulieren → Test rot →
zurückbiegen → Test grün). Reine Doku-Korrektur + 1 Test-Methode, 0
Code-Edits an Produktivcode, Volllauf 1165/1165 grün in 4:57 min.

## What changed

| Datei | Diff-Größe | Zweck |
|---|---:|---|
| `Docs/agent-api.md` | 1 Z. ersetzt (1 Ins, 1 Del) | Z. 238 wortwörtlich 1:1 vom Kritiker-Vorschlag (`units/008/review.md:101-108`) korrigiert: Zählung „7" → „6", `search_pattern` aus der C#-only-Aufzählung raus, explizite Nennung der zwei Struktur-Tools, Fallback-Satz mit „ist selbst nicht C#-only" |
| `src/AiNetLinter.Tests/Mcp/McpDocumentationSmokeTests.cs` | +33 Z., 1 neue Test-Methode | F-001 / A3-Wortlaut-Test `AgentApi_CountsCsharpOnlyToolsCorrectly`: liest `Docs/agent-api.md` (File-Read, kein Reflection), assertiert wortwörtlich gegen den korrigierten Doku-Wortlaut (4 Assertions) |
| `tasks/codegraph-mcp-server/units/008/result.md` | +22 Z. | F-002: A3-Block-Symmetrie — Dreischritt „Build grün → Test rot → Build grün + Test grün" für A3-1 und A3-3 explizit ergänzt, analog zu A3-2 |
| `tasks/codegraph-mcp-server/units/008/fix-01/result.md` | NEU | dieses Result-Dokument |

Keine Modifikationen an `konzept.md`, `kernel.md`, Rollen-Dateien,
`.agents/rules/**`, `rules.json`, `Docs/integration.md`,
`Docs/ROADMAP.md`, `README.md` oder Produktiv-Code (A5/A7/A8 eingehalten).

## Commit-Hashes

Drei Commits in der Reihenfolge Pflicht-1 → Optional-1 → Optional-3:

1. `700eb4e` — `docs(mcp): agent-api C#-only-zaehlung korrigiert [codegraph-mcp-server]`
2. `5593c91` — `test(mcp): doku-zaehlung-vs-agent-api-md-test [codegraph-mcp-server]`
3. `28d2c5e` — `chore(task): unit 008 fix-01 a3-block-symmetrie [codegraph-mcp-server]`
4. `?????` — `chore(task): unit 008 fix-01 result [codegraph-mcp-server]` (dieses `result.md`; Hash wird nach Commit-Schritt ergänzt, siehe Commit-Log unten)

## A3-Nachweis

### A3-1/2/3 (referenziert aus 008-Lauf)

Die A3-Pfade der 3 bestehenden Tests sind in `units/008/result.md`
ausführlich dokumentiert (Z. 45-95) und bleiben unverändert gültig. Die
3 Tests prüfen Tool-Output gegen Doku-Wortlaut und sind durch F-001
nicht betroffen (sie prüfen weder die C#-only-Zählung in Z. 238 noch
die Fallback-Formulierung). F-002 hat in `units/008/result.md` die
expliziten grünen Rücksprung-Blöcke für A3-1 und A3-3 ergänzt (analog
zu A3-2), damit alle 3 Tests die Dreischritt-Symmetrie aufweisen.

### A3-4 (`AgentApi_CountsCsharpOnlyToolsCorrectly`): Pflicht-A3-Pfad, frisch gefahren

**Strategie-Abweichung von Plan Z. 161-184** (dokumentiert): Die
Plan-Variante (hartkodierter Erwartungs-String in C#) liest die Doku
nicht und kann daher Doku-Drift **nicht** detektieren — der Test wäre
immer grün, egal ob die Doku driftet. Das widerspricht der A3-Methode
(siehe `kernel.md` A3: "neue Tests müssen fehlschlagen können"). Der
Task-Prompt hat die File-Read-Variante als Default empfohlen. Wir
nehmen File-Read auf `Docs/agent-api.md` über `AppContext.BaseDirectory`
+ 5× `..` (von `bin/Debug/net10.0/` zum Repo-Root, dann `Docs/agent-api.md`).
Die `using`-Edits werden durch qualifizierte `System.IO.*`-Aufrufe
vermieden (konsistent zum bestehenden Test-Stil mit `System.StringComparison.*`).

**Build:** grün (0/0, 3.90 s).

**Test grün (korrigierte Doku), wortwörtlich:**
```
Bestanden!   : Fehler:     0, erfolgreich:     4, übersprungen:     0, gesamt:     4, Dauer: 3 s - AiNetLinter.Tests.dll (net10.0)
```
4/4 in 3 s (3 alte Tests + 1 neuer A3-4).

**A3-Auslöser:** Doku Z. 238 temporär auf den ursprünglichen Wortlaut zurückgebogen („7 Tools sind C#-only … search_pattern nutzt auch Nicht-C#-Dateien").

**Test rot (alte Doku), wortwörtlich:**
```
Fehler AiNetLinter.Tests.Mcp.McpDocumentationSmokeTests.AgentApi_CountsCsharpOnlyToolsCorrectly [20 ms]
Fehlermeldung:
 Assert.Contains() Failure: Sub-string not found
String:    "# AiNetLinter — Agent-API Referenz\r\n\r\nKompakte Ref"···
Not found: "6 Tools sind C#-only"
  Stapelverfolgung:
     at AiNetLinter.Tests.Mcp.McpDocumentationSmokeTests.AgentApi_CountsCsharpOnlyToolsCorrectly() in C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.Tests\Mcp\McpDocumentationSmokeTests.cs:line 101
```

xUnit wertet Assertions in Quellcode-Reihenfolge aus, daher schlägt
Assert #1 (`Assert.Contains("6 Tools sind C#-only", …)`) als erstes fehl.
Würde man die Reihenfolge tauschen, würde analog `Assert.DoesNotContain("7 Tools sind C#-only", …)`
fehlschlagen — beide Pfade sind symmetrisch abgesichert.

**A3-Rückgängig:** Doku Z. 238 wieder auf den korrigierten Wortlaut gesetzt.

**Test grün (zurückgebogen), wortwörtlich:**
```
Bestanden!   : Fehler:     0, erfolgreich:     4, übersprungen:     0, gesamt:     4, Dauer: 4 s - AiNetLinter.Tests.dll (net10.0)
```

**Was der A3-Nachweis zeigt:** Der Test liest die Doku-Datei **tatsächlich**
und fängt jeden Drift zwischen Doku-Wortlaut und dem erwarteten
Soll-Wortlaut (4 Assertions). Die 3 bestehenden Tests haben das
methodisch korrekt **nicht** abgedeckt — sie prüfen Tool-Output,
nicht Doku-Fließtext (siehe `units/008/review.md:94-96`). A3-4
schließt diese spezifische Lücke. Der `DoesNotContain("7 Tools sind
C#-only", …)`-Bestandteil fängt die F-001/F-004-Wurzel (Klammer-
Inkonsistenz) in beide Richtungen: Re-Edit zurück auf „7" macht rot,
Edit auf „5" oder beliebige andere Zählung macht auch rot (über
`Contains("6 Tools …")`-Pfad).

## Build/Test-Ergebnis

| Schritt | Befehl | Ergebnis |
|---|---|---|
| Build nach Doku-Korrektur | `dotnet build AiNetLinter.slnx` | grün, 0 Warnungen, 0 Fehler, 9.19 s |
| Unit-Slice | `dotnet test --no-build --filter "Category=Unit"` | grün, 80/80, 15 s |
| Smoke-Slice (3 alte + 1 neuer Test) | `dotnet test --no-build --filter "FullyQualifiedName~McpDocumentationSmokeTests"` | grün, 4/4, 3 s |
| A3-4 isoliert (alter Wortlaut → erwartet rot) | `dotnet test --no-build --filter "FullyQualifiedName~AgentApi_CountsCsharpOnlyToolsCorrectly"` | rot, 0/1 (siehe A3-Block oben) |
| A3-4 isoliert (zurückgebogen → erwartet grün) | `dotnet test --no-build --filter "FullyQualifiedName~AgentApi_CountsCsharpOnlyToolsCorrectly"` | grün, 1/1, ~0 s |
| **Volllauf (Pflicht, AGENTS.md §2)** | `dotnet test --no-build` | **grün, 1165/1165, 4 m 57 s** (vorher 1164, +1 neuer Test = 1165) |

Keine Tests übersprungen, keine flaky Tests, alle 8
`McpServerCommandErrorHandlingTests`/`McpServerCommandStalenessTests`-
Läufe haben im Volllauf korrekt abgeschlossen (siehe die
`[Long Running Test]`-Logs im Output).

## Plan-Erfüllung

| Punkt | Soll | Ist | Status |
|---|---|---|---|
| **F-001** (Pflicht): Doku-Korrektur `Docs/agent-api.md:238` wortwörtlich 1:1 | ja | ja, 1 Z. ersetzt, wortwörtlich wie `review.md:101-108` vorgeschlagen | ✓ |
| **F-002** (optional): A3-Block-Symmetrie in `units/008/result.md` | optional, empfohlen | ja, 2 Blöcke „A3-1 grün (zurückgebogen)" + „A3-3 grün (zurückgebogen)" ergänzt | ✓ mitgenommen |
| **A3-4** (optional): 4. Test `AgentApi_CountsCsharpOnlyToolsCorrectly` | optional, empfohlen | ja, File-Read-Variante statt hartkodierter String (begründet) | ✓ mitgenommen, mit Abweichung |
| F-003 state.md-Hinweis | optional | nein — überlasse ich dem Orchestrator (Plan Z. 337-347: State-Edits normalerweise Orchestrator-Sache), dokumentiere aber im Block „Hinweis an Orchestrator" unten | dokumentiert, nicht umgesetzt |
| Konzept-Diskrepanzen 008 (Z. 539-552, 550, 564) | nicht in fix-01 (A7) | nicht angefasst | ✓ A7 eingehalten |
| F-001 Negativ-Ausschluss („7 Tools", „sind 7", „, search_pattern"-Klammer mit Komma) | prüfen | geprüft: kein solches Pattern im korrigierten Text | ✓ |

## Plan-Abweichungen

1. **Strategie des 4. Tests: File-Read statt hartkodierter String**
   (siehe A3-Block oben). Die Plan-Strategie verträgt sich nicht mit
   dem A3-Anspruch des Plans (Doku manipulieren → Test rot). Begründung
   im Test-Kommentar (Z. 82-85 der Test-Datei). Der Task-Prompt hat die
   File-Read-Variante als Default empfohlen und Reflection als Fallback
   für frickelige Pfade; Reflection wäre overkill (private const
   string, braucht Non-Public-Binding), File-Read ist mit 5× `..`
   robust und prüft 4 Assertions wortwörtlich.

2. **Tool-Name-Assertion mit Backticks:** Die Plan-Variante suchte nach
   `search_pattern ist der vorgesehene Fallback` (ohne Backticks), die
   Doku hat aber `` `search_pattern` `` mit Markdown-Backticks. Erste
   Test-Ausführung war deshalb **rot aus echtem Grund** (siehe Smoke-
   Slice-Failure im A3-Block-Versuch). Test um `` ` `` korrigiert:
   `Assert.Contains("`search_pattern` ist der vorgesehene Fallback", …)`.
   Wertvoller Nebeneffekt: prüft mit, dass der Doku-Stil Markdown-Code-
   Spans für Tool-Namen verwendet (Konsistenz zur Tabelle Z. 242-252).

3. **F-003 state.md-Hinweis nicht umgesetzt:** Plan Z. 337-347 sagt
   explizit, dass State-Edits normalerweise Orchestrator-Sache sind;
   bei Unsicherheit Hinweis nur im `result.md` dokumentieren. Ich
   dokumentiere unten im Block „Hinweis an Orchestrator".

## Commit-Disziplin (A4)

| Punkt | Status |
|---|---|
| Gezielter `git add` pro Datei (kein `-A`, kein `.`) | ✓ — `git add Docs/agent-api.md`, dann `git add src/AiNetLinter.Tests/Mcp/McpDocumentationSmokeTests.cs`, dann `git add tasks/codegraph-mcp-server/units/008/result.md` |
| Conventional Commits in Englisch, Imperativ, `[codegraph-mcp-server]`-Suffix | ✓ — siehe Commit-Hashes oben |
| Kein Push | ✓ — Working-Tree nach 3 Commits clean, Branch `main` ist 13 Commits ahead of `origin/main` (vorher 10, +3 aus fix-01) |
| Kein Amend, kein Force-Push, kein History-Rewrite | ✓ |
| Plan-Abweichungen begründet im `result.md` | ✓ — siehe Plan-Abweichungen-Block oben |
| Working-Tree nach Commits clean | ✓ — `git status` zeigt keine offenen Änderungen vor dem 4. Commit (result.md) |

## Tech-Debt-Beobachtungen

Keine neuen TD-Einträge. Begründung:

- F-001 ist mit dieser Korrektur behoben — kein Anlass für TD-008 o. ä.
- Der 4. A3-Test (A3-4) fängt genau diesen Drift-Typ künftig ab, kein
  Regress-Risiko.
- `Docs/agent-api.md` ist jetzt **interne-konsistent**: Z. 236 (zitierter
  `ServerInstructions`-Block, 6 C#-only-Tools) == Z. 238 (korrigierter
  Fließtext) == Z. 242-252 (Tabelle, 6× `ja` / 3× `nein`). Kein
  weiterer Drift in der Doku.
- `McpServerOptionsFactory.ServerInstructions` (Code-Wahrheit) ist
  weiterhin 1:1 mit Z. 236 identisch — keine Doku-Quote-Drift.
- `McpTruncation.cs:40, 66` (Trunkierungs-Meta-Zeilen) und
  `LinterErrorCodes.cs:10-24` (15 Error-Codes) bleiben unverändert
  korrekt zitiert.

## Nächste Aktion des Orchestrators

→ **Kritiker-Aufruf für 008/fix-01/** (Review-Datei
`tasks/codegraph-mcp-server/units/008/fix-01/review.md`).

Kritiker-Prüfpunkte:

1. **F-001-Korrektur wortwörtlich 1:1** zur Kritiker-Empfehlung in
   `units/008/review.md:101-108` — kein „Re-Edit-Rest", keine
   Re-Interpretation, exakt der vorgeschlagene Satz.
2. **Negativ-Ausschluss** (Plan Z. 143-149): kein „7 Tools", kein
   „sind 7", keine Klammer-Liste mit 7 Items, kein
   „search_pattern nutzt auch Nicht-C#-Dateien" im Fließtext.
3. **A3-4 echt:** A3-Pfad mit Doku-Manipulation gefahren und
   dokumentiert (wortwörtlicher Failure-Output im A3-Block, Zeile
   „Not found: '6 Tools sind C#-only'"). Test ist **nicht** ein
   tautologischer Selbstvergleich, sondern liest die Markdown-Datei
   und prüft 4 Assertions.
4. **F-002 symmetrisch:** A3-1 und A3-3 haben jetzt explizite
   „grün (zurückgebogen)"-Blöcke, identisch strukturiert wie A3-2.
5. **Volllauf 1165/1165 grün** ist Pflicht-Voraussetzung für
   `approved`. 1 Test mehr als 008-Baseline (1164 → 1165) wegen
   A3-4.
6. **Plan-Abweichung (File-Read statt hartkodiert)** begründet und
   im `result.md` dokumentiert. Strategie-Wahl ist verteidigbar
   (Plan widerspricht sich selbst; A3-Methode verlangt echte
   Drift-Detektion; File-Read ist robuster als Reflection auf
   private const string).
7. **Commit-Disziplin A4** eingehalten (3 Commits lokal, kein Push).

### Hinweis an Orchestrator (F-003, nicht vom Coder umgesetzt)

`units/008/review.md:127-135` (F-003) nennt eine Pfad-Differenz Plan vs.
Result beim Self-Lint: Plan nennt `tests/Fixtures/BaselineMini`, Result
nennt `src/BaselineMini/ViolatingClass.cs`. Plan Z. 337-347 sagt:
„State-Edits normalerweise Orchestrator-Sache." Empfehlung: in
`tasks/codegraph-mcp-server/state.md` einen 1-2-Zeilen-Hinweis im
Loop-Protokoll-Block zu 008 ergänzen, dass der reale Self-Lint-Pfad
`src/BaselineMini/ViolatingClass.cs` ist, nicht
`tests/Fixtures/BaselineMini` — als Planer-Memory für künftige
Self-Lint-Schritte.

### Aufruf-Budget nach 008/fix-01

`max_aufrufe`: 23 (Stand 008) + 2 (fix-01 Planer + Coder) = **25/40**
verbraucht, **15/40 verbleibend** für die P0/P1-Rest-Erweiterungen
aus `Docs/ROADMAP.md` (Kaltstart, Auto-Discovery, mtime-Sweep,
Verzeichnis-Sweep neu/gelöscht, `ILintConsole`, Last-Fixture,
`--mcp-log`, stdout-Schutz, 7 weitere Punkte).

`max_fix_pro_einheit` für 008: 0 → 1, **2 verbleibend**.

`max_fix_gesamt`: 1 (002/fix-01) → 2, **10 verbleibend**.

### Working-Tree / Push-Status

Stand nach Coder: 3 Commits lokal, kein Push, Branch `main` 13 Commits
ahead of `origin/main` (vorher 10, +3 aus fix-01; ein 4. Commit für
dieses `result.md` folgt mit Commit-Schritt). Coder wartet auf
`approved` (oder `fix-02`-Freigabe). **Kein Push durch Coder** (A4).
