# Finding: Kombi `get_server_health` + Resources (`agent-guide`, `overview`, `rules`)

Audit-Stand: 2026-09-07. Schema-Lookup nur für `get_server_health`. Live sequentiell: Health ohne/mit Target, danach `FetchMcpResource` (server=`user-AiNetLinter`, kein `downloadPath`), plus gezielte Folgecalls zu Token, Diagnosen, Sessions und Fehler-URIs. Keine neue Integration, kein anderes Tool, kein Build, kein Test.

Projektroot: `C:\Workspace\Sample.Project`. Resource-URIs laut Auftrag URL-kodiert; ein Unencoded-Vergleich zusätzlich.

## 1 Schema-Kurzfazit

`get_server_health`: Beschreibung steuert den Call **richtig** — optionales Target-Paar, Default kompakt, `includeDiagnostics`/`includeSessions` mit serverseitigem Cap. JSON-Schema schwächer: `targetType` ohne Enum, Property-Beschreibungen fehlen, kein Hinweis auf die drei Resources.

Die **Kette** ist in keiner Oberfläche als Workflow beschrieben. Widersprüchliche Steuerung entsteht erst im Verbund:

- Health-Beschreibung: aggregieren oder gezielt Session zeigen; Default klein.
- `ainetlinter://agent-guide`: „einmalig, nur bei ausdrücklichem Integrationsauftrag“; enthält trotzdem die **gesamte** dauerhafte MCP-Regel plus Bootstrap plus Laufzeit-`command`.
- `ainetlinter://overview{?projectRoot}`: laut Guide Query absolut und URL-kodiert; Overview selbst verweist als **ersten** „Weiter“-Schritt auf `agent-guide` („Erstkontakt und Integration“), nicht auf Health.
- `ainetlinter://rules{?projectRoot}`: in Health/Overview **nicht** verlinkt; Guide erwähnt nur die URI-Form.

Agent-Guide und Overview behaupten `tools/list` als Schemaquelle. Health kennt die Resources nicht. Overview kennt Health und `rules` nicht.

## 2 Calls

Alle Calls **schnell** (kein Timeout). Größen grob nach sichtbarer Markdown-Antwort.

