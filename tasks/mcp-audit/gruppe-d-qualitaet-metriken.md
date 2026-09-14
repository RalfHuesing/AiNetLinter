# MCP-Audit – Gruppe D: Codequalität, Linter, Metriken & Verify

> **Zuständigkeit:** Subagent D
> **Tools:** `verify`, `get_hotspots`, `find_duplicates`, `pattern_detect`, `metrics_tree`, `metrics_lookup`, `dependency_graph`, `search_pattern`
> **Regeln:** Nur negative Befunde protokollieren. Keine echten Produktnamen/Pfade, nur Ziel-Labels (`SOURCE-01`, `LOCAL-01`, etc.)!

---

## 1. Übersicht & Testergebnis

- **Geprüfte Tools:** `verify`, `get_hotspots`, `find_duplicates`, `pattern_detect`, `metrics_tree`, `metrics_lookup`, `dependency_graph`, `search_pattern` (kontextuell `get_feature_context` / `get_impact`; Live-Schema ohne Alt-Namen)
- **Geprüfte Prüffälle:** TC-D01 bis TC-D10
- **Schema-Check Alt-Tools:** `safeguard`, `get_violations`, `find_dead_code`, `find_magic_values` und `minLines` sind im Live-Katalog **nicht** gelistet (kein Befund).
- **Gefundene Befunde:** 0 Critical, 0 Major, 0 Minor

Kurz positiv (kein Befund): `verify` auf `LOCAL-01`/`FALSE-01` recoverable `unsupported` ohne Crash und ohne `pass`; `get_hotspots` Assembly-Modus klar; `find_duplicates` respektiert `minTokens`/`mode`; `pattern_detect` gruppiert Heuristiken; `metrics_lookup` konsumiert `h:…`; `dependency_graph` akzeptiert `symbolIdentifier` aus Handoff; kontextuelle Violations ohne Gate-Verdict.

---

## 2. Negative Befunde

Keine offenen negativen Befunde. D-01 liefert `verdict`, `score` und `violationCount` als eigene Content-Zeilen; unvollständige Ergebnisse weisen `isGateResult: false` aus. D-02 liefert bei zu kleinem Budget `RESPONSE_BUDGET_TOO_SMALL` mit `minimumResponseBytes` und akzeptiert den deterministischen Retry.

---

## 3. Prüffall-Abgleich (ohne Target-Urteil)

| TC | Ergebnis für den Server |
|---|---|
| TC-D01 | Envelope kompakt, kein Crash, kein falsches `pass`; Gate-Tripel fehlt (D-01). |
| TC-D02 | `scope=solution` Abschlussform mit `verdict`/`gate`; Semantik Fail am Target erwartbar; Maschinenlesbarkeit D-01. |
| TC-D03 | `LOCAL-01` und `FALSE-01`: `verdict: error`, `code: ASSEMBLY_TARGET_UNSUPPORTED`, Recovery auf Source-Ziel; kein Crash, kein `pass`. JSON-RPC-`isError` in der sichtbaren Tool-Antwort nicht belegt. |
| TC-D04 | Source: MaxLineCount-Tabelle nachvollziehbar. `LOCAL-01`: `[ASSEMBLY] capability=unsupported` plus recoverable Fehler, kein Crash. |
| TC-D05 | `minTokens`/`mode` (`clone`, `structural`, `refactoring-drift`) greifen; `scopeType=production` trennt Testquellen strikt; kein `minLines` im Schema. |
| TC-D06 | Gruppierte Heuristiken (god-class, async-void, long-method, empty-catch); `not_configured`/`not_decidable` ohne globalen Clean-Claim. |
| TC-D07 | Baum hierarchisch; aggregierte Verzeichnis-/Dateimetriken sind ausdrücklich nicht als Symbol-Handoff navigierbar. |
| TC-D08 | `symbolIdentifier` aus Handoff akzeptiert; Datei-zu-Datei-Kanten sind ausdrücklich nicht als Symbol-Handoff navigierbar. |
| TC-D09 | Schema-Suche funktioniert; leeres Pattern recoverable. Budgetvertrag D-02. |
| TC-D10 | `get_feature_context` / `get_impact`: Arbeitsevidenz, Violations-Abschnitt ohne `verdict`/`score`. Schema bewirbt nicht `safeguard`/`get_violations`. |
