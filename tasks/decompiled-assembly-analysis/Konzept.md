---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: large
rules_dir: .agents/rules
last_updated: 2026-08-28
open_questions:
  - ILSpy-/ICSharpCode.Decompiler-Version und Wiederverwendung des vorhandenen manuellen Decompilers
  - Exakter Regel-/Config-Vertrag fuer Lint-Tools auf dekompilierten Assemblies
  - Verbindliches Schema fuer explizite Gitea-Mappings, Aktualisierung und Authentifizierung
  - Sichtbarkeit und Suchscope der vollstaendigen Source-Solution bei einem Assembly-Target
  - Alias-/Lease-Modell fuer direkte DLL-Targets, Projektabhaengigkeiten und gemeinsame Source-Snapshots
---

# Konzept: Einheitliches Roslyn-Analyseziel für Projekte und dekompilierte Assemblies

## Ziel (Was)

AiNetLinter soll eine lokale .NET-DLL für die semantische Analyse so behandeln können
wie das Quellcodeprojekt, aus dem sie entstanden ist. Bei einem Quellcodeprojekt lädt
AiNetLinter die vorhandene Solution und ihre Documents. Bei einer Assembly soll zuerst
geprüft werden, ob verlässlich zugehöriger Quellcode aus einem explizit konfigurierten
Gitea-Repository verfügbar ist. Nur wenn kein passender Quellcode gefunden oder dessen
Zuordnung nicht ausreichend belegt werden kann, wird der Quellcode transparent aus der
DLL dekompiliert, in einem synthetischen Roslyn-Project materialisiert und anschließend
über dieselben Roslyn-Funktionen analysiert. Der lokale Clone eines Gitea-Repositories
ist dabei ein interner Cache und keine zweite, konkurrierende Source-of-Truth.

Für einen Agenten soll der Ablauf dadurch direkt sein:

1. Der Agent entdeckt beispielsweise `bar.dll`.
2. Er ruft ein beliebiges passendes Roslyn-Tool mit dieser DLL als Analyseziel auf.
3. AiNetLinter löst zuerst eine passende Quellcodequelle auf und verwendet nur bei
   fehlendem belastbarem Match transparent eine dekompilierte Roslyn-Session.
4. Weitere Fragen zu Symbolen, Bodies, Referenzen, Aufrufbäumen und Metriken verwenden
   dieselbe residente Session.

Eine Assembly benötigt dafür **keine** `ainetlinter.project.json`. Diese Datei bleibt
der primäre Konfigurationsvertrag für ein `project`-Target. Die Assembly-Analyse muss auch für DLLs aus
unbekannten Projekten und außerhalb eines bekannten Repositorys funktionieren. Eine
eine globale Mapping-Datei darf die Analyse einer bekannten eigenen DLL verbessern, ist
aber keine Voraussetzung für den Dekompilations-Fallback.

Das Ziel ist nicht, den ursprünglichen Quellcode zu behaupten. Ergebnisse aus einer
Dekompilation müssen als solche erkennbar sein und bei unvollständiger
Abhängigkeitsauflösung oder problematischer Decompilation einen partiellen Zustand
melden.

## Warum / Kontext

Der primäre Arbeitskontext ist ein aktuell bearbeitetes Projekt A. Eine von A
referenzierte DLL ist ein externes Nachschlageziel: AiNetLinter soll erklären können,
wie die DLL funktioniert, damit der Agent in A korrekt weiterprogrammieren kann.
Externe Quellen werden in diesem Task niemals verändert oder zurückgeschrieben.

„Extern“ bedeutet dabei nicht zwingend Drittanbieter. Auch eine eigene DLL mit
vorhandenem Quellcode oder eine gemeinsam verwendete Shared-Library ist extern, sobald
sie außerhalb des aktuell bearbeiteten Projektroots liegt. Das parallele Bearbeiten
eines solchen Quellprojekts durch einen weiteren Agenten ist möglich, aber nicht der
primäre Workflow und wird nicht als Multi-Branch-Synchronisation modelliert.

## Zielplattformen / Technischer Rahmen

- .NET 10, C# und Roslyn im bestehenden lokalen AiNetLinter-MCP-Daemon.
- Windows als Laufzeitumgebung; Projekt- und Assemblypfade sind lokale absolute Pfade.
- Der bestehende Projekt-Session-/Lease-/TTL-Lebenszyklus wird weiterverwendet und nur
  um externe Assembly-/Source-Sessions ergänzt.
- Externe DLLs werden statisch über PE-Metadaten, Referenzen und Decompilation
  analysiert; sie werden niemals geladen oder ausgeführt.
- Ein explizites Source-Mapping zeigt in der ersten Ausbaustufe auf einen
  konfigurierten Gitea-Stand. Der lokale Clone ist nur interner Cache; Gitea wird nicht
  anhand von DLL-Namen durchsucht.

## Draft-Ergänzung: Drei Quellen, ein Roslyn-/MCP-Kern

Die wichtige architektonische Trennung ist nicht „Projektanalyse oder
Assemblyanalyse“, sondern **Herkunft der Analysequelle** vor dem gemeinsamen
Roslyn-Kern. Für die Analyse gibt es drei relevante Eingangspfade:

1. **Quellcode des aktuell bearbeiteten Projekts:** Der MCP erhält einen
   Projektroot, lädt die konfigurierte Solution und verwendet deren Documents. Das
   ist der aktive Programmier- und Änderungscontext des Agenten.
2. **Bekannter externer Quellcode zu einer Assembly:** Die DLL ist das
   Nachschlageziel, aber ihre passende Source-Solution oder ihr passendes Repository
   ist explizit bekannt. In diesem Fall wird der Quellcode read-only verwendet; eine
   Dekompilation wäre für diesen Pfad unnötig und möglicherweise irreführend.
3. **Externe Assembly ohne Source-Zuordnung:** Die DLL wird statisch dekompiliert und
   als synthetisches Roslyn-Project read-only bereitgestellt.

„Externe DLL“ bedeutet dabei zunächst nur „liegt außerhalb des aktuell bearbeiteten
Projektroots“. Sie kann aus einem Drittanbieter stammen oder eine eigene, gemeinsam
verwendete Assembly wie `core.dll` sein. Eigentum ist kein ausreichendes Kriterium für
die Wahl des Analysepfads; entscheidend sind Verfügbarkeit und Nachweis der passenden
Quellcodeversion.

Das Zielbild lautet damit:

```text
Quellcode des MCP-Projekts  ────────┐
erkannter Quellcode zu einer DLL ──┼─> Roslyn-Quelle/Session ─> MCP-Analyse-Tools
DLL zur Dekompilation ─────────────┘          (ein gemeinsamer Kern)
```

Der Teil rechts von der Quellenauflösung wird nur einmal implementiert. Die drei
Pfade liefern eine gemeinsame Analysequelle mit Solution-/Project-/Document-Sicht,
SemanticModels, Symbolen und Origin-Metadaten. Unterschiede wie „Originalquelle“
gegen „dekompiliert“ bleiben Metadaten und Vertrauenszustand, nicht drei getrennte
Implementierungen der Roslyn-Abfragen.

### Arbeitskontext und Cache-Grenze

Ein MCP-Aufruf aus Projekt A darf ein externes Assembly-Target als Nachschlageziel
adressieren. Das externe Target ist dabei nicht automatisch ein zweiter Consumer- oder
Änderungskontext für A. Projekt A und die externe Quelle bleiben getrennte Sessions;
geteilt werden darf die eindeutig identifizierte Source-Snapshot-Repräsentation.

Die reale lokale Identität beginnt beim kanonisierten absoluten DLL-Pfad bzw. beim
kanonisierten Projektroot. Für gemeinsam verwendeten Quellcode kommt die konkrete
Source-Snapshot-Identität hinzu. Derselbe externe Quellstand soll unabhängig davon
wiederverwendet werden, ob er direkt über eine DLL oder als Referenz-/Nachschlagequelle
entdeckt wurde. Eine parallele Änderung durch einen weiteren Agenten ist kein eigener
Branch-Vertrag. Eine veraltete residente Session soll möglichst über Pfad-/Datei- oder
Snapshot-Prüfung erkannt werden; ein Neustart des lokalen MCP-Daemons bleibt ein
zulässiger manueller Fallback für seltene Sonderfälle.

### Analyseziel und Quellenherkunft getrennt halten

`targetType=assembly` sollte deshalb nicht mehr implizit „immer dekompilieren“
bedeuten. Es bezeichnet zunächst nur das Artefakt, zu dem Antworten erwartet werden.
Ein vorgeschalteter `AssemblySourceResolver` entscheidet anhand des expliziten Gitea-
Mappings und von Assembly-Metadaten, ob daraus eine
`source-backed`- oder eine `decompiled`-Quelle entsteht:

```text
AssemblyTarget (DLL)
        |
        v
AssemblySourceResolver
        |
        +--> verifizierter Source-Match --> vorhandene Source-Solution / Source-Project
        |
        `--> kein verifizierter Match --> statische Decompilation --> synthetisches Project
                                      \
                                       `--> gemeinsamer Roslyn-/MCP-Kern
```

Die Auflösung eines Quellprojekts für die DLL ist kein versteckter Consumer-Kontext.
Die Frage „in welchem Projekt wird diese Assembly verwendet?“ bleibt eine separate,
explizite Cross-Target-Abfrage. Hier geht es ausschließlich um die Herkunft des
analysierten Assembly-Artefakts.

### Was einen belastbaren Match ausmacht

Ein Match `fremde.dll <> Quellcode\in\dem\Verzeichnis` darf nicht allein aus dem
Dateinamen oder aus `repo-name <> dll-name` abgeleitet werden. Ein solcher Name kann
ein sinnvoller Kandidat sein, beweist aber weder das richtige Projekt noch die richtige
Version. Die Auflösung sollte Signale mit unterschiedlicher Verlässlichkeit sammeln
und im Ergebnis offenlegen:

Für eine externe `source-backed`-Analyse ist ein explizites globales Gitea-Mapping
erforderlich. Automatische Signale dürfen diese Zuordnung plausibilisieren oder einen
Mismatch melden, aber keinen unbekannten Source-Checkout und kein Gitea-Repository
eigenständig als Originalquelle auswählen. Ohne explizite Zuordnung bleibt die DLL der
Decompilationspfad.

- **Starke Signale:** nachweisbare SourceLink-/Repository-Metadaten, ein zur DLL
  passender PDB-/Build-Bezug sowie ein expliziter Mapping-Eintrag. Der beim
  Aktualisieren tatsächlich ausgecheckte Commit wird intern als Snapshot-Identität
  festgehalten; er muss im ersten Mapping nicht manuell eingetragen werden.