| # | Absicht | Argumente / URI | Größe / Completeness | Ergebnis |
|---|---|---|---|---|
| 1 | Health global, Default | kein Target | ~1,3k Zeichen, kompakt | Version **1.0.173**, **Mode: daemon**, Profil `cursor`, 1 Projekt **Loaded**, Config=`ainetlinter-rules.json`. **Assembly-Sessions (16)**: Details unterdrückt, Hint `includeSessions=true`. Status `complete=1, partial=15`. Diagnosen gesamt **37**. Kein Resource-Hint. |
| 2 | Health Projekt-Target | `project` + Root | ~1,2k, kompakt | Gleiches Projekt; **Assembly-Sessions (0)**. Kein Diagnoseblock, kein Hint auf Overview/Rules. |
| 3 | Resource agent-guide | `ainetlinter://agent-guide` | **groß** (~15–20k Zeichen): Bootstrap + vollständige `AiNetLinter-McpWorkflow`-Regel + Runtime-JSON | Warnung „nur bei Integrationsauftrag“, danach Dump der Always-on-Regel. Step 6: Prüfung via `get_server_health`. Query-Encoding für Overview/Rules dokumentiert. |
| 4 | Resource overview | `ainetlinter://overview?projectRoot=C%3A%5C…Platform` | ~0,5k, vollständig klein | **„laeuft als stdio-MCP-Server“**. Solution + Regeln-Pfad + Zuletzt genutzt. **Kein** LoadState, keine Sessions, keine Staleness. Weiter: **agent-guide**, `tools/list`, `get_impact`/`get_violations`. |
| 5 | Resource rules | `ainetlinter://rules?projectRoot=C%3A%5C…Platform` | ~8–10k (zwei Tabellen) | Aktive Regeln + Schwellwerte + deaktivierte Namen. Config-Pfad identisch zu Health. **„Projekt-Overrides: 1 Muster / Pfad-Overrides: 30 Muster“** ohne Inhalt. Kein Hint Health/Overview/`get_violations`. |
| 6 | Health Diagnosen ohne Sessions | `includeDiagnostics=true`, `maxDiagnostics=20`, kein Target | ~1,3k | **Keine Samples.** Assembly-Sessions plötzlich **(2)**, Diagnosen gesamt **44**. Flag ohne sichtbare Wirkung. |
| 7 | Health Sessions | `includeSessions=true`, `maxSessions=20` | ~4–5k | 2 externe Assemblies (`BusinessOperation`, `RecordEngine`), `LoadState: partial`, Origin decompiled, Profil **`released`**. `Fehlerursache: Keine Aktion erforderlich.` **Diagnosen: 0 von 22 (gekürzt)**. `Cleanup-Status`/`Fehlerphase` mit langen CS0234-Listen. |
| 8 | Health Sessions+Diagnosen | beide Flags true | ~8–10k | Dieselben 2 Sessions; **Diagnosen: 20 von 22**. Samples = fehlende Refs (Polly, SimpleInjector, `netstandard`, extern-Shared, WCF). Token-Last in Fehlertexten dupliziert (`Cleanup-Status` + Liste). |
| 9 | Overview ohne Query | `ainetlinter://overview` | Fehlerzeile | **`MCP resource not found`**. Kein Encoding-Beispiel, kein `PROJECT_NOT_INITIALIZED`. |
| 10 | Rules ohne Query | `ainetlinter://rules` | Fehlerzeile | Gleich: **not found**, kein Hint. |
| 11 | Health Projekt + Diagnosen | `project` + Root + `includeDiagnostics=true` | ~1,2k | Wieder **Assembly-Sessions (0)**, **keine Samples**. Flag tot am Projekt-Target. |
| 12 | Health Assembly-Target | `assembly` + `Example.External.Process.dll`, beide Flags | ~4k | Projekte **(0)**, 1 Session. Feldnamen **anders** als Call 7 (`Nächste Aktion` statt `Fehlerursache`; `Fehlerphase: decompilation`). Diagnosen **20 von 111** (nicht 22). |
| 13 | Overview unkodierter Pfad | `?projectRoot=C:\Daten\…Platform` | wie Call 4 | **Funktioniert.** URL-Encoding laut Guide **nicht** nötig. |
| 14 | Health `targetType` ohne Path | `targetType=project` | ~3 Zeilen | `INVALID_ARGUMENT`: `targetPath` erforderlich, Hint Paar `project`/`assembly`. |
| 15 | Overview unbekannter Root | encoded `C:\DoesNotExist\FakeProject` | Fehlerzeile | Wieder **`MCP resource not found`**. |
| 16 | Health unbekannter Root | `project` + `C:\DoesNotExist\FakeProject` | ~5 Zeilen | **`PROJECT_NOT_INITIALIZED`**, Hint: ersten Tool-Call senden, Key lazy über `ainetlinter.project.json`. |

## 3 Verdict

**Teilweise wie beschrieben** — jedes Stück einzeln oft formal ok, die **Kette steuert falsch**.

Stimmt: Health Default kompakt; Target-Paar erzwungen; `includeSessions` listet Sessions; `includeDiagnostics` füllt Samples **nur wenn Sessions sichtbar sind**; Overview/Rules mit Query liefern Status bzw. Regelkatalog; gleicher `ainetlinter-rules.json`-Pfad in Health, Overview und Rules; agent-guide nennt Health zur Prüfung nach Setup.

Abweichungen der Kombination:

