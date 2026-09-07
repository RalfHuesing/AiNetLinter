# Finding: `find_dead_code`

Anker: Default-Scan der Platform-Solution (`targetType=project`, `targetPath=C:\Workspace\Sample.Project`). Stichprobe gegen `[ComponentRegistration]` `DataTableComponentHandler`, `[ModuleInitializer]` `SchedulerTerminDetailsBridgeModule`, Contracts-Public-API `IAiDomainCapabilityProvider.PluginName`, `JsonConverter` `SchedulerContextMenuSettingsJsonConverter`, Test-Sentinel `CoversAiSsmsSqlPanel`. Assembly-Ziele laut Schema unsupported.

## 1 Schema-Kurzfazit

Die Beschreibung steuert den Call **weitgehend korrekt**: Solution nach unreferenziertem Code; `accessibility` (`private_internal` Default, `all`/`private`/`internal`/`public`); `confidence` (`both` Default, `high`/`low`); `kind`; `scopeFilter` plus Aliase `scope`/`path`; `includeTests` Default false; `mode` (`members` Default, `locals`/`both`); `maxResults` Default 50. Zielvertrag `targetType=project` + absoluter Projektpfad. High = privat/intern ohne Framework-Marker, Low = Public-API / mögliche Framework-Bindung. Hinweis auf Reflection/DI/Serializer/Routing steht im Antwortkopf.

Irreführend oder unvollständig:

- JSON-Schema hat **kein Enum** für `targetType`/`accessibility`/`confidence`/`kind`/`mode`; gültige Werte nur im Fließtext.
- Beschreibung: Assembly-Ziele **ausdrücklich unsupported**. Laufzeit akzeptiert `targetType=assembly` und verlangt eine `.dll`/`.exe` (`INVALID_ARGUMENT` auf Verzeichnis) — kein Tool-spezifisches „unsupported“.
- Ungültige `accessibility`/`kind`-Werte werden **still auf Default zurückgesetzt** (kein `INVALID_ARGUMENT`). Agent glaubt, gefiltert zu haben.
- `kind` kennt `type`/`class`/`method`/`field`/`property`/`event`/`delegate`, die Zusammenfassung zählt aber auch **`constructor`** — dafür gibt es keinen Filter.
- Kein `cursor`/`continuationToken`/`detailLevel`/Symbol-IDs. Truncation-Hint („maxResults erhöhen oder scopeFilter verfeinern“) passt hier.
- `maxResults=0` wird still auf **1** geklemmt.
- Pflicht `targetPath` fehlt: generischer Invoke-Fehler, kein `INVALID_ARGUMENT`.

## 2 Calls

Alle Calls über `CallDynamicTool` / `user-AiNetLinter` / `find_dead_code`. Ohne Angabe: **schnell** (kein Timeout).

