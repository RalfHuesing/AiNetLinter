---
status: done
type: step-result
task: codegraph-mcp
step: 006
epic: EPIC-03
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-07-31T15:10:00Z
code_commit_hash: c125511
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 006: get_file_skeleton Tool (Struktur-Skelett einer einzelnen Datei via SkeletonMapBuilder)

## Zusammenfassung

Alle sechs im Plan beschriebenen Dateien umgesetzt: `get_file_skeleton` ist
ein duenner Dispatch — loest `filePath` (relativ oder absolut) ueber
`Path.GetFullPath(Path.Combine(solutionDir, filePath))` +
`DiffImpactAnalyzer.FindDocumentByPath` zu einem `Document` auf, ruft die
neu auf `internal static` angehobene
`SkeletonMapBuilder.ExtractFromDocumentAsync` mit einem Default-`LinterArgs`
auf und rendert das Ergebnis unveraendert mit
`SkeletonMarkdownRenderer.Render`. Neuer Fehler-Helper
`McpToolResults.FileNotFound` nutzt den bereits existierenden
`RESOURCE_NOT_FOUND`-Code (kein vierter Fehlercode). Registrierung als
viertes Tool in `McpServerOptionsFactory`. Bestehender E2E-Tool-Zaehl-Test
auf vier Tools angepasst, neuer E2E-Subprozess-Test fuer
`get_file_skeleton` ergaenzt.

Footprint-Selbst-Lint (Pflicht-Verifikation laut Plan) zeigte, dass beide
im Plan als Risiko benannten Klassen unter dem 2500-Zeilen-Limit blieben
(`McpServerOptionsFactory`: 2480, `GetFileSkeletonTool`: 2428) — die
dokumentierten Ausweich-Stufen (Aufteilung von `BuildToolCollection`,
`rules.json`-Override) waren nicht noetig.

## Geänderte Dateien

- `src/AiNetLinter/Maps/Skeleton/SkeletonMapBuilder.cs` — `ExtractFromDocumentAsync` von `private static` auf `internal static` angehoben, Xml-Doc ergaenzt, keine Logikaenderung.
- `src/AiNetLinter/Mcp/McpToolResults.cs` — `FileNotFound(string relativePath)` ergaenzt.
- `src/AiNetLinter/Mcp/Tools/GetFileSkeletonTool.cs` (neu) — `ExecuteAsync`.
- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` — vierter `tools.Add(...)`-Aufruf fuer `get_file_skeleton`.
- `src/AiNetLinter.Tests/Mcp/Tools/GetFileSkeletonToolTests.cs` (neu) — vier Tests gemaess Plan-Testliste.
- `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` — Test umbenannt zu `RunAsync_ValidFixture_ServerRespondsWithFourTools`, Assertion auf vier Tools inkl. `get_file_skeleton` erweitert; neuer E2E-Test `RunAsync_ValidFixture_GetFileSkeletonReturnsGreeterSignature`.

## Commit

- **Code-Commit-Hash:** `c125511`
- **Message:**
  ```
  feat(mcp): add get_file_skeleton tool for single-file structure dump [codegraph-mcp]

  Dispatches to the already per-document SkeletonMapBuilder.ExtractFromDocumentAsync
  (newly internal) + SkeletonMarkdownRenderer.Render to expose a single .cs file's
  type/member skeleton without a whole-repo dump, reusing FindDocumentByPath and the
  existing RESOURCE_NOT_FOUND error code.

  Refs: tasks/codegraph-mcp/step-006
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx → gruen, 0 Warnungen
dotnet test AiNetLinter.slnx  → gruen (1056 Tests, 0 Fehler)
ainetlinter --config rules.json --path . → OK, 0 Violations
--footprint McpServerOptionsFactory → 2480 (Limit 2500)
--footprint GetFileSkeletonTool     → 2428 (Limit 2500)
```

## Dogfooding

Ausgefuehrt wie im DoD gefordert: gebautes `AiNetLinter.exe` per
`StdioClientTransport` (identisches Verbindungsmuster wie
`McpServerCommandTests`) als `--mcp-server --path
C:\Daten\Entwicklung\Ralf\AiNetLinter` gestartet (echtes Repo-Root, kein
Fixture) und `get_file_skeleton` per MCP-Client mit
`filePath = "src/AiNetLinter/Mcp/Tools/GetImpactTool.cs"` aufgerufen.
Client-Code lag in einem Scratch-Projekt (`ModelContextProtocol`-Client-
Package, nicht Teil des Repos, nicht committet).

Ergebnis: `IsError: false` (leer/falsy), kein Hang/Timeout — Antwort kam
sofort. Der zurueckgegebene Markdown-Text enthielt exakt die tatsaechliche
Struktur der realen Datei: Klasse `GetImpactTool` (`static`) mit allen drei
Methoden-Signaturen (`ExecuteAsync`, `ExecuteSymbolBranchAsync`,
`ExecuteGitRefBranchAsync`) inklusive korrekter Parameterlisten, keine
Bodies. Header zeigte korrekt `Typen: 1 | Member: 3 | Pfad:
src/AiNetLinter/Mcp/Tools/GetImpactTool.cs`. Plausibel und deckungsgleich
mit dem tatsaechlichen Dateiinhalt — keine Auffaelligkeit.

## Abweichungen vom Plan

- **Test `ExecuteAsync_AbsolutePath_ResolvesSameAsRelativePath`:** Der Plan
  beschreibt "identisches Ergebnis" fuer relativen vs. absoluten Pfad. Da
  `SkeletonMarkdownRenderer.Render` den unveraendert uebergebenen
  `filePath`-Parameter (nicht den aufgeloesten `absolutePath`) in die
  Kopfzeile schreibt, unterscheidet sich die Kopfzeile ("Pfad: ...")
  zwangslaeufig zwischen relativem und absolutem Aufruf — ebenso koennte
  der `DateTimeOffset.Now`-Zeitstempel bei zwei Aufrufen minimal
  divergieren. Eine Assertion auf vollstaendige String-Gleichheit waere
  daher fragil und wuerde ein Implementierungsdetail (welcher Pfad-String
  in der Kopfzeile landet) pruefen statt der eigentlichen Intention des
  Tests (derselbe Dokument-Typinhalt wird aufgeloest). Stattdessen prueft
  der implementierte Test, dass beide Aufrufe erfolgreich sind und beide
  die `Greet`-Signatur enthalten — das belegt weiterhin, dass
  `Path.Combine` mit bereits absolutem zweitem Argument den ersten
  ignoriert und `FindDocumentByPath` in beiden Faellen dasselbe Dokument
  findet. Keine Code-Logik geaendert, nur eine praezisere Testerwartung.

## Beobachtungen

Keine besonderen Beobachtungen ausserhalb des Plans. Der Fall "Datei
existiert, keine Typen" wurde wie im Plan unter "Bekannte Ausnahmen"
begruendet nicht extra getestet.

## Bekannte Unschärfen

- Wie im Plan unter „Bekannte Ausnahmen" dokumentiert: kein
  `search_pattern`-Fallback fuer nicht-C#-Dateien (EPIC-05, nicht Teil
  dieses Steps).
- Footprint-Werte (2480/2428) liegen nach diesem Step naeher am
  2500-Limit als zuvor (2469 vor diesem Step) — ein weiteres EPIC-03-Tool
  (`get_type_hierarchy`, naechster offener Step) koennte das Limit
  tatsaechlich reissen. Kein eigener Tech-Debt-Eintrag angelegt (bleibt
  dem Kritiker vorbehalten, siehe TD-004/TD-005).