1. **Modus-Lüge:** Health `Mode: daemon` vs. Overview „stdio-MCP-Server“ vs. Guide-Registrierung `--mcp-server`.
2. **Overview → agent-guide** als Erstkontakt auf einem **bereits Loaded**-Projekt. Guide selbst verbietet ungefragte Integration. Folge: unnötiger **Token-Dump** der Always-on-Regel.
3. **Health Projekt vs. global:** 16 bzw. 2 Assembly-Sessions global, **0** am Projekt-Target. Overview erwähnt Sessions gar nicht.
4. **Sessionzahl instabil** zwischen Call 1 (16) und Call 6 (2) in derselben Kette.
5. **`includeDiagnostics` allein** und **am Projekt-Target** ohne Samples; Schema verspricht Samples.
6. **Fehlerpfad-Bruch:** Health unbekannter Root → `PROJECT_NOT_INITIALIZED` + Hint. Overview/Rules ohne Query oder mit Fake-Root → generisches **not found** (kein URI-Beispiel, kein gleicher Fehlercode).
7. **Rules** blendet 31 Override-Muster aus; Schwellwerte wirken lösungweit (z. B. `MaxMethodLineCount` 60), obwohl Platform-Tests andere Limits haben.
8. **Keine Querverweise:** Health ↛ Overview/Rules; Overview ↛ Health/`rules`; Rules ↛ Folge-Tools außer einem Satz zu `find_duplicates` in der DuplicateCode-Zeile.
9. **Guide verlangt URL-Encoding**; unkodierter Windows-Pfad in Overview **ging** (Call 13).
10. Session-Felder zwischen Global-Liste und Assembly-Target **nicht dieselben Namen**; Diagnosen-Nenner 22 vs. 111.

## 4 Schwere

**`degraded`**

Nicht `broken`: Default-Health und kodiertes Overview/Rules antworten schnell und inhaltlich zum Loaded-Projekt. Nicht nur `friction`: Overview **lenkt** den Agenten in den teuren Bootstrap-Text, Health **versteckt** Assembly-Sessions hinter dem Projekt-Target, Rules **verschweigt** Overrides, und Resource-Fehlercodes **passen nicht** zu Health/`agent-guide`. Ein Agent kann daraus Integration anstoßen, Sessions für „leer“ halten oder Test-Limits falsch anwenden.

## 5 Nutzbarkeit

**nur mit Workaround**

- **Ja:** `get_server_health` ohne Target (Ist-Zustand, Version, Loaded). Kodiertes oder unkodiertes Overview nur als Mini-Bestätigung von Solution/Regeln-Pfad. Rules-Resource als **Katalog** aktiver Regelnamen, nicht als effektive Per-Projekt-Wahrheit.
- **Nein:** agent-guide als Folgeschritt nach Overview auf einem integrierten Projekt. Health mit nur `project`-Target als Aussage „keine Assemblies“. `includeDiagnostics` ohne `includeSessions`. Overview/Rules als Ersatz für Health-Fehlercodes.
- Workaround: Resources **nicht** nacheinander „zur Orientierung“ fetchen. `agent-guide` nur bei echtem `PROJECT_NOT_INITIALIZED`. Sessions über global Health + `includeSessions=true`. Overrides in der genannten `ainetlinter-rules.json` lesen (Call 5 sagt das, ohne Tool-Hint). Nicht neu integrieren.

## 6 Bugs+FP/FN

| Befund | Art | Call |
|---|---|---|
| Overview nennt stdio, Health nennt daemon | Widersprüchliche Steuerung | 1 vs. 4 |
| Overview „Weiter“ #1 = agent-guide trotz Loaded | Falscher Next-Call / Token-Falle | 4 → 3 |
| Projekt-Health Assembly-Sessions (0) bei global 16 bzw. 2 | FN / Scope-Lüge | 2, 11 vs. 1, 6 |
| Sessionzahl 16 → 2 ohne eigenen Assembly-Call | Instabile Completeness | 1 vs. 6–8 |
| `includeDiagnostics` ohne Sessions: 0 Samples | Beschreibung vs. Ist | 6, 11 |
| Sessions ohne `includeDiagnostics`: „0 von 22 (gekürzt)“ | Truncation ohne Inhalt | 7 |
| `Fehlerursache`/`Nächste Aktion`: „Keine Aktion erforderlich“ bei `partial` + Tausenden Decompiler-Diagnosen | Irreführend / FN für Agenten-Handlung | 7, 8, 12 |
| Overview/Rules fehlende Query oder Fake-Root: `MCP resource not found` statt `PROJECT_NOT_INITIALIZED` | Fehlerpfad-Bruch | 9, 10, 15 vs. 16 |
| Rules: 30 Pfad-Overrides unsichtbar, Limits wie Produktion | FN effektiver Config | 5 |
| Guide: Query URL-kodiert Pflicht; unkodiert geht | Überzogene Spezifikation | 3 vs. 13 |
| Diagnosen-Nenner 22 (Session-Liste) vs. 111 (Assembly-Target) | Inkonsistente Zählung | 8 vs. 12 |
| Daemon-Profil Header `cursor`, Session `released` | Widersprüchliche Session-Identität | 1 vs. 7 |
| Feldnamen Session-Block global ≠ Assembly-Target | Undokumentiertes Dual-Schema | 7 vs. 12 |
| Health-Hint bei unbekanntem Root: lazy Key via `ainetlinter.project.json` vs. Guide „keine ungefragte Integration“ | Steuerungskonflikt | 16 vs. 3 |

