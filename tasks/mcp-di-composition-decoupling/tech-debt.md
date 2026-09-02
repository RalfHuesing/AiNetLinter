# Tech Debt: mcp-di-composition-decoupling

Status: drei begründete `accepted-deferred`-Befunde.

## TD-001 — Stabiler Wire-/Format-Marker

- Schweregrad: P3
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs:35`
- Evidenz: MCP `find_magic_values` meldet `PrimaryCtor-Param` als
  wiederkehrenden String. Der Marker ist Teil des stabilen Output-/Format-
  Vertrags; eine zusätzliche Indirektion würde das Verhalten nicht verbessern.
- Disposition: `accepted-deferred`
- Nächster Schritt: Nur bei einer zukünftigen Formatvertragsänderung erneut
  prüfen.
- Log-Anker: Run 2026-09-02-06 / Epic 3 Implementierer

## TD-002 — Vollständige Buffer-Whitelist

- Schweregrad: P3
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesStringHeuristics.cs:52,59-62`
- Evidenz: MCP `find_magic_values` meldet die Literalwerte als Teil der
  absichtlichen vollständigen Whitelist. Eine Auslagerung würde nur die
  Literal-Indirektion erhöhen und die Heuristik nicht verständlicher machen.
- Disposition: `accepted-deferred`
- Nächster Schritt: Bei einer Änderung der Scanner-Heuristik erneut bewerten.
- Log-Anker: Run 2026-09-02-06 / Epic 3 Implementierer

## TD-003 — Heuristische Einzelkandidaten

- Schweregrad: P3
- Scope/Fundstelle: bestehende Diagnosecodes, Formatter-Texte und
  Erkennungspräfixe unter `src/AiNetLinter/Mcp`
- Evidenz: Der vollständige MCP-Scan meldet verbleibende Einzelkandidaten;
  für diese ist keine gemeinsame Konstante mit sicherem
  Verhaltenserhalt belegt. `find_magic_values` war nach Erweiterung auf
  `maxResults=300` vollständig.
- Disposition: `accepted-deferred`
- Nächster Schritt: Nur bei einer konkreten Mehrfachverwendung mit
  eindeutiger gemeinsamer Semantik erneut aktivieren.
- Log-Anker: Run 2026-09-02-06 / Epic 3 Implementierer

## TD-004 — Unreferenzierter DaemonStartupGate-Wrapper

- Schweregrad: P3
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Daemon/DaemonStartupGate.cs:9`,
  `AcquireAsync(CancellationToken, TimeSpan)`
- Evidenz: MCP `find_dead_code` meldet keine Managed-Referenzen; die Methode
  delegiert an die Drei-Parameter-Überladung. Ein interner
  Kompatibilitäts-/Reflection-Vertrag ist nicht ausgeschlossen.
- Disposition: `accepted-deferred`
- Nächster Schritt: Bei einer dedizierten Daemon-API-/Reflection-Prüfung
  `InternalsVisibleTo`, Serializer- und Routing-Verträge verifizieren; erst
  danach Entfernung oder Beibehaltung entscheiden.
- Log-Anker: Run 2026-09-02-08 / Abschluss-Audit

Neue Befunde werden mit Schweregrad, Scope, Evidenz, Disposition, nächstem
Schritt und Verweis auf den jeweiligen Execution-Log-Eintrag angehängt.
