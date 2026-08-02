---
unit: 008
fix_round: 01
task: codegraph-mcp-server
workflow: dynamic-loop
type: review
created_by: kritiker
created_at: 2026-08-02
trigger: units/008/fix-01/result.md (Commits 700eb4e + 5593c91 + 28d2c5e + 669b07d + 77e5ebf)
trigger_plan: units/008/fix-01/plan.md (Commit 96f1029)
trigger_review: units/008/review.md (Verdict: issues, 1 MAJOR F-001 + 3 MINOR)
---

# Review Einheit 008/fix-01 — F-001 Doku-Drift-Korrektur in `Docs/agent-api.md:238`

## 1. Verdict

**`approved`** — keine CRITICAL/MAJOR-Findings, drei MINOR-Beobachtungen,
alle nicht-befund-relevant. Der MAJOR-F-001 aus `units/008/review.md` ist
wortwörtlich 1:1 nach Kritiker-Vorschlag korrigiert; F-002 (A3-Symmetrie)
und der optionale A3-4-Wortlaut-Test sind sauber mitgezogen; F-003
(state.md-Hinweis) ist korrekt **nicht** vom Coder umgesetzt, sondern als
„Hinweis an Orchestrator" im `result.md` dokumentiert (A4-konform).
Volllauf 1165/1165 grün in 4:57 min, alle A3-Pfade (1-4) echt gefahren
und wortwörtlich protokolliert. 5 Commits lokal, kein Push, kein Amend,
A7 eingehalten.

## 2. Plan-Erfüllung

| Punkt | Soll (Plan) | Ist | Status |
|---|---|---|:---:|
| **F-001 Pflicht:** `Docs/agent-api.md:238` wortwörtlich 1:1 vom Kritiker-Vorschlag | 1 Z. ersetzen, exakter Wortlaut aus `units/008/review.md:101-108` | `git show 700eb4e -- Docs/agent-api.md` zeigt 1 Ins / 1 Del, der eingefügte Satz ist **byte-für-byte identisch** mit dem Kritiker-Vorschlag (verifiziert per Side-by-Side-Diff) | ✓ |
| **F-002 Optional:** A3-Block-Symmetrie in `units/008/result.md` | Dreischritt „Build grün → Test rot → Build grün + Test grün" für A3-1, A3-2, A3-3 explizit | `git show 28d2c5e` zeigt +22 Z. (Plan-Schätzung: 6-8 Z., tatsächlich 22 — mehr als nötig, weil 3 volle Blöcke); A3-1 grün (Z. 60-69), A3-3 grün (Z. 97-106), A3-2 grün (Z. 108-117, leicht versetzt aber vorhanden) | ✓ mitgenommen |
| **A3-4 Optional:** 4. Test `AgentApi_CountsCsharpOnlyToolsCorrectly` | als 4. Methode in `McpDocumentationSmokeTests.cs`, A3-Pfad mit verbogenen Assertions, File-Read oder hartkodiert — beides erlaubt | Position korrekt (nach `FindSymbol_WithWidePattern_TruncatesWithMetaLine` Z. 54-72), 4 Assertions wortwörtlich, File-Read mit Pfad-Resolution + Begründung im Test-Kommentar, A3-Pfad komplett dokumentiert (Build grün 3.90 s → Test rot mit Failure-Wortlaut → Test grün 4/4 in 3 s → Test grün 4/4 in 4 s) | ✓ mitgenommen, mit Abweichung (s. MINOR-1) |
| **F-003:** state.md-Hinweis nicht im Code fixen, Hinweis für Orchestrator | nicht umsetzen, in `result.md` dokumentieren | `git show 700eb4e..669b07d` zeigt **keine** `state.md`-Änderung durch den Coder; `result.md` Z. 232-242 enthält den „Hinweis an Orchestrator"-Block mit Pfad-Empfehlung | ✓ A4-konform |
| **F-004** (Klammer-Inkonsistenz) | mit F-001 behoben | neuer Satz Z. 238 hat keine Klammer-Liste mit `search_pattern`-Eintrag mehr; Suchmuster „search_pattern nutzt auch Nicht-C#-Dateien" ist im korrigierten Text nicht mehr enthalten | ✓ |
| **F-001 Negativ-Ausschluss:** kein „7 Tools", kein „sind 7", keine Klammer mit 7 Items, kein „search_pattern nutzt auch Nicht-C#-Dateien" | prüfen | `git show 700eb4e:docs/agent-api.md` enthält keine dieser Pattern (verifiziert per Augenschein auf den Diff) | ✓ |
| **Konzept-Diskrepanzen** (3 Stück, A7) | nicht in fix-01 | nicht angefasst (`git diff origin/main..HEAD --stat -- konzept.md` ist leer) | ✓ A7 |
| **A4:** kein Push, kein Amend, kein `git add -A` | einhalten | `git status` zeigt 15 Commits ahead of `origin/main`, kein Push, kein Amend; gezielter `git add` pro Datei (Diff-Stat pro Commit bestätigt das) | ✓ |
| **Volllauf** (AGENTS.md §2) | 1165/1165 grün (vorher 1164, +1 Test) | `result.md` Z. 131: 1165/1165 in 4 m 57 s — passt zum User-Kontext (1165/1165 grün in 4:57 min gemessen 2026-08-02 ~19:00) | ✓ |

