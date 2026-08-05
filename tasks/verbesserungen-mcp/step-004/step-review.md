---
status: done
type: step-review
task: verbesserungen-mcp
step: 004
epic: EPIC-03
step_type: batch
reviewed_by: kritiker
reviewed_by_model: Sonnet 5 Medium
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-05T13:15:00Z
verdict: approved
tech_debt_ids: [TD-005]
---

# Review Step 004: EPIC-03-Batch

## Verdict
- [x] **approved** — alle vier Prüfebenen ok; keine CRITICAL/MAJOR-Findings;
  ein projektweiter Tech-Debt-Hinweis (TD-005) angehängt.

## Befunde pro Ebene

### Ebene 1 – Plan-Erfüllung
Alle vier Items 1:1 umgesetzt. item-01: `FindReferencesTool.ResolveSymbolAsync`
ruft `ResolveSymbolCoreAsync` und normalisiert via `NormalizeToOwningMember`
(`IMethodSymbol.AssociatedSymbol` → Owner); Greeter.cs line 7 trägt
`public string Prefix { get; set; } = "Hi";` mit `get` exakt auf Spalte 28
und `set` exakt auf Spalte 33 (zeichengenau verifiziert). item-02:
`GetViolationsScanner.FormatReport` ist jetzt `internal static`, berechnet
`matchingFileCount` aus `fileToProject` via `MatchesScope` (statt aus
`filtered`), Bedingung und Wortlaut präzisiert; `MatchesScope` unverändert.
item-03: `McpCodeGraphServer.LoadState` peekt im `IsCompletedSuccessfully: true`-
Zweig `_loadTask.GetAwaiter().GetResult()` mit `ainetlinter-disable
BanBlockingTaskAccess`-Kommentar (selbe Begründungs-Form wie der bestehende
Eintrag in `GetCurrentSolution()`:108-110); `IsLoaded` bewusst unverändert.
item-04: `FindReferencesDescription` und `GetImpactDescription` enthalten
jeweils den 200-Knoten-Hard-Cap-Satz; bestehender `hard cap 3`-Stil
erhalten. Tests wie im Plan: zwei für item-01 (zentraler Nachweis +
Regressionsschutz in `GetSymbolBodyToolTests`), zwei für item-02
(Regression + Regressionsschutz), einer für item-03 (TCS-Pattern statt
`Task.FromResult` — Abweichung begründet), einer für item-04 in neuer
Klasse `SymbolGraphToolRegistrationsTests`. Commit-Passung: ein
Code-Commit (`e1d0124…`), ein Doku-Commit (`cdfcf28…`), Conventional
Commits auf Deutsch, Subject-Suffix `[verbesserungen-mcp]`, Body listet
alle vier Items einzeln auf, `Refs`-Zeile verweist auf
`tasks/verbesserungen-mcp/step-004`. Datei-Score-Assertions
(`GetIndexScopeToolTests` `.cs: 5 Dateien`) weiterhin grün — die
Prefix-Property ergänzt eine Zeile, keine Datei.

### Ebene 2 – Rules-Konformität
Alle im Plan unter „Rules-Refs" explizit zitierten Regeln eingehalten.
Zero-Warning-Direktive: `dotnet build` reproduziert 0 Fehler / 0
Warnungen. Testsuite-Parallelität: keine neue zwangsserialisierende
`[Collection]` / `DisableParallelization`; `IClassFixture<SymbolGraphCatalogFixture>`
an `McpServerCommandLoadingStateTests` ist die Standard-xUnit-
Klassen-Fixture und blockiert keine Parallelität auf Suite-Ebene.
`BanBlockingTaskAccess`: `ainetlinter-disable`-Kommentar in
`McpCodeGraphServer.cs:71` mit Begründung vorhanden (gleicher
Rechtfertigungs-Pfad wie der bestehende Kommentar in `GetCurrentSolution()`).
`MaxMethodParameterCount`: `ainetlinter-disable`-Kommentar in
`GetViolationsScanner.cs:106-113` mit Begründung, warum ein
Parameter-Record an dieser Stelle mehr Schaden als Nutzen bringt
(transitive `AIContextFootprint`-Auswirkung auf
`AnalysisToolRegistrations.cs`). Sparsame Code-Kommentare: keine
Task-/Step-/Epic-IDs im neuen Code; alle Kommentare sind reine
*Why*-Erläuterungen für unkonventionelle Stellen (Workaround-Begründung,
Test-Rationale). `MaxMethodLineCount` 60 (Produktion) / 100 (Tests):
`FindReferencesTool.NormalizeToOwningMember` ist 1-zeilig, neue
`FormatReport`-Logik bleibt unter 50 Zeilen, LoadState-Änderung ist
1-zeilig. `EnforceSealedClasses`: neue Testklasse
`SymbolGraphToolRegistrationsTests` ist `public sealed class`.
`EnforceNoSilentCatch`: keine neuen catch-Blöcke. Commit-Vorschlag-
Pflicht: kein Coder-Codeblock meinerseits (Kritiker-Aufgabe, hier nicht
anwendbar). TD-004 (zerrissener XML-Doc an `FindReferencesTool.cs:27-35`)
bewusst NICHT im selben Zug aufgeräumt — Plan und „Aufräumen erlaubt"-
Klausel wurden eingehalten.

