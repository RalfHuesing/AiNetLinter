---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: epic
rules_dir: .agents/rules
last_updated: 2026-08-26
open_questions:
  - ILSpy-/ICSharpCode.Decompiler-Version und Wiederverwendung des vorhandenen manuellen Decompilers
  - Exakter Regel-/Config-Vertrag fuer Lint-Tools auf dekompilierten Assemblies
  - Verbindliches Modell fuer Assembly-zu-Quellcode-Matches inklusive Versions-/Commit-Nachweis
  - Aufteilung zwischen projektlokaler Konfiguration und globalem Source-/Repository-Register
  - Gitea-Discovery, Klonen, Authentifizierung und Cache-Lebenszyklus fuer bekannte Quellcode-Repositories
  - Identitaet und Freigabepolicy fuer gemeinsame Source-Snapshots bei dirty/unbuilt Arbeitsstaenden
  - Alias-/Lease-Modell fuer direkte DLL-Targets, Projektabhaengigkeiten und gemeinsame Roslyn-Snapshots
---

# Konzept: Einheitliches Roslyn-Analyseziel für Projekte und dekompilierte Assemblies

## Intention

AiNetLinter soll eine lokale .NET-DLL für die semantische Analyse so behandeln können
wie das Quellcodeprojekt, aus dem sie entstanden ist. Bei einem Quellcodeprojekt lädt
AiNetLinter die vorhandene Solution und ihre Documents. Bei einer Assembly soll zuerst
geprüft werden, ob verlässlich zugehöriger Quellcode lokal oder aus einem bekannten
Repository verfügbar ist. Nur wenn kein passender Quellcode gefunden oder dessen
Zuordnung nicht ausreichend belegt werden kann, wird der Quellcode transparent aus der
DLL dekompiliert, in einem synthetischen Roslyn-Project materialisiert und anschließend
über dieselben Roslyn-Funktionen analysiert.

Für einen Agenten soll der Ablauf dadurch direkt sein:

1. Der Agent entdeckt beispielsweise `bar.dll`.
2. Er ruft ein beliebiges passendes Roslyn-Tool mit dieser DLL als Analyseziel auf.
3. AiNetLinter löst zuerst eine passende Quellcodequelle auf und verwendet nur bei
   fehlendem belastbarem Match transparent eine dekompilierte Roslyn-Session.
4. Weitere Fragen zu Symbolen, Bodies, Referenzen, Aufrufbäumen und Metriken verwenden
   dieselbe residente Session.

Eine Assembly benötigt dafür **keine** `ainetlinter.project.json`. Diese Datei bleibt
der primäre Konfigurationsvertrag für ein `project`-Target, kann aber optional lokale
Source-Zuordnungen überschreiben. Die Assembly-Analyse muss auch für DLLs aus
unbekannten Projekten und außerhalb eines bekannten Repositorys funktionieren. Eine
optionale projektlokale oder globale Source-Zuordnung darf die Analyse einer bekannten
eigenen DLL verbessern, ist aber keine Voraussetzung für den Dekompilations-Fallback.

Das Ziel ist nicht, den ursprünglichen Quellcode zu behaupten. Ergebnisse aus einer
Dekompilation müssen als solche erkennbar sein und bei unvollständiger
Abhängigkeitsauflösung oder problematischer Decompilation einen partiellen Zustand
melden.

## Draft-Ergänzung: Drei Quellen, ein Roslyn-/MCP-Kern

Die wichtige architektonische Trennung ist nicht „Projektanalyse oder
Assemblyanalyse“, sondern **Herkunft der Analysequelle** vor dem gemeinsamen
Roslyn-Kern. Für die Analyse gibt es drei relevante Eingangspfade:

1. **Quellcode des aktuell analysierten Projekts:** Der MCP erhält einen
   Projektroot, lädt die konfigurierte Solution und verwendet deren Documents.
2. **Bekannter Quellcode zu einer Assembly:** Die DLL ist das Analyseziel, aber ihre
   passende Source-Solution oder ihr passendes Repository ist bekannt. In diesem Fall
   wird der Quellcode verwendet; eine Dekompilation wäre für diesen Pfad unnötig und
   möglicherweise irreführend.
