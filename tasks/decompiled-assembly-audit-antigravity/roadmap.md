# Roadmap: 360-Grad-Audit der dekompilierten Assembly-Unterstützung

status: completed
current_epic: 8
last_checkpoint: Alle 8 Epic-Befundberichte erfolgreich erstellt und verifiziert

## Primäraufgabe

Erstelle acht eigenständige, code- und MCP-verifizierte Befundberichte für die dekompilierte Assembly-Unterstützung im AiNetLinter-MCP gemäß freigegebenem Konzept (`Konzept.md`).

## Epics

1. **Epic 01: Öffentliche MCP-Verträge und Discoverability**
   - Status: completed
   - Fokus: Registrierung, Schemas, Annotations, Capability-Matrix, Progressive Disclosure, Doku-Konsistenz.
   - Artefakt: `epic-01-mcp-contracts-and-discoverability.md`

2. **Epic 02: Decompilation und semantischer Snapshot**
   - Status: completed
   - Fokus: Metadata-only-Garantie, dekompilierte Syntax/Bodies, Generics/Attribute/Parameter, stabile Symbol-IDs.
   - Artefakt: `epic-02-decompilation-and-semantic-snapshot.md`

3. **Epic 03: Referenzen, Source Selection und Diagnosen**
   - Status: completed
   - Fokus: Referenzauflösung, Version-Matching, External Source Provider, Herkunft, Trust, `partial` und Diagnosen.
   - Artefakt: `epic-03-references-source-selection-diagnostics.md`

4. **Epic 04: Session-, Cache- und Lebenszeitsemantik**
   - Status: completed
   - Fokus: Fingerprints, Generationen, Cache-Publishing, Refresh bei Dateiänderung, Leases, TTL, Eviction, Concurrency.
   - Artefakt: `epic-04-session-cache-lifetime.md`

5. **Epic 05: Navigation und fachliche Query-Korrektheit**
   - Status: completed
   - Fokus: Assembly-Support der Symbolgraph-Tools, Root-vs-Referenz-Grenzen, Caller-/Callee-Semantik, Sufficiency-Hints.
   - Artefakt: `epic-05-navigation-and-query-correctness.md`

6. **Epic 06: Response-, Token- und Laufzeiteffizienz**
   - Status: completed
   - Fokus: 8-KB-Response-Budget, Trimming-Reihenfolge, Diagnose-Sampling, Structured Content vs Markdown.
   - Artefakt: `epic-06-response-token-runtime-efficiency.md`

7. **Epic 07: Betrieb, Sicherheit und Fehlerverhalten**
   - Status: completed
   - Fokus: Absolute Pfade, Dateitypen, native PE-Dateien (`FALSE-01`), Fail-Closed, Path-Guard, Nichtausführung.
   - Artefakt: `epic-07-operations-security-error-handling.md`

8. **Epic 08: Test- und Dokumentationsnachweis**
   - Status: completed
   - Fokus: Testabdeckung kritischer Verträge, Testlücken, Abweichungen zwischen Dokumentation und Code.
   - Artefakt: `epic-08-test-and-documentation-evidence.md`

## Abschluss-Checkliste

- [x] Alle 8 Epic-Berichte als eigenständige Markdown-Dateien im Task-Verzeichnis erstellt.
- [x] Jedes Finding nach Schema strukturiert (ID, Kategorie Bug/Optimierung/Missing Feature, P0–P3, S/M/L/XL, Vertrauen, Evidenz, Empfehlung).
- [x] Strikte Copyright- und Redaktionsregel eingehalten (keine externen Identitäten; nur Labels `GIT-01`, `LOCAL-01`, `LOCAL-02`, `LOCAL-03`, `FALSE-01`).
- [x] Keine Änderungen am Produktionscode, Testcode oder an Konfigurationen vorgenommen.
- [x] Tech-Debt-Register (`tech-debt.md`), Execution-Log (`execution-log.md`) und Code-Map (`code-map.md`) gepflegt.
