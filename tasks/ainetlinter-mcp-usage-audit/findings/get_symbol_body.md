# Finding: `get_symbol_body`

Datum: 2026-09-07  
Tool: `user-AiNetLinter` / `get_symbol_body`  
Targets: `project` = `C:\Workspace\Sample.Project`; `assembly` = `C:\ExternalAssemblies\Version-9\Example.External.Process.dll`  
Anker: `DataExecutor.ExecuteAsync` / `QueryAsync`, `ComponentRegistrationAttribute`, `EmployeeScheduleHandler.CreateCommandExecutor`; Assembly: `Example.External.Process.Record` (+ `GetDetailsForPosition`).

## 1 Schema-Kurzfazit

Pflicht: `targetType` + `targetPath`. Identifikator über `symbolIdentifiers` (Batch) oder String-Aliase `symbolIdentifier` / `symbol` / `identifier` / `name`. Ohne Identifikator: `INVALID_ARGUMENT` (Meldung nennt nur `symbolIdentifiers`, obwohl Aliase im Schema stehen).

Body-Fenster: `maxBodyLines` (Default 80), `startLine` (Default 1, 1-basiert im Body), `endLine` (optional; Schema: `maxBodyLines = endLine - startLine + 1`).

Antwort: Symbolart, Anzeigename, Dateipfad, kanonische `id`, `bodyAvailability`, `contentMode` (`source` vs. `decompiledProject`), `Zeilen: a-b von n`, Fence mit C#-Body, Truncation-Hinweis. Assembly-Calls prefixen Session-Metadaten (`origin=decompiled`, `generation`, `status=partial`, `completeness=partial`).

## 2 Calls

| # | Ziel | Identifikator / Parameter | Ergebnis |
|---|------|---------------------------|----------|
| 1 | project | `name`/`symbolIdentifier`: `DataExecutor.ExecuteAsync` | `AMBIGUOUS_SYMBOL` — Test-Fakes + Produktions-Overloads, inkl. präziser `M:`-IDs |
| 2 | project | `name`: `ComponentRegistrationAttribute` | Hit: `T:…ComponentRegistrationAttribute`, 13/13 Zeilen, `contentMode=source` |
| 3 | project | volle `M:`-ID `DataExecutor.ExecuteAsync(DataExecutionScope,…)` | Happy Path, 25/25 |
| 4 | project | `Sample.Project/Infrastructure/Data/DataExecutor.cs:95` | derselbe Overload wie #3 |
| 5 | project | `identifier`: `DataExecutor.cs:95:9` | derselbe Overload (Datei:Zeile:Spalte) |
| 6 | project | Ambiguity-`M:` von `QueryAsync``1(…)` | Roundtrip ok, 26/26 |
| 7 | project | `T:…DataExecutor` Default-80 | Truncation 1–80 von **292** |
| 8 | project | `maxBodyLines=5` auf ExecuteAsync | 1–5 von 25 + Truncation-Hint |
| 9 | project | `startLine=8`, `endLine=15` | Fenster 8–15 von 25 |
| 10 | project | `startLine=20`, `endLine=3` | nur Zeile 20 (Fenster degeneriert) |
| 11 | project | `maxBodyLines=0` auf Attribute | 1–1 von 13, faktisch leer |
| 12 | project | `startLine=500` auf DataExecutor-Typ (292) | Hinweis „außerhalb der Methode“, Zeile 500/292 |
| 13 | project | `M:Does.Not.Exist.AtAll()` | `SYMBOL_NOT_FOUND`; `()` abgestreift |
| 14 | project | `M:…ExecuteAsync(System.String)` (ungleiche Signatur) | `AMBIGUOUS_SYMBOL` (beide echten Overloads), kein NOT_FOUND |
| 15 | project | ohne Identifikator | `INVALID_ARGUMENT` Pflicht `symbolIdentifiers` |
| 16 | project | Batch: Attribute-`T:` + ExecuteAsync-`M:`, `maxBodyLines=10` | zwei Sektionen, beide gekürzt |
| 17 | project | Batch: gültige `T:` + kaputte `M:` | erster Body + zweite Sektion `SYMBOL_NOT_FOUND` |
| 18 | project | `symbol`: `IComponentHandler` | `SYMBOL_NOT_FOUND` |
| 19 | project | `name`: `EmployeeScheduleHandler` | Typ 1–80 von 262, `[ComponentRegistration(…)]` sichtbar |
| 20 | project | `name`: `….CreateCommandExecutor` | Methode 1–15 von 48 (Limit) |
| 21 | project | volle `M:` CreateCommandExecutor | Roundtrip 48/48 |
| 22 | project | `T:`-ID des Handlers | Roundtrip wie #19 |
| 23 | project | `name=DataExecutor.ExecuteAsync` **und** `symbolIdentifier=T:ComponentRegistrationAttribute` | **nur** Attribute — `symbolIdentifier` gewinnt |
| 24 | project | `T:`-ID mit umgebenden Spaces | getrimmt, Hit |
| 25 | project | Assembly-`assembly:sha:gen:T:…Record` | `INVALID_ARGUMENT` „gehört nicht zur aktuellen Assembly-Generation“ (irreführend auf project) |
| 26 | assembly | `name`: `BusinessOperation` / Namespace / `AssemblyInfo` | `SYMBOL_NOT_FOUND`; Session-Header trotzdem da |
| 27 | assembly | Projekt-`T:ComponentRegistrationAttribute` | `SYMBOL_NOT_FOUND` |
| 28 | assembly | `name`: `Record` | `AMBIGUOUS_SYMBOL` (Typ + Properties) mit `assembly:sha256:3:T/P:…`-IDs |
| 29 | assembly | Ambiguity-`assembly:…:T:…Record` | Happy Path, **1–80 von 7145**, `decompiledProject` |
| 30 | assembly | `T:Example.External.Process.Record` | wie #29; Antwort-ID bleibt `assembly:`-präfixiert |
| 31 | assembly | `Record.cs:31` | Typ Record (nicht Zeilenfenster ab 31) |
| 32 | assembly | falscher SHA in `assembly:`-ID | `INVALID_ARGUMENT` Generation/Hash |
| 33 | assembly | `Record.Speichern` | `SYMBOL_NOT_FOUND` |
| 34 | assembly | `startLine=7120`, `maxBodyLines=20` auf Record | 7120–7139 von 7145 |
| 35 | assembly | `name`: `Record.GetDetailsForPosition` | Methode 17/17, `assembly:…:M:…` |
| 36 | assembly | Roundtrip der `M:`-Assembly-ID aus #35 | identischer 17-Zeilen-Body |

