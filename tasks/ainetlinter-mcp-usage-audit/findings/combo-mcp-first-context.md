# Finding: Kombination MCP-first-Kontext

Kette live 2026-09-07, Namespace `user-AiNetLinter`, Cursor `CallDynamicTool`.  
Schema je Tool per `GetDynamicTools` (Einzelschema, nicht Katalog).  
Projekt: `targetType=project`, `targetPath=C:\Workspace\Sample.Project`.  
Anker: `DataExecutor` (Produktionstyp `Sample.Project.Infrastructure.Sql.DataExecutor`).  
Reihenfolge: `find_symbol` → `get_feature_context` → `get_symbol_body` → `get_call_tree`.  
IDs nur aus der unmittelbaren Vorgängerantwort, nicht neu geraten. Kein Build, kein Test.

## 1. Schema-Kurzfazit der Kette

Die vier Beschreibungen steuern **keinen** gemeinsamen Workflow. Jedes Tool verkauft sich als eigenständiger Einstieg; die ID-Formate in den Texten sind nicht dieselben, obwohl **eine** DocComment-ID (`T:…`) in allen vier Calls akzeptiert wurde.

| Tool | Beschreibung sagt | Was die Kette braucht |
|---|---|---|
| `find_symbol` | Namens-Substring, Ort unbekannt. Liefert strukturiertes `FindSymbolBatchDto`. | Stabile Symbol-ID für Folgetools. Erwähnt weder `get_feature_context` noch DocCommentId-Übergabe. JSON-`required` nur `targetType`/`targetPath`. |
| `get_feature_context` | **Composite One-Shot** (Deklaration, Metriken, Call-Sites, Tests, Violations). `symbolIdentifier`: `Namespace.Klasse.Methode`, `Datei.cs:Zeile` oder DocCommentId. Assembly unsupported. | Nimmt die `T:`-ID aus `find_symbol`. Liefert **keinen** Methoden-Body und **keine** `M:`-IDs. Caller-Sektion überschneidet sich mit `get_call_tree`. Agent kann den One-Shot als Ersatz für Schritte 3–4 lesen. |
| `get_symbol_body` | Body lesen; Beispiele zuerst `M:Namespace.Klasse.Methode`, `Datei.cs:Zeile:Spalte`. `maxBodyLines` Default 80. | `T:`-Typ-ID funktioniert trotzdem. Truncation-Hint (`startLine`/`maxBodyLines`) ist ehrlich. Keine Member-IDs für den Call-Tree. |
| `get_call_tree` | „Echter“ Aufrufer-/Aufgerufene-Baum. Identifier-Format **„wie find_references“**, nicht wie `find_symbol`. Default `incoming`, `depth=2`, `topN=10`. | Dieselbe `T:`-ID funktioniert. Auf einem **Typ** entsteht ein Referenzbaum (Ctor/DI), kein Methoden-Call-Graph. `direction=both` zeigte sichtbar nur Incoming. Knoten ohne `T:`/`M:`. |

**Steuert die Kette richtig?** Teilweise. Die Workflow-Rule (MCP-first: `find_symbol` → `get_feature_context` → `get_symbol_body`) ist in den Tooltexten nicht abgebildet. `get_call_tree` als vierter Schritt ist in keiner der vier Beschreibungen als Folge von `get_feature_context` vorgesehen — der Composite behauptet die Call-Sites bereits mitzuliefern.

## 2. Ausgeführte Calls

Alle vier: `targetType=project`, `targetPath=C:\Workspace\Sample.Project`. Wartezeit überall **schnell**, kein Timeout. Größen = sichtbarer Markdown-Text dieser Session (StructuredContent nicht separat sichtbar).

