---
status: done (Korrektur ausstehend)
type: step-plan
task: decompiled-assembly-analysis
step: 005
corrects: null
title: "Expliziten External-Source-Mappingvertrag mit strikter Validierung vorbereiten"
epic: EPIC-03
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-28T17:15:00+02:00
related_to: [step-004]
context_budget:
  read_first:
    - "appsettings.json"
    - "src/AiNetLinter/Logging/LoggingConfigLoader.cs"
    - "src/AiNetLinter/Configuration/ConfigLoader.cs"
    - "src/AiNetLinter/Mcp/Projects/ProjectDefinitionLoader.cs"
    - "src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisSessionModels.cs"
    - "src/AiNetLinter/Mcp/Assemblies/AssemblyDiagnosticCodes.cs"
    - "src/AiNetLinter.FastTests/Logging/LoggingConfigLoaderTests.cs"
    - "src/AiNetLinter.FastTests/Mcp/Projects/ProjectDefinitionLoaderTests.cs"
    - "src/AiNetLinter.FastTests/Mcp/AnalysisToolCallTests.cs"
    - "src/AiNetLinter.TestKit/TestTempDirectory.cs"
    - "Docs/configuration.md"
    - "tasks/decompiled-assembly-analysis/codemap.md"
  read_on_demand:
    - "src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisSession.cs — nur zur Prüfung, dass der neue Vertrag keine Session-/Cache-Grenze vorwegnimmt"
    - "src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs — nur zur späteren Übergabegrenze, nicht zur Änderung"
    - "src/AiNetLinter/Mcp/AnalysisToolCall.cs — nur zur Scope-Abgrenzung gegen Session-/MCP-Wiring"
    - "src/AiNetLinter/Mcp/McpServerOptionsFactory.cs, src/AiNetLinter/Commands/McpServerCommand.cs und src/AiNetLinter/Mcp/Daemon/DaemonHostCommand.cs — nur falls der Loader-Aufrufpunkt für eine spätere Verdrahtung dokumentiert werden muss"
    - "src/AiNetLinter/Baseline/SourceFileCatalog.cs und SourceFileCatalogLoader.cs — nicht für den Mapping-Loader, nur bei einer unbeabsichtigten Roslyn-Abhängigkeit"
    - "src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisSessionTests.cs und src/AiNetLinter.IntegrationTests/Mcp/ — nur für bestehende Testkonventionen; keine Session-/MCP-Tests in diesem Step"
  out_of_scope:
    - "Source-Snapshot-Identität, Revision/Commit-Modelle, SourceSnapshotRegistry, Source-Cache, Lease-, TTL-, Generation- und Disposal-Semantik"
    - "Direkte Source-Match-Auflösung über vollständige .sln/.slnx-Solutions, Project.AssemblyName-Auswahl, .csproj-Ableitung und Roslyn-Solution-Loading"
    - "AssemblyAnalysisSession, AssemblyAnalysisContextFactory, AssemblyAnalysisService, AnalysisToolCall, MCP-Registrierungen sowie Stdio-/Daemon- und Projekt-Session-Wiring"
    - "Gitea-Clone/Fetch, Authentifizierung, Branch-Refresh, Netzwerk, atomare Snapshot-Akquisition und produktive Source-of-Truth aus EPIC-04"
    - "Transitive Referenzen, gemeinsame Capability-Matrix, finale Agentenregel-/API-/Integrationsdokumentation, README-/rules.json-Synchronisation und Abschluss-Audit"
    - "Erweiterung von ainetlinter.project.json, Änderung des bestehenden Batch-Analysecaches sowie Assembly.Load, Reflection-Ausführung oder AssemblyLoadContext"
---

