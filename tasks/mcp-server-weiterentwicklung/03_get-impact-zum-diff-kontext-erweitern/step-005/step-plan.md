---
status: open
type: step-plan
task: 03_get-impact-zum-diff-kontext-erweitern
step: 005
corrects: null
title: "Violations-Stufe, get_impact-Vertrag change-context & Doku (EPIC-5+6+7)"
epic: EPIC-5+EPIC-6+EPIC-7
estimated_risk: high
step_type: single
items: []
created_by: orchestrator
created_by_model: stealth/ox-alpha
created_by_model_knowledge_cutoff: unbekannt
created_at: 2026-08-22T23:45:00+02:00
related_to: [step-002, step-003, step-004]
---

# Step 005: Violations-Stufe, get_impact-Vertrag „change-context" & Doku (EPIC-5+6+7)

## Bezug

- **Task:** `03_get-impact-zum-diff-kontext-erweitern`
- **Epics:** Konsolidierung laut Nutzerentscheidung (task-state.md) — EPIC-5
  (solutionweite Violations & diffbezogene Filterung), EPIC-6 (Tool-Vertrag +
  strukturierte Antwort) und EPIC-7 (Doku inkl. Grenzen) im letzten Step.
- **Konzept-Referenz:** §Öffentlicher Vertrag (detailLevel/maxChangedSymbols/
  maxTestsPerSymbol), §StructuredContent (exakte Feldnamen!), §Filterregeln für
  Violations, §Performance- und Größenregeln, §Definition of Done, Audit B/D.1–D.7,
  Testliste §Tests (alle noch offenen Punkte).

## Aktueller Projektzustand (JIT-Kontext)

Verifiziert am Codestand nach step-004 (get_file_skeleton + Datei-Lektüre):

1. **`GetImpactInput`** hat 4 Parameter (GitRef, SymbolIdentifier, MaxResults,
   Depth); `GetImpactTool.ExecuteAsync` dispatcht in Symbol-Branch
   (mit ct) und Git-Branch (**ohne ct** — Audit D.7: dort läuft u. a.
   `BuildAggregateWarningAsync` mit `CancellationToken.None`).
2. **`GetViolationsScanner.BuildViolationsTextAsync(GetViolationsScannerParameters)`**
   liefert solutionweite Violations als `GetViolationsResult` (Text,
   IsMalfunction, IsTruncated, Violations-Liste); Parameter-Record trägt bereits
   Solution/Config/Console/CancellationToken/MaxResults/ContextLines/
   IncludeSnippet — Basis für „Linter genau einmal".
3. **`McpToolResults.Text<T>(text, payload)`** erzeugt strukturierte Antworten;
   `InvalidArgument(message, hint)` ist der recoverable-Kanal für den
   INVALID_ARGUMENT-Fall; `McpSufficiencyHints.Append` existiert (step-001).
4. **Batch-Stufen stehen:** `AnalyzeChangeContextAsync` liefert das
   Ergebnisobjekt (changedFiles mit Hunk-Ranges, ChangedSymbols breit,
   References), `FindTestsForSymbolsAsync` ordnet alle Ziele in einem Scan zu,
   `TestRecommendationBuilder` baut deduplizierte Commands je Projekt,
   `DiffImpactCounters` zählt GitRuns/TestSolutionScans/LintRuns (LintRuns
   bisher immer 0).
5. **Docs:** `Docs/agent-api.md` dokumentiert bestehende Tool-Verträge;
   README-MCP-Tabelle hat eine Zeile zu `get_impact`; `Docs/ROADMAP.md` führt
   abgeschlossene Epics.

## Intention

