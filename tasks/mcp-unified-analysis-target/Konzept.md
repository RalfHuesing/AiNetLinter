---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: large
rules_dir: .agents/rules
last_updated: 2026-09-07
open_questions: []
depends_on: []
related_tasks: []
supersedes: null
---

# Konzept: Einheitliches MCP-Analyseziel für Solutions und Assemblies

## Status und Charakter

Dieses Dokument ist ein Diskussionsentwurf. Es beschreibt ein bewusst inkompatibles, vereinfachtes MCP-Zielmodell. Es ist noch keine Implementierungsfreigabe.

Die zentrale Produktentscheidung lautet:

> Für einen Agenten ist eine Quellcode-Solution und eine zu analysierende Assembly dasselbe: ein Analyseziel. Die technische Herkunft bleibt im Analysekontext transparent sichtbar.

## Ziel

Der MCP-Server soll ohne zusätzliche Projektmanifest-Datei auskommen. Ein Agent übergibt einen absoluten Pfad zu einer Solution oder Assembly. Der Server erkennt den Zieltyp selbst, erzeugt bei einer Assembly intern den notwendigen dekompilierten Roslyn-Kontext und stellt danach dieselbe grundsätzliche Navigationsoberfläche bereit.

Die Einrichtung eines Quellcodeprojekts soll auf eine klare Konvention reduziert werden:

```text
MyProject/
  MyProject.slnx
  ainetlinter-rules.json   # optional
```

Fehlt die Regeldatei, ist der Kontext trotzdem gültig und läuft im Navigations-/Analysemodus ohne aktive Lint-Regeln.

## Harte Schnitte

Die folgenden Altverträge werden nicht kompatibel weitergeführt:

- `targetType` entfällt aus allen öffentlichen MCP-Toolparametern.
- `ainetlinter.project.json` entfällt vollständig als Nutzer- und Serververtrag.
- Ein generiertes `<assembly-name>.project.json` wird nicht erzeugt.
- Die bisherige Pflicht, `solution` und `rules` über eine Projektdefinition zu liefern, entfällt.
- Alte Projektinitialisierungs- und Fehlerpfade wie `PROJECT_NOT_INITIALIZED` sind im neuen Modell nicht mehr maßgeblich.
- Der Zieltyp wird ausschließlich aus dem übergebenen Pfad und der Dateiendung erkannt.

Der Server darf intern Dateien für Roslyn und die Dekompilierung erzeugen. Diese Dateien sind Implementierungsdetails und kein weiterer Agentenvertrag.

## Neuer Eingabevertrag

Der öffentliche Zielparameter bleibt `targetPath`, erhält aber ausschließlich einen absoluten lokalen Pfad.

Unterstützte direkte Eingaben:

- `.slnx` oder `.sln`: Quellcode-Solution
- `.dll` oder `.exe`: Assembly, die im Decompiled-Modus analysiert wird

Ein gültiger `targetPath` muss auf eine vorhandene Datei mit einer unterstützten Endung zeigen. Verzeichnisse sind kein Zielvertrag; der Server durchsucht sie nicht nach einer passenden Solution.

Die Auflösung erfolgt ausschließlich anhand des vorhandenen Pfads und seiner Endung. Bei unbekannter Endung, fehlender Datei oder relativem Pfad liefert der Server einen deterministischen Invalid-Argument-Fehler.

`targetPath` bezeichnet immer die konkrete Eingabedatei, nicht den Projektroot:

```text
targetPath = C:\\repo\\MyProject.slnx
targetPath = C:\\libs\\ThirdParty.dll
```

Damit muss ein Agent nicht aus einem Verzeichnisinhalt erraten, welche Solution gemeint ist. Die kanonische Identität des Source-Kontexts basiert auf der aufgelösten Solution-Datei; die kanonische Identität des Decompiled-Kontexts basiert auf der Assemblydatei und ihrem Inhalt.

## Agentenvertrag

Der Agent soll für jede zielgebundene MCP-Abfrage denselben einfachen Vertrag verwenden:

```json
{
  "targetPath": "C:\\repo\\MyProject.slnx"
}
```

oder:

```json
{
  "targetPath": "C:\\libs\\ThirdParty.dll"
}
```

Die Antwort jedes zielgebundenen Tools muss, direkt oder über den gemeinsamen Analyse-Envelope, ausreichend Kontext liefern:

```json
{
  "analysis": {
    "inputPath": "C:\\libs\\ThirdParty.dll",
    "origin": "decompiled",
    "rulesConfigured": false,
    "lintStatus": "unsupported",
    "analysisMode": "navigation",
    "completeness": "partial",
    "capabilities": {
      "symbolNavigation": true,
      "lintViolations": false,
      "gitImpact": false
    }
  }
}
```

Der Agent darf nicht aus einer leeren Ergebnisliste ableiten müssen, ob die Analyse vollständig war, die Capability fehlte oder schlicht kein Treffer existierte. `origin`, `completeness`, `rulesConfigured` und relevante Capabilities sind deshalb Vertragsbestandteile und keine reine Formatierungsfrage.

Die öffentlichen Toolschemas enthalten keine parallelen Projekt-/Assembly-Parameter. Unterschiede werden ausschließlich im normalisierten Analysekontext und in der jeweiligen Capability-Antwort dargestellt.

### Status- und Capability-Vertrag

Die Herkunft des Kontexts und der Status der Lint-Funktionalität sind getrennte Dimensionen:

