# Tech-Debt-Register

## Queue

### E1-BUG-01 — Verdeckte Referenzexpansion bei Extension-Suche

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: öffentliche Registrierung und gemeinsamer Assembly-
  Dispatch von `find_assembly_extensions`.
- Evidenz: Öffentliche Signatur ohne `includeReferences`, interner Dispatch
  setzt die Expansion fest auf aktiv; siehe `epic-01-mcp-vertraege.md`.
- Nächster Schritt: In einem separaten Umsetzungstask fachlich entscheiden,
  ob ein sichtbarer Root-Default oder ein explizit dokumentierter Pflicht-
  Referenzmodus gilt.
- Log-Anker: Epic 1 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Dieses Task-Konzept erlaubt keine Code-, Test-,
  Konfigurations- oder Dokumentationsänderung.

### E1-BUG-02 — Abweichendes dokumentiertes Response-Budget

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyAnalysisResponseLimits` und öffentliche
  Assembly-Dokumentation.
- Evidenz: Implementierter Grenzwert und dokumentierter Grenzwert stimmen
  nicht überein; siehe `epic-01-mcp-vertraege.md`.
- Nächster Schritt: Autoritative Budgetquelle festlegen und nachgelagert Code
  oder Dokumentation konsistent aktualisieren.
- Log-Anker: Epic 1 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E7-BUG-01 — Assembly-Fehlerpfad redigiert Rohpfade und Rohdiagnosen nicht

- Schweregrad: P1; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: Assembly-Service, Reference-Resolver, Sessiondiagnosen,
  `AnalysisToolCall` und Health-Projektion.
- Evidenz: Kontrollierter Fehler mit synthetischem Marker gab den Marker im
  Text/Structured Content zurück; Rohpfade, Exception-Messages und Diagnosen
  werden ohne zentrale Redaction aggregiert.
- Nächster Schritt: Zentralen typed-error-/Redaction-Projektionspfad für
  Fehler, Diagnosen und Health festlegen.
- Log-Anker: Epic 7 Implementiererbericht, 2026-09-02.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E7-BUG-02 — Interne Creation-Cancellation wird als harter Toolfehler klassifiziert

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyAnalysisRegistry` und Source-Project-Lease-
  Coordinator.
- Evidenz: Nicht vom Caller ausgelöste interne Cancellation wird über den
  Default von `Failure` als `isError=true`/harter Aufbaufehler projiziert.
- Nächster Schritt: Caller-Cancellation, Lifecycle-Abbruch und Provider-/IO-
  Fehler als getrennte typed Zustände modellieren.
- Log-Anker: Epic 7 Implementiererbericht, 2026-09-02.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E7-OPT-01 — Eviction-/Lifecycle-Koordinator überschreitet MCP-Footprint

- Schweregrad: P3; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyAnalysisRegistryEvictionCoordinator`.
- Evidenz: `get_violations` meldete im Assembly-Analyse-Scope einen
  `AIContextFootprint`-Treffer oberhalb des Grenzwerts.
- Nächster Schritt: Nur in einem Umsetzungstask Lifecycle-Fassaden und
  Retirement-/Capacity-/Health-Verantwortungen risikoarm schneiden.
- Log-Anker: Epic 7 Implementiererbericht, 2026-09-02.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E7-MF-01 — Assembly-Health weist Lifecycle und Recoverability unvollständig aus

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: Assembly-Health-Snapshot, Projection und Server-Health-
  Response.
- Evidenz: Health zeigt Status/Origin/Generation/Diagnosen, aber keine typed
  Fehlerklasse, Recoverability, Lease-/Operation-, Resident-/Eviction- oder
  Resource-Ist-/Limitwerte.
- Nächster Schritt: Optionales, bounded `assemblyOperational`-Objekt mit
  redigierten Status- und Zählerfeldern spezifizieren.
- Log-Anker: Epic 7 Implementiererbericht, 2026-09-02.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E6-BUG-01 — Response-Budget prüft Kanäle getrennt statt CallToolResult gesamt

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyAnalysisResponseLimits`, `AssemblyAnalysisResponse`
  und Response-Builder.
- Evidenz: Text und Structured Content liegen jeweils unter 8192 Byte, ihre
  Summe überschreitet den Grenzwert mehrfach; es gibt keine Gesamtmessung.
