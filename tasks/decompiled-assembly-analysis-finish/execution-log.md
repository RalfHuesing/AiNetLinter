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
