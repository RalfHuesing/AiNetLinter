---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 033
epic: EPIC-04
step_type: correction
reviewed_by: kritiker-agent
reviewed_by_model: gpt-5 (Codex)
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-30T01:14:06+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 033: Konfigurierbare Cache-Root-/Refresh-Policy und Step-032-Evidenz

## Verdict

- [ ] `approved`
- [x] `issues`

Step 033 wird nicht freigegeben. Die gültige Konfiguration wird bis zur
konfigurierten Source-Root, zum Writer/Reader und zur Fresh/Stale-Policy
korrekt konstruiert. Zwei MAJOR-Befunde verhindern jedoch die vollständige
Fail-Closed-Abnahme: Die CacheRoot-Validierung lässt URI-/Credential-artige
Segmente passieren, und ein fehlgeschlagener Konfigurationsload kann im
bestehenden Assembly-Tool als erfolgreicher Decompilation-Fallback enden.
Beide Befunde gehören in einen gemeinsamen Korrekturscope für die
Konfigurations-Sicherheitsgrenze, ihre Fehlerweitergabe und die direkten
Regressionen; daraus werden keine Mini-Steps abgeleitet.

## Review-Grundlage

Geprüft wurden die Commits `0c6ab50e8e76c2d61f6a8f5e5ec088b963b7ea28`
und `c6787c123fe469f637566aa63eb0e7dc3d8896ae`, der tatsächliche Diff,
die in `step-plan.md` genannten Produktions- und Testdateien sowie die
vollständigen Task-, Regel-, Konzept-, Step-032- und Step-033-Nachweise.
Der zweite Commit enthält ausschließlich `step-033/step-result.md`.

## Kriterienbewertung

| Kriterium | Bewertung | Nachweis / Einschränkung |
|---|---|---|
| 1. CacheRoot strikt, sicher und geheimnisfrei bis zur effektiven Source-Root | **nicht erfüllt** | Gültige Werte werden als `<CacheRoot>/source` verdrahtet und Diagnosen geben den Wert nicht aus. `TryResolveCacheRoot` akzeptiert aber bestimmte URI-/Credential-artige und reservierte Segmente; siehe `MAJOR-001`. |
| 2. Positives, bounded RefreshInterval und tatsächliche Policy-Nutzung | **erfüllt** | Positives ganzzahliges JSON, `TimeSpan`-Grenze, Overflow-/Null-/Negativ-/Typfehler und Default 60 sind abgedeckt. Die konfigurierte Zeitspanne wird in der erzeugten Policy verwendet; Grenzzeitpunkt und Add-Overflow bleiben fail-closed. |
| 3. Korrigierte Step-032-Evidenz | **erfüllt** | `step-032/step-result.md` dokumentiert 5,83/10 bei Threshold 8,00 als FAIL, 369 Produktions-/140 Testmethoden, den engen ExternalSourceRepository-Scope mit 0 Violations, den breiten bestehenden MaxDirectoryChildren-Befund, keinen unbelegten changed-only-Magic-Values-Claim und den leeren `get_impact`-Diff samt Limitierung. |
| 4. Konfigurationsdokumentation | **teilweise erfüllt** | Defaults, Auflösung, Kanonisierung und Intervalle sind beschrieben. Die Aussage, URI-artige Werte würden abgelehnt, ist für die in `MAJOR-001` genannten Eingaben nicht wahr. |
| 5. Scope und bestehende Invarianten | **erfüllt, mit offenem Fehlerpfad** | Keine unerlaubte Erweiterung in Retention/GC, Invalidierung, Health, Dirty/Unbuilt, Host/MCP-Wiring, Provider/Snapshot/Registry oder EPIC-05; Publish-, Reuse-, Refresh-, Transport-, Credential-, HTTP-, Git-, Process-, Native- und Reparse-Code wurde nicht neu entworfen. Der explizit zu prüfende Invalid-Config-Fallback bleibt dennoch als MAJOR offen. |
| 6. Lokale, deterministische Tests | **teilweise erfüllt** | Die vorhandenen Konfigurations-, Factory-, Fresh/Stale-, Fehler-, Cancellation- und Ownership-Tests sind lokal und netzwerkfrei. Adversariale URI-/Credential-/reservierte Pfadformen und die end-to-end-Fail-Closed-Wirkung bis zum Tool-Ergebnis fehlen. |
| 7. Scoped MCP-Audits | **erfüllt** | Exact-Clone-Sweeps und Violations wurden ausschließlich mit absolutem `projectRoot` und begrenzten Konfigurations-/Assemblies-Scope ausgeführt. Keine neuen Exact-Duplikate; strukturelle Treffer sind manuelle Kandidaten bzw. bestehende/intentional ähnliche Hilfen. |
| 8. Build, Tests, Skips und Leaks | **erfüllt** | Build, Fokuslauf und beide Nicht-Stress-Gesamtläufe sind grün. Stress wurde nicht ausgeführt. Die zwei bekannten Win32-1314-Skips sind transparent dokumentiert; keine Testhost-/VSTest-Prozesse, keine Repo-Temp-Dateien und keine externe Source-Cache-Artefakte nach dem Lauf. Drei vorhandene MSBuild-NodeReuse-Prozesse blieben unverändert. |

