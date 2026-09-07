# Finding: `find_duplicates`

Audit-Stand: 2026-09-07. Nur Schema-Lookup (`GetDynamicTools`) plus Calls dieses Tools. Kein anderes MCP-Tool, kein Build, kein Test.

## 1 Schema-Kurzfazit

Beschreibung steuert den Call **weitgehend richtig**: Token-Clone auf Methodenebene, `mode` `clone` (Default) / `refactoring-drift` / `structural`, `targetType=project` + absoluter `targetPath`. Assembly **ausdrücklich unsupported** — live bestätigt (`ASSEMBLY_TARGET_UNSUPPORTED`). `helperSymbol` bei Drift Pflicht; Formate Datei:Zeile:Spalte, DocCommentId, Name — alle drei live belegt.

JSON-Schema schwächer als die Prosa: `targetType`/`mode`/`similarityThreshold`/`scopeType` ohne Enum; Zahlen ohne min. Zusätzliche Aliase `scope`, `path`, `helper`, `symbol` stehen **nur im Schema**, nicht in der Beschreibung — funktionieren live (`path`/`scope` = `scopeDir`, `helper`/`symbol` = `helperSymbol`). Agent, der nur die Prosa liest, kennt sie nicht; Agent, der nur das Schema liest, rätselt über Redundanz.

`scopeType: 'production' [Default]` ist der größte Steuerungsfehler: Default- und expliziter `production`-Scan enthalten Testhilfen unter `Tests.Logic` / `Tests.Components` / `Tests.Support` (z. B. `RequireDomainConnectionString`). Methodenzähler `4014 + 4773 = 8787` (`production` + `tests` = `all`) — die Partition ist intern konsistent, aber „production“ ≠ Produktionsprojekte.

## 2 Calls

Projektroot: `C:\Workspace\Sample.Project`. Alle Calls **schnell** (kein Timeout). Zeichen grob aus der Agent-Antwort; große Läufe über Tool-Output-Datei.

