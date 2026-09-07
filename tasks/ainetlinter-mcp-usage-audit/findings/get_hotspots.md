# Finding: `get_hotspots`

Audit-Stand: 2026-09-07. Nur Schema-Lookup (`GetDynamicTools`) plus Calls dieses Tools. Kein anderes MCP-Tool, kein Build, kein Test.

## 1 Schema-Kurzfazit

Beschreibung steuert den Call **richtig**: Zeilen-Hotspots gegen `MaxLineCount` (hier 600), vor einem Edit. `targetType=project` + absoluter `targetPath`. Assembly **ausdrücklich unsupported** — das trifft live zu (`ASSEMBLY_TARGET_UNSUPPORTED`).

`scopeFilter` (Projektname oder Pfad-Substring), `scopeType` (`production` Default / `tests` / `all`), `maxResults` (Default 50, Cap 200), `minLinePercentage` (Default 80, Bereich 0–100) stehen in der Beschreibung und funktionieren.

JSON-Schema schwächer als die Beschreibung: `targetType`/`scopeType` ohne Enum, `maxResults`/`minLinePercentage` ohne min/max. Außerhalb des dokumentierten Bereichs kein `INVALID_ARGUMENT`, sondern stilles Clamping — Agent muss die Prosa lesen, nicht nur das Schema.

Antwort ist Markdown-Tabellen (kritisch ≥95 %, Warnung ≥ `minLinePercentage`), plus Scan-Zähler. Keine Symbol-IDs. Pfade relativ zum Repo.

## 2 Calls

Projektroot: `C:\Workspace\Sample.Project`. Alle Calls **schnell** (kein Timeout).

| # | Absicht | Argumente | Größe / Completeness | Ergebnis |
|---|---|---|---|---|
| 1 | Happy Path | `project` + Root, Defaults | ~20 Tabellenzeilen, ~2,5k Zeichen; **nicht** trunkiert | 1251 `.cs` production. 6 kritisch (u. a. `SchedulerBindings.cs` 600 / 100 %), 14 Warnung. Footer fehlt (20 ≤ 50). Grün: 1231. |
| 2 | Assembly unsupported | `assembly` + extern-`BusinessOperation.dll` | ~4 Zeilen | `[ERROR]: ASSEMBLY_TARGET_UNSUPPORTED` + Hint auf `project` / andere Roslyn-Abfrage. |
| 3 | Fehler, kein Projekt | `project`, `C:\Workspace\DoesNotExist` | ~12 Zeilen | `PROJECT_NOT_INITIALIZED` — sucht `ainetlinter.project.json`, liefert JSON-Vorlage. |
| 4 | Truncation `maxResults` | wie 1, `maxResults=3` | ~20 Zeilen | 3 kritisch gezeigt. Warnung-Sektion **„Keine.“** Footer: `[20 Hotspots gesamt, 3 gezeigt — maxResults erhöhen]`. Grün bleibt 1231. |
| 5 | Schwelle 0 + Truncation | `minLinePercentage=0`, `maxResults=5` | ~20 Zeilen | 5 kritisch. Warnung „Keine.“ Grün **0**. Footer: `[1251 Hotspots gesamt, 5 gezeigt]`. |
| 6 | `scopeType=tests` | `maxResults=10` | ~25 Zeilen, trunkiert | 1036 Testdateien, 32 Hotspots, 10 gezeigt. Mix Components/Logic-Tests. |
| 7 | `scopeFilter=Scheduler` | `maxResults=20` | ~20 Zeilen, vollständig | 262 Dateien, 9 Hotspots (4 kritisch, 5 Warnung), kein Footer. |
| 8 | Leer, Filter | `scopeFilter=ZZZNoSuchScope_xyz123` | 1 Zeile | `Keine Dateien im Scope (Filter: '…') — Filter pruefen.` Kein Scan-Zähler, keine Tabelle. |
| 9 | `scopeType=all` | `maxResults=8` | ~25 Zeilen, trunkiert | 2287 Dateien, 52 Hotspots. Prod+Tests gemischt, sortiert nach Zeilen. Warnung „Keine.“ trotz 44 restlicher Hotspots. |
| 10 | `minLinePercentage=100` | Defaults | ~12 Zeilen | Nur die zwei exakt 600-Zeilen-Dateien. `SchedulerExternalDropCoordinator.cs` (599, Anzeige sonst „100 %“) **fehlt**. Warnung-Header `>= 100%`. |
| 11 | Ungültiges `targetType` | `bogus` | ~3 Zeilen | `INVALID_ARGUMENT`: nur `project` oder `assembly`. |
| 12 | Cap-nah, 50 %-Schwelle | `maxResults=200`, `minLinePercentage=50` | ~70 Zeilen, vollständig | 70 Hotspots, kein Footer. Grün 1181. |
| 13 | `minLinePercentage=150` | außerhalb 0–100 | wie Call 10 | **Still auf 100 geklemmt.** Kein Fehler. |
| 14 | `maxResults=0` | — | wie Call 1 | **Still Default 50.** Alle 20 Hotspots, kein Fehler. |
| 15 | `maxResults=201` | Default-Schwelle 80 | wie Call 1 | Bei nur 20 Treffern nicht vom Cap unterscheidbar. |
| 16 | Ungültiges `scopeType` | `prod` | ~3 Zeilen | `INVALID_ARGUMENT` mit zulässigen Werten — gut. |
| 17 | Filter Projektname | `scopeFilter=Sample.Project.Domain.Firmenkalender` | ~12 Zeilen | 9 Dateien, 1 Warnung (`FirmenkalenderCommandExecutor.cs` 517 / 86 %). |
| 18 | Cap-Nachweis | `maxResults=201`, `minLinePercentage=0` | **groß** (~200 Tabellenzeilen, Token-Last) | Footer: `[1251 Hotspots gesamt, 200 gezeigt]`. Cap **200** bestätigt. Grün 0. |
| 19 | `maxResults=-1` | — | wie Call 1 | Still Default 50. |
| 20 | `minLinePercentage=-5` | Default `maxResults` | ~50 Zeilen, trunkiert | Still Schwelle 0. Footer 1251/50. |
| 21 | Relativer Pfad | `targetPath=Sample.Project` | ~3 Zeilen | `INVALID_ARGUMENT`: Pfad muss absolut sein. |

