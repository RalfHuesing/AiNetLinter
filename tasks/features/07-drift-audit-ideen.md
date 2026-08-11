---
task: drift-audit-ideen
type: ideen-sammlung
status: draft
created: 2026-08-11
updated: 2026-08-11
purpose: Ausgearbeitete Ideensammlung für ein noch nicht spezifiziertes Feature — Unterstützung bei DRY- und Naming-Drift-Erkennung im autonomen agentischen Entwicklungsworkflow. DRY-Ideen (Schicht 1+2) sind priorisiert und methodisch detailliert ausgearbeitet; Naming-Drift (Schicht 3) bleibt Skizze bis DRY umgesetzt ist. Kein fertiger Plan, kein Score, keine Akzeptanzkriterien — dafür fehlt noch die endgültige Spezifikation.
references:
  - 05-roadmap.md (M9 — Drift-Audit, unspezifiziert, nach M8)
  - 06-nicht-umsetzen.md (D15 — keine semantische Suche via Embeddings)
  - 07-safeguard-determinismus-fix-review.md (Lehre: blockierte Reviews nicht überspringen)
---

# Drift-Audit — ausgearbeitete Ideensammlung (DRY priorisiert, Naming-Drift Skizze)

> **Status:** Offener Punkt, priorisiert direkt nach M8 (`--eval`/`--map`-Bereinigung) in
> [`05-roadmap.md`](05-roadmap.md) §2/§4. **DRY-Block (Ideen A–D) ist methodisch detailliert
> ausgearbeitet**, weil Ralf-Feedback 2026-08-11 "erstmal DRY angehen, was wären gute Methoden?"
> priorisiert. **Naming-Drift (Idee E) bleibt Skizze** und wird erst nach DRY-Umsetzung
> weiter ausgearbeitet — Idee E selbst löst das Problem noch nicht, sie zeigt nur die Richtung.
>
> **Reihenfolge der Umsetzung, falls M9 grünes Licht bekommt:** A → F (Skill-Hülle) → C → B → D → E.
> Begründung pro Schritt in den Ideen.

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

### Klassifikation der DRY-Probleme (Bellon-Taxonomie adaptiert)

Wir adoptieren die etablierte Clone-Type-Taxonomie aus Bellon et al. 2007 (s. Methoden-Grundlagen
unten), erweitern aber um die für den Agent-Workflow typischen Fälle:

| Fall | Beschreibung | Bellon-Type | Agent-Beispiel | Von Methode lösbar? |
|------|--------------|:-----------:|----------------|:-------------------:|
| **A.** Echter Klon | Copy-Paste, nur Whitespace/Kommentare anders | Type-1 | `JsonSerializerOptions` 4× | ✅ A, ✅ B |
| **B.** Umbenannter Klon | Syntaktisch identisch, Identifier/Literals anders | Type-2 | `CalculateTotal` vs. `BerechneSumme` | ✅ A, ✅ B |
| **C.** Modifizierter Klon | Statements hinzu/gelöscht/modifiziert | Type-3 | Helper inlined, leicht angepasst | ✅ A (mit Schwellwert), ✅ B |
| **D.** Pattern-Klon | Gleiche Struktur (z. B. Init-Sequenz), aber andere Konstruktoren | – (eigene Kategorie) | 5× `AddSingleton<IX>(new X(...))` mit gleicher Init-Logik | ❌ A, ❌ B, evtl. D (zurückgestellt) |
| **E.** Refactoring-Drift | Zentraler Helper existiert, einzelne Aufrufer duplizieren ihn aber inline | – (eigene Kategorie) | Früher `McpJsonOptions.Default`, inzwischen wieder 3× inline | ❌ A/B/D, ✅ C (Symbolgraph) |
| **F.** Semantischer Klon | Macht das gleiche, sieht ganz anders aus (foreach vs. LINQ) | Type-4 | – | ❌ alles (gewollt — D15) |

**Konsequenz für die Umsetzung:** Keine einzelne Methode fängt A–F komplett. Schicht 1 (Token-
oder AST-CPD) deckt A–C ab, deckt aber weder D noch E. Schicht 2 (Symbolgraph-basiert) fängt E.
Pattern-Detection-Cluster (D) ist eine eigenständige Familienerkennung und bleibt zurückgestellt.

## Bezug zu bereits gestrichenem Feature

Interessant: der mit M8 gestrichene `--eval naming-drift`-Typ (Epic 31, 2026-08-03) war ein
FRÜHERER Versuch, genau das Naming-Drift-Problem zu lösen — über einen statischen
Vocabulary-Map-Diff gegen eine Spec-Datei, ausgegeben als Copy-Paste-Prompt für eine externe
LLM-Session. Der Ansatz selbst wird gestrichen (überholt vom MCP-Live-Tool-Ansatz, siehe M8), aber
das zugrunde liegende PROBLEM bleibt bestehen — es sollte hier neu gedacht werden, diesmal als
MCP-native, interaktive Tools statt als statischer Einmal-Prompt für eine andere Session.

