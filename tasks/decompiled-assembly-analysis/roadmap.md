---
status: active
task: decompiled-assembly-analysis
derived_from: Konzept.md
created_at: 2026-08-28T11:11:40+02:00
last_updated: 2026-08-29T22:56:09+02:00
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
---

# Roadmap: decompiled-assembly-analysis

## Zielbild

Eine gemeinsame, residente Roslyn-Analyseplattform soll Projektquellcode,
explizit zugeordnete externe Quelllösungen und unbekannte .NET-Assemblies per
statischer Decompilation über denselben MCP-Zugriff analysierbar machen. Ziel
sind ein harter Target-Vertrag, sichtbare Herkunft und Vertrauens-/Ladezustände,
deterministische Source-Snapshots, transitive Referenzauflösung, atomare
Cache-Generationen sowie eine dokumentierte Capability-Matrix ohne
Runtime-Laden oder Ausführen fremder Assemblies.

## Epics

- [x] **EPIC-01 — Einheitlicher Analyse-Target-Vertrag und gemeinsame Dispatch-Grenze** — abgeschlossen durch `step-001` (Implementierung) und die genehmigte Korrektur `step-002` (Regel-/Dokumentationssynchronisation).

**Zweck:** Den MCP-Vertrag auf `targetType` (`project`/`assembly`) und
`targetPath` vereinheitlichen und eine gemeinsame, erweiterbare Dispatch- und
Session-Grenze schaffen, während der bestehende Projekt-Lifecycle und dessen
Regressionen erhalten bleiben. Dazu gehören die Zielmodelle, die gemeinsame
Tool-Aufrufstruktur, die Registrierung aller betroffenen Tools sowie die
Kompatibilitäts- und Fehlermodell-Entscheidungen aus Konzept Phase 1.

  Startpunkt; Grundlage für alle folgenden Epics.

- [x] **EPIC-02 — Residente Assembly-Sessions mit statischer Decompilation** —

  **Abschlussnachweis:** `step-003` hat das statische Assembly-Session-
  Fundament implementiert (`step-003/step-result.md`, Code-Commit
  `0704b763`). Die genehmigte Korrektur `step-004` hat die sechs Review-
  Findings zu Cache-/Manifest-Integrität, Limits, Referenzen, Identität,
  Generated-Source-Scans und gebündeltem Tech-Debt behoben
  (`step-004/step-result.md`, Code-Commit `639f0fc47c8f90897db12c868ecd1295f608ad1a`;
  genehmigt durch `step-004/step-review.md`).

**Zweck:** Unbekannte DLLs ohne Projektdefinition dauerhaft analysierbar machen:
mit eigener Assembly-Session, Fingerprint- und Generationsidentität,
Decompilation-Adapter, synthetischem Roslyn-Projekt, Metadaten-Referenzen,
Origin-/Confidence-Informationen, sichtbaren Complete/Partial/Degraded/Failed-
Zuständen und atomarem Refresh. Der Cluster umfasst außerdem die Begrenzung
von Zeit, Größe und Komplexität sowie den Nachweis, dass weder Assembly-Loading
noch Reflection-Ausführung stattfindet; die bestehenden Assembly-Metadaten-Tools
werden an diese Session-Grenze angeschlossen.

  Abhängigkeit: EPIC-01.

- [x] **EPIC-03 — Explizite externe Source-Solutions und Snapshot-Auflösung** —
  abgeschlossen durch die genehmigten Steps `step-005` bis `step-013`.

  **Abschlussnachweis:** `step-005`/`step-006` liefern den globalen,
  strikt validierten Mapping-Vertrag, Pfadauflösung, Diagnosen und den
  injizierbaren Provider-Port. `step-007` liefert die Snapshot-Identität,
  residente Registry und Leases; `step-008` das deterministische
  `Project.AssemblyName`-Matching mit `matched`, `no-match` und `ambiguous`.
  `step-009` verbindet das gematchte Source-Projekt mit der Factory und hält
  die statische Decompilation als deterministischen Fallback. `step-010` und
  die genehmigte Korrektur `step-011` komponieren Provider, Registry,
  Selection, Lease-Lifetime und Support-Fallback. `step-012` und die genehmigte
  Korrektur `step-013` schließen die gemeinsame Host-Komposition, das direkte
  registrierte Wiring sowie die geteilte Lifetime über mehrere Daemon-Sessions;
  der Nachweis liegt in den jeweiligen Result-/Review-Dateien.

  **Fachliche Grenze:** EPIC-03 ist damit innerhalb seines Ziels der
  expliziten Source-Auflösung abgeschlossen. Die noch nicht implementierte
  konkrete Gitea-Akquisition — Authentifizierung, Netzwerk-/Git-Semantik,
  Clone/Fetch/Refresh, atomare Snapshot-Veröffentlichung und die
  Source-of-Truth-Regeln für dirty/unbuilt Checkouts — gehört vollständig zu
  EPIC-04 und ist kein offener EPIC-03-Rest. `TD-004` ist durch `step-013`
  erledigt; `TD-001` bis `TD-003` bleiben als nicht direkt zu diesem Abschluss
  gehöriger Tech-Debt offen.

  **Zweck:** Eine globale, explizite Mapping-Konfiguration für externe Quellen
  einführen und daraus vollständige Solution-Snapshots, den passenden
  AssemblyName/Projektkandidaten sowie Evidenz und Confidence ableiten. Der
  Cluster umfasst getrennte Source-Registrierung und -Caches, gemeinsame
  Snapshot-Identität, Alias-Wiederverwendung zwischen direkter DLL-Analyse und
  Referenzauflösung sowie readonly-fähige Quell-Sessions; bei fehlender oder
  mehrdeutiger Zuordnung bleibt die Decompilation der definierte Fallback.

  Abhängigkeit: EPIC-01 und EPIC-02.