## 3 Verdict

**Teilweise wie beschrieben.** Happy Path, Filter, `scopeType`, Assembly-Absage, Fehlercodes und Sortierung (Zeilen absteigend) stimmen. Truncation-Footer ist brauchbar.

Abweichungen: Werte außerhalb 0–100 / `maxResults` ≤ 0 werden **still geklemmt**; Warnung-Sektion bei voller Critical-Quote sagt **„Keine.“** obwohl der Footer weitere Hotspots nennt; Prozentanzeige rundet 599/600 auf **100 %**, der Filter bei `minLinePercentage=100` schließt dieselbe Datei aus. JSON-Schema ohne Enums/Bounds.

## 4 Schwere

**`friction`**

Kernnutzen (welche `.cs` liegen nahe `MaxLineCount`?) ist korrekt und schnell. Reibung: stille Clamps, irreführendes „Keine.“ unter Truncation, gerundete 100 %-Anzeige vs. exakter Filter. Nicht `broken` (kein falscher Zeilenzähler in der Stichprobe). Nicht `degraded` in der Default-Nutzung (20 Treffer, kein Token-Flood). Token-Risiko nur bei bewusst `minLinePercentage=0` plus Cap 200.

## 5 Nutzbarkeit

**ja** — vor einem Edit an Scheduler/Handler-Dateien sinnvoll, mit `scopeFilter` eingrenzen.

Workaround: Footer lesen, nicht die Warnung-Sektion „Keine.“; `minLinePercentage`/`maxResults` im dokumentierten Bereich lassen; für Komplexität nicht dieses Tool (nur LOC).

## 6 Bugs+FP/FN

| Befund | Art | Call |
|---|---|---|
| `maxResults` 0/−1 → still Default 50 | Clamp ohne Fehler | 14, 19 |
| `minLinePercentage` 150/−5 → still 100 bzw. 0 | Clamp ohne Fehler; Schema sagt 0–100 | 13, 20 |
| Warnung „Keine.“ bei Truncation, obwohl Footer Rest nennt | Irreführend, kein Zahlen-FP | 4, 5, 9 |
| 599 Zeilen als „100 %“, bei Schwelle 100 aber ausgeschlossen | Anzeige-Rundung vs. Filter | 1 vs. 10 |
| Cap 200 bei `maxResults=201` | Wie beschrieben, Footer korrekt | 18 |

