---
task: features-nicht-umsetzen
type: entscheidung
status: final
created: 2026-08-06
purpose: Bewusst gestrichene bzw. nicht verfolgte Epics/Ideen aus der MCP-Server-Aufwertung, mit Begründung
references:
  - 00-master-overview.md
  - 05-roadmap.md
---

# Nicht umsetzen — bewusst gestrichene Ideen

Diese Epics/Ideen wurden in den Recon-Berichten bzw. der ursprünglichen Roadmap-Fassung vom 2026-08-06 vorgeschlagen, nach 360°-Review aber **bewusst gestrichen**. [`05-roadmap.md`](05-roadmap.md) enthält nur noch, was wir tatsächlich umsetzen — hier steht, was wir nicht tun und warum, damit die Entscheidung nachvollziehbar bleibt und bei Bedarf revidiert werden kann.

---

## 1. `trace_flow` (Multi-Symbol-Flow-Tracer) — inkl. Dynamic-Dispatch-Synthesizer

**Ursprünglich geplant als:** S1.1 (Sprint 1, 1-2 Wochen MVP, 5-6 Wochen "production-ready") + M7 (Dynamic-Dispatch-Synthesizer für MediatR/DispatchProxy/EF, 2 Wochen)
**Quelle:** [04-explore-vs-flow-tools.md](04-explore-vs-flow-tools.md), [05-roadmap.md](05-roadmap.md)

**Begründung:** `trace_flow` sollte CodeGraphs Killer-Feature (`codegraph_explore`) nachbilden — ein Tool, das eine mehrstufige Call-Chain zwischen mehreren Symbolen in einem Call auflöst statt in 9 sequenziellen Tool-Calls. Das Problem: CodeGraph braucht dieses Tool, weil es das **einzige** Werkzeug ist, das der jeweilige Agent für Codebase-Verständnis hat (kein natives Read/Grep-Äquivalent mit Symbolwissen). AiNetLinter ist dagegen ein *Zusatz*-Server neben einem Host-Agenten (Claude Code), der bereits gut lesen und grep-en kann. Wir würden also Aufwand investieren, um mit einer Fähigkeit zu konkurrieren, die der Agent schon hat — statt AiNetLinters eigentliche Stärken auszubauen (Roslyn-Präzision, Git-Diff-Impact, deterministisches Linting). Der Dynamic-Dispatch-Synthesizer (M7) diente primär dazu, `trace_flow`s Dynamic-Boundary-Scan zu füttern; ohne `trace_flow` entfällt sein Hauptzweck.

**Revival-Bedingung:** Falls eine echte Messung (z. B. via `--mcp-log`) zeigt, dass Agenten im Drift-Loop wiederholt 5+ Tool-Calls für reine Flow-Fragen brauchen und Read/Grep dabei nachweislich ineffizient sind — nicht auf Basis von CodeGraphs eigenen Zahlen (andere Tool-Kategorie, anderes Nutzungsmuster).

---

## 2. `skeleton` / Repo-Map mit PageRank

**Ursprünglich geplant als:** S2.1 (Sprint 2, 1 Woche)
**Quelle:** [03-market-research.md](03-market-research.md) §4.3 (Aider), [05-roadmap.md](05-roadmap.md)

**Begründung:** PageRank über den File-Dependency-Graph (Aiders Repo-Map-Pattern) löst das Problem "welches von vielen unscharf gefundenen Symbolen ist relevant". Dieses Problem existiert nur bei heuristischer, Tree-Sitter-basierter Symbolauflösung, die nicht weiß, was zusammengehört. Roslyn löst Symbole exakt auf (`find_symbol`, `get_type_hierarchy`) — eine Ranking-Schicht über ein bereits exaktes System zu bauen, behebt kein reales Defizit. `metrics_tree` (S2.5, bleibt in der Roadmap) deckt den Explore-/Drill-down-Bedarf deterministisch und mit weniger Aufwand ab.

