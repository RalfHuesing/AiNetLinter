---
status: done
type: step-review
task: codegraph-mcp
step: 009
epic: EPIC-04
step_type: single
reviewed_by: kritiker
reviewed_by_model: MiniMax-M3
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-07-31T20:55:00Z
verdict: approved
tech_debt_ids: [TD-007]
---

# Review Step 009: get_hotspots Tool (Zeilen-Hotspot-Kennzahlen der Solution)

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Fix-Step `step-009/fix-XX/` angelegt mit Fix-Plan
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (oder Baselines beachtet)

## Befund

### Plan-Erfüllung

Alle 8 Datei-Punkte des Plans umgesetzt (McpCodeGraphServer-Property, McpServerCommand-ResolveMaxLineCount, GetHotspotsTool-Dispatch, GetHotspotsScanner, FileStructureToolRegistrations-Block, GetHotspotsToolTests, McpServerCommandTests-Tool-Count + E2E + zwei ResolveMaxLineCount-Unit-Tests); beide im DoD geforderten Footprint-Checks (`GetHotspotsTool` 2424/2500, `FileStructureToolRegistrations` 2455/2500) eingehalten — keine dritte Registrar-Klasse nötig; DoD-Commit `feat(mcp): add get_hotspots tool [codegraph-mcp]` mit korrektem Suffix auf `main`.

### Rules-Konformität

Die im Plan unter „Rules-Refs" zitierten Regeln sind eingehalten: `AIContextFootprint` 2500 nicht gerissen (2424/2455); kein DI-Container — `McpCodeGraphServer` wird weiter per Delegate-Closure in `FileStructureToolRegistrations` an die Tools gereicht; Result-Pattern konsequent (`McpToolResults.SolutionNotLoaded()` für den Fehlerpfad, `McpToolResults.Text()` für den Erfolgspfad, `try/catch (IOException)` defensiv für Read-Skip); Build/Test-Pflicht durch eigenen Lauf verifiziert (siehe unten); Commit-Vorschlag als separater Code-Commit vorhanden. Der `catch (IOException) { return null; }` in `GetHotspotsScanner.TryCountLines` folgt exakt dem bereits in `GetIndexScopeScanner.SafeEnumerateFiles` (step-008, `approved`) akzeptierten Pattern und wird vom Selbst-Lint als regelkonform akzeptiert (0 Violations).

### Logische Korrektheit

