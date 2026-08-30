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
ist nur das Analyseziel. Die Herkunft der dafür verwendeten Roslyn-Quelle wird
separat aufgelöst:

1. Quellcode des aktuell bearbeiteten Projekts;
2. eine explizit zugeordnete externe Source-Solution;
3. eine statisch aus der DLL erzeugte, synthetische Roslyn-Quelle.

Ein Assembly-Target prüft zuerst eine verlässlich zugeordnete und attestierte
Source-Solution. Nur wenn kein belastbarer Match verfügbar ist, wird die DLL
decompiliert. Beide Pfade enden in derselben Roslyn-/MCP-Analyseschicht. Ein
Agent kann eine spontan entdeckte DLL direkt adressieren, ohne dafür eine
Projektdefinition oder manuelle Cachepflege anzulegen.

Der Nutzen liegt im sicheren Nachschlagen externer Abhängigkeiten aus einem
aktuell bearbeiteten Projekt heraus. Der Agent erhält Antworten zu Symbolen,
Bodies, Struktur, Referenzen, Aufrufbäumen, Dependency Graphs und Metriken.
Externe Quellen werden nicht verändert, und fremder Code wird im MCP-Prozess
weder geladen noch ausgeführt. Eine Decompilation bleibt als solche erkennbar;
fehlende Referenzen, Source-Mismatch, Decompilergrenzen und degradierte
Zustände werden nicht als vollständige Originalquelle ausgegeben.

## Fachliches Laufzeitmodell

Es gibt genau einen lokal beim Entwickler laufenden MCP-Daemon. Er verwaltet
die aktuell bearbeiteten Benutzer-Repositories beziehungsweise
Projektkontexte. Für diese residente Projektklasse gilt die bestehende Grenze
von maximal vier aktiven Kontexten.

Externe Quellen sind eine getrennte Klasse und verbrauchen keinen der vier
Benutzer-Kontexte. Es können logisch beliebig viele externe Quellen hinzukommen:

- eine lokale DLL wird statisch als externe Assembly decompiliert;
- eine explizit konfigurierte Git-/Gitea-Quelle wird als read-only
  Source-Solution-Snapshot geladen und für zugeordnete Assemblies verwendet;
- transitiv erreichbare externe DLLs und Source-Solutions werden bei Bedarf
  über denselben Resolver ergänzt.

Die Vierergrenze darf nicht als Obergrenze für externe Quellen wiederverwendet
werden. Physische Grenzen für Speicher, Disk, Parallelität oder gleichzeitig
residente externe Sessions müssen separat, explizit und diagnostizierbar sein.
Ein externes Snapshot- oder Assembly-Limit darf niemals still einen aktiven
Benutzer-Projektkontext verdrängen.

Der aktuell bearbeitete Projektkontext und eine externe Analysequelle bleiben
fachlich getrennt. Die externe Quelle ist ein read-only Nachschlageziel und
kein versteckter zweiter Consumer- oder Änderungskontext. Eine gemeinsame,
eindeutig identifizierte Source-Snapshot-Repräsentation darf von mehreren
Target-Aliassen und mehreren Benutzer-Projektkontexten wiederverwendet werden.

## Aktueller Stand und Abschlussbedarf

Die aktuelle Implementierung besitzt bereits eine umfangreiche Grundlage für:

- `targetType`-/`targetPath`-Auflösung und gemeinsamen Lease-Dispatch;
- statische PE-/Metadatenanalyse, Decompilation, Fingerprint und
  Decompilation-Cache;
- direkte Metadaten-Referenzen und Roslyn-Workspace-Erzeugung;
- explizites Mapping, Gitea-Transport, Source-Acquisition,
  Source-Snapshot, Cache-Reuse und Cache-Refresh;
- Ownership, Attestation, Trust-Zustände und Snapshot-Lebenszeit;
- Source-backed-Analyse oder statische Decompilation als Fallback;
- spezialisierte Assembly-Tools.

Der Abschlussbedarf dieses Konzepts umfasst deshalb die vollständige
End-to-End-Integration und die noch nicht belastbar abgeschlossenen Teile:

- produktive Default-Komposition des Gitea-Source-Providers im MCP-Daemon;
- vollständige gemeinsame Toolroute und Capability-Matrix für sinnvolle
  Roslyn-Abfragen auf Assembly-Zielen;
- bedarfsgesteuerte transitive Referenzauflösung mit Deduplizierung,
  Zyklus- und Missing-Reference-Semantik;
- getrennte Registry-, Health-, Kapazitäts- und Lebenszeitverträge für vier
  Benutzer-Kontexte und beliebig viele externe Quellen;
- abschließende Trust-, Attestation-, Statusparser- und Lease-Korrekturen;
- vollständige API-, Konfigurations-, Integrations-, Architektur-,
  Agentenregel- und Tooldokumentation;
- vollständige Verifikation gemäß den aktuellen Projektregeln.

Der aktuelle Codebestand und die aktuellen Projektregeln sind maßgeblich. Die
vorhandenen Source-, Cache- und Attestation-Bausteine werden weiterverwendet;
es entsteht keine parallele Decompiler- oder Repository-Architektur.

## Vollständiger Umsetzungsumfang

### Einheitlicher Target- und Session-Vertrag

- Alle sinnvollen Roslyn-orientierten MCP-Abfragen verwenden den harten
  Target-Vertrag `targetType` plus `targetPath`.
