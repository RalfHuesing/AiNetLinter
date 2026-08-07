---
status: done
type: step-review
task: flaky-and-test-performance
step: 009
epic: EPIC-02
step_type: batch
reviewed_by: kritiker
reviewed_by_model: MiniMax-M3
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-07T14:02:00+02:00
verdict: approved
tech_debt_ids: [TD-005]
---

# Review Step 009: Category-Traits für Configuration-Tests nachziehen (Batch 8)

## Verdict

- [x] **approved** — alle vier Prüfebenen ok, ein neuer Tech-Debt-Eintrag (TD-005, BOM-Inhomogenität) angelegt
- [ ] **issues** —
- [ ] **blocked** —

## Geprüft

- [x] Plan-Erfüllung: alle 8 Items umgesetzt (Traits an korrekten Zeilen, BOM/EOL/TrNL konserviert, CodeMap-Update erfolgt)
- [x] Rules-Konformität: `AiNetLinterRichtlinien.mdc` §4 (Subject-Länge) und §5 (Kommentar-Disziplin) eingehalten, `AiNetLinter.mdc`-Regeln nicht verletzt
- [x] Logische Korrektheit: Trait-Mechanik sauber, BOM-Bytes byte-genau verifiziert, String-Literal-`[Fact]` bestätigt, Filter-Delta korrekt erklärt
- [x] Konzept-Treue: entspricht EPIC-02-Muss-Haven „alle Tests tragen einen Category-Trait" für `Configuration/`-Ordner, keine Non-Goals berührt
- [x] Build: selbst nachgeprüft, grün (0/0)
- [x] Tests: selbst nachgeprüft, grün (Volllauf 1325/1325, Unit-Filter 656/656)
- [x] Self-Lint: selbst nachgeprüft, OK

## Befund

### Plan-Erfüllung
Alle 8 Items exakt wie geplant umgesetzt: Trait-Insert an den vorgegebenen Zeilen (Z. 16, 10, 5, 10, 32, 12, 8, 10), class-Zeile jeweils +1 verschoben (Z. 17, 11, 6, 11, 33, 13, 9, 11), Zeilenzahlen pro Datei jeweils +1, CodeMap-`Configuration/`-Eintrag auf `(zuletzt: step-009)` aktualisiert; BOM-Konservierung, EOL/TrNL-Erhaltung und Datei-Scope (nur `Configuration/`, 8 Dateien im Code-Commit) allesamt verifiziert.

### Rules-Konformität
`AiNetLinterRichtlinien.mdc` §4 eingehalten: Code-Commit-Subject 71 Zeichen (1 Reserve zur 72-Grenze, inkl. `[flaky-and-test-performance]`-Suffix, TD-002-Disziplin erfüllt), Doku-Commit-Subject 67 Zeichen; §5 (sparsame Kommentare) erfüllt — der Coder hat keine Kommentare mit `step-`/`TD-`/`EPIC-`-Verweisen eingeführt, rein additiver Trait-Insert; §6 (Zero-Warning) erfüllt — `dotnet build` mit `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` ist mit 0/0 grün, `[Trait(...)]` als xUnit-Standardattribut führt keine neuen Warnings ein.

### Logische Korrektheit
BOM-Scan (4/4 BOM-Dateien `EF BB BF` vor und nach Edit), EOL-Vollscan (alle 8 Dateien uniform CRLF, TrNL jeweils `0D 0A` am Dateiende), Class-Zeile-Trait-Reihenfolge (Trait über class) und String-Literal-`[Fact]` in `AgentFeaturesTests.cs:241` (innerhalb `const string testClass = """ ... """`, Z. 236–247) allesamt durch eigene Byte-/Regex-/Lese-Tools bestätigt; Per-Klasse-Summenprobe des Coders (15+4+3+9+11+12+10+3 = 67, also Unit-Filter 589→656 statt Plan 589→657, Delta −1 durch genau diesen String-Literal-`[Fact]`) ist konsistent mit dem xUnit-Lauf und korrekt dokumentiert.

### Konzept-Treue (Ebene 4)
Konzept-Muss-Haven-Punkt „konsequente Category-Traits auf allen Tests" ist für den gesamten `Configuration/`-Ordner (8/8 Klassen) erfüllt — `Configuration/` damit der achte vollständig abgeschlossene EPIC-02-Batch; keine Non-Goals verletzt (kein Framework-Wechsel, keine Änderung am sichtbaren MCP-/CLI-Verhalten, keine CI-Workflow-Einführung); keine `Configuration/`-fremden Dateien angefasst (Scope-Disziplin über `git show b484627 --name-only` verifiziert: ausschließlich 8 Dateien in `Configuration/`).

### Build-/Test-Status

```
dotnet build                                                      → grün (0 Warnungen, 0 Fehler, 2.07 s)
dotnet test --no-build                                            → grün (1325/1325, 2 m 16 s)
dotnet test --no-build --filter "Category=Unit"                   → grün (656/656, 13 s)
dotnet test --no-build --filter "Category=Integration"            → 1 Flake (pre-existing EPIC-06, isoliert 50 ms grün — DoD-toleriert)
dotnet run --project src/AiNetLinter -- --config rules.json --path . → OK
```

