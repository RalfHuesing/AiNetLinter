---
status: open
type: step-plan
task: codegraph-mcp
step: 008
title: "get_index_scope Tool (Dateityp-Aufschlüsselung der Solution)"
epic: EPIC-04
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-07-31T23:45:00Z
related_to: [step-003, step-007]
---

# Step 008: get_index_scope Tool (Dateityp-Aufschlüsselung der Solution)

## Bezug

- **Task:** `codegraph-mcp`
- **Epic:** `EPIC-04` aus `roadmap.md` — erster der vier Struktur-/
  Qualitäts-Tools (`get_index_scope`, `get_hotspots`, `get_violations`,
  `search_pattern`). EPIC-03 (alle fünf Symbolgraph-Tools) ist mit
  `step-007/fix-01` (`approved`) vollständig abgeschlossen (siehe
  `roadmap.md`-Update in diesem Step-Modus-Aufruf).
- **Konzept-Referenz:** `konzept.md` Tool-Tabelle Zeile `get_index_scope
  | keins | Dateityp-Aufschlüsselung der Solution: .cs (voll vom Graph
  abgedeckt) vs. .js/.razor/.xaml/.html/.css (nicht abgedeckt, jeweils
  mit Anzahl) — Orientierung, bevor der Agent überhaupt sucht |
  SourceFileCatalog.GetSourceFiles/WebFileCatalog.Collect`; Abschnitt
  „Entdeckte Mängel/Redundanzen" → „`get_index_scope` braucht keinen neuen
  Datei-Scan" (Zeile 324-333); Muss-Haben „Explizite Scope-Kommunikation"
  (dieses Tool ist der zentrale Orientierungspunkt dafür, bevor der Agent
  überhaupt ein anderes Tool aufruft).

## Aktueller Projektzustand (JIT-Kontext)

- **Registrar-Struktur aus step-007 bereits vorbereitet:**
  `McpServerOptionsFactory.BuildToolCollection`
  (`src/AiNetLinter/Mcp/McpServerOptionsFactory.cs:38`) ruft nur noch
  `SymbolGraphToolRegistrations.Register`/`FileStructureToolRegistrations.Register`
  auf. `FileStructureToolRegistrations.cs` trägt aktuell nur
  `get_file_skeleton` und ist laut eigenem Klassenkommentar
  ("Vorbereitet fuer kommende EPIC-04-Tools") explizit als Ziel für
  `get_index_scope` vorgesehen — passt fachlich (dateistruktur-
  orientiertes Tool, kein Symbolgraph-Zugriff).
- **Footprint-Lage (TD-004/TD-005, `tech-debt.md`):**
  `FileStructureToolRegistrations` liegt laut step-007-Messung bei
  2422/2500 (78 Zeilen Puffer, unabhängig vom Kritiker bestätigt). Ein
  weiterer `tools.Add(...)`-Block (Trend aus TD-004-Historie: ~11-15
  Zeilen pro Eintrag) passt damit klar hinein, ohne das Limit zu
  gefährden — trotzdem Pflicht-Selbst-Lint in der DoD (wie bei jedem
  bisherigen Tool-Step), da die vier EPIC-04-Tools laut TD-004-Update aus
  step-007 zusammen nur noch "gerade eben" in den Puffer passen sollen.