- `targetType=project` adressiert ein aktuell bearbeitetes Projektroot und
  verwendet dessen `ainetlinter.project.json` mit Solution und Regeln.
- `targetType=assembly` adressiert eine einzelne existierende DLL und benötigt
  keine `ainetlinter.project.json`.
- Absolute Pfade werden vor dem Registry-Zugriff validiert und kanonisiert.
- Legacy-Parameter wie parallele `projectRoot`-/`assemblyPath`-Kombinationen
  bleiben nicht als zweiter MCP-Vertrag bestehen.
- Target-Auflösung, Session-Erzeugung und Lease-Lifecycle liegen zentral.
  Einzelne Toolregistrierungen verzweigen nicht selbst zwischen Projekt- und
  Assemblypfaden.
- `get_server_health` bleibt ein Maintenance-Tool ohne Pflicht-Target und
  weist Projekt-, Source-Solution- und Assembly-Sessions getrennt aus. Mit
  einem Target kann es genau diese Session gezielt anzeigen.

### Drei Quellpfade, ein gemeinsamer Roslyn-/MCP-Kern

Die Quellenherkunft wird vor der gemeinsamen Roslyn-Schicht entschieden:

1. **Aktuelles Projekt:** konfigurierte Solution, Documents und Regeln des
   aktuell bearbeiteten Projektroots.
2. **Bekannte externe Quelle:** explizit gemappte Git-/Gitea-Repository- und
   Source-Solution. Die vollständige Solution wird als Snapshot geladen; das
   zugeordnete Source-Projekt wird daraus über Assembly-Identität ermittelt.
3. **Unbekannte externe DLL:** statische Decompilation und Materialisierung
   als synthetisches read-only Roslyn-Project.

Alle drei Pfade liefern Solution-/Project-/Document-Sicht, SyntaxTrees,
SemanticModels, Symbole, Referenzen und Origin-Metadaten an denselben
Tool-Dispatch. „Originalquelle“ und „dekompiliert“ sind Herkunfts- und
Vertrauensmetadaten, keine separaten Toolwelten.

Die Auflösung des Source-Projekts für eine DLL ist kein versteckter Consumer-
Kontext. Die Frage, in welchem Benutzer-Projekt eine DLL verwendet wird, bleibt
eine separate, explizite Cross-Target-Abfrage.

### Explizites Mapping und Gitea-Source-of-Truth

- Eine globale Mapping-Datei ist die einzige automatische Source-Zuordnung für
  bekannte externe Assemblies.
- Ihr Pfad wird über die externe Konfiguration, beispielsweise
  `ExternalSources:MappingsPath` in `appsettings.json`, bestimmt.
- Ein Eintrag enthält konzeptionell Repository-URL, Solution-Pfad und ein
  `assemblies`-Array mit DLL- oder Assembly-Namen:

  ```json
  {
    "repositories": [
      {
        "url": "https://gitea.example/shared.git",
        "solutionPath": "src/Shared.slnx",
        "assemblies": ["Foo.dll", "Bar.dll"]
      }
    ]
  }
  ```

- Einzelne `.csproj`-Pfade werden nicht redundant gepflegt. Die konfigurierte
  `.sln`/`.slnx` wird geladen; daraus werden Projekte, `AssemblyName` und die
  Projektstruktur bestimmt.
- Repository, Solution-Pfad, Source-Projekt, AssemblyName und tatsächlich
  geladener Commit müssen eindeutig zusammenpassen. Keine oder mehrere
  Auflösungen ergeben einen sichtbaren Konfigurations-/Matchfehler.
- Gitea wird nicht nach DLL- oder Repositorynamen durchsucht. Ein schwaches
  Namenssignal darf einen expliziten Eintrag plausibilisieren, aber kein
  unbekanntes Repository selbst auswählen.
- Der beim Aktualisieren geladene Commit wird intern als Snapshot-Identität
  gespeichert; ein Commit muss in der ersten Benutzerkonfiguration nicht
  manuell eingetragen werden.
- Ein lokaler Clone ist ausschließlich interner Cache. Ein lokaler dirty-,
  uncommitted- oder unbuilt-Checkout wird nicht still als Source-of-Truth
  verwendet.
- Änderungen sollen über committen, nach Gitea synchronisieren und anschließendes
  Refresh in den normalen gemeinsamen Arbeitsablauf gelangen. Branches sind
  keine eigene Analyseidentität und werden nicht automatisch zusammengeführt.

### Source-Match und Evidenz

- Explizites Mapping ist für eine `source-backed`-Analyse erforderlich.
- Starke Signale sind ein expliziter Mapping-Eintrag, nachweisbare
  SourceLink-/Repository-Metadaten, passender PDB-/Build-Bezug sowie
  Assembly-/Projektidentität.
- Mittlere Signale sind AssemblyName, Ziel-Framework, Projektpfad,
  Output-Verzeichnis und Build-Metadaten.
- Schwache Signale sind Repositoryname, DLL-Dateiname oder Namenskonventionen.
- Ein Repositoryname allein beweist weder das richtige Projekt noch die
  richtige Version.
- Die Antwort legt die verwendete Evidenz, Confidence, den geladenen Commit und
  das ausgewählte Source-Projekt offen.
- Bei Mismatch, unklarer Version oder nicht belegtem Match wird nicht still
  falscher Quellcode als Originalquelle ausgegeben; die DLL bleibt im
  Decompilations- oder sichtbaren Partial-/Fehlerpfad.

