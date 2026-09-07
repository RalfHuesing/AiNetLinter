# Finding: `find_implementations`

Audit-Stand: 2026-09-07. Nur Schema-Lookup (`GetDynamicTools`, `toolName=find_implementations`) plus Live-Calls dieses Tools. Kein anderes MCP-Tool, kein Build, kein Test.

## 1 Schema-Kurzfazit

Beschreibung steuert den Happy Path für **Typen** gut: konkrete Implementierungen/Overrides von Interfaces, abstrakten Klassen, virtuellen Methoden oder Properties; `targetType=project|assembly`; `symbolIdentifier` (Alias `symbol`) im Format wie `find_references` (`"M:Namespace.Klasse.Methode"`, `"IInterface"`, `"BaseClass.Method"`); `maxResults` Default 50.

**Irreführend vs. Live-Verhalten:**

- Die Beispiele `"IProcessor.Execute"` / `"BaseClass.Run"` suggerieren, **Methoden-Overrides** seien der Normalfall. Live scheitern Kurzform, FQN und `M:`-ID für Metadaten-Basismethoden fast immer (`SYMBOL_NOT_FOUND` oder `AMBIGUOUS_SYMBOL` auf den Overrides statt auf dem virtuellen Member).
- Schema listet **vier** Symbol-Aliase (`symbolIdentifier`, `symbol`, `identifier`, `name`) — Beschreibung nennt nur die ersten zwei. Pflicht laut JSON: nur `targetType` + `targetPath`; das Symbol ist Schema-seitig optional, serverseitig aber Pflicht (`INVALID_ARGUMENT`).
- `targetType` ist im JSON-Schema ein freier `string`, kein Enum.
- Kein `scopeType` / `origin`-Filter, obwohl Antworten standardmäßig **hunderte `[extern/metadata]`-Treffer** aus Referenzen liefern.
- Kein `cursor` / `continuationToken`; Truncation sagt nur „`maxResults` erhöhen“.
- Trefferliste enthält **keine** Symbol-IDs (`T:`/`M:`), obwohl `AMBIGUOUS_SYMBOL` genau solche IDs ausgibt.

Input-Schema:

| Parameter | Typ | Pflicht | Default | Bemerkung |
|---|---|---|---|---|
| `targetType` | string | ja | — | Beschreibung: `project` oder `assembly`. |
| `targetPath` | string | ja | — | Absoluter Projektroot bzw. `.dll`/`.exe`. |
| `symbolIdentifier` | string \| null | de facto ja | null | Primärschlüssel; leer → `INVALID_ARGUMENT`. |
| `symbol` | string \| null | nein | null | Funktioniert; bei Konflikt verliert er gegen `symbolIdentifier`. |
| `identifier` | string \| null | nein | null | Undokumentiert, funktioniert wie `symbolIdentifier`. |
| `name` | string \| null | nein | null | Undokumentiert, funktioniert wie `symbolIdentifier`. |
| `maxResults` | integer | nein | 50 | `0` und `-1` werden nicht abgewiesen, klemmen auf 1 Treffer. |

Assembly ist im Schema **erlaubt** und live nutzbar.

## 2 Calls

Projektroot: `C:\Workspace\Sample.Project`.  
Assembly: `C:\ExternalAssemblies\Version-9\Example.External.Core.dll`.  
Wartezeit: alle Calls **schnell** (kein Timeout).

