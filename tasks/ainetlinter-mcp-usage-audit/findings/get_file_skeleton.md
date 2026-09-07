# Finding: `get_file_skeleton`

Audit-Stand: 2026-09-07. Nur Schema-Lookup (`GetDynamicTools`) plus Calls dieses Tools. Kein anderes MCP-Tool, kein Build, kein Test.

Anker: Projekt `DataExecutor` / `ComponentRegistrationAttribute`; Assembly `Example.External.Business.Record`.

## 1 Schema-Kurzfazit

Beschreibung steuert den Happy Path gut: Überblick über Typen und Signaturen **ohne Bodies**, jede Signatur mit `id:` für `get_symbol_body`, Batch über `filePaths`, Aliase `filePath` / `path` / `file` für genau eine Datei. Zielvertrag `targetType=project|assembly` mit absolutem `targetPath` ist klar.

Input-Schema:

| Parameter | Typ | Pflicht im Schema | Bemerkung |
|---|---|---|---|
| `targetType` | string | ja | Kein Enum; Beschreibung nennt `project` / `assembly`. |
| `targetPath` | string | ja | Projektroot bzw. existierende `.dll`/`.exe`. |
| `filePaths` | array/null | nein (`default: null`) | Beschreibung: Array, auch für eine Datei. |
| `filePath` | string/null | nein | Alias für genau eine Datei. |
| `path` | string/null | nein | Alias. |
| `file` | string/null | nein | Alias. |
| `maxResponseBytes` | integer | nein (`default: 0`) | `0` = Standardbudget. **Kein** Min-Wert, **kein** Target-Unterschied im Schema. |

**Beschreibung vs. Schema vs. Laufzeit:**

- Schema und Beschreibung stellen `filePaths` optional dar; ohne jedes Datei-Argument (und bei `filePaths: []`) kommt `[ERROR]: INVALID_ARGUMENT: Pflichtparameter 'filePaths' fehlt oder ist leer.` Der Hint nennt nur `filePaths`, nicht die Aliase, die tatsächlich funktionieren.
- `maxResponseBytes` ist im Schema unspezifisch. Assembly: Werte `< 2048` werden abgelehnt, Default-Budget laut Hint **16384**. Projekt: 120/200/800 werden **still ignoriert** (volle Antwort, kein Fehler).
- „stabile `id:`“ gilt für Projekt-DocIds. Assembly-IDs enthalten `generation` und ändern sich in derselben Session.

## 2 Calls

Projektroot: `C:\Workspace\Sample.Project`.  
Assembly: `C:\ExternalAssemblies\Version-9\Example.External.Business.dll`.  
Latenz aller Calls: **schnell**, kein Timeout.