3. **Assembly ohne belastbaren Source-Match:** Die DLL wird statisch dekompiliert und
   als synthetisches Roslyn-Project bereitgestellt.

„Fremde DLL“ bedeutet dabei zunächst nur „liegt außerhalb des aktuell analysierten
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

### Analyseziel und Quellenherkunft getrennt halten

`targetType=assembly` sollte deshalb nicht mehr implizit „immer dekompilieren“
bedeuten. Es bezeichnet zunächst nur das Artefakt, zu dem Antworten erwartet werden.
Ein vorgeschalteter `AssemblySourceResolver` entscheidet anhand von Konfiguration,
lokalen Quellen, Assembly-Metadaten und optional Gitea, ob daraus eine
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

- **Starke Signale:** nachweisbare SourceLink-/Repository-Metadaten, ein passender
  Commit oder Tag, ein zur DLL passender PDB-/Build-Bezug sowie ein expliziter
  Mapping-Eintrag.
- **Mittlere Signale:** `AssemblyName`/Projekt-`AssemblyName`, Ziel-Framework,
  Projektpfad, Output-Verzeichnis und weitere Build-Metadaten stimmen überein.
- **Schwache Signale:** Repositoryname, DLL-Dateiname oder Namenskonventionen wie
  `Core.dll` und `Core` stimmen überein.

Die Quellcodeversion ist dabei mindestens so wichtig wie der Repository-Match. Ein
aktueller Checkout von `core` kann semantisch von der DLL abweichen, die ein anderes
Projekt tatsächlich verwendet. Die interne Identität eines Source-Matches sollte
deshalb eher `Repository + Commit/Tag + Projekt + AssemblyName` sein als nur ein
Verzeichnisname. Falls nur ein schwacher Kandidat existiert, sollte AiNetLinter nicht
still den Quellcode als Originalquelle ausgeben: Entweder verlangt die gewählte
Policy eine explizite Bestätigung, oder es wird auf Dekompilation zurückgefallen.

### Lokale Quellen, Gitea und Konfigurationsvarianten

Grundsätzlich kommen drei Konfigurationsorte infrage:

1. **Projektlokale Definition:** `ainetlinter.project.json` kann Overrides und
   projektspezifische Zuordnungen enthalten. Das ist verständlich, führt aber bei N
   Projekten und gemeinsam genutzten X DLLs schnell zu Wiederholungen.
2. **Globales Source-/Repository-Register:** Eine globale Konfiguration kann
   Repository-URLs, lokale Checkouts, Gitea-Repositories, zulässige Clone-Ziele und
   Assembly-/Projekt-Mappings einmalig beschreiben. Mehrere Projektroots können dann
   auf dieselbe `core`-Quelle verweisen.
3. **Automatische Ermittlung:** Lokale Output-Strukturen, PDB-/SourceLink-Daten und
   Assembly-Metadaten können Kandidaten liefern. Ein Gitea-Klon sollte nur erfolgen,
   wenn ein kanonisches Repository oder ein expliziter Mapping-Eintrag bekannt ist;
   ein blindes Suchen oder Klonen anhand eines DLL-Namens wäre nicht deterministisch.

Als Arbeitsrichtung bietet sich daher eine **Schichtung** an: automatische und
explizite Treffer werden zuerst geprüft, projektlokale Regeln überschreiben bei Bedarf
ein globales Register, und das globale Register hält die wiederverwendbaren
Repository-/Gitea-Beziehungen. Der genaue Dateiname, das Schema und die Priorität sind
noch offen. Wichtig ist, dass die Zuordnung nicht N-mal in den Projekten dupliziert
werden muss.

Für die gemeinsame `core.dll` bedeutet das: Es gibt einen global wiederverwendbaren
Source-Eintrag, auf den mehrere Projekte zeigen können. Verwendet ein Projekt jedoch
eine andere Core-Version, muss die Zuordnung auf einen anderen Commit oder eine andere
Source-Snapshot-Identität zeigen. `repo-name=core` allein darf diese Unterscheidung
nicht verdecken.