Der Integration-Filter-Flake (`McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`, duration 5.04 s = exakt die Poll-Loop-Deadline) reproduziert sich unter Last, ist aber isoliert nach 50 ms grün und gehört damit in den EPIC-06-Scope (Poll-Loop → Event-basiertes Warten) — kein step-009-Finding.

## Sonstige Beobachtungen / MINOR / NITPICK

- **TD-002-Disziplin eingehalten:** Code-Commit `b484627` 71 Zeichen (1 Reserve), Doku-Commit `b4a8c59` 67 Zeichen — beide deutlich unter der 72-Grenze, inkl. `[flaky-and-test-performance]`-Suffix. Damit ist step-009 ein weiterer Step in der Reihe, der die Subject-Längen-Vorgabe sauber erfüllt; die TD-002-Eintrag-Diagnose (Planer-DoD-Vorgaben ungenau, Skill-Präzisierung nötig) bleibt davon unberührt — der Coder hat die korrekte Länge **trotz** DoD-Vorgabe „71 Zeichen, 1 Zeichen Reserve" reproduziert, also eigene Disziplin bewiesen.
- **NITPICK-Linie: String-Literal-`[Fact]`-Ausschluss-Methodik im Plan-DoD für Folge-Steps ergänzen.** Der Planer hat in `step-009/step-plan.md` 16 `[Fact]` für `AgentFeaturesTests.cs` gezählt, real existieren nur 15 echte xUnit-Tests — der 16. `[Fact]` sitzt in `AgentFeaturesTests.cs:241` innerhalb eines `const string testClass = """ ... """`-Roh-String-Literals (Linter-Engine-Test-Input-Daten), wird vom xUnit-Runner korrekt ignoriert. Der Coder hat den Mis-count transparent dokumentiert und nicht „korrigierend" in den Trait-Insert eingegriffen — saubere Selbstkorrektur. Für die EPIC-02-Folge-Batches (`Core/Checkers/`, `Core/`, `Maps/`, `Mcp/`, `Commands/`, `Cli/`, `Baseline/`) sollte der Planer die Methoden-Inventur **mit** String-Literal-Ausschluss machen, z. B. `Select-String` über `\[(Fact|Theory)\]` minus Vorkommen, deren Zeile in einem offenen `"""`/`$"..."`/`@"..."`-Block liegt. Konkret: die aktuelle `Select-String`-basierte Variante in `step-plan.md` DoD zählt **alle** Treffer, das ist gut für die Methoden-Identifikation (Trait-Insert ist korrekt), aber irreführend für die Test-Case-Prognose (Filter-Delta). Empfehlung: Planer-Aufruf trennt künftig **„Klassen mit xUnit-Tests"** (regex-basiert, akzeptabler Over-count) von **„erwarteter Unit-Filter-Delta"** (per `dotnet test` gegen ein Ephemeron-File vor dem Edit, oder durch expliziten String-Literal-Ausschluss-Pass).
- **Heuristik-Punkt 7 (BOM-Inhomogenität) als positives Beispiel systematischer Heuristik-Beobachtung.** Die Planer-Heuristik-Punkte 1–6 sind alle etabliert und bestätigt; Punkt 7 (neu in step-009) ist analog zu TD-003 (EOL-Inhomogenität) und TD-004 (Nullable-Inhomogenität) — eine **Repository-weite Konsistenz-Frage ohne funktionale Auswirkung**, deren Konsolidierung `git add --renormalize .` + Commit oder eine `.gitattributes`-Änderung wäre. Wegen der strukturellen Analogie zu TD-003 (gleiche Beobachtungs-Klasse, gleicher vorgeschlagener Lösungs-Pfad, gleicher `auto_fixable: nein`-Status) als TD-005 angelegt (siehe unten) — Planer-Vermerk „in spe" im step-009-Plan ist eine bewusste Beobachtung ohne Konsolidierungs-Auftrag, der Kritiker kann die Beobachtung **tracked** im `tech-debt.md` ablegen, ohne in step-009-Scope einzugreifen.
- **`Configuration/`-Schnitt-Entscheidung bewährt:** 8 Klassen homogen Unit, exakt am 8-Item-Deckel, **kein** Misch-Batch mit anderen Ordnern, **kein** Split in 4+4 — die etablierte „1 Ordner = 1 Batch"-Linie (step-002..step-008) ist konsequent durchgehalten. Der Folge-Planer kann `Configuration/` als abgehakt voraussetzen.

## Tech-Debt-Einträge aus diesem Review

- `TD-005` (siehe `tech-debt.md`) — BOM-Inhomogenität in `src/AiNetLinter.Tests/Configuration/` (4/8 mit UTF-8-BOM, 4/8 ohne), analog zu TD-003 (EOL) und TD-004 (Nullable) — Repository-weite Konsistenz-Frage ohne funktionale Auswirkung, byte-genau konserviert in step-009, Konsolidierung als `git add --renormalize .`-Aufräum-Item denkbar (Nutzer-Entscheidung).
