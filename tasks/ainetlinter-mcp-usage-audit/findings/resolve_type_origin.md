# Finding: `resolve_type_origin`

Audit-Stand: 2026-09-07. Nur Schema-Lookup (`GetDynamicTools` namespace=`user-AiNetLinter`, `toolName=resolve_type_origin`) plus Calls dieses Tools. Kein anderes MCP-Tool, kein Build, kein Test.

Anker: `DataExecutor` (Platform-Quellcode) und extern-Typ `Record` in `Example.External.Business.dll`.

## 1 Schema-Kurzfazit

Beschreibung steuert den Call **im Kern richtig**: Typname → definierende Assembly (Name + Dateipfad), FQN, Symbol-Kind; `targetType` `project` **und** `assembly` sind ausdrücklich erlaubt. Beispiele (`IDataProvider`, `Vendor.Data.BaseCommand`) sind verständlich. Der Satz „Festplatten-Dateipfad der DLL“ verspricht aber einen **DLL-Pfad**; bei Solution-Quelltypen kommt stattdessen die `.slnx`.

Input-Schema (vollständig, JSON):

| Parameter | Typ | Pflicht | Bemerkung |
|---|---|---|---|
| `targetType` | string | ja | Kein Enum im JSON-Schema; Beschreibung und Live-Validierung: exakt `project` oder `assembly`. |
| `targetPath` | string | ja | Absolut; Project-Root bzw. existierende `.dll`/`.exe`. |
| `typeName` | string | ja | Kurzname oder FQN. Keine Beschreibung zu Generics, Nested (`+`), Mehrdeutigkeit. |

**Keine optionalen Parameter** (`maxResults`, `includeReferences`, `cursor`/`continuationToken`, `detailLevel`). Extra-Args werden still ignoriert. Keine Batch-IDs. `typeName` ist das einzige fachliche Argument — ohne Disambiguation, ohne `kind`-Filter.

## 2 Calls

Projektroot: `C:\Workspace\Sample.Project`.  
externe Assembly: `C:\ExternalAssemblies\Version-9\Example.External.Business.dll`.  
Alle Calls **schnell** (kein Timeout). Antwortgröße Happy Path ≈ 6 Markdown-Zeilen; Fehler ≈ 4–8 Zeilen plus gekappte Referenzliste.

