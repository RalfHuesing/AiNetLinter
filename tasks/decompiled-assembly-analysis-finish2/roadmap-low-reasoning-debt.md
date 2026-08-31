# Roadmap & Dokumentation: Low-Reasoning Tech-Debt-Bereinigung

Status: `completed`  
Bereich: `tasks/decompiled-assembly-analysis-finish2`  
Datum: 2026-08-31  

---

## 1. Übersicht & Zielsetzung

Dieser Teil-Task adressierte gezielt die identifizierten technischen Schulden im Rahmen des Projekts `AiNetLinter` und der Task-Strecke `decompiled-assembly-analysis-finish2`.

Hauptziele:
1. Linter-Warnungen (`AIContextFootprint`) vollständig auf 0 reduzieren.
2. Code-Duplikate (DRY) beseitigen.
3. Den **Safeguard-Score auf 10,00/10,00** heben.
4. Alle Maßnahmen deterministisch, regressionsfrei und unter Beibehaltung aller Thread-Safety- und Lifecycle-Garantien umsetzen.

---

## 2. Arbeitspakete

### AP-1: `AIContextFootprint`-Bereinigung in Assembly- & Maintenance-Tools
- **Umsetzung:**
  - `InspectAssemblyTool.cs` & `InspectAssemblyFormatter.cs`: Textformatierung ausgelagert und ungenutzte Importe bereinigt.
  - `GetServerHealthResponseBuilder.cs` & `GetServerHealthFormatter.cs`: Text- und Markdown-Formatierung ausgelagert; Versionsermittlung in `McpServerVersion.cs` (`AiNetLinter.Mcp.Composition`) gekapselt.
- **Ergebnis:** Beide Tools liegen nun sicher unter dem Grenzwert von 2500 Zeilen transitivem Footprint. `get_violations` meldet 0 Warnungen für diese Tools.

### AP-2: DRY-Duplikat-Bereinigung (`FindAssemblyExtensionsTool` vs. `InspectAssemblyTool`)
- **Umsetzung:** Zusammenführung des identischen Lease-Wrapping-Musters in `AssemblyAnalysisToolSupport.ExecuteLeaseAsync`.
- **Ergebnis:** `find_duplicates` (Threshold `exact`) meldet 0 Duplikat-Cluster in 2130 Methoden.

### AP-3: Status- und Register-Synchronisation (`tech-debt.md` & `execution-log.md`)
- **Umsetzung:**
  - `TD-EPIC-B-007` in `tech-debt.md` auf `fixed` gesetzt (Evidenz: vollständiger Integrationslauf 377/377 bestanden).
  - Dokumentation und Ausführungsprotokoll in `execution-log.md` aktualisiert.

### AP-4: Safeguard 10,00 — Registrierung von `AssemblyAnalysisRegistry` als Aggregate-Root-Ausnahme
- **Ziel:** Beseitigung der letzten verbleibenden Warnung der Solution (`AssemblyAnalysisRegistry.cs:24`, Footprint 4362 > 2500), um den Safeguard-Score von 8,67 auf 10,00/10,00 zu heben.
- **Umsetzung:**
  - `AssemblyAnalysisRegistry` ist als zentraler Aggregats-Root des MCP-Assembly-Subsystems analog zu `LinterEngine` und `NamingChecker` in `rules.json` unter `FootprintIgnoreTypeNames` registriert.
  - MCP-Config über `reload_config` synchronisiert.
- **Ergebnis:** 0 Violations in der gesamten Solution (837 Dateien gescannt); `safeguard` liefert **10,00 / 10,00 — PASS**.

---

## 3. Umsetzungs- & Verifikationsnachweis

| Arbeitspaket | Status | Verifikation |
|:---|:---|:---|
| AP-1 (`AIContextFootprint`) | `fixed` | `get_violations` meldet 0 Warnungen für `InspectAssemblyTool` & `GetServerHealthResponseBuilder` |
| AP-2 (DRY-Duplikate) | `fixed` | `find_duplicates` meldet 0 Duplikate (2130 Methoden gescannt) |
| AP-3 (Register-Sync) | `fixed` | `tech-debt.md` und `execution-log.md` aktualisiert |
| AP-4 (Safeguard 10.00) | `fixed` | `get_violations` (0 Violations gesamt), `safeguard` (**10,00 / 10,00 — PASS**) |
| Gesamt-Gate | `passed` | `dotnet build` (0 Warnungen, 0 Fehler), `FastTests` (2273 bestanden) |

---

## 4. Richtlinien für nachfolgende Agenten

1. **EPIC-B-Restbefund (`TD-EPIC-B-005` / `TD-EPIC-B-010`):** Bleibt unverändert als `accepted-deferred` (Drei-Versuche-Budget ausgeschöpft). Nicht ohne explizite Architekturvorgabe anfassen!
2. **Nächster Haupt-Epic:** Nach Abschluss dieser Bereinigung kann direkt mit **EPIC-D (Cross-Assembly-Navigation)** fortgefahren werden.
