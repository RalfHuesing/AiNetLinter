---
status: done
type: step-result
task: 03_get-impact-zum-diff-kontext-erweitern
step: 002
epic: EPIC-2
step_type: single
coded_by: coder
coded_by_model: stealth/ox-alpha
coded_by_model_knowledge_cutoff: unbekannt
coded_at: 2026-08-22T21:40:00+02:00
code_commit_hash: 5b26c63b
status_after: done
blocker_category: n/a
---

# Result Step 002: Strukturiertes DiffImpactAnalysis-Ergebnisobjekt im DiffImpactAnalyzer

## Zusammenfassung

Der Git-Diff-Zweig des `DiffImpactAnalyzer` hält sein Zwischenergebnis jetzt
im strukturierten Record `DiffImpactAnalysis` fest (RepositoryRoot,
SinceRef, `ChangedFiles` mit kompakten `HunkRange`s, `ChangedSymbols` als
`ChangedSymbolEntry` mit stabiler ID, `References` als
`ReferenceTraversalResult`) und wird über den neuen internen Kern
`AnalyzeDiffAsync` zurückgegeben; `AnalyzeEntriesAsync` ist nur noch
Wrapper, dessen Ausgabe feld- und reihenfolgetreu aus
`References.CallSites` abgebildet wird. Git läuft pro Analyse weiterhin
genau einmal (nur der Kern ruft `RunGitDiff`). Die Zeilen-Expansion von
`ParseGitDiffHunks` (Signatur/Nutzer unverändert) wird aus dem neuen
Range-Parsing abgeleitet — eine Parse-Wahrheit; `IntersectsWithChangedLines`
prüft äquivalent auf Range-Überlappung. `GetStableSymbolId` ist analog zum
step-001-Muster auf `internal` gestellt und gemeinsame Quelle der Symbol-IDs.

## Geänderte Dateien

- `src/AiNetLinter/Core/DiffImpactAnalysisModels.cs` (neu) — vier
  `internal sealed record`s (`HunkRange`, `ChangedFileRange`,
  `ChangedSymbolEntry`, `DiffImpactAnalysis`) in eigener Datei
  (MaxLineCount); XML-Doc hält die beiden FilePath-Bedeutungen fest
  (repo-root-relativ vs. solution-relativ).
