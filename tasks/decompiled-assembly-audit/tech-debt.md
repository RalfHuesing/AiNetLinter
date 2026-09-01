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
