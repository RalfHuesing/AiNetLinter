# Finding: `get_server_health`

Audit-Stand: 2026-09-07. Nur Schema-Lookup (`GetDynamicTools` namespace=`user-AiNetLinter` toolName=`get_server_health`) plus Live-Calls dieses Tools. Kein anderes MCP-Tool, kein Build, kein Test.

## 1 Schema-Kurzfazit

Beschreibung steuert den Call **weitgehend korrekt**: ohne Target aggregieren; `targetType`+`targetPath` paarweise für `project` (absoluter Root) oder `assembly` (`.dll`/`.exe`); `includeDiagnostics`/`maxDiagnostics` für Samples; `includeSessions`/`maxSessions` für Sessionliste (Default unterdrückt Details, `maxSessions` serverseitig 50).

JSON-Schema-Lücken (Agent muss die Prosa lesen):

| Parameter | Schema | Bemerkung |
|---|---|---|
| `targetType` | `string\|null`, Default `null` | Kein Enum (`project`/`assembly` nur in Beschreibung/Fehlerhint). |
| `targetPath` | `string\|null`, Default `null` | Paarzwang steht **nicht** im Schema (`required` leer, keine `dependentRequired`). |
| `includeDiagnostics` | `boolean`, Default `false` | |
| `maxDiagnostics` | `integer`, Default `20` | |
| `includeSessions` | `boolean`, Default `false` | |
| `maxSessions` | `integer`, Default `20` | Beschreibung: Cap 50. |

Irreführung: Beschreibung verspricht, `includeDiagnostics=true` liefere Diagnose-Samples. In der **Aggregation ohne** `includeSessions` erscheinen **keine** Samples, nur der Zähler `Diagnosen gesamt`. Samples kommen erst mit `includeSessions=true` oder am zielgebundenen Assembly-Call.

## 2 Calls

Alle Calls schnell (Subsekunde–wenige Sekunden, kein Timeout). Server: Version `1.0.173`, Mode `daemon`, PID `10328`, Profil `cursor`, `connectionId: 3`.