| # | Absicht | Argumente | Ergebnis |
|---|---|---|---|
| 1 | Happy Path, Kurzname | `project`, `typeName=DataExecutor` | Erfolg. FQN `Sample.Project.Infrastructure.Sql.DataExecutor`, `class`, Assembly `Sample.Project`, **Dateipfad = `…\Sample.Project.slnx`**, Herkunft `Projekt-Quellcode`. Kein `completeness`-Marker. |
| 2 | Leer/Miss | `project`, `DoesNotExistTypeXyz123` | `[ERROR]: SYMBOL_NOT_FOUND`. Hint: `Durchsuchte Referenzen (427):` plus **10** Assembly-Namen, dann Abbruch. Kein `truncated`-Flag, kein Cursor. |
| 3 | extern, falscher Kurzname | `assembly`, `RecordEngine` | Assembly-Header (`origin=decompiled`, `status=partial`, `completeness=partial`, `confidence=medium`, `contentMode=decompiledProject`) + `SYMBOL_NOT_FOUND` (135 Referenzen, wieder 10 Namen). |
| 4 | Happy Path, FQN | `project`, voller DataExecutor-FQN | **Identisch** zu Call 1. |
| 5 | Leer `typeName` | `project`, `typeName=""` | `[ERROR]: INVALID_ARGUMENT: typeName darf nicht leer sein.` Plus Hint mit Beispielen. |
| 6 | extern Happy Path | `assembly`, `typeName=Record` | Erfolg. FQN `Example.External.Business.Record`, `class`, Assembly `Example.External.Business`, Dateipfad = **echte DLL**. Herkunft fälschlich **`Projekt-Quellcode`**. Header wie Call 3. |
| 7 | Referenziertes BCL-Interface | `project`, `ILogger` | `Microsoft.Extensions.Logging.ILogger`, `interface`, Assembly `Microsoft.Extensions.Logging.Abstractions`, Dateipfad = voller Pack-`ref`-DLL-Pfad, Herkunft `Referenzierte Assembly`. |
| 8 | Ungültiger Projektpfad | `project`, `…\does-not-exist` | `[ERROR]: PROJECT_NOT_INITIALIZED` inkl. Vorlage für `ainetlinter.project.json` am **falschen** Root. |
| 9 | Undokumentierte Optionals | wie Call 1 plus `includeReferences=true`, `maxResults=5` | **Identisch** zu Call 1. Extra-Args still ignoriert. Keine Truncation-Wirkung. |
| 10 | `assembly` auf Projektordner | `targetType=assembly`, `targetPath=<Projektroot>` | `[ERROR]: INVALID_ARGUMENT`: Pfad muss existierende `.dll`/`.exe` sein. |
| 11 | Häufiger Kurzname | `project`, `Task` | `System.Threading.Tasks.Task` / `System.Runtime` / Pack-DLL. **Keine** Ambiguity-Liste. |
| 12 | Generic | `project`, `List<string>` | Löst zu `System.Collections.Generic.List<T>` (Typargumente abgestreift). Dateipfad Pack-DLL. |
| 13 | extern FQN | `assembly`, `Example.External.Business.Record` | Wie Call 6 (`generation=2`). Herkunft weiter `Projekt-Quellcode`. |
| 14 | Fehlende DLL | `assembly`, `…\DoesNotExist.dll`, `Record` | `[ERROR]: INVALID_ARGUMENT`: Assembly-Pfad muss auf vorhandene Datei zeigen. |
| 15 | Ungültiges `targetType` | `targetType=bogus` | `[ERROR]: INVALID_ARGUMENT: Der Parameter 'targetType' muss exakt 'project' oder 'assembly' sein.` |
| 16 | Whitespace-`typeName` | `typeName=" "` | Wie Call 5 (`darf nicht leer sein`). |
| 17 | Nested-Miss | `DataExecutor+NestedDoesNotExist` | `SYMBOL_NOT_FOUND` analog Call 2 (427 / 10 Namen). |
| 18 | Gleicher Kurzname, extern-Compilation | `assembly`, `ILogger` | **Anderer Typ:** `Example.External.Logging.ILogger` in `externde.Shared.Core.dll` (voller extern-Pfad), Herkunft `Referenzierte Assembly`. Kein Hinweis auf Microsoft-`ILogger`. |
| 19 | Solution-intern, anderes Projekt | `project`, `ComponentHandler` | FQN `Sample.Project.Contracts.ComponentHandler`, `class`, Assembly `Sample.Project.Contracts`, Dateipfad **nur Dateiname** `Sample.Project.Contracts.dll` (kein Verzeichnis), Herkunft `Referenzierte Assembly`. |
| 20 | BCL-Basis | `project`, `Exception` | `System.Exception` / `System.Runtime` / Pack-DLL. |
| 21 | Relativpfad | `targetPath=Sample.Project` | `[ERROR]: INVALID_ARGUMENT: … muss ein absoluter Pfad sein.` |
| 22 | Pflichtfeld fehlt (`typeName`) | nur `targetType`+`targetPath` | Generisch: `An error occurred invoking 'resolve_type_origin'.` Kein `INVALID_ARGUMENT`. |
| 23 | Interface-Guess zum Anker | `project`, `IDataExecutor` | `SYMBOL_NOT_FOUND` (wahrscheinlich korrekt; Anker ist Klasse). |
| 24 | extern-Miss | `assembly`, `DoesNotExistTypeXyz123` | Header + `SYMBOL_NOT_FOUND` (135 / 10 Namen), analog Call 3. |

Zusätzlich (nicht als eigener Happy Path): `IComponentHandler` → `SYMBOL_NOT_FOUND`. Ohne Folgetool nicht als FN beweisbar (Interface existiert unter diesem Namen möglicherweise nicht).

**Truncation / optionale Parameter:** Schema hat **keine Limits**. Call 9 ändert nichts. Truncation tritt nur im `SYMBOL_NOT_FOUND`-Hint auf: Zähler `(427)` bzw. `(135)`, sichtbar aber immer **10** Namen, ohne `truncated`/`continuationToken`. Assembly-Erfolg hat `completeness=partial` im Header (Session), nicht in der Typ-Nutzlast.

## 3 Verdict

**Teilweise wie beschrieben.** Kurzname und FQN treffen den Anker; extern-`Record` und BCL-Referenzen werden aufgelöst. Assembly-Ziel funktioniert inkl. Session-Header.

Abweichungen, die einen Agenten in die Irre führen:

- Beschreibung: „Dateipfad der DLL“ — Call 1 liefert die **Solution-Datei** (`.slnx`), Call 19 nur einen **Dateinamen ohne Verzeichnis**.
- extern-Treffer (Call 6/13): Herkunft **`Projekt-Quellcode`**, obwohl der Header `origin=decompiled` sagt.
- Solution-interne Typen aus anderen Projekten (Call 19) wirken wie NuGet-/Binärreferenzen.
- Kurznamen ohne Disambiguation (Call 7 vs. 18: zwei verschiedene `ILogger`).
- Fehlendes Pflichtfeld: generischer Invoke-Fehler statt `INVALID_ARGUMENT` (Call 22 vs. 5).

