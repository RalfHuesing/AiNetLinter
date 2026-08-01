---
unit: 002
task: codegraph-mcp-server
workflow: dynamic-loop
type: review
created_by: kritiker
created_at: 2026-08-01
code_commit: 28e6e58
result_commit: 91278ea
verdict: issues
---

# Review Einheit 002 — `search_pattern` Tool (letztes EPIC-04)

**Verdict: issues** — 1 MAJOR (funktionaler Bug in der Fehler-Hint-Ausgabe von
`SearchPatternTool` bei leerem `pattern`), Build/Test grün, 4 neue Dateien + 2
Modifikationen, A3-Nachweise plausibel, Konzept-Treue sauber.

## Selbst-Verifikation

**Plausibilitätsbewertung** auf Basis `result.md` (Commit `91278ea`),
`plan.md` (Commit `286233d`) und direkter Code-Inspektion. **Gezielte
Re-Runs** (A3, "selbst ausführen nur, um einen konkreten Verdacht zu
belegen"):

- `dotnet test --filter "FullyQualifiedName~SearchPattern"` → 9/9 grün,
  27 s — bestätigt die dokumentierte Testzahl (8 Unit + 1 E2E, alle
  9 mit A3-Nachweis).
- Footprint-Stichprobe: `ainetlinter --footprint <Klasse> --path .` für
  `SearchPatternTool`/`SearchPatternScanner`/`McpTruncation`/
  `AnalysisToolRegistrations`/`SymbolGraphToolRegistrations` — alle 5
  Werte exakt wie im `result.md` Z. 166-184 dokumentiert
  (2482/179/44/2476/2487).
- `git --no-pager show --stat 28e6e58` → 6 Dateien, 517 insertions,
  10 deletions — stimmt mit `result.md` "Geänderte Dateien" überein.

**Nicht selbst ausgeführt:** `dotnet build` (0 Warnungen als
plausibel akzeptiert — `TreatWarningsAsErrors=true` in
`AiNetLinterRichtlinien.mdc` Z. 81 würde den Build sonst abbrechen;
die Test-Stichprobe lief gegen den gebauten Binary) und der volle
`dotnet test AiNetLinter.slnx --no-build` (1097/1097 vom Coder
dokumentiert; Plausibilität durch die gefilterte 9/9-Stichprobe
hoch).

## Konzept-Konformität (Vor-der-Kritik-Check)

- Konzept Z. 95-97 (search_pattern als letztes EPIC-04-Tool): ✓
  Code-Commit `28e6e58` deckt das 9. Tool ab.
- Konzept Z. 215-225 (Trunkierung + `maxResults` Default 50): ✓
  `AnalysisToolRegistrations.cs:46` setzt `maxResults = 50` als
  Default-Parameter; `McpTruncation.cs:40` implementiert die
  Meta-Zeile.
- Konzept Z. 226-233 (Plain-Text-Format, einheitliche Meta-Zeile):
  ✓ `McpTruncation.cs:40` exakt mit dem Wortlaut aus dem Konzept.
- Konzept Z. 540-553 (Tool-Set-Tabelle): `search_pattern` ist
  weiterhin mit Status "offen" markiert. Die Status-Verschiebung
  auf "fertig" ist A7-Sache des Nutzers — wird hier nicht
  angefasst. **Aber:** die Voraussetzungen für die Verschiebung
  sind mit dieser Einheit erfüllt (Tool umgesetzt, Tests grün,
  A3 nachgewiesen, Dogfooding dokumentiert).
- Konzept Z. 604-606 (Miss-Hint-DoD, mittelbar durch 002-API): ✓
  `SearchPatternScanner.GetFilesWithHits(Solution, string, bool)` in
  `SearchPatternScanner.cs:88-112` ist die importierbare
  Schnittstelle für 003, liefert nur Pfade (kein Text), deterministisch
  sortiert via `SortedSet<string>(StringComparer.Ordinal)`.
- Konzept Z. 651-652 (Trunkierungs-DoD): ✓ die Meta-Zeile
  `[N Treffer gesamt, M gezeigt — Pattern verfeinern oder maxResults
  erhöhen]` ist fix, lebt in `McpTruncation.cs:40`, kann von 003+
  ohne API-Änderung wiederverwendet werden.

## Findings sortiert nach Ebene

### Ebene 1 — Plan-Erfüllung

Alle 7 Plan-Schritte aus `plan.md` Z. 218-457 umgesetzt:

| Schritt | Datei | Beleg | Status |
|---|---|---|---|
| 1. `McpTruncation.cs` | `src/AiNetLinter/Mcp/McpTruncation.cs:1-43` (43 Z.) | `#nullable enable`, `internal static class`, 3-Parameter-API, Plain-Text-Join, Meta-Zeile-Format | ✓ |
| 2. `SearchPatternScanner.cs` | `src/AiNetLinter/Mcp/Tools/SearchPatternScanner.cs:1-178` (178 Z.) | sequentieller Scan via `WebFileCatalog.GetProjectDirectories` + private `SafeEnumerateFiles`/`IsGeneratedPath`, Regex via `RegexOptions.IgnoreCase \| Compiled \| CultureInvariant` (Z. 30-31), `try/catch (ArgumentException)` wird im Tool abgefangen, Treffer-Format `{relativerPfad}:{zeile}: {inhalt}` (Z. 135), `GetFilesWithHits` für 003 exportiert (Z. 88-112) | ✓ (178 statt 120-140 Z. geplant — 2 öffentliche Methoden + 4 private Helper erklären den Mehr-Umfang; kein Verstoß gegen `MaxLineCount` ≤ 500) |
| 3. `SearchPatternTool.cs` | `src/AiNetLinter/Mcp/Tools/SearchPatternTool.cs:1-65` (65 Z.) | `Task.Run`-Wrapper (Z. 51-53), Argument-Validierung im Tool (Z. 38-43), `McpCodeGraphServer.GetCurrentSolution()` mit `null`-Check (Z. 45-46), `try/catch (ArgumentException)` → `McpToolResults.Error(LinterErrorCodes.InvalidArgument, …, hint: …)` (Z. 49-61) | ✓ |
| 4. `AnalysisToolRegistrations.cs` mod. | `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs:45-57` (Block), Z. 9-21 (XMLDoc) | `search_pattern`-Block nach `get_violations` ergänzt; XMLDoc von "Vorbereitet fuer" auf "aktuell `get_violations` und `search_pattern`" aktualisiert (Z. 10) | ✓ |
| 5. `McpServerCommandTests.cs` mod. | `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs:134-161` (Tool-Count-Test), Z. 245-270 (E2E) | `ServerRespondsWithEightTools` → `ServerRespondsWithNineTools` (Z. 134), `Assert.Equal(9, tools.Count)` (Z. 151), 9 Contains-Asserts inkl. `search_pattern` (Z. 160); neuer E2E `RunAsync_ValidFixture_SearchPatternReturnsExpectedHit` mit `pattern="Greeter"` (Z. 245) | ✓ |
| 6. `SearchPatternToolTests.cs` | `src/AiNetLinter.Tests/Mcp/Tools/SearchPatternToolTests.cs:1-175` (175 Z.) | alle 8 Unit-Tests aus dem Plan, `[Collection("ConsoleTestCollection")]`, `[Fact]` | ✓ |
| 7. Build + Test + Dogfooding | `result.md` Z. 59-70 + Z. 240-305 | Build grün, 0 Warnungen; Tests 1097/1097; Self-Lint 0 Violations; Dogfooding 3 Calls (CodeGraph, McpCodeGraphServer, InvalidRegex) gegen reale `AiNetLinter.slnx` | ✓ |

**Antworten auf die 6 offenen Fragen aus dem Plan** — alle in
`result.md` Z. 30-40 dokumentiert, jede Antwort folgt der
Plan-Empfehlung, jede Begründung ist nachvollziehbar:

| # | Frage | Plan-Empfehlung | Coder-Antwort | Begründung im Result | Bewertung |
|---|---|---|---|---|---|
| A | Argument-Validierung | im Tool | im Tool (Z. 38-43) | spart Scan-Start, hält Scanner rein | ✓ |
| B | `SafeEnumerateFiles`/`IsGeneratedPath` als `internal static`? | nein (private Kopie) | nein (Z. 161-177) | TD-006-Schließung wäre Scope-Creep | ✓ |
| C | `McpTruncation.cs` vs. Method in `McpToolResults.cs`? | separate Datei | separate Datei (Z. 1-43) | konzept.md wörtlich "neben"; thematisch sauber | ✓ |
| D | `description` ausführlicher? | nein (6-7 Z.) | nein (Z. 51-56) | LLM liest description im Listing | ✓ |
| E | `RegexOptions.Multiline`? | nein | nein (Z. 30-31) | `File.ReadAllLines` splittet zeilenweise; `^`/`$` als Zeilen-Anker ohne Multiline korrekt | ✓ |
| F | Forward-Slashes? | ja | ja (Z. 67, 105) | konsistent mit `GetViolationsScanner.cs:162` | ✓ |

### Ebene 2 — Rules-Konformität

| Regel | Datei:Zeile | Status |
|---|---|---|
| `EnforceNullableEnable` | `McpTruncation.cs:1`, `SearchPatternTool.cs:1`, `SearchPatternScanner.cs:1`, `AnalysisToolRegistrations.cs:1` | ✓ alle 4 Produktions-Dateien |
| `EnforceNullableEnable` | `SearchPatternToolTests.cs:1` (direkt `using System.IO;`, **kein** `#nullable enable`) | **MAJOR** — siehe Finding M-1 |
| `EnforceSealedClasses` | `SearchPatternTool` `internal static` (Klasse ohne Instanzen, kein `sealed` möglich) | ✓ |
| `EnforceSealedClasses` | `SearchPatternToolTests` `public sealed class` (Z. 14) | ✓ (Tests-Override `EnforceSealedClasses: false` würde auch un-sealed erlauben; sealed ist die strengere Wahl) |
| `AIContextFootprint` ≤ 2500 | `McpTruncation` 44 (Limit 2500), `SearchPatternTool` 2482 (Puffer 18), `SearchPatternScanner` 179 (Puffer 2321), `AnalysisToolRegistrations` 2476 (Puffer 24) | ✓ alle ≤ 2500 — TD-004-Vorhersage "4. Registrar-Klasse nötig" widerlegt |
| `MaxLineCount` ≤ 500 | `McpTruncation.cs` 43 Z., `SearchPatternTool.cs` 65 Z., `SearchPatternScanner.cs` 178 Z., `SearchPatternToolTests.cs` 175 Z. | ✓ |
| `MaxMethodLineCount` ≤ 60 (Prod) / ≤ 100 (Tests) | längste Methode in `SearchPatternScanner.cs` (alle ≤ 30 Z.); Tests unter 100 Z. | ✓ |
| `MaxMethodParameterCount` ≤ 4 (Prod, ohne `CancellationToken` via `MethodParameterCountIgnoreTypeNames`) | `McpTruncation.TruncateLines` 3, `SearchPatternTool.ExecuteAsync` 4, `SearchPatternScanner.SearchAndFormat` 4, `SearchPatternScanner.GetFilesWithHits` 3, `SearchPatternScanner.CollectFileHits` 6 (private → `MaxMethodParameterCountForNonPublic: 6` greift) | ✓ (am Limit für `CollectFileHits` mit 6 Parametern — siehe MINOR O-2) |
| `EnforceNamespaceDirectoryMapping` (`*.Mcp` → `src/AiNetLinter/Mcp/`, `*.Mcp.Tools` → `src/AiNetLinter/Mcp/Tools/`) | `McpTruncation.cs` → `AiNetLinter.Mcp` (Z. 6) ✓, `SearchPatternTool.cs` → `AiNetLinter.Mcp.Tools` (Z. 9) ✓, `SearchPatternScanner.cs` → `AiNetLinter.Mcp.Tools` (Z. 11) ✓, `AnalysisToolRegistrations.cs` → `AiNetLinter.Mcp` (Z. 7) ✓, `SearchPatternToolTests.cs` → `AiNetLinter.Tests.Mcp.Tools` (Z. 11) ✓ | ✓ |
| `AiNetLinterRichtlinien.mdc` §1 (Einfachheit vor Abstraktion) | `SearchPatternScanner.cs` nutzt `System.Text.RegularExpressions.Regex` direkt, `File.ReadAllLines` direkt — keine eigene Engine, keine Helper-Klasse über das Nötigste hinaus | ✓ |
| `AiNetLinterRichtlinien.mdc` §2 (kein DI) | `AnalysisToolRegistrations.cs:32-33, 46-47` Delegate-Closures auf `mcpState`, keine DI-Container | ✓ |
| `AiNetLinterRichtlinien.mdc` §5 (Result-Pattern, kein leerer Catch) | `SearchPatternTool.cs:55-61` `try/catch (ArgumentException)` → `McpToolResults.Error(LinterErrorCodes.InvalidArgument, …, hint: …)`; `SearchPatternScanner.cs:127-128, 148-149, 167-168` fangen `IOException`/`UnauthorizedAccessException` stumm ab — **kein leerer Catch im Sinne der Agent-API, aber sehr wohl stummes Ignorieren von Datei-Lese-Fehlern** | ✓ für Agent-API (ArgumentException); **MINOR** für Datei-Lese-Fehler-Behandlung (siehe O-3) |

### Ebene 3 — Logische Korrektheit

| Aspekt | Beleg | Bewertung |
|---|---|---|
| Trunkierung, Boundary `maxResults: 0` | `SearchPatternTool.cs:43` normalisiert auf 1; `McpTruncation.cs:34-37` `totalMatches <= maxResults` greift nur bei `totalMatches == 0` (Leermenge-Pfad), sonst Trunkierung | ✓ (sinnvolle Default-Normalisierung) |
| Trunkierung, Boundary `maxResults: 1` | `hitLines.Take(1).ToList()` + Meta `[N Treffer gesamt, 1 gezeigt — ...]` | ✓ |
| Trunkierung, kein Treffer | `SearchPatternScanner.cs:72-75` explizite Leermenge-Meldung `"0 Treffer fuer das angegebene Pattern."` — **vor** `McpTruncation.TruncateLines`, also nie durch Trunkierung überschrieben | ✓ |
| Meta-Zeile-Format | `McpTruncation.cs:40` exakt `[N Treffer gesamt, M gezeigt — Pattern verfeinern oder maxResults erhöhen]` (mit Em-Dash `—`, U+2014) | ✓ wortgleich zum Konzept |
| Regex-Modus, Sonderzeichen | `SearchPatternScanner.cs:154-159`: `regex is not null ? regex.IsMatch(line) : line.Contains(pattern, OrdinalIgnoreCase)` — `isRegex=false` nimmt `pattern` **literal** (kein Escape nötig), `isRegex=true` nutzt `Regex`-Engine | ✓ |
| Datei-Scan | `SearchPatternScanner.cs:61-70` iteriert `WebFileCatalog.GetProjectDirectories(solution).OrderBy(d => d, Ordinal)`, pro Verzeichnis `SafeEnumerateFiles(...).OrderBy(f => f, Ordinal)`, `IsGeneratedPath`-Filter, `File.ReadAllLines` + `Regex.Match`/`Contains` | ✓ (deterministisch — wichtig für A3 + E2E) |
| Sortierung pro Datei | `SearchPatternScanner.cs:130-138` `for (var i = 0; i < lines.Length; i++)` — Zeile 1 zuerst | ✓ |
| `Path.GetRelativePath` + Forward-Slashes | `SearchPatternScanner.cs:67, 105` `.Replace('\\', '/')` | ✓ (analog `GetViolationsScanner.cs:162`) |
| `Regex`-Fehlerbehandlung | `SearchPatternTool.cs:55-61` `catch (ArgumentException)` → `McpToolResults.Error(LinterErrorCodes.InvalidArgument, $"Ungueltige Regex: {ex.Message}", hint: "Pruefe pattern auf gueltige Regex-Syntax.")` | ✓ (Result-Pattern sauber) |
| `mcpState.GetCurrentSolution() is null`-Check | `SearchPatternTool.cs:45-46` vor `Task.Run`, damit nicht auf `null.Solution` operiert wird | ✓ |
| **M-1: Hint-Inkonsistenz bei `InvalidArgument` für leeres `pattern`** | `SearchPatternTool.cs:40` ruft `McpToolResults.InvalidArgument("pattern darf nicht leer sein.")` auf. Die Helper-Methode in `McpToolResults.cs:74-80` hat einen **hartcodierten** Hint "Entweder gitRef ODER symbolIdentifier angeben, nie beide." (zugeschnitten auf `get_impact`, dokumentiert in der XMLDoc Z. 71-73). Resultat: leerer `pattern` liefert dem Agenten einen **irreführenden** Hint, der mit `search_pattern` nichts zu tun hat. **Im selben Tool inkonsistent** mit Z. 57-60, wo `McpToolResults.Error(LinterErrorCodes.InvalidArgument, ...)` mit **korrektem** Hint verwendet wird. | **MAJOR** — siehe Finding M-1 |
| Test-Schwäche für Test 8 (`EmptyPattern`) | `SearchPatternToolTests.cs:162-174` assertiert nur `IsError=true` + `Assert.Contains("INVALID_ARGUMENT", text)`, **nicht** den Hint-Inhalt. A3-Nachweis (Z. 132-138 im `result.md`) entfernt die `IsNullOrEmpty`-Validierung und prüft, ob der Test rot wird — das fängt nur das **Fehlen** der Validierung, nicht die **Korrektheit** des Hints. Genau deshalb bleibt der Hint-Bug unentdeckt. | **MAJOR** (zusammen mit M-1) |

### Ebene 4 — Konzept-Treue

| Konzept-Stelle | Beleg | Bewertung |
|---|---|---|
| Konzept Z. 95-97 (search_pattern als letztes EPIC-04-Tool) | 9. Tool in `AnalysisToolRegistrations.Register` (Z. 45-57); Tool-Count-Test jetzt 9 (McpServerCommandTests.cs:151) | ✓ |
| Konzept Z. 215-225 (Trunkierung + maxResults Default 50 für alle Listen-Tools) | für `search_pattern` umgesetzt; `find_symbol`/`find_references`/`get_impact` bleiben bewusst separaten Einheiten 003/004/005 vorbehalten (vom Plan so geplant) | ✓ (002-Scope sauber) |
| Konzept Z. 226-233 (Plain-Text, einheitliche Meta-Zeile) | `McpTruncation.cs:40` fix; `McpToolResults.Text` weiterhin Plain-Text (kein JSON-Mix) | ✓ |
| Konzept Z. 604-606 (Miss-Hint-DoD) | `SearchPatternScanner.GetFilesWithHits` (Z. 88-112) liefert `IReadOnlyList<string>` — nur Pfade, deterministisch via `SortedSet<string>(StringComparer.Ordinal)`, geeignet für 003 | ✓ |
| Konzept Z. 651-652 (Trunkierungs-DoD) | Meta-Zeile-Format fix, Plain-Text | ✓ |
| Tool-Set-Tabelle Z. 540-553 (search_pattern Status) | Status "offen" — wird vom **Nutzer** verschoben (A7), nicht in 002. Voraussetzungen erfüllt. | ✓ (außerhalb 002-Scope) |
| Doku-Update-Befreiung (`Docs/agent-api.md` / `Docs/ROADMAP.md`) | `result.md` Z. 226-231 dokumentiert die Befreiung explizit mit Verweis auf 001-Konvention; Konzept Z. 106-107 markiert Doku als EPIC-08. `AiNetLinterRichtlinien.mdc` §4 ist ein generischer Default, der durch die explizite Konzept-Befreiung (P0/P1-Übernahme, Z. 206-213) außer Kraft gesetzt ist. | ✓ (Konzept-Befreiung analog zu 001) |

## Findings — Detail

### MAJOR

**M-1 — `McpToolResults.InvalidArgument`-Helper liefert irreführenden Hint
für `search_pattern`**

- **Ort:** `src/AiNetLinter/Mcp/Tools/SearchPatternTool.cs:40` (Aufruf) und
  `src/AiNetLinter/Mcp/McpToolResults.cs:74-80` (Definition).
- **Befund:** `McpToolResults.InvalidArgument` ist kein generischer
  Helper, sondern ein für `get_impact` zugeschnittener (XMLDoc
  `McpToolResults.cs:71-73`: "Kurzform für den Fall, dass ein Tool-Aufruf
  gegenseitig exklusive Parameter verletzt (z. B. `get_impact`s
  `gitRef` und `symbolIdentifier` beide gesetzt)"). Der Hint
  (Z. 79) ist hartcodiert auf "Entweder gitRef ODER symbolIdentifier
  angeben, nie beide.". Wenn `search_pattern` mit leerem `pattern`
  aufgerufen wird, liefert der Helper-Result diesem Tool einen
  **völlig unpassenden** Hint. Im selben Tool wird 14 Zeilen tiefer
  (`SearchPatternTool.cs:57-60`) korrekt `McpToolResults.Error(...
  hint: "Pruefe pattern auf gueltige Regex-Syntax.")` verwendet —
  die Inkonsistenz im selben Tool macht den Coder-Fehler besonders
  auffällig.
- **Konzept-Verstoß:** Konzept Z. 567-568 fordert "Fehlerfälle ...
  liefern eine strukturierte Fehlerantwort ... im bestehenden
  `[ERROR]`-Format". Der `[ERROR]`-Code ist vorhanden, der Hint ist
  aber irreführend — er verletzt die implizite Konzept-Erwartung
  "klarer Hint" für den Agenten.
- **Test-Schwäche:** `SearchPatternToolTests.cs:162-174` (Test 8
  `ExecuteAsync_EmptyPattern_ReturnsInvalidArgumentError`) assertiert
  nur `IsError=true` und `Assert.Contains("INVALID_ARGUMENT", text)`,
  **nicht** den Hint-Inhalt. A3-Nachweis (Z. 132-138 im `result.md`)
  entfernt die `IsNullOrEmpty`-Validierung und prüft das **Fehlen** der
  Validierung, nicht die **Korrektheit** des Hints. Genau deshalb
  bleibt der Hint-Bug unentdeckt.
- **Severity-Begründung:** echter funktionaler Fehler in der
  User-Experience (Agent bekommt irreführenden Hint), inkonsistente
  API-Nutzung im selben Tool (Beweis: Z. 40 vs. Z. 57-60), Konzept-
  Pflicht "klarer Hint" verletzt. **Kein** Build-/Test-Bruch (deshalb
  nicht CRITICAL), **aber** mehr als MINOR/akzeptabel, weil ein
  laufender Agent falsch geleitet wird.
- **Vorschlag (nicht selbst umsetzen — A2):** Fix-Runde 002/fix-01/
  mit Ein-Zeilen-Korrektur in `SearchPatternTool.cs:40`: Aufruf von
  `McpToolResults.InvalidArgument("pattern darf nicht leer sein.")`
  ersetzen durch
  `McpToolResults.Error(LinterErrorCodes.InvalidArgument,
  "pattern darf nicht leer sein.",
  hint: "Pattern angeben — leeres Pattern ist nicht erlaubt.")`
  (analog Z. 57-60). Optional gleich den Test 8 um eine
  Hint-Assertion erweitern
  (`Assert.Contains("Pattern", textContent.Text, Ordinal)` oder
  konkreter), damit der A3-Pfad auch die Hint-Qualität abdeckt.

### MINOR (Beobachtungen)

- **O-1 — `SearchPatternTool` 2482/2500 (Puffer 18 Z., knapp)** —
  `McpCodeGraphServer.Config`-Property zieht den `Configuration`-
  Namespace (~1110 Z.) transitiv in alle Tool-Klassen mit
  `McpCodeGraphServer`-Referenz. Derselbe Pull-in-Mechanismus wie bei
  `FindSymbolTool`/`FindReferencesTool` (TD-008, dort bereits durch
  `PathOverrides: 2700` pragmatisch aufgefangen). Strukturelle Lösung
  wäre `ILinterEngineConfig`-Interface (geschätzt 4-6h). **Tech-Debt-
  Vorschlag TD-010** (mittel) — siehe unten.
- **O-2 — `SearchPatternScanner.CollectFileHits` mit 6 Parametern
  am Limit `MaxMethodParameterCountForNonPublic: 6`** —
  (`SearchPatternScanner.cs:114-120`): die Methode braucht
  `filePath`, `relativePath`, `pattern`, `regex`, `hitLines`,
  `ref totalMatches`. Aktuell erlaubt (private → NonPublic-Override
  6), aber jede zukünftige Erweiterung (z. B. zusätzlicher
  `CancellationToken`-Parameter) reißt das Limit. **Vorschlag** (kein
  TD-Eintrag, nur Beobachtung): bei der nächsten Scanner-Erweiterung
  die Parameter in ein Input-`record` ziehen, analog dem TD-009-
  Vorschlag für `McpCodeGraphServer`.
- **O-3 — Stilles Ignorieren von Datei-Lese-Fehlern** — `SearchPatternScanner.cs:127-128, 148-149, 167-168` fangen `IOException`/
  `UnauthorizedAccessException` ohne Zählung/Logging. Plan Z. 341-343
  hat das explizit als "zu nah am 'nice to have'" für 002 markiert
  und eine aggregierte Fehlerzählung in den Tool-Output als
  Folge-Einheit zurückgestellt. **Akzeptabel für 002**, aber Wert
  für Tech-Debt-Notiz "EPIC-08-Pfad: aggregierte Fehlerzählung im
  Tool-Output bei großen Solutions".
- **O-4 — `Task.Run` in `SearchPatternTool.ExecuteAsync:51-53`** —
  andere Tools (z. B. `GetViolationsTool`) machen es ohne. Plan
  Z. 367-376 hat das explizit als bewusste Entscheidung markiert
  (CPU-/IO-bound Scan-Arbeit, hält `McpCodeGraphServer`-Lock nicht)
  und als **MINOR für den Kritiker** deklariert. A5 ("fertig ist
  fertig") gilt hier. **Akzeptabel** — kein Issue.
- **O-5 — `McpTruncation.TruncateLines` Meta-Zahl = `maxResults` statt
  `shown.Count`** — `McpTruncation.cs:40` gibt `{maxResults}` für
  "M gezeigt" aus, was == `shown.Count` ist, **solange** der
  Aufrufer `hitLines.Count >= maxResults` garantiert (was
  `SearchPatternScanner.SearchAndFormat` in 002 tut). In einer
  künftigen EPIC-08-Optimierung mit Vorab-Trunkierung wäre die
  Meta-Zahl dann falsch. Plan Z. 258-263 diskutiert das nur für
  `totalMatches`, nicht für "M gezeigt". **Latent**, nicht in 002
  relevant. Kein TD-Eintrag, Beobachtung für 003/004/005.
- **O-6 — `SymbolGraphToolRegistrations` 2487/2500 (Puffer 13 Z.,
  knapp)** — vorbestehende Beobachtung (Plan-Anhang Z. 604, 621),
  **nicht** durch 002 verursacht. Beim nächsten Symbolgraph-Tool
  sehr wahrscheinlich eine 5. Registrar-Klasse fällig. **Tech-Debt-
  Vorschlag TD-011** (niedrig) — siehe unten.

### Tech-Debt-Kandidaten (außerhalb 002)

Diese zwei Vorschläge werden **als Empfehlung** in dieses Review
aufgenommen, nicht direkt in `tech-debt.md` editiert (A7/A5 — der
Nutzer entscheidet):

- **TD-010 (mittel, Vorschlag) — `SearchPatternTool`-Footprint 2482/2500
  strukturell knapp.** Derselbe `McpCodeGraphServer.Config`-Pull-in wie
  TD-008. Konkrete Auslöser-Schwelle: jedes weitere analyse-orientierte
  Tool in `AnalysisToolRegistrations` (Puffer 24) **oder** jede
  weitere Konfigurations-Property an `McpCodeGraphServer`, die den
  Server-Referenzen transitiv hinzugefügt wird. Strukturelle Lösung
  (gleich wie TD-008): `ILinterEngineConfig`-Interface
  (4-6h-Refactor). Pragmatische Lösung (gleich wie TD-008):
  `PathOverrides`-Eintrag `MaxAIContextFootprint: 2700` für
  `SearchPatternTool` in `rules.json` — sofort, günstig. **Empfehlung:**
  pragmatische Lösung (PathOverride) jetzt, strukturelle Lösung
  konsolidiert mit TD-008 in einer späteren Refactor-Einheit.
- **TD-011 (niedrig, Vorschlag) — `SymbolGraphToolRegistrations` 2487/2500
  (Puffer 13 Z.) für das 6. Symbolgraph-Tool sehr wahrscheinlich
  nicht mehr ausreichend.** Vorbestehende Beobachtung, nicht durch
  002 verursacht. 5 Symbolgraph-Tools sind bereits in der Klasse
  registriert; der Plan-Anhang (Z. 604, 621) hat das notiert.
  **Empfehlung:** beim nächsten Planer-Aufruf, der ein
  Symbolgraph-Tool hinzufügt, **vorab** re-messen; bei Bedarf eine
  5. Registrar-Klasse `XxxToolRegistrations` einplanen, statt
  reaktiv nach gerissenem Limit (Lessons Learned aus TD-004).
- **TD-006 (bereits offen) — `SafeEnumerateFiles`/`IsGeneratedPath`
  jetzt 3× dupliziert** (`WebFileCatalog.cs:105-113/149-155` +
  `GetIndexScopeScanner.cs:78-94` + `SearchPatternScanner.cs:161-177`).
  Der Coder hat die "Default"-Variante (private Kopie, bewusste
  Entscheidung in Vor-der-Planung-Check 1) gewählt. **Kein** neuer
  Eintrag, nur Hinweis im Review — TD-006-Eintrag bleibt unverändert
  gültig, die Schließung (Extraktion in eine `FileSystemScanHelpers`-
  Klasse o. ä.) bleibt eine separate Tech-Debt-Einheit.
- **`McpToolResults.InvalidArgument` mit hartcodiertem Hint** — siehe
  **M-1** oben. Vorschlag als **TD-012 (niedrig)**: Helper entweder
  in `InvalidArgumentExclusiveParams()` umbenennen (Klarheit
  statt Bequemlichkeit) oder generisch machen mit optionalem
  `hint`-Parameter (Default = generischer Hinweis). Beide Optionen
  würden verhindern, dass Folge-Tools in dieselbe Falle laufen.
  **Nicht direkt vorgeschlagen als TD-012** — die M-1-Fix-Runde
  behebt das 002-Problem ad-hoc; TD-012 wäre eine sauberere
  Schließung, die der Nutzer entscheiden sollte.

## Verdict-Begründung

- **1 MAJOR (M-1)** → Verdict `issues` nach `agents/kritiker.md` Z. 38-41.
- Build und Tests sind grün (verifiziert per `dotnet test --filter
  "FullyQualifiedName~SearchPattern"` → 9/9), A3-Nachweise für alle
  9 neuen Tests + 1 modifizierter Test plausibel, Footprint-
  Messung TD-004 verifiziert (Werte exakt wie im `result.md`),
  Konzept-Treue sauber, Plan-Konformität vollständig (alle 7
  Schritte belegt), 6 offene Fragen beantwortet mit Begründung.
- **Kein CRITICAL, 1 MAJOR, 6 MINOR-Beobachtungen, 2 Tech-Debt-
  Vorschläge (TD-010, TD-011), 0 direkte Edits an `tech-debt.md`**
  (A7/A5, Vorschläge zur Nutzer-Entscheidung).

## Nächste Aktion des Orchestrators

`issues` → **Fix-Runde 002/fix-01/** mit gezielter Korrektur in
`src/AiNetLinter/Mcp/Tools/SearchPatternTool.cs:40`:
`McpToolResults.InvalidArgument("pattern darf nicht leer sein.")`
ersetzen durch
`McpToolResults.Error(LinterErrorCodes.InvalidArgument,
"pattern darf nicht leer sein.",
hint: "Pattern angeben — leeres Pattern ist nicht erlaubt.")`
(analog der bereits korrekten Nutzung in Z. 57-60). Optional:
Test 8 in `src/AiNetLinter.Tests/Mcp/Tools/SearchPatternToolTests.cs:162-174`
um eine Hint-Assertion erweitern (analog der Regex-Hint-Assertion in
Test 7, Z. 158), damit der A3-Pfad auch die Hint-Qualität abdeckt.
Aufrufe danach: 3/40 (1 Planer, 1 Coder, 1 Kritiker + ggf. 1 Coder für fix-01).
