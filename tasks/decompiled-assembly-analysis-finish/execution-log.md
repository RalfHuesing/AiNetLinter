# Ausführungsprotokoll: Einheitlicher Roslyn-Analysepfad

Dieses Protokoll ist append-only. Es enthält den für einen Resume-Lauf
relevanten Ereignis- und Feedbackstand; die knappe Ausführungssteuerung bleibt
in `roadmap.md`.

## 2026-08-30 — Resume-Stand und Blockerpersistenz

- Run-ID: `resume-2026-08-30-assembly-analysis`
- Betriebsart: Großkonzept
- Epic: 1 — Gemeinsame Target-, Session- und Roslyn-Route
- Status: `blocked`
- Baseline des Implementierungsstands: `a0d02cef`
- Letzte auftragsbezogene Commits: `109210f7`, `51d8f1ff`, `d99a7d98`,
  `366e2c33`
- Der aktuelle Working Tree wurde in sinnvolle Checkpoint-Commits aufgeteilt;
  es bestehen keine uncommitteten Änderungen.

### Letztes Review-Urteil: `issues`

Der unabhängige Reviewer hat den Epic-Commit nicht freigegeben. Die folgenden
beiden P1-Befunde sind der aktuelle Resume-Einstiegspunkt und müssen vor jeder
Fortsetzung von Epic 1 behoben und fokussiert erneut reviewed werden.

#### P1 — Cancellation-Propagation

Betroffene Stellen:

- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.cs:77-79`
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.cs:250-252`
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.cs:128-135`

In `AssemblyAnalysisSession.RefreshAsync`, beim Aufbau des Roslyn-Snapshots
und beim Warten auf die Registry-Creation wird `OperationCanceledException`
abgefangen und in ein normales Failure-Ergebnis umgewandelt. Ein abgebrochener
oder timeoutender MCP-Aufruf erscheint dadurch als regulärer Analysefehler,
statt die kooperative Cancellation an den aufrufenden Layer weiterzugeben.

Der beabsichtigte Vertrag ist:

1. bereits erworbene Ressourcen best-effort und isoliert bereinigen,
2. danach `OperationCanceledException` weiterwerfen,
3. bei einem shared Creation-Wait nur den abbrechenden Caller vom Warten lösen;
   die gemeinsame Creation darf weiterlaufen, sofern sie nicht selbst beendet
   wurde.

Erforderliche Korrektur: Die Cancellation-Catches müssen nach der notwendigen
Cleanup-Logik erneut werfen beziehungsweise die Caller-Cancellation von einem
echten Creation-Abbruch unterscheiden. Ergänzende Tests müssen mindestens
Session-Refresh, Registry-Lease/Creation-Wait und Cleanup-Verhalten abdecken.

#### P1 — Assembly-Identität bei `get_type_hierarchy`

Betroffene Stellen:

- `src/AiNetLinter/Mcp/Tools/SymbolGraph/GetTypeHierarchyTool.cs:38`
- `src/AiNetLinter/Mcp/Registration/SymbolGraphToolRegistrations.cs:163-170`

Assembly-Symbol-IDs sind im gemeinsamen Pfad an Hash und Generation gebunden.
Die `get_type_hierarchy`-Ausführung ruft den Resolver derzeit jedoch ohne
`state.AssemblySymbolIdentity` auf:

```csharp
ResolveSymbolAsync(solution, symbolIdentifier, ct)
```

Der Assembly-Dispatcher übergibt zwar den Lease, aber nicht die Identität bis
zum Resolver. Dadurch können gültige verpackte Assembly-IDs abgelehnt werden;
unverpackte oder alte IDs können die Hash-/Generation-Prüfung umgehen.

Erforderliche Korrektur:

```csharp
ResolveSymbolAsync(solution, symbolIdentifier, ct, state.AssemblySymbolIdentity)
```

Zusätzlich ist ein Route-Test erforderlich, der eine aktuelle Assembly-ID
akzeptiert und eine alte ID nach A→B→A als stale ablehnt. Projekt-IDs müssen
unverändert bleiben.

### Bereits verifizierte Invarianten

- Registry-Fingerprint wird je Retry neu gelesen; Churn ist begrenzt und
  fail-closed.
- Registry-Generationen sind über Entry-Ersetzungen hinweg monoton; A→B→A-
  Stale-ID-Schutz ist direkt getestet.
- Creation Barrier, Lease-Drain, aktive Leases bei Dispose und Cleanup-
  Isolation sind in gezielten Tests grün.
- mtime-only-Reuse, DLL-Refresh und Trust-Prüfungen sind vorhanden.
- `metrics_tree` und gemeinsame MCP-/Roslyn-Routen sind vorhanden.
- AIContextFootprints liegen nach der Host-Kompositionsaufteilung unter den
  Grenzwerten; `safeguard` meldete zuletzt 10/10 und `get_violations` 0.

### Letzte Verifikation

- `dotnet build AiNetLinter.slnx --no-restore`: erfolgreich, 0 Warnungen,
  0 Fehler.
- FastTests Non-Stress: 2.202 bestanden, 2 übersprungen.
- Gezielte IntegrationTests: 63 bestanden.
- Ein früherer vollständiger Integration-Non-Stress-Lauf meldete 372
  bestandene Tests; der jüngste vollständige Lauf blieb bei Long-Running-
  Daemon-/JSON-RPC-Tests ohne Abschluss und ist daher nicht als aktueller
  vollständiger grüner Abschlussnachweis zu werten.
- `git diff --check`: sauber.
- Keine untersuchte Assembly wurde ausgeführt; keine externen Repositories
  oder Source-Repositories wurden verändert.

### Resume-Vertrag

1. Roadmap und diesen Blocker zuerst lesen; `correction_round` bleibt bei 5,
   `cycle_state` bleibt `blocked`.
2. Nicht stillschweigend einen sechsten Epic-1-Korrekturversuch starten. Eine
   Fortsetzung benötigt eine explizite Nutzerentscheidung oder einen neuen
   Lauf mit bewusst zurückgesetztem Budget.
3. Bei autorisierter Fortsetzung: frischen Implementierer und danach frischen
   Reviewer starten, die beiden P1s gezielt testen, anschließend die
   vollständigen Abschluss-Gates erneut ausführen.

## 2026-08-30 — Blocker durch gezielten Follow-up behoben

- Der Nutzer hat drei Feedback-Dateien aus frischen, sequenziellen Chats
  bereitgestellt: zwei Implementierungsberichte und einen unabhängigen
  Reviewbericht.
- `feedback-P1-Cancellation-Fix.md` dokumentiert die Weitergabe von
  `OperationCanceledException`, Cleanup und Shared-Creation-Verhalten.
- `feedback-P1-Fix-get_type_hierarchy.md` dokumentiert die Durchreichung von
  `AssemblySymbolIdentity`, den A→B→A-Test und die unveränderten Projekt-IDs.
- `feedback-review-P1-fixes.md` enthält das unabhängige Urteil `approved`.
- Die Fixes sind in `148ac0c3` und `b1a461f3` committed; die Feedback-Dateien
  sind in `cc860c1f` und `ee743610` committed.
- Build, gezielte Tests, `safeguard` 10/10 und `get_violations` 0 sind für den
  Follow-up-Scope dokumentiert.

### Resume-Entscheidung

Der fachliche Blocker von Epic 1 ist geschlossen. Die historische Grenze von
fünf Korrekturrunden bleibt nachvollziehbar erhalten; sie wird nicht gelöscht
oder rückwirkend umetikettiert. Für das nun gestartete Epic 2 beginnt ein neuer
Korrekturzähler bei `0`. Vor dem nächsten Implementierer sind Roadmap und dieses
Ereignis committed; Epic 2 läuft mit frischen, strikt sequenziellen Rollen
weiter.

## 2026-08-30 — Epic 2 Implementierer gestartet

- Run-ID: `resume-2026-08-30-epic-2`
- Epic: 2 — Externe Source-of-Truth, Trust, Attestation und Cachegenerationen
- Rolle: Implementierer
- Subagent-ID: `01a05331-c495-7630-936c-130c7303219a`
- Diff-Baseline: `a8071f5f`
- Status: `running`
- Erwartete nächste Aktion: terminalen Implementiererbericht abwarten, danach
  den Subagenten archivieren und einen frischen unabhängigen Reviewer starten.

## 2026-08-30 — Epic 3 Implementierer abgeschlossen

- Subagent-ID: `01a05340-c106-7281-a1c0-ee18baf54b11`
- Status: `completed`, noch ohne Review
- Geänderte Bereiche: transitive PE-Metadatenreferenzen, Missing-/Cycle- /
  Version-/Partialzustände, bounded TPA-/Framework-Referenzgraph,
  ExternalResourceRegistry mit Budgets/Health/TTL/LRU/Leases, Creation Barrier,
  Generationen-/mtime-Reuse, Registry-Disposal sowie zugehörige Assembly-,
  Registry- und Resource-Tests.

### Verifikationsnachweis nach der letzten Codeänderung

- `dotnet build`: 0 Warnungen, 0 Fehler.
- `dotnet build AiNetLinter.slnx --no-restore`: erfolgreich.
- Gezielte Epic-3-FastTests mit Assembly-, Registry-, Wiring- und Resource-
  Scopes: 49/49 bestanden.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`:
  372/372 bestanden.
