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
- **Gefundene Befunde:** 0 Critical, 1 Major, 1 Minor

CHAIN-01 (gleiche kanonische `handoffId` weiterreichen), CHAIN-02, CHAIN-03 und CHAIN-04 sind durchgängig möglich.

---

## 2. Negative Befunde

### [Major] C-06: Assembly-Modus: Status/Lease unklar, Completeness widersprüchlich (TC-C02, TC-C07)

- **Betroffenes Tool / Schema**: `find_symbol`, `find_references`, `get_symbol_body` (Assembly-Header)
- **Ziel-Label**: `LOCAL-01` (Modus: Assembly)
- **Konkreter Aufruf**: `find_symbol(targetPath=LOCAL-01, includeReferences=false|true)`; `find_references(targetPath=LOCAL-01, includeReferences=false|true)`; `get_symbol_body` auf `LOCAL-01`
- **Beobachtung / Ist-Verhalten**:
  - Fast alle Assembly-Antworten: `status=partial; completeness=partial; confidence=medium`. `get_symbol_body` ergänzt gleichzeitig Footer `Assembly-Suche: 1/1; Vollständigkeit: complete` — widersprüchlich.
  - `includeReferences=true` bei `find_symbol`: Prosatext „Assemblies: 14 von 14“, **keine** `navigation` mit effektiven Assembly-Identitäten; Treffermenge gegenüber `false` unverändert; Diagnosen (Decompiler/CS*) statt Scope-Objekt.
  - `find_references` ohne `includeReferences`: „Keine Aufrufstellen“ ohne Lease-/Snapshot-Erklärung. Mit `includeReferences=true`: Treffer, aber Handoff zeigt das **angefragte** Symbol, Call-Sites nur als Text; dazu Diagnose-Samples statt klarer Scope-/Lease-Aussage.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Agent kann nicht unterscheiden: Snapshot unvollständig vs. wirklich keine Treffer vs. Referenz-Cap. TC-C02/C07 verlangen Scope in `navigation` und klare Lease-Aussage.
- **Empfehlung**:
  - Ein maschinenlesbares Scope-Objekt (Assemblies, Lease, `includeReferences`-Wirkung). Header-`completeness` an den Fachstatus angleichen. Decompiler-Rauschen nicht als Ersatz für Scope.

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

Gruppe C: 0 Critical, 1 Major, 1 Minor
IDs: C-06, C-07
Blocker: none