| # | Parameter | Antwort | Truncation / completeness | Größe (grob) |
|---|-----------|---------|---------------------------|--------------|
| A | project, Defaults | 10 tote Symbole, alle `[LOW]` intern; 0 high; `ask_user`; Hinweis vollständig | nein (10/10) | ~3 KB / ~40 Zeilen |
| B | `confidence=high`, `accessibility=private_internal`, `maxResults=20` | „Kein unreferenzierter Code“; 1280 Docs, 1669 Symbole, 0 Treffer | leer, vollständig | ~0,5 KB |
| C | `accessibility=all`, `maxResults=30` | MCP-Timeout `-32001` | — | Timeout |
| D | `kind=type`, `scopeFilter=Handlers`, `maxResults=15` | 1 Treffer: `SchedulerTerminDetailsBridgeModule` `[LOW]` class; 252 Docs | nein | ~1 KB |
| E | `maxResults=3` | 10 gesamt, 3 gezeigt; Hint maxResults/scopeFilter | ja 3/10 | ~1,2 KB |
| F | `targetType=assembly`, externe Assembly `BusinessOperation.dll` | **Cursor Auto-Review blockiert** (Pfad außerhalb Workspace); Tool nicht ausgeführt | — | — |
| G | `kind=method`, Alias `path=Sql`, `maxResults=10` | 1 Treffer: `SqlContextParameters.IsContextParameterName` `[LOW]`; 107 Docs | nein | ~1 KB |
| H | `includeTests=true`, `maxResults=15` | 33 gesamt (18 high, 15 low); Tests zuerst (HIGH-Felder), dann Production-LOW | ja 15/33 | ~4 KB |
| I | `mode=locals`, `scopeFilter=Auth`, `maxResults=10` | 0 Locals; Text „Compiler-Diagnose“; 77 Docs, 0 Symbole gescannt | leer, vollständig | ~0,6 KB |
| J | `accessibility=public`, `confidence=low`, `maxResults=10` | 229 tot (0 high); erste Hits Contracts-Public (`PluginName`, Enum `Selection`, JsonConverter `Read`/`Write`) | ja 10/229 | ~3 KB |
| K | `accessibility=bogus` | identisch zu A (Default), **kein Fehler** | nein | ~3 KB |
| L | `kind=notakind` | identisch zu A, **kein Fehler** | nein | ~3 KB |
| M | `scopeFilter=ThisPathDoesNotExistAnywhere_X9zQ` | 0 Docs; Hint Filter vs. Tests/`includeTests` | leer, erklärt | ~0,6 KB |
| N | `targetType=assembly`, `targetPath=<Projektroot>` | `INVALID_ARGUMENT`: Pfad muss existierende `.dll`/`.exe` sein; Hint Assembly-Vertrag | — | ~0,3 KB |
| O | `mode=both`, Alias `scope=Contracts`, `maxResults=8` | 1 Member (`RemoveSite`); keine Locals; 152 Docs | nein | ~1 KB |
| P | `accessibility=private`, `confidence=high`, `kind=field` | 0 Treffer (Production) | leer | ~0,5 KB |
| Q | nur `targetType=project` (ohne `targetPath`) | generisch: `An error occurred invoking 'find_dead_code'` | — | ~0,1 KB |
| R | `targetPath` nicht existent | `PROJECT_NOT_INITIALIZED` + JSON-Vorlage für `ainetlinter.project.json` | — | ~0,5 KB |
| S | `accessibility=internal`, `kind=class` | 1 Treffer: `SchedulerTerminDetailsBridgeModule` | nein | ~1 KB |
| T | `kind=property` (Default accessibility) | 0 (Properties sind public → außerhalb Default) | leer | ~0,5 KB |
| U | `accessibility=all`, `maxResults=5` | 307 tot (0 high); Art: property 78, field 33, method 130, constructor 51, class 15 | ja 5/307 | ~2 KB |
| V | `includeTests=true`, `maxResults=50` | 33/33 vollständig (18 high / 15 low) | nein | ~8 KB / ~90 Zeilen |
| W | `targetType=foobar` | `INVALID_ARGUMENT`: muss exakt `project` oder `assembly` sein | — | ~0,3 KB |
| X | `accessibility=private`, `kind=method`, `confidence=both` | 0 in Production | leer | ~0,5 KB |
| Y | `kind=event` bzw. `delegate` | 0 | leer | ~0,5 KB |
| Z | `includeTests=true`, `confidence=high`, `maxResults=10` | 18 high, 0 low; 10 gezeigt; alles Tests (`Covers*`, JS-Consts, `CapturingHandler`) | ja 10/18 | ~3 KB |
| AA | `maxResults=0` | 10 gesamt, **1 gezeigt**; Hint „auf 1 Eintraege gekappt“ | ja (0→1) | ~1 KB |

`cursor`/`continuationToken`/`detailLevel`/Batch-IDs: im Schema nicht vorhanden.

## 3 Verdict

**Teilweise wie beschrieben.** Happy Path, Scope-Aliase, `includeTests`, `maxResults`-Kappung, leerer Scope und Confidence-Trennung funktionieren. Dagegen: ungültige Enums still ignoriert; Assembly-Vertrag Beschreibung vs. Laufzeit; `accessibility=all` kann timeouten; HIGH in Tests trifft Framework-/Sentinel-Muster; LOW-Production enthält `[ModuleInitializer]` und `[ComponentRegistration]`-Nachbarn. Keine Symbol-IDs. Als Löschfreigabe unbrauchbar — als Kandidatenliste mit Pflicht-Nachlesen brauchbar.

## 4 Schwere

`degraded`

Begründung: Der Call kommt an und liefert eine nachvollziehbare Heuristik plus `ask_user`. Ein Agent, der HIGH als „direkt entfernen“ nimmt (so die Tool-Beschreibung), würde Test-Sentinels und unreferenzierte Consts löschen; LOW-Hits enthalten ModuleInitializer/JsonConverter/Public-API. Timeout und stille Default-Fallbacks machen Filter unzuverlässig. Nicht dauerhaft kaputt, aber riskant.

## 5 Nutzbarkeit

