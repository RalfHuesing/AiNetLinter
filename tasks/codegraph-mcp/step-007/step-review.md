---
status: done
type: step-review
task: codegraph-mcp
step: 007
epic: EPIC-03
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-07-31T18:00:00Z
verdict: issues
tech_debt_ids: []
---

# Review Step 007: get_type_hierarchy Tool (Basisklassen/abgeleitete Klassen/Interface-Implementierer via SymbolFinder)

## Verdict

- [ ] **approved**
- [x] **issues** — Fix-Step `step-007/fix-01` anzulegen
- [ ] **blocked**

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` (referenzierte Dateien) eingehalten
- [ ] Logische Korrektheit: Code macht was er soll, nicht nur „grün" — **verletzt, siehe Finding 1**
- [ ] Konzept-Treue: passt die Umsetzung zu `konzept.md` — **verletzt, siehe Finding 1**
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün

## Befund

### Plan-Erfüllung

Alle acht im Plan genannten Dateien wie beschrieben umgesetzt: Registrar-Aufteilung (Datei 1-3), `GetTypeHierarchyTool`/`GetTypeHierarchyFormatter` (Datei 4-5) im dokumentierten Dispatch-Muster, neue Fixture (Datei 6, inkl. dokumentierter und nachvollziehbarer Namensabweichung wegen Suffix-Kollision), Tests (Datei 7-8) exakt gemäß Plan-Testliste vorhanden. Alle sechs geplanten `GetTypeHierarchyToolTests`-Fälle sowie die zwei `McpServerCommandTests`-Anpassungen sind 1:1 vorhanden. DoD-Checkliste (Build/Test/Selbst-Lint/Footprint/Commit/Dogfooding/Status) vollständig abgearbeitet.

### Rules-Konformität

Keine Verstöße gegen die im Plan zitierten Rules-Refs gefunden. `#nullable enable` in allen vier neuen Produktionsdateien vorhanden, statische Klassen (`internal static class`), `Register(tools, mcpState)` bei 2 Parametern, keine geworfenen Exceptions im Fehlerpfad (Result-Pattern über `McpToolResults` konsequent weitergeführt), kein DI-Container, Delegate-Closure-Registrierung 1:1 aus dem bisherigen Muster übernommen. Die neue Fixture-Datei `Hierarchy.cs` hat kein `#nullable enable` — das entspricht aber der bestehenden Konvention der übrigen Fixture-Dateien (`Greeter.cs` hat es ebenfalls nicht) und ist damit keine neu eingeführte Abweichung dieses Steps.

### Logische Korrektheit

**Kernfund (siehe Finding 1 unten):** Die Basisklassen- und Interface-Sektionen von `get_type_hierarchy` nutzen `FindSymbolTool.FormatSymbolLocations`, die Symbole ohne Quell-Location (`location.IsInSource`) stillschweigend herausfiltert. Für jeden Basistyp/jedes Interface, das **nicht** im Quellcode der analysierten Solution deklariert ist (also praktisch jede Basisklasse/jedes Interface aus dem .NET-BCL oder einer NuGet-Bibliothek — `object`, `IDisposable`, `Exception`, `CSharpSyntaxWalker`, …), liefert die entsprechende Sektion **keine Zeile** und damit fälschlicherweise „Keine Basisklasse."/„Keine Interfaces.", obwohl der Typ tatsächlich eine Basisklasse bzw. ein Interface hat. Das betrifft nicht nur den im Plan als bekannte/bewusste Ausnahme dokumentierten `System.Object`-Fall (der Plan sagt explizit, `System.Object` solle **sichtbar** bleiben — tatsächlich verschwindet es spurlos), sondern **jeden** externen Basistyp/jedes externe Interface, was im echten Repo der Normalfall ist. Verifiziert per eigenem, unabhängigem Dogfooding gegen die reale `AiNetLinter.slnx` (drei von der Coder-Verifikation unabhängige Typen, andere Richtung als der Coder-Test):
- `IPerformanceProfiler` → korrekt: 2 Implementierer (`NullPerformanceProfiler`, `PerformanceProfiler`), deckt sich mit `grep`.
- `PerformanceProfiler` (Klasse, implementiert `IPerformanceProfiler`) → **"Basisklassen: Keine Basisklasse."** (obwohl object Basisklasse ist) — Interfaces-Sektion hier korrekt (`IPerformanceProfiler` selbst ist im Quellcode deklariert).
- `SourceFileCatalog` (Klasse, `: IDisposable`) → **"Implementierte Interfaces: Keine Interfaces."** trotz explizitem `IDisposable` — eindeutig falsch, da `IDisposable` aus dem BCL kommt und nicht im Quellcode der Solution deklariert ist.
- `SkeletonSyntaxWalker` (Klasse, `: CSharpSyntaxWalker`, Basisklasse aus Roslyn-NuGet-Paket) → **"Basisklassen: Keine Basisklasse."** trotz expliziter Vererbung.

