# Finding: `get_test_context`

Audit-Stand: 2026-09-07. Nur Schema-Lookup (`GetDynamicTools`) plus Calls dieses Tools. Kein anderes MCP-Tool, kein Build, kein Test.

Anker: `DataExecutor` (Produktionstyp mit Tests) und `[ComponentRegistration]` / `ComponentRegistrationAttribute`. Projektroot: `C:\Workspace\Sample.Project`.

## 1 Schema-Kurzfazit

Test-Dateien, -Klassen und -Methoden zu einem Produktions-Symbol (Klasse, Methode, `Datei.cs:Zeile` oder DocCommentId). Liefert statische Zuordnungsgründe, Kategorien (Unit/Integration/`RequiresSql`), kopierbare `dotnet test --filter`-Befehle und Hinweis bei fehlender Zuordnung.

Beschreibung steuert den Call **weitgehend korrekt**: `symbolIdentifier` primär; Aliase `symbol`, `identifier`, `name`; `maxResults` Default 30 (Testdateien). Zielvertrag: `targetType='project'`, `targetPath` absolut. **Assembly-Ziele ausdrücklich unsupported** — Laufzeit hält das ein (`ASSEMBLY_TARGET_UNSUPPORTED`).

Input-Schema:

| Parameter | Typ | Pflicht | Default | Bemerkung |
|---|---|---|---|---|
| `targetType` | string | ja | — | Kein Enum im JSON. Beschreibung: nur `project`. |
| `targetPath` | string | ja | — | Absoluter Projektroot. |
| `symbolIdentifier` | string \| null | **nein im JSON**, **ja zur Laufzeit** | null | Primär laut Beschreibung. |
| `symbol` | string \| null | nein | null | Alias; Fehlertext nennt ihn zuerst (`'symbol' (oder 'symbolIdentifier')`). |
| `identifier` | string \| null | nein | null | Alias, in Fehlertexten nicht genannt, live wirksam. |
| `name` | string \| null | nein | null | Alias, in Fehlertexten nicht genannt, live wirksam. |
| `maxResults` | integer | nein | 30 | Dateilimit. `0` und `-1` werden still auf 1 geklemmt. Kein `cursor`/`continuationToken`. |

**Irreführung:** JSON-`required` enthält nur `targetType`/`targetPath`, Laufzeit verlangt das Symbol. Fehlender-Parameter-Text priorisiert `symbol`, Schema/Beschreibung `symbolIdentifier`. Kurzname ohne Qualifikation ist in der Beschreibung erlaubt, trifft bei `DataExecutor` aber `AMBIGUOUS_SYMBOL` (Properties + Klasse). `ComponentRegistration` findet das Attribut nicht. Completeness-Hinweis „vollständig … kein Read/Grep“ erscheint **auch bei sichtbarer Truncation**.

## 2 Calls

Alle Calls schnell (kein Timeout). Grobe Antwortgröße = sichtbarer Markdown-Text.