- Nächster Schritt: Gesamtbudget über tatsächliche Response-Hülle festlegen
  oder kanalweise Budgets eindeutig benennen und dokumentieren.
- Log-Anker: Epic 6 Implementiererbericht, 2026-09-02.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E6-BUG-02 — Irreduzible feste Metadaten können Budget überschreiten

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyAnalysisResponseLimits.Budget` und feste
  Assembly-Payloadfelder.
- Evidenz: Nach Entfernung optionaler Listen fehlt ein terminaler bounded
  Fallback, wenn feste Pfad-/Identitäts-/Statuswerte weiterhin nicht passen.
- Nächster Schritt: Fixed-Metadata-Budgets und maschinenlesbaren
  `irreducibleBudget`-Zustand definieren.
- Log-Anker: Epic 6 Implementiererbericht, 2026-09-02.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E6-OPT-01 — Einzelweises Trimming serialisiert wiederholt

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyAnalysisResponseLimits.Budget`.
- Evidenz: Jede Einzelentfernung formatiert und serialisiert Text/JSON erneut.
- Nächster Schritt: Fixed-Overhead und Quoten in einem bounded Pass oder mit
  begrenzter Suchstrategie berechnen.
- Log-Anker: Epic 6 Implementiererbericht, 2026-09-02.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E6-OPT-02 — Query-Limits begrenzen vorgelagerte Arbeit nicht

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyAnalysisService` sowie Snapshot-/DTO-Aufbau.
- Evidenz: Typen, Extensions und Member werden vor `Take(maxResults/maxMembers)`
  vollständig gesammelt, sortiert oder projiziert.
- Nächster Schritt: Semantisch sichere frühe Filterung, Streaming oder bounded
  Auswahlstrukturen prüfen.
- Log-Anker: Epic 6 Implementiererbericht, 2026-09-02.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E6-OPT-03 — Referenzarbeit über sichtbare Grenze hinaus amplifiziert

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyReferenceResolver`, Session-Expander und
  Response-Limits.
- Evidenz: Vor der Projektion werden deutlich mehr Referenzen/Sessions besucht
  und diagnostiziert als sichtbar ausgegeben werden können.
- Nächster Schritt: Hard-Cap für Kanten/Sessions/Boundary-Einträge und
  gemeinsame Kostenstrategie definieren.
- Log-Anker: Epic 6 Implementiererbericht, 2026-09-02.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E6-OPT-04 — Diagnose-Samples sind nicht byteeffizient repräsentativ

- Schweregrad: P3; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: Diagnoseauswahl in `AssemblyAnalysisResponseLimits`.
- Evidenz: Root-first-Prefix-Scan bricht beim ersten Byteüberlauf ab und prüft
  spätere kürzere oder neue Samples nicht mehr.
- Nächster Schritt: Bounded Coverage-/Round-Robin-Auswahl unter Beibehaltung
  von Counts und Deduplizierung bewerten.
- Log-Anker: Epic 6 Implementiererbericht, 2026-09-02.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E6-MF-01 — Maschinenlesbare Budgettelemetrie fehlt

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: Assembly-Payload, Budgetprojektion und Enrichment.
- Evidenz: `responseBudget` signalisiert nur die Ursache; Byte-Limits,
  Kanal-/Gesamt-Istwerte und Trim-Anteile werden nicht ausgegeben.
- Nächster Schritt: Optionales, selbst bounded Budgetobjekt mit redigierten
  Counts, Limits, Istwerten und Ursachen spezifizieren.
- Log-Anker: Epic 6 Implementiererbericht, 2026-09-02.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E6-MF-02 — Namespace-Trimming nicht feldspezifisch sichtbar

- Schweregrad: P3; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `InspectAssemblyPayload`, Namespace-Trimming und Formatter.
- Evidenz: Keine Gesamt-/Shown-/Truncated-Werte für Namespaces; nur der
  allgemeine `responseBudget`-Grund bleibt sichtbar.
- Nächster Schritt: Feldspezifische Counts und Trunkierungsursachen in Text und
  Structured Content aus derselben Projektion ergänzen.
- Log-Anker: Epic 6 Implementiererbericht, 2026-09-02.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E4-BUG-01 — Ressourcen-Dimensionen bei Refresh nicht generationsgebunden

