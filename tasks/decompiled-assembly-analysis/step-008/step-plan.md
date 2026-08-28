---
status: done (pending audit)
type: step-plan
task: decompiled-assembly-analysis
step: 008
corrects: null
title: "Deterministische Source-Match-Auflösung über Project.AssemblyName"
epic: EPIC-03
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-28T19:07:06+02:00
related_to:
  - step-007/step-result.md
  - step-007/step-review.md
context_budget:
  read_first:
    - "tasks/decompiled-assembly-analysis/step-007/step-result.md"
    - "tasks/decompiled-assembly-analysis/step-007/step-review.md"
    - "tasks/decompiled-assembly-analysis/codemap.md"
    - "src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs"
    - "src/AiNetLinter/Mcp/Assemblies/SourceSnapshotModels.cs"
    - "src/AiNetLinter/Mcp/Assemblies/SourceSnapshotRegistry.cs"
    - "src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs"
    - "src/AiNetLinter/Baseline/SourceFileCatalog.cs"
    - "src/AiNetLinter/Baseline/SourceFileCatalogLoader.cs"
    - "src/AiNetLinter.FastTests/Mcp/Assemblies/SourceSnapshotRegistryTests.cs"
    - "src/AiNetLinter.TestKit/RoslynTestSolutionFactory.cs"
  read_on_demand:
    - "src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs — nur zur späteren Verbrauchergrenze; keine Änderung"
    - "src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs — nur zur Bestätigung der späteren Session-/Tool-Komposition; keine Änderung"
    - "src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisSession.cs und AssemblyAnalysisSessionModels.cs — nur falls die Herkunft des späteren Assembly-Alias-Inputs geklärt werden muss"
    - "src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisSessionTests.cs — nur für Testkonventionen, keine Session-Tests"
    - "src/AiNetLinter.TestKit/TestTempDirectory.cs — nur falls ein Test wider Erwarten Dateipfade benötigt"
    - "tasks/decompiled-assembly-analysis/Konzept.md — nur die Abschnitte zu Gitea-Mapping, Source-Match und Source-Snapshot-Identität bei einer offenen Invariante"
  out_of_scope:
    - "AssemblyAnalysisSession, AssemblyAnalysisContextFactory, AssemblyAnalysisService, AnalysisToolCall, MCP-Registrierungen sowie Stdio-/Daemon-/Projekt-Session-Komposition"
    - "Gitea-Clone/Fetch, Authentifizierung, Branch-/Refresh-Logik, Netzwerk, lokale Source-of-Truth, persistenter Source-Cache und Snapshot-Akquisition"
    - "Änderungen an SourceSnapshotIdentity, ExternalSourceSnapshot, SourceSnapshotRegistry, Provider-Port, Mapping-Schema, appsettings.json oder Konfigurationsdokumentation"
    - "Transitive Referenzen, Capability-Matrix, Binär-/PDB-/SourceLink-Versionsbeweis, Decompilation und externe Testausführung"
    - "Project.Name- oder DLL-Dateinamen-Fallbacks; die Auswahl basiert ausschließlich auf explizitem Mapping-Alias und Project.AssemblyName"
    - "Assembly.Load, Reflection-Ausführung, AssemblyLoadContext, Netzwerk, Fremdprojekt-Restore sowie breite DRY-/MagicValues-/DeadCode-Sweeps"
    - "Änderungen an task-state.md, codemap.md, früheren Steps oder weiteren vorausgeplanten Steps"
---

