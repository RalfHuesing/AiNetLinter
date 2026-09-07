# Finding: `get_file_tree`

Ziel: `targetType=project`, `targetPath=C:\Workspace\Sample.Project`.  
Kein Assembly-Call (Auftrag nur Projekt). Schema via `GetDynamicTools` (`user-AiNetLinter` / `get_file_tree`). Live-Calls nur dieses Tool.

## 1. Schema-Kurzfazit

Beschreibung steuert den **Happy Path grob richtig**: physische Dateilandkarte, `root`/`fileFilter`/`excludePatterns` relativ zu `targetPath`, `fileFilter` ist Pfad-Glob (keine Inhaltssuche), `view` ∈ `tree` (Default) / `summary` / `files`, `maxResults` Default 200 / Max 2000, `targetType`+`targetPath` Pflicht.

**Root-`files`-Flood:** Die Tool-Beschreibung **warnt nicht**. Sie nennt nur das Limit (200/2000), nicht „Root ohne Filter nicht als `view=files`“. Ein Agent, der nur das Schema liest, ruft genau den Flut-Call. Die Workflow-Rule sagt das Gegenteil; Schema und Rule divergieren.

Weitere Schema-Lücken:

- Aliase `root`/`path`/`directory` und `fileFilter`/`filter`/`pattern` stehen im Input-Schema, nicht in der Beschreibung (welches Feld kanonisch ist, bleibt Raten).
- Glob-Semantik fehlt: `wwwroot/js` und `wwwroot/js/**` matchen **nicht**; erst `**/wwwroot/js/**` oder `root=…/wwwroot/js`.
- Default-`maxDepth` bei `view=files` **ohne** Filter ist in der Beschreibung angedeutet („effektive Tiefe = maxDepth ?? treeDepth“), aber nicht als „dann siehst du nur die obere Schicht, nicht das ganze Repo“.
- `structuredContent` unter `fileTree` ist behauptet; über `CallDynamicTool` kommt nur Text.

## 2. Ausgeführte Calls

Alle schnell (kein Timeout). Größen grob nach sichtbarer Textantwort.

| # | Parameter | Größe / completeness | Wartezeit |
|---|-----------|----------------------|-----------|
| 1 | `view=summary` | ~24 Ordnerzeilen, 3843 Dateien aggregiert, `[vollstaendig]` | schnell |
| 2 | `view=tree`, `treeDepth=2` (Default `maxResults=200`) | Truncation: 1192 gematcht, 200 gezeigt; WARN `maxResults, maxDepth` | schnell |
| 3 | `view=files`, `fileFilter=wwwroot/js` | 0 Treffer, `[vollstaendig: 0]` | schnell |
| 4 | `view=files`, `fileFilter=*.razor` | 111 Dateien, vollständig | schnell |
| 5 | `view=files`, kein Filter, `maxResults=200` | 1192 gematcht / 200 gezeigt; WARN Truncation | schnell |
| 6 | `view=files`, `fileFilter=**/wwwroot/js/**` | 280 gematcht / 200 gezeigt; zuerst `Release/build/wwwroot/js/…` | schnell |
| 7 | `view=files`, `fileFilter=*wwwroot/js*` | 0 Treffer, vollständig | schnell |
| 8 | `view=files`, `root=Sample.Project/wwwroot/js` | 70 Dateien (69×`.js`, 1×`.md`), vollständig | schnell |
| 9 | `view=tree`, `root=this-folder-does-not-exist-xyz` | `RESOURCE_NOT_FOUND`, Hint relativ zum projectRoot | schnell |
| 10 | `view=files`, `fileFilter=zzzz-kein-treffer-xyz.nope` | 0 Treffer, vollständig (kein `isError`) | schnell |
| 11 | `view=files`, kein Filter, `maxResults=2000` | **Flut:** ~99,4 KB / 1199 Zeilen; Scan nur 1192 wegen Default-`maxDepth`; WARN Scantiefe | schnell |
| 12 | `view=files`, `fileFilter=*.razor`, `includeLineCount=true`, `sortBy=size_desc` | 111 Dateien, Größe+Zeilen, größtes zuerst (`DynamicFormFieldEditor.razor` 122 Zeilen) | schnell |
| 13 | `view=files`, `root=…/wwwroot`, `includeExtensions=[".js"]`, `maxResults=50` | 70 gematcht / 50 gezeigt | schnell |
| 14 | `view=summary`, `excludePatterns=["Release/**","legacy/**"]` | 3226 Treffer, ohne `Release/` und `legacy/` | schnell |
| 15 | `view=tree`, `treeDepth=2`, `maxResults=2000` | kein maxResults-Cut; WARN nur Scantiefe; **Prefix-Leak bleibt** | schnell |
| 16 | `view=files`, Alias `filter=*.csproj` | 15 csproj, vollständig | schnell |
| 17 | `view=files`, `maxDepth=32`, `maxResults=2000` | **Flut:** ~175,1 KB / 2007 Zeilen; 3844 gematcht / 2000 gezeigt | schnell |
| 18 | Alias `pattern=*.slnx`, `includeMetadata=false` | 1 Treffer, ohne Größenangabe | schnell |
| 19 | Alias `directory=Docs`, `view=files` | 10 Markdown-Dateien, vollständig | schnell |
| 20 | `view=files`, `fileFilter=wwwroot/js/**` (ohne `**/`) | 0 Treffer | schnell |
| 21 | Alias `path=SQL-Scripts`, `view=summary` | 54 Dateien, Unterordner inkl. `Platform/` | schnell |
| 22 | `view=tree`, `root=Sample.Project.Setup`, `treeDepth=1` | `Engine/` 17, `Themes/` 1, `Setup/` 34 | schnell |
| 23 | `view=tree`, `root=Sample.Project`, `treeDepth=1` | u. a. `Api/`, `Auth/`, `logs/`, `wwwroot/` | schnell |
| 24 | `view=not-a-view` | `INVALID_ARGUMENT`: view muss summary/tree/files sein | schnell |

