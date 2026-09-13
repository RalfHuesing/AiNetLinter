# MCP-Audit – Gruppe C: Semantische Symbol-Tools & Chaining-Ketten

> **Zuständigkeit:** Subagent C
> **Tools:** `find_symbol`, `get_symbol_body`, `get_file_skeleton`, `get_class_structure`, `find_references`, `get_call_tree`, `get_impact`, `get_type_hierarchy`, `find_implementations`, `resolve_type_origin`, `get_feature_context`, `get_test_context`; für CHAIN-04 zusätzlich `inspect_assembly`, `search_assembly`
> **Regeln:** Nur negative Befunde gegen den MCP-Server. Nur Ziel-Labels. Keine Produktnamen, Pfade oder dekompilierten Bezeichner.

---

## 1. Übersicht & Testergebnis

- **Geprüfte Tools:** TC-C01 bis TC-C13 plus FALSE-01 (`find_symbol`) und CHAIN-01 bis CHAIN-04
- **Geprüfte Chaining-Sequenzen:**
  - CHAIN-01 (`SOURCE-01`): `find_symbol` → `get_symbol_body` → `find_references` → `get_impact` (optional `get_feature_context`) — **innerhalb einer Index-Generation durchreichbar**
  - CHAIN-02 (`SOURCE-01`): `find_symbol(kind=class)` → `get_class_structure` → Member-`h:…` → `get_symbol_body` — **ohne String-Bau**
  - CHAIN-03 (`SOURCE-01`): `get_type_hierarchy` → Interface-`h:…` → `find_implementations` → `resolve_type_origin` — **qualifizierte Namen konsistent**
  - CHAIN-03 (`LOCAL-01`): Hierarchie-Basis ohne konsumierbare `h:…` — **Bruch**
  - CHAIN-04 (`LOCAL-01`): `inspect_assembly` → `search_assembly` → `get_symbol_body` — **Bruch** (Direktpfad `inspect_assembly`-`h:…` → `get_symbol_body` funktioniert)
- **FALSE-01:** recoverable `INVALID_ASSEMBLY`, kein Crash
- **Gefundene Befunde:** 0 Critical, 2 Major, 0 Minor

---

## 2. Negative Befunde

---

## 3. Kontrakt-Punkte ohne Befund (kurz)

- TC-C01: Prefix- und FQ-/Kind-Suche auf `SOURCE-01` mit `h:…` und Datei:Zeile.
- TC-C03 / CHAIN-01 / CHAIN-02: `h:…` durchreichbar, solange die Index-Generation stabil bleibt.
- TC-C04: Decompile-Stub auf `LOCAL-01` mit lesbarer Signatur; `Assembly-Scope` ausgewiesen.
- TC-C06: Memberlisten mit sofort nutzbaren Member-`h:…` (Source und Assembly).
- TC-C08 / TC-C09: Incoming/Outgoing-Bäume; Impact ohne Verify-Verdict.
- TC-C10–C12 auf `SOURCE-01`: Hierarchie, Implementierungen, Origin mit konsistentem `Namespace.Type`.
- FALSE-01: `INVALID_ASSEMBLY`, recoverable, kein Crash.
- `RESPONSE_BUDGET_TOO_SMALL` trat in Gruppe C nicht auf.