## Methodische Grundlagen (wissenschaftlich fundiert, nicht geraten)

Die Ideen A–D bauen auf der etablierten Code-Clone-Detection-Forschung auf. Quellen, auf die
wir uns stützen und die wir bei der Implementierung als Referenz heranziehen:

**Übersichtsarbeiten / Taxonomie:**
- **Roy & Cordy (2007):** "A Survey on Software Clone Detection". Queen's University TR
  2007-541. Die klassische Übersichtsarbeit, etabliert die Unterteilung in text-, token-,
  AST-, PDG- und metrik-basierte Verfahren.
- **Bellon, Koschke, Antoniol, Krinke, Merlo (2007):** "Comparison and Evaluation of Clone
  Detection Tools". IEEE TSE 33(9). Vergleichsstudie über 6 Tools, etabliert die
  Bellon-Taxonomie (Type-1 bis Type-3, "consistent changes" für Type-3), die wir oben adaptiert
  haben. Operationalisiert auch die "Manual Validation Procedure" für Ground-Truth-Erstellung —
  relevant, wenn wir später eine Test-Fixture bauen.
- **Roy, Cordy, Koschke (2014):** "Comparison and Evaluation of Clone Detection Techniques:
  Past, Present, and Future". Erweiterte Folgestudie zu Bellon 2007.
- **Koschke (2008):** "Survey of Research on Software Clones". In *Duplication, Redundancy,
  and Interoperability in Software*. Sammelband.

**Werkzeuge als Referenz-Implementierungen (jeweils mit ihrer Methode):**
- **CCFinder / CCFinderX** (Kamiya et al. 2002): Token-basiert, N-Gram-Index, parametrierbarer
  Threshold. Quasi-Standard für Token-CPD. Wir orientieren uns an dieser Methode.
- **PMD CPD** (Copy/Paste Detector): Token-basiert mit Normalisierung; pragmatisch, schnell,
  breit eingesetzt. "Gut-genug"-Referenz für unsere Mindestanforderung.
- **jscpd**: Token-basiert, Multi-Language, gestaffelte Schwellwert-Ausgabe. Referenz für
  unseren gestaffelten Ausgabe-Ansatz (s. Idee A §3.4).
- **NiCad** (Roy & Cordy 2008): Hybrid (Text + AST), konfigurierbarer Threshold, gut
  dokumentierte Precision-/Recall-Werte. Referenz für Idee B (AST-Erweiterung).
- **Deckard** (Jiang et al. 2007, ICSE): AST-basierte Vektorisierung + hierarchisches
  Clustering. Skaliert gut auf große Repos. Erwähnenswert, aber für unsere Größenordnung
  wahrscheinlich overkill.
- **CloneWorks** (Svajlenko et al. 2016, MSR): Vergleichsframework, misst konsistent über
  Tools hinweg. Relevant für unsere spätere Evaluation.
- **CloneDR** (Baxter et al. 1998): AST-basiert mit "isolate-compress-compare"-Pipeline.
  Industrieller Klassiker (semanticdesigns.com).

**Information-Retrieval-Grundlagen (für Jaccard / N-Gram / Inverted Index):**
- **Manning, Raghavan, Schütze (2008):** "Introduction to Information Retrieval", Cambridge
  University Press. Standardreferenz für Jaccard-Similarity, N-Gram-Shingling, Inverted Index.
- **Broder (1997/1998):** "On the resemblance and containment of documents" und "Identifying
  and filtering near-duplicate documents". Begründet das N-Gram-Shingling als
  Duplikat-Detection-Verfahren, das auch für fuzzy-Match robust ist.

**Symbolgraph / Refactoring-Drift (für Idee C):**
- Idee C nutzt den **bereits vorhandenen** Roslyn-Symbolgraphen und `find_references` — keine
  neue Forschung, aber ein pattern, das in der MSR-Literatur als "absence-of-calls"
  ("Method X is not called from places where it should be") in refactoring-Studien
  auftaucht, z. B. **Murphy-Hill (2005):** "How We Refactor, and How We Know It". IEEE TSE 31(8).
  Relevant für die spätere Evaluation von Idee C.
- Klassisch: **Chaikalis, Zaidman (2018):** "Practice-Based Recommendations on Composite
  Refactoring Tools". MSR 2018. Zeigt, dass die meisten Refactorings heute noch manuell gemacht
  werden — exakt der Punkt, an dem unser Tool ansetzt.

**Falsch-Positive-Strategie (Querschnitt, gilt für alle Ideen):**
- **Granularitäts-Wahl:** Method-Granularität statt File- oder Block-Granularität (weniger
  triviale Übereinstimmungen, siehe Bellon 2007 §3.2 zu Granularity-Trade-offs).