### Statische Decompilation und synthetisches Roslyn-Project

- Die vorhandene `ICSharpCode.Decompiler`-Integration wird über eine kleine
  Adaptergrenze genutzt; die aktuelle Paketversion `10.0.1.8346` und Optionen
  sind Teil der Cache-Identität.
- Decompilerdetails bleiben aus MCP-Toolregistrierungen und zentraler
  Roslyn-Logik herausgekapselt.
- Die Assembly-Session erzeugt ein `AdhocWorkspace`-Project ohne `.csproj`.
- Moderne C#-Parse- und Compilation-Optionen werden so gesetzt, dass erzeugte
  Quellen analysierbar sind.
- Documents werden vorzugsweise auf Typ- oder sinnvoller Decompiler-Einheit
  aufgeteilt; ein einzelner riesiger Quelltext ist zu vermeiden.
- Decompiler-Warnungen, Obfuscation, fehlende PDBs, dynamische Reflection und
  nicht rekonstruierbare Konstrukte führen zu sichtbarer Partialität oder
  Diagnose, nicht zu stillen Leertreffern.
- DLL, PDB, `.deps.json` und benachbarte Referenzen werden als untrusted input
  behandelt.

### Direkte und transitive Referenzauflösung

Die Referenzauflösung bleibt metadata-only, deterministisch und best effort:

1. Target-Assembly und ihre PE-Metadaten;
2. referenzierte DLLs aus dem Zielverzeichnis;
3. Framework-Assemblies aus Trusted Platform Assemblies oder passend
   ermitteltem Target Framework;
4. optionale maschinenlesbare Dependency-Informationen wie `.deps.json`, wenn
   sie ohne Codeausführung auswertbar sind.

Für eine Referenzkette wie `foo.dll -> bar.dll` gilt:

- direkte Referenzen werden aus Metadaten und verfügbaren Dependency-
  Informationen ermittelt;
- jede erreichbare externe Referenz wird über denselben Source-Resolver als
  gemappte Source-Solution oder statische Decompilation behandelt;
- derselbe kanonische DLL-Pfad oder Source-Snapshot wird nur einmal geladen;
- Zyklen, bereits besuchte Identitäten, fehlende Dateien und nicht auflösbare
  Versionen werden dedupliziert beziehungsweise als sichtbare
  `partial`-/External-Nodes ausgegeben;
- für Source-Projekte innerhalb derselben vollständigen Solution wird die
  vorhandene Projektstruktur verwendet;
- der Resolver arbeitet bedarfsgesteuert und lädt keine vollständige, beliebig
  tiefe Welt ohne konkreten Analysebedarf;
- ein vollständiger Runtime-Call-Graph über dynamische Bindings ist nicht
  versprochen.

NuGet-Assemblies sind normale externe Assemblies: ohne Mapping werden sie
decompiliert, mit explizitem Mapping source-backed analysiert. Eine
`PackageReference` allein ist kein Source-Mapping.

### Fingerprint, Cache und atomare Generationen

Decompilations- und Source-Solution-Cache bleiben getrennte Identitäten:

- DLL-Key: kanonischer DLL-Pfad, SHA-256, Decompiler-Version, Optionen und
  Manifest-Schema;
- Source-Key: kanonische Repository-URL, geladener Commit und Solution-Pfad;
- mtime und Dateigröße sind nur Vorprüfungen;
- gleicher Inhalt trotz neuer mtime darf wiederverwendet werden;
- neuer Hash oder relevante Options-/Schemaänderung erzeugt eine neue
  Generation.

Der externe Cache liegt in einem eigenen konfigurierbaren Cache-Root und nicht
im fremden Projekt. `ExternalSources:CacheRoot` und
`ExternalSources:RefreshIntervalMinutes` werden strikt validiert. Ein Manifest
enthält mindestens Pfad, Größe, mtime, Hash, Assembly-Identität, Referenzen,
Decompiler-/Schema-Information, erzeugte Dateien, Warnungen,
Referenzdiagnosen, Erstellungs-/Zugriffszeit und den Zustand
`complete`/`partial`/`failed`. Ein Source-Solution-Manifest enthält zusätzlich
Repository, Commit, Solution-Pfad, `AssemblyName`-zu-Source-Projekt und
Refreshdaten.

Cachegenerationen werden in temporären Verzeichnissen aufgebaut, unabhängig
validiert und atomar über einen `current`-Pointer veröffentlicht. Beschädigte,
unvollständige oder manipulierte Generationen werden nicht adoptiert. Laufende
Leases dürfen auf einer alten Generation fertig werden.

Beim ersten Zugriff auf ein gemapptes Repository und nach Ablauf des
Refresh-Intervalls wird der konfigurierte Standard-Branch aktualisiert. Ein
fehlgeschlagener fälliger Refresh behauptet den alten Stand nicht still als
aktuell. Ein validierter letzter guter Stand darf nur sichtbar degradiert oder
diagnostisch erhalten bleiben; der Assembly-Pfad fällt deterministisch auf
Decompilation oder einen sichtbaren Fehler zurück.

Persistente externe Cache-Einträge werden nicht automatisch gelöscht. Aktive
Workspaces, Leases und temporäre Checkouts werden dagegen vollständig
freigegeben.

### Gemeinsame Registry, Sessions und Lifetime