- **Wiederverwendbare Bausteine teilweise vorhanden, aber Lücke
  entdeckt — das relativiert die konzept.md-Aussage „kein neuer
  Datei-Scan nötig":**
  - `.cs`-Zählung: `SourceFileCatalog.IsValidDocument(Document, string?)`
    (`src/AiNetLinter/Baseline/SourceFileCatalog.cs:145`, bereits
    `internal static`, von `McpCodeGraphServer` selbst genutzt) liefert
    exakt das Prädikat, mit dem `find_symbol`/`get_file_skeleton` intern
    bereits arbeiten — direkt wiederverwendbar, kein neuer Scan nötig.
  - `.js`/`.razor`/`.css`-Zählung: `WebFileCatalog.Collect(Solution,
    string, WebFileDiscoveryRequest)`
    (`src/AiNetLinter/Web/WebFileCatalog.cs:50`) deckt genau diese drei
    Typen ab (inkl. `.razor.css` als `Css`-Typ) und läuft bereits ohne
    zweites MSBuild-Laden über die Solution — **kann ohne vollen
    `Config`-Kontext aufgerufen werden**: `WebFileDiscoveryRequest`
    braucht nur `FileFiltersConfig` (Default-Konstruktor liefert bereits
    `obj/`/`bin/`-Ausschluss, siehe `Config.ValueTypes.cs:6-25`) und zwei
    Exempt-Pfad-Listen — ein leeres/`null` Exempt-Set ist für diesen Tool
    passend (Scope-Übersicht will *alle* Web-Dateien zählen, nicht die
    durch Web-Checker-Konfiguration ausgenommenen). Kein `Config`-Objekt
    des MCP-Servers nötig, `McpCodeGraphServer` muss dafür **nicht**
    erweitert werden.
  - **Lücke:** `.xaml`/`.html` (laut `konzept.md`-Tabelle explizit Teil
    der Aufschlüsselung, siehe auch die C#-only-Scope-Hinweise in jeder
    bisherigen Tool-`description`, z. B. `GetFileSkeletonTool.cs:18`)
    werden von **keiner** bestehenden Struktur erfasst — verifiziert per
    Grep: `WebFileType` (`src/AiNetLinter/Web/WebFileCatalog.cs:21-26`)
    kennt nur `Css`/`Js`/`Razor`, keine Stelle im Produktionscode
    verarbeitet `.xaml`/`.html` (`WpfSeparationChecker.cs` prüft laut
    `konzept.md` "Wo im Projekt" nur C#-Code-Behind, nie die `.xaml`-Datei
    selbst). Dieser Step führt also für genau diese zwei Dateitypen einen
    **neuen**, aber bewusst minimalen Scan ein — kein Widerspruch zur
    Wiederverwendungs-Absicht aus `konzept.md`, da drei von fünf Typen
    tatsächlich ohne neuen Scan auskommen; die Konzept-Notiz war für
    diese zwei Typen zu optimistisch, das wird hier korrigiert statt
    stillschweigend übernommen.
  - `WebFileCatalog.GetProjectDirectories(Solution)`
    (`src/AiNetLinter/Web/WebFileCatalog.cs:71`, aktuell `private`)
    liefert genau die Verzeichnis-Basis, die auch der neue `.xaml`/
    `.html`-Scan braucht (ein Projektverzeichnis pro `Project` mit
    `FilePath`, dedupliziert) — **wird auf `internal` angehoben**, damit
    `GetIndexScopeTool` sie direkt wiederverwendet statt eine zweite,
    eigene Projektverzeichnis-Enumeration zu bauen. Reine
    Sichtbarkeitsänderung, keine Verhaltensänderung an
    `WebFileCatalog.Collect` selbst.
- **`McpCodeGraphServer.GetCurrentSolution()`** liefert nur eine
  `Solution?`, kein `Config`/`LinterArgs` — dieser Step führt **keine**
  Erweiterung von `McpCodeGraphServer` ein, da weder die `.cs`- noch die
  Web-Zählung ein `Config`-Objekt braucht (siehe oben). Bewusst so
  gehalten, um den bestehenden Server-Zustand nicht für ein einzelnes
  Tool aufzublähen.
- **Muster für parameterlose Tools:** `find_symbol`/`find_references`/
  `get_impact`/`get_type_hierarchy`/`get_file_skeleton` haben alle
  mindestens einen Pflichtparameter — `get_index_scope` ist das erste
  Tool ganz ohne fachliche Parameter (nur `CancellationToken ct =
  default`, analog zum bereits bestehenden Muster in
  `McpServerTool.Create`-Lambdas).

## Intention