**nur mit Workaround**

Workaround: Default belassen (`private_internal`, `includeTests=false`) nur als Verdachtsliste; **nichts löschen** ohne Datei zu lesen. `confidence=high` in Production ist hier leer — kein „safe delete“. `includeTests=true` + HIGH nicht auto-löschen (`typeof`-Sentinels, Playwright-JS-Consts). `accessibility=all` nur mit kleinem `maxResults` und LOW als Nicht-Löschen. Scope über `scopeFilter`/`path`. Nach dem Tool: Trefferdatei lesen (Attribute, Overrides, Enum/JSON). Folge-Call ohne `T:`/`M:`-ID nur über `Datei.cs:Zeile`.

**Würde ich danach etwas löschen?** Nein. Auch nicht die 18 HIGH-Tesstreffer pauschal. Einziger nach Dateilesen plausibler Kandidat in der Stichprobe: ungenutzte Nested-Klasse `CapturingHandler` — trotzdem nicht allein auf dieses Tool.

## 6 Bugs + FP/FN

Stichprobe gegen Dateiinhalt (kein `rg`, kein anderes MCP).

**Stimmt / nützliche Heuristik (TP-Kandidaten, nicht Löschbeweis):**

- `CapturingHandler` in `SiteViewComponentCommandRouterTests.cs` — private Nested-Klasse, in der Testdatei nie instanziert. HIGH plausibel.
- `AssertStoredHorizontalFractionsShifted` — private Methode, umliegender Test ruft sie nicht. HIGH plausibel, ohne Vollscan unbestätigt.
- Leerer Scope (M) und High-in-Production = 0 (B, P, X) sind ehrlich, kein Fake-Treffer.
- Antwortkopf warnt explizit vor Reflection/DI/Serializer/Routing; Aktion `ask_user`.

**Bugs (reproduzierbar):**

- **Ungültiges `accessibility`/`kind` still Default** (K, L) statt `INVALID_ARGUMENT`.
- **`maxResults=0` → 1** (AA), analog andere Tools.
- **`accessibility=all` + `maxResults=30` Timeout** (C); dieselben 1669 Symbole mit `maxResults=5` ok (U). Limit begrenzt die Ausgabe, nicht zuverlässig die Laufzeit/Serialisierung.
- **Fehlendes `targetPath`:** generischer Invoke-Fehler (Q), kein Hint.
- **Assembly:** Beschreibung „unsupported“, Fehlertext behandelt es als unterstütztes Assembly-Target (N, W). externe Assembly-Call kam wegen Cursor-Guard nicht an (F).
- **Keine Symbol-IDs** in der Liste, nur `Pfad:Zeile:Spalte` + Klarname.
- **`kind`-Enum vs. Zusammenfassung:** `constructor` zählbar, nicht filterbar (U).
- **Completeness-Hinweis** „kein zusätzliches Read/Grep“ widerspricht der eigenen `ask_user`-Empfehlung und der FP-Lage.

**False Positives (Löschen würde schaden oder Intent zerstören):**

- **`SchedulerTerminDetailsBridgeModule`** (`[ModuleInitializer] Initialize()` → `EnsureRegistered()`). Klasse absichtlich ohne Namensreferenz. Limits nur `internalsVisibleTo`, **nicht** ModuleInitializer. LOW, aber in Default-Liste. Agent-Löschen bricht Scheduler-Bridge-DI/Runtime-Attach.
- **`JsonConverter.Read`/`Write`** (J, U) — Overrides, von STJ aufgerufen. Limits nennen `reflection`/`publicApiSurface`; trotzdem in der Tot-Liste. Löschen bricht JSON.
- **`IAiDomainCapabilityProvider.PluginName`** — öffentliche Plugin-API; Dateikommentar: Erweiterungspunkt, **nicht** wegen Ungenutzt entfernen. Limits korrekt, Listung trotzdem verführerisch bei `accessibility=all`.
- **`SchedulerContextMenuDialogKind.Selection`** — Enum-Member, typisch JSON/Dispatch. Reflection-Limit gesetzt; Löschen bricht Verträge.
- **`CoversAiSsmsSqlPanel = typeof(AiSsmsSqlPanel)`** u. a. `Covers*` — EnableTestSentinel / StaticTestSentinel. HIGH, Text „ohne Framework-Marker“. Löschen verletzt Test-Coverage-Regel.
- **`[ComponentRegistration("DataTable", …)]` + `TryGetRuntimeCache`:** Methode intern am Handler; Klasse ist Framework-gebunden. Default `includeTests=false` ignoriert Testhits. Limits nur `internalsVisibleTo`.