### Sparring-Einschätzung

Ich würde die Entscheidung vorerst so festhalten: `targetType` beschreibt das
Analyseziel, eine separate Source-Auflösung beschreibt die Herkunft. Ein globales
Register ist für die N/X-Überlappung die geeignetere Basis; `ainetlinter.project.json`
sollte nur lokale Overrides und Kontext enthalten. Gitea sollte als reproduzierbare
Quelle mit URL und Commit behandelt werden, nicht als freies Suchsystem.

Die sichere Fallback-Regel lautet: **verifizierter Source-Match vor Dekompilation,
sonst Dekompilation**. Ein nicht verifizierter Match ist kein Gewinn, wenn dadurch eine
plausibel aussehende, aber zur DLL nicht passende Quelle analysiert wird. Beide Pfade
enden anschließend in derselben Roslyn-/MCP-Schicht und liefern explizit `origin` und
`confidence` zurück.

## Brainstorming: Gemeinsame DLLs, Buildpfad und Arbeitszustand

Dieser Abschnitt hält den Praxisfall bewusst nur halb fest. Er ist eine
Arbeitsannahme für die nächste Überarbeitung und noch keine abschließende
Konfigurationsentscheidung.

### `projektA` und `core.dll` gleichzeitig bearbeiten

Ein typischer Fall ist:

```text
Agent 1: projektA.exe analysieren oder ändern
Agent 2: core.dll / Core-Quellcode analysieren oder ändern
```

Wenn beide Aufrufe dieselbe Core-Quellversion meinen, sollte `core.dll` im Daemon
nicht zweimal als voneinander unabhängige Roslyn-Welt materialisiert werden. Dafür
reicht ein Registry-Key aus dem DLL-Pfad nicht aus. Zusätzlich braucht es eine
kanonische Source-Snapshot-Identität, beispielsweise:

```text
Repository-URL + Commit/Tag + Source-Project-Pfad + Target-Framework + Source-Hash
```

Die direkte Analyse von `core.dll` und die Auflösung von `core.dll` aus `projektA`
werden dann als zwei Target-Aliase auf denselben Source-Snapshot geführt. Die
Roslyn-Dokumente und SemanticModels der Core-Quelle können geteilt werden. Der
Consumer-Kontext von `projektA` bleibt trotzdem separat, weil dort zusätzlich die
Abhängigkeiten, Konfiguration und Fragen des Projekts A gelten.

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
der DLL im gemeinsamen Buildpfad. Das Konzept sollte diesen Fall nicht durch
Heuristiken „lösen“.

Als praktische Arbeitsregel bietet sich an:

- Der kanonische, zwischen Projekten teilbare Source-Snapshot ist ein sauberer,
  synchronisierter Commit.
- Ein dirty oder nicht gebauter Checkout ist ein lokaler, instabiler Arbeitsstand und
  darf nicht still mit einer DLL oder einer anderen Session vereinigt werden.
- Wenn ein Agent diesen Zustand trotzdem analysiert, muss die Antwort den dirty bzw.
  nicht verifizierten Ursprung sichtbar machen; ein Fehlschlag oder Fallback auf die
  tatsächliche DLL-Decompilation ist akzeptabel.

Damit wird nicht verhindert, dass parallel an `core.dll` gearbeitet wird. Es wird nur
vermieden, dass der Daemon aus einem zufälligen gemeinsamen Buildoutput eine falsche
Identität konstruiert. Für den normalen Arbeitsablauf ist die beabsichtigte Wahrheit:
Core fertigstellen, committen, nach Gitea synchronisieren und diesen Stand aus den
anderen Projekten wiederverwenden.

### Gitea als gemeinsame Wahrheit