- `git --no-pager diff --check`: sauber; nur bekannte CRLF-Hinweise.
- MCP `get_impact` im Änderungs-Kontext: 0 Violations.
- MCP `get_violations` für `src/AiNetLinter/Mcp`: 0.
- MCP `safeguard` für `src/AiNetLinter/Mcp`: 10/10.
- MCP `find_duplicates`: keine Cluster.
- MCP `find_dead_code`: kein High-Confidence-Dead-Code.
- MCP `find_magic_values`: 7 diagnostische/Identifier-Kandidaten; keine
  sichere scope-nahe Korrektur.
- Refactoring-Drift wurde nicht breit ausgeführt, da keine konkrete
  Helper-Klon-Hypothese bestand.

### Offener Gate-Befund

Der vollständige FastTests-Lauf
`dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` meldete
2216/2219 erfolgreiche Tests und scheiterte an einem bestehenden,
nicht von Epic 3 geänderten `ProjectRegistryTests`-Test; zwei bekannte
Reparse-Tests wurden übersprungen. Der fremde ProjectRegistry-Scope wurde
nicht verändert. Dieser Nachweis ist für den Review verfügbar und wird nicht
ohne konkrete Gegenhypothese erneut ausgeführt.

### Nächste Aktion

Der Implementierungsstand wird jetzt als unreviewter Epic-3-Checkpoint
gesichert. Danach folgt genau ein frischer unabhängiger Reviewer; er wertet
diesen Nachweis zuerst gegen den tatsächlichen Diff aus und wiederholt nur
fehlende, veraltete, scope-fremde oder fachlich widerlegte Prüfungen.

## 2026-08-30 — Epic 3 Reviewer gestartet

- Run-ID: `resume-2026-08-30-epic-3-review`
- Epic: 3 — Transitive Assembly-Referenzen und getrennte externe Ressourcen
- Rolle: Reviewer
- Subagent-ID: `01a05360-0cf4-74b2-8743-84bf720392b9`
- Diff-Baseline: `944d583c`
- Status: `running`
- Verifikationsnachweise werden gemäß aktualisiertem Review-Skill zuerst auf
  Scope, Frische und Vollständigkeit geprüft; erfolgreiche identische Checks
  werden nicht ohne konkreten Anlass wiederholt.

## 2026-08-30 — Epic 3 Review abgeschlossen

- Subagent-ID: `01a05360-0cf4-74b2-8743-84bf720392b9`
- Status: `completed`, Urteil: `issues`
- Review-Baseline: `944d583c` gegen `2b76b113`; seit dem Implementierer-
  Checkpoint wurde kein Produktions- oder Testcode geändert.