| Feld | Zulässige Werte | Bedeutung |
|---|---|---|
| `origin` | `source`, `decompiled` | Herkunft des analysierten Kontexts |
| `analysisMode` | `navigation`, `navigation_and_lint` | `navigation_and_lint` nur bei Source plus gültiger Regeldatei |
| `lintStatus` | `active`, `not_configured`, `unsupported` | `active` nur bei Source plus gültiger Regeldatei; `not_configured` bei Source ohne Regeldatei; `unsupported` bei Assemblies |
| `completeness` | `complete`, `partial`, `failed` | Vollständigkeit des Kontextes bzw. Ergebnisses |
| Operationstatus | `ok`, `unsupported`, `not_configured`, `invalid_argument`, `failed` | Status des konkreten Toolaufrufs, wenn `ok` nicht zutrifft |

`rulesConfigured=false` allein reicht für den Agenten nicht aus: Bei einer Assembly ist Linting strukturell nicht verfügbar, bei einer Source-Solution ohne Regeldatei nur noch nicht konfiguriert. Die strukturierte Antwort muss diese Fälle unterscheiden.

Ein Composite-Tool darf wegen einer nicht verfügbaren Teilfähigkeit nicht den gesamten Analysekontext als fehlgeschlagen ausgeben. Es liefert die verfügbaren Abschnitte und markiert den einzelnen Abschnitt mit `status` und `reasonCode`, zum Beispiel `tests: unsupported` oder `violations: not_configured`.

Ein unbekanntes oder explizit verbotenerweise mitgesendetes `targetType` gehört nicht zum neuen Schema. Der harte Schnitt wird als Schema- und Semantikbruch umgesetzt: Es gibt keinen Resolverpfad, der den Wert auswertet, und ein strikter Argumentvalidator soll den unbekannten Parameter als ungültige Eingabe melden statt ihn stillschweigend als Kompatibilitätsalias zu akzeptieren.

## Regelkonfiguration

### Kanonischer Name

Die Regeldatei erhält den AiNetLinter-spezifischen, transparenten Namen:

```text
ainetlinter-rules.json
```

Der Name ist bewusst nicht nur `rules.json`, weil `rules` in fremden Projekten zu unspezifisch ist und die Herkunft der Datei für Agenten und Menschen nicht erkennbar macht.

Der Dateiname wird im Code genau einmal als zentrale Konstante definiert, beispielsweise `AiNetLinterFileNames.RulesFileName`, und von Resolver, Tests und allen internen Dateinamensprüfungen wiederverwendet. Der Literalwert `ainetlinter-rules.json` darf nicht an vielen Stellen unabhängig als String wiederholt werden.

### Source-Modus

Bei Übergabe einer `.sln` oder `.slnx` sucht der Server ausschließlich im Verzeichnis der Solution nach der kanonischen Regeldatei.

- Datei vorhanden: Lint-Regeln sind konfiguriert.
- Datei nicht vorhanden: gültiger Navigations-/Analysekontext ohne Lint-Regeln.
- Datei vorhanden, aber ungültig: harter Konfigurationsfehler; es werden keine Default-Regeln geladen.

Es gibt keine Suche im aktuellen Arbeitsverzeichnis, in Elternverzeichnissen oder in zufälligen Unterverzeichnissen. Es gibt kein stilles Fallback auf Default-Regeln.

### Fehlende Regeln sind kein grüner Lint-Lauf

Ein fehlendes Regelwerk darf nicht als „null Verstöße“ erscheinen. Der Kontext muss maschinenlesbar ausweisen:

```text
rulesConfigured: false
analysisMode: navigation
```

Navigation, Symbolgraph, Referenzen, Call Trees, Bodies und rohe Metriken können weiterhin verfügbar sein. Schwellenwert- und regelabhängige Funktionen melden dagegen explizit `not_configured` oder eine gleichwertige maschinenlesbare Capability-Antwort.

Insbesondere darf `get_violations` bei fehlender Konfiguration nicht erfolgreich eine leere Violation-Liste als Qualitätsaussage zurückgeben.

### Decompiled-Modus

Eine Assembly hat zunächst kein automatisch zugeordnetes Regelwerk. Die Assembly wird als Analyse-/Navigationsziel behandelt.

Assemblies sind keine Lint-Ziele. Linting ist für Assembly-Kontexte deaktiviert und nicht verfügbar. Das ist eine fachliche Grenze: Regelverstöße gegen dekompilierten Code würden leicht als Aussagen über den ursprünglichen Quellcode missverstanden. Verfügbar sind Navigation, Struktur, Referenzen, Bodies, Assembly-Metadaten und rohe Metriken, soweit der Snapshot sie belastbar liefert.

Eine Assembly-Antwort muss deshalb zwischen „keine Regeln konfiguriert“ bei einem Source-Kontext und „Linting für diesen Zieltyp nicht verfügbar“ bei einem Assembly-Kontext unterscheiden. Dafür ist ein strukturierter Status wie `lintStatus: unsupported` erforderlich.

## Einheitliches internes Modell

Der externe Pfad wird in einen einheitlichen Analysekontext materialisiert:

```text
TargetPath
    ↓
TargetResolver
    ↓
ResolvedAnalysisContext
```

Der Kontext muss mindestens folgende Informationen tragen:

```text
InputPath
Origin: source | decompiled
SolutionPath
SourceRoot
RulesPath?
RulesConfigured
ContentIdentity?
Completeness
Capabilities
OriginalAssemblyPath?
```

`Origin` und `Completeness` sind keine optionalen UI-Details. Sie verhindern, dass ein Agent dekompilierten Code mit Originalquellcode oder partiellen Ergebnissen mit vollständigen Ergebnissen verwechselt.

Die gemeinsame Abstraktion bedeutet nicht, dass alle Fähigkeiten identisch sind:

| Fähigkeit | Source-Solution | Decompiled Assembly |
|---|---:|---:|
| Symbolsuche | ja | ja |
| Referenzsuche | ja | ja, ggf. partiell |
| Call Tree | ja | ja, ggf. partiell |
| Methodenrumpf | ja | ja, soweit dekompiliert |
| Git-Diff/Change-Impact | ja | nein |
| Build/Test | ja | nein |
| Originalzeilen/Source-Link | ja | nein |
| Assembly-Metadaten | indirekt | ja |
| Lint-Verstöße | mit Regeldatei | nein |