# Step 008: Deterministische Source-Match-Auflösung über Project.AssemblyName

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-03` aus `roadmap.md` — Step 007 hat Snapshot-Identität,
  Registry-Lease und Provider-Ergebnis abgeschlossen; die direkte Zuordnung
  einer Assembly zu einem Projekt der vollständigen Source-Solution ist noch
  offen.
- **Konzept-Referenz:** `Konzept.md` „Gitea-Register und wartungsarmes Mapping“,
  „Source-Auflösung vor der Dekompilation“, „Fingerprint und Cache-Key“ sowie
  Phase 3; umgesetzt wird hier nur die bereits geladene, read-only Solution-
  Matchgrenze.
- **Split-Gate:** Ein primärer Resolver-/Matchvertrag, drei Schichten und
  höchstens acht Akzeptanzkriterien. Provider-, Snapshot-, Session- und
  Kompositionsverträge werden nur konsumiert bzw. als Folgegrenze benannt.

## Aktueller Projektzustand (JIT-Kontext)

Step 007 liefert `ExternalSourceSnapshot` mit read-only Roslyn-`Solution` und
Workspace-Owner. `SourceSnapshotRegistry.Acquire` liefert dafür einen
`SourceSnapshotLease`; die Registry besitzt den Snapshot, der Resolver darf
keine Lease- oder Workspace-Freigabe übernehmen. `ExternalSourceProviderResult`
transportiert den Snapshot bereits optional, wird aber noch von keinem
Produktionskonsumenten ausgewertet.

`ExternalSourceMapping` enthält die von der Validierung normalisierten,
case-insensitiv verglichenen Assembly-Aliase ohne optionales `.dll`-Suffix.
`SourceFileCatalogLoader` lädt eine komplette Solution, während der Snapshot
deren bereits geladene `Solution` direkt exponiert. Im aktuellen Code gibt es
noch keinen Matchresolver und keine fachliche Verwendung von
`Project.AssemblyName`; `AssemblyAnalysisContextFactory` dekompiliert weiterhin
direkt und bleibt die spätere Verbrauchergrenze.

Die bestehende `RoslynTestSolutionFactory` setzt Projektname und AssemblyName
standardmäßig gleich. Für den Alias- und Ambiguous-Fall kann der neue
Resolver-Test deshalb lokal kleine `ProjectInfo`-Werte mit abweichendem oder
doppeltem `assemblyName` erzeugen, ohne TestKit oder Produktionscode zu
erweitern. Ein `tech-debt.md`-Index ist im Task-Verzeichnis nicht vorhanden;
es gibt daher keinen bekannten, anzuhängenden auto-fixable Befund.

## Intention

Ein neuer, synchroner `AssemblySourceMatchResolver` ordnet einen explizit
gemappten Assembly-Alias gegen die `Project.AssemblyName`-Werte einer über eine
Registry geleasten vollständigen Source-Solution zu. Er liefert einen
unveränderlichen Vertrag mit `matched`, `no-match` und `ambiguous`, stabiler
Kandidatenreihenfolge sowie Evidence und Confidence; normale Nichttreffer
werden nicht als Exception behandelt und können später den Decompilation-
Fallback auslösen.

## Kontext-Handoff

### Invarianten

- Der Resolver erhält einen gültigen `ExternalSourceMapping` und einen durch
  `SourceSnapshotRegistry.Acquire` gewonnenen `SourceSnapshotLease`; er liest
  nur `lease.Snapshot.Solution` und gibt weder Snapshot noch Lease frei.
- Vor dem Projektvergleich müssen `RepositoryUrl` und `SolutionPath` des
  Snapshot-Identitätswerts zum Mapping gehören. Ein fremder Snapshot ist
  `no-match`, niemals eine source-backed Auswahl.
- Der angefragte Alias, konfigurierte Mapping-Aliase und `Project.AssemblyName`
  werden für den Vergleich getrimmt, ohne `.dll`-Suffix normalisiert und
  case-insensitiv verglichen. `Project.Name`, Dateiname und Projektpfad sind
  weder Matchsignal noch Fallback.
- Genau ein passender `Project.AssemblyName` ergibt `matched` mit `high`
  Confidence. Kein Kandidat ergibt `no-match`; mehrere Kandidaten ergeben
  `ambiguous` mit allen geordneten Kandidaten und ohne ausgewähltes Projekt.
- Evidence-Codes und Kandidatenreihenfolge sind für gleiche Snapshot- und
  Solution-Daten stabil; das Ergebnis enthält die Snapshot-Identität sowie
  `ProjectId`/Name/AssemblyName/Dateipfad des Kandidaten.
- Der Resolver lädt keine Solution, liest keine DLL und führt keinen Code aus;
  Provider-Akquisition, Binär-/Versionsabgleich und Session-Wiring bleiben
  außerhalb.

### Relevante MCP-Symbole

- `T:AiNetLinter.Configuration.ExternalSourceMapping` — validierter Eingang
  mit expliziten Assembly-Aliasen.
- `T:AiNetLinter.Mcp.Assemblies.SourceSnapshotRegistry` und
  `M:AiNetLinter.Mcp.Assemblies.SourceSnapshotRegistry.Acquire` — Registry-
  Ownership und Lease-Einstieg.
- `T:AiNetLinter.Mcp.Assemblies.SourceSnapshotLease` — read-only Resolver-
  Kontext, dessen Lebensdauer der Aufrufer kontrolliert.
- `T:AiNetLinter.Mcp.Assemblies.ExternalSourceSnapshot` und
  `T:AiNetLinter.Mcp.Assemblies.SourceSnapshotIdentity` — Solution-Wert und
  Snapshot-Zuordnung.
- `M:AiNetLinter.Baseline.SourceFileCatalogLoader.LoadAsync` — bestehendes
  vollständiges Solution-Ladepattern, das hier nicht erneut verwendet wird.
- `T:AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyAnalysisContextFactory` —
  späterer Verbraucher von Source-Match/Fallback, ausdrücklich außerhalb.

### Sicherer Einstiegspunkt

Mit `AssemblySourceMatchResolver.cs` beginnen und darin den immutable Result-
vertrag, die drei Zustände, die Aliasnormalisierung und die deterministische
Projektselektion gemeinsam halten. Danach ausschließlich
`AssemblySourceMatchResolverTests.cs` ergänzen: Snapshot über
`SourceSnapshotRegistry.Acquire` leasen, kleine Adhoc-Solutions aufbauen und
die Zustände sowie Ownership-/Evidence-Invarianten direkt prüfen. Keine
Änderung an Session, Provider, Registry oder MCP-Komposition beginnen.

## Konkrete Änderungen

### Schicht 1 — Matchvertrag in `src/AiNetLinter/Mcp/Assemblies/AssemblySourceMatchResolver.cs` (neu)

- **Was:** Einen internen, unveränderlichen `ExternalSourceMatchResult` mit
  `ExternalSourceMatchState` (`Matched`, `NoMatch`, `Ambiguous`),
  `ExternalSourceMatchConfidence` (`None`, `High`), angefragtem Alias,
  `SourceSnapshotIdentity`, optional ausgewähltem
  `ExternalSourceMatchCandidate`, Kandidatenliste und immutable Evidence
  definieren. Kandidaten tragen mindestens `ProjectId`, `ProjectName`,
  `AssemblyName` und `FilePath`.
- **Warum:** Der spätere Session-Resolver braucht einen maschinenlesbaren
  Fallback- und Diagnosevertrag, statt `null`, Exception oder einen scheinbar
  gültigen beliebigen Project-Treffer zu interpretieren.

### Schicht 2 — Reine Resolverlogik in `src/AiNetLinter/Mcp/Assemblies/AssemblySourceMatchResolver.cs` (neu)

- **Was:** `Resolve(SourceSnapshotLease, ExternalSourceMapping, string)`
  implementieren. Zuerst die Snapshot-Identität gegen URL und
  repository-relativen Solution-Pfad des Mappings prüfen und danach verlangen,
  dass der normalisierte angefragte Alias im expliziten `assemblies`-Array
  vorkommt.
- **Was:** Anschließend alle Projekte der geleasten vollständigen Solution
  nach ihrem `Project.AssemblyName` durchsuchen. `Project.Name` und
  Dateinamen werden nicht als Ersatz verwendet; Projekte ohne AssemblyName
  bleiben unberücksichtigt. Null, ein oder mehrere Treffer werden als
  `NoMatch`, `Matched` oder `Ambiguous` ausgegeben. Ein einzelner Treffer
  erhält `High` für die Kombination aus explizitem Mapping, passender Snapshot-
  Identität und exact-normalisiertem `Project.AssemblyName`; die Evidence-Codes
  werden in fester Reihenfolge geliefert.
- **Was:** Kandidaten vor der Ausgabe nach `FilePath`, `ProjectName` und
  `AssemblyName` mit ordinalem Vergleich sortieren und im Ergebnis nur die
  Roslyn-`ProjectId` zur späteren Auflösung referenzieren. Der Resolver
  übernimmt keine Ownership und verändert keine `Solution`.
- **Warum:** Die vollständige Solution ist der gemeinsame Snapshot-Kontext;
  die Projektwahl bleibt eine reine, reproduzierbare Projektion darauf und
  verschmilzt weder Aliase noch unterschiedliche Snapshot-Identitäten.

### Schicht 3 — Direkte Resolver-Regressionen in `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblySourceMatchResolverTests.cs` (neu)

- **Was:** Mit `SourceSnapshotRegistry`, `SourceSnapshotLease` und kleinen
  In-Memory-Workspaces testen, dass ein `.dll`-Alias auf den passenden
  `Project.AssemblyName` trifft, die Snapshot-Identität erhalten bleibt und
  der geleaste Owner während der Auswertung nicht freigegeben wird.
- **Was:** Direkte No-Match-Fälle für einen nicht konfigurierten Alias, ein
  konfiguriertes Mapping ohne passendes `Project.AssemblyName` und einen
  Snapshot aus einem anderen Mapping absichern. Ein Projektname allein darf
  dabei keinen Treffer erzeugen.
- **Was:** Einen Ambiguous-Fall mit zwei verschiedenen Projektnamen und
  identischem `AssemblyName` prüfen: kein ausgewähltes Projekt, `None`
  Confidence, vollständige deterministisch sortierte Kandidaten und stabile
  Evidence. Die vorhandenen Step-007-Provider-/Registry-Tests bleiben
  unverändert.
- **Warum:** Die gesamte Entscheidung bleibt an der reinen Resolvergrenze
  prüfbar, ohne Gitea, Netzwerk, Solution-Load, MCP-Host, Assembly-Ausführung
  oder Änderungen an der späteren Session-Komposition.

## Tests

- [ ] `Resolve_MatchesConfiguredDllAliasToProjectAssemblyName` — Registry-Lease,
  `.dll`-Normalisierung, `ProjectId`, Snapshot-Identität, `High` und Evidence.
- [ ] `Resolve_DoesNotUseProjectNameAsAssemblyFallback` — abweichender
  Projektname ohne passenden `Project.AssemblyName` bleibt `NoMatch`.
- [ ] `Resolve_ReturnsNoMatchForUnconfiguredAliasAndForeignSnapshot` — explizite
  Mapping-Aliasgrenze und Snapshot-Identitätsabweichung bleiben sichtbar.
- [ ] `Resolve_ReturnsAmbiguousForDuplicateProjectAssemblyNames` — Kandidaten,
  Sortierung, `None` und Evidence sind vollständig und reproduzierbar.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~AssemblySourceMatchResolverTests"`
  läuft deterministisch ohne Netzwerk, Restore eines Fremdprojekts,
  Dateisystem-Temp, Assembly-Laden oder Codeausführung.
