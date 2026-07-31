---
status: done
type: step-review
task: codegraph-mcp
step: 003
epic: EPIC-03
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-07-31T18:00:00Z
verdict: issues
tech_debt_ids: [TD-004]
---

# Review Step 003: Tool-Registrierungs-Infrastruktur + erstes Tool: find_symbol

## Verdict

- [ ] **approved**
- [x] **issues** — Fix-Step `step-003/fix-01` nötig
- [ ] **blocked**

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `.agents/rules/**` (referenzierte Dateien) eingehalten
- [ ] Logische Korrektheit: ein zentraler Fehlerpfad ist ungetestet
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (inkl. Selbst-Lint)

## Befund

### Plan-Erfüllung

Alle sechs „Konkrete Änderungen"-Dateien wie geplant erstellt/geändert.
Beide dokumentierten Abweichungen (`McpServerOptionsFactory.cs` statt
private Methoden in `McpServerCommand.cs`; Default-Werte am
Delegate-Parameter `kind`/`ct`) sind nachvollziehbar begründet und
plausibel verifiziert (siehe Rules-Konformität/Logische Korrektheit
unten) — beide zähle ich als zulässige, dokumentierte Abweichungen
gemäß der „Vorab-Klassifikation bei Build/Test-Fehlern"-Logik, nicht als
Plan-Verstoß. Alle im Plan aufgeführten Tests wurden geschrieben und
sind grün. DoD-Punkte (Build ohne neue Warnungen, Tests grün, Commit mit
Conventional-Commit-Message + `[codegraph-mcp]`-Suffix, `step-result.md`
geschrieben, Status auf `done (pending audit)` gesetzt, Commit-Vorschlag
vorhanden) alle erfüllt.

### Rules-Konformität

- `AiNetLinterRichtlinien.mdc` §1/§2 (kein DI-Container, kein
  Plugin-System): eingehalten — Tools werden per Closure-Capture von
  `mcpState` registriert (`McpServerOptionsFactory.BuildToolCollection`),
  kein `IServiceProvider`.
- `AiNetLinter.mdc` (`#nullable enable`, `sealed`, Methodenlänge,
  Parameteranzahl, `AIContextFootprint`): selbst nachgeprüft per
  Selbst-Lint (`AiNetLinter.exe --config rules.json --path .` →
  `OK`, 0 Violations) — bestätigt die im `step-result.md` behauptete
  Auflösung der `AIContextFootprint`-Verletzung (2553 > 2500) durch die
  Auslagerung nach `McpServerOptionsFactory.cs`.
- Die Auslagerung selbst ist die einzige plausible minimal-invasive
  Lösung: jede Methode in `McpServerCommand.cs`, die `McpCodeGraphServer`
  als Parametertyp führt, zählt für die Footprint-Metrik dessen komplette
  transitive Typ-Kette mit (verifiziert per `git stash`-Vergleich laut
  `step-result.md`, plausibel und nachvollziehbar); die neue Datei selbst
  ist sauber (kurze Methoden, korrekte XML-Doku mit Begründung, keine
  neue Regelverletzung, kein `sealed`-Verstoß da statische Klasse
  exemptiert).