# Step 005: Expliziten External-Source-Mappingvertrag mit strikter Validierung vorbereiten

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-03` aus `roadmap.md` — ausschließlich der erste
  Mapping-/Validierungsschnitt; EPIC-03 bleibt danach offen.
- **Konzept-Referenz:** `Konzept.md` „Gitea-Register und wartungsarmes
  Mapping“, „Source-Auflösung vor der Dekompilation“ (nur die explizite
  Konfigurationsgrenze), Phase 3 und die Mapping-bezogene Teststrategie.
- **Split-Gate:** Der Step enthält genau einen primären Fachvertrag, höchstens
  drei Schichten und höchstens acht Akzeptanzkriterien. Der Provider-Port ist
  ein Adapter dieses Mapping-/Diagnosevertrags, kein zweiter Source-Snapshot-
  oder Sessionvertrag.

## Aktueller Projektzustand (JIT-Kontext)

`step-003` und `step-004` haben das EPIC-02-Fundament mit metadata-only
Assembly-Session, Decompilation-Generationen, Cache-Validierung, Referenz-
diagnosen und synthetischem Roslyn-Snapshot abgeschlossen. `AssemblyAnalysisSession`
und `AssemblyAnalysisContextFactory` sind damit bestehende Grenzen, die dieser
Step nicht erweitert.

Die Konfiguration ist aktuell getrennt: `ConfigLoader` lädt `rules.json`,
`ProjectDefinitionLoader` validiert und löst die beiden Pfade von
`ainetlinter.project.json` relativ zur Definitionsdatei auf, und
`LoggingConfigLoader` liest aus dem optionalen `appsettings.json` nur Logging.
Der neue Loader soll diese Zuständigkeiten nicht vermischen. Seine eigenen
Result-/Diagnosewerte müssen wie beim Projekt-Loader explizit bleiben, damit
ungültige Mappings nicht als stiller Fallback oder scheinbar gültige Quelle
weitergereicht werden.

Die vorhandenen Unit-Testmuster und `TestTempDirectory` reichen für JSON-/Pfad-
und Provider-Vertragstests aus. Eine vollständige Test-Solution, ein
`SourceFileCatalog` oder ein MCP-Host sind für diesen Mapping-Schnitt nicht
erforderlich.

## Intention

Nach diesem Step gibt es einen unveränderlichen, streng validierten Vertrag für
die globale External-Source-Mapping-Datei und deren Pfad aus `appsettings.json`.
Fehler, Mehrdeutigkeiten und nicht unterstützte Felder werden als strukturierte,
sichtbare Diagnosen erhalten; ein fehlendes optionales Mapping bleibt der normale
„kein Mapping“-Zustand. Ein kleiner injizierbarer Provider-Port kann diesen
validierten Vertrag übernehmen und den Zustand „Provider nicht verfügbar“ ohne
Netzwerk liefern, damit der spätere Snapshot-/Session-Step darauf aufsetzt.

## Kontext-Handoff

### Invarianten

- `ainetlinter.project.json` bleibt unverändert und enthält keine externen
  Mappings.
- Die einzige Benutzerzuordnung ist explizit: keine Repository-Discovery aus
  DLL- oder Repositorynamen und keine automatische `.csproj`-Auswahl.
- `ExternalSources:MappingsPath` wird deterministisch relativ zum Verzeichnis
  der gelesenen `appsettings.json` aufgelöst; absolute Pfade bleiben absolut.
- `solutionPath` bleibt ein normalisierter, repository-relativer `.sln`- oder
  `.slnx`-Pfad. Seine Existenz und sein Inhalt werden hier nicht geprüft, weil
  das erst am Provider-/Snapshot-Grenzübergang möglich ist.
- Assembly-Namen werden für Vergleiche ohne optionales `.dll`-Suffix und
  case-insensitive normalisiert. Doppelte oder über Repository-Einträge hinweg
  mehrdeutige Namen sind Fehler, niemals Zufallsauswahl.
- Ungültige explizite Konfiguration wird nicht an den Provider weitergegeben;
  Diagnosen enthalten mindestens Code, Schweregrad, Nachricht und Fundstelle.
- Der Provider-Port kennt in diesem Step weder `Solution`, `Project`, Revision,
  Snapshot-Key, Cache, Lease noch Session; er erhält nur einen validierten
  Mapping-Eintrag und kann einen sichtbaren Verfügbarkeits-/Fehlerzustand liefern.
- Keine Assembly wird geladen oder ausgeführt; es gibt keinen Netzwerkzugriff,
  keine Reflection-Ausführung und keinen `AssemblyLoadContext`.

### Relevante MCP-Symbole

Semantische Einstiegsanker für den Coder/Kritiker:

- `T:AiNetLinter.Logging.LoggingConfigLoader` — bestehendes
  `appsettings.json`-Lade- und Pfadpattern.
- `M:AiNetLinter.Mcp.Projects.ProjectDefinitionLoader.Load(System.String)~AiNetLinter.Mcp.Projects.ProjectDefinitionLoadResult` — bestehendes
  Result-/Diagnosepattern mit relativer Pfadauflösung.
- `M:AiNetLinter.Configuration.ConfigLoader.TryLoadConfig(System.String,System.Boolean)~AiNetLinter.Configuration.Config.Config` — bestehende
  `rules.json`-Grenze, die nicht um ExternalSources erweitert werden darf.
- `T:AiNetLinter.Mcp.Assemblies.AssemblyAnalysisSessionOptions` und
  `T:AiNetLinter.Mcp.Assemblies.AssemblyAnalysisSession` — bestehende
  EPIC-02-Grenze, die erst im Folge-Step Source-Ergebnisse konsumiert.
- `T:AiNetLinter.Mcp.AnalysisToolCall` — bestehende Dispatch-Grenze, die in
  diesem Step unverändert bleibt.

### Sicherer Einstiegspunkt

Zuerst die Records für Mapping, normalisierte Werte, Diagnose und
`ExternalSourceConfigurationLoadResult` unter
`src/AiNetLinter/Configuration/` anlegen und die Validierungsregeln als reine,
deterministische Funktionen festlegen. Danach Loader-Tests mit
`TestTempDirectory` schreiben. Erst wenn dieser Vertrag stabil ist, den
minimalen Provider-Port samt `UnavailableExternalSourceProvider` und dessen
Vertragstest ergänzen. Nicht in `AssemblyAnalysisSession`,
`AnalysisToolCall` oder MCP-Komposition einsteigen.

## Konkrete Änderungen

### Schicht 1 — Mapping-/Konfigurationsvertrag und Validierung

#### `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs`

- **Was:** Interne immutable Records für `ExternalSourceConfiguration`,
  `ExternalSourceMapping`, normalisierte Assembly-Namen und
  `ExternalSourceConfigurationDiagnostic` definieren. Das Benutzerformat
  enthält nur `ExternalSources:MappingsPath` sowie pro Mapping `url`,
  `solutionPath` und `assemblies`; Commit, Branch, `.csproj`, Cache-Root und
  Refresh-Intervall gehören nicht hinein.
- **Was:** Einen expliziten Load-Result-Typ mit gültiger Konfiguration und
  strukturierter Diagnose-Liste vorsehen. Ein Mapping-Dokument mit einem
  Validierungsfehler wird als nicht verwendbar markiert; ein fehlender
  optionaler Abschnitt ergibt eine leere Konfiguration ohne Fehlerdiagnose.
- **Warum:** Der Fachvertrag bleibt klein, unveränderlich und kann später von
  Resolver und Provider konsumiert werden, ohne Logging-, Rules- oder
  Sessionmodelle zu vermischen.

#### `src/AiNetLinter/Configuration/ExternalSourceConfigurationLoader.cs`

- **Was:** `appsettings.json` und die referenzierte Mapping-Datei über einen
  fokussierten `System.Text.Json`-Pfad laden. Der Loader akzeptiert einen
  expliziten Settings-Pfad für Tests, kanonisiert ihn und löst relative
  `MappingsPath`-Werte gegen dessen Verzeichnis auf; ein absoluter Pfad bleibt
  unverändert.
- **Was:** Fehlende `appsettings.json` bzw. fehlender optionaler
  `ExternalSources`-Abschnitt liefern eine leere Konfiguration. Ein explizit
  gesetzter, fehlender oder nicht lesbarer Mapping-Pfad sowie ungültiges JSON
  liefern strukturierte Fehlerdiagnosen statt Exception-basiertem oder
  stillem Ignorieren.
- **Warum:** Der Loader ist unabhängig testbar und führt keine MCP- oder
  Session-Komposition ein. Die Pfadbasis ist reproduzierbar und nicht vom
  zufälligen aktuellen Arbeitsverzeichnis abhängig.

#### `src/AiNetLinter/Configuration/ExternalSourceMappingValidator.cs`

- **Was:** Mapping-Einträge strikt prüfen: absolute HTTP(S)-Repository-URL,
  repository-relativer `.sln`-/`.slnx`-Pfad ohne `..`-Escape, nichtleere
  Assembly-Liste, nichtleere Assembly-Namen, optionales `.dll`-Suffix,
  case-insensitive Duplikate und unbekannte Felder.
- **Was:** Doppelte Namen innerhalb eines Eintrags und Mehrdeutigkeiten über
  mehrere Repository-Einträge mit stabilen Diagnosecodes markieren. Der
  Validator prüft nicht die Existenz oder den Inhalt der Solution und liest
  keine `.csproj`-Dateien.
- **Warum:** Explizite, fehlerhafte Konfiguration darf später nicht als
  source-backed Beweis verwendet werden; fehlende Mappings bleiben dagegen
  ein normaler Fallback-Fall.

### Schicht 2 — Injizierbarer Provider-Port und Adapter

#### `src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs` und
`src/AiNetLinter/Mcp/Assemblies/UnavailableExternalSourceProvider.cs`

- **Was:** Einen kleinen internen `IExternalSourceProvider`-Port definieren,
  der ausschließlich einen bereits validierten `ExternalSourceMapping`-
  Vertrag entgegennimmt und ein Ergebnis aus Verfügbarkeit plus Diagnosen
  zurückgibt. Die Antwort enthält bewusst noch keine `Solution`, kein
  `Project`, keine Revision und keinen Snapshot-Key.
- **Was:** Einen deterministischen `UnavailableExternalSourceProvider` als
  Produktionsdefault/Adapter ergänzen. Er führt kein Netzwerk aus und meldet
  „Provider nicht verfügbar“ sichtbar; vorhandene Konfigurationsdiagnosen
  werden nicht überschrieben.
- **Warum:** EPIC-04 kann später die Gitea-Akquisition hinter derselben
  injizierbaren Grenze ergänzen. Der Step baut dafür nur den Adapter-Port und
  verschiebt die eigentliche Source-Akquisition ausdrücklich nach EPIC-04.

### Schicht 3 — Tests und minimale Konfigurationsdokumentation

#### `src/AiNetLinter.FastTests/Configuration/ExternalSourceConfigurationLoaderTests.cs`

- **Was:** Unit-/Component-Tests für leere optionale Konfiguration, relative
  und absolute `MappingsPath`-Auflösung, fehlende Mapping-Datei, defektes JSON,
  unbekannte Felder, URL-/Solution-Pfad-Validierung, `.dll`-Normalisierung,
  leere/duplizierte/mehrdeutige Assembly-Einträge und stabile Diagnosen
  ergänzen.
- **Was:** Reale kleine JSON-Dateien ausschließlich unter
  `TestTempDirectory` verwenden; `ainetlinter.project.json` und der bestehende
  Rules-Loader bleiben unverändert. Die Tests prüfen, dass ein invalides
  Mapping nicht als verwendbare Konfiguration zurückkehrt.

#### `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceProviderContractTests.cs`

- **Was:** Einen Fake-Provider injizieren und prüfen, dass nur validierte
  Mapping-Werte übergeben werden, Cancellation weitergereicht wird und der
  `UnavailableExternalSourceProvider` ohne Netzwerk einen sichtbaren Zustand
  liefert. Es gibt keinen Roslyn-/Solution- oder MCP-Host-Aufbau.
- **Was:** Contract-Assertions auf die Abwesenheit von Snapshot-/Revision-/
  Sessionfeldern im Port-Ergebnis sowie auf die unveränderte Decompilation-
  Sicherheitsgrenze beschränken.

#### `Docs/configuration.md`

- **Was:** Minimal den implementierten Konfigurationsvertrag für
  `ExternalSources:MappingsPath`, die Mapping-Felder `url`, `solutionPath`
  und `assemblies`, die Pfadbasis, `.dll`-Normalisierung und sichtbare
  Validierungsdiagnosen dokumentieren. Festhalten, dass `.csproj`, Commit,
  Branch und automatische Repository-Suche keine Mapping-Felder sind.
- **Was:** Keine Aussage über bereits wirksame Source-Snapshots, vollständige
  Solution-Matches, Session-/MCP-Routing oder Gitea-Akquisition aufnehmen;
  diese Verträge werden erst nach den Folge-Steps dokumentiert.

## Ausdrückliche Folge-Abgrenzung

Der spätere EPIC-03-Folge-Step übernimmt als eigenes vertikales Paket:

- Source-Snapshot-Identität aus Repository, tatsächlich geladener Revision und
  Solution-Pfad;
- SourceSnapshotRegistry/-Cache, readonly Snapshot-Leases und gemeinsame
  Wiederverwendung zwischen direkten DLL-Aliasen und Consumer-Aufrufen;
- Auflösung des passenden Source-Projekts aus einer vollständig geladenen
  `.sln`/`.slnx` über `Project.AssemblyName`, einschließlich Match-Evidence,
  Confidence und Source-/Decompilation-Fallback;
- Anpassung von `AssemblyAnalysisSession`, Assembly-Kontextfabrik,
  `AnalysisToolCall`, MCP-Registrierungen sowie Stdio-/Daemon-Komposition.

Dieser Step darf dafür nur die Eingangsform und den injizierbaren Port stabil
machen. Er implementiert keine vollständige EPIC-03-Umsetzung. Die Gitea-
Akquisition, Authentifizierung, Refresh- und Fehlersemantik bleibt vollständig
in EPIC-04.

## Tests

- Unit: reine Validatorfälle für Schema, URL, repository-relative
  Solution-Pfade, Assembly-Normalisierung, unbekannte Felder und Duplikate.
- Component: Loader mit temporärer `appsettings.json` und Mapping-Datei für
  relative/absolute Pfade, leere optionale Konfiguration und sichtbare Fehler.
- Vertrags-Test: Fake-/Unavailable-Provider mit validiertem Mapping,
  Cancellation und „nicht verfügbar“ ohne Netzwerk oder Solution-Load.
- Bestehende schnelle Regressionen für `LoggingConfigLoader`,
  `ProjectDefinitionLoader`, `AnalysisToolCall` und EPIC-02-Assembly-Session
  bleiben unverändert; nur bei einer echten Regression werden sie gezielt
  ausgeführt.
- Abschluss-Verifikation nach Implementierung: `dotnet build`,
  `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und
  `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`;
  `Stress` bleibt ausgeschlossen.

