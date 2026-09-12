# MCP-Tool-Audit Prompt

Vollständiger Prompt für die periodische, autonome Durchführung des **AiNetLinter MCP-Tool Output-, Effizienz- & Chaining-Audits**.

---

## Aufruf im Chat

Einfach folgenden Befehl im Chat eingeben:

```text
Führe .agents/prompts/mcp-tool-audit.md durch
```

Optional kann ein spezifisches Verzeichnis angegeben werden (Default ist `tasks/mcp-tool-output-evaluation-<YYYY-MM-DD>/`).

---

```text
Verwende **KEINE** Skills (.agents/skills/*).
Beachte strikt AGENTS.md sowie .agents/rules/*.mdc (insbesondere AiNetLinter-Richtlinien.mdc und AiNetLinter-McpWorkflow.mdc).
Nutze für alle zielgebundenen Aufrufe targetPath: "c:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx" (bzw. für Assembly-Tools den absoluten Pfad zur kompilierten DLL).

Du bist Audit-Spezialist für MCP-Agenten-Ergonomie und Codeanalyse. Führe den vollständigen Output-, Effizienz- und Chaining-Audit für alle 32 MCP-Tools des AiNetLinter-Servers durch.

### 1. Verzeichnisstruktur anlegen
- Lege unter `tasks/` ein neues Verzeichnis an: `tasks/mcp-tool-output-evaluation-<YYYY-MM-DD>/` (mit aktuellem Tagesdatum; bei Mehrfachläufen am selben Tag Suffix `-2`, `-3` anhängen).

### 2. Die 32 Tools nach Priorität (01 bis 32)
Arbeite alle 32 Tools systematisch und autonom ab:

[Semantik & Navigation]
01: find_symbol
02: get_feature_context
03: get_symbol_body
04: get_file_skeleton
05: get_call_tree
06: find_references
07: get_impact
08: get_type_hierarchy
09: find_implementations
10: get_class_structure

[Discovery & Struktur]
11: get_file_tree
12: get_namespace_tree
13: dependency_graph
14: resolve_type_origin
15: search_pattern

[Linter, Metriken & Qualität]
16: get_violations
17: safeguard
18: find_dead_code
19: find_magic_values
20: find_duplicates
21: pattern_detect
22: metrics_lookup
23: metrics_tree
24: get_hotspots
25: get_test_context

[Assembly & Decompiler]
26: inspect_assembly
27: get_assembly_context
28: search_assembly
29: find_assembly_extensions

[Server & Lifecycle]
30: get_server_health
31: get_index_scope
32: reload_config

### 3. Vorgehen pro Tool & Markdown-Schema
Für jedes Tool wird eine eigene Markdown-Datei erstellt mit folgendem Inhalt:
- **1. Tool-Steckbrief & Agenten-Rolle:** Zweck, wichtigste Parameter und Defaults.
- **2. Test-Samples & Rohe Tool-Outputs:** Mindestens 2–3 reale, unterschiedliche Aufrufe (Standardfall, Scoped/Grenzfall, Negativfall/Fehlerfall). Der Text-Content des Tools MUSS als ungeschnittener Roh-Output in einem Code-Block enthalten sein.
- **3. Effizienz- & SNR-Bewertung:** Byte-/Token-Footprint, Signal-to-Noise Ratio (Boilerplate, redundante Pfade/Disclaimer, Progressive Disclosure, Content-Only-Einhaltung).
- **4. Composability & Chaining-Validierung:** Exakte Prüfung, ob erzeugte Ausgabefelder (`handoffId`, `Pfad:Zeile`, Typen) direkt ohne Transformation als Input für Folgetools genutzt werden können.
- **5. Fazit & Optimierungsempfehlungen:** Prädikat und konkrete Handlungsempfehlungen.

### 4. Synthetischer Code bei Null-Treffern (WICHTIG)
Liefert ein Tool auf der sauberen Codebase 0 Treffer (z. B. `get_violations`, `safeguard`, `find_dead_code`):
- Erzeuge temporär eine isolierte C#-Datei (z. B. `src/AiNetLinter/TempAuditViolation.cs`) mit absichtlichen Regelverstößen.
- Führe den Toolcall aus und sichere die echten Rohdaten.
- Lösche die temporäre Datei UNMITTELBAR wieder und verifiziere `git status` (keine Arbeitskopie-Verunreinigung!).

### 5. BENENNUNGSREGEL MIT `_okay.md` (PFLEGE & FILTER-EFFEKT)
Damit der Nutzer im Dateibaum sofort sieht, welche Tools sauber sind und wo Handlungsbedarf besteht:
- **Prüfung ohne Befund / alles in Ordnung:** Wenn ein Tool eine exzellente Effizienz aufweist, keine SNR-Mängel hat, lückenlose Chaining-Kompatibilität bietet und KEINE offenen To-Dos oder Fehler hinterlässt:
  $\rightarrow$ Benenne die Datei mit Suffix `_okay.md`, z. B.:
  `01_find_symbol_okay.md`, `02_get_feature_context_okay.md`, `03_get_symbol_body_okay.md`.
- **Prüfung mit Befund / Handlungsbedarf:** Wenn ein Tool Mängel aufweist (fehlende Handoff-IDs, gebrochene Ketten, übermäßige Boilerplate, Pfadasymmetrien):
  $\rightarrow$ Behalte den Dateinamen OHNE `_okay`, z. B.:
  `04_get_file_skeleton.md`, `19_find_magic_values.md`.

### 6. Zentrales README.md
Erstelle im Task-Verzeichnis eine zentrale `README.md` mit:
- Gesamt-Matrix aller 32 Tools (Prio, Name, Kategorie, SNR, Chaining-Status, Dateilink).
- Validierungsnachweis der Kern-Chaining-Ketten.
- Priorisierte Top-Findings für Entwickler.
```