Die Tests des Coders (`GetTypeHierarchyToolTests`, `McpServerCommandTests`) sowie das eigene Dogfooding des Coders (`ILintConsole` als Interface, `BaseGreeting`/`SpecialGreeting`/`IGreeting` als reine In-Fixture-Hierarchie) decken ausschließlich Fälle ab, in denen die komplette Hierarchie innerhalb der analysierten Solution liegt — der (weitaus häufigere) Fall externer Basistypen/Interfaces wurde nirgends geprüft und dadurch nicht bemerkt.

### Konzept-Treue (Ebene 4)

`konzept.md` Zeile 374 verspricht für `get_type_hierarchy` explizit „Basisklassen, abgeleitete Klassen, Interface-Implementierer" als Ergebnis. Der oben beschriebene Fund bedeutet, dass die Basisklassen- und Interface-Sektionen für den in der Praxis überwiegenden Fall (Basistyp/Interface aus dem BCL oder einer Bibliothek) ein falsches „Keine Basisklasse."/„Keine Interfaces." liefern statt der laut Konzept erwarteten Information — ein Muss-Haben-Punkt der Tool-Tabelle ist damit für den Normalfall nicht zuverlässig erfüllt, auch wenn die Implementierer-/Ableitungsrichtung (`SymbolFinder.FindDerivedClassesAsync`/`FindImplementationsAsync`) korrekt funktioniert.

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx → grün, 0 Warnungen
dotnet test AiNetLinter.slnx  → grün (1063 Tests, 0 Fehler)
ainetlinter --config rules.json --path . → OK, 0 Violations
--footprint McpServerOptionsFactory        → 2437 (bestätigt, Coder-Wert korrekt)
--footprint SymbolGraphToolRegistrations   → 2455 (bestätigt, Coder-Wert korrekt)
--footprint FileStructureToolRegistrations → 2422 (bestätigt, Coder-Wert korrekt)
--footprint GetTypeHierarchyTool           → 2423 (bestätigt, Coder-Wert korrekt)
tools/list via echtem Subprozess           → exakt 5 Tools: find_symbol, find_references, get_impact, get_file_skeleton, get_type_hierarchy
```

## Findings

1. `src/AiNetLinter/Mcp/Tools/GetTypeHierarchyFormatter.cs:29-52` (`FormatBaseTypes`/`FormatInterfaces`, über die Wiederverwendung von `FindSymbolTool.FormatSymbolLocations` in `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs:84`) — **[MAJOR]** **[Logische Korrektheit + Konzept-Treue]** Basisklassen und implementierte Interfaces, die außerhalb der analysierten Solution deklariert sind (jede BCL-/NuGet-Bibliotheksklasse/-Interface, nicht nur `System.Object`), werden durch den `location.IsInSource`-Filter in `FormatSymbolLocations` stillschweigend aus der Ausgabe entfernt. Ergebnis ist eine **falsche** Meldung „Keine Basisklasse."/„Keine Interfaces." für so gut wie jeden real existierenden Typ mit externer Basisklasse/externem Interface — verifiziert an drei realen Repo-Typen (`PerformanceProfiler`, `SourceFileCatalog`, `SkeletonSyntaxWalker`). Widerspricht sowohl der im Plan dokumentierten Design-Absicht ("System.Object bewusst nicht aus der Kette gefiltert … explizite Sichtbarkeit der vollständigen Kette") als auch dem Muss-Haben aus `konzept.md` Zeile 374. **Fix:** Für die Basisklassen-/Interface-Sektionen eine eigene Formatierung verwenden (nicht `FormatSymbolLocations`), die auch für Symbole ohne Quell-Location eine sinnvolle Zeile ausgibt (z. B. `<TypeKind>: <ToDisplayString()> (extern, keine Datei im Repo)` statt komplett zu verschwinden) — die Implementierer-/Ableitungs-Sektionen können `FormatSymbolLocations` unverändert weiterverwenden, da `FindDerivedClassesAsync`/`FindImplementationsAsync` ohnehin nur Solution-Symbole liefern und dort kein Informationsverlust entsteht. Testfälle ergänzen, die einen Typ mit externer Basisklasse/externem Interface prüfen (z. B. `SourceFileCatalog`/`IDisposable`-Analogon in der Fixture, oder direkt gegen die reale Solution wie im Dogfooding dieses Reviews).

## Sonstige Beobachtungen / MINOR / NITPICK

- Die Test- und Dogfooding-Abdeckung dieses Steps prüft ausschließlich In-Fixture-Hierarchien bzw. ein reines Interface ohne Basisklasse (`ILintConsole`) — keiner der Testfälle deckt eine Klasse mit externer (BCL-/Bibliotheks-)Basisklasse oder einem extern deklarierten Interface ab. Wird durch den Fix zu Finding 1 ohnehin mitbehoben (neue Testfälle erforderlich).

## Tech-Debt-Einträge aus diesem Review

Keine neuen Einträge. `TD-004`/`TD-005` wurden mit Updates zum aktuellen Footprint-Stand nach der Registrar-Aufteilung ergänzt (siehe `tech-debt.md`) — kein Fix-Step nötig, rein informativ für den nächsten Planer (EPIC-04).
