# Finding: `get_type_hierarchy`

Audit-Stand: 2026-09-07. Nur Schema-Lookup (`GetDynamicTools`) plus Calls dieses Tools. Kein anderes MCP-Tool, kein Build, kein Test.

Anker: `DataExecutor` / `ComponentRegistrationAttribute` (Projekt) und `Example.External.Business.Record` (Assembly).

## 1 Schema-Kurzfazit

Zweck laut Beschreibung: Vererbungs- und Interface-Hierarchie eines C#-Typs (Basisklassen, implementierte Interfaces, abgeleitete/implementierende Typen, heuristische DI-Registrierungen). Identifier-Formen: `"T:Namespace.Klasse"`, `"Datei.cs:10:5"`, `"Datei.cs:10"` oder `"Klasse"`. `maxResults` begrenzt abgeleitete/implementierende Typen (Default 50). Zielvertrag: `targetType=project` mit absolutem Projektpfad oder `targetType=assembly` mit existierender `.dll`/`.exe`. Assembly-Antworten tragen Herkunft, Snapshot/Generation, Status und Vollständigkeit.

Input-Schema:

| Parameter | Typ | Pflicht (JSON) | Pflicht (Runtime) | Bemerkung |
|---|---|---|---|---|
| `targetType` | string | ja | ja | Kein Enum im JSON; Runtime akzeptiert nur `project` / `assembly`. |
| `targetPath` | string | ja | ja | Projektroot bzw. `.dll`/`.exe`. |
| `symbolIdentifier` | string \| null | nein | **ja** (oder Alias) | Default `null`. Runtime: `INVALID_ARGUMENT` wenn leer/fehlend. |
| `symbol` | string \| null | nein | Alias | Dokumentierter Alias. |
| `identifier` | string \| null | nein | Alias | Undokumentiert, funktioniert. |
| `typeName` | string \| null | nein | Alias | Undokumentiert, funktioniert. |
| `name` | string \| null | nein | Alias | Undokumentiert, funktioniert. |
| `maxResults` | integer | nein | nein | Default 50. `0` wird wie `1` behandelt. |

Schema-Lücke: JSON markiert `symbolIdentifier` als optional; die Runtime macht ihn (oder `symbol`) zur Pflicht. Drei weitere Aliase stehen im Schema, nicht in der Beschreibung.

## 2 Calls

Projektroot: `C:\Workspace\Sample.Project`.  
Assembly: `C:\ExternalAssemblies\Version-9\Example.External.Business.dll`.