Wenn `projektA` eine eigene `foo.dll` referenziert, deren Quellcode lokal gerade nicht
vorhanden ist, kann die Source-Auflösung über das globale Repository-Register den
passenden Gitea-Eintrag verwenden. Der Quellcode wird dann als definierter Snapshot
bereitgestellt und in derselben Source-/Roslyn-Registry wiederverwendet wie bei einer
direkten Analyse von `foo.dll`.

Gitea ist dabei die gemeinsame Quelle für den Quellcode, aber ein Repositoryname
allein beweist noch nicht, welche DLL-Version dazugehört. Nach Möglichkeit müssen
Commit/Tag oder ein anderer reproduzierbarer Versionsbezug mitgeführt werden. Falls
der Arbeitsprozess diese Verbindung bewusst über synchronisierte Commits herstellt,
ist das eine zulässige organisatorische Vereinfachung; technisch sollte die Antwort
die verbleibende Sicherheit trotzdem als `confidence` ausweisen.

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

## Befund im aktuellen AiNetLinter

Der aktuelle MCP-Daemon hält pro kanonisiertem `projectRoot` einen residenten Key im
`ProjectRegistry`. Ein `ProjectLease` schützt den Zugriff; `McpCodeGraphServer` hält
die geladene Roslyn-Solution, den `SourceFileCatalog` und den Staleness-Zustand. Die
Solution wird bei Zugriff über mtime/hash-basierte Dokumentprüfungen inkrementell
aktualisiert. TTL, LRU-Druck, Creation Barriers und In-Flight-Leases sind bereits Teil
des Projektlebenszyklus.

Der aktuelle Assembly-Bereich unter
`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/` arbeitet dagegen anders:

- `AssemblyAnalysisContextFactory` liest PE-Metadaten und baut eine
  `CSharpCompilation` aus MetadataReferences.
- `InspectAssemblyTool` und `FindAssemblyExtensionsTool` erhalten heute
  `assemblyPath` und optional `projectRoot`.
- Die DLL wird metadata-only untersucht; es existiert kein dekompiliertes C#-Document,
  kein langlebiges Assembly-Roslyn-Project und kein Assembly-Eintrag im Daemon-Registry-
  Lifecycle.
- Bei Consumer-Analysen kann der bestehende Code eine Compilation aus einer geladenen
  Solution verwenden. Das ist ein spezieller Pfad, keine allgemeine Target-Abstraktion.

Der neue Task ersetzt diesen Unterschied auf der Session-Ebene. Die vorhandene
metadata-only Auflösung bleibt als Bestandteil der Assembly-Session nützlich, darf
aber nicht länger das primäre Modell für Assembly-Tools sein.

## Scope

### Muss-Haben

- Harter MCP-Vertrag mit `targetType` und `targetPath` für alle Roslyn-/Projekt-Tools.
- Klare Trennung zwischen Analyseziel (`project`/`assembly`) und Quellenherkunft
  (Projektquelle, bekannter Assembly-Quellcode oder Dekompilation).
- `targetType=project` routet zur bestehenden Solution-/Projekt-Session.
- `targetType=assembly` routet zu einer langlebigen Assembly-Session mit vorgelagerter
  Source-Auflösung und Dekompilations-Fallback.
- Deterministischer Source-Match mit Assembly-/Projektidentität, Quellcodeversion und
  sichtbarer Match-Confidence.
- Gemeinsame Source-Snapshot-Registry, damit mehrere Target-Aliase denselben
  verifizierten Quellstand nur einmal als Roslyn-Quelle materialisieren.
- Keine Pflicht zur Änderung oder Anlage einer `ainetlinter.project.json` für eine DLL;
  optionale Source-Mappings bleiben möglich.
- Statische Decompilation ohne Assembly-Ausführung und ohne `AssemblyLoadContext`.
- Persistenter Decompilation-Cache mit Hash-/Versionsprüfung.
- Synthetisches Roslyn-`Project` in einem residenten `AdhocWorkspace`.
- Roslyn-Documents aus dekompilierten C#-Dateien, vorzugsweise auf Typ-/Modulgranularität.
- MetadataReferences für das Target, seine erreichbaren Abhängigkeiten und die
  benötigten Framework-Assemblies.
