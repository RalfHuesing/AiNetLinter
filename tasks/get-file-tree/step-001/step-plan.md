---
status: open
type: step-plan
task: get-file-tree
step: 001
corrects: null
title: "Filesystem-only Dispatch und boundary-sicherer Root-Resolver"
epic: EPIC-01
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: GPT-5 (Codex)
created_by_model_knowledge_cutoff: nicht im Systemkontext angegeben
created_at: 2026-08-26T22:09:09+02:00
related_to: []
---

# Step 001: Filesystem-only Dispatch und boundary-sicherer Root-Resolver

## Bezug

- **Task:** `get-file-tree`
- **Epic:** `EPIC-01` aus `roadmap.md` — den projektgebundenen Dispatch um einen
  eng begrenzten physischen Pfad ergänzen und relative Scanroots sicher unterhalb
  des registrierten `projectRoot` auflösen.
- **Konzept-Referenz:** `Konzept.md` — „MCP- und Projektvertrag“,
  „Pfadnormalisierung und Sicherheitsgrenze“ sowie „Sicherheitskonzept / Root-Grenze“.

## Aktueller Projektzustand (JIT-Kontext)

- Dies ist der erste Step: `task-state.md` weist `total_steps: 0` und
  `current_step: null` aus; es existieren noch keine Step-Artefakte und kein
  `tech-debt.md`.
- `ProjectToolCall.ExecuteAsync` in
  `src/AiNetLinter/Mcp/Projects/ProjectToolCall.cs:18-49` verbindet heute den
  zentralen absoluten Root-Guard, `ProjectRegistry.Lease`, den Loading-/LoadFailed-
  Vertrag und den `[WARN]`-Header für degradierte Roslyn-Antworten. Der MCP-
  Symbolgraph weist 37 direkte Aufrufstellen nach.
- `GuardRequiredAbsoluteRoot` in derselben Datei (`:54-73`) wird auch von der
  Overview-Resource und `get_server_health` verwendet. Der bestehende Guard muss
  deshalb gemeinsam wiederverwendet werden; seine Fehlercodes und Antworten dürfen
  sich nicht ändern.
- `ProjectRegistry.Lease` in
  `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs:60-71` normalisiert den Registry-
  Key, lädt über `ProjectDefinitionLoader` die gebundene Definition und liefert
  eine `ProjectLease`. `ProjectLease.RootPath` ist bereits der kanonische Root für
  einen nachgelagerten physischen Consumer. Eine zweite Registry- oder freie
  Root-API ist nicht erforderlich.
- Der bestehende `ProjectDefinitionLoader` verlangt weiterhin
  `ainetlinter.project.json` sowie vorhandene `solution`- und `rules`-Ziele. Der
  filesystem-only Dispatch darf nur den Roslyn-Ladezustand entkoppeln, nicht diese
  Projektbindung umgehen.
- `PathNormalizer.ToRelative` in `src/AiNetLinter/Output/PathNormalizer.cs:10-28`
  ist eine Ausgabehilfe mit einem nicht boundary-sicheren Präfixvergleich. Er wird
  nicht zum Security-Guard umfunktioniert; der neue Resolver verwendet stattdessen
  kanonische relative Pfadberechnung plus explizite Grenzprüfung.
- Der vorhandene `FileSystemExclusionHelpers`-Walk und
  `FileFilterEvaluator.MatchesGlobForWeb` sind relevante Wiederverwendungsstellen,
  gehören aber zur Walk-/Filter-Grundlage von `EPIC-02` und werden in diesem Step
  nicht erweitert.
- Der `find_duplicates`-Audit fand keinen relevanten Produktions-Exact-Cluster.
  Der Near-Cluster der beiden `ProjectRegistryTests` sowie die strukturellen
  Test-Helper-Kandidaten sind unterschiedliche Test-/Lifecycle-Setups und begründen
  keine neue Produktionsabstraktion.

## Intention

