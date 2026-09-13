# MCP-Audit – Gruppe B: Discovery, Scope & Assembly-Inspektion

> **Zuständigkeit:** Subagent B
> **Tools:** `get_index_scope`, `get_file_tree`, `get_namespace_tree`, `inspect_assembly`, `search_assembly`, `find_assembly_extensions`, `get_assembly_context`
> **Regeln:** Nur negative Server-Befunde. Keine echten Produktnamen/Pfade, nur Ziel-Labels.

---

## 1. Übersicht & Testergebnis

- **Geprüfte Tools:** `get_index_scope`, `get_file_tree`, `get_namespace_tree`, `inspect_assembly`, `search_assembly`, `find_assembly_extensions`, `get_assembly_context`
- **Geprüfte Prüffälle:** TC-B01 bis TC-B10; optional Envelope-Vergleich `SOURCE-02`; Form-Stichprobe `LOCAL-02`/`LOCAL-03`; Fehlbedienung `MASK-01`/`MASK-02`
- **Gefundene Befunde:** 0 Critical, 0 Major, 0 Minor

Ohne Befund (kurz, kein Rauschen): TC-B01 Routing auf `search_pattern(pattern, scopeType=all, includePatterns=…)` mit Schema-treuen Parametern; TC-B02 `view=summary` und `view=tree`/`treeDepth=2` navigierbar; TC-B03 `RESPONSE_BUDGET_TOO_SMALL` mit `minimumResponseBytes=2090`, Retry lieferte nicht-leere Landkarte; TC-B04 Assembly-Header `origin=decompiled` / `contentMode=decompiledProject`; TC-B05 Prefix-Filter auf `SOURCE-01` und `LOCAL-01` schränkt ein; TC-B06 Public-API inkl. `h:…` und `continuationToken`, Budget-Retry `maxResponseBytes=2048` mind. 1 Einheit; TC-B07 `INVALID_ASSEMBLY` ohne Crash; TC-B08 Match-Liste mit `h:…` (Treffer nur als `MyNamespace.MyType`); TC-B09 leere Menge `0 von 0` mit Status; TC-B10 Verweise/Identität strukturiert, Zielframework ehrlich `nicht verfügbar`. Masken: Schema kennt kein Glob-`targetPath`; `INVALID_ARGUMENT` + `fieldPath=$.targetPath` recoverable.

---

## 2. Negative Befunde
