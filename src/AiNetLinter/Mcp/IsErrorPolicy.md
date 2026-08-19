# isError-Policy fuer AiNetLinter MCP-Tools

**Kontext:** CodeGraphs empirisch validierte Lehre (siehe `tasks/features/01-codegraph-recon.md`
und `tasks/features/05-roadmap.md` §3 Q1): 1-2 `isError: true`-Antworten am Session-Anfang und
ein Agent gibt das betroffene Tool auf, selbst wenn die Bedingung trivial behebbar gewesen waere
(Tippfehler im Symbolnamen, mehrdeutiger Identifikator, leeres Argument). Das MCP-Protokollflag
`CallToolResult.IsError` ist das Signal, das den Agenten diese Entscheidung treffen laesst — nicht
der Text-Inhalt. Diese Policy legt fest, wann `IsError=true` gerechtfertigt ist und wann eine
erwartbare Bedingung stattdessen `IsError=false` mit einer Handlungsanleitung im Text liefert.

## Policy-Tabelle

| Bedingung | isError | Begruendung |
|---|:---:|---|
| `SOLUTION_NOT_LOADED` (Server-Start ist fehlgeschlagen, `LoadState == LoadFailed`) | **true** | Ohne resident geladene Solution kann kein Tool sinnvoll antworten — kein Argument des Aufrufers kann das beheben, das ist ein Server-/Umgebungsproblem. |
| Path-Traversal- / Sicherheits-Verweigerung | **true** | Sicherheitsrelevant — ein Agent soll diesen Zustand nicht stillschweigend als normales Ergebnis behandeln. *(Aktuell kein solcher Fall im Code: Dateipfad-Aufloesung laeuft ausschliesslich ueber `DiffImpactAnalyzer.FindDocumentByPath` gegen Dokumente, die bereits Teil der geladenen Solution sind — ein Pfad ausserhalb der Solution matcht schlicht kein Dokument und faellt unter `RESOURCE_NOT_FOUND`, siehe unten. Falls kuenftig ein Tool direkten Dateisystemzugriff ausserhalb der Solution bekommt, gehoert die Verweigerung hierher.)* |
| Echte Malfunction (unerwartete Exception im defensiven `try/catch`, `WORKSPACE_DIAGNOSTIC`/`ANALYSIS_FAILED` bei internem Fehler) | **true** | Kein durch praezisere Argumente vermeidbarer Nutzerfehler, sondern ein Grenzfall/Bug. Hint enthaelt den Retry-once-Hinweis ("Einmal erneut versuchen") — ein einmaliger erneuter Versuch klaert transiente Faelle, bevor die Datei/das Symbol manuell inspiziert werden muss. |
| `SYMBOL_NOT_FOUND` (Identifikator loest zu keinem Symbol auf) | **false** | Erwartbar (Tippfehler, falscher Scope) und direkt behebbar — Hint verweist auf `find_symbol`. |
| `AMBIGUOUS_SYMBOL` (Identifikator loest zu mehreren Symbolen auf) | **false** | Erwartbar bei kurzen/ueberladenen Namen — die mitgelieferte Kandidatenliste ist selbst die Handlungsanleitung (Identifikator praezisieren). |
| `INVALID_ARGUMENT` (leeres/fehlendes Pflichtfeld — auch bei falsch benanntem Parameter im JSON-RPC-Aufruf —, unbekannter `kind`-Filter, ungueltige Regex, gegenseitig exklusive Parameter beide gesetzt, Identifikator loest zu falschem Symbol-Kind auf) | **false** | Nutzer-/Agentenfehler bei den Argumenten, kein Tool-Ausfall — der Hint nennt die korrekte Form. Pflicht-Identifikator-/Pattern-Parameter sind dafuer auf SDK-Ebene als optional (Default `null`) deklariert (siehe `McpServerTool.Create`-Delegates in `*ToolRegistrations.cs`), damit ein fehlender/falsch benannter Parameter nicht schon an der SDK-Argument-Bindung mit einer rohen Fehlermeldung scheitert, bevor der Tool-Code die explizite `null`/leer-Pruefung ausfuehrt. |
| `RESOURCE_NOT_FOUND` (Dateipfad matcht kein Dokument in der Solution) | **false** | Pfadfehler ist erwartbar (Tippfehler, falscher Separator) — Hint verweist auf Pfad-Konvention und `find_symbol` zur Orientierung. |
| `ANALYSIS_FAILED` bei nicht aufloesender `gitRef` (`get_impact`) | **false** | Ein falscher/erfundener Git-Ref ist ein behebbarer Nutzereingabe-Fehler (Tippfehler, falscher Branch-Name) — Hint verweist auf `git log`/`git branch` oder den Aufruf ohne `gitRef`. |
| Leere Treffermenge (0 Aufrufstellen, 0 Violations, Scope-Filter matched keine Datei, 0 Symbole gefunden) | **false** | Ein vollstaendiges, definitives "nichts gefunden" ist kein Fehler — der Text sagt das explizit statt einer generischen leeren Antwort. |
| Solution wird noch im Hintergrund geladen (`McpToolResults.Loading()`) | **false** | Transienter Wartezustand, kein Fehler — der Text ist ein `[INFO]`-Hinweis, Client kann nach kurzer Pause retryn. |

