---
task: speedup-tests
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-13
---

# Tech-Debt-Log: speedup-tests

Append-only. Jeder Eintrag ist eine vom Kritiker während eines
Step-Reviews beobachtete, aber bewusst **nicht** gefixte Auffälligkeit
außerhalb des Scopes des jeweiligen Steps (Architektur, Anti-Pattern,
Duplikation, Konsistenz) — siehe `../spec.md` §8.3/§9.

**Priorität ist reine Sortierhilfe für den Menschen, kein Auslöser.**
Bewusst `hoch`/`mittel`/`niedrig` (deutsch) statt `CRITICAL`/`MAJOR`/
`MINOR`, um jede Verwechslung mit den blockierenden Findings in
`step-review.md` auszuschließen — kein Eintrag hier führt automatisch zu
einem eigenen Korrektur-Step oder einem neuen Epic. Das entscheidet
grundsätzlich der Nutzer.

**`auto_fixable` (`ja`/`nein`) ist die einzige Ausnahme:** rein
mechanische, entscheidungsfreie Fixes ohne Architektur-Ermessen dürfen
vom Planer opportunistisch an einen ohnehin laufenden Step angehängt
werden. Default bei Unsicherheit ist `nein`.

## Index

| ID | Bereich / Datei | Priorität | Auto-Fixable | Kurzfassung |
|---|---|---|---|---|
| TD-001 | `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandJsonRpcFramingTests.cs` | mittel | nein | Framing-Wiederholung und PID-Nachweis sind in step-026 grün geschlossen. |
| TD-002 | `src/AiNetLinter.Tests/Cli/FilterCliIntegrationTests.cs` | mittel | nein | Durch die Migration nach `SkeletonMapFilterTests` gegen `FilterMini` geschlossen. |
| TD-003 | `.agents/rules/AiNetLinter.mdc` | mittel | ja | Generator-Synchronisation stellt `*Tests` und den separaten `AiNetLinter.TestKit`-Override wieder her (geschlossen in step-023). |
| TD-004 | `src/AiNetLinter.IntegrationTests/Platform/MsBuildFixtureHostTests.cs:14` | niedrig | ja | XML-Doc-Kommentar bereinigt. |
| TD-005 | `src/AiNetLinter.TestKit/RoslynTestSolutionFactory.cs` (`CoreReferences`) | hoch | nein | Durch deterministische testframework-freie BCL-Core-Referenzen geschlossen. |
| TD-006 | `src/AiNetLinter.TestKit/TestCategoryTraitInspector.cs` | niedrig | nein | Vollstaendige Trait-Validierungslogik im TestKit konsolidiert (geschlossen in step-027). |
| TD-007 | `src/AiNetLinter.FastTests/Maps/Skeleton` / `src/AiNetLinter.IntegrationTests/Maps/Skeleton` | niedrig | nein | Zwei lokale identische `CreateConfig`-Helfer fuer Skeleton-Tests. |
| TD-008 | `src/AiNetLinter.FastTests` / `src/AiNetLinter.IntegrationTests` | mittel | nein | Durch Löschung des Legacy-Projekts geschlossen (step-029 Paket 3). |
| TD-009 | `src/AiNetLinter.IntegrationTests/Mcp/Tools` / `Platform` | niedrig | nein | Durch `LoadedFixture` mit mehreren realen Konsumenten geschlossen. |
| TD-010 | `src/AiNetLinter.TestKit/IsolatedFixtureLease.cs` | mittel | nein | Durch Löschung von `FixtureWorkspaceBase` mit Legacy-Projekt geschlossen (step-029 Paket 3). |
| TD-011 | `src/AiNetLinter.IntegrationTests/Platform/SolutionRootLocator.cs` | niedrig | nein | Gemeinsame Root-Aufloesung fuer LoadedFixture und ihren Callsite-Guard (geschlossen in step-024). |

## Einträge

### TD-001 — Flaky Framing-Tests unter Volllast [Priorität: mittel] [Auto-Fixable: nein]