- Schweregrad: P1; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyAnalysisRegistry`, `AssemblyAnalysisResourceBudget`
  und `ExternalResourceRegistry`.
- Evidenz: Pfadbasierte Ressourcenidentität übernimmt bei Content-Wechsel nicht
  zuverlässig die neue Disk-/Memory-Anforderung während Alt-Leases laufen.
- Nächster Schritt: Generationsgebundene oder konservative Accounting-Übergabe
  mit Size-Change-/Alt-Lease-Test spezifizieren.
- Log-Anker: Epic 4 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E4-BUG-02 — Kein Stabilitätscheck vor Generation-Commit

- Schweregrad: P1; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyAnalysisSession.RefreshCoreAsync` und
  Fresh-/Cache-Generation-Installationspfade.
- Evidenz: Fingerprint wird vor langem Read-/Decompilation-Fenster ermittelt,
  ein zweiter Check vor Publish/Install fehlt.
- Nächster Schritt: In-Flight-Dateiänderung kontrolliert prüfen und Commit-
  Grenze mit rollback-sicherem Verhalten festlegen.
- Log-Anker: Epic 4 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E4-BUG-03 — Retirement-Fehler werden verschluckt

- Schweregrad: P1; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `RetireEntryAsync` und Registry-Disposal-Aggregation.
- Evidenz: Retirement fängt Entry-Disposal-Exceptions und lässt den Task
  erfolgreich erscheinen; zentrale Aggregation kann sie dadurch nicht sehen.
- Nächster Schritt: Fehler wie Pending-Entry-Fehler aggregieren oder sichtbaren
  Degraded-/Quarantäne-Zustand definieren.
- Log-Anker: Epic 4 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E4-BUG-04 — Session-Disposal kann Refresh-Race auslösen

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyAnalysisSession.RefreshAsync`, `Dispose` und
  `DisposeAsync`.
- Evidenz: Gate-Disposal ist nicht gegen wartende/laufende Refreshes drainend
  synchronisiert; spätes `Release` oder `WaitAsync` kann ungeplant scheitern.
- Nächster Schritt: Zweistufiges Shutdown- und Drain-Protokoll mit Race-Test.
- Log-Anker: Epic 4 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E4-BUG-05 — Cancellation ohne Commit-Grenzpunkt

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `BuildFreshGenerationAsync` und
  `CreateAndInstallGenerationAsync`.
- Evidenz: Cancellation wird vor Decompilation weitergereicht, aber direkt
  vor synchronem Cache-Publish/Install nicht erneut geprüft.
- Nächster Schritt: expliziten Cancellation-Commitpunkt und Rollback-/Late-
  Cancellation-Semantik definieren.
- Log-Anker: Epic 4 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E4-OPT-01 — Abgeschlossene Retirement-Tasks bleiben referenziert

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyAnalysisRegistry.retiredEntries`.
- Evidenz: Retirement-Tasks werden gesammelt, aber im Normalbetrieb nicht nach
  Abschluss entfernt.
- Nächster Schritt: abgeschlossene Tasks unter dem bestehenden Gate entfernen.
- Log-Anker: Epic 4 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E4-OPT-02 — Generation-Counter wächst pro Pfad unbegrenzt

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyAnalysisRegistry.nextGenerations`.
- Evidenz: Counter bleiben für jeden jemals gesehenen kanonischen Pfad, auch
  nach Eviction und Lease-Drain, dauerhaft im Dictionary.
- Nächster Schritt: bounded Epoch-/Identitätsstruktur ohne stale-ID-Reuse prüfen.
- Log-Anker: Epic 4 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E4-OPT-03 — Registry- und Cache-Pfad-Case uneinheitlich

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: Registry-Map, Fingerprint/Cache-Key und
  `AssemblyDecompilationCache`.
- Evidenz: Registry führt Pfadschreibweisen zusammen, Cache-Identity kann sie
  als unterschiedliche Key-Verzeichnisse behandeln.
- Nächster Schritt: plattformgerechte Cache-Identity mit case-sensitiver
  Dateisystemsemantik spezifizieren.
- Log-Anker: Epic 4 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E4-MF-01 — Kein Root-Cleanup für alte Content-Key-Verzeichnisse

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyDecompilationCache` und `AssemblyCacheCleanup`.
- Evidenz: Retention ist je Content-Key bounded, ein Root-/TTL-/Bytebudget-
  Cleanup über veraltete Key-Bäume fehlt.