- [ ] Abschlussverifikation: `dotnet build`,
  `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und
  `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`.

## Definition of Done

- [ ] Ein immutable Matchvertrag liefert explizit `Matched`, `NoMatch` und
  `Ambiguous` sowie Snapshot-Identität, Kandidaten, Evidence und Confidence.
- [ ] Der Resolver konsumiert ausschließlich einen Registry-`SourceSnapshotLease`,
  prüft dessen Zuordnung zum Mapping und übernimmt keine Workspace-/Lease-
  Ownership.
- [ ] Aliasnormalisierung und explizites `assemblies`-Mapping sind
  case-insensitiv und `.dll`-suffix-tolerant; ein Projektname oder DLL-Dateiname
  kann keinen Ersatztreffer erzeugen.
- [ ] Die Auswahl verwendet nur `Project.AssemblyName`: genau ein Treffer wird
  mit `High` markiert, kein Treffer als `NoMatch`, mehrere als `Ambiguous` ohne
  Zufallsauswahl.
- [ ] Kandidaten und Evidence werden deterministisch geordnet und enthalten die
  spätere Auflösung ermöglichende `ProjectId`- und Projektdaten.
- [ ] Unit-/Component-Regressionen decken Match, Aliasgrenze, Snapshot-Mismatch,
  fehlenden AssemblyName, Ambiguous und Lease-Read-only-Verhalten ohne
  Netzwerk, Runtime-Laden oder Fremdprojekt ab.
- [ ] Session-/MCP-/Provider-/Gitea-/Referenz-/Capability-Grenzen bleiben
  unverändert; es entsteht kein neuer Konfigurations- oder Snapshotvertrag.
- [ ] `dotnet build` sowie beide vollständigen Nicht-Stress-Testläufe sind grün;
  kein `Assembly.Load`, keine Reflection-Ausführung und kein
  `AssemblyLoadContext` wird eingeführt.

## Rules-Refs

- `.agents/rules/AiNetLinter-McpWorkflow.mdc#Verbindliche Priorität` und
  `#Werkzeugwahl` — C#-Symbole, Referenzen und Impact zuerst per MCP mit dem
  absoluten Projektroot prüfen; `rg` bleibt Text-/Dateikontext.
