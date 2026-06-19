# Code Audit — Executive Summary

> **Audit-Datum:** 2026-06-19
> **Analyst:** Automatisierter Code-Audit (Cursor Agent)
> **Codebasis:** AiNetLinter 1.0.50 (.NET 10, 150+ Typen, 13 Namespaces, ~12.000 LOC Produktionscode)
> **Scope:** Architektur, Code-Qualität, LLM/Agent-Optimierung, Test-Coverage, Konfigurationsmanagement

---

## 🎯 TL;DR

AiNetLinter ist ein ambitioniertes, gut durchdachtes Roslyn-basiertes Linter-Projekt mit ~30 Regeln für AI-optimierten C#-Code. Die **funktionale Substanz** ist solide (Sol-Linter mit echtem Semantikmodell, parallele Verarbeitung, inkrementeller Cache, Baseline-Ratchet, SARIF-Export, Playbook-Generator). Die **architektonische Reife** hinkt dem Feature-Umfang jedoch deutlich hinterher:

1. **Monolithische Checker-Pipeline** statt komponierbarem Plugin-System → Open/Closed-Prinzip verletzt
2. **Rule-Definitionen 3-fach dupliziert** über `CursorRulesGenerator`, `ViolationTextFormatter`, `RepoPlaybookGenerator`
3. **PerformanceProfiler als globaler Singleton** in Produktionscode → Test-Isolation unmöglich
4. **`Program.cs` mit 568 LOC** als CLI-Mono-Router für 8+ Sub-Befehle → schwer testbar, schwer erweiterbar
5. **Keine zentrale `IRuleRegistry`** → Agenten können Regeln nicht programmatisch nachschlagen

**Empfehlung:** Vor dem nächsten Feature-Schub (Epic 25+) **4–6 Wochen Refactoring-Initiative** starten (siehe `03-Architektur-Refactoring-Vorschlaege.md`). Danach sind neue Regeln 5× schneller hinzufügbar, und Agent-Integration (z. B. via MCP-Server) wird zur Pflichtübung.

---

## 📊 Top-12 Befunde nach Priorität

| #       | Befund                                                                       | Prio       | Aufwand | Nutzen | Datei                                                                              |
| ------- | ---------------------------------------------------------------------------- | ---------- | ------- | ------ | ---------------------------------------------------------------------------------- |
| **F1**  | Rule-Metadaten 3-fach dupliziert → `IRuleRegistry` einführen                 | 🔴 Hoch    | M       | ★★★★★  | `CursorRulesGenerator.cs`, `ViolationTextFormatter.cs`, `RepoPlaybookGenerator.cs` |
| **F2**  | `LinterAnalyzer` als monolithischer SyntaxWalker → Plugin-Pipeline           | 🔴 Hoch    | M       | ★★★★★  | `Core/LinterAnalyzer.cs`                                                           |
| **F3**  | `Program.cs` (568 LOC) als Mono-CLI-Router → Executor-Pattern                | 🔴 Hoch    | M       | ★★★★   | `Program.cs`                                                                       |
| **F4**  | `PerformanceProfiler` als globaler Singleton + IO in Produktion              | 🟠 Mittel  | N       | ★★★★   | `Diagnostics/PerformanceProfiler.cs`                                               |
| **F5**  | Kein `CancellationToken` durch Pipeline → Ctrl+C unsicher                    | 🟠 Mittel  | N       | ★★★★   | `LinterEngine.cs`, `LinterAnalyzer.cs`                                             |
| **F6**  | Triple-Layer `Apply` mit 5 `with`-Klonings → Extension-Method-Pattern        | 🟠 Mittel  | N       | ★★★    | `Configuration/LinterConfig.cs`                                                    |
| **F7**  | `Console.WriteLine` als Logging → strukturiertes `ILogger`-Interface         | 🟠 Mittel  | M       | ★★★★   | alle (15+ Stellen)                                                                 |
| **F8**  | Rule-Namen als String-Literale → `LinterRule.RuleId` Enum oder Const-Klasse  | 🟡 Niedrig | N       | ★★★    | alle Checker                                                                       |
| **F9**  | Tests unter `src/AiNetLinter.Tests/` statt `tests/` (inkonsistent)           | 🟡 Niedrig | XS      | ★★     | Solution-Layout                                                                    |
| **F10** | `RepoPlaybookGenerator.RuleDescriptions` hardcoded + veraltet (Werte falsch) | 🔴 Hoch    | XS      | ★★★    | `Core/RepoPlaybookGenerator.cs` (Zeile 36–59)                                      |
| **F11** | `LinterEngine` hat 3× `RunAsync`-Overloads → Strategy-Pattern                | 🟠 Mittel  | S       | ★★★    | `Core/LinterEngine.cs` (Zeile 35–57)                                               |
| **F12** | Kein `dotnet test`-Friendly Test-Setup (custom Integration-Runner)           | 🟡 Niedrig | S       | ★★     | Tests-Projekt                                                                      |

**Legende:**

