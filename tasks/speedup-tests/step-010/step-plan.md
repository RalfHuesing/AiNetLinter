---
status: done
type: step-plan
task: speedup-tests
step: 010
corrects: null
title: "EPIC-3 Teil 1 — Core/Checkers-Kohorte (28 Klassen) nach AiNetLinter.FastTests migrieren"
epic: EPIC-3
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-12
related_to: []
---

# Step 010: EPIC-3 Teil 1 — Core/Checkers-Kohorte (28 Klassen) nach AiNetLinter.FastTests migrieren

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-3` aus `roadmap.md` — „Checker-/Parser-/Renderer-Kohorte auf Unit-Ebene migrieren".
  Dieser Step deckt den ersten, klar abgegrenzten Teil ab: die komplette `Core/Checkers`-Kohorte
  (28 Legacy-Testklassen). Parser (`Web/*AnalyzerTests`) und Renderer (`Mcp/Tools/*RendererTests`)
  bleiben bewusst offen für die nächsten EPIC-3-Steps.
- **Konzept-Referenz:** `konzept.md` §9 „Sinnvolle Kohorten" Punkt 2 (reine Logik-/Syntax-/kleine-
  Compilation-Tests ohne MSBuild/Prozess/Repo → `AiNetLinter.FastTests`), Leitplanke 1.

## Aktueller Projektzustand (JIT-Kontext)

- Alle 28 Testklassen unter `src/AiNetLinter.Tests/Core/Checkers/` sind bereits heute
  `[Trait("Category", "Unit")]` und arbeiten ausschließlich über `CSharpSyntaxTree.ParseText`/
  `CSharpCompilation` (In-Memory-Syntaxbaum, kein `MSBuildWorkspace`, kein Prozess, kein Zugriff auf
  die eigene Solution) — verifiziert durch Stichproben (`NamingCheckerTests`, `AsciiIdentifiersTests`,
  `CouplingSemanticTests`, `WpfCodeBehindTests`, `UiFileSeparationCheckerTests`). Sie sind damit
  bereits heute strukturell das, was `AiNetLinter.FastTests`/Category `Unit` verlangt — reine
  Verschiebung, keine Testlogik-Änderung nötig.
- Alle 28 Klassen hängen ausschließlich von einer Teilmenge von `src/AiNetLinter.Tests/TestHelper.cs`
  ab: `CreateDefaultConfig`, `ParseCode`, `CreateContext`, `CreateContextWithLoadDiagnostics`,
  `CreateSemanticModel`, `BuildCalibratedMethod`/`CalibratedBaseStatements`,
  `DeleteDirectoryIfExists` (per Grep über `src/AiNetLinter.Tests/Core/Checkers/*.cs` verifiziert —
  keine andere `TestHelper`-Methode wird von dieser Kohorte referenziert). Das komplette
  `TestHelper.cs` (246 Zeilen, u. a. `CreateFaultySolution`, `FindSlnxFile`,
  `TryDeleteLogFileAndDirectory`) bewusst **nicht** 1:1 mitziehen — die dort fehlenden Methoden
  gehören zu anderen, noch nicht migrierten Kohorten (MCP-Tools, Baseline) und würden verfrühte
  Kopplung in `AiNetLinter.FastTests` einführen.
- `AiNetLinter.FastTests` hat aktuell keine eigene `TestHelper`-Klasse. `CheckerContext` und
  `DocumentLoadState` (`src/AiNetLinter/Core/Checkers/CheckerContext.cs`) sind `internal` —
  `AiNetLinter.FastTests` ist bereits über `InternalsVisibleTo` in `LinterEngine.cs` freigeschaltet
  (aus step-004), keine Änderung an `InternalsVisibleTo` nötig.
- `src/AiNetLinter.FastTests/Core/` enthält bisher zwei Dateien direkt im Ordner
  (`LinterEngineSolutionAnalysisTests.cs`, `TestProjectDetectorSuffixTests.cs`, siehe `codemap.md`)
  — neue Unterordner-Konvention `Core/Checkers/` folgt 1:1 der Legacy-Struktur
  `src/AiNetLinter.Tests/Core/Checkers/`.
- `test-migration-ledger.md` hat aktuell für alle 28 Zeilen mit Produktbereich `Checkers` Status
  `pending` und leere „Neuer Abdeckungsort"-Spalte — das ist die erste Kohorte, die diese Spalte
  überhaupt befüllt; Format laut `TestMigrationLedgerConsistencyTests.ExtractPathFromCoverageLocation`:
  ein Dateipfad, optional in Backticks (`` `src/...` ``), muss nach dem Move real existieren.
- `MaxDirectoryChildren` (Grenzwert 30, siehe `AiNetLinter.mdc`) ist relevant: 28 Testklassen + 1 neue
  `TestHelper.cs` in `Core/Checkers/` = 29 Dateien — unter dem Limit, aber knapp. Kein Grund, den Step
  deswegen zu splitten (Kohorte ist konzeptionell eine Einheit), aber falls diese Kohorte später noch
  wächst, ist eine Unterteilung nötig.

## Intention

Nach diesem Step deckt `AiNetLinter.FastTests` die komplette Checker-Kohorte (28 Testklassen, reine
Logik-/Syntax-Tests) verlustfrei ab; die entsprechenden Legacy-Klassen sind aus
`src/AiNetLinter.Tests/Core/Checkers/` physisch gelöscht, das Ledger spiegelt das wider. Der Move ist
bewusst rein mechanisch (nur `namespace`-Zeile ändert sich, `TestHelper.X`-Aufrufe bleiben
unqualifiziert und unverändert, weil die neue `TestHelper`-Klasse denselben Namen in der neuen
Projekt-Root-Namespace `AiNetLinter.FastTests` bekommt — identisches Auflösungsmuster zum
Legacy-Vorbild `AiNetLinter.Tests`) — keine Testlogik-Änderung, keine Assertion-Änderung.

## Konkrete Änderungen

### Neue Datei: `src/AiNetLinter.FastTests/TestHelper.cs`

- **Was:** Neue `internal static class TestHelper` in Namespace `AiNetLinter.FastTests` (Projekt-Root,
  damit `EnforceNamespaceDirectoryMapping` erfüllt bleibt — analog zum Legacy-Vorbild, das ebenfalls
  im Projekt-Root liegt). Enthält **nur** die von der Checker-Kohorte tatsächlich genutzte Teilmenge
  aus `src/AiNetLinter.Tests/TestHelper.cs`:
  - `CreateDefaultConfig()`
  - `ParseCode(string source)`
  - `CreateContext(Config?, SemanticModel?, bool, string, string?)`
  - `CreateContextWithLoadDiagnostics(Config, SemanticModel, bool, string, string?)`
  - `CreateSemanticModel(string source)`
  - `BuildCalibratedMethod(string className, string methodName)` + `CalibratedBaseStatements`
  - `DeleteDirectoryIfExists(string path)`
  Implementierung Zeile für Zeile identisch zum jeweiligen Legacy-Original (siehe Code-Skizze unten) —
  keine Verhaltensänderung, nur Kopie der benötigten Teilmenge in neue Namespace/Projekt.
- **Warum:** Diese sieben Methoden sind die einzige Abhängigkeit der 28 Checker-Testklassen auf
  `TestHelper`; der volle Grab-Bag (`CreateFaultySolution`, `FindSlnxFile`, `TryDeleteLogFileAndDirectory`,
  `TryDeleteDirectoryRecursive`) gehört zu anderen, noch offenen Kohorten (MCP-Tools, Baseline,
  Fixtures) und soll nicht vorzeitig in `AiNetLinter.FastTests` einziehen.

### Verschiebung: 28 Testklassen `src/AiNetLinter.Tests/Core/Checkers/*.cs` → `src/AiNetLinter.FastTests/Core/Checkers/*.cs`

- **Was:** Jede der folgenden Dateien 1:1 (Dateiname identisch) von
  `src/AiNetLinter.Tests/Core/Checkers/` nach `src/AiNetLinter.FastTests/Core/Checkers/` verschieben.
  Einzige Textänderung pro Datei: `namespace AiNetLinter.Tests.Core.Checkers;` →
  `namespace AiNetLinter.FastTests.Core.Checkers;`. Alle `using`-Zeilen, alle `TestHelper.X`-Aufrufe
  (unqualifiziert, lösen über die neue gleichnamige Klasse in der Eltern-Namespace
  `AiNetLinter.FastTests` auf — identisches Muster zum Legacy-Vorbild) und der gesamte Testkörper
  bleiben unverändert:
  - `AsciiIdentifiersTests.cs`, `AsyncVoidCheckerTests.cs`, `BlockingTaskCheckerTests.cs`,
    `CouplingSemanticTests.cs`, `DuplicateCodeCheckerTests.cs`, `DynamicTypeCheckerTests.cs`,
    `LinqChainLengthCheckerTests.cs`, `MaxBoolParameterCountTests.cs`,
    `MaxConstructorDependenciesTests.cs`, `MaxInheritanceDepthTests.cs`,
    `MaxPartialClassFilesTests.cs`, `MaxPublicMembersPerTypeTests.cs`, `MaxSwitchArmsTests.cs`,
    `MethodParameterCountAccessibilityTests.cs`, `MethodParameterCountIgnoreTypePrefixesTests.cs`,
    `MethodParameterCountOverrideTests.cs`, `MiddleManCheckerTests.cs`,
    `NamespaceCouplingCheckerTests.cs`, `NamespaceDirectoryMappingTests.cs`, `NamingCheckerTests.cs`,
    `NestedTypesCheckerTests.cs`, `PhantomDependencyCheckerTests.cs`, `SealedClassCheckerTests.cs`,
    `SilentCatchAllowedTypesTests.cs`, `SwitchDispatcherDetectorTests.cs`,
    `UiFileSeparationCheckerTests.cs`, `ValueObjectCheckerTests.cs`, `WpfCodeBehindTests.cs`
  - Nach dem Verschieben: Legacy-Quelldateien unter `src/AiNetLinter.Tests/Core/Checkers/` physisch
    löschen (Ordner bleibt leer stehen, wird nicht selbst gelöscht — andere `Core/*`-Legacy-Dateien
    bleiben unberührt).
- **Warum:** Bereits heute reine Unit-Logik-Tests ohne MSBuild/Prozess-Abhängigkeit — exakt die
  Zielgruppe von `konzept.md` §9 Punkt 2. Keine Umformulierung nötig, nur Ortswechsel.

### `tasks/speedup-tests/test-migration-ledger.md` — 28 Zeilen `pending` → `migrated`

- **Was:** Für jede der 28 Zeilen mit Produktbereich `Checkers` (Testklassen wie oben aufgeführt):
  Spalte „Status" von `pending` auf `migrated`, Spalte „Neuer Abdeckungsort" von leer auf
  `` `src/AiNetLinter.FastTests/Core/Checkers/<Dateiname>.cs` `` (Backtick-Pfad, exakt wie von
  `TestMigrationLedgerConsistencyTests.ExtractPathFromCoverageLocation` erwartet). `last_updated` im
  Frontmatter auf das aktuelle Datum.
- **Warum:** Konsistenzregel 2/3 des Ledgers (`TestMigrationLedgerConsistencyTests`) verlangt das
  mechanisch — ohne dieses Update schlägt
  `MigratedOrConsolidatedEntries_HaveExistingNewCoverageLocation` bzw. bleibt die Zeile fälschlich
  `pending`, obwohl die Legacy-Quelle bereits gelöscht ist (Regel 2 würde dann sogar fehlschlagen, weil
  eine `pending`-Zeile keine gelöschte Quelldatei referenzieren darf — Statuswechsel ist also nicht
  optional, sondern Teil der Konsistenzinvariante).

### `tasks/speedup-tests/codemap.md` — Eintrag ergänzen

- **Was:** Neuer Eintrag unter „Projekt- und Laufverträge" (oder eigener Abschnitt „Migrierte
  Kohorten"): `src/AiNetLinter.FastTests/Core/Checkers/` — 28 migrierte Checker-Testklassen + eigene
  `TestHelper.cs`-Teilmenge, erster EPIC-3-Meilenstein. Bestehende Zeile zu
  `src/AiNetLinter.Tests/TestHelper.cs`/`Core/Checkers/*` als weiterhin gültig für die verbleibenden
  Legacy-Kohorten belassen (nicht obsolet — andere Bereiche referenzieren das volle `TestHelper.cs`
  noch).
- **Warum:** Pointer-Pflicht des Planers/Coders (siehe `SKILL.md` Schritt 1a) — nächster Step-Modus-
  Aufruf muss sofort sehen, dass Checkers bereits migriert ist, um EPIC-3 Teil 2 (Parser/Renderer)
  korrekt JIT zu planen.

## Tests

- [ ] `dotnet build src/AiNetLinter.FastTests` → grün
- [ ] `dotnet build src/AiNetLinter.Tests` → grün (Projekt kompiliert weiterhin trotz gelöschter
      Dateien)
- [ ] `dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~Core.Checkers` → alle 28
      migrierten Klassen grün, gleiche Anzahl Testfälle wie vorher im Legacy-Projekt
      (`dotnet test src/AiNetLinter.Tests --filter FullyQualifiedName~Core.Checkers` **vor** dem Move
      als Vergleichsbasis laufen lassen)
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~TestMigrationLedgerConsistencyTests`
      → grün (alle vier Konsistenzregeln)
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~LegacyProjectBuildGateTests`
      → grün (weiterhin `pending`-Zeilen vorhanden, Legacy-Projekt muss Solution-Mitglied bleiben)
- [ ] `dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~FastTestsDependencyGuardTests`
      → grün (neue `TestHelper.cs` referenziert keine verbotene Infrastruktur)
- [ ] `dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~TestCategoryProfileGuardTests`
      → grün (alle verschobenen Klassen haben weiterhin genau einen `Category`-Trait; `TestHelper.cs`
      selbst hat keine `[Fact]`/`[Theory]`, fällt nicht unter den Guard)

Kein voller `Category!=Stress`-Lauf in diesem Step (siehe `roadmap.md` Tech-Stack-Notiz „Sparsame
Verifikation") — nur die gefilterten Läufe oben. Voller Lauf erst am EPIC-3-Ende bzw. Task-Ende.

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün
- [ ] Gefilterte Test-Commands aus „Tests" oben grün
- [ ] Commit auf aktuellem Branch (Conventional Commit)
- [ ] `step-010/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` „Projekt-Overrides" — `*Tests`-Override (`MaxMethodLineCount` 100,
  `EnforceSealedClasses` aus) gilt für `AiNetLinter.FastTests`, damit auch für die neue
  `TestHelper.cs`; `MaxDirectoryChildren` (30) — `Core/Checkers/` landet bei 29 Dateien, unter dem
  Limit, aber im Blick behalten, falls die Kohorte in einem Korrektur-Step noch wächst.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 „Sparsamer Einsatz von Code-Kommentaren" — beim
  Kopieren von `TestHelper`-Methoden keine neuen Task-/Step-ID-Referenzen einführen (die XML-Doc-
  Kommentare im Legacy-`TestHelper.cs` sind ID-frei, können 1:1 übernommen werden).

## Bekannte Ausnahmen

Keine.

## Code-Skizze

```csharp
// src/AiNetLinter.FastTests/TestHelper.cs
#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core.Checkers;
using AiNetLinter.Models;

namespace AiNetLinter.FastTests;

internal static class TestHelper
{
    public static Config CreateDefaultConfig() => new Config
    {
        Global = new GlobalConfig(),
        Metrics = new MetricsConfig()
    };

    public static (SyntaxTree Tree, SemanticModel Model) ParseCode(string source)
    {
        // 1:1 aus src/AiNetLinter.Tests/TestHelper.cs Zeile 27-59 übernehmen
    }

    public static CheckerContext CreateContext(
        Config? config = null, SemanticModel? semanticModel = null,
        bool isTestFile = false, string filePath = "Test.cs", string? projectName = null)
    {
        // 1:1 aus src/AiNetLinter.Tests/TestHelper.cs Zeile 61-81 übernehmen
    }

    public static CheckerContext CreateContextWithLoadDiagnostics(
        Config config, SemanticModel semanticModel, bool projectHasLoadDiagnostics,
        string filePath = "Test.cs", string? projectName = null)
    {
        // 1:1 aus src/AiNetLinter.Tests/TestHelper.cs Zeile 88-97 übernehmen
    }

    public static void DeleteDirectoryIfExists(string path)
    {
        // 1:1 aus src/AiNetLinter.Tests/TestHelper.cs Zeile 119-123 übernehmen
    }

    public static string BuildCalibratedMethod(string className, string methodName)
    {
        // 1:1 aus src/AiNetLinter.Tests/TestHelper.cs Zeile 178-187 übernehmen
    }

    public static readonly string[] CalibratedBaseStatements = [ /* 1:1 Zeile 194-200 */ ];

    public static SemanticModel CreateSemanticModel(string source)
    {
        // 1:1 aus src/AiNetLinter.Tests/TestHelper.cs Zeile 207-215 übernehmen
    }
}
```

## Notes

- **Kein `sed`/Skript für den Move:** Windows-Umgebung, `AiNetLinterRichtlinien.mdc` §3 — Dateien
  einzeln über das Edit-/Write-Tool verschieben (neu schreiben + alte löschen), kein Bulk-Rename-Skript.
- **Self-Lint-Beobachtung (kein Blocker, nur Hinweis):** Bis die verbleibenden ~155 Legacy-Kohorten
  migriert sind, existieren `TestHelper.CreateDefaultConfig`/`ParseCode` inhaltlich fast identisch in
  zwei Projekten (`AiNetLinter.Tests` und `AiNetLinter.FastTests`). Falls der Coder im Rahmen dieses
  Steps ohnehin einen Selbstlint-Lauf macht und `DuplicateCode`-Funde zwischen den beiden
  `TestHelper.cs`-Dateien auftauchen: das ist erwartbar und **kein** Grund, den Step zu blockieren oder
  vorzeitig das komplette `TestHelper.cs` zu migrieren — der Zustand ist während der laufenden
  Strangler-Migration unvermeidlich und löst sich mit EPIC-7 (Legacy-Löschung) von selbst auf. Nicht
  eigenmächtig unterdrücken oder in `tech-debt.md` eintragen, außer der Kritiker findet es beim Review
  tatsächlich relevant.
- **Nächste EPIC-3-Teilschritte (nicht Teil dieses Steps):** `Web/*AnalyzerTests` (Parser-Kohorte:
  `CssAnalyzerTests`, `JsAnalyzerTests`, `RazorAnalyzerTests`, `RazorAnalyzerExtendedTests`,
  `WebSuppressionDetectorTests`) und `Mcp/Tools/*RendererTests`
  (`CallTreeMermaidRendererTests`, `MetricsTreeRendererTests`) — bewusst für einen der nächsten
  Step-Modus-Aufrufe zurückgestellt, um diesen Step in sich geschlossen zu halten.
