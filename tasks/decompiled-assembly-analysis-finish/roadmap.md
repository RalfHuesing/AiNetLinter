# Ausführungs-Roadmap: Einheitlicher Roslyn-Analysepfad

status: executing
current_epic: 4
last_commit: d6cd4c58
blocker: Review der Epic-4-Statuskorrektur; laufende MCP-Registry bleibt externe Voraussetzung
correction_round: 1
cycle_state: active
recent_finding_signatures:
  - Partiality-Status-Consistency: gemeinsamer analysis-Header übernimmt Root-complete trotz Partial-Payload
  - Installed-MCP-Schema-Drift: lokale Registry 1.0.154 bietet nicht targetType/targetPath und Assembly-Sessions
  - Unsupported-Path-Consistency: get_file_tree verliert beim Assembly-Ziel den Zielpfad

Diese Roadmap ist der einzige dauerhafte Ausführungs- und Resume-Stand für den
autonomen Lauf. Sie leitet die Reihenfolge aus dem freigegebenen Konzept ab;
fachliche Muss-Kriterien und Non-Goals bleiben unverändert.

## Epic 1: Gemeinsame Target-, Session- und Roslyn-Route

- Ziel: `targetType`/`targetPath` zentral validieren, Projekt-, source-backed-
  und dekompilierte Assembly-Ziele über einen gemeinsamen Lease-/Dispatch-Pfad
  in die Roslyn-MCP-Kernabfragen führen und Herkunft, Generation, Zustand und
  Unsupported-Verträge strukturiert ausweisen.
- Abhängigkeiten: vorhandener TargetResolver, AssemblyAnalysisSession und
  bestehende MCP-Toolregistrierungen.
- Betroffene Bereiche: `Mcp/AnalysisToolCall*`, `AnalysisTarget*`,
  `Mcp/Tools/AssemblyAnalysis/`, gemeinsame Symbol-/Struktur-/Metrik-/Graph-
  Tools, Registrierungen und zugehörige Fast-/Integration-Tests.
- Muss-/Akzeptanzkriterien: unbekannte absolute DLL ohne Projektdefinition;
  ein gemeinsamer Roslyn-/MCP-Kern für Symbole, Bodies, Struktur, Referenzen,
  Call Trees, Dependency Graphs und Metriken; eindeutige Origin-/Confidence- /
  Partialitätsdaten; klare unsupported Antworten; Spezialtools behalten ihren
  Output; keine Assembly-Ausführung oder dynamische Ladearchitektur.
- Verifikation: gezielte FastTests/Component und MCP-Wiring-/Host-Tests,
  `get_impact`, `get_violations` und die relevanten Assembly-/Target-MCP-
  Abfragen mit aktuellem Schema.
- Status: done

## Epic 2: Externe Source-of-Truth, Trust, Attestation und Cachegenerationen

- Ziel: explizites Mapping, strikte Validierung, produktive öffentliche
  Git-/Gitea-Provider-Komposition, getrennte Credential-Resolvergrenze,
  Source-Match, Snapshot-/Commit-Identität, Materialization-Leases sowie
  atomare hashgeprüfte Cache-/Refresh-Generationen end-to-end abschließen.
- Abhängigkeiten: Epic 1 für die gemeinsame Assembly-Session und Origin-Antwort.
- Betroffene Bereiche: `Mcp/Assemblies/ExternalSource/`, Konfiguration,
  Host-Komposition, Provider, Repository-/Snapshot-/Cache-Lifecycle sowie
  Unit-/Component-/Integration-Tests.
- Muss-/Akzeptanzkriterien: source-backed gewinnt nur bei explizitem und
  attestiertem Match; Mehrdeutigkeit, Dirty/Unverified, Refresh-Fehler,
  Lone-CR/CRLF, Cancellation, InvalidData, Öffnungs- und Dispose-Fehler sind
  sichtbar und fail-closed; keine Secrets in Antworten/Manifesten; alte Leases
  bleiben gültig; keine Quelle wird verändert oder ausgeführt.
- Verifikation: Mapping-/Fingerprint-/Parser-/Attestation-/Lease-Unit-Tests,
  lokale Git-Testprovider-Integration und gezielte MCP-Violation-Prüfung.
- Status: done

## Epic 3: Transitive Assembly-Referenzen und getrennte externe Ressourcen

- Ziel: direkte und transitive metadata-only Referenzen bedarfsgesteuert,
  dedupliziert und mit sichtbaren Missing-/Cycle-/Partial-Zuständen auflösen;
  externe Assembly-/Source-Sessions von der Vierergrenze der Benutzer-
  Projekte trennen und mit expliziten Ressourcen-, TTL-/LRU- und Health-Verträgen
  verwalten.