- [ ] **EPIC-04 — Gitea-Source-of-Truth, Refresh und Fehlersemantik** —

**Zweck:** Explizit gemappte Gitea-Repositories deterministisch anhand von
Repository-URL, geladenem Commit, Solution-Pfad und Projektzuordnung laden und
aktualisieren. Der Cluster behandelt Authentifizierung, Default-Branch-
Refresh, lokale Cache-/Temp-Verzeichnisse, Cancellation, Netzwerk- und
Korruptionsfehler, dirty/unbuilt lokale Checkouts, atomare Veröffentlichung und
  den transparenten Wechsel auf Decompilation, ohne lokale Arbeitskopien zur
  Source-of-Truth zu machen.

  **Abschluss des Vorgängers `step-014`:** Der bestehende injizierbare
  `IExternalSourceProvider`-Port besitzt nun eine typisierte Auth-/Transport-
  und Fehlersemantik mit deterministischen Test-Doubles. Der Schnitt änderte
  das öffentliche Mapping-JSON nicht, führte keine Credential-Konfiguration
  ein und implementierte keinen Netzwerk- oder Git-Client; erwartete
  Providerfehler werden als nicht-quellfähige Ergebnisse mit stabilen
  Diagnosen modelliert, Cancellation bleibt echte Cancellation.

  **Vorgänger-Schnitt → `step-015`:** Ein zusammenhängendes
  Repository-Akquisitionspaket definiert den injizierbaren
  `IGiteaRepositoryTransport`-Vertrag und eine sichere
  `ExternalSourceRepositoryAcquirer`-Staging-/Clone-Fassade. Der Transport
  bleibt im Step ein Port mit deterministischem Doppel; echte
  Netzwerk-/Gitea-/Git-Ausführung, Credential-Bindung und produktive
  Adapterimplementierung werden nicht ausgeführt bzw. nicht vorweggenommen.
  Die Fassade begrenzt jeden neuen Checkout auf eine kontrollierte
  Staging-Wurzel, prüft den Solution-Pfad und räumt bei Fehler/Cancellation
  ihren Besitz auf.

  **Abgeschlossener Schnitt → `step-018`:** Die
  repository-spezifische Capability-Nichtverfügbarkeit für
  ERROR_PRIVILEGE_NOT_HELD (1314) und tatsächlich erkannte Reparse-
  Checkouts wird typed als ProviderUnavailable durch die bestehende
  Provider-/Orchestrator-Kette zum statischen Decompilation-Fallback
  geführt. Normale Repositories bleiben nutzbar; vollständige
  Snapshot-/Workspace-Materialisierung und produktives Provider-Wiring
  bleiben Folgepakete.

  **Nächster wirksamer Schnitt → `step-019`:** Ein produktiver,
  injizierbarer Git-over-HTTP(S)-Transport führt den initialen Clone des
  Default-Branch-Zustands aus und ermittelt die geladene HEAD-Revision.
  Laufzeit-Credentials werden über einen flüchtigen Resolver gebunden und
  geheimnisfrei über den Child-Process-Kanal verwendet. Das bestehende
  Acquirer-Ergebnis wird damit produktiv befüllbar, aber noch nicht an
  Provider, Snapshot oder Host verdrahtet. Der Step enthält keine echte
  Netzwerkverifikation in Tests.

  **Vertikale Folgepaket-Grenzen:**

  - Provider-Port, Auth-/Fehlervertrag und deterministische Doubles — `step-014`.
  - Akquisitionsvertrag, kontrollierte initiale Clone-/Staging-Fassade und
    deterministische netzwerkfreie Tests — `step-015`.
  - Produktiver Gitea-/Git-/HTTP-Transport, Credential-Bindung,
    Default-Branch- und echte initiale Clone-Semantik — `step-019`; Fetch und
    Refresh bleiben ausgenommen.
  - Erfolgreiches Acquirer→Snapshot-/Workspace-Wiring mit unveränderter
    repository-spezifischer 1314-/Reparse-Fallback-Regel — `step-024`,
    nach der Registry-Korrektur in `step-025` genehmigt; der Snapshot
    übernimmt den Checkout-Besitz bis zur Registry-/Snapshot-Lifetime,
    ohne Host-Wiring.
  - **Abgeschlossene Cache-Publish-Schnitte → `step-026` bis `step-028`:**
    `step-026` führte persistente Repository-Cache-Identität, Manifest,
    Inventory und Generation aus einem erfolgreichen Clone-/Acquirer-Ergebnis
    sowie die atomare Veröffentlichung des validierten `current`-Pointers
    über den lokalen Generation-Writer ein. `step-027` korrigierte Lock-
    Lifetime, unabhängige bounded Read-back-Validierung und Testisolation;
    `step-028` schloss den deterministischen A/B-Race-Nachweis und die
    vollständige Pointer-/Manifest-/Inventory-Malformed-Matrix. Der
    request-eigene Checkout blieb beim Acquirer, der persistente
    Generation-Ordner cache-eigen. Current-Reuse, Fetch/Refresh und neue
    Cache-Konfiguration waren in diesen drei Steps nicht enthalten.
  - **Abgeschlossene Current-Reuse-Schnitte → `step-029` bis `step-031`:**
    Cache-backed Initial Acquisition/Reuse mit strikt validiertem
    Current-Pointer, unabhängig geprüftem Manifest/Inventory/Inhalt und einem
    neuen besitzbaren Request-Checkout aus einer gültigen persistenten
    Generation. Step 030 und Step 031 korrigierten die konkreten
    Reuse-/Current-/Publish-Nachweise sowie die grünen Quality-Gates. Der
    Acquirer verwendet bei Miss, Invalidität oder kontrolliertem
    Materialisierungsfehler weiterhin den bestehenden Clone-/Write-through-
    Pfad; der persistente Generation-Pfad wird nie als Request-Handle
    weitergereicht. Review `d8cff007` genehmigte Step 031.
  - **Aktiver nächster Schnitt → `step-032` (planned/in_progress):**
    Ein gemeinsamer Refresh-/Fetch-Vertrag entscheidet anhand der validierten
    Generation und einer injizierten Policy über Current oder Staleness,
    materialisiert einen neuen request-eigenen Checkout, aktualisiert ihn
    über den bestehenden sicheren Git-Prozesspfad und veröffentlicht eine
    neue Generation mit atomarem Current-Pointer. Fällige Aktualisierungs-
    fehler geben den alten Snapshot nicht still als aktuell aus; die statische
    Decompilation bleibt der Fallback.
  - CacheRoot-/Refresh-Konfiguration über `appsettings.json` wird mit dem
    späteren Konfigurationsvertrag entschieden, nicht in Step 032.
  - Dirty/unbuilt Checkout-Abgrenzung und transparente Fallback-/Health-
    Semantik bleiben nach diesen Cache-Verträgen ein eigener Source-Policy-
    Schnitt; Step 024/025 markieren nur den erfolgreichen Workspace-Aufbau und
    die besitzgebundene Cleanup-Grenze.

  **Nachgelagerte Folgepakete:**

  - Cache-Reuse und Request-Checkout aus einer validierten Generation sind in
    `step-029` bis `step-031` auf den Initial-Acquisition-Vertrag begrenzt;
    sie werden nicht rückwirkend um Refresh erweitert.
  - Refresh/Fetch, Refresh-Intervall-Entscheidung und atomarer Wechsel auf
    eine neue Generation sind in `step-032` als ein vertikaler Vertrag
    gebündelt. Die öffentliche Cache-Konfiguration folgt separat.
  - Dirty/unbuilt lokale Checkouts sowie Health-/degraded-Fallback-Policy
    bleiben nach den Cache-Reuse-/Refresh-Verträgen ein eigener Source-
    Policy-Schnitt.
  - Host-Komposition, produktives Provider-/MCP-Wiring und eine gemeinsame
    Capability-Matrix bleiben außerhalb der Cache-Grenze.
  - Transitive Referenzen und gemeinsame Tool-Capabilities bleiben EPIC-05.
  - Retention, Garbage Collection, explizite Cache-Invalidierung und
    Telemetrie werden erst nach dem sicheren Cache-Vertrag geplant.

  Diese Grenzen bleiben sequenzielle vertikale Pakete; EPIC-04 wird nicht als
  monolithischer Gitea-Featureblock und nicht als Mini-Sweep umgesetzt. Die
  Aufteilung von Step 026 erfolgte ausdrücklich zur Context-Stabilität.

  Abhängigkeit: EPIC-03.