- **Gefunden in:** step-002 (Kritiker-Review vom 2026-08-12), ursprünglich vom Coder während der
  Baseline-Messung entdeckt und in `baseline-measurement.md` Abschnitt „Ausreißer/Fremdlast-Hinweis"
  dokumentiert.
- **Ort:** `src/AiNetLinter.Tests/Mcp/McpServerCommandJsonRpcFramingTests.cs`
  (`HandshakeOnly_AllStdoutLinesAreValidJsonRpcFrames`,
  `Initialize_ResponseInstructionsField_ContainsServerInstructionsDoctrine`).
- **Befund:** Beide Tests schlagen nur unter der Prozess-/Subprozess-Last eines vollen
  `Category!=Stress`-Parallel-Laufs fehl (1 von 3 Baseline-Läufen betroffen); isoliert
  (`FullyQualifiedName~McpServerCommandJsonRpcFramingTests`) laufen sie sofort grün. Vermutlich
  stdout-Framing-Empfindlichkeit gegen einen echten `AiNetLinter.exe`-MCP-Subprozess unter
  Parallel-Last (vgl. `AGENTS.md` §2).
- **Warum nicht sofort gefixt:** Bereits vor step-002 bestehend, nicht durch diesen Step verursacht;
  Ursachenanalyse/Fix eines last-abhängigen Subprozess-Framing-Tests ist ein eigenständiges,
  investigatives Thema außerhalb des Fundament-Scopes.
- **Vorschlag:** Bei der Migration der MCP-Framing-Kohorte (EPIC-6) gezielt untersuchen — evtl.
  gehört der Test in `konzept.md` Leitplanke 5 „exklusive Hosts" oder braucht ein höheres
  Start-/Read-Timeout unter Last.
- **Auto-Fixable:** nein — Ursache unklar, braucht Untersuchung/Ermessen, keine mechanische Korrektur.
- **Status:** geschlossen in step-026 — drei Framing-Läufe mit je 3/3 grün; PID-Snapshots vor/nach dem engen Integration-Gate zeigten keine neue zugehörige Prozesskette.

### TD-002 — Selbstlint-Testglob deckt neue Testprojekte nicht ab [Priorität: mittel] [Auto-Fixable: nein]

- **Gefunden in:** step-002 (Kritiker-Review vom 2026-08-12), vom Coder selbst während der Umsetzung
  entdeckt und in `step-result.md` Abschnitt „Beobachtungen" dokumentiert; Ursache liegt in
  step-001 (Projektanlage), nicht in step-002.
- **Ort:** `src/AiNetLinter.Tests/Cli/FilterCliIntegrationTests.cs`
  (`SkeletonMap_ExcludeProjectByGlob_OutputExcludesTests`,
  `SkeletonMap_ExcludeNamespaceGlob_ExcludesAllTestNamespaces`) — Ursache:
  `ExcludeProjects = new[] { "*.Tests" }` matcht per `ProjectConfigResolver`-Regex nur
  `^.*\.Tests$`, trifft also `AiNetLinter.Tests`, aber weder `AiNetLinter.FastTests` noch
  `AiNetLinter.IntegrationTests`.
- **Befund:** Enthält eine Datei in `AiNetLinter.FastTests`/`AiNetLinter.IntegrationTests`/
  `AiNetLinter.TestKit` einen zusammenhängenden String `"AiNetLinter.Tests"` (Kommentar, Doku,
  Literal), taucht er im Skeleton-Map-Output auf und lässt die beiden oben genannten Legacy-Tests
  fehlschlagen, obwohl das neue Projekt fachlich korrekt eingebunden ist. In step-002 bereits
  einmal aufgetreten (im eigenen neuen Code, dort per Literal-Split behoben); in step-001 bereits
  latent vorhanden (`src/AiNetLinter.FastTests/Configuration/ProjectOverrideResolutionTests.cs:12`,
  XML-Doc-Kommentar), dort bisher folgenlos.
