# AiNetLinter MCP Codegraph Server – Erweiterungs- & Next-Step-Konzept (`codegraph-mcp-next`)

Dieses Dokument sammelt Ergänzungen, Optimierungen und weiterführende Ideen für künftige Iterationen des `codegraph-mcp`-Servers.

---

## 1. Bereits identifizierte Kern-Erkenntnisse (aus Feedback-Runde 1)

### 1.1 Token-Schutz & Context-Guard (`max_results` & Pagination)
* **Problem:** Abfragen wie `find_references` auf stark genutzte Symbole (z. B. `ToString`, `ExecuteAsync`, `ILogger`) oder breite Substring-Suchen in `find_symbol` erzeugen potenziell Hunderte Ergebnisse und verbrennen Agenten-Tokens.
* **Lösung (KLAR):** Alle Listen-Tools (`find_symbol`, `find_references`, `get_violations`, `search_pattern`) erhalten einen verpflichtenden Parameter `max_results` (Default z. B. `50`).
* **Output-Format:** Wenn Ergebnisse abgeschnitten werden, enthält die Antwort Meta-Infos:
  `"truncated": true, "total_count": 342, "showing": 50`.

### 1.2 Projektstruktur-Änderungen (`.csproj` / Solution-Grenzfälle)
* **Problem:** `WithUpdatedSolution` aktualisiert C#-Dateiinhalte im Speicher. Werden aber `.csproj`-Dateien geändert (neue/gelöschte Dateien, neue Package-Referenzen) oder erfolgt ein `git checkout`, stimmt die In-Memory-Solution nicht mehr.
* **Lösung (KLAR):** Lazy Hash/mtime-Prüfung auf `.csproj`/`.sln`/`.slnx`. Bei Abweichung wird ein leichtgewichtiger Reload der Solution getriggert oder ein Hinweis an den Agenten ausgegeben.

### 1.3 Thread-Safety bei parallelen Tool-Calls
* **Problem:** Modernere KI-Agenten rufen oft mehrere MCP-Tools simultan/parallel auf.
* **Lösung (KLAR):** Der Inkremental-Updater (`SourceFileCatalog` / Cache-Map) wird intern strikt mit einem `SemaphoreSlim` bzw. `lock` geschützt, um Race-Conditions bei parallelen Invalidation-Checks zu verhindern.

### 1.4 Optionales `get_call_tree`-Tool (Zukunftsidee)
* **Idee:** Ein Tool für einen 2-Ebenen Method-Call-Graph (Wer ruft Methode X auf, und was ruft Methode X auf?). Vermeidet, dass der Agent 5x hintereinander `find_references` aufrufen muss.

---

## 2. LLM-Kommunikation & MCP-Bedienung (Feedback-Runde 2)

### 2.1 Weiß ein LLM, wie es den MCP-Server bedienen soll?
* **Analyse:** Modernste LLMs (Claude 3.5/3.7, GPT-4o) benötigen **keine** riesigen externen Dokumentations-Ressourcen. Sie verlassen sich primär auf die vom MCP-Protokoll gelieferten `description`- und `inputSchema`-Felder der Tools.
* **Maßnahmen (KLAR):**
  1. **Server `instructions` im Handshake:** Der MCP-Server nutzt das standardmäßige `instructions`-Feld der `initialize`-Antwort für eine prägnante Arbeitsanleitung (z. B. *"Best Practice Workflow: 1. Scope prüfen via `get_index_scope`, 2. Symbole suchen via `find_symbol`, 3. Skelett prüfen via `get_file_skeleton`"*).
  2. **Strukturierte Tool-Descriptions:** Jedes Tool beschreibt in 2–3 Sätzen exakt den Einsatzzweck (*"When to use / When NOT to use"*) und benennt Grenzen explizit.
  3. **Keine unnötige MCP-Doku-Ressource:** Ein separates Doku-Tool/Ressource verbraucht nur Token im Context-Window des Agenten. Das Schema selbst muss selbsterklärend sein.

---

## 3. Bekämpfung von AI-Drift & Verzahnung mit `rules.json`

### 3.1 AI-Drift Bekämpfung (Pragmatische Werkzeuge)
* **Unrealistisch / Zu teuer:** Vollwertige semantische Code-Klon-Erkennung (AST-Isomorphie oder Vektor-Embeddings), um z. B. zu erkennen, ob ein Algorithmus unter anderem Namen schon 40-mal existiert. Das sprengt die Latenz und das Speicherbudget eines residenten Roslyn-Servers.
* **Realistisch, hochgradig effektiv & machbar (High ROI):**
  * **Duplicate-Symbol-Warnung bei Namens-Kollisionen (`find_symbol`):** Gibt bei >1 Treffer für gleiche Namensmuster einen Drift-Warnhinweis zurück.
  * **Contextual Health & Violation Hints (`get_file_skeleton` / `get_violations`):** Meldet aktive Linter-Violations direkt im Header-Metadatenbereich.