- [ ] **EPIC-05 — Transitive Referenzen und gemeinsame Tool-Capability-Matrix** —

**Zweck:** Die externe Analyse über einzelne DLLs hinaus fachlich vollständig
und konsistent nutzbar machen: rekursive Referenzgraphen mit Deduplication,
Zyklen- und Missing-Reference-Zuständen, getrennten Kapazitäts-/Health-
Grenzen für Projekt-, Source- und Assembly-Sessions sowie einheitliches
Routing der relevanten Roslyn-Tools. Der Cluster definiert pro Herkunft die
Unterstützung für Symbolsuche, Struktur, Bodies, Referenzen/Call-Trees,
Dependency-Graphen, Metriken, Violations und Pattern-Erkennung; bewusst nicht
geeignete Bereiche wie Git-Diff, automatische externe Tests oder unklare
Safeguard-/Audit-Verträge werden explizit als unsupported bzw. kontraktgebunden
behandelt.

  Abhängigkeit: EPIC-02, EPIC-03 und EPIC-04.

- [ ] **EPIC-06 — Dokumentation, Verträge und Abschlussverifikation** —

**Zweck:** API-, Bootstrap-, Konfigurations-, Integrations- und
Architekturdokumentation auf den finalen Target-, Mapping-, Cache-,
Vertrauens- und Sicherheitsvertrag bringen; Tool-Beschreibungen und
Agentenregeln synchronisieren; die Capability-Matrix und sichtbaren Zustände
für Nutzer festhalten. Abschließend werden Fast-/Integration-/Dogfood-Tests,
Build, Nicht-Stress-Verifikation und der projektweite Drift-/Duplikat-Audit
ausgeführt.

  Abhängigkeit: EPIC-01 bis EPIC-05.