## 3 Verdict

**Tauglich / empfohlen** für MCP-first Body-Lesen, sobald eine eindeutige ID oder `Datei:Zeile[:Spalte]` vorliegt.

Kurznamen ohne Qualifikation sind im Projekt oft mehrdeutig (Test-Fakes). Assembly-Kurzname `BusinessOperation` trifft nichts; erst `Record` bzw. volle `T:`/`assembly:`-IDs. Truncation und Windowing funktionieren und sind für Typbodies (292 bzw. 7145 Zeilen) zwingend. Zurückgegebene IDs sind im **gleichen Target** stabil wiederverwendbar.

## 4 Schwere

**Mittel** für den Alltag, **niedrig** nach ID-Präzisierung.

- Blockierend ohne `find_symbol`/Ambiguity-Liste: kurze Namen.
- Assembly-Riesenklassen ohne Windowing: Token-Risiko (Default 80 begrenzt das).
- `maxBodyLines=0` und `endLine < startLine`: stilles, überraschendes Fenster statt Fehler.
- Cross-Target-IDs: Fehlertext „Assembly-Generation“ statt „falsches Target“.

Kein Datenverlust im Happy Path; kein Crash.

## 5 Nutzbarkeit

**Hoch** für bekannte `M:`/`T:`-IDs und `Datei:Zeile`. Aliase `name`/`identifier`/`symbol` greifen. Batch in einem Turn inkl. gemischter Erfolg/Fehler.

**Mittel** für Discovery: Ambiguity-Listen sind die eigentliche Suchhilfe; Hint verweist auf `find_symbol`. Assembly-Session ist `partial` — Kurzname-Misses können Indexlücken **oder** Namensmismatch sein.

Workflow: Kurzname → Ambiguity-`id` kopieren → `get_symbol_body` erneut; große Bodies mit `startLine`/`maxBodyLines` paginieren. Bei `name`+`symbolIdentifier` nur Letzteres senden.

## 6 Bugs + FP/FN (Body vs Datei)