- **Warum nicht sofort gefixt:** Root Cause liegt im über step-002 hinausgehenden Testglob von
  step-001; eine Korrektur würde die Absicht der beiden betroffenen Legacy-Tests neu bewerten
  (z. B. ob der Ausschluss auf `"*Tests"`/`"AiNetLinter.*Tests"` erweitert werden soll oder ob die
  Assertions stattdessen auf Namespace-Grenzen statt String-Suche umgestellt gehören) — das ist
  Ermessen, kein mechanischer Fix.
- **Vorschlag:** Bei Gelegenheit (z. B. wenn `FilterCliIntegrationTests` selbst migriert wird,
  Leitplanke 1/EPIC-4) den Ausschluss-Glob bzw. die Assertion auf die drei neuen Projektnamen
  abstimmen.
- **Auto-Fixable:** nein — erfordert Entscheidung über die richtige Glob-/Assertion-Strategie.
- **Status:** geschlossen in step-013

### TD-003 — `AiNetLinter.mdc` seit step-001 nicht neu synchronisiert [Priorität: mittel] [Auto-Fixable: ja]

- **Gefunden in:** step-002 (Kritiker-Review vom 2026-08-12), vom Coder als lokale Drift während der
  Baseline-Messläufe beobachtet und wieder zurückgesetzt (nicht committet); verifiziert per
  `git diff`/`git status`, dass der aktuelle committete Stand von `.agents/rules/AiNetLinter.mdc`
  unverändert und sauber ist — der Fund betrifft den **Inhalt** der Datei, nicht eine unsaubere
  Arbeitskopie.
- **Ort:** `.agents/rules/AiNetLinter.mdc` Abschnitt „Projekt-Overrides" (Zeile 82-86).
- **Befund:** Zeigt noch `**\`*.Tests\`:** MaxMethodLineCount 100; EnforceSealedClasses aus`, obwohl
  `rules.json` seit step-001 den Schlüssel `"*Tests"` (ohne Punkt, deckt `AiNetLinter.Tests`,
  `AiNetLinter.FastTests`, `AiNetLinter.IntegrationTests` ab) plus einen separaten Schlüssel
  `"AiNetLinter.TestKit"` enthält (siehe `codemap.md` Zeile 40). Die auto-generierte
  Kurzfassung ist damit für Agenten, die sich an dieser Datei orientieren, sachlich veraltet.
- **Warum nicht sofort gefixt:** Außerhalb des step-002-Scopes (keine step-002-Änderung an
  `rules.json`); Ursache ist eine ausgebliebene Regenerierung nach step-001.
- **Vorschlag:** `AiNetLinter.exe --sync-agent-rules` (o. ä., siehe `SyncAgentRulesCommand`) einmal
  laufen lassen und den Diff committen.
- **Auto-Fixable:** ja — rein mechanische Regenerierung aus der bereits korrekten `rules.json`, keine
  Architektur-Entscheidung, keine Verhaltensänderung am Produktcode.
- **Status:** geschlossen in step-023 — `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only` regenerierte die Datei; der isolierte Generatorvertrag belegt beide Override-Schlüssel.

### TD-004 — Task-Artefakt-Referenz in Code-Kommentar [Priorität: niedrig] [Auto-Fixable: ja]

- **Gefunden in:** step-007 (Kritiker-Review vom 2026-08-12), beim Rules-Konformitäts-Check
  zufällig aufgefallen (nicht Teil der im Plan zitierten Rules-Refs für diesen Step, daher kein
  Ebene-2-Finding, sondern Tech-Debt).