- Source-/IL-Origin-Mapping für Diagnose, Symbolidentität und Benutzerantworten.
- Wiederverwendung der gemeinsamen Toollogik für Symbolsuche, Bodies, Struktur,
  Referenzen, Call Trees, Dependency Graph und Metriken.
- Expliziter `complete`-/`partial`-/`degraded`-Vertrag bei fehlenden Referenzen,
  Decompiler-Warnungen oder unvollständigen Ergebnissen.
- Daemon-Health für residente Projekt- und Assembly-Sessions.
- Separate Kapazitätsgrenzen für Projekt-Sessions und Assembly-Sessions.
- Concurrent-Load-, Refresh-, Eviction- und Cache-Korruptions-Tests.

### Bewusst nicht im ersten Umfang

- Rekonstruktion des originalen Buildprozesses oder der ursprünglichen Projektdateien.
- Ausführung von Code aus der DLL.
- Änderung oder Rückschreiben von dekompiliertem Code in die Assembly.
- Automatische Ermittlung eines Consumer-Projekts als versteckter zweiter Target-Kontext.
- Vollständige Wiederherstellung von PDB-, SourceLink- oder Originaldateinamen-Garantien.
- Behauptung, dass der erzeugte C#-Code mit der Originalquelle identisch ist.
- Projektweite Git-Diff-Analysen für eine standalone Assembly.
- Testzuordnung zu Tests, die nicht Bestandteil der Assembly-Session sind.
- Ein allumfassender abstrakter Framework-Layer für beliebige zukünftige Artefakte.

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
`.dll` und benötigt keine Projektdefinition. Eine optionale Source-Konfiguration kann
für ein Assembly-Target zusätzlich einen lokalen Checkout oder ein Gitea-Repository
referenzieren.

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
    string? CommitOrTag,
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

Assemblys sollen nicht die bestehende `MaxProjects`-Grenze verbrauchen. Es braucht
separate Optionen, beispielsweise:

```csharp
internal sealed record AnalysisRegistryOptions(
    int MaxProjects,
    int MaxAssemblies,
    TimeSpan ProjectIdleTtl,
    TimeSpan AssemblyIdleTtl);
```

Ein neuer Assembly-Aufruf darf nicht synchron auf die vollständige Decompilation
blockieren. Wie beim bestehenden Solution-Load wird eine Session resident registriert,
der Load läuft als Task und liefert währenddessen einen stabilen `Loading`-Zustand.
Mehrere gleichzeitige Aufrufe derselben DLL teilen sich denselben Load.

Die Registry für Target-Leases und die Registry für Source-Snapshots sollten dabei
konzeptionell getrennt bleiben. Mehrere Targets können auf denselben
Source-Snapshot-Key zeigen, während ihre Consumer-Kontexte unterschiedliche Leases
erhalten. Für eine source-backed `core.dll` ist das die Grundlage dafür, dass ein
direkter Assembly-Aufruf und die Auflösung aus `projektA` dieselben Core-Documents und
SemanticModels wiederverwenden können.

## Assembly-Session

### Source-Auflösung vor der Dekompilation

Die Assembly-Session liest zunächst die Identität und die Referenzen der DLL und
übergibt das Artefakt an einen `AssemblySourceResolver`. Dieser Resolver kombiniert
explizite Mappings, globale Repository-Einträge, lokale Checkouts und aus der Assembly
ableitbare Metadaten. Er liefert nicht nur einen Pfad, sondern auch die Begründung und
die Versionssicherheit des Treffers.

Ein gültiger `source-backed`-Treffer muss mindestens auf ein konkretes Source-Project
oder eine klar abgrenzbare Source-Solution sowie auf eine nachvollziehbare
Versions-/Commit-Identität zeigen. Ein Treffer auf ein Repository allein reicht nicht,
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
können. Wiederverwendung darf aber nicht zu Versionsverwechslungen führen: Der
Cache-/Snapshot-Schlüssel muss Repository/URL und Commit bzw. Tag enthalten; für
unterschiedliche Core-Versionen entstehen unterschiedliche Source-Snapshots.