## 4 Schwere

**`degraded`** (Hauptbefund), plus **`friction`** bei Schema/Fehlern.

- Nicht `broken`: Happy Path und externe Assembly liefern FQN + Assembly-Name; Agent kann damit weiterarbeiten.
- `degraded`: irreführender `Dateipfad` / `Herkunft`; bare DLL-Name für Contracts; keine Symbol-IDs; gekappte Referenzliste ohne Folgeschritt; Namenskollision ohne Kandidatenliste.
- `friction`: JSON-Schema ohne Enum; Extra-Parameter still; fehlendes `typeName` ohne Fehlercode; `PROJECT_NOT_INITIALIZED`-Hint legt eine Datei am falschen Root nahe.

## 5 Nutzbarkeit

**Ja, mit Workaround.**

Wiederaufrufen für: „In welcher Assembly liegt Typ X?“ — FQN und Assembly-**Name** sind brauchbar (DataExecutor, Record, ILogger, List\<T\>, Exception).

Workarounds:

- `Dateipfad` bei `Herkunft: Projekt-Quellcode` **nicht** als Source- oder DLL-Pfad behandeln (Call 1 = `.slnx`).
- Solution-interne Typen: nackten DLL-Dateinamen nicht als `targetPath` für `assembly` verwenden.
- extern: DLL-Pfad aus dem Treffer bzw. dem Header nutzen; `Herkunft: Projekt-Quellcode` ignorieren.
- Kurznamen nur, wenn eindeutig; sonst FQN. Gleicher Kurzname ist **compilationsabhängig** (Microsoft- vs. extern-`ILogger`).
- Folgetools: **kein** Symbol-Identifier in der Antwort — Agent muss `find_symbol` / `get_feature_context` mit dem FQN neu ansetzen.

Ohne Workaround: nein, wenn der nächste Schritt „Datei öffnen“ oder „dieselbe ID in `get_symbol_body`“ ist.

## 6 Bugs+FP/FN

| Befund | Art | Call | Bewertung |
|---|---|---|---|
| Source-Typ `DataExecutor`: `Dateipfad` = `.slnx`, nicht `.cs` und nicht die Platform-DLL. | Bug / Vertragsbruch | 1, 4, 9 | Beschreibung verspricht DLL-Pfad. Agent, der den Pfad öffnet, landet in der Solution, nicht im Typ. |
| `ComponentHandler` (Contracts, gleiche Solution): Herkunft `Referenzierte Assembly`, Pfad nur `Sample.Project.Contracts.dll`. | Bug / FP der Herkunft | 19 | Typ ist Solution-Quellcode, nicht eine fremde Binary. Nackter Dateiname ist als `targetPath` unbrauchbar. |
| extern-`Record`: Herkunft `Projekt-Quellcode` bei `origin=decompiled`. | FP der Herkunft | 6, 13 | Header und Nutzlast widersprechen sich. |
| `ILogger` project → Microsoft; `ILogger` externe Assembly → `Example.External.Logging.ILogger`. Keine Kandidaten. | FP der Eindeutigkeit | 7, 18 | Formal „ein Treffer“, semantisch erste Compilation-Match. |
| `SYMBOL_NOT_FOUND`-Hint: 427/135 Referenzen, 10 Namen, kein Truncation-Marker. | Truncation | 2, 3, 17, 24 | Zähler vorhanden, Rest unsichtbar, kein Folge-Call. |
| Extra-Parameter still ignoriert. | Still-Ignore | 9 | Agent könnte glauben, `maxResults` wirke. |
| Fehlendes `typeName` → generischer Invoke-Fehler. | Error-Shape | 22 | Call 5 (leerer String) ist sauber `INVALID_ARGUMENT`. |
| `List<string>` → `List<T>` ohne Hinweis auf abgestreifte Typargs. | Reibung, kein FP | 12 | Roslyn-typisch; für Agenten undokumentiert. |
| `RecordEngine` als Typname in der RecordEngine-DLL: Miss. | kein FN | 3 | Typ heißt `Record`; Fehler korrekt. |
| `IDataExecutor` / `IComponentHandler` Miss. | unbewiesen | 23 + Extra | Ohne `find_symbol`/`rg` kein FN-Nachweis. |

Keine beobachteten Abstürze. `Task`/`Exception` wirken als kanonische BCL-Treffer, nicht als Solution-Typen — ohne Ambiguity-Report nicht als FN belegbar.

