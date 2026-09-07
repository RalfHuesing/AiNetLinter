# Finding: `get_violations`

Ziel: `targetType=project`, `targetPath=C:\Workspace\Sample.Project`.  
Schema via `GetDynamicTools` (`user-AiNetLinter` / `get_violations`). Live-Calls nur dieses Tool. Assembly-DLL-Probe (extern) vom Auto-Review blockiert; Assembly-Verhalten nur über `targetType=assembly` plus Projektpfad.

## 1. Schema-Kurzfazit

Beschreibung steuert den **C#-Happy-Path richtig**: aktuelle Lint-Verstöße, `scopeFilter`/`scope`/`path` als Projekt- oder Pfad-Substring, `ruleId`/`rule`, `minSeverity` ∈ info/warning/error, `maxResults` Default 50, `includeSnippet` plus `contextLines` 0–5 (Default 2). `targetType`+`targetPath` Pflicht. Assembly-Ziele laut Text **ausdrücklich unsupported**.

Was in die Irre führt:

- Beispiel-Regel `'ANL0021'` existiert in dieser Solution nicht; echte IDs sind Namen (`MaxPublicMembersPerType`). Ein Agent, der das Beispiel kopiert, bekommt eine **leere, als vollständig deklarierte** Liste.
- `fileFilter` steht **nicht** im Schema (Filter heißt `scopeFilter`/`path`/`scope`). Extra-Argument `fileFilter` wird still ignoriert → Full-Scan.
- `scopeFilter` ist Substring, kein Glob: `*.cs` → „Keine Dateien im Scope“, obwohl Tausende `.cs` existieren.
- Assembly: Beschreibung sagt unsupported; der Fehler bei `targetType=assembly` behandelt das Ziel wie ein unterstütztes Assembly-Tool („Pfad muss auf .dll/.exe zeigen“).
- Grenzen von `maxResults` und `minSeverity` stehen nicht als validiert da: `maxResults=0`/`-1` und `minSeverity=not-a-severity` werden still geschluckt.

## 2. Ausgeführte Calls

Alle schnell (kein Timeout). Größen grob nach sichtbarer Textantwort. Truncation der Trefferliste **nicht auslösbar** (Solution hat nur 1 aktuelle Violation).

| # | Parameter | Größe / completeness | Wartezeit |
|---|-----------|----------------------|-----------|
| 1 | Default (kein Filter, `maxResults` 50) | ~1 KB; 1 Treffer / 2287 Dateien; `[vollständig]` | schnell |
| 2 | `maxResults=5` | wie #1; Limit greift nicht (nur 1 Treffer) | schnell |
| 3 | `targetType=assembly`, `targetPath`=Projektroot | `INVALID_ARGUMENT`: Assembly-Pfad muss .dll/.exe sein | schnell |
| 4 | `ruleId=ZZZ_NO_SUCH_RULE_XYZ` | 0 Treffer / 2287 Dateien; kein Error | schnell |
| 5 | `includeSnippet=true`, `contextLines=3` | Tabelle + 7 Snippet-Zeilen (5–11); vollständig | schnell |
| 6 | `scopeFilter=SetupText.cs` | 1 Treffer / **1 Datei**; Scope in der Kopfzeile | schnell |
| 7 | `minSeverity=error` | 0 Treffer (Warning ausgefiltert); vollständig | schnell |
| 8 | `ruleId=MaxPublicMembersPerType` | 1 Treffer / 2287; Kopf `Regel: 'MaxPublicMembersPerType'` | schnell |
| 9 | Alias `path=SetupText.cs` | wie #6 (1/1) | schnell |
| 10 | Alias `scope=Sample.Project.Tests.Logic` | 0 / 452 Dateien; kein Error | schnell |
| 11 | `minSeverity=info` | 1 Warning (info schließt warning ein) | schnell |
| 12 | Alias `rule=ANL0021` (Beispiel aus Beschreibung) | 0 / 2287; vollständig | schnell |
| 13 | `scopeFilter=zzzz-keine-datei-xyz.cs` | **„Keine Dateien im Scope“** (anders als 0 Violations) | schnell |
| 14 | `minSeverity=not-a-severity` | still akzeptiert; 1 Treffer wie Default | schnell |
| 15 | `maxResults=0` | Limit ignoriert; 1 Treffer, vollständig | schnell |
| 16 | `includeSnippet=true`, `contextLines=0`, Scope `SetupText.cs` | Snippet nur Zeile 8 | schnell |
| 17 | `includeSnippet=true`, `contextLines=99` (Schema-Max 5) | still auf ~5 Kontextzeilen geklemmt, **kein Error** | schnell |
| 18 | `minSeverity=warning` | 1 Treffer | schnell |
| 19 | `scopeFilter=*.cs` | „Keine Dateien im Scope“ (Glob ≠ Substring) | schnell |
| 20 | `scopeFilter=Program.cs` | 0 Violations / **1 Datei** (existiert, sauber) | schnell |
| 21 | `targetPath=…\README.md` (Datei statt Root) | `PROJECT_NOT_INITIALIZED` (sucht `README.md\ainetlinter.project.json`) | schnell |
| 22 | Alias `rule=MaxPublicMembersPerType` | wie #8 | schnell |
| 23 | `maxResults=-1` | Limit ignoriert; 1 Treffer | schnell |
| 24 | `path=SetupText.cs` **und** `scopeFilter=Program.cs` | **scopeFilter gewinnt** (0/1, Scope Program.cs) | schnell |
| 25 | Extra `fileFilter=SetupText.cs` (nicht im Schema) | still ignoriert; Full-Scan 1/2287 | schnell |
| 26 | `scopeFilter=wwwroot/js` | Keine Dateien im Scope | schnell |
| 27 | `scopeFilter=ComponentRegistration` | 0 / 15 Dateien | schnell |
| 28 | `scopeFilter=Sample.Project.Setup/Setup` | 1 / 51 Dateien | schnell |
| 29 | `scopeFilter=.razor` | 0 / 103 Dateien | schnell |
| 30 | `ruleId=JS_MaxJsLineCount` | 0 / 2287 | schnell |
| 31 | `ruleId=CSS_MaxCssLineCount` | 0 / 2287 | schnell |
| 32 | `ruleId=RAZOR_MaxRazorLineCount` | 0 / 2287 | schnell |
| 33 | `scopeFilter=.css` | Keine Dateien im Scope | schnell |
| 34 | `maxResults=1` | 1 Treffer, vollständig (Truncation nicht sichtbar) | schnell |