- **Mittlere Signale:** `AssemblyName`/Projekt-`AssemblyName`, Ziel-Framework,
  Projektpfad, Output-Verzeichnis und weitere Build-Metadaten stimmen überein.
- **Schwache Signale:** Repositoryname, DLL-Dateiname oder Namenskonventionen wie
  `Core.dll` und `Core` stimmen überein.

Die Quellcodeversion bleibt wichtig, muss aber nicht als manuelle Konfigurationslast
beginnen. Ein aktueller Checkout von `core` kann semantisch von der DLL abweichen, die
ein anderes Projekt tatsächlich verwendet. Die interne Identität eines Source-
Snapshots sollte deshalb `Repository + tatsächlich geladener Commit + Projekt +
AssemblyName` enthalten. Falls nur ein schwacher Kandidat existiert, sollte AiNetLinter
nicht still einen automatisch gefundenen Checkout als Originalquelle ausgeben; ein
expliziter Mapping-Eintrag darf dagegen genau diese Quelle als gewünschtes
Nachschlageziel festlegen, mit sichtbarer Evidenz und gegebenenfalls niedriger
Confidence.

### Gitea-Register und wartungsarmes Mapping

Für den ersten Ausbauschritt gibt es genau eine Benutzerkonfiguration für externe
Source-Solutions: eine globale Mapping-Datei. `ainetlinter.project.json` bleibt auf
das aktuell bearbeitete `project`-Target (Solution und Regeln) beschränkt und muss
keine externe DLL-Konfiguration wiederholen. Der Pfad der globalen Mapping-Datei wird
über `appsettings.json` gesetzt.

Die Solution-Datei ist absichtlich Teil des Mappings, weil ein Repository mehrere
Solutions enthalten kann. Die einzelnen `.csproj`-Pfade werden jedoch **nicht** in der
Mapping-Datei gepflegt. AiNetLinter lädt die konfigurierte `.sln`/`.slnx`, liest daraus
die enthaltenen Projekte und ermittelt deren tatsächlichen `AssemblyName` bzw. die
von der Solution referenzierte Projektstruktur. Die Konfiguration benennt nur die
bekannten DLLs/AssemblyNames:

```json
{
  "repositories": [
    {
      "url": "https://gitea.example/shared.git",
      "solutionPath": "src/Shared.slnx",
      "assemblies": [
        "Foo.dll",
        "Bar.dll"
      ]
    }
  ]
}
```

Das ist ein konzeptionelles Schema, die endgültigen Property-Namen bleiben offen.
Beim Laden wird jeder Eintrag aus `assemblies` gegen die Projekte der Solution
aufgelöst. Ein abweichendes `<AssemblyName>` wird berücksichtigt; bei keinem oder
mehreren Treffern meldet AiNetLinter einen Konfigurationsfehler statt einen beliebigen
`.csproj` zu wählen. Damit kann ein Repository mehrere DLLs erzeugen, ohne dass ihre
Projektpfade redundant gepflegt werden müssen.

Die vollständige Solution ist der gemeinsame Snapshot-/Cache-Kontext. Das aufgelöste
Source-Projekt ist lediglich die Assembly-Zuordnung innerhalb dieser Solution. Ein
Repository wird nicht anhand von DLL-Namen in Gitea gesucht: Ohne expliziten Eintrag
bleibt die DLL im Decompilationspfad.

Ein lokal vorhandener Checkout ist in diesem Modell nur der interne Clone-/Cache des
konfigurierten Gitea-Repositories. Er ist keine konkurrierende Source-of-Truth. Ein
lokaler, dirty oder ungebauter Arbeitsstand wird in der ersten Ausbaustufe nicht
automatisch verwendet; wer Änderungen über mehrere Agenten teilen will, committed
und pusht sie nach Gitea. Ein späterer expliziter Local-Checkout-Modus wäre eine
separate Erweiterung und kein stiller Vorrang vor Gitea.

Für die gemeinsame `core.dll` bedeutet das: Es gibt einen global wiederverwendbaren
Source-Eintrag, auf den mehrere Projekte zeigen können. Verwendet ein Projekt jedoch
eine andere Core-Version, muss die Zuordnung auf einen anderen geladenen Source-Stand
oder eine andere Source-Snapshot-Identität zeigen. `repo-name=core` allein darf diese
Unterscheidung nicht verdecken.

### Sparring-Einschätzung

Ich würde die Entscheidung vorerst so festhalten: `targetType` beschreibt das
Analyseziel, eine separate Source-Auflösung beschreibt die Herkunft. Das globale
Register ist für die N/X-Überlappung die geeignete Basis; `ainetlinter.project.json`
bleibt auf den aktuellen Projektkontext beschränkt. Gitea wird als reproduzierbare
Quelle mit URL und automatisch festgehaltenem aktuellem Commit behandelt, nicht als
freies Suchsystem. Ein Gitea-Repository wird dabei als Source-Solution-Snapshot
behandelt, aus dem mehrere Assembly-Projekte adressiert werden können.

Die sichere Fallback-Regel lautet: **verifizierter Source-Match vor Dekompilation,
sonst Dekompilation**. Ein nicht verifizierter Match ist kein Gewinn, wenn dadurch eine
plausibel aussehende, aber zur DLL nicht passende Quelle analysiert wird. Beide Pfade
enden anschließend in derselben Roslyn-/MCP-Schicht und liefern explizit `origin` und
`confidence` zurück.

## Brainstorming: Gemeinsame DLLs, Buildpfad und Arbeitszustand

Dieser Abschnitt hält den Praxisfall und die daraus abgeleitete Konfigurationsrichtung
fest. Die endgültigen Property-Namen und Authentifizierungsdetails bleiben offen.

### `projektA` und `core.dll` gleichzeitig bearbeiten

Der primäre Fall ist nicht die gleichzeitige Bearbeitung beider Quellen, sondern das
Nachschlagen aus `projektA`:

```text
Agent 1: projektA.exe analysieren oder ändern
Agent 1: core.dll oder deren Quellcode read-only nachschlagen
optional Agent 2: Core-Quellcode separat bearbeiten
```

Wenn beide Aufrufe dieselbe Core-Quellversion meinen, sollte `core.dll` im Daemon
nicht zweimal als voneinander unabhängige Roslyn-Welt materialisiert werden. Für die
optionale parallele Bearbeitung reicht ein Registry-Key aus dem DLL-Pfad nicht aus.
Zusätzlich braucht es eine kanonische Source-Snapshot-Identität, beispielsweise:

```text
Repository-URL + geladener Source-Stand + Source-Project-Pfad + Target-Framework + Source-Hash
```

Die direkte Analyse von `core.dll` und die Auflösung von `core.dll` aus `projektA`
werden dann als zwei Target-Aliase auf denselben Source-Snapshot geführt. Source-
Documents bzw. deren Materialisierung sollen geteilt werden; eine gemeinsame
SemanticModel-Instanz ist keine fachliche Voraussetzung, weil sie an die jeweilige
Compilation gebunden sein kann. Der Consumer-Kontext von `projektA` bleibt separat.

Das ist eine wichtige Präzisierung von „einmal im Daemon“: Nicht zwingend die
vollständige `projektA`-Session und die vollständige Core-Session sind identisch,
sondern die darunterliegende kanonische Source-/Roslyn-Repräsentation von Core wird
geteilt. So bleiben Target, Source-Herkunft und Consumer-Kontext getrennt, ohne die
gleiche Quelle doppelt zu laden.

### Der gemeinsame Buildpfad

Dass alle DLLs in dasselbe Verzeichnis gebaut werden, ist praktisch, darf aber nicht
als Identität der Quelle interpretiert werden. Das Verzeichnis ist nur der Fundort
des Artefakts. Für die Zuordnung müssen mindestens kanonischer Pfad, Binary-Hash,
Assembly-Identität und die ermittelte Source-Snapshot-Identität berücksichtigt werden.

Das schützt insbesondere vor diesem Zustand:

1. `core.dll` wird aus einem unfertigen oder anderen Arbeitsstand in den gemeinsamen
   Buildpfad geschrieben.
2. `projektA` löst dieselbe Datei auf und erwartet daraus eine bestimmte Core-Version.
3. Ein weiterer Agent sieht nur den Pfad und würde fälschlich annehmen, die Quelle sei
   eindeutig.

Der Daemon sollte bei jeder relevanten Änderung des Artefakts den Binary-Fingerprint
   prüfen. Ein geänderter Buildoutput darf keinen alten Source-Match weiterverwenden.
Wenn Binary und Source-Snapshot nicht nachweisbar zusammengehören, ist ein sichtbarer
   `mismatch`-/`partial`-Zustand besser als eine stillschweigend gemeinsam genutzte,
   falsche Roslyn-Quelle.

### Uncommitted Source und nicht gebauter Arbeitsstand

Ein direkt bearbeiteter Core-Checkout kann Änderungen enthalten, die noch nicht
gebaut oder committed wurden. In diesem Zustand gibt es ohne Buildmanifest oder
zusätzlichen Nachweis keine sichere Verbindung zwischen dem aktuellen Quelltext und
der DLL im gemeinsamen Buildpfad. Die erste Ausbaustufe verwendet deshalb keinen
lokalen Dirty-/unbuilt-Checkout als konkurrierende Quelle.

Für den normalen Arbeitsablauf ist die beabsichtigte Wahrheit: Core fertigstellen,
committen, nach Gitea synchronisieren und diesen Stand aus den anderen Projekten
wiederverwenden. Ein MCP-Refresh lädt danach den neuen Stand; ein Neustart des lokalen
Daemons bleibt ein zulässiger manueller Fallback, ist aber nicht der normale Cache-
Vertrag. Ein späterer Local-Checkout-Modus müsste ausdrücklich aktiviert und als
eigener Origin-/Vertrauenszustand ausgewiesen werden.

### Gitea als gemeinsame Wahrheit

Wenn `projektA` eine eigene `foo.dll` referenziert, deren Quellcode lokal gerade nicht
vorhanden ist, kann die Source-Auflösung über das globale Repository-Register den
passenden Gitea-Eintrag verwenden. Der Quellcode wird beim ersten Zugriff geklont bzw.
aktualisiert, als definierter vollständiger Solution-Snapshot bereitgestellt und in
derselben Source-Registry wiederverwendet wie bei einer direkten Analyse von
`foo.dll`.