- Eine zentrale Target-/Lease-Grenze koordiniert Projekt- und externe Sessions,
  ohne ihre fachlichen Semantiken zu vermischen.
- Die vier `targetType=project`-Kontexte sind die einzige Klasse, die gegen die
  bestehende `MaxProjects`-Grenze zählt.
- Externe Source-Solutions und Assembly-Sessions besitzen getrennte Registry-,
  Health-, Ressourcen- und TTL-/LRU-Sichten. Sie zählen nicht gegen
  `MaxProjects`.
- Mehrere Target-Aliasse dürfen denselben Source-Snapshot teilen, erhalten aber
  eigene Consumer-/Target-Leases.
- Mehrere parallele Erstzugriffe teilen eine Creation Barrier und laden oder
  decompilieren dieselbe Identität nicht unkontrolliert mehrfach.
- Eine source-backed `core.dll` kann sowohl direkt als auch aus einem anderen
  Benutzer-Projekt heraus auf denselben Snapshot zeigen. Die SemanticModel-
  Instanz muss wegen ihrer Compilationbindung nicht geteilt werden; die
  Source-/Document-Repräsentation soll jedoch dedupliziert werden.
- Externe Ressourcenlimits, falls für Disk, Speicher, Parallelität oder Anzahl
  nötig, werden unabhängig von den vier Benutzer-Kontexten konfiguriert und
  bei Erreichen sichtbar als Nichtverfügbarkeit oder Degradation gemeldet.

### Trust, Attestation und Materialization

- Nur ein besessener, sauberer und erfolgreich attestierter Checkout darf als
  reguläre Source-backed-Quelle verwendet werden.
- `Clean`, `Dirty` und `Unverified` behalten ihre Bedeutung über Transport,
  Acquisition, Cache, Provider und Snapshot hinweg. `Dirty` und `Unverified`
  werden nicht zu `Clean` umgedeutet.
- Attestation wird vor Materialisierung, vor Cache-Veröffentlichung, vor
  Pointer-Wechsel und nach den relevanten Kopier-/Publish-Schritten geprüft.
- Cache-Reuse und Refresh binden die Nutzung der geprüften Generation an einen
  request-eigenen Checkout beziehungsweise eine Materialization-Use-Lease.
  Eine Generation darf während Kopie, Attestation und Workspace-Aufbau nicht
  unbemerkt mutieren oder freigegeben werden.
- Eine Materialization-Lease schützt die benötigten Dateien und Handles über
  Attestation, Kopie und Snapshot-Erzeugung bis zum Ende der Nutzung.
- Der Statusparser verwirft einen Lone-CR-Datensatz; gültige CRLF-Daten bleiben
  gültig.
- Cancellation, InvalidData, Fehler beim Öffnen und Fehler beim Aufräumen
  schließen bereits erworbene Handles vollständig best effort; Cancellation
  wird anschließend weitergegeben.
- Ein einzelner Dispose-Fehler darf die Bereinigung der übrigen Ressourcen
  nicht verhindern. Cleanup-Fehler bleiben diagnostisch sichtbar.
- Eine vollständige Namespace-Sperre gegen einen gleichberechtigten,
  konkurrierenden Prozess ist unter dem vereinbarten lokalen Bedrohungsmodell
  kein Muss. Ownership, Pfadregeln, Attestation und fail-closed-Verhalten
  bleiben verpflichtend.

### Roslyn-Parität und Capability-Matrix

„Gemeinsam“ bedeutet denselben zentralen Roslyn-/MCP-Codepfad und dieselben
Resultmodelle, nicht künstliche Gleichheit für fehlende Kontexte:

| Toolgruppe | Aktuelles Projekt | Externe Source-Solution | Externe dekompilierte Assembly |
|---|---|---|---|
| Symbolsuche, Skeleton, Klassenstruktur | vollständige Solution-/Projektansicht | vollständige gemappte Solution mit ausgewähltem Assembly-Projekt | dekompilierte Typen und Member |
| `get_symbol_body` | Original-Source-Body | Source-Body mit Origin | dekompilierter Body mit Hinweis |
| `find_references`, `get_call_tree` | Solution-Graph | verfügbare Source-Solution und auflösbare externe Ziele | Decompilation-Graph und auflösbare externe Nodes |
| `dependency_graph` | Projekt-/Dateiabhängigkeiten | Solution-Abhängigkeiten und externe Referenzen | rekursiv aufgelöste Assembly-Referenzen |
| Metriken | Source-Dokumentsicht | Source-Dokumente mit `source-backed`-Origin | dekompilierte Documents mit `decompiled`-Origin |
| `get_violations`, `pattern_detect` | Regeln aus Projektkonfiguration | nur mit ausdrücklich belastbarer Source-/Regelkonfiguration | nur mit explizitem Assembly-Regelprofil, sonst unsupported |
| Testkontext | aktuelle Solution | höchstens read-only geladene Testdokumente, keine Fremdtestausführung | standardmäßig nicht verfügbar |
| Git-/Change-Impact | Git-Diff und Projektkontext | unsupported für externe Quellen | unsupported |
| `safeguard` und Audits | aktueller Quality-/Regelkontext | nur mit eigenem explizitem Scanvertrag | nur mit eigenem explizitem Scanvertrag |