### Fingerprint und Cache-Key

Der Dateiname oder die Assembly-Version allein reicht nicht zur Invalidierung. Der
Cache-Key muss mindestens enthalten:

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

Da DLLs außerhalb eines Repositorys liegen können, soll der persistente Cache nicht
ungefragt im fremden Projekt angelegt werden. Standardmäßig eignet sich ein
benutzerbezogener Windows-Cache unterhalb von `%LOCALAPPDATA%\\AiNetLinter`, mit
hashbasierten Unterordnern. Ein späterer expliziter Cache-Root kann additiv konfiguriert
werden.

Beispiel:

```text
%LOCALAPPDATA%\\AiNetLinter\\decompilation\\
  8f34...\\
    manifest.json
    source\\Namespace\\BelegService.cs
    source\\Namespace\\Beleg.cs
    origin.json
```

Das Manifest enthält mindestens:

- Originalpfad, Dateigröße, mtime und SHA-256
- Assembly-Identität und Referenzliste
- Decompiler-Version, Optionen und Cache-Schema-Version
- erzeugte Dateien und Quelltext-Encoding
- Decompiler-Warnungen und Fehler
- verwendete Referenzpfade und nicht auflösbare Referenzen
- Erstellungszeit und letzter Zugriff
- Status `complete`, `partial` oder `failed`

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
gelangen. Bei einem Refresh-Fehler bleibt der letzte gute Stand verfügbar und die
Antwort trägt analog zum bestehenden degradierten Solution-Zustand eine Warnung.

## Roslyn-Parität und Toolsemantik

Der gemeinsame Kern soll für beide Target-Arten dieselben `Solution`-/`Project`-/
`Document`-/`SemanticModel`-Pipelines verwenden. Die Bedeutung einzelner Tools hat
aber eine explizite Grenze:

| Toolgruppe | `project` | `assembly` |
|---|---|---|
| `find_symbol`, Skeleton, Klassenstruktur | vollständige Solution-/Projektansicht | dekompilierte Typen und Member, inklusive interner Typen sofern erzeugt |
| `get_symbol_body` | Original-Source-Body | dekompilierter Body mit Origin-Hinweis |
| `find_references`, `get_call_tree` | Solution-Graph | Graph innerhalb der Assembly; externe Ziele nur als Metadaten-/External-Nodes |
| `dependency_graph` | Projekt-/Dateiabhängigkeiten | dekompilierte Dokumente und Assembly-Referenzen |
| Metriken | Quellcode-Documents | dekompilierte Documents, als `origin=decompiled` markiert |
| `get_violations`, `pattern_detect` | Projektregeln aus der Projektconfig | nur mit explizitem Assembly-Regelprofil oder klar ausgewiesenem Default |
| Testkontext | Tests der geladenen Solution | `not_available`, sofern kein Test-Target Bestandteil der Session ist |
| Git-/Change-Impact | Git-Diff und Solution-Kontext | `not_available` für standalone DLLs; kein erfundener Diff |
| `safeguard` | Quality Gate der Solution | nur, wenn ein definierter Assembly-Scanvertrag existiert |

„Gleich funktionieren“ bedeutet damit: Die Kernabfragen verwenden denselben Roslyn-
Codepfad und dieselben Symbol-/Result-Modelle. Es bedeutet nicht, dass eine DLL
nachträglich Git-Historie, Tests oder ursprüngliche Projektregeln besitzt.

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

So kann ein Agent erkennen, dass ein Body dekompiliert wurde, und bei einer später
geänderten DLL nicht blind eine alte Symbol-ID wiederverwenden.

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

### Schritt 1: Target-Vertrag und zentrale Dispatch-Grenze

- `AnalysisTargetKind`, `AnalysisTargetRequest` und `AnalysisTargetResolver` einführen.
- `targetType`/`targetPath` in allen betroffenen MCP-Tool-Schemas verbindlich machen.
- `projectRoot` aus den Tool-Signaturen entfernen.
- `ProjectToolCall` zu `AnalysisToolCall` umbauen oder als allgemeine Dispatchklasse
  ersetzen.
