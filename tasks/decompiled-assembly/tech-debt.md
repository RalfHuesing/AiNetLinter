# Tech-Debt-Register

Primäraufgabe: Behebe und konsolidiere die dekompilierte Assembly-Analyse gemäß dem freigegebenen Konzept.

## Aktive Korrekturen

Die folgenden P1-Befunde sind nach dem Review für eine frische Korrekturrunde
aktiviert. Der Versuchszähler gilt je technische Ursachensignatur.

### TD-ASM-ACCESSOR-001 – Accessor-Matching verliert das zugehörige Member

- Schweregrad: P1
- Status: behoben in Korrekturversuch 1/5.
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
- Disposition: `accepted-deferred`
- Attempts: 0
- Nächster Schritt: Im vorgesehenen Paket 3 die betroffenen transitive Footprints gezielt untersuchen und auf `AIContextFootprint <= 2500` bringen.
- Log-Anker: `execution-log.md`, 2026-09-02 Paket 1 Implementierer.

### DOC-GET-IMPACT-INCLUDE-REFERENCES-SCOPE – Gemeinsamer Dokuabschnitt vermischt Toolverträge

- Schweregrad: P1
- Status: aktiv, Versuch 1/5 läuft.
- Scope/Fundstelle: `Docs/agent-api.md`, Beschreibung von `find_references` und `get_impact`.
- Evidenz: Review des Paket-2-Diffs; `get_impact` besitzt weder `includeReferences` noch eine `navigation`-Struktur, obwohl der gemeinsame Abschnitt dies nahelegt.
- Disposition: `fix-now`
- Nächster Schritt: Dokumentation in getrennte, gegen die aktuellen Registrierungen verifizierte Toolverträge aufteilen.
- Log-Anker: `execution-log.md`, 2026-09-02 Paket 2 Review.

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
