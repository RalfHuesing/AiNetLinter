---
status: done (pending re-audit)
type: step-result
task: speedup-tests
step: 018
epic: EPIC-4
step_type: batch
coded_by: coder
coded_by_model: gpt-5.6-terra
coded_by_model_knowledge_cutoff: nicht ausgewiesen
coded_at: 2026-08-13
code_commit_hash: f0dbacc
status_after: done (pending re-audit)
blocker_category: n/a
---

# Result Step 018: Kumulativer MCP-Read-only-Snapshot-Super-Step

## Zusammenfassung

Step 018 migriert 23 historische read-only MCP-Testklassen auf virtuelle, besitzende
`RoslynTestSolution`-Snapshots und bewahrt `SuppressionScannerTests` als echten
Dateivertrag im Legacy-Projekt. Die interne Snapshot-Seam, virtuelle Projektpfade und der
dokumenttragende Walker ermoeglichen dabei residente Roslyn-Dokumente, ohne den Live-Refresh-Pfad
zu ersetzen.

Recovery 6 war der abschliessende, eng begrenzte Teilschnitt fuer fuenf FastTests-Klassen:
Temp-/Catalog-Helper und das Probeverzeichnis entfielen, waehrend alle 62 Testvertraege erhalten
blieben. Dieser Teilschnitt ist nicht mit dem gesamten kumulierten Codeumfang gleichzusetzen.

## Geaenderte Dateien in `f0dbacc`

- **31 FastTests-Dateien:** die 23 migrierten Zielklassen, fuenf Fixtures,
  `CompileErrorHeaderAssertions`, `McpCodeGraphServerReadOnlySnapshotTests` und
  `RoslynTestSolutionFactoryTests`.
- **7 Produktdateien:** `McpCodeGraphServerOptions.cs`, `McpCodeGraphServer.cs`,
  `ScopeChecker.cs`, `WalkedFile.cs`, `SolutionFileWalker.cs`, `GetHotspotsScanner.cs` und
  `MetricsTreeScanner.cs`. Die ersten beiden bilden die Snapshot-Seam; die weiteren fuenf
  Anpassungen behandeln residente virtuelle Dokumenttexte.
- **1 TestKit-Datei:** `RoslynTestSolutionFactory.cs` mit optionalem
  `ProjectSpec.VirtualProjectDirectory`.
- **1 Legacy-Datei:** `SuppressionScannerTests.cs`, ausschliesslich mit `#nullable enable`; keine
  Migration.

Die fuenf Recovery-6-Zieldateien sind `DependencyGraphScannerTests.cs`,
`DuplicateDetectionToolTests.cs`, `DuplicateDetectionToolRefactoringDriftTests.cs`,
`PatternDetectScannerTests.cs` und `SafeguardScannerTests.cs`.

## Commit-Historie

- `e864407` ist der vorangegangene Step-018-Roh-Move mit 24 Renames und Blocker-Ergebnis, kein
  fertiger Migrationsabschluss.
- `f0dbacc` ist der finale Sammel-Codecommit des bis dahin uncommittierten Recovery-Stands
  (40 `src`-Dateien, 857 Additionen, 831 Loeschungen):
  ```
  refactor(mcp): migriere Tooltests auf Snapshots [speedup-tests]
  ```
- Der vorherige Dokuabschluss `5fb77c1` wird durch den separaten Dokucommit dieser Korrekturrunde
  ersetzt. Code- und Dokucommit bleiben getrennt.
- Branch: `main`; kein Push.

## Recovery-Historie

| Phase | Belegter Inhalt | Ergebnis / Grenze |
|---|---|---|
| Baseline | `a6cc275` plante zwei Duplicate-Detection-Toolklassen; `880f6bc` erweiterte vor Ausfuehrung auf 24 Klassen. | Der Zweiklassenplan war damit ueberholt. |
| Recovery 1 | `e864407` sicherte 24 Roh-Renames; nach einer Legacy-Baseline 243/243 folgten 26 Compilefehler. | Blocker, kein fertiger Codeabschluss. |
| Recovery 2 | `e6b3000` plante 20 plattenfreie Klassen, vier Rueck-Moves und deklarative Specs; der Build wurde gruen, der 20er Lauf blieb 142/220 mit 78 Fehlern. | Die Annahme ohne Produkt-Seam war nicht tragfaehig. |
| Recovery 3 | `ae5aa73` plante Snapshot-Seam, `VirtualProjectDirectory`, Snapshot-Kontext und nur Suppression als Legacy-Rueck-Move. | Legitime Erweiterung auf Produkt/TestKit, Ziel: 23 Klassen. |
| Recovery 4 | `0846624`/`6f223ca` praezisierten die Schliessung der 42 verbleibenden Fehler. | Danach blieben Test-/Fixture-/Gatearbeiten. |
| Recovery 5 | Uncommittierter Zwischenstand schloss die funktionalen Gates und setzte den Ledger auf 23 `migrated`; Suppression blieb `pending`. | Kein eigener Plan- oder Codecommit; nur der statische Guard in fuenf Klassen blieb offen. |
| Recovery 6 | Die fuenf Zielklassen wechselten auf Factory-/Kontext-Snapshots und virtuelle Faulting-Snapshots. | 1:1 umgesetzt: 62 Namen und Assertions erhalten, statischer Guard null; nur dieser Teilschnitt. |

## Build-/Test-Output