- Nächster Schritt: lazy/periodischen sicheren Root-Cleanup mit Diskbudget und
  Last-Access-Vertrag bewerten.
- Log-Anker: Epic 4 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E4-MF-02 — Health zeigt keine Lifecycle-/Ressourcenmetriken

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: Assembly-Health-Snapshot und Health-Response-Builder.
- Evidenz: Lease-, Retirement-, Cache-Reuse-, Disk-/Memory- und Operation-
  Slot-Zähler des internen ResourceHealth sind nicht sichtbar.
- Nächster Schritt: bounded, redigierte Lifecycle-/Resource-Snapshots als
  optionale Health-Felder spezifizieren.
- Log-Anker: Epic 4 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E4-MF-03 — Kein hostweiter Gesamtblick getrennter Budgets

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyAnalysisHostComposition` und getrennte Resource-
  Registries.
- Evidenz: Session- und Source-Register besitzen getrennte Limits und keinen
  optionalen aggregierten Prozess-/Health-Wert.
- Nächster Schritt: Produktentscheidung zwischen bewusster Isolation und
  zusätzlichem hostweitem Accounting treffen.
- Log-Anker: Epic 4 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E3-BUG-01 — Starke Assembly-Identität unvollständig geprüft

- Schweregrad: P1; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyReferenceResolver` und Referenzmodell.
- Evidenz: Referenzkandidaten transportieren keinen Public-Key-Token und die
  Identitätsprüfung vergleicht nur Name, Version und Kultur; siehe
  `epic-03-referenzen-source-diagnosen.md`.
- Nächster Schritt: Starke Identität metadata-only übernehmen, vergleichen
  und bei Abweichung redigiert diagnostizieren.
- Log-Anker: Epic 3 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal; keine Korrekturschleife
  auf Nutzeranweisung.

### E3-BUG-02 — Referenzknotenlimit projiziert Zustand uneinheitlich

- Schweregrad: P1; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyReferenceResolver.VisitNode`/`VisitChild` und
  `AssemblyReferenceSessionExpander`.
- Evidenz: Früher Grenzreturn erzeugt keine Diagnose; später Grenzpfad
  diagnostiziert, ersetzt den zuvor eingefügten Kandidatenstatus aber nicht
  konsistent.
- Nächster Schritt: Atomare `node_limit`-Projektion mit einmaligem Boundary-
  Signal über Resolver, Session und Response abgleichen.
- Log-Anker: Epic 3 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E3-OPT-01 — Referenz-Expander überschreitet AIContext-Footprint

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyReferenceSessionExpander`.
- Evidenz: Gezielter `get_violations`-Check meldete im Epic-Scope einen
  `AIContextFootprint`-Wert oberhalb der Projektgrenze.
- Nächster Schritt: Verantwortungen für Traversierung, Leases und Projektion
  risikoarm trennen und denselben Violation-Check wiederholen.
- Log-Anker: Epic 3 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E3-OPT-02 — Kandidaten mehrfach gelesen

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyReferenceResolver.FindReferencePath`,
  `VisitChild` und Kandidatenenumeration.
- Evidenz: Identität, vollständige Metadaten und Verzeichnisenumeration werden
  in einem Resolve-Pfad wiederholt ausgeführt; quantitative Laufzeitwirkung
  wurde nicht behauptet.
- Nächster Schritt: Bounded Resolver-Session-Cache für kanonische Pfade und
  Identitäts-/Metadatenfehler bewerten.
- Log-Anker: Epic 3 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E3-MISSING-01 — Consumer-Kontext für Extension-Prüfung nicht bindbar

- Schweregrad: P1; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: registrierter Assembly-Dispatch und source-aware
  Overloads der Extension-Prüfung.
- Evidenz: Öffentliche Assembly-Aufrufe liefern `consumerProject=null` und
  `not_decidable`, obwohl alternate Dispatch-Pfade vorhanden sind.
- Nächster Schritt: Bounded optionalen Consumer-Projekt-/Solution-Kontext
  routebar machen und Origin-/Trust-/Partial-Signale erhalten.
- Log-Anker: Epic 3 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E3-MISSING-02 — Binary-zu-Source-Identität nicht attestiert

- Schweregrad: P1; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblySourceMatchResolver` und source-backed
  `AssemblyAnalysisContextFactory`.
- Evidenz: Nach Snapshot-/Mapping-/Aliasprüfung fehlt eine konkrete
  Binär-/Build-Identität für die Source-Zuordnung.