| # | Absicht | Argumente | Größe / Completeness | Ergebnis |
|---|---|---|---|---|
| 1 | Happy Path | `project` + Root, Defaults (`clone`, `fuzzy`, `maxResults` 20) | ~90 Zeilen, ~12k Zeichen; Footer | **20 von 98** Clustern, 4014 Methoden. Top: Test-Support 0,92. Footer: `maxResults erhoehen oder scopeDir eingrenzen`. Übersicht nur 5 Zeilen. |
| 2 | Assembly unsupported | `assembly` + extern-`BusinessOperation.dll` | ~4 Zeilen | `ASSEMBLY_TARGET_UNSUPPORTED` + Hint auf `project`. |
| 3 | Fehler, kein Projekt | `project`, `C:\Workspace\DoesNotExist` | — | **blockiert** (Auto-Review: Pfad außerhalb des Probe-Roots). Ersatz: Call 34. |
| 4 | Truncation | wie 1, `maxResults=2` | ~20 Zeilen | 2 Cluster, Footer `[98 Cluster gesamt, 2 gezeigt]`. |
| 5 | Leer, `exact` | `similarityThreshold=exact` | ~3 Zeilen | `Keine Duplikat-Cluster gefunden (4014 Methoden gescannt)` + Completeness-Hinweis. |
| 6 | Ungültiges `targetType` | `bogus` | ~3 Zeilen | `INVALID_ARGUMENT`: nur `project` oder `assembly`. |
| 7 | Explizit `production` | `scopeType=production` | wie Call 1 | **identisch** zu Default (4014 / 98, dieselben Test-Cluster). |
| 8 | `scopeType=tests` | Defaults | ~90 Zeilen, Footer | 4773 Methoden, **20 von 393**. Exact-1,00-Fakes oben. |
| 9 | `mode=structural` | Defaults | ~120 Zeilen, Footer | **20 von 71**, 4014 Methoden. Disclaimer „Pruefempfehlungen, keine Verstoesse“. Profilzeilen (`ret=`, `cf=`). |
| 10 | Drift ohne Helper | `mode=refactoring-drift` | ~3 Zeilen | `INVALID_ARGUMENT`: `helperSymbol` Pflicht, Formate genannt. |
| 11 | Leer, `minTokens` | `minTokens=500` | ~3 Zeilen | Leer, **4 Methoden** gescannt. Completeness-Hinweis. |
| 12 | Retry Call 3 | wie 3 | — | Approval-UI fehlgeschlagen; nicht wiederholt. |
| 13 | `scopeType=all` | Defaults | ~90 Zeilen, Footer | 8787 Methoden, **20 von 504**. Top = Test-Exacts. |
| 14 | `scopeDir` | `Sample.Project/Auth` | ~20 Zeilen, vollständig | 122 Methoden, **2 von 2**. Cluster 2 `fuzzy` 0,66. Relativer Repo-Pfad ok. |
| 15 | Drift, Name | `helperSymbol=DataTableColumnMapping.ToContract` | ~8 Zeilen | `AMBIGUOUS_SYMBOL` + zwei DocCommentIds in Backticks. |
| 16 | `normalizeIdentifiers=true` | Defaults | ~90 Zeilen, Footer | **20 von 257**, 4014 Methoden. Viele `exact` 1,00 (Kalender-Zwillinge, Prompt/Form-Factorys). |
| 17 | Ungültige Schwelle | `similarityThreshold=bogus` | ~3 Zeilen | `INVALID_ARGUMENT` mit `exact`/`near`/`fuzzy`. |
| 18 | Relativer Pfad | `targetPath=Sample.Project` | ~3 Zeilen | `INVALID_ARGUMENT`: Pfad muss absolut sein. |
| 19 | Drift, DocCommentId | `M:…ToContract(DataTableHandlerColumnDefinition)~…` | ~8 Zeilen, vollständig | 1 Kandidat: `ToPlatform` Score 0,87, „ruft Helper nicht auf“. Disclaimer. |
| 20 | Drift, Datei:Zeile:Spalte | `…DataTableColumnMapping.cs:10:1` | wie 19 | **gleicher** Treffer wie Call 19. |
| 21 | Ungültiges `mode` | `clone-all` | ~3 Zeilen | `INVALID_ARGUMENT` mit den drei Modi. |
| 22 | Ungültiges `scopeType` | `prod` | ~3 Zeilen | `INVALID_ARGUMENT` mit `all`/`production`/`tests`. |
| 23 | `maxResults=0` | — | ~3 Zeilen | `INVALID_ARGUMENT`: mindestens 1. **Kein stilles Default.** |
| 24 | Leer, `Docs` | `scopeDir=Docs` | ~3 Zeilen | 0 Methoden, keine Cluster, Completeness-Hinweis. |
| 25 | Alias `helper` | `helper=DataTableColumnMapping.ToPlatform`, Drift | ~8 Zeilen | `AMBIGUOUS_SYMBOL` — Alias wirkt. |
| 26 | Alias `symbol` | `symbol=NoSuchHelper_xyz123`, Drift | ~3 Zeilen | `SYMBOL_NOT_FOUND` + Hint `find_symbol`. |
| 27 | Alias `path` | `path=…/Auth` | wie 14 | identisch zu `scopeDir`. |
| 28 | Token-Stress | `scopeType=all`, `maxResults=100` | **64,1 KB / 429 Zeilen** | 100 von 504. Footer korrekt. Übersicht weiterhin 5 Zeilen. |
| 29 | `similarityThreshold=near` | Defaults | ~80 Zeilen, vollständig | **20 von 20**, 4014 Methoden. Default-fuzzy hatte 98 — Default ist wirklich `fuzzy`. |
| 30 | Drift, Ankertyp | `helperSymbol=DataExecutor` | ~20 Zeilen | `AMBIGUOUS_SYMBOL`: Properties + `T:DataExecutor` (Partial doppelt). Kein Methoden-Drift. |
| 31 | Alias `scope` | `scope=…/Auth` | wie 14 | identisch zu `scopeDir`. |
| 32 | Cap-nah | `scopeType=tests`, `maxResults=200` | **136,5 KB / 891 Zeilen** | 200 von 393, Footer korrekt. Cap ≥ 200. |
| 33 | Leer, Filter | `scopeDir=ZZZNoSuchDir_xyz123` | ~3 Zeilen | 0 Methoden, **kein** `INVALID_ARGUMENT`. Completeness-Hinweis. |
| 34 | Unterordner als Root | `targetPath=…\tasks\ainetlinter-mcp-usage-audit` | ~12 Zeilen | `PROJECT_NOT_INITIALIZED` + JSON-Vorlage (kein Walk-up zum Repo-Root). |
| 35 | `maxResults=201` | `scopeDir=Auth` | wie 14 | Nur 2 Cluster — Cap >200 **nicht** unterscheidbar. |

