# Ausführungsroadmap

- Primäraufgabe: Behebe die relevanten Audit-Findings der Analyse dekompilierter Assemblies.
- Task: `tasks/decompiled-assembly-analysis-audit-fix`
- Betriebsmodus: Großkonzept, Konzept `status: ready`
- Status: `completed`
- Current epic: `E4` (abgeschlossen)
- Letzter Checkpoint: Abschlussverifikation und MCP-Audit am 2026-08-31
- Tech-Debt-Queue: siehe `tech-debt.md`

## Epics

### E1 — Navigation und Eingabe-/Diagnosesemantik

- Ziel: Root-only-Verhalten, explizite Referenzfähigkeiten, Positionsvalidierung und deterministische Batch-/Session-Aggregation korrigieren.
- Abhängigkeiten: keine.
- Betroffene Bereiche: `AnalysisToolCall`, Assembly-Navigationstools, Positionsauflösung, zugehörige Fast-/IntegrationTests, `Docs/agent-api.md`.
- Muss-/Akzeptanzkriterien: `includeReferences=false` erzeugt keine Referenz-Sessions; ungültige Positionen liefern recoverable `INVALID_ARGUMENT`; erwartete Nicht-Treffer verschlechtern nicht global `partial`; Batch-Diagnostics und Trunkierung bleiben erhalten.
- Verifikation: gezielte Tests und MCP-`get_violations` nach der letzten Codeänderung.
- Status: `completed`

### E2 — External-Source-Vertrag und Ownership

- Ziel: URL-Policy, fail-closed Auth-/Credential-Semantik, Checkout-Cancellation-Ownership und source-backed Materialisierung/Fallback konsistent und diagnostisch belastbar machen.
- Abhängigkeiten: E1 nur für gemeinsame Tooldiagnosen; fachlich sonst unabhängig.
- Betroffene Bereiche: `ExternalSourceMappingValidator`, External-Source-Repository, Materialisierung, Resolver, Fixtures und IntegrationTests, `Docs/configuration.md`/`Docs/agent-api.md`.
- Muss-/Akzeptanzkriterien: Loader und Runtime teilen exakt denselben URL-Vertrag; öffentliche Remotes werden unterstützt, geschützte früh recoverable abgewiesen; jeder Checkout-Handle wird exakt einmal freigegeben; restaurierte source-backed Fixtures sind end-to-end unterscheidbar vom sicheren Decompiled-Fallback.
- Verifikation: gezielte Fast-/IntegrationTests, redigierte Diagnoseprüfungen und MCP-Checks.
- Status: `completed`

### E3 — Cache-Generationen, Freshness und Lock-Reclamation

- Ziel: Sichere Retention/Sweeps, race-sichere Key-Lock-Reclamation und vollständige Analyseidentität für Resident-Reuse umsetzen.
- Abhängigkeiten: E2 für Source-/Snapshot-Identität.
- Betroffene Bereiche: Assembly-Analysis-Registry, External-Source-Cache/Refresh, Fingerprints, Ressourcenregister und Lebensdauertests.
- Muss-/Akzeptanzkriterien: aktuelle, geleaste und geschützte Generationen bleiben erhalten; alte Generationen werden begrenzt; unbenutzte Lock-Keys werden sicher freigegeben; Source-/Dependency-Änderungen invalidieren Reuse deterministisch.
- Verifikation: Komponententests, wiederholte Refresh-/Parallelitäts-IntegrationTests und gezielte MCP-Prüfungen.
- Status: `completed`

### E4 — Wire-Budget, Health-Projektion und Abschlussqualität

- Ziel: globale StructuredContent-Grenze messbar einhalten, Health-Projektion scope-nah strukturieren, Dokumentation synchronisieren und den Abschluss-Audit durchführen.
- Abhängigkeiten: E1–E3.
- Betroffene Bereiche: Assembly-StructuredContent/Diagnoseprojektion, `GetServerHealthResponseBuilder`, Health-/Wire-Tests, relevante Dokumentation.
- Muss-/Akzeptanzkriterien: komplette serialisierte Assembly-StructuredContent-Payload bleibt innerhalb 4 KiB; Herkunft, Status, Completeness und Trunkierung bleiben konsistent; Health-Wire-Vertrag bleibt unverändert und Strukturmetriken sind regelkonform.
- Verifikation: JSON-Fixture-Serialisierung, Health-Tests, `get_violations`, Abschluss-Audit sowie vollständige Nicht-Stress-Gates.
- Status: `completed`

## Abschluss-Checkliste

- [x] `dotnet build`
- [x] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
- [x] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
- [x] gezielte MCP-Nachprüfung der betroffenen Symbole, Metriken und Violations
- [x] source-backed Fixture-/Integration-Nachweis; öffentlicher Live-Safeguard wurde im Wiederholungslauf grün bestätigt
- [x] vollständiger Audit auf DRY, Dead Code, Refactoring-Drift und Magic Values