| # | Absicht | Argumente | Ergebnis |
|---|---|---|---|
| 1 | Happy Path Kurzname | `symbolIdentifier=DataExecutor` | `[ERROR]: AMBIGUOUS_SYMBOL`. 9 Treffer: 7× Property `P:…DataExecutor`, Klasse `T:…DataExecutor` **doppelt** (Partial `DataExecutor.cs:9` und `DataExecutor.OptimisticConcurrency.cs:11`). IDs in Backticks. Hint: FQN oder `Datei:Zeile:Spalte`. ~20 Zeilen. |
| 2 | Unbekannt | `ThisTypeDefinitelyDoesNotExist_Xyz123` | `SYMBOL_NOT_FOUND` + Hint `find_symbol`. Klein. |
| 3 | Truncation vor Resolve | `DataExecutor`, `maxResults=1` | Identisch Call 1. `maxResults` greift **nicht** in der Ambiguous-Liste. |
| 4 | Pflicht fehlt | nur `targetType`/`targetPath` | `INVALID_ARGUMENT`: Pflichtparameter `'symbol' (oder 'symbolIdentifier')` fehlt. Beispiele: `Namespace.Klasse`, `Datei.cs:42`, DocCommentId. **`identifier`/`name` nicht genannt.** |
| 5 | Happy Path DocCommentId | `T:Sample.Project.Infrastructure.Sql.DataExecutor` | Erfolg. NamedType, Datei `…/DataExecutor.cs`. **10 Testmethoden in 3 Testdateien**, alle „Naming Convention Match“. 2× `RequiresSql`, 1× Unit. Kopierbarer Filter über die drei `*DataExecutor*Tests`-Klassen. `[HINWEIS]: vollständig … kein Read/Grep`. ~25 Zeilen. |
| 6 | FQN ohne `T:` | `Sample.Project.Infrastructure.Sql.DataExecutor` | Identisch Call 5. |
| 7 | Datei:Zeile nackt | `DataExecutor.cs:9` | Identisch Call 5. Dateiname ohne Ordner reicht hier. |
| 8 | Alias `symbol` | `symbol=` FQN | Identisch Call 5. |
| 9 | Alias `identifier` | `identifier=` FQN | Identisch Call 5. |
| 10 | Alias `name` | `name=` FQN | Identisch Call 5. |
| 11 | Truncation hart | `T:…DataExecutor`, `maxResults=1` | Header weiter **10 Methoden / 3 Dateien**. Liste: 1 Datei + `*(Zeige 1 von 3 Testdateien — maxResults erhoehen fuer alle)*`. Filterbefehl **weiter alle 3 Klassen**. Completeness-Hinweis **unverändert „vollständig“**. |
| 12 | Truncation 2 | `maxResults=2` | Wie 11, `Zeige 2 von 3`. Filter und Completeness weiter voll. |
| 13 | Methode ohne Signatur | `…DataExecutor.GetNextExternalSystemTransactionAsync` | `AMBIGUOUS_SYMBOL`: zwei Overloads, volle `M:…`-IDs, Datei `DataExecutor.OptimisticConcurrency.cs:22` / `:26`. |
| 14 | Property-ID aus Call 1 | `P:…FormLoadQueryRequest.DataExecutor` | Erfolg, Property. **2 Tests** in `PlatformExtensionsTests` — „Direct Member Match / Invocation“ (`IDataQueryExecutor_resolves_to_DataExecutor`, `IExternalDataQueryExecutor_resolves_to_DataExecutor`). |
| 15 | Assembly externe Assembly | `targetType=assembly`, `targetPath=…\Example.External.Core.dll` | `[ASSEMBLY] capability=unsupported`. `ASSEMBLY_TARGET_UNSUPPORTED`. Hint: unterstützte Roslyn-Abfrage oder `targetType='project'`. |
| 16 | Anker `[ComponentRegistration]` | `symbolIdentifier=ComponentRegistration` | `SYMBOL_NOT_FOUND`. Kein Vorschlag `ComponentRegistrationAttribute`. |
| 17 | Methode DocCommentId | volle `M:…GetNextExternalSystemTransactionAsync(Int32,String,CancellationToken)~Task{Int32}` | Erfolg, Method. Datei `DataExecutor.OptimisticConcurrency.cs`. **14 Methoden in 5 Dateien**: Firmen-/Mitarbeiterkalender-Stores (Invocation), ExternalSystemTransaction-Tests (Invocation), OptimisticConcurrency-Tests (**Naming Convention** auf `ExecuteMutationWithVersionCheck_*`). Filter über 5 Klassen. ~35 Zeilen. |
| 18 | Attribut-Typ | `ComponentRegistrationAttribute` | Erfolg. `T:…Contracts.ComponentRegistrationAttribute`. **0 Tests**. Empfehlung: neue Tests unter `Sample.Project.Setup.Tests/ComponentRegistrationAttributeTests.cs`. Completeness-Hinweis. |
| 19 | Typ ohne Tests | `FormLoadQueryRequest` | 0 Tests. Empfehlung: `…Setup.Tests/Handlers/Form/FormLoadQueryRequestTests.cs`. |
| 20 | `maxResults=0` | `T:…DataExecutor`, `maxResults=0` | Wie Call 11 (`Zeige 1 von 3`). Kein `INVALID_ARGUMENT`. |
| 21 | Ungültiger `targetType` | `targetType=projectx` | `INVALID_ARGUMENT`: muss exakt `project` oder `assembly` sein. Hint nennt Assembly, obwohl dieses Tool sie nicht kann. |
| 22 | Datei:Zeile:Spalte | `Sample.Project/Infrastructure/Data/DataExecutor.cs:9:1` | Identisch Call 5. Ambiguous-Hint funktioniert. |
| 23 | ComponentRegistration-Kandidat | `ScheduleHandler` | `AMBIGUOUS_SYMBOL`: nur **Test-Felder** `F:…ScheduleHandler` in Components-Tests. Keine Produktionsklasse in der Liste. |
| 24 | Leerer String | `symbolIdentifier=""` | Wie Call 4. |
| 25 | `maxResults=-1` | `T:…DataExecutor`, `maxResults=-1` | Wie Call 11. Still geklemmt. |
| 26 | Relativer `targetPath` | `Sample.Project` | `INVALID_ARGUMENT`: `targetPath` muss absolut sein. |
| 27 | Attribut DocCommentId | `T:…ComponentRegistrationAttribute` | Identisch Call 18 (0 Tests, `Setup.Tests`-Empfehlung). |
| 28 | Methoden-Kurzname | `ExecuteMutationWithVersionCheck` | `SYMBOL_NOT_FOUND` (im Gegensatz zu Klassen-Kurzname Call 1 = Ambiguous). |
| 29 | Geratener Handler | `MitarbeiterScheduleHandler` | `SYMBOL_NOT_FOUND`. |
| 30 | Geratenes Interface | `IComponentHandler` | `SYMBOL_NOT_FOUND`. |
| 31 | Methoden-Truncation | Call-17-ID, `maxResults=1` | Header 14/5. Liste: **erste** Datei = `FirmenkalenderStoreIntegrationTests` (Invocation, fachlich Rand). `Zeige 1 von 5`. Filter **weiter alle 5 Klassen**. Completeness „vollständig“. |

