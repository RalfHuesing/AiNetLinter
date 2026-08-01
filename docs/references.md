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
  `dev-loop/drift-loop/`.
- [12-Factor Agents](https://agentic-design.ai/patterns/evaluation-monitoring/twelve-factor-agent)
  — pragmatische Prinzipien-Liste (Zustand persistieren, Prompts
  versionieren, Kontext selbst managen); deckt sich stark mit dem, was
  hier schon gemacht wird.
- [AGENTS.md-Spec](https://agents.md/) — Standard für ein Root-Level
  „README für Agenten" (Build/Test-Commands, Konventionen, Ort tieferer
  Doku). Übernommen, siehe `AGENTS.md` in diesem Repo.
- [Anthropic — Building Effective Agents](https://www.anthropic.com/research/building-effective-agents)
  — fünf Grundmuster (Prompt Chaining, Routing, Parallelization,
  Orchestrator-Workers, Evaluator-Optimizer). `dev-loop/drift-loop/` ist
  im Kern Orchestrator-Workers.
- [Anthropic — When to use multi-agent systems](https://claude.com/blog/building-multi-agent-systems-when-and-how-to-use-them)
  — warnt vor **rollen-basierter** Zerlegung (Planner/Implementer/Tester)
  als Hauptquelle für Koordinations-Overhead; empfiehlt Zerlegung nach
  Kontext-Grenzen statt nach Aufgaben-Typ. Betrifft `dev-loop/drift-loop/`
  direkt (Planer/Coder/Kritiker) — dort aber durch vollständige
  Datei-Artefakte statt Konversationskontext-Übergabe abgefedert.
- [awesome-harness-engineering](https://github.com/ai-boost/awesome-harness-engineering)
  — kuratierte Sammel-Liste für weitere Recherche.

## 2026-08-01 — Laufzeit-generierte Rollen vs. feste Verfahren (`dynamic-loop`, `asimov-loop`)

Anlass: Nach `dynamic-loop/` und `asimov-loop/` (Rollen/Prompts entstehen
zur Laufzeit statt vorab, inspiriert von
[Claude-of-Duty](https://github.com/mshumer/Claude-of-Duty)) die Frage, ob
das sinnvolle Token-Ersparnis ist oder nur eine andere Form von
Verschwendung — im Vergleich zu `drift-loop/` (feste Rollen/Skills) und der
Sorge vor riesigen vorgefertigten Skill-Bibliotheken. Für Sparring in
`prompts/dev/sparring.md`.

**Ursprungsquelle noch mal genauer angesehen:**

- [Claude-of-Duty](https://github.com/mshumer/Claude-of-Duty) — der
  eigentliche Bauplan ist nicht „Agent schreibt sich seine Prompts selbst",
  sondern [`ARCHITECTURE.md`](https://github.com/mshumer/Claude-of-Duty/blob/main/ARCHITECTURE.md)
  als festes Vertragsdokument (Ordner-Eigentümerschaft, Event-Vokabular,
  gemeinsame Typen), gegen das alle Subagenten arbeiten. Näher an
  `drift-loop` (feste Artefakte, JIT-Plan) als an `asimov-loop` (kein
  Verfahren) — der Ursprungsimpuls trägt die radikale Lesart nur bedingt.

**Vorbilder für „Rollen/Struktur entstehen zur Laufzeit":**

- [Voyager](https://arxiv.org/abs/2305.16291) — Minecraft-Agent mit
  wachsender Skill-Bibliothek: jeder Skill ist verifizierter, ausgeführter
  Code, gespeichert mit Embedding, bei neuen Aufgaben semantisch
  abgerufen. Skills entstehen zur Laufzeit, werden aber geprüft und
  dauerhaft wiederverwendet — kein Wegwerf-Prompt pro Task.
- [AutoAgents](https://arxiv.org/abs/2309.17288) — generiert Rollen
  dynamisch aus der Aufgabe statt aus vordefinierten Rollen, plus eigene
  Observer-Rolle, die Plan und Rollen im Lauf reflektiert. Strukturell der
  akademische Zwilling von `dynamic-loop`.
- [Self-Discover](https://arxiv.org/abs/2402.03620) — Modell komponiert
  sich seine Reasoning-Struktur pro Aufgabe selbst aus atomaren Bausteinen,
  statt eine feste Prozedur zu befolgen; 10-40x weniger Inferenz-Aufwand
  als Self-Consistency. Konzeptionelle Blaupause für „Teil B: benannte
  Gefahren, kein Lösungsweg" in `dynamic-loop/kernel.md`.

**Empirische Grenzen dieser Idee:**

- [The Meta-Agent Challenge](https://arxiv.org/abs/2606.04455) — Modelle
  sollen selbst Agenten für neue Domänen bauen. Ergebnis: nur 5 von 39
  Konfigurationen erreichen menschliches Baseline-Niveau (davon 4
  proprietäre Frontier-Modelle), ein Drittel zeigt starke Varianz,
  teils Reward-Hacking unter Optimierungsdruck. Direkte Warnung an
  `dynamic-loop`/`asimov-loop`: „Modell entwirft sich seine Rollen/sein
  Vorgehen selbst" ist laut aktueller Forschung noch nicht zuverlässig,
  selbst bei Frontier-Modellen.
- [MetaGPT](https://arxiv.org/abs/2308.00352) — der etablierte
  Gegenentwurf: feste Rollen (Product Manager, Architect, Engineer, QA)
  plus SOP-Artefakte (PRD, Design-Doc) als strikte Schnittstellen
  zwischen Rollen — im Kern das, was `drift-loop` schon macht. Die
  Autoren nennen dynamische Rollen-Anpassung explizit als *zukünftige*
  Richtung, nicht als etwas, das heute schon zuverlässig funktioniert.
- [Anthropic — When to use multi-agent systems](https://claude.com/blog/building-multi-agent-systems-when-and-how-to-use-them)
  (bereits oben notiert) plus Faustregel aus der Praxis-Literatur:
  Multi-Agent-Setups kosten typischerweise 3-8x mehr Token als ein
  einzelner gut ausgestatteter Agent — nur zahlen, wenn ein messbarer
  Qualitäts- oder Geschwindigkeitsgewinn dagegensteht.

**Minimal-Regeln statt Verfahren (Vorbilder/Gegenstücke zu `asimov-loop`):**

- [Claude's Constitution](https://www.aigl.blog/claudes-constitution/)
  (Anthropic, 2026) — harte, nicht verhandelbare Constraints vs. eine
  vierstufige Prioritäts-Ordnung für alles andere (Safety → Ethik →
  Anthropic-Guidelines → Hilfsbereitschaft), statt einer langen
  Einzelregel-Liste. Strukturell fast deckungsgleich mit `kernel.md`
  Teil A (hart) / Teil B (benannt, kein Lösungsweg).
- [Anthropic cuts 80% of Claude Code's system prompt](https://www.developersdigest.tech/blog/claude-5-context-engineering-rules-hn-analysis)
  (Juli 2026, zu Claude Opus 5/Fable 5) — von ~800 auf 164 Token, „ohne
  messbaren Verlust" in den Coding-Evals. Begründung: „rules become
  judgment", „upfront context becomes progressive disclosure" — je
  fähiger das Modell, desto weniger explizite Verfahrens-Vorgabe nötig.
  Konkreter, aktueller Beleg für die Intuition „was heute nötig ist, ist
  morgen Ballast" — aber Vorsicht: das ist ein einzelnes, sehr fähiges
  Modell in einer Session, keine Multi-Agent-Rollenverteilung.
- [The Bitter Lesson](https://www.alphanome.ai/post/the-bitter-lesson-why-simple-methods-often-outperform-complex-ones-in-ai)
  (Rich Sutton) — historisches Muster über 70 Jahre KI-Forschung: von
  Menschen vorstrukturiertes Wissen hilft kurzfristig, verliert aber
  gegen allgemeine Methoden, die das System selbst entscheiden lässt,
  sobald genug Rechenleistung/Modellfähigkeit da ist. Liefert den Rahmen
  für „die Grenze ist dynamisch, nicht fix" — aber kein Freibrief, sie
  schon heute überall zu ziehen.
- Kritik an Asimovs drei Gesetzen ([Zusammenfassung/Quellensammlung](https://www.astro.sunysb.edu/fwalter/AST389/Why%20the%20three%20laws%20of%20robotics%20do%20not%20work.pdf))
  — Kernproblem: abstrakte Begriffe („harm") sind interpretationsoffen,
  und Asimov selbst hat seine gesamte Fiktion darauf aufgebaut, an
  Randfällen zu zeigen, wie kleine Regelmengen scheitern. Für
  `asimov-loop/orchestrator.md`: der Name ist eine Anspielung auf ein
  Werk, dessen Pointe „diese Gesetze reichen gerade *nicht*" ist — ein
  Hinweis, an welchen Stellen von Teil A vage Begriffe („was sinnvoll
  ist", „nachvollziehbare Kriterien") noch genauer gefasst werden
  könnten.

**Gegen „zu viele Skills" (Sorge zu `drift-loop/skills/` bzw. große
Skill-Sammlungen generell):**

- [MCP Tool Overload](https://dev.to/thedailyagent/mcp-tool-overload-why-more-tools-make-your-agent-worse-5a49)
  und verwandte Quellen (u. a. [EasyTool](https://arxiv.org/pdf/2401.06201))
  — Tool-Auswahl-Genauigkeit sinkt mit der Zahl gleichzeitig **im Kontext
  stehender** Tools spürbar ab ~10-15, bei 100+ gleichzeitig sichtbaren
  Tools nahe Zufallsniveau (~13 % laut zitierter Quelle).
- [Claude Agent Skills — Progressive Disclosure](https://platform.claude.com/docs/en/agents-and-tools/agent-skills/overview)
  (Anthropic Docs) — Skills laden standardmäßig nur Name+Beschreibung
  (wenige Dutzend Token), Volltext erst bei Bedarf. Die Zahl
  *vorhandener* Skills ist damit nicht automatisch mit Kontextkosten
  gleichzusetzen, sofern die Implementierung das wirklich so macht — die
  eigentlichen Kosten entstehen eher bei Fehlselektion (falsches Skill
  greift) oder wenn zu viele Kandidaten um Aufmerksamkeit in der
  Kurzliste konkurrieren. Relativiert die pauschale „2k Skills sind
  Verschwendung"-These: es ist wahrscheinlicher ein
  Auffindungs-/Selektionsproblem als ein reines Zählproblem — bleibt aber
  ungeprüft, ob `drift-loop/skills/` (aktuell 3 Skills) davon überhaupt
  betroffen wäre.
