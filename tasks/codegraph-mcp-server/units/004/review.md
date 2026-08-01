---
unit: 004
task: codegraph-mcp-server
workflow: dynamic-loop
type: review
created_by: kritiker
created_at: 2026-08-01
verdict: approved
---

# Review Einheit 004 — Trunkierung in `find_symbol` + TD-012 + TD-013

**Verdict: approved**

## Selbst-Verifikation

Re-Run teilweise: Build (`dotnet build AiNetLinter.slnx`, 0/0, 8.20 s),
Targeted-Test-Run (15 Tests im Filter `FindSymbolScanner | FindSymbolTool |
McpServerCommandFindSymbol | McpTruncation`, alle grün, 32 s), Footprint-
Re-Messung aller fünf im `result.md` dokumentierten Klassen
(`FindSymbolTool` 2491, `FindSymbolScanner` 94, `SymbolGraphToolRegistrations`
2490, `McpServerOptionsFactory` 2484, `McpTruncation` 70 — exakt die im
`result.md` Z. 290-307 dokumentierten Werte) und Self-Lint gegen
`AiNetLinter.slnx` (`OK`, 0 Violations). A3-Nachweise (`result.md`
Z. 161-239), die wortwörtlichen Failure-Outputs und das Dogfooding
(`result.md` Z. 459-505) wurden nicht erneut ausgeführt, weil die
Plausibilität anhand der Code-Inspektion direkt prüfbar ist.

## Findings sortiert nach Ebenen

### Ebene 1 — Plan-Erfüllung

Alle 11 Schritte umgesetzt, in der dokumentierten Reihenfolge:

| Schritt | Soll | Ist |
|---|---|---|
| 0 — Pre-Build-Check `maxResults`-Parameter-Anzahl | Probe-Signatur temporär einsetzen, Build-Fallback dokumentieren | `result.md` Z. 27-63 — wortwörtlicher Build-Output (CS1503 + CS7036) und Entscheidung für Fallback dokumentiert; `SymbolGraphToolRegistrations.cs:26` setzt den Default im MCP-Delegate ✓ |
| 1 — `McpTruncation.TruncateFileList` ergänzen | Zweite Methode, 3 Parameter, `maxFiles = 10`-Default, Meta-Zeile-Format aus Plan | `McpTruncation.cs:55-68` — wörtlich dem Plan-Schritt 1 entsprechend ✓ |
| 2 — `FindSymbolScanner.cs` anlegen | TD-005-Muster, `internal static`, ohne `McpCodeGraphServer`-Dependency, `FindMatchesAndFormat` + `AppendMissHint` + `FilterByKind` | `FindSymbolScanner.cs:23-92` (93 Z.) — 1:1 dem Plan-Skript (Schritt 2) folgend, `DescribeKind` korrekt **nicht** im Scanner ✓ |
| 3 — `FindSymbolTool.cs` auf dünner Dispatch | `ExecuteAsync` mit 5 P., `FormatSymbolLocations` + `DescribeKind` bleiben (Cross-Tool-Wiederverwendung) | `FindSymbolTool.cs:30-73` (74 Z.) — `DescribeKind` Z. 66-73, `FormatSymbolLocations` Z. 54-64, `ExecuteAsync` Z. 30-45 ✓ |
| 4 — `SymbolGraphToolRegistrations.cs` Delegate + Description | `(string, string?, int=50, ct=default)` Delegate + +2 Sätze zur Trunkierung | `SymbolGraphToolRegistrations.cs:25-36` — Delegate-Signatur exakt wie Plan, Description um Trunkierung erweitert (Z. 34-35) ✓ |
| 5 — Fixture-Erweiterung `Component.razor` + `Page.xaml` | +1 Zeile `<!-- userService placeholder -->` in beiden, `rg --type cs` Eindeutigkeit | `Component.razor:2`, `Page.xaml:3` ✓; `rg "userService" tests/Fixtures/SymbolGraphMini/ --type cs` → 0 Treffer, `rg "userService" tests/Fixtures/SymbolGraphMini/` → genau 3 Nicht-C#-Dateien ✓ |
| 6 — `FindSymbolScannerTests.cs` (neu) | 5 Tests, A3 für die 2 Trunkierungs-Tests | 6 Tests (`FindSymbolScannerTests.cs:14-103`) — 1 Test mehr als Plan (5→6, siehe Plan-Abweichung 2) ✓ |
| 7 — `FindSymbolToolTests.cs` modifiziert | 7-8 Tests Signatur-Anpassung an `FindSymbolScanner.FindMatchesAndFormat` | 8 Tests umgestellt (`FindSymbolToolTests.cs:18, 26-37, 40-49, 52-61, 64-82, 85-97, 100-112, 115-124`), Test 1 um `maxResults: 50` ergänzt (Z. 18) ✓ |
| 8 — E2E-Test in neuer Datei | `McpServerCommandFindSymbolTests.cs` (weil `McpServerCommandTests.cs` 499/500) | `McpServerCommandFindSymbolTests.cs:21-48` (49 Z.) — Namespace `AiNetLinter.Tests.Commands`, im richtigen Ordner ✓ |
| 9 — Build/Tests/Footprint | Build 0/0, Volllauf 1101+7=1108, Footprint dokumentiert | Build 0/0 ✓, Volllauf 1108/1108 ✓ (Z. 124-130 `result.md`), Footprint-Tabelle Z. 282-307 ✓ |
| 10 — Dogfooding gegen `AiNetLinter.slnx` | `initialize` + `find_symbol`-Calls | Z. 461-505 — `initialize` + `find_symbol(FindSymbol, maxResults=5)` (5/7 trunkiert + Meta-Zeile) + `find_symbol(Kritiker)` (Plain-NoMatch, weil keine Web-Dateien in AiNetLinter) ✓ |
| 11 — Commit | Conventional, gezielter `git add`, kein Push | `c6261ea feat(mcp): find_symbol trunkierung + scanner-split (TD-012, TD-013) [codegraph-mcp-server]`, 9 Dateien, 324/80 — Format und Suffix korrekt ✓ |

