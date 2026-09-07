# Finding: Kombination `get_file_tree` → `get_index_scope` → `search_pattern`

Audit-Stand: 2026-09-07. Live-Kette auf `targetType=project`, `targetPath=C:\Workspace\Sample.Project`. Nicht-C#-Anker: `wwwroot/js` und `Sample.Project/Components/Admin/Ai/AiChatComposer.razor`. Kein `find_symbol`, kein Build, kein Test.

Frage der Kombination: Schickt `get_index_scope` den Agenten für JS/Razor korrekt zu `search_pattern` statt `find_symbol` — und lassen sich Dateibaum-Pfade ohne Token-Flut und Truncation-Falle weiterreichen?

## 1 Schema-Kurzfazit

Die drei Beschreibungen **stecken die Routing-Absicht grob richtig**:

| Tool | Was die Beschreibung dem Agenten sagt |
|---|---|
| `get_file_tree` | Erster physischer Discovery-Schritt; `fileFilter` ist Pfad-Glob, keine Inhaltssuche; `root` relativ zu `targetPath`. |
| `get_index_scope` | Erster Discovery-Call **vor** `find_symbol`/`search_pattern`; `.cs` im Symbolgraph, `.js`/`.razor`/`.css`/`.xaml` nicht. |
| `search_pattern` | Fallback für Namen/Strings **außerhalb** des C#-Symbolgraphs (explizit JS, Razor, Config). |

Was die Kette **nicht** aus den Schemas ableitet:

- `get_index_scope` nennt `find_symbol` und `search_pattern` **gleichrangig** („vor find_symbol/search_pattern“), ohne Regel „abgedeckt → `find_symbol`, sonst `search_pattern`“. Die Antwortzeilen tun das nur indirekt über den Abdeckungstext.
- Kein Tool beschreibt, **welches Pfadformat** der Nächste erwartet. `get_file_tree` liefert relative Pfade mit Projektpräfix (`Sample.Project/wwwroot/js/…`). `search_pattern.scope` akzeptiert genau dieses Format; den Tree-Kindnamen `wwwroot/js` oder `fileFilter=wwwroot/js` ohne `**/` nicht.
- Keine Kettensemantik zu Truncation: `get_file_tree` warnt nur „root/fileFilter verfeinern“, nicht „die gezeigten Pfade sind Release-Kopien, Quelle folgt nach Truncation“.
- Zählwerte sind **nicht derselbe Index**: Dateibaum scannt Workspace (inkl. `Release/`), Index-Scope eine engere Menge. Schema sagt das nicht.
- `get_index_scope` hat keine IDs, keine Pfade, keinen Folgetool-Hint in der Nutzlast. Der Agent muss Pfade selbst aus `get_file_tree` holen.

`targetType`/`targetPath` sind in allen drei Schemas Pflicht und in dieser Kette unverändert wiederverwendbar. Das ist der einzige wirklich stabile Übergabeblock.

## 2 Calls

Alle schnell (kein Timeout). Größen nach sichtbarer Textantwort.

### Stufe A — `get_file_tree`

| # | Absicht | Parameter | Größe / completeness | Wartezeit |
|---|---|---|---|---|
| A1 | Discovery-Einstieg | `view=summary` | ~24 Ordnerzeilen; 3880 Dateien, 672 MB; `[vollstaendig]`; `.js 204`, `.razor 111` | schnell |
| A2 | JS-Glob naiv wie Workflow | `view=files`, `fileFilter=**/wwwroot/js/**`, `maxResults=50` | **280 gematcht / 50 gezeigt**; **nur** `Release/build/wwwroot/js/…` (inkl. `.br`/`.gz`) | schnell |
| A3 | Razor-Liste | `view=files`, `fileFilter=*.razor`, `maxResults=30` | **111 / 30**; Tests zuerst, dann `…/AiChatComposer.razor` | schnell |
| A4 | Host-Baum | `view=tree`, `root=Sample.Project`, `treeDepth=2` | 410 gematcht / 200 gezeigt; `wwwroot/js/ 12 Dateien` (nur direkte Kinder, `Sample-timelineview/` fehlt in der Tiefe) | schnell |
| A5 | Quell-JS per `root` | `view=files`, `root=Sample.Project/wwwroot/js`, `includeExtensions=[".js"]` | **69 / 50**; echte Host-Quellen, vollständige relative Pfade | schnell |
| A6 | Einzel-Razor | `fileFilter=Sample.Project/Components/Admin/Ai/AiChatComposer.razor` | 1 Treffer, 51 Zeilen, vollständig | schnell |
| A7 | Nach Truncation-Hinweis | `fileFilter=**/wwwroot/js/**`, `excludePatterns=["Release/**"]`, `maxResults=20` | 70 / 20; jetzt Host-Quelle | schnell |

