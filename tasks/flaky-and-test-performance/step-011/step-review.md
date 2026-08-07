---
status: done
type: step-review
task: flaky-and-test-performance
step: 011
epic: EPIC-02
step_type: batch
reviewed_by: kritiker
reviewed_by_model: MiniMax-M3
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-08T15:02:00+02:00
verdict: approved
tech_debt_ids: []
---

# Review Step 011: Category-Traits für Core/Checkers-Rest (12 Klassen M–W) und Core-Anfang (8 Klassen A–LinterEngineCache)

## Verdict

- [x] **approved** — alle vier Prüfebenen ok, keine CRITICAL/MAJOR-Findings
- [ ] **issues** — Korrektur-Step `step-<MMM>` angelegt (`corrects: step-011`)
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [x] Plan-Erfüllung: alle 20 Items umgesetzt, DoD-Punkte adressiert
- [x] Rules-Konformität: gegen die im `step-plan.md` §"Rules-Refs" zitierte Auswahl geprüft
- [x] Logische Korrektheit: Trait-Insertionen, BOM, EOL, Methoden-Inventar verifiziert
- [x] Konzept-Treue: passt zu `konzept.md` §Muss-Haven "Category-Traits auf allen Tests", keine Non-Goals verletzt
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (Filter-Delta exakt Plankonform)

## Befund

### Plan-Erfüllung

Alle 20 Items umgesetzt, je genau eine `[Trait("Category", "Unit")]`-Zeile an der spezifizierten Position; BOM für die 6 BOM-tragenden Dateien byte-genau erhalten (`EF BB BF` vor/nach Edit per `[System.IO.File]::ReadAllBytes` über alle 6 Dateien verifiziert), EOL uniform CRLF + Trailing-NL für alle 20 Dateien (Pflicht-Vollscan), `#nullable enable` für `LinterAnalyzerTests.cs` nicht hinzugefügt (Out-of-Scope-Disziplin gewahrt), `CompoundSuppressionIntegrationTests` korrekt als `Unit` getaggt (Heuristik-Punkt 2, 0/6 Subprozess-Marker bestätigt), CodeMap aktualisiert (`Core/Checkers/` 27/27 abgehakt, `Core/` 8/19 done), `tasks/flaky-and-test-performance/step-011/` enthält nur `step-plan.md` + `step-result.md` (keine Coder-Hilfsdateien-Leichen, Working Tree clean).

### Rules-Konformität

Gegen die in `step-plan.md` §"Rules-Refs" zitierte Auswahl geprüft: `AiNetLinterRichtlinien.mdc` §4 (Commit-Vorschlag Pflicht + Conventional Commits deutsch/imperativ + Testsuite-Parallelität bewahren), §5 (Zero-Warning-Direktive, Sparsame Kommentare, Symptom-Fixing verboten) und `AiNetLinter.mdc` (`EnforceSealedClasses` für `*.Tests` aufgehoben, `EnforceNullableEnable` toleriert fehlende Direktive in `LinterAnalyzerTests.cs` analog TD-004, `MaxMethodLineCount: 100` eingehalten) — alle eingehalten; einzige Beobachtung ist die Subject-Länge des Korrektur-Commits (siehe "Sonstige Beobachtungen"), die als MINOR außerhalb des Step-Scopes liegt.

### Logische Korrektheit

Eigene Plausibilitätsprüfung bestätigt: 149 `[Fact]` + 4 `[Theory]` = 153 Methoden, 27 `[InlineData]`-Expansionen ausschließlich in `UiFileSeparationCheckerTests` (12+8+3+4 verifiziert) = 176 Test-Cases; 0/20 Dateien mit String-Literal-`[Fact]`-Verschachtelung; 0/20 Dateien mit Subprozess-Marker; Trait-Positionen entsprechen der etablierten Bibliothek (18× Standard-Insert zwischen `namespace`/`class`, 2× XML-Doc-Variante zwischen `</summary>`/`class` für `AutoFixerTests` und `DiffImpactAnalyzerTests` — beide byte-genau auf den richtigen Zeilen positioniert, Interface `: IDisposable` für `UiFileSeparationCheckerTests` und `LinterEngineCacheTests` bleibt unverändert in der Klassensignatur).

### Konzept-Treue (Ebene 4)

