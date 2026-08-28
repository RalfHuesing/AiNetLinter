---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 012
epic: EPIC-03
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5 (Codex)
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-28T22:39:38+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 012: Gemeinsame Host-Komposition für direkte Assembly-MCP-Tools verdrahten

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Korrektur erforderlich
- [ ] **blocked** — Nutzer-Entscheidung nötig

Die Produktionsverdrahtung ist statisch schlüssig und alle geforderten Gates
sind grün. Der Step ist dennoch wegen zweier in-scope Testlücken nicht
abnahmefähig: Die neuen Tests können weder den kompositionsgeführten
Registration-Adapter gegenüber dem Legacy-Fallback unterscheiden noch die
Weitergabe derselben Komposition an mehrere Daemon-Sessions beweisen.

## Geprüfte Bereiche

- [x] Plan-Erfüllung
- [x] Rules-Konformität
- [x] Logische Korrektheit
- [x] Konzept-Treue
- [x] Build-/Test-Gates
- [x] DRY-, Magic-Value- und Dead-Code-Prüfung im angefassten
  Host-/MCP-Kompositionspaket

## Befund: Plan-Erfüllung

`AssemblyAnalysisHostComposition.Create` lädt Konfiguration, Provider,
Registry und Orchestrator einmal; Stdio erstellt sie auf Hostebene, und der
Daemon capturt sie in `CreateSessionRunner`. Die Registration reicht den
Kontext nur in `AssemblyAnalysisToolRegistrations` an die beiden direkten
Assembly-Tools weiter. `AnalysisToolCall` ist im Commit unverändert.

Die vorgesehenen Tests decken Ownership, den Unavailable-Fallback, das
29er-Inventar, Handshake und je einen direkten Stdio-/Daemon-Aufruf ab. Die
im Step-Plan geforderte aussagekräftige Adapterprüfung für source-backed
Matched sowie der Nachweis einer gemeinsamen Komposition über mehrere
Daemon-Sessions fehlen jedoch.

## Befund: Rules-Konformität

Die referenzierten Architektur- und Sicherheitsregeln sind eingehalten: kein
neues Runtime-Laden, keine Reflection-Ausführung, kein
`AssemblyLoadContext`, kein Netzwerk-/Gitea-Aufruf, keine transitive
Referenzauflösung, keine Capability-Matrix und keine neue DI-/Plugin-
Infrastruktur. Die MCP-Violationsprüfung meldet für alle angefassten Dateien
keine neuen Verstöße; die bekannte `AIContextFootprint`-Warnung auf
`DaemonHostCommand.cs:17` betrifft den bestehenden Abhängigkeitskern und wird
nicht diesem Step zugerechnet.

## Befund: Logische Korrektheit

`using var source` im bestehenden Support-Overload hält Selection und Lease
über Context-Erzeugung und Result-Builder; die Composition-Dispose-Grenze
ist idempotent und `DaemonHost.RunAsync` wartet vor der Rückkehr auf seine
Sessions. Die direkte Registrierung verwendet bei vorhandener Komposition
korrekt `composition.Orchestrator`, ansonsten den unveränderten Legacy-
Fallback. Die Testauswahl beweist diese zwei entscheidenden Laufzeitpfade
aber nicht trennscharf, weil der Composition-Test die Wrapper direkt aufruft
und die Prozess-Tests ausschließlich den Default-
`UnavailableExternalSourceProvider` sehen.

## Befund: Konzept-Treue

Die Änderung bleibt innerhalb der geplanten read-only Host-Komposition,
konsumiert die Source-/Snapshot-/Match-/Fallback-Verträge und verändert weder
Target-/Dispatch-Semantik noch Projekt- und Fremdtools. Kein Konzept-Non-Goal
wurde umgesetzt; die offenen Punkte betreffen ausschließlich den Nachweis
der im Plan festgelegten Host-/Adapter-Verträge.

## Findings

### 1. Kompositionsgeführter Registration-Adapter ist für source-backed nicht verifiziert

