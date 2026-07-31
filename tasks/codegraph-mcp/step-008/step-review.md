---
status: done
type: step-review
task: codegraph-mcp
step: 008
epic: EPIC-04
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-07-31T17:10:00Z
verdict: approved
tech_debt_ids: [TD-006]
---

# Review Step 008: get_index_scope Tool (Dateityp-Aufschlüsselung der Solution)

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues**
- [ ] **blocked**

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün

## Befund

Alle sechs Plan-Dateien 1:1 umgesetzt, inkl. der dokumentierten Abweichung
(`GetIndexScopeScanner.cs`-Auslagerung, im DoD bereits als Eventualität
vorgesehen). Rules eingehalten (`AIContextFootprint` unter 2500 nach
Auslagerung, `#nullable enable`, Result-Pattern über `SolutionNotLoaded()`,
kein DI-Container). Logik stimmt inkl. Edge-Cases (verschachtelte
Verzeichnisse, `obj`/`bin`-Ausschluss, Groß-/Kleinschreibung der Endungen —
alle drei von mir unabhängig mit einem eigenen, temporären Test gegen die
Fixture nachgewiesen, danach wieder entfernt). Konzept-Treue gegeben: die
Aufteilung `.cs` (Symbolgraph) / `.css`/`.js`/`.razor` (`WebFileCatalog`) /
`.xaml`/`.html` (neuer, minimaler Scan) entspricht der Tool-Tabelle in
`konzept.md`, kein Non-Goal verletzt (keine Cross-Language-Symbolgraph-
Verknüpfung, reine Zählung).

### Plan-Erfüllung

Alle sechs "Konkrete Änderungen"-Punkte erfüllt: Sichtbarkeitsanhebung
`GetProjectDirectories`, `GetIndexScopeTool`/`GetIndexScopeScanner`,
Registrierung in `FileStructureToolRegistrations`, Fixture-Erweiterung
(`wwwroot/` mit je einer Datei pro Typ), Unit-Tests (5), E2E-/Tool-Count-
Test aktualisiert. Alle sieben Tests aus dem Plan-Abschnitt „Tests"
vorhanden und grün (per `git show` und eigenem Testlauf verifiziert).
Alle DoD-Punkte erfüllt, inkl. Selbst-Lint-Footprint-Kontrolle (siehe
unten) und Dogfooding-Abschnitt in `step-result.md`.

### Rules-Konformität

`AiNetLinter.mdc` (`AIContextFootprint` 2500, `#nullable enable`, statische
Klassen): eingehalten — `GetIndexScopeTool` (2413) und
`FileStructureToolRegistrations` (2434) unabhängig per eigenem
`--footprint`-Lauf bestätigt (siehe Build-/Test-Status). Beide neuen
Klassen `#nullable enable`, `internal static`. `AiNetLinterRichtlinien.mdc`
(kein DI-Container, Result-Pattern, Build/Test-Pflicht, Commit-Vorschlag-
Pflicht): eingehalten — `GetIndexScopeTool` erreicht `McpCodeGraphServer`
weiterhin per Delegate-Closure, `SolutionNotLoaded()`-Kurzform statt
Exception, Commit-Message vorhanden und aussagekräftig.

### Logische Korrektheit

Zählung korrekt für alle sechs Zweige, per eigenem Testlauf gegen die
erweiterte `SymbolGraphMini`-Fixture bestätigt (`.cs: 4`, `.css`/`.js`/
`.razor: je 1`, `.xaml`/`.html: je 1`). Eigene, zusätzliche Stichproben
(temporärer Test, nach Verifikation wieder gelöscht, `git status` danach
sauber) bestätigen zwei vom Coder nicht separat getestete Edge-Cases:
(1) drei Verzeichnisebenen tief verschachtelte `.xaml`/`.html`-Dateien
werden korrekt mitgezählt (`Directory.EnumerateFiles(..., AllDirectories)`
arbeitet wie erwartet), (2) Groß-/Kleinschreibung der Endung (`.XAML`,
`.HTML`) wird dank `OrdinalIgnoreCase` korrekt erkannt. `bin/`-Ausschluss
(zusätzlich zum vom Coder bereits getesteten `obj/`-Fall) ebenfalls
bestätigt. Keine Doppelzählung zwischen dem neuen Scan und
`WebFileCatalog.Collect`, da `WebFileType` nur `Css`/`Js`/`Razor` kennt —
kein Überschneidungsrisiko mit `.xaml`/`.html`.

### Konzept-Treue (Ebene 4)

Tool-Tabellen-Zeile aus `konzept.md` (`get_index_scope | keins |
Dateityp-Aufschlüsselung ... | SourceFileCatalog.GetSourceFiles/
WebFileCatalog.Collect`) wird sinngemäß erfüllt; die im Plan dokumentierte
Korrektur (`.cs`-Zählung über `SourceFileCatalog.IsValidDocument` statt
`GetSourceFiles`, `.xaml`/`.html` über neuen Scan statt vollständiger
Wiederverwendung) ist im JIT-Kontext des Plans nachvollziehbar begründet
(`GetSourceFiles` würde `Config`/`LinterArgs` voraussetzen, die
`McpCodeGraphServer` nicht hat) und stellt keine Abweichung vom
eigentlichen Muss-Haben („Explizite Scope-Kommunikation") dar — der Text
kommuniziert weiterhin klar abgedeckt vs. nicht abgedeckt. Kein Non-Goal
("Kein Cross-Language-Symbolgraph") verletzt: der neue Scan zählt nur,
verknüpft nichts mit dem Symbolgraph. Das Dogfooding-Ergebnis (alle
Nicht-`.cs`-Typen bei 0 gegen die reale, reine C#-Solution) ist plausibel
und wird korrekt durch die Unit-/E2E-Tests gegen die Fixture ergänzt.

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx → grün, 0 Warnungen
dotnet test AiNetLinter.slnx  → grün (1071 Tests, 0 Fehler)
--footprint GetIndexScopeTool --path .              → 2413 (bestätigt)
--footprint FileStructureToolRegistrations --path . → 2434 (bestätigt)
```

## Sonstige Beobachtungen / MINOR / NITPICK

- Namensmuster `*Scanner` (statt `*Formatter` wie beim analogen
  `GetTypeHierarchyFormatter`-Vorbild) ist nachvollziehbar begründet (die
  Klasse scannt und formatiert), aber leicht inkonsistent zum sonst
  etablierten Suffix-Muster. Rein kosmetisch, keine Regelverletzung.

## Tech-Debt-Einträge aus diesem Review

- `TD-006` (siehe `tech-debt.md`) — neuer `.xaml`/`.html`-Scan dupliziert
  `WebFileCatalog.SafeEnumerateFiles`/`IsGeneratedPath` 1:1 statt sie
  (analog `GetProjectDirectories`) wiederzuverwenden; Priorität niedrig.
- `TD-004`/`TD-005` aktualisiert: `FileStructureToolRegistrations` jetzt
  bei 2434/2500 (66 Zeilen Puffer für die verbleibenden drei
  EPIC-04-Tools); TD-005 um die neue Beobachtung ergänzt, dass selbst ein
  von Anfang an dünner Dispatch das Limit reißen kann, wenn die
  ausgelagerte Logik selbst mehr als ~60-80 Zeilen braucht.