## Tech-Stack-Notiz

- **Sprache/Runtime:** C# mit .NET 10, nullable reference types, implicit
  usings und `TreatWarningsAsErrors`; Windows-/PowerShell-Entwicklungsumgebung.
- **Analyseplattform:** Roslyn 5.9, `MSBuildWorkspace`, PE-/Metadatenanalyse
  über `System.Reflection.Metadata`, bestehende Adhoc-/Workspace-Infrastruktur
  und statische Decompilation ohne Runtime-Laden.
- **MCP/CLI:** `ModelContextProtocol` 2.2, System.CommandLine 2.0.11,
  residente MCP-Daemon-/Registry-/Lease-Strukturen und Serilog-Logging.
- **Tests:** xUnit v3, `AiNetLinter.TestKit`, In-Memory-Roslyn-Workspaces,
  temporäre Testverzeichnisse und injizierbare Test-Doubles; Stress-Tests
  bleiben separat.
- **Build:** `dotnet build`
- **Schneller Testlauf:** `dotnet test src/AiNetLinter.FastTests --filter Category=Unit`
- **Abschluss-Testläufe:**
  `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und
  `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
- **Dogfood/Lint:** `dotnet run --project src/AiNetLinter -- --config rules.json --path AiNetLinter.slnx`;
  Regel-Synchronisation bei Regeländerungen über
  `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`.
- **Konventionen:** kurze, fokussierte C#-Methoden, Records für
  Request-/Value-Modelle, explizite Result-/Fehlerzustände, keine neue
  DI-/Plugin-/AssemblyLoadContext-Infrastruktur; Conventional Commits auf
  Deutsch im Imperativ. Dieser Roadmap-Aufruf erzeugt keinen Commit.

## Regel-Index

- `.agents/rules/AiNetLinter-McpWorkflow.mdc` — beschreibt die priorisierte Auswahl und Verwendung der AiNetLinter-MCP-Tools einschließlich absolutem `projectRoot` und der aktuellen Assembly-Analysegrenzen.
- `.agents/rules/AiNetLinter.mdc` — enthält die aus `rules.json` generierten C#-Qualitäts-, Metrik- und Komplexitätsregeln für das Projekt.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — legt Architektur, Entwicklungsworkflow, Test-/Dokumentationspflichten, Sicherheitsgrenzen und Drift-Prävention fest.

## Offene konzeptionelle Leitplanken für den JIT-Step-Modus

Die Roadmap ist nicht blockiert; die folgenden Punkte müssen in den jeweiligen
Epics anhand des tatsächlichen Codes entschieden und dokumentiert werden:

- konkrete Decompiler-Bibliothek, Versions-/Optionsidentität und synthetische
  Dokumentaufteilung;
- genaue Konfigurationsform und strikte Validierung der externen Mappings;
- Authentifizierungs- und Refresh-Schnittstelle zum Gitea-Provider;
- zulässiger Umfang der Source-Solution- und transitiven Referenzauflösung;
- genaue Alias-, Lease-, Generation- und Capability-Semantik der gemeinsamen
  Session-Registry.
