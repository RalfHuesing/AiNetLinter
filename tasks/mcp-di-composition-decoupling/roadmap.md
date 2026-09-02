# Roadmap: MCP-Komposition entkoppeln und Qualitätsgrenzen wiederherstellen

Primäraufgabe: MCP-Komposition entkoppeln und Qualitätsgrenzen wiederherstellen.

Status: executing  
current_epic: 2  
letzter_checkpoint: 3e837436  
Tech-Debt-Register: `tech-debt.md`  

Der Großkonzept-Modus folgt dem freigegebenen `Konzept.md`. Die Einordnung als
großes Konzept leitet sich aus seinem repositoryweiten MCP-Scope und dem
expliziten Orchestrierungsauftrag ab.

## Epic 1: Lease-basierte Zustandsgrenze entkoppeln

Status: done

Ziel: Die Assembly-Analyse verwendet an ihrer Lease-Grenze ausschließlich eine
schlanke Zustandsabstraktion statt des konkreten `McpCodeGraphServer`.

Abhängigkeiten: keine.

Betroffene Bereiche: MCP-Host, Assembly-Analyse-Lease, Entry-Erstellung,
Tool-Support sowie direkte zugehörige Tests.

Muss-/Akzeptanzkriterien:

- `ISolutionStateProvider` bildet ausschließlich die tatsächlich benötigten
  Lease-Capabilities ab und wird durch `McpCodeGraphServer` implementiert.
- `AssemblyAnalysisLease` und die zugehörigen Übergabepfade referenzieren
  keinen konkreten `McpCodeGraphServer` mehr.
- Locking- und Cancellation-Semantik der Assembly-Analyse bleiben erhalten.
- `ProjectLease`/`ProjectRegistry` bleiben unverändert konkret typisiert;
  weder DI-Container noch `IServiceProvider` werden eingeführt.

Verifikation: fokussierte FastTests, gezieltes `get_violations` nach der
letzten Codeänderung und unabhängiger Review des Implementierungsdiffs.

Annahmen/offene Fragen: Das Interface wurde nach MCP-first-Referenzprüfung um
die tatsächlich benötigten Capabilities erweitert; weitere Erweiterungen waren
nicht erforderlich.

## Epic 2: Größenlimits und Regressionsschutz bereinigen

Status: in_progress

Ziel: Die konkreten Line-Count-Verstöße werden durch kleine, verhaltensneutrale
Extraktionen behoben und die entkoppelten Komponenten erhalten passende
Regressionstests.

Abhängigkeiten: Epic 1.

Betroffene Bereiche: `AssemblySymbolResolver`,
`AssemblyAnalysisSessionTests` und unmittelbar betroffene Assembly-Analyse-
Tests.

Muss-/Akzeptanzkriterien:

- `AssemblySymbolResolver.ResolveAsync` hält das produktive Methodenlimit ein.
- `AssemblyAnalysisSessionTests.cs` hält das Dateilimit ein, ohne Testabdeckung
  oder Testparallelität zu verringern.
- Die Tests sichern Interface-Grenze, Body-Resolution und relevante
  Registry-Concurrency-Invarianten ab.

Verifikation: betroffene Unit-/Component-Tests und gezieltes
`get_violations` nach der letzten Codeänderung.

Annahmen/offene Fragen: Die konkrete Zuordnung ausgelagerter Tests folgt ihrer
fachlichen Verantwortung, nicht einer künstlichen Größenaufteilung.

## Epic 3: MCP-Qualitätsbefunde im vereinbarten Scope bereinigen

Status: open

Ziel: Belegte, scope-nahe Restbefunde zu Kopplung, DRY, Dead Code und Magic
Values werden ohne neue globale Abstraktionen bereinigt.

Abhängigkeiten: Epic 1 und Epic 2.

Betroffene Bereiche: die durch aktuelle MCP-Evidenz bestätigten Pfade unter
`src/AiNetLinter/Mcp` sowie ihre unmittelbaren Tests.

Muss-/Akzeptanzkriterien:

- Der MCP-/Assembly-Bereich hat keine AIContextFootprint-, MaxLineCount- oder
  MaxMethodLineCount-Verstöße.
- Sichere, belegte lokale Qualitätsbefunde werden behoben; Unsicheres oder
  scope-fremdes bleibt mit Evidenz im Tech-Debt-Register.
- `rules.json`, Roslyn-Rules, Logging und CLI-Framework bleiben unverändert.

Verifikation: gezieltes `get_violations`; die vollständigen MCP-Audits und
Solution-Gates erfolgen im Abschluss.

Annahmen/offene Fragen: Nur tatsächlich bestätigte Befunde werden in Arbeit
genommen; das Konzept ermächtigt keinen zufälligen globalen Cleanup.

## Abschluss-Checkliste

- [ ] Kein DI-Container, `IServiceProvider` oder neues NuGet-Paket.
- [ ] `ProjectLease`/`ProjectRegistry`, Roslyn-Rules, Logging, CLI und
  `rules.json` bleiben außerhalb des vereinbarten Scopes unverändert.
- [ ] MCP-/Assembly-Klassen liegen ohne Suppressions unter dem
  AIContextFootprint-Limit von 2.500.
- [ ] MCP-Produktions- und Testdateien erfüllen Datei- und Methodenlimits.
- [ ] MCP `find_dead_code`, `find_magic_values` und exaktes
  `find_duplicates` melden im MCP-Scope keine relevanten Befunde.
- [ ] MCP `get_violations` meldet projektweit keine Verstöße.
- [ ] MCP `safeguard` mit `minScore: 8.0` meldet 10,00 / 10,00 und PASS.
- [ ] `dotnet build` sowie beide vollständigen Nicht-Stress-Testsuiten sind
  nach dem letzten Codezustand grün.
- [ ] Die Core-Linter-Ausführung bleibt containerlos; kein zusätzlicher
  Startup-Pfad wird eingeführt.
