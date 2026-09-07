---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: large
rules_dir: .agents/rules
last_updated: 2026-09-07
open_questions:
  - Soll der inkompatible Zielvertragswechsel als eigener Release freigegeben werden?
depends_on:
  - tasks/01-mcp-agent-handoff/Konzept.md
  - tasks/02-mcp-development-workflow/Konzept.md
  - tasks/ainetlinter-mcp-usage-audit/shared/MCP-Verbesserungsrahmen.md
related_tasks:
  - tasks/04-mcp-assembly-quality
supersedes: null
---

# Task 03: Einheitliches MCP-Analyseziel

## Ziel und Problem

Der Agent soll eine konkrete `.sln`/`.slnx` oder `.dll`/`.exe` ausschließlich
über `targetPath` adressieren. Der Server erkennt die Zielart selbst und
materialisiert für Assemblies den internen Decompiled-Kontext. Der öffentliche
Vertrag soll nicht zwei fast gleiche Projekt-/Assembly-Bootstrapwege verlangen.

Dieser Task ist ein bewusst inkompatibler Vertragsrelease. Er wird von der
Qualitätsverbesserung des Decompilers getrennt: Bestehende Assemblypfade
bleiben funktionsfähig und werden regressionsgesichert; neue Assemblyqualität
gehört in Task 04.

## Neuer Eingabevertrag

Zulässige direkte Eingaben sind vorhandene Dateien mit absolutem Pfad:

- `.sln` oder `.slnx` für Source-Solutions;
- `.dll` oder `.exe` für Assemblys im Decompiled-Modus.

Verzeichnisse, relative Pfade, fehlende Dateien und unbekannte Endungen werden
deterministisch abgelehnt. Die Erkennung erfolgt anhand Existenz und Endung,
nicht durch Parent-/Child-Suche oder Dateinamensraten.

Beispiel:

```json
{ "targetPath": "C:\\repo\\MyProject.slnx" }
```

oder:

```json
{ "targetPath": "C:\\libs\\ThirdParty.dll" }
```

`targetPath` bezeichnet die konkrete Eingabedatei. Der kanonische Source-
Kontext basiert auf der aufgelösten Solution; der Decompiled-Kontext auf
Assemblypfad und Inhaltsidentität.

## Harte Schnitte

- `targetType` entfällt aus allen öffentlichen MCP-Schemas und Dispatcher-
  Verträgen;
- `ainetlinter.project.json` wird nicht mehr gelesen, erzeugt oder verlangt;
- ein generiertes `<assembly-name>.project.json` entfällt;
- `solution` und `rules` sind keine Projektdefinitionspflicht mehr;
- veraltete Bootstrap-/`PROJECT_NOT_INITIALIZED`-Pfade werden entfernt oder
  auf den neuen Vertrag umgestellt;
- `projectRoot` und freie `configPath`-Ersatzverträge bleiben nicht bestehen;
- alte Werte werden nicht stillschweigend als Kompatibilitätsalias akzeptiert.

## Regelkonfiguration

Die kanonische optionale Source-Regeldatei heißt `ainetlinter-rules.json` und
liegt ausschließlich neben der übergebenen Solution. Der Name wird über eine
zentrale Konstante von Resolver, Tests und Dokumentation verwendet.

- vorhanden und gültig: `lintStatus=active`;
- nicht vorhanden: gültiger Navigationskontext mit
  `lintStatus=not_configured`;
- vorhanden, aber ungültig oder nicht lesbar: eindeutiger Konfigurationsfehler;
- keine Parent-, Child-, Arbeitsverzeichnis- oder Default-Regelsuche;
- bei fehlenden Regeln niemals erfolgreiche leere Lint-Entwarnung.

Bei Assemblies ist Linting strukturell nicht verfügbar:
`lintStatus=unsupported`. Navigation, Struktur, Referenzen, Bodies,
Assembly-Metadaten und belastbare rohe Metriken bleiben je Capability möglich.

## Einheitliches Analysemodell

Der Resolver erzeugt einen gemeinsamen Kontext:

```text
TargetPath → TargetResolver → ResolvedAnalysisContext
```

