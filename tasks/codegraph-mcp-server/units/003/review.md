---
unit: 003
task: codegraph-mcp-server
workflow: dynamic-loop
type: review
created_by: kritiker
created_at: 2026-08-01
verdict: approved
---

# Review Einheit 003 — EPIC-05 Miss-Hint + Scope-Kommunikation in `find_symbol`

**Verdict: approved**

## Selbst-Verifikation

Re-Run teilweise: Build (`dotnet build AiNetLinter.slnx`, 0/0),
Targeted-Test-Run (9/9 grün), Volllauf `dotnet test`
(1101/1101 grün, 8 m 4 s), und Footprint-Messung aller drei
relevanten Klassen (`FindSymbolTool` 2529, `SymbolGraphToolRegistrations`
2488, `McpServerOptionsFactory` 2484 — exakt die im `result.md`
dokumentierten Werte). A3-Nachweise und Dogfooding-Output wurden
nicht erneut ausgeführt, weil das `result.md` beide wortwörtlich
dokumentiert und die Plausibilität anhand der Code-Inspektion
(`FindSymbolTool.cs:51-66`, `McpServerOptionsFactory.cs:26-31/47`)
direkt prüfbar ist.

## Findings sortiert nach Ebenen

### Ebene 1 — Plan-Erfüllung

Alle 9 Schritte umgesetzt, in der dokumentierten Reihenfolge:

| Schritt | Soll | Ist |
|---|---|---|
| 0 — Fixture `site.js` +1 Zeile `userService` | Eindeutig in keiner `.cs`-Datei | `rg "userService" tests/Fixtures/SymbolGraphMini --type cs` → no matches (verifiziert) |
| 1 — `FindSymbolTool.cs` Miss-Hint-Pfad | Block in `if (filtered.Count == 0)` mit `GetFilesWithHits` + Pfadliste + Fallback-Hinweis | `FindSymbolTool.cs:51-66` — wörtlich dem Plan-Schritt 1 entsprechend |
| 2 — `SymbolGraphToolRegistrations.cs` Description | +1 Satz "Bei 0 Treffern wird auf Textvorkommen in Nicht-C#-Dateien hingewiesen." | `SymbolGraphToolRegistrations.cs:33` — wörtlich |
| 3a — `McpServerOptionsFactory.cs` Const `ServerInstructions` | Tool-Liste vollständig (6 C#-only + Fallback + 2 gegenbeispielliche) | `McpServerOptionsFactory.cs:26-31` — wörtlich dem Plan-Wortlaut |
| 3b — `ServerInstructions = ServerInstructions,` im `McpServerOptions`-Initializer | Zuweisung hinzufügen | `McpServerOptionsFactory.cs:47` ✓ |
| 4 — Footprint-Re-Messung | 3 Klassen (FindSymbolTool, SymbolGraphToolRegistrations, McpServerOptionsFactory) | `result.md` Tabelle dokumentiert; unabhängige Re-Messung ergibt exakt die gleichen Zahlen |
| 5 — Tests in `FindSymbolToolTests.cs` | 1 modifiziert + 3 neu | 1 modifiziert (Fixture-Switch `BaselineMini`→`SymbolGraphMini`, `FindSymbolToolTests.cs:52`) + 3 neu (Z. 60-77, 79-92, 94-107) — wörtlich dem Plan folgend |
| 6 — Test für `ServerInstructions` | In `McpServerCommandTests.cs` | **Abweichung:** in neuer Datei `McpServerOptionsFactoryTests.cs` (siehe Plan-Abweichung unten) |
| 7 — Build und Tests | 0/0 Build, 1101/1101 grün | Verifiziert: Build 0/0, Volllauf 1101/1101 grün |
| 8 — Dogfooding | manueller Server-Start + `initialize` + `find_symbol`-Calls | `result.md` Z. 494-557 — 4 Szenarien (initialize, kein-Treffer, Caller-C#-Treffer, Miss-Hint) wortwörtlich protokolliert |
| 9 — Conventional Commit | `feat(mcp):` + Task-Suffix, gezielter `git add`, kein Push | `dd4b44e feat(mcp): find_symbol miss-hint + initialize instructions [codegraph-mcp-server]`, 6 Dateien, `git show --stat` bestätigt — Conventional-Format und Suffix korrekt |

**A3-Fehlschlag-Nachweise** für alle 4 neuen Tests sind plausibel:

- **Test 2** (`FindMatchesAsync_NoCsMatchButNonCsHit_ReturnsMissHintWithFileList`): A3-Auslöser ist das Auskommentieren des Miss-Hint-Blocks → `Assert.Contains("Hinweis: kein C#-Symbol…")` schlägt fehl. Failure-Output im `result.md` Z. 204-211 ist exakt der erwartete xUnit-`Assert.Contains` "Not found"-Output. Plausibel.
- **Test 3** (`FindMatchesAsync_NoCsMatchAndNoNonCsHit_ReturnsPlainNoMatchText`): A3-Auslöser ist das Entfernen der `if (missHits.Count == 0) return baseText;`-Verzweigung → `Assert.DoesNotContain("Hinweis: kein C#-Symbol…")` schlägt fehl. Failure-Output Z. 251-259 mit Diff-Visualisierung ist plausibel.
- **Test 4** (`FindMatchesAsync_KindFilterMissHit_StillFires`): A3-Auslöser identisch zu Test 2, mit `kind: "class"`. Failure-Output Z. 279-287 enthält den abgeschnittenen String `"(Kind-Filter: cla"`, der den Kind-Filter-Pfad nachweist — sauberer A3-Beleg, plausibel.
- **Test 5** (`Create_ServerInstructionsContainsScopeHint`): A3-Auslöser ist das Entfernen der `ServerInstructions = ServerInstructions,`-Zeile → `Assert.False(string.IsNullOrEmpty(options.ServerInstructions))` schlägt fehl. Failure-Output Z. 327-334 mit `Expected: False, Actual: True` ist plausibel (ServerInstructions ist `null`, `string.IsNullOrEmpty` → `True`).

Auch der modifizierte Test 1 hat plausibles "additives Regressionsschutz"-A3: `DoesNotExistXyz` existiert in keiner Datei der Fixture (im Plan verifiziert, im `result.md` Z. 169-173 mit `rg` belegt), daher grün mit und ohne Miss-Hint-Code — der additive Effekt der Erweiterung ist abgesichert.

**Plan-Abweichung: 1 neue Test-Datei statt 0.**

*Bewertung: **begründet** — kein Issue.*

- `McpServerCommandTests.cs` ist tatsächlich bei **499/500 Z.** (verifiziert per `(Get-Content).Count`).
- `MaxLineCount: 500` ist `Severity: error` in `rules.json:354-356` — `MaxLineCount` ist eine harte Build-Regel, kein Lint-Hinweis.
- +14 Z. neuer Testcode → 513 Z. → `MaxLineCount`-Violation → Build rot.
- Konsequenz: `CliIntegrationTests.RunLinterCli_OnWholeSolution_ReturnsSuccess`, `GeneratePlaybook_ForSolution_GeneratesAndUpdatesPlaybook`, `GeneratePlaybook_WithCheckFlag_ReturnsOkWhenUpToDate` (alle drei rufen `--path` auf die ganze Solution auf, also inkl. `McpServerCommandTests.cs:500+`) — die 4. `RunLinterCli_WithInvalidConfig_ReturnsErrorExitCode` (`CliIntegrationTests.cs:257`) verwendet ein non-existent config und lint't die Solution **nicht** (siehe Z. 262-263 `Path.Combine(rootDir, "non-existent-config.json")`). Der Coder-Bericht spricht von "4 `CliIntegrationTests`"; die korrekte Anzahl der betroffenen Tests ist **3**, nicht 4 — siehe MINOR-Beobachtung 1.
- Alternative wäre eine kosmetische Kürzung des `ServerInstructions`-Const-Strings; das wäre Konzept-Drift (A7: `ServerInstructions` ist "kanonische Formulierung" laut Plan, soll nicht pro Einheit anders klingen) und hätte die Aussage-Kraft der Scope-Kommunikation reduziert.
- Die Extraktion in eine eigenständige 31-Z.-Datei ist sauberer, thematisch fokussiert, und vermeidet `MaxLineCount`-Kollision dauerhaft (siehe MINOR-Beobachtung 1).
- **Die neue Datei selbst hält alle Regeln ein** (siehe Ebene 2), die Extraktion ist also kein Regel-Verstoß.

### Ebene 2 — Rules-Konformität

Regeln aus `.agents/rules/AiNetLinter.mdc` + `rules.json`:

| Regel | Soll | Ist (neue Datei `McpServerOptionsFactoryTests.cs`) |
|---|---|---|
| `EnforceNullableEnable` | `#nullable enable` am Dateianfang | ✓ Z. 1 |
| `EnforceSealedClasses` | `public sealed class` | ✓ Z. 17 |
| `MaxLineCount` | ≤ 500 | ✓ 31 Z. |
| `MaxMethodLineCount` (Tests) | ≤ 100 | ✓ Test-Methode 9 Z. |
| `MaxMethodParameterCount` (Tests) | ≤ 6 | ✓ 0 Parameter |
| `EnforceNamespaceDirectoryMapping` | Namespace = Verzeichnis-Pfad | ✓ Namespace `AiNetLinter.Tests.Mcp` matched `src/AiNetLinter.Tests/Mcp/` |
| `EnforceAsciiIdentifiers` | keine Umlaute/Sonderzeichen | ✓ ("ausgelagert", "weil", "Test") — Umlaut-Ersetzung befolgt |
| `EnforcePascalCase` | öffentliche Typen/Methoden PascalCase | ✓ `McpServerOptionsFactoryTests`, `Create_ServerInstructionsContainsScopeHint` |
| `EnforceSemanticNaming` | keine generischen Namen | ✓ sprechend |
| `Collection("ConsoleTestCollection")` | für Thread-Isolation analog zu `FindSymbolToolTests.cs:10` | ✓ Z. 16 |

Regel-Konformität der übrigen 5 modifizierten Dateien:

- `FindSymbolTool.cs:51-66`: cyclomatic +2 (jetzt 4), cognitive +1 (jetzt 2) → weit unter `MaxCyclomaticComplexity: 12` / `MaxCognitiveComplexity: 15`. `FindMatchesAsync` wächst von ~20 auf ~30 Z. → ≤ `MaxMethodLineCount: 60`. 4 Parameter (`solution, namePattern, kind, ct`) → ≤ `MaxMethodParameterCount: 4`. ✓
- `SymbolGraphToolRegistrations.cs:31-33`: nur String-Konkatenation, keine Logik. ✓
- `McpServerOptionsFactory.cs:26-31, 47`: `ServerInstructions` const (5 Zeilen String) + 1 Zuweisung. `Create` Methode bleibt 12 Z. (≤ 60). ✓
- `site.js`: `JS_MaxJsLineCount: 150` → 2 Z. ✓
- `FindSymbolToolTests.cs`: Datei wächst von 65 auf 119 Z. (≤ 500). 4 Test-Methoden ≤ 100 Z. ✓. **Achtung:** Die Datei hat **kein** `#nullable enable` am Dateianfang (`FindSymbolToolTests.cs:1` ist `using AiNetLinter.Baseline;`). Das ist ein **pre-existing** Issue aus Commit `e89ede5c` (2026-07-31, vor 003), nicht durch 003 verursacht — kein 003-Issue, kein 003-Fix (A2, A5). Siehe MINOR-Beobachtung 2.

### Ebene 3 — Logische Korrektheit

- **Miss-Hint-Pfad** (`FindSymbolTool.cs:51-66`): saubere 2-stufige Logik — zuerst `baseText` bauen (immer mit `kindSuffix`), dann prüfen ob `missHits` leer ist, bei leerer Liste `baseText` zurückgeben, sonst Hint-Zeile anhängen. Der Miss-Hint-Pfad **erbt** den `kindSuffix` aus dem `baseText`, was korrekt ist (Test 4 verifiziert das: `"(Kind-Filter: class)"` erscheint, dann Hint).
- **Edge-Case "weder C#-Treffer noch Nicht-C#-Treffer"** (Test 3): `userService` würde JS-Treffer liefern, deshalb wählt der Test `DoesNotExistXyzBlub123` — ein Name, der garantiert in keiner Datei der Fixture vorkommt. Test 3 verifiziert, dass bei leerer `missHits` der Pfad `return baseText;` greift und `Assert.DoesNotContain("Hinweis: kein C#-Symbol")` stimmt. Korrekt.
- **Edge-Case "Kind-Filter + Miss-Hint"** (Test 4): `userService` mit `kind: "class"` — `FilterByKind` filtert nur C#-Symbole, der `missHits`-Aufruf ist unabhängig vom Kind-Filter. `GetFilesWithHits("userService", isRegex: false)` liefert weiterhin `["site.js"]` (Substring-Match im JS-Content), der Hint wird angehängt. Korrekt.
- **`ServerInstructions`-Inhalt** (`McpServerOptionsFactory.cs:26-31`): nennt alle 6 C#-only-Tools (`find_symbol`, `find_references`, `get_impact`, `get_type_hierarchy`, `get_file_skeleton`, `get_violations`) + `search_pattern` als Fallback + `get_index_scope`/`get_hotspots` als gegenbeispielliche Tools. Vollständig, kein Tool vergessen. Dogfooding (`result.md` Z. 502-507) bestätigt, dass der Text wortwörtlich im `initialize`-`instructions`-Feld landet.
- **Tests echt (A3)**: alle 4 dokumentierten A3-Nachweise prüfen das tatsächliche Verhalten, nicht die Implementierung. Test 2 prüft die Hint-Zeile, Test 3 prüft das Fehlen der Hint-Zeile, Test 4 prüft das Zusammenspiel mit Kind-Filter, Test 5 prüft den Inhalt von `options.ServerInstructions`. Kein "Spricht-die-Implementierung-nach"-Test.
- **Fixture-Datei `site.js:2`** (`function userService() { return "ok"; }`): ein plausibles JS-Konstrukt, das im realen Code vorkommen würde. Nicht künstlich (z. B. `var x = 1;`).

### Ebene 4 — Konzept-Treue

- `konzept.md` Z. 604-606 (Miss-Hint-DoD: "Anfrage nach einem Namen, der nur in einer `.js`/`.razor`/`.xaml`-Datei vorkommt, liefert die explizite Miss-Hint-Meldung statt einer stillen Leermenge"): ✓ erfüllt durch Test 2 (userService → `site.js` → Hint mit `site.js` in der Liste, Fallback `search_pattern` erwähnt) und durch Dogfooding (Z. 538-551 im `result.md`).
- `konzept.md` Z. 98-101 (EPIC-05 Definition — Scope-Kommunikation + Miss-Hint in `find_symbol`): ✓ beide Teile erfüllt — Tool-Description erweitert (`SymbolGraphToolRegistrations.cs:33`), `initialize`-`instructions`-Feld gesetzt (`McpServerOptionsFactory.cs:47`).
- `konzept.md` Z. 161-164 (`initialize`-`instructions`-Feld, vom SDK unterstützt): ✓ Der SDK-Property-Name `ServerInstructions` ist semantisch korrekt (Plan-Check 3 hat das per Reflection-Probe verifiziert; das `initialize`-Antwort-Feld im Wire-Format heißt tatsächlich `instructions`, was im Dogfooding Z. 506 bestätigt ist). Der Konzept-Wortlaut ist semantisch verkürzt (nennt das Wire-Format-Feld, nicht die SDK-Property) — kein Konzept-Konflikt, weil Konzept und Code semantisch identisch sind. Siehe MINOR-Beobachtung 3.
- `konzept.md` Z. 184 ("Tool-Set wie unten unter 'Wie' beschrieben, 9 Tools"): ✓ kein Tool-Set-Eingriff, `find_symbol` bleibt im Set, nur das Verhalten ist erweitert.
- `konzept.md` Z. 156-158 ("jede Tool-`description` benennt die Grenze explizit"): ✓ für `find_symbol` schon vor 003 in Z. 32 ("Deckt nur .cs-Dateien ab, keine .js/.razor/.xaml/.html/.css-Dateien."), in 003 nur additiv um den Miss-Hint-Hinweis erweitert. Andere Tools (`find_references` Z. 42-44, `get_impact` Z. 53-56, `get_type_hierarchy` Z. 65-68) bereits explizit, kein 003-Scope.
- `konzept.md` Z. 167-174 (Miss-Hint-Pfad-Konzept): ✓ `find_symbol` ohne C#-Treffer macht Text-Fallback über Solution-Dateibestand, meldet "kein C#-Symbol, aber Texttreffer in `<Datei>` (nicht Teil des Graphs)" — Wortlaut entspricht dem Konzept in `FindSymbolTool.cs:64-65`.

## Sonstige Beobachtungen (MINOR)

1. **`McpServerCommandTests.cs` ist faktisch voll** (499/500 Z., `Commands/McpServerCommandTests.cs:499`). Coder hat das in `result.md` Beobachtung 3 dokumentiert. Empfehlung für 004+: thematische Aufteilung in `McpServerCommandResolvePathTests` (`ResolveSolutionPathOrError`/`TryLoadSolutionAsync`/`ResolveMaxLineCount`/`ResolveConfig`) + `McpServerCommandIntegrationTests` (`RunAsync_ValidFixture_*` E2E). Eigener Refactor, kein 003-Scope. **Coder-Bericht-Inkonsistenz:** `result.md` Z. 100 spricht von "4 `CliIntegrationTests`", die korrekte Anzahl der **betroffenen** Tests mit Whole-Solution-Lint ist **3** (`RunLinterCli_OnWholeSolution_ReturnsSuccess`, `GeneratePlaybook_ForSolution_GeneratesAndUpdatesPlaybook`, `GeneratePlaybook_WithCheckFlag_ReturnsOkWhenUpToDate`) — der 4. `RunLinterCli_WithInvalidConfig_ReturnsErrorExitCode` (`CliIntegrationTests.cs:257`) verwendet ein non-existent config und lint't nicht die ganze Solution. Die Begründung der Abweichung bleibt valide (3 betroffene Tests sind genug), nur die Zählung ist ungenau.

2. **`FindSymbolToolTests.cs` fehlt `#nullable enable` am Dateianfang** (`FindSymbolToolTests.cs:1` ist `using AiNetLinter.Baseline;`). Pre-existing aus Commit `e89ede5c` (2026-07-31), nicht durch 003 verursacht. Kein 003-Issue, kein 003-Fix (A2, A5). **Aber:** der Coder hat die Datei in 003 um 4 Tests erweitert — bei strikter A2/A5-Auslegung ist das keine 003-Verantwortung; bei strenger Auslegung wäre "wenn ich die Datei schon anfasse, fixe ich auch den `#nullable enable`-Verstoß mit" ein vertretbares Add-On. Beide Sichtweisen sind begründbar; ich notiere es als zukünftigen TD-Kandidaten. **Neue Datei** `McpServerOptionsFactoryTests.cs` hat das `#nullable enable` korrekt — der Coder hat das in der neuen Datei richtig gemacht, die Lücke in der bestehenden Datei aber nicht gesehen oder bewusst nicht angefasst.

3. **Konzept-Wortlaut `instructions` vs. SDK-Property `ServerInstructions`** (`konzept.md:161-164` spricht von "instructions-Feld", SDK-Property heißt `ServerInstructions`). Semantisch kein Konflikt — das `initialize`-Antwort-Feld im Wire-Format heißt `instructions` (Dogfooding Z. 506 bestätigt), die SDK-Property heißt `ServerInstructions` (Plan-Check 3 per Reflection verifiziert). **Keine** Konzept-Änderung in 003 (A7), nur Hinweis: bei einer künftigen Konzept-Überarbeitung könnte Z. 161-164 präzisiert werden, indem die SDK-Property `ServerInstructions` namentlich erwähnt wird. **Nicht** 003-Scope.

4. **`FindSymbolTool` 2529/2700 (PathOverride) — Puffer 171 Z.** (`result.md` Footprint-Tabelle). Mit PathOverride 2700 (TD-008-Präzedenz) hat das Tool weiterhin komfortablen Puffer. Nächste Erweiterung (Trunkierung 004) wird das Tool wahrscheinlich auf ~2700 treiben — dann TD-008-Verschärfung oder Scanner-Split nötig. Aktuell kein Handlungsbedarf, aber für 004+ im Hinterkopf.

## Tech-Debt-Kandidaten (außerhalb 003-Scope)

Drei Beobachtungen aus 003, die als zukünftige `tech-debt.md`-Einträge taugen. **Empfehlung** im Sinne von A2/A5/A6: als Vorschlag im Review, kein direkter Edit (A7).

### Vorschlag 1 — `FindSymbolScanner.cs` fehlt (TD-005-Generalisierung)

**Bereich:** `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` (Logik im Tool, kein separater Scanner).

**Befund:** `find_symbol` ist analog zu `search_pattern` (eigener `SearchPatternScanner`) und `get_violations` (eigener `GetViolationsScanner`) das einzige MCP-Tool ohne Scanner-Abspaltung — die gesamte Logik lebt im Tool (101 Z. → 112 Z. nach 003, inkl. `ExecuteAsync` + `FindMatchesAsync` + `FilterByKind` + `FormatSymbolLocations` + `DescribeKind`). Das ist ein bewusster Tradeoff (Plan-Check 1: 003 lebt nicht vom Scanner-Split, das wäre Scope-Creep) — aber genau die Diskrepanz macht den Punkt sichtbar: würde `find_symbol` als **neues** Tool gebaut, würde der Coder (per TD-005-Konvention) sofort `FindSymbolScanner` extrahieren. Bei bestehenden Tools ist das Nachholen teurer als bei neuen — also wartet man, bis eine Trunkierung/Erweiterung das Tool eh anfasst.

**Vorschlag:** Wenn 004+ Trunkierung in `find_symbol` einbaut (analog `McpTruncation.cs` für `search_pattern` in 002), den Scanner-Split in derselben Einheit mitnehmen — dann kostet es ~10 Z. extra Diff, statt einer eigenständigen Refactor-Einheit.

**Priorität:** niedrig.

### Vorschlag 2 — `McpTruncation` nicht an `find_symbol` Miss-Hint angeschlossen

**Bereich:** `FindSymbolTool.cs:63` (`var fileList = string.Join(", ", missHits);`).

**Befund:** Der Miss-Hint-Pfad hängt **alle** Treffer-Dateien kommasepariert an — bei sehr vielen Textfunden (>10 Dateien, was in `AiNetLinter.slnx` mit dem `EPIC-05`-Pattern schon 3 Dateien sind, siehe `result.md` Z. 542) wird die Hint-Zeile entsprechend lang. `McpTruncation` (eingeführt in 002 für `search_pattern`) trunkiert den Haupt-Treffer-Output, ist aber **nicht** auf den Miss-Hint angewendet. In der SymbolGraphMini-Fixture (1 Datei) und der AiNetLinter-slnx (max. 3 Dateien) ist das kein Problem; bei einer Last-Fixture (500 Dateien, `konzept.md` P1-6) mit einem weitverbreiteten String-Literal könnte der Hint Hunderte von Dateien auflisten.

**Vorschlag:** Bei 004+ (Trunkierung in `find_symbol`) `McpTruncation` auf die Miss-Hint-Liste anwenden, mit konsistenter Meta-Zeile (z. B. `"[342 Dateien mit Textfund, 10 gezeigt — search_pattern fuer Details]"`). Pattern identisch zum bestehenden `McpTruncation`-Helper.

**Priorität:** niedrig.

### Vorschlag 3 — `McpServerOptionsFactory` 2484/2500 (Puffer 16 Z.)

**Bereich:** `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` (gemessen 2484/2500, 16 Z. Puffer, Stand `dd4b44e`).

**Befund:** Der `ServerInstructions`-Block (+14 Z.) hat diese Klasse an die Grenze gebracht. Der Const-String ist konzeptuell bindend (kanonische Formulierung laut Plan-Schritt 3) und sollte **nicht** weiter wachsen. Aber: die P0/P1-Extensions aus `konzept.md` Z. 207-324 (z. B. `--mcp-log`-State, "lädt noch"-State, `rules.json`-Auto-Discovery, Staleness-Sweep-`mtime`-Kurzschluss) werden `McpServerOptionsFactory` mit hoher Wahrscheinlichkeit erneut erweitern — die nächsten 16 Z. reißen das Limit. Coder dokumentiert das in `result.md` Beobachtung 2.

**Vorschlag:** Vor der nächsten substanziellen Erweiterung an `McpServerOptionsFactory` (z. B. bei Einbau des `--mcp-log`-Flags aus P0/P1) eine Aufteilung prüfen — z. B. ein `McpServerOptionsBuilder`-Pattern (analog `McpServerCommand`-Aufteilung in `McpServerOptionsFactory` + Registrar-Klassen) oder ein Init-`record` (analog TD-009-Vorschlag für `McpCodeGraphServer`). Nicht eigenständige Refactor-Einheit, sondern **inline** beim nächsten Anlass.

**Priorität:** niedrig.

## Verdict

**approved** — alle 9 Schritte umgesetzt, 4 neue Tests mit plausiblen A3-Nachweisen, Build 0/0, Volllauf 1101/1101 grün, Footprint aller 3 Klassen unter Limit, Plan-Abweichung begründet, Regeln eingehalten, Konzept erfüllt. 4 MINOR-Beobachtungen (voll-`McpServerCommandTests.cs`, pre-existing `#nullable enable`-Lücke in `FindSymbolToolTests.cs`, Konzept-Wortlaut vs. SDK-Property, PathOverride-Puffer) plus 3 Tech-Debt-Vorschläge für 004+.