## 3. Verdict

**Abweichend.** `summary`, gefiltertes `files` (`*.razor`, `*.csproj`) und gezieltes `root=` wirken wie beschrieben. Default-`tree` und naive `fileFilter`-Pfade tun das nicht: falsche Eltern-Kind-Zuordnung, leere Treffer trotz existierendem Ordner, Root-`files` ohne Filter flutet Tokens.

## 4. Schwere

**`broken`** — der Default (`view=tree`) liefert eine **falsche** Verzeichnisstruktur (Pfad-Prefix ohne Trennzeichen). Ein Discovery-Agent glaubt danach z. B., `Themes/` gehöre zu `Setup.Tests` und `wwwroot/` zu `Tests.Support`.

Zusätzlich `degraded` (Token-Flut, Truncation ohne Continuation) und `friction` (Schema ohne Flood-Warnung, undokumentierte Aliase, Glob braucht `**/`).

## 5. Nutzbarkeit

**Nur mit Workaround.** Wiederverwenden als: zuerst `view=summary`, dann `root=<unterordner>` oder `fileFilter` mit `**`-Glob bzw. `*.ext`. Nicht wiederverwenden: Root-`tree` zur Orientierung, Root-`files` ohne Filter, `fileFilter=wwwroot/js`.

## 6. Bugs, FP/FN vs. Ist

Ground Truth nur über weitere `get_file_tree`-Calls (kein `rg`).

### Bug A — Tree-Prefix (reproduzierbar, Call 2/15 vs. 22/23)

`Sample.Project.Setup` ist Prefix von `Sample.Project.Setup.Tests`. Im Root-`tree` (auch mit `maxResults=2000`) hängen unter **Setup.Tests**:

- `Engine/` 19 + `Setup/` 14 (echt Setup.Tests)
- plus `Engine/` 17, `Themes/` 1, `Setup/` 34 — das sind die Kinder von **Setup** (Call 22)

`Setup` selbst erscheint ohne Kinder. Analog: Kinder von `Sample.Project` (`Api/`, `Auth/`, `logs/` 76,6 MB, `wwwroot/` …, Call 23) erscheinen unter **Tests.Support**. Das ist kein Truncation-Artefakt.

### Bug B — `fileFilter` ohne `**/` ist False Negative (Call 3/7/20 vs. 8)