| # | Absicht | Argumente | Ergebnis |
|---|---|---|---|
| 1 | Happy, Kurzname | project, `symbolIdentifier=DataExecutor` | `AMBIGUOUS_SYMBOL`. Mix aus Properties (`P:…DataExecutor`) und der Klasse `T:Sample.Project.Infrastructure.Sql.DataExecutor` (plus Partial-Datei). Hint: FQN oder Datei:Zeile:Spalte. |
| 2 | Happy, Attribut | project, `ComponentRegistrationAttribute` | Erfolg. Basisklassen `System.Attribute` → `object` (extern). Keine Interfaces, keine Abgeleiteten. Completeness-Hinweis. |
| 3 | Assembly, Kurzname | assembly, `RecordEngine` | Assembly-Header (`origin=decompiled`, `completeness=partial`) + `SYMBOL_NOT_FOUND`. Hint auf `find_symbol`. |
| 4 | Leer / Pflicht fehlt | project, ohne Identifier | `INVALID_ARGUMENT`: Pflichtparameter `symbolIdentifier` (oder `symbol`) fehlt oder ist leer. |
| 5 | Happy, FQN | project, `T:…Infrastructure.Sql.DataExecutor` | Erfolg. Basis `object`; Interfaces `IExternalDataQueryExecutor`, `IDataQueryExecutor`, `ISqlTransactionalExecutor` mit `T:`-IDs; keine Abgeleiteten; DI `AddSingleton` (2 Treffer, inkl. Test-Support). Completeness-Hinweis. |
| 6 | Datei:Zeile | project, `…/DataExecutor.cs:9` | Identisch zu Call 5. |
| 7 | Alias `symbol` + Truncation | project, `symbol=IComponentHandler`, `maxResults=1` | `SYMBOL_NOT_FOUND` (Typ existiert unter diesem Namen nicht). Alias `symbol` wird akzeptiert. |
| 8 | Fehler, unbekannt | project, `ThisTypeDoesNotExistAnywhere12345` | `SYMBOL_NOT_FOUND`. |
| 9 | Assembly, Kurzname | assembly, `Record` | `AMBIGUOUS_SYMBOL`. Eine Klasse `T:Example.External.Business.Record` plus viele Properties `P:…Record`. Opaque IDs `assembly:<sha256>:1:T:…` / `:P:…`. |
| 10 | Truncation | project, `T:…IDataQueryExecutor`, `maxResults=1` | Implementoren-Sektion: **2 Zeilen** (beide Partial-Dateien von `DataExecutor`), Footer `[21 Typen gesamt, 1 gezeigt — maxResults erhoehen]`. DI ungekürzt. |
| 11 | Alias `identifier` | project, `identifier=T:…DataExecutor` | Identisch zu Call 5. Undokumentierter Alias wirkt. |
| 12 | Alias `typeName` | project, `typeName=DataExecutor` | Wie Call 1: `AMBIGUOUS_SYMBOL`. Alias wirkt. |
| 13 | Alias `name` | project, `name=ComponentRegistrationAttribute` | Identisch zu Call 2. |
| 14 | Assembly Happy, FQN | assembly, `T:Example.External.Business.Record` | Erfolg. Header `generation=2`, `completeness=partial`. Basis `object`; Interface `System.IDisposable`; keine Abgeleiteten. Completeness-Hinweis. |
| 15 | Happy, Interface voll | project, `T:…IDataQueryExecutor` (Default `maxResults`) | 22 Listenzeilen / 21 eindeutige Typen (`DataExecutor` zweimal wegen Partial). Tests-Stubs + Produktion. Completeness-Hinweis. |
| 16 | Datei:Zeile:Spalte | project, `…/DataExecutor.cs:9:5` | Identisch zu Call 5. |
| 17 | Property statt Typ | project, `P:…FormLoadQueryRequest.DataExecutor` | `INVALID_ARGUMENT`: löst zu `Property` auf, nicht zu Klasse/Interface/Struct. |
| 18 | Cross-Target | project, extern-`T:…Record` | `SYMBOL_NOT_FOUND` (kein Source-Symbol). |
| 19 | Assembly Truncation | assembly, `T:System.IDisposable`, `maxResults=1` | **Liste leer** (`Keine abgeleiteten Typen.`), Footer `[372 Typen gesamt, 1 gezeigt — maxResults erhoehen]`. |
| 20 | Leerer String | project, `symbolIdentifier=""` | Wie Call 4: `INVALID_ARGUMENT`. |
| 21 | Assembly Truncation 5 | assembly, `IDisposable`, `maxResults=5` | Wie Call 19: leere Liste, Footer „5 gezeigt“. |
| 22 | `maxResults=0` | project, `IDataQueryExecutor`, `maxResults=0` | Wie Call 10: 2 Partial-Zeilen, Footer „1 gezeigt“. `0` ≡ `1`. |
| 23 | Attribut ohne Suffix | project, `ComponentRegistration` | `SYMBOL_NOT_FOUND` (findet `ComponentRegistrationAttribute` nicht). |
| 24 | Cross-Target | assembly, Platform-`T:…DataExecutor` | Header + `SYMBOL_NOT_FOUND`. |
| 25 | Falscher targetType | `targetType=assembly`, `targetPath=<Projektroot>` | `INVALID_ARGUMENT`: Assembly-Pfad muss vorhandene Datei sein. |
| 26 | Assembly Datei:Zeile | assembly, absoluter Decompile-Pfad `…\Record.cs:36` | Erfolg, Inhalt wie Call 14. |
| 27 | Assembly Default-Limit | assembly, `T:System.IDisposable` (Default 50) | Leere Liste, Footer `[372 Typen gesamt, 50 gezeigt — maxResults erhoehen]`. Implementoren werden **nicht** ausgegeben. |
| 28 | Ungültiger targetType | project-Pfad, `targetType=foo` | `INVALID_ARGUMENT`: muss exakt `project` oder `assembly` sein. |
| 29 | Opaque Assembly-ID | assembly, `assembly:<sha>:1:T:…Record` (ID aus Call 9) | `INVALID_ARGUMENT`: ID gehört nicht zur aktuellen Assembly-Generation (`generation` inzwischen 4). |
| 30 | Truncation 2 | project, `IDataQueryExecutor`, `maxResults=2` | **3 Zeilen** (2× Partial `DataExecutor` + 1 Stub), Footer „2 gezeigt“. |