**False Negatives / blinde Flecken:**

- Production hat **0 HIGH**; wirklich privater toter Code ohne Marker wäre hier nicht in der Default-Liste als „safe“ markiert — entweder selten oder die Heuristik stuft Internals immer LOW wegen `internalsVisibleTo` (Tests sehen Internals). Dann ist HIGH in Production faktisch tot und die Beschreibung „high für direkt entfernbaren privaten/internen Code“ in dieser Solution **leer**.
- `mode=locals` hängt an Compiler-Diagnosen; ungenutzte Locals ohne Diagnose erscheinen nicht (I, O).
- Default blendet Tests aus; Test-only-Nutzung einer Production-Methode sieht wie tot aus (FN der Nutzung, FP als Production-Dead).

## 7 Token/IDs

Default-Antwort klein (~3 KB, 10 Hits). `includeTests` vollständig ~8 KB. `accessibility=all` ungekürzt wäre 307 Einträge — Default `maxResults=50` würde stark kappen; ohne Paging nur „Limit hochsetzen“. Textfläche: Pfad, Position, `[HIGH|LOW]`, Kind, Accessibility, Name, Containing Type, Grund, Limits. **Keine** `T:`/`M:`-IDs, kein StructuredContent, kein Cursor. Folge-Call: Position als `Datei.cs:Zeile:Spalte` in anderen Tools denkbar, aus dieser Antwort nicht kopierbar als DocCommentId. Hinweis „vollständig, kein Read/Grep“ ist für eine Löschentscheidung falsch.

## 8 Roslyn-Wünsche

- `[ModuleInitializer]`, `[JsonConverter]`/`JsonConverter<T>`-Overrides, Enum-Member, `typeof`-Sentinels/`Covers*` als Framework-/Sentinel-Marker → nicht HIGH; optional ganz aus der Default-Liste.
- `[ComponentRegistration]` und DI-Module (`*Module`, `IServiceCollection`-Extensions) analog Routing/Reflection in `limits` aufnehmen.
- Ungültige Enums → `INVALID_ARGUMENT` mit erlaubter Werteliste.
- `targetType=assembly` → klares `UNSUPPORTED_TARGET` (wie Beschreibung), nicht DLL-Pfad-Validierung.
- Pro Treffer stabile Symbol-ID (`T:`/`M:`/`F:`) für `get_symbol_body` / `find_references`.
- `kind=constructor` dokumentieren oder filterbar machen.
- `maxResults=0` ablehnen; `accessibility=all` vor Timeout intern kappen oder Scan-Budget in `completeness` ausweisen.
- Completeness-Hinweis nicht „kein Read nötig“, solange Aktion `ask_user` ist.
- `InternalsVisibleTo`: HIGH nicht pauschal verbieten oder in der Zusammenfassung sagen, dass Production-HIGH deshalb 0 ist.

## 9 Phase 3

