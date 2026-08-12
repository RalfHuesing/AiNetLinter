---
task: speedup-tests
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-12
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
| TD-001 | `src/AiNetLinter.Tests/Mcp/McpServerCommandJsonRpcFramingTests.cs` | mittel | nein | Zwei Tests flaky unter Volllast des vollen `Category!=Stress`-Parallel-Laufs (stdout-Framing gegen echten MCP-Subprozess), isoliert stabil grün. |
| TD-002 | `src/AiNetLinter.Tests/Cli/FilterCliIntegrationTests.cs` | mittel | nein | Selbstlint-Skeleton-Map-Check `ExcludeProjects=["*.Tests"]` schließt nur `AiNetLinter.Tests` aus, nicht `AiNetLinter.FastTests`/`AiNetLinter.IntegrationTests` — jeder zusammenhängende `"AiNetLinter.Tests"`-String-Literal in einem der drei neuen Projekte kann die Tests erneut kippen. |
| TD-003 | `.agents/rules/AiNetLinter.mdc` | mittel | ja | „Projekt-Overrides"-Abschnitt zeigt noch den seit step-001 veralteten Override-Schlüssel `*.Tests` statt `*Tests` und nennt keine separate `AiNetLinter.TestKit`-Zeile — Datei nicht neu synchronisiert nach der `rules.json`-Änderung in step-001. |
| TD-004 | `src/AiNetLinter.IntegrationTests/Platform/MsBuildFixtureHostTests.cs:14` | niedrig | ja | XML-Doc-Kommentar referenziert „step-006" — verstößt gegen das Verbot von Task-/Planungsartefakt-Referenzen in Code-Kommentaren (`AiNetLinterRichtlinien.mdc` §5). |
| TD-005 | `src/AiNetLinter.TestKit/RoslynTestSolutionFactory.cs` (`CoreReferences`) | hoch | nein | `CoreReferences` wird einmalig aus dem Testhost-`AppDomain` gebaut und enthält dadurch für **jedes** In-Memory-Projekt (unabhängig vom tatsächlichen Status) die xunit-Assemblies des Hosts — `TestProjectDetector.IsTestProject` erkennt jedes In-Memory-Produktionsprojekt fälschlich als Testprojekt; verfälscht potenziell die EPIC-4-Filtermatrix-Migration. |

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
- **Status:** offen

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
- **Status:** offen

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
- **Status:** offen

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
- **Status:** offen

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
- **Status:** offen
