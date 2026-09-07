# Finding: `pattern_detect`

Audit-Stand: 2026-09-07. Nur Schema-Lookup (`GetDynamicTools`) plus Calls dieses Tools. Kein anderes MCP-Tool, kein Build, kein Test.

Anker: `SetupText` (`Sample.Project.Setup/Setup/SetupText.cs`). Assembly-Probe: extern-`BusinessOperation.dll` (unsupported laut Schema).

## 1 Schema-Kurzfazit

Beschreibung steuert den Call **größtenteils richtig**: Solution-weite Pattern-Suche, gruppiert nach Kategorie, statt der flachen Violations-Liste. Pflicht `targetType=project` + absoluter `targetPath`. Assembly **ausdrücklich unsupported** — live bestätigt. Default: alle 6 IDs (`god-class`, `async-void`, `long-method`, `public-without-doc`, `empty-catch`, `feature-envy`). Aliase `pattern` / `patterns`, `scopeFilter` / `scope` / `path`, `maxResultsPerPattern` Default 20 — funktionieren.

JSON-Schema schwächer als die Prosa: `targetType` ohne Enum, Pattern-IDs **kein Enum**, `maxResultsPerPattern` ohne min/max, `patterns`-Items dürfen `null` sein. Agent muss die Beschreibung lesen; nach einem Tippfehler rettet der `INVALID_ARGUMENT`-Hint mit der vollständigen ID-Liste (kein Raten nötig).

Lücken der Beschreibung:

- Kein `scopeType` (production/tests). Default scannt **alle** 2287 Dateien (Prod+Tests, gleiche Zahl wie bei anderen Tools unter `all`).
- Kein Hinweis, dass leere Pattern-Kategorien (0 Treffer) **weggelassen** werden, sobald mindestens ein anderes Pattern trifft.
- Kein Hinweis auf Alias-Vorrang, wenn `pattern` **und** `patterns` bzw. `scopeFilter` **und** `path` gleichzeitig gesetzt sind.
- Completeness-Hinweis „kein zusätzliches Read/Grep nötig“ überclaimt: ein Agent soll der Leermenge vertrauen.
- Keine Symbol-IDs; Treffer sind `relativerPfad:Zeile` plus Regelname.

`feature-envy` ist in der Antwort ehrlich als Middle-Man-Näherung gekennzeichnet — das Schema selbst sagt das nicht.

## 2 Calls

Projektroot: `C:\Workspace\Sample.Project`. Alle Calls **schnell**, kein Timeout. Größen = sichtbarer Markdown-Text.