## Definition of Done / Akzeptanzkriterien

- [ ] Ein unveränderlicher External-Source-Mappingvertrag liest ausschließlich
  `MappingsPath`, `url`, `solutionPath` und `assemblies`; `project.json`,
  `.csproj`, Commit und Branch werden nicht erweitert.
- [ ] Relative Settings-Pfade werden deterministisch gegen das Verzeichnis
  der gelesenen `appsettings.json` aufgelöst; repository-relative
  Solution-Pfade werden normalisiert und nicht aus dem aktuellen Arbeits-
  verzeichnis oder fremden Dateien abgeleitet.
- [ ] Ungültige URLs, Solution-Endungen, Pfad-Escapes, leere Listen, leere
  Namen, unbekannte Felder sowie interne/übergreifende Duplikate führen zu
  stabilen sichtbaren Diagnosen und nie zu Zufallsauswahl.
- [ ] Fehlende optionale ExternalSources-Konfiguration liefert eine leere
  Mapping-Konfiguration; ein explizit fehlerhafter Pfad oder ein fehlerhaftes
  Mapping wird nicht als gültiger Provider-Eingang verwendet.
- [ ] Der injizierbare Provider-Port und der Unavailable-Adapter transportieren
  ausschließlich validierte Mappings sowie Verfügbarkeit/Diagnosen und
  enthalten noch keine Snapshot-, Revision-, Solution- oder Sessionsemantik.
