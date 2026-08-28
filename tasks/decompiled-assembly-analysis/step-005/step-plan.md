---
status: open
type: step-plan
task: decompiled-assembly-analysis
step: 005
corrects: null
title: "Explizite Source-Solutions und Snapshot-Auflösung: Mapping, Identität und Session-Anbindung"
epic: EPIC-03
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-28T16:53:38+02:00
related_to: [step-004]
---

# Step 005: Explizite Source-Solutions und Snapshot-Auflösung: Mapping, Identität und Session-Anbindung

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-03` aus `roadmap.md` — explizite externe Source-Solutions
  und deterministische Snapshot-Auflösung als nächster offener Baustein nach
  dem abgeschlossenen EPIC-02-Fundament.
- **Konzept-Referenz:** `Konzept.md` §§ „Gitea-Register und wartungsarmes
  Mapping“, „Source-Auflösung vor der Dekompilation“, „Fingerprint und
  Cache-Key“, „Assembly-Session“, Phase 3 sowie die Teststrategie und
  Definition of Done für Mapping und Source-Snapshots.

## Aktueller Projektzustand (JIT-Kontext)

EPIC-02 ist durch `step-003` und die genehmigte Korrektur `step-004` im Code
vorhanden: `AssemblyAnalysisSession` liest PE-Metadaten metadata-only, erzeugt
immutable Decompilation-Generationen, validiert und veröffentlicht den
Assembly-Cache atomar, transportiert die echte PE-Identität und liefert
synthetische Roslyn-Snapshots. `AssemblyAnalysisContextFactory` und die
Assembly-Tools konsumieren diesen Vertrag weiterhin ohne Runtime-Laden.

Die Source-Seite existiert dagegen noch nicht. `AnalysisToolCall` dispatcht
Assembly-Ziele nur über einen Pfad-Callback, und
`AssemblyAnalysisToolSupport`/`AssemblyAnalysisContextFactory` erzeugen die
Assembly-Session aktuell pro Aufruf. Die MCP-Komposition in
`McpServerOptionsFactory`, `McpServerCommand` und `DaemonHostCommand` kennt nur
die bestehende `ProjectRegistry`; eine gemeinsame Source-Registry oder ein
Source-Resolver wird weder für direkte DLL-Ziele noch für Nachschlageaufrufe
aus einem Projekt übergeben.

Die Konfiguration ist ebenfalls noch getrennt: `ConfigLoader` liest
`rules.json`, `ProjectDefinitionLoader` liest ausschließlich den Vertrag aus
`ainetlinter.project.json`, und `LoggingConfigLoader` verarbeitet aus
`appsettings.json` nur den `Logging`-Abschnitt. `SourceFileCatalog` und
`SourceFileCatalogLoader` bieten bereits den wiederverwendbaren MSBuild-/Roslyn-
Ladepfad für eine vollständige Solution. Die vorhandene
`RoslynTestSolutionFactory`, `TestTempDirectory`- und MCP-TestKit-Infrastruktur
eignet sich für injizierbare, netzwerkfreie Source-Snapshot-Doubles.

Es gibt keinen relevanten `tech-debt.md`-Index. DRY-, MagicValues- und
DeadCode-Funde, die beim Anfassen dieser Konfigurations-, Registry- oder
Session-Grenze tatsächlich auftreten, werden innerhalb dieses Pakets in die
jeweilige Architekturänderung integriert; ein separater Sweep oder ein neuer
Tech-Debt-Step wird nicht erzeugt.

## Intention

Nach diesem Step entscheidet die Assembly-Session zuerst über einen expliziten,
validierten Source-Match und verwendet nur bei fehlendem, nicht verfügbarem oder
mehrdeutigem Match die bestehende statische Decompilation. Ein vollständig
geladener externer Source-Stand besitzt eine stabile Identität, wird als
readonly Snapshot in einer getrennten Registry dedupliziert und kann von einem
direkten DLL-Target sowie einem Projektkontext gemeinsam verwendet werden.

Die Provider-Grenze ist so vorbereitet, dass EPIC-04 später Gitea-Akquisition
ergänzen kann, ohne Mapping-, Snapshot- oder Roslyn-Verträge erneut zu bauen.
Alle neuen Pfade bleiben metadata-only bzw. read-only und sind über
deterministische Test-Doubles ohne Netzwerk überprüfbar.

## Große Paketgrenzen

Der Step ist ein zusammenhängendes High-Risk-Paket mit einem fachlichen
Vertrag. Die folgenden Grenzen sind Teil desselben Steps und dürfen nicht in
künstliche Mini-Steps aufgeteilt werden:

1. **Mapping- und Konfigurationsvertrag:** globale Mapping-Datei, strikte
   Validierung, Pfadauflösung und sichtbare Konfigurationsdiagnosen.
2. **Source-Match, Identität und Cache:** explizite Kandidatenauswahl aus einer
   vollständigen Solution, geladene Revision, Source-Snapshot-Key,
   Match-Evidenz/Confidence sowie getrennte Registry-/Cache-Lebensdauer.
3. **Session- und MCP-Anbindung:** ein gemeinsamer Resolver-/Registry-Kontext
   für direkte Assembly-Targets und Projekt-Nachschlagepfade, readonly Source-
   Sessions und sicherer Decompilation-Fallback.
4. **TestKit, Verträge und minimale Doku:** wiederverwendbare Fixtures,
   Unit-/Component-/repräsentative MCP-Tests, Konfigurationsdokumentation und
   die vorgeschriebenen Nicht-Stress-Gates.

## Nicht-Ziele

- Keine Gitea-Implementierung: kein Clone, Fetch, Branch-Refresh,
  Authentifizierungs- oder Netzwerkcode. Es entsteht nur die injizierbare
  Provider-Schnittstelle sowie ein expliziter „nicht verfügbar“-Pfad; die
  produktive Gitea-Akquisition gehört zu `EPIC-04`.
- Keine transitive Referenzauflösung, keine rekursive Source-/Decompilation-
  Matrix und keine gemeinsame Tool-Capability-Matrix; diese gehören zu
  `EPIC-05`. In diesem Step werden nur der direkte Assembly-Match und die
  bestehende unmittelbare Consumer-Anbindung verdrahtet.
- Kein Abschluss-Audit, kein projektweiter DRY-/Duplikat-/MagicValues-/DeadCode-
  Sweep und keine finale Dokumentations- oder Agentenregel-Synchronisation;
  diese gehören zu `EPIC-06`. Opportunistische Funde in den bearbeiteten
  Paketen werden davon abweichend direkt mitbehoben.
- Keine Erweiterung von `ainetlinter.project.json` um externe Mappings, keine
  automatische Repository-Discovery anhand von DLL- oder Repositorynamen und
  keine Nutzung lokaler dirty/unbuilt Checkouts als Source-of-Truth.
- Kein externes Projekt wird restauriert, getestet, ausgeführt oder verändert.
  Kein `Assembly.Load`, keine Reflection-Ausführung und keine
  `AssemblyLoadContext`-Nutzung.
- Keine neue allgemeine DI-/Plugin-Infrastruktur und keine Änderung am
  bestehenden Batch-Analysecache.

## Konkrete Änderungen

### Paket 1 — Globalen Mapping- und Konfigurationsvertrag einführen

#### `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs`

- **Was:** Interne immutable Records für `ExternalSources`-Optionen und
  Mapping-Einträge einführen. Der erste Vertrag umfasst den Pfad zur globalen
  Mapping-Datei (`MappingsPath`) und den separaten Source-Cache-Root
  (`CacheRoot`). Mapping-Einträge enthalten ausschließlich Repository-URL,
  Solution-Pfad und `assemblies` mit DLL-/Assembly-Namen; Commit, Branch und
  `.csproj`-Pfade sind kein Benutzerfeld.
- **Warum:** Externe Quellen müssen global und unabhängig vom aktuellen
  `ainetlinter.project.json`-Target beschrieben werden. Der Vertrag verhindert
  redundante Projektpfade und lässt mehrere Assembly-Projekte derselben
  Solution auf einen Snapshot zeigen.

#### `src/AiNetLinter/Configuration/ExternalSourceConfigurationLoader.cs` und
`src/AiNetLinter/Configuration/ExternalSourceMappingValidator.cs`

- **Was:** Einen fokussierten `System.Text.Json`-Lade-/Validierungspfad für den
  `ExternalSources`-Abschnitt aus `appsettings.json` und die referenzierte
  Mapping-Datei ergänzen. Relative Pfade werden deterministisch relativ zum
  Konfigurations-/Anwendungsstandort aufgelöst; absolute Pfade bleiben erlaubt.
  URL, Solution-Endung (`.sln`/`.slnx`), nichtleere Assembly-Namen und
  Normalisierung eines optionalen `.dll`-Suffixes werden strikt geprüft.
- **Was:** Doppelte Assembly-Einträge, leere Listen, doppelte Einträge in einem
  Repository und syntaktisch ungültige Pfade/URLs als strukturierte,
  sichtbare Konfigurationsdiagnosen liefern. Ein Assembly-Name, der in mehreren
  Repository-Einträgen vorkommt, wird als mehrdeutig behandelt und nie
  zufällig ausgewählt. Die Existenz des Solution-Pfads wird erst am
  Provider-/Snapshot-Grenzübergang geprüft, weil der Provider den geladenen
  Source-Stand liefert.
- **Warum:** Fehlende Mappings sind ein normaler Fallback, fehlerhafte oder
  mehrdeutige explizite Mappings müssen dagegen nachvollziehbar sein. Der
  Loader darf keine Gitea-Suche und keine implizite `.csproj`-Auswahl
  einführen. `RefreshIntervalMinutes` bleibt bis zur Gitea-Phase außerhalb
  dieses Vertrags.

#### `src/AiNetLinter/Logging/LoggingConfigLoader.cs`,
`src/AiNetLinter/Commands/McpServerCommand.cs` und
`src/AiNetLinter/Mcp/Daemon/DaemonHostCommand.cs`

- **Was:** Den neuen Loader neben dem bestehenden Logging-Loader in die
  MCP-Komposition aufnehmen, ohne Logging-Schlüssel oder `ConfigLoader` für
  `rules.json` umzudeuten. Fehlende optionale ExternalSources-Konfiguration
  ergibt eine leere Mapping-Konfiguration; eine formal fehlerhafte Datei wird
  als sichtbarer Server-/Session-Zustand an den Resolver weitergereicht.
- **Warum:** Stdio- und Daemon-Start müssen denselben globalen Mapping-Vertrag
  und denselben Source-Registry-Kontext verwenden. Ein zweiter Parser für
  Logging oder eine Aufblähung von `GlobalConfig` würde die vorhandenen
  Zuständigkeitsgrenzen und Public-Member-Grenzen verschlechtern.

### Paket 2 — Source-Match, Snapshot-Identität und getrennten Cache bauen

#### `src/AiNetLinter/Mcp/Assemblies/AssemblySourceModels.cs`

- **Was:** Interne Value-Modelle für Mapping-Match, Evidence/Confidence,
  geladenen Source-Stand und Snapshot-Identität einführen. Der kanonische
  `ExternalSourceSnapshotKey` besteht mindestens aus kanonischer Repository-
  URL, tatsächlich geladener Revision und kanonischem Solution-Pfad. Die
  Zuordnung zum Source-Projekt und `AssemblyName` bleibt ein Match innerhalb
  des Snapshot-Kontexts; sie darf den gemeinsamen Snapshot-Key nicht für
  direkte und indirekte Aliase duplizieren.
- **Was:** Die PE-Assembly-Identität und die Source-Match-Diagnosen getrennt
  transportieren. Ein Branch- oder Repositoryname allein ist keine Identität;
  eine fehlende geladene Revision darf keinen `source-backed`-Snapshot
  erzeugen. Evidence/Confidence markieren mindestens explizites Mapping,
  exakten `Project.AssemblyName`-Treffer und sichtbare Metadaten-Mismatches.
- **Warum:** Der Source-Code bleibt nur dann belastbar, wenn sein tatsächlicher
  Stand und das konkrete Projekt nachvollziehbar sind. Das DLL-Ziel bleibt
  die Target-Identität, auch wenn die Roslyn-Dokumente aus dem Source-Snapshot
  stammen.

#### `src/AiNetLinter/Mcp/Assemblies/AssemblySourceResolver.cs`

- **Was:** Einen Resolver vor der Decompilation einführen. Er liest zunächst
  die statisch ermittelte Assembly-Identität, sucht ausschließlich passende
  explizite Mapping-Einträge und fordert über eine kleine interne
  `IExternalSourceSnapshotProvider`-Grenze eine vollständige Solution mit
  geladenem Revisionsnachweis an. Aus den tatsächlich geladenen Roslyn-
  Projekten wird über `Project.AssemblyName` genau ein Source-Projekt
  ausgewählt; `.csproj`-Pfade werden nur aus der Solution abgeleitet.
- **Was:** Für keinen Eintrag, Provider-Nichtverfügbarkeit, fehlende Revision,
  keinen AssemblyName-Treffer oder mehrere Treffer einen sichtbaren
  `no-match`/`ambiguous`/`unavailable`-Zustand liefern und anschließend den
  bestehenden Decompilation-Fallback ermöglichen. Ein expliziter Match mit
  widersprechender PE-Metadatenplausibilität wird nicht still als Originalquelle
  ausgegeben.
- **Warum:** Explizite Konfiguration ist die einzige Quelle der Repository-
  Auswahl. Die Metadaten dürfen die Auswahl plausibilisieren, aber kein
  unbekanntes Repository entdecken.

#### `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotRegistry.cs` und
`src/AiNetLinter/Mcp/Assemblies/SourceSnapshotCache.cs`

- **Was:** Eine von `ProjectRegistry` und `AssemblyDecompilationCache` getrennte
  Registry/Cache-Grenze implementieren. Ein Snapshot wird unter seinem
  kanonischen Key nur einmal materialisiert; parallele Aufrufer erhalten
  begrenzte Leases auf denselben immutable, readonly `SourceFileCatalog`-/
  `Solution`-Stand. Die Registry verwaltet Snapshot-Lebensdauer und sichere
  Freigabe, ohne den Projekt- oder Batch-Cache zu verallgemeinern.
- **Was:** Den persistenten Source-Cache-Vertrag mit
  `source/<repository-key>/<revision>/<solution>` und einem typisierten
  Manifest für URL, Revision, Solution-Pfad, `AssemblyName -> Project` sowie
  Snapshot-Status festlegen. Nur vollständig materialisierte und validierte
  Provider-Ergebnisse dürfen sichtbar werden; unvollständige Einträge werden
  nicht als Source-backed verwendet. Die Akquisition/Refresh-Entscheidung
  selbst bleibt beim Provider aus EPIC-04.
- **Warum:** Direkte DLL-Targets und Projekt-Nachschlagepfade müssen denselben
  Source-Stand wiederverwenden, ohne die Source-Solution in den aktuellen
  Projektkontext zu kopieren oder mutierbar zu machen.

#### `src/AiNetLinter/Baseline/SourceFileCatalog.cs` und
`src/AiNetLinter/Baseline/SourceFileCatalogLoader.cs`

- **Was:** Den vorhandenen vollständigen Solution-Ladepfad wiederverwenden und
  für `ExternalSourceSnapshot` so kapseln, dass Workspace-/Solution-Ownership,
  readonly Nutzung und Disposal eindeutig beim Snapshot-Lease liegen. Keine
  zweite MSBuild-/Adhoc-Laderoutine bauen.
- **Warum:** Die Source-Solution ist der gemeinsame Snapshot-Kontext; das
  ausgewählte Projekt ist nur die Assembly-Zuordnung darin. So bleiben
  Dokument- und Roslyn-Semantik konsistent mit dem bestehenden Projektpfad.

### Paket 3 — Assembly-Session und MCP-Komposition an den Source-Vertrag anbinden

#### `src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisSessionModels.cs` und
`src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisSession.cs`

- **Was:** `AssemblyAnalysisSessionOptions`/Generation um den optionalen
  Source-Resolver- und Snapshot-Kontext erweitern. Der Refresh liest die
  PE-Identität zuerst, versucht danach einen verifizierten Source-Match und
  erzeugt bei Erfolg eine `source-backed`-Generation aus dem ausgewählten
  Roslyn-Projekt der vollständigen Solution. Die PE-Identität, Match-Evidence,
  Revision und Status bleiben im Generation-/Origin-Vertrag sichtbar.
- **Was:** Nur bei erfolgreicher, vollständiger Source-Snapshot-Validierung die
  Decompilation überspringen. Bei fehlendem, mehrdeutigem, nicht verfügbarem
  oder nicht plausibilisiertem Match den bestehenden `decompiled`-Pfad mit
  sichtbarer Diagnose verwenden. Source- und Decompilation-Cache bleiben
  getrennt; ein Source-Fehler darf keinen fremden oder alten Source-Stand als
  aktuell ausgeben.
- **Warum:** Die Auswahlentscheidung gehört vor den bestehenden Decompiler,
  ohne den bewährten EPIC-02-Fallback oder die statische Sicherheitsgrenze zu
  umgehen.

#### `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs`,
`AssemblyAnalysisToolSupport.cs` und `AssemblyAnalysisService.cs`

- **Was:** Die Kontextfabrik auf den gemeinsamen Resolver-/Registry-Kontext
  umstellen und source-backed sowie decompiled Snapshots in denselben
  `AssemblyContext`-/MCP-Ergebnisvertrag überführen. Für den Source-Pfad wird
  das konkrete Projekt innerhalb der vollständigen Solution adressiert; der
  Consumer-Kontext bleibt fachlich getrennt und wird nicht zur externen
  Source-of-Truth.
- **Was:** Direkte DLL-Aufrufe und projektbezogene Nachschlageaufrufe auf den
  identischen `ExternalSourceSnapshotKey` führen. Ein zweiter Provider-Load
  oder eine zweite Materialisierung desselben Snapshot-Keys ist im normalen
  Aliasfall ausgeschlossen.
- **Warum:** Alle bestehenden Assembly-spezifischen Tools sollen die Herkunft
  und den Zustand einheitlich sehen, ohne EPIC-05 vorwegzunehmen oder eine
  zweite Roslyn-Toolfamilie zu bauen.

#### `src/AiNetLinter/Mcp/AnalysisToolCall.cs`,
`src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs`,
`src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` und
`src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs`

- **Was:** Den gemeinsamen Source-Resolver/Registry-Kontext über die
  bestehende Dispatch- und Server-Composition an Assembly-Tools und residente
  Projekt-Sessions weiterreichen. `ProjectRegistry` bleibt die projektbezogene
  Registry; die neue Source-Registry wird nicht als versteckter globaler
  Singleton eingeführt, sondern pro Server-Lebensdauer erzeugt und explizit
  an die Closures/Optionen gebunden.
- **Was:** Stdio- und Daemon-Pfade identisch verdrahten und den Default-
  Provider als „nicht verfügbar“ behandeln, bis EPIC-04 die Gitea-Akquisition
  liefert. Test-Kompositionen müssen einen Fake-Provider injizieren können.
- **Warum:** Nur eine gemeinsam komponierte Registry garantiert Alias-
  Wiederverwendung über direkte und projektbezogene Aufrufe; getrennte
  Registries würden denselben Source-Stand mehrfach laden.

### Paket 4 — TestKit, Tests und minimale Vertragsdokumentation

#### `src/AiNetLinter.TestKit/Mcp/ExternalSourceSnapshotFixture.cs` sowie
`RoslynTestSolutionFactory.cs`/`ProjectRegistryFixture.cs`

- **Was:** Eine zentrale Fixture für eine vollständige, kleine deklarative
  Solution mit mindestens zwei unterschiedlichen `AssemblyName`s, einem
  expliziten Mapping, einer geladenen Test-Revision, Fake-Provider,
  Provider-Aufrufzähler und readonly Snapshot-Lease ergänzen. Die Fixture
  verwendet `TestTempDirectory` und vorhandene Roslyn-/MCP-Helfer; kein
  Netzwerk, Prozessstart, Restore oder fremder Projekt-Testlauf.
- **Warum:** Resolver-, Registry- und Alias-Tests müssen denselben
  unveränderlichen Teststand wiederverwenden und dürfen keine duplizierten
  Ad-hoc-Solution-Builder erzeugen.

#### `src/AiNetLinter.FastTests/Configuration/` und
`src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/`

- **Was:** Unit-Tests für `appsettings`-Pfade, Mapping-Schema,
  `.dll`-Normalisierung, URL-/Solution-Validierung, doppelte/mehrdeutige
  Einträge, fehlende Mapping-Treffer und das unveränderte
  `ainetlinter.project.json` ergänzen.
- **Was:** Component-Tests für `AssemblyName`-Auflösung aus einer vollständigen
  Fake-Solution, abweichendes Projekt-/DLL-Naming, Source-Mismatch,
  Provider-Nichtverfügbarkeit, Source-/Decompilation-Fallback,
  Snapshot-Key-Gleichheit/-Verschiedenheit, atomare Source-Cache-Adoption,
  readonly Verhalten und genau einen Provider-/Materialisierungsaufruf bei
  direktem Target plus Consumer-Alias schreiben.
- **Was:** Prüfen, dass ein gültiger Source-Match keine Decompilation aufruft,
  die PE-Target-Identität erhalten bleibt und Origin/Revision/Evidence im
  bestehenden Resultat sichtbar sind. Keine Tests für transitive Referenzen
  oder die spätere Capability-Matrix aufnehmen.

#### `src/AiNetLinter.IntegrationTests/Mcp/` und bestehende MCP-Testfixtures

- **Was:** Wenige repräsentative In-Process-/MCP-Host-Tests ergänzen, die eine
  Mapping-Datei aus einem `TestTempDirectory` laden und den Fake-Provider über
  die Server-Options injizieren. Ein Test deckt direkte DLL-Analyse und
  project-based Consumer-Lookup gegen denselben Snapshot ab; ein zweiter den
  konservativen Fallback ohne Provider/Mapping.
- **Warum:** Die echte Stdio-/Daemon-Komposition muss den gemeinsamen Registry-
  Lebenszyklus beweisen, ohne Gitea oder unbounded Sleeps einzuführen.

#### `Docs/configuration.md`, `Docs/integration.md`, `Docs/agent-api.md` und
`README.md`

- **Was:** Nur die jetzt implementierten Mapping-/Snapshot-/Fallback-
  Vertragsflächen dokumentieren: globaler Mapping-Pfad aus `appsettings.json`,
  Repository-/Solution-/Assembly-Schema, keine `.csproj`-Pfade, sichtbare
  Source-/Decompilation-Herkunft und die Gitea-Provider-Grenze. Die umfassende
  finale Capability-Matrix, Agentenregel- und Roadmap-Synchronisation bleibt
  EPIC-06.
- **Warum:** Konfigurations- und MCP-Vertragsänderungen dürfen nicht nur im
  Code existieren; zugleich wird die finale Gesamtdokumentation nicht
  vorgezogen.

## Tests

- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category=Unit` während
  der Iteration für Mapping-, Identity- und Fehlervertragsänderungen.