## 3 Verdict

**Teilweise wie beschrieben.** Happy Path, drei Modi, Schwellen, `minTokens`, `normalizeIdentifiers`, `scopeDir`, Assembly-Absage, Enum-Fehler und Truncation-Footer stimmen. DocCommentId- und Datei:Zeile:Spalte-Auflösung für Drift funktionieren; Mehrdeutigkeit liefert kopierbare IDs.

Abweichungen: `production` enthält Testhilfen; Schema-Aliase und fehlende Enums; Top-Übersicht fest auf 5 Cluster; Clone-Zeilen ohne stabile Symbol-ID; nicht existierendes `scopeDir` wie leeres Verzeichnis; Structural/`normalizeIdentifiers` labeln parallele Factorys als `exact` 1,00.

## 4 Schwere

**`degraded`**

Clone-Kern (Jaccard auf Methoden, Score, Datei:Zeile) ist korrekt und schnell. Ein Agent, der Default-Production als DRY-Audit der Fachprojekte nimmt, bekommt Test-Support oben und 98 Cluster ohne Produktionsfilter. Structural/Drift sind als Kandidaten deklariert, wirken in der Tabelle aber wie Verstöße (`exact` 1,00). `maxResults=200` auf Tests: >130 KB. Nicht `broken` (keine Abstürze, Stichprobe bestätigt Ähnlichkeit). Nicht nur `friction` (Scope-Semantik und FP-Optik verfälschen die Arbeitsentscheidung).

## 5 Nutzbarkeit

**nur mit Workaround.** Wieder aufrufen: ja, mit `scopeDir` auf das zu prüfende Verzeichnis, `scopeType` bewusst (`tests` vs. echtes Prod-Verzeichnis), `maxResults` klein (≤20), `mode=clone` zuerst. Drift nur mit eindeutiger DocCommentId. Structural und `normalizeIdentifiers` nicht als Merge-Auftrag lesen.

Workaround: Footer lesen; Completeness-Hinweis bei 0 Methoden **nicht** als „Scope war richtig“ nehmen; nach `AMBIGUOUS_SYMBOL` die Backtick-ID kopieren.

## 6 Bugs+FP/FN

| Befund | Art | Call |
|---|---|---|
| `scopeType=production` listet `Tests.*`-Support (`BuchungsserverSqlTestSupport`, `BrowserTestSiteBootstrap`, …) | Scope-FP vs. Agent-Erwartung | 1, 7 |
| Nicht vorhandenes `scopeDir` → 0 Methoden, kein Fehler, Completeness-Hinweis | Irreführende Leermenge | 33 |
| Top-Cluster-Übersicht immer 5 Einträge, auch bei `maxResults=100` | UI-Truncation ohne Hinweis | 1, 28 |
| `ToPlatform` als Drift von `ToContract` (inverse Mapper, kein Aufruf sinnvoll) | Drift-FP; Disclaimer vorhanden | 19, 20 |
| `BooleanSelectProperties` vs. `CodeProperties` structural `exact` 1,00 — verschiedene Property-Listen | Structural-FP | 9 vs. Datei |
| `PendingUserPrompt` vs. `PendingUserForm` clone `exact` 1,00 bei `normalizeIdentifiers` — parallele Factorys, anderes Feld | Clone-FP für Merge | 16 vs. Datei |
| `RevokeAllForSubjectAsync` vs. `DeleteAllForSubjectAsync` near 0,82 — gleiches Gerüst, andere SQL | Ähnlich ja, Merge-FP | 14 vs. Datei |
| `ListMemberUserIdsAsync` / `ListGrantSlugsAsync` / `ListApprovedUsernamesAsync` fuzzy 0,66 | Query-Boilerplate, kein Clone | 14 vs. Datei |
| `BuildFreshSteps` vs. `BuildUpdateSteps` structural `exact` — ähnliche Listen, andere Steps | Structural-FP | 9 vs. Datei |
| `DataExecutor` als Helper trifft Properties vor der Klasse | Ambiguity/FN für den Ankertyp | 30 |
| `maxResults=0` → Fehler (gut, kein Clamp) | Spec ok | 23 |