**Stichprobe Komplexität / LOC:** `SchedulerBindings.cs` — Tool: 600 Zeilen, 100 %, 0 verbleibend. Datei endet Zeile 599 mit schließender Klasse, Zeile 600 leer. **LOC-Hotspot ist real**, Datei sitzt am `MaxLineCount`. Das Tool misst **nicht** zyklomatische Komplexität; der Rumpf ist vor allem Bindings-/Weiterleitung an Collaborators. „Hotspot“ = Größe, nicht McCabe. Kein LOC-False-Positive.

Kein LOC-FN in der Stichprobe. Tool scannt nur `.cs` (Ausgabe sagt das); Razor/JS-Limits (`RAZOR_MaxRazorLineCount`, `JS_MaxJsLineCount`) erscheinen nicht — laut Beschreibung Absicht (`MaxLineCount`).

Leermenge (Call 8) ist klar, kein 200-mit-leerer-Tabelle.

## 7 Token/IDs

Default (Call 1): kompakt, agententauglich. Call 18: ~200 Zeilen Tabelle, unnötig für den dokumentierten Zweck.

Keine Symbol-IDs, kein Cursor. Folge-Call über **relative Pfade** (`get_file_skeleton` / `view_file` / `get_violations` + gleicher `scopeFilter`). Pfade stabil, Windows mit `/`.

StructuredContent (Gesamt/Anzeige/Truncation) laut Beschreibung vorhanden; in der Agent-Antwort nur Markdown plus Footer `[N gesamt, M gezeigt]`. Footer reicht zum Nachziehen von `maxResults`.

## 8 Roslyn-Wünsche

- `INVALID_ARGUMENT` bei `maxResults < 1` oder `maxResults > 200` und bei `minLinePercentage` außerhalb 0–100 (statt Clamp).
- Schema: Enum `targetType`/`scopeType`, min/max an den Zahlenfeldern.
- Truncation: Warnung-Sektion nicht „Keine.“, sondern „nicht gezeigt, siehe Footer“.
- Prozent ohne irreführendes Runden auf 100 bei 599/600, oder Filter und Anzeige dieselbe Rundung.
- Optional: gleiche Hotspot-Logik für `CSS_MaxCssLineCount` / `JS_MaxJsLineCount` / `RAZOR_MaxRazorLineCount` (Roslyn-Document-LOC, kein LLM).
- Optional: neben LOC die bestehende McCabe-/Cognitive-Metrik der Datei (bereits in Roslyn-Analyzern), klar getrennt von `MaxLineCount`.

## 9 Phase 3 (AiNetLinter-Quelle)