## 3 Verdict

Für **konkrete Klassen** (FQN, `T:`, Datei:Zeile, Datei:Zeile:Spalte) liefert das Tool auf Projekt **und** Assembly die erwartete Hierarchie plus — im Projekt — eine nützliche DI-Heuristik. Identifier-Aliase (`symbol`, `identifier`, `typeName`, `name`) funktionieren. Fehlercodes sind maschinenlesbar (`AMBIGUOUS_SYMBOL`, `SYMBOL_NOT_FOUND`, `INVALID_ARGUMENT`) und Hints sind handlungsleitend.

Der **Assembly-Pfad für implementierende Typen ist unbrauchbar**: Bei `System.IDisposable` zählt die Runtime 372 Typen und behauptet „n gezeigt“, listet aber niemanden und schreibt stattdessen „Keine abgeleiteten Typen.“ Kurzname-Auflösung mischt Klassen und Properties und produziert token-teure Ambiguitätslisten. Partial-Klassen verdoppeln Zeilen und bringen Footer (`maxResults`) und sichtbare Zeilenzahl auseinander. `maxResults=0` clamped still auf 1. Opaque `assembly:…`-IDs aus Ambiguitätsfehlern veralten, sobald die Decompile-Generation steigt.

## 4 Schwere

**degraded**

Happy Path für Klassen-Hierarchie (Projekt + Assembly-FQN) funktioniert. Der zweite Kernauftrag — implementierende/abgeleitete Typen — ist auf Assembly-Zielen faktisch tot (leere Liste trotz Zähler). Truncation-Semantik und Partial-Duplikate verzerren Projekt-Ergebnisse. Kein Totalausfall, aber kein verlässlicher Hierarchie-Explorer über beide Targets.

## 5 Nutzbarkeit

**Mittel bis hoch — mit präzisem Identifier.**

Agenten-tauglich, wenn der Anker schon als `T:Namespace.Typ` oder Datei:Zeile vorliegt:

- Klasse → Basisklassen (inkl. `extern`) + Interfaces + DI-Heuristik.
- Interface (Projekt) → implementierende Typen inkl. Test-Stubs.
- Assembly-Klasse per FQN oder Decompile-`Datei.cs:Zeile`.

Schwach für Discovery:

- Kurzname `DataExecutor` / `Record` / `ComponentRegistration` trifft Properties, nicht den Typ, oder findet das Attribut ohne `Attribute`-Suffix nicht.
- Assembly-Interfaces liefern keinen Implementoren-Katalog — der Agent muss den Footer ignorieren oder glaubt fälschlich „keine Implementoren“.
- Completeness-Hinweis („kein zusätzliches Read/Grep“) ist bei Assembly `completeness=partial` irreführend neben dem Header.

Pragmatische Reihenfolge: Kurzname nur, wenn eindeutig; sonst sofort `T:` oder Datei:Zeile. Opaque Assembly-IDs nicht über Calls hinweg wiederverwenden.

## 6 Bugs+FP/FN