**Revival-Bedingung:** Nur falls Solutions so groß werden (>50.000 Dateien), dass selbst `find_symbol`/`metrics_tree` unübersichtlich werden — für die aktuelle Größenordnung nicht der Fall.

---

## 3. `preview_refactor` / `apply_refactor` mit Rollback

**Ursprünglich geplant als:** M4 (Mid-Term, 2 Wochen)
**Quelle:** [03-market-research.md](03-market-research.md) §5.3 F8, [05-roadmap.md](05-roadmap.md)

**Begründung:** Widerspricht der bestehenden Architektur-Leitlinie (`AiNetLinterRichtlinien.mdc §2`: "monolithisch & schlank bleiben"); der MCP-Server ist bewusst read-only. Ein Mutations-Tool mit Rollback-Garantie ändert das Risikoprofil grundlegend — der Server würde selbst auf die Platte schreiben — und konkurriert mit Fähigkeiten, die Coding-Agent + Git ohnehin abdecken (Edit-Tool, `git stash`/`git diff`). Das ist genau das Feld (RoslynMcpServer, SharpLens), auf dem laut eigener Marktanalyse *nicht* differenziert werden soll — AiNetLinter soll Verifikations-Gatekeeper sein, nicht ein weiteres Refactoring-Tool.

**Revival-Bedingung:** Nur falls der agentische Workflow einen atomaren "Edit+Verify+Rollback"-Zyklus braucht, den Git nicht abdeckt — bisher kein belegter Bedarf.

---

## 4. Multi-Agent-Installer

**Ursprünglich geplant als:** S2.4 (Sprint 2, 1 Woche), 6+ Targets (Claude Code, Cursor, Codex, opencode, Windsurf, Aider)
**Quelle:** [01-codegraph-recon.md](01-codegraph-recon.md) §2.2 K9, [05-roadmap.md](05-roadmap.md)

**Begründung:** Onboarding-UX für ein Tool, das aktuell von einem Team in Claude Code genutzt wird. Kein Bezug zu Speed/Fokus/Audit/Kosten — reine Adoptions-Infrastruktur, kopiert von CodeGraphs OSS-Wachstumsstrategie (breite Multi-Agent-Verbreitung als Produktziel), die für ein internes Werkzeug nicht relevant ist.

**Revival-Bedingung:** Falls AiNetLinter tatsächlich von mehreren Teams/Agenten (Cursor, Codex, etc.) parallel genutzt werden soll — dann reicht vermutlich eine einzelne manuell dokumentierte `.mcp.json`-Anleitung, kein Installer-Framework.

---

## 5. Progressive-Disclosure-Meta-Tool

**Ursprünglich geplant als:** M6 (Mid-Term, 1-2 Wochen)
**Quelle:** [03-market-research.md](03-market-research.md) §3.2, [05-roadmap.md](05-roadmap.md)

**Begründung:** Nur nötig, wenn die Tool-Zahl unkontrolliert über ~50 wächst (MCP-Design-Lehre: 5-15 ideal, max 50). Wenn wir konsolidieren statt nur additiv zu erweitern, erreichen wir diese Schwelle nicht. Ein selbst erzeugtes Problem lösen, das wir durch Disziplin vermeiden können.

**Revival-Bedingung:** Falls die Tool-Zahl trotz Konsolidierungsdisziplin über ~30-40 wächst.

---

## 6. Gesamte L-Phase (Cloud-/Enterprise-Infrastruktur)

**Ursprünglich geplant als:** L1-L7 (Long-Term, Quartal 2+)
**Quelle:** [03-market-research.md](03-market-research.md) §2.4/§2.6, [05-roadmap.md](05-roadmap.md)

| Ursprünglich | Inhalt |
|---|---|
| L1 | Streamable-HTTP-Transport für CI/Cloud |
| L2 | Persistenter Index analog `.codegraph/` (`.ainetlinter/`) |
| L3 | Multi-Repo-Cross-Solution-Index (Sourcegraph-light) |
| L4 | OAuth 2.1 + Entra ID für Cloud-Mode |
| L5 | MCP-Apps-Integration (interaktive Diff-Vorschau) |
| L6 | OpenTelemetry-Traces (Observability) |
| L7 | F# + VB.NET-Support |