Die Toolschemas sollen nicht wieder zwei getrennte Benutzerpfade einführen. Unterschiede werden über `capabilities`, `origin`, `completeness` und klare Fehler-/Statuswerte ausgedrückt.

## Decompiled-Modus ohne Projektartefakt

Bei Übergabe einer DLL arbeitet der Server direkt im Decompiled-Modus:

```text
DLL
  ↓
metadata-only inspection / decompilation
  ↓
interner Roslyn-Solution-Kontext
  ↓
einheitliche Analysewerkzeuge
```

Es wird weder `ainetlinter.project.json` noch `<assembly-name>.project.json` erzeugt.

Falls eine physische `.slnx` für Roslyn erforderlich ist, liegt sie ausschließlich in einem internen, verwalteten Snapshot-/Cache-Verzeichnis. Sie ist kein vom Agenten zu übergebender oder zu editierender Vertrag.

Der Cache darf nicht nur auf dem Assemblynamen basieren. Die Identität muss mindestens den kanonischen Pfad und den Inhalt der Assembly berücksichtigen, vorzugsweise über einen Content-Hash. Dadurch werden gleichnamige Assemblies, Versionswechsel und veraltete Dekompilationen sauber getrennt.

Der Analysekontext muss für Assemblies transparent ausweisen:

- originale absolute Assemblyquelle
- Content-Identität oder Generation
- internen Decompiled-Status
- generierten Source-/Solution-Root, sofern für Diagnosezwecke erforderlich
- fehlende oder partielle Referenzen
- Ressourcen- und Lebensdauerstatus

## Bestehende Architektur und betroffene Bereiche

Der aktuelle Stand trennt Source- und Assemblyziele noch sichtbar:

- `src/AiNetLinter/Mcp/AnalysisTarget.cs` modelliert `Project` und `Assembly` als explizite Zieltypen.
- `src/AiNetLinter/Mcp/AnalysisTargetResolver.cs` verlangt aktuell `targetType` und einen absoluten `targetPath`.
- `src/AiNetLinter/Mcp/Projects/ProjectDefinitionLoader.cs` lädt aktuell zwingend `ainetlinter.project.json` mit `solution` und `rules`.
- `src/AiNetLinter/Mcp/Projects/ProjectInstanceFactory.cs` materialisiert Regeln und unterscheidet derzeit zwischen Default-/Batch-Semantik und strengem Registry-Pfad.
- Assembly-Registry, Decompiler und Snapshot-Lifecycle existieren bereits als eigener technischer Pfad.
- Die bisherige Projektdefinitionskette (`ProjectDefinition`, `ProjectDefinitionLoader`, projektdefinitionsbezogene Fehler- und Factory-Pfade) ist nach dem Schnitt kein versteckter MCP-Einstiegspunkt mehr; obsolete Teile werden entfernt oder auf den neuen direkten Solution-/Regelpfad umgebaut.
- `Docs/agent-api.md`, `Docs/integration.md` und gegebenenfalls `Docs/configuration.md` dokumentieren noch den alten Vertrag.
- `.agents/rules/AiNetLinter-McpWorkflow.mdc` beschreibt derzeit `targetType`; diese Regeln müssen nach der fachlichen Freigabe synchronisiert werden.

Die Vereinfachung soll den öffentlichen Vertrag zentralisieren. Sie verlangt nicht zwingend, Assembly-Registry und Source-Registry intern physisch zusammenzulegen. Gemeinsamer Analysekontext und gemeinsamer Resolver sind wichtiger als identische Speicherverwaltung.

## Refactor-Grenze: vollständiger MCP-Schnitt statt lokaler Reparatur

Die Umstellung muss bewusst über alle Schichten des MCP-Vertrags erfolgen. Ein Austausch nur von `AnalysisTargetResolver` oder einzelner Toolmethoden wäre nicht sinnvoll, weil der aktuelle Zielvertrag an mehreren unabhängigen Stellen modelliert und validiert wird.

### Verbindlich umzubauende Schichten

| Schicht | Aktueller Anker | Erforderliche Änderung |
|---|---|---|
| Öffentlicher MCP-Vertrag | Toolregistrierungen, Parameterrecords, `tools/list`, `ServerInstructions` | Nur `targetPath`; kein `targetType`, kein `projectRoot` als Zielalias, keine Projektdefinitionsparameter |
| Zielauflösung | `AnalysisTarget`, `AnalysisTargetRequest`, `AnalysisTargetResolver` | Direkte Dateiauflösung nach absolutem Pfad und Endung; Ergebnis ist ein normalisierter Analysekontext mit `origin` und Capabilities |
| Dispatch | `AnalysisToolCall`, Projekt-/Assembly-Routen, Toolregistrierungs-Lambdas | Routing nach dem aufgelösten Kontext statt nach einem vom Agenten gelieferten Zieltyp |
| Source-Session | `ProjectRegistry`, `ProjectToolCall`, `ProjectLease`, `ProjectEntry` | Registry-Key auf konkrete Solution und wirksame Regelidentität umstellen; `projectRoot` bleibt höchstens internes, abgeleitetes SourceRoot-Feld |
| Projektdefinition | `ProjectDefinition`, `ProjectDefinitionLoader`, `ProjectDefinitionLoadResult`, projektbezogene Fehlercodes | Aus dem MCP-Ladepfad entfernen; Solution direkt laden, benachbarte Regeldatei optional erkennen |
| Regelmaterialisierung | `ProjectInstanceFactory`, `reload_config` | Optionale feste Nachbardatei, zentrale Dateinamenskonstante, kein freier Config-Override, klare `active`/`not_configured`-Semantik |
| Assembly-Session | Assembly-Registry, Decompiler, Snapshot-/Resource-Lifecycle | In denselben Analyse-Envelope einordnen, aber eigene Hash-, Referenz-, Ressourcen- und Cleanup-Semantik behalten |
| Antwortprojektion | Assembly-Response, Project-/Daemon-Health, Composite-Tools | `targetType` und alte Zielbegriffe entfernen; `origin`, `lintStatus`, `operationStatus`, `completeness` und Abschnittsstatus standardisieren |
| Ressourcen | Overview-/Rules-Registrierung, Resource-Lease und Formatter | `targetPath` statt `projectRoot`; zielgebundene URIs benötigen eine konkrete Datei; Assembly-Regelresource ist `unsupported` |
| Tests | Resolver-, Wiring-, Registry-, Resource-, Daemon-, Assembly- und MCP-E2E-Tests | Alte Verträge löschen/ersetzen; keine Paralleltests, die den Altvertrag weiterhin als gültig festschreiben |
| Dokumentation/Agentenregeln | `Docs/*`, `.agents/rules/*`, Bootstrap-/Servertexte | Ein einziger neuer Vertrag; veraltete Beispiele, Fehlercodes und Setup-Anweisungen vollständig entfernen |