| # | Absicht | Argumente | Größe / Completeness | Ergebnis |
|---|---|---|---|---|
| 1 | Happy Path, Defaults | `project` + Root, alle 6 Patterns | ~8 Zeilen, ~0,8k; **nicht** trunkiert | Header: 1 von 6 Patterns mit Treffern, 1 Treffer, 2287 Dateien. Nur Sektion `god-class` (`SetupText.cs:8`, `MaxPublicMembersPerType` 19/15). Die fünf leeren Patterns **fehlen**. Completeness: „vollständig … kein Read/Grep“. |
| 2 | Assembly unsupported | `assembly` + extern-`BusinessOperation.dll` | ~4 Zeilen | `ASSEMBLY_TARGET_UNSUPPORTED` + Hint auf `project` / andere Roslyn-Abfrage. |
| 3 | Fehler, kein Projekt | `project`, `C:\Workspace\DoesNotExist` | ~12 Zeilen | `PROJECT_NOT_INITIALIZED` + Bootstrap-JSON für `ainetlinter.project.json`. |
| 4 | Truncation | Defaults + `maxResultsPerPattern=2` | identisch Call 1 | Nur 1 Treffer gesamt — Truncation-UI **nicht beobachtbar**. Kein Footer „N gesamt, M gezeigt“. |
| 5 | Alias `pattern` | `pattern=god-class` | ~8 Zeilen, vollständig | 1 von 1 Patterns, derselbe `SetupText`-Treffer. |
| 6 | Leer, Filter | `scopeFilter=ZZZNoSuchScope_xyz123` | 1 Zeile | `Keine Dateien im Scope (Filter: '…') — Filter pruefen.` Kein Completeness-Hinweis. |
| 7 | Einzel `async-void` | `pattern=async-void` | ~6 Zeilen | 0 von 1, Sektion mit **„Keine.“**, Completeness-Hinweis. 2287 Dateien. |
| 8 | Einzel `long-method` | `pattern=long-method` | ~6 Zeilen | 0 Treffer, „Keine.“ |
| 9 | Einzel `empty-catch` | `pattern=empty-catch` | ~6 Zeilen | 0 Treffer, „Keine.“ |
| 10 | Einzel `public-without-doc` | `pattern=public-without-doc` | ~6 Zeilen | **0 Treffer** trotz `SetupText` ohne XML-Docs (siehe §6). |
| 11 | Einzel `feature-envy` | `pattern=feature-envy` | ~8 Zeilen | 0 Treffer; Beschreibung nennt Middle-Man-Näherung. |
| 12 | Ungültige Pattern-ID | `pattern=not-a-real-pattern` | ~3 Zeilen | `INVALID_ARGUMENT` + vollständige ID-Liste. Gut. |
| 13 | Ungültiges `targetType` | `bogus` | ~3 Zeilen | `INVALID_ARGUMENT`: nur `project` oder `assembly`. |
| 14 | Cap 1 | `pattern=god-class`, `maxResultsPerPattern=1` | wie Call 5 | 1 Treffer, kein Truncation-Banner (Menge = Cap). |
| 15 | `maxResultsPerPattern=0` | Defaults | wie Call 1 | **Still Default.** Treffer sichtbar, kein Fehler. |
| 16 | `maxResultsPerPattern=-1` | Defaults | wie Call 1 | Still Default. |
| 17 | Alias `path` | `path=Setup` | ~8 Zeilen | 65 Dateien, Filter `'Setup'`, 1 `god-class`. |
| 18 | Alias `scope` | `scope=Sample.Project.Setup` | ~8 Zeilen | 153 Dateien, 1 Treffer. |
| 19 | Array `patterns` | `["god-class","long-method","empty-catch"]` | ~8 Zeilen | 1 von 3; nur `god-class` gezeigt, Leere weggelassen. |
| 20 | Case | `pattern=GOD-CLASS` | wie Call 5 | Case-insensitive akzeptiert. |
| 21 | Tests-Scope | `scopeFilter=Tests.Logic` | ~4 Zeilen | 452 Dateien, **0 von 6**, Text „Keine Auffälligkeiten gefunden.“ (ohne Pattern-Sektionen). Completeness-Hinweis. |
| 22 | Leeres Array | `patterns=[]` | wie Call 1 | Wie Default alle 6. |
| 23 | Konflikt Aliase | `pattern=god-class` **und** `patterns=["async-void"]` | wie Call 7 | **`patterns` gewinnt**, `pattern` still ignoriert. 0 Treffer `async-void`. |
| 24 | Relativer Pfad | `targetPath=Sample.Project` | ~3 Zeilen | `INVALID_ARGUMENT`: Pfad muss absolut sein. |
| 25 | Underscore-ID | `pattern=god_class` | ~3 Zeilen | `INVALID_ARGUMENT` (Bindestrich-IDs Pflicht). |
| 26 | Gemischt gültig/ungültig | `patterns=["god-class","not-a-real-pattern"]` | ~3 Zeilen | Ganzer Call Fehler, gültiges Pattern wird nicht ausgeführt. |
| 27 | Zwei Scope-Aliase | `scopeFilter=Setup` + `path=Setup` | ~8 Zeilen | Filter **`'Setup'`**, 155 Dateien. `path` still ignoriert (Call 17 hatte 65 Dateien / `'Setup'`). |
| 28 | Leerer String | `pattern=""` | ~3 Zeilen | `INVALID_ARGUMENT`: unbekannte ID `(leer)`. |
| 29 | Großes Limit | `maxResultsPerPattern=999` | wie Call 1 | Kein Token-Flood (nur 1 Treffer). Cap unklar, weil Menge winzig. |
| 30 | Nicht-C# | `scopeFilter=wwwroot` | 1 Zeile | Keine Dateien im Scope (JS nicht indexiert). |
| 31 | Razor-Filter | `scopeFilter=.razor` | ~4 Zeilen | **103 Dateien** im Scope, 0 von 6, „Keine Auffälligkeiten.“ C#-Patterns auf Razor-Zählung. |
| 32 | Domain-Scope | `pattern=god-class`, `scope=Domain.Firmenkalender` | ~6 Zeilen | 9 Dateien, 0 Treffer, Sektion „Keine.“ |
| 33 | `null` im Array | `patterns=["god-class", null]` | ~3 Zeilen | `INVALID_ARGUMENT` unbekannte ID leer (Schema erlaubt `null`-Items). |
| 34 | Pflichtfeld fehlt | nur `targetType` + `pattern` | Invoke-Fehler | Cursor: „An error occurred invoking 'pattern_detect'.“ Kein sauberer MCP-`INVALID_ARGUMENT`. |
| 35 | Bekannte große Datei | `scopeFilter=SchedulerBindings` | ~4 Zeilen | 17 Dateien, 0 von 6. `god-class` (Zeilen/Footprint) feuert hier nicht. |

