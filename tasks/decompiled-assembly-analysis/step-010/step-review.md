---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 010
epic: EPIC-03
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5 (Codex)
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-28T21:08:14+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 010: Provider-/Registry-Selection für direkte Assembly-Tools

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Korrektur erforderlich
- [ ] **blocked** — Nutzer-Entscheidung nötig

Der Step ist wegen eines aktuellen In-Scope-Linterverstoßes und nicht
ausreichender Regressionen an der neuen Support-Grenze nicht freigabefähig.
Die Produktionskomposition bleibt ansonsten innerhalb des geplanten
Provider-/Registry-/Lease-Schnitts.

## Geprüfte Bereiche

- [x] Plan-Erfüllung
- [x] Rules-Konformität
- [x] Logische Korrektheit
- [x] Konzept-Treue
- [x] Build-/Test-Gates
- [x] DRY-, Magic-Value- und Dead-Code-Prüfung im direkt angefassten
  Composition-/Lease-Scope

## Befund: Plan-Erfüllung

Der tatsächliche Codecommit `28b7b76d792efaf9993fe5d7b85ccdeee0513247`
enthält ausschließlich den Orchestrator/Scope, die direkte Support-
Überladung und die sechs vorgesehenen Component-Tests. Die Mapping-Auswahl
nutzt die metadata-only Identität, ruft den Provider nur für genau ein
gemapptes Mapping auf, registriert Snapshots nur nach dem Availability-Gate
und reicht die Auswahl an die bestehende Factory weiter. Der Legacy-Overload
und die MCP-/Daemon-Hostpfade bleiben unverändert.

Die Abnahme ist dennoch nicht vollständig: Der als
`ExecuteAsync_HoldsSelectionLeaseThroughResultBuilderAndReleasesItOnce`
bezeichnete Test ruft nur `ResolveAsync` direkt auf. Er prüft weder die neue
`AssemblyAnalysisToolSupport.ExecuteAsync`-Überladung noch Factory-/Result-
Builder-Ausnahme- oder Cancellation-Pfade. Auch NoMatch und Ambiguous werden
nur am Orchestrator-Scope geprüft, nicht als Support-Fallback.

## Befund: Rules-Konformität

Die geprüften Architektur- und Sicherheitsgrenzen sind eingehalten: kein
Runtime-Laden, keine Reflection-Ausführung, kein `AssemblyLoadContext`, kein
Netzwerk, keine Gitea-Akquisition, kein Fremdprojekt-Restore und keine
Host-/MCP-Registrierungsänderung. Der direkte Support hat keine offenen
Violations.

Die aktuelle MCP-Linterprüfung meldet jedoch auf der neuen
`AssemblySourceSelectionOrchestrator`-Klasse weiterhin `StaticTestSentinel`.
Die vorhandenen Tests sind funktional relevant, werden wegen der Klasse
`AssemblyAnalysisToolSupportTests` aber statisch nicht dieser Orchestrator-
Klasse zugeordnet.

## Befund: Logische Korrektheit

Die Orchestrator-Logik ist für die geprüften Pfade stimmig: Sie verwendet
`AssemblyReferenceResolver.Resolve` zur metadata-only Identitätswahl, das
unveränderte Provider-Ergebnis, `SourceSnapshotRegistry.Acquire` sowie den
bestehenden Match-/Selection-Vertrag. `Matched`, `NoMatch`, `Ambiguous`,
unavailable und ein ungültiger Loader führen deterministisch zur vorhandenen
Decompilation; Loader-/Providerdiagnosen werden dedupliziert in den Context
übernommen. Match-State und Evidence bleiben im transportierten
`Selection.MatchResult` erhalten. Der `using`-Scope umfasst in der neuen
Überladung Factory und Result-Builder und gibt idempotent frei.

Die Testlücke lässt aber eine Regression genau an dieser Grenze zu: Ein
vorzeitig entfernter Scope, ein verlorenes `BuildResult`-Cleanup oder ein
falsch weitergereichter NoMatch-/Ambiguous-Selectionwert würde durch die
aktuelle direkte Scope-Prüfung nicht zuverlässig auffallen.

## Befund: Konzept-Treue

Die Umsetzung bleibt bei der vorgelagerten Source-Auswahl und der read-only
Snapshot-/Lease-Grenze. Sie wählt kein Projekt über Name oder Pfad, übernimmt
keine Provider-/Registry-Ownership und zieht weder MCP-/Daemon-Wiring noch
transitive Referenzen, Gitea, Netzwerk oder Capability-Matrix vor. Die
weitergehende Match-Evidence-Ausgabe im öffentlichen MCP-Payload bleibt wie
im Step-Plan festgelegt außerhalb dieses Composition-Steps.