| # | Absicht | Argumente | Ergebnis |
|---|---|---|---|
| 1 | Happy Path Projekt, falscher Relativpfad | `filePath=Sample.Project/Infrastructure/DataExecutor.cs` | `[ERROR]: RESOURCE_NOT_FOUND`. Hint: Relativpfad, `find_symbol`. |
| 2 | Alias `path` | `path=…/ComponentRegistrationAttribute.cs` | Erfolg. 1 Typ, 2 Member, ~1 KB. `id:T:…ComponentRegistrationAttribute`, Properties mit `id:P:…`. |
| 3 | Assembly ohne Datei | nur `targetType`/`targetPath` | Envelope (`origin=decompiled`, `completeness=partial`, `generatedPath=…/Properties/AssemblyInfo.cs`) plus `INVALID_ARGUMENT` Pflicht `filePaths`. |
| 4 | Fehler, Datei fehlt (Projekt) | `filePath=DoesNotExist/NoSuchFile.cs` | `RESOURCE_NOT_FOUND`, gleicher Hint wie Call 1. |
| 5 | Happy Path `DataExecutor` | `filePath=Sample.Project/Infrastructure/Data/DataExecutor.cs` | Erfolg. 1 Typ, 15 Member, ~8–10 KB. `sealed`, Interfaces, öffentliche + private Methoden, lange DocIds `id:M:…`. Kein Truncation-Marker. |
| 6 | Alias `file` | `file=…/ComponentRegistrationAttribute.cs` | Identisch Call 2. |
| 7 | `filePaths` eine Datei | Array mit ComponentRegistration | Identisch Call 2. |
| 8 | Truncation klein, Projekt | ComponentRegistration, `maxResponseBytes=120` | **Keine** Kürzung, volle Antwort. |
| 9 | Assembly Leer | `filePath=Properties/AssemblyInfo.cs` | Envelope + „Keine Typen gefunden“. Kein ERROR. `generatedPath` zeigt genau diese Datei. |
| 10 | Fehler, Datei fehlt (Assembly) | `filePath=DoesNotExist/NoSuchFile.cs` | Envelope + `RESOURCE_NOT_FOUND` **„nicht in der Solution gefunden“** — Wortlaut Projekt, Ziel ist DLL. |
| 11–12 | Truncation Projekt | DataExecutor, `maxResponseBytes=200` bzw. `800` | Volle 15 Member, **Budget wirkungslos**. |
| 13 | Batch zwei Dateien | DataExecutor + ComponentRegistration | Beide Skelette hintereinander, getrennt durch `---`. Vollständig. |
| 14 | Absoluter Windows-Pfad | `C:\…\Infrastructure\Data\DataExecutor.cs` | Erfolg. Header-Pfad `C:/…` (Slash-Normalisierung). Relativpfad am Typ bleibt `Sample.Project/Infrastructure/Data/DataExecutor.cs`. |
| 15 | Backslash-Relativpfad | `Sample.Project\Infrastructure\Data\DataExecutor.cs` | Erfolg, Header mit `/`. |
| 16 | Leeres Array | `filePaths=[]` | Wie Call 3: Pflichtparameter leer. |
| 17 | Assembly Happy Path, Basename | `filePath=Record.cs` | Erfolg. **1099 Member**, Default-Budget. Abbruch **mitten in einer `id:`** (`id:as…`). Header weiter `Member: 1099`. Envelope `completeness=partial` (steht bei **jeder** Assembly-Antwort). |
| 18 | Assembly Namespace-Pfad | `Example.External.Business/Record.cs` | Wie Call 17 (gleicher Typ/ID-Stamm). |
| 19 | `maxResponseBytes` unter Min (Assembly) | Record, `400` | `INVALID_ARGUMENT`: mindestens **2048**; Hint Default-Assembly-Budget **16384**. Nicht im Schema. |
| 20 | Truncation Assembly groß | Record, `maxResponseBytes=50000` | ~48,8 KB / 282 Zeilen. Mehr Member als Default, trotzdem Abbruch `private…` ohne schließendes Fence, ohne „N von 1099“. Header weiter `Member: 1099`. |
| 21 | Basename Projekt | `filePath=DataExecutor.cs` | Erfolg wie Call 5 (undokumentierte Namensauflösung). |
| 22 | Konflikt `filePath` + `filePaths` (Projekt) | DataExecutor + Array ComponentRegistration | **Nur** `filePaths` (ComponentRegistration). `filePath` still verworfen. |
| 23 | Batch Exist + Fehlend | DataExecutor + `DoesNotExist/…` | DataExecutor vollständig, danach Hinweis „Datei nicht gefunden“ (kein harter ERROR für den Batch). |
| 24 | `maxResponseBytes=0` | DataExecutor | Wie Default: volle Antwort (Schema-konform: 0 = Standardbudget). |
| 25 | Konflikt Assembly | `filePath=Record.cs` + `filePaths=[Properties/AssemblyInfo.cs]` | Nur AssemblyInfo (leer). `filePaths` gewinnt. |
| 26 | Assembly Min-Budget | Record, `maxResponseBytes=2048` | Sehr früh abgeschnitten (mitten im Stringliteral). Header `Member: 1099`. Envelope unverändert `completeness=partial`. |

**Truncation / Token-Stress:** Bei `Record` (1099 Member, lange Assembly-IDs) ist Truncation der Normalfall. Es gibt **kein** `cursor`/`continuationToken`, keine „weiter ab Member k“. `maxResponseBytes` wirkt nur auf Assembly und erst ab 2048. Projektseitig ist das Limit in den getesteten Werten tot.

## 3 Verdict

**Teilweise wie beschrieben.** Für kleine/mittlere **Projekt**-`.cs`-Dateien tut das Tool genau das Richtige: Signaturen, `sealed`/Interfaces, öffentliche und private Member, stabile DocIds, Batch in einem Turn, Aliase und Slash/Backslash/absolut funktionieren.

Abweichungen, die einen Agenten behindern:

- Pflicht-Dateiargument vs. optionales Schema; Fehlermeldung ignoriert Aliase.
- `maxResponseBytes` projektseitig wirkungslos, assemblyseitig undokumentiertes Min/Default.
- Große Assembly-Typen: Token-Flut, harter Cut **in** IDs/Literalen, Header zählt 1099 obwohl der Body unvollständig ist, Envelope `completeness=partial` ist kein Truncation-Signal (steht immer).
- Assembly-`generatedPath` zeigt oft `Properties/AssemblyInfo.cs`, auch wenn `Record.cs` geliefert wurde.
- Fehlende Assembly-Datei spricht von „Solution“.
- Bei `filePath` **und** `filePaths`: stilles Gewinnen von `filePaths`.

## 4 Schwere

**degraded** (plus Reibung an Schema/Alias/Fehlern).

- Happy Path Projekt ist brauchbar (`ok`/`friction` allein würde die Assembly-Lage unterschlagen).
- `degraded`: Truncation ohne Folgeschritt, abgeschnittene IDs, `Member: 1099` trotz Teilmenge, generation-behaftete Assembly-IDs, Token-Last bei extern-God-Classes.
- `friction`: optionales vs. Pflicht-`filePaths`, Alias-Hint, `maxResponseBytes`-Vertrag, Konflikt still, „Solution“-Wortlaut auf DLL.
- Nicht `broken`: Ziel (Skelett + IDs) ist auf dem Projektanker erreichbar.

## 5 Nutzbarkeit

**Ja für Projektdateien; Assembly große Typen nur mit Workaround.**

Wieder aufrufen:

- Eine oder wenige bekannte `.cs`-Dateien (Relativpfad, absolut, Basename, Batch).
- Kleine decompilierte Dateien (`AssemblyInfo` → Leer-Hinweis ist ehrlich).

Workaround Assembly/`Record`:

- Dateiname raten oder Namespace-Pfad `Example.External.Business/Record.cs`.
- `maxResponseBytes` ≥ 2048, besser deutlich über 16384 — bleibt trotzdem unvollständig, IDs am Cut unbrauchbar.
- Envelope-`generatedPath` **nicht** als Datei des Treffers lesen.

Nicht sinnvoll als Dump einer 1000+-Member-Klasse: der Agent sieht vor allem private Felder und Konstanten, öffentliche API kommt in der Truncation oft nicht an.

## 6 Bugs+FP/FN

| Befund | Art | Call | Bewertung |
|---|---|---|---|
| Schema: `filePaths` optional; Laufzeit Pflicht (auch `[]`). | Vertrag | 3, 16 | Agent folgt Schema, scheitert. Hint verschweigt Aliase. |
| `maxResponseBytes` Projekt ignoriert (120/200/800), Assembly `<2048` Fehler. | Vertrag / Still-Ignore | 8, 11, 12, 19 | Schema lügt durch Auslassung. |
| Header `Member: 1099` bei abgeschnittenem Body. | FN der Vollständigkeit | 17, 20, 26 | Agent hält die Liste für komplett. |
| Cut mitten in `id:` / Literal, kein Rest-Hint. | Truncation | 17, 20, 26 | Folge-`get_symbol_body` mit Fragment unmöglich. |
| Assembly-ID `id:assembly:<hash>:<generation>:T:…` — `generation` 1→5 in einer Session. | ID-Instabilität | 3 vs. 17/20 | Widerspricht „stabile id“. |
| `generatedPath` bleibt AssemblyInfo bei `Record.cs`. | Irreführende Metadaten | 17, 18 | FP der Herkunftsangabe. |
| Fehlende DLL-Datei: „nicht in der Solution“. | Error-Copy | 10 | Wortlaut Projekt. |
| `filePath` + `filePaths`: Array gewinnt still. | Still-Ignore | 22, 25 | Agent denkt, beide Dateien kämen. |
| Basename `DataExecutor.cs` / `Record.cs` löst auf. | undokumentiert, nützlich | 17, 21 | FP-Risiko bei Namenskollision (hier nicht belegt). |
| Batch: fehlende Datei nur Hinweis, Einzelcall ERROR. | inkonsistent, Batch besser | 4 vs. 23 | Kein Bug, aber zwei Error-Shapes. |
| Skelett zeigt `async` an manchen `Task`-Methoden, an Overloads ohne `async` nicht. | Impl-Leak | 5 | Kein Signatur-Unterschied für Aufrufer; kann Overloads verzerren. |
| Keine Konstruktoren in ComponentRegistration/DataExecutor-Skelett. | vermutetes FN | 2, 5 | Ohne Dateilesen nicht verifiziert; Attribute haben typischerweise Ctor. |
| `completeness=partial` auf jeder Assembly-Antwort inkl. leerem AssemblyInfo. | Signal-FN | 3, 9, 17 | Kein Unterscheid Truncation vs. decompiled-Session. |