- 🔴 Hoch — Sofort angehen (blockiert weitere Entwicklung oder verursacht aktive Bugs)
- 🟠 Mittel — Sollte in nächster Initiative adressiert werden
- 🟡 Niedrig — Nice-to-have, kann später erfolgen
- Aufwand: XS = <1h, S = <1 Tag, M = 1–3 Tage, L = >3 Tage
- Nutzen: ★ = gering, ★★★★★ = transformativ

---

## 🏆 Quick Wins (< 1 Tag, hoher Nutzen)

Diese Befunde können **ohne Architektur-Refactoring** sofort umgesetzt werden:

1. **`RepoPlaybookGenerator.RuleDescriptions` reparieren** (XS) — Werte sind veraltet: `"max. 500 Zeilen"` aber Default ist 700, `"max. 5 Komplexität"` aber Default ist 12/15.
2. **Konstante `LinterRuleIds` einführen** (S) — alle Magic Strings wie `"EnforceSealedClasses"` durch typisierte Konstanten ersetzen → Compile-Time-Sicherheit für neue Checker.
3. **`CancellationToken` durchreichen** (S) — alle async-Methoden (`LinterEngine.RunAsync`, `AnalyzeDocumentAsync`, etc.) bekommen `CancellationToken`-Parameter; `Ctrl+C` bricht sauber ab.
4. **Strukturierte `IRuleRegistry`** (M) — neue Klasse `Core/RuleRegistry.cs` mit `IReadOnlyList<RuleMetadata>` als Single-Source-of-Truth. Ersetzt die 3 Duplikate.
5. **Dogfooding-Pipeline im `dotnet test`** (S) — Tests rufen `dotnet run -- --config rules.json --path src/` aus. Aktuell ist das ein Custom-Integration-Test-Skript.

---

## 🧭 Aufwand-vs-Nutzen-Matrix

```
                NUTZEN
         gering ─────────────────────────► hoch
   hoch  │  F8 (Rule-IDs)        │  F2 (Plugin-Pipeline)         │
   │     │  F9 (Test-Pfad)       │  F1 (RuleRegistry)            │
   │     │                       │  F3 (Executor-Pattern)        │
   │     │                       │                               │
AUFWAND  │───────────────────────┼───────────────────────────────│
   │     │  F11 (Strategy)       │  F7 (ILogger)                 │
   │     │  F6 (Apply-Ext)       │  F4 (Profiler raus)           │
niedrig  │  F5 (Cancellation)    │  F10 (Playbook fix)           │
```

---

## 🧬 Architektur-Scorecard

| Dimension                   | Score | Bemerkung                                                                        |
| --------------------------- | ----- | -------------------------------------------------------------------------------- |
| **Feature-Umfang**          | ★★★★★ | 30+ Regeln, Baseline, Playbook, SARIF, Impact, Footprint                         |
| **Funktionale Korrektheit** | ★★★★  | Keine offensichtlichen Bugs, gute Tests vorhanden                                |
| **Performance**             | ★★★★  | Parallelisiert, gecached, profiler verfügbar                                     |
| **LLM/Agent-Optimierung**   | ★★★   | Gute Textausgabe, aber keine JSON-API, Rule-Lookup mühsam                        |
| **Code-Struktur**           | ★★    | God-Classes, fehlende Plugin-Architektur, Singleton-Anti-Pattern                 |
| **Testbarkeit**             | ★★    | Performance-Singleton blockiert Isolationstests, statische Helper schwer mockbar |
| **Wartbarkeit**             | ★★    | Hohe Komplexität pro Datei, viele Cross-Cutting-Konzerne                         |
| **Dokumentation**           | ★★★★  | README, rationale.md, ROADMAP, codegraph.md sind vorbildlich                     |
| **Konfigurierbarkeit**      | ★★★★★ | Regeln einzeln aktivierbar, ProjectOverrides, PathOverrides                      |

**Gesamt-Score:** 3.4 / 5 — produktionsreif, aber **vor dem nächsten Feature-Schub refactoring-bedürftig**.

---

## 🚦 Empfohlene Reihenfolge

1. **Woche 1:** Quick Wins + F10 (Playbook fix)
2. **Woche 2:** F1 (RuleRegistry) als Fundament
3. **Woche 3:** F2 (Plugin-Pipeline) → größte Hebelwirkung
4. **Woche 4:** F3 (Executor-Pattern) + F4 (Profiler raus) + F5 (Cancellation)
5. **Woche 5:** F7 (ILogger) als Vorbereitung für MCP-Server
6. **Woche 6:** Dogfooding, Coverage-Report, finale Tests

---

## 📂 Weitere Audit-Dateien

- [`01-Architektur-Befunde.md`](01-Architektur-Befunde.md) — Detaillierte architektonische Schwachstellen
- [`02-Code-Qualitaet.md`](02-Code-Qualitaet.md) — Konkrete Code-Befunde mit Zeilennummern
- [`03-Architektur-Refactoring-Vorschlaege.md`](03-Architektur-Refactoring-Vorschlaege.md) — Konkrete Refactoring-Pläne mit Code-Skeletten
- [`04-LLM-Agent-Optimierung.md`](04-LLM-Agent-Optimierung.md) — Optimierungen für Agent-Integration