- Nächster Schritt: Redigiert vergleichbare Output-/Binary-Attestierung
  ergänzen oder bei fehlender Übereinstimmung sicher auf decompiled wechseln.
- Log-Anker: Epic 3 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E3-MISSING-03 — Keine konfigurierten Probe-Wurzeln für Dependencies

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyReferenceResolver` und
  `SourceProjectReferenceGraph`.
- Evidenz: Suche beschränkt sich auf Zielverzeichnis, TPA und Source-
  Projektgraph; vorhandene Source-/Output-Kontexte sind nicht als bounded
  Probe-Wurzeln adressierbar.
- Nächster Schritt: Vertrauensgebundene, kanonisierte und mengenmäßig
  begrenzte Probe-Wurzeln spezifizieren.
- Log-Anker: Epic 3 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E2-BUG-01 — Cache-Roundtrip verliert Dokument-Metadaten

- Schweregrad: P1; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyDecompilationCache` und `DecompiledDocument`.
- Evidenz: Der Cache schreibt nur relative Dateinamen und rekonstruiert beim
  Lesen Typname, Pfad und Token unvollständig; siehe
  `epic-02-decompilation-snapshot.md`.
- Nächster Schritt: In einem Umsetzungstask Fresh-/Cache-Roundtrip und
  Metadatenidentität korrigieren und testen.
- Log-Anker: Epic 2 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Das Konzept verbietet in diesem Lauf jede
  Code-/Teständerung und die vom Nutzer abbestellte Review-/Korrekturschleife.

### E2-BUG-02 — Uneindeutige Zeilenbasis in `get_class_structure`

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `GetClassStructureTool` und Struktur-Payload.
- Evidenz: `TotalLines` ist eine relative Typ-Spanne, Memberzeilen sind
  absolute Dokumentzeilen; die Payload kennzeichnet die Basis nicht.
- Nächster Schritt: Koordinatenvertrag festlegen und durch Strukturtests
  absichern.
- Log-Anker: Epic 2 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E2-BUG-03 — Konstruktor-ID nicht direkt an Body-Auflösung anschließbar

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AnalysisSymbolIdentity`, Skeleton- und Body-Resolver.
- Evidenz: Eine Skeleton-Konstruktor-ID führte zu `SYMBOL_NOT_FOUND`, die
  positionsbasierte ID desselben Members löste den Body auf.
- Nächster Schritt: Kanonischen Skeleton-zu-Body-Roundtrip für Konstruktoren
  und weitere Memberformen spezifizieren und testen.
- Log-Anker: Epic 2 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E2-OPT-01 — Wiederholte On-demand-Body-Dekomposition

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyDecompiledBodyResolver` und Decompiler-Fabrik.
- Evidenz: Jede Body-Anfrage erzeugt einen Body-fähigen Decompiler und
  dekompiliert den enthaltenden Typ erneut.
- Nächster Schritt: Bounded, generations-/hashgebundene Wiederverwendung
  unter Wahrung von Cancellation, TTL, Limits und Fehlerstatus bewerten.
- Log-Anker: Epic 2 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E1-BUG-03 — Unvollständige README-Discoverability für EXE-Targets

- Schweregrad: P3; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: Assembly-Hinweis in `README.md`.
- Evidenz: README nennt nur DLL, während der aktuelle Assembly-Vertrag DLL
  und EXE zulässt; siehe `epic-01-mcp-vertraege.md`.
- Nächster Schritt: Öffentliche Kurzbeschreibung im Umsetzungstask
  synchronisieren.
- Log-Anker: Epic 1 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E1-OPT-01 — Kontextabhängiger inspect_assembly-Referenzdefault

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `inspect_assembly`-Dispatch und Progressive Disclosure.
- Evidenz: Ungefilterte Abfrage expandiert Referenzen standardmäßig, obwohl
  der Progressive-Disclosure-Vertrag explizite Expansion nahelegt; siehe
  `epic-01-mcp-vertraege.md`.
- Nächster Schritt: Root-Default und Kosten-/Trunkierungswirkung fachlich
  festlegen.