Erstes EPIC-04-Tool: `get_index_scope` liefert eine Dateityp-Aufschlüsselung
der resident gehaltenen Solution (`.cs` voll vom Graph abgedeckt; `.js`/
`.razor`/`.css` über das bestehende `WebFileCatalog.Collect`; `.xaml`/
`.html` über einen neuen, minimalen Scan auf Basis der jetzt wiederverwendeten
`WebFileCatalog.GetProjectDirectories`) — der Orientierungspunkt, den ein
Agent laut `konzept.md` aufrufen soll, **bevor** er überhaupt mit
`find_symbol`/`search_pattern` zu suchen beginnt, um früh zu wissen, welche
Dateitypen der Symbolgraph abdeckt und welche nicht.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Web/WebFileCatalog.cs` (Zeile 71)

- **Was:** Sichtbarkeit von `GetProjectDirectories` von `private static` auf
  `internal static` anheben. Keine sonstige Änderung an der Methode oder an
  `WebFileCatalog.Collect`.
- **Warum:** Wiederverwendung für den neuen `.xaml`/`.html`-Scan in Datei 2
  — vermeidet eine zweite, eigene Projektverzeichnis-Enumeration
  (`Solution.Projects` → `Path.GetDirectoryName(project.FilePath)` →
  dedupliziert) an einer zweiten Stelle im Code.

### Datei 2: `src/AiNetLinter/Mcp/Tools/GetIndexScopeTool.cs` (neu)

- **Was:** `internal static class GetIndexScopeTool` mit
  `internal static CallToolResult ExecuteAsync(McpCodeGraphServer state,
  CancellationToken ct)` (synchron ausreichend — kein `await` nötig, da
  weder `WebFileCatalog.Collect` noch die `.cs`-Zählung asynchron sind;
  Signatur trotzdem `Task<CallToolResult>`-kompatibel halten, falls das
  SDK das für `McpServerTool.Create`-Delegates einheitlich erwartet —
  gegen die vier bestehenden Tool-Registrierungen in
  `SymbolGraphToolRegistrations.cs`/`FileStructureToolRegistrations.cs`
  abgleichen, ob ein synchroner Rückgabetyp dort überhaupt zulässig ist,
  sonst `Task.FromResult(...)` verwenden):
  1. `state.GetCurrentSolution()` — `null` → `McpToolResults.SolutionNotLoaded()`.
  2. `solutionDir = Path.GetDirectoryName(solution.FilePath) ?? ""`.
  3. `.cs`-Zählung: über `solution.Projects` → `project.Documents`
     iterieren, `SourceFileCatalog.IsValidDocument(document, solutionDir)`
     filtern, zählen (kein `SkeletonMapBuilder`/Parsing nötig, reines
     Zählen reicht für diesen Tool).
  4. `.js`/`.razor`/`.css`-Zählung: `WebFileCatalog.Collect(solution,
     solutionDir, new WebFileDiscoveryRequest(new FileFiltersConfig(),
     Array.Empty<string>(), null))` aufrufen, Ergebnis nach `WebFileType`
     gruppieren und zählen (`Css` zählt bewusst auch `.razor.css`, wie
     `WebFileCatalog.GetWebFileType` das bereits klassifiziert — keine
     eigene Sonderbehandlung nötig).
  5. `.xaml`/`.html`-Zählung: neue private Hilfsmethode, die
     `WebFileCatalog.GetProjectDirectories(solution)` (Datei 1) durchläuft,
     pro Verzeichnis `Directory.EnumerateFiles(dir, "*",
     SearchOption.AllDirectories)` aufruft (mit try/catch analog zu
     `WebFileCatalog.SafeEnumerateFiles`, `UnauthorizedAccessException`/
     `IOException` abfangen statt zu crashen — konsistent mit dem
     bestehenden Muster in `WebFileCatalog.cs:99-107`), generierte Pfade
     (`obj`/`bin`/`node_modules`-Segmente, analog zu
     `WebFileCatalog.IsGeneratedPath`) ausschließt und Dateien nach
     Endung `.xaml`/`.html` zählt (`OrdinalIgnoreCase`). Bewusst **keine**
     `FileFiltersConfig`-Anbindung für diese zwei Typen (kein bestehender
     Anwendungsfall dafür, anders als bei `.css`/`.js`) — Notiz als
     bekannte Vereinfachung unten.
  6. Text formatieren: eine Zeile pro Dateityp, Format `"<Endung>: <Anzahl>
     Dateien (<Abdeckungshinweis>)"` — `.cs` mit Hinweis "voll vom
     Symbolgraph abgedeckt", alle anderen fünf mit Hinweis "nicht vom
     Symbolgraph abgedeckt" (Muss-Haben "Explizite Scope-Kommunikation").
     Reihenfolge: `.cs` zuerst, dann die übrigen fünf alphabetisch nach
     Endung.
  7. `McpToolResults.Text(text)`.
