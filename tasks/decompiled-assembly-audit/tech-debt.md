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