| Befund | Art | Bewertung |
|---|---|---|
| Assembly `IDisposable`: Footer „372 / n gezeigt“, Liste leer, Text „Keine abgeleiteten Typen.“ | **Bug / FN** | Kernfunktion „implementierende Typen“ auf dekompilierten Assemblies. Agent sieht 0 statt 372. |
| Partial-Klasse `DataExecutor` erscheint zweimal (`.cs` + `.OptimisticConcurrency.cs`), gleiche `T:`-ID. | FP / Duplikat | Zählt in der Liste 22 Zeilen vs. 21 Typen (Call 15). |
| `maxResults` zählt eindeutige Typen, listet aber Partial-Zeilen extra. Call 10: „1 gezeigt“, 2 Zeilen. Call 30: „2 gezeigt“, 3 Zeilen. | Truncation-Bug | Agent kann Limit nicht zuverlässig parsen. |
| `maxResults=0` ≡ `1`. | Clamp / Vertrag | Schema erlaubt Integer ohne Minimum; Runtime clamped still. |
| Kurzname löst Properties (`P:`) gleichrangig zu Typen (`T:`) auf. | FP der Suche | `DataExecutor`/`Record` werden mehrdeutig, obwohl ein klarer Typ existiert. |
| `ComponentRegistration` findet `ComponentRegistrationAttribute` nicht. | FN | Attribut-Konvention `[ComponentRegistration]` ist der dokumentierte Anker; Tool braucht den vollen Typnamen. |
| Opaque `assembly:<sha>:<gen>:T:…` aus `AMBIGUOUS_SYMBOL` verfällt bei Generation-Bump. | ID-Stabilität | Call 9 (`generation=1`) → Call 29 (`generation=4`) `INVALID_ARGUMENT`. |
| Schema: `symbolIdentifier` optional; Runtime Pflicht. | Vertrag | Fehlender/leerer Wert → `INVALID_ARGUMENT` (immerhin klar). |
| Aliase `identifier` / `typeName` / `name` undokumentiert in der Beschreibung. | Reibung | Funktionieren; Agent ohne Schema-Dump kennt nur `symbol`. |
| DI-Heuristik doppelt `AddSingleton<IDataQueryExecutor>` in derselben Testdatei (Z. 55 und 64). | FP-leicht | Disclaimer „Convention-/Factory-Scanning nicht abgedeckt“ ist ehrlich; Dublette bleibt Rauschen. |
| Completeness-Hinweis „kein Read/Grep nötig“ bei Assembly `completeness=partial`. | Widerspruch | Header sagt partial, Footer sagt vollständig. |
| Label-Mix: Sektion „Implementierende Typen:“ gefolgt von „Keine abgeleiteten Typen.“ | UX | Falsches Wording auf Interface-Anfragen. |

Keine beobachteten False Positives der Form „falsche Basisklasse für `DataExecutor`/`Record`“. Kein FN der Form „`DataExecutor` implementiert `IDataQueryExecutor` nicht“, sobald der Typ präzise identifiziert ist.

## 7 Token/IDs

- **Sparsam (Happy Klasse):** `DataExecutor`/`ComponentRegistrationAttribute`/`Record`-FQN ≈ Basisklassen + wenige Interfaces + 0–2 DI-Zeilen. Gut für Agenten.
- **Teuer (Ambiguität):** Call 1 listet 9 Symbole (Properties + Klasse + Partial). Call 9 (`Record`) listet eine Klasse plus eine lange Property-Serie mit vollen Decompile-Pfaden und `assembly:…`-IDs — hoher Token-Preis für einen Kurzname-Tippfehler.
- **Teuer (Interface voll):** Call 15 dumpte 21+ Implementoren inkl. Test-Nested-Types, jeweils mit Pfad + `T:`-ID. Default `maxResults=50` deckt das hier noch; größere Interfaces würden hart anschlagen.
- **Ironisch sparsam (Assembly-Bug):** Call 19/21/27 bleiben klein, *weil* die 372 Implementoren nicht gerendert werden. Nach einem Fix wäre `IDisposable` ohne enges `maxResults` ein Token-Risiko.
- **IDs Projekt:** stabil `T:Namespace.Typ` und `P:…` in Ambiguitätslisten. Datei:Zeile und Datei:Zeile:Spalte resolven denselben Typ.
- **IDs Assembly:** `T:Example.External.Business.Record` ist generation-stabil. Opaque `assembly:<sha256>:<generation>:T:…` ist es nicht. Decompile-`generatedPath` und `Record.cs:36` funktionieren in derselben Session.
- **Header:** Jeder Assembly-Call wiederholt `targetType`, `targetPath`, `generatedPath`, `origin`, `confidence`, `generation`, `status`, `completeness`, `bodyAvailability`, `contentMode`.