**Truncation / Token-Stress:** Default 30 reicht für `DataExecutor` (3 Dateien). Hartes `maxResults` kürzt nur die **Listenzeilen**, nicht Header-Zähler, nicht den Filterbefehl, nicht den Completeness-Satz. Kein `truncated: true` / StructuredContent sichtbar. Kein Continuation-Token — Folgecall mit höherem `maxResults`. Ambiguous-Listen werden nicht limitiert.

## 3 Verdict

**Teilweise wie beschrieben — als Test-Lookup nützlich, sobald der Identifikator eindeutig ist; Zuordnung und Truncation-Ehrlichkeit sind riskant.**

Stimmt: Aliase, Datei:Zeile(:Spalte), DocCommentId, FQN, Kategorien, Zuordnungsgründe (Naming / Direct Member / Invocation), leere Zuordnung mit NOTE, kopierbare `dotnet test`-Filter, Fehlercodes `AMBIGUOUS_SYMBOL` / `SYMBOL_NOT_FOUND` / `INVALID_ARGUMENT`, Assembly wirklich unsupported.

Weicht ab / schwächt den Agenten:

- Kurzname `DataExecutor` / `ComponentRegistration` / Methoden-Kurzname sind **keine** brauchbaren Einstiege.
- Truncation sagt „Zeige N von M“ und gleichzeitig „vollständig … kein Read/Grep“; der Filter listet die **nicht** gezeigten Dateien.
- `maxResults` 0/−1 still → 1.
- Test-Empfehlung bei 0 Treffern zielt auf **`Sample.Project.Setup.Tests`**, das in dieser Solution nicht die Testprojekte sind (`Tests.Logic` / `Tests.Ama` / `Tests.Components`).
- Klassen-Matching nur Naming-Convention; Methoden erben Naming vom Containertyp (FP) und ziehen Invocation-Stores (laut, oft Rand).
- Ungültiges `targetType` behauptet Assembly sei ein gültiges Ziel.