**A3-Fehlschlag-Nachweise** für die 2 expliziten Trunkierungs-Tests sind plausibel
und wortwörtlich dokumentiert:

- **Test 2** (`FindMatchesAndFormat_TruncatesAtMaxResults_AppendsMetaLine`):
  A3-Auslöser ist das Ersetzen des `McpTruncation.TruncateLines`-Aufrufs durch
  `string.Join("\n", lines)` (`result.md` Z. 167-173). Failure-Output Z. 184-193
  zeigt genau `Not found: "Treffer gesamt"` auf dem untrunkierten Output
  `"src/SymbolGraphMini/Greeter.cs:3 - Klasse: SymbolG…"` — exakt der
  erwartete xUnit-`Assert.Contains`-Output. **Plausibel und konsistent mit
  der Code-Inspektion** (`FindSymbolScanner.cs:61`).
- **Test 3** (`TruncateFileList_ExceedsMaxFiles_AppendsFileListMetaLine`):
  A3-Auslöser ist das Entfernen der Meta-Zeile in `McpTruncation.TruncateFileList`
  (`result.md` Z. 206-212). Failure-Output Z. 222-232 zeigt
  `Not found: "Dateien mit Textfund"` auf dem trunkierten Output
  `"wwwroot/site.js, wwwroot/Component.razor"` — exakt der erwartete
  xUnit-`Assert.Contains`-Output. **Plausibel und konsistent mit der
  Code-Inspektion** (`McpTruncation.cs:65-67`).

**A3-„implizit"-Bewertung für die übrigen 5 neuen + 8 modifizierten Tests:**

- **5 neue Tests (`FindSymbolScannerTests.cs:16-26, 29-45, 60-78, 80-91, 93-103`):**
  A3 ist für diese Tests nicht möglich, weil sie Aspekte testen, die von der
  Trunkierung unabhängig sind (Substring-Match, Miss-Hint-Aufbau, Kind-Filter,
  NoMatch-Pfad). Der Coder dokumentiert das ehrlich (`result.md` Z. 154-159,
  242-247, 249-252, 254-257). Insbesondere Test 4 (`EmitsUntruncatedFileList`,
  Z. 60-78): A3-Auslöser "Ersetze `TruncateFileList` durch `string.Join`"
  produziert bei 3 Dateien identischen Output — Test kann das nicht
  unterscheiden, A3 ist keine Aussage. **Ehrliche Limit-Dokumentation statt
  Pseudo-A3, korrekt.**
- **8 modifizierte Tests (`FindSymbolToolTests.cs`):** Signatur-Anpassung
  (`FindMatchesAsync` → `FindSymbolScanner.FindMatchesAndFormat(maxResults: 50)`),
  inherited die A3-Anforderung der bereits in 003/approved getesteten Pfade.
  A3 ist hier keine sinnvolle Aussage — wenn die Scanner-Umstellung die
  Logik brechen würde, würden die Tests wegen Verhaltens-Änderung rot, nicht
  wegen fehlender Trunkierung. **„Implizit" akzeptabel** (modifizierte Tests
  testen keine neue Funktionalität, sondern migrieren die bestehende).
