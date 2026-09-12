# Gesamtbewertung: AiNetLinter MCP-Tools Output- & Chaining-Audit

> **Audit-Umfang:** Systematische empirische Untersuchung aller **32 MCP-Tools** des `AiNetLinter`-Servers auf Live-Daten der C#/.NET 10 Solution.
> **Evaluierungskriterien:** Token-Effizienz (Signal-to-Noise Ratio gemäß [AiNetLinter-Richtlinien](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/.agents/rules/AiNetLinter-Richtlinien.mdc)), Content-Only-Hard-Cut und Zero-Transformation-Composability (Output von Tool A direkt als Input für Tool B).

---

## 1. Statusübersicht der 32 MCP-Tools

> **Hinweis zur Verzeichnisbereinigung:** Gemäß Vorgabe wurden alle 27 einwandfreien Detailberichte (`*_okay.md`) bereinigt. Das Verzeichnis enthält nun fokussiert die Berichte zu den Tools mit Handlungs- bzw. Abstimmungsbedarf sowie den Behebungsnachweis für `find_duplicates`.

### Tools mit Detailbericht (Handlungs- & Abstimmungsbedarf)

| Prio | Tool-Name | Kategorie | Effizienz / SNR | Status / Befund | Detailbericht |
|:---|:---|:---|:---:|:---|:---|
| **04** | `get_file_skeleton` | Semantik & Navigation | 🟡 Gut (kompakt) | ⏳ **Offener Architekturentscheid:** Handoff-IDs auf Typebene vs. Memberebene vs. Dokukorrektur | [04_get_file_skeleton.md](04_get_file_skeleton.md) |
| **13** | `dependency_graph` | Discovery & Struktur | 🟢 Sehr gut | ⏳ **Offener Architekturentscheid:** Pfad-Philosophie (relative Pfade vs. absolute Pfade für dekompilierte Assemblies) | [13_dependency_graph.md](13_dependency_graph.md) |
| **19** | `find_magic_values` | Linter & Qualität | 🔴 Hoher Footprint | ⏳ **Offener Architekturentscheid:** Unterdrückung leerer Kategorien berührt `Docs/agent-api.md`-Kontrakt | [19_find_magic_values.md](19_find_magic_values.md) |
| **20** | `find_duplicates` | Linter & Qualität | 🟢 **Bereinigt** | ✅ **Behoben:** `Evidenzgrenze` & `Countercheck` in Header verlagert (~42% Token-Ersparnis, Tests grün) | [20_find_duplicates.md](20_find_duplicates.md) |
| **21** | `pattern_detect` | Linter & Qualität | 🟢 Gut | ⏳ **Offene Design-Überlegung:** Zusammenfassung von 0-Treffer-Pattern-Blöcken | [21_pattern_detect.md](21_pattern_detect.md) |

### Geprüfte Tools ohne Befund (27 Tools – Vollständig verifiziert)
Folgende 27 Tools wurden im Audit vollständig empirisch getestet, erreichten eine exzellente SNR und Zero-Transformation-Composability und wiesen keinerlei Mängel auf:
- `find_symbol` (01), `get_feature_context` (02), `get_symbol_body` (03), `get_call_tree` (05), `find_references` (06), `get_impact` (07), `get_type_hierarchy` (08), `find_implementations` (09), `get_class_structure` (10)
- `get_file_tree` (11), `get_namespace_tree` (12), `resolve_type_origin` (14), `search_pattern` (15)
- `get_violations` (16), `safeguard` (17), `find_dead_code` (18), `metrics_lookup` (22), `metrics_tree` (23), `get_hotspots` (24), `get_test_context` (25)
- `inspect_assembly` (26), `get_assembly_context` (27), `search_assembly` (28), `find_assembly_extensions` (29), `get_server_health` (30), `get_index_scope` (31), `reload_config` (32)

---

## 2. Zentrale Chaining-Ketten (Funktionsnachweis)