### Bewusste interne Grenze

Der Refactor vereinheitlicht die Auflösung, das Analysemodell und die Agentenantworten. Er erzwingt nicht, dass Source- und Assembly-Sessions denselben Registry-Code verwenden. Die Assembly-Registry darf wegen Decompiler-Snapshots, Referenzexpansion und externen Ressourcenlimits separat bleiben, solange sie denselben `ResolvedAnalysisContext`- und Antwortvertrag erfüllt.

Eine gemeinsame Registry wäre nur dann sinnvoll, wenn sie ohne künstliche Sonderfälle dieselben Lease-, Eviction- und Fehlersemantiken tragen kann. Das ist kein Ziel dieses Konzepts und darf nicht als Nebenprodukt erzwungen werden.

### Scope-Grenze zur Batch-CLI

Die neue Konvention `ainetlinter-rules.json` betrifft die automatische MCP-Auflösung neben einer übergebenen Solution. Der Batch-CLI-Vertrag mit explizitem `--config` bleibt davon getrennt; ein expliziter CLI-Regelpfad darf weiterhin frei benannt sein. Dadurch wird der MCP-Schnitt nicht unnötig zu einer globalen CLI-Migrationsaufgabe erweitert.

Die MCP-Implementierung darf die alte Projektdefinitionslogik trotzdem nicht als versteckten Fallback behalten. Für MCP gibt es nach dem Schnitt genau einen Source-Einstieg: konkrete Solution-Datei plus optionale benachbarte `ainetlinter-rules.json`.

### Refactor-Abschlusskriterium

Der Refactor ist erst abgeschlossen, wenn eine Suche über produktiven MCP-Code, MCP-Tests, MCP-Dokumentation und Agentenregeln keinen aktiven Vertragsrest von `targetType`, `projectRoot` als Zielparameter oder `ainetlinter.project.json` mehr findet. Historische Changelog-/Roadmap-Hinweise dürfen den entfernten Vertrag als Historie erwähnen, aber nicht als nutzbaren Pfad beschreiben.

### Konkrete Umsetzungsleitplanken

- Es gibt genau einen öffentlichen `targetPath`-Resolver. Die MCP-Tool-Dispatcher erhalten keinen vom Agenten gelieferten Zieltyp mehr.
- Der Resolver entscheidet zunächst anhand von Existenz und Endung, danach materialisiert der passende Source- oder Decompiled-Provider einen gemeinsamen Analysekontext.
- Für eine `.sln`/`.slnx` wird ausschließlich die benachbarte `ainetlinter-rules.json` betrachtet.
- Für eine DLL wird kein Regelwerk automatisch entdeckt, Linting wird deaktiviert und kein user-facing Projektmanifest erzeugt.
- Der kanonische Regeldateiname wird über genau eine zentrale Konstante bereitgestellt; Resolver und Tests verwenden diese Konstante statt eigener String-Literale.
- `projectRoot` darf nicht als versteckter Parallelvertrag fortbestehen. Ressourcen und Tools müssen auf den neuen konkreten `targetPath`-Vertrag umgestellt werden.
- Session- und Registry-Schlüssel müssen den normalisierten Kontext identifizieren. Bei Assemblies gehören mindestens Assemblypfad und Inhaltsidentität dazu; bei Source-Kontexten mindestens Solutionpfad und die wirksame Regelkonfiguration.
- Der Resolver darf nicht durch Fallbacks, Parent-Suche oder Dateinamensraten eine andere Solution oder Regeldatei auswählen.
- Die Antwortprojektion darf Capability-Information nicht nur als Freitext liefern. Für Agenten relevante Zustände benötigen strukturierte Felder und stabile Statuswerte.

### Tool- und Ressourcen-Matrix

Der harte Schnitt entfernt nicht zwingend spezialisierte Tools. Er entfernt den vom Agenten zu liefernden Zieltyp. Ein assembly-spezifisches Tool darf weiterhin existieren, erhält aber nur `targetPath` und validiert anhand der Endung selbst, ob es auf eine Assembly angewendet werden kann.