| # | Absicht | Argumente | Ergebnis | Größe / Truncation |
|---|---|---|---|---|
| 1 | Anker Interface-Kurzname | `project`, `symbolIdentifier=IComponentHandler` | `[ERROR]: SYMBOL_NOT_FOUND` + Hint `find_symbol` | ~3 Zeilen |
| 2 | Anker `ComponentRegistration` | `project`, `symbolIdentifier=ComponentRegistration` | `SYMBOL_NOT_FOUND` | ~3 Zeilen |
| 3–5 | Aliase auf totem Anker | `symbol` / `name` / `identifier` = `IComponentHandler` | identisch `SYMBOL_NOT_FOUND` | ~3 Zeilen |
| 6 | Truncation auf totem Anker | wie 1, `maxResults=2` | `SYMBOL_NOT_FOUND` (Truncation greift nicht) | — |
| 7 | FQN Interface | `Sample.Project.Contracts.IComponentHandler` | `SYMBOL_NOT_FOUND` | — |
| 8 | `T:`-Dokumentations-ID | `T:Sample.Project.Contracts.IComponentHandler` | `SYMBOL_NOT_FOUND` | — |
| 9 | Happy Path BCL-Interface | `IDisposable` | Auflösung zu `System.IDisposable` (**namedtype**), **996** Treffer, fast alles `[extern/metadata]` | Default **50 von 996** trunkiert |
| 10 | Happy Path Framework-Interface | `IHostedService` | `Microsoft.Extensions.Hosting.IHostedService`, **8** Treffer (1× abstract metadata + 7 Platform-Klassen, Datei:Zeile:Spalte) | vollständig, Completeness-Hinweis |
| 11 | Leer: Symbol fehlt | nur `targetType`+`targetPath` | `[ERROR]: INVALID_ARGUMENT: Pflichtparameter 'symbolIdentifier' (oder 'symbol') fehlt oder ist leer.` Hint mit `"IProcessor.Execute"` | ~3 Zeilen |
| 12 | Leer: unbekannter Name | `DieserTypExistiertGanzSicherNichtXYZ123` | `SYMBOL_NOT_FOUND` | ~3 Zeilen |
| 13 | Realer Anker abstrakte Klasse | `ComponentHandler` | `Sample.Project.Contracts.ComponentHandler`, **97** Implementierungen (Produktion, Domain, Tests) | Default **50 von 97** trunkiert |
| 14 | Abstrakte Basisklasse | `Microsoft.Extensions.Hosting.BackgroundService` | **5** konkrete Platform-Services (ohne `ComponentRegistryHostedService` / `AppSettingsUnknownKeyScanHostedService`, die nur `IHostedService` sind) | vollständig |
| 15 | Alias + Truncation | `symbol=IHostedService`, `maxResults=2` | 8 gefunden, **2 von 8** gezeigt | Truncation-Zeile |
| 16–17 | Undokumentierte Aliase | `identifier` / `name` = `IHostedService` | identisch zu Call 10, vollständig | wie 10 |
| 18 | Starke Truncation | `IDisposable`, `maxResults=1` | 996 gefunden, **1 von 996** | Truncation-Zeile |
| 19 | Truncation aufheben | FQN `ComponentHandler`, `maxResults=200` | alle **97**, Completeness-Hinweis | ~290 Zeilen, vollständig |
| 20 | Methode laut Beschreibung | `IHostedService.StartAsync` | `SYMBOL_NOT_FOUND` | — |
| 21 | Methode Kurzform | `BackgroundService.ExecuteAsync` | `[ERROR]: AMBIGUOUS_SYMBOL` — trifft **Overrides** (4× `M:…ExecuteAsync…`), nicht das virtuelle Basismember | IDs in der Fehlermeldung |
| 22 | Leerer String | `symbolIdentifier=""` | `INVALID_ARGUMENT` wie Call 11 | — |
| 23 | Ungültiger Projektpfad | `targetPath=C:\DoesNotExist\NoProjectHere` | **nicht vom MCP beantwortet**: Auto-review hat den Call blockiert | — |
| 24 | Assembly Happy Path | `assembly`, externe Assembly, `IDisposable` | Header `[ASSEMBLY] origin=decompiled; status=partial; completeness=partial`, **379** Treffer, BCL/Newtonsoft/`externde.Core.*`, `[extern/metadata]` | **50 von 379** trunkiert |
| 25 | Falscher `targetType` | `assembly` + Projektverzeichnis | `INVALID_ARGUMENT`: Pfad muss existierende `.dll`/`.exe` sein | ~3 Zeilen |
| 26 | `maxResults=0` | `IHostedService`, `maxResults=0` | 8 gefunden, **1 von 8** gezeigt (klemmt auf 1, kein Fehler) | Truncation |
| 27 | Methode FQN (Metadata) | `Microsoft.Extensions.Hosting.BackgroundService.ExecuteAsync` | `SYMBOL_NOT_FOUND` | — |
| 28 | Methode `M:`-ID (Metadata) | `M:Microsoft.Extensions.Hosting.BackgroundService.ExecuteAsync(System.Threading.CancellationToken)~System.Threading.Tasks.Task` | `SYMBOL_NOT_FOUND` | — |
| 29 | Konkrete Klasse (Ableitungen) | `SchedulerComponentHandler` | **39** Subklassen, vollständig | ~120 Zeilen |
| 30 | Leer-Erfolg (0 Treffer) | `ComponentRegistrationAttribute` | `Gefunden: 0` + „Keine konkreten Implementierungen…“ + Completeness | ~6 Zeilen; **kein** Error |
| 31 | `maxResults=-1` | `IHostedService` | wie Call 26: 1 von 8 | Truncation |
| 32 | Alias-Konflikt | `symbolIdentifier=IHostedService` **und** `symbol=IDisposable` | **IHostedService** gewinnt | vollständig 8 |
| 33 | Assembly Truncation | externe Assembly, `IDisposable`, `maxResults=5` | 379 gefunden, **5 von 379**; Header `generation=7` | klein |
| 34 | Assembly unbekannter Typ | externe Assembly, `Sample.ExternalSuite` | Assembly-Header **dann** `SYMBOL_NOT_FOUND` | — |
| 35 | Methode Interface-Member | `IDisposable.Dispose` | `SYMBOL_NOT_FOUND` | — |
| 36 | Methode Projekt-Typ | `ComponentHandler.GetDataAsync` | `SYMBOL_NOT_FOUND` | — |
| 37 | `M:`-ID einer Override-Methode | ID aus Call 21 (`ActiveUsersLoggingBackgroundService.ExecuteAsync`) | **0** weitere Overrides, Completeness (korrekt: sealed/keine Ableitung) | ~6 Zeilen |
| 38 | Massenanker | `ILogger` | `Microsoft.Extensions.Logging.ILogger`, **32** (3× metadata + 29 Test-Logger) | vollständig |
| 39 | Assembly ohne Symbol | nur `assembly`+DLL | Header, dann `INVALID_ARGUMENT` fehlendes Symbol | — |
| 40 | Token-Stress | `IDisposable`, `maxResults=1000` | 996 Treffer **vollständig** (~1997 Zeilen, ~96 KB) | Completeness; keine harte Obergrenze sichtbar |
| 41 | Generisches Interface | `IEquatable` | auflöst zu `System.IEquatable<T>`, **492**, ValueTuple/CSS-Typen metadata | **50 von 492** |
| 42 | Case-Folding | `icomponenthandler` | `SYMBOL_NOT_FOUND` (kein Case-Fold auf den fehlenden Interface-Namen) | — |

