# Finding: `get_index_scope`

Audit-Stand: 2026-09-07. Nur Schema-Lookup (`GetDynamicTools`) plus Calls dieses Tools. Kein anderes MCP-Tool, kein Build, kein Test.

## 1 Schema-Kurzfazit

`get_index_scope` ist als **erster Discovery-Call** vor `find_symbol` / `search_pattern` dokumentiert: Dateityp-Aufschlüsselung der Solution mit Dateianzahl und klarer Abdeckungsangabe (`.cs` vom Symbolgraph, `.css`/`.html`/`.js`/`.razor`/`.xaml` nicht).

Zielvertrag laut Beschreibung: `targetType='project'` und `targetPath` als **absoluter, kanonischer Projektpfad**. Assembly-Ziele sind **ausdrücklich unsupported**.

Input-Schema (vollständig):

| Parameter | Typ | Pflicht | Bemerkung |
|---|---|---|---|
| `targetType` | string | ja | Kein Enum im JSON-Schema; Beschreibung nennt `'project'` (Assembly nur als Gegenanzeige). |
| `targetPath` | string | ja | Absoluter Projektroot. |

**Keine optionalen Parameter** im Schema. Keine Limits (`maxResults`, `maxDiagnostics`, `includeDiagnostics`). Keine Filter, keine View-Varianten. Die Nutzlast ist bewusst kompakt: eine Zeile pro vorkommender Dateiendung.

## 2 Calls

Projektroot: `C:\Workspace\Sample.Project`.

| # | Absicht | Argumente | Ergebnis |
|---|---|---|---|
| 1 | Happy Path | `targetType=project`, `targetPath=<Projektroot>` | Erfolg. 19 Endungen, u. a. `.cs: 2287 Dateien (voll vom Symbolgraph abgedeckt)`, `.js: 71 … (nicht …)`, `.razor: 111 … (nicht …)`, `.xaml: 15 … (nicht …)`, `.css: 61 … (nicht …)`. Weitere nicht abgedeckte Typen: `.csproj`, `.json`, `.jsonl`, `.md`, `.sql`, `.txt`, `.log`, `.slnx`, `.svg`, `.woff2`, `.manifest`, `.example`, `.openai`, `.user`. |
| 2 | Fehler, falscher Pfad | `targetType=project`, `targetPath=C:\Workspace\DoesNotExist.Platform` | `[ERROR]: PROJECT_NOT_INITIALIZED` — sucht `ainetlinter.project.json` am angegebenen Root, liefert JSON-Vorlage (`solution`/`rules`) und Retry-Hinweis. |
| 3 | Assembly (laut Beschreibung unsupported) | `targetType=assembly`, `targetPath=<Projektroot>` (Verzeichnis, keine DLL) | `[ERROR]: INVALID_ARGUMENT` — behandelt das Ziel als Assembly: Pfad muss existierende `.dll`/`.exe` sein. **Kein** Tool-spezifisches „unsupported“. |
| 4 | Pflichtfeld fehlt (`targetType`) | nur `targetPath` | Generisch: `An error occurred invoking 'get_index_scope'.` Kein `INVALID_ARGUMENT`, kein Hint. |
| 5 | Relativer Pfad | `targetType=project`, `targetPath=Sample.Project` | `[ERROR]: INVALID_ARGUMENT: Der Parameter 'targetPath' muss ein absoluter Pfad sein.` Plus Hint zu `targetType`/`targetPath`. |
| 6 | Extra-Parameter (nicht im Schema) | wie Call 1 plus `includeDiagnostics=true`, `maxResults=5` | **Identisch** zu Call 1. Extra-Args werden still ignoriert. Keine Truncation, keine Diagnostics. |
| 7 | Pflichtfeld fehlt (`targetPath`) | nur `targetType=project` | Wie Call 4: generischer Invoke-Fehler. |
| 8 | Unterordner statt Projektroot | `targetType=project`, `targetPath=…\Sample.Project\Sample.Project` | `PROJECT_NOT_INITIALIZED` analog Call 2 (sucht `ainetlinter.project.json` im Unterordner). |
| 9 | Canonical/Case | `targetType=Project`, `targetPath` kleingeschrieben (`c:\daten\…`) | Erfolg, **identisch** zu Call 1. Pfad und `targetType` sind case-tolerant (Windows). |

