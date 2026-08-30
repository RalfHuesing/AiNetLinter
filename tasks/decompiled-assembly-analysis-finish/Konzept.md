---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: large
rules_dir: .agents/rules
---

# Konzept: Einheitlicher Roslyn-Analysepfad für Projekte und externe Assemblies

## Ziel und Nutzen

AiNetLinter soll ein Assembly-Target so in die semantische MCP-Analyse
einbinden, dass ein Agent dieselben sinnvollen Roslyn-Nachschlagefunktionen
verwenden kann wie für ein aktuell bearbeitetes Projekt. Das Assembly-Artefakt
ist dabei nur das Analyseziel. Die Herkunft der dafür verwendeten Roslyn-Quelle
wird separat aufgelöst:

1. Quellcode des aktuell bearbeiteten Projekts;
2. eine explizit zugeordnete externe Source-Solution;
3. eine statisch aus der DLL erzeugte, synthetische Roslyn-Quelle.

Ein Assembly-Target verwendet zuerst eine verlässlich zugeordnete und attestierte
Source-Solution, sofern eine solche über das globale externe Mapping konfiguriert
und erreichbar ist. Andernfalls wird die DLL statisch decompiliert. Beide Pfade
enden in derselben Roslyn-/MCP-Analyseschicht. Ein Agent soll daher eine spontan
entdeckte DLL direkt adressieren können, ohne eine Projektdefinition für die DLL
anzulegen oder manuelle Cachepflege zu benötigen.

Der Nutzen liegt im sicheren Nachschlagen externer Abhängigkeiten aus einem
aktuell bearbeiteten Projekt heraus. Der Agent erhält Antworten zu Symbolen,
Bodies, Struktur, Referenzen, Aufrufbäumen, Dependency Graphs und Metriken,
ohne dass externe Quellen verändert werden oder fremder Code im MCP-Prozess
ausgeführt wird. Eine Decompilation bleibt als solche erkennbar; fehlende
Referenzen, Mismatch, Decompilergrenzen und degradierte Zustände werden nicht
als vollständige Originalquelle ausgegeben.

## Fachliches Laufzeitmodell

Es gibt genau einen lokal beim Entwickler laufenden MCP-Daemon. Dieser Daemon
verwaltet die aktuell bearbeiteten Benutzer-Repositories beziehungsweise
Projektkontexte. Für diese residente Projektklasse gilt die bestehende Grenze
von maximal vier aktiven Kontexten.

Externe Quellen sind eine getrennte Klasse und verbrauchen keinen der vier
Benutzer-Kontexte. Es können logisch beliebig viele externe Quellen hinzukommen:

- eine lokale DLL wird statisch als externe Assembly decompiliert;
- eine explizit konfigurierte Git-/Gitea-Quelle wird als read-only
  Source-Solution-Snapshot geladen und für zugeordnete Assemblies verwendet;
- weitere DLL- und Source-Solution-Sessions besitzen eigene Leases,
  Lebensdauern und Ressourcenbudgets.

„Beliebig viele“ bedeutet dabei, dass die Vierergrenze der Benutzer-Repositories
nicht als Obergrenze für externe Quellen wiederverwendet werden darf. Physische
Grenzen für Speicher, Disk, Parallelität oder aktive externe Sessions müssen
separat, explizit und diagnostizierbar behandelt werden.

Der aktuell bearbeitete Projektkontext und eine externe Analysequelle bleiben
fachlich getrennt. Die externe Quelle ist ein read-only Nachschlageziel und kein
versteckter zweiter Consumer- oder Änderungskontext. Eine gemeinsame, eindeutig
identifizierte Source-Snapshot-Repräsentation darf von mehreren Target-Aliassen
und mehreren Benutzer-Projektkontexten wiederverwendet werden.

## Aktueller Stand

Die aktuelle Implementierung besitzt bereits eine belastbare Grundlage für:

- `targetType`-/`targetPath`-Auflösung und gemeinsamen Lease-Dispatch;
- statische PE-/Metadatenanalyse, Decompilation, Fingerprint und
  Decompilation-Cache;
- direkte Metadaten-Referenzen und Roslyn-Workspace-Erzeugung;
- explizites Mapping, Gitea-Transport, Source-Acquisition,
  Source-Snapshot, Cache-Reuse und Cache-Refresh;
- Ownership, Attestation, Trust-Zustände und Snapshot-Lebenszeit;
- Source-backed-Analyse oder statische Decompilation als Fallback;
- spezialisierte Assembly-Tools.

Noch nicht als vollständiges Zielbild abgeschlossen sind insbesondere:

- die produktive Default-Komposition des externen Source-Providers im einzelnen
  MCP-Daemon;
- die vollständige gemeinsame Toolroute für die sinnvollen Roslyn-Abfragen eines
  Assembly-Targets;
- die bedarfsgesteuerte transitive Auflösung externer Assembly-Referenzen;
- die explizite Trennung der vier residenten Projektkontexte von beliebig vielen
  externen Source-/Assembly-Sessions in allen Registry-, Health- und
  Ressourcenverträgen;