## 4 Schwere

Gesamt: **`degraded`**.

| Befund | Schwere |
|---|---|
| Completeness-Hinweis „vollständig / kein Read/Grep“ bei aktiver Truncation; Filter enthält gekürzte Dateien | `degraded` |
| Test-Zuordnung: FN Klasse `DataExecutor` ohne DI-Tests; FP Methode `GetNextExternalSystemTransactionAsync` inkl. `ExecuteMutation*`; FP Property → `PlatformExtensionsTests` | `degraded` |
| Leere Zuordnung empfiehlt `Setup.Tests` statt bestehender Testprojekte | `degraded` |
| Kurzname / `ComponentRegistration` / Methoden-Kurzname → Ambiguous/NotFound ohne Attribut-Hinweis | `friction` |
| Schema: Symbol nicht required; Fehlertext `symbol` zuerst; Aliase `identifier`/`name` fehlen im Fehler | `friction` |
| `maxResults` 0/−1 still geklemmt; `targetType=projectx` nennt Assembly als gültig | `friction` |
| Partial-Klasse doppelt in Ambiguous; Test-Felder vor Produktionsklassen (`ScheduleHandler`) | `friction` |
| Truncation-Payload (`truncated`, Filter nur für gezeigte Dateien); Attribut-Alias; Naming nicht auf Methoden erben | `wish` |

Kein `broken`: mit `T:`/`M:`/`P:` oder `Datei.cs:Zeile` kommt der Agent zu einer Antwort. Nicht `ok`, weil Default-Kurzname scheitert und Truncation/Matching den Agenten in falsche Tests oder „nichts fehlt“ treiben.

## 5 Nutzbarkeit

**Nur mit Workaround: ja.**

Wieder aufrufen nach einem eindeutigen Produktions-Symbol, aber:

1. Identifikator **nie** als Kurzname bei häufigen Membernamen (`DataExecutor`); sofort `T:`/`M:`/`P:` oder `Datei.cs:Zeile` aus dem Ambiguous-Block.
2. `[ComponentRegistration]` → `ComponentRegistrationAttribute` raten (Folgetool `find_symbol` laut Hint, hier nicht aufgerufen).
3. `maxResults` ernst nehmen: Header-Zähler und Filterbefehl sind **keine** „nur das Gezeigte“-Liste. Completeness-Satz ignorieren, sobald `Zeige N von M` erscheint.
4. Zuordnungsgrund lesen: „Naming Convention Match“ auf Containertyp ≠ Tests **dieser** Methode. Invocation-Treffer können Store-Integrationstests sein, die die Methode nur als Seiteneffekt rufen.
5. Bei 0 Tests **nicht** blind `Setup.Tests` anlegen — Platform-Tests liegen unter `*.Tests.Logic` / `*.Tests.Ama` / `*.Tests.Components`.
6. Assembly nicht versuchen; `targetType=project` belassen.

Ohne diese Workarounds würde ein Agent `DataExecutor` als unauflösbar abbrechen oder bei Truncation drei Filter-Klassen laufen lassen, obwohl nur eine Datei gezeigt wurde.

## 6 Bugs+FP/FN

Stichprobe gegen bekannte Platform-Fakten (kein `rg`/Folgetool; Evidenz aus denselben Calls).