Gitea ist dabei die gemeinsame Quelle für den Quellcode, aber ein Repositoryname
allein beweist noch nicht, welches der enthaltenen Source-Projekte und welche DLL
dazugehört. Repository, konkreter Solution-Pfad und AssemblyName müssen deshalb
gemeinsam auflösbar sein; der konkrete Projektpfad wird aus der Solution abgeleitet
und nicht separat konfiguriert. Der beim Aktualisieren festgestellte Commit wird
intern mit dem Snapshot gespeichert und in der Antwort als Versionsnachweis sichtbar
gemacht.

### Branches als bewusste Grenze

Branches werden in diesem Konzept nicht als eigene Analyse-Dimension modelliert. Die
stabile Identität ist der konkrete Commit bzw. Source-Snapshot; ein Branchname ist nur
eine bewegliche Referenz darauf. Das deckt den normalen Ablauf ab, in dem Änderungen
fertiggestellt, committed und über Gitea synchronisiert werden.

Ein Wechsel zwischen parallelen oder lokalen Branch-Arbeitsständen ist damit kein
versprochenes Szenario. Der Daemon kann einen neuen Commit als neue Source-Identität
behandeln oder einen Refresh wegen Dirty-/Mismatch-Zustand ablehnen. Er muss nicht
versuchen, zwei widersprüchliche Core-Stände automatisch zu verschmelzen oder beim
Zurückwechseln die vorherige Bedeutung zu erraten. Dieses „geht in diesem Sonderfall
nicht zuverlässig“ ist eine bewusste Grenze zugunsten eines nachvollziehbaren
Normalfalls.

## Kurzentscheidung

Alle Roslyn-orientierten MCP-Tools erhalten in einem harten API-Schnitt ein einheitliches
Target-Schema:

```json
{
  "targetType": "project",
  "targetPath": "C:\\Daten\\MeineAnwendung"
}
```

oder:

```json
{
  "targetType": "assembly",
  "targetPath": "D:\\Vendor\\bar.dll"
}
```

`targetPath` ist bei `project` ein absoluter Projektroot und bei `assembly` ein
absoluter Pfad zu einer existierenden `.dll`. Die beiden Parameter sind in allen
projekt-/roslynbezogenen Tools Pflicht. Bei einem Assembly-Target entscheidet die
Source-Auflösung anschließend, ob eine passende Source-Solution bzw. ein passendes
Source-Project verwendet oder die DLL dekompiliert wird. Der bisherige Parameter
`projectRoot` wird nicht parallel weitergeführt; ebenso bleiben `assemblyPath` plus
optionalem `projectRoot` nicht als zweite API bestehen.

Die Wire-API bleibt bewusst flach, weil sie für MCP-Tool-Schemas und Agenten leicht
lesbar ist. Intern wird sie sofort in einen unveränderlichen `AnalysisTarget`-Record
überführt. Es wird keine Migrationserkennung und kein dualer Dispatch eingeführt.

## Wo im Projekt

- `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs` — bestehender Registry-, Lease-,
  TTL-, LRU- und Creation-Barrier-Anker für residente Projekt-Sessions.
- `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` — bestehende Roslyn-Solution-Session und
  Datei-Staleness-Anker.
- `src/AiNetLinter/Mcp/Projects/ProjectToolCall.cs` — bestehende gemeinsame Target-
  Guard-/Lease-Grenze der projektgebundenen MCP-Tools.
- `src/AiNetLinter/Mcp/Registration/` — MCP-Toolregistrierungen und aktuelle
  `projectRoot`-Schemas, die auf den neuen Target-Vertrag geprüft werden müssen.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/` — bestehender metadata-only Assembly-
  Pfad, der zur externen Assembly-Session und Source-Auflösung erweitert wird.
- `src/AiNetLinter/Configuration/` sowie `ainetlinter.project.json` — vorhandene
  Konfigurationsgrenzen; die neue globale Mapping-Datei wird dort angebunden, ohne
  den Projektvertrag um externe DLL-Listen zu erweitern.
- `src/AiNetLinter/Cache/AnalysisCacheManager.cs` — vorhandener Batch-Analyse-Cache
  unter `cache` neben der EXE; dieser ist nicht der neue MCP-Source-Cache.
- `src/AiNetLinter/Mcp/Daemon/MruStateStore.cs` — vorhandener persistenter Daemon-
  MRU-Zustand unter `%LOCALAPPDATA%`; nicht als Source-/Decompilation-Cache verwenden.
- `Docs/agent-api.md`, `Docs/integration.md`, `Docs/configuration.md`, `README.md` und
  `Docs/ROADMAP.md` — zu aktualisierende MCP-, Konfigurations- und Statusdokumentation.

## Entdeckte Mängel/Redundanzen

- **Assembly-Pfad ohne residente Source-Sicht**
  - **Gefunden:** `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/` arbeitet aktuell
    metadata-only; es gibt dort keine langlebige dekompilierte Roslyn-Session.
  - **Bezug:** Lücke zum Ziel dieses Konzepts, kein zusätzlicher Regelverstoß.
  - **Vorschlag:** Bestehenden metadata-only Pfad als Bestandteil der gemeinsamen
    Assembly-Session wiederverwenden und nicht daneben einen zweiten MCP-Analysepfad
    aufbauen.
  - **Entscheidung:** übernommen ins Scope.
- **Vorhandener Projekt-Lifecycle**
  - **Gefunden:** `ProjectRegistry`, `ProjectToolCall` und `McpCodeGraphServer` bilden
    bereits die relevante Lease-/Session-Infrastruktur.
  - **Bezug:** Architekturregel Einfachheit vor Abstraktion; kein paralleler
    Assembly-spezifischer Lifecycle ohne Wiederverwendung.
  - **Vorschlag:** Gemeinsame Registry-/Lease-Grundlagen generalisieren, aber Projekt-
    und externe Source-Sessions wegen ihrer unterschiedlichen Semantik getrennt halten.
  - **Entscheidung:** übernommen ins Scope.

## Scope

### Muss-Haben

- Harter MCP-Vertrag mit `targetType` und `targetPath` für alle Roslyn-/Projekt-Tools.
- Klare Trennung zwischen Analyseziel (`project`/`assembly`) und Quellenherkunft
  (Projektquelle, bekannter Assembly-Quellcode oder Dekompilation).
- `targetType=project` routet zur bestehenden Solution-/Projekt-Session.
- `targetType=assembly` routet zu einer langlebigen Assembly-Session mit vorgelagerter
  Source-Auflösung und Dekompilations-Fallback.
- Externe Assembly- und Source-Targets sind read-only Nachschlageziele und verändern
  weder Projekt A noch die externe Quelle.
- Bekannte externe Quellen werden ausschließlich über explizite globale Gitea-
  Mappings aufgelöst; es gibt keine Suche in Gitea anhand von DLL- oder
  Repositorynamen.
- Ein Mapping kann eine vollständige Source-Solution mit mehreren Source-Projekten und
  mehreren erzeugten DLLs beschreiben; die Zuordnung berücksichtigt Repository,
  Version, Source-Projekt und AssemblyName.
- Die vollständige, eindeutig versionierte Source-Solution ist der bevorzugte Snapshot-
  und Cache-Kontext. Das konkrete Source-Projekt wird als Assembly-Zuordnung kenntlich
  gemacht.
- Deterministischer Source-Match mit Assembly-/Projektidentität, Quellcodeversion und
  sichtbarer Match-Confidence.
- Gemeinsame Source-Snapshot-Registry, damit mehrere Target-Aliase denselben
  verifizierten Quellstand nur einmal als Roslyn-Quelle materialisieren.
- Keine Pflicht zur Änderung oder Anlage einer `ainetlinter.project.json` für eine DLL;
  die globale Mapping-Datei wird nur für bekannte Source-backed-DLLs benötigt.
- Statische Decompilation ohne Assembly-Ausführung und ohne `AssemblyLoadContext`.
- Persistenter Decompilation-Cache mit Hash-/Versionsprüfung.
- Synthetisches Roslyn-`Project` in einem residenten `AdhocWorkspace`.
- Roslyn-Documents aus dekompilierten C#-Dateien, vorzugsweise auf Typ-/Modulgranularität.
- MetadataReferences für das Target, seine erreichbaren Abhängigkeiten und die
  benötigten Framework-Assemblies.
- Bedarfsgesteuertes transitives Nachladen erreichbarer externer Referenzen als
  Source-Solution oder Decompilation, dedupliziert über kanonische Source-/DLL-
  Identitäten.
- Source-/IL-Origin-Mapping für Diagnose, Symbolidentität und Benutzerantworten.
- Wiederverwendung der gemeinsamen Toollogik für Symbolsuche, Bodies, Struktur,
  Referenzen, Call Trees, Dependency Graph und Metriken.
- Expliziter `complete`-/`partial`-/`degraded`-Vertrag bei fehlenden Referenzen,
  Decompiler-Warnungen oder unvollständigen Ergebnissen.
- Daemon-Health für residente Projekt-, Source-Solution- und Assembly-Sessions.
- Externe Source-Solutions und Assembly-Sessions werden separat von der bestehenden
  `MaxProjects`-Grenze gezählt; eine vollständige Source-Solution ist ein gemeinsamer
  externer Snapshot für mehrere DLLs.
- Concurrent-Load-, Refresh- und Cache-Korruptions-Tests; persistentes Cache-Aufräumen
  bleibt zunächst manuell und ist kein Muss-Haben.

### Nice-to-Have (Zwischenspeicher — aktuell leer)

Aktuell keine unentschiedenen Nice-to-Have-Punkte.

### Non-Goals (bewusst NICHT Teil davon)

- Rekonstruktion des originalen Buildprozesses oder der ursprünglichen Projektdateien.
- Ausführung von Code aus der DLL.
- Änderung oder Rückschreiben von dekompiliertem Code in die Assembly.
- Automatische Ermittlung eines Consumer-Projekts als versteckter zweiter Target-Kontext.
- Automatische Suche oder Discovery von Repositories in Gitea.
- Vollständige Wiederherstellung von PDB-, SourceLink- oder Originaldateinamen-Garantien.
- Behauptung, dass der erzeugte C#-Code mit der Originalquelle identisch ist.
- Git-Diff-Analysen für externe Quellen, insbesondere dekompilierte Assemblies; der
  Git-Diff-Workflow bleibt dem aktuell bearbeiteten Projekt vorbehalten.
- Ausführung oder Analyse der Tests eines externen Projekts als Bestandteil des
  Nachschlage-Workflows. Die neuen AiNetLinter-Funktionen selbst erhalten selbstverständlich
  vollständige Unit-, Component- und Integrationstests.
- Automatische Synchronisation oder Zusammenführung verschiedener Branches, Dirty-
  Checkouts und ungebauter Arbeitsstände.
- Automatisches Aufräumen persistierter externer Cache-Einträge.
- Ein allumfassender abstrakter Framework-Layer für beliebige zukünftige Artefakte.

## Verworfene Alternativen

- **Freie Gitea-Discovery anhand von DLL- oder Repositorynamen:** verworfen, weil ein
  Repository mehrere Projekte und DLLs enthalten kann und die Zuordnung dadurch nicht
  reproduzierbar wäre.
- **Repository als alleinige Assembly-Zuordnung:** verworfen, weil zusätzlich das
  konkrete Source-Projekt, AssemblyName und die Version benötigt werden.
- **Externer Consumer als versteckter zweiter Target-Kontext:** verworfen, weil der
  primäre Workflow das read-only Nachschlagen einer externen Quelle aus Projekt A ist.
- **Branches als eigene Cache-Identität und automatische Zusammenführung:** verworfen,
  weil der normale Workflow über synchronisierte Commits läuft und Multi-Branch-
  Koordination nicht erforderlich ist.
- **Separate metadata-only Assembly-Toolwelt:** verworfen, weil Assemblys in denselben
  Roslyn-/MCP-Kern wie Projektquellen gelangen sollen.
- **Legacy- und neuer Target-Vertrag parallel:** verworfen; der MCP-Schnitt soll nach
  der Umstellung eindeutig bleiben.

## Target-Vertrag

### MCP-Wire-API

Jedes Tool, das heute den projektgebundenen `projectRoot`-Dispatch nutzt, wird auf
folgende Pflichtparameter umgestellt:

```text
targetType: "project" | "assembly"
targetPath: string
```

Beispiele:

```json
{
  "targetType": "assembly",
  "targetPath": "D:\\Programme\\ThirdParty\\bar.dll",
  "namePatterns": ["Save"]
}
```

```json
{
  "targetType": "project",
  "targetPath": "C:\\Daten\\MeineAnwendung",
  "symbolIdentifier": "M:MeineAnwendung.BelegService.Save"
}
```

Die semantischen Zusatzparameter eines Tools bleiben erhalten. Nur der gemeinsame
Target-Teil wird ersetzt. Pfade werden absolut verlangt und vor dem Registry-Zugriff
kanonisiert. Ein `project`-Target muss weiterhin über die Projektdefinition eine
Solution und Regeldatei auflösen können. Ein `assembly`-Target prüft eine existierende
`.dll` und benötigt keine Projektdefinition. Eine globale Source-Konfiguration kann
für ein Assembly-Target ein Gitea-Repository referenzieren; dessen lokaler Clone ist
nur interner Cache.

`get_server_health` ist ein Maintenance-Tool und darf weiterhin ohne Target den
gesamten Daemon-Status liefern. Mit einem Target soll es gezielt genau diese
Projekt- oder Assembly-Session anzeigen.

### Interne Modelle

```csharp
internal enum AnalysisTargetKind
{
    Project,
    Assembly,
}