## 8 Roslyn-Wünsche

1. **Assembly-Implementoren wirklich listen** (oder ehrlich `completeness=unavailable` ohne „n gezeigt“). Call 19/27 ist der härteste Fix.
2. **Partial-Klassen einmal** pro `T:`-ID; Zusatzdateien optional als `partialFiles[]`.
3. **`maxResults` an sichtbaren Zeilen oder eindeutigen Typen ausrichten** und Footer = tatsächlich gerenderte Menge. `maxResults=0` ablehnen oder als „keine Implementoren-Liste“ definieren.
4. **Kurzname typsensitiv:** bei Hierarchie-Tools zuerst `kind=class|interface|struct|enum`; Properties nicht in denselben Ambiguitäts-Top.
5. **Attribut-Alias:** `ComponentRegistration` → `ComponentRegistrationAttribute` (Compiler-Konvention).
6. **Stabile Assembly-IDs:** `T:` über Generationen hinweg akzeptieren; opaque `assembly:sha:gen:…` nicht in `AMBIGUOUS_SYMBOL` empfehlen, oder Generation in der ID ignorieren, solange SHA gleich ist.
7. **Schema = Runtime:** `symbolIdentifier` required; Aliase in der Beschreibung nennen; `maxResults` Minimum 1 dokumentieren.
8. Completeness-Hinweis nicht setzen, wenn der Assembly-Header `completeness=partial` trägt.
9. Sektionslabel „Implementierende Typen“ vs. „Abgeleitete Klassen“ konsistent zum angefragten Symbol-Kind.

## 9 Phase 3 (AiNetLinter-Quellzeiger)