Konzept §"Muss-Haven"-Punkt "konsequente Category-Traits auf **allen** Tests" wird mit diesem Step messbar vorangebracht (+176 Test-Cases getaggt, `Core/Checkers/`-Ordner vollständig abgehakt, `Core/`-Ordner zu 8/19 eröffnet); Konzept §"Wie" Schritt 2 (Category-Traits nachziehen) exakt befolgt; keine der in `konzept.md` §"Non-Goals" aufgeführten Ausschlüsse berührt (kein Test-Framework-Wechsel, kein sichtbares Verhalten des MCP-Servers/CLI geändert, kein festes Zeitbudget-Abnahmekriterium, keine CI-Workflow-Einführung); Scope-Disziplin sauber: nur die 20 explizit gelisteten Dateien modifiziert, `git show bb39619 --stat` bestätigt exakt 20 Dateien in `Core/` (8) + `Core/Checkers/` (12), keine andere Datei angefasst.

### Build-/Test-Status

```
dotnet build                                                        → grün (0 Warnungen, 0 Fehler, 4,5 s)
dotnet test --no-build                                              → grün (1325 Tests, 0 Fehler, 2 m 6 s)
dotnet test --no-build --filter "Category=Unit"                     → grün (882 Tests, 0 Fehler, 13 s) — exakt Plankonform (+176 vs. step-010 = 706)
dotnet test --no-build --filter "Category=Integration"              → grün (113 Tests, 0 Fehler, 1 m 48 s) — unverändert ±0
dotnet run --project src/AiNetLinter -- --config rules.json --path . → OK
```

## Sonstige Beobachtungen / MINOR / NITPICK

- **Korrektur-Commit `daad777` Subject 77 Zeichen (5 über TD-002 72-Grenze, nicht 73 wie im Coder-Briefing angedeutet):** `docs(tasks): step-011 step-result Hash-Korrektur [flaky-and-test-performance]`. Die abweichende Zahl im Briefing (73) übersah die Wörter "step-result" + "Hash-Korrektur" zusammen mit dem Suffix — eigene Nachzählung ergibt **77 Zeichen**. Der Verstoß ist **dokumentiert und nicht korrigierbar**, weil `spec.md` §10.7 History-Reset absolut verbietet (`git commit --amend` als einzig möglicher Korrektur-Pfad) und der Coder die kürzere Alternative `docs(tasks): step-011 Hash-Korrektur [flaky-and-test-performance]` (61 Zeichen) explizit benannt hat. Funktional nicht betroffen (reine Commit-Message-Form), Severity nach `kritiker/SKILL.md` Severity-Gating MINOR. **Kein** neuer TD-Eintrag: TD-002 (Priorität niedrig) erfasst bereits denselben Mechanismus, ein weiterer Eintrag würde TD-002 zur reinen Pro-Forma-Liste aufblähen ohne neuen Inhalt. Stattdessen: **Empfehlung an den Planer für Folge-Steps** — die Coder-Pipeline-Konvention "Code-Commit zuerst, Hash direkt in `step-result.md` referenzieren (statt Placeholder), Doku-Commit danach" sollte im DoD verankert werden, damit dieser Workaround strukturell überflüssig wird. Der Korrektur-Commit selbst ist transparent und Spec-konform (kein History-Rewrite), die Datei `step-result.md` enthält nach `daad777` den korrekten Hash `bb39619` (verifiziert).
- **Mega-Batch-Effizienz (positiv):** mit dem gelockerten `config.md`-Deckel (`max_batch_items: 20`, `max_batch_diff_lines: 80`) deckt step-011 20 Klassen in 3 Commits (~0,15 Commits/Klasse) ab — vorherige Batches benötigten 2 Commits für 3–8 Klassen (0,25–0,67 Commits/Klasse). Die Bündelung funktioniert wie intendiert.

## Tech-Debt-Einträge aus diesem Review

Keine — TD-002 (bestehend, Priorität niedrig) deckt den beobachteten Subject-Längen-Verstoß-Mechanismus bereits ab; ein weiterer Eintrag wäre redundant. Beobachtungen außerhalb des Step-Scopes (BOM-Inhomogenität in `Core/Checkers/` und `Core/`: 6/20 mit BOM in step-011, 11/27 im Gesamtordner — entspricht TD-006-Heuristik-Punkt 8; `#nullable enable` fehlt in `LinterAnalyzerTests.cs` — entspricht TD-004-Heuristik für `Output/`) sind als step-011-interne Konsolidierungs-Beobachtungen bereits im `step-result.md` §"Bekannte Unschärfen" dokumentiert und bleiben in den offenen TD-004/TD-006-Einträgen erfasst.
