---
status: done
type: step-plan
task: decompiled-assembly-analysis
step: 014
corrects: null
title: "Injizierbaren External-Source-Port für Gitea-Auth- und Transportfehler schärfen"
epic: EPIC-04
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-28T23:28:41+02:00
related_to:
  - step-013/step-result.md
  - step-013/step-review.md
  - roadmap.md
---

# Step 014: Injizierbaren External-Source-Port für Gitea-Auth- und Transportfehler schärfen

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04` aus `roadmap.md` — der erste kontextbegrenzte Schnitt
  für die Gitea-Provider-Grenze nach dem abgeschlossenen EPIC-03.
- **Konzept-Referenz:** `Konzept.md`, Phase 4 „Gitea als konfigurierte
  Source-of-Truth“; insbesondere Authentifizierung, sichtbare
  Netzwerk-/Korruptionsfehler und deterministische Test-Doubles.

## Aktueller Projektzustand (JIT-Kontext)

EPIC-03 besitzt bereits genau eine Injektionsgrenze:
`IExternalSourceProvider.ResolveAsync(ExternalSourceMapping, CancellationToken)`.
`ExternalSourceProviderResult` transportiert derzeit nur `IsAvailable`,
Diagnosen und optional einen bereits geladenen `ExternalSourceSnapshot`.
`UnavailableExternalSourceProvider` liefert den bestehenden
`ProviderUnavailable`-Zustand; Cancellation wird als
`OperationCanceledException` erhalten.

`ExternalSourceMapping` und der strikte Loader erlauben ausschließlich `url`,
`solutionPath` und `assemblies`; es gibt weder Credential-Felder noch einen
produktiven HTTP-/Git-/Gitea-Provider. Der Orchestrator ruft den injizierten
Provider nur nach eindeutiger Assembly-Zuordnung auf und fällt bei einem nicht
verfügbaren oder nicht-quellfähigen Ergebnis auf statische Decompilation zurück.
Registry, Snapshot-Ownership, Host-Komposition und registriertes Multi-Session-
Wiring sind durch `step-013` abgeschlossen und werden wiederverwendet.

Der vorhandene `RecordingProvider` in
`ExternalSourceProviderContractTests` ist der passende deterministische Test-
Double-Einstiegspunkt. `TD-001` (Pfadnormalisierung), `TD-002`/`TD-003`
(Origin-Vertrag) und der bereits erledigte `TD-004` liegen außerhalb dieses
Portschnitts.

## Intention

Der bestehende Provider-Port soll erwartete Gitea-nahe Zustände typisiert
unterscheiden können, ohne eine zweite Provider-Abstraktion oder bereits eine
Netzwerkimplementierung einzuführen. Authentifizierungs-, Zugriffs-,
Netzwerk-, Timeout- und Protokollfehler bleiben sichtbare, deterministische
Nicht-Source-Ergebnisse; Cancellation bleibt eine echte Abbruchsemantik.

## Split-Gate

- **Primärverträge:** genau ein eng gekoppeltes Vertragspaar aus dem bereits
  vorhandenen `IExternalSourceProvider`-Port und
  `ExternalSourceProviderResult`; kein zweites Gitea-/DI-Interface.
- **Schichten:** (1) Provider-Ergebnis und stabile Failure-Klassifikation,
  (2) bestehender Orchestrator-/Fallback-Transport, (3) deterministische
  Vertrags- und Regressionstests.
- **Akzeptanzkriterien:** genau acht, explizit unten aufgeführt; die Testliste
  beschreibt nur deren Verifikation.
- **Kontextbudget:** höchstens zwölf `read_first`-Dateien; die vollständige
  Liste steht unten. Weitere Dateien werden nur bei einem konkreten Symbol-
  oder Testbezug nachgeladen.

## Akzeptanzkriterien

1. `IExternalSourceProvider` bleibt die einzige Provider-Injektionsgrenze;
   es entsteht kein zweites Gitea-/DI-Interface.
2. `ExternalSourceProviderResult` kann `None`,
   `ProviderUnavailable`, `AuthenticationRequired`, `AccessDenied`,
   `RepositoryNotFound`, `NetworkUnavailable`, `Timeout` und
   `InvalidResponse` typisiert transportieren.
3. Fehlerergebnisse sind snapshot-frei und diagnostizierbar; erfolgreiche
   Ergebnisse behalten die bestehende Snapshot-/Ownership-Invariante.
4. Der bestehende Default-Provider und das vorhandene deterministische Double
   erfüllen den erweiterten Result-Vertrag einschließlich Cancellation.
5. Der Orchestrator macht typisierte Providerfehler im Scope sichtbar und
   bewahrt den lease-freien statischen Decompilation-Fallback.
6. Mapping-JSON, Credential-Speicherung, Host-/Session-Lifetime, Registry,
   Lease und Source-of-Truth werden in diesem Step nicht erweitert.
7. Es gibt keinen Netzwerk-/Git-Zugriff, keinen produktiven Gitea-Adapter und
   keinen Clone/Fetch/Refresh- oder Cache-/Veröffentlichungscode.
8. Die vollständigen Nicht-Stress-Build-/Test-Gates laufen grün, ohne
   Integration dieses Steps in einen Stress- oder echten MCP-Netzwerktest.

## Kontextbudget

### `read_first`

1. `tasks/decompiled-assembly-analysis/step-013/step-result.md`
2. `tasks/decompiled-assembly-analysis/step-013/step-review.md`
3. `tasks/decompiled-assembly-analysis/Konzept.md` — Phase 4 und Teststrategie
4. `src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs`
5. `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs`
6. `src/AiNetLinter/Configuration/ExternalSourceMappingValidator.cs`
7. `src/AiNetLinter/Configuration/ExternalSourceConfigurationLoader.cs`
8. `src/AiNetLinter/Mcp/Assemblies/UnavailableExternalSourceProvider.cs`
9. `src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelectionOrchestrator.cs`
10. `src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisHostComposition.cs`
11. `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceProviderContractTests.cs`
12. `src/AiNetLinter.FastTests/Configuration/ExternalSourceConfigurationLoaderTests.cs`

### `read_on_demand`

- `src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelection.cs` und
  `SourceSnapshotModels.cs` — nur zur Prüfung, dass Snapshot- und Lease-
  Ownership nicht in den Providerfehlervertrag rutscht.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupport.cs`
  sowie die bestehende Support-Testdatei — nur für den Nachweis, dass der
  unveränderte Decompilation-Fallback die Diagnosen weiter sichtbar macht;
  keine Ausweitung eines bereits großen Tests ohne direkten Bezug.