```
statischer Fuenf-Dateien-Guard (Dateisystem/Catalog/Builder) → grün (0 Treffer; 62 Methoden)
dotnet test src\AiNetLinter.FastTests --filter "FullyQualifiedName~DependencyGraphScannerTests|FullyQualifiedName~DuplicateDetectionToolTests|FullyQualifiedName~DuplicateDetectionToolRefactoringDriftTests|FullyQualifiedName~PatternDetectScannerTests|FullyQualifiedName~SafeguardScannerTests" → grün (62 Tests, 0 Fehler)
dotnet test src\AiNetLinter.FastTests --filter "FullyQualifiedName~GetHotspotsToolTests|FullyQualifiedName~MetricsTreeToolTests|FullyQualifiedName~MetricsTreeRoslynScannerTests|FullyQualifiedName~GetCallTreeToolTests|FullyQualifiedName~FindReferencesToolTests|FullyQualifiedName~GetTypeHierarchyToolTests|FullyQualifiedName~GetViolationsToolTests|FullyQualifiedName~PatternDetectToolTests|FullyQualifiedName~SafeguardScannerTests|FullyQualifiedName~SafeguardToolTests|FullyQualifiedName~McpCodeGraphServerReadOnlySnapshotTests|FullyQualifiedName~RoslynTestSolutionFactoryTests" → grün (126 Tests, 0 Fehler)
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src\AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~LinterAnalyzerArchitectureRuleTests|FullyQualifiedName~LinterAnalyzerTests|FullyQualifiedName~CallGraphTraversalTests|FullyQualifiedName~DependencyGraphScannerTests|FullyQualifiedName~DependencyGraphToolTests|FullyQualifiedName~DiRegistrationHeuristicsTests|FullyQualifiedName~DuplicateDetectionToolRefactoringDriftTests|FullyQualifiedName~DuplicateDetectionToolTests|FullyQualifiedName~FindReferencesToolTests|FullyQualifiedName~GetCallTreeToolTests|FullyQualifiedName~GetFileSkeletonToolTests|FullyQualifiedName~GetHotspotsToolTests|FullyQualifiedName~GetSymbolBodyToolTests|FullyQualifiedName~GetTypeHierarchyToolTests|FullyQualifiedName~GetViolationsToolTests|FullyQualifiedName~McpToolResultsTests|FullyQualifiedName~MetricsTreeRoslynScannerTests|FullyQualifiedName~MetricsTreeToolTests|FullyQualifiedName~PatternDetectScannerTests|FullyQualifiedName~PatternDetectToolTests|FullyQualifiedName~SafeguardScannerTests|FullyQualifiedName~SafeguardToolTests|FullyQualifiedName~SymbolIdentifierResolverTests|FullyQualifiedName~McpCodeGraphServerReadOnlySnapshotTests|FullyQualifiedName~RoslynTestSolutionFactoryTests" → grün (253 Tests, 0 Fehler)
dotnet test src\AiNetLinter.Tests --no-build --filter "FullyQualifiedName~McpCodeGraphServerConstructorTests|FullyQualifiedName~McpCodeGraphServerFileDiscoveryTests|FullyQualifiedName~McpCodeGraphServerStalenessMtimeCacheTests" → grün (8 Tests, 0 Fehler)
dotnet test src\AiNetLinter.Tests --no-build --filter "FullyQualifiedName~SuppressionScannerTests" → grün (1 Test, 0 Fehler)
dotnet test src\AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~FastTestsDependencyGuardTests|FullyQualifiedName~TestCategoryProfileGuardTests" → grün (3 Tests, 0 Fehler)
dotnet test src\AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~TestMigrationLedgerConsistencyTests|FullyQualifiedName~LegacyProjectBuildGateTests" → grün (5 Tests, 0 Fehler)
statischer 23-Scope-Guard, Ledger-Check und git diff --check → grün (0 Treffer; 23 migrated; Suppression pending)
```

## Abweichungen vom letzten Recovery-6-Plan

- Der Gesamt-Step und `f0dbacc` sind nicht 1:1 aus dem letzten Recovery-6-Plan entstanden;
  `f0dbacc` aggregiert mehrere vorherige uncommittierte Recovery-Phasen.
- Nur der Recovery-6-Fuenfklassenschnitt wurde 1:1 umgesetzt; seine 62 Vertraege blieben erhalten.
- Die Snapshot-Seam und `VirtualProjectDirectory` waren ab Recovery 3 geplant.
- Die fuenf weiteren Produktanpassungen fuer residente Dokumenttexte entstanden in frueheren
  Recoveries und waren nicht Teil des engen Recovery-6-Plans.
- Entgegen dem 24-Klassen-Ausgangsplan wurde `SuppressionScannerTests` bewusst nicht migriert,
  sondern als Legacy-`pending` belassen.

## Beobachtungen

`f0dbacc` fasst mehrere zuvor uncommittierte Recovery-Phasen in einem Codecommit zusammen. Die
Zuordnung einzelner Hunks zu einer Recovery beruht daher auf Plan-, State- und TRX-Historie, nicht
auf einer Serie getrennter Codecommits.

## Bekannte Unschaerfen

Recovery 1 bis 3 waren in den damaligen Planfrontmattern nicht einzeln nummeriert. Ihre hier
dokumentierte Zuordnung rekonstruiert die Commitfolge und die nachfolgenden Eingangszustaende; sie
stellt keine getrennte Codecommit-Serie dar.
