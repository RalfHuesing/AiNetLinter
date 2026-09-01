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