- **Min-Token-Filter:** Standard-Schwellwert 30 Tokens — verhindert, dass einzelne
  Konfig-Blöcke oder `using`-Folgen als "Klone" gezählt werden (Empirie: jscpd-Default
  ebenfalls 50, PMD CPD 100 Tokens; wir beginnen konservativ bei 30 und justieren empirisch).
- **Schwellwert-Staffelung:** Statt eines harten Schwellwerts gestaffelte Ausgabe
  (siehe Idee A §3.4). Reduziert False-Positives, weil Grenzfälle nicht in eine harte
  ja/nein-Entscheidung gezwungen werden.
- **Identifier-Normalisierung optional:** Type-2-Klone werden nur sichtbar, wenn Identifier
  auf Platzhalter normalisiert werden — als Konfig-Flag, nicht default (sonst werden
  `CalculateOrderTotal` und `CalculateInvoiceTotal` als "Klon" markiert, was semantisch
  falsch wäre).
- **Manuelle LLM-Bewertung am Ende:** Das Tool liefert Kandidaten, das LLM (oder der Mensch)
  entscheidet. Niemals Auto-Konsolidierung, das wäre `preview_refactor` (S3) und ist nicht
  M9-Scope.

## Idee A: Token-basiertes CPD auf Method-Granularität (Schicht 1, **priorisiert**)

### A.1 Ziel

Erkennung von Bellon-Type-1, Type-2 und konsistenten Type-3-Klonen auf der Ebene einzelner
Methoden/Bodies. Konkretes Ziel-Beispiel: 4× instanziierte `JsonSerializerOptions` über die
gesamte Solution hinweg → Tool zeigt 1 Kandidaten-Cluster mit den 4 Methoden, in denen die
Instanziierung stattfindet.

### A.2 Methode (im Detail)

Wir adoptieren den **CCFinder/Jaccard-N-Gram**-Ansatz, weil er das beste
Precision/Performance-Verhältnis für unsere Solution-Größenordnung hat
(10K–100K LOC, Build-Verzeichnis ausgeschlossen) und ohne weitere Infrastruktur
auskommt (kein Embedding-Modell, kein zusätzlicher Persist-Layer):

1. **Token-Extraktion** mit Roslyn: Pro Methode/Funktion `body.Statement.DescendantTokens()`
   liefert den Token-Stream. Identifier werden auf einen Normalisierungs-Modus konfigurierbar
   gemappt (default: unverändert; optional: alle Identifier auf `$ID$`, alle Literale auf
   `$LIT$` — schaltet Type-2-Erkennung an/aus).
2. **N-Gram-Shingling** (Broder 1997): Sliding-Window der Größe `k` (default `k=5` Tokens)
   über den Token-Stream, Whitespace und Kommentare werden vorab verworfen. Jedes N-Gram wird
   zu einem deterministischen Hash (z. B. SHA-256-Truncation) verrechnet — effizienter
   String-Compare.
3. **Inverted Index** (Manning 2008, Kap. 1): `Dictionary<Hash, List<MethodId>>`. Pro
   N-Gram-Hash merken wir uns, in welchen Methoden es vorkommt. Aufbau: O(N) für alle
   Methoden einer Solution, einmal pro Audit-Lauf.
4. **Kandidaten-Pair-Generierung:** Method-Paare, die mindestens `t` gemeinsame N-Gram-Hashes
   haben, werden zu Kandidaten. Empirischer Default für `t`: 3 gemeinsame N-Gramme (kann
   über `rules.json` justiert werden).
5. **Jaccard-Similarity** (Manning 2008, Kap. 3): Für jedes Kandidaten-Pair berechnen wir
   `|A ∩ B| / |A ∪ B|` über die N-Gram-Mengen. Das ist der endgültige Score.
6. **Schwellwert-Staffelung** statt hartem Cut (s. A.4).
7. **Ausgabe:** Sortiert nach Jaccard-Score absteigend, Top-`N` (default 20) — kein
   vollständiger Dump.

### A.3 Granularität & Scope (False-Positive-Disziplin)

- **Method/Funktion-Granularität** (kein Block, kein File): Bellon 2007 §3.2 zeigt, dass
  Method-Granularität das beste Precision/Recall-Verhältnis hat. Block-Granularität
  produziert 3–5× mehr False-Positives wegen trivialer Übereinstimmungen
  (Konfig-Blöcke, `using`-Folgen, einzelne `try`/`catch`-Gerüste).
- **Min-Token-Filter** (default 30, konfigurierbar): Verhindert, dass winzige Helper
  (3–5 Zeilen) als Klone markiert werden. Triviale Methoden sind in C# ohnehin oft
  identisch (leere `Dispose`, leere `ToString`-Override) und für DRY-Audits nicht
  relevant. Empirie: PMD CPD default 100 Tokens, jscpd 50 — wir starten konservativ bei
  30 für unsere kleinere Codebase.
