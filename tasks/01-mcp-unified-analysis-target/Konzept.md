---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: large
execution_mode: autonomous
rules_dir: .agents/rules
last_updated: 2026-09-07
open_questions: []
depends_on:
  - tasks/ainetlinter-mcp-usage-audit/Konzept.md
  - tasks/ainetlinter-mcp-usage-audit/shared/Befundmatrix.md
  - tasks/ainetlinter-mcp-usage-audit/shared/MCP-Verbesserungsrahmen.md
related_tasks:
  - tasks/02-mcp-agent-handoff/Konzept.md
  - tasks/04-mcp-assembly-quality/Konzept.md
supersedes: null
---

# Task 01: Einheitlicher MCP-Analysezielvertrag

## Ziel und Problem

Ein Agent gibt für jede zielgebundene Operation nur einen absoluten
`targetPath` an: eine vorhandene `.sln`/`.slnx` für Source oder `.dll`/`.exe`
für Decompiled-Analyse. Der Server erkennt die Herkunft deterministisch. Heute
erzwingen `targetType`, Verzeichnis-`projectRoot` und
`ainetlinter.project.json` mehrere Bootstrap-Wahrheiten; dadurch sind Schema,
Resources, Health und Folge-IDs nicht verlässlich kombinierbar.

## Primärer Agentennutzen

Der primäre Agentennutzen ist ein einziger, vorhersagbarer Einstieg ohne
Projektdatei-Raten oder falsche Assembly-/Source-Fallbacks. Dies ist bewusst
der erste Release: Jeder spätere Handoff- oder Workflowvertrag würde sonst
zweimal implementiert und getestet.

## Source of Truth und betroffene Bereiche

- Evidenz: Audit plus `shared/Befundmatrix.md`, insbesondere
  `combo-cross-target`, `combo-batch-ids`, `combo-resources-health`,
  `get_server_health`, `resource-*`, `reload_config`.
- Gemeinsame Semantik: `shared/MCP-Verbesserungsrahmen.md`.
- Runtime: `Mcp/AnalysisTarget*`, Registrierungen, `Projects/*`,
  `Assemblies/Analysis/*`, `ProjectDefinition*`, Resources, Dispatcher,
  `reload_config`, Health und alle MCP-E2E-Tests.
- Dokumentation: `Docs/agent-api.md`, `Docs/integration.md`, MCP-Instructions
  und Toolbeschreibungen.

## Scope

- Alle öffentlichen zielgebundenen Tool- und Resource-Schemas migrieren
  atomar zu `targetPath`; `targetType`, `projectRoot`, freie `configPath` und
  `ainetlinter.project.json` verlassen aktive Runtime, Schema und Doku.
- Ein Resolver validiert absolute vorhandene Dateien und Endungen ohne
  Verzeichnissuche. Er liefert Ursprung, kanonischen Pfad, Fingerprint,
  Capability und Source-Regelstatus an getrennte Project-/Assembly-Leases.
- Eine Source-Session ist genau an die übergebene Solution gebunden.
  `analysisRoot` für dateiorientierte Tools ist ausschließlich deren
  normalisiertes Elternverzeichnis; der Server steigt niemals in ein
  übergeordnetes Repository auf und wählt bei mehreren Solutions nie selbst.
  Projektverweise außerhalb dieses Verzeichnisses bleiben semantische
  Abhängigkeiten, aber erweitern keinen Dateibaum stillschweigend.
- Vor dem ersten zielgebundenen Call wählt der Agent die konkrete Solution aus
  der hostsichtbaren Workspace-Dateiliste. Eine targetfreie MCP-Solution-Suche
  ist bewusst kein Ersatzvertrag und kein Bestandteil dieses Releases.
- Source liest ausschließlich optionale `ainetlinter-rules.json` neben der
  Solution: fehlt = Navigation `not_configured` für Lint; ungültig/nicht lesbar
  = Konfigurationsfehler; keine Arbeitsverzeichnis-, Eltern- oder Defaultsache.
- Assembly ist `origin=decompiled`, Lint `unsupported`; Navigation erhält nur
  tatsächlich verfügbare Capabilities. Der Task verbessert noch keinen
  Decompiler-Output oder dessen Cache-Qualität.
- Ressourcen übernehmen den Pfad in URL-kodierter `targetPath`-Form; Health
  bleibt ohne Target global. `report_observability_feedback` bleibt ungebunden.

## Muss-Kriterien

- Eine `.sln`, `.slnx`, `.dll` und `.exe` lassen sich ausschließlich über
  `targetPath` korrekt auflösen; falsche Endung, relativer/fehlender Pfad und
  Verzeichnis sind `invalid_argument` mit Feldpfad und nächstem Schritt.
- Der alte Vertrag wird weder still akzeptiert noch ignoriert. Ein alter Key
  ist ein expliziter `invalid_argument`; aktiver Schema-/Doku-/Runtime-Scan
  findet keine Altverwendung außerhalb markierter Historie.
- Source ohne Regeln navigiert, aber lintet nie scheinbar sauber; ungültige
  Regeln fallen nie auf Defaultregeln zurück. Assembly-Source-Mismatch ist
  `unsupported`, nicht `empty`.
- Resolver, Dispatcher, Resources, Text und `structuredContent` teilen
  `origin`, Capability, Operationstatus, Scope und den vom Rahmen verlangten
  Navigationkern.

## Messbare Akzeptanzkriterien