Stichprobe Ist vs. Antwort (ohne `rg`/zweites MCP-Tool, nur aus der Tool-Antwort und bekanntem Platform-Wissen):

- `IHostedService` listet u. a. `ActiveUsersLoggingBackgroundService`, `TokenCleanupBackgroundService`, `ComponentRegistryHostedService`, `ScheduleAllocationRecalcBackgroundService`, `ScheduledJobsBackgroundService`, `NavigationHistoryCleanupService`, `AppSettingsUnknownKeyScanHostedService` — plausibel.
- `BackgroundService` ist echte Teilmenge (5), ohne die reinen `IHostedService`-Impls — korrekt.
- `ComponentHandler` enthält Domain-Handler (`CompanyCalendarHandler`, `EmployeeScheduleHandler`, …) und viele Test-Stubs — erwartbar, aber **ohne Produktionsfilter**.

## 3 Verdict

**Teilweise wie beschrieben.** Typ-Suche (Interface / abstrakte / konkrete Basisklasse) mit eindeutigem Kurznamen ist der tragfähige Pfad: Auflösung auf FQN, Kind `(namedtype)`, Status `concrete`/`abstract`, Quellposition oder `[extern/metadata]`, Truncation mit Zähler, 0-Treffer als Erfolg.

**Abweichend** ist der in Schema/Hint beworbene **Methoden-Pfad**. `BaseClass.Method` findet das virtuelle Basismember aus Metadaten nicht; die Kurzform trifft Overrides und wird `AMBIGUOUS_SYMBOL`. `M:`-IDs funktionieren nur, wenn sie bereits auf ein **Quell**-Member zeigen (Call 37), nicht auf das deklarierende Metadata-Member (Call 28).