- `src/AiNetLinter/Core/DiffImpactAnalyzer.cs` — neuer Kern
  `AnalyzeDiffAsync` (4 Parameter), Wrapper `AnalyzeEntriesAsync`,
  `ParseGitDiffHunkRanges`/`ExpandHunkRanges` (DRY-Ableitung),
  Range-basierte Überlappungsprüfung, Mapping
  `CreateChangedSymbolEntry(ISymbol, Document)` +
  `BuildReferencesAsync` (bewusst OHNE `TraversalState.CreateResult`,
  damit unsortiert/undedupliziert wie bisher), `ToCallSiteEntries`;
  Datei 485 Zeilen (unter Limit).
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/CallGraphTraversal.cs` —
  `GetStableSymbolId` `private`→`internal` + Why-XML-Doc; sonst unverändert.
- `src/AiNetLinter.FastTests/Core/DiffImpactAnalyzerTests.cs` — sechs neue
  Unit-Tests (kompakte Ranges Multi-File/Einzeiler/count=0,
  Expansions-Äquivalenz, Entry-Mapping über `CreateScenario` inkl.
  public/internal/protected/private, lokale Funktion, Wrapper-Mapping);
  Bestandstest unangetastet.
- `src/AiNetLinter.IntegrationTests/Mcp/Tools/SymbolGraph/GetImpactToolIntegrationTests.cs`
  — neuer End-to-End-Test `AnalyzeDiffAsync_OnModifiedWorkspace_...`
  auf `GitImpactMiniFixtureWorkspace` (RepositoryRoot/SinceRef/Ranges/
  Symbol-Entry + Wrapper-Äquivalenz elementweise).

## Commit

- **Code-Commit-Hash:** `5b26c63b`
- **Message:**
  ```
  feat: DiffImpactAnalysis-Kern [03_get-impact-zum-diff-kontext-erweitern]

  Der Git-Diff-Zweig des Analyzers baut intern ein strukturiertes
  DiffImpactAnalysis-Ergebnisobjekt ... (Body gekürzt)

  Refs: tasks/mcp-server-weiterentwicklung/03_get-impact-zum-diff-kontext-erweitern/step-002
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier
  drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build                                                          → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress       → grün (1591 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (346 Tests, 0 Fehler)
```

Schnelliteration während der Entwicklung: nur
`FullyQualifiedName~DiffImpactAnalyzerTests` (7/7) bzw. der neue
Integrationstest einzeln.

## Abweichungen vom Plan

1. **Plan-Prämisse „lokale Funktion → Fallback statt null“ ist falsch
   (Produktion korrekt, Test angepasst):** Der Plan ging davon aus,
   `DocumentationCommentId.CreateDeclarationId` liefere für lokale
   Funktionen null, sodass der deterministische
   FullyQualifiedFormat-Fallback greift. Empirisch liefert
   `CreateDeclarationId` für die lokale Funktion eine Doc-ID (beobachtet:
   die ID der *einschließenden Methode*, z. B.
   `M:ScenarioNs.LocalFuncHost.Run(System.Int32)~System.Int32`) — der
   Fallback-Pfad greift dort gar nicht. Die Produktionslogik blieb exakt
   wie geplant die bestehende, unveränderte stabile-ID-Logik; der Test
   heißt deshalb `CreateChangedSymbolEntry_ForLocalFunction_
   UsesSharedStableIdLogic` (statt `...UsesDeterministicFallbackId`) und
   pinnt: identische ID wie `CallGraphTraversal.GetStableSymbolId`,
   deterministisch bei Wiederholung, und explizit ≠ Fallback-String.
2. **DocCommentId-Format mit Rückgabetyp-Suffix:** nicht-void-Methoden
   bekommen `~System.Int32`-artige Suffixe in der Doc-ID — die konkreten
   Expected-Literale in Unit- und Integrationstest mussten entsprechend
   ausfallen (Plan enthielt dazu keine Annahme; rein
   Testerwartungs-Anpassung, kein Code-Unterschied).
3. Sonst Plan 1:1 umgesetzt (Scanner-Scope unverändert schmal, kein
   detailLevel/EPIC-6, Docs/agent-api.md unberührt, Git genau einmal,
   `AnalyzeAsync`/CLI unberührt, `ParseGitDiffHunks`-Signatur/Nutzer
   unverändert).

## Beobachtungen

- **Relevant für den nächsten Step (breiter Scanner):** Wenn lokale
  Funktionen über die gemeinsame Stabile-ID-Logik laufen, erhalten ALLE
  lokalen Funktionen derselben einschließenden Methode denselben
  Symbol-Identifier (Doc-ID des umgebenden Members) — ein
  Kollisions-/Zuordnungsrisiko für `changedSymbols` und
  `ReachedFromSymbolId`, sobald der breite Scanner lokale Funktionen
  einschließt. Der Kritiker könnte daraus einen Tech-Debt-/Konzept-Hinweis
  machen (z. B. Sonderfall lokal: Name + Deklarationsposition in die ID
  einbeziehen). Ich habe nichts daran geändert — außerhalb des Scopes.
- `ParseHunkLine` war `internal` mit GENAU einem Aufrufer in derselben
  Datei (per `find_references` am laufenden Server verifiziert) — durfte
  beim Range-Umbau privat werden und arbeitet jetzt auf
  `List<HunkRange>`.
- Kleine DRY-Extraktion über den Wortlaut des Plans hinaus, verhaltens-
  identisch: der Ausdruck `"ContainingType.Membername"` steckt jetzt im
  Helper `FormatMemberDisplayName` und wird von `FindCallSiteEntriesAsync`
  UND `CreateChangedSymbolEntry` geteilt (statt dupliziert zu werden).
- Dogfooding ausgeführt: `metrics_lookup` über alle sechs neuen/geänderten
  Analyzer-Methoden — LOC/CC/CogC/Parameter je im Grünen (Kern 4 Parameter,
  MaxBoolParameterCount eingehalten); `find_duplicates` (clone/near,
  minTokens 20, Scope Core) — keine Cluster.
- TD-001 (`CreateScenario` ohne `CreateServer`): meine neuen Tests nutzen
  `CreateScenario` serverlos (reine Mapping-Tests) — die Ergonomie-Lücke
  trifft meinen Step nicht mechanisch, nichts angehängt.

## Bekannte Unschärfen

- Die Byte-Identität der `callers`-Textausgabe ist nicht durch einen
  direkten Alt/Neu-Diff belegt, sondern durch die unverändert grünen
  Bestands-Tool-/Subprozess-/Dogfood-Tests (alle ohne Anpassung) plus die
  strukturelle Erhaltung der Reihenfolgenlogik (Distinct über Erstvorkommen,
  kein Sortieren/Deduplizieren im Wrapper-Pfad). Der neue Integrationstest
  sichert zusätzlich Element- und Reihenfolgegleichheit von
  `References.CallSites` und `AnalyzeEntriesAsync`.
- Die genaue Roslyn-Semantik von
  `DocumentationCommentId.CreateDeclarationId` bei lokalen Funktionen
  (liefert offenbar die ID des einschließenden Members) ist empirisch über
  den Testlauf beobachtet, nicht gegen Roslyn-Quelltext verifiziert.
