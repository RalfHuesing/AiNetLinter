---
status: done
type: step-result
task: metrics-tree
step: 002
epic: EPIC-01
step_type: batch
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-08
code_commit_hash: 2cdaa7f
status_after: done
blocker_category: n/a
---

# Result Step 002: Korrektur MaxMethodParameterCount (metrics_tree) + TD-002 (EPIC-01)

## Zusammenfassung

Alle drei Items 1:1 wie geplant umgesetzt. item-01: `MetricsTreeScanner.BuildTree` nimmt jetzt
einen `MetricsTreeQuery`-Record statt 6 Einzelparametern. item-02: `MetricsTreeTool.ExecuteAsync`
nimmt einen `MetricsTreeToolArgs`-Record (rohe, ungeparste Werte) statt 6 Einzelparametern; baut
daraus intern den validierten `MetricsTreeQuery` für den Scanner-Aufruf. item-03 (TD-002):
`WalkedFile` aus `SolutionFileWalker.cs` in eigene Datei `WalkedFile.cs` extrahiert. Beide neuen
Records liegen auf Namespace-Ebene (nicht genestet), um `BanPublicNestedTypes` nicht neu
auszulösen.

## Geänderte Dateien

- item-01: `src/AiNetLinter/Mcp/Tools/MetricsTreeScanner.cs` — `MetricsTreeQuery`-Record ergänzt,
  `BuildTree`-Signatur auf `(Solution, MetricsTreeQuery)` umgestellt, Methodenkörper auf
  `query.*`-Zugriffe umgestellt.
- item-02: `src/AiNetLinter/Mcp/Tools/MetricsTreeTool.cs` — `MetricsTreeToolArgs`-Record ergänzt,
  `ExecuteAsync`-Signatur auf `(McpCodeGraphServer, MetricsTreeToolArgs, CancellationToken)`
  umgestellt, baut vor dem Scanner-Aufruf den `MetricsTreeQuery` aus den validierten Werten.
- item-02: `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` — `AddMetricsTree`-Lambda baut
  `MetricsTreeToolArgs` vor beiden `ExecuteAsync`-Aufrufen (CallLog-Zweig + Direkt-Zweig);
  Registrierungs-Lambda selbst bleibt mit benannten Einzelparametern (MCP-Schema-Bindung).
- item-03 (TD-002): `src/AiNetLinter/Mcp/Tools/WalkedFile.cs` (neu) — `WalkedFile`-Record-Struct,
  1:1 aus `SolutionFileWalker.cs` verschoben.
- item-03 (TD-002): `src/AiNetLinter/Mcp/Tools/SolutionFileWalker.cs` — genestete
  `WalkedFile`-Deklaration entfernt, Verwendungsstellen unverändert (gleicher Namespace).
- item-01/item-03: `src/AiNetLinter/Mcp/Tools/MetricsTreeScanner.cs:61,68` —
  `SolutionFileWalker.WalkedFile f` → `WalkedFile f` (Qualifikation entfällt).
- (nicht im Plan, notwendige Konsequenz aus item-02, siehe „Abweichungen"):
  `src/AiNetLinter.Tests/Mcp/Tools/MetricsTreeToolTests.cs` — alle 13 `ExecuteAsync`-Aufrufe auf
  `new MetricsTreeToolArgs(...)` statt 6 Einzelargumente umgestellt.

## Commit

- **Code-Commit-Hash:** `2cdaa7f`
- **Message:**
  ```
  refactor(mcp): Parameter-Records fuer metrics_tree, WalkedFile extrahiert [metrics-tree]

  Behebt zwei MaxMethodParameterCount-Verstoesse aus dem Kritiker-Review
  zu step-001: MetricsTreeScanner.BuildTree buendelt seine fuenf
  Datei-Walk-Parameter jetzt in einem validierten MetricsTreeQuery-Record,
  MetricsTreeTool.ExecuteAsync analog in einem rohen MetricsTreeToolArgs-
  Record (Tool validiert weiterhin, Scanner bekommt nur gepruefte Werte).
  Beide Records liegen bewusst auf Namespace-Ebene, nicht genestet in
  ihrer jeweiligen static class, um denselben BanPublicNestedTypes-Fehler
  nicht neu einzufuehren, den TD-002 gerade behebt: WalkedFile aus
  SolutionFileWalker.cs in eine eigene Datei WalkedFile.cs extrahiert
  (rein mechanisch, keine Verhaltensaenderung).

  Refs: tasks/metrics-tree/step-002
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx                          → grün (0 Warnungen, 0 Fehler)
dotnet test --filter "FullyQualifiedName~MetricsTree"   → grün (17 Tests, 0 Fehler)
dotnet test --filter Category=Unit                      → grün (1191 Tests, 0 Fehler)
get_violations (Scope MetricsTree/SolutionFileWalker/WalkedFile/FileStructureToolRegistrations)
  → keine MaxMethodParameterCount-, keine BanPublicNestedTypes-Verstöße mehr
```

## Abweichungen vom Plan

- **`src/AiNetLinter.Tests/Mcp/Tools/MetricsTreeToolTests.cs` nicht im Plan als zu ändernde Datei
  genannt, aber notwendig angepasst:** Der Plan sagt unter „Tests", die 13 Tests dieser Datei
  „müssen nach der Signaturänderung unverändert grün bleiben, da nur interne Aufrufe/Wiring
  betroffen sind" — das trifft auf ihr *Verhalten* zu, nicht auf ihren *Quelltext*: die Tests rufen
  `MetricsTreeTool.ExecuteAsync` direkt mit den bisherigen 6 Positionsargumenten auf, was nach der
  Signaturänderung in item-02 nicht mehr kompiliert. Alle 13 Aufrufstellen auf
  `new MetricsTreeToolArgs(...)` umgestellt — reine Anpassung an die neue Signatur, keine
  Assertion/kein Testverhalten geändert, keine neuen Tests hinzugefügt. Damit bleibt die Aussage
  „Tests bleiben unverändert grün" korrekt, nur der Call-Syntax musste mitziehen.
- Ansonsten alle drei Items 1:1 wie im Plan (inkl. Code-Snippets) umgesetzt.

## Beobachtungen

- **`AIContextFootprint`-Warnungen auf `MetricsTreeTool.cs` (2536 > 2500) und
  `FileStructureToolRegistrations.cs` (2895 > 2890) weiterhin aktiv** — beide bereits vor diesem
  Step als TD-001 erfasst (`tasks/metrics-tree/tech-debt.md`), explizit außerhalb des Scopes dieses
  Steps (Notes-Abschnitt im Plan). Die neuen `internal sealed record`-Typen dieses Steps haben den
  Wert auf `MetricsTreeTool.cs` minimal erhöht (vorher laut TD-001-Eintrag 2532, jetzt 2536) —
  bleibt aber dieselbe TD-001-Ursache (Config-Override-Kette), kein neues, eigenständiges Problem.
  Kein Handlungsbedarf in diesem Step, nur zur Sichtbarkeit für den Kritiker.

## Bekannte Unschärfen

- Keine über die in `step-001/step-result.md` bereits dokumentierten hinaus (Sortier-Tie-Breaking,
  Kommentar-Heuristik) — dieser Step hat daran nichts verändert.