**Stichprobe Ähnlichkeit (Datei gelesen):**

- `RequireDomainConnectionString` in `BuchungsserverSqlTestSupport.cs:18` und `DomainCalendarSqlTestSupport.cs:21`: **echt nah** (gleiche Env/Config/Skip-Struktur, anderer Skip-Text). Score 0,92 gerechtfertigt. Liegt in Testprojekten, erscheint unter Default-`production`.
- `HealColumnWidths` / `HealColumnFilters` (`ComponentColumnUserConfigHeal.cs:27` / `:53`): **echtes DRY-Muster** (Where+ToDictionary, Generic vs. `string`).
- `ToContract` / `ToPlatform` (`DataTableColumnMapping.cs:10` / `:30`): **Feld-für-Feld spiegelbildlich**, kein Kandidat „Helper aufrufen“.
- `ExecuteCommandAsync` Firmen- vs. Mitarbeiterkalender (`CompanyCalendarHandler.cs:57` vs. `MitarbeiterkalenderSchedulerHandler.cs:59`): **echt ähnlich** (Executor + PostDispatch, andere Notification-Factory). Clone-TP, Merge in Core wäre Domain-Grenzbruch.
- `GetSiteComponentDataAsync` vs. `GetTestSiteComponentDataAsync` (`DataEndpoints.cs:41` / `:56`): dünne Wrapper auf dieselbe Internal-Methode — structural `exact` 0,99, **kein** ungenutztes Duplikat.

Kein FN in der Stichprobe für offensichtliche Token-Klone im Auth-`scopeDir`. Razor/JS werden nicht gescannt (Method-Granularität, Absicht).

## 7 Token/IDs

Default (Call 1): agententauglich, Footer sagt den nächsten `maxResults`/`scopeDir`. Call 28/32: Token-Flut ohne Cursor; Übersicht hilft nicht (nur 5).

Clone-/Structural-Zeilen: FQN-Signatur + relativer Pfad + Zeile + Token-Zahl. **Keine** `M:`-IDs, kein `cursor`. Folge-Call: Datei lesen oder Drift mit `Datei:Zeile:1`. `AMBIGUOUS_SYMBOL` ist die einzige Stelle mit kopierbaren DocCommentIds.

StructuredContent in der Agent-Antwort nicht sichtbar — nur Markdown. Completeness-Hinweis bei Leermenge ist zu kategorisch („kein zusaetzliches Read/Grep“), obwohl `scopeDir` vertippt sein kann.

## 8 Roslyn-Wünsche

- `scopeType=production`: nur Nicht-Testprojekte (Roslyn `IsTestProject` / Testhost / `*.Tests.*`), Testhilfen nach `tests`.
- Clone-Zeilen um DocumentationCommentId ergänzen (gleiche ID wie `find_references`).
- Schema: Enums für `mode`/`similarityThreshold`/`scopeType`/`targetType`; Aliase in der Beschreibung oder entfernen.
- Nicht existierendes `scopeDir` → `INVALID_ARGUMENT`, nicht 0-Methoden-Erfolg.
- Übersicht: `min(5, maxResults)` dokumentieren oder an `maxResults` koppeln.
- Structural/`exact` bei `normalizeIdentifiers`: Label nicht `exact`, wenn Identifier/Literale differieren; oder Score von Identifier-Normalisierung getrennt ausweisen.
- Drift: Inverse Mapper / gleiche Klasse mit Gegenrichtung nicht als „ruft Helper nicht auf“-Kandidat, wenn Signatur kontravariant zum Helper ist (Roslyn-Signaturvergleich).
- Kein Cursor nötig, wenn Default 20 bleibt; bei `maxResults` ≥ 50 Footer plus `scopeDir`-Zwang in der Beschreibung.