Klassifikationsgrenzen korrekt umgesetzt (`>= 0.95` Critical, `>= 0.80 and < 0.95` Warning, exklusive/inclusive-Logik verhindert Doppel-Klassifikation); Scope-Filter matched `null`/leer → alle Dateien, sonst case-insensitive `Contains` gegen Projekt-Name **oder** solution-relativen Pfad (plan-konform, in „Bekannte Ausnahmen" als Namespace-Vereinfachung dokumentiert); `try/catch (IOException)` sitzt ausschließlich um `File.ReadAllLines`, nicht um die Verarbeitung; Tests wirklich aussagekräftig (kritisch mit `maxLineCount:1`, Warning mit `maxLineCount:7` gegen `Greeter.cs` — verifiziert: `File.ReadAllLines(Greeter.cs).Length = 6`, 6/7 ≈ 85,7 % landet sauber in der Warnungs-Sektion; E2E-Test gegen die reale `AiNetLinter.slnx` deckt die Default-Config-Verdrahtung mit `MaxLineCount: 500` ab); Coder-Dogfooding-Werte (`RuleRegistry.cs` 459, `MaxConstructorDependenciesTests.cs` 495, `FalsePositiveTests.cs` 475) gegen die reale Platte verifiziert — `File.ReadAllLines().Length` liefert exakt diese Werte (`Measure-Object -Line` zählt inkonsistent und ist kein gültiger Vergleichsmaßstab; Differenzen 13/50/44 vs. `Measure` sind PowerShell-Quirks, keine Tool-Bugs).

### Konzept-Treue (Ebene 4)

Konzept-Tabelle Zeile `get_hotspots` erfüllt: optionaler `scopeFilter` (Projekt/Pfad), `.cs`-only (Description nennt die Grenze explizit), gleiche Kennzahl wie `--map hotspots` aus `HotspotMapBuilder`; Non-Goals eingehalten (kein Editier-Tool, kein Embedding, kein DI, kein Cross-Language-Symbolgraph, kein Plugin/ALC, CLI-Batch-Modus unangetastet); Muss-Haben „Explizite Scope-Kommunikation (C#-only)" erfüllt — Description beginnt mit „Liefert .cs-Dateien…", vergleichbar zu `get_file_skeleton`/`get_index_scope` aus step-006/008; Muss-Haben „Dogfooding pro Tool-Step" mit Subprozess-Lauf gegen die reale `AiNetLinter.slnx` dokumentiert (297 Dateien gescannt, 2 kritisch / ≥7 warnung, Plausibilitäts-Stichproben `RuleRegistry.cs` 459/500 = 92 % → Warning und `MaxConstructorDependenciesTests.cs` 495/500 = 99 % → Critical mit der realen `File.ReadAllLines`-Zeilenzahl konsistent).

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx                       → grün, 0 Warnungen
dotnet test  AiNetLinter.slnx                       → grün (1080 Tests, 0 Fehler)
ainetlinter --config rules.json --path .            → OK (0 Violations)
--footprint GetHotspotsTool                          → 2424/2500
--footprint FileStructureToolRegistrations           → 2455/2500
```

## Sonstige Beobachtungen / MINOR / NITPICK

- **Test-Präzision `ExecuteAsync_MidRangeMaxLineCount_MarksFileAsWarning`** (Zeile 42-55 in `GetHotspotsToolTests.cs`): prüft nur `Assert.Contains("Warnungs-Dateien", textContent.Text)` und `Assert.Contains("Greeter.cs", textContent.Text)`. Bei `maxLineCount: 7` landen auch `Caller.cs` (10/7 ≈ 143 %) und `Hierarchy.cs` (24/7 ≈ 343 %) in „Kritische Dateien" — der Test differenziert nicht zwischen "Greeter.cs ist in der Warnungs-Sektion" und "Greeter.cs kommt irgendwo im Text vor". `Assert.Contains("Greeter.cs|Warnungs-Dateien", ...)`-Style oder Reihenfolgen-Check wäre präziser, aktueller Test ist grün, weil die Assertion erfüllt ist — kein Blocker, kosmetisch.
- **Floating-Point-Edge-Case** an der `>= 0.95`-Grenze (`GetHotspotsScanner.cs:45`): `475/500 = 0.95` ist binär exakt darstellbar, daher aktuell kein Problem; bei künftigen Grenzwerten, die im IEEE-754 nicht exakt sind (z. B. `0.855` als neue Schwelle), könnte ein `>= 0.95` knapp daneben greifen. Kein aktueller Bug, dokumentationswürdiger Mini-Punkt.
- **Division-by-Zero bei `maxLineCount: 0`** (`GetHotspotsScanner.cs:45-46`): `6.0 / 0 = double.PositiveInfinity`, `Infinity >= 0.95` ist `true` → alle Dateien landen in „Kritisch" ohne Fehlermeldung. In der Praxis nicht erreichbar (`ResolveMaxLineCount` liefert mindestens `MetricsConfig.MaxLineCount = 500`), aber defensiv ein `ArgumentOutOfRangeException` oder Default-Fallback wäre robuster. Nicht blocker-relevant.
- **„Alle anderen Dateien: 0 Dateien im grünen Bereich"**-Meldung (`GetHotspotsScanner.cs:118-119`), wenn alle Dateien ≥ 80 % sind: kosmetisch unschön, aber semantisch korrekt (zeigt ehrlich 0 statt zu schweigen). Mini.

## Tech-Debt-Einträge aus diesem Review

- `TD-007` (siehe `tech-debt.md`) — `McpCodeGraphServer.TryApplyContentChange` hat 5 Parameter (über `MaxMethodParameterCount` = 4), vorbestehend aus step-002, nicht durch step-009 verursacht; Kandidat für Input-`record`-Refactoring bei einem künftigen Schritt, der `McpCodeGraphServer` ohnehin anfasst (z. B. EPIC-06 Robustheit, kombinierbar mit TD-003-Lock-Fix).