### Stufe B — `get_index_scope`

| # | Absicht | Parameter | Größe / completeness | Wartezeit |
|---|---|---|---|---|
| B1 | Abdeckung | nur Target | 19 Zeilen, keine Truncation. `.cs: 2287 (voll … abgedeckt)`; `.js: 71 (nicht …)`; `.razor: 111 (nicht …)` | schnell |

Kein Pfad, keine Symbol-ID, kein Hinweis auf `search_pattern` in der Antwort.

Zählvergleich A1 vs. B1 (Stichprobe): `.cs` 2287=2287, `.razor` 111=111, `.xaml` 15=15. **`.js` 204 vs. 71**, `.css` 80 vs. 61, `.md` 178 vs. 52, `.sql` 246 vs. 98. Index-Scope ist enger als der Dateibaum.

### Stufe C — `search_pattern` (Pfad aus A / Routing aus B)

| # | Absicht | Parameter | Größe / completeness | Wartezeit |
|---|---|---|---|---|
| C1 | JS mit Baum-`root`-Pfad | `pattern=invokeMethodAsync`, `scope=Sample.Project/wwwroot/js`, `fileFilter=*.js`, `scopeType=production` | **35 / 20**; nur Host-Quelle | schnell |
| C2 | Razor-Einzelpfad aus A3/A6 | `pattern=MudTextField`, `fileFilter=…/AiChatComposer.razor` | 1 Treffer, Zeile 19 | schnell |
| C3 | Truncation-Falle A2 | `scope=Release/build/wwwroot/js` | **39 / 10**; Release-Kopien + README; 137 binäre übersprungen | schnell |
| C4 | Kindname als `fileFilter` | `fileFilter=wwwroot/js` (ohne `**/`) | **0 Treffer**, kein `isError` | schnell |
| C5 | Ohne Pfad (Token-Vergleich) | nur `pattern=invokeMethodAsync` | **260 / 10**; zuerst `.cursor/rules` und `ClientTests/` | schnell |
| C6 | Razor nach Index-Hinweis | `pattern=MudButton`, `fileFilter=*.razor`, `scopeType=production` | **104 / 15**; echtes Markup, beginnt bei `AiChatComposer.razor` | schnell |
| C7 | JS-Einzeldatei, Pattern fehlt | `fileFilter=…/amaChatInterop.js`, `pattern=DotNet` | 0 Treffer (Datei enthält das Literal nicht) | schnell |
| C8 | JS-Einzeldatei, Pattern sitzt | `fileFilter=…/globalSearch.js`, `pattern=invokeMethodAsync` | 1 Treffer, Zeile 6 | schnell |
| C9 | `scope` = exakte JS-Datei | `scope=…/amaChatInterop.js`, `pattern=function` | 1 Treffer, Zeile 3 | schnell |
| C10 | Nur Extension nach B1 | `fileFilter=*.js`, `scopeType=production`, `maxResults=10` | **72 / 10**; **~358 KB**; erste Hits `Release/build/wwwroot/_framework/blazor.server.js` (eine Minify-Zeile) | schnell |
| C11 | Tree-Kind ohne Präfix | `scope=wwwroot/js` | **0 Treffer** | schnell |
| C12 | Razor-Ordner aus A3 | `scope=…/Components/Admin/Ai`, `fileFilter=*.razor`, `pattern=@page` | 3 Hits in `AiSiteWorkspaceLayout.razor`, inkl. FP `@PageTitleText` | schnell |
| C13 | `includePatterns` + Scope aus A5 | `scope=…/wwwroot/js`, `includePatterns=["*.js"]` | 35 / 5, analog C1 | schnell |
| C14 | Index-Scope ohne Baum-Pfad, mit Exclude | `fileFilter=*.js`, `excludePatterns=["Release/**","**/_framework/**"]`, `scopeType=production` | **35 / 8**; Host-Quelle wie C1 | schnell |
| C15 | ClientTests-JS | `scope=ClientTests`, `fileFilter=*.js` | **173 / 5** — liegt **außerhalb** der Index-Zahl 71 | schnell |

## 3 Verdict