internal sealed record AnalysisTarget(
    AnalysisTargetKind Kind,
    string CanonicalPath);

internal sealed record AnalysisTargetRequest(
    string? TargetType,
    string? TargetPath);

internal sealed record AnalysisOrigin(
    string Kind,
    string SourcePath,
    string? ContentHash,
    string? GeneratedPath,
    string? MetadataToken,
    string Confidence);

// Konzeptionelle Herkunftsmodelle; konkrete Namen und Typen bleiben offen.
internal enum AnalysisSourceKind
{
    ProjectSource,
    AssemblySource,
    DecompiledAssembly,
}

internal sealed record AssemblySourceMatch(
    AnalysisSourceKind Kind,
    string? RepositoryUrl,
    string? LoadedRevision,
    string? ProjectPath,
    string? AssemblyName,
    string Confidence,
    IReadOnlyList<string> Evidence);
```

`AnalysisTargetRequest` ist nur das Eingabe-/Validierungsmodell. Nach erfolgreicher
Validierung sollen Analyzer ausschließlich `AnalysisTarget` oder eine residente
`AnalysisSession` sehen. Die Assembly-Session erhält zusätzlich das Ergebnis der
Source-Auflösung, damit sie zwischen Originalquelle und Dekompilation unterscheiden
kann. Dadurch verteilt sich die Prüfung von `targetType`, Pfadsemantik und
Source-Match nicht über alle Toolregistrierungen.

## Zielarchitektur

Die Quellenauflösung liegt vor dem gemeinsamen Roslyn-Kern. Ein Assembly-Target wird
also nicht direkt an den Decompiler gebunden: Die Assembly-Session kann entweder eine
passende Source-Solution bzw. ein Source-Project materialisieren oder den
Dekompilationspfad verwenden.

```text
MCP Tool Call
    |
    v
AnalysisTargetRequest
    |
    v
AnalysisTargetResolver
    |
    v
AnalysisRegistry -- Lease / TTL / LRU / Creation Barrier
    |
    +-- ProjectAnalysisSession
    |     `-- bestehender McpCodeGraphServer + MSBuildWorkspace/Solution
    |
    `-- AssemblyAnalysisSession
          +-- AssemblySourceResolver
          |     +-- Source-Solution / Source-Project (Match vorhanden)
          |     `-- DecompilationCache + Decompiler (kein Match)
          +-- AssemblyFingerprint
          +-- Roslyn-Workspace / Project-Materialisierung
          +-- MetadataReferenceResolver
          `-- Origin-/IL-Mapping
```

### Gemeinsame Session-Oberfläche

Die bestehende `McpCodeGraphServer`-Klasse soll nicht mit allen Assembly-Sonderfällen
aufgefüllt werden. Stattdessen wird eine kleine gemeinsame Session-Schnittstelle für
den zentralen Dispatch eingeführt:

```csharp
internal interface IAnalysisSession : IDisposable, IAsyncDisposable
{
    AnalysisTarget Target { get; }
    ServerLoadState LoadState { get; }
    bool IsLoaded { get; }
    DateTime? LastGoodStateUtc { get; }
    string? LastLoadError { get; }
    Solution? GetCurrentSolution();
    AnalysisOrigin Origin { get; }
}
```

Die Oberfläche soll klein bleiben. Config-, Console-, Catalog- und Linter-spezifische
Details werden über einen zweiten, ebenfalls kleinen Session-Kontext oder bestehende
Services geliefert. Kein DI-Container und kein generischer Objektgraph nur zur
Abstraktion werden eingeführt.

Der zentrale Dispatch wird konzeptionell von `ProjectToolCall` zu einem allgemeinen
`AnalysisToolCall`:

```csharp
internal static async Task<CallToolResult> ExecuteAsync(
    AnalysisRegistry registry,
    AnalysisTargetRequest request,
    Func<AnalysisLease, Task<CallToolResult>> call)
{
    var target = AnalysisTargetResolver.Resolve(request);
    if (!target.Succeeded)
    {
        return target.Error!;
    }

    var leaseResult = registry.Lease(target.Value!);
    if (!leaseResult.Succeeded || leaseResult.Lease is null)
    {
        return leaseResult.Error!;
    }

    using var lease = leaseResult.Lease;
    return await lease.ExecuteAsync(call).ConfigureAwait(false);
}
```

Der konkrete Result-Typ ist ein Implementierungsdetail. Entscheidend ist, dass alle
Toolregistrierungen denselben Target-/Lease-Pfad verwenden und nicht mehr selbst
zwischen `projectRoot is null` und standalone Assembly verzweigen.

### Registry und Lebensdauer

Die aktuelle `ProjectRegistry` ist fachlich künftig eine Registry für Analyseziele.
Für den harten Schnitt gibt es zwei mögliche interne Ausprägungen:

1. `ProjectRegistry` wird zu `AnalysisRegistry` umbenannt und erhält polymorphe
   Session-Factories.
2. Eine kleine `AnalysisRegistry` koordiniert getrennte Projekt- und Assembly-Maps.

Empfohlen ist Variante 2, weil die Ressourcen- und Fehlersemantik beider Sessionarten
unterschiedlich ist. Der gemeinsame Registry-Code bleibt trotzdem zentral für Lease,
Creation Barrier, TTL, LRU und Dispose.

Externe Quellen sollen nicht die bestehende `MaxProjects`-Grenze verbrauchen. Die
heute standardmäßig auf 4 gesetzte Grenze zählt ausschließlich aktive
`targetType=project`-Sessions für die aktuell bearbeiteten Projektroots. Eine
vollständige externe Source-Solution zählt dabei als
ein gemeinsamer externer Snapshot, unabhängig davon, wie viele ihrer DLLs der Agent
analysiert. Externe Source-Snapshots und dekompilierte Assembly-Sessions erhalten
eine getrennte Registry und eigene Ressourcen-/Lebensdauerwerte:

```csharp
internal sealed record AnalysisRegistryOptions(
    int MaxProjects,
    int MaxSourceSolutions,
    int MaxAssemblies,
    TimeSpan ProjectIdleTtl,
    TimeSpan SourceSolutionIdleTtl,
    TimeSpan AssemblyIdleTtl);