Nach diesem Step liefert `get_impact(detailLevel="change-context")` im
Git-Diff-Modus die vollständige diffbezogene Antwort: geänderte Dateien mit
Hunk-Ranges, geänderte Symbole (breiter Scope aus step-003), Call-Sites,
statisch zugeordnete Tests (gebatcht, step-004), direkt relevante Violations
(ein Solution-Lint, gefiltert auf Hunks/Symbolspannen) und
recommendedTestCommands — gekappt, deterministisch, mit Completeness-Metadaten
und Sufficiency-Hint bei vollständigen Ergebnissen. Der `callers`-Modus bleibt
byte-kompatibel. Doku nennt Vertrag, Verhaltenskorrektur und Grenzen exakt.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/Tools/SymbolGraph/GetImpactTool.cs`

- **Was:**
  - `GetImpactInput` um drei additive Optionen mit Defaults erweitern:
    `DetailLevel DetailLevel` (enum `Callers | ChangeContext`, Default
    `Callers`; Binding aus String „callers"/„change-context", unbekannter Wert
    → `InvalidArgument`), `MaxChangedSymbols` (Default 20, Cap 100),
    `MaxTestsPerSymbol` (Default 10, Cap 50).
  - Validierung VOR jeder Analyse: `DetailLevel=ChangeContext` zusammen mit
    `SymbolIdentifier != null` → `InvalidArgument("detailLevel=change-context ist nur im Git-Diff-Modus verfügbar.", hint="Verwende get_feature_context für Symbol-Kontext.")`.
  - Git-Branch an `ct` binden (Signatur `ExecuteGitRefBranchAsync(...,
    CancellationToken ct)`; kein `CancellationToken.None` mehr — Audit D.7).
  - `Depth` bleibt im gesamten Git-Branch dokumentiert unwirksam (kein Code-
    Change nötig; nur Doku-Vertrag).
  - Neuer Pfad `ExecuteChangeContextAsync(state, input, ct)`: ruft
    `AnalyzeChangeContextAsync` mit Counters, Kappung vorweg:
    `MaxChangedSymbols` deterministisch kappen NACH Projekt, Datei, Startzeile,
    Symbol-ID sortiert (VOR References/Test-Zuordnung/Violations-Filterung);
    dann References-Stufe (bestehend), Batch-Test-Zuordnung je gezeigtem
    Symbol mit `MaxTestsPerSymbol`-Kappung JE Symbol nach Evidenz-Priorität,
    Violations einmal solutionweit über `GetViolationsScanner` (nur
    Violations-Liste verwenden, NICHT deren Text) und Filterung: Violation ist
    relevant wenn (Datei+Zeile in geändertem Hunk) ODER (Datei+Zeile in
    Deklarationsspanne eines GEZEIGTEN Symbols); recommendedTestCommands via
    `TestRecommendationBuilder` über die tatsächlich gezeigten Treffer;
    Completeness-Objekt (changedSymbolsTotal/Shown, symbolsTruncated,
    callSitesTruncated, testsTruncated); Sufficiency-Hint analog Symbol-Branch
    bei vollständigen Ergebnissen. Antwort via `McpToolResults.Text<T>`:
    kompakter Text (Counts + max. Top-3 Symbole + Hinweise), Payload =
    strukturiertes Objekt EXAKT mit den Konzept-Feldnamen (mode, detailLevel,
    changedFiles[{filePath, ranges[{startLine,lineCount}]}],
    changedSymbols[{documentationCommentId, displayName, kind, accessibility,
    projectName, filePath, startLine, endLine}], callSites,
    testAssociations[{symbolId, filePath, testMethods, matchReason}],
    violations, recommendedTestCommands, completeness). Keine Source-Bodies.
  - `BuildAggregateWarningAsync`-Aufrufstelle erhält den echten ct.
- **Warum:** Kernvertrag EPIC-6; Audit D.7; Performance-Regeln (kappen vor
  teuren Folgestufen).

### Datei 2: Counter-Stelle (`DiffImpactCounters`)

- **Was:** LintRuns-Inkrement an der Stelle, an der der Violations-Scan für
  change-context ausgeführt wird (genau ein Inkrement pro Tool-Aufruf, nicht
  pro Symbol).
- **Warum:** Vollzug des Einmal-Nachweises (Konzept §Tests).

### Datei 3: `src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs`

- **Was:** Falls nötig: schmale interne Überladung/Aufteilung, die die rohe
  `IReadOnlyList<RuleViolation>` OHNE Textformatierung liefert (der bestehende
  Textpfad bleibt unangetastet); keine Regel-/Formatänderungen.
- **Warum:** change-context braucht Liste + Metadaten, nicht den Reporttext;
  DRY statt zweitem Lint-Pfad.

### Datei 4: Tests — FastTests

- **Neue Datei** `src/AiNetLinter.FastTests/Mcp/Tools/SymbolGraph/GetImpactChangeContextTests.cs`:
  - `ChangeContext_WithSymbolIdentifier_ReturnsInvalidArgument` (recoverable,
    Hinweis auf get_feature_context)
  - `ChangeContext_UnknownDetailLevel_ReturnsInvalidArgument`
  - Caps: >20 geänderte Symbole → shown=20, symbolsTruncated=true,
    Completeness korrekt; `maxChangedSymbols=100` hart gecappt;
    `maxTestsPerSymbol`-Kappung sichtbar (testsTruncated=true)
  - Violations-Filter: Treffer im Hunk drin; benachbarte irrelevante Violation
    derselben Datei draußen; Violation in Deklarationsspanne eines gezeigten
    Symbols drin
  - `LintRuns == 1` bei Multi-Symbol-Lauf (Counter-Vollzug)
  - Determinismus: zwei identische Aufrufe → identische Reihenfolge/Payload
  - Gelöschte Datei im Diff → taucht nicht in changedSymbols auf, Antwort
    valide (dokumentierte Grenze)
- **Bestand:** `GetImpactToolTests.cs` erweitern um Absicherung, dass `callers`
  ohne neue Parameter unverändert antwortet.

### Datei 5: Tests — IntegrationTests

- **Erweiterung** `GetImpactToolIntegrationTests.cs`: Ende-zu-Ende auf
  `GitImpactMiniFixtureWorkspace` (+ privater Normalize-Änderung):
  `get_impact` über den echten MCP-Server mit `detailLevel="change-context"`
  → structuredContent enthält changedFiles-Ranges, private Methode in
  changedSymbols, callSites korrekt, testAssociations und
  recommendedTestCommands je Projekt dedupliziert; `callers`-Aufruf im selben
  Workspace liefert das alte Antwortbild (Snapshot-Schutz).

### Datei 6: `Docs/agent-api.md` (EPIC-7)

- **Was:** `get_impact`-Abschnitt erweitern: neue Parameter mit Default/Cap;
  exakte StructuredContent-Feldnamen (Beispiel aus dem Konzept 1:1);
  **Verhaltenskorrektur** `depth>1` in `find_references`/`get_impact`-
  Symbol-Branch ausgewiesen (echte Aufruferketten statt Override-only —
  Audit B); dokumentierte Grenzen: gelöschte Dateien erscheinen nie in
  changedSymbols (keine Hunks ohne Ziel-Datei), Umbenennungs-Randfälle,
  `depth` im gesamten Git-Branch wirkungslos, stabile ID = DocCommentId oder
  deterministischer Fallback (#lf:-Sonderfall lokale Funktionen), Testinfos
  als „statische Zuordnung" bezeichnet (keine Coverage-Garantie),
  Multi-Hunk-Container-Regel (innerste Deklaration global je Datei).
- **Warum:** DoD; Doku-Objektivität (Richtlinien §1).

### Datei 7: `README.md` + `Docs/ROADMAP.md`

- **Was:** README-MCP-Zeile zu `get_impact` um change-context kurz ergänzen;
  `Docs/ROADMAP.md` um Task-Eintrag (abgeschlossene Weiterentwicklung
  get_impact/find_references-Traversierung) erweitern — Update-Pflicht
  Richtlinien §4.

## Tests

- [ ] Alle neuen FastTests oben (Vertrag, Caps, Filter, Counter, Grenzen)
- [ ] Integrationstest End-to-End change-context + callers-Snapshot
- [ ] Alle Bestands-tests rund um get_impact/find_references/get_violations bleiben grün
- [ ] Volles Gate: build + beide Category!=Stress-Läufe grün

## Definition of Done

- [ ] Ein Git-Diff mit einem `get_impact(detailLevel="change-context")` vollständig lokalisierbar
- [ ] Kein neues MCP-Tool registriert (Registrierungsdatei unverändert)
- [ ] Kein N-mal-Vollscan (Testsolution einmal, Linter einmal, Git einmal — Counter belegt)
- [ ] Antwort deterministisch, gekappt, completeness-bewusst; keine Source-Bodies
- [ ] `callers` snapshot-kompatibel (Bestandstests unangepast grün)
- [ ] Doku: agent-api.md/README/ROADMAP aktualisiert; „statische Zuordnung" korrekt benannt
- [ ] Dogfooding: metrics_lookup neuer Symbole grün, find_duplicates ohne Cluster
- [ ] Commit(s) + `step-result.md` + Status→`done (pending audit)` + CodeMap

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#grenzwerte-produktion` — ≤500 Zeilen/Datei
  (bei Wachstum Helper-Datei im selben Ordner), ≤60 Zeilen/Methode, ≤4 Parameter
  (Input-Record wächst additiv — Audit D.6), MaxBoolParameterCount, sealed,
  #nullable enable