externe Assembly mit `targetType=assembly` nicht ausgeführt (Auto-Review: Ziel außerhalb des Workspace).

## 3. Verdict

**Teilweise.** Happy Path, Datei-Substring, `ruleId`/`rule`, `minSeverity=error|warning|info`, Snippets und die Unterscheidung „0 Violations in N Dateien“ vs. „Keine Dateien im Scope“ wirken wie beschrieben. Abweichend: Assembly-Text vs. Fehlermeldung, Beispiel-`ANL0021`, stilles Schlucken ungültiger Limits/Severity, Glob als Filter, Alias-Vorrang, Completeness-Hinweis trotz JS/CSS außerhalb des Index.

## 4. Schwere

**`friction`** — Kernjob (C#-Violations nach Edit, Scope auf eine Datei) funktioniert und ist klein. Schema/Beschreibung, Aliase, ungültige Parameter und der Completeness-Satz („kein zusätzliches Read/Grep nötig“) können Agenten fehlsteuern.

Zusätzlich Ansatz `degraded` nur für JS/CSS: Regel-IDs existieren, Index enthält diese Dateien aber nicht (`wwwroot/js` / `.css` → keine Dateien); 0 Treffer plus „vollständig“ wirkt wie ein echtes Negativ.

## 5. Nutzbarkeit

**Ja**, für C# nach Änderungen: Default oder `scopeFilter=<Dateiname>` plus optional `includeSnippet=true`.  

Nicht ohne Workaround: `fileFilter`, Glob `*.cs`, Beispiel-`ANL0021`, JS/CSS-Regeln als Abdeckungsnachweis, `maxResults` als harte Kappung (hier wirkungslos, weil nur 1 Treffer und 0/−1 ignoriert werden).

## 6. Bugs, FP/FN vs. Ist

Ground Truth: eine Violation gegen `Sample.Project.Setup/Setup/SetupText.cs` gelesen (kein `rg`, kein anderes MCP-Tool).

### Stichprobe — `SetupText` (Call 1/5/6 vs. Datei)

| MCP | Ist | Urteil |
|-----|-----|--------|
| Datei `…/Setup/Setup/SetupText.cs` | Datei existiert | ok |
| Zeile 8 | `public static class SetupText` | ok |
| Regel `MaxPublicMembersPerType`, 19 öffentliche Member (Limit 15) | genau **19** `public const string` (u. a. `Back`, `Browse`, `ProductName`, … `LabelServiceName`); Methoden sind `internal` | **TP** |
| Snippet Call 5/16 | Zeile 8 und Nachbarn stimmen | ok |

### Weitere FP/FN

| Probe | MCP | Ist | Urteil |
|-------|-----|-----|--------|
| `minSeverity=error` | 0 | einzige Violation ist Warning | ok |
| `Program.cs` | 0 Violations / 1 Datei | Datei existiert | ok (Leermenge ≠ Error) |
| unsinnige Datei | keine Dateien im Scope | korrekt leer | ok, klarer Text |
| `rule=ANL0021` | 0, vollständig | Regelname in dieser Solution nicht verwendet | **Beschreibung-FN-Falle** (Agent glaubt „keine ANL0021“) |
| `scopeFilter=*.cs` | keine Dateien | Tausende `.cs` | **FN durch Filter-Semantik** |
| `wwwroot/js`, `.css` | keine Dateien | JS/CSS liegen im Repo (`get_file_tree`-Wissen aus Parallel-Audit nicht hier nachgeprüft; Call 26/33) | Index-Lücke; Completeness überclaimt |
| `.razor` | 103 Dateien, 0 Violations | Razor wird gesehen | C#/Razor-Scan wirkt lebendig |
| ungültige `minSeverity` / `maxResults=0`/−1 | wie Default | Schema verspricht Filter/Limit | **still akzeptiert** (kein `INVALID_ARGUMENT`) |
| `path` + `scopeFilter` | nur `scopeFilter` | undokumentierte Vorrangregel | Reibung, kein Datenfehler |

Kein reproduzierbarer Crash. `isError` nur bei Assembly-Pfad und `PROJECT_NOT_INITIALIZED`.

## 7. Token / IDs

- Default mit 1 Treffer: klein (~Tabelle + Hinweis), für den nächsten Edit-Zyklus brauchbar.
- `includeSnippet` erhöht linear pro Treffer; `contextLines=0` bleibt minimal.
- Keine Symbol-IDs, keine Rule-Numeric-IDs, kein `cursor`/`continuationToken`. Folge-Schlüssel sind **relativer Dateipfad** + Zeile + Regelname — als `scopeFilter`/`path` wiederverwendbar.
- Completeness immer „vollständig“; Truncation ungetestet, weil n=1 < Default-50. `maxResults` ist in dieser Solution kein Token-Schutz.
- StructuredContent in dieser Oberfläche nicht sichtbar (nur Texttabelle).
- Kopfzeile nennt Scope/Regel/Min-Severity — gut für den Agenten, die Parameter echoen.

## 8. Roslyn-konforme Wünsche

Statisch/deterministisch, ohne LLM:

1. Beschreibungsbeispiel auf eine echte `ruleId` dieser Ruleset-Welt (`MaxPublicMembersPerType`), nicht `ANL0021`.
2. `maxResults` und `minSeverity` validieren (`INVALID_ARGUMENT` bei 0/−1 bzw. unbekanntem Enum); `contextLines>5` ebenfalls Fehler statt stiller Clamp.
3. Schema-Text: Filter ist **Pfad-Substring**, kein Glob; `fileFilter` nicht erwähnen bzw. als Alias verdrahten oder ablehnen.
4. Alias-Vorrang dokumentieren (`scopeFilter` > `path` in Call 24).
5. Assembly: entweder wirklich `unsupported` mit passendem Fehler, oder Beschreibung an das dll/exe-Verhalten anpassen.
6. Completeness-Hinweis nicht setzen, wenn der Scope 0 indexierte Dateien hat oder JS/CSS-Regeln auf einem C#-only-Index laufen.
7. Optionale stabile IDs (Dateipfad + Regel + Span) für Folge-Calls; bei n > `maxResults` ehrliche Truncation-WARN plus Restanzahl.

## 9. Phase 3 (AiNetLinter-Quellzeiger)

Nur Nicht-`ok`-Befunde. Kein Patch in diesem Task.

### Beschreibungsbeispiel `ANL0021` (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs`.
- **Symbol:** `GetViolationsDescription` (Literal `'ANL0021'`).
- **Ansatz:** Beispiel auf eine echte `LinterRuleIds`-Konstante stellen (`MaxPublicMembersPerType`). IDs kommen aus `RuleViolation.RuleName` (`src\AiNetLinter\Core\LinterRuleIds.cs`), nicht aus einem ANL-Nummernschema. Unbekannte `ruleId` optional als Hinweis „keine solche Regel in diesem Ruleset“, nicht als `0 Verstösse / vollständig`.

### Extra-`fileFilter` still ignoriert; Glob `*.cs` als Substring (`friction`)

- **Pfad:** `AnalysisToolRegistrations.AddGetViolations`; Filter `src\AiNetLinter\Mcp\Tools\Analysis\ViolationScopeFilter.cs`; Vergleich `src\AiNetLinter\Output\PathNormalizer.cs`; Glob bereits `src\AiNetLinter\Configuration\PathGlobMatcher.cs`.
- **Symbol:** `AddGetViolations` (`effectiveScope = scopeFilter ?? scope ?? path`, kein `fileFilter`); `ViolationScopeFilter.MatchesScope`; `PathNormalizer.MatchesScope` (`Contains`).
- **Ansatz:** `fileFilter` als Alias verdrahten **oder** unbekannte Keys `INVALID_ARGUMENT`. Filter mit `*`/`?` über `PathGlobMatcher` (Roslyn-unabhängig, Dateipfade); reiner Text bleibt Substring. Alias-Vorrang (`scopeFilter` > `scope` > `path`) in der Beschreibung nennen.

### Assembly-Text vs. DLL-Pfadvalidator (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\AnalysisTargetResolver.cs`; `src\AiNetLinter\Mcp\AnalysisToolCall.cs`; `McpToolRegistrationOptions.ReadOnlyTool`.
- **Symbol:** `AnalysisTargetResolver.Resolve`; `ProjectAnalysisDispatcher.ExecuteProjectAsync` → `UnsupportedAssemblyTarget`.
- **Ansatz:** Wie bei den anderen project-only-Tools: `targetType=assembly` sofort `ASSEMBLY_TARGET_UNSUPPORTED`, bevor `File.Exists` / `.dll`/`.exe` geprüft wird. `targetType=projectx` Hint ohne „oder assembly“, wenn das Tool Assembly nicht kann.

### `maxResults` 0/−1, ungültige `minSeverity`, `contextLines>5` still (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\Analysis\GetViolationsTool.cs`; `GetViolationsScanner.cs`; `ViolationScopeFilter.cs`.
- **Symbol:** `GetViolationsTool.ExecuteAsync` (`MaxResults < 1 ? 1`); `GetViolationsScanner.BuildViolationsTextAsync` (`Math.Clamp(ContextLines, 0, 5)`); `ViolationScopeFilter.SeverityRank` (unbekannt → `0` → kein Filter).
- **Ansatz:** `INVALID_ARGUMENT` bei `maxResults < 1`, unbekannter Severity-Enum und `contextLines` außerhalb 0–5 statt Clamp/Ignore. `SeverityRank` Default nicht als „alles durchlassen“ verwenden.

### JS/CSS-Scope + Completeness-Overclaim (`degraded`)

- **Pfad:** `ViolationScopeFilter.BuildFileToProjectMap`; `src\AiNetLinter\Baseline\SourceFileCatalog.cs`; Web-Scan `src\AiNetLinter\Web\WebFileCatalog.cs` / `WebFileSeparationChecker.cs` (über `PostAnalysisChecks` in `LinterEngine.RunAsync`); Sufficiency `GetViolationsTool` + `McpSufficiencyHints`.
- **Symbol:** `SourceFileCatalog.IsValidDocument` (nur `.cs`); `GetViolationsScanner.FormatReport` (`matchingFileCount == 0` → „Keine Dateien im Scope“); `GetViolationsTool.ExecuteAsync` hängt Hint an, wenn `!IsTruncated`.
- **Ansatz:** Dateikarte um `WebFileCatalog.Collect` erweitern (JS/CSS/Razor-Pfade), damit `scopeFilter=wwwroot/js` nicht an der C#-Document-Map scheitert. Completeness-Hinweis nicht setzen bei 0 indexierten Dateien im Scope oder wenn `ruleId` eine Web-Regel ist (`JS_MaxJsLineCount` / `CSS_MaxCssLineCount`) und der C#-Index den Scope nicht kennt.

### Truncation-Ehrlichkeit / stabile Treffer-IDs (`wish`)

- **Pfad:** `GetViolationsScanner.FormatReport`; DTO `RuleViolation`.
- **Symbol:** Truncation-Zeile existiert bereits (`N gesamt, maxResults gezeigt`); StructuredContent ohne Symbol-ID.
- **Ansatz:** Folge-Schlüssel Datei+Zeile+`RuleName` beibehalten; optional `DocumentationCommentId` am Syntaxknoten der Violations-Span (`SemanticModel.GetDeclaredSymbol` am `TypeDeclarationSyntax`/`MethodDeclarationSyntax`). Bei n=1 war Truncation nicht sichtbar — `maxResults`-Footer auch dann, wenn Limit ignoriert wurde (nach Fix der 0/−1-Validierung).