## 3 Verdict

**Teilweise wie beschrieben.** Happy Path, Aliase, Assembly-Absage, leerer Scope, ungültige IDs und absolute-Pfad-Prüfung stimmen. Die Antwort ist kompakt.

Abweichungen: fünf der sechs Default-Patterns liefern in dieser Solution **0 Treffer** (davon `public-without-doc` nachweislich falsch, §6); Default-Ausgabe **verschweigt** die leeren Kategorien; Completeness-Text verbietet Nachprüfung; `maxResultsPerPattern` 0/−1 stilles Default statt Fehler; bei Doppel-Alias stiller Vorrang (`patterns` > `pattern`, `scopeFilter` > `path`); Truncation-Verhalten am Limit **nicht sichtbar**, weil die Treffermenge 1 ist; fehlendes `targetPath` ist Invoke-Fehler statt Server-Hint.

## 4 Schwere

**`degraded`**

Nicht `broken`: Call gelingt, der eine `god-class`-Treffer ist korrekt und actionable. Nicht nur `friction`: ein Agent, der Defaults nimmt, hält 5/6 Kategorien für sauber und folgt dem Hinweis, nicht zu greppen — das ist faktisch falsch bei `public-without-doc` und riskant bei den übrigen Leermengen. Token/Latenz sind unproblematisch.

## 5 Nutzbarkeit

**nur mit Workaround**

Brauchbar für `god-class` mit `pattern=god-class` und optional `scopeFilter`. Nicht als Ersatz für `get_violations` über alle sechs Kategorien. Workaround: jede Pattern-ID einzeln aufrufen; Completeness-Hinweis ignorieren; XML-Docs / Catch / Methodenlänge nicht aus 0-Treffer ableiten; Aliase nicht kombinieren.

## 6 Bugs+FP/FN

| Befund | Art | Call |
|---|---|---|
| `SetupText` 19 öffentliche `const` ohne jedes `///`, Limit 15 | **TP** `god-class` / `MaxPublicMembersPerType` | 1, Datei gelesen: genau 19 `public const` (Back, Browse, TestConnection, ProductName, AdminIntro, FeatureSchedule, FeatureXrm, LabelServer, LabelDatabase, LabelSqlUser, LabelPassword, RuntimeHint, AdminHint, ReviewConnectionsTitle/Hint, ReviewPacksTitle/Hint, LogFileLabel, LabelServiceName). Klasse Zeile 8. |
| Dieselbe Klasse: keine XML-Docs an öffentlichen Membern, Pattern `public-without-doc` = 0 | **FN** | 10 vs. Datei `SetupText.cs`. Projektregel `EnforceXmlDocumentation` ist deaktiviert — Detektor liefert trotzdem „0 Treffer / vollständig“, nicht „Regel aus“. |
| Default blendet 0-Treffer-Patterns aus | Irreführende Vollständigkeit | 1 vs. 7–11 |
| Completeness „kein Read/Grep“ bei nachweisbarem FN | Agent-Falle | 1, 10 |
| `maxResultsPerPattern` 0/−1 → still Default | Clamp ohne Fehler | 15, 16 |
| `pattern` + `patterns`: nur Array gilt | Stiller Alias-Vorrang | 23 |
| `scopeFilter` + `path`: nur `scopeFilter` gilt | Stiller Alias-Vorrang | 27 vs. 17 |
| `patterns` mit einem ungültigen Eintrag: ganzer Call tot | Kein Partial | 26, 33 |
| Fehlendes `targetPath` | Invoke-Fehler statt `INVALID_ARGUMENT` | 34 |
| `.razor`-Scope zählt 103 Dateien, C#-Patterns 0 | Erwartbar, aber Zähler ≠ Pattern-Sprache | 31 |
| `god-class`-Label auf String-Konstanten-Klasse | Naming-FP, Regelzähler korrekt | 1 |