**Truncation / Token-Stress:** Im Schema gibt es **keine Limits**. Call 6 (`maxResults=5`) ändert nichts. Die Antwort ist fest ~19 Zeilen, ohne `truncated`/`completeness`-Marker. Token-Stress ist bei diesem Tool praktisch nicht erzeugbar.

## 3 Verdict

**Nützlich und vertragstreu für den Happy Path.** Ein Agent erfährt in einer kompakten Liste, welche Dateitypen existieren und — entscheidend — dass **nur `.cs` vom Symbolgraph abgedeckt** ist. `.js`, `.razor`, `.css`, `.xaml` sind explizit als nicht abgedeckt markiert; das erfüllt den Audit-Zweck (JS/Razor vs. C#).

Schwächen: Schema ohne optionale Parameter (ok), aber Extra-Args werden still geschluckt. Fehlende Pflichtfelder liefern einen generischen Invoke-Fehler statt `INVALID_ARGUMENT`. Assembly-Calls werden nicht als „Tool unterstützt kein Assembly-Ziel“ abgewiesen, sondern in den allgemeinen Assembly-Pfadvalidator geleitet — Widerspruch zur Toolbeschreibung.

Keine Truncation-Semantik, weil keine Limits existieren; das ist hier eher ein Feature (Antwort bleibt klein).

## 4 Schwere

**Niedrig bis mittel.**

- Happy Path und die zentralen Agenten-Fragen (was ist indexiert?) funktionieren.
- Mittel: Vertragslücke Assembly (`unsupported` vs. Assembly-Pfadprüfung); generische Fehler bei fehlenden Required-Feldern; stille Extra-Parameter.
- Kein Produktionsrisiko, kein Datenverlust, keine irreführende Abdeckungsaussage bei `.cs`/`.js`/`.razor`.

## 5 Nutzbarkeit

**Hoch als Discovery-Einstieg.**

Ein Agent kann nach einem Call entscheiden:

- C#-Symbole → `find_symbol` / `get_feature_context` (2287 `.cs` voll abgedeckt).
- JS / Razor / CSS / XAML / SQL / JSON / Markdown → nicht im Symbolgraph; Textsuche (`search_pattern`) bzw. Dateibaum.

Die Formulierung „voll vom Symbolgraph abgedeckt“ vs. „nicht vom Symbolgraph abgedeckt“ ist agententauglich, ohne dass man die Toolbeschreibung noch einmal lesen muss.

Einschränkungen für Agenten:

- Keine Aussage, **welche** `.cs` (obj/bin/generated vs. Source) in die 2287 eingehen.
- `.html` kommt in der Beschreibung vor, in diesem Repo aber **nicht in der Liste** (vermutlich 0 Dateien → Endung weggelassen). Ein Agent darf daraus nicht schließen, HTML wäre abgedeckt.
- Kein strukturiertes JSON (nur Fließtext-Zeilen) — parsing ist trotzdem trivial.
- Kein Hinweis in der Antwort selbst, welches Folgetool für Nicht-`.cs` gedacht ist (steht nur in der Toolbeschreibung).

## 6 Bugs+FP/FN

| Befund | Art | Bewertung |
|---|---|---|
| `targetType=assembly` wird nicht als Tool-Unsupported zurückgewiesen, sondern als Assembly-Pfad validiert. | Vertrag / FP der Beschreibung | Beschreibung: „ausdrücklich unsupported“. Verhalten: generischer Assembly-Pfadfehler. Agent kann denken, das Tool ginge mit einer echten DLL. |
| Fehlende Required-Parameter → generischer Invoke-Fehler. | UX / Error-Shape | Call 2/3/5 haben maschinenlesbare `[ERROR]: CODE`. Call 4/7 nicht. |
| Unbekannte Parameter (`maxResults`, `includeDiagnostics`) still ignoriert. | Still-Ignore | Kein Fehler, keine Wirkung. Agent könnte glauben, Truncation sei aktiv. |
| Endungen mit 0 Dateien fehlen (z. B. `.html` aus der Beschreibung). | FN der Vollständigkeit | Kein False Negative zur Abdeckung, aber Lücke gegenüber dem dokumentierten Typenkatalog. |
| Unterordner-Root → `PROJECT_NOT_INITIALIZED` statt „kein Projektroot / gehe eine Ebene höher“. | Error-Qualität | Korrekt nach Vertrag (kanonischer Projektpfad), aber der Hint erzeugt eine `ainetlinter.project.json` im **falschen** Ordner, falls der Agent das wörtlich befolgt. |
| Keine Truncation-Flags. | kein Bug | Limits existieren nicht; kein FP/FN. |

Keine beobachteten False Positives der Form „JS/Razor als symbolgraph-abgedeckt“. Keine False Negatives der Form „`.cs` als nicht abgedeckt“.

## 7 Token/IDs

- **Tokens:** Sehr sparsam. Happy-Path-Antwort ≈ 19 kurze Zeilen, eine Endung pro Zeile. Kein Symbolgraph-Dump, keine Pfadliste, keine Diagnostics-Samples.
- **IDs:** Keine Symbol-IDs, keine Dateipfade, keine Session-IDs in der Nutzlast. Nur Endung + Anzahl + Abdeckungsstatus.
- **Stabilität:** Call 1, 6 und 9 liefern denselben Text; Zählwerte (2287 `.cs`, 71 `.js`, 111 `.razor`) sind in dieser Session reproduzierbar.
- Extra-Parameter erhöhen die Antwort **nicht**.

## 8 Roslyn-Wünsche

1. **Strukturierte Nutzlast** (JSON): `{ extension, fileCount, symbolGraphCovered }` plus `completeness: complete` — leichter zu parsen als Fließtext.
2. **Null-Endungen optional** (`includeEmptyExtensions=true`): `.html` und andere in der Beschreibung genannte Typen mit `0`, damit der Katalog vollständig bleibt.
3. **Assembly-Ziel explizit ablehnen** mit Tool-spezifischem Code (`TOOL_TARGET_UNSUPPORTED`) statt allgemeinem DLL-Pfadfehler — Beschreibung und Verhalten angleichen.
4. **Required-Validierung** vor Invoke: fehlendes `targetType`/`targetPath` als `INVALID_ARGUMENT` mit Hint, analog zum Relativpfad.
5. **Coverage-Feinheiten:** Sind `obj/`/`bin/`/generated `.cs` in den 2287? Ein `sourceOnly` vs. `includingGenerated` würde Agenten vor falscher „voll indexiert“-Lesart schützen.
6. **Nächster-Schritt-Hint** in der Antwort (eine Zeile): Nicht-`.cs` → `search_pattern`; `.cs` → `find_symbol`.
7. Unbekannte Parameter **ablehnen** statt still zu ignorieren (verhindert Truncation-Illusion).

## 9 Phase 3 (AiNetLinter-Quelle)

Welle 3, read-only `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Kein Patch.

| Befund | Pfad | Symbol | Roslyn-/statischer Ansatz |
|---|---|---|---|
| `targetType=assembly` nicht tool-unsupported, sondern DLL-Pfadprüfung | `src\AiNetLinter\Mcp\Registration\FileStructureToolRegistrations.cs`; `src\AiNetLinter\Mcp\AnalysisToolCall.cs`; `src\AiNetLinter\Mcp\AnalysisTargetResolver.cs` | `AddGetIndexScope` → `ProjectAnalysisDispatcher.ExecuteAsync`; `ExecuteProjectAsync`; `AnalysisTargetResolver.Resolve` (`IsSupportedAssemblyPath`) | Description kommt von `McpToolRegistrationOptions.ReadOnlyTool` (`ProjectTargetContract`: Assembly unsupported). Dispatch validiert zuerst den Assembly-Pfad (Verzeichnis → `INVALID_ARGUMENT` „.dll/.exe“). `UnsupportedAssemblyTarget` greift nur nach erfolgreicher Auflösung. Ansatz: `targetType=assembly` **vor** der Pfad-als-DLL-Prüfung ablehnen (`ASSEMBLY_TARGET_UNSUPPORTED` / gleichwertig), analog `get_hotspots` mit echter DLL. |
| Fehlende Required-Parameter → generischer Invoke-Fehler | `src\AiNetLinter\Mcp\Registration\FileStructureToolRegistrations.cs`; `src\AiNetLinter\Mcp\AnalysisTargetResolver.cs` | `AddGetIndexScope` (`string targetType, string targetPath` ohne Default); `Resolve` (`IsNullOrWhiteSpace`) | MCP-SDK bindet Pflicht-C#-Parameter und wirft vor dem Handler („An error occurred invoking“). `Resolve` hätte bereits `INVALID_ARGUMENT`. Ansatz: Parameter optional machen (`string?`) und in `Resolve`/`ExecuteProjectAsync` denselben maschinenlesbaren Code liefern wie beim Relativpfad. |
| Extra-Args (`maxResults`, `includeDiagnostics`) still ignoriert | `src\AiNetLinter\Mcp\Registration\FileStructureToolRegistrations.cs` | `AddGetIndexScope`-Lambda (nur `targetType`/`targetPath`/`ct`) | Unbekannte JSON-Keys verwirft das MCP-SDK, AiNetLinter sieht sie nicht. Ansatz: Roh-Argumente prüfen (`JsonExtensionData` / Request-Element) und unbekannte Keys als `INVALID_ARGUMENT` listen — kein LLM. Limits ins Schema nur, wenn sie wirklich kappen. |
| Endungen mit 0 Dateien fehlen (z. B. `.html`) | `src\AiNetLinter\Mcp\Tools\FileStructure\GetIndexScopeScanner.cs` | `BuildBreakdown`; `CountNonCSharpFiles`; `FormatBreakdown` | Zähler nur für **vorkommende** Extensions (`Dictionary`). Katalog `.css/.html/.js/.razor/.xaml` aus Description als optionale Nullzeilen (`includeEmptyExtensions`) mergen; `FileTypeBreakdownEntry(".html", 0, false)`. |
| Unterordner-Root → `PROJECT_NOT_INITIALIZED` + JSON-Vorlage | `src\AiNetLinter\Mcp\Projects\ProjectToolCall.cs` | `ResolveLease` / `RecoverHint` | Korrekt nach Kanon-Root. Hint darf keine `ainetlinter.project.json` im Unterordner vorschlagen; stattdessen „eine Ebene zum integrierten Root“. Dateisystem, kein Roslyn. |
| Keine Aussage `obj`/`bin`/generated vs. Source in den 2287 `.cs` | `src\AiNetLinter\Mcp\Tools\FileStructure\GetIndexScopeScanner.cs`; `src\AiNetLinter\Baseline\SourceFileCatalog.cs`; `src\AiNetLinter\Baseline\FileSystemExclusionHelpers.cs` | `CountCsFiles`; `SourceFileCatalog.IsValidDocument`; `IsGeneratedPath` | `.cs` kommt aus `project.Documents` mit Filter `*.cs`, nicht `obj`/`bin`/`.g.cs`. Eine Zeile `sourceOnly` (bereits gefiltert) reicht; optional zweiter Zähler ohne Filter über dieselbe `Solution`. |
| Strukturierte Nutzlast nur intern; Text ohne Folgetool-Hint | `src\AiNetLinter\Mcp\Tools\FileStructure\GetIndexScopeTool.cs`; `src\AiNetLinter\Mcp\Tools\FileStructure\GetIndexScopeScanner.cs` | `ExecuteAsync` (`McpToolResults.Text(text, new { Breakdown = entries })`); `FormatBreakdown` | StructuredContent existiert (`Breakdown`/`FileTypeBreakdownEntry`). Agent sieht nur `FormatFileCountLine`. Eine Abschlusszeile: `.cs` → `find_symbol`, sonst `search_pattern`; `completeness: complete` am Objekt. |