Nicht unterstützte Kombinationen liefern einen eindeutigen Unsupported- oder
Partialitätsvertrag, keinen leeren Erfolg. Die Capability-Matrix wird für alle
im MCP vorhandenen Toolfamilien vollständig und nutzerverständlich
dokumentiert.

Assembly-spezifische Werkzeuge wie `inspect_assembly` und
`find_assembly_extensions` behalten ihren speziellen Output, verwenden aber
denselben Assembly-Session-Dispatcher. Ein optionaler Consumer-Kontext wird
nicht als versteckter zweiter Root weitergeführt.

### Origin, Symbolidentität und Antworten

- Jede externe Antwort weist mindestens `origin`, Source-Pfad, Assembly-Hash,
  Source-Snapshot/Commit, generierten Pfad und Confidence aus.
- Decompilierte Bodies enthalten einen kurzen standardisierten Hinweis auf die
  mögliche Abweichung von der Originalquelle.
- Symbolidentitäten verwenden Target-/Generationsherkunft, beispielsweise
  `assembly:<sha256>:M:Vendor.Service.Save`, und werden nicht allein über einen
  flüchtigen Cachepfad identifiziert.
- Folgeabfragen berücksichtigen Assembly-Hash und Generation, damit eine
  veraltete Symbol-ID nicht blind auf eine neue DLL angewendet wird.
- `complete`, `partial`, `degraded` und `failed` werden von `origin` und Trust
  getrennt ausgewiesen.
- Origin-Bezeichnungen werden an einer zentralen Konvention ausgerichtet. Der
  interne `AssemblyOrigin.Kind`-Alias wird entweder kompatibilitätssicher
  entfernt oder bewusst beibehalten und dokumentiert; eine unbewiesene
  Löschung ist nicht zulässig.

## Muss-Kriterien

- Das gesamte Zielbild ist über einen einzigen lokalen MCP-Daemon erreichbar.
- Höchstens vier residente Benutzer-Projektkontexte zählen gegen
  `MaxProjects`; externe Quellen zählen nicht dagegen und sind logisch nicht
  auf vier begrenzt.
- Eine unbekannte absolute DLL kann ohne `ainetlinter.project.json` direkt über
  `targetType=assembly` analysiert werden.
- Ein explizit gemapptes und attestiertes Git-/Gitea-Source-Snapshot gewinnt
  gegen Decompilation; ohne belastbaren Match greift der transparente
  Decompilationspfad.
- Mapping, Source-Projekt, AssemblyName, Commit und Solution-Pfad werden
  eindeutig validiert; Gitea-Discovery bleibt ausgeschlossen.
- Direkte und transitive externe Referenzen werden metadata-only,
  bedarfsgesteuert, dedupliziert und mit sichtbaren Missing-/Cycle-/Partial-
  Zuständen aufgelöst.
- Mehrere Target-Aliasse teilen identische Source-Snapshots, ohne Consumer-
  oder Target-Leases unzulässig zu vermischen.
- Die Kernabfragen für Symbole, Bodies, Struktur, Referenzen, Call Trees,
  Dependency Graphs und Metriken verwenden denselben Roslyn-/MCP-Kern.
- Die vollständige Capability-Matrix für alle vorhandenen Toolfamilien weist
  supported, partial und unsupported fachlich korrekt aus.
- Source-, Decompilation-, Trust-, Generation-, Ownership- und
  Snapshot-Lebenszeit bleiben in Antworten und Diagnosen erkennbar.
- Cache, Refresh, Manifest, Pointer und Generationen sind hashgeprüft,
  atomar, versioniert und gegen halbfertige Veröffentlichung geschützt.
- Statusparser, Attestation und Materialization-Lease sind bei allen
  festgelegten Fehler- und Cancellation-Pfaden fail-closed und
  ressourcensicher.
- Kein Assembly-Code wird geladen, reflektiert oder ausgeführt.
- Der lokale Default-Host kann konfigurierte öffentliche Git-/Gitea-Quellen
  verwenden. Private Quellen werden nur über eine getrennte, injizierbare
  Credential-Auflösung ergänzt; Geheimnisse erscheinen nicht in Diagnosen,
  Manifesten oder Konzeptdaten.
- Externe Quellen und generierte Documents bleiben read-only.
- Persistente Cache-Bereinigung, lokale Branch-Zusammenführung und fremde
  Testausführung bleiben außerhalb des Funktionsvertrags.

## Akzeptanzkriterien

- Ein unbekannter DLL-Pfad funktioniert ohne Projektdefinition und liefert eine
  residente, statische Roslyn-Analyse mit Origin und Zustand.
- Ein gemapptes Repository mit mehreren DLL-Projekten wird über Solution und
  `AssemblyName` eindeutig aufgelöst; Mehrdeutigkeit wird nicht still gewählt.
- Eine gemappte Quelle wird nicht zusätzlich decompiliert; eine nicht gemappte
  oder nicht belastbar passende DLL fällt auf Decompilation zurück.
- Direkte Analyse und Analyse derselben externen Assembly aus einem
  Benutzer-Projekt teilen den identischen Source-Snapshot, bleiben aber
  getrennte Consumer-/Target-Kontexte.
- `foo.dll -> bar.dll` lädt `bar.dll` bei Bedarf source-backed oder decompiliert,
  dedupliziert bereits geladene Quellen und meldet fehlende oder zyklische
  Referenzen sichtbar.
- Mehrere parallele Erstzugriffe erzeugen nur eine relevante Session,
  Source-Solution-Materialisierung oder Decompilation.