| Befund | Art | Call | Bewertung |
|---|---|---|---|
| Kurzname `DataExecutor` trifft Properties vor/neben der Klasse; Partial doppelt | erwartet / Reibung | 1, 3 | IDs copy-paste-fähig. Agent ohne Folgeschritt bleibt stehen. |
| `ComponentRegistration` findet den Attributtyp nicht | FN Namensauflösung | 16 vs. 18 | C#-Konvention `Foo` ↔ `FooAttribute` fehlt. Hint nur `find_symbol`. |
| Methoden-Kurzname `ExecuteMutationWithVersionCheck` → NotFound, Klassen-Kurzname → Ambiguous | inkonsistente Auflösung | 28 vs. 1 | Beschreibung erlaubt „Methode“ ohne zu sagen, dass Kurznamen nur bei Typen (mehrdeutig) greifen. |
| Klasse `DataExecutor`: nur 3 `*DataExecutor*Tests`; `PlatformExtensionsTests` (`IDataQueryExecutor_resolves_to_DataExecutor`) fehlt | FN | 5 vs. 14 | Dieselben Methodennamen werden der **Property** `FormLoadQueryRequest.DataExecutor` zugeordnet, nicht der Klasse. |
| Property `FormLoadQueryRequest.DataExecutor` → DI-Resolutionstests | FP | 14 | Tests erwähnen `DataExecutor` im Namen; sie testen nicht das Form-Request-Property. Klasse `FormLoadQueryRequest` selbst: 0 Tests (Call 19). |
| `GetNextExternalSystemTransactionAsync`: 6× `ExecuteMutationWithVersionCheck_*` per Naming | FP | 17 | Dateiname `DataExecutorOptimisticConcurrencyIntegrationTests` matcht den **Containertyp**, nicht die Methode. |
| `GetNextExternalSystemTransactionAsync`: Firmen-/Mitarbeiterkalender-Store-Tests per Invocation | laut / möglicher FP für „Tests dieser Methode“ | 17, 31 | Statisch wahr (Aufruf), als Agent-Kontext aber Rand; Truncation zeigt **genau diese** ersten. |
| Completeness „vollständig“ bei `Zeige 1 von 3` / `1 von 5` | Bug Completeness | 11, 12, 20, 25, 31 | Direkt widersprüchlich zur Truncation-Zeile. |
| Filterbefehl listet auch nicht gezeigte Testdateien | irreführend / Token | 11, 31 | Agent kopiert den Block und hält ihn für den sichtbaren Scope. |
| 0-Tests-Empfehlung `Setup.Tests` | FP Pfad | 18, 19, 27 | Solution-Testprojekte heißen anders. Agent würde am falschen Ort Dateien anlegen. |
| `maxResults` 0/−1 → 1 ohne Fehler | stilles Clamp | 20, 25 | Schema sagt integer, nicht „≥1“. |
| `targetType=projectx` Hint „project oder assembly“ | Vertrag vs. Capability | 21 vs. 15 | Assembly-Call ist korrekt unsupported; der Validator-Text nicht. |
| `ScheduleHandler` Ambiguous nur Test-Felder | Ranking | 23 | Produktions-`[ComponentRegistration]`-Typ nicht in der Liste (oder anderer Name). Agent bleibt in Testhost-Feldern. |

Kein beobachteter Absturz. Leere Zuordnung ist eine NOTE, kein `isError` — das ist für „Typ ohne Tests“ korrekt, für „unbekannt“ gibt es `SYMBOL_NOT_FOUND`.

## 7 Token/IDs

- **Tokens:** Erfolgsantwort für `DataExecutor` klein (~25 Zeilen, ~2k Zeichen). Methoden-Kontext mit 5 Dateien noch handhabbar (~35 Zeilen). Ambiguous-Blöcke ähnlich klein, aber ohne `maxResults`-Deckel. Keine Token-Flut in dieser Probe (Default 30, nur 3–5 Dateien).
- **IDs am Subjekt:** Erfolg zeigt Anzeigenamen + Kind (`NamedType`/`Method`/`Property`), **kein** DocCommentId im Success-Header. Ambiguous-Fehler sind das beste Handoff (`id: \`T:…\`` / `M:…` / `P:…`).
- **IDs an Tests:** **fehlen.** Nur Dateipfad + Methodennamen in Klammern. Folgetool muss Namen neu auflösen. Kein `T:` der Testklasse.
- **IDs in Fehlern:** `AMBIGUOUS_SYMBOL` copy-paste-fähig. `SYMBOL_NOT_FOUND` nennt `find_symbol`. Success-Pfad nennt **kein** Folgetool.
- **Filterbefehle:** nützlich und kopierbar; bei Truncation übervollständig. `FullyQualifiedName~Klassenname` (nicht einzelne Methoden).
- **StructuredContent:** in der Agent-Sicht nicht erkennbar; nur Markdown. Truncation nur als Prosa.
- **Stabilität:** Call 5, 6, 7, 8, 9, 10, 22 textgleich für denselben Typ. Call 18 = 27.