Welle 3, read-only `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Kein Patch.

| Befund | Pfad | Symbol | Roslyn-/statischer Ansatz |
|---|---|---|---|
| `maxResults` 0/−1 → still Default 50; `201` → Cap 200 | `src\AiNetLinter\Mcp\Tools\FileStructure\GetHotspotsScanner.cs`; `src\AiNetLinter\Mcp\Registration\FileStructureToolRegistrations.cs` | `NormalizeMaxResults` (`maxResults < 1 ? DefaultMaxResults : Clamp(..., MaxResultsCap)`); `AddGetHotspots` (`int maxResults = DefaultMaxResults`) | Schema vom C#-`int` ohne min/max. Statt Clamp: außerhalb 1–200 `INVALID_ARGUMENT` mit erlaubtem Bereich (wie `IsValidScopeType`). Cap 200 bleibt `MaxResultsCap`. |
| `minLinePercentage` 150/−5 still 100/0 | `src\AiNetLinter\Mcp\Tools\FileStructure\GetHotspotsScanner.cs` | `NormalizeMinLinePercentage` (`Math.Clamp` auf `MinLinePercentage`/`MaxLinePercentage`; NaN/Inf → Default 80) | Gleicher Vertrag: außerhalb 0–100 `INVALID_ARGUMENT`. Schema-Bounds an `double minLinePercentage` (Description nennt 0–100 bereits). |
| Warnung „Keine.“ bei Truncation, Footer nennt Rest | `src\AiNetLinter\Mcp\Tools\FileStructure\GetHotspotsScanner.cs`; `src\AiNetLinter\Output\HotspotTableFormatter.cs` | `BuildHotspots` (`shown.Take` → `critical`/`warning` nur aus **Shown**); `FormatReport`; `HotspotTableFormatter.AppendSection` (leere Liste → `"Keine."`) | Sortierung ist LOC absteigend; die ersten N sind oft nur kritisch. Warnung-Sektion sieht die Restmenge nicht. Ansatz: leere Warnung bei `TotalHotspots > ShownHotspots` als „nicht gezeigt, siehe Footer“; `AppendSection` bekommt Truncation-Flag. Zahlen bleiben `GetUtilization` auf `SolutionFileWalker.TryReadAllLines`. |
| 599 Zeilen als „100 %“, Filter bei Schwelle 100 schließt aus | `src\AiNetLinter\Output\HotspotTableFormatter.cs`; `src\AiNetLinter\Mcp\Tools\FileStructure\GetHotspotsScanner.cs` | `AppendSection` (`$"{pct:F0} %"`); `GetUtilization`; `Where(f => GetUtilization >= effectiveMinLinePercentage)` | Anzeige rundet (`F0`), Filter nutzt den `double`. Eine Nachkommastelle wie `HotspotEntry.UtilizationPercent` (`Math.Round(..., 1)`) **oder** Filter und Text dieselbe Rundung. LOC: `SourceText.Lines` / Dateizeilen, kein McCabe. |
| JSON-Schema ohne Enums/Bounds | `src\AiNetLinter\Mcp\Registration\FileStructureToolRegistrations.cs`; `src\AiNetLinter\Mcp\Tools\McpToolRegistrationOptions.cs` | `AddGetHotspots`; `ReadOnlyTool` | SDK inferiert `string`/`int`/`double`. `scopeType` ist runtime-enum (`IsValidScopeType`); `targetType` prüft `AnalysisTargetResolver.ResolveTargetType`. Enums/min/max in Description sind da — Schema nachziehen, sobald das SDK Attribute hergibt, sonst weiterhin Laufzeit-`INVALID_ARGUMENT`. |
| Assembly-Absage (ok live mit DLL) | `src\AiNetLinter\Mcp\AnalysisToolCall.cs`; `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResponse.cs` | `ProjectAnalysisDispatcher.UnsupportedAssemblyTarget`; `AssemblyAnalysisResponse.Unsupported` | Bereits `ASSEMBLY_TARGET_UNSUPPORTED`, wenn `Resolve` die DLL akzeptiert. Kein weiterer Ansatz; Verzeichnis-Ziel bleibt Resolver-`INVALID_ARGUMENT` (siehe `get_index_scope`). |
| Optional: CSS/JS/Razor-LOC-Limits | `src\AiNetLinter\Mcp\Tools\FileStructure\SolutionFileWalker.cs`; `src\AiNetLinter\Core\RuleRegistry.Web.cs`; `src\AiNetLinter\Web\WebFileCatalog.cs` | `CollectFiles` (`IsValidDocument` → nur `.cs`); `BuildCssMaxCssLineCount` / `BuildJsMaxJsLineCount` / `BuildRazorMaxRazorLineCount` | Walker ist C#-Documents. Dieselben Limits existieren als Web-Regeln. Zweiter Walk über `WebFileCatalog` + Zeilenzahl, Kategorie pro Limit — Document-LOC, kein LLM. |
| Optional: McCabe/Cognitive neben LOC | `src\AiNetLinter\Mcp\Tools\MetricsTree\MetricsTreeRoslynScanner.cs`; `src\AiNetLinter\Mcp\Tools\MetricsLookup\MetricsLookupScanner.cs` | `ComplexityCalculator.GetCyclomaticComplexity`; `MetricNames.CyclomaticComplexity` | Nicht in `get_hotspots` mischen. Verweis auf bestehende Analyzer-Metriken; Datei-Hotspot bleibt `MaxLineCount`. |
