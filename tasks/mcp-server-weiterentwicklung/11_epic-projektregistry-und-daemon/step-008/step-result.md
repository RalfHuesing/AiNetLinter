---
status: done
type: step-result
task: 11_epic-projektregistry-und-daemon
step: 008
epic: EPIC-A
step_type: single
coded_by: coder
coded_by_model: GPT-5
coded_by_model_knowledge_cutoff: nicht deklariert
coded_at: 2026-08-24T01:15:00+02:00
code_commit_hash: 3c01d78a
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 008: EPIC-A-Abschluss mit Overview-Liveprüfung und Meilenstein-Doku

## Zusammenfassung

Die bestehende C#-MCP-Teststrecke kann jetzt Resource- und Template-Discovery
sowie `resources/read` über den SDK-Client ausführen. Der echte Repository-Host
las `ainetlinter://overview?projectRoot=<Uri.EscapeDataString(repoRoot)>`; die
Prüfung bestätigte den `text/markdown`-Snapshot, den adressierten Repository-
Root, Solution-/Regelstatus, die leere statische Resource-Liste, das
Overview-Template und die 26 Tools aus den sechs bestehenden
Registrierungsgruppen. Der Host akzeptierte das Query-Template, daher war kein
Resource→Tool-Rückfall erforderlich.

## Geänderte Dateien

- `src/AiNetLinter.IntegrationTests/Mcp/Platform/McpProcessHost.cs` — SDK-
  Wrapper für Tool-, Resource- und Resource-Template-Discovery sowie Resource-
  Reads.
- `src/AiNetLinter.IntegrationTests/Mcp/Platform/ReadOnlyMcpHostFixture.cs` —
  read-only Fixture-Fassade für die neuen SDK-Aufrufe.
- `src/AiNetLinter.IntegrationTests/Mcp/McpLiveRepositoryTests.cs` — echter
  Repository-Live-Test mit URI-Encoding, Discovery und Toolgruppen-Inventur.
- `Docs/ROADMAP.md`, `tasks/mcp-server-weiterentwicklung/00_uebersicht-und-entscheidungen.md`
  und `tasks/mcp-server-weiterentwicklung/90_bewusst-nicht-umsetzen/Konzept.md`
  — EPIC-A-Meilenstein und Entscheidungsregister sachlich nachgezogen.
- `tasks/mcp-server-weiterentwicklung/11_epic-projektregistry-und-daemon/codemap.md`
  — Pointer für die erweiterte Live-Teststrecke.
- `tasks/mcp-server-weiterentwicklung/11_epic-projektregistry-und-daemon/tech-debt.md`
  — ein append-only Eintrag für den im Audit belegten exact-Testhelper-Cluster.

## Commits

- **Code-Commit-Hash:** `3c01d78a`
- **Code-Message:** `test: Pruefe Overview-Resource live [11_epic-projektregistry-und-daemon]`
- **Doku-Commit:** folgt als separater zweiter Commit dieses Steps.
- **Push:** nein (lokal)

## Live-Grenzen und Registrierungsnachweis

- Der erreichbare SDK-Host lieferte das Template
  `ainetlinter://overview{?projectRoot}` und akzeptierte die konkrete, mit
  `Uri.EscapeDataString` gebildete Resource-URI.
- `resources/read` lieferte genau einen `TextResourceContents`-Eintrag mit
  `MimeType = text/markdown`; der Text enthielt Root, Solution- und
  Regelstatus. `resources/list` war leer, weil keine statische Resource
  registriert ist.
- Die Live-Inventur prüfte 26 Tools in sechs Gruppen: SymbolGraph (6),
  SymbolBody (1), FileStructure (5), Analysis (10), DuplicateDetection (1)
  und ServerMaintenance (3). Die Mengen entsprechen dem bestehenden
  Inventory-Vertrag.
- Read-only geprüft: `ainetlinter.project.json` enthält nur `solution` und
  `rules`; Repo-`.mcp.json` und
  `C:\Users\Ralf\AppData\Local\hermes\config.yaml` registrieren `ainetlinter`
  mit `command` plus `--mcp-server`. Im MCP-Registrierungsweg wurden keine
  `--path`- oder `--config`-Argumente gefunden; die externe Datei wurde nicht
  verändert.
- Kein erreichbarer Hostfehler trat auf. Deshalb wurde weder ein
  Resource→Tool-Rückfall umgesetzt noch als notwendige Hostentscheidung
  behauptet. Andere, nicht getestete MCP-Hosts bleiben außerhalb dieses
  Nachweises.

## Drift-Audit (einmalige Epic-Runde)

Die im Planerlauf bereits ausgeführte Runde wurde nicht wiederholt:

