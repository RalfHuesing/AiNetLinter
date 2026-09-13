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
- **Gefundene Befunde:** 0 Critical, 1 Major, 2 Minor

CHAIN-01 (gleiche kanonische `handoffId` weiterreichen), CHAIN-02, CHAIN-03 und CHAIN-04 sind durchgängig möglich.

---

## 2. Negative Befunde

### [Major] C-06: `Assembly-Scope` meldet sichtbare Diagnosen falsch (Live-Review)

- **Betroffenes Tool / Schema**: `find_symbol` mit `includeReferences=true`
- **Ziel-Label**: `LOCAL-01` (Modus: Assembly)
- **Konkreter Aufruf**: Suche nach einem bekannten Typ mit `includeReferences=true`
- **Beobachtung / Ist-Verhalten**:
  - Der sichtbare Scope meldet `diagnostics=100/100; diagnosticsTruncated=false`.
  - Im Antworttext stehen zugleich nur „Diagnosen (5 von 100 gezeigt)“. `find_references` meldet für dieselbe Projektion dagegen korrekt `diagnostics=5/100; diagnosticsTruncated=true`.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Scope-Felder müssen die tatsächlich sichtbare Diagnoseprojektion beschreiben. Andernfalls wertet ein Agent 100 Diagnosen als vollständig geliefert und übersieht 95 Einträge.
- **Empfehlung**:
  - In `find_symbol` die sichtbaren Werte wie bei `find_references` ausgeben: `diagnostics=5/100; diagnosticsTruncated=true`. Falls zusätzlich die Gesamtmenge gebraucht wird, sie klar getrennt als verfügbare, nicht als sichtbare Diagnosen benennen.

### [Minor] C-08: Reference-Closure-Scope verdrängt das Suchergebnis (Live-Review)

- **Betroffene Tools / Schema**: `find_symbol`, `find_references` mit `includeReferences=true`
- **Ziel-Label**: `LOCAL-01` (Modus: Assembly)
- **Konkreter Aufruf**: Suche nach einem bekannten Typ mit `includeReferences=true`
- **Beobachtung / Ist-Verhalten**:
  - Bei einem einzigen eigentlichen Treffer listet `Assembly-Scope` alle 16 Assembly-Identitäten vollständig auf, überwiegend Framework-Assemblies; dazu kommen lange Diagnosesamples.
  - Die tatsächliche Such- bzw. Referenzevidenz wird dadurch im Antwortbudget optisch und token-seitig nachrangig.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Der Agent braucht die Ziel-Assembly vollständig und ausreichend Scope-Transparenz, aber nicht jede Framework-Identität als Volltext, wenn sie für die nächste Navigation keine direkte Aktion ermöglicht.
- **Empfehlung**:
  - Ziel- und relevante Nicht-Framework-Assemblies vollständig ausgeben, Referenz-Identitäten begrenzen oder gruppieren und `assemblyIdentitiesShown/Total` plus `identitiesTruncated` ausweisen. So bleibt der Scope prüfbar, ohne die Arbeitsinformation zu verdrängen.

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

Gruppe C: 0 Critical, 1 Major, 2 Minor
IDs: C-06, C-07, C-08
Blocker: none