- [ ] Unit-, Component- und Provider-Vertragstests laufen deterministisch mit
  `TestTempDirectory`, ohne Netzwerk, Restore, Fremdprojekt, MCP-Host oder
  Assembly-Ausführung.
- [ ] `Docs/configuration.md` beschreibt nur den in diesem Step implementierten
  Mapping-/Validierungsvertrag; Snapshot-, Session-, MCP- und Gitea-Aussagen
  bleiben den Folge-Epics vorbehalten.
- [ ] `dotnet build` sowie beide vollständigen Nicht-Stress-Testläufe sind
  grün; kein `Assembly.Load`, keine Reflection-Ausführung und kein
  `AssemblyLoadContext` wird eingeführt.

## Rules-Refs

- `.agents/rules/AiNetLinter-McpWorkflow.mdc#Verbindliche Priorität` und
  `#Werkzeugwahl` — C#-Semantik zuerst über MCP mit absolutem
  `projectRoot`; `rg` bleibt auf Text-/Diff-Arbeit beschränkt.
- `.agents/rules/AiNetLinterRichtlinien.mdc#2 Architektur-Verbote` — keine
  Runtime-Ladung, Reflection, `AssemblyLoadContext`, Plugin- oder DI-
  Infrastruktur; Projektvertrag und universelle Pfadauflösung bleiben
  generisch.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3 Windows-Umgebung & Tool-Regeln`
  — PowerShell-kompatible Pfade, bestehende Loader-Patterns und
  `TestTempDirectory` verwenden.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — xUnit-v3-
  Tests, deterministische Test-Doubles, vollständige Nicht-Stress-Gates und
  Dokumentationspflicht bei Konfigurationsänderungen.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` —
  Result-/Diagnosemuster, Zero-Warning-Gate und DRY-/MagicValues-/DeadCode-
  Funde nur opportunistisch im betroffenen Mapping-/Validierungspaket.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md#10.2`, `#10.6`
  und `#10.7` — ein vertikaler Step innerhalb eines Epics, kein High-Risk-
  Multi-Vertragspaket, Pointer-Referenzen und vollständige Planinhalte ohne
  Template-Platzhalter.