## Audit-Ergebnis pro Tool (20 Tools)

Review-Basis: alle `McpToolResults.Error(...)`/`.Recoverable(...)`-Aufrufe je Tool, siehe
`src/AiNetLinter/Mcp/Tools/*.cs`.

| Tool | isError=true Faelle | isError=false Faelle (recoverable) |
|---|---|---|
| `find_symbol` | `SOLUTION_NOT_LOADED`; echte Malfunction (`WORKSPACE_DIAGNOSTIC`, catch-Block) | `INVALID_ARGUMENT` (fehlendes/leeres `namePattern`, unbekannter `kind`) |
| `find_references` | `SOLUTION_NOT_LOADED`; echte Malfunction (`WORKSPACE_DIAGNOSTIC`) | `INVALID_ARGUMENT` (fehlendes/leeres `symbolIdentifier`); `SYMBOL_NOT_FOUND`, `AMBIGUOUS_SYMBOL` (ueber `ResolveSymbolAsync`); leere Treffermenge |
| `get_impact` | `SOLUTION_NOT_LOADED` | `INVALID_ARGUMENT` (beide Parameter gesetzt); `SYMBOL_NOT_FOUND`/`AMBIGUOUS_SYMBOL` (Symbol-Branch, wiederverwendet von `find_references`); `ANALYSIS_FAILED` (unaufloesbare `gitRef`); leere Treffermenge |
| `get_type_hierarchy` | `SOLUTION_NOT_LOADED` | `INVALID_ARGUMENT` (fehlendes/leeres `symbolIdentifier`, Identifikator ist kein Typ); `SYMBOL_NOT_FOUND`/`AMBIGUOUS_SYMBOL` (wiederverwendet) |
| `get_call_tree` | `SOLUTION_NOT_LOADED`; echte Malfunction (`WORKSPACE_DIAGNOSTIC`) | `INVALID_ARGUMENT` (fehlendes/leeres `symbolIdentifier`, ungueltiger `direction`); `SYMBOL_NOT_FOUND`/`AMBIGUOUS_SYMBOL` (wiederverwendet) |
| `get_file_skeleton` | `SOLUTION_NOT_LOADED` | `INVALID_ARGUMENT` (fehlendes/leeres `filePath`); `RESOURCE_NOT_FOUND` (Pfad matcht kein Dokument) |
| `get_class_structure` | `SOLUTION_NOT_LOADED`; echte Malfunction (`WORKSPACE_DIAGNOSTIC` via `CompilationError`, catch-Block) | `INVALID_ARGUMENT` (fehlendes/leeres `symbolIdentifier`, unbekannter `sortBy`); `SYMBOL_NOT_FOUND`/`AMBIGUOUS_SYMBOL` (ueber `FindReferencesTool.ResolveSymbolAsync`, wiederverwendet) |
| `get_index_scope` | `SOLUTION_NOT_LOADED` | *(keine — Tool hat keine Argumente, daher keine erwartbare Fehlerbedingung ausser dem Solution-Zustand)* |
| `get_hotspots` | `SOLUTION_NOT_LOADED` | leere Treffermenge (Scope-Filter matched keine Datei — eigene Textmeldung, kein `[ERROR]`-Code noetig) |
| `get_violations` | `SOLUTION_NOT_LOADED`; echte Malfunction (`ANALYSIS_FAILED`, unerwartete Exception in `LinterEngine.RunAsync`) | leere Treffermenge (0 Violations); Scope-Filter matched keine Datei |
| `get_symbol_body` | `SOLUTION_NOT_LOADED`; echte Malfunction (`WORKSPACE_DIAGNOSTIC`) | `INVALID_ARGUMENT` (fehlendes/leeres `symbolIdentifier`); `SYMBOL_NOT_FOUND`/`AMBIGUOUS_SYMBOL` (wiederverwendet) |
| `search_pattern` | `SOLUTION_NOT_LOADED` | `INVALID_ARGUMENT` (fehlendes/leeres `pattern`, ungueltige Regex); leere Treffermenge (eigene "0 Treffer"-Textmeldung) |
| `metrics_tree` | `SOLUTION_NOT_LOADED` | `INVALID_ARGUMENT` (fehlendes/leeres `mode`, unbekannter `mode`, `depth`/`top_n` ausserhalb Range, ungueltiger `file_filter`) |
| `find_duplicates` | `SOLUTION_NOT_LOADED`; echte Malfunction (`WORKSPACE_DIAGNOSTIC`) | `INVALID_ARGUMENT` (fehlendes `helperSymbol` bei `mode=refactoring-drift`, ungueltiger `mode`/`similarityThreshold`, `minTokens`/`maxResults` < 1) |
| `reload_config` (Q2) | `SOLUTION_NOT_LOADED` | `CONFIG_NOT_FOUND` (Pfad existiert nicht); `CONFIG_INVALID` (ungueltiges JSON) — bisherige Config bleibt in beiden Faellen aktiv |
| `get_server_health` (Q3) | `SOLUTION_NOT_LOADED` (nur bei `LoadState == LoadFailed`) | *(keine — reine Diagnose ohne Argumente, `Loading`-Zustand wird im Report selbst als Solution-Status "wird noch geladen" angezeigt statt als Loading-Antwort)* |
| `dependency_graph` | `SOLUTION_NOT_LOADED`; echte Malfunction (`WORKSPACE_DIAGNOSTIC` via `CompilationError`, catch-Block) | `INVALID_ARGUMENT` (`filePath`/`symbolIdentifier` gegenseitig exklusiv, ungueltiger `direction`-Wert, Identifikator loest zu Nicht-Typ ohne einschliessenden Typ auf); `RESOURCE_NOT_FOUND` (`filePath` matcht kein Dokument); `SYMBOL_NOT_FOUND`/`AMBIGUOUS_SYMBOL` (ueber `FindReferencesTool.ResolveSymbolAsync`, wiederverwendet); leere Treffermenge (0 Kanten) |
| `pattern_detect` | `SOLUTION_NOT_LOADED`; echte Malfunction (`ANALYSIS_FAILED`, unerwartete Exception in der `LinterEngine`) | `INVALID_ARGUMENT` (unbekannte `patterns`-ID(s), Hint nennt gueltige Werte); leere Treffermenge (Scope-Filter matched keine Datei — Text-only ohne `StructuredContent`) |
| `safeguard` | `SOLUTION_NOT_LOADED`; echte Malfunction (`ANALYSIS_FAILED`, unerwartete Exception in der Score-Berechnung) | *(keine dedizierte `INVALID_ARGUMENT`-Bedingung — `minScore`/`maxViolations` werden geclamped statt abgelehnt; ein normaler Score-Output ist auch bei `Passed=false` kein Fehler, sondern das erwartete Quality-Gate-Ergebnis)* |
| `find_magic_values` | `SOLUTION_NOT_LOADED`; echte Malfunction (`ANALYSIS_FAILED`, unerwartete Roslyn-/Laufzeit-Exception im defensiven `try/catch`) | `INVALID_ARGUMENT` (unbekannter `valueType`, unbekannter `categoryFilter` — Hint nennt jeweils gueltige Werte); leere Treffermenge (0 Funde — Text-only ohne `StructuredContent`); Scope-Filter matched keine Datei; `minOccurrences`/`maxResults` werden geclamped statt abgelehnt |