## Findings

### 1. Orchestrator verletzt die aktive Testabdeckungsregel

- **Severity:** MAJOR
- **Ebene:** Rules-Konformität / Plan-Erfüllung
- **Ort:** `src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelectionOrchestrator.cs:13`
- **Beleg:** Zielgebundener MCP-`get_violations` meldet `StaticTestSentinel`.
  `get_test_context` findet für das Symbol null statisch zugeordnete
  Testdateien, obwohl die Tests nur in
  `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs`
  liegen und weder eine passende Testklasse noch `typeof`/`@covers`
  verwenden.
- **Remediation:** Die bestehende direkte Testklasse explizit über eine
  zulässige `@covers AssemblySourceSelectionOrchestrator`-Zuordnung markieren
  oder eine fokussierte `AssemblySourceSelectionOrchestratorTests`-Klasse
  anlegen. Danach `get_violations` für den Orchestrator erneut ausführen;
  keine Suppression verwenden.

### 2. Support-/Lease-Lifetime und Orchestrator-Fallbacks sind nicht an der gemeinsamen Grenze getestet

- **Severity:** MAJOR
- **Ebene:** Plan-Erfüllung / logische Korrektheit
- **Ort:** `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs:65-109,197-223`
- **Beleg:** Der Test `ExecuteAsync_HoldsSelectionLeaseThroughResultBuilderAndReleasesItOnce`
  ruft in den Zeilen 87 und 93 ausschließlich
  `orchestrator.ResolveAsync` auf und disposiert die zurückgegebenen Scopes
  manuell. Die Zeilen 197-223 prüfen NoMatch/Ambiguous ebenfalls nur über
  `ResolveAsync`. Kein Test ruft dort die neue
  `AssemblyAnalysisToolSupport.ExecuteAsync(parameters, orchestrator)`-
  Überladung auf, beobachtet die Selection während Factory und
  `BuildResult` oder prüft Freigabe nach einem Builder-/Cancellation-Fehler.
  Die vorhandenen Factory-Tests aus Step 009 ersetzen diese gemeinsame
  Support-Grenze nicht.
- **Remediation:** Die Component-Regressionen so ergänzen bzw. umstellen,
  dass sie die neue Support-Überladung für Matched, NoMatch und Ambiguous
  ausführen, den Lease während Factory/Builder beobachten und die einmalige
  Freigabe nach normalem, gecanceltem und fehlerhaftem Result-Build
  nachweisen. Unavailable, invalid Loader, Provider-Cancellation,
  Providerdiagnosen, Deduplizierung und die bestehende Decompilation müssen
  dabei weiterhin mit den vorhandenen Assertions erhalten bleiben.

## Build-/Test-Status

- `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~AssemblyAnalysisToolSupportTests"` — **grün**, 6/6.
- `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~AssemblyAnalysis"` — **grün**, 30/30 bestehende Assembly-Regressionen.
- `dotnet build` — **grün**, 0 Warnungen, 0 Fehler.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` — **grün**, 1917/1917.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` — **grün**, 360/360, 3 m 5 s.
- Stress-Tests wurden **nicht** ausgeführt.

Der MCP-`get_impact`-Aufruf mit dem korrekten lokalen Vollhash lieferte
fälschlich „kein Git-Repository oder leerer Diff“; die Diffprüfung erfolgte
deshalb mit `git show 28b7b76d792efaf9993fe5d7b85ccdeee0513247`. Das Verhalten
wurde als Observability-Feedback protokolliert. Der in `step-result.md`
angegebene Codecommit `28b7b76d4f9025495a6f1089954e14b42a9e0ca2` ist kein
lokal auflösbarer Commit; der korrekte Hash steht oben.

## DRY-, Magic-Value- und Dead-Code-Prüfung

Der begrenzte `find_duplicates`-Audit im Assembly-Composition-/Lease-Bereich
fand keine Duplikat-Cluster; die Orchestrator- und Support-Dateien enthielten
keine sicher zu zentralisierenden Magic Values. `find_dead_code` meldete nur
`CreateFromSettings` mit LOW-Confidence. Dieser Einstieg ist die geplante
spätere MCP-/Host-Kompositionsgrenze und wird nicht als Dead-Code-Finding
oder Tech-Debt-Eintrag erfasst. `TD-001`, `TD-002` und `TD-003` bleiben
unverändert; es gibt keinen neuen Tech-Debt-Eintrag.