- [ ] Unit: gültige/ungültige `ExternalSources`-Konfiguration, relative und
  absolute Pfade, strikte Mapping-Validierung, keine `.csproj`-Felder,
  Duplikat-/Ambiguitätsdiagnosen und stabile AssemblyName-Normalisierung.
- [ ] Component: vollständige Fake-Solution mit mehreren Assembly-Projekten,
  exakter `Project.AssemblyName`-Auflösung, Source-Mismatch, fehlender
  Revision, Provider-Nichtverfügbarkeit und Decompilation-Fallback.
- [ ] Component: gleicher Snapshot-Key für direkte DLL und Consumer-Alias,
  ein Provider-/Materialisierungsaufruf, unterschiedliche Keys für andere
  Revision/Solution/Repository und readonly-/Lease-/Disposal-Semantik.
- [ ] Integration/MCP: Stdio-/Daemon-Komposition mit injiziertem Fake-Provider,
  sichtbare Origin-/Confidence-/Revision-Felder und unveränderte
  projektbezogene Registry-Semantik.
- [ ] `dotnet clean` vor dem Abschlusslauf, um alte Build-/Cacheartefakte aus
  dem Gate auszuschließen.
- [ ] `dotnet build` — null Warnungen und null Fehler bei
  `TreatWarningsAsErrors`.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` —
  vollständiger Unit-/Component-Lauf grün.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
  — vollständiger Integration-/Dogfood-/Performance-Lauf grün; `Stress`
  bleibt ausgeschlossen.
- [ ] Keine `Assembly.Load`-, Reflection- oder `AssemblyLoadContext`-Nutzung;
  kein Netzwerk- oder externer Projekt-Testlauf in den neuen Tests.

## Definition of Done

- [ ] EPIC-03-Paket implementiert einen strikten globalen Mapping-Vertrag über
  `appsettings.json`; `ainetlinter.project.json` bleibt auf das aktuelle
  Projekt-Target beschränkt.
- [ ] Der Resolver wählt ausschließlich explizite Mappings, leitet das
  konkrete Source-Projekt aus der vollständigen Solution über
  `Project.AssemblyName` ab und behandelt keinen/mehrere Treffer konservativ
  mit sichtbarer Diagnose.
- [ ] Source-Snapshot-Identität enthält kanonische Repository-URL, tatsächlich
  geladenen Revisionsnachweis und Solution-Pfad; Projekt-/Assembly-Match,
  Evidence und Confidence sind nachvollziehbar.
- [ ] Source-Registry und Source-Cache sind von `ProjectRegistry` und
  `AssemblyDecompilationCache` getrennt. Identische Snapshot-Keys werden nur
  einmal materialisiert und readonly über direkte sowie Consumer-Aliase geteilt.
- [ ] Ein verifizierter Source-Match verwendet den Source-Roslyn-Stand und
  überspringt Decompilation; alle anderen Fälle behalten den bestehenden
  statischen Decompilation-Fallback und sichtbare Zustände.
- [ ] Stdio- und Daemon-Komposition verwenden denselben explizit gebundenen
  Source-Kontext; kein versteckter globaler Singleton und keine neue DI-
  Infrastruktur.
- [ ] Gitea-Akquisition, Refresh, Authentifizierung, transitive Referenzmatrix
  und Abschluss-Audit sind nicht Bestandteil dieses Steps und bleiben für die
  vorgesehenen Epics sichtbar offen.
- [ ] Alle genannten Unit-, Component- und repräsentativen MCP-Tests sowie
  `dotnet build` und beide vollständigen Nicht-Stress-Testcommands sind grün.
- [ ] Der ausführende Coder erstellt einen deutschen Conventional-Commit im
  Imperativ mit dem Task-Suffix `[decompiled-assembly-analysis]`; Code, Tests
  und die minimal erforderliche Vertragsdokumentation gehören in den
  ausführenden Step-Commit.
- [ ] Nach Ausführung werden `step-005/step-result.md` geschrieben und der
  Step-Status gemäß Drift-Loop aktualisiert; `task-state.md` wird erst durch
  den Orchestrator nach Prüfung geändert.

## Rules-Refs

- `.agents/rules/AiNetLinter-McpWorkflow.mdc#Verbindliche Priorität` und
  `#Werkzeugwahl` — C#-Semantik, Referenzen, Impact und Codeverständnis zuerst
  über AiNetLinter-MCP mit absolutem `projectRoot`; Assembly-Analyse bleibt
  metadata-only.