| # | Absicht | Argumente | Ergebnis | Größe / Truncation |
|---|---|---|---|---|
| 1 | Aggregation ohne Target | `{}` | Erfolg. 1 Projekt `Loaded` (Platform-`slnx`, Rules unter `Tests.Logic\AiNetLinter\rules\platform-default.rules.json`). Assembly-Sessions: 2, Details unterdrückt, `partial=2`, `Diagnosen gesamt: 51`. | ~35 Zeilen, kompakt. |
| 2 | Projektziel | `targetType=project`, `targetPath=C:\Workspace\Sample.Project` | Erfolg. `Projekte (1)` wie Call 1. `Assembly-Sessions (0)` — Assemblys werden ausgeblendet. | ~28 Zeilen. |
| 3 | Assemblyziel extern | `targetType=assembly`, `targetPath=C:\ExternalAssemblies\Version-9\Example.External.Process.dll` | Erfolg. `Projekte (0)`. Session: `LoadState/Vollständigkeit: partial`, `Origin: decompiled`, `Generation: 6`, Labels korrekt (`Lock-Status: released`, `Lease-Status: bounded`, `Cleanup-Status: not-observed`, `Fehlerphase: decompilation`, `Nächste Aktion: Keine Aktion erforderlich.`). Hash + `GeneratedPath` unter `C:\Daten\Tools\AiNetLinter-win-x64\cache\asm.cursor\…`. `Confidence: medium`. | ~30 Zeilen. `Diagnosen: 0 von 111 (gekürzt)` **ohne** Samples. |
| 4 | Aggregation + Diagnostics | `includeDiagnostics=true` | Formal Erfolg, **identisch** zu Call 1 (gleiche Zähler, keine Sample-Liste). | ~35 Zeilen. Beschreibung verfehlt. |
| 5 | Projekt + Diagnostics | wie Call 2 plus `includeDiagnostics=true` | Identisch zu Call 2. Keine Projekt-Diagnosen, keine Samples. | ~28 Zeilen. |
| 6 | Assembly + Diagnostics | wie Call 3 plus `includeDiagnostics=true` | Labels weiter korrekt. `Diagnosen: 20 von 111 (gekürzt)` — Default `maxDiagnostics=20` greift. Samples: CS0234/VB-Refs, 128-Assembly-Cap, Polly/SimpleInjector/netstandard Versionsmismatch, fehlende extern-/BCL-Refs. | ~55 Zeilen, Samples gekürzt (`…`). |
| 7 | Fehler: nur `targetType` | `targetType=project` | `[ERROR]: INVALID_ARGUMENT: Der Parameter 'targetPath' ist erforderlich.` plus Hint auf Paar `project`/`assembly` + absoluter Pfad. | 2 Zeilen. |
| 8 | Fehler: unsinniger Projektpfad | `targetType=project`, `targetPath=C:\DoesNotExist\NoSuchProject` | `[ERROR]: PROJECT_NOT_INITIALIZED` für den Pfad. Hint: ersten Call senden, Server lege Key lazy über `ainetlinter.project.json` an. | 3 Zeilen. Health sollte nicht zur Neu-Integration auffordern. |
| 9 | Fehler: nur `targetPath` | `targetPath=<Platform-Root>` | `[ERROR]: INVALID_ARGUMENT: Der Parameter 'targetType' ist erforderlich.` gleicher Hint wie Call 7. | 2 Zeilen. |
| 10 | Aggregation + Sessions | `includeSessions=true` | Sessionliste erscheint. **Feldlabels verrutscht** gegenüber Call 3: `Daemon-Profil: released`, `Lock-Status: bounded`, `Lease-Status: not-observed`, `Cleanup-Status:` = 1766-Diagnosen-Fließtext, `Fehlercode: decompilation`, `Fehlerphase:` = CS0234-Dump, `Fehlerursache: Keine Aktion erforderlich.` | ~50 Zeilen. `Diagnosen: 0 von 22 (gekürzt)` — Nenner **22**, nicht 111. |
| 11 | Aggregation + Sessions + Diagnostics | `includeDiagnostics=true`, `includeSessions=true` | Samples erscheinen (`20 von 22`). Label-Shift **reproduzierbar**. | ~70 Zeilen. |
| 12 | `maxDiagnostics` | Assembly + `includeDiagnostics=true`, `maxDiagnostics=3` | `Diagnosen: 3 von 111 (gekürzt)` — Limit wirkt. | ~35 Zeilen. |
| 13 | `maxSessions` | `includeSessions=true`, `maxSessions=1` | Marker `Sessiondetails: 1 von 2 (gekürzt: maxSessions)`. Label-Shift bleibt. | ~45 Zeilen. |
| 14 | `maxDiagnostics=1` + Sessions | `includeDiagnostics=true`, `includeSessions=true`, `maxDiagnostics=1` | Pro Session `1 von 22` bzw. `1 von 29` (zweite Session `Example.External.Core.dll`). | ~65 Zeilen. |

**Latenz:** alle Calls schnell. Sessionzahl/Daemon-Keys schwanken (parallel andere Audit-Calls): zuerst 1–2 Projekte inkl. EAST, später nur Platform; Assembly-Sessions 1–6.

## 3 Verdict

**Teilweise wie beschrieben.** Daemon-/Projekt-Health (Version, PID, `LoadState`, Solution, Rules-Pfad, Uptime, Refreshes) und der Paarzwang `targetType`/`targetPath` stimmen. Assembly-Ziel liefert brauchbare Dekompilat-Metadaten.

Abweichungen:

- `includeDiagnostics=true` in der Aggregation ohne `includeSessions` ist wirkungslos (Call 4 vs. Beschreibung).
- `includeSessions`-Formatter vertauscht Labels/Werte (Call 10/11/13/14 vs. korrektem zielgebundenem Call 3/6).
- Diagnose-Nenner 111 (zielgebunden) vs. 22 (Sessionliste) für dieselbe DLL.
- `Diagnosen: 0 von 111 (gekürzt)` ohne Samples ist widersprüchlich.
- Unsinniger Pfad → `PROJECT_NOT_INITIALIZED` + Lazy-Init-Hint statt „Ziel unbekannt / nicht resident“.

## 4 Schwere

**degraded**

Happy Path (lebt der Daemon? ist Platform `Loaded`?) ist nutzbar. Sessiondetails und das Diagnostics-Flag sind formal ok, aber falsch beschriftet bzw. tot ohne zweites Flag — ein Agent liest Lock/Lease/Cleanup/Fehlerphase falsch und hält `includeDiagnostics` für defekt.

## 5 Nutzbarkeit