AiNetLinter-Root: `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Nur Nicht-ok-Befunde.

**Ungültiges `accessibility`/`kind` still Default** (`friction`)
- Pfad: `src\AiNetLinter\Mcp\Tools\DeadCode\DeadCodeModels.cs`
- Symbol: `FindDeadCodeArgs.ParseAccessibility` / `ParseKind` / `ParseConfidence` / `ParseMode`
- Ansatz: Unbekannte Werte nicht auf Default mappen. `INVALID_ARGUMENT` mit der erlaubten Liste (wie `DuplicateDetectionTool.ExecuteAsync` bei `mode`/`scopeType`). Schema-Enums an `AddFindDeadCode` in `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs` (heute `string?`).

**`maxResults=0` → 1** (`friction`)
- Pfad: `src\AiNetLinter\Mcp\Tools\DeadCode\FindDeadCodeTool.cs`
- Symbol: `FindDeadCodeTool.ExecuteAsync` (`Math.Max(1, rawArgs.MaxResults)`)
- Ansatz: `< 1` ablehnen statt klemmen. Vorbild `DuplicateDetectionTool.ExecuteAsync` (`input.MaxResults is < 1`).

**`accessibility=all` Timeout** (`degraded`)
- Pfad: `src\AiNetLinter\Mcp\Tools\DeadCode\FindDeadCodeScanner.cs`
- Symbol: `IsSymbolUnreferencedAsync` (`SymbolFinder.FindReferencesAsync` solution-weit pro nicht-privatem Symbol); `BuildScanResult` kappt erst danach mit `Take(MaxResults)`
- Ansatz: Scan-Budget (Zeit oder Symbolzahl) in `completeness` ausweisen; `CancellationToken` an `FindReferencesAsync`. `maxResults` darf die Suche stoppen oder die Ausgabe kappen — beides sichtbar machen. Private Symbole bleiben auf Container-Dokumenten (`documents:`-Überladung).

**Fehlendes `targetPath` → generischer Invoke-Fehler** (`friction`)
- Pfad: `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs`; Fallback `src\AiNetLinter\Mcp\AnalysisTargetResolver.cs`
- Symbol: `AddFindDeadCode` (`string targetPath` Pflicht am MCP-Lambda); `AnalysisTargetResolver.Resolve` hat bereits `INVALID_ARGUMENT` „targetPath ist erforderlich“
- Ansatz: Parameter nullable machen, damit der Resolver den Hint liefert statt SDK-Invoke-Fehler.

**Assembly: Beschreibung unsupported, Laufzeit DLL-Validierung** (`friction`)
- Pfad: `src\AiNetLinter\Mcp\AnalysisTargetResolver.cs`; `src\AiNetLinter\Mcp\AnalysisToolCall.cs`; `src\AiNetLinter\Mcp\Tools\McpToolRegistrationOptions.cs`
- Symbol: `AnalysisTargetResolver.Resolve` (Assembly-Pfad vor Tool-Absage); `ProjectAnalysisDispatcher.ExecuteProjectAsync` → `UnsupportedAssemblyTarget`; `ReadOnlyTool` hängt `ProjectTargetContract` an
- Ansatz: Bei projekt-only Tools `ASSEMBLY_TARGET_UNSUPPORTED` **vor** `.dll`/`.exe`-Existenzcheck; oder `targetType=assembly` sofort absagen. Beschreibung (`ReadOnlyTool`) und Fehlercode angleichen.

**Keine sichtbaren Symbol-IDs** (`degraded`)
- Pfad: `src\AiNetLinter\Mcp\Tools\DeadCode\FindDeadCodeTool.cs`; `FindDeadCodeScanner.cs`
- Symbol: `AppendDeadSymbols`; `AddDeadSymbol` setzt `DeadCodeEntry.Id` auf `ISymbol.ToDisplayString()`, nicht `DocumentationCommentId.GetDocumentationCommentId`
- Ansatz: DocCommentId (`T:`/`M:`/`F:`/`P:`) in Text **und** StructuredContent ausgeben, dieselbe Form wie `find_references`.

**`kind=constructor` zählbar, nicht filterbar** (`friction`)
- Pfad: `src\AiNetLinter\Mcp\Tools\DeadCode\DeadCodeFilters.cs`; `DeadCodeModels.cs`
- Symbol: `GetSymbolKindString` (`MethodKind.Constructor` → `"constructor"`); `DeadCodeKindFilter` ohne Constructor; `ShouldCheckMemberKind` lässt Konstruktoren nur über `kind=method`/`all` durch
- Ansatz: `DeadCodeKindFilter.Constructor` plus Parse-Wert `constructor`, oder Konstruktoren in der Zusammenfassung unter `method` führen.

**Completeness „kein Read/Grep“ vs. `ask_user`** (`degraded`)
- Pfad: `src\AiNetLinter\Mcp\McpSufficiencyHints.cs`; `FindDeadCodeTool.cs`
- Symbol: `McpSufficiencyHints.Append`; `FindDeadCodeTool.ExecuteAsync` hängt den Hinweis an, sobald nicht trunkiert; `BuildScanResult` setzt immer `RecommendedNextAction.Action = "ask_user"`
- Ansatz: Sufficiency-Hinweis nicht anhängen, solange die Aktion `ask_user` ist; oder tool-spezifischer Text: Heuristik, Datei/Attribute nachlesen.

**FP `[ModuleInitializer]`-Klasse** (`degraded`)
- Pfad: `src\AiNetLinter\Mcp\Tools\DeadCode\DeadCodeWhitelist.cs`
- Symbol: `HasWhitelistedAttribute` prüft nur `symbol.GetAttributes()`; `ModuleInitializerAttribute` steht in `WhitelistedAttributeNames`, gilt damit für `Initialize()`, nicht für den enthaltenden Typ
- Ansatz: Typ whitelisten, wenn ein Member `ModuleInitializerAttribute` trägt (`INamedTypeSymbol.GetMembers()` + `GetAttributes()`). Optional `IMethodSymbol.MethodKind == StaticConstructor` bleibt wie bisher.

**FP `JsonConverter.Read`/`Write`** (`degraded`)
- Pfad: `src\AiNetLinter\Mcp\Tools\DeadCode\FindDeadCodeScanner.cs`
- Symbol: `HasReferencedInterfaceOrOverrideAsync` (Base-`FindReferencesAsync` findet STJ-Reflection nicht); `DetermineLimitsApplies` setzt `jsonSerializer` nur bei `IPropertySymbol`
- Ansatz: Overrides von `JsonConverter`/`JsonConverter<T>` (`INamedTypeSymbol.InheritsFrom` / OriginalDefinition) whitelisten oder `jsonSerializer` auf diese Methoden legen und Confidence `low`. Roslyn: `IMethodSymbol.OverriddenMethod.ContainingType`.

**FP Public-API (`PluginName`) / Enum-Member** (`degraded`)
- Pfad: `FindDeadCodeScanner.cs`
- Symbol: `ClassifyConfidence` (Public → `low`, bleibt in der Liste); `DetermineLimitsApplies` (`publicApiSurface`/`reflection`); Enum-Felder sind `IFieldSymbol`, ohne Extra-Limit
- Ansatz: Enum-Member (`IFieldSymbol.ContainingType.TypeKind == Enum`) analog Serializer/Dispatch in `limits` oder Default-Liste. Public bleibt `low`, aber Default `private_internal` beibehalten.

**FP `Covers*` / `typeof`-Sentinels HIGH** (`degraded`)
- Pfad: `DeadCodeWhitelist.cs`; `FindDeadCodeScanner.cs`
- Symbol: `ClassifyConfidence` (Private → `high`, unabhängig von Markern); Reason-Text behauptet „ohne Framework-Marker“, prüft sie nicht
- Ansatz: Feld-Initializer `TypeOfExpressionSyntax` / Name `Covers*` / `EnableTestSentinel` whitelisten. HIGH nur nach tatsächlichem Marker-Check, nicht allein aus `Accessibility.Private`.

**FP `[ComponentRegistration]` / interne Handler-Member** (`degraded`)
- Pfad: `DeadCodeWhitelist.cs`; `FindDeadCodeScanner.cs`
- Symbol: `WhitelistedAttributeNames` ohne `ComponentRegistrationAttribute`; `DetermineLimitsApplies` kennt `aspNetRouting`/`di` in `DeadCodeLimits.DefaultLimits`, weist sie den Treffern aber nicht zu
- Ansatz: Attributnamen `ComponentRegistrationAttribute` (und analog Framework-Handler) whitelisten; bei Treffer `di`/`aspNetRouting` in `limitsApplies`. Typ-Attribute auf die Member vererben (`ContainingType.GetAttributes()`).

**Production-HIGH faktisch 0 durch `InternalsVisibleTo`** (`wish` / `degraded`)
- Pfad: `FindDeadCodeScanner.cs`
- Symbol: `CheckInternalsVisibleTo` (`InternalsVisibleToAttribute` an `IAssemblySymbol`); `ClassifyConfidence` stuft dann jedes `internal` auf `low`
- Ansatz: In der Zusammenfassung ausweisen, dass IVT Production-HIGH leert; oder HIGH für `internal` ohne eigene Framework-Marker erlauben und IVT nur als `limitsApplies` führen.

**`mode=locals` nur Compiler-Diagnosen** (`wish`)
- Pfad: `src\AiNetLinter\Mcp\Tools\DeadCode\FindDeadCodeDiagnosticsScanner.cs`
- Symbol: `ScanProjectDiagnosticsAsync` (`compilation.GetDiagnostics`, IDs `CS0169`/`CS0414`/`IDE0051`/`IDE0052`)
- Ansatz: Beschreibung an Diagnosen koppeln. Zusätzliche ungenutzte Locals nur über `DataFlowAnalysis`/`AnalyzeDataFlow` — teuer, eigenes Scan-Budget.

**Schema ohne Enums / Aliase nur im Fließtext** (`friction`)
- Pfad: `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs`
- Symbol: `AddFindDeadCode` (Parameter `string? accessibility` …); `FindDeadCodeDescription`
- Ansatz: Enum-Typen oder Schema-Constraints; `kind=constructor` dokumentieren. Kein Roslyn nötig.