- `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotRegistry.cs` — nur falls ein
  Test versehentlich Registry-Besitz berührt.
- `Docs/configuration.md` und `Docs/agent-api.md` — nur falls die bestehende
  interne Diagnose-/Providerbeschreibung durch die konkrete Implementierung
  tatsächlich nutzerseitig geändert wird.
- `Directory.Packages.props` und das Produktionsprojekt — nur zur Prüfung,
  dass für den reinen Port-/Double-Schnitt keine neue Netzwerk-/Git-Abhängigkeit
  erforderlich ist.

### `out_of_scope`

- Konkreter Gitea-Provider, `HttpClient`, Git-CLI/-Bibliothek, Clone, Fetch,
  Default-Branch-Auflösung, Refresh-Intervall und produktive Credential-
  Auflösung oder Secret-Speicherung.
- Änderung des Mapping-JSON-Schemas, insbesondere neue Auth-/Token-/Profile-
  Felder; keine Geheimnisse in Mapping, Result, Diagnosen oder Logs.
- Persistenter Source-Cache, Cache-Root, Snapshot-Erzeugung aus einem Checkout,
  Solution-/MSBuild-Akquisition, atomare Veröffentlichung, Manifest-/Korruptions-
  handling und konkurrierende Generationen.
- Dirty-/unbuilt-Checkout-Regeln, Source-of-Truth-Entscheidung, Health-/Capacity-
  Semantik, transitive Referenzen, Capability-Matrix sowie weitere MCP-Tools.
- Änderungen an Host-Wiring, Registration, `AnalysisToolCall`, Registry,
  `AssemblySourceSelection`, Factory, Decompilation, `task-state.md`, `codemap.md`,
  `tech-debt.md` oder früheren Steps.