**Ja, nur mit Workaround.**

- Status „Server/Projekt geladen?“: Call ohne Target oder `targetType=project`.
- Assembly-Zustand: **zielgebunden** `targetType=assembly` (korrekte Labels), nicht die aggregierte Sessionliste.
- Diagnose-Samples: am Assembly-Ziel `includeDiagnostics=true`; in der Aggregation nur zusammen mit `includeSessions=true`.
- `includeSessions`-Felder nicht wörtlich übernehmen (Shift).
- `PROJECT_NOT_INITIALIZED` bei Health **nicht** als Integrationsauftrag lesen.

## 6 Bugs+FP/FN

| Befund | Art | Call | Bewertung |
|---|---|---|---|
| Aggregiertes `includeSessions`: Labels um eine/mehrere Spalten verschoben (`released` landet unter `Daemon-Profil` statt `Lock-Status`; Cleanup/Fehlerphase tragen Diagnosetext; „Keine Aktion erforderlich“ unter `Fehlerursache` statt `Nächste Aktion`). | Bug / falsch | 10, 11, 13, 14 vs. 3, 6 | Reproduzierbar. Agent-FP: Session „released“ als Profil, Diagnosetext als Cleanup. |
| `includeDiagnostics=true` ohne `includeSessions` in der Aggregation liefert keine Samples. | Vertrag / FN | 4 vs. 11 | Beschreibung lügt; Flag wirkt tot. |
| Projektziel + `includeDiagnostics` zeigt nichts (auch keinen „0 Diagnosen“-Block). | FN / Lücke | 5 | Agent kann „keine Diagnosen“ nicht von „nicht gerendert“ unterscheiden. |
| `0 von 111 (gekürzt)` ohne Samples. | UX / irreführend | 3 | „gekürzt“ bei 0 Samples ist falsch. |
| Diagnosen-Nenner 111 vs. 22 für dieselbe BusinessOperation-Session. | Inkonsistenz | 6 vs. 11 | Welche Menge gilt? |
| Unsinniger Pfad → `PROJECT_NOT_INITIALIZED` + Lazy-`ainetlinter.project.json`. | Error-Shape / FP | 8 | Health-Check darf keine Neu-Integration vorschlagen. |
| JSON-Schema kodiert den Paarzwang nicht. | friction | — | Laufzeit fängt es ab (`INVALID_ARGUMENT`, Call 7/9). |
| `targetType` ohne Enum im Schema. | friction | — | Hint in Call 7 nennt die Werte. |

