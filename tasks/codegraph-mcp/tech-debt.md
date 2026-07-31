---
task: codegraph-mcp
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-07-31
---

# Tech-Debt-Log: codegraph-mcp

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
| TD-001 | `AiNetLinter.csproj` (`ModelContextProtocol`-Paket) | niedrig | Transitive `Microsoft.Extensions.AI.Abstractions`-Abhängigkeit ungenutzt mitgezogen, relevant für spätere Footprint-Tools (EPIC-04). |
| TD-002 | `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` | niedrig | End-to-End-Test startet echten Subprozess (`AiNetLinter.exe`), spürbar länger als Unit-Tests — bei weiteren Subprozess-Tests in EPIC-07 ggf. Fixture-Prozess-Pool erwägen. |

## Einträge

### TD-001 — Ungenutzte transitive `Microsoft.Extensions.AI.Abstractions`-Abhängigkeit [Priorität: niedrig]

- **Gefunden in:** step-001 (Kritiker-Review vom 2026-07-31)
- **Ort:** `src/AiNetLinter/AiNetLinter.csproj` — `PackageReference Include="ModelContextProtocol" Version="2.0.0"`
- **Befund:** Das `ModelContextProtocol`-NuGet-Paket zieht `Microsoft.Extensions.AI.Abstractions` 10.8.3 transitiv mit (für SDK-Features wie `SampleAsync`/`IChatClient`, in diesem Step ungenutzt). Vom Coder in `step-result.md` unter "Beobachtungen" selbst vermerkt.
- **Warum nicht sofort gefixt:** Kein Fehlverhalten, keine Regelverletzung — reine Abhängigkeits-Footprint-Beobachtung, die erst relevant wird, wenn EPIC-04 (`get_hotspots`/Footprint-Tools) gegen die Solution-Kopplung rechnet und diese zusätzliche Abhängigkeit mitzählt. Außerhalb des Scopes von step-001, das nur den Server-Grundbau liefert.
- **Vorschlag:** Bei EPIC-04 kurz prüfen, ob die zusätzliche Abhängigkeit den `AIContextFootprint`/`MaxConstructorDependencies` bestehender Regeln für den MCP-Codepfad spürbar beeinflusst; falls ja, ggf. gezieltere Paket-Referenz (nur Server statt vollem SDK) evaluieren, falls das SDK das anbietet.
- **Status:** offen

### TD-002 — Subprozess-basierter E2E-Test ohne Fixture-Pool [Priorität: niedrig]

- **Gefunden in:** step-001 (Kritiker-Review vom 2026-07-31)
- **Ort:** `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` — `RunAsync_ValidFixture_ServerRespondsWithEmptyToolList`
- **Befund:** Der einzige echte End-to-End-Test startet pro Testlauf einen vollständigen Subprozess (`AiNetLinter.exe`) inkl. MSBuildLocator-Registrierung und Solution-Load der Mini-Fixture — spürbar langsamer als die übrigen (überwiegend reinen In-Process-)Tests. Bei 1021 Gesamttests aktuell nicht spürbar, aber ein Muster, das sich bei weiteren MCP-Integrationstests multiplizieren würde.
- **Warum nicht sofort gefixt:** Betrifft die künftige Teststrategie über mehrere kommende Steps/Epics hinweg (EPIC-02/EPIC-03/EPIC-07), nicht step-001 selbst — dort ist ein einzelner Subprozess-Test angemessen und ausreichend.
- **Vorschlag:** Falls EPIC-07 weitere Subprozess-basierte MCP-Integrationstests ergänzt, einen gemeinsamen, wiederverwendbaren Fixture-Prozess (bzw. In-Memory-Transport statt Subprozess, falls das SDK das für Tests anbietet) statt eines Subprozesses pro Testfall erwägen.
- **Status:** offen