Zusätzlich: Default-Scope mischt Platform-Code mit der gesamten Referenzhülle. Für `IDisposable`/`IEquatable` ist das Ergebnis formal richtig, für Agentenarbeit an *diesem* Repo aber unbrauchbar, solange `maxResults` nicht explizit und kein Source-Filter existiert.

## 4 Schwere

**`degraded`** (Gesamt).

| Teil | Schwere | Warum |
|---|---|---|
| Typ-Happy-Path (`ComponentHandler`, `IHostedService`, `SchedulerComponentHandler`) | `ok` | Stabile, vollständige Listen mit Pfaden. |
| Methoden-Overrides laut Beschreibung | `broken` | Dokumentierter Call-Shape findet das Basismember nicht. |
| BCL-/NuGet-Interfaces ohne Origin-Filter | `degraded` | 996/492/379 Treffer, Default 50 alles Metadata. |
| `maxResults=0`/`-1` klemmt auf 1 | `friction` | Kein Fehler, falsche Erwartung. |
| Vier Aliase, nur zwei beschrieben; `symbolIdentifier` schlägt `symbol` | `friction` | Funktioniert, aber unnötig. |
| Fehlende Symbol-IDs in der Trefferliste | `degraded` | Folge-Calls müssen FQN raten; nur der Ambiguitätsfehler liefert `M:`-IDs. |
| Anker `IComponentHandler` | kein Tool-Bug | Im Index existiert die **Klasse** `ComponentHandler`, kein Interface dieses Namens. Agenten, die Doku-Literale übernehmen, landen auf `SYMBOL_NOT_FOUND`. |
| Ungültiger Projektpfad | nicht bewertet | Call 23 vom Host-Auto-review blockiert, MCP-Fehlerform unbekannt. |

## 5 Nutzbarkeit

**Nur mit Workaround.**

Wieder aufrufen: **ja**, wenn der Typname eindeutig ist (`ComponentHandler`, `IHostedService`, eine konkrete Basisklasse) und `maxResults` zur erwarteten Trefferzahl passt.

Nicht als Erstaufruf verwenden für:

- Doku-Namen ohne Prüfung (`IComponentHandler`, `ComponentRegistration`) — erst eindeutigen Typ haben.
- Virtuelle Methoden (`*.ExecuteAsync`, `IDisposable.Dispose`) — Beschreibung lügt; Workaround: Implementierungen des **Typs** holen, Bodies woanders lesen.
- Weite BCL-Interfaces (`IDisposable`, `IEquatable`) ohne Filter — Token-Falle.
- „Wer trägt `[ComponentRegistration]`?“ — 0 Implementierungen von `ComponentRegistrationAttribute` ist korrekt (Attribut ≠ Interface), aber der falsche Tool-Wahl.

Workaround-Reihenfolge, die live funktioniert: Kurznamen der abstrakten/Interface-Typs → bei Truncation `maxResults` auf die gemeldete Gesamtzahl → bei `AMBIGUOUS_SYMBOL` eine `M:`-ID aus dem Fehler übernehmen (nur für weitere Overrides *dieser* Methode).

## 6 Bugs+FP/FN

