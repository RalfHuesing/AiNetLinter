---
task: drift-audit-ideen
type: ideen-sammlung
status: draft
created: 2026-08-11
purpose: Grobe, bewusst unausgereifte Ideensammlung für ein noch nicht spezifiziertes Feature — Unterstützung bei Naming-Drift- und DRY-Verstoß-Erkennung im autonomen agentischen Entwicklungsworkflow. Kein fertiger Plan, kein Score, keine Akzeptanzkriterien — dafür fehlt noch die Ausarbeitung.
references:
  - 05-roadmap.md
  - 06-nicht-umsetzen.md
---

# Drift-Audit — offene Ideensammlung (Naming-Drift, DRY, evtl. weitere Fälle)

> **Status:** Offener Punkt, priorisiert direkt nach M8 (`--eval`/`--map`-Bereinigung) in
> [`05-roadmap.md`](05-roadmap.md) §2/§4. Noch NICHT spezifiziert — dieses Dokument sammelt
> Ideen, damit das Thema nicht verloren geht, bis es ausgearbeitet wird.

## Problem (Nutzer-Beschreibung 2026-08-11)

Beim autonomen agentischen Entwickeln entstehen "typische" Drift-Probleme über die Zeit einer
Session/eines Projekts hinweg, die sich nicht in einem einzelnen Lint-Regelverstoß fassen lassen,
weil sie erst im Vergleich über Zeit/Kontext hinweg sichtbar werden — nicht an einer einzelnen
Stelle isoliert:

- **Naming-Drift:** Etwas heißt zu Anfang z. B. "A", driftet über mehrere Iterationen/
  Refactorings zu "A23456" (Nutzer-Beispiel, abstrahiert) — derselbe Begriff bekommt über die
  Zeit inkonsistente Namen, ohne dass es an einer einzelnen Stelle als Fehler auffällt.
- **DRY-Verstöße:** Code-Duplikation, die entsteht, weil eine bereits existierende Lösung nicht
  wiedergefunden/wiederverwendet wird. Konkretes Beispiel aus der Dogfooding-Session
  2026-08-10/11: die `JsonSerializerOptions`-Duplikation, die vor S1.3 in mehreren MCP-Tools
  einzeln instanziiert war, bevor sie zu `McpJsonOptions.Default` zentralisiert wurde (siehe
  Doc-Kommentar in `src/AiNetLinter/Mcp/McpJsonOptions.cs`).

Beide Probleme sind schwer über einzelne, punktuelle Lint-Regeln zu fangen — sie brauchen einen
Blick über die ganze Codebase (DRY) bzw. über Zeit/Historie (Naming-Drift), eher einen
Audit-Vorgang als eine Einzeldatei-Regel.

## Bezug zu bereits gestrichenem Feature

Interessant: der mit M8 gestrichene `--eval naming-drift`-Typ (Epic 31, 2026-08-03) war ein
FRÜHERER Versuch, genau das Naming-Drift-Problem zu lösen — über einen statischen
Vocabulary-Map-Diff gegen eine Spec-Datei, ausgegeben als Copy-Paste-Prompt für eine externe
LLM-Session. Der Ansatz selbst wird gestrichen (überholt vom MCP-Live-Tool-Ansatz, siehe M8), aber
das zugrunde liegende PROBLEM bleibt bestehen — es sollte hier neu gedacht werden, diesmal als
MCP-native, interaktive Tools statt als statischer Einmal-Prompt für eine andere Session.

## Eigene Ideen (Claude, 2026-08-11) — unausgereift, zur Diskussion

### Idee A: Duplicate-Code-Detection via Roslyn-AST-Vergleich (DRY)