## 7 Token/IDs

- **Tokens:** Sehr sparsam. Happy Path ~6 Zeilen. Fehler kompakt; die 10 Hint-Namen sind unproblematisch. Assembly-Header ~1 Zeile Metadaten (`generatedPath`, `generation`, `completeness`).
- **IDs:** **Keine** Symbol-IDs, keine Document-IDs, kein `symbolIdentifier`. Nur FQN + Assembly-Name + (unzuverlässiger) Dateipfad. Folge-Call muss den FQN als neuen `typeName`/`pattern` recyceln.
- **Stabilität:** Call 1 = 4 = 9 (DataExecutor). Call 6 ≈ 13 (Record; `generation` 1 vs. 2). externe Session `completeness=partial` ist reproduzierbar.
- **StructuredContent:** In der Agenten-Sicht nur Markdown-Liste; kein maschinenlesbares JSON-Objekt sichtbar.

## 8 Roslyn-Wünsche

1. **`Dateipfad` nach Herkunft trennen:** Source → `.cs` (bzw. SyntaxTree-Pfad); Metadata → kanonischer Assembly-Image-Pfad (absolut). Nie `.slnx`, nie nackter Dateiname.
2. **Herkunft an Roslyn-`ISymbol.Locations` / `ContainingAssembly` koppeln:** `CompilationSource` vs. `MetadataReference` vs. `DecompiledStub` — externe Typen nicht als Projekt-Quellcode labeln.
3. **Solution-interne ProjectReferences** als Source auflösen (wie `DataExecutor`), nicht als Binary mit Dateinamen.
4. **Symbol-Identifier** (derselbe wie `find_symbol` / `get_symbol_body`) in der Antwort, plus FQN.
5. **Ambiguity:** bei mehreren `INamedTypeSymbol` gleichem Namen Liste (FQN, Assembly) statt stiller Ersttreffer; optional `namespaceFilter`.
6. **`SYMBOL_NOT_FOUND`:** `truncated=true`, Sample-Limit dokumentieren, oder Hint ganz weglassen (Zähler reicht). Kein Dump aller 427 Referenzen.
7. JSON-Schema: `targetType` als Enum; unbekannte Properties ablehnen; fehlendes `typeName` wie leerer String als `INVALID_ARGUMENT`.
8. Generics: dokumentieren, dass `List<string>` auf `List<T>` normalisiert wird (`ConstructedFrom`).

## 9 Phase 3