- TD-001 bis TD-003 und breit angelegte DRY-, MagicValues- oder DeadCode-
  Sweeps; nur ein unmittelbar notwendiger, architektonisch sicherer Fix wäre
  im Paket zulässig, ist nach aktuellem Befund aber nicht eingeplant.

## Konkrete Änderungen

### Schicht 1: Provider-Ergebnis und Failure-Klassifikation

#### `src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs`

- **Was:** Einen internen `ExternalSourceProviderFailureKind` mit den stabilen
  Zuständen `None`, `ProviderUnavailable`, `AuthenticationRequired`,
  `AccessDenied`, `RepositoryNotFound`, `NetworkUnavailable`, `Timeout` und
  `InvalidResponse` ergänzen. `ExternalSourceProviderResult` erhält die
  typisierte Failure-Klassifikation neben den bestehenden Feldern.
- **Warum:** Ein späterer Gitea-Adapter braucht eine auswertbare Fehlersemantik,
  ohne Fehlertexte zu parsen oder eine zweite Provider-Schnittstelle zu erfinden.
- **Invarianten:** Erfolgreiche Ergebnisse haben `None`; nicht verfügbare
  Ergebnisse tragen keinen Snapshot und normalisieren den alten parameterlosen
  Pfad auf `ProviderUnavailable`. Ein Snapshot bleibt ausschließlich ein
  verfügbarer, bereits geladener Snapshot. Cancellation wird nicht in einen
  Failure-Wert umgewandelt.

#### `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs`

- **Was:** Stabil benannte Diagnoseschlüssel für die Failure-Klassen ergänzen,
  soweit der bestehende Diagnosevertrag sie noch nicht abdeckt; den vorhandenen
  `ProviderUnavailable`-Schlüssel kompatibel erhalten.
- **Warum:** Nutzer- und Testausgaben müssen die typisierte Klassifikation
  deterministisch und ohne Secret-/Transportdetails sichtbar machen.

### Schicht 2: Bestehender Orchestrator-/Fallback-Transport

#### `src/AiNetLinter/Mcp/Assemblies/UnavailableExternalSourceProvider.cs`

- **Was:** Das bestehende Fallback-Ergebnis explizit als
  `ProviderUnavailable` klassifizieren; Mapping-Prüfung und Cancellation-
  Verhalten unverändert lassen.
- **Warum:** Der Default-Port soll denselben vollständigen Vertrag wie ein
  späterer deterministischer Gitea-Double erfüllen.

#### `src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelectionOrchestrator.cs`

- **Was:** Den typisierten Providerfehler durch den vorhandenen Selection-
  Scope transportieren, ohne neue Akquisitionslogik einzuführen. Der Scope
  behält bei jedem nicht-quellfähigen Ergebnis die Diagnose und den statischen
  Decompilation-Fallback; Leases und Snapshot-Ownership bleiben unverändert.
- **Warum:** Nachgelagerte Provider-/Capability-Schritte können den Zustand
  auswerten, ohne den bestehenden Host- oder Tool-Vertrag zu umgehen.

### Schicht 3: Deterministische Vertrags- und Regressionstests

#### `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceProviderContractTests.cs`

- **Was:** Den vorhandenen `RecordingProvider` zu einem skriptbaren Double für
  Success und alle typisierten, nicht-quellfähigen Ergebnisse erweitern.
  Tests für Result-Invarianten, `UnavailableExternalSourceProvider`,
  Mapping-/Cancellation-Weitergabe, fehlende Snapshots bei Fehlern, stabile
  Diagnosecodes und unveränderte `OperationCanceledException` ergänzen.
- **Warum:** Der Port wird ohne Netzwerk, Git-Prozess, Schlafen oder unbounded
  Retry deterministisch gegen den späteren Adapter abgesichert.
- **Was nicht:** Keine zweite Fake-Implementierung und kein Test echter Gitea-
  Erreichbarkeit.

#### Bestehende Orchestrator-/Support-Regressionen