Ordner **existiert**: `root=Sample.Project/wwwroot/js` → 70 Dateien.  
`fileFilter=wwwroot/js`, `wwwroot/js/**`, `*wwwroot/js*` → 0 Treffer, fälschlich `[vollstaendig]`. Agent schließt „gibt es nicht“.

Workaround `**/wwwroot/js/**` (Call 6) trifft, sortiert aber `Release/build/wwwroot/js` zuerst; bei Default-200 fehlen die Quelldateien unter `Sample.Project/wwwroot/js`.

### Bug C — Default-Tiefe bei Root-`files` ohne Filter (Call 5/11 vs. 1/17)

Ohne `fileFilter` scannt `view=files` nur ~1192 Dateien (wie `treeDepth`-Default), nicht die 3844 aus `summary`. Completeness-WARN spricht von `maxDepth`, nicht davon, dass die Dateiliste fachlich unvollständig ist (z. B. fast keine tiefen `.razor`: Header Call 5 zeigt `.razor 6` vs. 111 real).

### Stichprobe FP/FN

| Probe | MCP | Ist | Urteil |
|-------|-----|-----|--------|
| `*.razor` | 111 | Summary-Extension `.razor 111` | Treffer plausibel |
| `wwwroot/js` als Filter | 0 | 70 Dateien unter dem Ordner | **FN** |
| Setup `Themes/` unter Setup.Tests | angezeigt | gehört zu `…Setup/` | **FP** (falscher Parent) |
| `Docs/` via Alias `directory` | 10×`.md` | Summary `Docs/` 10 Dateien | ok |
| unsinniger `root` | ERROR | Ordner existiert nicht | ok |
| unsinniger `fileFilter` | leere Menge, kein Error | korrekt leer | ok (leicht verwechselbar mit Bug B) |

## 7. Token / IDs

- `view=summary`: klein, aggregiert, für den nächsten Call brauchbar (Ordnernamen → `root=`).
- Root `view=files` `maxResults=2000` ohne Tiefe: ~99 KB / ~1200 Zeilen.
- Dasselbe mit `maxDepth=32`: ~175 KB / ~2007 Zeilen, **2000/3844**, kein `cursor`/`continuationToken`. Nur Hint „root/fileFilter verfeinern oder maxResults anpassen“ — Max ist bereits 2000, also tot.
- Keine Symbol-IDs. Folge-Call-Schlüssel sind **relative Pfade** (funktionieren als `root`).
- `includeMetadata=false` spart Größen; `includeLineCount` erhöht Tokens linear.
- StructuredContent `fileTree` in dieser Oberfläche nicht sichtbar.

## 8. Roslyn-konforme Wünsche

Das Tool ist Dateisystem, nicht Roslyn. Trotzdem statisch/deterministisch behebbar, ohne LLM:

1. Eltern-Zuordnung nur mit Pfadtrenner (`StartsWith(parent + "/")` bzw. `Path.GetRelativePath`), nie `StartsWith(parent)`.
2. Schema-Text: explizite Flood-Warnung für Root-`view=files` ohne Filter; Glob-Beispiele (`*.razor` vs. `**/wwwroot/js/**`).
3. Bei Truncation: Continuation oder ehrliches „nicht das ganze Repo, Default-Tiefe X“.
4. `fileFilter` auf Verzeichnisnamen: entweder Directory-Prefix-Match oder klare Fehlermeldung statt stiller 0-Treffer mit `vollstaendig`.

## 9. Phase 3

Welle 3, read-only `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Je Nicht-ok-Befund: Pfad + Symbol + Ansatz. Kein Patch. Dateisystem-Tool, kein Roslyn.

### Bug A — Tree-Prefix / falscher Parent (`broken`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetFileTreeRenderer.cs`
- **Symbol:** `GetFileTreeRenderer.AppendDirectories`
- **Ansatz:** Kein `StartsWith(parent)` ohne Trenner (das gibt es hier nicht; `GetFileTreeScanner.GetDepth` nutzt bereits `path.StartsWith(_rootRelativePath + "/")`). Der Baum ist eine **flache**, nach `Path` sortierte Liste mit Indent = `(Depth - 1) * 2` und Name = letztes Pfadsegment. Dadurch sortiert `….Setup.Tests/…` vor `….Setup/…` (`.` vor `/`), und die Kinder von `Setup` erscheinen visuell unter `Setup.Tests`. Echten Baum bauen: Parent über bestehendes `GetFileTreeScanner.GetParentPath` (`LastIndexOf('/')`), rekursiv rendern, Geschwister nach Segmentnamen. Kind nur anhängen, wenn `GetParentPath(child) == parent.Path`.

