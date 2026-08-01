# Implementierungsplan: Test-Infrastruktur & Performance-Optimierung (AiNetLinter.Tests)

Dieses Dokument dient als detaillierter Implementierungsplan und Fortschritts-Checkliste für die Optimierung der Testsuite von `AiNetLinter.Tests`.
Das Dokument `tasks/test-optimierung/konzept.md` bleibt unverändert.

---

## 0. Automatische Messung der Testdauer (Profiling & Diagnostics)

Ziel: Jedes `dotnet test` speichert automatisch detaillierte Zeitmessungen pro Testmethode und hebt langlaufende Tests in der Konsole hervor.

- [x] **0.1 .runsettings Konfiguration für automatisches TRX-Logging**
  - Anlegen von `.runsettings` im Solution-Root (oder Konfiguration in `AiNetLinter.Tests.csproj`), die `.trx`-Dateien automatisch nach `TestResults/` schreibt (Pfad ist bereits in `.gitignore` abgedeckt).
- [x] **0.2 xUnit Warnungen für langsame Tests**
  - Konfiguration von `longRunningTestSeconds: 3` in `xunit.runner.json`, sodass Tests, die länger als 3 Sekunden dauern, automatisch als Warnung diagnostiziert werden.

---

## 1. Muss-Haben 3: xUnit Parallelisierungskonfiguration (`xunit.runner.json`)

- [x] **1.1 `xunit.runner.json` anlegen**
  - Pfad: `src/AiNetLinter.Tests/xunit.runner.json`
  - Einstellungen:
    - `"parallelizeAssembly": false`
    - `"parallelizeTestCollections": true`
    - `"maxParallelThreads": 0` (nutzt alle verfügbaren CPU-Kerne)
    - `"diagnosticMessages": true`
    - `"longRunningTestSeconds": 3`
- [x] **1.2 `AiNetLinter.Tests.csproj` anpassen**
  - `<Content Include="xunit.runner.json" CopyToOutputDirectory="PreserveNewest" />` hinzufügen.

---

## 2. Muss-Haben 6: `ILintConsole`-Testdouble konsolidieren

- [x] **2.1 Neues konsolidiertes `TestLintConsole` erstellen**
  - Pfad: `src/AiNetLinter.Tests/Output/TestLintConsole.cs`
  - Bietet `List<string> Output` & `List<string> Errors` sowie `string OutputText` & `string ErrorText`.
- [x] **2.2 Duplikat löschen**
  - `src/AiNetLinter.Tests/Maps/TestLintConsole.cs` entfernen.
- [x] **2.3 Aufrufe umstellen & verifizieren**
  - Alle Testdateien in `Maps/*`, `Output/*`, `Commands/*`, `Evals/*`, `Core/*` auf das neue `TestLintConsole` umstellen.

---

## 3. Muss-Haben 4: `ConsoleTestCollection`-Zwangsserialisierung eingrenzen

- [x] **3.1 `ConsoleTestCollection` von Nicht-Console-Tests entfernen**
  - Entfernen von `[Collection("ConsoleTestCollection")]` bei Tests, die keine globalen `Console.Out`/`Console.Error` Umleitungen durchführen (`SkeletonMapBuilderTests`, `LinterEngineCacheTests`, `EvalAssemblerTests` etc.).
- [x] **3.2 Zwangsserialisierung nur bei echten CLI-/Console-Captures belassen**
  - Dokumentierte Einbehaltung bei `ProgramTests`, `CliIntegrationTests`, `BaselineCliTests`, `DocsCommandTests`, `AuditCommandTests`, `SyncAgentRulesCommandTests`, `PlaybookCheckCommandTests`, `FilterCliIntegrationTests`.

---

## 4. Muss-Haben 5: Gemeinsamer Temp-Dir-Helper (`TestTempDirectory`)

- [x] **4.1 `TestTempDirectory` Class implementieren**
  - Pfad: `src/AiNetLinter.Tests/Fixtures/TestTempDirectory.cs`
  - Implementiert `IDisposable`.
  - Verwendet `Directory.CreateTempSubdirectory("AiNetTest_")` mit robuster Löschlogik im `Dispose()`.
- [x] **4.2 Handgerollte `Path.GetTempPath()` Stellen refactoren**
  - Umstellen von Stellen in `Cache/`, `Evals/` etc. auf `TestTempDirectory.Create()`.

---

## 5. Muss-Haben 7: Root-Testdateien in Namespace-Ordner einsortieren

- [x] **5.1 Testdateien in Zielordner verschieben & Namespaces anpassen**
  - 21 lose Testdateien aus `src/AiNetLinter.Tests/` in die passenden Namespace-Unterordner verschoben (`Architecture/`, `Core/`, `Core/Checkers/`, `Metrics/`, `Configuration/`, `Cli/`).
- [x] **5.2 Imports & Namespace-Referenzen korrigieren**
  - Namespaces in allen 21 verschobenen Testdateien aktualisiert.

---

## 6. Muss-Haben 2: `[Trait("Category", "Unit"|"Integration")]`-Kategorisierung

- [x] **6.1 Integration- & Unit-Tests auszeichnen**
  - `[Trait("Category", "Integration")]` an `CliIntegrationTests`, `FilterCliIntegrationTests`, `BaselineCliTests`, `AuditCommandTests`, `DocsCommandTests`, `SyncAgentRulesCommandTests`, `PlaybookCheckCommandTests`, `ProgramTests` angebracht.
  - `[Trait("Category", "Unit")]` an Unit-Test-Klassen in `Core/Checkers` angebracht.

---

## 7. Muss-Haben 1 (Teilweise): Fixture-Sharing für `BaselineMini` & `SymbolGraphMini`

- [x] **7.1 Duplizierte Workspace-Logik refactoren**
  - Pfad: `src/AiNetLinter.Tests/Fixtures/FixtureWorkspaceBase.cs`
  - Gemeinsame Logik für `FindSolutionRoot()`, `CopyFixture()`, `IsGeneratedPath()` in Basisklasse ausgelagert.
  - `BaselineMiniFixtureWorkspace` & `SymbolGraphMiniFixtureWorkspace` erben nun von `FixtureWorkspaceBase`.

---

## 8. Verifikation & Abschluss

- [x] **8.1 `dotnet build` & `dotnet test` Verifikation**
  - Vollständigen Testlauf durchgeführt: 1150/1150 Tests grün (0 Fehler).
  - Gefilterten Lauf durchgeführt: `dotnet test --filter Category=Unit` läuft in **1 Sekunde** durch.
- [x] **8.2 Performance-Messung vergleichen**
  - **TRX-Logging & Diagnostics:** `.runsettings` schreibt automatisch TRX-Profile nach `TestResults/` (in `.gitignore`).
  - **xUnit Diagnostic Warnings:** `longRunningTestSeconds: 3` in `xunit.runner.json` diagnostiziert langsame Tests direkt in der Konsole.
  - **Kategorien-Filter:** Schnelle Unit-Iteration für Coder/Agenten in **1s** statt Volllauf.
- [x] **8.3 Git Commits**
  - Strukturierte Commits durchgeführt.