- `.agents/rules/AiNetLinterRichtlinien.mdc#1-grundprinzipien-design-philosophie`
  — Doku-Objektivität (nur Implementiertes, gegen Code verifizieren);
  `.agents/rules/AiNetLinterRichtlinien.mdc#5-qualitätsdrift-prävention` —
  DRY, Zero-Warning, keine Task-ID-Kommentare; §4 Updates & Tests

## Bekannte Ausnahmen

- Der Violations-Text von `GetViolationsResult` wird ignoriert (nur Liste) —
  bewusst, damit keine zweite Formatwahrheit entsteht.
- TD-001 bleibt offen (bewusst nicht angehängt).

## Notes

- **Anti-Loop-Check:** CodeMap widerspricht nicht; dieser Step verdrahtet
  ausschließlich die in step-002/003/004 gebauten Stufen und schließt die
  Doku-Lücke (EPIC-7-Zeile der Roadmap wurde in step-003 um die
  Reached-From-ID-Anmerkung präzisiert — hier abdecken).
- **Cap-Reihenfolge ist vertraglich:** Kappung der changedSymbols VOR
  References/Test/Violations-Stufen; Violations-Filterung nur gegen GEZEIGTE
  Symbole (Konzept-Wortlaut).
- **Kein neues Tool:** Registrierung in `SymbolGraphToolRegistrations.cs`
  bleibt unberührt (DoD-Kontrolle).
- **Determinismus:** Sortierung der Payload-Listen festlegen (changedFiles
  nach filePath, changedSymbols nach Projekt/Datei/Startzeile/ID — wie die
  Kappungsordnung, tests je symbolId+filePath, commands alphabetisch).
