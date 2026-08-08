---
status: done
type: step-plan
task: metrics-tree
step: 001
corrects: null
title: "metrics_tree: Walk-Kern-Extraktion + code_size/comment_density-Modi + ASCII-Renderer + Tool"
epic: EPIC-01
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-08
related_to: []
---

# Step 001: metrics_tree — Datei-Walk-Modi (EPIC-01)

## Bezug

- **Task:** `metrics-tree`
- **Epic:** `EPIC-01` aus `roadmap.md` — kompletter Block: Walk-Kern-Extraktion,
  neues Tool `metrics_tree` mit den zwei Datei-Walk-Modi `code_size` und
  `comment_density`, gemeinsamer ASCII-Tree-Renderer, Input-Parameter
  (`root`/`mode`/`depth`/`top_n`/`file_filter`), Sufficiency-/Drill-down-Hinweis,
  Tool-Registrierung, Tests. Erster Step im Task — es gibt noch keinen
  vorherigen Step, EPIC-01 wird komplett in diesem einen Step umgesetzt (siehe
  „Risikoabwägung" unten, warum trotz Größe kein Split).
- **Konzept-Referenz:** `konzept.md` „Muss-Haben" (alle Punkte außer den zwei
  Roslyn-Modi) + „Hinweis zur Umsetzungsgranularitaet" Block 1 + „Entdeckte
  Mängel/Redundanzen" (Walk-Kern-Extraktion aus `GetHotspotsScanner.cs`) +
  „Wo im Projekt".

## Aktueller Projektzustand (JIT-Kontext)

Gelesen: `FileStructureToolRegistrations.cs`, `GetHotspotsTool.cs` +
`GetHotspotsScanner.cs`, `AnalysisToolRegistrations.cs`, `McpToolResults.cs`,
`McpSufficiencyHints.cs`, `SourceFileCatalog.cs`, `StructureMapBuilder.cs`
(CLI-Referenz), `GetHotspotsToolTests.cs`, `FindSymbolTool.cs` (Validierungs-
Pattern), `SymbolGraphCatalogFixture.cs` + `SymbolGraphMiniFixtureWorkspace.cs`
(Test-Fixture).

Wichtigste Befunde, die diesen Plan prägen:

1. **Tool/Scanner-Split ist zwingendes Projekt-Muster** (nicht optional):
   `GetHotspotsTool` (dünner Dispatch: `LoadState`-Check, `GetCurrentSolution()`,
   Aufruf `GetHotspotsScanner.BuildHotspotsText`, `FindSymbolTool.BuildAggregateWarningAsync`
   für den Compile-Fehler-Hinweis) vs. `GetHotspotsScanner` (reine Scan-/
   Formatierungslogik, keine `McpCodeGraphServer`-Abhängigkeit → direkt
   unit-testbar). `metrics_tree` übernimmt exakt dieses Muster.
2. **Walk-Kern in `GetHotspotsScanner.cs` (Zeile 51-93):** `CollectFiles`
   (iteriert `solution.Projects` → `project.Documents`, filtert über
   `SourceFileCatalog.IsValidDocument` + `MatchesScope`), `MatchesScope`
   (Projekt-Name-Substring ODER Pfad-Substring, beide `OrdinalIgnoreCase`),
   `TryCountLines` (liest Datei, fängt `IOException` ab, gibt `null` zurück).
   Das ist exakt der Kern, den `konzept.md` extrahiert sehen will — wird nach
   `SolutionFileWalker.cs` verschoben und generalisiert (Regex-`fileFilter`
   zusätzlich zu `scopeFilter`, `TryReadAllLines` statt nur `TryCountLines`
   fürs Comment-Zählen). `GetHotspotsScanner` wird auf den neuen Walker
   umgestellt (kein Verhaltensunterschied, nur Ort der Logik) — bestehende
   `GetHotspotsToolTests.cs` sind die Regressionsabsicherung dafür.
3. **`McpToolResults`:** `SolutionNotLoaded()`, `Loading()`, `Text()` fertig
   nutzbar. Für Validierungsfehler mit tool-spezifischer Message/Hint wird
   **nicht** die generische `McpToolResults.InvalidArgument(message)`-Kurzform
   verwendet (deren Hint ist hart auf `gitRef`/`symbolIdentifier` gemünzt),
   sondern direkt `McpToolResults.Recoverable(LinterErrorCodes.InvalidArgument, message, hint: ...)`
   — identisches Muster wie in `FindSymbolTool.ExecuteAsync` (Zeile 53-67) für
   `namePattern`/`kind`-Validierung.
