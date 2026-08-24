---
status: done
type: step-result
task: 11_epic-projektregistry-und-daemon
step: 015
epic: EPIC-B
step_type: single
coded_by: coder
coded_by_model: stealth/ox-alpha (openrouter)
coded_by_model_knowledge_cutoff: nicht deklariert
coded_at: 2026-08-24T16:40:00+02:00
code_commit_hash: 7a1431d9
status_after: done
blocker_category: n/a
---

# Result Step 015: Task-weites Drift-Audit — Duplicates, Magic Values, Dead Code

## Zusammenfassung

Alle drei Audit-Werkzeuge wurden über eine direkte stdio-JSON-RPC-Session gegen die
frisch gebaute EXE (`--mcp-server`, `AINETLINTER_NO_DAEMON=1` — bewusst kein detached
Daemon, siehe TD-008) ausgeführt. Triage-Ergebnis: **13 Fund-Cluster im Produktionscode
fixiert** (6 Regel-ID-Duplikate auf bestehende `LinterRuleIds`, 5 CLI-Optionsnamen
zentralisiert, 2 identische symbolIdentifier-Hinweise als geteilte Konstanten),
**4 tote Symbole entfernt**, der einzige Duplicate-Cluster ist der bereits als TD-006
dokumentierte Test-Helper-Fall (bewusster No-op), und alle übrigen Magic-Value-Funde
sind kategorisiert als bewusste No-ops mit Begründung (Details unten). Keine neuen
TD-Einträge nötig — nichts Nicht-Gefixtes braucht einen verfolgten Folgeteil; zwei
systemische Beobachtungen stehen für den Kritiker unten. Verhalten ist unverändert
(reine Konstanten-/Struktur-Maßnahmen); Build 0/0, FastTests 1726/1726,
IntegrationTests 357/359 mit 2 klassifizierten TD-008-Kontaminationsausfällen
(isoliert jeweils 1/1 grün).

## Audit-Läufe und Triage je Werkzeug

### 1. `find_duplicates` (`scopeDir="src"`, `minTokens=20`, `similarityThreshold="exact"`, `mode="clone"`)

- **Fundanzahl:** 1 Cluster (3739 Methoden gescannt), Ergebnis laut Tool vollständig.
- **Fund:** `TrackingServerFactory.MinimalConfig()` (FastTests/Mcp/Projects/
  ProjectRegistryTestDoubles.cs:130) ≡ `TestConfigFactory.CreateEmpty()`
  (src/AiNetLinter.TestKit/TestConfigFactory.cs:12).
- **Entscheidung: No-op** — identisch mit dem in step-013 dokumentierten und als
  TD-006 geführten Befund (Test-/TestKit-Grenzen, Konsolidierung braucht eine
  Abhängigkeitsentscheidung). Kein neuer Code, kein mechanisches Dedupe.

### 2. `find_magic_values`

Läufe: Produktion `scopeFilter="src/AiNetLinter"`, `includeTests=false`,
`maxResults=3000` (**878 Einträge / 758 eindeutige Werte / 335 Dateien — vollständig,
keine Trunkierung**); Stichproben über `FastTests/Mcp/Daemon` (56 Treffer/9 Dateien)
und `Mcp/Projects` (44 Treffer/19 Dateien).

**Fixiert (13 der 56 mehrfach über Dateien verteilten Werte):**