- **Build-Artefakte ausgeschlossen:** `bin/`, `obj/`, `.ainetlinter/`, `tests/Fixtures/`
  (siehe Safeguard-Fix-Review 2026-08-06: Lehre — Fixture-Verzeichnisse mit
  absichtlichen Verstößen niemals in Drift-Audits einbeziehen). Filter auf
  `Project.Documents`-Ebene, nicht erst beim Vergleich.
- **Generated Code übersprungen** (`[GeneratedCode]`-Attribut): Reduziert
  False-Positives massiv bei Source-Generator-Outputs.

### A.4 Schwellwert-Staffelung (statt hartem Cut)

Wir adoptieren das jscpd-Pattern: **drei Ausgabe-Buckets** statt eines harten Schwellwerts.
Damit erzwingen wir keine binäre Entscheidung, sondern geben dem Agent/LLM gestaffelte Evidenz:

| Bucket | Jaccard-Score | Default-Threshold | Bedeutung | Default-Farbe |
|--------|---------------|-------------------|-----------|---------------|
| `exact` | `≥ 0.95` | `0.95` | Fast identisch, Konsolidierung dringend | rot |
| `near` | `0.80 – 0.95` | `0.80` | Sehr ähnlich, lohnt den Blick | gelb |
| `fuzzy` | `0.65 – 0.80` | `0.65` | Grenzwertig, evtl. Pattern-Klone (Idee D) | grau |

Unter `0.65` zeigen wir nichts — das wäre Signal-Rauschen, das den Agent-Context zumüllt.

**Begründung Staffelung:** Bellon 2007 zeigt, dass Clone-Detection-Tools systematisch zu
viele oder zu wenige Klone melden, weil sie einen harten Schwellwert erzwingen. Gestaffelte
Ausgabe ist die einzige Möglichkeit, **Precision hoch zu halten ohne Recall zu opfern** — der
LLM- oder Menschen-Beurteiler entscheidet anhand der Farbe, welche Stufe ernst genommen wird.

### A.5 Konfiguration (in `rules.json`)

Analog zu den bestehenden Checkern, mit gleichen Schlüssel-Wert-Konventionen:

```json
{
  "id": "DuplicateCode",
  "enabled": true,
  "params": {
    "minTokens": 30,
    "ngramSize": 5,
    "minSharedNgrams": 3,
    "thresholds": { "exact": 0.95, "near": 0.80, "fuzzy": 0.65 },
    "normalizeIdentifiers": false,
    "scope": { "include": ["src/**/*.cs"], "exclude": ["**/bin/**", "**/obj/**", "tests/Fixtures/**", "**/*Generated*.cs"] },
    "maxResults": 20
  }
}
```

### A.6 Tool-Exposition (zwei Wege parallel)

- **Linter-Checker** `DuplicateCodeChecker` (registriert in `LinterEngine` wie die anderen
  Checker): läuft im normalen Lint, meldet Verstöße in der gleichen
  Violation-Infrastruktur wie `BanAsyncVoid` etc. Score geht in `safeguard` ein.
- **MCP-Tool `find_duplicates`** mit Argumenten:
  - `minTokens` (default aus `rules.json`)
  - `similarityThreshold` (default `fuzzy`)
  - `scopeDir` (optional, default Solution-Root)
  - `topN` (default 20)
  - Ausgabe: Markdown-Liste mit Methoden-Signatur + Jaccard-Score + Cluster-Zugehörigkeit
    ("Methode A und B und C sind 0.91 ähnlich" als Cluster, nicht 3 isolierte Paare).

### A.7 Funktionsgarantie & False-Positive-Budget

- **Garantie:** Erkennt garantiert alle Type-1-Klone (identische Token-Streams) oberhalb
  `minTokens`. Type-2 nur, wenn `normalizeIdentifiers=true`. Type-3 abhängig von
  Jaccard-Score.
- **False-Positive-Budget:** Ziel < 10% der gemeldeten Cluster (Bellon 2007 hat für die
  besten Tools ~15% gemessen; wir sind kleiner und können besser sein). Validierung: manuell
  an einer Ground-Truth-Fixture mit 5 künstlichen Klonen + 5 künstlichen Nicht-Klonen
  (Bellon 2007 "Manual Validation Procedure", vereinfacht).
- **False-Negative-Budget:** Ziel < 20% verpasste Klone ≥ `minTokens`. Wird gemessen,
  indem wir den Safeguard-Score-Run vor S1.3 (`McpJsonOptions.Default`-Einführung) als
  historischen Test benutzen — vorher müssen die 4 Duplikate erkannt worden sein, wenn
  wir das Tool auf den damaligen Code anwenden.

### A.8 Aufwand-Schätzung (grobe Spanne)

- Inverted-Index-Implementierung: 1–2 Tage
- Roslyn-Token-Stream + N-Gram: 1 Tag
- Schwellwert-Staffelung + Cluster-Bildung: 1 Tag
- `rules.json`-Schema-Erweiterung + Linter-Checker-Registrierung: 0,5 Tag
- MCP-Tool `find_duplicates` (mit Argumenten + Markdown-Output): 1 Tag
- Tests (Unit + Integration inkl. Ground-Truth-Fixture): 1–2 Tage
- Doku (`Docs/configuration.md`, `Docs/agent-api.md`): 0,5 Tag
- **Gesamt: 5–7 Tage** (1 Woche realistisch)