```

Die externen Einträge werden also nicht auf `MaxProjects` angerechnet. Ob für
`MaxSourceSolutions` und `MaxAssemblies` im ersten Schritt ein harter Default nötig
ist, wird anhand des Speicherverhaltens festgelegt; eine automatische Bereinigung
des persistenten Disk-Caches ist davon getrennt und bleibt zunächst bewusst manuell.

Ein neuer Assembly-Aufruf darf nicht synchron auf die vollständige Decompilation
blockieren. Wie beim bestehenden Solution-Load wird eine Session resident registriert,
der Load läuft als Task und liefert währenddessen einen stabilen `Loading`-Zustand.
Mehrere gleichzeitige Aufrufe derselben DLL teilen sich denselben Load.

Die Registry für Target-Leases und die Registry für Source-Snapshots sollten dabei
konzeptionell getrennt bleiben. Mehrere Targets können auf denselben
Source-Snapshot-Key zeigen, während ihre Consumer-Kontexte unterschiedliche Leases
erhalten. Für eine source-backed `core.dll` ist das die Grundlage dafür, dass ein
direkter Assembly-Aufruf und die Auflösung aus `projektA` dieselben Core-Documents bzw.
deren Materialisierung wiederverwenden können. Eine gemeinsame SemanticModel-Instanz
ist wegen ihrer Bindung an eine konkrete Compilation keine fachliche Zusage.

## Assembly-Session

### Source-Auflösung vor der Dekompilation

Die Assembly-Session liest zunächst die Identität und die Referenzen der DLL und
übergibt das Artefakt an einen `AssemblySourceResolver`. Dieser Resolver verwendet
zuerst das explizite globale Gitea-Mapping. Assembly-Metadaten können den Treffer
plausibilisieren, dürfen aber kein Repository in Gitea selbstständig auswählen. Der
Resolver liefert nicht nur einen Pfad, sondern auch die Begründung und die
Versionssicherheit des Treffers.

Ein gültiger `source-backed`-Treffer muss mindestens auf ein konkretes Source-Project
oder eine klar abgrenzbare Source-Solution sowie auf einen tatsächlich geladenen
Source-Stand zeigen. Ein Treffer auf ein Repository allein reicht nicht,
weil ein Repository mehrere DLLs und Projekte enthalten kann. Ebenso darf ein
Repositoryname wie `core` nicht automatisch den Quellcode für jede `core.dll`
auswählen.

Bei einem verifizierten Treffer wird das passende Source-Project in die gemeinsame
Roslyn-Sicht übernommen; eine Decompilation findet für dieses Assembly-Target nicht
statt. Die DLL bleibt trotzdem die Target-Identität, damit Origin-, Versions- und
Referenzdiagnosen sich auf das tatsächlich analysierte Artefakt beziehen. Ohne
verifizierten Treffer wird der bestehende Decompilation-Plan verwendet und die
Antwort markiert den Ursprung als `decompiled`.

Ein globaler Source-Eintrag sollte von mehreren Projektroots referenziert werden
können. Er beschreibt deshalb eine Source-Solution bzw. ein Repository und die darin
enthaltenen Assembly-Projekte. Beim ersten Zugriff wird das Repository geklont oder
aktualisiert; der tatsächlich geladene Commit wird intern als Snapshot-Identität
festgehalten. Eine manuelle Commit-/Tag-Angabe ist für die erste Benutzerkonfiguration
nicht erforderlich. Für unterschiedliche geladene Repository-Stände entstehen
unterschiedliche Source-Snapshots.

### Fingerprint und Cache-Key

Es gibt zwei getrennte Cache-Identitäten:

- **Externe Source-Solution:** kanonische Repository-URL, tatsächlich geladener Commit
  und Solution-Pfad. Der Commit ist ein internes Ergebnis des Aktualisierens, kein
  Pflichtfeld der ersten Benutzerkonfiguration.
- **Decompilationsartefakt:** kanonischer DLL-Pfad, Binary-Hash, Decompiler-Version,
  Decompiler-Optionen und Cache-Schema.

Der Dateiname oder die Assembly-Version allein reicht nicht zur Invalidierung. Für das
Decompilationsartefakt muss der Cache-Key mindestens enthalten:

```text
kanonischer DLL-Pfad
+ SHA-256 der DLL-Bytes
+ Decompiler-Version
+ Decompiler-Optionen
+ Format-/Schema-Version des Cache-Manifests
```

mtime und Dateigröße dürfen als schneller Vorcheck verwendet werden. Sobald sich einer
der Vorwerte ändert, wird der Inhalt gehasht. Gleicher Inhalt bei neuer mtime erzeugt
keine neue Decompilation.

Da DLLs außerhalb eines Repositorys liegen können, wird der externe Cache nicht im
fremden Projekt angelegt. Arbeitsrichtung ist ein Cache-Root `cache` relativ zur
AiNetLinter-EXE; ein absoluter Cache-Root soll über `appsettings.json` überschreibbar
sein. Der bestehende Batch-Analyse-Cache liegt ebenfalls unter `cache`, bleibt aber
mit seinen bisherigen Dateien direkt im Root von den neuen externen Unterordnern
getrennt. Das automatische Aufräumen persistierter externer Cache-Einträge ist in
diesem Task ausdrücklich nicht enthalten; alte Einträge werden zunächst manuell
gelöscht. In-Memory-Leases und ein sicherer atomarer Aufbau sind davon unabhängig.

Beispiel:

```text
<AiNetLinter-EXE>\\cache\\
  <bestehende Batch-Cache-Dateien>\\
  source\\
    <Repository-Schlüssel>\\
      <geladener-Commit-oder-Snapshot-Schlüssel>\\
        solution\\                 # vollständige Source-Solution
        manifest.json
  assembly\\
    <kanonischer-DLL-Schlüssel>\\
      <DLL-SHA256-und-Optionen>\\
        source\\Namespace\\BelegService.cs
        source\\Namespace\\Beleg.cs
        manifest.json
        origin.json
```

Der Pfad zur manuellen Mapping-Datei wird in `appsettings.json` konfiguriert, zum
Beispiel konzeptionell über `ExternalSources:MappingsPath`. Der Pfad darf absolut sein;
relative Pfade werden relativ zur Konfiguration bzw. EXE aufgelöst. Der Cache-Root kann
analog über `ExternalSources:CacheRoot` überschrieben werden. Die endgültigen
Schlüsselnamen bleiben bis zur Konfigurationsrunde offen. Auch das Refresh-Intervall
für Gitea soll dort konfigurierbar sein, zum Beispiel:

```json
{
  "ExternalSources": {
    "MappingsPath": "D:\\Konfiguration\\ainetlinter-sources.json",
    "CacheRoot": "cache",
    "RefreshIntervalMinutes": 60
  }
}
```

Beim ersten Zugriff auf ein konfiguriertes Gitea-Repository wird immer ein Clone oder
Fetch gegen den konfigurierten Standard-Branch ausgeführt. Ein bereits vorhandener
Source-Cache darf nur innerhalb eines noch gültigen Refresh-Intervalls ohne erneute
Aktualisierung verwendet werden. Läuft das Intervall ab, wird vor der Analyse erneut
aktualisiert. Schlägt diese Aktualisierung fehl, wird der alte Source-Snapshot nicht
still als aktuell ausgegeben; die Session meldet den Zustand sichtbar und kann auf die
Decompilation der angefragten DLL zurückfallen.

Das Manifest enthält mindestens:

- Originalpfad, Dateigröße, mtime und SHA-256
- Assembly-Identität und Referenzliste
- Decompiler-Version, Optionen und Cache-Schema-Version
- erzeugte Dateien und Quelltext-Encoding
- Decompiler-Warnungen und Fehler
- verwendete Referenzpfade und nicht auflösbare Referenzen
- Erstellungszeit und letzter Zugriff
- Status `complete`, `partial` oder `failed`

Ein Source-Solution-Manifest enthält zusätzlich mindestens Repository-URL, geladenen
Commit, Solution-Pfad, die Zuordnung `AssemblyName -> Source-Projekt` sowie den letzten
erfolgreichen Refresh und das nächste Refresh-Intervall.

Cache-Einträge werden in einem temporären Verzeichnis aufgebaut und anschließend
atomar veröffentlicht. Ein beschädigter oder unvollständiger Eintrag wird nicht als
gültige Session adoptiert, sondern neu erzeugt.

### Decompilation als Fallback

Die Decompilation wird nur als Fallback hinter einer kleinen Adaptergrenze gekapselt.
Die konkrete ILSpy-/ICSharpCode.Decompiler-API darf nicht in allen Roslyn-Tools
auftauchen. Ein fehlender oder nicht ausreichend belegter Source-Match ist dabei ein
normaler Auswahlpfad, kein Decompilerfehler.

```csharp
internal sealed record DecompilationRequest(
    string AssemblyPath,
    string CacheDirectory,
    string DecompilerVersion,
    CancellationToken CancellationToken);

internal sealed record DecompilationResult(
    IReadOnlyList<DecompiledDocument> Documents,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    bool IsComplete);

internal sealed record DecompiledDocument(
    string GeneratedPath,
    string TypeMetadataName,
    string CSharpSource,
    string? MetadataToken);
```

Der Adapter soll nach Möglichkeit die vorhandene manuelle Decompiler-Logik oder deren
bewährte Einstellungen wiederverwenden. Er darf die DLL nicht laden oder ausführen.
Ein Timeout und eine Cancellation-Grenze sind erforderlich, damit eine problematische
oder obfuskierte Assembly den Daemon nicht dauerhaft blockiert.

### Synthetisches Roslyn-Project

Die Assembly-Session baut nach erfolgreicher oder partiell erfolgreicher Decompilation
ein `AdhocWorkspace`-Project. Das Project ist kein MSBuild-Projekt und benötigt keine
`.csproj`-Datei.

```csharp
var projectId = ProjectId.CreateNewId("decompiled-assembly");
var projectInfo = ProjectInfo.Create(
    projectId,
    VersionStamp.Create(),
    name: assemblyIdentity.Name,
    assemblyName: assemblyIdentity.Name,
    language: LanguageNames.CSharp,
    filePath: generatedProjectPath,
    compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
    metadataReferences: references);

