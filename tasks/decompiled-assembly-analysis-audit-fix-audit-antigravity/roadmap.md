# Roadmap: AiNetLinter MCP-Server Optimierungen

Basierend auf den Ergebnissen des Agentic Audits (2026-08-31) strukturiert diese Roadmap die empfohlenen Maßnahmen in drei Phasen.

---

## Phase 1: Sofort-Maßnahmen (Token-Ökonomie & Quick Wins — P1)

- [ ] **M1.1 (TOK-001): Referenz-Kompaktierung in `inspect_assembly`**
  - Wenn `typeName`, `memberName` oder `namespace` gesetzt sind, die Textausgabe von `Referenzen` und `Referenz-Sessions` standardmäßig auf kompakte Summenzeilen reduzieren (`Referenzen: X von Y (gekürzt)`).
  - Einsparung: ~4.000 Tokens pro gezielter Typinspektion.
- [ ] **M1.2 (TOK-002): Diagnose-Deckelung in `find_references` & `get_call_tree`**
  - Bei `includeReferences=true` die Ausgabe von `[Assembly-Diagnostic]` auf max. 5 Zeilen begrenzen und eine Summenzeile anhängen (`Diagnosen: 5 von N (gekürzt)`).
  - Einsparung: ~5.500 Tokens pro Referenzsuche.

---

## Phase 2: UX- und Filter-Verbesserungen (P2)

- [ ] **M2.1 (ASM-002): Heuristische `receiverType`-Vorfilterung in `find_assembly_extensions`**
  - Bei Zustand `not_decidable` (ohne Consumer-Projekt) den Typnamen des ersten Parameters gegen `receiverType` abgleichen und unpassende Treffer ausblenden.
- [ ] **M2.2 (DOC-001): JSON-RPC Beispiele in `Docs/agent-api.md` aktualisieren**
  - Tool-Call-Snippets mit `targetType` und `targetPath` ausstatten.

---

## Phase 3: Dokumentation, Konsolidierung & Minor DX (P3)

- [ ] **M3.1 (DOC-002): Konsolidierung `get_file_tree.maxResults`**
  - Schema-Default (`200`) und Beschreibungstext (`100`) auf einen einheitlichen Wert anpassen.
- [ ] **M3.2 (NAV-002): Inline-Stub-Hinweis in `get_symbol_body`**
  - Bei dekompilierten Metadata-Only-Stubs einen erklärenden Kommentar einfügen (`// [Hinweis: Metadata-only Stub...]`).
- [ ] **M3.3 (SRC-001): Dokumentationsempfehlung für Parameter `symbolIdentifier` vereinheitlichen**
- [ ] **M3.4 (MET-001): Sortierparameter für `get_hotspots` (`sortBy`, `minLineCount`)**