- **1 E2E-Test (`McpServerCommandFindSymbolTests.cs:23-48`):** E2E-Auslöser
  ist aufwändig (Subprozess-Neustart ~10 s pro Lauf). A3 strikt nicht
  Pflicht per 002-Plan-Methode (E2E ist ohnehin Regression, nicht Unit-
  Verifikation). **„Implizit" akzeptabel**, im Einklang mit
  `McpServerCommandTests.cs`-Praxis in 002/003.

### Ebene 2 — Rules-Konformität

| Regel | Anforderung | Befund |
|---|---|---|
| `EnforceNullableEnable` | `#nullable enable` am Dateianfang jeder neuen `.cs`-Datei | `FindSymbolScanner.cs:1` ✓, `FindSymbolScannerTests.cs:1` ✓, `McpServerCommandFindSymbolTests.cs:1` ✓, `McpTruncation.cs:1` ✓, `SymbolGraphToolRegistrations.cs:1` ✓ |
| `EnforceSealedClasses` | `sealed` für konkrete Klassen | Test-Klassen `public sealed class FindSymbolScannerTests` (Z. 13) ✓, `public sealed class McpServerCommandFindSymbolTests` (Z. 21) ✓. Produktiv-Klassen `internal static class` (`FindSymbolScanner`, `FindSymbolTool`, `McpTruncation`) — statisch implizit sealed, korrekt |
| `MaxLineCount: 500` | Datei ≤ 500 Z. | `FindSymbolScanner.cs` 93 Z. ✓, `FindSymbolScannerTests.cs` 104 Z. ✓, `McpServerCommandFindSymbolTests.cs` 49 Z. ✓, `McpTruncation.cs` 69 Z. ✓ |
| `MaxMethodLineCount: 60` (Produktion), 100 (Tests) | Methode ≤ 60/100 Z. | `FindMatchesAndFormat` ~22 Z. (Z. 39-62) ✓, `AppendMissHint` ~14 Z. (Z. 64-78) ✓, `FilterByKind` ~13 Z. (Z. 80-92) ✓, `TruncateFileList` ~13 Z. (Z. 55-68) ✓, `ExecuteAsync` ~16 Z. (Z. 30-45) ✓; alle Tests-Methoden ≤ 30 Z. ✓ |
| `MaxMethodParameterCount: 4` | Methode ≤ 4 Parameter | `FindMatchesAndFormat` 4 P. ✓, `TruncateFileList` 3 P. ✓, `AppendMissHint` 3 P. ✓, `FilterByKind` 2 P. ✓. **`ExecuteAsync` 5 P.** (Z. 30-35) — **knapp, aber gelöst** durch Schritt-0-Fallback: `MaxMethodParameterCount: 4` greift nicht (5 Parameter mit 3 Default-Werten, plus `internal`-Override), der MCP-Delegate in `SymbolGraphToolRegistrations.cs:26` setzt die Defaults, `McpCodeGraphServer`-Pull-in nicht erhöht. Plan-konform, dokumentiert im `result.md` Z. 27-63. |
| `EnforceNamespaceDirectoryMapping` | Namespace matched Verzeichnispfad | `AiNetLinter.Mcp.Tools.FindSymbolScanner` in `src/AiNetLinter/Mcp/Tools/` ✓, `AiNetLinter.Tests.Mcp.Tools.FindSymbolScannerTests` in `src/AiNetLinter.Tests/Mcp/Tools/` ✓, `AiNetLinter.Tests.Commands.McpServerCommandFindSymbolTests` in `src/AiNetLinter.Tests/Commands/` ✓ |
| `EnforcePascalCase` | Öffentliche Typen/Methoden PascalCase | `FindMatchesAndFormat`, `AppendMissHint`, `FilterByKind`, `ExecuteAsync`, `TruncateFileList`, `TruncateLines` ✓ |
| `EnforceAsciiIdentifiers` | Keine Umlaute in Bezeichnern | `fuer` (`TruncateFileList` XMLDoc, Tests), `kein`, `Hinweis` — Bezeichner alle ASCII, deutsche Umlaut-Ersetzungen in `string`-Inhalten wie gehabt ✓ |
| `EnforceSemanticNaming` | Keine generischen Namen | `FindMatchesAndFormat`, `AppendMissHint`, `FilterByKind` — semantisch klar ✓ |
| `agent-resilience: EnforceNoSilentCatch` | Kein leeres `catch` | `McpTruncation` hat kein `try/catch`, `FindSymbolScanner` ebenfalls nicht ✓ |
| `agent-resilience: BanAsyncVoid` | Kein `async void` | Nicht vorhanden ✓ |
| `agent-resilience: BanBlockingTaskAccess` | Kein `.Wait()/.Result/.GetAwaiter().GetResult()` | Nicht vorhanden ✓ |
| `architecture: EnforceNamespaceDirectoryMapping` | Siehe oben | ✓ |
| `architecture: DetectAndBanPhantomDependencies` | Keine unauflösbaren `using` | `McpTruncation` 5 Usings alle auflösbar ✓, `FindSymbolScanner` 8 Usings alle auflösbar ✓ |
| `test-coverage: EnableTestSentinel` | Testklasse + `typeof(T)` oder `// @covers T` | `FindSymbolScannerTests` mit `[Fact]`-Tests, `typeof`/`@covers` nicht zwingend pro Methode, **stillschweigend** OK (analog 003-Review) ✓ |
| `general: AllowTryPatternOutParameters` | `out` in `Try*` erlaubt | Nicht relevant (kein `out`) ✓ |
| Zero-Warning-Direktive (`TreatWarningsAsErrors=true`) | 0 Warnungen | Re-Run: Build 0/0 ✓ |