AiNetLinter-Root: `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Nur Nicht-`ok`.

| Befund | Pfad + Symbol | Roslyn-Ansatz |
|---|---|---|
| Assembly-`IDisposable`: 372 gezählt, Liste leer, „n gezeigt“ | `src\AiNetLinter\Mcp\Tools\SymbolGraph\GetTypeHierarchyFormatter.cs` — `ProjectSubtypesAsync` (`SymbolFinder.FindImplementationsAsync` / `FindDerivedClassesAsync`) zählt korrekt; `ProjectSubtypes` rendert via `FindSymbolTool.FormatSymbolLocations` (**nur** `Location.IsInSource`). Metadata-Implementoren erzeugen 0 Zeilen. `Take(maxResults)` trifft zuerst Referenzhüllen-Typen. Basen nutzen bereits den Extern-Fallback `FormatHierarchyTypeReference`. | Dieselbe Fallback-Zeile wie bei Basistypen (`(extern, keine Datei im Repo)` + `T:` via `DocumentationCommentId.CreateDeclarationId`). Source-Typen vor Metadata sortieren (`Locations.Any(IsInSource)`), dann kappen. Footer = gerenderte Zeilen **oder** eindeutige Typen, nicht `shown.Count` bei leerem Body. Alternative: `completeness=unavailable` ohne „n gezeigt“, wenn 0 Zeilen bei Total>0. |
| Partial-Klasse doppelt / Footer ≠ Zeilen | `ProjectSubtypes` + `FormatSymbolLocations`: eine Zeile je Source-Location. Truncation laut Kommentar auf Typ-Ebene (`types.Take`), Ausgabe trotzdem `SelectMany` Locations. | Ein Listeneintrag je `SymbolEqualityComparer`/`T:`-ID; weitere Partial-Dateien als `partialFiles[]` oder zweite Spalte. `ShownSubtypeCount` = sichtbare Zeilen **oder** eindeutige Typen — eine Wahrheit. |
| `maxResults=0` ≡ `1` | `src\AiNetLinter\Mcp\Tools\SymbolGraph\GetTypeHierarchyTool.cs` — `ExecuteAsync`: `normalizedMaxResults = maxResults < 1 ? 1 : maxResults`. | `maxResults < 1` → `INVALID_ARGUMENT`; Schema-Minimum 1. |
| Kurzname mischt `P:` und `T:` | `FindReferencesTool.ResolveByNameAsync`: `SymbolFinder.FindSourceDeclarationsAsync(..., SymbolFilter.TypeAndMember)` + `name == lastSegment`. Hierarchie-Tool braucht einen Typ, filtert aber erst **nach** eindeutiger Auflösung (`resolvedSymbol is not INamedTypeSymbol`). | Vor Ambiguität: `INamedTypeSymbol` bevorzugen; Properties nur wenn kein Typ gleichen Namens. Oder Lookup mit `SymbolFilter.Type`. Ambiguitätsliste: Typen zuerst, dann `maxResults` auf Kandidaten. |
| `ComponentRegistration` findet `ComponentRegistrationAttribute` nicht | Dieselbe `ResolveByNameAsync`, exakter Namensvergleich. | Zweiter Versuch `identifier + "Attribute"` auf `INamedTypeSymbol` (C#-Attribut-Lookup). |
| Opaque `assembly:sha:gen:T:` verfällt | `src\AiNetLinter\Mcp\AnalysisSymbolIdentity.cs` — `Matches` verlangt Hash **und** Generation. `SymbolIdentifierResolver.TryNormalizeAssemblyId` / `StaleAssemblyId`. Generation steigt in `AssemblyAnalysisRegistry` / `AssemblyAnalysisSession.InstallGeneration`. Nacktes `T:` wird bei Assembly-Ziel schon akzeptiert (`isAssemblyId` wenn Prefix fehlt). | In `AMBIGUOUS_SYMBOL` nacktes `T:`/`P:` ausgeben (wie `FindSymbolTool.FormatSymbolLocationEntries` vor `assemblyIdentity.Format`). Opaque-IDs: Generation ignorieren, solange SHA gleich (`Matches` nur Hash), oder Rebind über `DocumentationCommentId.GetFirstSymbolForDeclarationId`. |
| Schema: Identifier optional; Aliase undokumentiert | `SymbolGraphToolRegistrations.AddGetTypeHierarchy`: `string? symbolIdentifier = null` plus `symbol`/`identifier`/`typeName`/`name`. Runtime-Pflicht in `GetTypeHierarchyTool.ExecuteAsync`. Beschreibung nennt nur `symbol`. | Identifier ohne Default (required). Beschreibung = Lambda-Aliase. JSON-Enum `targetType`. |
| DI-Dublette in derselben Datei | `src\AiNetLinter\Mcp\Tools\SymbolGraph\DiRegistrationHeuristics.cs` — `ScanWith` / `RecordHit`: jede Regex-Match-Zeile, kein Distinct. | Dedup `(FilePath, Line, lifestyle)` oder SemanticModel auf die Generic-Name-Syntax (`INamedTypeSymbol` der Type-Args), nicht zweimal denselben `AddSingleton<IDataQueryExecutor>`. |
| Completeness-Hinweis bei Assembly `partial` | `GetTypeHierarchyTool.ExecuteAsync` hängt `McpSufficiencyHints.Append` an, sobald `!SubtypesTruncated` — trotz `state.AssemblySymbolIdentity`. Header kommt später (`AssemblyAnalysisResponse.FormatHeader`, `completeness=partial`). | Sufficiency nicht setzen, wenn `AssemblySymbolIdentity != null` oder Header `partial`. Truncation-Fall bleibt ohne Hint (schon so). |
| Label „Keine abgeleiteten Typen.“ unter „Implementierende Typen:“ | `FormatSubtypesSection`: `emptyMessage` fest `"Keine abgeleiteten Typen."`; Heading kommt aus `TypeKind.Interface` vs. Class in `BuildHierarchyAsync`. | Empty-Text an `SubtypeHeading` koppeln („Keine implementierenden Typen.“ vs. „Keine abgeleiteten Klassen.“). |

Nach Fix gegenprüfen (Live, nicht in dieser Welle): `T:System.IDisposable` auf `Example.External.Business.dll` mit `maxResults=5` — Liste und Footer konsistent; `IDataQueryExecutor` listet `DataExecutor` einmal; `maxResults=0` Fehler; Kurzname typ-first; `ComponentRegistration` → Attribut; opaque ID nach Generation-Bump oder nacktes `T:`.
