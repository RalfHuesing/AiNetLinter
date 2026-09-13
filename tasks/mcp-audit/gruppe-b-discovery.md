# MCP-Audit – Gruppe B: Discovery, Scope & Assembly-Inspektion

> **Zuständigkeit:** Subagent B
> **Tools:** `get_file_tree`, `get_namespace_tree`, `get_index_scope`, `inspect_assembly`, `search_assembly`, `find_assembly_extensions`, `get_assembly_context`
> **Regeln:** Nur negative Befunde protokollieren. Keine echten Produktnamen/Pfade, nur Ziel-Labels (`SOURCE-01`, `LOCAL-01`, etc.)!

---

## 1. Übersicht & Testergebnis

- **Geprüfte Tools:** `get_file_tree`, `get_namespace_tree`, `get_index_scope`, `inspect_assembly`, `search_assembly`, `find_assembly_extensions`, `get_assembly_context`
- **Geprüfte Prüffälle:** TC-B01 bis TC-B10 aus `FlightPlan.md`
- **Gefundene Befunde:** 0 Critical, 1 Major, 0 Minor

---

## 2. Negative Befunde

### [Major] B-06: `search_assembly`-Treffer ohne Handoff-IDs

- **Betroffenes Tool / Schema**: `search_assembly` (Parameter: `pattern`, `kind`)
- **Ziel-Label**: `LOCAL-01` (Modus: Assembly)
- **Konkreter Aufruf**: `search_assembly(targetPath="<LOCAL-01>", pattern="<MyClass>", kind="type", declarationOnly=true)` und `search_assembly(targetPath="<LOCAL-01>", pattern="<MyMethod>", kind="method")`
- **Beobachtung / Ist-Verhalten**:
  - Pattern stammt aus `inspect_assembly`-Handoff (nicht geraten). Treffer strukturiert als `MyNamespace/MyClass.cs:<Zeile>: <Snippet>`.
  - Keine `handoffId` / keine kanonische Symbol-ID am Match. Status `Assembly-Suche: text; n von n`, `completeness=complete`.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - TC-B08 und die Kette Suche → `get_symbol_body` verlangen konsumierbare Handoff-IDs. Ohne ID muss der Agent Pfad/Zeile oder Typnamen selbst zusammenbauen.
- **Empfehlung**:
  - Pro Match dieselbe `handoffId` wie bei `inspect_assembly` ausgeben (Typ- bzw. Member-ID), nicht nur Datei:Zeile.
