# Audit-Report 06: Token-Effizienz, Agentic UX, Error-Formate & MCP-Protokoll-Verträge

**SubAgent:** SubAgent 6 (Token Economics & Protocol UX)  
**Status:** Abgeschlossen  
**Prüfdatum:** 2026-08-31  
**Fokus:** Token-Footprint aller Tools, Payload-Größen, Sufficiency-Hinweise, StructuredContent vs. Text, Fehlerbehandlung & `isError`-Policy

---

## 1. Systematische Messung der Token-Effizienz

| Tool & Abfrageszenario | Typische Payload | Geschätzte Tokens | Bewertung |
| :--- | :--- | :--- | :--- |
| `get_server_health` (global) | ~0,8 KB | ~180 Tokens | **Hervorragend** (Kompakt, schnell, informativ) |
| `get_server_health` (Assembly mit `maxDiagnostics=5`) | ~1,2 KB | ~280 Tokens | **Sehr gut** (Effektiv gedeckelt) |
| `get_file_tree` (`view="summary"`) | ~1,1 KB | ~250 Tokens | **Vorbildlich** (Ersetzt 15k-Token File-Lists) |
| `get_file_tree` (`view="tree"`, `treeDepth=2`) | ~1,8 KB | ~400 Tokens | **Sehr gut** |
| `get_feature_context` (Composite 5-in-1) | ~1,8 KB | ~420 Tokens | **Benchmark** (Höchste Informationsdichte pro Token) |
| `get_file_skeleton` (1 Datei) | ~0,9 KB | ~200 Tokens | **Hervorragend** (Mit stabilen DocCommentIds) |
| `get_symbol_body` (1 Methode) | ~1,2 KB | ~280 Tokens | **Hervorragend** (Exakter Rumpf) |
| `get_class_structure` (1 Klasse, 14 Member) | ~1,4 KB | ~320 Tokens | **Hervorragend** (Dichte Markdown-Tabelle) |
| `get_type_hierarchy` (1 Typ) | ~0,7 KB | ~160 Tokens | **Hervorragend** |
| `dependency_graph` (1 Datei) | ~0,8 KB | ~180 Tokens | **Hervorragend** |
| `get_violations` (Clean Solution, 856 Dateien) | ~0,4 KB | ~80 Tokens | **Hervorragend** |
| `safeguard` (Quality-Gate) | ~0,3 KB | ~60 Tokens | **Hervorragend** |
| `metrics_lookup` (1 Typ) | ~0,9 KB | ~200 Tokens | **Hervorragend** |
| `find_assembly_extensions` (4 Extensions) | ~1,5 KB | ~350 Tokens | **Sehr gut** (Kompakte Referenzzeilen) |
| `inspect_assembly` (Vollständige Assembly) | ~18,4 KB | ~4.500 Tokens | **Akzeptabel** für kompletten API-Überblick |
| `inspect_assembly` (mit Filter `typeName="X"`) | ~18,4 KB | ~4.500 Tokens | **KRITISCH INEFFIZIENT** (>98% unangeforderte Referenzdaten) |
| `find_references` (`includeReferences=true`) | ~24,8 KB | ~6.000 Tokens | **KRITISCH INEFFIZIENT** (100+ Zeilen Roh-Diagnosen) |

---

## 2. Analyse der Agentic Developer Experience (DX)

### 2.1 Standardisierte Header & Provenienz
- Jeder Assembly-Aufruf beginnt mit einem konsistenten Metadaten-Header:
  `[ASSEMBLY] targetType=assembly; targetPath=...; origin=decompiled; sourcePath=none; snapshot=none; confidence=medium; trust=untrusted; generation=1; status=partial; completeness=partial`
- Agenten können Provenienz (`decompiled` vs `source-backed`), Vertrauen und Vollständigkeit deterministisch parsen.

### 2.2 Anti-Looping Sufficiency-Hinweise
- Nahezu alle semantischen Tools schließen mit einer klaren Sufficiency-Zeile ab:
  `[HINWEIS]: Diese Daten sind vollstaendig fuer den angefragten Scope — kein zusaetzliches Read/Grep noetig.`
- **Wirkung:** Verhindert redundante `rg`/`grep`-Nachprüfungen durch LLMs und stoppt unnötige Agenten-Schleifen zuverlässig.

### 2.3 GFM-Formatierung & Tabellen
- Starker Einsatz von Markdown-Tabellen (Klassenstrukturen, Regeln, Metrikabgleiche, Hotspots).
- Ermöglicht LLMs eine strukturierte, spaltenbezogene Informationsaufnahme.

---

## 3. Fehler- und Protokoll-Verträge (`isError`-Policy)

1. **Target-Validierung:**
   - Ungültige `targetType`- oder fehlende `targetPath`-Werte liefern konsistent `[ERROR]: INVALID_ARGUMENT` mit `IsError=false` (recoverable) und einem konkreten `hint`-Block.
2. **Unsupported Targets:**
   - Der Aufruf projektgebundener Tools gegen Assembly-Pfade liefert einheitlich `[ERROR]: ASSEMBLY_TARGET_UNSUPPORTED` mit Handlungsanweisung (`Für dieses Assembly-Ziel eine unterstützte Roslyn-Abfrage oder targetType='project' verwenden`).
3. **Deterministische Fehlercodes:**
   - Alle Fehler sind mit standardisierten Codes typisiert (`INVALID_ARGUMENT`, `ASSEMBLY_TARGET_UNSUPPORTED`, `PROJECT_NOT_INITIALIZED`, `RULES_INVALID`).

---

## 4. Kern-Befunde & Handlungsempfehlungen

### Befund TOK-001 (S1 / U1 / P1): Referenz-Drosselung bei gefilterten `inspect_assembly`-Abfragen
- **Priorität:** P1 (Höchste Priorität für Token-Einsparung)
- **Maßnahme:** Wenn `typeName`, `memberName` oder `namespace` übergeben werden, die Sektionen `Referenzen:` und `Referenz-Sessions:` auf jeweils 1 Zeile zusammenfassen (analog zu `find_assembly_extensions`).
- **Einsparpotenzial:** ~4.000 Tokens (~85-90%) pro gefiltertem Aufruf.

### Befund TOK-002 (S1 / U1 / P1): Diagnose-Deckelung bei `find_references(includeReferences=true)`
- **Priorität:** P1
- **Maßnahme:** Deckelung der `[Assembly-Diagnostic]`-Zeilen im Textoutput auf maximal 5 Zeilen mit Summenzeile (`Diagnosen: 5 von 95 (gekürzt)`).
- **Einsparpotenzial:** ~5.500 Tokens (~90%) pro Referenzsuche.

---

## 5. Fazit SubAgent 6
Bis auf die beiden isolierten Token-Spitzen bei gefiltertem `inspect_assembly` und `find_references(includeReferences=true)` gehört der AiNetLinter MCP-Server zu den token-effizientesten und beststrukturierten MCP-Servern im gesamten Entwicklungsumfeld. Die Behebung der beiden Punkte wird die Kontext-Ökonomie perfektionieren.