- `.agents/rules/AiNetLinterRichtlinien.mdc#1 Grundprinzipien` und
  `#2 Architektur-Verbote` — immutable Records, direkte kleine Lösung,
  read-only externe Quellen, kein Runtime-Laden, kein Reflection-/ALC-
  Einsatz und keine neue DI-/Plugin-Infrastruktur.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — xUnit-v3,
  zentrale Testinfrastruktur, keine Netzwerktests und vollständige
  Nicht-Stress-Gates.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` und
  `.agents/rules/AiNetLinter.mdc#Kurz-Stil` — kurze Methoden, Result-/State-
  Modell, Zero-Warning-Gate und nur direkt im Resolverpaket sinnvolle
  DRY-/MagicValues-/DeadCode-Bereinigung.
- `tasks/decompiled-assembly-analysis/follow-up-strategy.md#Split-Gate vor dem Coder`
  — ein vertikales, testbares Paket mit höchstens drei Schichten; Session-,
  Akquisitions- und Folge-Epics bleiben getrennt.

## Bekannte Ausnahmen

- Dieser Step beweist keine Binary-/PDB-/SourceLink-Versionstreue. Er matched
  nur den expliziten Alias gegen die bereits geladene Solution und deren
  `Project.AssemblyName`; Akquisition, Refresh und Versionsprüfung bleiben
  bewusst in Folgepaketen.