- Eine geänderte DLL erzeugt eine neue Generation; laufende alte Leases bleiben
  gültig. mtime-only-Änderungen lösen keine unnötige Decompilation aus.
- Ein fälliger, fehlgeschlagener Gitea-Refresh gibt den alten Stand nicht still
  als aktuell aus. Der Zustand ist degraded oder fällt auf Decompilation/
  Fehler zurück.
- Statusparser-Tests weisen Lone-CR als ungültig und gültiges CRLF als gültig
  nach.
- Cancellation-, InvalidData-, Öffnungs- und Dispose-Fehlertests weisen nach,
  dass keine bereits erworbenen Handles oder temporären Checkouts verbleiben.
- Vier Benutzer-Projektkontexte werden getrennt von mehreren externen DLL- und
  Git-/Gitea-Quellen verwaltet; Ressourcenengpässe werden sichtbar und ohne
  stillen Kontextverlust behandelt.
- Für jede Capability-Matrix-Zeile gibt es positive, partielle und fachlich
  nicht unterstützte Tests beziehungsweise einen begründeten Vertrag.
- Toolantworten unterscheiden Projektquelle, Source-backed-Quelle und
  Decompilation sowie `complete`, `partial`, `degraded` und `failed`.
- `inspect_assembly` und `find_assembly_extensions` verwenden den gemeinsamen
  Sessionpfad ohne Verlust ihres speziellen Outputs.
- Keine Testausführung lädt die untersuchte oder eine fremde Assembly in den
  MCP-Prozess.
- Dokumentation und MCP-Toolbeschreibungen enthalten den finalen Target-,
  Source-, Cache-, Trust-, Capability- und Fallback-Vertrag ohne Legacy-
  Parameter oder widersprüchliche Aussagen.
- Vor Abschluss sind Build, vollständige Fast- und Integration-Tests ohne
  `Stress`, MCP-Safeguard-/Violation-Prüfungen und der projektinterne
  DRY-/Drift-/Dead-Code-/Magic-Value-Audit erfolgreich.

## Non-Goals und bewusste Grenzen

- Kein Laden, Ausführen oder Reflection-basierter Betrieb fremder Assemblies
  und kein `AssemblyLoadContext`.
- Keine Rekonstruktion des ursprünglichen Buildprozesses, der ursprünglichen
  Projektdateien, PDB-Garantien oder SourceLink-Gleichheit.
- Keine freie Gitea-Discovery, kein Repository-Matching allein nach Namen und
  keine automatische Auswahl unbekannter Source-Repositories.
- Kein versteckter Consumer- oder Änderungskontext für externe Quellen und
  keine automatische Ermittlung des Benutzer-Projekts, in dem eine DLL genutzt
  wird.
- Keine automatische Nutzung lokaler dirty-, uncommitted- oder unbuilt-
  Checkouts als alternative Source-of-Truth.
- Keine automatische Branch-Zusammenführung oder Multi-Branch-
  Synchronisation.
- Kein Git-Diff-, Change-Impact-, externer Testausführungs- oder allgemeiner
  Quality-Gate-Vertrag für externe Quellen ohne eigenen expliziten Kontext.
- Keine pauschale Gleichbehandlung von Regel-, Audit-, Test-, Dead-Code-,
  Duplicate- oder Pattern-Tools ohne fachlich belastbare Quelle und Vertrag.
- Keine vollständige Namespace-Immutability oder Garantie gegen einen
  bösartigen lokalen Administrator beziehungsweise konkurrierende Prozesse
  mit denselben lokalen Rechten.
- Keine privilegierte Windows-Reparse-Umgebung als allgemeine Start- oder
  CI-Voraussetzung.
- Keine automatische Garbage-Collection oder umfassende Retention-Architektur
  für persistierte externe Cache-Einträge.
- Keine neue allgemeine Plugin- oder Artefakt-Framework-Schicht.
- Keine Erstellung einer separaten Roadmap, von Step-Dateien oder eines
  zusätzlichen Task-States für dieses Vorhaben.

## Betroffene Projektbereiche und Dokumente

Die zentrale Target-, Session- und Toolroute betrifft insbesondere:

- `src/AiNetLinter/Mcp/Tools/AnalysisToolCall.cs`;
- `src/AiNetLinter/Mcp/Tools/AnalysisTargetResolver.cs`;
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/`;
- `src/AiNetLinter/Mcp/Assemblies/Analysis/`;
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/`;
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Providers/`;
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Snapshots/`;
- Registry-, Health-, Lease- und Snapshot-Lifecycle;
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

Die Dokumentationspflicht umfasst nach Umsetzung mindestens:

- `README.md`;
- `Docs/agent-api.md`;
- `Docs/integration.md`;
- `Docs/configuration.md`;
- `Docs/ROADMAP.md` als bestehende Projektdokumentation, sofern der
  implementierte Meilenstein dort synchronisiert werden muss;
- `Docs/rationale.md` und sonstige relevante Architektur-/Rationale-
  Dokumentation;
- alle MCP-Toolbeschreibungen und den MCP-Bootstrap;
- `.agents/rules/AiNetLinter-McpWorkflow.mdc` für den finalen
  Entscheidungs- und Arbeitsablauf;
- `.agents/rules/AiNetLinter.mdc` nur über die vorgeschriebene Generierung,
  falls `rules.json` geändert wird;