| Befund | Art | Call | Bewertung |
|---|---|---|---|
| Metadata-Basismethode nicht auflösbar (`BackgroundService.ExecuteAsync` FQN/`M:`, `IHostedService.StartAsync`, `IDisposable.Dispose`) | FN vs. Beschreibung | 20, 27, 28, 35 | Kernvertrag „virtuelle Methoden“ ist für Framework-Member tot. |
| Kurzform `BackgroundService.ExecuteAsync` disambiguiert auf **Overrides**, nicht auf `protected override`-Deklaration der Basis | FN + irreführender Fehler | 21 | Die ausgegebenen `M:`-IDs sind nützlich, beantworten aber nicht „alle Overrides von X“. |
| `IDisposable`/`IEquatable` listen die Referenzhülle (AngleSharp, Azure, Bunit, xUnit, Tuples, …) | Scope-FP aus Agentensicht | 9, 40, 41 | Roslyn-korrekt als Implementierer, falsch als Antwort auf „unsere Handler“. Kein `production`/`sourceOnly`. |
| Assembly-`IDisposable`: `SafeProcessHandle` **zweimal** hintereinander | FP / Duplikat | 24 | Identische Zeile doppelt. |
| `maxResults=0` und `-1` → 1 Treffer + Truncation | Bug | 26, 31 | Sollte 0 Treffer oder `INVALID_ARGUMENT` sein. |
| Default 50 ohne Source-Filter bei 996 Treffern | Token-FP | 9 | Erste Seite beginnt bei `AngleSharp.*`, Platform-Typen sind unsichtbar. |
| Keine Symbol-IDs in Erfolgslisten | Folge-Call-Lücke | 10, 13, 19 | Agent kopiert FQN; `M:` nur im Ambiguitätsfehler. |
| `ComponentHandler.GetDataAsync` | FN oder falscher Membername | 36 | Entweder Member heißt anders oder Typ.Methode-Lookup für Source-Typen ebenfalls kaputt. Ohne `find_symbol` nicht entscheidbar. |
| `IComponentHandler` / `ComponentRegistration` | FN nur vs. Doku-Anker | 1–8 | Index kennt `ComponentHandler` (Klasse) und `ComponentRegistrationAttribute` (0 Impls). Kein Case-Fold-Rettungsanker. |
| Test-Handler dominieren die 97 `ComponentHandler`-Treffer | kein FP, fehlender Filter | 13, 19 | Produktion (~15 sichtbare Domain/Platform-Handler) geht in der Stub-Menge unter. |
| Assembly-Antwort `completeness=partial` bei 379 gezählten Treffern | Unklare Vollständigkeit | 24 | Header sagt `partial`, Truncation zählt 379 als gefunden. Agent weiß nicht, ob 379 die Untergrenze ist. |
| Assembly-Fehlerpfade prefixen trotzdem den vollen `[ASSEMBLY]`-Header | Rauschen | 34, 39 | Token für einen Fehler. |

Kleine Ground-Truth-Stichprobe: Call 10 vs. Call 14 — die Differenz (`ComponentRegistryHostedService`, `AppSettingsUnknownKeyScanHostedService` nur in der Interface-Liste) entspricht der Erwartung (IHostedService direkt vs. BackgroundService). Call 30 (`ComponentRegistrationAttribute` → 0) ist fachlich richtig: Attribute werden nicht „implementiert“. Call 37 (0 Overrides einer konkreten `ExecuteAsync`) ist plausibel.

## 7 Token/IDs

- **Default 50** begrenzt die Liste, nicht die Zählung. Die Truncation-Zeile (`50 von 996; maxResults erhöhen`) ist klar, aber ohne `continuationToken` muss der Agent die **gesamte** Menge nachladen (Call 40: ~96 KB / ~1997 Zeilen für 996 `IDisposable`).
- Erfolgsliste: FQN, `(class|struct)`, `[concrete|abstract]`, relative Datei **oder** `[extern/metadata]`. **Keine** `T:`/`M:`-IDs, kein `symbolIdentifier` zum Durchreichen.
- `AMBIGUOUS_SYMBOL` ist die einzige Quelle brauchbarer `M:`-IDs; Call 37 zeigt, dass diese IDs als Folge-Call funktionieren.
- Completeness-Hinweis bei vollständigen Antworten („kein zusätzliches Read/Grep“) — nützlich, widerspricht aber dem Assembly-Header `completeness=partial`.
- StructuredContent war in der Agent-Oberfläche nicht sichtbar; nur Fließtext.
- Alias-Konflikt (Call 32): stilles Winner-`symbolIdentifier` — kein Warnhinweis, dass `symbol` ignoriert wurde.
- Assembly-Header (Pfad, `generatedPath`, `generation`, `origin=decompiled`, `confidence=medium`) ist für Cross-Target wertvoll, auf Fehlerpfaden zu lang.