Strukturelle Duplikat-Erkennung (normalisierte Syntaxbaum-Vergleiche, ähnlich PMD CPD / Simian /
ReSharpers Duplicate-Finder) ist deterministisch, Roslyn-basiert und passt sehr gut zur
bestehenden Positionierung (§0 der Roadmap: "Roslyns strukturelle Präzision dort, wo Konkurrenz
nur textuelle Heuristiken hat"). Denkbar als:
- Neuer Linter-Checker (`DuplicateCodeChecker`, konfigurierbar über `rules.json` wie jede andere
  Regel) UND/ODER
- Neues MCP-Tool `find_duplicates`/`duplicate_scan` für gezielte On-Demand-Abfragen ("finde
  Duplikate zu dieser Methode / in diesem Verzeichnis")

Das ist der konkreteste, technisch am wenigsten unsichere Teil dieser Ideensammlung — DRY-Verstöße
sind strukturell fassbar, kein Embeddings/RAG nötig.

### Idee B: Naming-Familien-Erkennung via String-Ähnlichkeit (Naming-Drift, "lexikalische" Fälle)

Für Fälle wie "A" vs. "A23456" (gleicher Wortstamm, Suffix-/Versions-Drift) reicht klassische
String-Ähnlichkeit (Levenshtein-Distanz, gemeinsame Präfixe/Tokens) über die volle Liste der
Symbol-Namen aus dem Roslyn-Symbolgraphen — kein Embeddings/RAG nötig, deterministisch,
erklärbar. Denkbar als MCP-Tool `naming_families`/`similar_names`: liefert Cluster ähnlich
benannter Symbole, das LLM entscheidet dann, ob das gewollte Varianten oder echter Drift sind —
das Tool liefert nur Kandidaten, keine automatische Bewertung (menschliches/LLM-Urteil bleibt
nötig).

### Idee C: "Self-Audit"-Skill/Playbook statt neuem Tool

Ein Teil des Problems ist evtl. gar kein fehlendes Tool, sondern ein fehlender PROZESS: ein
wiederkehrender Audit-Durchlauf (z. B. am Ende eines Drift-Loop-Blocks), der das LLM aktiv
anleitet, mit den BEREITS VORHANDENEN Tools (`find_symbol` mit breiten Wildcard-Mustern,
`search_pattern`) gezielt nach Drift zu suchen — analog zu einer Checkliste/einem Skill statt
einem neuen MCP-Tool. Günstigster Ansatz (kein neuer Code), hängt aber komplett von
Disziplin/Konsequenz ab, ob es tatsächlich genutzt wird.

### Erwogen, aktuell nicht empfohlen: RAG/Vektor-Suche (z. B. Qdrant)

Nutzerfrage 2026-08-11: wäre semantische Suche/RAG (z. B. Qdrant) technisch machbar und sinnvoll,
auch für Naming-Drift?

**Technisch machbar: ja.** Aber für BEIDE konkreten Nutzer-Beispiele ist es vermutlich das
falsche Werkzeug:
- "A" vs. "A23456" ist ein **lexikalisches** Problem (gleicher Wortstamm) — String-Ähnlichkeit
  (Idee B) löst das deterministisch, günstiger und nachvollziehbarer als Embeddings.
- DRY/Code-Duplikation ist ein **strukturelles** Problem (identischer/fast identischer
  Syntaxbaum) — AST-Vergleich (Idee A) ist der Industriestandard dafür (PMD CPD, Simian,
  ReSharper), nicht Embeddings.

Embeddings/RAG wären dagegen das richtige Werkzeug für eine dritte, hier noch nicht konkret
belegte Kategorie: echte **semantische** Namens-Drift ohne jede lexikalische Überschneidung
(z. B. "OrderTotal" wird später zu "InvoiceAmount" umbenannt — komplett anderes Wort, gleiches
Konzept). Dafür gibt es aktuell kein Nutzer-Beispiel, nur die Vermutung, dass es vorkommen könnte.

Zusätzliche Bedenken gegen RAG/Qdrant, unabhängig vom konkreten Nutzen:
- Braucht ein Embedding-Modell — entweder lokal gehostet (Ressourcen-/Ops-Aufwand) oder per
  Cloud-API (widerspricht dem bestehenden Anti-Ziel "kein Modell-/Cloud-Abhängigkeit", siehe §0
  der Roadmap sowie D14/D15).
- Der Vektor-Index müsste zusätzlich zur bereits bestehenden Live-Refresh-Logik der
  Roslyn-Solution (`McpCodeGraphServerRefresh`, mtime+SHA-256-Staleness-Check pro Datei bei jedem
  Tool-Call) synchron gehalten werden — ein zweiter, eigener Staleness-Mechanismus, der bei jedem
  Datei-Edit erneut Embeddings berechnen müsste. Reale Zusatzkomplexität, nicht nur "einmal
  einrichten".
- Widerspricht der bereits getroffenen Entscheidung D15 (semantische Suche allgemein nicht
  bauen, siehe `06-nicht-umsetzen.md` §10).

**Empfehlung (vorläufig):** Erst Idee A + B ausarbeiten (beide deterministisch, ohne neue
Infrastruktur, passen zur bestehenden Architektur). RAG/Qdrant nur revisitieren, falls sich in
der Praxis zeigt, dass es echte semantische Drift-Fälle gibt, die weder String-Ähnlichkeit noch
AST-Vergleich fangen — bisher unbelegt (gleiche Beweislage wie bei D14/D15).

## Offene Fragen (noch nicht beantwortet, absichtlich)

- Wie wird "gewollte Namensvielfalt" (z. B. legitime Interface-/Implementierungs-Namenspaare wie
  `IGreeting`/`BaseGreeting`) von echtem Drift unterschieden? Vermutlich bleibt das LLM-Urteil
  nötig, das Tool liefert nur Kandidaten, keine automatische Entscheidung.
- Reicht ein On-Demand-Tool (Agent ruft es bei Bedarf auf) oder braucht es einen
  periodischen/erzwungenen Audit-Schritt im Drift-Loop (Skill-Integration, siehe Idee C)?
- Score/Aufwand/Priorität sind bewusst noch nicht geschätzt — das Feature ist noch nicht
  spezifiziert. Erst ausarbeiten, dann als richtiges Epic mit Score + Akzeptanzkriterien in
  [`05-roadmap.md`](05-roadmap.md) übernehmen.