Quelle der Body-Prüfung: nur Tool-Antworten (kein Datei-Read laut Auftrag). Abgleich intern: Methodenbody vs. Ausschnitt im Typbody; Vollständigkeitszeile vs. Fence.

**Stimmt überein**

- `ExecuteAsync(scope,…)`-Methodenbody (25 Z.) kommt im `T:DataExecutor`-Fenster ab Zeile 81 wortgleich wieder (Signatur, `MergeParameters`, `RunWithLoggingAsync`, Dapper-`ExecuteAsync`).
- `ComponentRegistrationAttribute`: 13/13, Primary-Constructor + zwei Properties, wirkt vollständig.
- `CreateCommandExecutor`: Roundtrip 48/48, schlüssiger Methodenrumpf.
- Assembly-Methode `GetDetailsForPosition`: 17/17, in sich geschlossener dekompilierter Rumpf (SQL, Parameter, Reader).

**Auffällig / möglicher FN**

- `T:DataExecutor` ist `partial class`, gemeldet **292 Zeilen einer Datei**. Weitere Partial-Dateien stecken nicht in diesem Body — Typ-`get_symbol_body` ≠ ganze Typdefinition über alle Partials.
- Pfadverdopplung im Header: `Sample.Project/Sample.Project/Infrastructure/Data/DataExecutor.cs` (Repo-Root + Projektordner). Relatives `…/Infrastructure/Data/DataExecutor.cs:95` löst trotzdem.
- Assembly-Typ `Record`: **7145** Zeilen Dekompilat, beginnt mit nested `RecordPropertiesLegacy`-Feldern; `contentMode=decompiledProject`. Das ist nicht der Original-extern-Source; FP/FN vs. IL nur als Dekompilat bewertbar. Nested Types liegen im äußeren Typbody.
- `Record.cs:31` adressiert den Typ, liefert aber Body ab Zeile 1 (Default-Fenster), nicht ab Dateizeile 31.
- Kurzname `IComponentHandler`: NOT_FOUND (Interface heißt im Repo vermutlich anders) — kein FP, aber Alias `symbol` sucht nicht „ähnlich“.
- `ExecuteAsync(System.String)` als DocId: kein NOT_FOUND, sondern Ambiguity der echten Overloads — weiche Signatur, kein exakter Miss.

**Bugs / überraschendes Verhalten**

1. `maxBodyLines=0` → 1 leere Anzeigezeile, kein `INVALID_ARGUMENT`.
2. `endLine < startLine` → eine Zeile ab `startLine`, kein Fehler (Schema-Formel würde negativ).
3. `startLine` hinter Body-Ende: Höflichkeitshinweis, aber Header `Zeilen: 500-500 von 292` ist intern widersprüchlich.
4. Konflikt `name` vs. `symbolIdentifier`: stilles Gewinnen von `symbolIdentifier`.
5. Fehlender Identifikator: Fehlertext ignoriert Aliase `name`/`symbol`/`identifier`.
6. Assembly-ID auf **project**: „nicht aktuelle Assembly-Generation“ statt Target-Mismatch.
7. Session-Header Assembly `bodyAvailability=available` auch bei SYMBOL_NOT_FOUND (Session vs. Symbol).

Keine stillen Falsch-Bodies im Happy Path beobachtet.

## 7 Token / IDs