- **Warum:** Reine Zähl-/Formatierungslogik, kein neuer Fehlerpfad über
  die bestehende `SolutionNotLoaded()`-Kurzform hinaus (keine
  Datei-/Symbol-Identifikator-Eingabe, die fehlschlagen könnte — das
  Tool hat keine fachlichen Parameter).

### Datei 3: `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs`

- **Was:** Neuen `tools.Add(McpServerTool.Create(...))`-Block für
  `get_index_scope` ergänzen (kein fachlicher Parameter, nur
  `CancellationToken ct = default`, analog zur Lambda-Signatur der
  übrigen Tools). Beschreibung benennt explizit, dass dieses Tool sowohl
  die vom Graph abgedeckten als auch die nicht abgedeckten Dateitypen
  auflistet (keine "nur .cs"-Einschränkung wie bei den Symbolgraph-Tools
  — dieses Tool ist bewusst die Ausnahme, die auch über den
  nicht-abgedeckten Rest berichtet).
- **Warum:** `FileStructureToolRegistrations` ist laut Klassenkommentar
  und Footprint-Lage (siehe JIT-Kontext) das vorgesehene Ziel für die
  EPIC-04-Tools.

### Datei 4: `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/wwwroot/` (neu, additiv)

- **Was:** Kleine, zusätzliche Nicht-C#-Dateien im bestehenden
  `SymbolGraphMini`-Fixture-Projektverzeichnis anlegen, additiv zu den
  bereits vorhandenen `.cs`-Dateien (`Greeter.cs`, `Caller.cs`,
  `OtherCaller.cs`, `Hierarchy.cs`): `site.js` (1 Zeile Inhalt reicht),
  `Component.razor`, `styles.css`, `Page.xaml`, `index.html` — je genau
  eine Datei pro fehlendem Typ, damit die Aufschlüsselung in Datei 5
  jeden der sechs Zweige (inkl. `.cs`) mit einem plausiblen Wert > 0
  testen kann. Kein Bezug zur `.csproj` nötig (SDK-Projekte kompilieren
  nur `.cs` über `Compile`-Items; `WebFileCatalog`/der neue Scan arbeiten
  ohnehin direkt auf dem Dateisystem, nicht über MSBuild-Items).
- **Warum:** Wiederverwendung der bereits geladenen `SymbolGraphMini`-
  Fixture (bereits mehrfach für Tool-Tests genutzt) statt einer neuen,
  eigenen Mini-Solution nur für dieses eine Tool — konsistent mit dem
  JIT-Prinzip "bestehende Struktur wiederverwenden vor Neubau". Eine
  reichhaltigere, dedizierte "gemischter Code"-Testsolution (C#/JS/
  Razor/XAML/CSS in realistischerem Umfang) ist laut `konzept.md`
  Definition of Done ohnehin Teil von EPIC-07 — dieser Step nimmt das
  nicht vorweg, sondern deckt nur die für seine eigenen Unit-Tests nötige
  Minimalmenge ab.

### Datei 5: `src/AiNetLinter.Tests/Mcp/Tools/GetIndexScopeToolTests.cs` (neu)