`async-void` / `long-method` / `empty-catch` / `feature-envy`: **0 Treffer in 2287 Dateien** (Calls 7–9, 11). Ohne anderes MCP/`rg` kein Vollabgleich. Nicht als FN behauptet; als Agent aber **nicht** als Nachweis „Codebase sauber“ verwendbar — Completeness-Text legt das Gegenteil nahe.

Leermenge mit 0 Dateien (Call 6, 30) ist klar und nicht mit 0-Violations (Call 21) verwechselbar.

## 7 Token/IDs

Default und alle Treffer-Calls: **sehr kompakt** (~0,5–0,8k Zeichen). Kein Token-Risiko in dieser Solution.

Keine Roslyn-Symbol-IDs, kein Cursor, kein StructuredContent in der Agent-Antwort. Trefferform:

`Sample.Project.Setup/Setup/SetupText.cs:8 - MaxPublicMembersPerType: 'SetupText' hat 19 …`

**Stabil:** relativer Repo-Pfad mit `/`, Zeile der Typdeklaration, Regelname, Typname in Quotes. Folge-Call über Pfad (`view_file` / `get_class_structure` / `find_symbol` `SetupText`), nicht über kopierbare Doc-ID. Zeile 8 bleibt stabil, solange die Klassendeklaration nicht wandert.

Truncation: Parameter existiert, Footer „gesamt/gezeigt“ trat **nie** auf. Agent kann nicht lernen, `maxResultsPerPattern` zu erhöhen.

## 8 Roslyn-Wünsche

- `public-without-doc` entweder an `EnforceXmlDocumentation` koppeln und **explizit „Regel deaktiviert“** ausgeben, oder unabhängig zählen (dann müsste `SetupText` erscheinen).
- Default-Lauf: leere Kategorien als `0 Treffer` stehen lassen **oder** Footer „5 Patterns ohne Treffer: …“, niemals „kein Grep nötig“.
- Completeness-Hinweis nur wenn wirklich alle aktivierten Detektoren gelaufen sind; bei deaktivierter Regel kein „vollständig“.
- `INVALID_ARGUMENT` bei `maxResultsPerPattern < 1`; Schema-Enum für die sechs Pattern-IDs und `targetType`.
- Konflikt zweier Aliase: Fehler oder Merge-Hinweis, kein stilles Gewinner-Feld.
- Treffer um **stabile Symbol-ID** (DocId / `SetupText`) ergänzen, nicht nur Pfad:Zeile.
- Truncation-Footer analog anderer Tools (`N gesamt, M gezeigt`), sobald Cap greift.
- Optional: `scopeType` wie bei Hotspots (`production`/`tests`/`all`), Default in der Beschreibung nennen.
- `feature-envy` im Schema/ID ehrlich `middle-man` nennen (Antwortstext tut das schon).

## 9 Phase 3

AiNetLinter-Root: `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Je Nicht-ok-Befund: Pfad + Symbol + Roslyn-Ansatz.

### `public-without-doc` = 0 trotz fehlender `///` (Call 10; Regel aus)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\PatternDetect\PatternCatalog.cs`, `src\AiNetLinter\Core\RuleRegistry.General.cs`, `src\AiNetLinter\Core\Checkers\NamingChecker.cs`
- **Symbol:** `PatternCatalog` (`public-without-doc` → `LinterRuleIds.EnforceXmlDocumentation`), `RuleMetadata.IsEnabled` (`c => c.Global.EnforceXmlDocumentation`), `NamingChecker` (früher Return wenn Flag false)
- **Ansatz:** Pattern hängt an `LinterEngine`-Violations; deaktivierte Regel erzeugt keine `RuleViolation`. Entweder Sektion „Regel deaktiviert (`EnforceXmlDocumentation=false`)“ statt 0 Treffer / „vollständig“, oder Detektor unabhängig zählen (`ISymbol.GetDocumentationCommentXml` / Leading-Trivia `DocumentationCommentTrivia` an `public` Membern von `INamedTypeSymbol`). Kein LLM.