### A.9 Reihenfolge in der Umsetzung

**Idee A wird zuerst gebaut**, weil:
1. DRY hat ein konkretes Nutzer-Beispiel (`JsonSerializerOptions`-Duplikation) — Idee E
   (Naming-Drift) hat nur ein hypothetisches Beispiel.
2. Token-CPD ist mit Abstand am besten verstanden, am günstigsten, hat die stärkste
   Werkzeug-Referenz (CCFinder, jscpd, PMD CPD) — niedrigstes Risiko.
3. Idee F (Self-Audit-Skill) braucht Idee A, weil der Skill sonst ins Leere greift.

## Idee B: AST-basiertes CPD als Schicht-1.5 (optional, später)

### B.1 Wann sinnvoll

Erst wenn sich in der Praxis nach Idee A zeigt, dass Token-CPD zu viele Type-2-Klone
verpasst (Identifier-nicht-normalisiert-Fall) oder dass der gestaffelte Threshold zu
viele Grenzfälle im Bucket `fuzzy` sammelt. Konkreter Auslöser: zwei- oder dreimal pro
Quartal meldet der Agent "Tool zeigt zu wenig, ich weiß aber, dass es Klone gibt."

### B.2 Methode (Skizze, keine Ausarbeitung hier)

NiCad-Hybrid (Roy & Cordy 2008) oder Deckard-ähnlich (Jiang 2007): Syntaxbaum in
kanonische Form bringen (Whitespace, Kommentare raus; Identifier-Hashing), Subtrees
extrahiren, dann Baum-Distanz statt Jaccard. **Bauen wir nicht ohne Empirie aus Idee
A**, weil teurer und komplexer.

### B.3 Warum nicht von vornherein

Token-CPD ist 2–3× schneller, einfacher zu konfigurieren, robuster gegen
Refactoring-Tools (die Identifier umbenennen), und reicht für 80% der Fälle. AST-CPD
ist ein **optionales Tuning** der Schicht 1, kein Ersatz.

## Idee C: Refactoring-Drift-Detection via Symbolgraph (Schicht 2, **nach A**)

### C.1 Ziel — der "interessante" Fall

Erkennung, ob ein **bereits existierender Helper** (`McpJsonOptions.Default`,
`JsonNamingPolicy.CamelCase`, etc.) von Einzelschritten "ausgewickelt" und inline
dupliziert wurde. Das ist der Fall, den Token-CPD nicht fängt, der aber im Agent-Loop
systematisch entsteht, wenn der Agent einen Helper nicht kennt und das Pattern inlined.

### C.2 Konzept

Wir nutzen den **bereits vorhandenen Roslyn-Symbolgraphen** (`find_symbol`,
`find_references`) und kombinieren ihn mit der Schicht-1-Maschine:

1. **Input:** Ein Helper-Symbol `H` (vom User vorgegeben oder per `find_symbol` ermittelt).
2. **Aufrufer-Liste** `Callers(H)` über `find_references`: billig, schon implementiert.
3. **Candidate-Set** = alle Methoden `M` in der Solution mit `M ∉ Callers(H)`, die
   eine **ähnliche Struktur wie `H`'s Body** haben. "Ähnlich" = Jaccard-Score aus Idee A
   zwischen `H.Body` und `M.Body` ≥ `near`-Schwellwert.
4. **Filter:** Aus den Kandidaten werden die mit `M.Body` enthält *Symbole vom selben Typ
   wie `H`'s Returns/Writes* herausgefiltert — sonst finden wir generische Pattern-Klone,
   nicht refactoring-spezifische.
5. **Output:** "Methode `M` (Datei:Zeile) ist `Jaccard=0.91` ähnlich zu Helper `H`, ruft
   `H` aber NICHT auf. Möglicher Refactoring-Drift-Kandidat."

### C.3 Forschungs-Anker

Das ist im Kern das, was in der MSR-Literatur als **"absence-of-calls"**-Heuristik
diskutiert wird (Murphy-Hill 2005 "How We Refactor, and How We Know It", IEEE TSE 31(8);
Chaikalis & Zaidman 2018 "Practice-Based Recommendations on Composite Refactoring Tools",
MSR 2018). Akademisch nicht als fertiges Tool ausgearbeitet, aber die Idee ist etabliert:
wenn ein Helper existiert, aber Code, der ihn nutzen sollte, ihn nicht nutzt, ist das
ein Refactoring-Smell.

### C.4 Implementierungs-Aufwand

Aufbauend auf Idee A: 2–3 Tage. Keine neue Datenstruktur nötig — wir nutzen den
bestehenden N-Gram-Index und die bestehende Symbol-API. Neu ist nur die
"Vergleich-mit-Helper-welcher-nicht-aufgerufen-wird"-Pipeline.