Ground Truth (kleine Stichprobe, kein Vollabgleich): Platform-Root existiert, `Sample.Project.slnx` und `platform-default.rules.json` unter `Tests.Logic\AiNetLinter\rules\` sind plausible Health-Fakten. externe Assembly-Pfad existiert; `partial`/`decompiled` passt zu fehlenden Refs (Polly 8 vs. 7 am Disk-Pfad, extern-Shared nicht auflösbar). Label-Shift ist intern widersprüchlich zur zielgebundenen Antwort, kein Datei-Vergleich nötig.

## 7 Token/IDs

- Aggregation ohne Sessions: klein (~30–40 Zeilen), agententauglich.
- `includeDiagnostics` am Assembly-Ziel: Default 20 Samples, Zeilen wachsen moderat; `maxDiagnostics` begrenzt zuverlässig.
- `includeSessions` + Default-20-Samples: noch akzeptabel; lange CS0234-Zeilen werden mit `…` gekürzt.
- **IDs:** keine Session-ID, keine Symbol-IDs, kein StructuredContent sichtbar. Folge-Calls nur über denselben absoluten `targetPath` (Projektroot bzw. DLL-Pfad). Hash/`GeneratedPath` sind Cache-Zeiger, keine Tool-IDs.
- Truncation-Marker vorhanden: `Diagnosen: N von M (gekürzt)`, `Sessiondetails: 1 von 2 (gekürzt: maxSessions)`.

## 8 Roslyn-Wünsche

1. **Ein Formatter** für Assembly-Sessionzeilen (Aggregation = zielgebunden): dieselben Property-Namen, keine Extra-Felder, die die Zuordnung verschieben.
2. `includeDiagnostics=true` in der Aggregation **ohne** `includeSessions`: entweder begrenzte Samples unter dem Zähler **oder** explizit „Samples nur mit `includeSessions` / Assembly-Ziel“.
3. Einen Diagnosen-Nenner (Session vs. Compilation) dokumentieren und überall gleich verwenden.
4. `0 von N (gekürzt)` nur wenn wirklich Samples weggelassen wurden; sonst `0` ohne „gekürzt“.
5. Health bei unbekanntem Pfad: `TARGET_NOT_RESIDENT` / `PATH_NOT_FOUND`, **kein** `PROJECT_NOT_INITIALIZED` und kein Lazy-Init-Hint.
6. Schema: `targetType` Enum, `dependentRequired` für das Paar; `maxSessions`-Cap (50) im Schema erwähnen.
7. Optionale stabile `sessionId` für Folgetools (Assembly-Graph), rein als Handle — kein LLM.

## 9 Phase 3

Welle 3: AiNetLinter-Pfad/Symbol + Ansatz (read-only `C:\Daten\Entwicklung\Ralf\AiNetLinter`). Diagnosen sind Roslyn-`Diagnostic`-Texte der Decompiler-Compilation plus Referenzauflösung; Session-Lebenszyklusfelder sind das nicht.

### Label-Shift aggregiertes `includeSessions` vs. zielgebundenem Assembly-Call

- **Pfad:** `src\AiNetLinter\Mcp\Assemblies\Analysis\Coordinators\AssemblyAnalysisHealthSnapshotProvider.cs`; Record `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisSessionModels.cs`; Mapping `src\AiNetLinter\Mcp\Tools\ServerMaintenance\Projection\AssemblyHealthProjection.cs`; Ausgabe `src\AiNetLinter\Mcp\Tools\ServerMaintenance\GetServerHealthFormatter.cs`
- **Symbol:** `AssemblyAnalysisHealthSnapshotProvider.CreateCompletedSnapshot` (positionale Args); `AssemblyAnalysisHealthSnapshot` (zusätzliches Feld `AnalysisOrigin` zwischen `Diagnostics` und `DaemonProfile`); `AssemblyHealthProjection.FromSnapshot` vs. `FromLease`; `GetServerHealthFormatter.AppendAssemblyHeader` / `AppendOptionalAssemblyValue`
- **Roslyn-Ansatz:** Snapshot-Ctor ist um eins verschoben: `daemonProfile` landet in `AnalysisOrigin`, `"released"` in `DaemonProfile`, erste Compilation-Diagnose in `CleanupStatus`/`ErrorPhase`. `FromLease` setzt Lock/Lease/Cleanup/ErrorPhase/NextAction per Name und lässt `DaemonProfile`/`ErrorCode`/`ErrorCause` leer — deshalb Call 3 korrekt, Call 10 verschoben. Ansatz: `CreateCompletedSnapshot` mit **benannten** Argumenten spiegeln zu `FromLease`; Compilation-Diagnosen nur in `Diagnostics`, nicht in Error*-Feldern. Ein Mapping, zwei Aufrufer. Formatter bleibt dann korrekt, ohne Label-Hack.

### `includeDiagnostics=true` ohne `includeSessions` in der Aggregation wirkungslos; Projektziel ohne Diagnoseblock

- **Pfad:** `src\AiNetLinter\Mcp\Tools\ServerMaintenance\GetServerHealthResponseBuilder.cs`; Routing `src\AiNetLinter\Mcp\Tools\ServerMaintenance\GetServerHealthTool.cs`; Beschreibung `src\AiNetLinter\Mcp\Registration\ServerMaintenanceToolRegistrations.cs`
- **Symbol:** `GetServerHealthResponseBuilder.SelectShownAssemblies` (`!targeted && !IncludeSessions` → `null`); `AppendAssemblyText` → nur `AppendAssemblyAggregate` (Zähler, keine Samples); `GetServerHealthTool.ExecuteDaemonProjectAsync` / Projektzweig mit `Array.Empty<AssemblyHealthEntry>()`; `GetServerHealthDescription`
- **Roslyn-Ansatz:** Samples stecken schon in `AssemblyHealthProjection.Project(..., includeDiagnostics, …)` (`AssemblyAnalysisResponseLimits.ProjectDiagnostics` auf Compilation- + Transitivtexte). Sie werden nur nicht gerendert, solange `shownAssemblies` null ist. Ansatz: bei `includeDiagnostics` und Aggregation ohne Sessions begrenzte Samples unter „Diagnosen gesamt“ aus den bereits projezierten Einträgen, **oder** Description/Schema: Samples nur mit `includeSessions` / `targetType=assembly`. Projektziel: Assemblies nicht als `(0)` täuschen oder denselben Aggregate-Zähler plus Hint.

### Diagnosen-Nenner 111 (zielgebunden) vs. 22 (Sessionliste)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\ServerMaintenance\Projection\AssemblyHealthProjection.cs`; Limits `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisResponseLimits.cs`
- **Symbol:** `FromLease` (`lease.Context.Diagnostics` + `TransitiveDiagnostics: lease.ReferenceExpansionDiagnostics`); `FromSnapshot` (nur `context.Diagnostics`, keine Expansion); `Project` → `ProjectDiagnostics(root, transitive)`
- **Roslyn-Ansatz:** 111 = Session-Compilation-Diagnosen plus Referenz-Resolve-Diagnosen (Lease inkl. `ExpandReferencesAsync`). 22 = nur `context.Diagnostics` im Snapshot ohne Expansion. Einen Quellwert: Snapshot dieselben beiden Listen führen wie das Lease, `TotalCount` immer `ProjectDiagnostics(root, transitive).TotalCount`.