- Projekt-Target zunächst fachlich unverändert über die bestehende
  `ProjectDefinitionLoader`-/`ProjectRegistry`-Logik laden.
- Alle vorhandenen Wiring-, Schema- und E2E-Tests auf den neuen Vertrag umstellen.

### Schritt 2: Gemeinsame Session-Sicht

- Gemeinsame `IAnalysisSession`-/Lease-Sicht mit minimaler Oberfläche einführen.
- Bestehenden `McpCodeGraphServer` als Project-Session adaptieren.
- Scanner und Resolver, die heute konkret `McpCodeGraphServer` erwarten, auf die
  gemeinsame Session-Sicht oder einen kleinen `AnalysisSessionContext` umstellen.
- Keine doppelte projekt- und assemblyspezifische Implementierung derselben Roslyn-
  Symbol-/Referenzlogik erzeugen.

### Schritt 3: Source-Match, Repository-Register und persistente Snapshots

- Pfad-, Existenz-, DLL- und Fingerprint-Validierung zentralisieren.
- Projektlokale Overrides und ein globales Source-/Repository-Register als Schichtung
  festlegen.
- Assembly-/Projekt-Mappings mit Repository, konkretem Source-Project und
  Commit-/Tag-Identität modellieren.
- Lokale Checkouts wiederverwenden und Gitea-Repositories nur über kanonische,
  konfigurierte Einträge klonen.
- Source-Snapshot-Cache und Decompilation-Cache voneinander unterscheiden.
- SHA-256, Decompiler-Version, Optionen und Cache-Schema in den Cache-Key aufnehmen.
- Repository-/Commit-Identität in den Source-Snapshot-Key aufnehmen.
- Atomare Erstellung, Wiederverwendung, beschädigte Einträge und Cancellation testen.

### Schritt 4: Langlebige Assembly-Session

- `AssemblyAnalysisSession` mit Load-State, Last-Good-State, Diagnostics und Dispose
  implementieren.
- Creation Barrier und Assembly-Lease in die Analysis-Registry integrieren.
- Separate TTL-/LRU-/Kapazitätswerte für Assemblys vorsehen.
- `get_server_health` um Target-Art, Cache-Key, Hash, Decompilerstatus und Warnungen
  erweitern.

### Schritt 5: Source-Auflösung, Decompilation und synthetisches Roslyn-Project

- Verifizierten Source-Match in das passende Source-Project bzw. die Source-Solution
  überführen.
- Bei fehlendem Match den ILSpy-/Decompiler-Adapter mit statischem Resolver und
  Timeout integrieren.
- Decompilierte Documents und Origin-Map erzeugen.
- `AdhocWorkspace`-Project mit deterministischen Parse-/Compilation-Optionen aufbauen.
- Framework- und Assembly-Referenzen mit der vorhandenen metadata-only Logik verbinden.
- Source-Match-, Decompiler- und Compilation-Diagnosen in einen gemeinsamen Status
  sowie `origin`-/`confidence`-Metadaten überführen.

### Schritt 6: Gemeinsame Toolpfade aktivieren

- Zuerst `find_symbol`, `get_file_skeleton`, `get_class_structure`, `get_symbol_body`,
  `find_references`, `get_call_tree`, `dependency_graph` und Metriken auf Assembly-
  Sessions ausführen.
- Bestehende `inspect_assembly`- und `find_assembly_extensions`-Funktionen auf die
  gemeinsame Session umstellen.
- Symbolidentitäten, Generated Paths und Origin-Metadaten in die vorhandenen Result-
  Modelle integrieren.
- Danach gezielt Regeln, Violations, Dead Code und Pattern-Tools für Assemblys
  aktivieren oder mit einem klaren `not_available`-/`context_required`-Vertrag versehen.

### Schritt 7: Dokumentation und Abschlussverifikation