| Cluster | Stellen | Fix |
|---|---|---|
| Regel-IDs `"AvoidExcessiveMiddleMen"`, `"ForbiddenNamespaceDependency"`, `"BlazorRequireCssIsolation"` (2×), `"CSS_MaxCssLineCount"`, `"CSS_PreferScopedCss"`, `"CSS_MaxCssSelectorComplexity"` | MiddleManChecker, NamespaceCouplingChecker, UiFileSeparationChecker, CssAnalyzer, PatternCatalog | Bindung an bestehende `LinterRuleIds`-Konstanten (vom Regelwerk explizit vorgesehen). Der private `PatternCatalog`-Const samt Kommentar („hat keine LinterRuleIds-Konstante") war faktisch falsch — die Konstante existiert seit je — und wurde entfernt |
| CLI-Optionsnamen `"--daemon-start"`, `"--mcp-log"`, `"--mcp-project-ttl-minutes"`, `"--mcp-max-projects"`, `"--mcp-daemon-idle-exit-minutes"` | CliOptionFactory (Definition) ↔ ThinClientLauncher:61–65 (Spawn) | Konstanten in `CliOptionFactory`; Fabrik und detached Spawn nutzen dieselben Namen — Umbenennungsdrift zwischen Parsing und Daemon-Spawn ausgeschlossen |
| symbolIdentifier-Hinweise (je 1 Satz exakt doppelt) | GetCallTreeTool + FindReferencesTool bzw. GetSymbolBodyTool + MetricsLookupTool | `McpToolResults.SymbolIdentifierHint` / `.SymbolIdentifierBatchHint` |

**Bewusste No-ops (43 weitere Multi-Datei-Werte + alle Einzeltreffer), gruppiert:**

- *Zahlen mit lokaler Default-Semantik* (~19 Werte: 1, 2, 3, 4, 5, 10, 20, 50, 60,
  100, 150, 500, 1024, 4096, …): identische Zahl, unterschiedliche Bedeutung je
  Scanner/Default (maxResults-, TTL-, Puffer-Größen). Eine gemeinsame Konstante wäre
  Scheinkopplung. Die Hotspot-Schwellwerte 0.8/0.95 sind bereits `const` und laut
  XML-Doc in `GetHotspotsScanner` **bewusst** aus `HotspotMapBuilder` dupliziert
  (Schicht-Trennung Maps ↔ Mcp.Tools); `DuplicateDetectionModels` nutzt dieselben
  Zahlen für andere Semantik (Similarity-Buckets).
- *Nachrichten-/Schematext-Fragmente* („Die Klasse '", „ · Compound-Suppression
  inaktiv: ", „ Verstoesse in ", „ [Ignore-Suppressions: ", „'Lost in the Middle'…",
  „AI-Context-Footprint"-Label): Textbausteine ohne Vertragsscharfkante.
- *Registry-Taxonomie* (`agent-context`, `agent-resilience`, `test-coverage`,
  `csharp-idiom`, `aspnet-binding`, `control-flow`, `suffix-match`): deklarative
  Intent-/Modus-Werte in Regeldefinitionen plus deren Anzeige-Ordnung; öffentlich
  dokumentiertes Config-Vokabular, keine driftgefährdete Logik.
- *Kind-/Endungs-Literale* (`.css`, `.razor.css`, `css`, `class`, `Klasse`, `klasse`,
  `Interface`, `.AssemblyAttributes.cs`, `---`): konventionelle Literale in
  unabhängigen Subsystemen.
- *Toolname `get_class_structure`* (Registrierung ↔ Overview-Tabelle): codebase-weites
  Muster — ALLE Toolnamen liegen als Literale an Registrierung und Overview-Tabelle;
  nur dieser eine wurde geflaggt. Einzelfix wäre Inkonsistenz, Gesamtkonsolidierung
  (24+ Namen) ist eine eigene Entscheidung → Beobachtung unten.
- *Kategorien ohne Handlungsbedarf:* security_candidates (48×, False Positives wie
  XML-Attribut `publicKeyToken`, „Token-Verbrauch"-Texte, Typnamen), localization_
  candidates (13× deutsche Ausnahmen-/Hinweistexte — CLI ist bewusst deutsch, keine
  resx-Infrastruktur), nameof_candidates (31×, lokal auflösbar, kein Gewinn),
  config_candidates (3× = die URL-Schema-Liste `http(s)/ftp://` IM Detector selbst —
  selbstreferenzielle Klassifikationsdaten).
- *Test-Stichproben* (Daemon-/Registry-Testordner): testlokale Identifikatoren und
  Temp-Präfixe („exe-1", „daemon-host-", „project-def-loader-*") — bewusste
  Test-Lokalität (Eindeutigkeit je Test), kein Makel, nicht dedupliziert.

### 3. `find_dead_code` (`scopeFilter="src/AiNetLinter"`, Defaults, `includeTests=false`)

- **Fundanzahl:** 4 Symbole (0 high, 4 low confidence); 639 gescannt.
- **Entscheidung: alle 4 gefixt (entfernt)** nach manueller Referenzprüfung
  repo-weit (Solution + Tests + Doku): je **null Referenzen**.
  1. `DaemonPipeEndpoint.ForCurrentUser()` (DaemonPipeTransport.cs:24)
  2. `DaemonProtocol.GetCurrentUserPipeName()` (DaemonProtocol.cs:38)
  3. `ServerInstructions.FitsBudget` (ServerInstructions.cs:41; inkl. nun ungenutztem
     `using System.Text`. `MaxUtf8Bytes` bleibt — Test-Budget-Referenz in beiden Suiten)
  4. `LinterErrorCodes.AmbiguousSolution` (LinterErrorCodes.cs:21)
- Das Tool empfiehlt ask_user vor Löschung (dynamische Bindung möglich) — im Repo
  ausgeschlossen (statische Kompilation Pflicht, ALC/Reflection-Load verboten,
  Richtlinien §2); alle vier sind `internal`.

## Geänderte Dateien

- `src/AiNetLinter/Core/Checkers/MiddleManChecker.cs` — Regel-ID auf `LinterRuleIds`
- `src/AiNetLinter/Core/Checkers/NamespaceCouplingChecker.cs` — dito
- `src/AiNetLinter/Core/Checkers/UiFileSeparationChecker.cs` — 2× dito (Suppression-Marker + Violation)
- `src/AiNetLinter/Web/CssAnalyzer.cs` — 3× CSS-Regel-IDs auf `LinterRuleIds` (+using)
- `src/AiNetLinter/Mcp/Tools/PatternDetect/PatternCatalog.cs` — falscher Kommentar +
  privater Duplikat-Const entfernt, Nutzung von `LinterRuleIds`
- `src/AiNetLinter/Cli/CliOptionFactory.cs` — 5 Optionsnamen-Konstanten, Fabrik nutzt sie
- `src/AiNetLinter/Mcp/Daemon/ThinClientLauncher.cs` — Spawn über die Konstanten (+using)
- `src/AiNetLinter/Mcp/McpToolResults.cs` — 2 Hint-Konstanten ergänzt
- `src/AiNetLinter/Mcp/Tools/CallTree/GetCallTreeTool.cs`, `.../SymbolGraph/FindReferencesTool.cs`,
  `.../GetSymbolBodyTool.cs`, `.../MetricsLookup/MetricsLookupTool.cs` — Hints auf Konstanten
- `src/AiNetLinter/Mcp/Daemon/DaemonPipeTransport.cs` — `ForCurrentUser()` entfernt
- `src/AiNetLinter/Mcp/Daemon/DaemonProtocol.cs` — `GetCurrentUserPipeName()` entfernt
- `src/AiNetLinter/Mcp/ServerInstructions.cs` — `FitsBudget` + ungenutztes using entfernt
- `src/AiNetLinter/Output/LinterErrorCodes.cs` — `AmbiguousSolution` entfernt

## Commit

- **Code-Commit-Hash:** `7a1431d9`
- **Message:** `refactor: Drift-Funde konsolidieren und Totcode entfernen [11_epic-projektregistry-und-daemon]`
  (Body mit Einzelliste + Refs: tasks/mcp-server-weiterentwicklung/11_epic-projektregistry-und-daemon/step-015)
- **Branch:** main · **Push:** nein (lokal)
- Doku-Commit: separater zweiter Commit (step-result + Plan-Status).

## Build-/Test-Output

Vollständiger Nicht-Stress-Stack genau einmal (nach allen Bereinigungen):

```
dotnet build                                                          → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress        → grün (1726/1726)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → 357/359 grün, 2 Fehler (Klassifikation s. u.)
```

Der timingabhängige EPIC-A-Lease-Test lief diesmal im Vollstack grün (in FastTests
enthalten). Stress nie ausgeführt.

### Klassifikation der 2 Integrationsfehler (Coder-Schritt 4a)

Beide Fehler sind **Umfeld-Kontamination der TD-008-Klasse, keine Code-Defekte**:

- `DaemonHostProcessContractTests.TwoDaemonProcessesOnOneEndpointRejectSecondAndReleaseLock`
- `DaemonHostMcpProcessContractTests.HostPipeHandshakeThenMcpInitializeListsToolsAndExitsIdle`

TRX-Nachweis beider Läufe: `OperationCanceledException` im Semaphore-Wait von
`DaemonProcessContractHarness.AcquireEndpointAsync` — beide Tests wurden beim Warten
auf das suite-weite Endpunkt-Gate gecancelt, statt fachlich zu scheitern. Ursache:
meine eigenen Diagnose-Zwischenläufe (gefilterter Daemon/ThinClient-Slice zur
Fehleridentifikation) ließen Daemons am benutzergebundenen Endpunkt zurück; ein
isoliert gestarteter Einzeltest scheiterte in 438 ms an genau diesen zurückgebliebenen
Prozessen (live beobachtet: 2 AiNetLinter.exe in tasklist), die sich dann per Idle-Exit
selbst beendeten. Nach Prozessfreigabe:

- isolierter Nachlauf Test 1: **1/1 grün**
- isolierter Nachlauf Test 2: **1/1 grün** (jeweils ohne zurückbleibende Prozesse)

Im selben Diagnose-TRX-Lauf grün und damit von meiner Diff-Fläche entlastet:
alle ThinClient-Contracts inkl. `NormalMcpServerPath_ConnectsThroughDaemon_…`
(echter Spawn über den geänderten Launcher) und der Zwei-Prozess-SharedWarmth-Lauf.

Gezielte Läufe während der Entwicklung (vor dem Vollstack): `dotnet build` 0/0;
FastTests Unit+Component 1726/1726.

## MCP-Quality-Gates

Der AiNetLinter-MCP-Server ist in dieser Subagent-Umgebung nicht als eingebettetes
Tool registriert; Gates via stdio-JSON-RPC gegen die gebaute EXE (wie step-014):

- Vor den Audit-Läufen „lädt noch": `get_server_health` abgefragt, Batch erst bei
  geladenem Server (Richtlinien §1) — beim ersten Versuch ohne Wartezeit antworteten
  alle fünf Aufrufe mit dem Loading-Hinweis; Wiederholung regelkonform nach Health-Poll.
- `find_duplicates`: 1/1 Cluster vollständig · `find_magic_values`: 878/878 Einträge
  (erster Lauf mit maxResults=500 war bei 500 gekappt und wurde mit 3000 wiederholt) ·
  `find_dead_code`: 4 Funde, vollständig.
- **Vor dem Code-Commit:** `get_violations` (Repo-Root): **0 Violations in 659
  Dateien** · `safeguard`: **10,00/10 (Threshold 8,00) — PASS**, 0 Top-Verstöße,
  676 Klassen.
- Hygiene: Session mit `AINETLINTER_NO_DAEMON=1` (kein detached Daemon); nach allen
  Sessions und vor dem Vollstack `tasklist`-Check — keine AiNetLinter-Prozesse.

## Abweichungen vom Plan

Keine inhaltlichen. Anmerkungen:

1. **Magic-Values-Production-Lauf doppelt gestartet:** der erste Lauf (maxResults=500)
   war bei 500 von 878 Einträgen gekappt; für die geforderte Vollständigkeitsprüfung
   wurde er mit maxResults=3000 wiederholt (gleicher Scope, gleiche Parameter sonst).
   Beide Rohdaten liegen der Auswertung zugrunde; ausgewertet wurde der vollständige.
2. **Diagnose-Zwischenläufe im Integrationsbereich:** zur Identifikation der zwei
   Vollstack-Ausfälle wurde der Daemon/ThinClient-Filter-Slice zweimal und die beiden
   Fehlertests je einmal isoliert gefahren. Das ist kein erneuter Vollstack (Vorgabe
   bleibt eingehalten: kompletter Stack genau einmal), aber mehr Integrationsläufe als
   strikt nötig — Folge der fehlenden TRX-Loggers im Vollstack-Lauf. Die daraus
   entstandene Kontamination wurde live beobachtet, beseitigt und ist oben dokumentiert.
3. **Keine neuen TD-Einträge:** alles Nicht-Gefixte ist als begründeter No-op
   dokumentiert oder bereits als TD-006 vorhanden; die beiden systemischen Punkte
   unten stehen als Beobachtungen für den Kritiker (TD-Anlage bleibt dessen Kanal).

## Beobachtungen (für den Kritiker)

- **Toolnamen doppelt gepflegt (systemisch):** Jeder MCP-Toolname liegt als Literal an
  der Registrierung UND in der Overview-Tabelle (`OverviewResourceRegistration`) sowie
  teils in Prosa-Workflow-Texten. Nur `get_class_structure` wurde vom Audit geflaggt,
  das Muster betrifft aber ~24 Tools. Eine zentrale Namensquelle (oder generierte
  Overview-Tabelle) würde Rename-Drift strukturell ausschließen — eigenständige
  Test-/Architektur-Entscheidung, nicht mechanisch.
- **Stale Doku-Zeile `AMBIGUOUS_SOLUTION`:** `Docs/agent-api.md` (Fehlertabelle)
  beschreibt den Batch-Fehlercode `AMBIGUOUS_SOLUTION` weiterhin; im Code existiert
  weder Emitter noch Referenz mehr (nur die jetzt entfernte Konstante). Entweder
  Doku-Zeile korrigieren oder Verhalten (wieder) implementieren — Dokumentations-
  Objektivität (Richtlinien §1) berührt; außerhalb dieses Steps (kein Fund der drei
  Werkzeuge, keine Doku-Sync-Pflicht laut Plan).
- **Falscher Bestandskommentar:** PatternCatalog behauptete, `AvoidExcessiveMiddleMen`
  habe keine `LinterRuleIds`-Konstante (stimmt seit Einführung der Konstante nicht
  mehr) und führte RuleRegistry.General als gleiches Beispiel an — dort steht das
  Literal ebenfalls noch (als Registry-Datenzeile, s. Taxonomie-No-op). Im Zug des
  Fixes entfernt; erwähnt, weil der Kommentar eine Weile aktiv irreführend war.
- **Audit-Werkzeug-Detail:** `find_magic_values` flaggt `get_class_structure`, aber
  keine der anderen ~23 ebenso duplizierten Toolnamen — vermutlich Whitelist der
  älteren Toolnamen im Classifier. Kosmetisch, erklärt aber, warum nur ein Paar
  sichtbar wurde.
- Eigene Prozess-Hygiene hat den Vollstack kurzzeitig gestört (siehe Klassifikation);
  für künftige Steps: TRX-Logger direkt im Gate-Lauf spart Diagnose-Reruns.

## Bekannte Unschärfen

- Die Dead-Code-Entfernung stützt sich auf Tool-Befund + manuelle Referenzsuche über
  das gesamte Repo (inkl. Tests/Doku). Reflection-basierte Bindung ist per Richtlinien
  §2 ausgeschlossen; ein Restrisiko externer Consumer besteht formal nicht (alle vier
  Symbole waren `internal`).
- Die No-op-Begründungen für Zahlen-Literale beruhen auf meiner semantischen Bewertung
  der jeweiligen Verwendungsstelle (Default-Werte je Scanner/Konfig). Der Kritiker kann
  einzelne davon anders gewichten — die vollständige Wertliste liegt in
  `%LOCALAPPDATA%/Temp/step015-audit/magic_production_full.json`.
- Die 2 Vollstack-Ausfälle sind als TD-008-Kontamination klassifiziert (isolierter
  Nachlauf entscheidet laut Plan — entschieden: grün); ein Wiederholungsverbot für den
  Vollstack verhindert einen kollektiven Grün-Nachweis, wie in step-013/014 bereits
  gehandhabt.

## Falls Status `blocked`

Nicht blockiert — Step vollständig umgesetzt.