**Stichprobe intern (ohne zweites Tool):** Config-Pfad in Call 1/4/5 ist derselbe `ainetlinter-rules.json`-Pfad unter `Tests.Logic` — Overview/Health/Rules sind hier konsistent. Solution-`.slnx`-Pfad ebenfalls. Die 16→2-Sessions und die extern-Pfade stammen aus Health-Text, nicht aus einem zweiten Tool. `Example.External.Core.dll` (im Konzept als drittes Assembly-Ziel) war in Call 1 in der 16er-Menge implizit möglich, in Call 7 **nicht** mehr gelistet — kein Beweis, nur Verschwinden der Menge.

Leermenge Overview ohne Query ist **kein** „Projekt unbekannt“, sondern Cursor-„resource not found“. Health unterscheidet das (Call 16).

## 7 Token/IDs

| Oberfläche | Default-Last | Stress |
|---|---|---|
| Health ohne Flags | agententauglich klein | — |
| Health `includeSessions` | mittel; lange CS0234 in `Cleanup-Status` **und** `Fehlerphase` | Call 8 ~doppelt durch Diagnose-Samples, Cap 20 greift |
| Overview | sehr klein, gut | — |
| Rules | mittel (Tabellen), akzeptabel | Overrides fehlen statt zu fluten |
| agent-guide | **Flood**: Bootstrap + komplette Always-on-Regel | Jeder Overview-Folgeschritt wiederholt Host-Kontext, der in `.cursor/rules` schon liegt |

Keine Symbol-IDs, kein Cursor, kein StructuredContent in der Agent-Ansicht (nur Markdown). Stabile Anker: absoluter Projektroot, Solution-Pfad, `ainetlinter-rules.json`-Pfad, Assembly-Vollpfad, Hash, `GeneratedPath`.

**Fehlende Hints zum nächsten Call (Kern der Kombi):**

- Health Default → nicht `ainetlinter://overview`, nicht `ainetlinter://rules`, nicht „Diagnosen nur mit beiden Flags“.
- Overview → nicht Health, nicht Rules; **stattdessen** agent-guide (falsche Richtung) und `tools/list` (nicht die Cursor-`GetDynamicTools`-Oberfläche).
- Rules → nicht `get_violations`/`safeguard`; Overrides nur „siehe ainetlinter-rules.json“.
- Resource-404 → kein Beispiel `overview?projectRoot=<url-encoded absolut>`.
- agent-guide Step 6 → Health ja; Overview/Rules unerwähnt als Alltags-Status.

## 8 Roslyn-Wünsche