- die letzten konkreten Trust- und Lease-Fehlerpfade;
- die abschließende Capability-, Origin-, Partialitäts- und
  Dokumentationsbeschreibung.

Der aktuelle Code ist die maßgebliche Wahrheit. Die bereits vorhandenen
Source-, Cache- und Attestation-Bausteine werden weiterverwendet; das Konzept
verlangt keine neue parallele Decompiler- oder Repository-Architektur.

## Aktueller Scope

### Einheitlicher Target- und Session-Vertrag

- Alle sinnvollen Roslyn-orientierten MCP-Abfragen verwenden den harten
  Target-Vertrag `targetType` plus `targetPath`.
- `targetType=project` adressiert ein aktuell bearbeitetes Projektroot und
  verwendet weiterhin dessen `ainetlinter.project.json` mit Solution und Regeln.
- `targetType=assembly` adressiert eine einzelne existierende DLL und benötigt
  keine `ainetlinter.project.json`.
- Die Target-Auflösung und der Lease-Lifecycle liegen zentral; einzelne Tools
  verzweigen nicht eigenständig zwischen Projekt- und Assemblypfaden.
- `get_server_health` bleibt ein Maintenance-Tool ohne Pflicht-Target und kann
  zusätzlich gezielt Projekt-, Source-Solution- und Assembly-Sessions ausweisen.

### Drei Quellpfade, ein Roslyn-/MCP-Kern

Die Quellenherkunft wird vor der gemeinsamen Roslyn-Schicht entschieden:

1. Projektquelle des aktuell bearbeiteten Projekts mit dessen Solution,
   Documents und Regeln.
2. Externe Source-Solution aus einem expliziten Git-/Gitea-Mapping. Die
   vollständige Solution ist der gemeinsame Snapshot; das zugeordnete
   Source-Projekt wird daraus über Assembly-Identität ermittelt.
3. Externe DLL ohne verifizierten Source-Match. Sie wird statisch decompiliert
   und als synthetisches read-only Roslyn-Project materialisiert.

Der gemeinsame Teil liefert Solution-/Project-/Document-Sicht, SyntaxTrees,
SemanticModels, Symbole, Referenzen, Origin-Metadaten und den einheitlichen
Tool-Dispatch. „Originalquelle“ und „dekompiliert“ sind Herkunfts- und
Vertrauensmetadaten, keine drei voneinander unabhängigen Toolimplementierungen.

### Explizite Source-Auflösung

- Eine globale externe Mapping-Datei ist die einzige vorgesehene automatische
  Source-Zuordnung für bekannte externe Assemblies.
- `ainetlinter.project.json` bleibt auf den aktuell bearbeiteten Projektkontext
  beschränkt und wird für eine DLL nicht vorausgesetzt.
- Gitea wird nicht nach DLL- oder Repositorynamen durchsucht. Ohne explizites
  Mapping bleibt die DLL im Decompilationspfad.
- Ein Mapping benennt Repository-URL, Solution-Pfad und die bekannten DLL- oder
  Assembly-Namen. Einzelne `.csproj`-Pfade werden nicht redundant gepflegt;
  das Assembly-Projekt wird aus der geladenen Solution abgeleitet.
- Ein Repository kann mehrere Solutions, Source-Projekte und erzeugte DLLs
  enthalten. Repository, tatsächlich geladener Commit, Solution-Pfad,
  Source-Projekt und AssemblyName müssen eindeutig zusammenpassen.
- Der lokale Clone ist ein interner Cache der konfigurierten Gitea-Quelle und
  keine konkurrierende Source-of-Truth.
- Lokale dirty-, uncommitted- oder unbuilt-Checkouts werden nicht still als
  alternative Quelle verwendet. Ein späterer ausdrücklich aktivierter
  Local-Checkout-Modus wäre eine eigene Erweiterung.

### Gemeinsame Source-Snapshots und Referenzen

- Eine kanonische Source-Snapshot-Identität umfasst mindestens Repository,
  tatsächlich geladenen Commit, Solution-Pfad, Source-Projekt und
  AssemblyName.
- Direkter Assembly-Aufruf und Nachschlageauflösung aus einem Projekt können
  auf denselben verifizierten Source-Snapshot zeigen, ohne den Consumer-Kontext
  des Projekts zu teilen.
- Die gleiche Source-Solution oder DLL darf bei mehreren Target-Aliassen nicht
  unnötig mehrfach materialisiert werden.
- Direkte Referenzen werden weiterhin metadata-only ermittelt.
- Referenzen wie `foo.dll -> bar.dll` werden bei Bedarf rekursiv über denselben
  Resolver als Source-backed- oder Decompilation-Quelle nachgeladen.
- Kanonische DLL- und Source-Snapshot-Identitäten verhindern Zyklen und doppelte
  Materialisierung. Nicht erreichbare Ziele bleiben sichtbare
  `partial`-/External-Nodes und werden nicht durch Phantomsymbole ersetzt.
- Die transitive Auflösung bleibt best effort, bedarfsgesteuert und durch lokale
  DLLs, explizite Mappings, Decompilergrenzen sowie Tiefe-, Größen- und
  Ressourcenlimits begrenzt. Ein beliebig vollständiger Runtime-Call-Graph ist
  kein Ziel.