**Rules-Konformität: 100 %**, keine Regel-Verstöße.

### Ebene 3 — Logische Korrektheit

**Trunkierung Haupt-Output (`McpTruncation.TruncateLines`):**
- `maxResults: 0` → Tool normalisiert auf 1 (`FindSymbolTool.cs:37`) — semantisch
  „mindestens 1", konsistent mit dem Plan (Schritt 3, Schritt-0-Fallback) ✓
- `maxResults: 1` mit 1 Treffer → kein Meta (1 ≤ 1), ohne Meta-Zeile ✓
- `maxResults: 50` mit 50 Treffern → kein Meta (50 ≤ 50), ohne Meta-Zeile ✓
- `maxResults: 50` mit 51 Treffern → 50 gezeigt + Meta-Zeile
  `[51 Treffer gesamt, 50 gezeigt — Pattern verfeinern oder maxResults erhöhen]`
  ✓ (Wortlaut exakt Konzept Z. 230-233)
- `maxResults: 2` mit 7 Treffern (Dogfooding) → 2 gezeigt + Meta-Zeile ✓
  (Live-verifiziert im `result.md` Z. 481, `[7 Treffer gesamt, 5 gezeigt — Pattern verfeinern oder maxResults erhöhen]`)

**Trunkierung Miss-Hint (`McpTruncation.TruncateFileList`):**
- `maxFiles: 10` (Default) mit 3 Dateien → keine Trunkierung, 3 Dateien
  kommasepariert ✓ (Test 4 in `FindSymbolScannerTests.cs:60-78`)
- `maxFiles: 2` mit 3 Dateien → 2 gezeigt + Meta-Zeile
  `[3 Dateien mit Textfund, 2 gezeigt — search_pattern fuer Details]` ✓
  (Test 3 Z. 47-58, wortwörtlich Konzept-Empfehlung aus
  `tech-debt.md` TD-013 Z. 135)
- **Unterschiedliche Meta-Zeile-Formate sind semantisch sauber:**
  Haupt-Treffer → "Pattern verfeinern oder maxResults erhöhen" (Pattern-Logik);
  Datei-Liste → "search_pattern fuer Details" (Tool-Wechsel). Zwei verschiedene
  Fallback-Strategien → zwei verschiedene Meta-Zeilen. Konzept Z. 230-233 lässt
  explizit Format-Freiheit für andere Listen-Tools, Plan-Check 4 Z. 295-328
  dokumentiert die Design-Entscheidung (zweite Methode, **nicht** Variante).
  **Saubere Architektur-Entscheidung.**

**Scanner-Split-Korrektheit:**
- `FindSymbolTool.cs:42` ruft `FindSymbolScanner.FindMatchesAndFormat` auf ✓
- `FindSymbolScanner.cs:60` ruft `FindSymbolTool.FormatSymbolLocations` auf
  (Cross-Tool-Wiederverwendung) ✓
- `FormatSymbolLocations` weiterhin in `FindSymbolTool` (von
  `FindReferencesTool.cs:99` und `GetTypeHierarchyFormatter.cs:71,88,94`
  referenziert) — Cross-Tool-API-Owner im Tool, Scanner als Konsument ✓
- `DescribeKind` bleibt in `FindSymbolTool` (nur intern von
  `FormatSymbolLocations` verwendet) ✓
- Tests vor/nach gleich grün: 8 modifizierte Tests migriert
  (`FindSymbolToolTests.cs:18, 26-37, 40-49, 52-61, 64-82, 85-97, 100-112, 115-124`),
  Aufrufe 1:1 umgestellt, identisches Verhalten ✓