Der Kontext enthält mindestens Eingabepfad, Herkunft, Solution-/SourceRoot,
optionale Regeldatei, Regelstatus, Content-Identität, Completeness, Capabilities
und bei Assemblies Originalpfad, Generation und Snapshot-Status.

Herkunft und Capability bleiben getrennt:

| Feld | Werte |
| --- | --- |
| `origin` | `source`, `decompiled` |
| `analysisMode` | `navigation`, `navigation_and_lint` |
| `lintStatus` | `active`, `not_configured`, `unsupported` |
| `completeness` | `complete`, `partial`, `failed` |
| Operationstatus | `ok`, `unsupported`, `not_configured`, `invalid_argument`, `failed` |

Source und Decompiled teilen den Ziel- und Antwortvertrag, nicht automatisch
alle Fähigkeiten. Git, Build, Tests, Originalzeilen und Lint bleiben Source-
Capabilities; Assembly-Metadaten und Decompiled-Herkunft bleiben Assembly-
Capabilities.

## Bestehende Architektur und vollständige Refactor-Grenze

Der aktuelle Code modelliert Source- und Assemblyziele noch getrennt. Der
Schnitt betrifft deshalb alle Vertragsschichten und nicht nur den Resolver:

| Schicht | Betroffene Anker | Zieländerung |
| --- | --- | --- |
| Öffentlicher MCP-Vertrag | Registrierungen, Parameterrecords, `tools/list`, `ServerInstructions` | Nur `targetPath`; keine alten Ziel- oder Projektdefinitionsparameter |
| Zielauflösung | `AnalysisTarget`, `AnalysisTargetRequest`, `AnalysisTargetResolver` | absolute Datei und Endung auflösen; normalisierten Kontext erzeugen |
| Dispatch | `AnalysisToolCall`, Projekt-/Assembly-Routen, Registrierungs-Lambdas | nach Kontext und Capability routen |
| Source-Session | `ProjectRegistry`, `ProjectToolCall`, `ProjectLease`, `ProjectEntry` | Schlüssel auf Solution und wirksame Regelidentität umstellen |
| Projektdefinition | `ProjectDefinition`, `ProjectDefinitionLoader`, Load-Result und Fehlercodes | aus dem MCP-Ladepfad entfernen oder vollständig auf direkten Solutionpfad umbauen |
| Regelmaterialisierung | `ProjectInstanceFactory`, `reload_config` | feste optionale Nachbardatei, zentrale Konstante, kein freier Override |
| Assembly-Session | Assembly-Registry, Decompiler, Snapshot-/Resource-Lifecycle | gemeinsamen Analyse-Envelope erfüllen, eigene Lifecycle-Semantik behalten |
| Antwortprojektion | Assembly-Response, Project-/Daemon-Health, Composite-Tools | Status, Herkunft und Completeness standardisieren |
| Ressourcen | Overview-/Rules-Registrierung, Leases und Formatter | `targetPath` in URI; konkrete Datei; Assembly-Rules `unsupported` |
| Tests | Resolver, Wiring, Registry, Resource, Daemon, Assembly und MCP-E2E | Altverträge ersetzen, nicht parallel als gültig festschreiben |
| Dokumentation | `Docs/*`, `.agents/rules/*`, Bootstrap- und Servertexte | einen einzigen neuen Vertrag beschreiben |

Source- und Assembly-Registries werden nicht künstlich zusammengelegt. Der
gemeinsame Resolver und `ResolvedAnalysisContext` sind die Naht; Hashing,
Referenzexpansion, Lease, Eviction und Ressourcenlimits bleiben in den
jeweiligen Subsystemen.

Der Batch-CLI-Vertrag mit explizitem `--config` bleibt außerhalb dieses Tasks.
Die Konvention `ainetlinter-rules.json` gilt für die automatische MCP-
Auflösung neben einer übergebenen Solution, nicht als globale CLI-Migration.

## Ressourcen und Composite-Tools

- Zielgebundene Tools und Ressourcen verwenden nur `targetPath`;
- `ainetlinter://overview` und `ainetlinter://rules` kodieren den absoluten
  Pfad korrekt in der URI;