4. **Sufficiency-Hinweis-Bausteine sind gegenläufig, nicht wiederverwendbar:**
   `McpSufficiencyHints.CompleteDataHint` sagt „Daten vollständig, kein
   Read/Grep nötig" — passt nicht zu `metrics_tree`, dessen Output *per
   Definition* nie vollständig ist (immer Top-N, nie alle Kinder). Neuer,
   eigener Hinweistyp `McpDrillDownHints` (siehe `codemap.md`-Eintrag zu
   `McpSufficiencyHints.cs`), sibling-Datei, gleiches Kurz-Prinzip (ein
   einheitlicher Text, keine tool-spezifischen Varianten).
5. **Kein bestehender ASCII-Tree-Renderer im Projekt** (geprüft:
   `src/AiNetLinter/Maps/**` enthält keine Baum-Zeichnung, nur Tabellen —
   `StructureMapBuilder` z. B. nutzt flache Markdown-Tabellen mit
   Verzeichnis-Gruppierung). `MetricsTreeRenderer` ist folglich komplett neu,
   nicht wiederverwendet — das ist kein Widerspruch zu einem CodeMap-Eintrag,
   da keiner existiert.
6. **Registrierungsort:** `metrics_tree` gehört nach `FileStructureToolRegistrations.cs`
   (nicht `AnalysisToolRegistrations.cs`), weil die zwei Modi dieses Steps
   reine Datei-Walks sind (kein `LinterEngine`-Pull-in) — passt zur
   dokumentierten Trennung (`AnalysisToolRegistrations`-Kommentar: dorthin
   ausgelagert wegen `LinterEngine`-Footprint-Druck). Für EPIC-02
   (`violation_density`/`complexity`, ziehen `LinterEngine` nach) muss der
   nächste Step prüfen, ob `FileStructureToolRegistrations`s Footprint das
   noch verträgt oder ob eine Verschiebung nötig wird — das ist explizit
   **nicht** Teil dieses Steps.
7. **Test-Fixture:** `SymbolGraphCatalogFixture`/`SymbolGraphMiniFixtureWorkspace`
   liefert eine Mini-Solution unter `src/SymbolGraphMini/` mit `Greeter.cs`
   + `Caller.cs` (bereits von `GetHotspotsToolTests` genutzt) — Basis für die
   `metrics_tree`-Tests dieses Steps, keine neue Fixture nötig.

**Bewusste Design-Entscheidungen für diesen Step** (nicht in `konzept.md`
vorgegeben, hier getroffen):

- **`root`-Semantik:** Pfad relativ zum Solution-Verzeichnis (Forward- oder
  Backslash, wie bei `get_file_skeleton`s `filePath`), Default = Solution-
  Verzeichnis selbst. Filterung erfolgt als Pfad-**Präfix** auf die bereits
  vom `SolutionFileWalker` gesammelten relativen Pfade — bewusst **nicht**
  dasselbe wie `scopeFilter` bei `get_hotspots` (Substring irgendwo im Pfad
  ODER Projekt-Name): `root` ist hierarchisch gemeint (Baum-Startknoten), ein
  Substring-Treffer mitten im Pfad wäre für einen Baum-Einstiegspunkt
  irreführend.
- **`file_filter`:** zusätzlicher, unabhängiger Regex-Filter auf den
  relativen Pfad (case-insensitive) — kombinierbar mit `root` (UND-Verknüpft,
  nicht ODER).
- **Sortierrichtung pro Modus** (für Top-N je Ebene):
  - `code_size`: absteigend nach LoC-Summe (größte Knoten zuerst — analog zu
    `get_hotspots`, wo große Dateien das Signal sind).
  - `comment_density`: **aufsteigend** nach Kommentar-Ratio (niedrigste
    Ratio zuerst — das ist das eigentliche Risiko-Signal, ein Verzeichnis mit
    hoher Ratio ist unauffällig). Wird im Code-Kommentar/Doku kurz begründet,
    da die Richtung sich vom LoC-Modus unterscheidet und sonst überraschend
    wirkt.