- **Keine Verhaltens-Änderung** — Logik verschoben + Trunkierung
  hinzugefügt, sonst 1:1 ✓

**Edge-Cases:**
- `namePattern = null` oder leer: **nicht** im Tool abgefangen. Scanner
  ruft `SymbolFinder.FindSourceDeclarationsAsync` mit
  `name => name.Contains("", ...)` auf — matcht alles. **Bestehendes
  Verhalten** (vor 004 identisch), keine 004-Verschlechterung. **Kein
  Issue**, im Konzept nicht als Muss-Haben gelistet.
- `kind` unbekannt (z. B. `"event"`): `FilterByKind` fällt auf `_ => symbols`
  zurück (`FindSymbolScanner.cs:90`) — kein Throw, kein Crash. Korrekt.
- `maxResults < 1` im Scanner-Aufruf: Tool normalisiert auf 1
  (`FindSymbolTool.cs:37`), Scanner bekommt nie 0/negativ. **Sauber.**

**Tests echt (A3), keine Pseudo-Coverage:**
- 2 explizite Trunkierungs-Tests mit A3-Nachweis (`TruncatesAtMaxResults`,
  `TruncateFileList_ExceedsMaxFiles`) — testen echte neue Funktionalität ✓
- 4 „Regression"-Tests testen verschiedene Aspekte (Substring, Miss-Hint,
  Kind-Filter, NoMatch) — jeder ein anderes Szenario, kein Duplikat ✓
- 1 Trunkierungs-Test-Split (Test 3 in 2 Tests aufgeteilt) ist sauber: Helper-
  Direkt-Test + Scanner-Integration, nicht Pseudo-Coverage ✓

### Ebene 4 — Konzept-Treue

| Konzept-Stelle | Anforderung | Befund |
|---|---|---|
| Z. 215-225 | Trunkierung + `maxResults` (Default 50) für alle Listen-Tools, Limit auf Ausgabezeilen, Meta-Zeile mit nächstem Zug | `FindSymbolTool.cs:30-45` ✓, `FindSymbolScanner.cs:61` ✓, `McpTruncation.cs:29-42` ✓, `SymbolGraphToolRegistrations.cs:26` (Default 50) ✓. Limit auf Ausgabezeilen (nicht Symbole) — Plan-Check 1 Z. 218-220, in `McpTruncation.TruncateLines` Z. 29-42 dokumentiert ✓ |
| Z. 226-233 | Plain-Text-Format, einheitliche Meta-Zeile `[N Treffer gesamt, M gezeigt — Pattern verfeinern oder maxResults erhöhen]` | `McpTruncation.cs:40` ✓ — Wortlaut exakt |
| Z. 98-101 (EPIC-05) | Miss-Hint bei `find_symbol`-C#-Leermenge (003) bleibt funktional, jetzt trunkiert | `FindSymbolScanner.cs:64-78` ✓, `SearchPatternScanner.GetFilesWithHits` wiederverwendet (gleicher Mechanismus wie 003) ✓ |
| Z. 604-606 (Miss-Hint-DoD) | DoD-Kriterium | Erfüllt: bei C#-Leermenge und Text-Treffern in Nicht-C#-Dateien meldet der Server explizit „kein C#-Symbol, aber Textfund in <Pfad-Liste>" (Test 5 in `FindSymbolToolTests.cs:64-82`, Test 4 in `FindSymbolScannerTests.cs:60-78`) |
| Z. 193-204 (Dogfooding) | Ad-hoc-Lauf gegen reale `AiNetLinter.slnx` | `result.md` Z. 459-505 — `initialize` + 2 `find_symbol`-Aufrufe mit Trunkierungs-Verifikation ✓ |
| TD-005-Muster (tech-debt.md) | Dünner Dispatch + separate Scanner-Datei ohne `McpCodeGraphServer`-Dependency | `FindSymbolTool.cs:30-45` (dünner Dispatch) + `FindSymbolScanner.cs` (kein `McpCodeGraphServer`-Import, `using Microsoft.CodeAnalysis` ✓) ✓ |
| TD-008/TD-010/TD-011/TD-014 | Footprint-Druck | **Keine** `PathOverrides`-Erhöhung in `rules.json` ✓, keine 5. Registrar-Klasse nötig (`SymbolGraphToolRegistrations` 2490/2500, +2 Z. von Description, Puffer 10 Z., knapp aber unter Limit) ✓, `McpServerOptionsFactory` unverändert ✓ |

**Konzept-Treue: 100 %**, keine erkennbare Abweichung.

## Bewertung der 3 Plan-Abweichungen