**Begründung:** Cloud-/Enterprise-Infrastruktur (Remote-Transport, Auth, Multi-Repo, Observability, weitere Sprachen) für ein lokales stdio-Tool, das von einem Solo-/Kleinteam genutzt wird. Kein aktueller Bedarf, keine Nutzer, für die es gebaut würde. Bindet Planungsaufmerksamkeit ohne Wirkung auf das eigentliche Ziel (Agenten schneller/fokussierter/kosteneffizienter machen).

**Revival-Bedingung:** Falls AiNetLinter in CI/CD-Pipelines oder als Mehrnutzer-/Cloud-Dienst betrieben werden soll.

---

## 7. Gesamte XL-Liste ("Beyond L-Phase")

**Ursprünglich geplant als:** XL1-XL10, lose Vision-Ideen ohne feste Phase
**Quelle:** [05-roadmap.md](05-roadmap.md)

| Ursprünglich | Inhalt |
|---|---|
| XL1 | C# 13+-spezifische Analyzer (`required`, Collection Expressions, Primary Constructors) |
| XL2 | F# + VB.NET-Support (Duplikat von L7) |
| XL3 | Custom-Linting-Plugin-Hot-Reload |
| XL4 | IDE-Integration (VS-Extension) |
| XL5 | ML-gestützte Pattern-Suggestions |
| XL6 | Distributed Workspace (mehrere MSBuildWorkspaces, Monorepos) |
| XL7 | Cross-Solution-Symbol-Resolution |
| XL8 | History-Intelligence (git-blame + Churn-Analyse) |
| XL9 | Auto-Generated MCP-Apps |
| XL10 | Federation mit anderen MCP-Servern |

**Begründung:** Vision-Ideen ohne Bezug zum aktuellen Ziel, teils bereits als Duplikat vorhanden (XL2 = L7), teils spekulativ (ML-Suggestions, Federation) oder architektonisch riskant (Plugin-Hot-Reload widerspricht der "monolithisch & schlank"-Leitlinie). Als strukturierte Roadmap-Sektion mit Scores suggerierten sie eine Planungsreife, die nicht bestand.

**Revival-Bedingung:** Einzelfallprüfung bei konkretem Bedarf. Am ehesten denkbar: XL8 (History-Intelligence/Churn-Analyse) hat die geringste Einstiegshürde und passt zum Audit-Fokus, falls dafür mal Bedarf entsteht.

---

## 8. Weitere bewusst nicht verfolgte Punkte (aus der ursprünglichen Roadmap übernommen)

- **Multi-Repo-Index** (Sourcegraph-Pattern) — Aufwand 1 Quartal, Markt noch unklar.
- **MCP-Apps-Integration** — Spec noch instabil, UI-Komplexität (siehe auch Punkt 6, L5).
- **Generative Refactorings (LLM-Aktionen im Server)** — AiNetLinter soll Verifikation liefern, nicht Code generieren.
- **Custom-Plugin-System** — explizit verboten in `AiNetLinterRichtlinien.mdc §2` ("Monolithisch & schlank bleiben").
- **Telemetry/Cloud-Sync** — Datenschutz, AiNetLinter ist internes Werkzeug, kein SaaS-Produkt.
- **Multi-Language-Support (33 Sprachen wie CodeGraph)** — AiNetLinter ist C#-pur, das ist eine Stärke, keine Lücke.
- **Source-Generator-Generation** (`IIncrementalGenerator` für AiNetLinter selbst) — Zukunftsmusik ohne aktuellen Bedarf.

---

## 9. Git-Historie/Blame als eigenes MCP-Tool