- **Kommentar-Zählung:** bewusst einfache Zeilen-Heuristik (kein vollständiger
  C#-Tokenizer): pro Zeile trim, Block-Kommentar-Status über die Datei
  hinweg mitführen (`/* ... */`, auch mehrzeilig), `//`- und `///`-Zeilen
  zählen als Kommentar. Kein Roslyn-Parse nötig (bewusst schneller Datei-Walk,
  siehe `konzept.md` „Zielplattformen" — Roslyn ist den zwei EPIC-02-Modi
  vorbehalten). Leerzeilen zählen weder als Code- noch als Kommentarzeile.
- **Baum-Tiefe vs. Top-N:** `depth` begrenzt, wie viele Verzeichnisebenen
  überhaupt aggregiert/aufgebaut werden (1-5). `top_n` begrenzt pro Ebene,
  wie viele Kind-Knoten im Output erscheinen (Rest wird als Zahl in einer
  „... und N weitere" -Zeile zusammengefasst, kein stillschweigendes
  Weglassen).

## Intention

Nach diesem Step ist `metrics_tree` über MCP mit den zwei Datei-Walk-Modi
`code_size` und `comment_density` aufrufbar, liefert einen ASCII-Baum mit
aggregierten Werten pro Verzeichnisknoten und sortierten Top-N-Kindern, und
der zugrunde liegende Walk-Kern ist aus `GetHotspotsScanner` extrahiert statt
dupliziert. Der `MetricsTreeRenderer` ist so gebaut, dass EPIC-02 ihn ohne
Änderung für `violation_density`/`complexity` wiederverwenden kann (reine
`MetricsTreeNode`-Baumstruktur als Eingabe, kein Wissen über die Herkunft der
Werte).

## Konkrete Änderungen

### Datei 1 (neu): `src/AiNetLinter/Mcp/Tools/SolutionFileWalker.cs`

- **Was:** Extrahiert `CollectFiles`/`MatchesScope`/`TryCountLines` aus
  `GetHotspotsScanner.cs` in eine eigenständige, wiederverwendbare Klasse.
  Generalisiert um:
  - `internal readonly record struct WalkedFile(string RelativePath, string AbsolutePath)`
  - `internal static List<WalkedFile> CollectFiles(Solution solution, string solutionDir, string? scopeFilter, Regex? fileFilter = null)`
    — wie bisher `MatchesScope`, zusätzlich (falls `fileFilter != null`)
    `fileFilter.IsMatch(relativePath)`.
  - `internal static bool MatchesScope(Document document, string solutionDir, string? scopeFilter)`
    — unverändert aus `GetHotspotsScanner` übernommen (identisches Verhalten,
    nur Sichtbarkeit/Ort geändert).
  - `internal static string[]? TryReadAllLines(string path)` — ersetzt
    `TryCountLines` (liefert jetzt die Zeilen selbst statt nur die Anzahl,
    weil `comment_density` den Inhalt braucht; `code_size` nutzt `.Length`
    auf dem Ergebnis). Gleiches `try/catch (IOException) → null`-Verhalten.
- **Warum:** Zentrale Umsetzung von `konzept.md` „Entdeckte Mängel/
  Redundanzen" — ein Walk-Kern statt zwei unabhängiger Implementierungen.

### Datei 2 (geändert): `src/AiNetLinter/Mcp/Tools/GetHotspotsScanner.cs`

- **Was:** `CollectFiles`/`MatchesScope`/`TryCountLines` entfernen, stattdessen
  `SolutionFileWalker.CollectFiles(solution, solutionDir, scopeFilter)` +
  `SolutionFileWalker.TryReadAllLines(path)?.Length` verwenden. Rest der
  Datei (Schwellwert-Konstanten, `BuildHotspotsText`, `FormatReport`,
  `AppendSection`, `HotspotFileInfo`) bleibt unverändert.
- **Warum:** Kein Verhaltensunterschied für `get_hotspots` (bestehende
  `GetHotspotsToolTests.cs` müssen unverändert grün bleiben — das ist der
  Regressionstest für diese Umstellung), aber keine zweite Walk-
  Implementierung mehr im Projekt.

### Datei 3 (neu): `src/AiNetLinter/Mcp/Tools/MetricsTreeMode.cs`

- **Was:** Kleines Enum + Parser, bewusst nur mit den zwei in diesem Step
  implementierten Werten (kein Platzhalter für `violation_density`/
  `complexity` — die kommen mit EPIC-02 als Erweiterung dieser Datei, kein
  spekulativer Dead-Code jetzt):
  ```csharp
  internal enum MetricsTreeMode
  {
      CodeSize,
      CommentDensity,
  }

  internal static class MetricsTreeModeParser
  {
      internal static MetricsTreeMode? TryParse(string mode) => mode switch
      {
          "code_size" => MetricsTreeMode.CodeSize,
          "comment_density" => MetricsTreeMode.CommentDensity,
          _ => null,
      };
  }
  ```
- **Warum:** Eigene, kleine Datei statt Enum in `MetricsTreeTool`/-`Scanner`
  versteckt — beide brauchen den Typ, vermeidet zirkuläre Verantwortlichkeit.

### Datei 4 (neu): `src/AiNetLinter/Mcp/Tools/MetricsTreeRenderer.cs`

- **Was:** Modus-agnostischer ASCII-Tree-Renderer + gemeinsamer Node-Typ:
  ```csharp
  internal sealed record MetricsTreeNode(
      string Name,
      string RelativePath,
      int FileCount,
      double SortValue,
      string DisplayLine,
      IReadOnlyList<MetricsTreeNode> Children);

  internal static class MetricsTreeRenderer
  {
      internal static string Render(MetricsTreeNode root, int topN, bool sortDescending)
      {
          var sb = new StringBuilder();
          sb.AppendLine($"{root.Name} — {root.DisplayLine}");
          RenderChildren(sb, root.Children, "", topN, sortDescending);
          return sb.ToString().TrimEnd();
      }

      private static void RenderChildren(
          StringBuilder sb, IReadOnlyList<MetricsTreeNode> children, string prefix,
          int topN, bool sortDescending)
      {
          var sorted = sortDescending
              ? children.OrderByDescending(c => c.SortValue).ToList()
              : children.OrderBy(c => c.SortValue).ToList();
          var visible = sorted.Take(topN).ToList();

          for (var i = 0; i < visible.Count; i++)
          {
              var isLast = i == visible.Count - 1 && visible.Count == sorted.Count;
              AppendNodeLine(sb, visible[i], prefix, isLast);
              var childPrefix = prefix + (isLast ? "    " : "│   ");
              RenderChildren(sb, visible[i].Children, childPrefix, topN, sortDescending);
          }

          if (sorted.Count > visible.Count)
          {
              sb.AppendLine($"{prefix}└── ... und {sorted.Count - visible.Count} weitere");
          }
      }

      private static void AppendNodeLine(StringBuilder sb, MetricsTreeNode node, string prefix, bool isLast)
      {
          var branch = isLast ? "└── " : "├── ";
          sb.AppendLine($"{prefix}{branch}{node.Name} — {node.DisplayLine}");
      }
  }
  ```
  (Feinschliff bei „... und N weitere" vs. letztes-Element-Präfix obliegt dem
  Coder — Kernidee: Top-N pro Ebene, Rest als Zahl, kein stillschweigendes
  Weglassen.)
- **Warum:** Von `metrics_tree`s Scannern (Datei 5) UND EPIC-02s Roslyn-
  Scannern gemeinsam nutzbar — reines Formatierungsproblem über einer
  bereits aggregierten Baumstruktur, kennt weder `Solution` noch die
  Modus-Herkunft der Werte (`DisplayLine` ist vorformatiert).

### Datei 5 (neu): `src/AiNetLinter/Mcp/Tools/MetricsTreeScanner.cs`

- **Was:** Walk + Aggregation für die zwei Datei-Modi dieses Steps. Grober
  Aufbau:
  ```csharp
  internal static class MetricsTreeScanner
  {
      internal static string BuildTree(
          Solution solution, string? root, MetricsTreeMode mode, int depth, int topN, Regex? fileFilter)
      {
          var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
          var rootRelative = NormalizeRoot(root); // "" fuer Default (Solution-Root)

          var files = SolutionFileWalker.CollectFiles(solution, solutionDir, scopeFilter: null, fileFilter);
          var scoped = files.Where(f => f.RelativePath.StartsWith(rootRelative, StringComparison.OrdinalIgnoreCase)).ToList();

          if (scoped.Count == 0)
          {
              return $"Keine Dateien unter root='{rootRelative}'" +
                     (fileFilter != null ? $" mit file_filter" : "") + " — Pfad/Filter pruefen.";
          }

          var metrics = mode == MetricsTreeMode.CodeSize
              ? scoped.Select(ComputeCodeSizeMetric).ToList()
              : scoped.Select(ComputeCommentDensityMetric).ToList();

          var tree = BuildTreeFromFlatList(metrics, rootRelative, depth, mode);
          var sortDescending = mode == MetricsTreeMode.CodeSize;
          return MetricsTreeRenderer.Render(tree, topN, sortDescending);
      }

      private sealed record FileMetric(string RelativePath, double SortValue, int CommentLines, int CodeLines, long Bytes);

      private static FileMetric ComputeCodeSizeMetric(SolutionFileWalker.WalkedFile f) { /* Zeilen + FileInfo.Length */ }
      private static FileMetric ComputeCommentDensityMetric(SolutionFileWalker.WalkedFile f) { /* Kommentar-Heuristik, siehe unten */ }

      private static MetricsTreeNode BuildTreeFromFlatList(
          List<FileMetric> metrics, string rootRelative, int depth, MetricsTreeMode mode)
      { /* gruppiert nach Verzeichnis-Segmenten relativ zu rootRelative, bis maximal `depth` Ebenen;
           Dateien/Unterverzeichnisse jenseits von depth werden in den letzten sichtbaren Knoten
           aufaggregiert (FileCount/SortValue-Summe), nicht abgeschnitten */ }

      private static string FormatDisplayLine(MetricsTreeMode mode, int fileCount, double sortValue, int commentLines, int codeLines)
      { /* z.B. "12 Dateien | 1.480 LoC" bzw. "8 Dateien | 23% Kommentaranteil (340/1.480 Zeilen)" */ }

      private static (int CommentLines, int CodeLines) CountCommentLines(string[] lines)
      { /* Block-Kommentar-Status ueber die Datei mitfuehren, siehe Design-Entscheidung oben */ }
  }
  ```
  Falls `MetricsTreeScanner.cs` durch die Aggregationslogik über ~350-400
  Zeilen wächst (MaxLineCount 500 im Auge behalten): `CountCommentLines`
  + `FormatDisplayLine` in eine eigene `CommentLineCounter.cs` auslagern —
  Entscheidung dem Coder überlassen, abhängig von der tatsächlichen Zeilen-
  zahl nach Implementierung (Konzept-Vorgabe „große Blöcke" heißt nicht
  „Regeln ignorieren").
- **Warum:** Kapselt die eigentliche Aggregationslogik getrennt vom dünnen
  Tool-Dispatch (Datei 6) — identisches Muster zu `GetHotspotsScanner`.

### Datei 6 (neu): `src/AiNetLinter/Mcp/Tools/MetricsTreeTool.cs`

- **Was:** Dünner Dispatch, Validierung, Sufficiency-/Drill-down-Hinweis:
  ```csharp
  internal static class MetricsTreeTool
  {
      internal static async Task<CallToolResult> ExecuteAsync(
          McpCodeGraphServer state, string? root, string mode, int depth, int topN,
          string? fileFilter, CancellationToken ct)
      {
          if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
          var solution = state.GetCurrentSolution();
          if (solution is null) return McpToolResults.SolutionNotLoaded();

          var parsedMode = MetricsTreeModeParser.TryParse(mode);
          if (parsedMode is null)
          {
              return McpToolResults.Recoverable(LinterErrorCodes.InvalidArgument,
                  $"Unbekannter mode '{mode}'.",
                  hint: "Gueltige Werte in dieser Version: code_size, comment_density.");
          }

          if (depth is < 1 or > 5)
          {
              return McpToolResults.Recoverable(LinterErrorCodes.InvalidArgument,
                  "depth muss zwischen 1 und 5 liegen.", hint: "depth anpassen.");
          }

          if (topN < 1)
          {
              return McpToolResults.Recoverable(LinterErrorCodes.InvalidArgument,
                  "top_n muss mindestens 1 sein.", hint: "top_n anpassen.");
          }

          Regex? filterRegex = null;
          if (!string.IsNullOrWhiteSpace(fileFilter))
          {
              try { filterRegex = new Regex(fileFilter, RegexOptions.IgnoreCase); }
              catch (ArgumentException ex)
              {
                  return McpToolResults.Recoverable(LinterErrorCodes.InvalidArgument,
                      $"file_filter ist kein gueltiger regulaerer Ausdruck: {ex.Message}",
                      hint: "Regex-Syntax pruefen.");
              }
          }

          var text = MetricsTreeScanner.BuildTree(solution, root, parsedMode.Value, depth, topN, filterRegex);
          var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
          var withHint = McpDrillDownHints.Append(text, depth);
          return McpToolResults.Text(FindSymbolTool.PrependWarning(warning, withHint));
      }
  }
  ```
- **Warum:** Identisches Dispatch-Muster zu `GetHotspotsTool` — Validierung
  vor dem Scanner-Aufruf (analog `FindSymbolTool`), Compile-Fehler-Warnung
  per bereits bestehendem `FindSymbolTool.BuildAggregateWarningAsync`
  wiederverwendet (kein neuer Mechanismus).

### Datei 7 (neu): `src/AiNetLinter/Mcp/McpDrillDownHints.cs`

- **Was:** Sibling zu `McpSufficiencyHints.cs`, gegenläufiger Hinweistyp:
  ```csharp
  internal static class McpDrillDownHints
  {
      internal static string Append(string text, int depth)
      {
          return text + "\n\n" +
              $"[HINWEIS]: Dies zeigt Ebene 1-{depth} ab dem angefragten root — " +
              "Top-N-Ausschnitt, nicht vollstaendig. Fuer tiefere Details: root auf einen " +
              "der angezeigten Kind-Pfade setzen und/oder depth erhoehen.";
      }
  }
  ```
- **Warum:** `konzept.md`/`codemap.md` fordern explizit einen eigenen,
  neuen Hinweistyp statt `McpSufficiencyHints.CompleteDataHint`
  wiederzuverwenden, da `metrics_tree`-Output nie „vollständig" im Sinne
  dieser Klasse ist.

### Datei 8 (geändert): `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs`

- **Was:** `AddMetricsTree` ergänzen (analog `AddGetHotspots`), Aufruf in
  `Register(...)` ergänzen, XML-Doc-Kommentar der Klasse um `metrics_tree`
  erweitern:
  ```csharp
  private static void AddMetricsTree(
      McpServerPrimitiveCollection<McpServerTool> tools,
      McpCodeGraphServer mcpState,
      McpCallLog? callLog)
  {
      tools.Add(McpServerTool.Create(
          async (string? root, string mode, int depth = 1, int topN = 10, string? fileFilter = null, CancellationToken ct = default) =>
          {
              if (callLog is null)
              {
                  return await MetricsTreeTool.ExecuteAsync(mcpState, root, mode, depth, topN, fileFilter, ct);
              }
              return await callLog.ExecuteCallAsync("metrics_tree", $"{root}|{mode}|{depth}|{topN}|{fileFilter}",
                  () => MetricsTreeTool.ExecuteAsync(mcpState, root, mode, depth, topN, fileFilter, ct));
          },
          new McpServerToolCreateOptions
          {
              Name = "metrics_tree",
              Description = MetricsTreeDescription,
          }));
  }

  private const string MetricsTreeDescription =
      "Wann nutzen: Verzeichnishierarchie einer unbekannten/grossen Codebase Ebene fuer Ebene " +
      "erkunden statt Komplett-Dump zu lesen — aggregierte Werte pro Knoten + sortierte " +
      "Top-N-Kinder. mode in dieser Version: code_size, comment_density (weitere Modi folgen). " +
      "root grenzt auf einen Teilbaum ein (Default: Solution-Root), depth (1-5) begrenzt die " +
      "Baumtiefe, top_n die sichtbaren Kinder pro Ebene, file_filter (Regex) auf den Pfad.";
  ```
- **Warum:** Registrierungspunkt für dateistruktur-orientierte Tools, siehe
  `codemap.md`-Eintrag — `metrics_tree` reiht sich als viertes Tool ein.

## Tests

- [ ] `GetHotspotsScannerTests`/`GetHotspotsToolTests` (bestehend) bleiben
      unverändert grün — Regressionsabsicherung für die `SolutionFileWalker`-
      Extraktion (kein neuer Test nötig, nur Ausführung im gefilterten Lauf).
- [ ] Neue Datei `src/AiNetLinter.Tests/Mcp/Tools/MetricsTreeToolTests.cs`
      (`[Trait("Category","Unit")]`, `[Collection("SymbolGraphCatalog")]`
      analog `GetHotspotsToolTests`):
  - [ ] `ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode`
  - [ ] `ExecuteAsync_UnknownMode_ReturnsInvalidArgument`
  - [ ] `ExecuteAsync_DepthOutOfRange_ReturnsInvalidArgument` (0 und 6 als Theory-Fälle)
  - [ ] `ExecuteAsync_TopNZeroOrNegative_ReturnsInvalidArgument`
  - [ ] `ExecuteAsync_InvalidFileFilterRegex_ReturnsInvalidArgument`
  - [ ] `ExecuteAsync_CodeSizeMode_ReturnsTreeSortedByLocDescending`
  - [ ] `ExecuteAsync_CommentDensityMode_ReturnsTreeSortedByRatioAscending`
  - [ ] `ExecuteAsync_RootNotMatchingAnyFile_ReturnsExplicitEmptyMessage` (leeres
        Verzeichnis / Edge-Case aus Definition of Done)
  - [ ] `ExecuteAsync_RootPointingToSingleFile_ReturnsSingleNodeTree` (single-file
        Edge-Case)
  - [ ] `ExecuteAsync_MaxDepth_DoesNotThrowAndClampsGracefully` (depth=5 Edge-Case
        gegen die flache Fixture-Struktur — verifiziert, dass ein depth über
        der tatsächlichen Verzeichnistiefe nicht crasht, nicht dass 5 Ebenen
        tatsächlich befüllt sind)
  - [ ] `ExecuteAsync_FileFilterExcludesMatchingFiles_NarrowsTree`
  - [ ] `ExecuteAsync_ContainsDrillDownHint`
- [ ] Neue Datei `src/AiNetLinter.Tests/Mcp/Tools/MetricsTreeRendererTests.cs`
      (reine Formatierungstests ohne Solution/Fixture — konstruiert
      `MetricsTreeNode`-Bäume von Hand):
  - [ ] `Render_SortsChildrenDescending_WhenRequested`
  - [ ] `Render_SortsChildrenAscending_WhenRequested`
  - [ ] `Render_TopNLimitsVisibleChildren_AndAppendsRemainingCount`
  - [ ] `Render_NestedChildren_ProducesCorrectIndentation`

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt (Dateien 1-8)
- [ ] `dotnet build` (TreatWarningsAsErrors) grün
- [ ] Gezielter Testlauf grün: `dotnet test --filter Category=Unit` (kein
      Volllauf — siehe `roadmap.md` Tech-Stack-Notiz, abweichendes Gate für
      diesen gesamten Task)
- [ ] Commit auf aktuellem Branch (Conventional Commit, Deutsch, imperativ)
- [ ] `tasks/metrics-tree/step-001/step-result.md` geschrieben
- [ ] `tasks/metrics-tree/codemap.md` um die neuen/geänderten Dateien ergänzt
      (Coder-Pflicht vor dem Commit, siehe `codemap.md` „Pflege")
- [ ] `status` in `step-plan.md` von `open` über `in_progress` auf
      `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` §„Kurz-Stil"/§„Grenzwerte" — `sealed` für
  konkrete Klassen (alle neuen Klassen hier sind `static`, also nicht
  betroffen, außer `MetricsTreeNode`/`FileMetric`/`WalkedFile`: als `record`/
  `record struct` bereits automatisch sealed-äquivalent), `#nullable enable`
  je Datei, `MaxLineCount` 500 (siehe Hinweis zu `MetricsTreeScanner.cs`
  oben), `MaxMethodLineCount` 60, `MaxCyclomaticComplexity` 12/
  `MaxCognitiveComplexity` 15 (v. a. `CountCommentLines`/`BuildTreeFromFlatList`
  im Auge behalten — bei Bedarf in kleinere Methoden aufteilen),
  `AIContextFootprint` 2500 transitive Zeilen — bei 8 neuen/geänderten
  Dateien in diesem Bereich gezielt prüfen (`get_hotspots` auf die
  geänderten Dateien nach Fertigstellung).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §1 „Einfachheit vor Abstraktion"
  (Begründung für Walk-Kern-Extraktion), §4 „xUnit v3 Tests: Pflicht für
  jede Logik-Änderung" + „Testsuite-Parallelität bewahren" (keine neue
  `[Collection]`-Zwangsserialisierung ohne Begründung — `MetricsTreeToolTests`
  nutzt dieselbe `SymbolGraphCatalog`-Collection wie `GetHotspotsToolTests`,
  `MetricsTreeRendererTests` braucht gar keine Collection), §5
  „Sparsamer Einsatz von Code-Kommentaren" (Why-Kommentare ja, keine
  Redundanz, keine Task-ID-Referenzen im Code — insbesondere die
  Sortierrichtungs-Begründung für `comment_density` gehört als kurzer
  Why-Kommentar in `MetricsTreeScanner.cs`, nicht nur hierher in den
  Step-Plan).

## Bekannte Ausnahmen

<keine>

## Notes

- **`Docs/agent-api.md`/`Docs/ROADMAP.md`-Update, Epic-S2.5-Abhaken:**
  bewusst **nicht** Teil dieses Steps — laut `roadmap.md` EPIC-02 gehört das
  dorthin, weil erst nach allen 4 Modi sinnvoll dokumentierbar
  („gehört inhaltlich zu diesem Block, da erst nach Fertigstellung aller
  4 Modi sinnvoll dokumentierbar"). Nicht vergessen, wenn EPIC-02 geplant
  wird.
- **`violation_density`/`complexity` noch nicht im `mode`-Parameter
  akzeptiert** — das ist in diesem Step erwartetes, dokumentiertes Verhalten
  (siehe `MetricsTreeMode.cs`-Begründung oben), kein Bug, den ein späterer
  Kritiker melden sollte. EPIC-02 erweitert `MetricsTreeMode`/
  `MetricsTreeModeParser` um die zwei fehlenden Werte plus einen zweiten
  Scanner (`MetricsTreeRoslynScanner.cs` o. ä.), der denselben
  `MetricsTreeRenderer`/`MetricsTreeNode` aus diesem Step wiederverwendet.
- **`GetHotspotsScanner`-Umstellung ist bewusst Teil dieses Steps, nicht ein
  eigener vorgelagerter Mini-Step** — der Nutzer-Vorgabe „große,
  in sich geschlossene Blöcke, keine Mini-Steps" folgend wird die
  Extraktion zusammen mit ihrer ersten Wiederverwendung (`metrics_tree`)
  umgesetzt statt isoliert vorab.

## Risikoabwägung (warum EPIC-01 als ein Step, nicht mehrere)

`estimated_risk: medium` (nicht `low`): 8 neue/geänderte Dateien, ein
Kern-Refactoring mit bestehendem Regressionsrisiko (`GetHotspotsScanner`),
neuer Renderer ohne Vorbild im Projekt. Trotzdem **ein** Step statt Split,
weil:

- Der Coder ist laut `konzept.md`/Nutzer-Vorgabe explizit leistungsfähig
  genug für diesen Zuschnitt (Referenzgröße `safeguard`/S1.2).
- Die acht Dateien sind **nicht unabhängig voneinander committbar** — ein
  Zwischenzustand (z. B. nur `SolutionFileWalker.cs` + `GetHotspotsScanner`-
  Umstellung ohne `metrics_tree`-Tool) wäre kein sinnvoller, in sich
  geschlossener Meilenstein, sondern nur ein künstlicher Schnitt mitten durch
  ein zusammenhängendes Feature — genau das, was die Umsetzungsgranularitaets-
  Vorgabe vermeiden will.
- Jeder Teil bleibt trotzdem einzeln testbar (Renderer-Tests unabhängig von
  Solution/Fixture, Scanner/Tool-Tests über die Fixture, Hotspot-Regression
  über bestehende Tests) — die Test-Abdeckung wird durch die Bündelung nicht
  reduziert (siehe `konzept.md` „das ist keine Lockerung").