### Ebene 3 – Logische Korrektheit
- **item-01** (zentraler Fix): `NormalizeToOwningMember` prüft
  `symbol is IMethodSymbol { AssociatedSymbol: { } owner } ? owner : symbol` —
  das Muster-Pattern-Match schreibt nur Accessor-`IMethodSymbol`e um
  (deren `AssociatedSymbol` per Roslyn-API auf das Property/Event
  zurückführt). Lokale Funktionen, Lambda-Symbole, Konstruktoren und
  reguläre Methoden ohne `AssociatedSymbol` werden durchgereicht
  (Planer-Hinweis in „Bekannte Unschärfen" verifiziert).
  `GetSymbolBodyToolTests.ExecuteAsync_PositionOnPropertyAccessorKeyword_ReturnsPropertyIdNotAccessorId`
  prüft `Contains("id: \`P:SymbolGraphMini.Greeter.Prefix\`", text)`
  **und** `DoesNotContain("get_Prefix", text)` — beide Bedingungen
  vorhanden, exakt wie spezifiziert.
  `FindReferencesToolTests.ResolveSymbolAsync_PositionOnPropertyAccessorKeyword_ReturnsPropertySymbolNotAccessor`
  prüft `symbol!.Name == "Prefix"` **und** `IsAssignableFrom<IPropertySymbol>(symbol)`
  **und** `IsNotAssignableFrom<IMethodSymbol>(symbol)` — sogar
  verschärft (doppelte Richtung), zentraler Nachweis erbracht.
- **item-02**: `matchingFileCount` wird **nur** aus `fileToProject` über
  `MatchesScope` berechnet; `filtered` fließt nicht in die Dateizahl ein.
  Tests: `FormatReport_FilesInScopeButZeroViolations_DistinguishesFromNoFilesInScope`
  prüft `DoesNotContain("Keine Dateien im Scope")` **und**
  `Contains("Dateien im Scope")` mit einem passenden
  `fileToProject`-Eintrag bei leerem Violations-Array; korrekt.
  `FormatReport_NoFileMatchesScope_ReturnsExplicitNoFilesMessage`
  prüft die bestehende Meldung mit nicht-matchendem Projektnamen;
  Regressionsschutz.
- **item-03**: Race-Bedingung exakt wie im Plan beschrieben —
  `IsCompletedSuccessfully: true` + `_catalog is null` (vor
  `GetCurrentSolution()`-Adoption) lieferte fälschlich `LoadFailed`.
  Fix: `(_catalog ?? _loadTask.GetAwaiter().GetResult()) is null ? LoadFailed : Loaded`
  peek-t das Resultat ohne Adoption; `IsLoaded` bleibt korrekt bei
  „noch nicht adoptiert". Test
  `LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`
  konstruiert mit `LoadFunc = _ => release.Task` (TCS), verifiziert
  Initialzustand `Loading`, löst die Task aus, pollt bis `Loaded` und
  prüft `LoadState == Loaded` **ohne** vorher `GetCurrentSolution()`
  aufgerufen zu haben — exakt der verifizierte Lückenfall aus dem Plan.
  `Task.FromResult`-Variante (Plan-Vorschlag) wäre racy wegen
  `Task.Run`-Scheduling in `McpCodeGraphServer.cs:50`; TCS-Pattern ist
  die korrekte deterministische Alternative.
- **item-04**: `descriptions["find_references"]` und
  `descriptions["get_impact"]` enthalten beide `"200"` (Substring,
  hartkodiert). `Assert.Contains("200", descriptions["find_references"],
  StringComparison.Ordinal)` prüft genau das. `hard cap 3`-Stil für
  `depth` in beiden Texten erhalten.

### Ebene 4 – Konzept-Treue
Alle vier Muss-Haben-Punkte aus `Konzept.md` Scope P2/P3, die dieser
Step laut Plan abdecken sollte, sind addressiert:
P2 „`get_symbol_body`-ID-Korruption beheben" (item-01), P2
„`get_violations`-Meldung präzisieren" (item-02), P3
„`ainetlinter://overview`-Status synchronisieren" (item-03), P3
„`find_references`/`get_impact` depth-Hard-Cap dokumentieren" (item-04).
Die zwei in `step-plan.md` „Notes" explizit als verifizierte
Präzisierungen (nicht Scope-Drift) markierten Abweichungen (item-01
Root-Cause in Property-Accessor-Resolution statt generischen Methoden,
item-03 Root-Cause in `McpCodeGraphServer.LoadState` statt
`OverviewResourceRegistration.DescribeSolution`) sind begründet
umgesetzt — zentrale Normalisierung in `ResolveSymbolAsync` wirkt
einheitlich auf alle vier Symbolgraph-Tools, der LoadState-Peek
ändert ausschließlich den direkt beobachtbaren `LoadState`-Wert
(kein Tool-Dispatch-Pfad betroffen, da Tool-Guards nur `Loading`
prüfen, dann unbedingt `GetCurrentSolution()` adoptieren). P2
„Globaler Rausch-Hinweis eindämmen" war laut Konzept explizit
funktional von P1 (Roslyn-Source-Generator-Integration) abhängig und
ist durch `step-001/002/002-fix-01` bereits addressiert; keine Lücke
in diesem Step. Nice-to-Have (EII-Darstellung) bleibt wie geplant
außerhalb dieses Batches.

## Findings
Keine.

## Tech-Debt-Einträge aus diesem Review
- **TD-005** (neu, Priorität mittel) — `AIContextFootprint`-Schwellwert
  2800 für `AnalysisToolRegistrations.cs` ist zu knapp +
  `CliIntegrationTests.RunLinterCli_OnWholeSolution_ReturnsSuccess`
  ist ein fragiler Smoke-Test (siehe `tech-debt.md`).

## Test-/Build-Status
- `dotnet build` → grün, 0 Fehler / 0 Warnungen (kritiker hat selbst gebaut).
- `dotnet test --filter Category!=Integration` → grün, 1160 / 0 / 0.
- `dotnet test` (Volllauf) → grün, 1267 / 0 / 0 in 1 m 49 s, kein
  Testhost-Absturz (TD-003 trat diesmal nicht auf — kein TD-003-Lauf
  nötig). Coder-Count 1267 reproduziert.
- TD-004-Notiz: zerrissener XML-Doc an `FindReferencesTool.cs:27-35`
  weiterhin vorhanden, vom Coder korrekt nicht angefasst (Plan-Vorgabe
  „TD-004 bleibt unangetastet").

## Sonstige Beobachtungen
- Der `// ainetlinter-disable MaxMethodParameterCount`-Kommentar in
  `GetViolationsScanner.cs:106-113` ist mit acht Zeilen ungewöhnlich
  lang, aber jede Zeile trägt zur Begründung bei (5 Eingaben,
  AIContextFootprint-Auswirkung auf transitive `AnalysisToolRegistrations.cs`,
  bereits bestehende zentrale Parameter-Bündelung in
  `GetViolationsScannerParameters`, internal-Sichtbarkeits-Erzwingung).
  Das ist kein Verstoß gegen sparsame Kommentare — alle Aussagen sind
  entscheidungsrelevant und nicht aus dem Code selbst rekonstruierbar.
- Die `MaxMethodParameterCount`-Accessibility-Differenzierung
  (`MaxMethodParameterCountForNonPublic: 6` greift nicht für `internal`)
  ist ein bekanntes Linter-Verhalten und in `codegraph-mcp-finish/
  step-010/step-review.md §67` bereits angemerkt — nicht erneut als
  Tech-Debt-Eintrag aufgenommen (würde TD-003-Nachbarschaft duplizieren).
- `McpServerCommandLoadingStateTests` ist als `Category=Integration`
  markiert; die Klasse benötigt `SymbolGraphCatalogFixture` (löst
  MSBuildWorkspace-Load aus), daher ist die Kategorisierung korrekt.