### C.5 Funktionsgarantie

- **Garantie:** Findet alle Methoden, deren Token-Stream dem eines gegebenen Helpers
  ähnelt (≥ Jaccard-Score) und die den Helper nicht aufrufen.
- **False-Positive-Budget:** Höher als bei Idee A (Ziel < 25%), weil strukturelle
  Ähnlichkeit nicht zwingend Refactoring-Drift bedeutet — könnte auch legitimer
  gleichförmiger Code sein (`Dispose` in 50 Klassen, alle ähnlich zu `Object.Dispose`).
  Mitigation: explizit als "Kandidaten" labeln, nicht als "Verstöße", und den
  Agent entscheiden lassen.
- **False-Negative-Budget:** Schwer zu messen, weil wir keine Ground-Truth für
  Refactoring-Drift haben. Akzeptanz: "wir finden die groben Fälle, die feinen
  müssen manuell bleiben."

### C.6 Reihenfolge

**Idee C wird nach Idee A gebaut**, weil:
1. C setzt A voraus (N-Gram-Index, Jaccard-Score).
2. Das Dogfooding-Beispiel (`McpJsonOptions.Default`) ist genau ein A-C-Hybrid:
   zuerst müssen wir CPD-Klone sehen (A), dann können wir die Frage stellen
   "ist das ein Helper, der nicht aufgerufen wird" (C).

## Idee D: Pattern-Cluster-Detection (DRY-Type, **zurückgestellt**)

### D.1 Was sie lösen würde

Pattern-Klone (Fall D in unserer Tabelle oben): nicht der gleiche Code, aber dasselbe
Strukturmuster. Konzeptuell näher an `pattern_detect` (S1) als an CPD.

### D.2 Warum zurückgestellt

- **Kein konkretes Beispiel aus der Praxis** (anders als `JsonSerializerOptions`-Duplikation
  für Idee A). Solange wir keinen belegten Anwendungsfall haben, ist das
  Spekulations-Engineering.
- **Konzeptionell anderes Tool**, eher eine Erweiterung von `pattern_detect` als von
  `DuplicateCodeChecker` — gehört sauberer zu S1 als zu M9.
- **Aufwand unklar**: Pattern-Cluster brauchen entweder AST-Pattern-Mining (teuer) oder
  einen kuratierten Pattern-Katalog (Wartungsaufwand) oder Embeddings (D15 widerspricht).

### D.3 Revival-Bedingung

Wenn nach Ideen A + C in der Praxis 2+ konkrete Beispiele von Pattern-Klonen auftauchen,
die weder Token-CPD noch Symbolgraph fangen — dann mit frischem Spec-Doc wieder
aufnehmen, vermutlich als eigenständiges Epic neben `pattern_detect` (S1) statt unter M9.

## Idee E: Naming-Familien-Erkennung via String-Ähnlichkeit (Naming-Drift, **Skizze**)

> **Status:** Bewusst als Skizze belassen, weil Ralf-Feedback 2026-08-11 "erstmal DRY
> angehen" priorisiert. Erst nach DRY-Umsetzung (Ideen A–D) weiter ausarbeiten.

### E.1 Idee (unverändert aus Vor-Iteration)

Für Fälle wie "A" vs. "A23456" (gleicher Wortstamm, Suffix-/Versions-Drift) reicht
klassische String-Ähnlichkeit (Levenshtein-Distanz, gemeinsame Präfixe/Tokens) über
die volle Liste der Symbol-Namen aus dem Roslyn-Symbolgraphen — kein Embeddings/RAG
nötig, deterministisch, erklärbar. Denkbar als MCP-Tool `naming_families` /
`similar_names`: liefert Cluster ähnlich benannter Symbole, das LLM entscheidet dann,
ob das gewollte Varianten oder echter Drift sind — das Tool liefert nur Kandidaten,
keine automatische Bewertung (menschliches/LLM-Urteil bleibt nötig).

### E.2 Forschungs-Anker

- **Bani-Fatideh, Roy, Chiang (2015):** "Naming Practices in Java". MSR 2015. Empirische
  Studie zu Naming-Konventionen und Drift-Phänomenen. Relevant für die spätere
  Ausarbeitung, weil sie zeigt, dass konsistente Identifier-Namen eines der
  stärksten Signale für Code-Qualität sind.
- **Anquetil, Lethbridge (2003):** "Comparative Study of Naming Conventions". Working
  Conference on Reverse Engineering. Hintergrund, falls wir später Konventionen
  prüfen wollen.

### E.3 Warum nicht sofort

DRY ist dringender, weil:
- Es ein konkretes Nutzer-Beispiel gibt (`JsonSerializerOptions`).
- Es eine etablierte wissenschaftliche Methode gibt (CPD-Forschung seit 1990ern).
- Es mit den vorhandenen Roslyn-APIs und dem existierenden Linter-Framework trivial
  integrierbar ist (Method-Granularität ist Roslyn natürlich).

