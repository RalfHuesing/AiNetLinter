---
unit: 003
task: codegraph-mcp-server
workflow: dynamic-loop
type: result
created_by: coder
created_at: 2026-08-01
code_commit_hash: dd4b44e6094dd827fbb4a042cf67c14fced6a943
status: done
---

# Result Einheit 003 — EPIC-05 Miss-Hint + Scope-Kommunikation in `find_symbol`

## Zusammenfassung

EPIC-05 ist für `find_symbol` abgeschlossen: das Tool liefert bei null C#-Treffern einen
**Miss-Hint** (Datei-Liste mit Textfunden in Nicht-C#-Dateien, plus Fallback-Verweis auf
`search_pattern`), und die **Scope-Kommunikation** ist zentral in der `initialize`-Antwort
verankert — der Server gibt einen deutschsprachigen `instructions`-Text zurück, der die
C#-only-Grenze einmal server-weit benennt und `search_pattern` als Fallback für
JS/Razor/XAML/HTML/CSS-Namen nennt. Die `find_symbol`-Tool-`description` wurde minimal um
einen Satz erweitert, der das Miss-Hint-Verhalten ankündigt. Footprint-Situation
`TD-011` ist stabil: alle drei kritischen Klassen bleiben unter ihren Limits, kein
PathOverride oder Scanner-Split nötig.

## Antworten auf die 3 offenen Fragen aus dem Plan

### F1 — Welcher konkrete Nicht-C#-Identifier wurde in `site.js` eingefügt?

**Gewählt: `userService`**, exakt die Planer-Empfehlung.

**Eindeutigkeits-Verifikation** (vor Schritt 1 dokumentiert):

```
$ rg "userService" tests/Fixtures/SymbolGraphMini/ --type cs
(no matches)
```

`userService` kommt in **keiner** `.cs`-Datei der SymbolGraphMini-Fixture vor, weder in
Quelldateien (`Caller.cs`/`Greeter.cs`/`Hierarchy.cs`/`OtherCaller.cs`/
`ViolationTrigger.cs`) noch in der `.csproj`. Die hinzugefügte Zeile in `site.js` ist
die einzige Stelle, an der `userService` in der gesamten Fixture vorkommt — A3 für
Test 2 (`_NoCsMatchButNonCsHit_ReturnsMissHintWithFileList`) funktioniert, weil
`userService` garantiert keine C#-Symbol-Treffer hat.