| Gruppe | Beispiele | Verhalten im neuen Vertrag |
|---|---|---|
| Gemeinsame Navigation | `get_file_tree`, `get_namespace_tree`, `find_symbol`, `find_references`, `get_call_tree`, `get_type_hierarchy`, `find_implementations`, `get_class_structure`, `get_symbol_body` | `targetPath` ist ein Solution- oder Assemblypfad; Ergebnis enthält den gemeinsamen Analyse-Envelope und ggf. partielle Decompiled-Diagnostics. |
| Gemeinsame Suche/Struktur | `search_pattern`, `metrics_tree` mit nicht regelabhängigen Modi, `dependency_graph` und weitere statische Strukturabfragen, soweit der jeweilige Scanner einen Decompiled-Snapshot unterstützt | Die Capability wird pro Tool explizit ausgewiesen. Ein fehlender Decompiled-Support liefert `unsupported`, keinen leeren Erfolg. |
| Assembly-spezifische Analyse | `inspect_assembly`, `find_assembly_extensions`, `search_assembly`, `get_assembly_context` | `targetPath` muss eine `.dll`/`.exe` sein; kein `targetType`; der Resolver aktiviert automatisch den Decompiled-/Assemblykontext. |
| Source-/Regelwerk | `get_violations`, `safeguard`, `pattern_detect`, regelabhängige `metrics_lookup`- und `metrics_tree`-Modi, `reload_config` | Nur Source-Kontext. Ohne `ainetlinter-rules.json`: `not_configured`; bei Assembly: `unsupported`. |
| Source-/Git-/Testsemantik | Git-Zweig von `get_impact`, `get_test_context`, Testabschnitte in Composite-Tools | Nur Source-Kontext. Der Symbol-Zweig von `get_impact` kann für Assemblies als Navigation unterstützt werden; der Git-Zweig ist dort `unsupported`. |
| Ungebundene Verwaltung | `get_server_health`, `report_observability_feedback`, Agent-Guide | Kein Target für globale Aufrufe. Wenn ein Kontext angegeben wird, heißt der optionale Parameter ebenfalls `targetPath`; `projectRoot` entfällt. |
| Ressourcen | `ainetlinter://overview`, `ainetlinter://rules` | URI-Query heißt `targetPath` und enthält einen URL-kodierten konkreten absoluten Pfad. Beide zielgebundenen Ressourcen benötigen `targetPath`; Overview funktioniert für Source und Assembly, Rules liefert bei Assembly `unsupported` und bei Source ohne Regeldatei `not_configured`. |

Die endgültige Zuordnung wird im Implementierungspaket gegen die tatsächlich registrierten Tools geprüft. Sie darf nicht durch eine veraltete `targetType`-Matrix aus der bestehenden Dokumentation ersetzt werden.

### Spezielle Composite-Fälle

- `get_feature_context` kann bei einer Assembly Navigation und Metriken liefern, markiert aber Violations und Testzuordnung als `unsupported`, ohne den gesamten Kontext zu verwerfen. Bei einer Source-Solution ohne Regeldatei ist nur der Violations-Abschnitt `not_configured`.
- `get_impact` unterscheidet anhand der angeforderten Operation: Symbol-Impact kann im Assembly-Snapshot möglich sein; Git-Diff und Change-Context sind `unsupported`.
- `reload_config` besitzt keinen freien `configPath`-Override mehr. Es lädt ausschließlich die feste benachbarte `ainetlinter-rules.json`; fehlt diese, wechselt der Source-Kontext zu `not_configured`. Auf Assemblies ist das Tool `unsupported`.
- `get_server_health` ohne `targetPath` bleibt ein globaler Aggregat-Aufruf. Mit `targetPath` wird der Typ automatisch erkannt und die entsprechende Source- oder Assembly-Session detailliert ausgegeben.
- `report_observability_feedback` erhält keinen `projectRoot`-Parameter. Ein optionaler `targetPath` ist nur Kontext und darf keinen Analyse- oder Registry-Eintrag implizit erzeugen.

## Muss-Kriterien

- Der Agent kann eine `.sln`/`.slnx` oder `.dll`/`.exe` ausschließlich über `targetPath` adressieren.
- `targetPath` adressiert die konkrete Datei, nicht ein Verzeichnis oder einen implizit ermittelten Projektroot.
- `targetType` ist aus MCP-Schemas, Toolargumenten, Ressourcenverträgen und relevanter Dokumentation entfernt.
- `ainetlinter.project.json` wird nicht mehr gelesen, erzeugt oder als Setup-Voraussetzung beschrieben.
- Eine Source-Solution funktioniert ohne Regeldatei im Navigationsmodus.
- Der kanonische Regeldateiname ist transparent und ausschließlich relativ zum Solution-Verzeichnis auflösbar.
- Ungültige vorhandene Regeldateien führen zu einem klaren Konfigurationsfehler und nicht zu Default-Regeln.
- Eine DLL aktiviert automatisch den Decompiled-Modus.
- Assembly-Kontexte unterstützen grundsätzlich keine regulären Lint-Verstöße.
- Der Decompiled-Modus erzeugt keine user-facing Projektdefinition.
- Source- und Decompiled-Kontexte verwenden denselben öffentlichen Ziel- und Analysevertrag.
- Herkunft, Vollständigkeit, Content-Identität und Capabilities bleiben maschinenlesbar.
- Assembly-Snapshots werden nicht allein über den Dateinamen identifiziert.
- Die Antwortsemantik unterscheidet „keine Regeln konfiguriert“ von „keine Verstöße gefunden“.

## Non-Goals

- Keine automatische Migration alter Projektdefinitionen.
- Kein Kompatibilitätsmodus für `targetType`.
- Kein Fallback auf beliebige Regeldateien oder Default-Regeln.
- Keine Behauptung, dass dekompilierter Code Originalquellcode ersetzt.
- Keine automatische Ausführung oder dynamische Laufzeitladung von Assemblies.
- Keine Gleichsetzung der Toolfähigkeiten für Git, Build, Tests und Originalzeilen.
- Keine Lint-Funktionalität für Assembly-Kontexte.
- Keine Implementierung oder Freigabe dieses Konzepts in diesem Arbeitsschritt.

