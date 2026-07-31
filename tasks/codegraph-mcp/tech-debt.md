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
| TD-003 | `src/AiNetLinter/Baseline/SourceFileCatalog.cs` (`RegisterMSBuild`) | mittel | Nicht-thread-sicherer Check-then-Act (`if (!MSBuildLocator.IsRegistered)`) führt bei parallel laufenden Testklassen, die `SourceFileCatalog.LoadAsync` erstmalig aufrufen, intermittierend zu `InvalidOperationException`; durch die 5 neuen parallelen `LoadAsync`-Aufrufe in `McpCodeGraphServerTests` steigt die Kollisionswahrscheinlichkeit. |
| TD-004 | `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` | mittel | Sammelpunkt für alle `McpServerTool.Create(...)`-Registrierungen der 9 MCP-Tools; schon nach dem ersten Tool musste die Konstruktion aus `McpServerCommand.cs` ausgelagert werden, um `AIContextFootprint` (2500) nicht zu reißen — Risiko, dass die Factory selbst mit den restlichen 8 Tools erneut ans Limit kommt. |
| TD-005 | `src/AiNetLinter/Mcp/Tools/*Tool.cs` (pro-Tool-Klassen) | mittel | `McpCodeGraphServer` als Parametertyp einer Tool-`ExecuteAsync`-Methode zieht bereits allein einen Großteil des `AIContextFootprint`-Budgets (2500); in step-004 riss dadurch erstmals die Tool-Klasse selbst (`FindReferencesTool`, 2515), nicht die Factory (TD-004) — jede weitere EPIC-03/04-Tool-Klasse mit demselben Parameter hat kaum noch eigenen Zeilen-Spielraum. |

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

### TD-003 — Race Condition in `SourceFileCatalog.RegisterMSBuild` bei paralleler Testausführung [Priorität: mittel]

- **Gefunden in:** step-002 (Kritiker-Review vom 2026-07-31), beim eigenen Nachvollziehen von `dotnet test AiNetLinter.slnx`.
- **Ort:** `src/AiNetLinter/Baseline/SourceFileCatalog.cs`, Methode `RegisterMSBuild()` (Zeile 223 ff.) — `if (!MSBuildLocator.IsRegistered) { ... MSBuildLocator.RegisterDefaults(); ... }` ist ein klassischer nicht-thread-sicherer Check-then-Act: existierte bereits vor step-002 (nicht Teil des Commit-Diffs `81cf007`), unverändert übernommen.
- **Befund:** Ein erster `dotnet test`-Lauf schlug mit `System.InvalidOperationException: MSBuildLocator.RegisterInstance was called, but MSBuild assemblies were already loaded` fehl (in `McpCodeGraphServerTests.GetCurrentSolution_FileTouchedWithoutContentChange_SkipsSolutionUpdate`, ausgelöst über `SourceFileCatalog.LoadAsync` → `RegisterMSBuild`). Ein direkt anschließender zweiter Lauf war grün (1027/1027) — klassisches Timing-Flake, kein Logikfehler in `McpCodeGraphServer` selbst. Bereits vor diesem Step existierte mindestens ein weiterer Aufrufer ohne Serialisierungs-Kollektion (`SourceFileCatalogTests.cs`, keine `[Collection(...)]`-Annotation); `McpCodeGraphServerTests.cs` fügt fünf weitere parallele Erstaufrufe von `LoadAsync` hinzu (kein `[Collection("ConsoleTestCollection")]` o. ä.) und erhöht damit die Kollisionswahrscheinlichkeit spürbar.
- **Warum nicht sofort gefixt:** Die Race Condition liegt in bereits bestehendem, nicht von diesem Step geändertem Code (`RegisterMSBuild`) — ein Fix (z. B. `lock`/`SemaphoreSlim` um die Registrierung, oder eine gemeinsame xUnit-Test-Collection zur Serialisierung aller Solution-Load-Tests) würde über den Scope von step-002 (Staleness-Logik in `McpCodeGraphServer`) hinausgehen.
- **Vorschlag:** `RegisterMSBuild()` mit einem statischen Lock absichern (Check-Lock-Check), und/oder alle Testklassen, die `SourceFileCatalog.LoadAsync` erstmalig aufrufen, in eine gemeinsame, nicht-parallele xUnit-Collection stecken. Vor EPIC-07 (weitere MCP-Integrationstests, die ebenfalls `LoadAsync` nutzen werden) angehen, da die Kollisionswahrscheinlichkeit mit jeder weiteren parallelen Testklasse weiter steigt.
- **Status:** offen

### TD-004 — `McpServerOptionsFactory` als wachsender Sammelpunkt nahe `AIContextFootprint`-Limit [Priorität: mittel]