### 3.2 Diskussion: Verzahnung mit `rules.json` (Pro vs. Kontra & Reality-Check)

Hier stehen vier Ideen zur Diskussion, zusammen mit den kritischen Nutzer-Kontra-Argumenten für die spätere Abwägung:

#### 1. Handlungshinweise für KI-Agenten (`"agent_hint"`)
* **Pro-Idee:** `rules.json` speichert pro Regel einen `agent_hint`-Text (*"Controllers must not access DbContext directly..."*), der bei `get_violations` mit ausgegeben wird.
* **⚠️ KONTRA / REALITY-CHECK (Nutzer-Hinweis):**
  * Ist die präzise Detektion solcher komplexen semantischen Regeln nicht teilweise utopisch oder zu schwer zu pflegen?
  * Roslyn-Regeln erkennen oft syntaktische Muster, aber spezifische Architektur-Fixes in Prosa sind schwer wartbar und blähen das Regelwerk auf.

#### 2. Rausch-Filterung & Kategorisierung (`"mcp_config"`)
* **Pro-Idee:** Kosmetische Regeln (Formatting) im MCP-Server ausblenden, um KI-Token zu sparen.
* **⚠️ KONTRA / REALITY-CHECK (Nutzer-Hinweis):**
  * `rules.json` ist bereits heute riesig und unübersichtlich – selbst LLMs blicken bei zu vielen Konfigurationsoptionen kaum noch durch. Noch mehr JSON-Optionen verschlimmern die Komplexität.
  * Ein Verstoß ist ein Verstoß und sollte behoben werden. Verstecken von Regeln schafft verdeckte Tech-Debt.

#### 3. "No-New-Violations"-Ratchet Mode (Delta-Prüfung)
* **Pro-Idee:** In Brownfield-Dateien Alt-Verstöße dulden und nur *neue* Verstöße als Fehler ausgeben.
* **⚠️ KONTRA / REALITY-CHECK (Nutzer-Hinweis):**
  * Eine Baseline/Ratchet muss in Projekten irgendwann abgearbeitet und entfernt werden.
  * Das Duldungs-Pattern führt in der Praxis dazu, dass Tech-Debt über Jahrzehnte rumgeschleppt wird, statt sie konsequent zu beseitigen.

#### 4. Kompaktes Architektur-Briefing (`get_active_rules`)
* **Pro-Idee:** MCP-Ressource oder Tool zur Zusammenfassung der aktiven `rules.json`.
* **⚠️ KONTRA / REALITY-CHECK (Nutzer-Hinweis):**
  * **Redundant!** Wir haben dafür bereits `.agents\rules\AiNetLinter.mdc`, das über den CLI-Command `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only` automatisch generiert und von Agenten-IDEs (Cursor/Windsurf/Antigravity) direkt geladen wird. Ein zusätzliches MCP-Tool dafür ist reine Doppelarbeit.

---

## 4. Benchmark & Markt-Analyse: Features moderner MCP-Codegraph-Server

*(Recherche zu etablierten Tools: Serena, CodeGraphContext, kirograph, codesight-mcp, coa-codenav-mcp, Sourcegraph SCIP)*