## 8 Roslyn-Wünsche

Alles statisch/Roslyn, kein RAG:

1. **Completeness ehrlich:** bei `maxResults < Treffer` kein „vollständig / kein Read/Grep“; explizit `truncated=true` und Restanzahl.
2. **Filter nur für gezeigte Dateien** oder zwei Blöcke (`sichtbar` vs. `alle N`).
3. **`maxResults`:** `<1` als `INVALID_ARGUMENT` oder dokumentiertes Clamp mit einer Zeile „clamped to 1“.
4. **Test-Zuordnung:** Invocation/Direct Member strikt vor Datei-Naming; Naming vom Containertyp **nicht** auf unbeteiligte Methoden erben. Property-Match nicht über Testmethoden-Substring `DataExecutor`.
5. **Attribut-Alias:** `ComponentRegistration` / `[ComponentRegistration]` → `ComponentRegistrationAttribute`.
6. **Ambiguous:** `T:` vor `P:`/`F:`; Partial nicht doppelt; Produktion vor Testhost-Feldern.
7. **Methoden-Kurzname:** wie Typen auflösen (`AMBIGUOUS` mit `M:`-IDs) oder Beschreibung einschränken.
8. **0-Tests-Pfad:** Testprojekt aus der Workspace-Solution (`*.Tests.*`), nicht hart `Setup.Tests`.
9. **Success-Header:** DocCommentId des Subjekts in Backticks (wie Ambiguous).
10. **Testmethoden:** DocCommentId je Test in Backticks für Folgetools.
11. **Schema:** `symbolIdentifier` required (oder anyOf mit Aliasen); Fehlertext alle Aliase; `targetType`-Hint ohne Assembly, wenn unsupported.
12. Optional: `scopeType` analog (`unit` / `RequiresSql` / alle), damit Invocation-Stores nicht den Default-Slice füllen.

## 9 Phase 3 (AiNetLinter-Quellzeiger)

Nur Nicht-`ok`-Befunde. Kein Patch in diesem Task.

### Completeness „vollständig“ bei Truncation; Filter übervollständig (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\TestContext\TestContextFormatter.cs`; `GetTestContextTool.cs`; `TestRecommendationBuilder.cs`; Hinweis `src\AiNetLinter\Mcp\McpSufficiencyHints.cs`.
- **Symbol:** `TestContextFormatter.FormatReport` (hängt `CompleteDataHint` **immer** an); `GetTestContextTool.ExecuteAsync` (`BuildRecommendedCommands(testResults.TestFiles)` = ungekappt); `TestRecommendationBuilder.BuildDotNetTestCommands`.
- **Ansatz:** Sufficiency nur wenn `!payload.IsTruncated` (wie `FeatureContextFormatter.FormatReport`). Filterbefehle aus der **gezeigten** `testFiles`-Liste bauen oder zwei Blöcke (`sichtbar` vs. `alle N`). Gegenprobe: `T:…DataExecutor` + `maxResults=1` sagt nicht „vollständig“; Filter höchstens die eine gezeigte Klasse.

### Test-Zuordnung FN Klasse / FP Methode und Property (`degraded`)

- **Pfad:** `src\AiNetLinter\Core\TestCoverageScanner.cs`; `TestCoverageBatchScan.cs`.
- **Symbol:** `SelectMatchingMethodsAndReason`; `IsNamedAfterMember` (`Contains`); `FindTestMethods` / `CallsOrUsesTargetType`; `NormalizeTargets` (`targetMemberName = symbol.Name` bei Property).
- **Ansatz:** Invocation/`SemanticModel.GetSymbolInfo` strikt vor Datei-Naming. Bei Methode: kein Erben aller Klassentests über `MatchesTestClassName` auf den Containertyp. Property-Match nicht über Testmethoden-Substring (`DataExecutor` in `IDataQueryExecutor_resolves_to_DataExecutor`) — `CallsTargetSymbol` auf das **Property-Symbol**, nicht den Namensrest. Gegenprobe: `M:…GetNextExternalSystemTransactionAsync` ohne `ExecuteMutation*`; Klasse `DataExecutor` sieht DI-Tests oder Property-Treffer klar getrennt.

