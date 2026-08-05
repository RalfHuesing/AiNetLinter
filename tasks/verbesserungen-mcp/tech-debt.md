---
task: verbesserungen-mcp
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-05
---

# Tech-Debt-Log: verbesserungen-mcp

Append-only. Jeder Eintrag ist eine vom Kritiker während eines
Step-Reviews beobachtete, aber bewusst **nicht** gefixte Auffälligkeit
außerhalb des Scopes des jeweiligen Steps (Architektur, Anti-Pattern,
Duplikation, Konsistenz) — siehe `../spec.md` §8.3/§9.

**Priorität ist reine Sortierhilfe für den Menschen, kein Auslöser.**
Bewusst `hoch`/`mittel`/`niedrig` (deutsch) statt `CRITICAL`/`MAJOR`/
`MINOR`, um jede Verwechslung mit den blockierenden Findings in
`step-review.md` auszuschließen — kein Eintrag hier führt automatisch zu
einem Fix-Step oder einem neuen Epic. Das entscheidet ausschließlich der
Nutzer (manuell, z. B. durch Ergänzen eines Epics in `roadmap.md` mit
Verweis auf die Tech-Debt-ID).

## Index

| ID | Bereich / Datei | Priorität | Kurzfassung |
|---|---|---|---|
| TD-001 | `src/AiNetLinter.Tests/Mcp/Tools/*ToolTests.cs` (Aggregat-Warnung-Regex) | mittel | Regex `Dateien?` in mehreren bestehenden Aggregat-Warnung-Tests matcht Plural, nicht Singular „1 Datei" — aktuell durch Mehrfach-Datei-Fixtures maskiert. |

## Einträge

### TD-001 — Fehlerhafte `Dateien?`-Regex in Aggregat-Warnung-Tests maskiert Singular-Fall [Priorität: mittel]

- **Gefunden in:** step-001 (Kritiker-Review vom 2026-08-05)
- **Ort:** `src/AiNetLinter.Tests/Mcp/Tools/GetIndexScopeToolTests.cs:107` und
  `GetHotspotsToolTests.cs:109` (verifiziert: exakt derselbe
  `Assert.Matches(@"\b\d+\s+Dateien?\s+haben\s+Compile-Fehler", text)`).
  Dieselbe Testmethode `ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning`
  existiert zusätzlich (mit vermutlich analoger oder einfacherer
  `Assert.Contains`-Prüfung, nicht im Detail geprüft) in
  `FindReferencesToolTests.cs`, `FindSymbolToolTests.cs`,
  `GetImpactToolTests.cs`, `GetTypeHierarchyToolTests.cs`,
  `SearchPatternToolTests.cs` — alle nutzen dieselbe
  `CompileErrorMiniFixtureWorkspace` (3 kaputte Dateien, Plural-Fall).
- **Befund:** Die Regex `\d+\s+Dateien?\s+haben` matcht nur den Plural
  „N Dateien" (das `?` bezieht sich grammatikalisch nur auf das letzte
  „n" von „Dateien", nicht auf das ganze Wort „Datei"). Die Produktions-
  logik selbst ist korrekt: `McpCompileDiagnostics.FormatAggregateWarning`
  (`src/AiNetLinter/Mcp/Tools/McpCompileDiagnostics.cs:103-109`)
  unterscheidet bereits sauber zwischen „Datei" (1) und „Dateien" (>1).
  Der Bug steckt ausschließlich in der Test-Assertion. Da
  `CompileErrorMiniFixtureWorkspace` immer 3 Dateien bricht, bleibt das
  Problem in allen aktuell existierenden Nutzungen unsichtbar (False
  Negative nur im Singular-Fall). Neu entdeckt beim Anlegen der
  `BlazorPartialMini`-Fixture in diesem Step, die genau einen
  Compile-Fehler-File-Fall erzeugt und beim ersten Testlauf exakt daran
  scheiterte (Coder-Hinweis in `step-001/step-result.md`, „Beobachtungen").
- **Warum nicht sofort gefixt:** Betrifft ausschließlich bestehende
  Testklassen aus früheren, nicht in diesem Step behandelten
  Arbeitsschritten — außerhalb des Scopes von step-001 (der nur die neue
  `BlazorPartialMini`-Fixture/Testklasse mit bereits korrigierter Regex
  `Datei(en)?` einführt).
- **Vorschlag:** In einem künftigen kleinen Schritt die Regex in allen
  betroffenen `*ToolTests.cs`-Dateien einheitlich auf
  `\d+\s+Datei(en)?\s+haben` (oder gleichwertig) korrigieren — ggf. auch
  auf eine gemeinsame Test-Hilfsmethode/Konstante auslagern, um die
  Duplikation über sieben Testklassen zu vermeiden.
- **Status:** offen