## 3. Findings

### CRITICAL

Keine.

### MAJOR

Keine.

### MINOR

**MINOR-1: Plan-Verstoß bei Test-Strategie (File-Read), aber methodisch besser.**
Der `fix-01`-Plan Z. 213-216 schreibt explizit:

> Bewusst NICHT in diesem Test: … Kein `File.ReadAllText` auf `Docs/agent-api.md`
> (würde C#-Test von Markdown-Datei abhängig machen — Anti-Pattern, wenn
> der Test dann bei jeder Doku-Umformulierung rot wird, auch wenn der
> Inhalt korrekt bleibt).

Der Coder hat genau das getan, was der Plan verbietet — File-Read über
`AppContext.BaseDirectory` + 5× `..` + `Docs/agent-api.md`. **Aber:** der
Coder hat das sehr stichhaltig begründet (Test-Kommentar Z. 82-85 +
`result.md` Z. 67-77): die Plan-Variante (hartkodierter String) liest die
Doku gar nicht und kann Doku-Drift **nicht** detektieren — der Test wäre
bei einem erneuten Drift zurück auf „7 Tools" immer grün, weil er nur
sich selbst mit sich selbst vergleicht. Das widerspricht der A3-Methode
(`kernel.md` A3: „neue Tests müssen fehlschlagen können"). Der Coder hat
außerdem einen wortwörtlichen A3-Pfad gefahren (Doku temporär zurück auf
„7 Tools" → Test rot mit `Not found: "6 Tools sind C#-only"` → Doku
zurück → Test grün) — der Beweis, dass der Test **tatsächlich** Drift
detektiert.

**Bewertung:** Der Coder hat eine **korrekte** methodische Entscheidung
gegen den **buchstabierten** Plan getroffen. Der Plan ist intern
inkonsistent (A3-Methode verlangt echte Drift-Detektion, schreibt aber
eine Strategie vor, die genau das nicht leistet). Der Coder hat das
erkannt, begründet, im `result.md` dokumentiert (Plan-Abweichungen-Block
Z. 151-158), und einen besseren A3-Beweis geliefert als die
Plan-Variante. **Akzeptabel** — kein Pflicht-Fix, aber für künftige
Planer als Lerneffekt: Plan-Strategien, die A3 widersprechen, sollten
vom Planer selbst nochmal nachgeschärft werden, nicht erst vom Coder
korrigiert werden müssen.

**Auswirkung auf Volllauf:** 0 — der A3-4-Test ist grün im Volllauf,
file-read einer 250-Z.-Markdown-Datei ist im Mikrosekunden-Bereich.

## 4. Sonstige Beobachtungen (informativ)

### A) Backticks-Anpassung im A3-4-Test (methodisch wertvoll)

Der Coder hat bei der ersten Test-Ausführung festgestellt, dass die
Plan-Assertion `Assert.Contains("search_pattern ist der vorgesehene
Fallback", …)` rot war, weil die Doku den Tool-Namen in Markdown-Backticks
setzt (`` `search_pattern` ``). Coder hat das im `result.md` Z. 160-167
dokumentiert und die Assertion auf `` Assert.Contains("`search_pattern`
ist der vorgesehene Fallback", …)`` korrigiert. Diese Korrektur ist
methodisch wertvoll: sie prüft **mit**, dass der Doku-Stil
Markdown-Code-Spans für Tool-Namen verwendet (Konsistenz zur Tabelle
Z. 242-252). Der initiale Rot-Phase-Output wurde leider **nicht** im
A3-Block dokumentiert (nur der finale Rot-Output nach A3-Auslösung), was
einen kleinen A3-Beweis-Schwund bedeutet — aber der Coder hat den
Lerneffekt transparent im Plan-Abweichungen-Block festgehalten.

**Bewertung:** Positiv. Der A3-4-Test prüft jetzt **mehr** als geplant
(5 implizite Assertions: 4 explizite + 1 implizite Markdown-Stil-Konsistenz).

### B) A7-Konformität vollständig bestätigt

`git diff origin/main..HEAD --stat` zeigt nur folgende Dateien geändert
(gekürzt auf 008/fix-01-relevante):

```
Docs/agent-api.md                                  | 130 +++   (aus 008)
.../Mcp/McpDocumentationSmokeTests.cs              | 106 +++   (aus 008 + 33 in 5593c91)
.../codegraph-mcp-server/units/008/fix-01/plan.md  | 446 ++++++ (vom Planer)
.../units/008/fix-01/result.md                     | 261 +++++ (vom Coder)
tasks/codegraph-mcp-server/units/008/result.md     | 191 +++++ (+22 in 28d2c5e)
```

**Keine** Änderung an:
- `tasks/codegraph-mcp-server/konzept.md` (A7) ✓
- `.agents/rules/**` (A7) ✓
- `.agents/Agent-Scaffolding/**` (A8) ✓
- `rules.json` (A5) ✓
- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` (A5) ✓
- `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` (A5) ✓
- `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` (A5) ✓
- `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` (A5) ✓
- `kernel.md`/Rollen-Dateien (A8) ✓
- `Docs/integration.md`, `Docs/ROADMAP.md`, `README.md` (A5) ✓

### C) A4-Konformität (Commit-Disziplin)

| Aspekt | Status |
|---|---|
| 5 Commits lokal, alle A4-konform | ✓ (700eb4e, 5593c91, 28d2c5e, 669b07d, 77e5ebf) |
| Gezielter `git add` pro Datei (kein `-A`/`.`) | ✓ — jeder Commit betrifft genau 1 Datei, verifiziert per `git show <hash> --stat` |
| Conventional Commits englisch, Imperativ, `[codegraph-mcp-server]`-Suffix | ✓ (alle 5 Messages) |
| Kein Push | ✓ — `main` 15 Commits ahead of `origin/main`, kein Push ausgeführt |
| Kein Amend, kein Force-Push, kein History-Rewrite | ✓ |
| Working-Tree nach Coder-Aufruf clean (außer state.md vom Orchestrator) | ⚠ siehe Punkt D) |

### D) `state.md` im Working-Tree modifiziert (Orchestrator, nicht Coder)

`git status` zeigt `modified: tasks/codegraph-mcp-server/state.md` (nicht
gestaged). Der `git diff` zeigt, dass das **Loop-Protokoll-Update** für
008 und 008/fix-01 in `state.md:684+` eingefügt wurde — typische
Orchestrator-Aufgabe. Der Coder hat `state.md` **nicht** angefasst (kein
Commit der Coder-Fix-01-Commits betrifft `state.md`, verifiziert per
`git show 700eb4e..77e5ebf --stat`).

**Bewertung:** Kein A4-Verstoß durch den Coder. Der uncommittete
state.md-Stand ist Sache des Orchestrators und gehört in dessen
nächsten `chore(task): state.md update`-Commit. **Kein Finding.**

### E) Konzept-Diskrepanzen aus 008 (A7-Sache, dokumentiert)

Die 3 in `units/008/result.md:129-142` dokumentierten
Konzept-Diskrepanzen (`konzept.md` Z. 539-552 Tool-Status-Tabelle, Z. 550
`get_impact`-Beschreibung, Z. 564 Kaltstart-Suggestion) sind explizit
nicht in `fix-01` (A7), und der Coder hat sie nicht angefasst. Passt zu
meiner Empfehlung im 008-Review (Z. 329-335): separate Konzept-Pflege-
Einheit beim Nutzer.

### F) Volllauf 1165/1165 grün in 4:57 min (Pflicht-Voraussetzung erfüllt)

| Test-Slice | Befehl | Ergebnis |
|---|---|---|
| Smoke-Slice (3 alte + 1 neuer Test) | `dotnet test --no-build --filter "FullyQualifiedName~McpDocumentationSmokeTests"` | grün, 4/4, 3 s (zwei Läufe dokumentiert) |
| A3-4 isoliert (korrigierte Doku) | `dotnet test --no-build --filter "FullyQualifiedName~AgentApi_CountsCsharpOnlyToolsCorrectly"` | grün, 1/1, ~0 s |
| A3-4 isoliert (alte Doku, A3-Auslöser) | dto. | rot, 0/1, mit wortwörtlichem Failure `Not found: "6 Tools sind C#-only"` an Z. 101 |
| A3-4 isoliert (zurückgebogen) | dto. | grün, 1/1, ~0 s |
| **Volllauf** | `dotnet test --no-build` | **grün, 1165/1165, 4 m 57 s** (vorher 1164, +1 Test) |
| Build | `dotnet build AiNetLinter.slnx` | grün, 0 Warnungen, 0 Fehler, 9.19 s (Pflicht wegen `TreatWarningsAsErrors=true`) |

### G) F-003-Hinweis für künftige Planer (vom Coder dokumentiert, nicht umgesetzt)

Der Coder hat im `result.md` Z. 232-242 den F-003-Hinweis korrekt als
„Hinweis an Orchestrator" formuliert mit der Empfehlung, in `state.md`
einen 1-2-Zeilen-Eintrag im Loop-Protokoll-Block zu 008 zu ergänzen, dass
der reale Self-Lint-Pfad `src/BaselineMini/ViolatingClass.cs` ist (nicht
`tests/Fixtures/BaselineMini` wie im Plan stand). Das ist die vom Plan
Z. 337-347 explizit vorgesehene Vorgehensweise (State-Edits
normalerweise Orchestrator-Sache). **Sauber.**

### H) Test-Datei-Check (AiNetLinter.mdc-Stilregeln)

`McpDocumentationSmokeTests.cs` nach dem Edit: 105 Z. (vorher 72 Z.,
+33 Z. für den 4. Test in Commit 5593c91). Plan-Schätzung war ~92 Z.,
tatsächlich 105 Z. — minimal über Schätzung, weil der File-Read-Code
etwas mehr Boilerplate braucht als ein hartkodierter String. **Weit unter
500-Z.-Linter-Limit** (kein Regress). Klasse bleibt `sealed`
(Z. 18, korrekt).

### I) Beobachtung: Test-Datei `McpDocumentationSmokeTests.cs` als 4. Test in der `IClassFixture<McpLiveRepositoryFixture>`-Klasse

Der neue 4. Test `AgentApi_CountsCsharpOnlyToolsCorrectly` ist `void`
(nicht `async Task`) und nutzt die `McpLiveRepositoryFixture` nicht
trotz Klassen-Deklaration. **Bewertung:** OK, kein Test-Infra-Impact.
Die Fixture wird im Konstruktor zugewiesen, das ist normales xUnit-Pattern
auch für synchrone Tests. Der Test wäre theoretisch auch in einer
eigenen Klasse ohne `IClassFixture` denkbar, aber dann hätte der Coder
eine 2. Test-Datei anlegen müssen (Plan-Verstoß: „Keine weiteren
Dateien" in der Pflicht-Änderungs-Tabelle Z. 79-93). Die Wahl, den
Test in die bestehende Klasse zu legen, ist die **minimal-invasivere**
Lösung — methodisch korrekt. Kein Finding.

## 5. A7-Konformität (Zusammenfassung)

`konzept.md` ist im Working-Tree unverändert (`git diff origin/main..HEAD
--stat -- konzept.md` ist leer). Der Coder hat in `fix-01` **null**
Edits an Konzept-Datei, Projektregeln, `kernel.md`, Rollen-Dateien,
`rules.json` oder anderen, vom Plan als „nicht in fix-01" markierten
Dateien gemacht. Die 3 in 008 dokumentierten Konzept-Diskrepanzen bleiben
offen für eine separate Konzept-Pflege-Einheit beim Nutzer — A7 wird
eingehalten.

## 6. Tech-Debt-Vorschläge

**Keine neuen TD-Einträge.** Begründung analog zum 008-Review
Z. 274-291, plus:

- F-001 ist Doku-interner Drift im **eigenen** Scope dieser Einheit,
  behoben — kein TD-Anlass.
- Der A3-4-Test fängt genau diesen Drift-Typ künftig ab
  (File-Read + 4 Assertions), kein Regress-Risiko.
- `Docs/agent-api.md` ist jetzt **intern-konsistent**:
  - Z. 236 (zitierter `ServerInstructions`-Block, 6 C#-only-Tools) ==
  - Z. 238 (korrigierter Fließtext, 6 + 2 + Fallback) ==
  - Z. 242-252 (Tabelle, 6× `ja` / 3× `nein`).
  Alle drei Stellen stimmen wortwörtlich überein.
- `McpServerOptionsFactory.ServerInstructions` (Code-Wahrheit, Z. 26-31)
  bleibt 1:1 mit Z. 236 identisch — keine Doku-Quote-Drift.
- MINOR-1 (Plan-Verstoß File-Read) ist methodisch korrekt, kein TD.

## 7. Zusammenfassung (für Orchestrator)

### Verdict

**`approved`** — keine CRITICAL/MAJOR-Findings, ein MINOR (Plan-Verstoß
File-Read, methodisch besser als Plan-Variante, akzeptabel).

### Empfehlung an Orchestrator

1. **Push der 5 lokalen Commits** nach `origin/main` — A4 erlaubt Push
   nach `approved`. Commit-Reihenfolge:
   - `700eb4e` docs(mcp): agent-api C#-only-zaehlung korrigiert
   - `5593c91` test(mcp): doku-zaehlung-vs-agent-api-md-test
   - `28d2c5e` chore(task): unit 008 fix-01 a3-block-symmetrie
   - `669b07d` chore(task): unit 008 fix-01 result
   - `77e5ebf` chore(task): unit 008 fix-01 result-hash-nachtrag
2. **State.md-Update** des Orchestrators in einem eigenen Commit
   (Loop-Protokoll-Block 008/fix-01 + F-003-Hinweis vom Coder
   übernehmen — ist die einzige noch ausstehende State.md-Pflege).
3. **008 ist komplett abgeschlossen** (1 erfolgreiche Fix-Runde).
4. **Planer für 009 aufrufen** — nächste Einheit aus `state.md` Block
   „Nächste Aktion" (P0/P1-Rest-Erweiterungen: Kaltstart, Auto-Discovery,
   mtime-Sweep, Verzeichnis-Sweep neu/gelöscht, `ILintConsole`,
   Last-Fixture, `--mcp-log`, stdout-Schutz, 7 weitere Punkte gemäß
   `Docs/ROADMAP.md`).

### Aufruf-Budget

Nach `008/fix-01/`:

- `max_aufrufe`: 25/40 (siehe `result.md` Z. 246-250), **15/40
  verbleibend** für die P0/P1-Rest-Erweiterungen. Mit diesem
  Kritiker-Aufruf (1 weiterer) → **26/40 verbraucht, 14/40 verbleibend**.
- `max_fix_pro_einheit` für 008: 1 (0 → 1), **2 verbleibend**.
- `max_fix_gesamt`: 2 (002/fix-01 + 008/fix-01), **10 verbleibend**.

### Hinweis an den Nutzer (A7-Sache, nicht Teil von 008/008-fix-01)

`konzept.md` enthält 3 veraltete Stellen (Z. 539-552 Tool-Status-Tabelle,
Z. 550 `get_impact`-Beschreibung, Z. 564 Kaltstart-Suggestion) aus
008-Review Z. 329-335. Empfehlung: bei nächster Konzept-Pflege-Gelegenheit
in einer eigenen Einheit an Code-Stand anpassen — nicht Teil dieser
Fix-Runde, weil A7 Konzept-Edits durch den Coder verbietet.

### Working-Tree / Push-Status

Stand nach Coder: 5 Commits lokal (`700eb4e`, `5593c91`, `28d2c5e`,
`669b07d`, `77e5ebf`), kein Push durch Coder (A4). Branch `main` ist
15 Commits ahead of `origin/main` (vorher 10, +5 aus 008/fix-01 inkl.
Planer-Plan-Commit `96f1029`). `state.md` ist im Working-Tree modifiziert
vom Orchestrator, nicht vom Coder — separater `chore(task):`-Commit
nach `approved` empfohlen.

**Kritiker pusht nicht (A4).**
