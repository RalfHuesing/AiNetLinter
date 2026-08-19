# Nicht umsetzen — bewusst gestrichene / verworfene Ideen

Diese Liste enthält alle Feature-Ideen, die in den Recon-Berichten, Konzepten oder Diskussionen vorgeschlagen, nach Evaluierung aber **bewusst abgelehnt oder verworfen** wurden. Sie dient als Nachweis und verhindert, dass dieselben Ideen erneut diskutiert oder implementiert werden.

---

## 1. `validate_file` (Kompakte Post-Edit-Validierung)

* **Idee:** Ein MCP-Tool, das nach einem Datei-Edit Compiler-Fehler (`CS...`) und Linter-Verstöße für eine einzelne Datei bündelt.
* **Begründung für Ablehnung:** 
  1. **Redundant zu Standard-Workflows:** Coding-Agenten führen im Terminal standardmäßig `dotnet build` und `dotnet test` aus. Compiler-Fehler werden von Roslyn/MSBuild schnell (<2s), vollständig und präzise ausgegeben.
  2. **Bereits abgedeckt:** Linter- und Architekturverstöße werden durch `get_violations` und `safeguard` deterministisch bedient.
  3. **Tool-Sprawl:** Ein zusätzlicher MCP-Wrapper für In-Memory-Kompilierung auf Dateiebene erhöht die Server-Komplexität ohne Mehrwert.

---

## 2. `trace_flow` (Multi-Symbol-Flow-Tracer & Dynamic Dispatch Synthesizer)

* **Idee:** Nachbildung von CodeGraphs `codegraph_explore` (mehrstufige Call-Chains mit Source-Bodies über mehrere Symbole in einem Call).
* **Begründung für Ablehnung:** 
  1. CodeGraph benötigt dies als alleiniges Werkzeug, da es keine guten Einzeltools hat. AiNetLinter läuft als Ergänzung zu Host-Agenten (Claude, Cursor), die mit `find_references`, `get_call_tree`, `dependency_graph` und `get_symbol_body` gezielter und token-effizienter navigieren.
  2. Hoher Heuristik- und Wartungsaufwand bei Reflection / Dynamic Dispatch ohne deterministische Garantien.

---

## 3. `preview_refactor` / `apply_refactor` (Mutierende Refactoring-Tools mit Rollback)

* **Idee:** MCP-Tools, die Roslyn-Refactorings (z. B. Rename, Extract Method) vorberechnen, als Diff anzeigen und anwenden.
* **Begründung für Ablehnung:** 
  1. **Architektur-Verletzung:** Der AiNetLinter MCP-Server ist strikt **read-only** und dient als deterministisches Quality-Gate / Verifikations-Layer.
  2. **Rolle moderner Agenten:** Coding-Agenten führen Datei-Edits und Diff-Prüfungen nativ über ihre Editor-Tools aus. Mutation über MCP erhöht das Fehlerrisiko und die Komplexität drastisch.

---

## 4. `get_fixes` / MCP-Auto-Fix-Generator

* **Idee:** Bereitstellung von automatischen Code-Fixes für Linter-Regeln über den MCP-Server.
* **Begründung für Ablehnung:** 
  1. Linter-Verstöße werden von `get_violations` und `safeguard` mit klaren Hinweisen ausgegeben.
  2. LLMs korrigieren den Code direkt in der Datei schneller und kontextbezogener, als ein statischer Fix-Generator es vorgeben kann.

---

## 5. Semantische / Fuzzy-Codesuche via Embeddings (RAG / Vektordatenbank / Qdrant)

* **Idee:** Suche nach Code über Vektor-Embeddings ("Finde Code für Authentifizierung").
* **Begründung für Ablehnung:** 
  1. **Positionierungs-Bruch:** AiNetLinter steht für deterministische, Roslyn-präzise C#-Statikanalyse ohne externe Modell- oder Cloud-Abhängigkeiten.
  2. **Synchronisations-Overhead:** Ein Vektor-Index müsste bei jedem Datei-Edit zusätzlich zum Roslyn-Staleness-Check aktualisiert werden.
  3. Relevante Symbole lassen sich strukturell über `find_symbol`, `search_pattern` und `metrics_tree` deterministisch auffinden.

---

## 6. PageRank-Repo-Map (`skeleton` im Aider-Stil)

* **Idee:** Gewichtung von Quellcode-Dateien und Symbolen mittels PageRank über den Abhängigkeitsgraphen.
* **Begründung für Ablehnung:** 
  1. Aider benötigt PageRank für heuristisches Tree-Sitter-Parsing. Roslyn liefert bereits exakte semantische Typ- und Referenzauflösung.
  2. `metrics_tree`, `get_index_scope` und `get_hotspots` decken den Orientierungsbedarf deterministisch ab.

---

## 7. Multi-Agent-Installer & Detached-Daemon

* **Idee:** Automatisches Konfigurieren verschiedener IDE-Agenten (Claude, Cursor, Windsurf) über komplexe Installer-Skripte und Betrieb als separater Hintergrund-Daemon mit Lock-File-Arbitration.
* **Begründung für Ablehnung:** 
  1. Unnötige Komplexität. Der Standard über `ainetlinter --mcp-server` (stdio) reicht für alle gängigen Clients völlig aus.
  2. Ein residenter Workspace mit File-Staleness-Check (`McpCodeGraphServerRefresh`) benötigt keine Daemon-Infrastruktur.

---

## 8. Git-History & Blame als eigene MCP-Tools

* **Idee:** Nachbildung von `git log`, `git blame` oder Commit-Historie über MCP.
* **Begründung für Ablehnung:** 
  1. Host-Agenten haben direkten Zugriff auf Git-Befehle im Terminal.
  2. `get_impact` liefert bereits den spezifischen Mehrwert (Git-Diff kombiniert mit Roslyn-Callsite-Blast-Radius).

---

## 9. Cloud- / Enterprise-Features (Phase L / XL)

* **Idee:** Multi-Tenancy, OAuth, OpenTelemetry-Export, zentrale Server-Cluster.
* **Begründung für Ablehnung:** 
  * AiNetLinter ist ein schlankes lokales Entwickler- und Agenten-Tool. Kein Cloud-Service.