- Log-Anker: Epic 1 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E1-MISSING-01 — Maschinenlesbare Assembly-Capability

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblyTool`-Registrierungsmetadaten und beide
  Assembly-Schemas.
- Evidenz: Assembly-Einschränkung erscheint als Beschreibungstext und
  Laufzeitvalidierung, nicht als separat belegte Schema-Capability; siehe
  `epic-01-mcp-vertraege.md`.
- Nächster Schritt: Rohes `tools/list`-Schema verifizieren und Capability-
  Darstellung für generische Clients fachlich spezifizieren.
- Log-Anker: Epic 1 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E5-BUG-01 — Root-Treffer werden durch globale Referenzsortierung verdrängt

- Schweregrad: P1; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblySymbolSearch.FindMatchesAsync` und
  `AssemblyNavigationSupport`.
- Evidenz: Bei `includeReferences=true` und kleinem `maxResults` wurden nur
  Referenztreffer angezeigt, obwohl Root-Treffer existierten; die globale
  Sortierung erfolgt vor der Kappung.
- Nächster Schritt: Root-first-Reservierung und getrennte Treffer-/Assembly-
  Trunkierungsgründe spezifizieren.
- Log-Anker: Epic 5 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E5-BUG-02 — Trefferlisten-Kappung wird als Assembly-Kappung markiert

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblySymbolSearch` und Navigation-Summary.
- Evidenz: Bei unverändert vollständigem Assembly-Suchraum wechselte
  `assembliesTruncated` allein durch `maxResults` von false zu true.
- Nächster Schritt: `resultsTruncated`/`truncatedBy` getrennt von
  `assembliesTruncated` projizieren.
- Log-Anker: Epic 5 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E5-BUG-03 — Referenz-Stable-ID ist für Body-Folgeabfrage nicht nutzbar

- Schweregrad: P1; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `AssemblySymbolSearch`, `SymbolIdentifierResolver` und
  Body-Tool-Registrierung.
- Evidenz: Eine referenzgebundene ID aus `find_symbol(includeReferences=true)`
  wurde vom `get_symbol_body`-Dispatch mit `INVALID_ARGUMENT` abgewiesen.
- Nächster Schritt: Referenz-Lease/Origin-Route im Body-Tool ergänzen oder
  solche IDs explizit als nicht weiterleitbar markieren.
- Log-Anker: Epic 5 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E5-BUG-05 — Response-Budgettrimming wird als Extension-Trunkierung markiert

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: `FindAssemblyExtensionsResponseBuilder` und
  `AssemblyAnalysisResponseLimits`.
- Evidenz: `extensions=[]`, `totalExtensions=0`, aber `truncated=true` wegen
  `responseBudget`, obwohl Begleitlisten und nicht Extension-Treffer gekürzt
  wurden.
- Nächster Schritt: Extension-Listen- und Response-Budget-Trunkierung mit
  eigenen Flags, Counts und Ursachen trennen.
- Log-Anker: Epic 5 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E5-MF-01 — Referenzsicht für Struktur- und Metriktools fehlt

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: Registrierungen für Hierarchie-, Struktur- und
  Metrikwerkzeuge.
- Evidenz: Nur Symbol-/Referenz-/Calltree-Pfade besitzen Assembly-
  `includeReferences`; Struktur-/Metrikpfade bleiben Root-only.
- Nächster Schritt: Gemeinsamen bounded Opt-in-Vertrag oder maschinenlesbare
  Root-only-Grenze definieren.
- Log-Anker: Epic 5 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E5-MF-02 — Signatur-only-Basis wird in Metrics/Calltree nicht hinreichend projiziert

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: Metrics-/Calltree-Response-Projektion.
- Evidenz: Globales `analysis` nennt `contentMode`/`bodyAvailability`, aber
  Metrics-/Tree-/Leerresultate tragen keine eigene Messbasis.
- Nächster Schritt: `measurementBasis` sowie unterscheidbare leere Calltree-
  Zustände spezifizieren.
- Log-Anker: Epic 5 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E5-MF-03 — Consumer-basierte Extension-Anwendbarkeit fehlt standalone

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Scope/Fundstelle: Assembly-Dispatch und `AssemblyAnalysisService.FindExtensions`.
- Evidenz: Standalone-Calls liefern ohne Consumer-Kontext höchstens
  `not_decidable`; `receiverType` ist nur ein Filter.
- Nächster Schritt: Expliziten bounded Consumer-/Projekt-Target-Kontext oder
  source-backed Consumer-Route definieren.
- Log-Anker: Epic 5 Implementiererbericht, 2026-09-01.
- Begründung der Disposition: Analyse-Only-Non-Goal.

### E8-BUG-01 — Negativtest verankert die falsche Redaction-Erwartung

- Schweregrad: P1; Disposition: `accepted-deferred`; attempts: 0
- Kategorie: Bug; Vertrauen: hoch; Größe: M
- Evidenz: `ManagedAssemblyBinaryTests.cs` erwartet im strukturierten
  Negativpayload den unveränderten Eingabepfad eines nicht verwalteten PE.
  Damit würde der Test eine spätere sichere Redaction als Regression werten.
- Aktion: Nach der technischen Korrektur aus E7-BUG-01 nur typisierten,
  recoverable Grund und das Fehlen konkreter Pfade/Exceptiontexte in Text und
  Structured Content assertieren.
- Abhängigkeit: E7-BUG-01. Keine Duplikation der technischen Ursache.
- Begründung der Disposition: Analyse-Only-Non-Goal; keine Teständerung in
  diesem Read-only-Lauf.

### E8-OPT-01 — Statische Testzuordnung unterschätzt indirekte Assembly-Abdeckung

- Schweregrad: P2; Disposition: `accepted-deferred`; attempts: 0
- Kategorie: Optimierung; Vertrauen: hoch; Größe: M
- Evidenz: `get_test_context` meldet für mehrere Assembly-Orchestrierungs-,
  Response- und Health-Hilfen `isUntested=true`, obwohl route-nahe Tests nur
  indirekt abdecken und überwiegend keine `@covers`-Markierung tragen.
- Aktion: Direkte, indirekte Route-Abdeckung und nur gelesene Artefakte im
  Testkontext getrennt ausweisen oder vorhandene Tests gezielt markieren.
- Begründung der Disposition: Analyse-Only-Non-Goal; keine Test- oder Mapper-
  Änderung in diesem Lauf.

### E8-MF-01 — Öffentlicher Assembly-Capability-Regressionstest fehlt

- Schweregrad: P1; Disposition: `accepted-deferred`; attempts: 0
- Kategorie: Missing Feature; Vertrauen: hoch; Größe: L
- Evidenz: Direkte Route- und Schema-Bausteine existieren, aber keine
  table-driven öffentliche MCP-Matrix über GIT-01, LOCAL-01 bis LOCAL-03 und
  FALSE-01 mit Root-/Referenzsicht, Folgeabfragen, Herkunft, Diagnosen,
  Trunkation und Redaction.
- Aktion: Redigierten öffentlichen Integrationstest mit den fünf opaken
  Labels ergänzen; konkrete externe Identitäten nicht versionieren.
- Begründung der Disposition: Analyse-Only-Non-Goal; keine neuen Tests in
  diesem Lauf.

### E8-MF-02 — Öffentlicher source-backed Vertrag ist nicht konfiguriert belegt

- Schweregrad: P1; Disposition: `accepted-deferred`; attempts: 0
- Kategorie: Missing Feature; Vertrauen: hoch; Größe: M
- Evidenz: Fake-/Component- und Host-Composition-Tests decken Source-/Fallback-
  Zustände ab; ein öffentlicher MCP-Nachweis über den konfigurierten GIT-01-
  Pfad fehlt.
- Aktion: Öffentlichen, redigierten Source-backed-Call mit Source-Erfolg und
  kontrolliertem Fallback als wiederholbaren Test verankern.
- Abhängigkeit: E3-MISSING-02 und E3-MISSING-03 bleiben technische
  Abgrenzungen.
- Begründung der Disposition: Analyse-Only-Non-Goal; kein externer Provider-
  Testlauf in diesem Audit.

### E8-MF-03 — Cache-Test prüft keinen semantischen Dokument-Roundtrip

- Schweregrad: P1; Disposition: `accepted-deferred`; attempts: 0
- Kategorie: Missing Feature; Vertrauen: hoch; Größe: M
- Evidenz: Der bestehende Session-Test prüft Manifest, Statusmarker und
  generierte Dateien, vergleicht aber keine semantischen `DecompiledDocument`-
  Metadaten und keine stabile ID-/Body-Folgeabfrage nach Cache-Restaurierung.
- Aktion: Fresh-vs-Cache-Snapshotvergleich inklusive Origin/Generation,
  Dokumentmetadaten, C#-Source und Body-Navigation ergänzen.
- Abhängigkeit: E2-BUG-01 bleibt die technische Ursache.
- Begründung der Disposition: Analyse-Only-Non-Goal; keine Teständerung in
  diesem Lauf.

### E8-MF-04 — Kritische Lebenszeit- und Health-Verträge ohne Regression-Matrix

- Schweregrad: P1; Disposition: `accepted-deferred`; attempts: 0
- Kategorie: Missing Feature; Vertrauen: hoch; Größe: L
- Evidenz: Einzelne Session-, Registry-, Retirement- und Health-Tests decken
  Teilpfade ab, aber keine zusammenhängende öffentliche Matrix für Refresh-
  Rennen, Publish-Entscheidung, Cancellation, Cleanup, Fehler-/Degraded-/
  Retirement-Health und Resource-/Lease-/Operationstelemetrie.
- Aktion: Gezielte fehlerinjizierende Fast-/Integration-Matrix ergänzen und
  Text, Structured Content, Status, Recoverability sowie Redaction gemeinsam
  prüfen.
- Abhängigkeiten: E4-BUG-01 bis E4-BUG-05, E4-MF-02/03, E7-BUG-02 und
  E7-MF-01; bestehende Ursachen werden nicht dupliziert.
- Begründung der Disposition: Analyse-Only-Non-Goal; keine Testausführung oder
  Teständerung in diesem Lauf.

### AUD-N01 — Private Prüfhelfer duplizieren Kontroll- und Fehlerpfad

- Schweregrad: P3; Disposition: `accepted-deferred`; attempts: 0
- Kategorie: Optimierung / DRY; Vertrauen: hoch; Größe: S
- Evidenz: Der Abschluss-Audit fand zwei private Prüfhelfer mit nahezu
  identischem Kontroll- und Fehlerbehandlungspfad.
- Aktion: Gemeinsame, eng begrenzte Hilfsabstraktion prüfen; Verhalten und
  Diagnoseprojektion dabei separat regressionssichern.
- Begründung der Disposition: Analyse-Only-Non-Goal; kein Refactoring in
  diesem Lauf.

### AUD-N02 — Stabiler typbestimmender Ausgabemarker ist doppelt definiert

- Schweregrad: P3; Disposition: `accepted-deferred`; attempts: 0
- Kategorie: Optimierung / Magic Value; Vertrauen: mittel-hoch; Größe: S
- Evidenz: Derselbe stabile typbestimmende Ausgabemarker wird an zwei Stellen
  separat verwendet.
- Aktion: Eine benannte Konstante oder gemeinsame Projektion mit gezieltem
  Contract-Test erwägen.
- Begründung der Disposition: Analyse-Only-Non-Goal; keine Wert- oder
  Codeänderung in diesem Lauf.

### AUD-N03 — Interner Kompatibilitätsalias ohne statische Referenzen

- Schweregrad: P3; Disposition: `accepted-deferred`; attempts: 0
- Kategorie: Dead Code / unused surface; Vertrauen: niedrig; Größe: S
- Evidenz: Der Audit fand keine statischen Referenzen auf den internen Alias;
  dynamische Nutzung bleibt als Unsicherheitsvorbehalt möglich.
- Aktion: Dynamische Registrierungs-/Reflexionspfade prüfen und den Alias erst
  danach entfernen oder ausdrücklich als Kompatibilitätsfläche dokumentieren.
- Begründung der Disposition: Analyse-Only-Non-Goal; keine Entfernung ohne
  zusätzliche Laufzeitattestation.

### AUD-N04 — Fallback-Fehlermeldung ist in zwei Startfehlerpfaden dupliziert

- Schweregrad: P3; Disposition: `accepted-deferred`; attempts: 0
- Kategorie: Optimierung / DRY / Magic Value; Vertrauen: hoch; Größe: S
- Evidenz: Identische Fallback-Fehlermeldung wird in zwei Startfehlerpfaden
  separat erzeugt.
- Aktion: Gemeinsame redigierte Fehlerprojektion prüfen und dabei E7-BUG-01s
  sichere Pfad-/Diagnosebehandlung als Leitplanke beibehalten.
- Abgrenzung: Nicht als neue Sicherheitsursache gewertet; E7-BUG-01 bleibt
  der maßgebliche Redaction-Befund.
- Begründung der Disposition: Analyse-Only-Non-Goal; kein Refactoring in
  diesem Lauf.
