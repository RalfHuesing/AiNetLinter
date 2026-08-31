# Tech-Debt-Queue

## Abgeschlossene Befunde

- `ASM-001`, `ASM-002`, `ASM-003` und `MCP-L6-001` — Assembly-Routing ist
  capability-basiert, erwartete Referenz-Nichttreffer bleiben lokal,
  Multi-Pattern-Navigation wird aggregiert und ungültige Positionen werden
  vor Roslyn als recoverable `INVALID_ARGUMENT` behandelt.
- `EXTSRC-01`, `EXTSRC-02`, `EXTSRC-03` und `CHK-001` — Loader und Runtime
  teilen die öffentliche URL-Policy; geschützte Remotes bleiben fail-closed,
  Checkout-Ownership wird auch an Cancellation-Grenzen bereinigt und
  Materialisierungs-/Fallback-Ursachen werden sicher diagnostiziert.
- `F-05-01`, `F-05-02`, `F-05-03` — erfolgreiche Cache-Generationen werden
  retentiv gesweept, Key-Locks reclamieren race-sicher und Resident-Reuse
  berücksichtigt Root-Fingerprint plus Source-Snapshot-Identität. Der
  produktive Orchestrator cached bekannte Snapshot-IDs für Reuse, während
  Resolver-Testdoubles weiterhin echte Wechsel direkt prüfen können.
- `MCP-WIRE-001` — die komplette serialisierte Assembly-StructuredContent-
  Payload wird global auf 4.096 UTF-8-Bytes kompaktifiziert; Summaries werden
  anschließend mit den sichtbaren Arrays synchronisiert.
- `MCP-L6-002` — `GetServerHealthResponseBuilder` wurde in eine schmale
  Response-Fassade und `GetServerHealthProjection` zerlegt. MCP-Messung nach
  dem Refactor: AIContextFootprint 72/2500, keine direkte Violation.
- `UX-001` — `AssemblyAnalysisRegistry` wurde um Source-Project-Leases,
  Eviction/Retirement, Identitätsprobe und Health-Snapshot-Erstellung entlastet.
  Die Registry liegt laut MCP bei 371 Type-LOC und 0 direkten Violations; die
  Datei liegt unter dem `MaxLineCount`-Limit.

## Audit-Vorbehalt

Ein unabhängiger Subagent-/Review-Task konnte in dieser Ausführung nicht
gestartet werden, weil kein entsprechendes Delegationstool verfügbar war. Die
Abschlussbewertung wird deshalb als direkte Selbstprüfung mit reproduzierbaren
MCP-, Build- und Test-Nachweisen geführt; ein separater Reviewer-Blick ist nicht
simuliert.