- Overview funktioniert für Source und Assembly;
- Rules meldet bei Assembly `unsupported` und bei Source ohne Regeldatei
  `not_configured`;
- `get_server_health` bleibt ohne Target ein globaler Aggregat-Aufruf und kann
  mit `targetPath` zielgebunden werden;
- `report_observability_feedback` erzeugt durch einen optionalen Kontext
  keinen impliziten Analyse- oder Registry-Eintrag;
- Composite-Tools liefern verfügbare Abschnitte und markieren einzelne
  nicht verfügbare Capabilities statt den ganzen Kontext zu verwerfen;
- Source-Tool auf Assembly und Assembly-Tool auf Solution liefern
  `unsupported`, keine leere Erfolgsliste.

### Tool- und Ressourcen-Matrix

| Gruppe | Beispiele | Verhalten im neuen Vertrag |
| --- | --- | --- |
| Gemeinsame Navigation | `get_file_tree`, `get_namespace_tree`, `find_symbol`, `find_references`, `get_call_tree`, `get_type_hierarchy`, `find_implementations`, `get_class_structure`, `get_symbol_body` | Solution- oder Assemblypfad; gemeinsamer Analyse-Envelope und bei Decompiled-Kontext sichtbare Diagnostics/Completeness |
| Gemeinsame Suche/Struktur | `search_pattern`, nicht regelabhängige `metrics_tree`-Modi, `dependency_graph` | Capability je Tool ausweisen; fehlender Decompiled-Support ist `unsupported`, keine leere Erfolgsliste |
| Assembly-spezifisch | `inspect_assembly`, `find_assembly_extensions`, `search_assembly`, `get_assembly_context` | Nur `.dll`/`.exe`; Resolver aktiviert automatisch den Decompiled-Kontext |
| Source-/Regelwerk | `get_violations`, `safeguard`, `pattern_detect`, regelabhängige `metrics_lookup`-/`metrics_tree`-Modi, `reload_config` | Nur Source; ohne Regeldatei `not_configured`, bei Assembly `unsupported` |
| Source-/Git-/Testsemantik | Git-Zweig von `get_impact`, `get_test_context`, Testabschnitte in Composites | Nur Source; Symbol-Impact kann im Assembly-Snapshot möglich sein, Git-Zweig dort `unsupported` |
| Ungebundene Verwaltung | `get_server_health`, `report_observability_feedback`, Agent-Guide | Globale Aufrufe ohne Target; optionaler Kontext heißt `targetPath` |
| Ressourcen | `ainetlinter://overview`, `ainetlinter://rules` | Konkreter URL-kodierter `targetPath`; Overview für beide Herkünfte, Rules bei Assembly `unsupported` |

Die endgültige Zuordnung wird gegen die tatsächlich registrierten Tools
geprüft. Eine alte `targetType`-Matrix aus der Dokumentation darf nicht als
Ersatzvertrag weitergeführt werden.

### Spezielle Composite-Fälle

- `get_feature_context` kann bei einer Assembly Navigation und Metriken
  liefern; Violations und Testzuordnung sind dort `unsupported`. Bei Source
  ohne Regeldatei ist nur der Violations-Abschnitt `not_configured`.
- `get_impact` kann Symbol-Impact im Assembly-Snapshot unterstützen; Git-Diff
  und Change-Context sind dort `unsupported`.
- `reload_config` lädt ausschließlich die feste Nachbardatei und besitzt
  keinen freien `configPath`-Override.
- `get_server_health` bleibt ohne Target global und wird mit `targetPath`
  zielgebunden detailliert.

## Assembly-Lifecycle-Grenze für diesen Task

Interne Snapshot-/Cacheartefakte dürfen weiterhin für Roslyn und Decompilation
entstehen, sind aber kein Agentenvertrag. Der Resolver dieses Tasks stellt
sicher, dass sie nicht als neue Projektdefinition erscheinen. Die vollständige
Qualität von Content-Hash, Generation, paralleler Erzeugung, Cleanup und
Referenzdiagnostics wird in Task 04 bearbeitet; hier werden nur die für den
öffentlichen Zielvertrag notwendigen bestehenden Pfade angeschlossen und
regressionsgesichert.