- Delegate-Default-Werte (`kind = null`, `ct = default`): reine
  SDK-Notwendigkeit (JSON-Schema-Generierung markiert Parameter ohne
  Default als „required"), keine sauberere Alternative ersichtlich ohne
  ein eigenes Schema-Attribut zu bauen (das wäre mehr Abstraktion, nicht
  weniger). Nachvollziehbar durch den E2E-Test verifiziert, der
  `find_symbol` ohne `kind`-Argument aufruft.
- `#nullable enable` fehlt in `FindSymbolToolTests.cs` (neue Datei) —
  geprüft, aber **kein neuer Verstoß**: 57 von 126 bestehenden
  `.Tests`-Dateien haben dieselbe Lücke, Selbst-Lint bleibt trotzdem
  grün (Testprojekt-Konvention offenbar bereits inkonsistent, nicht
  durch diesen Step verursacht) — daher weder Finding noch neuer
  Tech-Debt-Eintrag, da projektweit, nicht step-spezifisch.

### Logische Korrektheit

`FindSymbolTool.FindMatchesAsync` (Substring/Case-Insensitivity/Kind-Filter/
Kein-Treffer-Text) ist durch die vier neuen Unit-Tests in
`FindSymbolToolTests.cs` tatsächlich aussagekräftig geprüft — insbesondere
der Kind-Filter-Test ist kein Blindtreffer (die Fixture-Klasse
`ViolatingClass` enthält keine Methode mit „Violating" im Namen, der
Test verifiziert also wirklich Ausschluss, nicht nur einen zufällig
leeren Nebeneffekt). Der E2E-Test in `McpServerCommandTests.cs` ruft
`find_symbol` tatsächlich über den echten `StdioClientTransport`/
`CallToolAsync`-Pfad auf und prüft Inhalt + `IsError`.

**Lücke:** Der zweite Hauptpfad des Tools — `FindSymbolTool.ExecuteAsync`
mit `state.GetCurrentSolution() == null` (also `McpCodeGraphServer.IsLoaded
== false`) → `McpToolResults.SolutionNotLoaded()` — ist **an keiner Stelle
automatisiert getestet**. Weder `FindSymbolToolTests.cs` noch
`McpServerCommandTests.cs` bringen den Server in einen Zustand ohne
geladene Solution und rufen dann `find_symbol` auf; `McpToolResults`
(`Error`/`SolutionNotLoaded`/`Text`) hat ebenfalls keine eigenen Tests.
Das ist keine kosmetische Lücke: Genau dieser Pfad ist laut Step-Plan
(„Aktueller Projektzustand", Bullet 2) **die** konkrete Umsetzung von
`konzept.md`s Muss-Haben „Solution lädt gar nicht → jeder Tool-Call
liefert eine strukturierte Fehlerantwort, Server bleibt am Leben" für
das erste Tool — und im selben Step wurde bereits ein Verhalten
(Default-Werte am Delegate) entdeckt, das ausschließlich durch
tatsächliches Ausführen über den echten MCP-Client sichtbar wurde, nicht
durch Code-Lesen/Reflection. Genau dieses Präzedenzbeispiel im eigenen
Step zeigt, dass „sieht beim Lesen richtig aus" hier nicht ausreicht, um
den `IsError=true`-Pfad tatsächlich als über das SDK funktionierend zu
betrachten. Ein einziger günstiger In-Process-Unit-Test hätte gereicht,
z. B.: `new McpCodeGraphServer(null)` → `FindSymbolTool.ExecuteAsync(state,
"x", null, ct)` → `Assert.True(result.IsError)` + Text enthält
`SOLUTION_NOT_LOADED`. Kein Subprozess/Fixture nötig, da `catalog: null`
trivial konstruierbar ist.

### Konzept-Treue (Ebene 4)

- `find_symbol` deckt die Tabellenzeile aus `konzept.md` „Wie" korrekt
  ab (Basis-API sinnvoll durch `FindSourceDeclarationsAsync` ersetzt,
  Output-Format Datei:Zeile/Kind/Signatur wie gefordert).
- Substring-only (kein Glob) ist im Step-Plan unter „Bekannte Ausnahmen"
  bewusst begründet und durch `konzept.md`s „Offene Punkte" („exakte
  finale Tool-Namen/Parametrisierung explizit Sache des Planers")
  gedeckt — kein Konzept-Verstoß, da die Grenze vom Planer aktiv
  getroffen wurde und nicht stillschweigend geschieht. Ich bestätige
  diese Einschätzung.
- Kein Vorgriff auf EPIC-05: Es gibt keinen Miss-Hint-Text-Fallback, das
  `initialize`-`instructions`-Feld bleibt unangetastet. Die knappe
  C#-only-Erwähnung im `Description`-Feld des Tools selbst
  („Deckt nur .cs-Dateien ab...") ist additiv und lokal zum Tool, steht
  der späteren zentralen Scope-Kommunikation aus EPIC-05 nicht im Weg
  und muss dafür nicht revidiert werden — kein Konzept-Verstoß.
- Kein Non-Goal umgesetzt (kein Cross-Language-Symbolgraph, kein
  Plugin-System, keine ALC-Nutzung).

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx → grün, 0 Warnungen
dotnet test AiNetLinter.slnx  → grün (1032 Tests, 0 Fehler, ~1m46s)
Selbst-Lint (ainetlinter --config rules.json --path .) → OK, 0 Violations
```

## Findings

1. `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` (Methode `ExecuteAsync`,
   Null-Solution-Zweig) / `src/AiNetLinter/Mcp/McpToolResults.cs` — [MAJOR]
   [Logische Korrektheit] Der „Solution nicht geladen → strukturierte
   `[ERROR]`-Antwort statt Absturz"-Pfad — laut Step-Plan die konkrete
   Umsetzung eines `konzept.md`-Muss-Habens für dieses Tool — ist
   vollständig ungetestet (weder `FindSymbolToolTests.cs` noch
   `McpServerCommandTests.cs` noch ein eigener `McpToolResults`-Test
   decken ihn ab). **Fix:** Mindestens einen günstigen Unit-Test
   ergänzen, der `new McpCodeGraphServer(null)` konstruiert,
   `FindSymbolTool.ExecuteAsync(state, ...)` aufruft und `IsError == true`
   sowie den `SOLUTION_NOT_LOADED`-Text im Ergebnis verifiziert (kein
   Subprozess/Fixture nötig). Optional zusätzlich ein direkter
   `McpToolResults`-Unit-Test für `Error`/`SolutionNotLoaded`/`Text`, da
   diese neue, wiederverwendbare Infrastruktur für alle 9 Tools ist.

## Tech-Debt-Einträge aus diesem Review

- `TD-004` (siehe `tech-debt.md`) — `McpServerOptionsFactory` ist bereits
  nach dem ersten Tool nahe am `AIContextFootprint`-Limit, Risiko für die
  restlichen 8 Tools.