**Teilweise.** Die Kette **kann** JS/Razor korrekt zu `search_pattern` führen, wenn der Agent (1) die Abdeckungszeilen von `get_index_scope` liest, (2) die Toolbeschreibung von `search_pattern` kennt und (3) aus `get_file_tree` den **vollen** relativen Pfad mit Projektpräfix oder ein hartes `excludePatterns` für `Release/**` mitnimmt.

Abweichend von einer brauchbaren Agentenkette:

- Index-Antwort **nennt** `search_pattern` nicht; sie sagt nur „nicht vom Symbolgraph abgedeckt“. Wer die Fallback-Beschreibung nicht parallel kennt, bleibt bei `find_symbol` oder leerer Suche.
- Index-Menge ≠ Suchmenge: B1 zählt 71 `.js`, C10 durchsucht trotzdem `Release/` und `_framework` und explodiert tokenweise.
- Pfadübergabe ist **formatempfindlich**: `Sample.Project/wwwroot/js` funktioniert als `scope` und als `root`; `wwwroot/js` und `fileFilter=wwwroot/js` liefern 0 Treffer bei `[vollstaendig]`-artigem Leergebnis.
- Truncation in A2 übergibt die **falschen** Pfade (`Release/build/…`) an C3. Die Quelle erscheint in A2 gar nicht.

## 4 Schwere

**`degraded`** — die Routing-Idee (JS/Razor nicht im Symbolgraph → Textsuche) ist inhaltlich richtig und live nachvollziehbar. Die Kette ist trotzdem riskant: Token-Bombe bei `fileFilter=*.js` ohne Scope (C10, ~358 KB / 10 Zeilen), False Negative durch Glob/Kurzpfad (C4/C11), und Truncation, die Release-Pfade als „die“ `wwwroot/js` verkauft (A2→C3).

Zusätzlich **`friction`**: kein Folgetool-Hint in der Index-Antwort; kein dokumentiertes Pfadkontrakt zwischen `get_file_tree` und `search_pattern.scope`; gleichrangige Nennung von `find_symbol` und `search_pattern` in der Index-Beschreibung.

Nicht `broken`: Mit `view=summary` → `root=…/wwwroot/js` → Index-Abdeckung → `search_pattern` mit diesem `scope` kommt ein Agent zum Ziel (C1, C2, C6, C8, C9).

## 5 Nutzbarkeit

**Nur mit Workaround.** Wiederverwenden als:

1. `get_file_tree` `view=summary` (nicht Root-`files`, nicht ungefiltertes `**/wwwroot/js/**`).
2. `get_index_scope`: `.js`/`.razor` als „nicht abgedeckt“ lesen → **nicht** `find_symbol`.
3. Quellpfad holen über `root=Sample.Project/wwwroot/js` oder `excludePatterns=["Release/**"]`.
4. Denselben **vollen** Relativpfad nach `search_pattern.scope` bzw. exakten Dateipfad nach `fileFilter` kopieren.

Nicht wiederverwenden: Index-Zahl als Suchscope; `fileFilter=*.js` allein; Tree-Kindnamen ohne Projektpräfix; die ersten 50 Pfade aus A2 als Quelle.

## 6 Bugs

Reproduzierbar in dieser Kette (kein Vollabgleich, Stichprobe gegen dieselben MCP-Antworten).

| Befund | Art | Call |
|---|---|---|
| A2 zeigt 50/280 und **ausschließlich** `Release/build/wwwroot/js`. Host-Quelle existiert (A5: 69 `.js`). Agent, der die Liste weiterreicht, sucht in C3 die falsche Kopie. | Truncation → falsche ID/Pfad-Übergabe | A2 → C3 |
| `fileFilter=wwwroot/js` und `scope=wwwroot/js` → 0 Treffer, obwohl A4 `wwwroot/js/` als Kind zeigt. | False Negative / Pfadkontrakt | C4, C11 vs. A4/A5/C1 |
| `get_index_scope` 71 `.js` vs. Dateibaum 204 `.js`. `search_pattern` mit `*.js`+`production` trifft trotzdem zuerst minifizierte `Release/build/wwwroot/_framework/blazor.server.js` (~358 KB). Index-Inventar steuert die Suche nicht. | Index ≠ Scan-Set | B1 vs. A1 vs. C10 |
| ClientTests-JS: 173 Treffer (C15), liegt nicht in den 71 Index-Dateien. `scopeType=production` in C10 schließt `Release/` nicht aus. | Scope-Lüge in der Kette | C10, C15 |
| Index-Antwort enthält keinen Next-Tool-String. Routing zu `search_pattern` hängt an Schema-Wissen, nicht an der Payload. | Missing hint | B1 |
| C12 `@page` trifft auch `@PageTitleText` (Substring). Nach korrektem Razor-Routing bleibt der Treffer unscharf. | FP in der Folgesuche | C12 |
| C7 0 Treffer bei gültigem Dateipfad, weil das Literal fehlt — kein Bug der Übergabe (C8/C9 belegen denselben Mechanismus). | kein Bug | C7 vs. C8/C9 |