### P1 — Transitive-Reference-Session-Expansion

- Stellen: `AssemblyReferenceResolver.cs:51`,
  `AssemblyAnalysisSession.cs:135`, `AssemblyDecompilationAdapter.cs:27`.
- Der Resolver entdeckt transitive DLLs und erzeugt MetadataReferences, aber
  Session- und Decompilation-Pfad verarbeiten weiterhin nur die Root-Assembly.
- Dadurch wird `foo.dll -> bar.dll` zwar angezeigt, `bar.dll` aber nicht über
  denselben Source-backed-/Decompilation-Pfad als eigenes Analyseziel
  bedarfsgesteuert verfügbar.
- Erforderlich: transitive Nodes über gemeinsame Assembly-/Source-Sessions
  mit eigenen Consumer-Leases, Deduplizierung, Bounds und sichtbaren
  Partial-/Missing-Zuständen anbinden; Route-/E2E-Test ergänzen.

### P1 — External-Source-Resource-Lifecycle

- Stellen: `AssemblyAnalysisHostComposition.cs:16`,
  `AssemblyAnalysisResourceBudget.cs:9`,
  `SourceSnapshotRegistry.cs:10`.
- `ExternalResourceRegistry` wird bisher nur von `AssemblyAnalysisRegistry`
  verwendet. `SourceSnapshotRegistry` besitzt weder unabhängige
  Disk-/Memory-/Parallelitätsbudgets noch Health, TTL/LRU oder Creation
  Barrier.
- Dadurch ist der externe Ressourcen-/Lebenszeitvertrag für Source-Snapshot-
  Sessions nicht nachgewiesen, obwohl diese nicht gegen `MaxProjects=4` zählen
  sollen.
- Erforderlich: eigener budgetierter Source-Snapshot-Registrypfad oder ein
  nachweisbar gleichwertiger durchgereichter Vertrag; Tests für Capacity,
  TTL/LRU, Health, parallele Erstzugriffe und aktive Leases ergänzen.

### Review-Verifikation

- Erfolgreiche Implementierer-Nachweise für Build, IntegrationTests,
  `safeguard`, `get_violations` und `git diff --check` wurden nicht wiederholt,
  weil sie frisch, vollständig und unverändert waren.
- Unabhängig abgefragt: `get_file_tree`, `get_feature_context`,
  `get_class_structure`, `dependency_graph`, `find_references`,
  `get_test_context`.
- Nicht blockierend: unveränderter `ProjectRegistryTests`-Fehler bleibt
  `accepted-deferred`; diagnostische Magic Values bleiben
  `accepted-deferred`; kein konkreter Refactoring-Drift-Fund.
- Nächste Aktion: Reviewstand als Checkpoint committen, danach gezielte
  Korrektur mit frischem Implementierer und anschließendem frischem Review.

## 2026-08-30 — Epic 3 Korrekturrunde 1 gestartet

- Run-ID: `resume-2026-08-30-epic-3-correction-1`
- Epic: 3 — Transitive Assembly-Referenzen und getrennte externe Ressourcen
- Rolle: Implementierer
- Subagent-ID: `01a05366-bbc6-76c0-b369-6fa29c8566bf`
- Diff-Baseline: `d7ffdaa6`
- Status: `running`
- Korrektursignaturen: `Transitive-Reference-Session-Expansion`,
  `External-Source-Resource-Lifecycle`
- Die neue Regel zur Wiederverwendung frischer Verifikationsnachweise wurde
  übergeben; nur echte P1-Lücken sollen neu geprüft werden.

## 2026-08-30 — Tech-Debt-Register initialisiert

- Aufgrund der aktualisierten Orchestrator-Regeln wurde die genau eine
  task-lokale `tech-debt.md` angelegt.
- Übernommen wurden ausschließlich drei nicht blockierende, bereits belegte
  Deferred-Befunde: Windows-Git-Prozess-Tests, unveränderter ProjectRegistry-
  FastTest und sieben diagnostische Magic-Value-Kandidaten.
- P1-Befunde verbleiben ausschließlich als Review-/Blocker-Einträge und wurden
  nicht in Tech Debt umetikettiert.

## 2026-08-30 — Epic 2 Implementierer abgeschlossen

- Subagent-ID: `01a05331-c495-7630-936c-130c7303219a`
- Status: `completed`
- Geänderte Bereiche: produktive Gitea/Git-Default-Komposition,
  injizierbarer Credential-Resolver, konfigurierte Cache-/Checkout-Pfade,
  zentrale Cache-Konstante und Host-Kompositionstests.
- Implementiererbericht: Build erfolgreich; relevante FastTests 244 bestanden,
  2 übersprungen; relevante IntegrationTests 11 bestanden; `git diff --check`
  sauber; `get_violations` 0; `safeguard` 10/10; keine Assemblies ausgeführt
  und keine externen Repositories verändert.
- Nicht abgeschlossen: vollständige Nicht-Stress-Abschlussgates und
  unabhängiges Epic-Review.
- Nächste Aktion: frischen unabhängigen Reviewer für Epic 2 starten.

## 2026-08-30 — Epic 2 Review abgeschlossen

- Subagent-ID: `01a0533a-8492-7170-9f9d-e89c9dbae6f7`
- Status: `completed`, Urteil: `approved`
- Geprüfter Scope: produktive Gitea/Git-Default-Komposition,
  Provider-Priorität, Credential-Resolver-Grenze, Cache-/Checkout-Pfade und
  Regressionen zu Epic 1.
- MCP: `get_file_tree`, `get_feature_context`, `get_impact`,
  `get_violations`, `safeguard`, `metrics_lookup`; 0 Violations, safeguard
  10/10, Footprints unter den Limits.
- Verifikation: Build erfolgreich, relevante FastTests 75/75 und relevante
  IntegrationTests 84/84 bestanden, `git diff --check` sauber.
