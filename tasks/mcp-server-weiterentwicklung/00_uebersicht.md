---
status: vorschlag
type: findings
project_kind: brownfield
estimated_scope: medium
priority: P1-P3 (je Datei)
author: ox-alpha (Review-Agent)
last_updated: 2026-08-21
open_questions: []
---

# MCP-Server-Weiterentwicklung: Review-Findings (2026-08-21)

## Zweck

Unabhängiger 360°-Review des MCP-Server-Modus durch einen externen LLM-Reviewer. Fokus:
fehlende Features, Architektur-Tech-Debt und Agenten-Ergonomie. Alle Befunde sind gegen den
aktuellen Code verifiziert (Datei-/Zeilenangaben je Finding), nicht spekulativ.

## Kompatibilität mit bestehenden Entscheidungen

Diese Findings widersprechen **keiner** Entscheidung in
`tasks/mcp-agenten-effizienz/90_bewusst-nicht-umsetzen.md` und
`tasks/features/06-nicht-umsetzen.md`:

- Kein RAG, keine Embeddings, kein neuer Analyseprompt-Stack.
- Keine neuen MCP-Tools ohne Nutzungsdaten — die beiden Vorschläge mit neuem Surface
  (Call-Log-Auswertung, Config-Resource) sind bewusst **CLI-offline** bzw. **Resource**
  statt neuer Tools.
- Keine mutierenden Refactoring-Tools, keine Cursor-Pagination, keine pauschalen
  Output-Schemas.

Wo ein Vorschlag nahe an einer verworfenen Idee liegt, ist das explizit markiert.

## Übersicht der Findings

| # | Datei | Thema | Priorität | Charakter |
|---:|---|---|---|---|
| 01 | [01_staleness-check-performance.md](01_staleness-check-performance.md) | Staleness-Check macht bei **jedem** Tool-Call einen rekursiven Verzeichnisbaum-Walk unter dem globalen Lock | P1 | Tech-Debt / Performance |
| 02 | [02_observability-calllog-luecke.md](02_observability-calllog-luecke.md) | `get_server_health` meldet hartcodierte Nullen fürs Call-Log; `calls.jsonl` existiert, aber es gibt keine Auswertung — die Evidenzbasis der 90er-Entscheidungen ist nicht messbar | P1 | Bug-Kandidat + Feature-Lücke |
| 03 | [03_wire-texte-sprachstrategie.md](03_wire-texte-sprachstrategie.md) | Wire-Texte sind ASCII-transliteriertes Deutsch ("geaendert") — worst-of-both zwischen Deutsch und Englisch; Strategieentscheidung fehlt | P2 | Konsistenz / Ergonomie |
| 04 | [04_architektur-beobachtungen.md](04_architektur-beobachtungen.md) | Scanner-Muster, Dateigrößen-Hotspots, Handler-Boilerplate — überwiegend positiv, mit zwei Monitoring-Empfehlungen | P3 | Architektur-Beobachtung |
| 05 | [05_bedingt-sinnvolle-erweiterungen.md](05_bedingt-sinnvolle-erweiterungen.md) | Config-Resource, MCP-Prompts, Progress-Notification, Multi-Solution — je mit Evidenzbedarf | P2-P3 | Feature-Ideen (bedingt) |
| 06 | [06_regel-design-llm-failure-patterns.md](06_regel-design-llm-failure-patterns.md) | Neue Regel-Kandidaten, bewertet am eigenen Akzeptanzkriterium "greift ein konkretes LLM-Failure-Pattern" | P2-P3 | Regel-Design |

## Methodik

- Gelesen: `src/AiNetLinter/Mcp/**` (Kern-Server, Refresh, Registrations, ToolResults,
  IsErrorPolicy.md, OverviewResource), `Docs/ROADMAP.md`, `tasks/**` (beide Initiativen),
  Auszüge aus `Baseline/SourceFileCatalog.cs`, `Commands/McpServerCommand.cs`.
- Live-Abfragen über den laufenden AiNetLinter-MCP-Server (`find_duplicates`,
  `search_pattern`) gegen den eigenen Code.
- Gezielte Verifikationssuchen: Throttling/TTL im Refresh-Pfad (0 Treffer),
  `McpServerPrompt`/Prompts-Primitive (0 Treffer), Call-Log-Auswertung (nur CLI-Option,
  kein Analyzer).

## Empfohlene Reihenfolge

1. **02** zuerst lesen: Der Null-Befund in `get_server_health` ist ein möglicher echter Bug
   und die Call-Log-Auswertung schafft die Nutzungsdaten, die mehrere andere Entscheidungen
   (Tool-Removal, Profile, Prompts) erst entscheidungsfähig machen.
2. **01** danach: Performance-Fix mit klarem, kleinem Scope (TTL + Verzeichnis-Ausschlüsse),
   entlastet jede fremde Codebase, nicht nur dieses Repo.
3. **03–06** sind eigenständig und unabhängig voneinander bewertbar.