- `find_duplicates(scopeDir="src", minTokens=20)`: ein `exact`-Cluster aus
  `TrackingServerFactory.MinimalConfig` und `TestConfigFactory.CreateEmpty`
  wurde semantisch geprüft. Beide Methoden erzeugen dieselbe leere Config;
  die Aufrufstellen liegen aber in unterschiedlichen Test-/TestKit-Grenzen.
  Ohne Architekturentscheidung zur gemeinsamen TestKit-Abhängigkeit wurde
  nicht refaktoriert; der Befund steht append-only als TD-006 im Tech-Debt-Log.
- Near-Kandidaten, darunter Registry-Testhelfer, sind überwiegend
  testbezogene Szenario-Varianten oder unterschiedliche Aufrufkontexte.
- Der strukturelle Scan lieferte beabsichtigte Test-Helfer-Kandidaten; ohne
  nachgewiesene gemeinsame fachliche Absicht erfolgte keine Konsolidierung.
- `find_magic_values(scopeFilter="src")` lieferte zahlreiche heuristische
  Kandidaten; daraus wurde kein EPIC-A-relevanter, entscheidungsfreier Fix
  abgeleitet.
- `find_dead_code(scopeFilter="src")` meldete die LOW-Hinweise
  `ServerInstructions.FitsBudget` und `LinterErrorCodes.AmbiguousSolution`.
  Beide sind nicht Teil der Live-Harness-Änderung und wurden nicht gelöscht.

## Build-/Test-Output

- `dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~OverviewResource --no-restore` → grün, 10 Tests.
- `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~LiveDogfood_OverviewResourceRead_UsesEncodedRepositoryRoot --no-restore` → grün, 1 Test.
- `dotnet test src/AiNetLinter.IntegrationTests --filter 'FullyQualifiedName~McpServerCommandJsonRpcFramingTests|FullyQualifiedName~McpLiveRepositoryTests' --no-restore` → grün, 31 Tests.
- `dotnet build` → grün, 0 Warnungen, 0 Fehler.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` → grün, 1682 Tests.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` → grün, 352 Tests.
- Ein parallel gestarteter FastTests-/IntegrationTests-Build kollidierte nur
  auf der gemeinsamen `AiNetLinter.dll`-Datei (`CS2012`); nach dem Abschluss
  des ersten Builds lief der identische Integration-Slice seriell grün. Es
  wurde kein Code-Fix aus diesem Infrastrukturartefakt abgeleitet.
- Stress-Tests wurden nicht ausgeführt.

## MCP-Quality-Gates

- `get_server_health`: `Loaded`, Solution `AiNetLinter.slnx`, Config
  `rules.json`, 0 MCP-Fehler im Call-Log.
- `get_feature_context` und `get_impact`: neue Host-/Fixture-Methoden und der
  Live-Test semantisch geprüft; Aufruferkette Host → read-only Fassade →
  Repository-Test bestätigt.
- `get_violations` im Scope `src/AiNetLinter.IntegrationTests/Mcp`: 0.
- `safeguard` im gleichen Scope: 10,00/10 bei Threshold 8,00.
- `metrics_lookup`: alle neuen Wrapper sowie der Live-Test innerhalb der
  konfigurierten LOC-, Komplexitäts- und Parametergrenzen.

## Abweichungen vom Plan

Der vorhandene SDK-Pfad wurde verwendet; der Raw-Wire-Harness blieb
unangetastet, weil der SDK-Client die erforderlichen Discovery- und Read-
Operationen bereits anbietet. Die konkrete Hostprüfung war erfolgreich, daher
war die im Konzept erlaubte Rückfallentscheidung nicht anzuwenden.

## Beobachtungen

Die Toolgruppen-Inventur muss im Integrationstest als bewusst gespiegelt
dokumentierte Menge geführt werden, weil der bestehende exakte Inventory-Test
auf die interne `OverviewResourceRegistration` zugreift und diese
Testprojektgrenze im Live-Test nicht direkt exponiert. Die Live-Menge wird
gegen den eingefrorenen 26er-Vertrag und die sechs Registrierungsgruppen
assertiert.

## Bekannte Unschärfen

Der Test belegt den laufenden lokalen SDK-Host und die Repository-Registrierung
für Hermes read-only, nicht die Query-Expansion jedes externen MCP-Clients.
Die externe Hermes-Konfiguration ist kein Git-Artefakt und wurde nur gelesen.
EPIC-B (Daemon-Transport, Thin-Client und Lifecycle) ist nicht Gegenstand
dieses Ergebnisses.

## Falls Status `blocked`

Nicht zutreffend.