### Kette 1: Semantische Symbol-Navigation (Quellcode)
$$\text{find\_symbol} \xrightarrow{\text{handoffId}} \text{get\_feature\_context} \xrightarrow{\text{handoffId}} \text{get\_symbol\_body}$$
- **Ergebnis:** **100% Zero-Transformation.** Die von `find_symbol` generierte Handoff-ID (`s:tJX4Nn33...`) kann ohne jede Modifikation durchgereicht werden.

### Kette 2: Binärcode- & NuGet-Dekompilation
$$\text{resolve\_type\_origin} \xrightarrow{\text{Output-Assembly}} \text{inspect\_assembly} \xrightarrow{\text{a:... handoffId}} \text{get\_symbol\_body}$$
- **Ergebnis:** **100% Zero-Transformation.** Der vom Roslyn-Metadatenfinder ermittelte Pfad (`xunit.v3.assert.dll`) öffnet die Assembly in `inspect_assembly`, welche Assembly-IDs (`a:WmlQsg...`) erzeugt. Diese dekompiliert `get_symbol_body` live in genau 1 Turn zu sauberem C#-Code.

### Kette 3: Quality-Gate & Refactoring
$$\text{safeguard} \xrightarrow{\text{Datei:Zeile}} \text{get\_symbol\_body} \xrightarrow{\text{replace\_file\_content}} \text{safeguard}$$
- **Ergebnis:** **100% Zero-Transformation.** Die `Datei:Zeile`-Positionen aus Fehlermeldungen passen exakt zu den `symbolIdentifiers` von `get_symbol_body` und den Zeilen-Offsets im Editor.

---

## 3. Umgesetzte Behebung & Offene Architekturentscheidungen

### ✅ Direkt umgesetzt: `find_duplicates` Boilerplate-Deduplizierung
- **Problem:** Unter jeder einzelnen Methode in jedem Duplikat-Cluster wurden 2 identische Disclaimer-Zeilen (`Evidenzgrenze:...` und `Countercheck:...`) wiederholt (~40% Token-Overhead).
- **Lösung:** In [`DuplicateDetectionTool.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/DuplicateDetection/DuplicateDetectionTool.cs) und [`RefactoringDriftResponseBuilder.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/DuplicateDetection/RefactoringDriftResponseBuilder.cs) werden die Disclaimer nun einmalig im Header ausgegeben und bei den Member-Einträgen unterdrückt.
- **Verifikation:** Neuer Red-Test `ExecuteAsync_MultipleMembersInCluster_EvidenzgrenzeAppearsOnlyOnceInHeader` sowie alle FastTests und IntegrationTests erfolgreich grün.

### ⏳ Zur Abstimmung vorgemerkt (Architekturentscheidungen erforderlich)

1. **`04_get_file_skeleton` – Handoff-IDs:**
   - Abstimmung nötig, ob Handoff-IDs auf Typebene, Typebene + Memberebene (hoher Token-Footprint) oder gar nicht (reiner Signaturüberblick, Dokukorrektur) ausgegeben werden sollen.

2. **`13_dependency_graph` – Pfad-Philosophie & Decompiler-Workflows:**
   - Abstimmung nötig, wie mit absoluten Pfaden verfahren wird. Bei dekompilierten Assemblies ist der absolute Verzeichnispfad essenziell, damit Agenten Quellcode auch mit Built-in-Tools durchsuchen können. Mögliche Lösung: Einmalige Basis-Pfad-Angabe im Header.

3. **`19_find_magic_values` – Kontrakt in `Docs/agent-api.md`:**
   - Das Ausgeben aller 7 Kategorien ist vertraglich in `Docs/agent-api.md` und `FindMagicValuesPrecisionContractTests` fest verankert. Eine Unterdrückung leerer Kategorien erfordert eine Freigabe zur Kontraktänderung.

4. **`21_pattern_detect` – Zusammenfassung leerer Abschnitte:**
   - UX-Formatierungsentscheidung, ob leere Abschnitte bei ungefilterten 0-Treffer-Läufen in eine gemeinsame Statuszeile überführt werden sollen.