- `.agents/rules/AiNetLinterRichtlinien.mdc#2 Architektur-Verbote` — keine
  Runtime-Ladung, Reflection, `AssemblyLoadContext`, Plugin-/DI-Ausweitung;
  externe Quellen bleiben read-only.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3 Windows-Umgebung & Tool-Regeln`
  — Windows-kompatible Pfadauflösung, sichere Cache-/Temp-Grenzen und
  bestehende TestTempDirectory-/PowerShell-Muster.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — xUnit-v3-
  Tests, Dokumentationspflicht bei Konfigurationsänderungen, vollständige
  Nicht-Stress-Gates und keine unbounded Retries/Sleeps.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` —
  Zero-Warning-Gate, explizite Fehlerzustände, DRY-/MagicValues-/DeadCode-
  Funde opportunistisch im betroffenen Paket beheben und kein künstlicher
  Einzel-Sweep.
- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#Grenzwerte` — immutable
  Records, kurze fokussierte Methoden sowie Public-Member-, Methoden- und
  Komplexitätsgrenzen für Config-/Resolver-/Registry-Verträge.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md#10.2` und `#10.6` —
  ein großer, in sich geschlossener Single-Step; keine Mini-Batches für
  mittel-/hoch-riskante Vertragsänderungen, `related_to` bleibt ein Pointer.