## Befunde

### MAJOR-001 — CacheRoot lässt unsichere URI-/Credential-artige Pfade passieren

- **Priorität:** MAJOR
- **Kategorie:** Logische Korrektheit / Sicherheits- und Konfigurationsvertrag
- **Stelle:** `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs:171-192`, insbesondere die Prüfung in den Zeilen 179-180
- **Auswirkung:** Die Prüfung verwirft nur `://` und die Segmente `.`/`..`. Die reale gebaute Implementierung akzeptiert unter Windows unter anderem `https:/user:secret@example.invalid/cache`, `file:/C:/secret`, `C:/temp/a:secret` und `C:/temp/a?b` und löst diese als Pfade auf. Damit kann ein Wert, der als URI-/Credential-Segment ausgeschlossen sein muss, die Loader-Grenze passieren und an Factory/Writer gelangen. Das widerspricht dem strikt validierten, geheimnisfreien CacheRoot-Vertrag und der Aussage in `Docs/configuration.md:1636-1637`; außerdem werden unsichere direkte `ExternalSourceCacheOptions`-Konstruktionen durch die reine Kanonisierung in den Zeilen 23-26 nicht grundsätzlich verhindert.
- **Korrekturscope:** Die gemeinsame Pfadvalidierung muss rohe CacheRoot-Eingaben vor jeder Kanonisierung als zulässige Windows-Dateipfade klassifizieren und URI-Schemes, Authority/Userinfo, Nicht-Drive-Doppelpunkte sowie reservierte/ungültige Pfadsegmente fail-closed ablehnen. Loader, Options-Konstruktor und Cache-Factory müssen dieselbe Sicherheitssemantik verwenden, ohne Eingabewerte in Diagnosen zu spiegeln. Ergänze eine lokale Regression für die genannten Formen sowie für bereits vorhandene Dot-Segmente und prüfe `Configuration == null` plus geheimnisfreie Diagnose.

### MAJOR-002 — Ungültige Konfiguration kann als erfolgreicher Decompilation-Fallback enden

- **Priorität:** MAJOR
- **Kategorie:** Logische Korrektheit / Fail-Closed-Kontrakt
- **Stellen:** `src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelectionOrchestrator.cs:45-47` und `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupport.cs:53-75`; bestehender Nachweis in `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs:243-269`
- **Auswirkung:** Bei `configurationResult.Succeeded == false` gibt der Orchestrator nur eine leere Selection-Scope zurück. `AssemblyAnalysisToolSupport` ruft anschließend `CreateContextAsync` ohne Source-Selection auf und liefert bei erfolgreicher Decompilation ein normales Ergebnis mit `OriginKind == "decompiled"` und angehängter Diagnose. Für einen neu eingeführten ungültigen `CacheRoot` ist das damit kein fehlgeschlagener Config-/Provider-Pfad, sondern ein erfolgreicher Fallback-Erfolg. Der vorhandene Test schreibt diese Semantik ausdrücklich fest; sie erfüllt nicht die für Step 033 geforderte Prüfung, dass invalid config nicht in einen erfolgreichen Provider-/Fallback-Erfolg fällt.
- **Korrekturscope:** Die Fehlergrenze zwischen Loader, Source-Selection und Assembly-Tool muss für ungültige Konfiguration explizit fail-closed werden: kein erfolgreicher Context/Tool-Erfolg aus einem Konfigurationsfehler, keine Provider-Aktion und weiterhin geheimnisfreie Diagnosen. Die bestehende allgemeine Fallback-Semantik für fachlich nicht verfügbare externe Sources darf dabei nicht ungezielt verändert werden. Aktualisiere den vorhandenen Testvertrag und ergänze einen konkreten `CacheRootInvalid`-Regressionstest vom Loader bis zum Tool-Ergebnis.

## Logische und konzeptionelle Prüfung ohne Findings

Die zentrale `ExternalSourceCacheOptions`-Defaultdefinition verwendet 60
Minuten; die Refresh-Policy referenziert diesen Default und führt keine
zweite unabhängige Defaultzahl ein. `SourceDirectoryName` ist im Cachevertrag
zentralisiert. Die konfigurierte Factory erzeugt eine gemeinsame lokale
Writer-/Reader-Konstruktion und eine Policy mit der konfigurierten Zeitspanne;
die optionale Policy-Injektion bleibt für deterministische Tests erhalten.
Es wurde kein DI-Container, keine Runtime-Assembly-Ladung und kein Netzwerk-
oder Providerzugriff in den neuen Tests eingeführt. Die fehlende produktive
Host-/MCP-Verdrahtung ist gemäß Step-033-Plan bewusst außerhalb dieses Steps
und wird nicht als Scope-Finding bewertet.