- Ein breiterer Lauf hatte zwei Fehler in unveränderten
  `ExternalSourceGitProcessExecutorTests` wegen Windows-Zugriffsrechten bzw.
  Prozess-Timeouts. Disposition: `accepted-deferred`, da außerhalb des
  Epic-2-Diffs und als Test-/Umgebungsinfrastruktur eingeordnet.
- Nächste Aktion: Epic 2 als `done` markieren, den vollständigen Epic-Stand
  committen und danach Epic 3 mit frischen Rollen starten.

## 2026-08-30 — Epic 3 Implementierer gestartet

- Run-ID: `resume-2026-08-30-epic-3`
- Epic: 3 — Transitive Assembly-Referenzen und getrennte externe Ressourcen
- Rolle: Implementierer
- Subagent-ID: `01a05340-c106-7281-a1c0-ee18baf54b11`
- Diff-Baseline: `2b76b113`
- Status: `running`
- Erwartete nächste Aktion: terminalen Implementiererbericht abwarten, danach
  den Subagenten archivieren und einen frischen unabhängigen Reviewer starten.

## 2026-08-30 — Epic 3 Korrekturrunde 1 Implementierer abgeschlossen

- Run-ID: `resume-2026-08-30-epic-3-correction-1`
- Epic: 3 — Transitive Assembly-Referenzen und getrennte externe Ressourcen
- Rolle: Implementierer
- Subagent-ID: `01a05366-bbc6-76c0-b369-6fa29c8566bf`
- Diff-Baseline: `d7ffdaa6`
- Status: `completed`, kein Commit durch den Implementierer.
- Vollständiger Implementiererbericht:

  Korrekturrunde 1 von Epic 3 ist implementiert und übergabefertig. Kein
  Commit wurde erstellt; `roadmap.md` und `execution-log.md` blieben
  unverändert.

  Umgesetzt:

  - Transitive Referenzen werden über `AssemblyAnalysisRegistry` lazy mit
    eigenen Leases aufgelöst.
  - Gemeinsamer Source-Selection-Pfad inklusive Creation Barrier für
    parallele Erstzugriffe.
  - Deduplizierung, Bounds und sichtbare Fehlerzustände bleiben erhalten.
  - `SourceSnapshotRegistry` besitzt jetzt einen unabhängigen
    `ExternalResourceRegistry`-Lifecyclepfad.
  - Separate Disk-/Memory-/Resident-/Parallelitätsbudgets für
    Source-Snapshots.
  - TTL/LRU, Health, Eviction, Cancellation, Dispose und aktive Leases
    abgesichert.
  - Source-Resource-Budget im Host getrennt vom Assembly-Resource-Budget.
  - Fehlerhafte Duplikat-/Eviction-Dispose-Pfade korrigiert.
  - Fokussierte Tests ergänzt, ohne bestehende Resolver-/Graph-/Lease-Tests
    zu duplizieren.

  Geänderte Dateien:

  - `AssemblyAnalysisEntry.cs`
  - `AssemblyAnalysisRegistry.cs`
  - `AssemblySourceSelectionOrchestrator.cs`
  - `AssemblyAnalysisHostComposition.cs`
  - `ExternalResourceRegistry.cs`
  - `SourceSnapshotRegistry.cs`
  - `SourceSnapshotModels.cs`
  - `ExternalSourceSnapshotMaterializer.cs`
  - bestehende Registry-/Snapshot-Tests
  - neuer Creation-Barrier-Test

  Verifikation nach letzter Codeänderung:

  - `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler
  - Fokussierte FastTests: Filter
    `FullyQualifiedName~AssemblyAnalysisToolSupportTests|FullyQualifiedName~SourceSnapshotRegistryTests|FullyQualifiedName~AssemblyAnalysisRegistryTests`,
    36/36 bestanden
  - Host-/Provider-Tests: Filter
    `FullyQualifiedName~AssemblyAnalysisHostCompositionTests|FullyQualifiedName~GiteaExternalSourceProviderTests`,
    8/8 bestanden
  - `git diff --check`: sauber

  Finale MCP-Abfragen mit absolutem `projectRoot`:

  - `get_impact(detailLevel=change-context, maxChangedSymbols=100,
    maxTestsPerSymbol=20, maxResults=200)`: 11 geänderte Dateien, 87
    Symbole, nicht vollständig testassoziiert wegen MCP-Limit.
  - `get_violations(scopeFilter=src/AiNetLinter/Mcp/Assemblies,
    includeSnippet=false, contextLines=0, maxResults=100)`: 0 Verstöße.
  - `safeguard(..., minScore=8, maxViolations=100)`: 8,82/10, PASS.
  - `find_duplicates(scopeDir=src/AiNetLinter/Mcp/Assemblies,
    scopeType=production, similarityThreshold=exact, maxResults=50)`: 0
    Cluster.
  - `find_dead_code(accessibility=private_internal, confidence=both, kind=all,
    mode=members, includeTests=false)`: 38 Treffer, alle Low-Confidence,
    keine High-Confidence-Treffer.
  - `find_magic_values(scopeFilter=..., changedOnly=true, includeTests=false,
    includeSuppressed=false)`: 9 bestehende Vertrags-/Meldungskandidaten.

  Offene Punkte/Risiken:

  - Fresh Review und abschließender Audit durch unabhängige Rollen stehen
    noch aus.
  - Die vollständigen finalen Läufe `dotnet test ... FastTests
    --filter Category!=Stress` und `dotnet test ... IntegrationTests
    --filter Category!=Stress` wurden gemäß der neuen Regel nicht erneut
    ausgeführt; vorhandene Nachweise waren älter als die letzte Codeänderung.
    Das muss der Orchestrator vor Abschluss durchführen.
  - Der MCP-Safeguard meldet weiterhin nur den bekannten
    `AIContextFootprint`-Warnhinweis in `AssemblyAnalysisToolSupport`.
  - Keine Commits durch diesen Implementierer.

- Nächste Aktion: diesen vollständigen Stand als Orchestrator-Checkpoint
  committen und danach einen frischen unabhängigen Reviewer starten.

## 2026-08-30 — Epic 3 Korrekturrunde 1 Review gestartet

- Run-ID: `resume-2026-08-30-epic-3-correction-1-review`
- Epic: 3 — Transitive Assembly-Referenzen und getrennte externe Ressourcen
- Rolle: Reviewer
- Subagent-ID: `01a05381-9148-7a00-920e-599d745e3cb3`
- Diff-Baseline: `eb1727b8`
- Status: `running`
- Prüfauftrag: unabhängige Prüfung der beiden vorherigen P1-Signaturen
  `Transitive-Reference-Session-Expansion` und
  `External-Source-Resource-Lifecycle` einschließlich tatsächlicher
  Routenwirksamkeit, Leases, Budgets, Lifecycle-Races und Testabdeckung.
- Freshness-Regel: die erfolgreichen Nachweise aus der abgeschlossenen
  Implementierung werden nicht bloß zur Bestätigung wiederholt; erneute
  Prüfungen sind nur bei konkreter Gegenhypothese oder unklarem Nachweis
  zulässig.

## 2026-08-30 — Epic 3 Korrekturrunde 1 Review abgeschlossen

- Run-ID: `resume-2026-08-30-epic-3-correction-1-review`
- Epic: 3 — Transitive Assembly-Referenzen und getrennte externe Ressourcen
- Rolle: Reviewer
- Subagent-ID: `01a05381-9148-7a00-920e-599d745e3cb3`
- Diff-Baseline: `eb1727b8`; seit dem Implementierer-Checkpoint waren nur
  `execution-log.md`-Einträge ergänzt worden, Produktions- und Testcode waren
  unverändert.
- Status: `completed`; Urteil: `blocked`.
- Vollständiger Reviewerbericht:

  Epic 3 ist gegen `eb1727b8` nicht freigabefähig. Der aktuelle Diff
  gegenüber diesem Commit betrifft nur `execution-log.md`; Produktions- und
  Testcode entsprechen dem geprüften Stand. Zwei P1-Signaturen bleiben
  bestehen.

  1. P1 — `Transitive-Reference-Session-Expansion`

  `LeaseReferenceAsync` existiert zwar in
  `AssemblyAnalysisEntry.cs:228` und wird in
  `AssemblyAnalysisRegistry.cs:214` mit einem Registry-Lease hinterlegt. Der
  tatsächliche Dispatcher-/Toolpfad ruft diese Methode aber nicht auf:

  - `AnalysisToolCall.cs:114` dispatcht nur den Assembly-Callback.
  - `InspectAssemblyTool.cs:28` serialisiert lediglich `Context.References`.
  - Der statische Aufrufnachweis findet außer der internen Factory-Verknüpfung
    nur die Tests.

  Der angebliche E2E-Test `AssemblyAnalysisRegistryTests.cs:152` erstellt
  außerdem ein Registry-Objekt ohne Source-Resolver und analysiert den Child
  anschließend durch einen separaten manuellen Dispatcher-Aufruf. Das beweist
  keine transitive Expansion über den realen gemeinsamen Route-Pfad.

  Zusätzlich kann der Source-Pfad Source-Projekt-Referenzen nicht auflösen:
  `AssemblyAnalysisContextFactory.cs:83` ruft den Resolver nur mit dem
  DLL-Pfad auf; `AssemblyReferenceResolver.cs:226` durchsucht nur das
  DLL-Verzeichnis und Trusted Platform Assemblies. Eine `Foo -> Bar`-
  Projekt-Referenz innerhalb einer gemappten Solution ohne vorhandene
  `bar.dll` neben `foo.dll` bleibt damit `missing` und wird von
  `LeaseReferenceAsync` abgewiesen.

  Verletzt sind Konzept Zeilen 225–238 sowie Akzeptanzkriterien 426–428 und
  447–448.

  2. P1 — `External-Source-Resource-Lifecycle`: fehlerhafter
  Duplicate-Rollback

  In `SourceSnapshotRegistry.cs:88–107` wird bei einem Duplicate-Acquire
  `resident.LeaseCount` erhöht. Wenn anschließend das Dispose des
  Duplicate-Snapshots fehlschlägt, führt `SourceSnapshotRegistry.cs:127–148`
  zwar das externe Resource-Lease zurück, reduziert aber den Snapshot-
  `LeaseCount` nicht.

  Folge: Der residente Snapshot behält einen Phantom-Lease. Die spätere
  Freigabe des echten Leases reicht nicht auf null;
  `SourceSnapshotRegistry.Dispose:169–180` überspringt den Eintrag dauerhaft.
  Workspace, Checkout und Snapshot können dadurch nach Registry-Dispose
  verbleiben.

  Der vorhandene Duplicate-Test `SourceSnapshotRegistryTests.cs:19–42` deckt
  nur den erfolgreichen Dispose-Pfad ab, nicht diesen Fehlerfall. Das
  verletzt insbesondere das Cleanup-Kriterium Konzept Zeilen 438–439.

  Nicht blockierende Deferred-Kandidaten:

  - P2, `accepted-deferred`: `SourceSnapshotRegistry.EvictIdle:202–214`
    entfernt Ressourcen vor der Snapshot-Sperre. Parallel kann `Acquire`
    einen Lease erwerben, bevor der Snapshot anschließend trotzdem entfernt
    und disposed wird. Aktuell wurde kein produktiver Aufrufer von `EvictIdle`
    gefunden.
  - P2, `accepted-deferred`: Source-Ressourcen werden erst nach vollständiger
    Materialisierung budgetiert (`ExternalSourceSnapshotMaterializer.cs:78–89`);
    bei Schätzfehlern fällt `SourceSnapshotModels.cs:203–205` auf `1,1` zurück.
    Das schützt keine transienten Disk-/Memory-Spitzen.
  - P2, `accepted-deferred`: Die Creation Barrier verwendet das Token des
    ersten Aufrufers (`AssemblySourceSelectionOrchestrator.cs:151–177`);
    `Cancel` beendet nur den Completion-Task bei Zeile 357. Der Test
    `AssemblyAnalysisToolSupportCreationBarrierTests.cs:14–53` deckt nur den
    erfolgreichen Join ab.

  Muss-Kriterien-Abgleich:

  | Kriterium | Urteil |
  |---|---|
  | Deduplizierung, Tiefen-/Knoten-Bounds, sichtbare Resolverzustände | überwiegend erfüllt |
  | Eigene Leases für transitive Ziele | API vorhanden, Route nicht wirksam integriert |
  | Source-backed `ProjectReference` innerhalb derselben Solution | nicht erfüllt |
  | Tatsächlicher Route-/E2E-Nachweis | nicht erfüllt |
  | Unabhängiges Source-Resource-Registry vom Assembly-Budget | strukturell erfüllt |
  | TTL/LRU/Health/Parallelitätsbudgets | grundsätzlich vorhanden |
  | Fehlerfester Snapshot-Lease-/Dispose-Lifecycle | nicht erfüllt |
  | Keine Verdrängung der vier `MaxProjects`-Kontexte | erfüllt |

  Verifikation: Die maßgeblichen sechs Dateien wurden vollständig gelesen.
  Verwendet wurden MCP `get_file_tree`, `get_feature_context`,
  `get_class_structure`, `get_symbol_body`, `get_test_context` und
  `dependency_graph` mit absolutem `projectRoot`. Die frischen
  Implementierer-Nachweise aus `execution-log.md` — Build 0/0, fokussierte
  Tests 36/36, Host-/Provider-Tests 8/8, Safeguard 8,82 — wurden gemäß
  Auftrag nicht wiederholt. Es wurden keine Assemblies ausgeführt, keine
  Korrekturen implementiert und keine Task-Dateien verändert.

- Nächste Aktion: Nach Checkpoint und Tech-Debt-Triage startet eine neue,
  frische Korrekturrunde 2 für die beiden P1-Ursachen innerhalb des
  Epic-3-Korrekturbudgets.

## 2026-08-30 — Epic 3 Korrekturrunde 2 Implementierer gestartet

- Run-ID: `resume-2026-08-30-epic-3-correction-2`
- Epic: 3 — Transitive Assembly-Referenzen und getrennte externe Ressourcen
- Rolle: Implementierer
- Subagent-ID: `01a05398-072b-76e3-9e3e-969a0f3f56cd`
- Diff-Baseline: `46e76037`
- Status: `running`
- Korrekturauftrag: ausschließlich die beiden P1-Ursachen
  `Transitive-Reference-Session-Expansion` und
  `External-Source-Resource-Lifecycle` beheben, einschließlich eines echten
  Dispatcher-/Tool-Routen-Tests und des Duplicate-Rollback-Regressionstests.
- Bereits triagierte P2-Kandidaten bleiben `accepted-deferred` in
  `tech-debt.md` und sind nicht Teil dieser Korrekturschleife.
- Freshness-Regel: erfolgreiche vorhandene Checks werden nicht ohne Anlass
  wiederholt; neue Checks müssen nach den letzten Codeänderungen den
  Korrekturscope gezielt abdecken.

## 2026-08-30 — Architekturhinweis für spätere Rollen

- In einzelnen Verzeichnissen können sinnvolle Dateianzahlgrenzen erreicht
  werden. Falls eine solche Grenze durch den Task berührt oder überschritten
  wird, soll die zuständige Rolle ein sauberes, fachlich begründetes
  Refactoring in Unterverzeichnisse mit konsistenten Namespace-Anpassungen
  prüfen und bei Bedarf scopeübergreifend umsetzen. Der Hinweis ist kein
  Anlass für einen vorsorglichen globalen Cleanup; maßgeblich sind die
  konkrete Grenze, die betroffene fachliche Einheit und der aktuelle Task-
  Scope.

## 2026-08-30 — Epic 3 Korrekturrunde 2 Implementierer abgeschlossen

- Run-ID: `resume-2026-08-30-epic-3-correction-2`
- Epic: 3 — Transitive Assembly-Referenzen und getrennte externe Ressourcen
- Rolle: Implementierer
- Subagent-ID: `01a05398-072b-76e3-9e3e-969a0f3f56cd`
- Diff-Baseline: `46e76037`
- Status: `completed`, kein Commit durch den Implementierer.
- Vollständiger Implementiererbericht:

  Korrekturrunde 2 für Epic 3 ist gegen Baseline `46e76037` implementiert und
  bereit für unabhängige Review. Kein Commit erstellt.

  Fachliche Wirkung:

  - Der produktive `AssemblyAnalysisDispatcher` expandiert Referenzen jetzt
    vor dem Tool-Aufruf.
  - Transitive Sessions besitzen eigene Consumer-Leases, Deduplizierung sowie
    Tiefen-/Knotenlimits (`8`/`128`).
  - Missing-, Cycle-, Depth-, Node-Limit- und Partial-Zustände werden in
    Payload und Text sichtbar.
  - Source-backed `ProjectReferences` werden über die gemappte
    Snapshot-Solution aufgelöst, unabhängig vom DLL-Nachbarverzeichnis.
  - Source-Project-Kinder erhalten eigene Snapshot- und Resource-Leases.
  - `SourceSnapshotRegistry` rollt bei fehlgeschlagenem Duplicate-Dispose
    sowohl den Resource-Lease als auch den Resident-Lease zurück;
    Phantom-Leases bleiben nicht bestehen.
  - Bestehende Budgets, Health, TTL/LRU, CreationBarrier, Cancellation,
    Dispose und aktive Leases wurden erhalten.

  Geänderte Produktionsbereiche:

  - `src/AiNetLinter/Mcp/AnalysisToolCall.cs`
  - `src/AiNetLinter/Mcp/AnalysisTarget.cs`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.cs`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.SourceProjects.cs`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisEntry.cs`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSessionModels.cs`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyReferenceResolver.cs`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblySourceSelection.cs`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/IAssemblyAnalysisRegistry.cs`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/References/AssemblyAnalysisLease.cs`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/References/AssemblyReferenceSessionExpander.cs`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/References/SourceProjectReferenceGraph.cs`
  - `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Snapshots/SourceSnapshotRegistry.cs`
  - `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs`
  - `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs`
  - `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyTool.cs`
  - `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs`

  Geänderte Tests/Fixtures:

  - `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyAnalysisRouteTests.cs`
  - `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyAnalysisRegistryTests.cs`
  - `src/AiNetLinter.FastTests/Mcp/Assemblies/SourceSnapshotRegistryTests.cs`
  - `src/AiNetLinter.FastTests/Fixtures/ExternalSourceSnapshotTestFactory.cs`

  Die task-lokale `code-map.md` wurde mit Dateien, Symbolen, Aufrufern,
  Tests und der MCP-Schemaabweichung aktualisiert. `roadmap.md`,
  `execution-log.md` und `tech-debt.md` blieben durch den Implementierer
  unverändert. Fremde Working-Tree-Änderungen wurden erhalten.

  Verifikation:

  - `dotnet build --no-restore`: erfolgreich, `0` Warnungen, `0` Fehler.
  - Fokussierter Testlauf über Dispatcher/Tool, Source-Project-Auflösung und
    Snapshot-Rollback: `40/40` bestanden.
  - `git diff --check`: keine Whitespace-Fehler; lediglich erwartete
    CRLF-Hinweise.
  - MCP-Qualitätsprüfung: keine Duplikat-Cluster, keine Dead-Code-Befunde;
    eigener neuer Magic-Value-Befund bereinigt, verbleibender
    Localization-Befund außerhalb des Scopes.
  - Letzter codebezogener MCP-Schritt nach der letzten Codeänderung:
    `get_violations`. Tatsächliches Schema: absolutes `projectRoot`, da die
    Installation `targetType`/`targetPath` nicht akzeptiert. Produktionsscope
    `projectRoot=C:\Daten\Entwicklung\Ralf\AiNetLinter`,
    `scopeFilter=src/AiNetLinter/Mcp/Assemblies/Analysis`: eine bestehende
    zentrale `AIContextFootprint`-Warnung bei `AssemblyAnalysisRegistry`.
    Testscope `scopeFilter=src/AiNetLinter.FastTests/Mcp/Assemblies`: `0`
    Violations.

  Keine Ziel-/Decompilation-Assemblies wurden ausgeführt; die Tests
  verwendeten nur erzeugte Testartefakte und den produktiven Analysepfad.

  Risiken/Deferred:

  - Der Safeguard meldet weiterhin den zentralen
    `AssemblyAnalysisRegistry`-Footprint sowie den älteren
    `AssemblyAnalysisToolSupport`-Footprint. Ein weiterer
    Verantwortlichkeitssplit wäre ein separates Refactoring und nicht für
    diese beiden P1-Ursachen erforderlich.
  - Die Expansion ist bewusst begrenzt und kann bei großen oder zyklischen
    Graphen `partial` liefern.

  Commit-Vorschlag des Implementierers (vom Orchestrator anzupassen):
  `fix(decompiled-assembly-analysis-finish): transitive Assembly-Sessions
  und Snapshot-Lease-Rollback korrigieren`

- Nächste Aktion: aktuellen auftragsbezogenen Code-, Test-, Log- und
  Code-Map-Stand als Orchestrator-Checkpoint committen und danach einen
  frischen unabhängigen Reviewer starten.

## 2026-08-30 — Epic 3 Korrekturrunde 2 Review gestartet

- Run-ID: `resume-2026-08-30-epic-3-correction-2-review`
- Epic: 3 — Transitive Assembly-Referenzen und getrennte externe Ressourcen
- Rolle: Reviewer
- Subagent-ID: `01a053dd-18ee-70a1-bfc0-782247f593ad`
- Diff-Baseline: `c613d09d`
- Status: `running`
- Prüfauftrag: unabhängige Prüfung der korrigierten Dispatcher-/Tool-
  Expansion, Source-backed ProjectReference-Auflösung und des
  Duplicate-Dispose-Lease-Rollbacks.
- Die aktualisierte `code-map.md` ist als Navigationshilfe zu lesen und gegen
  Working Tree und MCP zu verifizieren; sie ist keine Source of Truth.
- Die frischen Implementierer-Nachweise (Build `--no-restore` 0/0,
  fokussierte Tests 40/40 und letzter `get_violations`-Check) werden nicht
  bloß zur Bestätigung wiederholt. Nur konkrete Gegenhypothesen oder fehlende
  Abdeckung rechtfertigen eine gezielte Wiederholung.

## 2026-08-30 — Epic 3 Korrekturrunde 2 Review abgeschlossen

- Run-ID: `resume-2026-08-30-epic-3-correction-2-review`
- Epic: 3 — Transitive Assembly-Referenzen und getrennte externe Ressourcen
- Rolle: Reviewer
- Subagent-ID: `01a053dd-18ee-70a1-bfc0-782247f593ad`
- Diff-Baseline: `c613d09d`; gegenüber dieser Baseline waren nur
  `roadmap.md`-/`execution-log.md`-Updates erfolgt, kein Produktionscode.
- Status: `completed`; Urteil: `issues`.
- Vollständiger Reviewerbericht:

  Der aktuelle Working Tree enthält gegenüber `c613d09d` nur Log-/Roadmap-
  Änderungen, keinen Produktionscode. Die Implementierung aus `c613d09d`
  wurde semantisch gegen den Elternstand geprüft.

  ### P1 — `External-Source-Resource-Lifecycle` bleibt blockierend

  In `SourceSnapshotRegistry.cs:127-166` rollt `CleanupFailedAcquire` den
  Resident-Lease zwar zurück, aber `ReleaseResidentLease` entfernt einen
  Eintrag nach dem letzten Lease nicht mehr, wenn die Registry inzwischen
  terminal disposed ist.

  Reproduzierbare Interleaving-Folge:

  1. Resident-Lease: `LeaseCount = 1`.
  2. Duplicate-Acquire erhöht auf `2`.
  3. Parallel `SourceSnapshotRegistry.Dispose()`; der Eintrag bleibt wegen
     `LeaseCount = 2` erhalten.
  4. Original-Lease reduziert auf `1`.
  5. Duplicate-Dispose schlägt fehl; Rollback reduziert auf `0`, entfernt den
     Eintrag aber nicht.
  6. Der Snapshot bleibt in `snapshots` und wird nie disposed; `Dispose()` wird
     nicht erneut ausgeführt.

  Der normale `Release`-Pfad bei `SourceSnapshotRegistry.cs:234-257` enthält
  genau diese terminale Null-Lease-Bereinigung, der Rollback-Pfad nicht. Der
  vorhandene Test `SourceSnapshotRegistryTests.cs:45-72` deckt keine parallele
  Registry-Schließung ab.

  Disposition: vor Freigabe beheben und durch einen deterministischen
  Dispose-Race-Test absichern. Keine Korrektur wurde vorgenommen.

  ### `Transitive-Reference-Session-Expansion`: ursprünglicher P1 behoben

  Die Route-Wirksamkeit ist jetzt belegt:

  - `AnalysisToolCall.cs:113-158` ruft `ExpandReferencesAsync` vor dem
    Assembly-Tool auf.
  - Produktive Host-Verdrahtung ist in `McpServerCommand.cs:72-74` und
    `DaemonHostCommand.cs:45-47` vorhanden.
  - Die Source-Project-Route nutzt die gemappte Snapshot-Solution; der Test
    verschiebt die Dependency-DLL bewusst in ein isoliertes Verzeichnis
    (`AssemblyAnalysisRouteTests.cs:70-139`).
  - Expander und Source-Graph begrenzen auf 8 Ebenen/128 Knoten und erzeugen
    sichtbare Zustände (`AssemblyReferenceSessionExpander.cs:52-108`,
    `SourceProjectReferenceGraph.cs:65-85`).
  - MCP-Abfragen bestätigten Dispatcher-Aufrufer, `ExpandReferencesAsync`-
    Callsite, `LeaseReferencedAsync`-Factorypfad und Source-Project-
    Symbolkette.

  ### P2 — Expansion-Diagnosen fehlen bei `find_assembly_extensions`

  `FindAssemblyExtensionsTool.cs:26-29` baut nur aus `lease.Context`.
  Expansion-Diagnosen werden dort nicht übernommen, während
  `InspectAssemblyTool.cs:57-78` sie explizit ergänzt. Ein Fehler beim Öffnen
  eines transitiven Child-Leases kann daher im Extensions-Ergebnis als
  `complete` erscheinen.

  Dispositionsempfehlung: `accepted-deferred` bis zur Capability-/Tool-
  Integration in Epic 4; vor Epic-Abschluss entweder beheben oder den Vertrag
  ausdrücklich auf Root-Diagnosen beschränken. Nicht als verbliebener P1 der
  ursprünglichen Route-Signatur eingestuft.

  ### P2 — Negative Route-Testaussagekraft

  Die neuen Route-Tests belegen erfolgreiche physische und Source-Project-
  Expansion. Missing-/Cycle-/Limit-Tests liegen überwiegend auf Resolver-
  Ebene und testen nicht den vollständigen Dispatcher-/Tool-Antwortpfad. Ein
  gezielter Route-Test pro Fehlerklasse ist für die Capability-/E2E-Prüfung
  empfehlenswert.

  Dispositionsempfehlung: `accepted-deferred` mit Übergabe an Epic 4; kein
  eigener P1-Korrekturzyklus.

  ### Code-Map und MCP-Schema

  `code-map.md:30` ist navigationsseitig korrekt; alle referenzierten Dateien
  und Symbole wurden per Working Tree und MCP bestätigt. Es gab keinen
  konkreten Navigationsfehler, daher wurde die Datei nicht geändert.

  Die tatsächliche MCP-Installation akzeptiert als Projektparameter nur das
  absolute Pflichtfeld `projectRoot`; `targetType`/`targetPath` gehören zum
  analysierten Anwendungsschema, nicht zum MCP-Tool-Schema. Die Abweichung ist
  in `code-map.md:105` korrekt dokumentiert.

  Die gemeldeten Build-, 40/40-Test- und Violations-Ergebnisse wurden gemäß
  Vorgabe nicht redundant wiederholt. Es wurden keine Assemblies ausgeführt,
  kein Produktionscode geändert und kein Commit erstellt.

- Nächste Aktion: nach Checkpoint und Tech-Debt-Triage eine neue frische
  Korrekturrunde 3 ausschließlich für den Snapshot-Dispose-Race starten.

## 2026-08-30 — Epic 3 Korrekturrunde 3 Implementierer gestartet

- Run-ID: `resume-2026-08-30-epic-3-correction-3`
- Epic: 3 — Transitive Assembly-Referenzen und getrennte externe Ressourcen
- Rolle: Implementierer
- Subagent-ID: `01a053ef-772a-7673-a8db-c6ca9b88c155`
- Diff-Baseline: `b2c546f8`
- Status: `running`
- Korrekturauftrag: ausschließlich den belegten Snapshot-Dispose-Race
  beheben, bei dem ein fehlgeschlagenes Duplicate-Dispose nach terminalem
  Registry-Dispose einen Snapshot mit Null-Lease resident lässt.
- Die ursprüngliche Transitiv-Route gilt als behoben. Die P2-Befunde zu
  Extensions-Diagnosen und negativer Route-Testabdeckung bleiben
  `accepted-deferred` für Epic 4.
- Freshness-Regel: bestehende erfolgreiche Nachweise werden nicht bloß zur
  Bestätigung wiederholt; der Implementierer liefert gezielte Regression,
  Qualitätschecks und den letzten `get_violations`-Nachweis nach seiner
  letzten Codeänderung.
