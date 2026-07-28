# Referenzen & Recherche-Notizen

Externe Quellen, die in Sparring-/Design-Gesprächen zu diesem Repo
aufgetaucht sind — Anker für spätere Überlegungen, kein Anspruch auf
Vollständigkeit. Kurze Kernaussage statt Volltext-Zitat; bei Bedarf selbst
nachlesen.

## 2026-07-28 — Meta: wie macht man Agent-Scaffolding/Prompts heute üblicherweise

Anlass: Vergleich dieses Repos (`dev-loop/`, `prompts/`) gegen aktuelle
Industriepraxis, im Zuge der Einführung von Micro-Batches und
`prompts/dev/sparring.md`.

- [GitHub Spec-Kit](https://github.blog/ai-and-ml/generative-ai/spec-driven-development-with-ai-get-started-with-a-new-open-source-toolkit/)
  — offene Referenzimplementierung für Spec-driven Development
  (Spec → Plan → Tasks → Code), direkteste Entsprechung zu
  `dev-loop/task-loop/`.
- [12-Factor Agents](https://agentic-design.ai/patterns/evaluation-monitoring/twelve-factor-agent)
  — pragmatische Prinzipien-Liste (Zustand persistieren, Prompts
  versionieren, Kontext selbst managen); deckt sich stark mit dem, was
  hier schon gemacht wird.
- [AGENTS.md-Spec](https://agents.md/) — Standard für ein Root-Level
  „README für Agenten" (Build/Test-Commands, Konventionen, Ort tieferer
  Doku). Übernommen, siehe `AGENTS.md` in diesem Repo.
- [Anthropic — Building Effective Agents](https://www.anthropic.com/research/building-effective-agents)
  — fünf Grundmuster (Prompt Chaining, Routing, Parallelization,
  Orchestrator-Workers, Evaluator-Optimizer). `dev-loop/task-loop/` ist
  im Kern Orchestrator-Workers.
- [Anthropic — When to use multi-agent systems](https://claude.com/blog/building-multi-agent-systems-when-and-how-to-use-them)
  — warnt vor **rollen-basierter** Zerlegung (Planner/Implementer/Tester)
  als Hauptquelle für Koordinations-Overhead; empfiehlt Zerlegung nach
  Kontext-Grenzen statt nach Aufgaben-Typ. Betrifft `dev-loop/task-loop/`
  direkt (Planer/Coder/Auditer) — dort aber durch vollständige
  Datei-Artefakte statt Konversationskontext-Übergabe abgefedert.
- [awesome-harness-engineering](https://github.com/ai-boost/awesome-harness-engineering)
  — kuratierte Sammel-Liste für weitere Recherche.