Naming-Drift hat:
- Kein konkretes Beispiel (das "A23456"-Beispiel ist hypothetisch).
- Eine schwächere Methoden-Basis (String-Ähnlichkeit ist trivial, aber die Frage "ist
  das Drift oder legitime Variante" bleibt).
- Mehr Überschneidung mit `pattern_detect` (S1).

→ Erst DRY, dann Naming, in dieser Reihenfolge. Idee E wird nach Idee A ausgearbeitet.

## Idee F: Self-Audit-Skill/Playbook als Hülle (Skill, **zusammen mit A**)

### F.1 Konzept

Unabhängig davon, ob die Ideen A–C umgesetzt sind: ein **wiederkehrender Audit-Durchlauf**
als Skill, der das LLM aktiv anleitet, mit den vorhandenen Tools gezielt nach DRY/Drift
zu suchen. Konkret:

- Skill-Datei `.agents/skills/drift-audit/SKILL.md` mit:
  1. Schritt 1: `find_duplicates` (oder `get_violations --scope=DRY`) über `src/` mit
     `minTokens=20`.
  2. Schritt 2: Pro `exact`-Cluster entscheiden: Konsolidierung jetzt (dieser Loop) oder
     Tech-Debt-Eintrag (später)?
  3. Schritt 3: Pro `near`-Cluster: manueller LLM-Blick auf 1–2 Beispiele, dann
     Entscheidung.
  4. Schritt 4: Wenn ein Helper in den vorherigen Schritten auffiel → optional
     `find_references --symbol=H` und mit Idee-C-Logik nach Aufrufer-Lücken suchen.
- **Cadence:** Vor Epic-Abschluss, nach großen Refactorings, optional als Check im
  Kritiker-Workflow.

### F.2 Warum das auch ohne Idee A Sinn ergibt

Selbst wenn wir kein neues Tool bauen, kann der Skill mit **ausschließlich vorhandenen
Tools** (`find_symbol` mit Wildcards, `search_pattern` mit Regex) eine simple
DRY-Erkennung leisten — billiger als jede Tool-Implementierung, aber abhängig von
LLM-Disziplin. Genau dieser Punkt ist Idee F: Hülle, die die Wahrscheinlichkeit
erhöht, dass das passiert, was sonst nur passiert, wenn der Agent daran denkt.

### F.3 Forschungs-Anker

Im Kern "**Checklisten-Scaffolding für Experten-Diagnose**" — Pattern aus der
kognitiven Ergonomie (Klein NDM, "Naturalistic Decision Making", 2008; Reason Swiss-Cheese-Model)
und der Software-Engineering-Praxis (Hertzum, "Expertise-Centered Design", 2010). Nicht
originell, aber billig und wirksam.

### F.4 Aufwand

1 Tag (Skill-Datei schreiben, in `.agents/`-Scaffolding registrieren, einmal im
Drift-Loop dry-runnen). Sollte **gleichzeitig mit Idee A** ausgeliefert werden, nicht
nachträglich.

## Erwogen, aktuell nicht empfohlen: RAG/Vektor-Suche (z. B. Qdrant)

Nutzerfrage 2026-08-11: wäre semantische Suche/RAG (z. B. Qdrant) technisch machbar und
sinnvoll, auch für Naming-Drift?

**Technisch machbar: ja.** Aber für ALLE hier spezifizierten Fälle ist es das falsche
Werkzeug:

- **Type-1/2/3-Klone (Ideen A, B):** strukturelles Problem — AST-/Token-Vergleich löst
  das deterministisch, günstiger und nachvollziehbarer als Embeddings (Roy & Cordy 2007).
- **Refactoring-Drift (Idee C):** strukturelles + symbolgraph-basiertes Problem — kein
  Embedding nötig, der Symbolgraph weiß, wer wen aufruft.
- **Pattern-Klone (Idee D):** semantisches Problem, aber **D15 verbietet semantische
  Suche explizit**, und Pattern-Cluster brauchen eher AST-Mining als Embeddings.
- **Echter semantischer Klon (Type-4):** würde Embeddings erfordern, aber ist explizit
  out of scope (D15, Revival-Bedingung: keine).
- **Naming-Drift mit komplett verschiedenen Wörtern** (z. B. `OrderTotal` → `InvoiceAmount`):
  semantisches Problem, aber kein konkretes Nutzer-Beispiel; String-Ähnlichkeit (Idee E)
  deckt die Fälle mit Wortstamm-Überschneidung ab, und der semantische Rest ist
  seltener als vermutet (Bani-Fatideh 2015 zeigt, dass Identifier meist aus dem
  Domain-Vokabular stammen — semantische Umbenennungen sind selten, weil sie
  Verständlichkeit kosten).

Zusätzliche Bedenken gegen RAG/Qdrant, unabhängig vom konkreten Nutzen:
- Braucht ein Embedding-Modell — entweder lokal gehostet (Ressourcen-/Ops-Aufwand) oder
  per Cloud-API (widerspricht dem bestehenden Anti-Ziel "kein Modell-/Cloud-Abhängigkeit",
  siehe §0 der Roadmap sowie D14/D15).
- Der Vektor-Index müsste zusätzlich zur bereits bestehenden Live-Refresh-Logik der
  Roslyn-Solution (`McpCodeGraphServerRefresh`, mtime+SHA-256-Staleness-Check pro Datei
  bei jedem Tool-Call) synchron gehalten werden — ein zweiter, eigener
  Staleness-Mechanismus, der bei jedem Datei-Edit erneut Embeddings berechnen müsste.
  Reale Zusatzkomplexität, nicht nur "einmal einrichten".
- Widerspricht der bereits getroffenen Entscheidung D15 (semantische Suche allgemein
  nicht bauen, siehe `06-nicht-umsetzen.md` §10).

**Empfehlung (beibehalten):** Erst Ideen A + C ausarbeiten und umsetzen (beide
deterministisch, ohne neue Infrastruktur, passen zur bestehenden Architektur).
RAG/Qdrant nur revisitieren, falls sich in der Praxis zeigt, dass es echte semantische
Drift-Fälle gibt, die weder Token- noch AST-Vergleich noch Symbolgraph fangen — bisher
unbelegt (gleiche Beweislage wie bei D14/D15).

## Vorgeschlagene Umsetzungs-Reihenfolge (wenn M9 grünes Licht bekommt)

| # | Idee | Was | Aufwand | Voraussetzung |
|:-:|------|-----|--------:|---------------|
| 1 | **A** | Token-CPD Schicht 1 (DuplicateCodeChecker + `find_duplicates`) | 5–7 Tage | keine |
| 2 | **F** | Self-Audit-Skill als Hülle | 1 Tag | A (für die Tool-Aufrufe) |
| 3 | **C** | Refactoring-Drift-Detection Schicht 2 | 2–3 Tage | A (N-Gram-Index wiederverwenden) |
| 4 | **B** | AST-CPD Schicht 1.5 (nur bei Empirie-Bedarf) | 5–7 Tage | A, **+ Empirie-Beweis aus 1–3** |
| 5 | **D** | Pattern-Cluster-Detection (nur bei Empirie-Bedarf) | TBD | A+C, **+ 2+ konkrete Beispiele** |
| 6 | **E** | Naming-Familien-Erkennung | TBD | A+C, dann neu spezifizieren |

**Frühester realistischer Lieferpfad:** 8–11 Tage für A + F + C (1 Schritt 1 + Skill +
Schritt 2).

## Offene Fragen (noch nicht beantwortet, absichtlich)

- **Threshold-Defaults empirisch validieren:** Die Schwellwerte 0.95/0.80/0.65 sind
  CCFinder-/jscpd-üblich, aber für unsere Codebase-Größe und unseren LLM-Konsumenten
  noch nicht gemessen. Vorschlag: nach erstem Dogfooding-Lauf mit Idee A die
  Schwellwerte anpassen und in `rules.json` committen.
- **Min-Token-Default 30 vs. 50:** jscpd-Default ist 50, wir starten konservativer bei
  30. Anpassen, wenn False-Positives überhandnehmen.
- **Identifier-Normalisierung default an/aus:** Default aus ist sicherer (keine
  semantisch falschen Klone markiert), aber Type-2-Klone werden dann nicht erkannt.
  Vorschlag: Default aus, Opt-in pro Audit-Run via Tool-Argument.
- **Reicht ein On-Demand-Tool (Agent ruft es bei Bedarf auf) oder braucht es einen
  periodischen/erzwungenen Audit-Schritt im Drift-Loop (Skill-Integration, Idee F)?**
  Wahrscheinlich beides: On-Demand-Tool + Skill-Cadence. Aber: Skill-Disziplin ist
  nicht garantiert — die Lehre aus dem Safeguard-Fix-Review (2026-08-06) zeigt, dass
  Skills, die "irgendwann mal laufen sollen", oft genug nicht laufen. Daher: ergänzend
  in den Safeguard-Check einhängen (Score-Sub-Komponente)?
- **Cadence im Drift-Loop:** Einmal pro Epic-Abschluss oder pro Step? Zu häufig
  (pro Step) macht den Loop träge; zu selten (pro Epic) verpasst Refactoring-Drift
  in der Mitte. Pragmatischer Vorschlag: pro Step optional, pro Epic verpflichtend.
- **Score/Aufwand/Priorität** sind noch nicht endgültig geschätzt — Schicht 1
  (Idee A) hat eine grobe Spanne (5–7 Tage), Schicht 2 (Idee C) ebenso (2–3 Tage).
  Erst nach Spec-Schreib-Konvertierung als richtiges Epic mit Score + Akzeptanzkriterien
  in [`05-roadmap.md`](05-roadmap.md) übernehmen.