- Eine **Status-Wahrheit**: Overview als Alias/Subset von Health (Mode, LoadState, Sessionzahlen, letzter guter Zustand) oder Overview streichen und Health verlinken.
- Overview „Weiter“ auf Loaded-Projekt: `get_server_health`, `ainetlinter://rules`, `get_violations` — **nicht** Bootstrap. `agent-guide` nur bei `PROJECT_NOT_INITIALIZED`/`RULES_INVALID`.
- `agent-guide` in Bootstrap vs. Always-on-Regel **splitten** (zwei Resources oder Guide ohne Regel-Dump).
- Resource-Fehler denselben Code wie Health (`PROJECT_NOT_INITIALIZED`) plus Beispiel-URI.
- Projekt-Target: Assembly-Sessions nicht als `(0)` täuschen; Zähler + Hint `includeSessions` oder „Assemblies nur im Global-Health“.
- `includeDiagnostics` ohne Sessions: explizit „0 Samples, setze includeSessions“ statt still nichts.
- Session-Schema vereinheitlichen (Feldnamen, Diagnosen-Nenner, Profil `cursor` vs. `released`).
- `partial` + Decompiler-Flut: eine Zeile Cap („1766 Diagnosen, Sample N“) statt denselben CS0234-Block in drei Feldern; „Keine Aktion erforderlich“ nur wenn Completeness für Tool-Calls reicht.
- Rules-Resource: Override-Muster wenigstens als Pfad-Glob + abweichendes Limit listen, oder klar „nicht effektiv, lies ainetlinter-rules.json / `reload_config`“.
- Query-Encoding als Empfehlung, nicht als harte Pflicht, wenn unkodierte Absolute Pfade akzeptiert werden.
- Health-Footer: Resource-URIs mit bereits bekanntem Root (kodiert).

## 9 Phase 3 (AiNetLinter-Quellzeiger)

Nur Nicht-ok-Kettenbefunde. Pfade unter `C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\`.

1. **Modus-Lüge daemon vs. stdio vs. `--mcp-server`.**  
   Pfad: `Mcp\Tools\ServerMaintenance\Projection\DaemonHealthProjection.cs`; `Mcp\Registration\OverviewResourceRegistration.cs`; `Mcp\McpRegistrationInstructions.cs`.  
   Symbol: `DaemonHealthProjection.FromContext`; `OverviewResourceRegistration.BuildOverviewText`; `McpRegistrationInstructions.BuildRuntimeBlock`.  
   Ansatz: eine `Mode`-Zeichenkette aus `DaemonRuntimeContext.Mode` in Overview/Health/Guide; den Literal „stdio-MCP-Server“ in `BuildOverviewText` streichen.

2. **Overview „Weiter“ #1 = `agent-guide` auf Loaded-Projekt.**  
   Pfad: `Mcp\Registration\OverviewResourceRegistration.cs`; `Mcp\Registration\McpAgentGuideRegistration.cs`.  
   Symbol: `OverviewResourceRegistration.BuildOverviewText`; `McpAgentGuideRegistration.BuildGuideText`.  
   Ansatz: `BuildOverviewText` an `snapshot.Server.LoadState` koppeln: bei Loaded `get_server_health` / `ainetlinter://rules` / `get_violations`; `agent-guide` nur bei fehlendem Key. Guide in Bootstrap (`Docs/mcp-bootstrap.md`) vs. Always-on-Regel (`AgentRules/AiNetLinter-McpWorkflow.mdc`) nicht in einer Resource konkatenieren.

3. **Projekt-Health Assembly-Sessions (0).**  
   Pfad: `Mcp\Tools\ServerMaintenance\GetServerHealthTool.cs`; `Mcp\Registration\ServerMaintenanceToolRegistrations.cs`.  
   Symbol: `GetServerHealthTool.ExecuteAsync` (Zweig `options.ProjectRoot`); `GetServerHealthTool.ExecuteDaemonProjectAsync`.  
   Ansatz: nicht `Array.Empty<AssemblyHealthEntry>()` an den Builder geben; `assemblyRegistry.SnapshotsAsync()` mitzählen oder explizit „Assemblies nur im Global-Health, Zähler N“ schreiben.