- Raw-Wire-Tests für jeden Tooltyp und beide Resources beweisen genau ein
  `targetPath`-Schema sowie Ablehnung jedes Altkeys.
- Je eine Solution mit gültigen, fehlenden und fehlerhaften Nachbarregeln,
  zwei DLLs/EXE, UNC-/Leerzeichenpfad und ein falscher Targettyp laufen durch
  E2E. Erwartete Status stehen in Testdaten, nicht nur im Markdown.
- Ein Workspace mit zwei Solutions beweist: Der übergebene Pfad bestimmt
  `analysisRoot` und Semantik; keine Parent-/Sibling-Solution wird geraten
  oder still einbezogen.
- Ein neuer Serverprozess löst denselben Target-Fingerprint gleich auf;
  zielgebundenes Health, Overview und Rules stimmen mit Toolantworten überein.

## Non-Goals

Keine Kompatibilitätsphase, automatische Migration, Regeldateisuche,
Decompiler-Qualitätsarbeit, ID-/Ranking-Überarbeitung, Git/Build/Test-Parität
für Assemblys oder Zusammenlegung der Leases.

## Architekturannahmen

`ResolvedAnalysisContext` ist die schmale Naht zwischen Resolver und
Dispatch. Project- und Assembly-Registry behalten eigene Schlüssel, Hashing,
Lease, TTL und Eviction. Nur Target-/Capability-/Statusprojektion wird geteilt;
kein DI-Container, Command-Bus oder Mega-Sessionmodell.

## Fehler-, Fallback- und Lebenszeitsemantik

Der Resolver trennt `invalid_argument`, `not_configured`, `unsupported` und
exogenen Fehler. Assembly-Content-Hash und kanonischer Pfad gehören zur
Sessionidentität; eine während des Öffnens veränderte Datei liefert stabilen
Snapshot oder `stale_snapshot`/Retry. Task 04 präzisiert Generation, Cleanup
und Diagnosequalität. Ohne Regeln darf nur die Lint-Capability fehlen,
Navigation bleibt `ok`.

## Abhängigkeiten

Nur Audit und Rahmen. Task 02, 03 und 04 hängen hiervon ab. Das verhindert,
dass sie `targetType`-Adapter oder doppelte Resource-/Schema-Tests einführen.

## Risiken und Sackgassen

Der Schnitt bricht Clients. Ein Alias würde aber genau die Mehrdeutigkeit
konservieren und ist ausgeschlossen. Der Hauptschutz ist atomare Migration
aller Registrierungen, nicht ein „teilweise“ umgestellter Resolver. Fehlt die
feste Nachbarregeldatei oder ist sie ungültig, ist `not_configured` bzw. ein
Konfigurationsfehler das beabsichtigte Ergebnis; ein versteckter Override ist
keine Fallback-Option.

## Alternativen mit Konsequenzen

- Alten Vertrag behalten: geringerer Release-Schmerz, aber dauerhafte doppelte
  Discovery und keine verlässliche Capability-Matrix.
- `targetType` als Alias: kurzfristig kompatibel, aber Agenten können nie
  erkennen, welche Eingabe maßgeblich war.
- Targetmigration nach Navigation: garantiert doppelte Contract-Arbeit;
  deshalb verworfen.

## Konkrete Agenten-Szenarien

| Fall | Call / erforderliche Antwort / verbotene Behauptung | Nächster Schritt |
| --- | --- | --- |
| D: fehlende/ungültige Regeln | `get_violations(targetPath=solution)` zeigt `not_configured` bzw. Fehler samt Rule-Pfad; nie „0 Violations“. | Regeln bereitstellen oder nur Navigation fortsetzen. |
| E: falsche Capability | Source-Tool an DLL bzw. Assembly-Tool an Solution liefert `unsupported`, `origin` und Capability. | Ein unterstütztes Tool oder passendes Target wählen. |
| F: Restart/stales Ziel | derselbe Pfad wird neu aufgelöst; abweichender Fingerprint liefert `stale_snapshot`, nicht „nicht gefunden“. | Mit ausgegebenem aktuellen Snapshot erneut versuchen. |

## Token-/Antwortbudget-Annahmen

Der Resolver-/Statusblock bleibt unter 1 KiB. Ressourcen liefern keine
vollständige Tooldokumentation, sondern Targetstatus und Verweis auf Schema.
Für Ergebnisnutzdaten gelten die Rahmenbudgets; dieser Task führt keine
Continuation ein.

## Verifikation

Unit- und Contract-Tests für Resolver/URI/Altkeys, Integrationstests für beide
Herkünfte und Regelzustände, frischer MCP-Dogfood-Call, Build und beide
Nicht-Stress-Suiten. Zusätzlich aktiver Vertrags-Scan und `git diff --check`.

## Dokumentationsbedarf

`Docs/agent-api.md`, `Docs/integration.md`, Resources, Serverinstructions und
`.agents/rules/AiNetLinter-McpWorkflow.mdc` erklären ausschließlich den neuen
Vertrag und die Source-/Decompiler-Grenzen.

## Release-Gate

Kein Release, solange ein aktiver Altkey, eine implizite Regel-/Projektdatei-
Suche oder ein `empty` statt Capability-/Konfigurationsstatus nachweisbar ist.

## Nächste fachliche Entscheidung

Keine weitere Produktentscheidung: Der harte dateibasierte Vertrag ist
akzeptiert. Der Dogfood-Release belegt nur dessen Umsetzung; danach beginnt
Task 02 ohne Compatibility-Arbeit.