## Build-/Test-Status

Tatsächlich ausgeführt:

```text
dotnet build
  Build succeeded; 0 warnings, 0 errors

dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceConfigurationLoaderTests|FullyQualifiedName~ExternalSourceRepositoryCacheConfigurationTests|FullyQualifiedName~ExternalSourceRepositoryCacheRefreshTests"
  47 passed, 0 skipped, 47 total

dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
  2091 passed, 2 skipped, 0 failed, 2093 total

dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
  370 passed, 0 skipped, 0 failed, 370 total
```

Stress wurde nicht ausgeführt. Die beiden FastTest-Skips betreffen die
echten Reparse-Regressionen
`ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`
und
`ExternalSourceRepositoryCacheWriterTests.PublishAsync_ActualReparseEntryFailsClosed`;
beide wurden ausschließlich wegen `ERROR_PRIVILEGE_NOT_HELD (1314)`
übersprungen. Das ist kein Sicherheitsnachweis ohne privilegierte Umgebung.

Nach den Läufen waren keine `testhost`-/`vstest.console`-Prozesse vorhanden.
Es gab 0 Dateien unter `temp` und 0 Dateien unter dem externen
`cache/source`-Pfad. Ein vorhandenes einzelnes JSON im bestehenden
`src/AiNetLinter/bin/Debug/net10.0/cache` wurde nicht als Testartefakt des
neuen External-Source-Caches gelöscht. Drei vorhandene `dotnet ... MSBuild.dll`
NodeReuse-Prozesse wurden beobachtet; sie sind keine Testhosts und wurden
nicht verändert.

## Scoped MCP-/Qualitätsaudits

Alle MCP-Aufrufe nutzten
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter`. Es wurden keine
solutionweiten Text-/Regex-Sweeps als Ersatz für semantische Audits verwendet.

- `get_violations`: 0 im Scope `src/AiNetLinter/Configuration` (27 Dateien),
  0 im engen `ExternalSourceRepository`-Scope (32 Dateien).
- `find_duplicates`, Mode `clone`, `minTokens=20`, `similarityThreshold=exact`,
  `normalizeIdentifiers=false`: 0 Cluster bei 76 Produktionsmethoden in
  Configuration, 370 in Assemblies, 71 Testmethoden in FastTests/Configuration
  und 141 in FastTests/Mcp/Assemblies.
- Der vorgeschriebene strukturelle Nachlauf fand nur manuelle Kandidaten,
  keine Lint-Verstöße: den dünnen `CreateError`-/`CreateCacheDiagnostic`-
  Wrapper in Configuration, vier bestehende/semantisch unterschiedliche
  Assemblies-Paare sowie je einen bzw. zwei Testcluster. Keiner ist ein neu
  eingeführtes Exact-Clone.
- `find_magic_values` mit `changedOnly=false` lieferte 41 Treffer in
  Configuration (39 constant-, 1 localization-, 1 security-candidate) und
  108 in Assemblies (67 constant-, 2 standard-, 39 localization-candidates).
  Es wurde kein unbelegter changed-only-Claim verwendet.
- `find_dead_code`, `private_internal`/`high`, inklusive Tests: 0 tote Symbole
  in Configuration bei 51 gescannten Symbolen und 0 in Assemblies bei 155.
- `safeguard` im breiten bestehenden Scope `src/AiNetLinter/Mcp/Assemblies`
  ergab 5,80/10 bei Threshold 8,00, also FAIL, mit denselben drei bestehenden
  Befunden (58 Directory-Einträge, `DaemonHostCommand`-Footprint und der
  breite Task-Directory-Befund). Der korrigierte historische Step-032-Wert
  5,83/10 bleibt davon getrennt und wird nicht schöngeschrieben.
- Der lokale `get_impact`-Git-Diff war leer (`changedFiles=[]` und
  `changedSymbols=[]`); die fehlende Änderungsbasis und die symbolische
  Trunkierung bei größeren Caller-Mengen sind im Step-032-Nachweis ehrlich
  ausgewiesen.

## Nächste Folgeaktion

Step 033 bleibt auf `issues`. Der nächste Coder-Scope ist ein zusammenhängendes
Fail-Closed-Korrekturpaket aus strenger roher CacheRoot-/Optionsvalidierung,
der eindeutigen Weitergabe eines invaliden Config-Status ohne erfolgreichen
Decompilation-Fallback sowie den zugehörigen lokalen Regressionen. Danach
müssen Build, Fokuslauf, beide Nicht-Stress-Gates und dieselben begrenzten
MCP-Audits erneut ausgeführt werden; der bekannte 1314-Skip bleibt transparent.
