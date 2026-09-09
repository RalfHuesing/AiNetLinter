# Legacy-Entfernungsmatrix: Task 03

Diese Datei ist eine read-only Prüfgrundlage für den Hard Cut. Sie ist keine
öffentliche Produktdokumentation. Historische Task- und Auditdateien bleiben
ausdrücklich außerhalb des Aktivscans.

| Legacy-/Parallelform | Aktive Prüfpfade | Zulässige Evidenz | Release-Aktion |
| --- | --- | --- | --- |
| `targetType`, `projectRoot`, `configPath`, `solutionPath` als Toolinput | `src/`, produktive MCP-Tests/Fixtures, `Docs/`, `README.md`, `.agents/rules/`, MCP-Registrierungen/Instructions | Task 01/02 Konzepte und Rahmen | weder lesen noch erzeugen; unbekannte Properties als `invalid_argument` ablehnen |
| `gitRef` als Symbol-Impact-Ersatz | Workflow-/Impact-Registrierung, Schema- und Raw-Wire-Tests | `get_impact`-Change-Context-Katalog | nur im ausdrücklich kanonischen Git-Change-Modus; kein stiller Symbol-Fallback |
| `query`, `searchPattern`, `fileFilter`, `includePattern` als Suchalias | `search_pattern`-Schema, Formatter, Docs, Tests | Task-02-Hard-Cut | entfernen; keine Aliasauflösung |
| `isTruncated`/`truncated` ohne benannten Abschnitt | alle Composite-/Workflowpayloads und Textprojektionen | gemeinsamer Navigationsteil | durch abschnittsbezogene `completeness`, Counts, `truncatedBy` und `next` ergänzen |
| Test-„Coverage“ oder „abgedeckt“ als Laufzeitbehauptung | `get_test_context`, `get_feature_context`, Formatter, Docs | statische Testheuristik | als statische Kandidaten/Zuordnung kennzeichnen; keine Runtime-Coverage behaupten |
| Score als vollständiges Quality-/Lint-Urteil | `safeguard`, Docs, Wire-Tests | Lint-/Scope-Semantik | Scope, Violations und Konfiguration separat ausweisen; fehlende Regeln nie grün |
| Dead-Code-/Duplicate-Treffer als Löschauftrag | Precision-Scanner, Formatter, Tests, Docs | Evidenzgrenze Reflection/DI/Generatoren | nur Kandidat mit Confidence, Evidenzgrenze und Gegenprüfung |
| `tasks/**`-Historie als aktiver Produktvertrag | Aktivscan | abgeschlossene Konzepte/Auditunterlagen | nicht ändern und nicht als Produktbeleg werten |

## Aktivscan-Regeln

Der Release-Scan deckt die aktiven Prüfpfade ab und schließt `tasks/**` aus.
Jeder Treffer wird gegen diese Matrix klassifiziert; ein nicht erklärbarer
Treffer blockiert den Release. Der Scan ist textuell und ersetzt keine
StructuredContent-/Schema-Prüfung.