var solution = workspace.AddProject(projectInfo).Solution;
foreach (var document in decompiledDocuments)
{
    solution = solution.AddDocument(
        DocumentId.CreateNewId(projectId),
        Path.GetFileName(document.GeneratedPath),
        SourceText.From(document.CSharpSource, Encoding.UTF8),
        filePath: document.GeneratedPath);
}
```

Die tatsächliche Erzeugung soll einen passenden `ParseOptions`-/`CompilationOptions`-
Stand setzen, damit moderne C#-Syntax des Decompilers vom Roslyn-Parser akzeptiert
wird. Ein Dokument pro Typ oder sinnvoller Decompiler-Einheit erleichtert
`get_symbol_body`, Dateifundstellen und Source-/Origin-Mapping. Ein einziger großer
Assembly-Text ist für Folgeabfragen und Antwortlimits ungünstig.

### Referenzauflösung

Die Referenzauflösung bleibt best effort, aber deterministisch und sichtbar:

1. Target-Assembly selbst als `MetadataReference`.
2. Referenzierte DLLs aus dem Verzeichnis der Target-Assembly.
3. Framework-Assemblies aus den verfügbaren Trusted Platform Assemblies bzw. einem
   passend ermittelten Target Framework.
4. Optional vorhandene, maschinenlesbare Dependency-Informationen wie eine passende
   `.deps.json`, sofern sie ohne Codeausführung ausgewertet werden kann.
5. Keine Reflection- oder Runtime-Ausführung zur Auflösung.

Die bestehende Logik aus `AssemblyAnalysisContextFactory` für PEReader,
Assembly-Identitäten, lokale Abhängigkeiten und `MetadataReference.CreateFromFile`
ist hierfür wiederverwendbar und wird in einen eigenständigen
`AssemblyReferenceResolver` überführt.

Unaufgelöste Abhängigkeiten dürfen die Session nicht pauschal verwerfen. Roslyn kann
den dekompilierten Code teilweise analysieren; die Antwort erhält Diagnosen und den
Zustand `partial`.

### Transitive externe Referenzen

Der heutige Assembly-Pfad kann die direkten Assembly-Referenzen metadata-only auslesen.
Er materialisiert daraus aber noch keine transitiven Roslyn-Quellen: Ein Aufruf wie
`projektA -> foo.dll -> new Bar().Call() -> bar.dll` kann aktuell nicht automatisch
bis zum `Bar`-Quellcode oder zu dessen dekompiliertem Body nachgeladen werden. Auch
`dependency_graph` arbeitet heute innerhalb der geladenen Project-Solution und ist
noch kein Resolver für eine externe Assembly-Referenzkette.

Das Ziel ist ein rekursiver, bedarfsgesteuerter Referenzresolver:

1. Für `foo.dll` werden die direkten Referenzen aus Metadaten und verfügbaren
   Dependency-Informationen ermittelt.
2. Jede auflösbare Referenz wie `bar.dll` wird als eigene externe Quelle über denselben
   Resolver behandelt: explizites Gitea-Mapping zuerst, sonst statische Decompilation.
3. Derselbe absolute DLL-Pfad bzw. dieselbe Source-Snapshot-Identität wird nur einmal
   geladen. Zyklen und bereits besuchte Referenzen werden über kanonische Identitäten
   dedupliziert; die Auflösung braucht kein Runtime-Loading.
4. Für Source-Projekte innerhalb derselben vollständigen Solution verwendet Roslyn
   die vorhandene Projektstruktur. Für Quellen aus anderen Repositories oder für
   Third-Party-DLLs werden externe Source-/Decompilation-Sessions als Referenzen
   eingebunden.

Damit kann der Agent erkennen, dass ein Symbol aus `bar.dll` kommt, und dessen
Quellcode bzw. dekompilierten Body nachladen. Der erreichbare Bereich ist durch die
lokal vorhandenen DLLs, explizite Source-Mappings und die Decompilergrenzen bestimmt;
fehlende Referenzen bleiben sichtbare `partial`-/External-Nodes statt stiller
Phantomsymbole. Ein vollständiger, beliebig tiefer Call-Graph über alle denkbaren
Runtime-Bindings ist nicht versprochen.

### NuGet-Pakete

NuGet-Pakete sind in diesem Modell kein eigener Analysepfad, sondern normale externe
Assemblies. Für ein lokal vorhandenes Paket wie `MudBlazor.dll` gilt daher:

- Ohne passenden Eintrag in der globalen Mapping-Datei wird die DLL statisch
  dekompiliert.
- Mit einem expliziten Gitea-Mapping für `MudBlazor.dll` wird die konfigurierte
  Solution geladen und das passende Projekt automatisch über `AssemblyName` ermittelt;
  ein `.csproj` muss nicht gepflegt werden.
- Die `PackageReference` in Projekt A allein ist noch kein Source-Mapping. Sie sagt,
  welche DLL verwendet wird, aber nicht, welches Repository die passende Originalquelle
  liefert.
- Assembly-Identität, Version und Target-Framework dienen zur Plausibilisierung. Passt
  der geladene Gitea-Stand nicht zur DLL oder kann er nicht aktualisiert werden, wird
  nicht still falscher Quellcode verwendet, sondern auf Decompilation bzw. `partial`
  zurückgefallen.
- Referenziert das Paket weitere Assemblies, greift derselbe rekursive Resolver auch
  für diese Abhängigkeiten. Ein NuGet-Paket kann damit auf eine weitere source-backed
  oder dekompilierte externe Quelle zeigen.

Der NuGet-Paketname muss für das Grundmapping nicht separat gepflegt werden; maßgeblich
ist die tatsächlich analysierte Assembly. Nur wenn gleiche AssemblyNames aus mehreren
Versionen oder Repositories unterschieden werden müssen, sind zusätzliche optionale
Selektoren wie Version oder Target-Framework sinnvoll.

## Staleness und atomarer Session-Wechsel

Die Assembly-Session benötigt eine eigene Variante des bestehenden Datei-Zustands:

```csharp
internal sealed record AssemblyFileState(
    DateTime MtimeUtc,
    long Length,
    string Sha256,
    string DecompilerVersion,
    string CacheKey);
```

Beim Zugriff:

1. Existenz und mtime/Größe der DLL prüfen.
2. Bei unveränderten Vorwerten die residente `Solution` verwenden.
3. Bei Änderungen den SHA-256 neu berechnen.
4. Bei gleichem Hash nur den bekannten Zustand aktualisieren.
5. Bei neuem Hash eine neue Cache-/Roslyn-Generation bauen.
6. Erst nach vollständigem Aufbau die neue Generation unter einem Lock veröffentlichen.

Laufende Leases dürfen auf der alten Generation fertig werden. Eine halbfertige
Decompilation darf niemals in die von anderen Tool-Aufrufen sichtbare `Solution`
gelangen. Bei einem Refresh-Fehler darf ein veralteter externer Source-Snapshot nicht
still als aktuell verwendet werden; die Antwort trägt den Fehler sichtbar und kann auf
die Decompilation der angefragten DLL zurückfallen. Der letzte gute Snapshot darf für
Diagnosezwecke erhalten bleiben.

## Roslyn-Parität und Toolsemantik

Der gemeinsame Kern soll für beide Target-Arten dieselben `Solution`-/`Project`-/
`Document`-/`SemanticModel`-Pipelines verwenden. Die Bedeutung einzelner Tools hat
aber abhängig von der Quellenherkunft eine explizite Grenze. Für jedes bestehende Tool
wird im Umsetzungsplan festgehalten, ob es für Projektquellen, externe Source-Solutions
und externe dekompilierte Assemblies unterstützt, eingeschränkt unterstützt oder
bewusst nicht umgesetzt wird. „Alle Features“ bedeutet damit vollständige Abdeckung
der sinnvollen Fälle, nicht künstliche Gleichheit für fehlende Git-, Test- oder
Consumer-Kontexte:

| Toolgruppe | aktuelles `project` | externe Source-Solution | externe dekompilierte Assembly |
|---|---|---|---|
| `find_symbol`, Skeleton, Klassenstruktur | vollständige Solution-/Projektansicht | Source-Dokumente der gematchten vollständigen Solution, mit ausgewähltem Assembly-Projekt | dekompilierte Typen und Member, inklusive interner Typen sofern erzeugt |
| `get_symbol_body` | Original-Source-Body | Original-Source-Body mit Source-Origin | dekompilierter Body mit Origin-Hinweis |
| `find_references`, `get_call_tree` | Solution-Graph | Graph der verfügbaren Source-Solution einschließlich auflösbarer externer Ziele | Graph der dekompilierten Assembly einschließlich auflösbarer externer Source-/Decompilation-Nodes |
| `dependency_graph` | Projekt-/Dateiabhängigkeiten | Abhängigkeiten der Source-Solution und ihrer aufgelösten externen Referenzen | dekompilierte Dokumente und rekursiv aufgelöste Assembly-Referenzen |
| Metriken | Quellcode-Documents | Source-Dokumente, als `origin=source-backed` markiert | dekompilierte Documents, als `origin=decompiled` markiert |
| `get_violations`, `pattern_detect` | Projektregeln aus der Projektconfig | nur, wenn Regeln/Config der Source-Solution belastbar verfügbar sind | nur mit explizitem Assembly-Regelprofil; sonst bewusst nicht umgesetzt |
| Testkontext | Tests der aktuellen Solution | Test-Dokumente dürfen mit der Solution geladen werden; Tests des externen Projekts werden nicht automatisch ausgeführt | nicht erforderlich und standardmäßig nicht verfügbar |
| Git-/Change-Impact | Git-Diff und Solution-Kontext | bewusst nicht umgesetzt für externe Quellen | bewusst nicht umgesetzt |
| `safeguard` | Quality Gate der aktuellen Solution | nur bei explizitem, später festzulegendem externem Scanvertrag | nur bei explizitem Assembly-Scanvertrag; sonst bewusst nicht umgesetzt |

„Gleich funktionieren“ bedeutet damit: Die Kernabfragen verwenden denselben Roslyn-
Codepfad und dieselben Symbol-/Result-Modelle. Es bedeutet nicht, dass eine externe
DLL nachträglich Git-Historie, Tests oder ursprüngliche Projektregeln besitzt.

### Symbolidentität und Origin

Ein Pfad in einem Cacheordner ist kein ausreichender stabiler Symbolidentifikator.
Assembly-Symbole benötigen deshalb eine Target-/Generationsherkunft, beispielsweise:

```text
assembly:<sha256>:T:Vendor.BelegService
assembly:<sha256>:M:Vendor.BelegService.Save
```

Die bestehende Roslyn-DocumentationCommentId kann weiterhin als lokaler Symbol-
Identifikator dienen, muss aber zusammen mit Target-Identität oder Assembly-Origin
interpretiert werden. Antworten sollen mindestens diese Metadaten liefern:

```json
{
  "origin": {
    "kind": "decompiled",
    "sourcePath": "D:/Vendor/bar.dll",
    "contentHash": "8f34...",
    "generatedPath": ".../source/Vendor/BelegService.cs",
    "confidence": "medium"
  }
}
```

Wenn ein Tool dekompilierten Code oder einen dekompilierten Body ausgibt, enthält die
Antwort zusätzlich einen kurzen, standardisierten Hinweis, beispielsweise:

> Hinweis: Der angeforderte Code wurde dekompiliert und kann von der Originalquelle
> abweichen.

Der Hinweis ist für den Agenten relevant, damit er den Code als Nachschlagehilfe und
nicht als behauptete Originalquelle behandelt. Die maschinenlesbare `origin`-Struktur
bleibt trotzdem maßgeblich für Folgeabfragen und Automatisierung. So kann ein Agent
erkennen, dass ein Body dekompiliert wurde, und bei einer später geänderten DLL nicht
blind eine alte Symbol-ID wiederverwenden.

## Umgang mit den bestehenden Assembly-Tools

`inspect_assembly` und `find_assembly_extensions` werden im harten Schnitt nicht als
separate metadata-only Parallelwelt erhalten. Sie verwenden künftig den gemeinsamen
Assembly-Session-Dispatcher und den dekompilierten Roslyn-Graph.

Für `inspect_assembly` bleibt eine assembly-spezifische API sinnvoll, wenn sie die
Assembly-Identität, Referenzen und den öffentlichen API-Überblick kompakt ausgibt. Sie
verwendet dann dieselbe Session wie `find_symbol` und `get_class_structure`.

Der heutige optionale Consumer-`projectRoot`-Pfad von `find_assembly_extensions` wird
nicht als versteckter zweiter Target-Kontext weitergeführt. Ohne Consumer-Target kann
die Assembly ihre Extensions und Signaturen liefern; die konkrete Anwendbarkeit für
einen Receiver ist nur dann entscheidbar, wenn der benötigte Receiver-/Referenzkontext
in der Assembly-Session vorhanden ist. Eine spätere Frage „wo wird diese Extension in
Projekt X verwendet?“ ist eine explizite Cross-Target-Abfrage und kein Grund, jedes
Tool mit zwei unklaren Roots zu versehen.

## Fehler-, Sicherheits- und Vertrauensvertrag

### Keine Codeausführung

- Keine `Assembly.Load`, kein `AssemblyLoadContext`, keine Reflection-Ausführung.
- PEReader, ILSpy-Decompiler und Roslyn-MetadataReferences arbeiten statisch.
- DLL, PDB, `.deps.json` und benachbarte Referenzen gelten als untrusted input.
- Decompiler-Aufruf erhält Cancellation, Timeout und Größen-/Komplexitätsgrenzen.
- Parser-/Decompiler-Ausnahmen werden in einen kontrollierten Sessionfehler oder einen
  partiellen Zustand übersetzt.

### Vertrauensstufen

Die Antwort sollte zwischen folgenden Fällen unterscheiden:

```text
complete:        Decompilation und benötigte Referenzen erfolgreich
partial:         C# erzeugt, aber Referenzen oder Teilbereiche fehlen
degraded:        letzter guter Stand nach fehlgeschlagenem Refresh
failed:          kein analysierbarer Roslyn-Stand vorhanden
```

Die Sprache in Toolbeschreibungen und Antworten muss klar zwischen „dekompiliert“ und
„Originalquelle“ unterscheiden. Bei transformiertem Compiler-Code, Obfuscation,
fehlenden PDBs oder dynamischer Reflection können Aussagen unvollständig sein.

## Vorgeschlagene Code-Struktur

```text
src/AiNetLinter/Mcp/Analysis/
├── AnalysisTarget.cs
├── AnalysisTargetResolver.cs
├── AnalysisRegistry.cs
├── AnalysisLease.cs
├── AnalysisSessionState.cs
└── AnalysisToolCall.cs

