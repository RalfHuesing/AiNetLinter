---
type: priorisierung
erstellt: 2026-08-12
frage: "Nächster Schritt, der bei agentischer Programmierung projektübergreifend hilft — magic-values-in-mcp oder etwas aus tasks/features/?"
---

# Priorität: Nächster Schritt für agentisches Programmieren (projektübergreifend)

## Bewertungslinse

Nicht "was ist für AiNetLinter selbst am elegantesten", sondern: **welcher Schritt hilft am meisten, wenn ein KI-Agent (Claude Code) in einer beliebigen C#-Codebase arbeitet** — inkl. fremder Projekte (`SqlToAi`, `SourceToAI`), nicht nur im eigenen Repo. Eigenes Leitbild (`05-roadmap.md` §0) stützt das: AiNetLinter soll die *"Verifikations-Schicht für agentische C#-Workflows — Gatekeeper zwischen AI-Output und CI-Merge"* sein, nicht ein Code-Intelligence-Server für ein einzelnes Team.

Portabilität selbst ist **kein offener Punkt** — `README.md` dokumentiert bereits `--path <beliebige.sln>` und "in mehreren Projekten gleichzeitig nutzen" als Standard-Einstieg. Es geht also um: welches *Tool/Feature* liefert den größten Hebel, sobald AiNetLinter woanders läuft.

## Empfehlung in einem Satz

**Zuerst `find_magic_values` fertig umsetzen** (Konzept ist heute fertig geworden, sofort startklar, deckt ein universelles KI-Codegenerierungs-Antipattern inkl. Secrets ab) — **danach direkt `validate_file`** als Konzept aufsetzen (fehlendes Kern-Primitive: kompaktes Post-Edit-Feedback, das in *jedem* Edit in *jedem* Projekt gebraucht wird, öfter als jedes Audit-Tool).

## P1 — Jetzt

| # | Idee | Warum P1 (cross-project Hebel) | Aufwand | Quelle |
|---|---|---|---|---|
| 1 | **`find_magic_values`** | Konzept fertig (heute), sofort an `drift-loop` übergebbar. Magic Values/Secrets sind ein universelles KI-Generierungsproblem — in jeder Codebase relevant, nicht AiNetLinter-spezifisch. | mittel, Konzept steht | `tasks/magic-values-in-mcp/konzept.md` |
| 2 | **`validate_file`** (F1) | Kompaktes Post-Edit-Feedback (Compiler-Errors + Lint-Diagnostics + `nextSteps`) in *einem* Call. Fehlt aktuell im 18-Tool-Set — dabei ist "nach jedem Edit verifizieren" die mit Abstand häufigste Aktion in jeder Agent-Loop, in jedem Projekt. Höherer Frequenz-Hebel als jedes Audit-Tool. | noch kein Konzept, Basis (`LinterAutoFixer`/Diagnostics) existiert schon | `03-market-research.md` §5.1 |

## P2 — Als Nächstes

| # | Idee | Warum | Quelle |
|---|---|---|---|
| 3 | **Dogfooding auf `SqlToAi`/`SourceToAI`** *(neu, Vorschlag)* | Kein Code — einfach `ainetlinter --mcp-server --path <fremdes-repo>.sln` real gegen eines der anderen KI-generierten Projekte laufen lassen und beobachten, wo Pfad-/Config-Annahmen brechen. Billigste Methode, "funktioniert cross-project wirklich" zu verifizieren, bevor mehr draufgebaut wird. | — |
| 4 | **`metrics_lookup`** (S2.3) | One-Shot-Metrik-Bündel (CC/CogC/LOC/Params) — generisch in jeder Codebase nützlich, entsperrt `feature_context`. | `05-roadmap.md` |
| 5 | **`get_ai_context_footprint`** | Hilft Agenten beim Einschätzen des Refactoring-Budgets — am wertvollsten genau dort, wo der Agent den Code noch **nicht** kennt (fremdes Projekt). Basis (`AIContextFootprintCalculator`) existiert schon. | `02-ainetlinter-mcp-current.md` Q14 |

## P3 — Später, sobald Bedarf belegt ist

| # | Idee | Warum zurückgestellt | Quelle |
|---|---|---|---|
| 6 | `feature_context` (M3) | Hängt an `metrics_lookup`, erst danach sinnvoll. | `05-roadmap.md` |
| 7 | Naming-Familien-Erkennung (Idee E, `naming_families`) | Logischer DRY-Nachfolger (A/F/C fertig), aber eher aus AiNetLinter-Selbstaudit entstanden als aus generischem Cross-Project-Bedarf. | `07-drift-audit-ideen.md` |
| 8 | `get_fixes`/Auto-Fix-Preview | Nett, aber Agent kann Diffs im Editor/Git ohnehin selbst lesen — kein harter Blocker. | `02-ainetlinter-mcp-current.md` Q7 |
| 9 | ASP.NET-Analyzer-Suite (M1) | Hoher Aufwand (6 Regeln + Tools), nur relevant falls Zielprojekte tatsächlich ASP.NET sind — vorher prüfen, ob `SqlToAi`/`SourceToAI` das überhaupt betrifft. Explizit als eigenständiges Vorhaben markiert. | `05-roadmap.md` M1 |
| 10 | `test_coverage_context` (M5) | Laut eigener Roadmap bereits als nachrangig eingestuft — `find_references` deckt einen Teil ab. | `05-roadmap.md` |

## P4 — Geringer Hebel, nicht aktiv verfolgen

`list_projects`, `get_node`-Aufwertung, Cursor-Pagination für `get_violations`, Tool-Call-Stat-Aggregat, FileSystemWatcher-Push — verwaiste Ideen aus den Recon-Docs (01-04), nie in die Roadmap übernommen. Interne Politur ohne direkten Agentic-Hebel; erst aufgreifen, wenn eines davon konkret als Reibungspunkt auffällt.

## Nicht erneut vorschlagen (bereits abgelehnt, `06-nicht-umsetzen.md`)

`trace_flow`, `skeleton`/PageRank-Repo-Map, `preview_refactor`/`apply_refactor`, Multi-Agent-Installer, Progressive-Disclosure-Meta-Tool, komplette L-Phase (Cloud/HTTP/OAuth/OTel), komplette XL-Liste (Plugin-System, ML-Suggestions, Cross-Solution), Embeddings/RAG-Suche, Git-History als eigenes Tool.

## Offener Punkt

`tasks/magic-values-in-mcp/konzept.md` ist inhaltlich fertig, aber noch nicht explizit auf `status: ready` gesetzt — das entscheidet der Nutzer, nicht der Agent (siehe `dev-loop/planning/orchestrator.md` Schritt 6).
