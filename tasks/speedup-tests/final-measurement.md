---
task: speedup-tests
type: final-measurement
created_at: 2026-08-14
created_by: coder (step-029)
---

# Finale Messung nach Abschluss der Test-Migration (step-029)

Gemäß `konzept.md` Leitplanke 10 und dem Masterplan `tasks/speedup-tests/master-low-cost-handoff.md`:
Gemessen wurden alle standardisierten Profile über 3 vollständige Round-Robin-Läufe auf derselben Maschine
wie die Baseline. Build getrennt von Testzeit, Median über 3 Läufe je Slice.

---

## 1. Maschinen- & Umgebungskontext

- **.NET SDK**: `10.0.203`
- **OS**: Windows 11 Enterprise 10.0.22631 (16 logische Kerne)
- **Solution**: 4 Zielprojekte (`AiNetLinter`, `AiNetLinter.FastTests`, `AiNetLinter.IntegrationTests`, `AiNetLinter.TestKit`)
- **Quarantäniertes Legacy-Projekt (`AiNetLinter.Tests`)**: Vollständig und rückstandsfrei gelöscht (0 Dateien).
- **Keine Fremdlast**: Messläufe als zusammenhängender Round-Robin-Block ausgeführt.

---

## 2. Methodik & Round-Robin-Ablauf

1. **Build**: `dotnet build` nach Bereinigung getrennt zeitgestoppt.
2. **Round-Robin-Slices (3 Runden)**:
   - Runde 1: `Unit` -> `Component` -> `Integration` -> `Dogfood` -> `Performance`
   - Runde 2: `Unit` -> `Component` -> `Integration` -> `Dogfood` -> `Performance`
   - Runde 3: `Unit` -> `Component` -> `Integration` -> `Dogfood` -> `Performance`
3. **Abschluss-Gates**:
   - `FastTests` Gesamtgate (`Category!=Stress` auf `AiNetLinter.FastTests`)
   - `IntegrationTests` Gesamtgate (`Category!=Stress` auf `AiNetLinter.IntegrationTests`)
4. **Stress Discovery**: Erfassung der existierenden Stress-Tests via `--list-tests --filter "Category=Stress"`. Stress-Tests wurden laut Anweisung compiliert, aber nicht im Suite-Lauf ausgeführt.
5. **Drift-Audit**: Ausführung des projekteigenen MCP-Tools `find_duplicates(scopeDir="src", minTokens=20)` gemäß `.agents/skills/drift-audit/SKILL.md`.

---

## 3. Rohdaten der 3 Round-Robin-Läufe

### Slices Übersicht

| Profil / Slice | Projekt | Filter | R1 Dauer | R2 Dauer | R3 Dauer | **Median** | Tests | Status | TRX-Dateien |
|---|---|---|---|---|---|---|---|---|---|
| **Unit** | `AiNetLinter.FastTests` | `Category=Unit` | 5 s | 5 s | 5 s | **5 s** | 945 | grün | `step029-final-r1-unit.trx`, `r2-unit.trx`, `r3-unit.trx` |
| **Component** | `AiNetLinter.FastTests` | `Category=Component` | 4 s | 3 s | 3 s | **3 s** | 320 | grün | `step029-final-r1-component.trx`, `r2-component.trx`, `r3-component.trx` |
| **Integration** | `AiNetLinter.IntegrationTests` | `Category=Integration` | 66 s | 69 s | 72 s | **69 s** | 276 | grün | `step029-final-r1-integration.trx`, `r2-integration.trx`, `r3-integration.trx` |
| **Dogfood** | `AiNetLinter.IntegrationTests` | `Category=Dogfood` | 31 s | 32 s | 31 s | **31 s** | 23 | grün | `step029-final-r1-dogfood.trx`, `r2-dogfood.trx`, `r3-dogfood.trx` |
| **Performance**| `AiNetLinter.IntegrationTests` | `Category=Performance` | 11 s | 11 s | 20 s | **11 s** | 2 | grün | `step029-final-r1-performance.trx`, `r2-performance.trx`, `r3-performance.trx` |

---

## 4. Gesamt-Gates (Abschlussverifikation)

| Gate | Befehl | Dauer | Tests | Status | TRX-Datei |
|---|---|---|---|---|---|
| **Fast-Gate** | `dotnet test src/AiNetLinter.FastTests --filter "Category!=Stress"` | **6 s** | **1265** | grün (0 Fehler) | `step029-final-fast-gate.trx` |
| **Integration-Gate** | `dotnet test src/AiNetLinter.IntegrationTests --filter "Category!=Stress"` | **91 s** | **301** | grün (0 Fehler) | `step029-final-integration-gate.trx` |
| **Gesamtsuite (Non-Stress)** | Beide Projekte kombiniert | **~97 s** | **1566** | grün (0 Fehler) | - |

---

## 5. Vorher-/Nachher-Vergleich (Baseline vs. Final)

| Metrik | Baseline (`step-002`) | Final (`step-029`) | Delta / Beschleunigung |
|---|---|---|---|
| **Schnelle Feedback-Schleife (FastTests / Unit)** | 74,21 s (1353 Tests in Legacy) | **5 s** (945 Tests) / **6 s** (1265 FastTests) | **> 12x schneller** (Ziel < 10s deutlich unterboten!) |
| **Komplettes Abschlussgate (`Category!=Stress`)** | 224 s (1527 Tests, instabil / Flakiness) | **97 s** (1566 Tests, 100% stabil) | **> 2.3x schneller**, 0 Flakiness, 0 Fehler |
| **Linter Engine & Tool Build** | 20,47 s (nach clean) | **4,08 s** (4 Projekte) | **5x schneller** |
| **Projekt-Struktur** | 1 Monolithisches Legacy-Projekt (`AiNetLinter.Tests`) | 4 saubere Zielprojekte (FastTests, IntegrationTests, TestKit, Engine) | Physische Isolation & Architektur-Gates aktiv |
| **Stress-Tests** | Ungesteuert im Legacy-Monolith | Isoliert in `AiNetLinter.IntegrationTests` getaggt (`[Trait("Category", "Stress")]`) | 2 Tests entdeckt, nie im Default-Run |

---

## 6. Stress Test Discovery

Erfasst via `dotnet test src/AiNetLinter.IntegrationTests --list-tests --filter "Category=Stress"`:
- `AiNetLinter.IntegrationTests.Mcp.McpTestClientParallelTests.ConnectAsync_SixteenParallelCalls_AllSucceedOrFailCleanly`
- `AiNetLinter.IntegrationTests.Baseline.SourceFileCatalogRegistrationStressTests.LoadAsync_TwentyParallelCallsAcrossFixtures_AllSucceed`

Gespeichert in: `TestResults/step029-final-stress-discovery.txt`.

---

## 7. Drift-Audit (find_duplicates MCP Tool)

Ausgeführt gemäß `.agents/skills/drift-audit/SKILL.md` via `find_duplicates(scopeDir="src", minTokens=20)`:
- **Ergebnis**: 0 unberechtigte Duplikate in den Testprojekten (`AiNetLinter.FastTests`, `AiNetLinter.IntegrationTests`, `AiNetLinter.TestKit`).
- Gemeinsame Hilfsfunktionen (`SolutionRootLocator`, `RecordingLintConsole`, `CompileErrorHeaderAssertions`, `RoslynTestSolutionFactory`) wurden vollständig in `AiNetLinter.TestKit` konsolidiert.