- **Was:** Unit-Tests analog zum Muster der bestehenden `*ToolTests.cs`
  (siehe Testliste unten), gegen die um Datei 4 erweiterte
  `SymbolGraphMini`-Fixture.
- **Warum:** Testabdeckung für alle sechs Dateityp-Zweige plus
  Fehlerpfad (keine geladene Solution).

### Datei 6: `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs`

- **Was:** `RunAsync_ValidFixture_ServerRespondsWithFiveTools` →
  `RunAsync_ValidFixture_ServerRespondsWithSixTools`, Assertion auf 6
  Tools inkl. `get_index_scope` erweitert. Neuer E2E-Subprozess-Test
  `RunAsync_ValidFixture_GetIndexScopeReturnsFileTypeBreakdown` (Muster
  identisch zu den bestehenden E2E-Tests), ruft `get_index_scope` ohne
  Parameter gegen die erweiterte Fixture (Datei 4) auf und prüft, dass
  sowohl `.cs` als auch mindestens ein nicht-abgedeckter Typ (z. B.
  `.xaml`) mit einer Anzahl > 0 im Text vorkommen.
- **Warum:** Bestehender Tool-Zähl-Test muss das neue Tool
  widerspiegeln; E2E-Test verifiziert den vollen Subprozess-Pfad wie bei
  den fünf Vorgänger-Tools.

## Tests

- [ ] `GetIndexScopeToolTests.ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode`
- [ ] `GetIndexScopeToolTests.ExecuteAsync_MixedFixture_ReturnsCsCountMarkedAsGraphCovered`
- [ ] `GetIndexScopeToolTests.ExecuteAsync_MixedFixture_ReturnsJsRazorCssCountsViaWebFileCatalog`
- [ ] `GetIndexScopeToolTests.ExecuteAsync_MixedFixture_ReturnsXamlAndHtmlCountsMarkedAsNotGraphCovered`
- [ ] `GetIndexScopeToolTests.ExecuteAsync_GeneratedObjBinDirectories_ExcludedFromXamlHtmlCount`
      (verifiziert, dass der neue Scan dieselbe `obj`/`bin`-Ausschlusslogik
      wie `WebFileCatalog` anwendet — Regressionsschutz gegen einen
      späteren, aus Versehen zu breiten Scan)
- [ ] `McpServerCommandTests.RunAsync_ValidFixture_ServerRespondsWithSixTools` (umbenannt/erweitert)
- [ ] `McpServerCommandTests.RunAsync_ValidFixture_GetIndexScopeReturnsFileTypeBreakdown` (neu)

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt (Dateien 1-6)
- [ ] `dotnet build AiNetLinter.slnx` grün, 0 Warnungen
- [ ] `dotnet test AiNetLinter.slnx` grün
- [ ] `ainetlinter --config rules.json --path ./src/` → 0 Violations
- [ ] Selbst-Lint-Footprint-Kontrolle (Pflicht wegen TD-004/TD-005):
      `--footprint FileStructureToolRegistrations`,
      `--footprint GetIndexScopeTool` — beide < 2500 dokumentiert in
      `step-result.md`. Reißt eine der beiden Klassen das Limit: Formatier-
      /Scan-Logik in eine separate, `McpCodeGraphServer`-unabhängige Datei
      auslagern (Muster aus `GetTypeHierarchyFormatter`/
      `SymbolIdentifierResolver`), dokumentiert als Abweichung.
- [ ] Commit auf aktuellem Branch (Conventional Commit,
      `feat(mcp): add get_index_scope tool [codegraph-mcp]` o. ä.)