### Sinnvolle Assembly-Capabilities

Die Kernabfragen verwenden denselben Roslyn-Codepfad für Projekt- und
Assembly-Ziele:

- Symbolsuche, Skeleton und Klassen-/Strukturansicht;
- `get_symbol_body` für Original- oder dekompilierten Body;
- Referenzen und Call Trees innerhalb der verfügbaren Source-/Decompilation-
  Graphen;
- Dependency Graph einschließlich aufgelöster externer Referenzen;
- Metriken über die tatsächlich verfügbaren Documents.

Für jede weitere Toolfamilie wird eine Capability-Matrix geführt. Regeln,
Violations, Audits, Testkontext, Git-Diff und Change-Impact werden nur dort
angeboten, wo der jeweilige Source-, Regel-, Test- oder Consumer-Kontext
fachlich vorhanden und ausdrücklich beschrieben ist. Ein nicht sinnvoller
Fall liefert eine eindeutige Nichtunterstützungs- oder Partialitätsdiagnose,
keinen leeren Erfolg und keine Scheinanalyse.

Assembly-spezifische Werkzeuge wie `inspect_assembly` und
`find_assembly_extensions` behalten ihren sinnvollen Assembly-Output, verwenden
aber denselben Assembly-Session-Dispatcher und dieselben Herkunftsmetadaten.
Ein Consumer-Projekt wird nicht als versteckter zweiter Target-Parameter
eingeführt; Cross-Target-Fragen bleiben explizite spätere Abfragen.

### Origin, Fingerprint und Generationen

- Jede Antwort kann zwischen `source-backed` und `decompiled` unterscheiden
  und legt Source-Pfad, Assembly-Hash, Source-Snapshot beziehungsweise
  generierten Pfad und Confidence offen.
- Decompilierte Bodies erhalten einen standardisierten Hinweis, dass sie von
  der Originalquelle abweichen können.
- Der Decompilation-Key umfasst kanonischen DLL-Pfad, SHA-256 der DLL-Bytes,
  Decompiler-Version, Optionen und Manifest-Schema.
- Der Source-Key umfasst kanonische Repository-URL, tatsächlich geladenen
  Commit und Solution-Pfad.
- mtime und Dateigröße dürfen nur als Vorprüfung dienen. Ein neuer Hash erzeugt
  eine neue Generation; ein gleicher Hash darf trotz neuer mtime wiederverwendet
  werden.
- Generationen werden vollständig und atomar aufgebaut. Laufende Leases dürfen
  auf einer alten Generation fertig werden; halbfertige Quellen gelangen nicht
  in die sichtbare Roslyn-Solution.

### Daemon-, Registry- und Ressourcenmodell

- Die bestehende Vierergrenze zählt ausschließlich residente
  `targetType=project`-Kontexte für aktuell bearbeitete Projektroots.
- Externe Source-Solution- und Assembly-Sessions zählen nicht gegen diese
  Grenze und werden in separaten Registry-/Health-Kategorien geführt.
- Externe Quellen können logisch in beliebiger Anzahl adressiert werden. Falls
  harte Limits für gleichzeitig residente Source-Solutions oder Assemblies
  erforderlich sind, werden sie separat konfiguriert, diagnostiziert und mit
  eigenen TTL-/LRU-/Lease-Regeln versehen.
- Persistente externe Cache-Einträge werden in diesem Vorhaben nicht automatisch
  gelöscht. In-Memory-Leases, aktive Workspaces und temporäre Checkouts müssen
  hingegen vollständig und sicher freigegeben werden.
- Mehrere gleichzeitige Erstzugriffe auf dieselbe DLL oder denselben
  Source-Snapshot teilen eine Creation Barrier und erzeugen keine unkontrollierte
  Vervielfachung.

## Muss-Kriterien

- Der MCP-Daemon verwendet einen einheitlichen `targetType`-/`targetPath`-
  Vertrag für Projekt- und Assembly-Ziele.
- Eine unbekannte absolute DLL kann ohne `ainetlinter.project.json` analysiert
  werden.
- Ein Assembly-Target wird zuerst gegen explizite Source-Mappings geprüft und
  nur bei fehlendem verifiziertem Match statisch decompiliert.
- Externe Quellen bleiben read-only; weder die untersuchte DLL noch ein
  externer Source-Checkout wird durch Analysewerkzeuge verändert.
- Source-Solution, Source-Projekt und Assembly-Zuordnung sind eindeutig und
  verwenden den tatsächlich geladenen Commit als interne Snapshot-Identität.
- Direkte und transitiv erreichbare externe Referenzen werden bedarfsgesteuert
  über denselben Source-/Decompilation-Resolver behandelt und dedupliziert.
- Die Kernabfragen für Symbole, Bodies, Struktur, Referenzen, Call Trees,
  Dependency Graphs und Metriken verwenden denselben Roslyn-/MCP-Kern.
- Capability, Origin, Confidence und Partialität sind für jede externe Antwort
  eindeutig erkennbar. Nicht sinnvolle Tool-/Quellenkombinationen werden
  ausdrücklich als nicht unterstützt ausgewiesen.