src/AiNetLinter/Mcp/Projects/
├── ProjectAnalysisSession.cs       (Adapter um den bestehenden Project-Server)
└── ProjectSessionFactory.cs

src/AiNetLinter/Mcp/Assemblies/
├── AssemblyAnalysisSession.cs
├── AssemblySessionFactory.cs
├── AssemblySourceResolver.cs
├── AssemblySourceMatch.cs
├── SourceRepositoryRegistry.cs
├── SourceSnapshotCache.cs
├── AssemblyFingerprint.cs
├── AssemblyDecompilationCache.cs
├── AssemblyDecompilationAdapter.cs
├── AssemblyReferenceResolver.cs
├── AssemblyOriginMap.cs
└── AssemblySessionHealth.cs
```

Die endgültige Ordnerstruktur darf an die aktuelle Toolorganisation angepasst werden.
Die fachlichen Grenzen sollen aber erhalten bleiben:

- Target-/Lease-/Registry-Code kennt keine ILSpy-Details.
- Source-/Repository-Auflösung kennt keine MCP-Toolnamen und liefert nur eine
  verifizierte Quelle oder einen begründeten Fallback.
- Decompiler- und Cache-Code kennt keine Toolnamen wie `find_symbol`.
- Roslyn-Scanner erhalten eine gemeinsame Session-/Solution-Sicht.
- Assembly-Health und Cache-Warnungen werden nicht als normale Lint-Violations
  missbraucht.

## MCP-Registrierung

Statt der aktuellen Form:

```csharp
async (string projectRoot, ...) =>
    await ProjectToolCall.ExecuteAsync(registry, projectRoot, ...)
```

werden die Registrierungen gleichförmig:

```csharp
async (
    string targetType,
    string targetPath,
    string? symbolIdentifier = null,
    CancellationToken ct = default) =>
    await AnalysisToolCall.ExecuteAsync(
        registry,
        new AnalysisTargetRequest(targetType, targetPath),
        lease => FindReferencesTool.ExecuteAsync(
            lease.Session,
            symbolIdentifier,
            ct))
