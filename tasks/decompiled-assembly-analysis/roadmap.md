---
status: active
task: decompiled-assembly-analysis
derived_from: Konzept.md
created_at: 2026-08-28T11:11:40+02:00
last_updated: 2026-08-28T11:11:40+02:00
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

- [ ] **EPIC-01 — Einheitlicher Analyse-Target-Vertrag und gemeinsame Dispatch-Grenze** —

**Zweck:** Den MCP-Vertrag auf `targetType` (`project`/`assembly`) und
`targetPath` vereinheitlichen und eine gemeinsame, erweiterbare Dispatch- und
Session-Grenze schaffen, während der bestehende Projekt-Lifecycle und dessen
Regressionen erhalten bleiben. Dazu gehören die Zielmodelle, die gemeinsame
Tool-Aufrufstruktur, die Registrierung aller betroffenen Tools sowie die
Kompatibilitäts- und Fehlermodell-Entscheidungen aus Konzept Phase 1.

  Startpunkt; Grundlage für alle folgenden Epics.

- [ ] **EPIC-02 — Residente Assembly-Sessions mit statischer Decompilation** —

**Zweck:** Unbekannte DLLs ohne Projektdefinition dauerhaft analysierbar machen:
mit eigener Assembly-Session, Fingerprint- und Generationsidentität,
Decompilation-Adapter, synthetischem Roslyn-Projekt, Metadaten-Referenzen,
Origin-/Confidence-Informationen, sichtbaren Complete/Partial/Degraded/Failed-
Zuständen und atomarem Refresh. Der Cluster umfasst außerdem die Begrenzung
von Zeit, Größe und Komplexität sowie den Nachweis, dass weder Assembly-Loading
noch Reflection-Ausführung stattfindet; die bestehenden Assembly-Metadaten-Tools
werden an diese Session-Grenze angeschlossen.

  Abhängigkeit: EPIC-01.

- [ ] **EPIC-03 — Explizite externe Source-Solutions und Snapshot-Auflösung** —

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