- Ein einzelner lokaler MCP-Daemon verwaltet höchstens vier residente
  Benutzer-Projektkontexte. Externe DLL- und Git-/Gitea-Quellen verbrauchen
  diese Plätze nicht und sind logisch nicht auf vier Einträge begrenzt.
- `Clean`, `Dirty` und `Unverified` werden über Transport, Acquisition, Cache,
  Provider und Snapshot erhalten. Dirty oder Unverified werden nie als Clean
  ausgegeben.
- Statusparser und Materialization-Lease sind bei Lone-CR, Cancellation,
  invaliden Inventaren, Öffnungsfehlern und Aufräumfehlern fail-closed und
  ressourcensicher.
- Kein Assembly-Code wird geladen, reflektiert oder ausgeführt. PEReader,
  Decompiler und Roslyn-MetadataReferences bleiben statische Verfahren.
- Der lokale Default-Host kann konfigurierte öffentliche Git-/Gitea-Quellen
  über den produktiven Provider verwenden. Private Quellen benötigen eine
  getrennte, injizierbare Credential-Auflösung; Geheimnisse erscheinen nicht
  in Konzepten, Diagnosen oder normalen Konfigurationswerten.
- Konfigurierte Quelle, Fallback, Fehlerzustand, Ownership, Generation und
  Snapshot-Lebenszeit sind dokumentiert.

## Akzeptanzkriterien

- Ein Agent kann eine unbekannte DLL mit `targetType=assembly` direkt über MCP
  analysieren, ohne eine Projektdefinition für diese DLL anzulegen.
- Eine gemappte Git-/Gitea-Quelle verwendet nach erfolgreicher Prüfung die
  passende Source-Solution und decompiliert das zugeordnete Assembly-Target
  nicht zusätzlich. Ohne belastbaren Match wird die DLL transparent
  decompiliert.
- Ein Repository mit mehreren DLL-Projekten löst die konfigurierten
  Assembly-Namen auf die korrekten Source-Projekte derselben Solution auf;
  unklare oder mehrdeutige Zuordnungen werden nicht still akzeptiert.
- Ein direkter Assembly-Aufruf und ein Nachschlageaufruf aus einem
  Benutzer-Projekt teilen bei identischer Source-Snapshot-Identität die
  zugrunde liegende Source-/Document-Repräsentation, behalten aber getrennte
  Consumer- und Target-Leases.
- Eine transitive Referenz `foo.dll -> bar.dll` kann `bar.dll` als Source-
  backed- oder Decompilation-Quelle laden. Bereits geladene Quellen werden
  wiederverwendet; Zyklen und fehlende Referenzen werden sichtbar behandelt.
- Mehrere parallele Erstzugriffe auf denselben Target- oder Snapshot-Key führen
  nicht zu mehrfacher Decompilation oder mehrfacher Source-Solution-
  Materialisierung.
- Der Binary-Fingerprint erkennt geänderte Bytes, mtime-only-Änderungen und
  identische Bytes korrekt. Neue Generationen werden atomar veröffentlicht;
  laufende alte Leases bleiben gültig.
- Ein abgelaufener oder fehlgeschlagener externer Refresh gibt einen alten
  Stand nicht still als aktuell aus. Ein letzter guter Stand bleibt höchstens
  als sichtbarer degradierter Zustand erhalten; die angefragte DLL kann
  deterministisch auf Decompilation zurückfallen.
- Ein Statusdatensatz mit Lone-CR wird abgelehnt, während gültige CRLF-Daten
  unverändert funktionieren.
- Cancellation, invalides Inventar, Fehler beim Öffnen und Fehler beim
  Aufräumen hinterlassen keine bereits erworbenen Materialization-Handles oder
  temporären Checkout-Ressourcen.
- Vier residente Benutzer-Projektkontexte bleiben innerhalb ihrer Grenze,
  während mehrere externe DLL- und Git-/Gitea-Quellen unabhängig davon
  adressierbar sind. Ein Ressourcenmangel führt zu einer sichtbaren,
  kontrollierten Nichtverfügbarkeit und nicht zur stillen Überschreibung eines
  aktiven Kontexts.
- Für jede Kern-Capability existieren positive, fehlende/partielle und nicht
  unterstützte Fälle. Tool-Antworten unterscheiden Projektquelle,
  Source-backed-Quelle und Decompilation.
- Der lokale Default-Host verwendet für eine konfigurierte öffentliche
  Git-/Gitea-Quelle den produktiven Provider. Die Provider-Injektion bleibt für
  deterministische Tests möglich.
- `README.md`, `Docs/configuration.md`, `Docs/integration.md`,
  `Docs/agent-api.md` und die relevanten Toolbeschreibungen spiegeln nach der
  Umsetzung den tatsächlichen Vertrag wider. Widersprüchliche Legacy-
  Parameter oder Capability-Aussagen bleiben nicht bestehen.