### `0 von 111 (gekürzt)` ohne Samples

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisResponseLimits.cs`; `src\AiNetLinter\Mcp\Tools\ServerMaintenance\GetServerHealthFormatter.cs`; Aufruf `AssemblyHealthProjection.Project`
- **Symbol:** `WithoutSamples` (ShownCount=0, `Truncated` bleibt); `Project` bei `includeDiagnostics=false`; `GetServerHealthFormatter.AppendAssemblyDiagnostics`
- **Roslyn-Ansatz:** `CreateSummary` setzt `Truncated`, sobald `diagnostics.Count > limit` — nach `WithoutSamples` bleibt das Flag. Ansatz: `Truncated` nur wenn `ShownCount > 0` und Samples weggelassen wurden; sonst `Diagnosen: 0` ohne „gekürzt“.

### Unsinniger Pfad → `PROJECT_NOT_INITIALIZED` + Lazy-Init-Hint

- **Pfad:** `src\AiNetLinter\Mcp\Tools\ServerMaintenance\GetServerHealthTool.cs` (nicht die Loader-Vorlage)
- **Symbol:** `GetServerHealthTool.ProjectNotInitialized`
- **Roslyn-Ansatz:** nicht anwendbar. Health ist Lookup residenter Keys (`FindProjectSnapshot`), kein Bootstrap. Ansatz: eigener Code `TARGET_NOT_RESIDENT` / `PATH_NOT_FOUND`, Hint ohne `ainetlinter.project.json`. `ProjectDefinitionLoader.NotInitializedTemplate` hier nicht wiederverwenden.

### JSON-Schema ohne Paarzwang / ohne `targetType`-Enum

- **Pfad:** `src\AiNetLinter\Mcp\Registration\ServerMaintenanceToolRegistrations.cs`; Laufzeit `src\AiNetLinter\Mcp\AnalysisTargetResolver.cs`
- **Symbol:** `AddGetServerHealth` (`string? targetType = null, string? targetPath = null`); `AnalysisTargetResolver.ResolveOptional`; `GetServerHealthDescription` (Prosa-Paarzwang, Cap 50)
- **Roslyn-Ansatz:** nicht anwendbar. Schema entsteht aus dem Lambda (`McpServerTool.Create`). Ansatz: Enum/`dependentRequired` im Descriptor (oder SDK-Attribute); `maxSessions`-Cap (`GetServerHealthTool.MaxSessions`) im Schema erwähnen. Laufzeit in `ResolveOptional` ist bereits korrekt.

### Optional: stabile `sessionId`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\ServerMaintenance\GetServerHealthModels.cs`; Snapshot-Key in `AssemblyAnalysisHealthSnapshotProvider`
- **Symbol:** `AssemblyHealthEntry`; `CreateCompletedSnapshot` (`entry.CanonicalPath` / Registry-Key)
- **Roslyn-Ansatz:** Handle = kanonischer DLL-Pfad plus `Generation`/`ContentHash` aus der Decompiler-Session (`AssemblyOrigin`), kein LLM. Folgetools bleiben `targetType=assembly` + `targetPath`.