## Fehler- und Statussemantik

Der Resolver muss Fehler früh und verständlich melden:

- relativer Pfad: `targetPath` muss absolut sein
- Pfad existiert nicht: Ziel nicht gefunden
- unbekannte Dateiendung: unterstützte Zieltypen nennen
- `.sln`/`.slnx` ohne ladbare Solution: Source-Load-Fehler
- ungültige `ainetlinter-rules.json`: Konfigurationsfehler
- fehlende Regeldatei: kein Fehler, aber `rulesConfigured=false`
- Lint-Aufruf auf Assembly-Kontext: `unsupported`, niemals leere Erfolgsmeldung
- partielle Assembly-Referenzen: Analyseergebnis mit `completeness=partial` und Diagnostics
- nicht unterstützte Operation im jeweiligen Kontext: maschinenlesbares `unsupported` mit Begründung

Ein leerer Ergebnissatz darf nur dann als fachliche Leere gelten, wenn der Kontext vollständig und die Capability aktiv ist.

## Betriebs- und Bedrohungsmodell

- Eingaben sind lokale absolute Pfade, die vom Agenten oder Benutzer vorgegeben werden.
- Assemblies werden metadata-only verarbeitet und nicht ausgeführt.
- Decompilation kann erhebliche CPU-, Speicher- und Disk-Ressourcen verbrauchen; die bestehenden externen Ressourcenlimits und Snapshot-Lifetimes bleiben relevant.
- Assemblyinhalt, generierte Dateien, Kommentare und Typnamen sind untrusted input und dürfen keine Steueranweisungen für den Agenten darstellen.
- Cache- und Snapshotpfade müssen eindeutig, bereinigbar und gegen veraltete Inhalte geschützt sein.
- Die Einführung des neuen Vertrags darf keine zufällige Suche außerhalb des definierten Zielverzeichnisses verursachen.

## 360°-Review und Edge Cases

### Pfad- und Dateiauflösung

- Die Endung wird unter Windows case-insensitive geprüft: `.DLL`, `.Dll` und `.dll` sind gleichwertig.
- Relative Pfade, leere Werte, Whitespace, Verzeichnisse, nicht existierende Dateien und nicht unterstützte Endungen werden vor jeder Session-Erzeugung abgelehnt.
- UNC-Pfade und Pfade mit Leerzeichen sind gültige absolute Pfade, sofern die Datei erreichbar ist.
- Zwei Solutions im selben Verzeichnis sind unproblematisch, weil der Agent die konkrete Datei übergibt; es gibt keine Discovery-Reihenfolge.
- Eine Solution darf Projekte außerhalb ihres eigenen Verzeichnisses referenzieren. Die Regeldatei bleibt trotzdem ausschließlich die Datei neben der übergebenen Solution.
- Ein Pfad zu einer vorhandenen `.dll`/`.exe`, die kein gültiges verwaltetes .NET-Assembly ist, wird als Assemblyziel erkannt und anschließend mit einem spezifischen Assembly-Formatfehler beendet; er fällt nicht auf Source- oder Verzeichnisanalyse zurück.
- Ein vorhandener Pfad mit falscher Endung wird nicht anhand seines Inhalts umgedeutet. Der Vertrag bleibt endungsbasiert und deterministisch.
- Die Pfadnormalisierung muss für Windows-Vergleiche stabil sein. Session-Schlüssel dürfen nicht durch triviale Schreibweisen oder Groß-/Kleinschreibung doppelte Source-Sessions erzeugen.

### Regeldatei und Lintstatus

- Die Regeldatei wird ausschließlich als `<solution-directory>\\ainetlinter-rules.json` aufgelöst.
- Eine gleichnamige Regeldatei in einem Parent-, Child- oder Arbeitsverzeichnis wird ignoriert.
- Eine fehlende Datei ist bei Source kein Fehler. Eine vorhandene, nicht lesbare oder ungültige Datei ist ein harter Konfigurationsfehler.
- Eine leere, aber syntaktisch gültige Regeldatei wird nach der normalen Config-Semantik bewertet; sie darf nicht mit „Datei fehlt“ verwechselt werden.
- Es werden bei fehlender oder ungültiger Konfiguration keine Default-Regeln heimlich aktiviert.
- `reload_config` darf keine beliebige externe Datei einschleusen. Die feste Nachbardatei ist die einzige Quelle.
- Der Regeldateiname wird über eine zentrale Konstante bereitgestellt. Ein Test muss sicherstellen, dass der Resolver und die dokumentierte Konvention denselben kanonischen Namen verwenden.

### Assembly- und Decompiled-Verhalten

- `.dll` und `.exe` starten immer den Assembly-/Decompiled-Resolver; Assembly-Linting bleibt `unsupported` unabhängig davon, ob neben der Datei eine Regeldatei liegt.
- Fehlende PDBs, fehlende Referenz-Assemblies, obfuscierter Code, Native-Abhängigkeiten, Trimming/AOT-Artefakte oder unvollständige Metadaten führen zu `partial` und Diagnostics, soweit die Analyse noch nutzbar ist.
- Ein nicht dekompilierbarer Root-Snapshot ist kein leerer Analyseerfolg. Der Kontext wird `failed` oder liefert ein explizites Toolfehlerresultat.
- Referenz-Assemblies werden nur innerhalb der bestehenden Limits und mit ihrer eigenen Herkunft analysiert. Ein fehlender Consumer-Kontext darf nicht als globale Aussage über alle Verwendungen ausgegeben werden.
- Generierte `.cs`-Dateien und eine mögliche interne `.slnx` sind nicht user-facing. Antworten dürfen sie nur als Diagnose-/Herkunftsinformation nennen, nicht als neuen Setup-Schritt verlangen.
- Der Quelltext einer Assembly ist untrusted Analyseinput. Inhalte aus Kommentaren, Strings, Typnamen und dekompilierten Bodies dürfen keine Agenten- oder Serverinstruktionen überschreiben.