- Es gibt keinen MCP-/Integrationstest, weil der Resolver noch nicht in
  `AssemblyAnalysisContextFactory`, `AssemblyAnalysisSession` oder Tool-
  Registrierungen verdrahtet wird. Die vollständigen Nicht-Stress-Gates bleiben
  trotzdem Abschlusskriterien für den Coder.
- `Docs/configuration.md` wird nicht geändert, da weder Mapping-Felder noch
  ein öffentlicher MCP-Vertrag in diesem reinen Resolver-Step erweitert werden.

## Code-Skizze (optional)

```csharp
var result = AssemblySourceMatchResolver.Resolve(sourceLease, mapping, assemblyName);
// result.State: Matched | NoMatch | Ambiguous
// result.MatchedCandidate?.ProjectId wird erst im Folgepaket in eine Session projiziert.
```

## Notes

- Die Registry besitzt aktuell keinen Lookup nach Identität; der Resolver erhält
  deshalb bewusst den bereits über `Acquire` gewonnenen Lease-Kontext. Ein
  späterer Provider-/Session-Adapter bleibt für die Lease-Erzeugung zuständig.
- Das Mapping aus dem Loader ist bereits kanonisiert. Die Resolvergrenze darf
  den angefragten Alias und die Projekt-AssemblyName defensiv normalisieren,
  ohne den bestehenden Konfigurationsvertrag oder dessen Validator zu duplizieren.
- `Project.AssemblyName == null` ist kein Name-Match. Die Projektauflösung muss
  bei gleichen AssemblyNames alle Kandidaten zurückgeben und darf nicht die
  Reihenfolge der Roslyn-Solutionenumeration als Auswahlkriterium verwenden.
- DRY-, MagicValues- oder DeadCode-Befunde dürfen nur dann opportunistisch im
  neuen Resolver-/Matchcode bereinigt werden, wenn sie direkt dort liegen und
  ohne eine weitere Vertragsgrenze architektonisch sinnvoll lösbar sind; kein
  separater Audit-Step.
- Der Coder erstellt nach der Umsetzung das reguläre `step-result.md` und den
  deutschen Implementierungscommit mit dem Suffix
  `[decompiled-assembly-analysis]`; dieser Plan enthält keine Session-/MCP-
  Verdrahtung und keine Folgeplanung.