- **Ort:** `src/AiNetLinter.IntegrationTests/Platform/MsBuildFixtureHostTests.cs:14`.
- **Befund:** Der XML-Doc-Kommentar auf `SharedSolutionIdentityWitness` lautet u. a. „...analog zum
  Referenz-Caching-Test aus step-006)" — enthält damit eine Referenz auf ein Planungsartefakt, das
  `AiNetLinterRichtlinien.mdc` §5 „Sparsamer Einsatz von Code-Kommentaren" ausdrücklich verbietet
  („Jede Referenz auf Task-/Planungsartefakte, die den Code überleben soll ... ist verboten").
  `tasks/speedup-tests/` wird nach Task-Abschluss gelöscht, der Verweis wird dann bedeutungslos.
- **Warum nicht sofort gefixt:** Außerhalb der für step-007 kuratierten Rules-Refs (nur §4
  „Testsuite-Parallelität" war zitiert, nicht §5); Kritiker fixt laut eigenem Auftrag ohnehin nicht.
- **Vorschlag:** „aus step-006" aus dem Kommentar entfernen, stattdessen ID-frei begründen, z. B.
  „...analog zum bereits vorhandenen Referenz-Caching-Test-Muster)".
- **Auto-Fixable:** ja — reine Wortlautänderung im Kommentar, keine Architektur-Entscheidung, keine
  Verhaltensänderung.
- **Status:** geschlossen in step-021

### TD-005 — `RoslynTestSolutionFactory.CoreReferences` kontaminiert jedes In-Memory-Projekt mit Testhost-Referenzen [Priorität: hoch] [Auto-Fixable: nein]

- **Gefunden in:** step-008 (Kritiker-Review vom 2026-08-12), ursprünglich vom Coder selbst während
  der Umsetzung entdeckt und in `step-008/step-result.md` Abschnitt „Abweichungen vom Plan" bzw.
  „Beobachtungen" dokumentiert; Ursache liegt im Factory-Design aus step-006, nicht in step-008.
- **Ort:** `src/AiNetLinter.TestKit/RoslynTestSolutionFactory.cs` (`CoreReferencesLazy`/
  `BuildCoreReferences()`, `AddProject`).
- **Befund:** `CoreReferences` wird einmalig statisch aus `AppDomain.CurrentDomain.GetAssemblies()`
  gebaut (Testhost-Prozess) und ungefiltert an **jedes** über `CreateSolution` gebaute Projekt
  gehängt — es gibt in `ProjectSpec` keinen Mechanismus, diese Kernreferenzen für ein einzelnes
  Projekt auszuschließen (nur `AdditionalReferences` zum Hinzufügen). Da der Testhost selbst
  `xunit`/`Microsoft.TestPlatform` referenziert, erkennt `TestProjectDetector.IsTestProject`
  (referenzbasiert, vor dem Namenssuffix-Fallback geprüft) **jedes** In-Memory-Projekt als
  Testprojekt — auch reine Produktionsprojekte wie `FilterMini`. Verifiziert in
  `FilterMiniFidelityTests.AssertTestProjectDetectionMatches`: Disk-`FilterMini` liefert korrekt
  `false`, In-Memory-`FilterMini` liefert `true`.
- **Warum nicht sofort gefixt:** Root Cause liegt im Factory-Design aus step-006
  (`RoslynTestSolutionFactory`), außerhalb des step-008-Scopes (FilterMini-Fixture); ein Fix erfordert
  eine Architektur-Entscheidung (z. B. `ProjectSpec` um eine Möglichkeit erweitern, den globalen
  Testhost-Referenzsatz für ein Projekt zu filtern/auszuschließen, oder `TestProjectDetector` mit
  einem expliziten `testHostReferencesAreNoise`-Parameter für In-Memory-Aufrufer versehen) — kein
  mechanischer Fix.
- **Vorschlag:** Vor der EPIC-4-Filtermatrix-Migration klären, ob dort `IsTestProject`-Verhalten
  gegen In-Memory-Solutions geprüft wird; falls ja, `RoslynTestSolutionFactory`/`ProjectSpec` um eine
  projektspezifische Filtermöglichkeit für `CoreReferences` erweitern (oder `TestProjectDetector`
  bewusst mit explizitem `testProjectNameSuffixes`-Parameter statt Default-Referenzheuristik
  aufrufen), bevor die Migration reale Filtermatrix-Assertions auf diesem Verhalten aufbaut.
- **Auto-Fixable:** nein — Architektur-Entscheidung über Filter-/Ausschluss-Mechanismus nötig.
- **Status:** geschlossen in step-013

### TD-006 — Kategorie-Trait-Auslesung zentralisieren [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-019 (Kritiker-Review vom 2026-08-13).
- **Ort:** `src/AiNetLinter.FastTests/Architecture/TestCategoryProfileGuardTests.cs` und `src/AiNetLinter.IntegrationTests/Architecture/TestCategoryProfileGuardTests.cs`.
- **Befund:** Beide Assembly-Guards reflektieren `TraitAttribute` und extrahieren die Kategorie mit derselben Implementierung.
- **Warum nicht sofort gefixt:** Eine gemeinsame Hilfsschicht zwischen den Testassemblies braucht eine Abhaengigkeits- und Sichtbarkeitsentscheidung; sie liegt ausserhalb des Find-Symbol-Schnitts.
- **Vorschlag:** Bei einer gemeinsamen Guard-Weiterentwicklung einen testframeworkfreien Helper im TestKit bewerten.
- **Auto-Fixable:** nein — Zielort und Assembly-Abhaengigkeit erfordern Architektur-Ermessen.
- **Status:** geschlossen in step-027 — vollstaendige Kategoriepruefung liegt im TestKit; beide Assemblyguards sind Ein-Zeilen-Konsumenten, exact-Duplikatcluster beseitigt.

### TD-007 — Lokale Skeleton-Testkonfigurationen bewerten [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-019 (Kritiker-Review vom 2026-08-13).
- **Ort:** `src/AiNetLinter.FastTests/Maps/Skeleton/SkeletonMapFilterTests.cs` und `src/AiNetLinter.IntegrationTests/Maps/Skeleton/SkeletonMapBuilderAdapterTests.cs`.
- **Befund:** Beide privaten `CreateConfig`-Methoden erzeugen dieselbe minimale Konfiguration.
- **Warum nicht sofort gefixt:** Die Helfer sind je Testklasse lokal; eine Extraktion wuerde fuer zwei kurze Aufrufer eine gemeinsame Testoberflaeche einfuehren und gehoert nicht zum Step.
- **Vorschlag:** Erst bei einem dritten Konsumenten eine schmale gemeinsame Testkonfiguration erwägen.
- **Auto-Fixable:** nein — die Wiederverwendungsgrenze ist eine Designentscheidung.
- **Status:** offen

### TD-008 — Parallele Fast-/Legacy-Testhelfer bis EPIC-7 verfolgen [Priorität: mittel] [Auto-Fixable: nein]

- **Gefunden in:** step-019 (Kritiker-Review vom 2026-08-13).
- **Ort:** `src/AiNetLinter.FastTests/TestHelper.cs`, `src/AiNetLinter.Tests/TestHelper.cs`, `src/AiNetLinter.FastTests/Mcp/CompileErrorHeaderAssertions.cs` und `src/AiNetLinter.Tests/Mcp/CompileErrorHeaderAssertions.cs`.
- **Befund:** Die exakten Paare `CompileErrorHeaderAssertions`, `CreateDefaultConfig`, `ParseCode`, `CreateContext`, `CreateContextWithLoadDiagnostics`, `DeleteDirectoryIfExists` und `CreateSemanticModel` bestehen in FastTests und Legacy parallel; die Compile-Error-Assertion liegt inzwischen auch lokal in `AiNetLinter.IntegrationTests/Mcp/Tools/GetIndexScopeToolTests.cs` vor.
- **Warum nicht sofort gefixt:** Die Legacy-Konsumenten bleiben bis EPIC-7 absichtlich separat und TestKit darf nicht ohne breiteren Bedarf zum Allzweckhelper werden.
- **Vorschlag:** Bei der jeweiligen Restmigration Konsumenten auf die etablierte Zielassembly umstellen und die Legacy-Kopie mit dem Projekt entfernen.
- **Auto-Fixable:** nein — die Konsumenten- und Assembly-Grenzen muessen kohortenweise entschieden werden.
- **Status:** geschlossen in step-029 (Paket 3) — Legacy-Projekt `AiNetLinter.Tests` und seine Helfer gelöscht; FastTests und IntegrationTests nutzen saubere Zielstrukturen.

### TD-009 — Integration-Fixture-Lebensdauer gemeinsam bewerten [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-019 (Kritiker-Review vom 2026-08-13).
- **Ort:** `src/AiNetLinter.IntegrationTests/Mcp/Tools/FindSymbolFileAdapterTests.cs` und `src/AiNetLinter.IntegrationTests/Platform/MsBuildFixtureHost.cs`.
- **Befund:** Beide Fixtures entsorgen `SourceFileCatalog` und `IsolatedFixtureLease` in derselben Reihenfolge; die neue Fixture benoetigt aber `SymbolGraphMini`, der bestehende Assembly-Host `BaselineMini`.
- **Warum nicht sofort gefixt:** Eine parametrisierte oder vererbte Fixture-Oberflaeche wuerde den bestehenden Assembly-Fixture-Vertrag beruehren und den Find-Symbol-Step ueber die lokale Adaptergrenze ausweiten.
- **Vorschlag:** Bei einem weiteren diskbasierten Integration-Adapter einen kleinen gemeinsamen Lifecycle-Host innerhalb von IntegrationTests evaluieren.
- **Auto-Fixable:** nein — Fixture-Instanziierung und Lebensdauer erfordern Designentscheidung.
- **Status:** geschlossen in step-021

### TD-010 — Doppelte Workspace-Kopie beim Strangler-Ende aufloesen [Priorität: mittel] [Auto-Fixable: nein]

- **Gefunden in:** step-019 (Kritiker-Review vom 2026-08-13).
- **Ort:** `src/AiNetLinter.TestKit/IsolatedFixtureLease.cs` und `src/AiNetLinter.Tests/Fixtures/FixtureWorkspaceBase.cs`.
- **Befund:** Beide Implementierungen kopieren Fixture-Baeume und schliessen `bin`/`obj` aus; die Legacy-Variante besitzt zusaetzlich ihr historisches Workspace-Basisklassen-API.
- **Warum nicht sofort gefixt:** Nach step-023 entfaellt nur `DisableAllCliTests`; weiterhin referenzieren 20 Legacy-Dateien die sechs `FixtureWorkspaceBase`-Ableitungen oder darauf aufbauende Catalog-/MCP-Fixtures. Eine vorzeitige Vereinheitlichung wuerde EPIC-6-Vertraege vorziehen.
- **Vorschlag:** Im EPIC-7-Legacy-Entfernungsstep verifizieren, dass keine Legacy-Referenz verbleibt, und die verbleibende TestKit-Primitive beibehalten.
- **Auto-Fixable:** nein — die Legacy-Kohorte bestimmt den sicheren Zeitpunkt.
- **Status:** geschlossen in step-029 (Paket 3) — `FixtureWorkspaceBase` mit dem Legacy-Projekt gelöscht, nur `IsolatedFixtureLease` verbleibt im TestKit.

### TD-011 — Lokale `FindSolutionRoot`-Duplikation der Loaded-Fixture bewerten [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-023 (EPIC-5-Drift-Audit vom 2026-08-13).
- **Ort:** `src/AiNetLinter.IntegrationTests/Platform/LoadedFixture.cs` und `src/AiNetLinter.IntegrationTests/Platform/LoadedFixtureTests.cs`.
- **Befund:** Beide privaten Methoden suchen vom Assembly-Basisverzeichnis aufwaerts nach `AiNetLinter.slnx` und sind exakt dupliziert.
- **Warum nicht sofort gefixt:** Keine der beiden Dateien gehoert zur Step-023-Config-/Suppression-Kohorte; eine gemeinsame Test-/Fixture-Grenze ist ausserhalb des Migrationsschritts zu entscheiden.
- **Vorschlag:** Bei der naechsten Bearbeitung der Loaded-Fixture eine schmale, testbare Root-Aufloesung bewerten und dann beide Aufrufer umstellen.
- **Auto-Fixable:** nein — Sichtbarkeit und Zielort der gemeinsamen Funktion brauchen Architektur-Ermessen.
- **Status:** geschlossen in step-024