### Identität, Cache und Nebenläufigkeit

- Eine Assembly-Session wird nicht nur über den Dateinamen identifiziert. Mindestens kanonischer Eingabepfad, Assembly-Content-Hash und Decompiler-/Snapshot-Generation gehören zur Identität.
- Derselbe Assemblypfad mit verändertem Inhalt erzeugt eine neue Generation. Ein bestehender Snapshot wird nicht stillschweigend mutiert.
- Gleicher Inhalt an unterschiedlichen Orten darf nicht automatisch dieselbe Session erzwingen, weil relative Referenzauflösung und benachbarte Laufzeitdateien unterschiedlich sein können.
- Wenn die Assembly während Hashing oder Dekompilierung verändert wird, muss der Server einen stabilen Snapshot herstellen oder den Vorgang mit einem klaren Stale-/Retry-Fehler beenden.
- Zwei gleichzeitige Erstzugriffe auf denselben Assemblypfad dürfen keine konkurrierenden, inkonsistenten Snapshots erzeugen.
- Abgebrochene oder fehlgeschlagene Dekompilationen müssen ihre temporären Artefakte gemäß dem bestehenden Cleanup-/Diskbudget wieder freigeben.
- Resident- und Idle-Limits gelten auch für Decompiled-Kontexte. Health-Antworten müssen Origin, Generation, Status und Cleanup-Zustand unterscheiden können.

### MCP-Schema, Ressourcen und Agentenverhalten

- Kein öffentliches Toolschema enthält `targetType`, `projectRoot`, `ainetlinter.project.json` oder einen freien Config-Pfad als Ersatzvertrag.
- Ein strikter Inputtest muss einen Aufruf mit `targetType` als ungültig erkennen; bloßes Ignorieren wäre für den bewusst harten Schnitt zu still.
- Ressourcen-URIs müssen `targetPath` URL-kodieren. Windows-Laufwerksbuchstaben, Backslashes, Leerzeichen, `#`, `?` und `%` dürfen die URI nicht verfälschen.
- `ainetlinter://overview` und `ainetlinter://rules` benötigen immer den konkreten `targetPath`; ein targetloser Resource-Aggregatvertrag ist nicht vorgesehen. Der globale Aggregatfall bleibt ausschließlich `get_server_health` vorbehalten.
- `ainetlinter://rules` darf bei Assembly nicht so aussehen, als gäbe es ein fehlendes Regelwerk, sondern muss `unsupported` ausweisen.
- Ein Source-Tool auf einer DLL und ein Assembly-Tool auf einer Solution liefern spezifische `unsupported`-Fehler; sie fallen nicht in eine leere Ergebnisliste.
- Composite-Tools liefern verfügbare Abschnitte trotz einzelner nicht verfügbarer Abschnitte und kennzeichnen jeden Abschnitt separat.
- Absolute Pfade in Payloads sind konsistent normalisiert; relative Trefferpfade bleiben relativ zum jeweils ausgewiesenen SourceRoot.

### Semantische Grenzen

- „Keine Regeln konfiguriert“ ist nur bei Source ein sinnvoller Zustand; bei Assemblies ist die Lint-Capability strukturell nicht verfügbar.
- „Keine Treffer“ ist erst dann eine fachliche Aussage, wenn `operationStatus=ok`, die Capability aktiv und `completeness` ausreichend ist.
- Decompiled-Zeilen sind Snapshot-Zeilen, keine Originalzeilen. Toolbeschreibungen und Payloads müssen diese Herkunft sichtbar halten.
- Fehlende Caller, Referenzen oder Tests sind bei einem partiellen Snapshot keine globale Negativaussage.
- Git- und Build-/Test-Aussagen dürfen bei Assemblies nicht aus dem generierten Cacheprojekt abgeleitet werden.

### Review-Verdikt

Die Vereinfachung ist tragfähig, wenn „ein Analyseziel“ als gemeinsamer Agentenvertrag verstanden wird und nicht als Behauptung identischer Fähigkeiten. Der gefährlichste Fehler wäre, `targetType` zwar zu entfernen, aber alte Projektroot-, Regel- oder Assemblyannahmen in Ressourcen, Composite-Tools und Fehlerwerten weiterzuführen. Der Konzeptumfang deckt deshalb bewusst auch diese scheinbar sekundären Verträge ab.

## Verifikation

Nach der späteren Implementierungsfreigabe sind mindestens folgende Nachweise erforderlich:

### Resolver und Konfiguration

- `.sln` und `.slnx` mit vorhandener `ainetlinter-rules.json`
- `.sln` und `.slnx` ohne Regeldatei
- ungültige Regeldatei
- DLL und EXE als Decompiled-Eingabe
- unbekannte Endung, relativer Pfad und fehlende Datei
- Groß-/Kleinschreibung der Endung, Leerzeichen im Pfad, UNC-Pfad und Verzeichnis als ungültiges Ziel
- gültige Solution mit Projektreferenzen außerhalb des Solution-Verzeichnisses
- vorhandene DLL/EXE ohne gültige verwaltete Assembly-Metadaten
- keine Akzeptanz von `targetType`
- keine Akzeptanz oder Erzeugung von `ainetlinter.project.json`
- keine Suche nach Regeldateien außerhalb des Solution-Verzeichnisses
- zentrale Regeldateikonstante wird von Resolver und Tests gemeinsam verwendet

### Assembly-Lifecycle

