# MCP-Audit – Gruppe C: Semantische Symbol-Tools & Chaining-Ketten

> **Zuständigkeit:** Subagent C
> **Tools:** `find_symbol`, `get_symbol_body`, `get_file_skeleton`, `get_class_structure`, `find_references`, `get_call_tree`, `get_impact`, `get_type_hierarchy`, `find_implementations`, `resolve_type_origin`, `get_feature_context`, `get_test_context`
> **Regeln:** Nur negative Befunde protokollieren. Keine echten Produktnamen/Pfade, nur Ziel-Labels (`SOURCE-01`, `LOCAL-01`, etc.)!

---

## 1. Übersicht & Testergebnis

- **Geprüfte Tools:** 12 semantische Symbol- und Chaining-Tools (TC-C01 bis TC-C13) plus `inspect_assembly` / `search_assembly` für CHAIN-04
- **Geprüfte Chaining-Sequenzen:**
  - CHAIN-01: Symbol Deep-Dive (`find_symbol` -> `get_symbol_body` -> `find_references` -> `get_impact`; zusätzlich `get_feature_context` mit derselben ID)
  - CHAIN-02: Klassen-Exploration zu Member-Body (`find_symbol` -> `get_class_structure` -> `get_symbol_body`)
  - CHAIN-03: Typ-Hierarchie zu Implementierungen (`get_type_hierarchy` -> `find_implementations` -> `resolve_type_origin`)
  - CHAIN-04: Assembly-Exploration (`inspect_assembly` -> `search_assembly` -> `get_symbol_body`)
- **Gefundene Befunde:** 0 Critical, 0 Major, 1 Minor

CHAIN-01 (gleiche kanonische `handoffId` weiterreichen), CHAIN-02, CHAIN-03 und CHAIN-04 sind durchgängig möglich.

---

## 2. Negative Befunde

### [Minor] C-07: Call-Sites und Tests ohne Folgetool-Handoffs (TC-C07, TC-C13)

- **Betroffenes Tool / Schema**: `find_references`, `get_feature_context`, `get_test_context`
- **Ziel-Label**: `SOURCE-01` (Modus: Source)
- **Konkreter Aufruf**: `find_references` / `get_feature_context` / `get_test_context` mit derselben `SiteComponentHandler`-`handoffId` wie CHAIN-01
- **Beobachtung / Ist-Verhalten**:
  - `find_references`: jede Zeile trägt die **Query**-`handoffId`, nicht die des Callers. Navigation zur Aufrufstelle nur über Freitext `Datei:Zeile`.
  - `get_feature_context` / `get_test_context`: Caller- und Testdateien ohne `handoffId`. Typ-`get_test_context`: „29 Testkandidaten“, aber „0 von 29 Evidenztreffer zurückgegeben“ (nur Dateien).
- **Soll-Verhalten / Problem aus Agentensicht**:
  - CHAIN-01 zum **selben** Symbol bleibt möglich (IDs sind identisch). Vertiefen in einen konkreten Caller/Test erfordert Parsing. 0/29 Evidenzzeilen trotz gemeldeter Kandidaten ist unnötige Unsicherheit.
- **Empfehlung**:
  - Pro Call-Site und Testmethode eine eigene `handoffId`. Evidenztreffer nicht nur zählen, sondern listen oder `truncatedBy` klar setzen.

---

Gruppe C: 0 Critical, 0 Major, 1 Minor
IDs: C-07
Blocker: none
