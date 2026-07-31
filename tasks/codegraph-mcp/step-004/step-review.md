---
status: done
type: step-review
task: codegraph-mcp
step: 004
epic: EPIC-03
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-07-31T13:15:00Z
verdict: approved
tech_debt_ids: [TD-005]
---

# Review Step 004: find_references Tool (Symbol- und Positions-Aufloesung + Aufrufstellen)

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues**
- [ ] **blocked**

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` (referenzierte Dateien) eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün

## Befund

### Plan-Erfüllung

Alle zehn „Konkrete Änderungen"-Dateien wie geplant umgesetzt; alle sieben Test-Fälle aus der Plan-Testliste vorhanden und grün.

### Rules-Konformität

Selbst-Lint (`ainetlinter --config rules.json --path .`) bestätigt `OK`, 0 Violations, inkl. `AIContextFootprint` für `FindReferencesTool`/`McpServerOptionsFactory`; die neue `SymbolIdentifierResolver.cs` ist sauber (kein neues Regelproblem, `#nullable enable`, statische Klasse `sealed`-exemptiert) — Begründung der Auslagerung selbst per Code-Lesen von `AIContextFootprintCalculator.cs` nachvollzogen und bestätigt (Zielklasse zählt mit eigener Dateilänge voll in ihren eigenen Footprint).

### Logische Korrektheit

Alle sechs `FindReferencesToolTests` sind aussagekräftig, insbesondere der Ambiguitäts-Test (`OtherCaller.Run()` erzeugt eine echte zweite Fundstelle statt die Mehrdeutigkeit nur zu unterstellen, exakt wie im Plan als Ausweichoption vorgesehen) und die Positions-Korrektur `:5:19` (nachvollziehbar durch tatsächliche Zeilenzählung der Fixture-Datei, kein tieferer Fehler); die Sichtbarkeits-Anhebung (`private`→`internal`) von `DiffImpactAnalyzer.FindCallSitesAsync`/`FindDocumentByPath`/`FindSymbolTool.FormatSymbolLocations` ist reine Signaturerweiterung ohne Verhaltensänderung, bestätigt durch den vollständig grünen Gesamttestlauf (bestehende `--impact`-Tests unberührt).

### Konzept-Treue (Ebene 4)

Deckt die `konzept.md`-Tabellenzeile für `find_references` korrekt ab (Basis `DiffImpactAnalyzer.FindCallSitesAsync` wiederverwendet statt neu gebaut, C#-only-Scope im Tool-`Description`-Feld kommuniziert); kein verfrühter Miss-Hint-Fallback ist konsistent mit dem in `konzept.md` beschriebenen Mechanismus (nur für `find_symbol` als Einstiegspunkt vorgesehen) und dem Präzedenzfall aus step-003.

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx → grün, 0 Warnungen
dotnet test AiNetLinter.slnx  → grün (1043 Tests, 0 Fehler)
Selbst-Lint (ainetlinter --config rules.json --path .) → OK, 0 Violations
```

## Tech-Debt-Einträge aus diesem Review

- `TD-005` (siehe `tech-debt.md`) — `McpCodeGraphServer` als Tool-Parametertyp lässt jeder künftigen EPIC-03/04-Tool-Klasse kaum eigenen `AIContextFootprint`-Spielraum; TD-004s Risikoprognose traf ein, aber in der Tool-Klasse selbst (`FindReferencesTool`) statt in der Factory — TD-004 dort um einen Update-Hinweis ergänzt.