### 0-Tests-Empfehlung `Setup.Tests` (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\TestContext\GetTestContextTool.cs`; `src\AiNetLinter\Core\TestDetector.cs`.
- **Symbol:** `GetTestContextTool.SuggestTestFilePath`; `TestDetector.FindPreferredTestProject` (`Unit`/`Fast`/`Spec`, sonst `FirstOrDefault`).
- **Ansatz:** Bevorzugtes Testprojekt aus `Solution.Projects` mit `HasTestFrameworkReferences` wählen, WPF-`Setup.Tests` nicht als Default wenn `*.Tests.Logic`/`Tests.Ama`/`Tests.Components` existieren; optional Namens-Sibling zum Quellprojekt (`X` → `X.Tests.*`). Gegenprobe: `ComponentRegistrationAttribute` empfiehlt eines der Platform-Testprojekte.

### Kurzname / `ComponentRegistration` / Methoden-Kurzname (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindReferencesTool.cs`; `FindSymbolTool.FormatSymbolLocations`.
- **Symbol:** `ResolveByNameAsync`; `ResolveByNameAsync` bei 0 Treffern → `SYMBOL_NOT_FOUND` (Methoden-Kurzname ohne Typsegment); Ambiguous über `symbol.Locations` (Partial-Duplikat).
- **Ansatz:** Gleicher Attribut-Alias und Partial-Dedup wie bei `get_feature_context`. Ambiguous: `T:` vor `P:`/`F:`; Produktion (`!TestDetector.IsTestProject`) vor Testhost-Feldern (`ScheduleHandler`). Methoden-Kurzname: `SymbolFinder.FindSourceDeclarationsAsync` mit `SymbolFilter.Member` und `name == lastSegment` → `AMBIGUOUS_SYMBOL` mit `M:`-IDs statt NotFound. Gegenprobe: `ComponentRegistration` → Attribut; `ExecuteMutationWithVersionCheck` → Ambiguous mit IDs.

### Schema-Required und Alias-Fehlertext (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs`; `GetTestContextTool.cs`.
- **Symbol:** `AddGetTestContext`; `GetTestContextTool.ExecuteAsync` (Text `'symbol' (oder 'symbolIdentifier')`).
- **Ansatz:** `symbolIdentifier` im JSON-Schema required (SDK: nicht-nullable). Fehlertext Reihenfolge an Beschreibung anpassen und `identifier`/`name` nennen; Bindung bleibt `symbolIdentifier ?? symbol ?? identifier ?? name`.

### `maxResults` 0/−1 Clamp; `targetType=projectx` nennt Assembly (`friction`)

- **Pfad:** `GetTestContextTool.cs`; `src\AiNetLinter\Mcp\AnalysisTargetResolver.cs`.
- **Symbol:** `Math.Clamp(options.MaxResults, 1, 100)`; `AnalysisTargetResolver.Invalid` (Hint immer `'project' oder 'assembly'`).
- **Ansatz:** `maxResults < 1` → `INVALID_ARGUMENT` oder eine sichtbare Clamp-Zeile. Project-only-Hint ohne Assembly-Capability; Resolver-Hint toolabhängig (Flag/Überladung), nicht global.

### Success-Payload ohne DocCommentIds (`wish`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\TestContext\TestContextModels.cs`; `GetTestContextTool.cs`; `TestContextFormatter.cs`.
- **Symbol:** `TestContextPayload` (`TargetSymbol` nur `ISymbol.ToDisplayString()`); Testdateien `TestFileCoverageResult.TestMethods` als nackte Namen.
- **Ansatz:** Subjekt-`TryGetDocCommentId()` in den Header. Je Testmethode das `IMethodSymbol` aus der Testdatei (`SemanticModel.GetDeclaredSymbol`) und `DocumentationCommentId.CreateDeclarationId` in Backticks — Folgetool ohne Re-Resolve.