**Ursprünglich als Idee vorhanden:** XL8 "History-Intelligence (git-blame + Churn-Analyse)", siehe Punkt 7 oben — dort nur als vage Vision-Idee ohne konkrete Prüfung.
**Quelle:** Dogfooding-Session 2026-08-10/11 gegen das eigene Repo (Nutzer-Nachfrage 2026-08-11: "gibt doch git command line tools — macht es sinn das nachzubauen? wäre das gleiche argument wie search_pattern mit grep?!").

**Begründung:** Ja — genau dasselbe Argument wie bei `search_pattern` (das bewusst als dünner Fallback für Nicht-C#-Inhalte existiert, aber keinen strukturellen Mehrwert gegenüber grep bietet): der Host-Agent (Claude Code) hat bereits nativen Zugriff auf `git log`/`git blame`/`git show` per Bash-Tool — ausgereifte, ubiquitäre CLI-Werkzeuge, die AiNetLinter nicht neu erfinden muss. Ein reiner MCP-Wrapper um diese Befehle würde keinen Mehrwert liefern, den der Agent nicht schon hat. Die einzige denkbare Rechtfertigung wäre eine ECHTE Kombination aus Git-Historie und Roslyn-Symbolauflösung (z. B. "zeige alle Commits, die den Body dieser spezifischen Methode geändert haben — robust über Refactorings/Verschiebungen hinweg, nicht nur über Zeilennummern wie `git log -L`"). Das ist technisch nicht trivial (Symbol-Tracking über Commit-Historie), aktuell nicht als konkreter Bedarf belegt, und würde inhaltlich eher unter M2 `dependency_graph`/M3 `feature_context` fallen als ein eigenes Tool zu rechtfertigen.

**Revival-Bedingung:** Nur falls sich im Drift-Loop wiederholt zeigt, dass reines `git log`/`git blame` (ohne Symbolbezug) für einen Agenten-Task nicht ausreicht UND eine symbolbewusste Variante nachweislich einen Unterschied macht — bisher unbelegt.

---

## 10. Semantische/Fuzzy-Codesuche (Embeddings-basiert)

**Quelle:** Dogfooding-Session 2026-08-10/11, Nutzer-Nachfrage 2026-08-11.

**Begründung:** "Finde den Code, der Authentifizierung macht" ohne bekannten Namen/String ist mit dem aktuellen Tool-Set (exakte Roslyn-Symbolauflösung + Substring-/Regex-Textsuche) nicht abbildbar — das würde Embeddings/Vector-Search erfordern. Widerspricht der strategischen Positionierung (§0: deterministisch, Roslyn-präzise, kein Modell-/Cloud-Abhängigkeit) und dem Anti-Ziel "kein weiterer Code-Intelligence-Server, sondern Verifikations-Gatekeeper". Es gibt bereits spezialisierte Werkzeuge für semantische Codesuche (IDE-Plugins, dedizierte RAG-Systeme) — AiNetLinters Differenzierung liegt woanders (siehe §0).

**Revival-Bedingung:** Keine vorgesehen — würde die Kern-Positionierung des Tools verändern, nicht nur erweitern.

---

## 11. Revidierte frühere Planungs-Philosophie

Die ursprüngliche Roadmap-Fassung folgte der Prämisse "perspektivisch alles drin" — jede aus den Recons abgeleitete Idee wurde in eine Phase (Q/S/M/L/XL) einsortiert, mit dem Ziel, möglichst vollständig zu sein. Nach Review wurde das umgekehrt: **die Roadmap enthält nur noch, was wir aktiv bauen wollen.** Grund: Die Recon-Dokumente selbst identifizieren als Kernlehre aus CodeGraph "5-15 Tools ideal, Single-Tool-Doctrine, Progressive Disclosure" — eine Roadmap, die auf 17+ Tools plus Cloud-Infrastruktur plus einer zehnteiligen Vision-Liste zusteuert, widerspricht dieser eigenen Erkenntnis. Konsolidieren vor Erweitern, und nur bauen, was Speed, Fokus, Audit-Fähigkeit oder Kosten-/Token-Effizienz direkt verbessert.