## Muss-Kriterien

- `.sln`/`.slnx` und `.dll`/`.exe` funktionieren ausschließlich über
  `targetPath`;
- `targetType`, `projectRoot`, `configPath` als Ersatz und
  `ainetlinter.project.json` sind aus aktivem MCP-Vertrag und Doku entfernt;
- Source-Navigation funktioniert ohne Regeldatei;
- ungültige Regeldateien erzeugen keinen Default-Regel-Fallback;
- Assemblys starten automatisch den Decompiled-Kontext und melden Linting als
  `unsupported`;
- Herkunft, Status, Completeness und Capabilities sind maschinenlesbar;
- Ressourcen, Dispatcher, Schema, Instructions, Tests und Dokumentation
  beschreiben denselben Vertrag;
- ein mit `targetType` gesendetes Argument wird als ungültig erkannt und nicht
  still ignoriert.

## Non-Goals

- keine automatische Migration alter Projektdefinitionen;
- kein Kompatibilitätsmodus für `targetType`;
- keine Suche nach beliebigen Regeldateien oder Default-Regeln;
- keine Behauptung, dass Decompiled-Code Originalquellcode ersetzt;
- keine Runtime-Ausführung oder dynamische Assemblyladung;
- keine neue Assembly-Parität für Git, Build, Tests oder Originalzeilen;
- keine Decompiler-Qualitätsneuentwicklung; diese gehört in Task 04.

## Risiken und Verifikation

Der harte Schnitt kann bestehende Integrationen brechen. Deshalb erfolgt vor
dem Release ein vollständiger Vertrags-Scan über produktiven MCP-Code,
MCP-Tests, `Docs/` und `.agents/`; aktive Vorkommen des alten Zielvertrags
müssen auf null sinken, ausgenommen klar markierte Historie und dieses
Konzept.

Zusätzlich werden Solutions mit und ohne Regeln, ungültige Regeln, Pfade mit
Leerzeichen/UNC-/Großschreibung, falsche Eingabetypen, DLL/EXE, fehlende
Assembly-Metadaten, Resource-URIs, Composite-Partial-Ergebnisse und Health-
Varianten geprüft.

Die Prüfung umfasst auch Projekte mit Referenzen außerhalb des Solution-
Verzeichnisses, zwei gleichnamige Solutions, nicht verwaltete DLL/EXE-Dateien,
falsch endende Dateien, Parent-/Child-Regeldateien, leere gültige Regeldateien,
fehlende PDBs und Referenz-Assemblies, obfuszierten oder getrimmten Code,
interne generierte `.cs`-/`.slnx`-Artefakte sowie normale Empty-vs-
Unsupported-Fälle. Windows-Endungen werden case-insensitive behandelt;
UNC-Pfade, Leerzeichen und stabile Pfadnormalisierung sind gültig.

Bei Assemblies gehören mindestens kanonischer Pfad, Content-Hash und
Decompiler-/Snapshot-Generation zur Sessionidentität. Gleicher Inhalt an
unterschiedlichen Orten erzwingt wegen relativer Referenzauflösung nicht
automatisch dieselbe Session. Änderungen während Hashing/Dekompilierung
erfordern einen stabilen Snapshot oder einen klaren Stale-/Retry-Fehler.
Fehlgeschlagene Snapshots geben Artefakte gemäß Diskbudget frei; Health macht
Origin, Generation und Cleanup-Zustand sichtbar.

Vor Abschluss gelten `dotnet build` und beide vollständigen Nicht-Stress-
Testläufe gemäß `AGENTS.md`. Die betroffenen MCP-Dokumente werden gegen den
implementierten Vertrag synchronisiert.

## Abnahme / Release-Gate

Der Task ist releasefähig, wenn der Agent nur noch eine konkrete Datei als
`targetPath` übergeben muss, Source- und Decompiled-Kontext korrekt
unterscheiden kann und kein alter Bootstrap- oder Zieltypvertrag in aktiven
Schemas, Ressourcen, Runtimepfaden oder Dokumentationen verbleibt.