- `Docs/agent-api.md`, `Docs/integration.md`, `Docs/configuration.md`, `README.md`,
  `Docs/ROADMAP.md` und ggf. `Docs/rationale.md` gegen den implementierten Vertrag
  aktualisieren.
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
- Source-Match: exakter Commit-/Projekt-Treffer, mehrdeutiger Repositoryname,
  Versionskonflikt, fehlender Treffer und korrekter Fallback auf Dekompilation.
- Direkter `core.dll`-Aufruf und die Auflösung derselben DLL aus `projektA` verwenden
  denselben Source-Snapshot und materialisieren Core nicht doppelt.
- Projektlokale Overrides und globale Source-Einträge werden mit definierter Priorität
  ausgewertet; mehrere Projektroots können denselben Core-Source-Snapshot verwenden.
- Ein Repository-Treffer ohne konkretes Source-Project oder ohne Versionsnachweis wird
  nicht still als Originalquelle behandelt.
- Fingerprint erkennt DLL-Änderung, mtime-only Änderung und identische Bytes.
- Cache-Key ändert sich bei Decompiler-/Schema-Version, nicht bei bloßer Cache-Lesung.
- Source-Snapshot-Key unterscheidet Repository-/Commit-Versionen derselben Assembly.
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
- Direkte DLL-Targets und abhängige Projekt-Targets teilen bei identischer
  Source-Snapshot-Identität die zugrunde liegende Roslyn-Repräsentation.
- Ein konfiguriertes Gitea-Repository kann reproduzierbar über URL und Commit als
  Source-Snapshot bereitgestellt werden.
- Zwei parallele Erstaufrufe erzeugen nur eine Assembly-Session.
- Ein zweiter Aufruf verwendet den residenten Workspace und dekompiliert nicht erneut.
- Eine geänderte DLL erzeugt eine neue Generation; laufende alte Leases bleiben gültig.
- Assembly-Session kann per TTL/LRU entfernt und aus dem Cache erneut aufgebaut werden.
- Health listet Projekt- und Assembly-Sessions getrennt.
- Tool-Responses markieren dekompilierte Herkunft und partielle Referenzen.
- Projekt-Tools mit `targetType=project` und Assembly-Tools mit `targetType=assembly`
  werden über denselben MCP-Dispatch verifiziert.
- Bestehende Stress-Tests bleiben ausdrücklich außerhalb der normalen Abschlussläufe.

## Definition of Done

Der Task ist fachlich erfüllt, wenn:

- ein Agent eine unbekannte `bar.dll` direkt als `targetType=assembly` adressieren kann;
- dafür keine Projektdefinition und keine manuelle Cachepflege erforderlich ist;
- eine Assembly bei nachgewiesenem Match aus dem zugehörigen Quellcode und sonst aus
  einer statischen Dekompilation analysiert wird;
- identische Source-Snapshots über mehrere Target-Aliase nur einmal materialisiert
  werden, während Consumer-Kontexte getrennt bleiben;
- Assembly-/Repository-Matches die konkrete Source-Projekt- und Versionsidentität
  berücksichtigen und ihre Evidenz offenlegen;
- dieselben zentralen Roslyn-Funktionen für Project- und Assembly-Targets arbeiten;
- die DLL statisch dekompiliert und als residenter Roslyn-Workspace verfügbar ist;
- wiederholte Aufrufe Cache und Workspace verwenden;
- DLL-Änderungen über Fingerprint erkannt und atomar als neue Generation geladen werden;
- fehlende Abhängigkeiten und Decompilergrenzen sichtbar statt verschluckt werden;
- keine untersuchte DLL geladen oder ausgeführt wird;
- Project- und Assembly-Sessions getrennte Lebensdauer-/Kapazitätsbudgets besitzen;
- Symbol-, Body-, Referenz- und Call-Tree-Antworten ihre dekompilierte Herkunft erkennen
  lassen;
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
  kontrollierten lokalen Cache. Fehlt der Zugriff, muss ein vorhandener Checkout oder
  die Dekompilation als deterministischer Fallback verfügbar bleiben.
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
