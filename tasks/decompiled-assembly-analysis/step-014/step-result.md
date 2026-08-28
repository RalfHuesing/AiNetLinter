---
status: done (pending audit)
type: step-result
task: decompiled-assembly-analysis
step: 014
corrects: null
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-28
code_commit_hash: 3f83c5f2
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 014: Injizierbaren External-Source-Port für Gitea-Auth- und Transportfehler schärfen

## Zusammenfassung

Der bestehende `IExternalSourceProvider`-Port transportiert jetzt die stabile
Failure-Klassifikation `None`, `ProviderUnavailable`, `AuthenticationRequired`,
`AccessDenied`, `RepositoryNotFound`, `NetworkUnavailable`, `Timeout` und
`InvalidResponse`. Verfügbare Ergebnisse behalten `None`; Fehlerergebnisse
bleiben ohne Snapshot. Der bisherige parameterlose Nichtverfügbarkeits-Pfad wird
rückwärtskompatibel als `ProviderUnavailable` normalisiert.

Der Orchestrator reicht den typisierten Providerzustand über den bestehenden
`AssemblySourceSelectionScope` weiter. Bei nicht quellfähigen Ergebnissen
bleiben Providerdiagnosen sichtbar, die Registry bleibt lease-frei und die
statische Decompilation wird unverändert verwendet.

## Änderungen

- `src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs`
  - `ExternalSourceProviderFailureKind` ergänzt.
  - `ExternalSourceProviderResult` um `FailureKind` erweitert.
  - Erfolgs-/Fehler-/Snapshot-Invarianten validiert; die bestehende
    Konstruktorreihenfolge bleibt kompatibel.
- `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs`
  - stabile Diagnosecodes für Authentifizierung, Zugriff, Repository, Netzwerk,
    Timeout und ungültige Providerantwort ergänzt.
- `src/AiNetLinter/Mcp/Assemblies/UnavailableExternalSourceProvider.cs`
  - liefert explizit `ProviderUnavailable`; Mapping und Cancellation bleiben
    unverändert.
- `src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelectionOrchestrator.cs`
  - transportiert `ProviderFailureKind` im bestehenden Scope, ohne neue
    Providerinstanz, Host-Komposition oder Akquisitionslogik.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceProviderContractTests.cs`
  - deterministisches Recording-Double und Vertragsprüfungen für alle sieben
    Fehlerzustände, `None`, Snapshot-Freiheit, Ownership, Kompatibilität,
    Diagnosecodes und Cancellation.
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs`
  - fokussierter Regressionstest für Diagnoseweitergabe, typisierten Scope,
    fehlende Selection/Leases und den Decompilation-Fallback.

Mapping-JSON, Credentials, Registry-/Session-Lifetime, Source-of-Truth,
Snapshot-Erzeugung, Netzwerk-/Git-Zugriff und produktive Gitea-Akquisition
wurden nicht erweitert.

## Tests

- Fokussierte Provider-/Fallback-Regressionen: 28/28 bestanden.
- `dotnet build`: grün, 0 Warnungen, 0 Fehler.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`: 1937/1937 bestanden.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`: 360/360 bestanden.
- Stress-Tests wurden nicht ausgeführt.

## Tech-Debt

Es wurde kein neuer Tech-Debt-Eintrag angelegt. `TD-001` bis `TD-003` bleiben
unverändert; `TD-004` bleibt gemäß Step-013-Review erledigt. Ein breitflächiger
DRY-, Magic-Value- oder Dead-Code-Sweep war nicht Bestandteil dieses
Vertragsschnitts.

## Abweichungen vom Plan

- `ExternalSourceConfigurationLoader.cs` und
  `ExternalSourceMappingValidator.cs` wurden nur auf unveränderte Mapping- und
  Credential-Grenzen geprüft; eine Anpassung war nicht erforderlich.
- `AssemblyAnalysisHostComposition.cs` blieb unverändert, weil der bestehende
  Orchestrator-/Provider-Port die Klassifikation ohne neue Host-Komposition
  weitergeben kann.
- Es wurde keine zusätzliche Support-Testdatei angelegt; die bestehende
  `AssemblyAnalysisToolSupportTests`-Grenze deckt den Fallback direkt ab.

## Bekannte Unschärfen

- `ExternalSourceProviderResult` synthetisiert keine Diagnose. Der typisierte
  `FailureKind` ist die maschinenlesbare Klassifikation; konkrete Adapter liefern
  die passende bestehende `ExternalSourceConfigurationDiagnostic` mit dem
  stabilen Diagnosecode.
- Ein verfügbares Ergebnis ohne Snapshot bleibt aus Kompatibilitätsgründen
  zulässig und trägt `None`; der Orchestrator behandelt es wie bisher als nicht
  quellfähigen Scope und verwendet die Decompilation.
- Der nachgelagerte Kritiker-/Drift-Audit steht noch aus.

## Commits

- **Code-/Test-Commit:** `3f83c5f2`
- **Message:** `feat: Gitea-Fehlerzustände typisieren [decompiled-assembly-analysis]`
- **Doku-Commit:** folgt als separater Commit mit Step-Ergebnis und
  Planstatus.
- **Branch:** `main`
- **Push:** nein

## Auditstatus

`done (pending audit)` — der nachgelagerte Kritiker-/Drift-Audit steht noch aus.