Nicht-ok-Befunde (Schwere `degraded` / `friction` / `wish`). Pfade relativ zu `C:\Daten\Entwicklung\Ralf\AiNetLinter\`.

1. **Source-Typ: `Dateipfad` = `.slnx` statt `.cs`/DLL** (`degraded`)  
   Pfad: `src\AiNetLinter\Mcp\Tools\TypeResolution\ResolveTypeOriginTool.cs`  
   Symbol: `ExecuteProjectAsync` / `BuildDirectResult`  
   Ansatz: Bei `isSource` wird `path = context.FallbackPath` gesetzt; Fallback ist `solution.FilePath` (die `.slnx`). Stattdessen `INamedTypeSymbol.Locations` mit `IsInSource` → `SourceTree.FilePath` (SyntaxTree). Metadata weiter über `PortableExecutableReference.FilePath`.

2. **extern-`Record`: Herkunft `Projekt-Quellcode` bei Header `origin=decompiled`** (`degraded`)  
   Pfad: `src\AiNetLinter\Mcp\Tools\TypeResolution\ResolveTypeOriginTool.cs`; Envelope: `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResponse.cs`  
   Symbol: `BuildDirectResult` (`SymbolEqualityComparer` vs. `compilation.Assembly`) / `RenderMarkdown`  
   Ansatz: Dekompilat-Typen liegen in der Session-`Compilation.Assembly`, daher `isSource=true` → Label „Projekt-Quellcode“. Label an `ISymbol.Locations` koppeln: Source vs. Metadata; bei Assembly-Session zusätzlich `AssemblyOrigin.IsDecompiled` → `DecompiledStub`, nie „Projekt-Quellcode“.

3. **Solution-interne ProjectReference (`ComponentHandler`) als nackte DLL** (`degraded`)  
   Pfad: `src\AiNetLinter\Mcp\Tools\TypeResolution\ResolveTypeOriginTool.cs`  
   Symbol: `ExecuteProjectAsync` / `SearchInCompilation` / `SearchReferencedAssemblies`  
   Ansatz: Projekte werden `OrderBy(p => p.Name)` durchlaufen; der Host (`Sample.Project`) kommt vor `*.Contracts`. Dort ist der Typ nicht `compilation.Assembly`, sondern Referenz — `SearchReferencedAssemblies` setzt **immer** `isSource: false` und fällt auf `asm.Name + ".dll"` zurück, wenn `PortableExecutableReference.FilePath` fehlt (`CompilationReference`). Zuerst alle Solution-Projekte per `compilation.Assembly` / Source-Locations ablaufen; Referenzen nur für echtes Metadata. `isSource = type.Locations.Any(l => l.IsInSource)`.

4. **Kurznamen ohne Disambiguation (`ILogger`)** (`degraded`)  
   Pfad: `src\AiNetLinter\Mcp\Tools\TypeResolution\ResolveTypeOriginTool.cs`  
   Symbol: `FindTypeInNamespace` / `IsTypeMatch`  
   Ansatz: Erstes `GetTypeMembers()`-Match gewinnt (DFS, case-insensitive). Alle Treffer sammeln; bei Count>1 `McpToolResults.AmbiguousSymbol` mit FQN + `ContainingAssembly.Name` (wie `FindReferencesTool.ResolveByNameAsync`).

5. **`SYMBOL_NOT_FOUND`-Hint: Zähler 427, sichtbar 10, kein Truncation-Flag** (`friction`)  
   Pfad: `src\AiNetLinter\Mcp\Tools\TypeResolution\ResolveTypeOriginTool.cs`  
   Symbol: `NotFoundResult` (`distinctSearched.Take(10)`)  
   Ansatz: Limit dokumentieren oder Hint auf den Zähler beschränken; `truncated=true` analog anderer Tools. Kein Dump aller `compilation.References`.

6. **Fehlendes `typeName` → generischer Invoke-Fehler; Extra-Args still** (`friction`)  
   Pfad: `src\AiNetLinter\Mcp\Registration\SymbolGraphToolRegistrations.cs`  
   Symbol: `AddResolveTypeOrigin` (`string typeName` ohne Default)  
   Ansatz: SDK bindet Pflichtparameter vor dem Tool — leerer String trifft `ExecuteProjectAsync` (`INVALID_ARGUMENT`), fehlendes Feld nicht. Signatur `string? typeName = null` und dieselbe Leerprüfung. Extra-Properties: MCP-SDK verwirft unbekannte JSON-Felder; `additionalProperties: false` an der Create-Option, kein Roslyn.

7. **`PROJECT_NOT_INITIALIZED` am falschen Root** (`friction`)  
   Pfad: `src\AiNetLinter\Mcp\Projects\ProjectDefinitionLoader.cs`; Guard: `src\AiNetLinter\Mcp\AnalysisTargetResolver.cs`  
   Symbol: `Load` / `NotInitializedTemplate`  
   Ansatz: `targetType=project` prüft nicht `Directory.Exists`. Nicht existierender Root → `INVALID_ARGUMENT` „Pfad nicht gefunden“, Template nur wenn das Verzeichnis existiert, die Definitionsdatei aber fehlt.

8. **Keine Symbol-ID in der Erfolgsantwort** (`wish`)  
   Pfad: `src\AiNetLinter\Mcp\Tools\TypeResolution\ResolveTypeOriginModels.cs`, `ResolveTypeOriginTool.cs`  
   Symbol: `TypeOriginInfoDto` / `BuildSuccess`  
   Ansatz: `DocumentationCommentId.CreateDeclarationId` via `TryGetDocCommentId` in DTO und Markdown; Folgetools können dieselbe ID nutzen.

9. **`List<string>` → `List<T>` undokumentiert** (`wish`)  
   Pfad: `src\AiNetLinter\Mcp\Tools\TypeResolution\ResolveTypeOriginTool.cs`  
   Symbol: `FindWithArity` / `GetTypeByMetadataName` (MetadataName mit Arity)  
   Ansatz: Roslyn-`ConstructedFrom`/`OriginalDefinition`. Im Markdown `OriginalDefinition.ToDisplayString()` nennen, wenn die Eingabe Typargs hatte.

10. **JSON-Schema: `targetType` kein Enum** (`friction`)  
    Pfad: `src\AiNetLinter\Mcp\Tools\McpToolRegistrationOptions.cs`; Validierung: `AnalysisTargetResolver.ResolveTargetType`  
    Symbol: `TargetedReadOnlyTool` / `Create`  
    Ansatz: Schema wird aus `string targetType` inferiert. Runtime-Enum existiert bereits (`project`|`assembly`). Schema-Enum an der Registration; Reject unbekannter Properties wie (6).