- `Docs/ROADMAP.md` nur als bestehende Projektdokumentation, falls der
  implementierte Meilenstein dort synchronisiert werden muss.

Die Dokumentation muss den Target-Vertrag, die drei Quellpfade, das Mapping-
Schema, Source-of-Truth, Commit-/Snapshot-Identität, Cache-/Refresh-Vertrag,
vier Benutzer-Kontexte, externe Quellen, transitive Referenzen,
Capability-Matrix, Origin/Confidence, Trust-/Partialitätszustände,
No-Execution-Grenze, Fallbacks, Sicherheitsgrenzen und bekannte Nicht-
Unterstützungen erklären.

## Betriebs- und Bedrohungsmodell

Der MCP-Daemon läuft lokal beim Entwickler und bedient den aktiven
Projektkontext sowie read-only externe Nachschlagequellen. Bis zu vier
Benutzer-Repositories sind residente Projektkontexte. Externe DLLs,
Source-Solutions und transitiv geladene Referenzen sind davon getrennt und
logisch nicht auf vier Einträge begrenzt.

Remote-Repository-Inhalte, DLLs, PDBs, `.deps.json` und benachbarte Referenzen
sind untrusted input. Das System schützt insbesondere vor falschen,
beschädigten, unvollständigen oder nachträglich veränderten Snapshots,
gefährlichen Pfaden, Reparse-/Link-Ausweichungen, falscher Source-Zuordnung,
stiller Versionsvermischung, unvollständiger Cache-Veröffentlichung und
unbeabsichtigter Codeausführung.

Credentials werden nur flüchtig über eine getrennte Resolvergrenze gebunden und
niemals in Diagnosen, Prozessargumenten, Cachemanifesten oder Konzeptdaten
persistiert. Eine vollständige Abwehr gegen einen bösartigen lokalen
Administrator oder einen gleichberechtigten konkurrierenden Prozess ist nicht
Teil des Modells.

## Fehler-, Fallback- und Lebenszeitsemantik

- Ungültige Konfiguration endet terminal und wird nicht durch Decompilation
  kaschiert.
- Fehlendes Mapping, nicht erreichbare optionale Quelle oder fehlender
  belastbarer Match führen grundsätzlich zur statischen Decompilation, sofern
  die DLL selbst analysierbar ist.
- Ein invalidierter oder widersprüchlicher Source-Stand darf nicht als
  Source-backed-Erfolg weitergereicht werden.
- Nur `Clean` und attestierte Source-Snapshots werden regulär source-backed
  verwendet. `Dirty` bleibt sichtbar; `Unverified` bleibt unverified.
- Decompiler-, Referenz-, Netzwerk-, Authentifizierungs-, Cache- und
  Ressourcenfehler werden als `partial`, `degraded`, `failed`, unsupported
  oder terminaler Konfigurationsfehler klassifiziert.
- Cancellation wird nach best-effort-Cleanup weitergegeben.
- Request-, Checkout-, Snapshot- und Workspace-Leases bleiben verschachtelt
  nachvollziehbar. Keine Lease wird vor dem Ende der abhängigen Nutzung
  freigegeben.
- Cachegenerationswechsel sind atomar. Laufende alte Leases bleiben gültig;
  neue Aufrufe sehen nur vollständige Generationen.
- Ein letzter guter Stand darf nach einem fälligen Refresh-Fehler nur als
  sichtbar degradierter Nachweis dienen, nicht als still aktuelle Quelle.
- Bei erschöpften externen Ressourcen wird der konkrete Vorgang kontrolliert
  abgewiesen oder degradiert; aktive Benutzer- oder externe Kontexte werden
  nicht still überschrieben.

## Verifikation und Abschluss

Die Verifikation erfolgt mit vorhandener Testinfrastruktur und injizierbaren
Test-Doubles:

- FastTests/Unit für Target-Parsing, Mapping, Identitäten, Fingerprints,
  Cache-Keys, Origin, Zustände, Parser und Lease-Cleanup;
- FastTests/Component für deklarative Roslyn-Solutions, synthetische DLLs,
  Source-backed-/Decompilation-Pfade und gemeinsame Fixtures;
- Integration/MCP für echten Datei-I/O-, lokalen Git-Test-Provider-, Host-,
  Refresh-, Snapshot- und Cross-Target-Vertrag;
- Performance-/Stress-Tests separat, hohe Last ausschließlich unter `Stress`.

Tests restaurieren, laden oder führen keine fremden Projekte oder Assemblies
aus. Netzwerk- und privilegierte Reparse-Tests werden deterministisch über
Doubles oder capability-gesteuert ergänzt. Neue Tests enthalten keine
unbounded Retries, Sleeps, impliziten Restores oder unkontrollierten
Netzwerkzugriffe.

Der Abschluss erfordert `dotnet build`, vollständige Fast- und
Integration-Testläufe mit `Category!=Stress`, passende MCP-Safeguard- und
Violation-Prüfungen sowie den projektinternen Audit auf DRY,
Refactoring-Drift, Dead Code und Magic Values. Diese Befehle werden im
Konzeptierungsdurchlauf nicht ausgeführt.

## Annahmen

- .NET 10, C#, Roslyn und Windows mit lokalen absoluten Projekt- und
  Assemblypfaden bilden die Laufzeitbasis.