Keine Beobachtung, dass Index-Scope `.js` oder `.razor` fälschlich als symbolgraph-abgedeckt markiert. Die Abdeckungsaussage selbst ist gegenüber den Ankern korrekt.

## 7 Token/IDs

- **Stabile Übergabe:** nur `targetType` + `targetPath`. Keine Symbol-IDs in der ganzen Kette.
- **Pfad-IDs:** `get_file_tree` `view=files` liefert kopierbare Relativpfade. Die funktionieren in `search_pattern` als `scope` (Ordner oder Datei) und als `fileFilter` (exakte Datei oder `*.razor`). Kurzformen und Globs ohne `**/` nicht.
- **Index-Scope:** 19 Zeilen, tokenarm, aber **keine** weiterreichbaren IDs. Folgetool muss Pfade erfinden oder aus dem Dateibaum holen.
- **Token-Last der Kette, wenn Workaround greift:** A1 (~1 KB) + B1 (~1 KB) + A5/A7 (kompakt) + C1/C2 (kompakt) — agententauglich.
- **Token-Last ohne Workaround:** A2 (50 Release-Zeilen inkl. `.br`/`.gz`) + C5 (260 Treffer, erste Seite Rules/Tests) + C10 (**~358 KB / 10 Zeilen** durch Minify). Truncation-Footer ohne Continuation-Token; nur „Pattern verfeinern oder maxResults erhöhen“.
- **Doppelte Last:** Dieselbe JS-Funktion erscheint in Host-Quelle (C1), Release-Kopie (C3) und ClientTests (C15). Ohne Scope zahlt der Agent dreimal.

## 8 Roslyn-Wünsche

Alles statisch/dateibasiert, kein LLM hinter den Tools.

1. **Next-Tool in der Index-Payload:** je Endung `symbolGraphCovered` plus `suggestedTool` (`find_symbol` vs. `search_pattern`). Eine Zeile reicht.
2. **Gemeinsames Pfadkontrakt:** Dateibaum-Relativpfade 1:1 als `search_pattern.scope` dokumentieren; Kurzpfade entweder auflösen oder mit `INVALID_ARGUMENT` + Beispielpfad ablehnen statt 0 Treffer.
3. **Scan-Set angleichen:** `search_pattern` defaultmäßig dieselbe Dateimenge wie `get_index_scope`, oder Index-Scope listet ausgeschlossene Wurzeln (`Release/`, `legacy/`, `ClientTests/`).
4. **Truncation nicht Release-first:** bei `fileFilter=**/wwwroot/js/**` Quelle vor `Release/build` sortieren, oder Default-`exclude` für Build-Outputs; Continuation statt abgeschnittener Fremdkopie.
5. **Glob-False-Negative:** `fileFilter=wwwroot/js` nicht als vollständig-leer verkaufen, wenn `root=…/wwwroot/js` 69 Dateien hat — Hint „Pfad als `root` oder `**/wwwroot/js/**`“.
6. **Minify-Schutz:** Trefferzeilen aus `_framework`/`*.min.js` kappen oder Dateien als binär überspringen; C10 darf nicht 358 KB für 10 Hits liefern.
7. Strukturierte Index-Zeilen `{ extension, fileCount, covered }` damit der Agent nicht Fließtext parst.

## 9 Phase 3

Welle 3, nur Nicht-ok-Kettenbrüche. AiNetLinter read-only; Ansatz = Dateisystem + Glob, kein LLM.

