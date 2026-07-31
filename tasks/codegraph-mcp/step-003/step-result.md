---
status: done
type: step-result
task: codegraph-mcp
step: 003
epic: EPIC-03
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-07-31T11:15:00Z
code_commit_hash: e89ede5c68ac0da7abb543e156aff94c5971ca6f
status_after: done
blocker_category: n/a
---

# Result Step 003: Tool-Registrierungs-Infrastruktur + erstes Tool: find_symbol

## Zusammenfassung

`McpServerCommand` registriert jetzt das erste echte MCP-Tool (`find_symbol`)
ueber eine wiederverwendbare Infrastruktur (`McpServerOptionsFactory`,
`McpToolResults`), die den resident gehaltenen `McpCodeGraphServer` per
Delegate-Closure erreicht. `find_symbol` durchsucht die Solution per
Substring (case-insensitive) auf Symbolnamen mit optionalem Kind-Filter
(`class`/`interface`/`method`/`property`) und liefert Datei:Zeile/Kind/
Signatur pro Fundstelle oder eine `[ERROR]`-Antwort, wenn keine Solution
geladen ist.

## Geänderte Dateien

- `src/AiNetLinter/Output/LinterErrorCodes.cs` — Konstante `SolutionNotLoaded` ergaenzt.
- `src/AiNetLinter/Mcp/McpToolResults.cs` (neu) — `Error`/`SolutionNotLoaded`/`Text`-Helper fuer `CallToolResult`.
- `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` (neu) — `ExecuteAsync` (Tool-Einstiegspunkt) + `FindMatchesAsync` (reine Such-/Formatierungslogik via `SymbolFinder.FindSourceDeclarationsAsync`).
- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` (neu, nicht im Plan vorgesehen) — baut `McpServerOptions` inkl. Tool-Collection; siehe "Abweichungen vom Plan".
- `src/AiNetLinter/Commands/McpServerCommand.cs` — `RunAsync` ruft jetzt `McpServerOptionsFactory.Create(mcpState)` statt der bisherigen leeren `CreateServerOptions()`; die bisherigen privaten Methoden `CreateServerOptions`/`GetServerVersion` wurden in die Factory verschoben (siehe "Abweichungen vom Plan").
- `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` — `RunAsync_ValidFixture_ServerRespondsWithEmptyToolList` umbenannt/angepasst zu `..._ServerRespondsWithFindSymbolTool` (`Assert.Single` + Namenscheck); neuer E2E-Test `RunAsync_ValidFixture_FindSymbolReturnsMatch` ueber echten `StdioClientTransport`/`CallToolAsync`.
- `src/AiNetLinter.Tests/Mcp/Tools/FindSymbolToolTests.cs` (neu) — 4 Unit-Tests gegen `FindSymbolTool.FindMatchesAsync` (Substring-Treffer, Kind-Filter, kein Treffer, Case-Insensitivity), `[Collection("ConsoleTestCollection")]` annotiert (TD-003-Vorsicht laut Plan).

## Commit

- **Code-Commit-Hash:** `e89ede5c68ac0da7abb543e156aff94c5971ca6f`
- **Message:**
  ```
  feat(mcp): add tool registration infra and find_symbol tool [codegraph-mcp]

  Wire McpServerCommand's tool collection to the resident McpCodeGraphServer
  via closure-based registration (no DI container) and add the first
  EPIC-03 symbol-graph tool, find_symbol, which substring-searches
  declarations across the loaded Solution and returns file:line/kind/
  signature per match, or a structured [ERROR] when no Solution is loaded.

  Refs: tasks/codegraph-mcp/step-003
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx → grün, 0 Warnungen
dotnet test AiNetLinter.slnx  → grün (1032 Tests, 0 Fehler)
```

Kein `RegisterMSBuild`-Flake (TD-003) beobachtet, kein Wiederholungslauf noetig.

## Abweichungen vom Plan

- **`McpServerOptionsFactory.cs` (neue Datei, nicht im Plan):** Der Plan
  sah `CreateServerOptions(McpCodeGraphServer mcpState)` und
  `BuildToolCollection(McpCodeGraphServer mcpState)` als private Methoden
  **innerhalb** `McpServerCommand.cs` vor. Wortgetreu umgesetzt, schlug
  danach `dotnet test` rot: `CliIntegrationTests.RunLinterCli_OnWholeSolution_ReturnsSuccess`
  (Selbst-Lint des Repos, laeuft `ainetlinter --config rules.json --path .`)
  meldete eine neue `AIContextFootprint`-Verletzung —
  `McpServerCommand (2553 > 2500)`. Ursache: `AIContextFootprintCalculator`
  traversiert die Signaturen (Parameter-/Returntypen) **aller** Member
  einer Klasse; sobald `McpCodeGraphServer` als Parametertyp eines
  `McpServerCommand`-Members auftaucht, wird dessen komplette transitive
  Typ-Kette (inkl. `FindSymbolTool`, `SourceFileCatalog`, ...) mitgezaehlt
  — vorher war `mcpState` nur eine lokale Variable in `RunAsync` und blieb
  fuer die Metrik unsichtbar (die Berechnung schaut nicht in Methodenkoerper).
  Verifiziert per Vergleich mit `git stash` (Baseline ohne Aenderungen:
  `OK`, 0 Violations). Fix: `CreateServerOptions`/`BuildToolCollection`/
  `GetServerVersion` in eine neue, dedizierte Klasse
  `Mcp/McpServerOptionsFactory.cs` verschoben — `McpServerCommand.RunAsync`
  ruft sie nur noch aus dem Methodenkoerper auf (`McpServerOptionsFactory.Create(mcpState)`),
  wodurch `McpCodeGraphServer` kein Signaturtyp eines `McpServerCommand`-Members
  mehr ist. Verhalten/Wiring identisch zum Plan, nur der Ort der Methoden
  geaendert. Nach dem Fix: Selbst-Lint wieder `OK`, alle 1032 Tests gruen.
  Dies ist eine Code-Fix-Massnahme im Rahmen der Vorab-Klassifikation
  (Schritt 4a, echter Code-Defekt durch die im Plan vorgegebene Signatur),
  kein Scope-Zuwachs — keine neuen Features, kein DI, keine Architektur-
  entscheidung, nur Verschiebung bestehender privater Methoden in eine
  neue Datei.
- **Delegate-Parameter mit Default-Werten:** Der Plan nannte fuer den
  registrierten Delegate `(string namePattern, string? kind, CancellationToken ct)`
  ohne Defaultwerte. In der Praxis fuehrte das dazu, dass der MCP-SDK-
  Client bei einem Aufruf ohne `kind`-Argument serverseitig eine generische
  Exception warf ("An error occurred invoking 'find_symbol'.") — das SDK
  generiert aus Parametern ohne Default ein "required"-JSON-Schema-Feld.
  Fix: `(string namePattern, string? kind = null, CancellationToken ct = default)`
  — macht `kind` im generierten Schema optional, exakt wie fachlich
  beabsichtigt ("optionaler Kind-Filter"). Verifiziert per E2E-Test
  `RunAsync_ValidFixture_FindSymbolReturnsMatch` (ruft `find_symbol` nur
  mit `namePattern` auf).

## Beobachtungen

- Die `AIContextFootprint`-Grenze (2500 Zeilen) liegt fuer den
  `Mcp`/`Cli`-Bereich offenbar knapp am Limit — schon eine kleine
  Signaturaenderung an `McpServerCommand` (ein zusaetzlicher Parameter
  vom Typ `McpCodeGraphServer`) reichte, um sie zu ueberschreiten. Fuer
  die 4 verbleibenden EPIC-03-Tools (`find_references`, `get_impact`,
  `get_type_hierarchy`, `get_file_skeleton`) lohnt sich ein bewusster
  Blick darauf, in welcher Klasse ihre Registrierung/Wiring landet — die
  jetzt eingefuehrte `McpServerOptionsFactory` als zentraler Sammelpunkt
  fuer alle `McpServerTool.Create(...)`-Aufrufe duerfte selbst irgendwann
  ebenfalls in die Naehe des Limits kommen, sobald 5 Tools + `FindSymbolTool`-
  artige Abhaengigkeiten dort zusammenlaufen. Kein Tech-Debt-Eintrag von
  mir angelegt (bleibt Aufgabe des Kritikers) — nur als Vorwarnung fuer
  die Planung der Folge-Steps.
- `McpServerToolCreateOptions` hat kein explizites Feld fuer
  "erforderlich vs. optional" pro Parameter — Optionalitaet wird laut
  Beobachtung ausschliesslich ueber C#-Default-Werte in der
  Delegate-Signatur gesteuert. Fuer alle folgenden Tools mit optionalen
  Parametern (`get_impact` etc., falls sie optionale Filter bekommen)
  relevant.

## Bekannte Unschärfen

- Die Reflection-gestuetzte API-Verifikation aus dem Step-Plan
  (`McpServerTool.Create`, `McpServerToolCreateOptions`, `CallToolResult`,
  `TextContentBlock`, `McpServerOptions.ToolCollection`,
  `McpClientTool.Name`, `McpClient.CallToolAsync`) wurde vor der
  Implementierung erneut per PowerShell-Reflection gegen die tatsaechlich
  vorhandenen DLLs (`ModelContextProtocol.Core.dll` v2.0.0) gegengeprueft
  — alle im Plan dokumentierten Signaturen stimmten exakt, bis auf den
  oben beschriebenen Default-Werte-Sonderfall, der sich erst zur Laufzeit
  (nicht per Reflection der Methodensignatur) zeigte.
- `SymbolFinder.FindSourceDeclarationsAsync`-Signatur konnte nicht per
  Reflection direkt gegen die im `bin`-Verzeichnis vorhandene
  `Microsoft.CodeAnalysis.Workspaces.dll` verifiziert werden (Assembly-
  Versionskonflikt mit der im PowerShell-Prozess bereits geladenen
  Roslyn-Version) — die Methode ist jedoch eine stabile, seit Jahren
  unveraenderte oeffentliche Roslyn-API; der Build/die Tests bestaetigen
  die verwendete Signatur indirekt (kompiliert, alle Testfaelle gruen).