## 9 Phase 3

AiNetLinter-Root: `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Nur Nicht-ok-Befunde.

**`scopeType=production` enthält `Tests.*`-Hilfen** (`degraded`)
- Pfad: `src\AiNetLinter\Core\DuplicateDetection\DuplicateMethodCollector.cs` (Aufruf aus `src\AiNetLinter\Mcp\Tools\DuplicateDetection\DuplicateDetectionScanner.cs` / `StructuralDuplicateScanner.cs`)
- Symbol: `MatchesScopeType` — `PathNormalizer.IsTestFile` plus `Project.Name.EndsWith("Tests"|".TestKit")`; **nicht** `TestDetector.IsTestProject`
- Ansatz: Dieselbe Heuristik wie Dead-Code: `TestDetector.IsTestProject(document.Project)` (`src\AiNetLinter\Core\TestDetector.cs`, inkl. `HasTestProjectNameSuffix` mit `Contains("." + suffix + ".")` für `*.Tests.Logic`). Testhilfen ohne `*Tests.cs`-Dateiname (`*TestSupport.cs`) folgen dem Projekt, nicht dem Dateisuffix.

**Nicht existierendes `scopeDir` → 0 Methoden, Completeness-Hinweis** (`friction`)
- Pfad: `src\AiNetLinter\Output\PathNormalizer.cs`; `DuplicateDetectionTool.cs`; `McpSufficiencyHints.cs`
- Symbol: `PathNormalizer.MatchesScope` (Substring, kein Verzeichnis-Existenzcheck); `DuplicateDetectionTool.RenderText` / `BuildResponse` hängt bei 0 Clustern `McpSufficiencyHints.Append` an
- Ansatz: Wenn `PathScopeFilter` gesetzt und kein Dokument matcht: `INVALID_ARGUMENT` (Pfad unter Solution unbekannt). Completeness-Satz nicht bei 0-Methoden-Filtermiss.

**Top-Cluster-Übersicht fest 5** (`friction`)
- Pfad: `src\AiNetLinter\Mcp\Tools\DuplicateDetection\DuplicateDetectionTool.cs`
- Symbol: `RenderText` (`topCount = Math.Min(5, result.ShownClusters.Count)` sobald `TotalClusters > 20 || ShownClusters.Count > 20`)
- Ansatz: `min(ShownClusters.Count, maxResults)` oder die 5 in der Beschreibung. Truncation der Übersicht explizit.

**Clone-/Structural-Zeilen ohne DocCommentId** (`degraded`)
- Pfad: `src\AiNetLinter\Core\DuplicateDetection\DuplicateDetectionModels.cs`; `DuplicateDetectionEngine.cs`; `DuplicateDetectionTool.cs`
- Symbol: `DuplicateClusterMember` (nur Pfad/Zeile/`SignatureName`); Factory in `ScanAsync` nutzt `MethodFingerprint` ohne `ISymbol`; `AppendCluster` schreibt keine `M:`-ID
- Ansatz: `DocumentationCommentId.GetDocumentationCommentId(EligibleMethod.Symbol)` am Member mitschleifen; in Text und StructuredContent ausgeben (wie `AMBIGUOUS_SYMBOL` bei Drift).

**Drift-FP inverse Mapper (`ToContract`/`ToPlatform`)** (`degraded`)
- Pfad: `src\AiNetLinter\Core\DuplicateDetection\RefactoringDriftDetector.cs`; `src\AiNetLinter\Mcp\Tools\DuplicateDetection\RefactoringDriftScanner.cs`
- Symbol: `FindSimilarToAsync` (Jaccard ≥ `NearThreshold` und nicht in `callers`); kein Signaturvergleich
- Ansatz: Kandidaten in derselben `INamedTypeSymbol` überspringen, deren Parameter-/Return-Typen zum Helper kontravariant/invertiert sind (`IMethodSymbol.ReturnType` vs. `Parameters`). Optional: gleiches ContainingType + gespiegelte Parameternamen.

**Structural `exact` 1,00 bei verschiedenen Literalen** (`degraded`)
- Pfad: `src\AiNetLinter\Core\DuplicateDetection\StructuralDuplicateDetector.cs`; `DuplicateDetectionEngine.cs`
- Symbol: `FindSimilarPairs` (`StructureSimilarity.Cosine` auf `ret:`/`form:`/`cflowseq:`); `ClassifyBucket` (`score >= ExactThreshold` → `exact`)
- Ansatz: Structural nie als `exact` labeln, oder Token-Jaccard/`ILiteralExpression` getrennt ausweisen. Cosine auf Kontrollfluss ist kein Token-Klon.

**`normalizeIdentifiers` → Clone `exact` 1,00** (`degraded`)
- Pfad: `src\AiNetLinter\Core\DuplicateDetection\DuplicateDetectionEngine.cs`
- Symbol: `GetTokenRepresentation` (`IdentifierToken` → `$ID$`, Literale → `$LIT$`); `ClassifyBucket` danach wie ohne Normalisierung
- Ansatz: Bei `NormalizeIdentifiers=true` Bucket höchstens `near`, oder zwei Scores (roh vs. normalisiert). Identifier-/Literal-Differenz bleibt an `SyntaxToken.Text` sichtbar.

**Query-Boilerplate fuzzy 0,66 / Auth-SQL near 0,82** (`wish`)
- Pfad: `DuplicateDetectionEngine.cs`; `DuplicateDetectionTool.cs`
- Symbol: `ClassifyBucket` + Default `fuzzy` (≥ 0,65); `RenderText` ohne Merge-Disclaimer im Clone-Modus (nur Structural/Drift)
- Ansatz: Clone-Disclaimer analog Structural; oder `minTokens`/N-Gram-Frequenzkappe (`MaxMethodsPerNgramBucket`) für SQL-Gerüst. Kein LLM.

**`helperSymbol=DataExecutor` trifft Properties vor der Klasse** (`friction`)
- Pfad: `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindReferencesTool.cs`; `RefactoringDriftScanner.cs`
- Symbol: `ResolveByNameAsync` (`SymbolFinder.FindSourceDeclarationsAsync`, `SymbolFilter.TypeAndMember`, Reihenfolge ungewichtet); `RefactoringDriftScanner.ScanAsync` verlangt danach `IMethodSymbol`
- Ansatz: Bei Drift `IMethodSymbol` bevorzugen; sonst `INamedTypeSymbol` vor `IPropertySymbol`. Partial-Typen zusammenführen. `AMBIGUOUS_SYMBOL` mit DocIds bleibt für echte Mehrdeutigkeit.

**Schema: fehlende Enums, Aliase nur im Schema** (`friction`)
- Pfad: `src\AiNetLinter\Mcp\Registration\DuplicateDetectionToolRegistrations.cs`
- Symbol: `Register` (`scope`/`path`/`helper`/`symbol` als Lambda-Parameter); `FindDuplicatesDescription` nennt die Aliase nicht
- Ansatz: Enums für `mode`/`similarityThreshold`/`scopeType`/`targetType`; Aliase in der Beschreibung oder entfernen. `ReadOnlyTool` hängt bereits `ASSEMBLY_TARGET_UNSUPPORTED` korrekt an.

**Token-Flut ohne Cursor bei `maxResults≥100`** (`degraded`)
- Pfad: `DuplicateDetectionTool.cs`; `DuplicateDetectionScanner.cs`
- Symbol: `BuildToolResult` (`Take(effectiveMax)`); `RenderText` Footer ohne `cursor`
- Ansatz: Bei großem `maxResults` Footer plus `scopeDir`-Zwang in der Beschreibung (Wunsch). Paging optional; Default 20 belassen.