Der Step schafft die interne, projektgebundene Nahtstelle, über die ein späteres
physisches Tool seinen Scan unabhängig von `ServerLoadState.Loading` oder
`LoadFailed` ausführen kann, ohne den bestehenden Roslyn-Dispatch zu verändern.
Zusätzlich wird die relative `root`-Auflösung als nicht-werfender, boundary-sicherer
Resolver isoliert, damit spätere Scanner keine eigene Präfix- oder Traversalprüfung
implementieren.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/Projects/ProjectToolCall.cs` (Zeile 16-139)

- **Was:** Einen internen `ExecuteFilesystemAsync`-Dispatch mit der bestehenden
  `ProjectRegistry`-/`ProjectLease`-Bindung ergänzen. Er soll denselben
  `GuardRequiredAbsoluteRoot`-Pfad und dieselbe Registry-Fehlerübersetzung wie
  `ExecuteAsync` verwenden, den Lease bis zum Ende des Callbacks halten und den
  Callback mit der kanonischen `ProjectLease` aufrufen.
- **Was:** Für diesen ausschließlich physischen Pfad `ServerLoadState` nicht
  auswerten: der Callback wird sowohl bei `Loading` als auch bei `LoadFailed`
  erreicht. Der Dispatch darf deshalb weder `Loading()` noch
  `LoadFailedResult()`/`MarkLoadFailedResponseEmitted()` noch den Roslyn-
  Degraded-Header anwenden.
- **Warum:** Physische Enumeration braucht keine Roslyn-Solution, bleibt aber an
  den registrierten Projektroot und die Definition gebunden. Die bestehende
  `ExecuteAsync`-Semantik aller Roslyn-Tools bleibt unverändert; gemeinsame Guard-
  und Lease-Fehlerlogik darf dafür intern extrahiert werden, aber keine bestehende
  Signatur oder Antwort ändern.

### Datei 2: `src/AiNetLinter/Mcp/Tools/FileStructure/FileTreePathResolver.cs` (neu)

- **Was:** Einen internen Resolver für `projectRoot` plus relativen `root` anlegen.
  Null/leerer Root wird als `.` behandelt; absolute Roots werden abgelehnt. Der
  Resolver bildet den Kandidaten mit `Path.GetFullPath`/`Path.Combine`, berechnet
  anschließend `Path.GetRelativePath` und akzeptiert nur `.` oder einen relativen
  Pfad ohne führendes `..` bzw. `../` und ohne absoluten Rückgabepfad.
- **Was:** Das Ergebnis als kleines unveränderliches internes Result-/Record-Modell
  mit Erfolgspfad oder validierbarer Fehlermeldung ausgeben; keine rohe Exception
  als erwartbaren Eingabefehler verwenden. Für die Ausgabe-/Vergleichsform
  `PathNormalizer.NormalizeSeparators` wiederverwenden, aber nicht
  `PathNormalizer.ToRelative` als Sicherheitsprüfung einsetzen.
- **Was:** Die kanonische Grenzprüfung muss auch einen Root-Präfix-Sibling wie
  `C:/repo-sibling` gegenüber `C:/repo` abweisen. Existenz-, Directory-,
  Reparse-Point- und Walk-Prüfungen bleiben ausdrücklich außerhalb dieses
  lexicalen Resolvers und werden in den Folge-Epics am tatsächlichen Walk
  abgesichert.
- **Warum:** Spätere `get_file_tree`-Validierung und Scanner erhalten einen
  wiederverwendbaren, nicht aus dem MCP-Argument frei zugänglichen effektiven Root
  und duplizieren keine unsichere `StartsWith`-Logik.

### Datei 3: `src/AiNetLinter.FastTests/Mcp/WiringContractTests.cs` (Zeile 123-260)

- **Was:** Vertragstests für den neuen Dispatch ergänzen:
  `FilesystemDispatch_MissingOrRelativeProjectRoot_ReturnsExistingGuardWithoutLease`,
  `FilesystemDispatch_InvokesCallbackWhileServerIsLoading`,
  `FilesystemDispatch_InvokesCallbackAfterLoadFailure` und
  `FilesystemDispatch_HoldsLeaseUntilCallbackCompletes`.
- **Was:** Mit den vorhandenen `ProjectRegistryFixture`-/`OverviewTestServers`-
  Mustern prüfen, dass der Callback nicht durch Loading/LoadFailed ersetzt wird,
  `lease.RootPath` kanonisch ist, der Lease während des Callbacks busy bleibt und
  Registry-/Root-Fehler weiterhin im bestehenden Fehlervertrag erscheinen.
- **Warum:** Der zentrale Dispatch wird von vielen Roslyn-Tools genutzt; die
  Tests frieren deshalb sowohl die neue Ausnahme als auch die unveränderten
  bestehenden Guards und Lease-Lifetime-Annahmen ein.

### Datei 4: `src/AiNetLinter.FastTests/Mcp/Tools/FileStructure/FileTreePathResolverTests.cs` (neu)

- **Was:** Unit-Tests mit `TestTempDirectory` und deterministischen Pfaden für
  Default-Root `.`, verschachtelte relative Roots, Forward-/Backslash-Formen,
  absolute `root`-Werte, Ausbruch über `..` und Root-Präfix-Sibling-Fälle ergänzen.
- **Was:** Erfolgsfälle müssen den kanonischen absoluten Effektivpfad liefern;
  abgewiesene Fälle müssen ohne Dateisystem-Walk einen expliziten
  `INVALID_ARGUMENT`-tauglichen Grund liefern.
- **Warum:** Die Boundary-Prüfung ist sicherheitsrelevant und soll unabhängig von
  späterer Enumeration, Dateirechten oder Roslyn-Loading regressionsfest bleiben.

## Tests

- [ ] `FilesystemDispatch_MissingOrRelativeProjectRoot_ReturnsExistingGuardWithoutLease`
      deckt fehlenden, leeren/whitespace-only und relativen `projectRoot` ab.
- [ ] `FilesystemDispatch_InvokesCallbackWhileServerIsLoading` und
      `FilesystemDispatch_InvokesCallbackAfterLoadFailure` beweisen den
      filesystem-only Load-State-Vertrag ohne `[INFO]`-/`PROJECT_LOAD_FAILED`-
      Kurzschluss.
- [ ] `FilesystemDispatch_HoldsLeaseUntilCallbackCompletes` beweist die
      unveränderte Lease-Lifetime gegenüber Eviction.
- [ ] `FileTreePathResolverTests` decken `.`/verschachtelte Roots sowie absolute,
      ausbrechende und Root-Präfix-Sibling-Pfade ab.
- [ ] Bestehende `WiringContractTests` für Loading, LoadFailed, Degraded-Header,
      Toolbestand und projectRoot-Pflicht bleiben unverändert grün.
- [ ] Kein neuer Integration-/Dogfood-/MCP-Inventory-Test in diesem Step: Es wird
      noch kein `get_file_tree` registriert; diese sichtbaren Toolverträge gehören
      zu `EPIC-04`.

## Definition of Done

- [ ] `ExecuteFilesystemAsync` ist intern projektgebunden, lease-sicher und
      entkoppelt ausschließlich den Roslyn-Load-State; bestehende
      `ExecuteAsync`-Roslyn-Verträge bleiben unverändert.
- [ ] Der Root-Resolver akzeptiert nur kanonische Pfade innerhalb des absoluten
      `projectRoot` und liefert erwartbare Eingabefehler ohne rohe Exception.
- [ ] Es gibt keine zweite Registry-Key-/Root-Browser-API und keine neue
      Ausschluss-/Glob-/Walk-Implementierung in diesem Step.
- [ ] Alle in „Konkrete Änderungen“ genannten Tests sind implementiert und grün.
- [ ] `dotnet build` aus der Tech-Stack-Notiz in `roadmap.md` ist grün.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` ist grün.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` ist
      grün.
- [ ] Der Coder schreibt `step-001/step-result.md` und setzt den Status auf
      `done (pending audit)`; der spätere Commit erfolgt auf dem aktuellen Branch.

## Rules-Refs

- `.agents/rules/AiNetLinter-McpWorkflow.mdc#Verbindliche Priorität` und `#Standard`
  — semantische C#-Fragen zuerst über den AiNetLinter-MCP mit absolutem
  `projectRoot`; Textwerkzeuge bleiben ergänzend.
- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#agent-resilience` — nullable C#,
  kurze Methoden, Records für Result-Modelle und keine stillen Fehlerpfade.
- `.agents/rules/AiNetLinterRichtlinien.mdc#2 Architektur-Verbote` — keine freie
  Root-Freigabe, kein DI-/Reflection-Overhead und keine repo-spezifischen
  Hardcodings.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3 Windows-Umgebung & Tool-Regeln` —
  Windows-kompatible Pfadsemantik und MCP-first-Werkzeugwahl.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` sowie
  `#5 Qualitätsdrift-Prävention` — xUnit-v3-/`TestTempDirectory`-Vorgaben,
  Zero-Warning und DRY-/Regressionserwartung.

## Bekannte Ausnahmen

- Keine bekannten flaky Tests oder bewusst ignorierten Findings.
- Die bestehende `PathNormalizer.ToRelative`-Präfixschwäche wird nicht in diesem
  Step repariert; sie ist keine geeignete Boundary-API und bleibt deshalb bewusst
  unberührt.

## Notes

- `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs` bleibt produktiv unverändert;
  `Lease`, `Canonicalize`, `ProjectDefinitionLoader` und `ProjectLease.RootPath`
  sind die vorhandenen Bindungspunkte.
- `FileSystemExclusionHelpers`, `TreeWalkStats`, Glob-Kern, Cancellation,
  Reparse-Point-Walk, Scanresult und `get_file_tree`-Registrierung sind nicht Teil
  dieses Steps. Sie folgen erst, wenn der EPIC-01-Vertrag steht.
- Es ist keine Änderung an `roadmap.md` erforderlich: Es gibt keinen vorherigen
  Step-Abgleich, und `EPIC-01` bleibt nach diesem internen Vertrags-Step offen.