## 8 Roslyn-Wünsche

Alles statisch/Roslyn, ohne LLM:

1. **Deklarierendes virtuelles/abstraktes Member bevorzugen**, wenn `Type.Method` / `M:` auf ein Metadata- oder Source-Basismember zielt; Overrides als Trefferliste, nicht als Ambiguitätskandidaten.
2. **Origin-/Scope-Filter** analog anderer Tools: `source` vs. `metadata`, optional `production` vs. `tests`. Default für `project` sollte Source-first sein.
3. Pro Treffer eine **stabile Symbol-ID** (`T:`/`M:`) plus Kind, damit `get_symbol_body` / `get_feature_context` ohne Namensraten folgen.
4. Truncation mit **`continuationToken`** statt „`maxResults` erhöhen“; hartes Cap und Warnung bei `maxResults` ≫ 200.
5. `maxResults <= 0` → `INVALID_ARGUMENT`.
6. Deduplizierung identischer Metadata-Symbole (Assembly-`SafeProcessHandle`).
7. JSON-Schema: `targetType` Enum, Symbol-Parameter als wirklich required **oder** Beschreibung an `INVALID_ARGUMENT` anpassen; undokumentierte Aliase (`identifier`, `name`) entweder streichen oder benennen; Winner bei Alias-Konflikt explizit machen.
8. Beschreibung anpassen oder Lookup reparieren: `"IProcessor.Execute"` nur versprechen, wenn Metadata-Methoden auflösbar sind.
9. Optional: Implementierungen eines Attributs nicht 0-Erfolg stillschweigend — Hint „Attribute: `find_references` auf den Attributtyp“.

## 9 Phase 3 (AiNetLinter-Quellzeiger)