| Kettenbefund | Bruch | Pfad + Symbol | Roslyn-/Datei-Ansatz |
|---|---|---|---|
| A2: 50/280 nur `Release/build/wwwroot/js` → C3 sucht die Kopie | Truncation | `src\AiNetLinter\Mcp\Tools\FileStructure\GetFileTreeScanner.cs` `SortMatches` (`sortBy=path` Default); `GetFileTreeRenderer.Render` Hint nur „root/fileFilter verfeinern“; `FileSystemWalkOptions.ForFileTree` / `FileSystemExclusionHelpers.SearchExcludedDirectories` **ohne** `Release` | Nach Match: Quelle vor Build sortieren (`Release/`, `build/` nach hinten) oder Default-`exclude` analog `IsSearchExcludedRelativePath`. Truncation-Footer: „gezeigte Pfade sind Präfix der Sortierung, keine vollständige `wwwroot/js`-Quelle“. Continuation statt nur maxResults. |
| `fileFilter=wwwroot/js` / `scope=wwwroot/js` → 0 Treffer, A4 zeigt Kind `wwwroot/js/` | ID-Handover | `FileTreeFilter.MatchesPathOrFileName` (Glob gegen vollen Relativpfad); `SearchPatternScanner.NormalizeScope` / `MatchesScope` (Prefix `scope + "/"`); `FileTreePathResolver.ResolveRoot` akzeptiert denselben Kindnamen als `root` | Kontrakt: Tree-Relativpfade 1:1 als `scope`. Kurzpfad: Unique-Suffix gegen `GetProjectDirectories` auflösen **oder** `INVALID_ARGUMENT` mit Beispiel `Sample.Project/wwwroot/js`. 0 Treffer bei existierendem Ordner nicht als vollständig-leer. |
| Index `.js` 71 vs. Dateibaum 204; `search_pattern` `*.js`+`production` trifft `Release/…/_framework/blazor.server.js` | ID-Handover | `GetIndexScopeScanner.CountNonCSharpFiles` über `WebFileCatalog.GetProjectDirectories` (csproj-Ordner, `IsGeneratedPath` ohne `Release`); `SearchPatternScanner.ScanFiles` enumeriert `SolutionRoot` (`SafeEnumerateFilesWithErrors`); `IsExcludedByScopeType` = nur `TestDetector.IsTestFile`, nicht Build-Output | Dieselbe Dateimenge: Index-Walker **oder** Search-Default = Projektverzeichnisse + gemeinsame Exclude-Liste (`Release`, `_framework`, `bin`/`obj`). `scopeType=production` darf Build-Kopien nicht als Produktion zählen. Index-Text: ausgeschlossene Wurzeln nennen. |
| ClientTests-JS (C15) außerhalb Index-71; `production` schließt `Release/` nicht aus | ID-Handover | `WebFileCatalog.GetProjectDirectories` (kein npm-`ClientTests`); `SearchPatternScanner.IsExcludedByScopeType`; `FileSystemExclusionHelpers.IsSearchExcludedRelativePath` | Index-Zeile `.js` mit Scan-Wurzeln. `scopeType` um Build/Test/ClientTests oder Exclude-Default `Release/**`. |
| Index-Antwort ohne Next-Tool; Schema nennt `find_symbol`/`search_pattern` gleichrangig | Formatter | `GetIndexScopeScanner.FormatFileCountLine` / `FileTypeBreakdownEntry`; `src\AiNetLinter\Mcp\Registration\FileStructureToolRegistrations.cs` `GetIndexScopeDescription` | Pro Endung `suggestedTool`: `.cs` → `find_symbol`, sonst `search_pattern`. StructuredContent-Feld existiert schon (`SymbolGraphCovered`) — im Markdown-Text ausgeben. Beschreibung: abgedeckt → Symbol, sonst Pattern. |
| C10 ~358 KB / 10 Zeilen (`blazor.server.js`) | Truncation | `SearchPatternScanner.IsMinified` (nur `.min.` im Dateinamen); `SearchPatternLegacyFormatter.Format` dumpft volle `LineText`; `McpTruncation.TruncateLines` | Skip oder Kappung: `_framework`, `*.min.js`, Zeilen > N Zeichen als `[minified, k Zeichen]`. `IsMinified` um Framework-Pfade erweitern, nicht nur `.min.`. |
| C12 `@page` trifft `@PageTitleText` | Formatter | `SearchPatternScanner` Plain-First / Regex-Autodetect; `SearchPatternLegacyFormatter` ohne Wortgrenzen-Hint | Kein Kettenbruch der Pfadübergabe. Optional: bei 0 Regex und Identifier-artigem Pattern Wortgrenzen-Hinweis; nicht die Discovery-Kette blockieren. |

Nach Fix: A1 → B1 → C1/C2; C4/C10/C11 dürfen nicht mehr die Falle sein.