- Abhängigkeiten: Epic 1 und Epic 2.
- Betroffene Bereiche: `AssemblyReferenceResolver`, Assembly-Session-/Registry-
  Lifecycle, Health-Modelle, externe Cache-/Snapshot-Leases und Tests.
- Muss-/Akzeptanzkriterien: `foo.dll -> bar.dll`, gemeinsame Snapshots bei
  getrennten Consumer-Leases, Creation Barrier bei parallelen Erstzugriffen,
  neue Generation bei Inhaltänderung ohne mtime-only-Neuberechnung, maximal
  vier Benutzerkontexte ohne Verdrängung durch externe Quellen, sichtbare
  Ressourcenengpässe und keine fremde Test-/Assembly-Ausführung.
- Verifikation: FastTests für Graph-/Grenzfälle und Integrationstests für
  Registry, Health, Parallelität, Refresh und Cross-Target-Lebenszeit.
- Status: done

## Epic 4: Capability-Matrix, Host-Integration und End-to-End-Verträge

- Ziel: alle vorhandenen MCP-Toolfamilien fachlich korrekt an den gemeinsamen
  Pfad anschließen oder explizit als supported/partial/unsupported ausweisen;
  Default-Daemon, Bootstrap, Toolbeschreibungen, Origin-/Statuspayloads und
  Testinfrastruktur konsistent machen.
- Abhängigkeiten: Epic 1 bis 3.
- Betroffene Bereiche: MCP-Registrierung, Daemon-/CLI-Host-Komposition,
  Resultmodelle, `safeguard`/`get_violations`, Integration-MCP-Tests und
  capabilitybezogene Fixtures.
- Muss-/Akzeptanzkriterien: vollständige Matrix für Projekt, source-backed und
  Decompilation; klare Test-/Regel-/Audit-/Git-/Change-Impact-Grenzen;
  inspect/find-extensions über den gemeinsamen Sessionpfad; ein lokaler
  Default-Host; keine Legacy-Parameter oder widersprüchlichen Toolverträge.
- Verifikation: Wiring-, Host-, MCP-E2E- und Capability-Tests sowie passende
  `safeguard`-/`get_violations`-Abfragen.
- Status: in_progress

## Epic 5: Dokumentation und Abschluss-Gates

- Ziel: finalen Target-, Source-, Mapping-, Cache-, Trust-, Capability-,
  Fallback-, Sicherheits- und No-Execution-Vertrag in allen betroffenen
  Dokumenten synchronisieren und den vollständigen Abschluss nachweisen.
- Abhängigkeiten: Epic 1 bis 4.
- Betroffene Bereiche: `README.md`, `Docs/agent-api.md`,
  `Docs/integration.md`, `Docs/configuration.md`, `Docs/ROADMAP.md`,
  `Docs/rationale.md`, MCP-Bootstrap/Agentenregeln soweit tatsächlich nötig,
  Tests und diese Roadmap.
- Muss-/Akzeptanzkriterien: keine Legacy-Parameter oder veralteten Aussagen;
  alle verbindlichen Detailentscheidungen sind in Code, Tests und Doku sichtbar;
  keine Änderungen an `rules.json`, außer eine Regeländerung erfordert die
  vorgeschriebene Generierung der Agentenregeln.
- Verifikation: `dotnet build`,
  `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`,
  `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`,
  konzeptspezifische MCP-Safeguard-/Violation-Prüfungen und der abschließende
  DRY-/Refactoring-Drift-/Dead-Code-/Magic-Value-Audit.
- Status: open

## Abschluss-Checkliste

- [ ] unbekannte absolute DLL direkt ohne Projektdefinition analysierbar
- [ ] source-backed Match, Decompilation-Fallback und Origin/Trust/Status sichtbar
- [ ] Mapping, AssemblyName, Solution, Commit und Cachegenerationen eindeutig
- [x] direkte/transitive Referenzen metadata-only, dedupliziert und begrenzt
- [x] vier Projektkontexte getrennt von externen Ressourcen mit sichtbaren Limits
- [ ] gemeinsamer Roslyn-/MCP-Kern und vollständige Capability-Matrix
- [ ] Spezialtools, Bootstrap und Toolbeschreibungen konsistent
- [ ] Attestation, Parser, Cancellation, InvalidData und Cleanup fail-closed
- [ ] keine fremde Assembly geladen, reflektiert, ausgeführt oder extern verändert
- [ ] Dokumentation synchronisiert und ohne Legacy-Vertrag
- [ ] Build und beide vollständigen Nicht-Stress-Testgates grün
- [ ] konzeptspezifische MCP-Safeguard-/Violation-Prüfungen grün
- [ ] Audit auf DRY, Refactoring-Drift, Dead Code und Magic Values ausgeführt