4. **Sessionzahl 16 → 2 ohne eigenen Assembly-Call.**  
   Pfad: `Mcp\Assemblies\Analysis\AssemblyAnalysisRegistry.cs`; `Mcp\Assemblies\Analysis\Coordinators\AssemblyAnalysisHealthSnapshotProvider.cs`; `Mcp\Assemblies\Analysis\ExternalResourceRegistry.cs`.  
   Symbol: `AssemblyAnalysisHealthSnapshotProvider.GetSnapshotsAsync`; `AssemblyAnalysisRegistry` Eviction; `ExternalResourceRegistry.EvictLeastRecentlyUsedNoLock`.  
   Ansatz: Health-Zähler an ein Snapshot-Inventar binden, das Root-Sessions von Referenz-/MRU-Opfern trennt; Footer „N evicted“ statt stiller Schrumpfung.

5. **`includeDiagnostics` ohne Sessions / am Projekt-Target: 0 Samples.**  
   Pfad: `Mcp\Tools\ServerMaintenance\GetServerHealthResponseBuilder.cs`; `Mcp\Tools\ServerMaintenance\Projection\AssemblyHealthProjection.cs`.  
   Symbol: `GetServerHealthResponseBuilder.SelectShownAssemblies`; `AssemblyHealthProjection.Project`.  
   Ansatz: Samples nicht an `includeSessions` koppeln (`SelectShownAssemblies` liefert `null` wenn `!targeted && !IncludeSessions` → Aggregate ohne Samples). Entweder begrenzte Samples im Aggregate oder eine Zeile „0 Samples, setze includeSessions“.

6. **Sessions ohne `includeDiagnostics`: „0 von 22 (gekürzt)“.**  
   Pfad: `Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisResponseLimits.cs`; `Mcp\Tools\ServerMaintenance\GetServerHealthFormatter.cs`.  
   Symbol: `AssemblyAnalysisResponseLimits.WithoutSamples`; `GetServerHealthFormatter.AppendAssemblyDiagnostics`.  
   Ansatz: `WithoutSamples` muss `Truncated=false` setzen; Formatter „gekürzt“ nur wenn `ShownCount>0` und wirklich Samples weggelassen wurden.

7. **`Keine Aktion erforderlich` bei `partial` + Decompiler-Flut.**  
   Pfad: `Mcp\Assemblies\Analysis\Coordinators\AssemblyAnalysisHealthSnapshotProvider.cs`; `Mcp\Tools\ServerMaintenance\Projection\AssemblyHealthProjection.cs`.  
   Symbol: `AssemblyAnalysisHealthSnapshotProvider.CreateCompletedSnapshot`; `AssemblyHealthProjection.FromLease`.  
   Ansatz: `NextAction` an `AssemblySessionStatus` / Diagnosen-Count koppeln; „Keine Aktion“ nur bei Completeness, die Tool-Calls trägt. CS0234 nicht in `Cleanup-Status`, `Fehlerphase` und Sample-Liste dreifach ausgeben — ein Cap-Satz plus `ProjectDiagnostics`.

8. **Resource-404 vs. Health `PROJECT_NOT_INITIALIZED`.**  
   Pfad: `Mcp\Registration\ProjectResourceLease.cs`; `Mcp\Projects\ProjectToolCall.cs`; `Mcp\Tools\ServerMaintenance\GetServerHealthTool.cs`.  
   Symbol: `ProjectResourceLease.Execute`; `ProjectToolCall.GuardRequiredAbsoluteRoot`; `GetServerHealthTool.ProjectNotInitialized`.  
   Ansatz: fehlendes Query und unbekannter Root nicht als SDK-`McpException` werfen (Host macht daraus `MCP resource not found`). Denselben `LinterErrorFormatter`-Code wie Health liefern plus Beispiel-URI `overview?projectRoot=<absolut>`. Fake-Root: `FindSnapshot`/`Lease`-Fehlercode nicht verschlucken.

9. **Rules blendet Override-Muster aus.**  
   Pfad: `Mcp\Registration\RulesResourceFormatter.cs` (Gegenvorlage `Generators\AgentRulesGenerator.cs`).  
   Symbol: `RulesResourceFormatter.AppendProjectOverrides`; `AgentRulesGenerator.AppendProjectOverridesDelta`.  
   Ansatz: pro `Config.ProjectOverrides`/`PathOverrides` Glob + abweichendes Limit listen (wie der Generator), nicht nur „N Muster“.

