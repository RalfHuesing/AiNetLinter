---
status: done
type: step-result
task: 03_get-impact-zum-diff-kontext-erweitern
step: 008
epic: EPIC-6
step_type: single
coded_by: coder
coded_by_model: stealth/ox-alpha
coded_by_model_knowledge_cutoff: unbekannt
coded_at: 2026-08-23T11:50:00+02:00
code_commit_hash: 5425f95f1283ea4937fb2edcb041a1de7c156dc9
status_after: done
blocker_category: n/a
---

# Result Step 008: get_impact-Vertrag „change-context" & strukturierte Antwort

## Sonderlage zuerst (für Kritiker/Planer)

Die Implementierung lag **vollständig uncommittet im Working-Tree** vor: der
Vorgänger-Coder scheiterte an einem transienten Modellfehler unmittelbar vor
dem Code-Commit (beide Gates laut seinem Transkript bereits grün). Diese
Instanz hat **nichts implementiert** — sie hat das Gate eigenverantwortlich
frisch geführt (grün, identische Zahlen), den kompletten Diff gegen den
Step-Plan gesichtet (1:1, siehe unten) und erst danach Code-Commit,
step-plan-Status, CodeMap und dieses Result erstellt.

## Zusammenfassung

`get_impact` trägt jetzt den vollständigen change-context-Vertrag:
`detailLevel=change-context` (nur Git-Diff-Modus, nie zusammen mit
`symbolIdentifier` → recoverable INVALID_ARGUMENT mit Hint auf
`get_feature_context`) liefert ein strukturiertes Payload-Objekt mit EXAKT
den Konzept-Feldnamen (`changedFiles[].ranges[]`, `changedSymbols[]` mit
`accessibility` als STRING, `callSites`, `testAssociations`, `violations`
ohne Snippet, `recommendedTestCommands`, `completeness` mit den fünf
Metadaten-Feldern) plus kompakter Textform — Sufficiency-Hint nur bei
vollständigem Ergebnis, sonst Trunkierungs-Meta-Zeile. Die Symbol-Kappung
sitzt im Analyzer-Kern NACH Symbolermittlung und VOR der teuren
Referenz-Stufe (deterministische Sortierung Projekt→Datei→Startzeile→
Symbol-ID); die ISymbol-Handles der gezeigten Symbole überleben die Kappung
für die Batch-Test-Stufe, `completeness.changedSymbolsTotal` kommt aus dem
Kern. Der `callers`-Pfad bleibt verhaltensidentisch (Cap-Default
`int.MaxValue` = No-op, Sortierung greift nur bei wirksamem Cap). Audit
D.7 ist geschlossen: `BuildAggregateWarningAsync` läuft an echtem `ct`,
kein `CancellationToken.None` mehr im Tool.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` — `AddGetImpact`-
  Lambda ADDITIV erweitert (`detailLevel`, `maxChangedSymbols`,
  `maxTestsPerSymbol`; Defaults aus `ChangeContextContract`-Konstanten),
  `GetImpactDescription` um beide detailLevel-Werte, Caps, Kombinationsverbot
  und den D.3-Hinweis („depth im gesamten Git-Branch wirkungslos") ergänzt;
  weiterhin EINE `tools.Add`-Zeile für `get_impact`.
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/GetImpactTool.cs` —
  `GetImpactInput` um die drei neuen Member ergänzt; case-insensitive
  detailLevel-Validierung VOR dem Dispatch (null/leer/„callers" →
  Bestands-Pfad; „change-context"+symbolIdentifier bzw. unbekannter Wert →
  `McpToolResults.InvalidArgument`); `ExecuteGitRefBranchAsync` nimmt `ct`
  (D.7); neuer Zweig `ExecuteChangeContextBranchAsync` (Cap-Normalisierung
  via `ChangeContextContract`, Analyzer mit Cap, dann Batch-Tests →
  Violations-Stufe, Antwort immer als Objekt via `Text<T>`, auch
  „kein Repo/leerer Diff" als leere Contract-Struktur + Sufficiency-Hint;
  GitDiffFailedException → bestehendes Recoverable-Muster; Malfunction →
  `Error(AnalysisFailed)` analog `GetViolationsTool`); kompakte Textform
  (`BuildChangeContextText` + Format-/Trunkierungs-Helper).
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/ChangeContextResponseModels.cs`
  (neu) — `ChangeContextContract` (Default-/Cap-/Modus-Konstanten +
  Clamp), DTO-Records mit den Vertragsfeldnamen (`ChangeContextPayload`,
  `ChangedFilePayload`, `HunkRangePayload`, `ChangedSymbolPayload`,
  `TestAssociationPayload`, `ViolationPayload`, `CompletenessPayload`),
  `ChangeContextResponseMapper` (Mapping der drei Stufen-Ergebnisse →
  Payload inkl. Test-Kappung je Symbol und Completeness-Spiegelung).
- `src/AiNetLinter/Core/DiffImpactAnalyzer.cs` — `DiffAnalysisRequest` um
  optionalen `ChangedSymbolCap` (Default `int.MaxValue`); `RunAnalysisAsync`
  kappt nach `GetChangedSymbolsFromHunksAsync` über
  `ApplyChangedSymbolCap` (deterministische Sortierung, No-op ohne
  wirksamen Cap) VOR `BuildReferencesAsync`; Ergebnis trägt
  `ChangedSymbolsTotal` + `ShownSymbolHandles`; bestehende Signaturen
  unverändert.
- `src/AiNetLinter/Core/DiffImpactAnalysisModels.cs` — `DiffImpactAnalysis`
  um additive optionale Member `ChangedSymbolsTotal = 0` /
  `ShownSymbolHandles = null` (XML-Doc); alle bestehenden Konstruktor-
  aufrufe bleiben gültig.
- `src/AiNetLinter.FastTests/Mcp/Tools/SymbolGraph/GetImpactToolTests.cs` —
  Vertragstests: INVALID_ARGUMENT-Fälle (Kombinationsverbot mit Hint,
  unbekannter Wert, null/""/"callers"-Dispatch), Cap-Normalisierung,
  StructuredContent-Feldnamen inkl. Verschachtelung, accessibility als
  String, Komplettmetadaten bei gekapptem Szenario (weggekapptes Symbol
  taucht nirgends auf), testsTruncated + Command-Dedup nur gezeigter
  Treffer, keine Snippets in Violations, Textform (Hint/Meta-Zeile/Counts).
- `src/AiNetLinter.FastTests/Mcp/Tools/SymbolGraph/ChangeContextResponseModelTests.cs`
  (neu) — reine Mapping-Tests des `ChangeContextResponseMapper`/der DTOs
  (Feldnamen-Pinning, Accessibility-String-Mapping, Completeness-Spiegelung,
  Test-Kappung, leeres Payload).
- `src/AiNetLinter.IntegrationTests/Mcp/Tools/SymbolGraph/GetImpactToolIntegrationTests.cs`
  — Ende-zu-Ende `detailLevel="change-context"` auf
  `ChangeContextMiniWorkspace`: beide geänderten Methoden inkl. privater
  `LogInternal` in `changedSymbols`, Call-Sites für `PlaceAsync`,
  nicht-leere `testAssociations`/`recommendedTestCommands`, Violations nur
  aus Hunk/Spanne; die Subprozess-Snapshot-Tests (`callers`) blieben
  unangetastet und grün (Abwärtskompatibilitätsnachweis).

## Commit

- **Code-Commit-Hash:** `5425f95f`
- **Message:**
  ```
  feat: change-context-Vertrag [03_get-impact-zum-diff-kontext-erweitern]

  Refs: tasks/mcp-server-weiterentwicklung/03_get-impact-zum-diff-kontext-erweitern/step-008
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier
  drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build                                                          → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress       → grün (1628 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (350 Tests, 0 Fehler)
```

Zusatzchecks via MCP nach `reload_config` (Server-Index kannte die neue
Datei vorher nicht — step-007-Beobachtung bestätigt sich): `metrics_lookup`
auf 6 neuen/geänderten Symbolen alle Schwellwerte OK
(`ChangeContextResponseMapper` 81 LOC/Footprint 240, `ChangeContextContract`
16/215, `ExecuteChangeContextBranchAsync` 50 ≤ 60 Zeilen bei Komplexität
6/5 und 4 effektiven Parametern, `GetImpactInput`, `ApplyChangedSymbolCap`
15 Zeilen); `find_duplicates` (Scope SymbolGraph, production) → 0 Cluster
bei 81 Methoden; `find_dead_code` (high, Scope SymbolGraph) → 0;
`find_magic_values` (Scope SymbolGraph) → nur Audit-Kandidaten, keine
Violation (Einordnung siehe Beobachtungen).

## Abweichungen vom Plan

Keine funktionalen — die eigene Diff-Sichtung bestätigt die Umsetzung 1:1
zu „Konkrete Änderungen". Übernommene, bereits im Step-Plan unter „Bekannte
Ausnahmen" dokumentierte Deutungen (für den Kritiker nochmals sichtbar,
da DoD-relevant):

1. **DoD-Deutung Registration:** der bestehende `AddGetImpact`-Eintrag ist
   ADDITIV erweitert (drei neue Delegat-Parameter + Beschreibungstext) —
   keine zweite `tools.Add`-Zeile, kein neues Tool. Die wörtliche Lesart
   („Registrierungsdatei bleibt unverändert") ist ohne Lambda-Erweiterung
   unerfüllbar (MCP-Parameter entstehen aus der Delegat-Signatur); Konzept-
   DoD und codemap legen die additive Lesart nahe. Falls der Orchestrator
   die wörtliche Lesart will: Step zurückweisen.
2. **Namensabweichung gitRef/gitSinceRef:** Tool-Argument heißt weiterhin
   `gitRef` (Abwärtskompatibilität); EPIC-7 dokumentiert die tatsächlichen
   Namen.

## Beobachtungen

- **Defaults an der Signatur sind Konstanten:** das Registrierungs-Lambda
  referenziert `ChangeContextContract.DefaultMaxChangedSymbols/MaxTestsPerSymbol`
  statt Literale — Magic-Value-Regel an der MCP-Signatur eingehalten.
- **callers-Pfad strukturell geschützt:** `ApplyChangedSymbolCap` ist bei
  `matches.Count <= cap` ein No-op (Rückgabe der Originalliste) — die
  deterministische Sortierung greift NUR bei wirksamem Cap; die grünen
  unangetasteten Snapshot-/Subprozess-Tests stützen die Bytegleichheit.
- **D.7 vollständig:** im gesamten `GetImpactTool` steht kein
  `CancellationToken.None` mehr; beide Zweige rufen
  `BuildAggregateWarningAsync(solution, ct)`.
- **Magic-Value-Audit-Einordnung (an Kritiker):** `find_magic_values`
  meldet die Vertragskonstanten 20/100/50 und "change-context" in
  `ChangeContextResponseModels.cs` als Kandidaten; die angezeigte
  „Duplizierung" von 20 mit `DiRegistrationHeuristics.MaxRegistrationHits`
  (und 50 mit `GetTypeHierarchyTool`) ist zufällige Wertgleichheit fachlich
  unabhängiger Konstanten, kein echter Klon — bewusst nicht "gefixt",
  ggf. Tech-Debt-Eintrag nach eurer Bewertung.
- **Verzeichnisgrenze:** `Mcp/Tools/SymbolGraph` hat jetzt 15 Dateien
  (Limit `MaxDirectoryChildren` 30) — Luft für EPIC-7 unverändert groß.
- **Residenter MCP-Server:** `reload_config` war auch diesmal nötig, bevor
  `find_symbol`/`metrics_lookup` die neue Datei auflösen konnten.

## Bekannte Unschärfen

- **Autorenschaft geteilt:** Implementierung durch die Vorgänger-Coder-
  Instanz, Gate/Sichtung/Doku durch diese Instanz (beide
  `coded_by_model: stealth/ox-alpha`, Knowledge-Cutoff beider unbekannt).
  Ob der Vorgänger die MCP-Zusatzchecks bereits selbst fuhr, ist nicht
  überliefert — ich habe sie reproduziert (alle grün, siehe oben).
- **Byte-Identität des `callers`-Modus** ist nicht per Alt/Neu-Diff belegt,
  sondern strukturell begründet (No-op-Cap, additive Record-Member mit
  Defaults, unveränderte Snapshot-/Subprozess-Tests grün).
- **ct-Bindung (D.7)** ist wie im Plan vorgesehen strukturell review-bar
  (kein dedizierter Abbruchtest — Flakiness-Vermeidung laut Plan-Begründung).