### Default blendet 0-Treffer-Patterns aus (Call 1 vs. 7–11)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\PatternDetect\PatternDetectScanner.cs`
- **Symbol:** `FormatReport` (`reports.Where(report => reports.Count == 1 || report.Entry.Occurrences > 0)`; bei allen 0 und `Count != 1`: nur „Keine Auffälligkeiten gefunden.“)
- **Ansatz:** Immer alle angefragten IDs als `## id (0 Treffer)` / „Keine.“ ausgeben, oder Footer `Patterns ohne Treffer: async-void, …`. StructuredContent hat die Einträge bereits (`PatternDetectPayload.Patterns`).

### Completeness „kein Read/Grep“ bei FN / deaktivierter Regel (Call 1, 10)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\PatternDetect\PatternDetectTool.cs`, `src\AiNetLinter\Mcp\McpSufficiencyHints.cs`
- **Symbol:** `PatternDetectTool.ExecuteAsync` (`McpSufficiencyHints.Append` auf jedem Report mit Payload)
- **Ansatz:** `Append` nur wenn jedes angeforderte Pattern entweder Treffer hat oder explizit als „Regel aus“ / „0, Detektor gelaufen“ gekennzeichnet ist. Bei deaktiviertem `EnforceXmlDocumentation` kein Vollständigkeits-Satz.

### `maxResultsPerPattern` 0/−1 stilles Clamp (Call 15, 16)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\PatternDetect\PatternDetectTool.cs`
- **Symbol:** `ExecuteAsync` (`Math.Max(1, maxResultsPerPattern)`)
- **Ansatz:** `< 1` → `INVALID_ARGUMENT`. Schema-Minimum 1. Truncation-Footer greift erst bei `Occurrences > maxResultsPerPattern` (`McpTruncation.TruncateLines` in `FormatReport`).

### Truncation-Footer nie sichtbar (Call 4, 14; Menge = 1)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\PatternDetect\PatternDetectScanner.cs`, `src\AiNetLinter\Mcp\McpTruncation.cs`
- **Symbol:** `FormatReport` / `McpTruncation.TruncateLines` (Footer nur wenn `totalMatches > maxResults`)
- **Ansatz:** Verhalten ist korrekt am Cap; in der Beschreibung sagen, dass der Footer nur bei `Occurrences > maxResultsPerPattern` erscheint. Optional immer `N gesamt, M gezeigt` auch bei M=N.

### `pattern` + `patterns`: Array gewinnt still (Call 23)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs`
- **Symbol:** `AddPatternDetect` (`var effectivePatterns = patterns ?? (pattern is not null ? [pattern] : null)`)
- **Ansatz:** Beide gesetzt → `INVALID_ARGUMENT` oder Union. Leeres `patterns=[]` ist absichtlich Default-alle (`ResolvePatterns`: `Count == 0` → Katalog).

### `scopeFilter` + `path`: nur `scopeFilter` (Call 27 vs. 17)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs`
- **Symbol:** `effectiveScope = scopeFilter ?? scope ?? path`
- **Ansatz:** Zwei Scope-Aliase gleichzeitig → Fehler oder dokumentierte Präzedenz in der Tool-Beschreibung (nicht nur stilles `??`).

### Gemischt gültig/ungültig / `null` im Array: ganzer Call tot (Call 26, 33)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\PatternDetect\PatternDetectTool.cs`
- **Symbol:** `ResolvePatterns` (`unknown.Count > 0` → ein `INVALID_ARGUMENT`; `null`-Items werden zu leerer ID)
- **Ansatz:** `null`/unbekannt enumerieren; gültige IDs trotzdem scannen **oder** Fehlertext mit den gültigen IDs behalten (ist schon da). Schema: `patterns.items` ohne `null`.