- **Severity:** MAJOR
- **Ebene:** Plan-Erfüllung / logische Korrektheit
- **Ort:** `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyAnalysisHostCompositionTests.cs:19-62`; `src/AiNetLinter.FastTests/Mcp/WiringContractTests.cs:37-70`; `src/AiNetLinter.IntegrationTests/Mcp/McpServerAllToolsE2ETests.cs:44-83`
- **Beleg:** Der neue Composition-Test ruft in Zeilen 34-52 `InspectAssemblyTool.ExecuteAsync` und `FindAssemblyExtensionsTool.ExecuteAsync` direkt mit `composition.Orchestrator` auf und umgeht damit `AssemblyAnalysisToolRegistrations`. Der Wiring-Test prüft in Zeilen 39-43 nur Inventar und Schema. Die echte Stdio-Regression ruft zwar die registrierten Tools auf, verwendet aber ausschließlich den produktiven Default-Provider und erwartet in Zeilen 54-59 den decompiled-Fallback. Es gibt keinen Test, der über die tatsächliche Registration mit einem kontrollierten Provider `Matched`/`source-backed`, Source-only-Symbol, Filter-/Limit-Weitergabe, Providerdiagnose und Registry-Deduplizierung prüft. Dadurch würde ein Rückfall des Adapters auf den Legacy-Overload weiterhin grün bleiben.
- **Remediation:** Eine fokussierte Component-Regression über die bestehende Tool-Collection bzw. den vorhandenen in-process-MCP-Harness ergänzen. Eine `AssemblyAnalysisHostComposition` mit deterministischem Recording-Provider und Snapshot-Fixture an `McpServerOptionsFactory.BuildToolCollection` übergeben, beide registrierten Tools aufrufen und `source-backed`, Source-only-Symbol, Diagnose, Filter/Limits sowie ResidentCount/Deduplizierung prüfen. Die bestehende Support-Testabdeckung und der netzwerkfreie Unavailable-Fallback bleiben bestehen; kein zweiter Result-Builder und keine neue Fixture-Duplikation.

### 2. Gemeinsame Composition-Instanz über mehrere Daemon-Sessions ist nicht testbelegt

- **Severity:** MAJOR
- **Ebene:** Plan-Erfüllung / logische Korrektheit
- **Ort:** `src/AiNetLinter/Mcp/Daemon/DaemonHostCommand.cs:44-66,68-90`; `src/AiNetLinter.FastTests/Mcp/Daemon/DaemonHostMcpContractTests.cs:13-22`; `src/AiNetLinter.IntegrationTests/Mcp/Daemon/DaemonHostMcpProcessContractTests.cs:34-89`
- **Beleg:** Der Produktionscode capturt die Composition zwar in `CreateSessionRunner` (Zeilen 54-66), aber der Fast-Test führt `RunMcpSessionAsync` nur einmal mit einer EOF-Verbindung aus. Der Prozess-Contract-Test baut ebenfalls genau eine Verbindung auf und ruft beide Assembly-Tools nur einmal auf. Die vorhandenen Mehrclient-Tests prüfen die gemeinsame Projekt-Registry, nicht Provider-/Snapshot-Registry-/Orchestrator-Identität. Es fehlt ein Test mit zwei Sessions/Connections, der dieselbe Composition verwendet und anhand eines kontrollierten Providers bzw. residenten Snapshots die gemeinsame Host-Lifetime und Deduplizierung nachweist.
- **Remediation:** Einen deterministischen Daemon-Contract ergänzen, der zwei MCP-Sessions nacheinander oder parallel über denselben Hostpfad mit derselben Composition ausführt und beide direkten Assembly-Tools aufruft. Mit einem Recording-Provider und vorhandenen Snapshot-Fixtures mindestens identische Provider-/Registry-Nutzung, einen gemeinsamen ResidentCount sowie Freigabe erst nach Hostende assertieren; die Session-spezifische `DaemonRuntimeContext`-Weitergabe und bestehende Projektpfade unverändert lassen.

## Build-/Test-Status

- `dotnet build` — **grün**, 0 Warnungen, 0 Fehler.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` — **grün**, 1921/1921.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` — **grün**, 360/360, Dauer 2 m 45 s.
- `git diff --check db386bc4^ db386bc4` — **grün**.
- Stress-Tests wurden **nicht** ausgeführt.

## DRY-, Magic-Value- und Dead-Code-Prüfung

Der begrenzte `find_duplicates`-Audit im `src/AiNetLinter/Mcp`-
Produktionsscope fand keine exakten Duplikat-Cluster. Die sieben angefassten
Produktionsdateien lieferten im `find_magic_values`-Audit keine Treffer; der
gezielte `find_dead_code`-Audit des MCP-Scopes fand keinen High-Confidence-
Dead-Code. `TD-001` bis `TD-004` bleiben unverändert, weil kein Befund direkt
und sicher aus diesem Host-/MCP-Kompositionsschnitt folgt.
