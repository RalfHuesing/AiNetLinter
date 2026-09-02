# Task: Schonungsloser Agentic Usability & Token-Cost Audit des AiNetLinter MCP-Servers

## Rolle & Mindset
Du bist ein kritischer, autonomer Senior-Entwickler und MCP-Architekt. Deine Aufgabe ist es, den **AiNetLinter MCP-Server** in einem realen, praxisnahen Praxistest aus rein **agentischer Sicht** auf Herz und Nieren zu prüfen.

Es gilt eine strikte **Zero-Praise Policy**: Notiere **keinerlei Lob** und keine Dinge, die „gut funktionieren“. Dokumentiere ausschließlich Reibungsverluste, inkonsistente Parameter, Token-Verschwendung, Sackgassen bei der Code-Navigation und fehlende Analysefähigkeiten. Ziel ist ein schonungsloser, priorisierter Mängel- und Backlog-Katalog zur Weiterentwicklung des Servers.

---

## ⚠️ Verbindliche Sicherheits- & Arbeitsregeln

1. **Ausschließlich AiNetLinter MCP-Tools:**
   - Du darfst für die semantische Code- und Assembly-Analyse **NUR** die AiNetLinter-MCP-Tools verwenden.
   - Die Nutzung von `grep`, `ripgrep`, Bash-Dateilesen (`cat`, `type`), `find_in_files` oder externen Decompilern ist für die Code-Inspektion **streng verboten**.
2. **Strikter Schutz externer IP / Vollständige Anonymisierung:**
   - Als externe Assemblies nutzt du die Pfade aus `temp/decompiled-assembly-audit-examples.md`.
   - **Niemals** dürfen Klarnamen von fremden Namespaces, Klassen, Methoden, Parametern oder Dateipfaden in Git-Dateien gelangen!
   - Nutze im gesamten Bericht ausnahmslos die Labels (`LOCAL-01`, `LOCAL-02`, `FALSE-01` etc.) und abstrakte Platzhalter (z. B. `[LOCAL-01] Type_A.Method_B() -> [LOCAL-02] Type_C`).
3. **Vollautonome Ausführung:**
   - Führe den gesamten Audit ohne Zwischenfragen autonom durch.
   - Halte alle Ergebnisse in `tasks/using-audit-funktionstest/findings.md` fest.

---

## Durchführung in 3 Phasen

### Phase 1: Externe Assemblies & Cross-Assembly-Navigation (Teil B)
Nutze `temp/decompiled-assembly-audit-examples.md` mit den deklarierten Labels (`LOCAL-01` bis `LOCAL-03`, `FALSE-01`):
1. **Explorative Entdeckung:**
   - Starte mit `inspect_assembly` und `find_assembly_extensions` auf `LOCAL-01` und `LOCAL-02`.
   - Suche dir selbstständig interessante Business-Typen oder Schnittstellen heraus und leite dir daraus eigenständig realistische Entwickler-Workflows ab (z. B. *„Wie hängen Buchungslogik und Belegverarbeitung über Assembly-Grenzen hinweg zusammen?“*).
2. **Cross-Assembly Tracking & Call-Trees:**
   - Versuche mit `find_symbol`, `find_references` und `get_call_tree` von einem Typ in `LOCAL-01` zu einem aufgerufenen Typ in `LOCAL-02` zu springen.
   - Prüfe kritisch: Nennt `LOCAL-01` die Referenz auf `LOCAL-02`? Wo bricht die Navigation ab? Bekommst du Stubs ohne Inhalt oder irreführende leere Listen?
3. **Negativ- & Robustheitstest:**
   - Teste den Negativfall `FALSE-01` (nicht verwaltete Binärdatei). Wie reagiert der Server? Gibt es einen Crash, Token-Müll oder eine saubere recoverable Diagnose?

### Phase 2: Solution- & Architektur-Tools an der AiNetLinter-Solution (Teil A)
Prüfe die Projekt-Tools (`targetType: "project"`, `targetPath: "."`) an diesem Repository:
1. **Kontext- & Navigationsergonomie:**
   - Teste `get_feature_context` und `get_call_tree` an zentralen Linter-Klassen. Liefert der Kontext präzise Informationen für einen Prompt oder flutet er den Kontext mit Token-Ballast?
   - Teste `get_file_tree` mit verschiedenen Views (`summary`, `tree`, `files`). Ist die Begrenzung ergonomisch oder verleitet das Tool zu Fehlern?
2. **Regel- & Metrik-Tools:**
   - Teste `safeguard`, `get_violations`, `find_dead_code`, `find_duplicates` und `find_magic_values`.
   - Sind die Outputs maschinenlesbar, präzise und für einen Agenten direkt verwertbar, oder erfordern sie unnötige Folgeabfragen?

### Phase 3: Root-Cause-Lokalisierung im eigenen Code (Selbsttest)
Nutze für **jedes** identifizierte Problem wiederum die AiNetLinter MCP-Tools (`find_symbol`, `get_symbol_body`, `find_references` an der eigenen Solution), um die exakte Stelle im Source-Code von `src/AiNetLinter/` zu lokalisieren, an der der Fehler oder das Optimierungspotenzial liegt (z. B. Handler-Klasse, Formatting-Service, Option-Parser).

---

## Output-Format: `tasks/using-audit-funktionstest/findings.md`

Strukturiere das Zieldokument wie folgt:

### 1. Management Summary & Backlog-Matrix
Eine tabellarische Übersicht aller Findings:
| ID | Kategorie | Schweregrad (P1-P3) | Aufwand (S/M/L) | Betroffenes Tool | Kurzbeschreibung |
|---|---|---|---|---|---|

### 2. Detaillierte Mängelberichte
Jedes Finding muss nach diesem einheitlichen Raster aufgebaut sein:

#### `[F-xx]` <Kurztitel des Problems>
- **Kategorie:** `[API & Parameter]` | `[Token-Waste & Payload-Bloat]` | `[Agenten-Sackgasse / Graph-Bruch]` | `[Fehlende Capability / Falsche Annahme]`
- **Schweregrad:** P1 (Blocker / massiver Token-Waste) | P2 (Mittelschwer / Ergonomie-Hürde) | P3 (Minor / Ineffizienz)
- **Geschätzter Aufwand:** S (Stunden) | M (1-2 Tage) | L (Architektur-Änderung)
- **Betroffene MCP-Tools & Parameter:** Exakter Tool-Name und typischer Aufruf.
- **Symptom & Agentic Friction:** Was wollte der Agent erreichen? Warum scheiterte er oder warum war der Weg unnötig mühsam?
- **Token-Impact:** Warum war die Antwort ineffizient (z. B. redundante Doppelung von Text/JSON, ungefilterte Listen, fehlende Paginierung)?
- **Lokalisierte Codestelle (AiNetLinter):** Exakte C#-Klasse und Methode im AiNetLinter-Repository, die dafür verantwortlich ist (ermittelt via AiNetLinter MCP).
- **Konkreter Lösungsvorschlag:** Klare Handlungsanweisung, wie das Verhalten im Code angepasst werden muss.