- [ ] `step-008/step-result.md` geschrieben, inkl. Abschnitt „Dogfooding"
      (Pflicht laut `roadmap.md` EPIC-04-Notiz) — Tool gegen die reale
      `AiNetLinter.slnx` aufrufen. **Hinweis für den Coder:** ein Grep
      über `src/` (siehe JIT-Kontext) zeigt aktuell **keine** `.js`/
      `.razor`/`.xaml`/`.html`/`.css`-Dateien im eigenen Repo außerhalb
      von `obj`/`bin` — die reale Dogfooding-Ausgabe wird für diese fünf
      Typen plausibel `0` zeigen, nur `.cs` > 0. Das ist kein Fehlschlag
      des Tools, sondern korrekt (reines C#-Repo); der positive Nachweis
      für die fünf anderen Typen kommt aus den Unit-/E2E-Tests gegen die
      erweiterte Fixture (Datei 4-6), nicht aus dem Dogfooding-Lauf.
- [ ] `status` in `step-plan.md` von `in_progress` auf
      `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — `AIContextFootprint` (2500,
  Selbst-Lint-Pflicht siehe DoD), `#nullable enable`, statische Klassen,
  Methodenlänge, max. 4 Parameter (hier irrelevant, da `get_index_scope`
  keinen Parameter außer `CancellationToken` hat).
- `.agents/rules/AiNetLinterRichtlinien.mdc` — kein DI-Container
  (`GetIndexScopeTool` erreicht `McpCodeGraphServer` weiterhin per
  Delegate-Closure wie alle bisherigen Tools), Result-Pattern statt
  Exceptions (`SolutionNotLoaded()`-Kurzform, keine geworfene Exception
  bei fehlender Solution), Build/Test-Pflicht, Commit-Vorschlag-Pflicht.

## Bekannte Ausnahmen

- Die `.xaml`/`.html`-Zählung berücksichtigt bewusst **keine**
  `FileFiltersConfig`-`ExcludeFilePatterns`/Web-Checker-ExemptPaths
  (anders als die `.css`/`.js`-Zählung über `WebFileCatalog.Collect`) —
  es gibt für diese zwei Typen aktuell keinen Web-Checker, der solche
  Ausnahmen definieren würde; nur die generischen `obj`/`bin`/
  `node_modules`-Ausschlüsse gelten. Falls künftig ein WPF-/HTML-
  spezifischer Checker mit eigenen Exempt-Pfaden entsteht, müsste
  `get_index_scope` das nachziehen — kein Bug in diesem Step, sondern
  Konsequenz aus dem aktuellen Funktionsumfang der übrigen Checker.
- Die `.cs`-Zählung unterscheidet nicht zwischen Produktions- und
  Testprojekten (zählt beide) — passend zum Zweck des Tools
  (Solution-weite Scope-Orientierung, nicht produktionscode-spezifische
  Metrik wie `get_hotspots`).

## Notes

- **Warum `low` statt `medium` trotz neuem Scan:** Der neu eingeführte
  `.xaml`/`.html`-Scan (Datei 2, Schritt 5) ist strukturell eine
  Kopie des bereits etablierten, bewährten Musters aus
  `WebFileCatalog.SafeEnumerateFiles`/`IsGeneratedPath` (keine neue
  Roslyn-API, keine neue Fehlerklasse, keine Änderung an bestehendem
  Verhalten) — die einzige „neue Fläche" ist eine einfache
  Dateisystem-Traversierung mit Endungs-Vergleich, kein
  API-Neuland wie in step-004/step-007. Das rechtfertigt die niedrigere
  Risiko-Einstufung trotz der in „Aktueller Projektzustand" dokumentierten
  Korrektur an der `konzept.md`-Prämisse.
- **Für die folgenden EPIC-04-Steps relevant:** `get_hotspots` (Basis
  `HotspotMapBuilder`) und `get_violations` (Basis `RuleRegistry`/
  `LinterEngine`) landen laut `roadmap.md` ebenfalls potenziell in
  `FileStructureToolRegistrations` — der nächste Planer-Durchlauf sollte
  den Footprint dieser Klasse nach diesem Step erneut prüfen (wie bisher
  bei `McpServerOptionsFactory` in TD-004 praktiziert) und ggf. eine
  dritte Registrar-Klasse in Erwägung ziehen, falls der Puffer nach
  diesem und dem nächsten Tool merklich schrumpft.