Keine beobachteten False Positives der Form „falscher Typ in der richtigen Datei“. `DataExecutor` und `ComponentRegistrationAttribute` matchen die erwarteten Anker.

## 7 Token/IDs

- **Projekt DataExecutor:** kompakt relativ zur Alternative Body-Lesen; 15 Zeilen Signatur + sehr lange DocIds (`QueryAsync``1(…)` inkl. voller Typnamen). Für eine Datei akzeptabel.
- **Projekt ComponentRegistration:** wenige hundert Tokens, ideal.
- **Batch:** additiv, keine gemeinsame Deduplizierung.
- **Assembly Record:** Default ~16 KB, 50 KB-Call ≈ 48,8 KB und **immer noch** nur ein Bruchteil von 1099 Membern — fast alles private Felder/`const` mit IDs > 120 Zeichen. Token-ineffizient für „Überblick“.
- **IDs Projekt:** DocId-Form `T:` / `M:` / `P:` — für `get_symbol_body` vorgesehen; in dieser Session unverändert.
- **IDs Assembly:** Präfix `assembly:<sha>:<generation>:`; Generation ist Session-/Reload-Zähler, nicht inhaltsstabil.
- **StructuredContent:** in den Textantworten nicht sichtbar; Truncation nur als abgebrochener Markdown-Fließtext.
- **Hints:** `find_symbol` bei `RESOURCE_NOT_FOUND` ist der richtige nächste Schritt (dieses Audit durfte ihn nicht nutzen).

## 8 Roslyn-Wünsche

Alles statisch/Roslyn, kein LLM:

1. **Truncation ehrlich:** `shownMembers`/`totalMembers`, Cut nur an Member-Grenzen, letzte `id:` immer vollständig, eine Zeile `truncated=true` plus `maxResponseBytes`/`nextOffset`.
2. **Fortsetzung:** `memberOffset` oder `continuationToken` (Symbol-Cursor in der Memberliste), kein zweites Voll-Skelett.
3. **Filter:** `memberKinds` (Method/Property/Field), `accessibility=public`, `excludeCompilerGenerated` — bei 1099 Membern sonst unbrauchbar.
4. **`maxResponseBytes`:** Schema mit Min/Default pro Target; Projekt entweder honorieren oder Parameter für `project` ablehnen statt ignorieren.
5. **Dateiargument:** Schema `anyOf` (eins von `filePaths`/`filePath`/`path`/`file` required); Fehlermeldung listet Aliase.
6. **Konflikt:** `INVALID_ARGUMENT` wenn `filePath` und `filePaths` beide gesetzt, oder dokumentierte Merge-Semantik.
7. **Stabile Assembly-IDs:** `generation` aus der ID heraus; Session nur im Envelope.
8. **`generatedPath`:** Datei des angefragten Skeletts, nicht die Session-Default-Datei.
9. **Error-Copy:** Assembly → „nicht in der decompilierten Assembly gefunden“, nicht „Solution“.
10. **Konstruktoren** und Operatoren analog anderer Member mit `id:`.
11. Signaturen ohne `async`-Modifier (nicht teil der C#-Aufrufsignatur).
12. Envelope-`completeness`: `partial` nur bei Truncation/fehlender Body-Verfügbarkeit, sonst `complete` für das angefragte File-Skelett.

## 9 Phase 3 (AiNetLinter-Quelle)

Welle 3, read-only `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Kein Patch.

| Befund | Pfad | Symbol | Roslyn-/statischer Ansatz |
|---|---|---|---|
| Schema: `filePaths` optional, Laufzeit Pflicht; Hint ohne Aliase | `src\AiNetLinter\Mcp\Registration\FileStructureToolRegistrations.cs`; `src\AiNetLinter\Mcp\Tools\FileStructure\GetFileSkeletonTool.cs`; `src\AiNetLinter\Mcp\McpToolResults.cs` | `AddGetFileSkeleton` (`string[]? filePaths = null`); `GetFileSkeletonTool.ExecuteAsync`; `McpToolResults.FilePathsBatchHint` | C#-Default macht JSON-`required` leer. Validierung bleibt in `ExecuteAsync` nach `McpBatchArguments.Normalize`. Hint um Aliase `filePath`/`path`/`file` erweitern; Schema-Text: eins der vier Felder Pflicht. MCP-`anyOf` ist SDK-seitig nicht aus einem Lambda ableitbar — Vertrag in Description + Laufzeitfehler, nicht RAG. |
| `filePath` + `filePaths`: Array gewinnt still | `src\AiNetLinter\Mcp\Registration\FileStructureToolRegistrations.cs` | `ResolveFilePaths` | Bei beiden gesetzt: `INVALID_ARGUMENT` (Konflikt) **oder** dokumentiertes Merge. Heute: `filePaths is { Length: > 0 }` verwirft den Alias. |
| `maxResponseBytes` Projekt ignoriert | `src\AiNetLinter\Mcp\Registration\FileStructureToolRegistrations.cs`; `src\AiNetLinter\Mcp\AnalysisToolCall.cs`; `src\AiNetLinter\Mcp\AnalysisTarget.cs` | `AddGetFileSkeleton` → `AnalysisToolDispatch.MaxResponseBytes`; `AnalysisToolCall.CreateTargetRoute` | Budget hängt nur an der Assembly-Route (`AssemblyAnalysisDispatcher.ExecuteAsync` → `AssemblyAnalysisResponse.Enrich`). Projekt-`ProjectCall` bekommt den Wert nicht. Entweder `GetFileSkeletonTool` das Budget durchreichen und honorieren, oder für `targetType=project` Werte `> 0` als `INVALID_ARGUMENT` (unsupported) ablehnen. |
| Assembly `<2048` Fehler, Default-Budget undokumentiert | `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResponse.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisResponseLimits.cs` | `AssemblyAnalysisResponse.Enrich` / `IsBelowMinimumResponseBudget`; `MinimumResponseBytes` (2048); `lease.Context.ResponseBudgetBytes` | Min/Default in Schema und Description pro Target nennen. Konstante 2048 ist hard; Default kommt aus der Session, nicht aus dem Toolschema. |
| Header `Member: 1099` trotz Truncation; Cut mitten in `id:` | `src\AiNetLinter\Maps\Skeleton\SkeletonMarkdownRenderer.cs`; `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResponse.cs` | `SkeletonMarkdownRenderer.Render` (`types.Sum(t => t.Members.Count)`); `ApplyWireBudget` → `TrimUtf8` | Renderer zählt **alle** Member, bevor das Wire-Budget den UTF-8-Text hart schneidet (`TrimUtf8` an Bytegrenze, inkl. Surrogate — nicht an Member-/`id:`-Grenze). Ansatz: Memberliste **vor** dem Render kappen (`shown`/`total`), Fence schließen, letzte `id:` vollständig; `truncated=true` + `maxResponseBytes` in Text und StructuredContent. `GetFileSkeletonTool` liefert derzeit `McpToolResults.Text` ohne Payload — deshalb fällt Enrich auf reinen Text-Trim. |
| Kein Folgeschritt / Offset | `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisModels.cs`; `src\AiNetLinter\Mcp\Tools\FileStructure\GetFileSkeletonTool.cs` | `AssemblyPaging.ReadOffset` / `CreateToken`; `RenderSingleFileSkeletonAsync` | Bestehenden Offset-Cursor (wie `inspect_assembly`) auf die **Memberliste** legen (`memberOffset`/`continuationToken`), nicht ein zweites Voll-Skelett. Roslyn: gleiche `ISymbol`-Reihenfolge wie der Walker. |
| Filter `memberKinds` / `public` / `excludeCompilerGenerated` | `src\AiNetLinter\Maps\Skeleton\SkeletonSyntaxWalker.cs` | `ExtractMembers`; `BuildFieldInfo`; `BuildMethodInfo` | Filter auf `ISymbol.Kind` / `DeclaredAccessibility` und `IsImplicitlyDeclared` bzw. `CompilerGeneratedAttribute`. Decompilierte Felder wie `__positionen` sind echte `FieldDeclarationSyntax` — ohne Symbolfilter landen sie im Skelett. |
| Assembly-ID mit `generation` | `src\AiNetLinter\Mcp\AnalysisSymbolIdentity.cs`; `src\AiNetLinter\Mcp\Assemblies\Analysis\Factories\AssemblyAnalysisEntryFactory.cs`; `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisSession.cs` | `AnalysisSymbolIdentity.Format`; `new AnalysisSymbolIdentity(contentHash, context.Generation)`; `nextGeneration` | `Format` schreibt `assembly:{hash}:{generation}:{docId}`. Generation nur im Envelope (`AssemblyAnalysisResponse.FormatHeader`); Lookup über SHA+DocCommentId (`ISymbol.GetDocumentationCommentId()`). Session intern weiter versionieren. |
| `generatedPath` = Session-Default (`AssemblyInfo.cs`) | `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisSession.cs`; `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResponse.cs` | `CreateGenerationOrigin` (`documents.FirstOrDefault()?.GeneratedPath`); `FormatHeader` (`generatedPath=`) | Origin speichert das **erste** Dekompilat-Dokument der Session, nicht das angefragte File. Envelope-Pfad = `document.FilePath` des per `SolutionDocumentPathResolver.FindCandidates` getroffenen `Document`. |
| `completeness=partial` immer | `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisSession.cs`; `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblySessionStatusExtensions.cs` | `DetermineStatus`; `ToCompletenessLabel` | Session-Status (fehlende Refs/Diagnostics → `Partial`) wird 1:1 als Tool-Completeness ausgegeben. Für das Datei-Skelett: `complete`, solange nicht member-trunkiert / Body unavailable; Truncation extra (`truncatedBy=responseBudget`). |
| Fehlende DLL-Datei: „nicht in der Solution“ | `src\AiNetLinter\Mcp\McpToolResults.cs`; `src\AiNetLinter\Mcp\Tools\FileStructure\GetFileSkeletonTool.cs` | `FileNotFound`; `RenderSingleFileSkeletonAsync` (`totalCount == 1`) | Eine Überladung/Parameter `isAssemblySession`: Wortlaut „nicht in der decompilierten Assembly gefunden“. Batch-Hinweis analog (`existiert nicht in der Solution`). Resolver bleibt `Solution.Projects.SelectMany(p => p.Documents)` — das ist der Roslyn-Workspace der Dekompilation. |
| Basename-Auflösung undokumentiert | `src\AiNetLinter\Core\Documents\SolutionDocumentPathResolver.cs` | `FindCandidates` → `IsBareFileName` / `document.Name` | Verhalten belassen, in Description nennen; bei `Count > 1` bereits `AmbiguousPath`. |
| Batch fehlend = Hinweis, Einzelcall = ERROR | `src\AiNetLinter\Mcp\Tools\FileStructure\GetFileSkeletonTool.cs` | `RenderSingleFileSkeletonAsync` (`totalCount == 1` vs. Markdown-Hinweis) | Bewusst zwei Shapes; Description angleichen oder Batch ebenfalls `RESOURCE_NOT_FOUND` mit Teilergebnis. |
| Keine Konstruktoren / Operatoren | `src\AiNetLinter\Maps\Skeleton\SkeletonSyntaxWalker.cs`; `src\AiNetLinter\Maps\Skeleton\SkeletonMarkdownRenderer.cs` | `ExtractMembers` (`ConstructorDeclarationSyntax` ja; Primary-Ctor nur Record); `AppendMembersOfKind(..., MemberKind.Constructor)` | Explizite Ctor-Syntax wird erfasst. Lücken: class-primary-ctor (`TypeDeclarationSyntax.ParameterList` analog Record); implizite Default-Ctor über `INamedTypeSymbol.InstanceConstructors` ohne `DeclaringSyntaxReference`. Operatoren: `OperatorDeclarationSyntax` / `ConversionOperatorDeclarationSyntax` + `IMethodSymbol.GetDocumentationCommentId()`. |
| `async` in Signaturen | `src\AiNetLinter\Maps\Skeleton\SkeletonSyntaxWalker.cs` | `BuildModifiers`; `BuildMethodInfo` | `SyntaxTokenList` ungefiltert (`async` ist Modifier, nicht Aufrufsignatur). `SyntaxKind.AsyncKeyword` (ggf. `PartialKeyword`) beim Signaturtext weglassen; `IMethodSymbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)` oder eigene Format ohne `async`. |