**Vor diesem Audit abweichend von der Policy** (jetzt korrigiert):
`SYMBOL_NOT_FOUND`, `AMBIGUOUS_SYMBOL`, `INVALID_ARGUMENT` und `RESOURCE_NOT_FOUND` liefen ueber
`McpToolResults.Error(...)` und setzten damit `IsError=true`, obwohl es sich in allen Faellen um
erwartbare, durch praezisere Argumente behebbare Bedingungen handelt. Ebenso lief
`get_impact`s `ANALYSIS_FAILED` bei unaufloesbarer `gitRef` ueber `Error(...)`. Fix: neue
`McpToolResults.Recoverable(...)`-Methode (identisches Textformat, `IsError=false`), die
`SymbolNotFound`/`AmbiguousSymbol`/`InvalidArgument`/`FileNotFound` intern nutzen; die direkten
`Error(InvalidArgument, ...)`-Aufrufe in `FindSymbolTool` und `SearchPatternTool` sowie der
`Error(AnalysisFailed, ...)`-Aufruf in `GetImpactTool` wurden auf `Recoverable(...)` umgestellt.
`get_violations`s bisheriger Malfunction-Pfad lief ohne `IsError`-Flag ueberhaupt durch
`McpToolResults.Text(...)` (also faktisch `IsError=false` fuer einen echten internen Fehler) —
korrigiert auf `Error(...)` mit Retry-once-Hinweis, siehe `GetViolationsScanner.GetViolationsResult
.IsMalfunction`.

## Verwendung

- `McpToolResults.Error(...)` — nur fuer die drei `isError=true`-Kategorien oben.
- `McpToolResults.Recoverable(...)` — fuer alle anderen strukturierten `[ERROR]:`-Texte; identisches
  Format wie `Error(...)`, aber `IsError=false`.
- `McpToolResults.SolutionNotLoaded()`, `SymbolNotFound(...)`, `AmbiguousSymbol(...)`,
  `InvalidArgument(...)`, `FileNotFound(...)`, `CompilationError(...)` — vordefinierte Kurzformen,
  die die richtige Wahl bereits treffen (siehe XML-Doc auf der jeweiligen Methode in
  `McpToolResults.cs`).