- **Was:** Einen eng begrenzten Test ergänzen oder die bestehende fokussierte
  Abdeckung erweitern, der für ein typisiertes Failure weiterhin sichtbare
  Providerdiagnose, keinen Source-Selection-Lease und den vorhandenen
  Decompilation-Fallback bestätigt. Dafür ist die kleinste bestehende
  Testgrenze zu verwenden; keine Mini-Sweep-Ausweitung.
- **Warum:** Die neue Klassifikation darf die in EPIC-03 abgeschlossene
  Fallback- und Lifetime-Semantik nicht verändern.

## Tests

- [ ] `ExternalSourceProviderContractTests` prüft Success-, Failure- und
  Snapshot-Invarianten einschließlich aller acht Failure-Klassifikationen.
- [ ] `UnavailableExternalSourceProvider` liefert weiterhin den stabilen
  `ProviderUnavailable`-Code, ohne Snapshot; Cancellation wirft weiterhin
  `OperationCanceledException`.
- [ ] Das deterministische Double erhält exakt Mapping und CancellationToken;
  Tests verwenden weder Netzwerk noch Git-Prozess noch Zeitverzögerungen.
- [ ] Ein fokussierter Orchestrator-/Support-Test bestätigt Failurediagnose,
  keinen Lease und unveränderten statischen Decompilation-Fallback.
- [ ] `dotnet build` ist grün.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` ist grün.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
  ist grün; Integration bleibt dabei unverändert, dient aber dem Abschluss-
  Gate des Repository-Workflows.
- [ ] Kein MCP-/Host-/Produktionsnetzwerk-Test und kein Stress-Test wird in
  diesen Port-Schnitt aufgenommen.

## Definition of Done

- [ ] Der bestehende `IExternalSourceProvider` bleibt die einzige Provider-
  Injektionsgrenze; es existiert kein zweites Gitea-/DI-Interface.
- [ ] Auth-/Zugriffs-, Repository-, Netzwerk-, Timeout- und Protokollfehler
  sind typisiert, diagnostizierbar und ohne Snapshot; Cancellation bleibt
  echte Cancellation.
- [ ] Mapping-JSON, Credential-Speicherung, Host-Komposition, Registry-, Lease-
  und Source-Ownership-Vertrag bleiben unverändert.
- [ ] Der Default-Provider und das vorhandene deterministische Test-Double
  erfüllen denselben Result-Vertrag; keine produktive Gitea-Akquisition ist
  enthalten.
- [ ] Der EPIC-03-Fallback bleibt bei nicht-quellfähigen Ergebnissen sichtbar
  und lease-frei.
- [ ] Die acht Split-Gate-Akzeptanzpunkte und die drei Schichten bleiben auf
  diesen Vertragsschnitt begrenzt.
- [ ] Build und beide vollständigen Nicht-Stress-Testläufe sind grün.
- [ ] `step-014/step-result.md` und Review-Artefakt werden nach der Umsetzung
  geschrieben; der Step-Status wird erst nach Audit auf den projektüblichen
  Abschlussstatus gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinter-McpWorkflow.mdc` — für C#-Symbol-/Referenzfragen
  zuerst AiNetLinter-MCP mit absolutem `projectRoot`; keine Assembly-Ausführung,
  kein `Assembly.Load`, keine Reflection-Ausführung und kein
  `AssemblyLoadContext`.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — einfache Architektur,
  injizierbare Verträge, Result-/Diagnosemodell, Cancellation, deterministische
  Tests und kein vorgezogener Netzwerk-/Git-Stack.