- Vor Abschluss der späteren Implementierung sind die projektweit
  vorgeschriebenen Build-, Fast-Test- und Integration-Test-Läufe ohne
  Stress-Kategorie, MCP-Safeguard-/Violation-Prüfungen und der gezielte
  DRY-/Drift-Audit erfolgreich. Diese Läufe werden in diesem
  Konzeptierungsdurchlauf nicht ausgeführt.

## Non-Goals und bewusste Grenzen

- Kein Laden, Ausführen oder Reflection-basierter Betrieb fremder Assemblies
  und kein `AssemblyLoadContext`.
- Keine Rekonstruktion des ursprünglichen Buildprozesses, der ursprünglichen
  Projektdateien, PDB-Garantien oder SourceLink-Gleichheit.
- Keine automatische Suche oder Discovery von Gitea-Repositories anhand von
  DLL- oder Repositorynamen.
- Kein versteckter Consumer- oder Änderungskontext für eine externe Quelle und
  keine automatische Ermittlung, in welchem Benutzer-Projekt eine DLL
  verwendet wird.
- Keine automatische Nutzung lokaler dirty-, uncommitted- oder unbuilt-
  Checkouts als alternative Source-of-Truth.
- Keine automatische Branch-Zusammenführung oder Multi-Branch-Synchronisation.
- Kein Git-Diff-, Change-Impact-, externer Testausführungs- oder allgemeiner
  Quality-Gate-Vertrag für externe Quellen, solange dafür kein eigener
  fachlicher Kontext ausdrücklich definiert ist.
- Keine pauschale Freischaltung sämtlicher Regel-, Audit-, Test-, Dead-Code-,
  Duplicate- oder Pattern-Tools. Nicht sinnvolle Fälle werden als solche
  dokumentiert.
- Keine vollständige Namespace-Immutability oder Garantie gegen einen
  bösartigen lokalen Administrator beziehungsweise einen konkurrierenden
  Prozess mit denselben lokalen Rechten.
- Keine privilegierte Windows-Reparse-Umgebung als allgemeine Start- oder
  CI-Voraussetzung.
- Keine automatische Garbage-Collection persistierter externer Cache-Einträge
  in diesem Vorhaben.
- Keine neue allgemeine Plugin- oder Artefakt-Framework-Schicht nur für die
  Vereinheitlichung.
- Keine Roadmap, keine Step-Dateien und kein zusätzlicher Task-State als
  Bestandteil dieses Konzeptierungs- und Implementierungsvorhabens.

## Betroffene Projektbereiche

Die zentrale Target-, Session- und Toolroute betrifft insbesondere:

- `src/AiNetLinter/Mcp/Tools/AnalysisToolCall.cs`;
- `src/AiNetLinter/Mcp/Tools/AnalysisTargetResolver.cs`;
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/`;
- `src/AiNetLinter/Mcp/Assemblies/Analysis/`;
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/`;
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Providers/`;
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Snapshots/`;
- die Projekt-Registry, Source-Snapshot-Registry, Assembly-Session-Registry
  und Health-Ausgabe;
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysisHostComposition.cs`;
- `src/AiNetLinter/Cli/McpServerCommand.cs` und
  `src/AiNetLinter/Cli/DaemonHostCommand.cs`.

Die Decompilation- und Referenzgrenzen betreffen insbesondere
`AssemblyAnalysisSession`, `AssemblyDecompilationAdapter`,
`AssemblyDecompilationCache`, `AssemblyReferenceResolver`, Origin-Modelle und
die Roslyn-Workspace-Fabrik.

Die externe Quellenverwaltung betrifft Mapping-Konfiguration,
`ExternalSourceRepositoryAcquirer`, Provider, Statusparser, Attestation,
Materialization-Lease, Cache-Reuse, Cache-Refresh, Cache-Writer und
Snapshot-Materializer.

Die öffentliche Beschreibung betrifft mindestens `README.md`,
`Docs/configuration.md`, `Docs/integration.md`, `Docs/agent-api.md` und die
MCP-Toolbeschreibungen. Änderungen an CLI- oder Regelverträgen ziehen die
jeweiligen Projekt-Synchronisationspflichten nach sich.

## Betriebs- und Bedrohungsmodell

Der MCP-Daemon läuft lokal beim Entwickler und bedient den aktiven
Projektkontext sowie read-only externe Nachschlagequellen. Der Entwickler kann
bis zu vier Benutzer-Repositories als residente Projektkontexte offen haben.
Externe DLLs und konfigurierte Git-/Gitea-Quellen liegen außerhalb dieser
Projektklasse und dürfen in beliebiger logischer Anzahl adressiert werden.

Remote-Repository-Inhalte, DLLs, PDBs, `.deps.json` und benachbarte Referenzen
sind untrusted input. Der Dienst schützt insbesondere vor:

- falschen, beschädigten, unvollständigen oder nachträglich veränderten
  Source-Snapshots;
- gefährlichen Pfaden, Reparse-/Link-Ausweichungen und unkontrollierten
  Schreibzielen;
- falscher Zuordnung von DLL, Repository, Source-Projekt oder Commit;
- Veröffentlichung unvollständiger Cache-Generationen;
- stiller Vermischung verschiedener Assembly- oder Source-Versionen;
- unbeabsichtigter Ausführung oder dynamischem Laden fremden Codes;
- versehentlicher Preisgabe von Credentials in Logs, Diagnosen, Argumenten,
  Cachemanifesten oder Konzeptdaten.

Das Modell garantiert keine vollständige Abwehr gegen einen bösartigen lokalen
Administrator oder einen konkurrierenden Prozess mit denselben lokalen Rechten.
Die zugesicherte Grenze ist stattdessen: explizite Ownership- und Attestation-
Prüfung, sichere Pfadregeln, fail-closed bei unklarer Integrität und sichtbare
Degradation bei fehlender Quelle. Eine stärkere Namespace-Sperre wäre ein
separates Sicherheitsvorhaben.

## Fehler-, Fallback- und Lebenszeitsemantik

### Quellenentscheidung

- Ein valides Mapping mit attestiertem, passendem Source-Snapshot gewinnt gegen
  Decompilation.
- Ohne verifizierten Match wird die DLL statisch decompiliert.
- Ein unklarer, dirty oder unverified Source-Stand darf nicht als bestätigte
  Originalquelle ausgegeben werden.
- Ungültige oder widersprüchliche Konfiguration endet mit einem sichtbaren
  Konfigurationsfehler und wird nicht durch eine scheinbar erfolgreiche
  Decompilation kaschiert.

### Zustände und Antworten

Die Analyseantwort unterscheidet mindestens:

- `complete`: angeforderte Quelle und benötigte Referenzen sind vollständig
  analysierbar;
- `partial`: Roslyn-Quelle ist vorhanden, aber Referenzen oder Teilbereiche
  fehlen;
- `degraded`: ein letzter guter oder eingeschränkter Zustand wird sichtbar,
  ohne ihn als aktuelle Originalquelle zu behaupten;
- `failed`: es gibt keinen analysierbaren Roslyn-Stand.

Zusätzlich bleiben `origin` und Trust getrennt sichtbar: `project`,
`source-backed` oder `decompiled` beschreiben die Herkunft; `Clean`, `Dirty`
und `Unverified` beschreiben deren Vertrauenszustand. Nur ein ausreichend
attestierter Clean-Stand darf regulär als Source-backed verwendet werden.

### Externe Refreshes und Fallback

- Ein vorhandener Source-Snapshot darf innerhalb seines gültigen
  Refresh-Intervalls wiederverwendet werden.
- Beim ersten Zugriff oder nach Ablauf wird die konfigurierte Git-/Gitea-Quelle
  gegen den Standard-Branch aktualisiert. Der tatsächlich geladene Commit wird
  zur Snapshot-Identität.
- Schlägt ein fälliger Refresh fehl, wird der alte Stand nicht still als aktuell
  behauptet. Er kann als degradierter Diagnosezustand erhalten bleiben; die
  angefragte DLL fällt deterministisch auf Decompilation oder einen sichtbaren
  Fehler zurück.
- Ein nicht verfügbarer optionaler Source-Match darf den Assembly-Fallback
  verwenden. Ein invalidierter oder widersprüchlicher Zustand darf nicht in
  einen erfolgreichen Source-backed-Zustand umgedeutet werden.

### Ownership, Lease und Cancellation

- Request-, Checkout-, Snapshot- und Workspace-Lebenszeiten bleiben getrennt,
  aber verschachtelt nachvollziehbar.
- Eine Materialization-Lease hält die benötigten Handles von der Attestation
  über Kopie und Snapshot-Erzeugung bis zum Ende der Nutzung.
- Snapshot und Workspace dürfen ihre Lease nicht vorzeitig freigeben.
- Bei Cancellation, invaliden Daten, Fehlern beim Öffnen oder Fehlern beim
  Aufräumen werden bereits erworbene Ressourcen best effort vollständig
  geschlossen; Cancellation wird anschließend weitergegeben.
- Ein Fehler beim Aufräumen darf keine ungültige Quelle in einen Erfolg
  umwandeln und wird diagnostisch sichtbar.
- Cache-Generationen werden temporär aufgebaut, manifest- und hashgeprüft und
  erst vollständig atomar veröffentlicht.
- Externe Quellen werden nicht stillschweigend für andere aktive Vorgänge
  überschrieben. Bei erschöpften Ressourcen wird der konkrete Vorgang
  kontrolliert abgewiesen oder degradiert.

## Verifikation und Dokumentationspflichten

Die spätere Implementierung verwendet zuerst deterministische Unit- und
Component-Tests für Target-Vertrag, Mapping, Source-/DLL-Identität, Fingerprint,
Cache-Key, Capability, Origin, Statusparser und Lease-Cleanup. Teure oder
externe Schritte erhalten injizierbare Test-Doppelgänger mit endlicher
Cancellation-/Timeout-Grenze.

Repräsentative Integration-/MCP-Tests decken mindestens ab:

- unbekannte DLL ohne Projektdefinition;
- gemappte Source-Solution versus Decompilation-Fallback;
- mehrere Assemblys aus einer Source-Solution;
- direkte und transitive Referenzen mit Deduplizierung;
- gemeinsame Source-Snapshot-Nutzung aus mehreren Target-Aliassen;
- vier residente Benutzer-Kontexte getrennt von mehreren externen Quellen;
- Default-Host mit öffentlicher Git-/Gitea-Quelle und injiziertem Provider;
- atomaren Refresh, alte laufende Leases und fehlerhafte Cache-Generationen;
- Trust-, Partialitäts-, Cancellation- und Cleanup-Verträge.

Kein Test restauriert oder führt ein fremdes Projekt aus. Externe Netzwerk-
und privilegierte Reparse-Umgebungen sind keine Voraussetzung für die normale
Testpyramide; capability-gesteuerte Nachweise bleiben ergänzend. Hohe
Nebenläufigkeitslast gehört ausschließlich in `Stress`.

Vor Abschluss der späteren Implementierung gelten die Projektvorgaben:

- vollständiger Build ohne Warnungen;
- vollständige Fast- und Integration-Testläufe ohne `Stress`;
- MCP-Safeguard-/Violation-Prüfungen;
- gezielter DRY-, Refactoring-Drift-, Dead-Code- und Magic-Value-Audit.

## Spätere Annahmen und Abhängigkeiten

- Ein späteres Ressourcen-Epic kann eigene `MaxSourceSolutions`- und
  `MaxAssemblies`-Budgets sowie getrennte TTL-/LRU-Regeln festlegen, ohne die
  Vierergrenze der Benutzer-Projektkontexte zu verändern.
- Ein späteres Referenz-Epic kann die transitive Closure erweitern, solange
  statisches No-Execution, Deduplizierung, sichtbare Partialität und endliche
  Ressourcenlimits erhalten bleiben.
- Ein späteres Capability-Epic kann weitere Toolfamilien aufnehmen, ohne
  fachlich fehlende Git-, Test-, Regel- oder Consumer-Kontexte zu simulieren.
- Ein späterer Local-Checkout-Modus müsste eigenen Origin-, Trust- und
  Synchronisationsregeln folgen und darf Gitea nicht still verdrängen.
- Ein stärkeres Multi-Process- oder Namespace-Sicherheitsmodell wäre ein
  separates Sicherheits-/Betriebs-Epic.

## Offene Fragen und spätere Teilbereichsentscheidungen

Für den Startumfang bestehen nach den getroffenen Entscheidungen keine
blockierenden Scope-Fragen mehr. Die folgenden Fragen werden erst im jeweils
betroffenen Teilbereich entschieden:

1. Welche konkreten Ressourcenlimits gelten zusätzlich zur unbegrenzten
   logischen Anzahl externer Quellen, insbesondere für gleichzeitig residente
   Source-Solutions, Assembly-Sessions, Disk, Parallelität und Idle-TTL?
2. Wie wird die Credential-Auflösung für private Git-/Gitea-Quellen konkret an
   die lokale Umgebung angebunden, ohne Geheimnisse in Konfiguration,
   Diagnosen oder Cachemanifesten zu persistieren?
3. Soll eine gematchte vollständige Source-Solution bei einer Suche
   standardmäßig solutionweit oder zunächst nur im ausgewählten
   Source-Projekt durchsucht werden, sofern der jeweilige Toolvertrag beides
   zulässt?
4. Welche zusätzlichen Toolfamilien erhalten nach der Kernmenge einen eigenen
   Assembly-Capability-Vertrag, insbesondere für Regel-, Audit-, Test- und
   Quality-Gate-Kontext?
5. Welche genauen Tiefe-, Größen- und Abbruchgrenzen gelten für transitive
   Referenzauflösung und rekursive externe Source-Snapshots?
6. Welcher nachweisbare Kompatibilitätsnutzen besteht für die spätere
   Bereinigung interner Origin-Aliase oder die Zentralisierung von
   Origin-Bezeichnungen?

## Arbeitsgedächtnis (nur Draft)

### Korrigierte Zielinterpretation

Der zentrale Zweck ist die Fertigstellung eines einheitlichen MCP-/Roslyn-
Analysepfads für Projektquellen, bekannte externe Source-Solutions und
unbekannte DLLs. Der Schwerpunkt ist nicht nur die Reparatur von Trust- und
Lease-Grenzfällen. Diese sind notwendige Abschlussarbeiten innerhalb eines
größeren Vertrages für Target-Auflösung, Source-Herkunft, Snapshot-Sharing,
transitive Referenzen und sinnvolle Tool-Parität.

### Bestätigte Nutzerentscheidungen

- Die sinnvolle Assembly-Kernmenge umfasst Symbol-, Body-, Struktur-,
  Referenz-, Call-Tree-, Dependency-Graph- und Metrikabfragen.
- Es gibt einen lokal beim Entwickler laufenden MCP-Daemon.
- Dieser Daemon verwaltet maximal vier residente Benutzer-Repositories bzw.
  Projektkontexte.
- Externe Quellen sind davon unabhängig und können logisch in beliebiger
  Anzahl hinzukommen.
- Externe Quellen sind entweder DLLs für statische Decompilation oder
  konfigurierte Git-/Gitea-Quellen für Source-backed-Analyse.
- Der lokale Default-Host soll konfigurierte öffentliche Git-/Gitea-Quellen
  selbstständig verwenden. Private Quellen bleiben an eine separate,
  injizierbare Credential-Auflösung gebunden.
- Das begrenzte lokale Bedrohungsmodell gilt; eine vollständige
  Namespace-Sperre gegen gleichberechtigte Prozesse ist kein Muss.

### Geprüfte aktuelle Evidenz

- Die aktuelle Assembly-Analyse umfasst 68 Produktionsdateien; der aktuelle
  MCP-Violation- und Dead-Code-Abgleich ergab dort keine Befunde.
- Die Resident-Registry ist auf höchstens vier residente Projekt-Keys
  ausgelegt; diese Grenze darf nicht als Obergrenze für externe Quellen
  wiederverwendet werden.
- Statische Decompilation, direkte Referenzauflösung, Mapping, Gitea-
  Acquisition, Cache-Reuse, Cache-Refresh, Attestation und Snapshot-Lebenszeit
  sind bereits als Bausteine vorhanden.
- `AssemblyReferenceResolver` löst derzeit direkte Metadaten-Referenzen auf;
  eine transitive Closure ist noch nicht vorhanden.
- Die produktiven Host-Einstiegspunkte erzeugen derzeit standardmäßig eine
  `UnavailableExternalSourceProvider`-Komposition; Provider-Injektion ist in
  Kompositions- und Testpfaden möglich.
- Allgemeine Assembly-Toolaufrufe sind noch nicht in der gewünschten
  Capability-Parität freigeschaltet.
- Der Statusparser hat noch einen Lone-CR-Grenzfall. Die Lease-Erzeugung hat
  bei Cancellation, invaliden Eingaben und einzelnen Dispose-Fehlern noch
  keinen vollständig geschlossenen Cleanup-Pfad.
- Die zentrale Pfadprüfung ist bereits gemeinsam verwendet; eine frühere
  Duplikationsannahme ist erledigt.
- Der interne `AssemblyOrigin.Kind`-Alias hat keine produktiven Referenzen;
  seine Bereinigung ist kein Startkriterium.

### Bewertung der geprüften Kandidaten

**Weiterhin relevant und im Scope:**

- gemeinsamer Target-/Roslyn-/MCP-Kern für die drei Quellpfade;
- externe Source-Snapshot-Identität, Alias-Sharing und getrennte
  Benutzer-/Quellen-Lebensdauer;
- transitive, bedarfsgesteuerte Referenzauflösung;
- sinnvolle Kern-Capability-Matrix und sichtbare Origin-/Partialitätsverträge;
- Default-Provider-Komposition für konfigurierte öffentliche Quellen;
- Lone-CR-Korrektur und vollständige Lease-Bereinigung;
- Dokumentation und gezielte Abschlussverifikation.

**Bereits umgesetzt:**

- statische Analyse ohne Runtime-Loading;
- Decompilation, Fingerprint, Cache-Grundlage und direkte Referenzauflösung;
- Target-Basis, Mapping, Source-Selection, Gitea-Transport, Acquisition,
  Cache-Reuse, Refresh, Attestation und Snapshot-Lebenszeit;
- fail-closed-Konfiguration und Erhalt der Dirty-Trust-Semantik;
- zentrale Pfad-, URL- und Test-Snapshot-Hilfen.

**Veraltet:**

- die Annahme, Decompiler-Version, grundlegende Mapping- und Cacheverträge
  oder die erste Attestation-Architektur seien noch grundsätzlich offen;
- kleinteilige Implementierungsabfolgen und historische Statusstände;
- die Annahme, die gemeinsame Pfadprüfung sei noch doppelt implementiert.

**Ungeklärt und einem späteren Teilbereich zuzuordnen:**

- konkrete externe Ressourcenlimits und Credential-Adapter;
- solutionweite versus ausgewählte Source-Projekt-Suche;
- zusätzliche Toolfamilien jenseits der Kernabfragen;
- Tiefe- und Größenlimits der transitiven Referenzauflösung;
- Kompatibilitätsnutzen interner Origin-Bereinigung.

**Overkill oder bewusst nicht übernehmen:**

- Runtime-/Reflection-Loading;
- Gitea-Discovery, automatische Branch-Zusammenführung und lokaler dirty-
  Checkout als versteckte Source-of-Truth;
- vollständige Namespace-Immutability gegen gleichberechtigte Prozesse;
- privilegierte Pflichtumgebungen, externe Netzwerkpflichtläufe und pauschale
  Freischaltung fachlich unpassender Tools;
- automatische persistente Cache-Garbage-Collection ohne konkreten Vertrag.

### Übergabestatus

Der Scope ist nach der Nutzerklärung fachlich ausreichend bestimmt, bleibt aber
bis zur ausdrücklichen Freigabe auf `status: draft`. Vor der Freigabe wird der
Arbeitsgedächtnisabschnitt entfernt und nur belastbare Anforderungen,
Annahmen, Grenzen und spätere Abhängigkeiten bleiben im eigentlichen Konzept.
Ein Orchestrator wird nicht automatisch gestartet.