AiNetLinter-Root: `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Nur Nicht-`ok`. (`IComponentHandler` / Call 23 Host-Block: kein Server-Fix.)

| Befund | Pfad + Symbol | Roslyn-Ansatz |
|---|---|---|
| Metadata-Basismethode (`*.ExecuteAsync`, `IDisposable.Dispose`, `M:` der BCL) → `SYMBOL_NOT_FOUND` | `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindReferencesTool.cs` — `ResolveByNameAsync` / `TryResolveMetadataTypeAsync` (nur `INamedTypeSymbol`, `Compilation.GetTypeByMetadataName`). `M:`: `SymbolIdentifierResolver.FindExactStableIdAsync` sucht ausschließlich `SymbolFinder.FindSourceDeclarationsAsync` — Metadata-Member kommen nicht vor. Danach fällt `M:` in Namenssuche und scheitert. Handler `src\AiNetLinter\Mcp\Tools\TypeHierarchy\FindImplementationsTool.cs` — `FindMethodImplementationsAsync` wird nie erreicht. | `DocumentationCommentId.GetFirstSymbolForDeclarationId(id, compilation)` (bereits `AssemblyNavigationSourceFactory`). `Type.Method`: Typ zuerst (`GetTypeByMetadataName` / Source-Typ), dann `INamedTypeSymbol.GetMembers(name)` mit `IMethodSymbol`/`IPropertySymbol`, Slot über `OverriddenMethod`-Kette bzw. Interface-Member. Dann bestehendes `SymbolFinder.FindImplementationsAsync` / `FindOverridesAsync`. Beschreibung (`FindImplementationsDescription`) erst anpassen, wenn dieser Lookup steht. |
| Kurzform `BackgroundService.ExecuteAsync` → Ambiguität auf **Overrides** | `ResolveByNameAsync`: `lastSegment == "ExecuteAsync"`, `SymbolFilter.TypeAndMember`, Source-Treffer sind die Overrides. Kein Prefer-declaring-slot. | Unter den Kandidaten den virtuellen/abstrakten Slot wählen: `OverriddenMethod == null` und `ContainingType.Name` matcht das Präfix; Rest als Trefferliste, nicht als `AMBIGUOUS_SYMBOL`. |
| `ComponentHandler.GetDataAsync` NOT_FOUND | Derselbe Namenspfad (`lastSegment` + Source-only). Ob der Member anders heißt, ist ohne zweites Tool offen — der Lookup kann Source-Methoden trotzdem verfehlen, sobald Display-String/`EndsWith` nicht greift. | Nach Typ-Resolve `GetMembers`; Signatur über `DocumentationCommentId` oder Parameterliste, nicht nur `EndsWith(display)`. |
| BCL-/NuGet-Flut, Default 50 beginnt bei `AngleSharp.*` | `src\AiNetLinter\Mcp\Tools\TypeHierarchy\FindImplementationsTool.cs` — `FindTypeImplementationsAsync`: `SymbolFinder.FindImplementationsAsync(type, solution, transitive: true)` solutionweit inkl. Referenzen. `BuildResultDto`: `OrderBy TypeName` ordinal. `FormatLocation`: Metadata → `[extern/metadata]`, aber unfiltered. | Filter `origin=source\|metadata` (Default `source` bei `targetType=project`): `symbol.Locations.Any(l => l.IsInSource)`. Optional Testhilfsprojekte nachrangig (Pfad/`AssemblyName`). Metadata erst nach Source oder hinter `scopeType`. |
| Assembly-`SafeProcessHandle` doppelt | `BuildResultDto`: kein `Distinct`. `FindImplementationsAsync` kann denselben Typ mehrfach liefern. | `Distinct(SymbolEqualityComparer.Default)` vor `MapToDto`; Location eine (`First` Source, sonst Metadata). |
| `maxResults<=0` klemmt auf 1 | `FindImplementationsTool.ExecuteAsync` — `normalizedMax = maxResults < 1 ? 1 : maxResults`. | `INVALID_ARGUMENT`. Truncation: Offset-Token auf der bereits sortierten Liste statt „maxResults erhöhen“ bis Vollabruf. |
| Keine `T:`/`M:` in Erfolgslisten | `ImplementationItemDto` / `MapToDto` / `FormatResultText` — nur Name, Kind, Status, `Datei:Zeile:Spalte`. Ambiguität nutzt `FindSymbolTool.FormatSymbolLocations` (hat IDs). | `DocumentationCommentId.CreateDeclarationId` (bzw. `TryGetDocCommentId`) in DTO + Textzeile, analog Ambiguity. |
| Vier Aliase, nur zwei beschrieben; `symbolIdentifier` schlägt `symbol` | `SymbolGraphToolRegistrations.AddFindImplementations`: `effectiveIdentifier = symbolIdentifier ?? symbol ?? identifier ?? name`. `FindImplementationsDescription` nennt nur die ersten zwei. Schema: Identifier `string? = null`. | Required ohne Default; Beschreibung = Lambda. Bei Konflikt explizite Warnzeile (Winner `symbolIdentifier`). `targetType` Enum. |
| Assembly-Header `completeness=partial` vs. 379 gezählt; Sufficiency „vollständig“ | Tool: `McpSufficiencyHints.Append` wenn `!IsTruncated`. Header: `AssemblyAnalysisResponse.FormatHeader`. 379 ist die Roslyn-Menge, `partial` die Session. | Sufficiency nicht bei Assembly-Ziel. Header: `resultCompleteness` (Liste) von `sessionCompleteness` (Dekompilat) trennen. |
| Assembly-Fehlerpfade mit vollem Header | Dieselbe `FormatHeader`-Umhüllung vor `INVALID_ARGUMENT`/`SYMBOL_NOT_FOUND`. | Header auf eine Zeile (`generation`+`origin`) oder nur bei Erfolg. |
| Attribut → 0 Treffer ohne Hint | `FindTypeImplementationsAsync`: `TypeKind` weder Interface noch Class-Ableitung → oder Class ohne Derived → `FormatResultText` „Keine konkreten Implementierungen“. Attribute sind Klassen, `FindDerivedClassesAsync` liefert 0. | Wenn `type.BaseType?.Equals(Attribute)` oder Name auf `Attribute`: Hint `find_references` auf den Attributtyp. |
| Test-Stubs dominieren `ComponentHandler` | Dieselbe unfiltered Derived-Liste + ordinaler Name. | `scopeType=production` über Dokumentpfad / Testhilfsprojekt-Erkennung; Default Source-Produktion zuerst. |