- **Gefunden in:** step-003 (Kritiker-Review vom 2026-07-31), vom Coder selbst unter „Beobachtungen" in `step-003/step-result.md` vorgewarnt.
- **Ort:** `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` (neu in step-003).
- **Befund:** Bereits die Registrierung des ersten von 9 MCP-Tools zwang dazu, `CreateServerOptions`/`BuildToolCollection`/`GetServerVersion` aus `McpServerCommand.cs` in eine eigene Datei auszulagern, weil `McpCodeGraphServer` als Parametertyp eines `McpServerCommand`-Members die `AIContextFootprint`-Metrik (2500 Zeilen transitive eigene Typen) auf 2553 trieb (verifiziert per `git stash`-Vergleich, siehe `step-003/step-result.md` „Abweichungen vom Plan"). Die neue `McpServerOptionsFactory` sammelt jetzt alle `McpServerTool.Create(...)`-Aufrufe an einer Stelle — mit den restlichen 4 EPIC-03-Tools (`find_references`, `get_impact`, `get_type_hierarchy`, `get_file_skeleton`) plus den EPIC-04-Tools droht dieselbe Factory selbst wieder an die Grenze zu stoßen, da jedes Tool seine eigene transitive Typ-Kette (z. B. `DiffImpactAnalyzer`) mitbringt.
- **Warum nicht sofort gefixt:** Betrifft die Registrierungs-Architektur über mehrere kommende EPIC-03/EPIC-04-Steps hinweg, nicht step-003 selbst — dort reicht die jetzige Lösung (1 Tool) aus.
- **Vorschlag:** Bei den nächsten 1-2 EPIC-03-Tool-Steps `dotnet build`/Selbst-Lint auf `AIContextFootprint` für `McpServerOptionsFactory` im Auge behalten; falls die Grenze erneut droht, Tool-Registrierung auf mehrere kleinere Factory-Klassen (z. B. pro Tool-Gruppe) aufteilen statt einer monolithischen Sammelstelle.
- **Status:** offen
- **Update (step-004, 2026-07-31):** Die Factory selbst blieb wie
  vorhergesagt unauffällig (vom Coder in `step-004/step-plan.md`
  vorab geprüft und durch Selbst-Lint bestätigt) — das Limit riss
  stattdessen in der neuen Tool-Klasse selbst (`FindReferencesTool`,
  2515>2500). Gleiches Grundproblem (`McpCodeGraphServer` als
  footprint-teurer Parametertyp), andere Stelle — siehe TD-005 für die
  verallgemeinerte Beobachtung, die über TD-004s ursprünglichen Scope
  (nur die Factory) hinausgeht.

### TD-005 — `McpCodeGraphServer`-Parameter lässt Tool-Klassen kaum eigenen `AIContextFootprint`-Spielraum [Priorität: mittel]

- **Gefunden in:** step-004 (Kritiker-Review vom 2026-07-31), beim
  Nachvollziehen der in `step-004/step-result.md` dokumentierten
  Abweichung 1 (`SymbolIdentifierResolver.cs`-Auslagerung).
- **Ort:** `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs` (aktuell),
  strukturell jede künftige `*Tool.cs`-Klasse mit
  `ExecuteAsync(McpCodeGraphServer state, ...)`-Signatur
  (`FindSymbolTool.cs` bisher knapp unter dem Limit geblieben).
- **Befund:** `McpCodeGraphServer` als Parametertyp zieht über
  `SourceFileCatalog`/Config-Klassen bereits einen erheblichen Teil des
  `AIContextFootprint`-Limits (2500 Zeilen) transitiv mit — bestätigt
  durch `AIContextFootprintCalculator` (`src/AiNetLinter/Metrics/
  AIContextFootprintCalculator.cs`): die Summe zählt **alle** besuchten
  Typen inkl. der Zielklasse selbst, d. h. die eigene Dateilänge der
  Tool-Klasse zählt voll mit. In step-003 riss dadurch
  `McpServerOptionsFactory` (TD-004), in step-004 traf es stattdessen
  `FindReferencesTool` selbst (2515>2500) — derselbe strukturelle
  Engpass, zwei unterschiedliche Stellen. Jede der noch ausstehenden
  EPIC-03/04-Tool-Klassen (`get_impact`, `get_type_hierarchy`,
  `get_file_skeleton`, EPIC-04-Footprint-Tools) hat mit diesem Parameter
  von vornherein nur noch wenig eigenen Zeilen-Spielraum, bevor
  Hilfsmethoden in eine separate Datei ausgelagert werden müssen.
- **Warum nicht sofort gefixt:** Betrifft die Tool-Architektur über
  mehrere kommende EPIC-03/04-Steps hinweg, nicht step-004 selbst — dort
  wurde die konkrete Verletzung bereits sauber (ohne neue
  Abstraktionsebene) durch Auslagerung der reinen Parsing-Helfer
  behoben.
- **Vorschlag:** Falls sich das Muster bei einem der nächsten 2-3
  Tool-Steps wiederholt, in Erwägung ziehen: entweder generell pro
  Tool-Klasse von Anfang an in „Kern-Methode" + „private Helfer-Datei"
  aufteilen (wie in step-004 nachträglich gemacht), oder prüfen, ob
  `McpCodeGraphServer`s eigener transitiver Footprint reduzierbar ist
  (z. B. durch `ignoreTypeNames`/`ignoreNamespacePrefixes` in
  `AIContextFootprintCalculator`, falls dafür bereits ein
  Konfigurationsmechanismus existiert).
- **Status:** offen