## Bekannte Ausnahmen

- Ohne EPIC-04-Provider kann die Produktionskomposition in diesem Step keinen
  echten Gitea-Stand laden. Der Default muss deshalb einen sichtbaren
  „Provider nicht verfügbar“-Zustand und den bestehenden Decompilation-
  Fallback liefern; die Source-backed-Integration wird über einen injizierten
  Test-Provider verifiziert.
- Persistierte Source-Snapshots werden in diesem Step nur über ihre
  Identitäts-/Validierungsgrenze abgesichert. Automatisches Refresh,
  Authentifizierung und Aufräumen alter Einträge bleiben bewusst außerhalb.

## Code-Skizze (optional)

```text
PEReader / AssemblyIdentity
        -> explizite Mapping-Datei aus appsettings.json
        -> Provider-Port liefert vollständige Solution + geladene Revision
        -> Project.AssemblyName -> genau ein Source-Projekt
        -> ExternalSourceSnapshotKey(URL + Revision + Solution)
        -> SourceSnapshotRegistry (ein readonly Snapshot, mehrere Aliase)
        -> source-backed AssemblyGeneration
        -> bei no-match/ambiguous/unavailable: bestehende Decompilation
```

## Notes

Der tatsächliche Codeabgleich erfolgte für die C#-Semantik über den
AiNetLinter-MCP mit `projectRoot`
`C:\Daten\Entwicklung\Ralf\AiNetLinter`; Textsuche diente nur zur Ergänzung
der Fundstellen. Die bestehende CodeMap deckt die betroffenen
Konfigurations-, Assembly-, Roslyn-, MCP- und TestKit-Bereiche bereits ab und
wird deshalb nicht mit spekulativen zukünftigen Dateien erweitert.

`step-003` und `step-004` bilden gemeinsam das abgeschlossene EPIC-02-
Fundament. Dieser Step erweitert es um die Source-Herkunftsentscheidung und
Snapshot-Wiederverwendung, ersetzt aber weder die genehmigten EPIC-02-
Entscheidungen noch die späteren Gitea-/Transitivitäts-/Abschluss-Epics.