### Bug B — `fileFilter` ohne `**/` ist FN (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\FileTreeFilter.cs`; `src\AiNetLinter\Configuration\PathGlobMatcher.cs`
- **Symbol:** `FileTreeFilter.MatchesPathOrFileName`, `PathGlobMatcher.Matches` / `BuildRegex`
- **Ansatz:** Matcher ist `^…$` auf dem ganzen Relativpfad, sobald das Muster `/` enthält; ohne `/` nur auf `Path.GetFileName` (daher `*wwwroot/js*` = 0). Verzeichnisartige Filter (`wwwroot/js`, `wwwroot/js/**`) als Prefix behandeln: `Equals(pattern)` oder `StartsWith(pattern.TrimEnd('*','/') + "/")`, alternativ stilles Promote zu `**/pattern/**`. Glob-Beispiele in die Description. Kein LLM.

### Bug C — Default-Tiefe Root-`files` ohne Filter (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetFileTreeScanner.cs`; `GetFileTreeRenderer.cs`
- **Symbol:** `GetFileTreeScanner.DetermineEffectiveWalkDepth` (Fallback `return 2`); `GetFileTreeRenderer.AppendCompleteness` (`isDepthOnly`)
- **Ansatz:** Bei `view=files` ohne `fileFilter` und Root `.` nicht still auf Tiefe 2 walken, oder die Completeness-WARN explizit sagen, dass die **Dateiliste** unvollständig ist (nicht nur „tiefere Ebenen“). `FileTreeCompleteness` hat kein Continuation-Feld; Offset/`cursor` analog anderer Tools oder ehrliches „Default-Tiefe 2, maxDepth setzen“.

### Schema ohne Flood-Warnung / undokumentierte Aliase (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\FileStructureToolRegistrations.cs`
- **Symbol:** `FileStructureToolRegistrations.AddGetFileTree`, `GetFileTreeDescription`
- **Ansatz:** Description: Root-`view=files` ohne Filter nicht aufrufen; `**/ordner/**` vs. `*.ext`; Aliase `root`/`path`/`directory` und `fileFilter`/`filter`/`pattern` benennen (Bindung schon in der Lambda: `root ?? path ?? directory`, `fileFilter ?? filter ?? pattern`). Schema-Enums für `view`/`sortBy` gehen über die C#-Parameter der `McpServerTool.Create`-Lambda (SDK inferiert JSON) — optionale Enum-Typen statt `string`.

### Truncation ohne Continuation, Cap 2000 tot (`degraded` / `wish`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetFileTreeScanner.cs` (`FileTreeAccumulator.Build`); `GetFileTreeRecords.cs` (`FileTreeCompleteness`); `GetFileTreeTool.cs` (`MaxResultsCap`)
- **Symbol:** `FileTreeAccumulator.Build` (`Take(_input.MaxResults)`); `GetFileTreeRenderer.AppendCompleteness` (Hint „maxResults anpassen“)
- **Ansatz:** `shownOffset`/`continuationToken` in `FileTreeCompleteness`; Hint unterscheiden: Cap bereits 2000 vs. Filter verfeinern. Kein Roslyn.

### `structuredContent.fileTree` in der Agentenfläche unsichtbar (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetFileTreeTool.cs`
- **Symbol:** `GetFileTreeTool.ExecuteAsync` (`McpToolResults.Text(text, new { fileTree = scan.Payload })`)
- **Ansatz:** Server setzt StructuredContent bereits. Host (`CallDynamicTool`) zeigt nur Text — kein AiNetLinter-Patch. Description nicht als sichtbares Agenten-Feld verkaufen.