### Fehlendes `targetPath`: Invoke-Fehler statt `INVALID_ARGUMENT` (Call 34)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs`, `src\AiNetLinter\Mcp\AnalysisTargetResolver.cs`
- **Symbol:** `AddPatternDetect` (`string targetPath` Pflicht im C#-Delegate), `AnalysisTargetResolver.Resolve` (leeres `targetPath` → `INVALID_ARGUMENT`, erreicht den Server nicht)
- **Ansatz:** Server-Pfad ist schon korrekt. Cursor-SDK bricht vorher ab. Beschreibung/Schema `required: [targetType, targetPath]` hart lassen; kein Roslyn-Thema.

### `.razor`-Scope zählt 103 Dateien, Patterns 0 (Call 31)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\Analysis\ViolationScopeFilter.cs`, `src\AiNetLinter\Baseline\SourceFileCatalog.cs`
- **Symbol:** `CountMatchingFiles` / `MatchesScope` (Substring), `IsValidDocument` (nur `.cs`)
- **Ansatz:** Filter `.razor` trifft `*.razor.cs` per `Contains`. Zähler als „C#-Dokumente im Scope“ labeln oder Extension-Match (`.razor` ≠ `.razor.cs`). `LinterEngine` läuft nur über C#-Syntax; Razor-Markup bleibt 0 Treffer — das in der Leer-Sektion sagen.

### `god-class` auf String-Konstanten-Klasse (Call 1, Naming-FP)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\PatternDetect\PatternCatalog.cs`, `src\AiNetLinter\Mcp\Tools\MetricsLookup\MetricsLookupScanner.cs` (gleiche Public-Member-Zählung)
- **Symbol:** `PatternDefinition` `god-class` → `MaxPublicMembersPerType`; `IsPublicMember` zählt `public const`
- **Ansatz:** Regelzähler ist korrekt (19/15). Optional Exemption: Typ nur `const`/`static` Felder (`IFieldSymbol.IsConst`, keine Ordinary-Methods) → nicht unter `god-class` listen, oder Katalogtext „Public-Member-Limit, nicht zwingend God-Class“.

### Keine Symbol-IDs in Trefferzeilen (§7, Wunsch)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\PatternDetect\PatternDetectScanner.cs`
- **Symbol:** `ToItem` / `FormatLine` (`FilePath`, `LineNumber`, `RuleName`, `Details`)
- **Ansatz:** `RuleViolation` um DocCommentId ergänzen, wo der Checker das `ISymbol` hat; sonst aus `FilePath`+Zeile `SemanticModel.GetDeclaredSymbol` am Span. In die Zeile `` `T:…SetupText` ``.

### `feature-envy` heißt im Schema nicht `middle-man` (§8)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\PatternDetect\PatternCatalog.cs`, `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs`
- **Symbol:** `PatternDefinition` Id `feature-envy` → `AvoidExcessiveMiddleMen`; `PatternDetectDescription`
- **Ansatz:** Alias `middle-man` in `ResolvePatterns` akzeptieren (case-insensitive, wie `GOD-CLASS`). Id in der Antwort schon ehrlich; Schema-Enum um den Alias erweitern.

### Kein `scopeType` production/tests (Wunsch)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\Analysis\ViolationScopeFilter.cs`, `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs`
- **Symbol:** `FilterAndSortViolations` / `BuildFileToProjectMap`; analog `SearchPatternScanner` `scopeType`
- **Ansatz:** denselben `TestDetector.IsTestProject`/`IsTestFile`-Split wie Magic-Values/`search_pattern`. Default `all` in der Beschreibung. Rein projekt-/pfadbasiert, kein LLM.

### Pattern-IDs / `targetType` ohne JSON-Enum (§1)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs`, `src\AiNetLinter\Mcp\Tools\PatternDetect\PatternDetectTool.cs`
- **Symbol:** `AddPatternDetect` (`string[]? patterns`, `string targetType`), `ResolvePatterns` (Live-Enum über Katalog)
- **Ansatz:** Live-Validierung bleibt Quelle. SDK-Schema: `enum` aus `PatternCatalog.Patterns.Select(p => p.Id)` plus `project`/`assembly`, soweit das C#-MCP-SDK Attribute hergibt.