- Ein einzelner MCP-Daemon läuft lokal beim Entwickler.
- Die maximale Zahl von vier residenten Benutzer-Projektkontexten ist von
  externen Source-/Assembly-Sessions getrennt.
- Externe Quellen sind read-only und logisch beliebig zahlreich; ihre konkrete
  Nutzung unterliegt sichtbaren Ressourcen- und Lebenszeitregeln.
- `ainetlinter.project.json` gilt für das aktuelle Projekt, nicht als Pflicht
  für eine externe DLL.
- Nur explizite Git-/Gitea-Mappings können eine Source-backed-Quelle auswählen.
- Öffentliche Quellen funktionieren ohne persistierte Credentials; private
  Quellen verwenden eine injizierbare Resolvergrenze.
- Der Standard-Branch und der tatsächlich geladene Commit bilden den
  reproduzierbaren Source-Snapshot.
- Direkte und transitive Referenzen werden best effort, metadata-only und
  bedarfsgesteuert behandelt.

## Verbindliche Detailentscheidungen innerhalb des Tasks

Die folgenden Punkte dürfen nicht verloren gehen oder still entfallen. Sie
blockieren nicht den Konzeptstart, müssen aber vor Abschluss der jeweiligen
Implementierung entschieden, umgesetzt, getestet und dokumentiert werden:

- konkrete externe Ressourcenbudgets und TTL-/LRU-Regeln neben der
  Vierergrenze;
- konkrete Credential-Resolver-Anbindung für private Git-/Gitea-Quellen;
- endgültige Property-Namen und strikte Validierung des Mapping-Schemas;
- Default-Suchscope in einer vollständigen gematchten Source-Solution;
- Tiefe-, Größen-, Zyklus- und Abbruchgrenzen der transitiven Auflösung;
- genaue Test-/Regel-/Audit-/Safeguard-Capabilities je Quellenherkunft;
- endgültige Origin-String-Konvention und sichere Behandlung des internen
  Origin-Alias;
- konkrete Decompiler-Optionen, Document-Aufteilung und Manifestdetails.

Diese Punkte sind keine optionalen Nachfolgevorhaben. Sie sind begrenzte
Detailentscheidungen innerhalb des vollständigen Tasks und müssen in dessen
Code, Tests und Dokumentation sichtbar abgeschlossen werden.

## Bewertung der geprüften Punkte

**Weiterhin relevant und vollständig umzusetzen:**

- gemeinsamer Target-/Roslyn-/MCP-Kern für Projektquelle, gemappte
  Source-Solution und Decompilation;
- Default-Host-Provider, Git-/Gitea-Source-of-Truth, Authentifizierung,
  Refresh, Cache, atomare Generationen und Fallbacks;
- vier residente Benutzer-Kontexte getrennt von beliebig vielen externen
  DLL-/Source-Sessions;
- Source-Match, AssemblyName-/Solution-Auflösung, Snapshot-Identität und
  Alias-Sharing;
- transitive Referenzen, Deduplizierung, Zyklen und Missing-Reference-
  Zustände;
- Capability-Matrix, Origin-/Confidence-/Partialitätsantworten und
  assembly-spezifische Tools im gemeinsamen Sessionpfad;
- Lone-CR-Korrektur, vollständiges Lease-/Cleanup-Verhalten und Generation-
  Attestation;
- vollständige Dokumentation, Bootstrap-/Toolbeschreibungen, Regelabgleich und
  Abschlussverifikation.

**Bereits umgesetzt und als Basis zu erhalten:**

- Target-Basis und grundlegender gemeinsamer Dispatch;
- statische Decompilation, Fingerprint, Cache-Grundlage und direkte
  Referenzauflösung;
- Mapping, Source-Selection, Gitea-Transport, Acquisition, Cache-Reuse,
  Refresh, Attestation und Snapshot-Lebenszeit als vorhandene Bausteine;
- fail-closed-Konfigurationspfad und Erhalt der Dirty-Trust-Semantik;
- zentrale Pfad-, URL- und Test-Snapshot-Hilfen.

**Veraltet oder nicht mehr als offene Grundlage zu behandeln:**

- die grundsätzliche Auswahl der Decompiler-Bibliothek;
- die grundlegende Mapping-, Source-Selection-, Cache- oder Attestation-
  Architektur;
- die Duplikationsannahme bei der zentralen Pfadprüfung;
- kleinteilige Implementierungsabfolgen und frühere Statusstände.

**Bewusst nicht als vergessene Lücke übernommen:**

- Runtime-/Reflection-Loading;
- freie Gitea-Discovery;
- lokaler dirty-/unbuilt-Checkout als versteckte Source-of-Truth;
- automatische Branch-Zusammenführung;
- vollständige Namespace-Sperre gegen gleichberechtigte Prozesse;
- automatische persistente Cache-Garbage-Collection;
- externe Testausführung und fachlich unpassende Git-Diff-/Quality-Gate-
  Scheinimplementierungen.

## Übergabestatus

Der Draft bildet nun den vollständigen fachlichen Abschlussumfang einschließlich
der bislang offenen technischen und dokumentarischen Themen ab. Er bleibt bis
zur ausdrücklichen Nutzerfreigabe auf `status: draft`. Vor der Freigabe wird
das Draft-Arbeitsgedächtnis entfernt; nur dauerhafte Anforderungen,
Annahmen, Grenzen und verbindliche Detailentscheidungen bleiben erhalten.
Ein Orchestrator wird nicht automatisch gestartet.