| Feature in anderen Servern | Wer bietet es? | Was macht es? | Sinnvoll für AiNetLinter (Roslyn/C#)? |
| :--- | :--- | :--- | :--- |
| **Blast-Radius Traversal** | *kirograph*, *codesight-mcp* | Rekursive Auswirkungsanalyse über $N$ Ebenen ("Wenn ich Signatur X ändere, welche Aufrufer & Interface-Implementierer brechen transitiv?"). | **SEHR SINNVOLL.** Unser `get_impact` ist aktuell eher flach/Git-diff-basiert. Transitive Auswirkungstiefe (Depth=1..N) via Roslyn `SymbolFinder` ist für Refactorings in C# ein absoluter Gamechanger. |
| **Symbol-Level Body Reading (`get_symbol_body`)** | *Serena (Coding Agent Toolkit)* | Liest gezielt nur den Code-Body *einer einzelnen Methode*, statt die gesamte 500-Zeilen-Datei zu laden (`view_file`). | **SEHR SINNVOLL.** Spart massiv Tokens! Agent fragt erst Skelett ab, liest dann punktgenau nur 15 Zeilen Methoden-Rumpf. |
| **DI- & Interface-Implementation Mapping** | *coa-codenav-mcp*, *tokensave* | Welcher Service implementiert `IService.DoWork()`? Auflösung von C# Dependency Injection / Abstraktionen. | **SEHR SINNVOLL.** In modernem C# (ASP.NET Core / DI) sucht die KI ständig nach der konkreten Klasse hinter dem Interface. Roslyn `SymbolFinder.FindImplementationsAsync` leistet das perfekt. |
| **Dead Code / Unused Symbol Detection** | *kirograph* | Erkennt private/interne Methoden oder Felder mit 0 Referenzen in der Solution. | **NICE TO HAVE.** Roslyn kann ungenutzte private/internal Symbole leicht ermitteln. Hilfreich bei Cleanup-Tasks. |
| **PageRank / Symbol Centrality (Repo Map)** | *Aider*, *Sourcegraph SCIP* | Ranking der "wichtigsten" Core-Klassen im Projekt basierend auf Inbound-Referenzen. | **BEREITS TEILWEISE VORHANDEN.** Unsere `HotspotsMap` deckt Kopplung/Zyklomatische Komplexität ab, lässt sich ggf. um Centrality-Scores ergänzen. |

---

## 5. KI-Forschung & Roslyn-Architektur: Stabile Symbol-IDs für Lese-Abfragen

### 5.1 Warum KEIN Server-seitiges File-Edit?
* **Strikte Trennung:** Der MCP-Server führt **keine Schreib-/Editier-Operationen** durch. Das bleibt Aufgabe des Agenten (via Git, `replace_file_content` oder seine eigenen File-Edit-Tools).
* **Vermeidung von Schreib-Konflikten:** Ein zweiter schreibender Prozess auf demselben Git-Working-Tree erzeugt Race-Conditions, unklare Rollbacks und Risiko ohne Mehrwert für Linters Kernkompetenz.

### 5.2 Wozu dient die eindeutige Roslyn Symbol-ID (`DocumentationCommentId`) bei REINEM LESEN?

In mehrstufigen Agenten-Workflows (Read $\rightarrow$ Agent-Edit $\rightarrow$ Re-Read) entsteht ohne eindeutige IDs ein fundamentales Orientierungsproblem:

#### A. Problem: Verrutschte Zeilennummern bei Folgeabfragen
1. **Agent liest:** `find_symbol("ProcessOrder")` $\rightarrow$ Server antwortet: `OrderService.cs:Zeile 45`.
2. **Agent editiert selbst:** Der Agent fügt in `OrderService.cs` 20 Zeilen neuen Validation-Code vor Zeile 45 ein.
3. **Agent fragt nach:** Der Agent möchte nun `find_references` oder `get_impact` für `ProcessOrder` ausführen.
4. **Fehlgeschlagen:** Übergibt der Agent `OrderService.cs:45`, zeigt Zeile 45 im aktualisierten Dokument jetzt auf den neu eingefügten Validation-Code! Die Abfrage schlägt fehl oder analysiert die falsche Code-Stelle.

#### B. Lösung: Roslyn `DocumentationCommentId` als stabiler Anker
* Roslyn bietet über `DocumentationCommentId.CreateDeclarationId(ISymbol)` einen eindeutigen, deterministischen String-Key für jedes Symbol (z. B. `M:MyNamespace.OrderService.ProcessOrder(System.Guid)`).
* Über `DocumentationCommentId.GetFirstSymbolForDeclarationId(id, compilation)` kann der Server das Symbol in der aktualisierten Compilation **sofort wiederfinden** – völlig unabhängig davon, wie viele Zeilen davor eingefügt oder gelöscht wurden!

#### C. Disambiguierung überladener Methoden (Overloads)
In C# gibt es häufig überladene Methoden in derselben Klasse:
```csharp
public class OrderService {
    public void ProcessOrder(int id) { ... }
    public void ProcessOrder(string name) { ... }
    public void ProcessOrder(OrderDto order) { ... }
}
```
Die Symbol-ID `M:MyNamespace.OrderService.ProcessOrder(MyNamespace.OrderDto)` identifiziert **exakt** die dritte Überladung. Der Agent muss nicht raten oder blinde String-Suchen durchführen.

---

## 6. Fundierte Quellenangaben & Referenzen

### Microsoft .NET / Roslyn Compiler API
1. **Microsoft Learn – `DocumentationCommentId.CreateDeclarationId`:** [learn.microsoft.com/.../microsoft.codeanalysis.documentationcommentid.createdeclarationid](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.documentationcommentid.createdeclarationid) – *Erzeugt den eindeutigen XML-Signatur-String für ein Roslyn ISymbol.*
2. **Microsoft Learn – `DocumentationCommentId.GetFirstSymbolForDeclarationId`:** [learn.microsoft.com/.../microsoft.codeanalysis.documentationcommentid.getfirstsymbolfordeclarationid](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.documentationcommentid.getfirstsymbolfordeclarationid) – *Löst eine Symbol-ID deterministisch gegen ein Roslyn Compilation-Objekt auf.*

### KI-Forschung & Agentic Architecture Papers (2024–2026)
3. **Anthropic Research – "Building Effective Agents" (Dec 2024):** [anthropic.com/research/building-effective-agents](https://www.anthropic.com/research/building-effective-agents) – *Forschung zu Orchestrator-Workers, Context Window Spill & Prompt-Pruning in Tool-Calling-Loops.*
4. **ICLR 2025 Paper – "RepoGraph: Repository-Level Code Graph for AI Software Engineering":** [arxiv.org/abs/2410.02678](https://arxiv.org/abs/2410.02678) – *Nachweis, dass deterministisches Context-Engineering über Code Property Graphs auf SWE-bench-Leaderboards höhere Genauigkeit erzielt als bloßes Modell-Scaling.*
5. **Sourcegraph SCIP (Source Code Indexing Protocol) Spec:** [github.com/sourcegraph/scip](https://github.com/sourcegraph/scip) – *Standard für symbol-basierte Code-Navigation und eindeutige Symbol-Bezeichner in großen Codebasen.*

### Etablierte MCP-Codegraph-Implementierungen
6. **Serena (Coding Agent Toolkit MCP):** [github.com/oriserena/serena](https://github.com/oriserena/serena) – *Feature: Symbol-Level Body Reading (`get_symbol_body`).*
7. **kirograph (Semantic Code Knowledge Graph MCP):** [github.com/davide-desio-eleva/kirograph](https://github.com/davide-desio-eleva/kirograph) – *Feature: Blast-Radius Traversal & Dead Code Detection.*
8. **coa-codenav-mcp (Roslyn-basierter MCP Server):** [github.com/anortham/coa-codenav-mcp](https://github.com/anortham/coa-codenav-mcp) – *Feature: Roslyn Call Hierarchy & C# Inheritance Navigation.*

---

## 7. Gesamt-Übersicht: Status & Roadmap-Kandidaten

| Thema | Status | Maßnahme / Abwägung |
| :--- | :--- | :--- |
| `max_results` / Token-Schutz | **Klar** | Verpflichtend für alle Listen-Tools |
| Thread-Safety / Async-Locks | **Klar** | Interne Synchronisation via SemaphoreSlim |
| LLM-Anleitung via Handshake | **Klar** | Server `instructions` im MCP-Initialization-Call |
| Duplicate-Symbol Drift-Warning | **Klar** | Warnhinweis bei >1 Treffer für gleiche Namensmuster |
| Health-Header in `get_file_skeleton` | **Klar** | Metadaten mit Linter-Violations & Hotspot-Score anhängen |
| **`agent_hint` in `rules.json`** | **⚠️ Kontra-Vorbehalt** | *Kritik:* Detektion oft utopisch / schwer zu pflegen. |
| **`mcp_config` Rausch-Filterung** | **⚠️ Kontra-Vorbehalt** | *Kritik:* `rules.json` ist bereits zu groß; Filterung versteckt nur Tech-Debt. |
| **No-New-Violations Ratchet** | **⚠️ Kontra-Vorbehalt** | *Kritik:* Ratchet schleppt Tech-Debt über Dekaden mit. Verstöße müssen gefixt werden. |
| **Architektur-Briefing Tool** | **❌ Verworfen** | *Kritik:* Redundant zu `.agents\rules\AiNetLinter.mdc` (`--sync-agent-rules-only`). |
| **Stabile Symbol-IDs (`DocId`)** | **Klargestellt** | Für Folge-Abfragen (Lese-Referenzen) nach Agenten-Edits, nicht zum Schreiben |
| **Blast-Radius (`get_impact` Depth)** | **NEU (Markt-Benchmark)** | Transitive Aufrufer-Analyse über N Ebenen via Roslyn |
| **`get_symbol_body`** | **NEU (Markt-Benchmark)** | Punktgenaues Lesen nur eines Methodenrumpfs |
| **Interface/DI Resolution** | **NEU (Markt-Benchmark)** | Zuordnung Interface-Methode $\rightarrow$ konkrete Impl. |
| `.csproj`-Invalidierung | **In Diskussion** | Wie tiefgehend? Nur mtime-Check oder voller Event-Reload? |
| `get_call_tree` (Method Graph) | **Idee / Später** | Reicht `find_references` für V1 oder direkt Call-Tree? |