## Bekannte Ausnahmen

- Der Default-Provider bleibt bis EPIC-04 absichtlich nicht verfügbar. Das ist
  kein fehlender Test, sondern der explizite Adaptervertrag; Tests injizieren
  einen Fake ohne Netzwerk.
- Die Existenz und Projektzuordnung von `solutionPath` kann erst geprüft
  werden, wenn der spätere Provider einen Source-Stand liefert. Dieser Step
  prüft deshalb nur den repository-relativen Pfad und seine Endung.

## Notes

- DRY-, MagicValues- und DeadCode-Funde werden nicht als eigener Sweep geplant.
  Nur ein Fund, der direkt im neuen Mapping-/Validierungscode liegt und ohne
  zusätzliche Vertragsgrenze architektonisch sinnvoll bereinigt werden kann,
  wird im selben Step mitgeführt.
- `SourceFileCatalog`, `AssemblyAnalysisSession`, `AnalysisToolCall` und die
  bestehende `ProjectRegistry` sind Handoff-/Abgrenzungsanker, keine neuen
  Änderungsziele dieses Steps.
- Für C#-Semantik wurden die vorhandenen Typen und Aufrufer über den
  AiNetLinter-MCP gegen das absolute Projektroot
  `C:\Daten\Entwicklung\Ralf\AiNetLinter` geprüft; Textsuche diente nur der
  exakten Dateikontextprüfung.
- Der Coder erstellt den Implementierungscommit in einem deutschen
  Conventional-Commit im Imperativ mit dem Suffix
  `[decompiled-assembly-analysis]`; der Plan selbst wird vom Orchestrator
  separat als Planung committed.
