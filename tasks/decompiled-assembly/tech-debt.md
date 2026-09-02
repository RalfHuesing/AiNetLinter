# Tech-Debt-Register

Primäraufgabe: Behebe und konsolidiere die dekompilierte Assembly-Analyse gemäß dem freigegebenen Konzept.

## Aktive Korrekturen

Die folgenden P1-Befunde sind nach dem Review für eine frische Korrekturrunde
aktiviert. Der Versuchszähler gilt je technische Ursachensignatur.

### TD-ASM-ACCESSOR-001 – Accessor-Matching verliert das zugehörige Member

- Schweregrad: P1
- Status: behoben in Korrekturversuch 2/5.
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/AssemblyDecompiledBodyResolver.cs`, Accessor-Matching.
- Evidenz: Review des Diffs `396e77f1..b3bb2ea8`; ein Descendant-Scan vergleicht nur Accessor-Kinds und kann bei mehreren gleichartigen Properties/Events den falschen Body wählen.
- Disposition: `fixed`
- Nächster Schritt: Accessor über `AssociatedSymbol` auf den direkten Property-/Indexer-/Event-Member begrenzen und Regressionen ergänzen.
- Log-Anker: `execution-log.md`, 2026-09-02 Paket 1 Review, P1-Finding A.

### TD-ASM-CACHE-001 – In-Flight-Generation wird vor Publish-Return löschbar

- Schweregrad: P1
- Status: behoben in Korrekturversuch 1/5.
- Scope/Fundstelle: `AssemblyDecompilationCache.Publish` und `AssemblyCacheCleanup`.
- Evidenz: Review-Gegenbeispiel mit konkurrierenden Publishern unterschiedlicher Fingerprints; eine erfolgreich gemeldete Generation kann vor dem Return durch Retention gelöscht werden.
- Disposition: `fixed`
- Nächster Schritt: Publisher/Retention pro Cache-Key synchronisieren oder In-Flight-Generationen schützen und mit einem verzögerten Multi-Fingerprint-Test absichern.
- Log-Anker: `execution-log.md`, 2026-09-02 Paket 1 Review, P1-Finding B.

### TD-ASM-FRAMEWORK-001 – Exakte Assembly `System` nicht vereinheitlicht

- Schweregrad: P1
- Status: behoben in Korrekturversuch 1/5.
- Scope/Fundstelle: `AssemblyReferenceResolver` Framework-Namens-/Versionsprüfung.
- Evidenz: `StartsWith("System.")` erfasst nicht den exakten Namen `System`, obwohl die freigegebene Fachentscheidung die Familie `System` nennt.
- Disposition: `fixed`
- Nächster Schritt: exaktes `System` ergänzen, `Systemish` ausschließen und Regressionstest hinzufügen.
- Log-Anker: `execution-log.md`, 2026-09-02 Paket 1 Review, P1-Finding C.

### TD-ASM-HEALTH-TEST-001 – Direkter Registrierungstest der Health-Route fehlt

- Schweregrad: P2
- Status: zurückgestellt bis zur passenden Test-Matrix.
- Scope/Fundstelle: private Route in `ServerMaintenanceToolRegistrations.cs`.
- Evidenz: Der vorhandene Test prüft den Tool-Level-Aufruf, nicht die Closure der Registrierung.
- Disposition: `accepted-deferred`
- Nächster Schritt: In Paket 4 einen direkten Registrierungstest ergänzen, sofern die Route weiterhin privat verdrahtet ist.
- Log-Anker: `execution-log.md`, 2026-09-02 Paket 1 Review, P2-Empfehlung 1.

### TD-ASM-EVIDENCE-001 – Abweichende get-impact-Nachweiszahl

- Schweregrad: P2
- Status: zurückgestellt; kein Codefehler belegt.
- Scope/Fundstelle: Implementiererbericht versus aktueller MCP-Impact-Nachweis.
- Evidenz: Implementierer meldete 37 geänderte Symbole; Reviewer-Abfrage mit `gitRef=396e77f1` meldete 48.
- Disposition: `accepted-deferred`
- Nächster Schritt: Abschlussnachweise mit eindeutigem Git-Ref und aktuellem Scope reproduzieren.
- Log-Anker: `execution-log.md`, 2026-09-02 Paket 1 Review, P2-Empfehlung 2.

## TD-QL-001 – AIContextFootprint-Warnungen in Assembly-Modulen

- Schweregrad: P2
- Ursache/Scope: Sechs bestehende `AIContextFootprint`-Warnungen in den Assembly-Coordinators/Navigators unter `src/AiNetLinter/Mcp`.
- Evidenz: Paket-1-Implementierer meldet `get_violations` mit 0 Fehlern und 6 Warnungen sowie `safeguard` 1,0/10; die Warnungen lagen bereits außerhalb des Paket-1-Änderungsbereichs.
- Disposition: `fix-now`
- Attempts: 1
- Nächster Schritt: Im vorgesehenen Paket 3 die betroffenen transitive Footprints gezielt untersuchen und auf `AIContextFootprint <= 2500` bringen.
- Log-Anker: `execution-log.md`, 2026-09-02 Paket 1 Implementierer.

### DOC-GET-IMPACT-INCLUDE-REFERENCES-SCOPE – Gemeinsamer Dokuabschnitt vermischt Toolverträge

- Schweregrad: P1
- Status: behoben in Korrekturversuch 2/5.
- Scope/Fundstelle: `Docs/agent-api.md`, Beschreibung von `find_references` und `get_impact`.
- Evidenz: Review des Paket-2-Diffs; `get_impact` besitzt weder `includeReferences` noch eine `navigation`-Struktur, obwohl der gemeinsame Abschnitt dies nahelegt.
- Disposition: `fixed`
- Nächster Schritt: Erledigt; `Docs/agent-api.md` und `Docs/integration.md` sind getrennt und durch Doku-Smoke-Regressionen abgesichert.
- Log-Anker: `execution-log.md`, 2026-09-02 Paket 2 Review.

### DOC-GET-IMPACT-NAVIGATION-ASSERTION-001 – Explizite Negativassertion im Doku-Smoke-Test fehlt

- Schweregrad: P2
- Status: zurückgestellt.
- Scope/Fundstelle: `McpDocumentationSmokeTests` und `Docs/agent-api.md`.
- Evidenz: Review bestätigt korrekten Dokuinhalt, aber die fehlende `navigation`-Negativassertion wird nur indirekt durch Abschnitts-/Payload-Prüfungen abgesichert.
- Disposition: `accepted-deferred`
- Nächster Schritt: Bei einer späteren Doku-Vertragsrunde eine explizite Assertion ergänzen, dass `get_impact` keine `navigation`-Struktur dokumentiert.
- Log-Anker: `execution-log.md`, 2026-09-02 Paket 2 Folge-Review.

### NAV-COMPLETENESS-SESSION-CAP-OVERWRITE – Completeness kann Trunkierung überdecken

- Schweregrad: P2
- Status: außerhalb des aktuellen Korrekturscopes als Projektbefund vorgemerkt.
- Scope/Fundstelle: `AssemblyReferenceNavigator`, Session-Cap und `assembliesTruncated`.
- Evidenz: Review-Hinweis: Bei mehr als 32 Assembly-Sessions kann `completeness` auf `complete` gesetzt werden, obwohl `assembliesTruncated=true` bleibt.
- Disposition: `promoted-to-project-debt`
- Nächster Schritt: In einem separaten Projekt-Backlog/-Task die Vollständigkeitsprojektion und Diagnosesemantik korrigieren.
- Log-Anker: `execution-log.md`, 2026-09-02 Paket 2 Review.

### SIGONLY-PROJECTION-GAP – Signature-only-Basis bei Calltree/Metrics nicht explizit

- Schweregrad: P2
- Status: zurückgestellt.
- Scope/Fundstelle: `get_call_tree`/`metrics_tree` bei dekompilierten Signature-only-Snapshots.
- Evidenz: Review bestätigt die korrigierte `find_references`-Einschränkung; für Calltree/Metrics bleibt eine explizite Sichtbarkeit der Signatur-only-Basis offen.
- Disposition: `accepted-deferred`
- Nächster Schritt: Bei der vorgesehenen Efficiency-/Quality-Arbeit oder einem späteren Vertragstask bewerten.
- Log-Anker: `execution-log.md`, 2026-09-02 Paket 2 Review.

### TD-SESSION-EVICTION-REFRESH-001 – Request-Set verliert alte Entry-Identitäten nicht

- Schweregrad: P1
- Status: aktiv, Versuch 1/5 läuft.
- Scope/Fundstelle: `AssemblyAnalysisRegistryReferenceEviction` und direkter Refresh-Retirement in `AssemblyAnalysisRegistry`.
- Evidenz: Review des Paket-3-Diffs; ein offener Request mit Fremd-Lease wird beim Fingerprint-Refresh nicht per `OnRetired/ClearRequest` entfernt und hält den alten Entry samt Server/Solution referenziert.
- Disposition: `fix-now`
- Nächster Schritt: Alle Retirement-Pfade per Entry-Identität bereinigen und wiederholte Generationwechsel testen.
- Log-Anker: `execution-log.md`, 2026-09-02 Paket 3 Review.

### TD-SIGONLY-COVERAGE-001 – Signature-only-Ausschlüsse und Operatoren nicht direkt abgesichert

- Schweregrad: P2
- Status: zurückgestellt bis Paket 4.
- Scope/Fundstelle: `AssemblyDecompilationSourceText` und Signature-only-Regressionen.
- Evidenz: Review: Abstract/Extern/Partial/Interface, echte Bodies sowie Operator-/Conversion-Operator-Syntax sind nicht jeweils direkt getestet.
- Disposition: `accepted-deferred`
- Nächster Schritt: Paket-4-Testmatrix um diese Ausschlüsse und Operatoren erweitern.
- Log-Anker: `execution-log.md`, 2026-09-02 Paket 3 Review.

### TD-SESSION-LEASE-RACE-001 – Verspätete Registrierung nach asynchroner Expansion möglich

- Schweregrad: P2
- Status: zurückgestellt.
- Scope/Fundstelle: `OpenReferenceExpansionNodeAsync` und `RegisterReferenceLease`.
- Evidenz: Zwischen beiden Operationen liegt ein `await`; eine parallele Freigabe kann die Liste vor der Registrierung leeren.
- Disposition: `accepted-deferred`
- Nächster Schritt: Defensive atomare Prüfung gegen `disposed` im Lifecycle-/Paket-4-Test bewerten.
- Log-Anker: `execution-log.md`, 2026-09-02 Paket 3 Review.

### TD-RESPONSE-SUMMARY-001 – Namespace-Summary kann beim Gesamttrimming verschwinden

- Schweregrad: P2
- Status: zurückgestellt.
- Scope/Fundstelle: `InspectAssemblyFormatter.TryRemoveLastNamespace` und Gesamtpayload-Budget.
- Evidenz: Review: Bei weiterhin übergroßem Payload kann der Summary-Eintrag selbst entfernt werden.
- Disposition: `accepted-deferred`
- Nächster Schritt: Grenztest für Summary-Erhalt bei maximalem Response-Trimming ergänzen.
- Log-Anker: `execution-log.md`, 2026-09-02 Paket 3 Review.

### TD-REGISTRY-DEAD-WIRING-001 – Ungelesene Retirement-Callbacks

- Schweregrad: P3
- Status: zurückgestellt bis nach Paket 4.
- Scope/Fundstelle: `AssemblyAnalysisRegistryCoordinatorContext.BeforeRetirementAsync` und `RetireEntryAsync`.
- Evidenz: Review: Delegates werden gesetzt, vom aktuellen Koordinator jedoch nicht mehr gelesen.
- Disposition: `accepted-deferred`
- Nächster Schritt: Nach Abschluss der Lifecycle-Änderungen Referenzen erneut prüfen und sichere Bereinigung durchführen.
- Log-Anker: `execution-log.md`, 2026-09-02 Paket 3 Review.