- gleichnamige Assemblies aus verschiedenen Verzeichnissen
- gleicher Assemblyname mit verändertem Inhalt
- Assemblyänderung während Hashing/Dekompilierung
- parallele Erstöffnung desselben Assemblyziels
- fehlende und partielle Referenzen
- fehlende PDBs und nicht dekompilierbare Snapshots
- Snapshot-Wiederverwendung und Bereinigung
- transparente Herkunfts-, Hash-, Generation- und Completeness-Angaben

### Agentenvertrag

- `tools/list`, Ressourcen und Toolbeschreibungen enthalten keinen alten `targetType`-Pfad.
- `projectRoot` und `configPath` sind nicht als versteckte Ersatzparameter vorhanden.
- Navigation funktioniert ohne Regeldatei.
- Source-Regeltools melden fehlende Konfiguration eindeutig; Assembly-Lint-Aufrufe melden `unsupported`.
- Source- und Decompiled-Aufrufe verwenden denselben `targetPath`-Vertrag.
- `get_server_health` funktioniert global ohne Target und zielgebunden mit automatisch erkanntem Target.
- Resource-URIs mit Windows-Pfaden werden korrekt URL-kodiert.
- Composite-Tools liefern partielle Abschnitte mit eigenen Statuswerten.

### Projektabschluss

Die Implementierung muss zusätzlich den in `AGENTS.md` geforderten Build und die vollständigen Nicht-Stress-Testläufe über `FastTests` und `IntegrationTests` grün durchlaufen. Wegen der MCP-Vertragsänderung sind außerdem gezielte MCP-Handshake-, Tool-Schema-, Source- und Assembly-Integrationstests erforderlich.

Zusätzlich ist ein abschließender Vertrags-Scan mit `rg` über produktiven MCP-Code, MCP-Tests, `Docs/` und `.agents/` verpflichtend. Er muss aktive Vertragsvorkommen von `targetType`, `projectRoot` als MCP-Zielparameter und `ainetlinter.project.json` auf null reduzieren; rein lokale C#-Variablennamen wie ein fachlich anderes `targetType` sowie klar markierte historische Dokumentation oder der Konzeptentwurf selbst sind davon ausgenommen.

Betroffene Dokumentation muss nach der Implementierung aktualisiert werden, insbesondere `Docs/agent-api.md`, `Docs/integration.md`, `Docs/configuration.md` und die MCP-Arbeitsregeldatei.

## Arbeitsgedächtnis (nur Draft)

### Bestätigte Entscheidungen dieser Runde

- Brainstorming bleibt aktiv; keine Implementierung.
- Harte Schnitte sind gewünscht, keine Kompatibilitätsschicht.
- `targetType` wird vollständig entfernt.
- `ainetlinter.project.json` wird vollständig entfernt.
- Die Regeldatei heißt verbindlich `ainetlinter-rules.json`.
- Der Regeldateiname wird im Code über eine zentrale Konstante verwendet und nicht an vielen Stellen als String-Literal dupliziert.
- Die Regeldatei liegt bei Source-Projekten neben der `.sln`/`.slnx` und ist optional.
- Bei fehlender Regeldatei läuft der MCP-Kontext für Navigation/Analyse weiter.
- Eine DLL aktiviert den Decompiled-Modus automatisch; Assembly-Linting ist deaktiviert und nicht verfügbar.
- Es wird kein `<assembly-name>.project.json` erzeugt.
- Technische Dekompilierungsartefakte bleiben intern und werden über Herkunft, Vollständigkeit und Identität transparent gemacht.
- Die MCP-Umstellung wird als vollständiger Refactor aller betroffenen Vertrags-, Session-, Ressourcen-, Test- und Dokumentationsschichten umgesetzt; ein lokaler Resolver-Patch ist ausdrücklich nicht ausreichend.

### Relevante geprüfte Evidenz

- Der aktuelle MCP-Vertrag verlangt in `AnalysisTargetResolver` `targetType` und `targetPath`.
- Der aktuelle `ProjectDefinitionLoader` verlangt eine feste `ainetlinter.project.json` mit `solution` und `rules`.
- Die bestehende Assemblyanalyse besitzt bereits getrennte Registry-/Snapshot- und Ressourcenpfade.
- Die aktuelle MCP-Dokumentation beschreibt Source- und Assembly-Tools mit unterschiedlichen Capabilities.
- Die aktuelle Code-/Dokumentationssuche zeigt aktive Vertragsverweise in Registrierungen, Dispatch, ProjectRegistry/Leases, Ressourcen, ServerInstructions, Tests, E2E-Harness und mehreren Dokumentationen; damit ist ein Querschnittsrefactor sachlich begründet.
- Die Repository-Regeln verlangen MCP-first für C#-Semantik, aber dieses Konzept betrifft primär den externen Vertrag und wurde deshalb anhand der vorhandenen Resolver-, Loader- und Dokumentationsstrukturen geprüft.

### Vorläufige Empfehlungen

- Nur bestehende konkrete Dateien mit unterstützter Endung akzeptieren: `.sln`/`.slnx` oder `.dll`/`.exe`; keine Verzeichnisheuristik.
- Fehlende Regeln als `navigation`, nicht als erfolgreiche leere Lint-Auswertung behandeln.
- Assembly-Kontexte nicht linten; die Lint-Semantik würde sonst Aussagen über generierten statt originalen Code nahelegen.
- Den Assembly-Cache content-basiert identifizieren.
- Einen gemeinsamen `ResolvedAnalysisContext` einführen, ohne alle internen Registry-Implementierungen künstlich zu verschmelzen.

### Nächste fachliche Klärung

Die fachlichen Scope-Entscheidungen sind bestätigt. Der 360°-Review bestätigt den vollständigen MCP-Refactor als sinnvollen Scope. Vor einer Freigabe ist nur noch zu prüfen, ob die beschriebene Matrix und die Verifikationsanforderungen als ausreichender Übergabevertrag für die Umsetzung akzeptiert werden.