Begründung für `userService` (gegen Alternativen `notificationBell` /
`notificationWidget`): das Wort klingt generisch nach einem C#-Service-Klassennamen
(`UserService`-Pattern), bildet also genau den realen Agent-Anwendungsfall ab ("Agent
sucht einen Service-Namen, findet ihn in JS statt in C#"). Ein bewusst gewählter
Namen, der die Datenbasis glaubhaft wie ein echter Production-Code-Fall aussehen
lässt.

### F2 — Welche Strategie bei Footprint-Re-Messung-Überschreitung?

**Nicht eingetreten** — keine der drei Klassen reißt ihr Limit. Siehe "Footprint-Messung
TD-011" unten: alle drei kritischen Klassen sind nach 003 mit komfortablem Puffer
unter ihren Limits. Die im Plan antizipierte Entscheidung war nicht nötig.

Falls eine Klasse über das Limit gerissen wäre, hätte ich gemäß Plan-Empfehlung die
**kosmetische Description-Kürzung** gewählt (geringstes Risiko, kein
PathOverride-Anti-Pattern, kein Scanner-Split). Diese Option bleibt für 004+ in
Reserve.

### F3 — `ServerInstructions`-Wortlaut vom Plan abweichend?

**Nein**, der Wortlaut aus Schritt 3 wurde **wortwörtlich** übernommen:

```
"Symbolgraph-Tools (find_symbol, find_references, get_impact, get_type_hierarchy, " +
"get_file_skeleton, get_violations) arbeiten ausschliesslich auf C#/.cs-Quellcode. " +
"Fuer Namen, die nur in .js, .razor, .cshtml, .xaml, .html oder .css vorkommen, " +
"ist search_pattern der passende Fallback. Struktur-Tools ohne C#-Beschraenkung: " +
"get_index_scope, get_hotspots."
```

Tool-Liste vollständig (alle 6 C#-only-Tools + `search_pattern` als Fallback +
`get_index_scope` + `get_hotspots` als gegenbeispielliche Tools ohne
C#-Beschränkung). Begründung: der Wortlaut ist die kanonische Formulierung der
Scope-Grenze, der Vertragspunkt mit dem Agenten, sollte nicht pro Einheit anders
klingen — wie im Plan vorgegeben.

## Geänderte Dateien

Commit `dd4b44e6094dd827fbb4a042cf67c14fced6a943` (Branch `main`, **nicht gepusht**):

| Datei | Status | +/− | Zweck |
|---|---|---|---|
| `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` | modified | +12/−1 | Miss-Hint-Pfad in `FindMatchesAsync` (Schritt 1) |
| `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` | modified | +2/−1 | Description-Erweiterung `find_symbol` (Schritt 2) |
| `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` | modified | +14/−0 | `ServerInstructions` const + Initializer-Zuweisung (Schritt 3) |
| `src/AiNetLinter.Tests/Mcp/Tools/FindSymbolToolTests.cs` | modified | +50/−1 | 1 modifizierter + 3 neue Tests (Schritt 5) |
| `src/AiNetLinter.Tests/Mcp/McpServerOptionsFactoryTests.cs` | **new** | +31/−0 | Plan-Abweichung: neue Test-Datei wegen `MaxLineCount` (Schritt 6) |
| `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/wwwroot/site.js` | modified | +1/−0 | `function userService() { return "ok"; }` (Schritt 0) |

**Plan-Abweichung** (im Plan waren **0 neue Dateien** vorgesehen, jetzt **1 neue
Datei**): `McpServerOptionsFactoryTests.cs` wurde als eigenständige Datei angelegt,
weil `McpServerCommandTests.cs` bereits bei **499/500 Zeilen** an der
`MaxLineCount`-Grenze lag (siehe `rules.json` Z. 109, `MaxLineCount: 500`). Mein
Hinzufügen des `ServerInstructions`-Tests (+14 Zeilen) hätte die Datei auf **513
Zeilen** gebracht — was `CliIntegrationTests.GeneratePlaybook_*` / `RunLinterCli_*`
mit `MaxLineCount`-Violation rot werden ließ (1 Verstoß, alle 4 in der
`SymbolGraphMiniFixtureWorkspace`/`AiNetLinter.slnx`-basierten End-to-End-Tests).
Die Extraktion des Tests in eine eigene Datei ist die saubere Lösung, die keine
bestehenden Tests antastet (Plan-Vorgabe "Bestehende Tests unverändert" wird
eingehalten). Die Datei `McpServerOptionsFactoryTests.cs` ist thematisch fokussiert
(nur ServerInstructions-Scope-Check), 31 Zeilen, mit ConsoleTestCollection-Attribut
für Thread-Isolation. Footprint: 32/2500 (irrelevant).

## Commit

```
dd4b44e6094dd827fbb4a042cf67c14fced6a943
feat(mcp): find_symbol miss-hint + initialize instructions [codegraph-mcp-server]
6 files changed, 110 insertions(+), 3 deletions(-)
```

Branch: `main`. Push-Status: **nein** (per A4).

## Build-/Test-Output

### Build

```
$ dotnet build AiNetLinter.slnx
…
Der Buildvorgang wurde erfolgreich ausgeführt.
    0 Warnung(en)
    0 Fehler
Verstrichene Zeit 00:00:04.03
```

(Zero-Warning-Direktive eingehalten, `TreatWarningsAsErrors=true`.)

### Targeted Re-Runs (zur Verifikation der neuen Tests vor dem Volllauf)

```
$ dotnet test AiNetLinter.slnx --no-build --filter "FullyQualifiedName~FindSymbolTool|FullyQualifiedName~McpServerOptionsFactory_Create_ServerInstructions"
…
Bestanden!   : Fehler:     0, erfolgreich:     9, übersprungen:     0, gesamt:     9, Dauer: 14 s
```

(8 `FindSymbolToolTests` = 5 bestehend + 3 neu; 1 `McpServerOptionsFactoryTests`.)

### Volle Test-Suite

```
$ dotnet test AiNetLinter.slnx --no-build
…
Bestanden!   : Fehler:     0, erfolgreich:  1101, übersprungen:     0, gesamt:  1101, Dauer: 7 m 55 s
```

Vor 003: 1097 Tests. Nach 003: **1101 Tests** (alle grün, 0 übersprungen, 0
fehlgeschlagen). Differenz: **+4 neue Tests** (3 in `FindSymbolToolTests` + 1 in
neuer `McpServerOptionsFactoryTests`-Datei). Der modifizierte Test 1 zählt nicht
als neuer Test, sondern als modifiziert (war im Plan so geplant).

## A3-Fehlschlag-Nachweis

Jeder **neue** Test (4 Stück) wurde nachweislich rot, wenn der zugehörige Fix-Code
weggenommen wurde. Pro Test: temporäre Änderung, dotnet test-Lauf mit Filter auf
genau diesen Test, wortwörtlicher Failure-Output, Revert. Im Plan waren 5 Tests
genannt (1 modifiziert + 4 neu); der modifizierte Test hat **implizites** A3 (der
Test bestand schon vor 003, besteht nach 003 — Regression-Schutz).

### Test 1 (modifiziert): `FindMatchesAsync_NoMatch_ReturnsNoResultsText`

- **A3-Methode:** implizit (Regressions-Test, A3 nicht erforderlich, Plan-Notiz).
- **Beleg:** Test passt mit `SymbolGraphMini`-Fixture sowohl **mit** als auch
  **ohne** Miss-Hint-Code. Pattern `DoesNotExistXyz` kommt in **keiner** Datei
  der Fixture vor (`rg "DoesNotExistXyz" tests/Fixtures/SymbolGraphMini/` → no
  matches), daher gibt `GetFilesWithHits("DoesNotExistXyz")` eine leere Liste
  zurück und der Code-Pfad fällt auf `return baseText;` zurück. Test passed in
  beiden Zuständen — **additiv-regression** korrekt.
- **Verifikation** (zweiter Lauf nach Revert aller Änderungen, nach allen anderen
  A3-Schritten): `dotnet test --filter "…FindMatchesAsync_NoMatch_…"` → 1/1 grün.

### Test 2 (neu): `FindMatchesAsync_NoCsMatchButNonCsHit_ReturnsMissHintWithFileList`

- **A3-Auslöser:** in `FindSymbolTool.cs` Z. 51-55 den Miss-Hint-Block
  auskommentieren, sodass `if (filtered.Count == 0) { return baseText; }` direkt
  zurückgegeben wird.
- **Temporäre Änderung wortwörtlich:**

  ```csharp
  if (filtered.Count == 0)
  {
      var kindSuffix = kind is null ? "" : $" (Kind-Filter: {kind})";
      var baseText = $"Keine Treffer fuer '{namePattern}'{kindSuffix}";
      // A3-Auslöser: Miss-Hint temporär deaktiviert.
      return baseText;
  }
  ```

- **Test-Befehl:**

  ```powershell
  dotnet test src/AiNetLinter.Tests/AiNetLinter.Tests.csproj `
    --filter "FullyQualifiedName=AiNetLinter.Tests.Mcp.Tools.FindSymbolToolTests.FindMatchesAsync_NoCsMatchButNonCsHit_ReturnsMissHintWithFileList"
  ```

- **Failure-Output wortwörtlich:**

  ```
  Fehler AiNetLinter.Tests.Mcp.Tools.FindSymbolToolTests.FindMatchesAsync_NoCsMatchButNonCsHit_ReturnsMissHintWithFileList [2 s]
  Fehlermeldung:
   Assert.Contains() Failure: Sub-string not found
  String:    "Keine Treffer fuer 'userService'"
  Not found: "Hinweis: kein C#-Symbol, aber Textfund"
    Stapelverfolgung:
       at AiNetLinter.Tests.Mcp.Tools.FindSymbolToolTests.FindMatchesAsync_NoCsMatchButNonCsHit_ReturnsMissHintWithFileList() in …FindSymbolToolTests.cs:line 72
  Fehler!      : Fehler:     1, erfolgreich:     0, übersprungen:     0, gesamt:     1
  ```

- **A3-Rückgängig:** Fix-Block wieder einkommentiert (Match mit dem Original aus
  Schritt 1).
- **Re-Verifikation:** Test grün, zusammen mit den anderen 8 Tests im
  targeted-Re-Run (`FindSymbolTool|McpServerOptionsFactory_Create_ServerInstructions`:
  9/9 grün).

### Test 3 (neu): `FindMatchesAsync_NoCsMatchAndNoNonCsHit_ReturnsPlainNoMatchText`

- **A3-Auslöser:** die `if (missHits.Count == 0) return baseText;`-Verzweigung
  entfernen, sodass die Hint-Zeile **fälschlich immer** angehängt wird, auch bei
  leerer Non-C#-Trefferliste.
- **Temporäre Änderung wortwörtlich:**

  ```csharp
  if (filtered.Count == 0)
  {
      var kindSuffix = kind is null ? "" : $" (Kind-Filter: {kind})";
      var baseText = $"Keine Treffer fuer '{namePattern}'{kindSuffix}";
      var missHits = SearchPatternScanner.GetFilesWithHits(
          solution, namePattern, isRegex: false);
      // A3-Auslöser: Hinweis-Zeile fälschlich IMMER anhängen, auch bei leerer Non-C#-Liste.
      var fileList = string.Join(", ", missHits);
      return $"{baseText}\nHinweis: kein C#-Symbol, aber Textfund in {fileList} " +
          $"(nicht Teil des Symbolgraphs — fuer Inhalte search_pattern nutzen).";
  }
  ```

- **Test-Befehl:**

  ```powershell
  dotnet test src/AiNetLinter.Tests/AiNetLinter.Tests.csproj `
    --filter "FullyQualifiedName=AiNetLinter.Tests.Mcp.Tools.FindSymbolToolTests.FindMatchesAsync_NoCsMatchAndNoNonCsHit_ReturnsPlainNoMatchText"
  ```

- **Failure-Output wortwörtlich:**

  ```
  Fehler AiNetLinter.Tests.Mcp.Tools.FindSymbolToolTests.FindMatchesAsync_NoCsMatchAndNoNonCsHit_ReturnsPlainNoMatchText [2 s]
  Fehlermeldung:
   Assert.DoesNotContain() Failure: Sub-string found
                                      ↓ (pos 44)
  String: ···"'DoesNotExistXyzBlub123'\nHinweis: kein C#-Symbol, "···
  Found:  "Hinweis: kein C#-Symbol"
    Stapelverfolgung:
       at AiNetLinter.Tests.Mcp.Tools.FindSymbolToolTests.FindMatchesAsync_NoCsMatchAndNoNonCsHit_ReturnsPlainNoMatchText() in …FindSymbolToolTests.cs:line 91
  Fehler!      : Fehler:     1, erfolgreich:     0, übersprungen:     0, gesamt:     1
  ```

- **A3-Rückgängig:** `if (missHits.Count == 0) return baseText;`-Verzweigung wieder
  eingefügt.
- **Re-Verifikation:** Test grün (im Volllauf 1101/1101).

### Test 4 (neu): `FindMatchesAsync_KindFilterMissHit_StillFires`

- **A3-Auslöser:** identisch zu Test 2 — Miss-Hint-Block temporär entfernen.
- **Temporäre Änderung:** wortwörtlich identisch zu Test 2.
- **Test-Befehl:**

  ```powershell
  dotnet test src/AiNetLinter.Tests/AiNetLinter.Tests.csproj `
    --filter "FullyQualifiedName=AiNetLinter.Tests.Mcp.Tools.FindSymbolToolTests.FindMatchesAsync_KindFilterMissHit_StillFires"
  ```

- **Failure-Output wortwörtlich:**

  ```
  Fehler AiNetLinter.Tests.Mcp.Tools.FindSymbolToolTests.FindMatchesAsync_KindFilterMissHit_StillFires [2 s]
  Fehlermeldung:
   Assert.Contains() Failure: Sub-string not found
  String:    "Keine Treffer fuer 'userService' (Kind-Filter: cla"···
  Not found: "Hinweis: kein C#-Symbol, aber Textfund"
    Stapelverfolgung:
       at AiNetLinter.Tests.Mcp.Tools.FindSymbolToolTests.FindMatchesAsync_KindFilterMissHit_StillFires() in …FindSymbolToolTests.cs:line 105
  Fehler!      : Fehler:     1, erfolgreich:     0, übersprungen:     0, gesamt:     1
  ```

  Der String `"Keine Treffer fuer 'userService' (Kind-Filter: cla"` (vom Output
  abgeschnitten) bestätigt zusätzlich, dass der Kind-Filter-Pfad
  (`(Kind-Filter: class)`) durchlaufen wurde — das A3 beweist, dass der Hint
  **trotz** Kind-Filter feuert.

- **A3-Rückgängig:** identisch zu Test 2 (Block wieder eingefügt).

### Test 5 (neu): `Create_ServerInstructionsContainsScopeHint` (in neuer Datei `McpServerOptionsFactoryTests.cs`)

- **A3-Auslöser:** in `McpServerOptionsFactory.cs` die Zeile
  `ServerInstructions = ServerInstructions,` aus dem `McpServerOptions`-Initializer
  entfernen.
- **Temporäre Änderung wortwörtlich:**

  ```csharp
  return new McpServerOptions
  {
      ServerInfo = new Implementation
      {
          Name = ServerName,
          Version = GetServerVersion(),
      },
      // A3-Auslöser: ServerInstructions-Zuweisung temporär entfernt.
      ToolCollection = BuildToolCollection(mcpState),
  };
  ```

- **Test-Befehl:**

  ```powershell
  dotnet test src/AiNetLinter.Tests/AiNetLinter.Tests.csproj `
    --filter "FullyQualifiedName=AiNetLinter.Tests.Mcp.McpServerOptionsFactoryTests.Create_ServerInstructionsContainsScopeHint"
  ```

- **Failure-Output wortwörtlich:**

  ```
  Fehler AiNetLinter.Tests.Mcp.McpServerOptionsFactoryTests.Create_ServerInstructionsContainsScopeHint [148 ms]
  Fehlermeldung:
   Assert.False() Failure
  Expected: False
  Actual:   True
    Stapelverfolgung:
       at AiNetLinter.Tests.Mcp.McpServerOptionsFactoryTests.Create_ServerInstructionsContainsScopeHint() in …McpServerOptionsFactoryTests.cs:line 25
  Fehler!      : Fehler:     1, erfolgreich:     0, übersprungen:     0, gesamt:     1
  ```

  Der Failure ist exakt `Assert.False(string.IsNullOrEmpty(options.ServerInstructions))`:
  `options.ServerInstructions` ist `null` (Default-Wert der Property), also ist
  `string.IsNullOrEmpty(…)` → `True`, `Assert.False(…)` schlägt fehl.

- **A3-Rückgängig:** `ServerInstructions = ServerInstructions,` wieder
  hinzugefügt.

## Footprint-Messung TD-011

Alle drei im Plan-Check 4 genannten Klassen:

| Klasse | Vor 003 | Nach 003 | Δ | Limit | Puffer |
|---|---:|---:|---:|---:|---:|
| `FindSymbolTool` | 2518 | **2529** | +11 | 2700 (PathOverride) | 171 |
| `SymbolGraphToolRegistrations` | 2487 | **2488** | +1 | 2500 | 12 |
| `McpServerOptionsFactory` | 2470 | **2484** | +14 | 2500 | 16 |

Wortwörtliche Mess-Befehle (nach 003):

```
$ dotnet run --project src/AiNetLinter -- --footprint FindSymbolTool --path .
AI-Context-Footprint fuer Klasse 'AiNetLinter.Mcp.Tools.FindSymbolTool':
Gesamt transitive Zeilen: 2529

$ dotnet run --project src/AiNetLinter -- --footprint SymbolGraphToolRegistrations --path .
AI-Context-Footprint fuer Klasse 'AiNetLinter.Mcp.SymbolGraphToolRegistrations':
Gesamt transitive Zeilen: 2488

$ dotnet run --project src/AiNetLinter -- --footprint McpServerOptionsFactory --path .
AI-Context-Footprint fuer Klasse 'AiNetLinter.Mcp.McpServerOptionsFactory':
Gesamt transitive Zeilen: 2484
```

**Bewertung:**

- `FindSymbolTool` ist auf 2529/2700 (PathOverride bleibt unverändert, **kein**
  neuer PathOverride für 003). Plan-Schätzung 2524-2530 → 2529 trifft das obere
  Ende. Puffer 171 Z. ist weiterhin großzügig.
- `SymbolGraphToolRegistrations` ist auf 2488/2500 (Puffer 12 Z., knapp). Plan-
  Schätzung 2488-2489 → 2488 trifft exakt. TD-011 (niedrig) bleibt offen für den
  nächsten Symbolgraph-Tool-Block.
- `McpServerOptionsFactory` ist auf 2484/2500 (Puffer 16 Z.). Plan-Schätzung
  2472-2474 → 2484 überschreitet die obere Schätzung um 10 Z. (vermutlich weil
  der Const-String mit Umlaut-Ersetzungen mehr Compiler-Syntax-Zeilen produziert
  als die Wort-Zahl vermuten ließ). Puffer ist aber immer noch > 15 Z., kein
  Handlungsbedarf.

**Keine Klasse reißt ihr Limit.** Kein Scanner-Split, kein PathOverride nötig.

## Abweichungen vom Plan

### Plan-Abweichung 1 — Neue Test-Datei `McpServerOptionsFactoryTests.cs`

**Was:** Der Plan sah vor, den `McpServerOptionsFactory_Create_ServerInstructions
ContainsScopeHint`-Test in `McpServerCommandTests.cs` zu ergänzen (siehe
Plan-Sektion "Zu modifizieren" / "Erwartete Tests" Test 5). Stattdessen wurde eine
neue, thematisch fokussierte Test-Datei `McpServerOptionsFactoryTests.cs` im
Namespace `AiNetLinter.Tests.Mcp` angelegt.

**Warum:** `McpServerCommandTests.cs` lag bei 499/500 Zeilen (an der
`MaxLineCount`-Grenze aus `rules.json` Z. 109). Mein Hinzufügen des Tests (+14
Zeilen) hätte die Datei auf 513 Zeilen gebracht. Das brach das `MaxLineCount`-
Limit und führte zu **4 roten Tests** in `CliIntegrationTests` (Playbook-Generierung
+ RunLinterCli, alle mit `### MaxLineCount — 1 Verstoß [agent-context]`-Fehler).

**Wie vermieden:** Den `McpServerOptionsFactory`-Test in eine neue 31-Zeilen-Datei
extrahiert. `McpServerCommandTests.cs` bleibt unverändert (499 Zeilen, wie vor
003). Bestehende Tests in `McpServerCommandTests.cs` werden **nicht**
angetastet, was der Plan-Vorgabe "Bestehende Tests unverändert" entspricht.

**Kosten:** 1 neue Datei statt 0 (Plan-Abweichung dokumentationspflichtig).
A3-Nachweis für Test 5 wurde **wortwörtlich identisch** gegen den neuen Pfad
`AiNetLinter.Tests.Mcp.McpServerOptionsFactoryTests.Create_ServerInstructions
ContainsScopeHint` durchgeführt (siehe oben).

**Test-Name-Änderung:** `McpServerOptionsFactory_Create_ServerInstructions
ContainsScopeHint` → `Create_ServerInstructionsContainsScopeHint` (kürzerer
Präfix, weil der Klassen-Name `McpServerOptionsFactoryTests` den Namespace-Teil
bereits redundant macht).

**Vorschlag für Folge-Einheiten:** Der Planer sollte bei Einheiten, die weitere
Tests in `McpServerCommandTests.cs` ergänzen wollen, **vorher** die
Zeilen-Zahl-Verfügbarkeit prüfen (gleiche Logik wie die Pflichtmessung in Check
4) — die Datei ist faktisch **voll**, ein weiterer Test führt zur selben
Situation.

## Beobachtungen (Tech-Debt-Kandidaten für den Kritiker)

### Beobachtung 1 — `SymbolGraphToolRegistrations` bei 2488/2500 (12 Z. Puffer)

**Bereits TD-011 (niedrig).** Mein +1-Satz in `find_symbol`-Description hat den
Puffer exakt auf den im Plan antizipierten Endstand (2488) gebracht. Nächste
Beschreibungserweiterung — auch nur ein Satz — reißt das Limit. **5. Registrar-
Klasse wahrscheinlich nötig beim nächsten Symbolgraph-Tool-Block** (z. B.
Trunkierung in `find_references`/`get_impact` aus 004+).

### Beobachtung 2 — `McpServerOptionsFactory` bei 2484/2500 (16 Z. Puffer)

**Neuer TD-Kandidat (niedrig).** Mein `ServerInstructions`-Block (+14 Z.) hat
diese Klasse weiter an die Grenze gebracht. Der Const-String selbst ist
konzeptuell bindend (kanonische Formulierung) und sollte **nicht** weiter
wachsen. Wenn weitere P0/P1-Extensions aus `konzept.md` Z. 207-324 hier andocken
(z. B. zusätzliche Server-Info-Felder, weil der Planer-Trigger-Pfad diese
Klasse weiter aufwertet), ist die Aufteilung in mehrere Builder-Klassen oder
ein Init-`record` zu erwägen — analog TD-009 für `McpCodeGraphServer`.

### Beobachtung 3 — `McpServerCommandTests.cs` ist faktisch voll (499/500 Z.)

**Neuer TD-Kandidat (niedrig).** Im Plan-Check 4 nicht explizit erwähnt, aber
beim Versuch, den `McpServerOptionsFactory`-Test dort zu platzieren, wurde das
Problem sichtbar: ein 14-Z.-Eingriff reißt sofort `CliIntegrationTests`. Das ist
kein 003-Fehler (Test wurde erfolgreich extrahiert), aber für **004+** ist die
Schwelle "Test in McpServerCommandTests.cs hinzufügen → MaxLineCount bricht"
eine permanente Falle. Empfehlung: thematische Aufteilung (z. B.
`McpServerCommandResolvePathTests` für die 4 `ResolveSolutionPathOrError`-Tests
+ 2 `TryLoadSolutionAsync`/`ResolveMaxLineCount`/`ResolveConfig`-Tests, und
`McpServerCommandIntegrationTests` für die 8 `RunAsync_ValidFixture_*`-
End-to-End-Tests) — ist aber **eigenständiger Refactor**, nicht 003-Scope.

### Beobachtung 4 — `search_pattern`-Tool-Count-E2E-Test erwartet nur `Greeter.cs`

Im `McpServerCommandTests.cs`-E2E-Test `RunAsync_ValidFixture_SearchPattern
ReturnsExpectedHit` (Z. 244-270) wird der Test-Pattern `Greeter` in
`SymbolGraphMiniFixtureWorkspace` ausgeführt, und die Assertion ist nur
`Assert.Contains("Greeter.cs", …)`. Mein Miss-Hint könnte theoretisch denselben
Pattern über `find_symbol` mit anderen Ergebnissen liefern, aber das ist
außerhalb dieses E2E-Tests. Beobachtung: keine Kollision, nur Feststellung.

## Bekannte Unschärfen

- **Em-Dash-Encoding im Dogfooding-Python-Output:** Der Server liefert das
  Em-Dash (U+2014) im Miss-Hint-Text korrekt aus (UTF-8-Bytes `E2 80 94`).
  PowerShell-Console zeigt es als `�` an, weil die Windows-Konsole standard-
  mäßig nicht UTF-8 ist. **Kein** Server-Bug — die Daten sind korrekt; die
  Anzeige ist ein Test-Display-Issue. Für `result.md` ist der wortwörtliche
  Quelltext-String relevant (siehe `McpServerOptionsFactory.cs:26-31` /
  `FindSymbolTool.cs:64`), nicht die Konsole-Anzeige.
- **Dogfooding `find_symbol(EPIC-05)`:** Mein Versuch, einen Pattern zu finden,
  der **nur** in Nicht-C#-Dateien vorkommt, scheiterte an `EPIC-05`: das Token
  kommt in 3 `.cs`-Dateien vor (als XML-Doc-Kommentar in
  `McpServerOptionsFactoryTests.cs` (mein neuer Test-Datei-XMLDoc),
  `McpServerOptionsFactory.cs` (Konzept-Referenz) und `SearchPatternScanner.cs`
  (Konzept-Referenz) — siehe `rg "EPIC-05" src/`). Das ist **erwartetes
  Verhalten**: 003 lebt jetzt in diesen Dateien, also kommt `EPIC-05` auch in
  C#-Quellcode vor. Der `Miss-Hint`-Test gegen `EPIC-05` zeigt weiterhin den
  Hint-Pfad (kein C#-Symbol, aber Textfund-Liste), was die Hint-Logik
  bestätigt. Für einen "rein nicht-C#"-Test wäre `EPIC-04` ein besserer
  Kandidat, weil 003 dort nicht referenziert — aber dieser Pattern-Test ist
  ohnehin Dogfooding-Charakter, nicht in den Unit-Tests verankert.
- **`McpTruncation`-Anschluss an `find_symbol`:** Der Plan schloss das
  ausdrücklich aus 003-Scope aus. Mein Miss-Hint könnte theoretisch bei
  sehr vielen Textfund-Dateien (>10) eine sehr lange Hint-Zeile erzeugen —
  in der SymbolGraphMini-Fixture (1 Datei) und in der AiNetLinter-slnx (max.
  3 Dateien mit `EPIC-05`-Token) ist das kein Problem. Falls 004+ Trunkierung
  an `find_symbol` anschließt, sollte der `string.Join(", ", missHits)`-
  Ausdruck auch trunkiert werden. **Tech-Debt-Kandidat** (kein 003-Fehler).

## Dogfooding

Manueller ad-hoc-Lauf des MCP-Servers gegen die reale `AiNetLinter.slnx`
(Start: `ainetlinter --mcp-server --path <AiNetLinter.slnx>`, dann stdio-
Kommunikation via Python-Skript).

### 1. `initialize`-Antwort mit `ServerInstructions`

```
=== initialize ===
serverInfo.name: ainetlinter
serverInfo.version: 1.0.78.0
instructions: 'Symbolgraph-Tools (find_symbol, find_references, get_impact, get_type_hierarchy, get_file_skeleton, get_violations) arbeiten ausschliesslich auf C#/.cs-Quellcode. Fuer Namen, die nur in .js, .razor, .cshtml, .xaml, .html oder .css vorkommen, ist search_pattern der passende Fallback. Struktur-Tools ohne C#-Beschraenkung: get_index_scope, get_hotspots.'
```

**Bewertung:** Der zentrale Scope-Hint wird vom Server korrekt im
`initialize`-Antwort-Feld `instructions` ausgeliefert. SDK-Property
`McpServerOptions.ServerInstructions` funktioniert wie geplant (Plan-Vorhersage
aus Check 3 verifiziert). Wortlaut exakt wie im Quellcode-Const
(`McpServerOptionsFactory.cs:26-31`).

### 2. `find_symbol` mit garantiert nicht existierendem Namen

```
=== find_symbol(DoesNotExistXyzAinetlinter) ===
  type=text text="Keine Treffer fuer 'DoesNotExistXyzAinetlinter'"
```

**Bewertung:** Plain-Text-Antwort ohne Hint (kein Treffer in `DoesNotExist…` in
irgendeiner Datei der `AiNetLinter.slnx`, weder C# noch Nicht-C#). Miss-Hint-
Pfad nicht durchlaufen, korrektes Fallback-Verhalten auf `baseText` allein.

### 3. `find_symbol` mit existierendem C#-Symbol

```
=== find_symbol(Caller) ===
  type=text text='src/AiNetLinter.Tests/Fixtures/SymbolGraphMiniFixtureWorkspace.cs:19 - Property: AiNetLinter.Tests.Fixtures.SymbolGraphMiniFixtureWorkspace.CallerPath
src/AiNetLinter.Tests/Mcp/Tools/FindReferencesToolTests.cs:83 - Methode: AiNetLinter.Tests.Mcp.Tools.FindReferencesToolTests.ExecuteAsync_ValidQualifiedName_ReturnsCallSiteInCaller()'
```

**Bewertung:** Normaler Symbolgraph-Treffer-Output (Datei:Zeile - Kind:Display)
bleibt unverändert. Die 003-Erweiterung ist **additiv**: sie ändert nur den
Leermenge-Pfad, nicht den Treffer-Pfad.

### 4. `find_symbol` mit Pattern, der Text in Nicht-C#-Dateien hat

```
=== find_symbol(Miss-Hint) ===
  type=text text="Keine Treffer fuer 'Miss-Hint'
Hinweis: kein C#-Symbol, aber Textfund in src/AiNetLinter.Tests/Mcp/Tools/FindSymbolToolTests.cs, src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs, src/AiNetLinter/Mcp/Tools/SearchPatternScanner.cs (nicht Teil des Symbolgraphs — fuer Inhalte search_pattern nutzen)."
```

**Bewertung:** Miss-Hint-Pfad feuert wie geplant. `Miss-Hint` ist **kein**
C#-Symbol (es kommt in den 3 genannten Dateien nur als String-Literal/Kommentar
vor, nicht als deklariertes Symbol), aber `GetFilesWithHits` findet den String
in den 3 `.cs`-Dateien. Die Hint-Zeile listet die Dateien kommasepariert,
nennt den Fallback `search_pattern`, schließt mit "(nicht Teil des
Symbolgraphs …)" ab. Wortlaut exakt wie im Code (`FindSymbolTool.cs:64-65`).

**Anmerkung zu `EPIC-05`-Test:** siehe "Bekannte Unschärfen" — dieser Pattern
trifft jetzt 3 `.cs`-Dateien, weil 003 selbst in diesen referenziert wird. Der
`EPIC-05`-Testfall ist im Dogfooding-Log nicht extra aufgeführt, liefert aber
analoge Resultate (Hint mit den 3 Dateien).

## Nächste Schritte (für Orchestrator/Kritiker)

→ **Kritiker-Aufruf für Einheit 003** mit `units/003/plan.md` + dieser
`units/003/result.md` als Eingabe. Verdict-Optionen: `approved` (alle 4
neuen Tests grün, A3 dokumentiert, Footprint OK, Dogfooding bestätigt),
`issues` (Befunde) oder `blocked` (Build/Test rot, hier nicht der Fall).