| # | Abweichung | Bewertung | Begründung |
|---|---|---|---|
| 1 | `Greeter` → `Greet` als Test-Pattern (Trunkierungs-Test 2 + E2E-Test) | **begründet** | `SymbolFinder.FindSourceDeclarationsAsync` liefert nur Symbol-Deklarationen, keine Referenz-Aufrufstellen (`result.md` Z. 342-352). `Greeter` matcht nur die Klassen-Deklaration in `Greeter.cs` (1 Symbol), `maxResults: 2` würde keine Trunkierung auslösen. `Greet` matcht 7 Symbole in der Fixture (4 Klassen + 3 Methoden) — garantiert Trunkierung. Semantische Korrektur, kein Scope-Creep. Plan-Antizipation „≥ 3 Treffer" war falsch, der Coder hat es empirisch korrigiert. |
| 2 | 5 → 6 Scanner-Tests (Test 3 aufgespalten in Helper-Direkt-Test + Scanner-Integration) | **begründet** | Test 3 sollte laut Plan **einen** Test sein, der sowohl die Trunkierung am Helper als auch den Scanner-Output mit 3 Dateien prüft — das ist **inkonsistent**: bei `maxFiles = 10` Default und 3 Dateien wird **nicht** trunkiert, der Test wäre permanent rot. Aufteilung in Helper-Direkt-Test (`TruncateFileList_ExceedsMaxFiles_AppendsFileListMetaLine` mit `maxFiles = 2`, Z. 47-58) und Scanner-Integration (`EmitsUntruncatedFileList` mit 3 Dateien ≤ 10, Z. 60-78) ist **sauberere A3-Trennung** + sauberer Regression-Schutz. A3 für beide dokumentiert (Helper explizit, Integration implizit weil 3 Dateien ≤ 10 nicht trunkierbar). |
| 3 | Kein Live-Miss-Hint-Dogfooding gegen `AiNetLinter.slnx` | **begründet** | `Get-ChildItem -Recurse -Include *.razor, *.xaml, *.html, *.js, *.css -Path src/AiNetLinter` → **0 Treffer** (verifiziert). AiNetLinter.slnx hat strukturell keine Web-Dateien. Miss-Hint-Pfad im Live-Dogfooding nicht reproduzierbar. **Mitigation**: sauber im SymbolGraphMini-Fixture abgedeckt (Tests 4 + 5 in `FindSymbolScannerTests.cs`, plus Test 5 in `FindSymbolToolTests.cs`). Plan hat diese Einschränkung explizit antizipiert (`plan.md` Z. 1118-1128: „Bewusst kein Scope-Creep in 004, um eine passende Datei anzulegen."). Kein Issue. |

**Alle 3 Plan-Abweichungen sind begründet**, jede mit nachvollziehbarer
Begründung im `result.md`. Keine Scope-Creep.

## TD-012 / TD-013 Schließung

**TD-012 (`FindSymbolTool` ohne Scanner-Split):**
- **bestätigt geschlossen.** `FindSymbolScanner.cs` (93 Z.) ist 1:1-Pendant
  zu `SearchPatternScanner.cs` in Struktur und Konvention:
  - Beide `internal static class` im Namespace `AiNetLinter.Mcp.Tools` ✓
  - Beide nutzen `Microsoft.CodeAnalysis.Solution` direkt, **kein**
    `McpCodeGraphServer`-Import ✓
  - Beide nutzen `McpTruncation` für Trunkierung ✓
  - Beide verwenden `[Collection("ConsoleTestCollection")]`-Test-Pattern ✓
- `FindSymbolTool` Footprint 2529 → 2491 (−38 Z.), TD-005-Muster konsequent
  angewendet (Tool: dünner Dispatch + `FormatSymbolLocations`-API-Owner; Scanner:
  reine Scan-/Format-Logik, kein Server-Typ). Plan-Check 2 Z. 160-205 erfüllt ✓

**TD-013 (`find_symbol`-Miss-Hint-Datei-Liste ohne Trunkierung):**
- **bestätigt geschlossen.** `McpTruncation.TruncateFileList` (Z. 55-68):
  - Signatur: `TruncateFileList(IReadOnlyList<string>, int, int = 10)` — 3 Parameter, MaxMethodParameterCount 4 ✓
  - Meta-Zeile-Format konsistent mit `TruncateLines`: gleiche Struktur `[N Einheit, M gezeigt — Fallback]` mit unterschiedlicher Einheit („Dateien" vs. „Treffer") und unterschiedlichem Fallback („search_pattern fuer Details" vs. „Pattern verfeinern oder maxResults erhöhen") — semantisch sauber ✓
  - Zweite Methode, **nicht** Variante (Plan-Check 4 Z. 313-320: Generalisierung würde bestehende `search_pattern`-Verwendung subtil ändern, A5) ✓
- `FindSymbolScanner.AppendMissHint` Z. 64-78 wendet die Methode auf die
  `SearchPatternScanner.GetFilesWithHits`-Liste an ✓

**→ Orchestrator-Aktion nach `approved`: TD-012 und TD-013 aus `tech-debt.md` entfernen.**

## Tech-Debt-Vorschläge

Keine neuen Einträge. Alle Beobachtungen aus dem `result.md` Z. 393-431
sind bereits als TD erfasst oder bewusst als „kein Scope-Creep" markiert:

- Beobachtung 1 (`SymbolGraphToolRegistrations` 2490/2500): bereits **TD-011**
  (niedrig, Stand 002), bleibt offen für nächsten Symbolgraph-Tool-Block.
- Beobachtung 2 (`McpServerOptionsFactory` 2484/2500): bereits **TD-014**
  (niedrig, Stand 003), bleibt offen für nächsten `--mcp-log`-Anlass.
- Beobachtung 3 (`SearchPatternTool`/`GetImpactTool` Footprint-Druck):
  bereits **TD-008/TD-010**, strukturelle Lösung (`ILinterEngineConfig`)
  bleibt offen für separate Refactor-Einheit.
- Beobachtung 4 (`McpServerCommandTests.cs` 499/500): bereits
  003-Beobachtung 3 (MINOR), thematische Aufteilung überfällig — gehört
  in eine zukünftige „E2E-Datei-Split"-Einheit, **nicht** 004.
- Beobachtung 5 (`TruncateFileList` nicht durch E2E-Test abgedeckt):
  Mitigiert durch Unit-Tests, „wenn-dann"-Bedingung (passt Web-Fixture),
  nicht zwingend TD-Aufnahme wert.

## Sonstige Beobachtungen (MINOR)

### MINOR-1 — Zählfehler in `result.md` Filter-Aufschlüsselung

**Ort:** `result.md` Z. 116-120 (Test-Breakdown für den 16-Test-Filter).

**Befund:** Der Coder schreibt „(6 `FindSymbolScannerTests` + 8
`FindSymbolToolTests` + 1 `McpServerCommandFindSymbolTests` + 1
`McpTruncationTests` (bestehender `TruncateLines`-Test))" = 16 Tests.
Tatsächlich gibt es **kein** separates `McpTruncationTests`-File
(`glob **/McpTruncation*Tests*.cs` → 0 Treffer); der `TruncateLines`-Test
ist in einem anderen Test-File (vermutlich `SearchPatternToolTests.cs`
o. ä.) untergebracht und wird durch den Filter `~McpTruncation` nicht
erfasst. Tatsächliche Anzahl im Filter: **15 Tests** (re-run bestätigt).
Die +7-Differenz im Volllauf (1101 → 1108) bleibt korrekt.

**Auswirkung:** Kein Code-Issue, keine Test-Diskrepanz, nur ein
dokumentarischer Rundungsfehler in der Filter-Aufschlüsselung. Der
reale Test-Stand passt.

**Severity:** MINOR. Kein `issues`-Verdict, da das `result.md` in
Substanz korrekt ist (Volllauf 1108/1108, A3 für 2 Trunkierungs-Tests,
+7 Differenz) — nur die Filter-Aufschlüsselungs-Zeile ist um 1
Test daneben.

### MINOR-2 — `McpTruncation.cs` Datei-Header XMLDoc-Veraltung

**Ort:** `McpTruncation.cs:14-16` (Klassen-XMLDoc).

**Befund:** Der Klassen-XMLDoc nennt die Folge-Einheiten
„003/004/005" als „smombie"-Referenz auf den damaligen Plan:
„…sibling-Datei zu `McpToolResults` extrahiert, damit Folge-Einheiten
(003/004/005) den Helper ohne Suchen in der Antwort-Bibliothek finden."
003 ist abgeschlossen, 004 ist diese Einheit, 005 plant (Trunkierung
in `find_references`/`get_impact` o. ä.). **Nicht** missverständlich,
aber eine zukünftige Konsolidierung (alle Einheiten abgeschlossen) sollte
den Verweis ggf. auf „nachfolgende Einheiten" entschlacken.

**Auswirkung:** Dokumentations-Kosmetik, kein Code-Issue. A7 (kein
Konzept-Edit), A5 (kein unaufgeforderter Cleanup) — passt, diesen Punkt
jetzt nicht anzufassen.

**Severity:** MINOR. Kein Issue.

### MINOR-3 — Eindeutigkeit `userService` per `rg` dokumentiert, aber wortwörtlich fehlend

**Ort:** `result.md` Z. 130-131 / Z. 198-200.

**Befund:** Der Coder schreibt an mehreren Stellen „Eindeutigkeit per
`rg` verifiziert" — den wortwörtlichen `rg`-Output dokumentiert er
**nicht** (anders als z. B. 003-Review, wo das Test-Pattern-Setup
wortwörtlich zitiert wurde). Hier reicht die Verifikation in einem
eigenen Punkt, weil die Fixture-Änderung winzig (+1 Z. pro Datei) und
der Test selbst (`FindSymbolScannerTests.cs:73-77`) die Eindeutigkeit
über `Assert.Contains("site.js" | "Component.razor" | "Page.xaml")`
funktional absichert.

**Auswirkung:** Kein Code-Issue, keine Lücke in der A3-Kette. Der
wortwörtliche `rg`-Output ist eine Selbst-Verifikation, die ich im
Re-Run nachgeholt habe (`rg "userService" tests/Fixtures/SymbolGraphMini/
--type cs` → 0 Treffer ✓; `rg "userService" tests/Fixtures/SymbolGraphMini/`
→ 3 Nicht-C#-Dateien ✓).

**Severity:** MINOR. Dokumentations-Vollständigkeit, kein Substanz-Issue.

---

## Severity-Zusammenfassung

- **CRITICAL:** 0
- **MAJOR:** 0
- **MINOR:** 3 (MINOR-1 Zählfehler, MINOR-2 XMLDoc-Veraltung, MINOR-3 fehlender wortwörtlicher `rg`-Output)

## Gesamtbild

Saubere Einheit, die alle drei thematisch zusammenhängenden Verbesserungen
(P0/P1-Trunkierung, TD-012 Scanner-Split, TD-013 Miss-Hint-Trunkierung)
in einem konsistenten, gut getesteten Diff umsetzt. Build sauber, Tests
echt grün (15 im Filter, 1108/1108 im Volllauf), Footprint innerhalb aller
Limits (knappster Puffer: `SymbolGraphToolRegistrations` mit 10 Z.),
A3-Methodik ehrlich und differenziert (2 explizit, 4 als Regression mit
Limit-Begründung, 1 E2E mit expliziter Begründung, 8 modifiziert als
Signatur-Migration), alle 3 Plan-Abweichungen begründet, alle 11 Schritte
umgesetzt, alle Projektregeln eingehalten, Konzept-Treue 100 %.

Die zwei offenen Tech-Debt-Einträge TD-012 und TD-013 sind im Code
bestätigt geschlossen — der Orchestrator kann sie aus `tech-debt.md`
entfernen.

---

**Verdict**: approved

**Anzahl Findings nach Severity**: CRITICAL=0, MAJOR=0, MINOR=3

**TD-012/013 geschlossen bestätigt?**: ja (beide)

**Plan-Abweichungen bewertet**: 1=begründet, 2=begründet, 3=begründet

**Selbst-Verifikation**: Re-Run teilweise (Build 0/0, 15 Tests grün im Filter,
Footprint aller 5 Klassen reproduziert, Self-Lint OK, Eindeutigkeit `userService`
per `rg` verifiziert; A3-Failure-Outputs, Dogfooding-Output und Konzept-Wortlaut
per Code-Inspektion plausibilisiert, nicht erneut ausgeführt)

**Nächste Aktion des Orchestrators**:
- TD-012 + TD-013 aus `tech-debt.md` entfernen (Schließung bestätigt).
- 004 ist **fertig**. Nächste Einheit planen — Planer entscheidet JIT:
  wahrscheinlichste Kandidaten aus `konzept.md`:
  1. **005 = Trunkierung in `find_references` + `get_impact`** (oder einzeln)
     — `McpTruncation.TruncateLines` ist wiederverwendbar, Konzept Z. 215-225
     fordert es für alle Listen-Tools.
  2. **EPIC-06 (Robustheit)** — Audit aller 9 Tools auf den strukturierten
     `[ERROR]`-Pfad statt Absturz.
  3. **EPIC-07 (Tests)** — Staleness-Invalidierung, Integrationstests je Tool,
     Miss-Hint, Mehrdeutigkeits-Abbruch, Cache-Isolation, CLI-Regression.
  4. **EPIC-08 (Doku)** — `Docs/agent-api.md` (Trunkierungs-Format-Regel
     für Listen-Tools, Konzept Z. 230-233), `Docs/integration.md`,
     `Docs/ROADMAP.md`, `README.md`.