| # | Tool | Parameter (ID aus Vorgänger) | Größe (grob) | Truncation / completeness | Wartezeit |
|---|---|---|---|---|---|
| 1 | `find_symbol` | `pattern=^DataExecutor$`, `kind=class`, `maxResults=10` | ~4 Zeilen, ~0,5k | Vollständig: 2 Treffer, **dieselbe** ID | schnell |
| 2 | `get_feature_context` | `symbolIdentifier=T:Sample.Project.Infrastructure.Sql.DataExecutor` (aus #1, unverändert) | 5 Sektionen, ~6–8k | Caller: **10 von 248** (`maxCallers` Default). Tests 3 Dateien / 10 Methoden. Violations „0, Status: complete“ | schnell |
| 3 | `get_symbol_body` | `symbolIdentifier=T:Sample.Project.Infrastructure.Sql.DataExecutor` (aus #2 DocCommentId) | Header + 80 Zeilen Source, ~3,5–4,5k | **1–80 von 292**; Hint `startLine`/`maxBodyLines`. `bodyAvailability=available` | schnell |
| 4 | `get_call_tree` | `symbolIdentifier=T:Sample.Project.Infrastructure.Sql.DataExecutor` (aus #3 `id:`), `direction=both` | tiefer ASCII-Baum, ~10–15k | Banner: Baum trunkiert, `topN`; Wurzel `… und 99 weitere`; Kinder `… und N weitere`. Outgoing nicht sichtbar | schnell |

**ID-Übergabe (Ist):**

1. `#1` → zwei Partial-Dateien, eine ID: `` `T:Sample.Project.Infrastructure.Sql.DataExecutor` ``. Zweite Datei: `DataExecutor.OptimisticConcurrency.cs:11`.
2. `#2` akzeptiert genau diese ID. Echo: **DocCommentId** dieselbe `T:…`. Deklaration nur `DataExecutor.cs:9-297` (289 Zeilen). Type-LOC in Metriken **479** (beide Partials), Dateianzeige eine Datei.
3. `#3` akzeptiert dieselbe `T:…`. Echo `id:` unverändert. Pfad abweichend verdoppelt: `Sample.Project/Sample.Project/Infrastructure/Data/DataExecutor.cs`.
4. `#4` akzeptiert dieselbe `T:…`. Wurzel `DataExecutor — …/DataExecutor.cs:9` **ohne** Backtick-ID in den Knoten.

Es gab in der gesamten Kette **keine** `M:`-ID, kein `continuationToken`, keine Caller-DocCommentIds.

## 3. Verdict

**teilweise** — Die Typ-ID überlebt alle vier Tools (Handover auf `T:` klappt). Die Kette als MCP-first-Kontext scheitert an Promotion (kein Methodensymbol), an doppelter Caller-Last, an Truncation, die Produktionsaufrufer und den zweiten Partial unsichtbar macht, und an Beschreibungen, die den Folgeschritt nicht vorbereiten bzw. ihn als überflüssig darstellen.

## 4. Schwere

**degraded**

Nicht `broken`: mit der kopierten `T:`-ID kommt der Agent durch alle vier Calls. Formal ok, aber Token-Duplikat, Truncation ohne Offset und fehlende Member-IDs machen den vierstufigen Kontext riskant — der Agent glaubt, den Anker zu kennen, sieht aber weder den Rest des Bodys noch Produktions-Call-Sites noch einen Methoden-Graphen.

## 5. Nutzbarkeit

**nur mit Workaround**

Brauchbares Minimum aus dieser Kette: `find_symbol` (`^Name$` + `kind=class`) → `get_feature_context` **oder** `get_symbol_body`, nicht beides plus `get_call_tree` auf dem Typ.

- `get_call_tree` nach `get_feature_context` auf derselben `T:`-ID nicht zusätzlich aufrufen, solange keine `M:`-ID existiert — Incoming wiederholt die flache Caller-Liste und bläht sie transitiv mit Tests auf.
- Body: nach Truncation `startLine`/`maxBodyLines` am **selben** `T:`, nicht die Kette neu starten.
- Methoden-Call-Tree: ID gibt die Kette nicht her; Agent müsste Namen aus dem gekürzten Body raten (gegen den Auftrag dieser Probe).

## 6. Bugs und FP/FN

Live in **dieser** Kette, nicht aus anderen Finding-Dateien übernommen.

### Bugs (reproduzierbar)

1. **Keine ID-Promotion Typ → Member.** `#2` und `#3` zeigen Methoden (`QueryAsync`, `QuerySingleOrDefaultAsync`, …) nur als Text. `#4` kann daher nur den Typ-Referenzbaum bauen. Beschreibung von `get_call_tree` („echten Aufrufer-Baum“) trifft auf `T:` nicht zu.
2. **`direction=both` ohne sichtbares Outgoing.** Call `#4` lieferte ausschließlich `[incoming]`-Knoten. Entweder Outgoing leer/unterdrückt oder hinter Truncation — die Antwort sagt das nicht.
3. **Pfad-Inkonsistenz.** `#1`/`#2`/`#4`: `Sample.Project/Infrastructure/Data/DataExecutor.cs`. `#3`: Projektordner **doppelt**. Dieselbe ID, anderer Pfad — Folgetool mit Copy-Paste des Body-Pfads als `Datei.cs:Zeile` riskiert Resolve-Fehler.
4. **Partial verschwindet nach Schritt 1.** `#1` listet `DataExecutor.OptimisticConcurrency.cs`. `#2`–`#4` tun so, als gäbe es nur `DataExecutor.cs`. Metriken (`Type LOC 479`) zählen den Partial mit, die Deklarationszeile nicht.
5. **Violations intern widersprüchlich.** `#2` Sektion 2: Public Members 18/15 **VIOLATION**. Sektion 5: „0 Verstoesse, Status: complete“ auf derselben Datei.
6. **Caller-Truncation ohne Folgeschritt.** `#2`: 10/248, Hint nur `maxCallers`. `#4`: `topN` erhöhen. Kein Cursor, kein Offset. Präfix bleibt Tests.Ama (`AdminAiExplorationPlatformSupport` zuerst in `#2` und `#4`).

### False Positives / False Negatives (Stichprobe)

- **FP (Call-Tree auf Typ):** Incoming-Wurzeln sind Test-Hosts/Ctors (`AdminAiExplorationPlatformSupport` Zeile 21, `AdminAiExplorationTestHost` Zeile 21) — dasselbe Präfix wie `#2` Sektion 3. Das sind DI-/Typ-Referenzen, keine Aufrufe von `QueryAsync`.
- **FN (Produktions-Call-Sites):** In den sichtbaren 10+ Baumwurzeln keine Domain-/Platform-Produktionshandler. Liegen hinter „99 weitere“ bzw. hinter `maxCallers=10`.
- **FN (Tests):** `#2` Sektion 4 ordnet Logic-Tests per Naming zu (`DataExecutorOptimisticConcurrencyIntegrationTests`, …). Genau diese Dateien fehlen im sichtbaren `#4`-Baum (Ama-Flut). Zwei Tools, zwei Testmengen, beide unvollständig.
- **Kein FP auf den Anker selbst:** `#1` mit `^DataExecutor$` + `kind=class` trifft den Produktionstyp; ID ist korrekt (Body `#3` zeigt `public sealed partial class DataExecutor`).

## 7. Token / IDs

**Handover:** `` `T:Sample.Project.Infrastructure.Sql.DataExecutor` `` ist über alle vier Schritte stabil und copy-paste-fähig. Das ist der einzige brauchbare ID-Kanal dieser Kette.

**Doppelte Token-Last:**

- Datei + Typname in `#1`, `#2` §1, `#3` Header, `#4` Wurzel — viermal.
- Caller-Präfix Tests.Ama in `#2` §3 **und** `#4` erste Incoming-Wurzeln, `#4` expandiert dieselben Knoten noch um Kinder (Exploration-Szenarien, Plugin-Tests). Composite hat die teure Referenzsuche schon bezahlt; der Call-Tree zahlt sie plus Tiefe nochmal.
- `#2` enthält Metriken + Violations + Testnamen, die `#3`/`#4` nicht nutzen. One-Shot-Payload ist für Schritte 3–4 Ballast, wenn die Kette trotzdem weitergeht.

**Truncation zwischen Schritten (kein Weiterreichen des Rests):**

| Schritt | Sichtbar | Verborgen | Hinweis in der Antwort |
|---|---|---|---|
| `#1` | 2 Partials, 1 ID | — | keiner |
| `#2` | 10 Call-Sites, Logic-Testnamen, Datei 9–297 | 238 Referenzen; OptimisticConcurrency-Datei | `maxCallers` |
| `#3` | Zeilen 1–80 | 81–292 und zweiter Partial | `startLine`/`maxBodyLines` |
| `#4` | topN-Incoming, Tests.Ama | ≥99 Geschwister, Outgoing, Produktionswurzeln | `topN` erhöhen |

Limits erhöhen wiederholt das **bereits gesehene Präfix** (nochmals Token), statt ab Offset 11 / Zeile 81 / nächste Wurzel weiterzumachen.

**StructuredContent:** In dieser Cursor-Oberfläche nicht sichtbar. Agent arbeitet nur mit Markdown. Knoten in `#4` haben keine Backtick-IDs; `#2`-Call-Sites ebenfalls nur `Datei:Zeile` + `Type()`. Diese Locations als nächstes `symbolIdentifier` zu verwenden, **wechselt** den Anker (Caller statt `DataExecutor`).

## 8. Roslyn-Wünsche

Alles statisch/Roslyn, kein RAG:

1. **Kettenschema:** Jedes Tool nennt akzeptierte Vorgänger-IDs (`T:` aus `find_symbol` / `get_feature_context`) und den sinnvollen Folgeschritt. `get_feature_context` muss sagen, dass kein Body enthalten ist und Call-Sites **flach** sind (kein Ersatz für `get_call_tree` auf `M:`).
2. **Member-IDs im Kontext:** In `#2` und `#3` die öffentlichen Methoden als `` `M:…` `` listen (Roslyn `IMethodSymbol.GetDocumentationCommentId()`), damit `#4` ohne Namensraten auf `QueryAsync` zielen kann.
3. **Typ-Call-Tree kennzeichnen** oder ablehnen: bei `T:` entweder „reference tree (ctors/named-type)“ oder `INVALID_ARGUMENT` mit Hint „Methode wählen“. `outgoing` auf Typen: Member-Fan-out oder explizit leer mit Begründung.
4. **`scopeType` analog `search_pattern`** (`production`/`tests`/`all`) für Caller in `#2` und Incoming in `#4`; Default Produktion zuerst. Truncation darf nicht nur Tests.Ama zeigen.
5. **Partial als eine Einheit:** dieselben Dateien in allen vier Tools; oder zweite Datei mit eigener Location, weiterhin **eine** `T:`-ID.
6. **Pfad kanonisch** (kein doppeltes Projektsegment in `get_symbol_body`).
7. **Truncation mit Offset/Cursor** statt „Limit erhöhen und Präfix wiederholen“: Caller ab 11, Body ab Zeile 81, Geschwister ab topN+1.
8. **Knoten-IDs** im Call-Tree (`` `T:` `` / `` `M:` ``), damit Tiefe ohne Re-Resolve weitergeht.
9. **Violations-Sektion** an Typ-Metriken koppeln (Partial-Union); `complete` + „0 Verstöße“ nicht neben Public-Members-VIOLATION.
10. JSON-`required`: `symbolIdentifier` bzw. Musterfeld an die Laufzeit angleichen (betrifft Einstieg `#1`/`#2`, nicht die ID-Kette selbst).

## 9. Phase 3

Welle 3, nur Nicht-ok-Kettenbrüche. AiNetLinter read-only; Ansatz = statisch/Roslyn.

| Kettenbefund | Bruch | Pfad + Symbol | Roslyn-Ansatz |
|---|---|---|---|
| Keine `M:`-Promotion Typ → Member (`#2`/`#3` nur Textnamen; `#4` bleibt Typ-Referenzbaum) | ID-Handover | `src\AiNetLinter\Mcp\Tools\FeatureContext\FeatureContextFormatter.cs` `AppendDeclarationSection` / `AppendCallersSection`; `FeatureContextScanner.ExtractDeclaration`; `src\AiNetLinter\Mcp\Tools\GetSymbolBodyTool.cs` Render-Pfad | Bei `INamedTypeSymbol` öffentliche Member als `` `M:` `` aus `ISymbol.GetDocumentationCommentId()` (bzw. `DocumentationCommentId.CreateDeclarationId`) listen — gleiche Quelle wie `FindSymbolTool.FormatSymbolLocationEntries`. Ohne das kann `#4` nicht auf `QueryAsync` zielen. |
| Caller-Zeilen ohne Folge-ID (`Datei:Zeile` + `Type()`, kein `T:`/`M:`) | ID-Handover | `src\AiNetLinter\Core\DiffImpactAnalysisModels.cs` `CallSiteEntry`; `FeatureContextFormatter.AppendCallersSection`; `CallGraphTraversal.ResolveEnclosingMemberAsync` | `CallSiteEntry` um DocCommentId des **Enclosing-Members** erweitern (`GetEnclosingSymbol` + `NormalizeToOwningMember` + `CreateDeclarationId`). Markdown mit Backticks wie `find_symbol`. |
| Call-Tree-Knoten ohne Backtick-ID; `ToMetricsTreeNode` verwirft das `ISymbol` | ID-Handover | `src\AiNetLinter\Mcp\Tools\SymbolGraph\CallGraphTreeBuilder.cs` `AddChild`, `ToMetricsTreeNode`; `src\AiNetLinter\Mcp\Tools\MetricsTree\MetricsTreeRenderer.cs` `Render`; `CallGraphTraversal.FormatSymbolName` | `MetricsTreeNode` um Id-Feld; in `AddChild` `CreateDeclarationId(group.CallerSymbol)` setzen; Renderer emittiert `` `T:` `` / `` `M:` ``. Wurzel: `FormatRootDisplay` analog. |
| `direction=both` ohne sichtbares Outgoing auf `T:` | Formatter | `src\AiNetLinter\Mcp\Tools\SymbolGraph\OutgoingCallScanner.cs` `ScanAsync`; `src\AiNetLinter\Core\DuplicateDetection\MethodBodyLocator.cs` `GetBody`; `CallGraphTreeBuilder.BuildGroupsAsync` / `InterleaveDirections`; `GetCallTreeTool.TryParseDirection` | `GetBody` liefert für `TypeDeclarationSyntax` `null` → Outgoing-Liste leer, Interleave zeigt nur Incoming. Bei `INamedTypeSymbol`: entweder `INVALID_ARGUMENT` „Methode wählen“ oder Fan-out über `type.GetMembers().OfType<IMethodSymbol>()` und je Member `ScanAsync`. Leeres Outgoing im Banner benennen (`outgoing=0`, Reason). |
| Pfad in `#3` verdoppelt (`…/Platform/…/Platform/Infrastructure/…`) | Formatter | `GetSymbolBodyTool.FormatLocation` (`Path.GetFileName(OutputRoot)+"/"+ToRelative`) vs. `PathNormalizer.ToRelative` in `FeatureContextScanner.ExtractLocation` / `CallGraphTreeBuilder.FormatPath` | Eine Pfadfunktion: nur `PathNormalizer.ToRelative(solutionDir, SourceTree.FilePath)`. `FormatLocation` darf den Root-Ordnernamen nicht nochmal präfixen. |
| Partial `OptimisticConcurrency.cs` nach `#1` unsichtbar | Formatter | `FindSymbolTool.FormatSymbolLocationEntries` (iteriert **alle** `symbol.Locations`); `FeatureContextScanner.ExtractLocation` (`DeclaringSyntaxReferences.FirstOrDefault`); `GetSymbolBodyTool` erstes Source-Location | Deklaration als Union: `DeclaringSyntaxReferences` → Liste `(FilePath, Start, End)`; eine `T:`-ID. Body-Fenster pro Partial oder explizite Partial-Zeile. Metriken (`type.DeclaringSyntaxReferences.Sum`) bleiben die Union. |
| Metriken „Public Members 18/15 VIOLATION“ vs. Violations „0, complete“ | Formatter | `FeatureContextScanner.ScanAsync` (Metriken = `MetricsLookupScanner.ScanType` via `INamedTypeSymbol.GetMembers()`); Violations = `FilterViolationsForFile` → `LinterEngine` → `src\AiNetLinter\Core\Checkers\PublicMembersChecker.CountPublicMembers` (**ein** `TypeDeclarationSyntax.Members`) | Dieselbe Zählung: Partial-Union über `INamedTypeSymbol.GetMembers()` **oder** Lint explizit per Partial-Datei und Status nicht `complete`+0 neben Metrik-VIOLATION. `AppendViolationsSection` Status an `ThresholdCheckDto` koppeln. |
| Caller 10/248 ohne Offset; Präfix Tests.Ama | Truncation | `FeatureContextScanner.CollectCallersAsync` (`OrderBy` Pfad, `Take(maxCallers)`); `FeatureContextFormatter.AppendCallersSection` Hint nur `maxCallers`; `GetCallTreeTool.HasTreeOverflow` / `MetricsTreeRenderer` „topN erhöhen“ | Sortierung: Produktion (`!TestDetector.IsTestFile`) vor Tests; `.` vs `/` im Relativpfad nicht Tests-first. Offset/`continuationToken` statt Limit erhöhen (Präfix-Wiederholung). Optional `scopeType` analog `search_pattern`. |
| Body 1–80 von 292, Hint ok aber keine Member-Tabelle | Truncation | `GetSymbolBodyTool` `TruncationMarker` / `HasMoreLines`; Window `startLine`/`maxBodyLines` | Window-Hint belassen. Nach dem Typ-Body eine kompakte Member-ID-Tabelle (nicht den Rest-Body dumpfen). |
| Tooltexte steuern die Kette nicht (`get_feature_context` als One-Shot) | Formatter | `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs` `GetFeatureContextDescription`; `SymbolGraphToolRegistrations` `FindSymbolDescription` / `GetCallTreeDescription`; `SymbolBodyToolRegistrations` `GetSymbolBodyDescription` | Ein Satz Folge-ID: `T:` aus `find_symbol` → Kontext/Body; Call-Tree auf `M:`; Composite enthält keinen Body und nur flache Call-Sites. |

Gegenprobe nach Fix (gleiche `T:`-Kette, kein Raten): `#2` enthält `` `M:…DataExecutor.QueryAsync…` ``; `#4` mit dieser `M:` zeigt Produktionsaufrufer oder `productionRemaining`; `#3`-Pfad ohne doppeltes `Sample.Project/`; Partial `OptimisticConcurrency.cs` in `#2`; Metriken-VIOLATION ⇒ Sektion 5 nicht „0 complete“.