```

Die Registrierungsbeschreibungen müssen den Agenten ausdrücklich sagen:

- `targetType=project` für eine Source-Solution;
- `targetType=assembly` für eine einzelne DLL;
- für Assemblys ist keine `ainetlinter.project.json` erforderlich;
- eine Assembly kann bei verifiziertem Match aus zugehörigem Quellcode oder sonst aus
  einer Dekompilation analysiert werden;
- der Agent soll bei einer spontan entdeckten DLL direkt den Assembly-Target-Aufruf
  verwenden;
- Ergebnisse müssen ihre Quellenherkunft (`source-backed` oder `decompiled`) und bei
  Bedarf den Vertrauenszustand ausweisen.

## Implementierungsplan

### Phase 1: Einheitlicher Target-Vertrag und Projektregression

- `targetType`/`targetPath` als einheitlichen MCP-Target-Vertrag einführen und den
  bisherigen Projektpfad fachlich unverändert weiterführen.
- Den bestehenden Registry-/Lease-Lifecycle wiederverwenden, ohne Projekt- und
  Assembly-Semantik in einer unklaren Universal-Session zu vermischen.
- Alle betroffenen Toolregistrierungen und bestehenden Projekt-E2E-Tests auf den
  neuen Vertrag umstellen.

### Phase 2: Nutzbarer Decompilationspfad für unbekannte DLLs

- Eine residente Assembly-Session mit Fingerprint, Decompilation, Referenzdiagnosen,
  Origin-Mapping und atomarem Refresh ergänzen.
- Unbekannte DLLs ohne `ainetlinter.project.json` als synthetisches Roslyn-Project
  bereitstellen; ein fehlender Source-Match ist dabei der normale Fallback.
- Zuerst die Nachschlagefunktionen für Symbole, Struktur, Bodies, Referenzen,
  Call-Trees, Dependency-Graph und Metriken anbinden.
- Referenzen wie `foo.dll -> bar.dll` rekursiv und bedarfsgesteuert als weitere
  Source-/Decompilation-Sessions auflösen; bereits geladene Quellen deduplizieren und
  fehlende Ziele als `partial`/External-Nodes ausweisen.
- `inspect_assembly` und `find_assembly_extensions` in denselben Sessionpfad
  überführen, ohne ihren sinnvollen Assembly-spezifischen Output zu verlieren.

### Phase 3: Explizite Source-Solutions und wartungsarmes Mapping

- Eine globale Mapping-Datei mit konfigurierbarem Pfad festlegen; `ainetlinter.project.json`
  bleibt auf den aktuellen Projektkontext beschränkt.
- Pro Repository nur URL, Solution-Pfad und ein `assemblies`-Array mit DLL-/Assembly-
  Namen pflegen. Die enthaltenen `.csproj`-Pfade werden aus `.sln`/`.slnx` gelesen und
  nicht redundant konfiguriert.
- Eine vollständige, beim Laden intern versionierte Source-Solution als wiederverwendbaren
  Snapshot materialisieren und das ausgewählte Assembly-Projekt darin kenntlich machen;
  der tatsächlich geladene Commit bleibt interner Versionsnachweis.
- Direkte Assembly-Targets und Nachschlageaufrufe aus Projekt A auf denselben
  Source-Snapshot führen, ohne Projekt A zum externen Änderungscontext zu machen.

### Phase 4: Gitea als konfigurierte Source-of-Truth

- Gitea nur über explizite Repository-/Solution-/Assembly-Mappings verwenden; das
  Source-Projekt wird aus der Solution abgeleitet. Keine Discovery und kein Klonen
  anhand von DLL-Namen.
- Beim ersten Zugriff immer gegen den konfigurierten Standard-Branch aktualisieren;
  den tatsächlich geladenen Commit intern als Snapshot-Identität speichern. Bei
  weiteren Zugriffen nur innerhalb eines gültigen Refresh-Intervalls ohne erneute
  Aktualisierung arbeiten.
- Authentifizierung, Clone-Ziel, Cancellation, beschädigte Snapshots und fehlenden
  Netzwerkzugriff als sichtbare Zustände mit Decompilations-Fallback behandeln.
- Einen lokalen Dirty-/unbuilt-Checkout nicht still als alternative Source-of-Truth
  verwenden; ein späterer Local-Checkout-Modus bleibt eine separate Erweiterung.

### Phase 5: Vollständige sinnvolle Tool-Abdeckung

- Für jedes bestehende MCP-Tool die Capability-Matrix aus diesem Konzept umsetzen.
- Für externe Quellen ohne sinnvollen Kontext, insbesondere Git-Diff, keine
  Scheinimplementierung bauen; stattdessen einen dokumentierten Nicht-Umsetzungs-
  Vertrag verwenden.
- Regeln, Violations, Audits, Testkontext und Quality Gates nur dort aktivieren, wo
  Source-/Regelkontext fachlich belastbar vorhanden ist.

### Phase 6: Dokumentation und Abschlussverifikation

- `Docs/agent-api.md`, `Docs/integration.md`, `Docs/configuration.md`, `README.md`,
  `Docs/ROADMAP.md` und ggf. `Docs/rationale.md` gegen den implementierten Vertrag
  aktualisieren.
- Besonders `.agents/rules/AiNetLinter-McpWorkflow.mdc` mit dem neuen Entscheidungs-
  und Arbeitsablauf für `project`, externe Source-Solution und dekompilierte Assembly
  aktualisieren; widersprüchliche Legacy-Hinweise dürfen nicht stehen bleiben.
- Toolbeschreibungen und MCP-Bootstrap auf `targetType`/`targetPath` aktualisieren.
- Cache-, Sicherheits- und Vertrauensgrenzen dokumentieren.
- Vollständige nicht-Stress-Testläufe beider Zieltestprojekte sowie `dotnet build`
  ausführen.
- Vor Abschluss des größeren Tasks den projektinternen DRY-/Drift-Audit ausführen.

## Teststrategie für die spätere Umsetzung

### Unit-/Component-Tests

- Target-Parsing: gültige und ungültige Kombinationen, absoluter Pfad, falsche DLL-
  Extension, Verzeichnis als Assembly-Target.
- `project` lädt weiterhin die Definitionsdatei; `assembly` verlangt sie nicht.
- Source-Match: explizites Repository-/Solution-/Assembly-Mapping, mehrdeutiger
  Repositoryeintrag, fehlender Treffer und korrekter Fallback auf Decompilation.
- Direkter `core.dll`-Aufruf und die Auflösung derselben DLL aus `projektA` verwenden
  denselben Source-Snapshot und materialisieren Core nicht doppelt.
- Der Pfad der globalen Mapping-Datei wird aus `appsettings.json` gelesen; die
  Mapping-Einträge benötigen keine redundanten `.csproj`-Pfade.
- Ein NuGet-Assembly ohne Mapping wird dekompiliert; mit explizitem Gitea-Mapping wird
  die passende Source-Solution verwendet. Eine bloße `PackageReference` reicht nicht
  als Mapping.
- Ein Repository-/Solution-Eintrag ohne eindeutige Auflösung eines `AssemblyName` wird
  nicht still als Originalquelle behandelt; der intern geladene Source-Stand wird
  gespeichert.
- Ein Repository mit mehreren DLL-Projekten löst zwei konfigurierte Assemblys auf die
  richtigen unterschiedlichen Source-Projekte derselben Solution auf.
- Eine transitive Referenz `foo.dll -> bar.dll` wird erkannt und `bar.dll` als Source-
  oder Decompilation-Quelle nachgeladen; bereits geladene Quellen werden dedupliziert.
- Externe Source-Snapshots und dekompilierte Documents bleiben read-only; kein Tool
  verändert die externe Quelle oder die untersuchte DLL.
- Fingerprint erkennt DLL-Änderung, mtime-only Änderung und identische Bytes.
- Cache-Key ändert sich bei Decompiler-/Schema-Version, nicht bei bloßer Cache-Lesung.
- Source-Snapshot-Key unterscheidet Repository-/geladene Revisionsstände derselben
  Assembly.
- Manifest wird bei Teilfehlern nicht als vollständiger Eintrag adoptiert.
- Decompiler-Warnungen führen zu `partial`, nicht zu stillen Leertreffern.
- Synthetisches Roslyn-Project liefert SyntaxTree, SemanticModel und Symbolauflösung.
- Origin-Mapping verweist auf DLL, Hash und generierten Pfad.
- Keine Testausführung lädt die untersuchte DLL in den Prozess.

### Integration-/MCP-Tests

- Ein unbekannter absoluter DLL-Pfad kann ohne `ainetlinter.project.json` per MCP
  analysiert werden.
- Eine bekannte eigene DLL verwendet bei verifiziertem Match den passenden Quellcode
  und dekompiliert nicht.
- Mehrere Projekte können eine gemeinsame Core-Quelle referenzieren, ohne die
  Source-Version eines anderen Projekts zu überschreiben.
- Direkte DLL-Targets und Nachschlageaufrufe aus `projektA` teilen bei identischer
  Source-Snapshot-Identität die zugrunde liegende Source-/Document-Repräsentation.
- Ein konfiguriertes Gitea-Repository wird beim ersten Zugriff bzw. nach Ablauf des
  Refresh-Intervalls aktualisiert und als Source-Snapshot bereitgestellt.
- Zwei parallele Erstaufrufe erzeugen nur eine Assembly-Session bzw. eine gemeinsame
  Source-Solution-Session.
- Ein zweiter Aufruf verwendet den residenten Workspace und dekompiliert nicht erneut.
- Eine geänderte DLL erzeugt eine neue Generation; laufende alte Leases bleiben gültig.
- Assembly-Session kann per TTL/LRU entfernt und aus dem Cache erneut aufgebaut werden.
- Externe Sessions werden nicht auf `MaxProjects` angerechnet; Health listet Projekt-,
  Source-Solution- und Assembly-Sessions getrennt.
- Tool-Responses markieren dekompilierte Herkunft, enthalten den kurzen Decompilation-
  Hinweis und zeigen partielle Referenzen.
- Die Capability-Matrix ist für alle bestehenden Tools umgesetzt; fachlich nicht
  sinnvolle Fälle wie Git-Diff auf externe Quellen liefern einen dokumentierten
  Nicht-Umsetzungs-Vertrag statt einer Scheinanalyse.
- Projekt-Tools mit `targetType=project` und Assembly-Tools mit `targetType=assembly`
  werden über denselben MCP-Dispatch verifiziert.
- Bestehende Stress-Tests bleiben ausdrücklich außerhalb der normalen Abschlussläufe.

## Definition of Done

Der Task ist fachlich erfüllt, wenn:

- ein Agent eine unbekannte `bar.dll` direkt als `targetType=assembly` adressieren kann;
- dafür keine Projektdefinition und keine manuelle Cachepflege für den normalen Zugriff
  erforderlich ist; das Löschen alter Cache-Einträge bleibt manuell;
- eine Assembly bei nachgewiesenem Match aus dem zugehörigen Quellcode und sonst aus
  einer statischen Dekompilation analysiert wird;
- NuGet-Assemblies ohne Mapping dekompiliert und mit explizitem Gitea-Mapping wie jede
  andere externe Assembly source-backed analysiert werden;
- ein Repository mit mehreren DLL-Projekten über konkrete Source-Projekte und
  Versionen eindeutig aufgelöst werden kann, wobei die Projektpfade aus der Solution
  stammen und nicht separat gepflegt werden;
- transitive Referenzen wie `foo.dll -> bar.dll` erkannt und als weitere Source- oder
  Decompilation-Quelle nachgeladen werden können;
- identische Source-Snapshots über mehrere Target-Aliase nur einmal materialisiert
  werden, während der Arbeitscontext von Projekt A getrennt bleibt;
- externe Source-Snapshots und dekompilierte Documents read-only bleiben;
- Assembly-/Repository-Matches die konkrete Source-Projekt- und intern geladene
  Revisionsidentität berücksichtigen und ihre Evidenz offenlegen;
- dieselben zentralen Roslyn-Funktionen für Project- und Assembly-Targets arbeiten;
- die DLL statisch dekompiliert und als residenter Roslyn-Workspace verfügbar ist;
- wiederholte Aufrufe Cache und Workspace verwenden;
- DLL-Änderungen über Fingerprint erkannt und atomar als neue Generation geladen werden;
- fehlende Abhängigkeiten und Decompilergrenzen sichtbar statt verschluckt werden;
- keine untersuchte DLL geladen oder ausgeführt wird;
- Project- und Assembly-Sessions getrennte Lebensdauer-/Kapazitätsbudgets besitzen;
- externe Sessions nicht gegen die `MaxProjects`-Grenze zählen;
- Symbol-, Body-, Referenz- und Call-Tree-Antworten ihre dekompilierte Herkunft erkennen
  lassen;
- die Capability-Matrix alle sinnvollen Tool-/Quellenkombinationen abdeckt und
  fachlich unpassende Kombinationen ausdrücklich als nicht umgesetzt dokumentiert sind;
- der harte MCP-Vertrag vollständig dokumentiert, getestet und ohne Legacy-Parameter
  implementiert ist.

## Risiken und Leitplanken

- **Decompiler ist nicht Wahrheit:** Jede Verhaltensaussage aus C# muss als
  dekompiliert/partiell kenntlich bleiben. Für niedrige Ebenen muss IL als ergänzende
  Origin- oder Diagnoseansicht verfügbar bleiben.
- **Abhängigkeitsauflösung:** Ohne Consumer-Projekt können Typen fehlen. Das ist ein
  sichtbarer partieller Zustand und kein Anlass für stillschweigende Fallback-Symbole.
- **Source-Mismatch:** Ein falscher, aber plausibel benannter Checkout kann genauer
  aussehen als eine Dekompilation und trotzdem nicht zur DLL gehören. Match-Evidenz,
  Commit-/Snapshot-Identität und ein konservativer Fallback sind daher erforderlich.
- **Gemeinsame Assemblies:** Ein globales Mapping für `core.dll` darf verschiedene
  Versionen nicht zusammenführen. Repository und Source-Snapshot müssen getrennt
  versionierbar sein.
- **Gitea-Abhängigkeit:** Klonen benötigt Netzwerk, Authentifizierung und einen
  kontrollierten lokalen Cache. Innerhalb eines gültigen Refresh-Intervalls darf ein
  vollständiger Snapshot aus dem Cache verwendet werden. Nach einem fehlgeschlagenen
  fälligen Refresh darf kein alter Stand still als aktuell gelten; dann bleibt die
  Decompilation der deterministische Fallback.
- **Speicherverbrauch:** Decompilation plus Roslyn-SemanticModels kann bei vielen DLLs
  teuer sein. Separate Assembly-Limits, TTL, LRU und Cache-Reuse sind Pflicht.
- **Problematische Eingaben:** Obfuskierte, beschädigte oder sehr große Assemblys
  benötigen Timeout, Cancellation und kontrollierte Fehlerpfade.
- **Linter-Regeln:** Projektregeln und Tests sind nicht automatisch Eigenschaften einer
  standalone DLL. Regel- und Quality-Gate-Tools brauchen einen expliziten Vertrag.
- **Symbolstabilität:** Cachepfade und Zeilennummern ändern sich bei neuer Decompilation.
  Target-Hash und Origin müssen deshalb Teil der Folgeabfrage-Semantik sein.
- **Architekturdrift:** Die gemeinsame Session-Oberfläche soll klein bleiben. Sie darf
  nicht zu einer neuen Plugin-Architektur oder einem dynamischen Ladeframework werden.

## Offene Punkte

- Welche ILSpy-/ICSharpCode.Decompiler-Version und welche vorhandene manuelle
  Decompiler-Logik wird verwendet?
- Wie sieht das verbindliche globale Mapping-Schema einschließlich Gitea-
  Authentifizierung, Refresh-Intervall und Cachepfad aus?
- Soll ein lokaler Checkout später als ausdrücklich aktivierbarer Sondermodus neben
  Gitea unterstützt werden, oder bleibt Gitea dauerhaft die einzige Source-of-Truth?
- Wird bei einer gematchten vollständigen Source-Solution standardmäßig solutionweit
  oder nur im ausgewählten Source-Projekt gesucht?
- Welche Regel-/Audit-Tools erhalten für externe Source-Solutions bzw. dekompilierte
  Assemblies einen sinnvollen Vertrag und welche bleiben bewusst nicht umgesetzt?