- Default 80 Zeilen schützt; `T:Record` wäre ohne Limit ~7145 Zeilen Dekompilat — Windowing Pflicht.
- Ambiguity-Antworten sind lang (viele Test-Fakes zu `DataExecutor.*`), aber kompakter als Vollbodies.
- Truncation-Hint nennt `startLine`/`maxBodyLines` — paginierbar ohne zweites Tool.
- IDs:
  - Projekt: klassische DocIds `T:…`, `M:…(args)~Return`; Generics `QueryAsync``1` und ``0` in der ID — **Roundtrip funktioniert**, wenn die Backticks erhalten bleiben.
  - Assembly: `assembly:<SHA256>:<generation>:T|M|P:…`. `T:…` allein reicht am Assembly-Target; die Antwort normalisiert auf `assembly:`-Form.
  - Dieselbe `assembly:`-ID ist **nur** am passenden Assembly-Target + passender Generation gültig.
  - Spaces um `T:`-IDs werden getrimmt.
  - `()` an `M:Does.Not.Exist.AtAll()` wird vor der Suche entfernt.

## 8 Roslyn-Wünsche

- Pflichtfehler: „mindestens einer von `symbolIdentifiers` | `symbolIdentifier` | `symbol` | `identifier` | `name`“.
- `maxBodyLines < 1` und `endLine < startLine` als `INVALID_ARGUMENT`.
- Bei `name`+`symbolIdentifier`: explizite Priorität oder Fehler bei Widerspruch.
- Cross-Target: „ID-Target ≠ Request-Target“.
- Typbodies: Partial-Dateien zählen oder `completeness` „nur diese Syntax-Datei“.
- `Datei:Zeile` am Typ: Fenster an der Dateizeile oder klar „Typ ab Body-Start“.
- Assembly-Kurzname der DLL (`BusinessOperation`) → Hinweis auf Typnamen im Modul (`Record`), nicht nur `find_symbol`.
- Ambiguity: Produktions-Hits vor Test-Fakes oder Scope-Filter analog `search_pattern.scopeType`.

## 9 Phase 3

Nicht-ok-Befunde (Alltag `degraded`/`friction`, Wünsche `wish`; kein reines `ok`). Pfade relativ zu `C:\Daten\Entwicklung\Ralf\AiNetLinter\`.

1. **`maxBodyLines=0` und `endLine < startLine`: stilles Fenster statt Fehler** (`friction`)  
   Pfad: `src\AiNetLinter\Mcp\Tools\GetSymbolBodyTool.cs`, `src\AiNetLinter\Mcp\Tools\SourceSymbolBodyResolver.cs`  
   Symbol: `GetSymbolBodyRequest.EffectiveMaxBodyLines` / `Extract` (`Math.Max(1, maxBodyLines)`)  
   Ansatz: `EffectiveMaxBodyLines` macht negatives `endLine-startLine+1` zu 1; `Extract` clampt 0 auf 1. Vor dem Extract `INVALID_ARGUMENT`, wenn `maxBodyLines < 1` oder `EndLine < StartLine`.

2. **`startLine` hinter Body-Ende: Header `500-500 von 292`** (`friction`)  
   Pfad: `src\AiNetLinter\Mcp\Tools\SourceSymbolBodyResolver.cs`  
   Symbol: `Extract` (Zweig `normalizedStart > totalLines`)  
   Ansatz: `DisplayedStartLine`/`DisplayedEndLine` bleiben die Anfragezeile, `TotalBodyLines` die echte Länge. Entweder `INVALID_ARGUMENT` oder angezeigte Spanne auf `1..totalLines` clampen; Hint darf die Anfragezeile nennen.

3. **Konflikt `name` vs. `symbolIdentifier`: stilles Gewinnen** (`friction`)  
   Pfad: `src\AiNetLinter\Mcp\Tools\GetSymbolBodyTool.cs`  
   Symbol: `GetSymbolBodyRequest.EffectiveSymbolIdentifier` / `NormalizeIdentifiers`  
   Ansatz: Priorität `SymbolIdentifier` > `Symbol` > `Identifier` > `Name`; Array `symbolIdentifiers` schlägt den Single-Alias. Bei zwei gesetzten, inhaltlich verschiedenen Aliasen `INVALID_ARGUMENT`; sonst Priorität in der Beschreibung nennen.

4. **Fehlender Identifikator: Fehlertext nennt nur `symbolIdentifiers`** (`friction`)  
   Pfad: `src\AiNetLinter\Mcp\McpToolResults.cs`; Call: `GetSymbolBodyTool.ExecuteAsync`  
   Symbol: `SymbolIdentifiersBatchHint`  
   Ansatz: Hint um `symbolIdentifier` / `symbol` / `identifier` / `name` erweitern (wie `GetSymbolBodyDescription` in `SymbolBodyToolRegistrations.AddGetSymbolBody`).

5. **Assembly-ID auf `project`: „nicht aktuelle Assembly-Generation“** (`friction`)  
   Pfad: `src\AiNetLinter\Mcp\Tools\SymbolGraph\SymbolIdentifierResolver.cs`  
   Symbol: `TryNormalizeAssemblyId` / `StaleAssemblyId`  
   Ansatz: `assembly:`-Präfix + `expectedIdentity is null` (Projekt-Session) läuft in denselben Fehler wie Hash-/Generation-Mismatch. Getrennte Meldung: ID-Target ≠ Request-Target, bevor `Matches` geprüft wird.

6. **Assembly-Header `bodyAvailability=available` auch bei `SYMBOL_NOT_FOUND`** (`friction`)  
   Pfad: `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResponse.cs`, `AssemblyAnalysisSessionModels.cs`  
   Symbol: `FormatHeader` / `AssemblyOrigin.BodyAvailability` (Default `"available"`)  
   Ansatz: Envelope hängt Session-Metadaten **vor** jedem Tool-Ergebnis. Header-Feld als Session-Capability kennzeichnen oder bei recoverable Symbol-Fehlern weglassen; Symbol-`bodyAvailability` bleibt in `SourceSymbolBodyResolver.Resolve`.

7. **Typbody = eine Partial-Datei, nicht alle `DeclaringSyntaxReferences`** (`degraded`)  
   Pfad: `src\AiNetLinter\Mcp\Tools\SourceSymbolBodyResolver.cs`  
   Symbol: `Extract` (`DeclaringSyntaxReferences.FirstOrDefault`)  
   Ansatz: `ISymbol.DeclaringSyntaxReferences` listet alle Partials. Entweder alle Syntaxbäume konkatenieren oder `completeness` „nur diese Datei“ plus Liste der übrigen `SourceTree.FilePath`. `GetSymbolBodyTool.FormatLocation` nutzt ebenfalls nur `Locations.FirstOrDefault`.

8. **Pfadverdopplung `Sample.Project/Sample.Project/...`** (`friction`)  
   Pfad: `src\AiNetLinter\Mcp\Tools\GetSymbolBodyTool.cs`  
   Symbol: `FormatLocation` / `ToRelative`  
   Ansatz: `outputRoot = Path.GetDirectoryName(solution.FilePath)` (Solution-Ordnername = Repo-Root). `FormatLocation` hängt `Path.GetFileName(outputRoot)` **vor** den bereits relativen Pfad. Nur `PathNormalizer.ToRelative` verwenden.

9. **`Datei:Zeile` am Typ startet Body bei 1, nicht an der Dateizeile** (`friction`)  
   Pfad: `src\AiNetLinter\Mcp\Tools\GetSymbolBodyTool.cs`, `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindReferencesTool.cs`  
   Symbol: `TryResolveEnclosingMemberForBodyAsync` / `ResolveByLineAsync` / `Extract`  
   Ansatz: Zeile löst das **Symbol** (`BaseTypeDeclarationSyntax` via `TryFindEnclosingMember`); `startLine` bleibt Body-relativ Default 1. Dateizeile auf Body-Offset mappen (`lineSpan` vs. `DeclaringSyntaxReference.GetSyntax().Span`) oder im Header klar „Typ ab Body-Start, nicht Dateizeile“.

10. **Falsche Signatur `M:…ExecuteAsync(System.String)` → Ambiguity statt NOT_FOUND** (`friction`)  
    Pfad: `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindReferencesTool.cs`  
    Symbol: `ResolveByNameAsync` (`IsSymbolMatch` / `EndsWith`)  
    Ansatz: Parameterliste wird für den Namensfilter abgestreift; ohne exakten `ToDisplayString`-Match bleiben alle Overloads. Wenn die ID Parameter hat und 0 Exact-Matches: `SYMBOL_NOT_FOUND`, nicht `AMBIGUOUS_SYMBOL`.

11. **Ambiguity: Test-Fakes vor Produktion** (`wish`)  
    Pfad: `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindReferencesTool.cs`; Locations: `FindSymbolTool.FormatSymbolLocations`  
    Symbol: `ResolveByNameAsync`  
    Ansatz: Kandidaten mit `TestDetector.IsTestFile` / `IsTestProject` nachrangig; `INamedTypeSymbol` vor Membern. Gleicher Hebel wie `dependency_graph` Incoming-Ranking.

12. **Assembly-Kurzname der DLL (`BusinessOperation`) ohne Typ-Hinweis** (`wish`)  
    Pfad: `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindReferencesTool.cs`  
    Symbol: `ResolveByNameAsync` / `SymbolNotFound`  
    Ansatz: `lastSegment`-Match gegen Deklarationsnamen, nicht gegen Assembly-/Modulnamen. Bei 0 Treffern in Assembly-Session: `compilation.Assembly.GlobalNamespace.GetTypeMembers()` (gekappt, 3–5 öffentliche Typen) als Hint — rein Roslyn, kein Index-Raten.