- `.agents/rules/AiNetLinter.mdc` — Nullable-/Warning-/Footprint-Grenzen,
  Testabdeckung und Vermeidung stiller Fehlerbehandlung.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/skills/planer/SKILL.md` —
  genau ein nächster Step, JIT-Kontext, Split-Gate und Handoff.
- `tasks/decompiled-assembly-analysis/follow-up-strategy.md` — EPIC-04 zuerst
  als Provider-/Auth-/Fehlervertrag, danach getrennte Acquisition-/Refresh-
  und Veröffentlichungs-/Source-of-Truth-Pakete.

## Bekannte Ausnahmen

- Die bestehende `AIContextFootprint`-Überschreitung in
  `DaemonHostCommand.cs` bleibt außerhalb des Steps; sie gehört nicht zum
  Provider-Port und darf nicht als Mini-Sweep angehängt werden.
- Falls eine bestehende Diagnosekonvention die Failure-Codes bereits anders
  zentralisiert, ist diese vorhandene Stelle zu verwenden; keine parallele
  Code-Tabelle anlegen.

## Handoff

### Invarianten für den Coder

- Der einzige Provider-Einstieg bleibt
  `IExternalSourceProvider.ResolveAsync`; keinen zweiten generischen oder
  Gitea-spezifischen Port einführen.
- `ExternalSourceProviderResult` mit `SourceSnapshot` bedeutet weiterhin
  „bereits geladener, verfügbarer Snapshot“. Jeder Failure-Zustand ist
  snapshot-frei und liefert stabile Diagnosen.
- Authentifizierungsdaten, Tokens und Secret-Material dürfen weder im Mapping,
  Result, Scope, Diagnostic-Text noch Log erscheinen. Ein Auth-Profil gehört
  erst in den späteren Akquisitions-/Credential-Schnitt.
- `CancellationToken` wird geprüft und weitergereicht; Cancellation wird nicht
  als ProviderUnavailable, Timeout oder Netzwerkfehler maskiert.
- Registry, Lease, Match, Factory, Decompilation-Fallback und die gemeinsame
  Host-/Multi-Session-Lifetime bleiben fachlich unverändert.
- Es gibt in diesem Step keinen Netzwerkzugriff, keinen Git-Prozess und keinen
  produktiven Source-of-Truth.

### Relevante MCP-Symbole

- `T:AiNetLinter.Mcp.Assemblies.IExternalSourceProvider`
- `T:AiNetLinter.Mcp.Assemblies.ExternalSourceProviderResult`
- `T:AiNetLinter.Configuration.ExternalSourceMapping`
- `T:AiNetLinter.Configuration.ExternalSourceConfigurationDiagnosticCodes`
- `T:AiNetLinter.Mcp.Assemblies.UnavailableExternalSourceProvider`
- `T:AiNetLinter.Mcp.Assemblies.AssemblySourceSelectionOrchestrator`
- `M:AiNetLinter.Mcp.Assemblies.AssemblySourceSelectionOrchestrator.ResolveAsync`
- `T:AiNetLinter.Mcp.Assemblies.AssemblyAnalysisHostComposition` — nur als
  unveränderte Lifetime-Grenze.

### Sicherer Einstiegspunkt

1. `IExternalSourceProvider.cs` öffnen und Failure-Enum sowie Result-Invarianten
   ausformulieren; die bestehende Konstruktor-Kompatibilität bewusst erhalten.
2. `ExternalSourceConfiguration.cs` und
   `UnavailableExternalSourceProvider.cs` an die stabilen Diagnosen anbinden.
3. Den kleinsten notwendigen Orchestrator-Transport ergänzen und unmittelbar
   danach `ExternalSourceProviderContractTests` mit dem vorhandenen
   `RecordingProvider` erweitern. Erst danach den fokussierten Fallback-Test
   ausführen; Host-Wiring und produktive Akquisition nicht öffnen.

## Code-Skizze (optional)

```csharp
internal enum ExternalSourceProviderFailureKind
{
    None,
    ProviderUnavailable,
    AuthenticationRequired,
    AccessDenied,
    RepositoryNotFound,
    NetworkUnavailable,
    Timeout,
    InvalidResponse,
}
```

## Notes

- Dies ist ein Port-/Vertragsschnitt, kein Gitea-Featureblock. Insbesondere
  `authProfile`, Token-Lookup, Clone/Fetch/Refresh und lokale Cachepfade werden
  nicht vorweggenommen.
- Die Fehlerklassifikation ist fachlich stabil, die konkrete Zuordnung von
  HTTP-/Git-Fehlern erfolgt erst im späteren Akquisitionsschnitt. Retry-
  Entscheidungen und Backoff gehören ebenfalls dorthin.
- `TD-001` bis `TD-003` bleiben offen. `TD-004` ist erledigt und wird nicht
  erneut bearbeitet.