10. **Guide: URL-Encoding Pflicht, unkodiert geht.**  
    Pfad: `Mcp\Registration\OverviewResourceRegistration.cs`; `Mcp\Registration\RulesResourceRegistration.cs`; `.agents\rules\AiNetLinter-McpWorkflow.mdc` (eingebettet via `McpAgentGuideRegistration`).  
    Symbol: `OverviewResourceRegistration.Register` (Description); `BuildCanonicalUri` (`Uri.EscapeDataString`).  
    Ansatz: Encoding als Empfehlung in Description/Guide; Handler akzeptiert bereits unkodierte Absolute Pfade — Spezifikation an das Ist angleichen.

11. **Diagnosen-Nenner 22 (Session-Liste) vs. 111 (Assembly-Target).**  
    Pfad: `Mcp\Tools\ServerMaintenance\Projection\AssemblyHealthProjection.cs`; `Mcp\Tools\ServerMaintenance\GetServerHealthTool.cs`.  
    Symbol: `AssemblyHealthProjection.FromSnapshot` (`context.Diagnostics`); `FromLease` (`Diagnostics` + `ReferenceExpansionDiagnostics` nach `ExpandReferencesAsync`).  
    Ansatz: einen Nenner: Root-Diagnosen **oder** Root+Transitive, in beiden Pfaden gleich; Target-Health nicht still `ExpandReferencesAsync` mitzählen, wenn Global-Health das nicht tut.

12. **Profil Header `cursor` vs. Session `released`.**  
    Pfad: `Mcp\Tools\ServerMaintenance\Projection\DaemonHealthProjection.cs`; `Mcp\Tools\ServerMaintenance\Projection\AssemblyHealthProjection.cs`.  
    Symbol: `DaemonHealthProjection.FromContext` (`snapshot.DaemonProfile`); `AssemblyHealthProjection.FromLease` (hart `LockStatus: "released"`, kein `DaemonProfile`).  
    Ansatz: Session-Profil aus demselben `DaemonRuntimeContext` wie der Header; `released` nur als `Lock-Status` belassen.

13. **Feldnamen Global-Liste ≠ Assembly-Target.**  
    Pfad: `Mcp\Tools\ServerMaintenance\GetServerHealthFormatter.cs`; `Mcp\Tools\ServerMaintenance\Projection\AssemblyHealthProjection.cs`.  
    Symbol: `GetServerHealthFormatter.AppendAssemblyHeader`; `FromSnapshot` setzt `ErrorCause`; `FromLease` setzt `NextAction`/`ErrorPhase`, lässt `ErrorCause` leer.  
    Ansatz: ein Mapping `AssemblyHealthEntry` — dieselben Labels in beiden Pfaden; leere Felder nicht als anderes Schema verkaufen.

14. **Health-Hint lazy `ainetlinter.project.json` vs. Guide „keine ungefragte Integration“.**  
    Pfad: `Mcp\Tools\ServerMaintenance\GetServerHealthTool.cs`; `Mcp\ServerInstructions.cs`; `Mcp\Registration\McpAgentGuideRegistration.cs`.  
    Symbol: `GetServerHealthTool.ProjectNotInitialized`; `ServerInstructions.Text`.  
    Ansatz: Hint auf `ainetlinter://agent-guide` und „nur bei Integrationsauftrag“, nicht „ersten Tool-Call senden, Key lazy anlegen“.

15. **Keine Querverweise Health ↛ Overview/Rules.**  
    Pfad: `Mcp\Tools\ServerMaintenance\GetServerHealthResponseBuilder.cs`; `Mcp\Registration\OverviewResourceRegistration.cs`; `Mcp\Registration\RulesResourceFormatter.cs`.  
    Symbol: `GetServerHealthResponseBuilder.BuildText`; `OverviewResourceRegistration.BuildOverviewText`; `RulesResourceFormatter.BuildMarkdown`.  
    Ansatz: Health-Footer mit `ainetlinter://overview?projectRoot=` und `…/rules?…` aus dem bekannten Root; Overview verweist auf Health und Rules; Rules auf `get_violations`/`safeguard`.
