---
status: done (pending audit)
type: step-plan
task: flaky-and-test-performance
step: 009               # flach, Task-weite Sequenz — auch Korrekturen liegen hier, nie in einem Unterordner
corrects: null             # <null | step-NNN> — nur gesetzt, wenn dieser Step eine Korrektur ist (treibt das Kettenbudget, siehe ../spec.md §10.5/§10.6)
title: "Category-Traits für Configuration-Tests nachziehen (Batch 8 von N, Configuration am 8-Item-Deckel)"
epic: EPIC-02          # Bezug zum Epic in roadmap.md, dem dieser Step zuarbeitet (bei corrects: vom korrigierten Step übernommen)
estimated_risk: low  # Einschätzung des Planers, siehe skills/planer/SKILL.md
step_type: batch  # single (Default) | batch — siehe ../spec.md §10.6. Bei batch: items-Liste unten füllen.
items:  # nur bei step_type: batch. Ein Eintrag pro gebündeltem Mini-Befund innerhalb des Epics (oder pro opportunistisch angehängtem auto_fixable-Tech-Debt, siehe ../spec.md §9.1/§10.6):
  - id: item-01
    title: "AgentFeaturesTests → Unit (in-process AgentFeatures + Config-Konstruktion; 16 [Fact]; 4 // @covers L12-15, // @covers-Block-Insert, neue Trait-Zeile L16, class L17; keine BOM, CRLF+TrNL, kein Nullable — Heavyweight 355 Z.)"
    source: "konzept.md §Wie Schritt 2, §Muss-Haven Traits-Punkt"
  - id: item-02
    title: "ConfigLoaderRulesJsonTests → Unit (in-process ConfigLoader.LoadRulesJsonContent; 4 [Fact]; 1 // @covers L9, // @covers-Block-Insert, neue Trait-Zeile L10, class L11; **BOM-tragend** — Byte-Scan vorher/nachher, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-03
    title: "ConfigNormalizerTests → Unit (in-process ConfigNormalizer; 3 [Fact]; Standard-Insert ohne // @covers, neue Trait-Zeile L5, class L6; **BOM-tragend** — Byte-Scan vorher/nachher, CRLF+TrNL, kein Nullable)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-04
    title: "ConfigSyncerTests → Unit (in-process ConfigSyncer + Konfigurations-IO; 9 [Fact]; Standard-Insert ohne // @covers, neue Trait-Zeile L10, class L11; **BOM-tragend** — Byte-Scan vorher/nachher, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-05
    title: "DeveloperExperienceTests → Unit (in-process DevEx-Komponenten + XML-Doc; 11 [Fact]; 5 // @covers L23-27 + XML-Doc L29-31 kombiniert, **XML-Doc-Variante** = Trait nach </summary> L31, neue Trait-Zeile L32, class L33; keine BOM, CRLF+TrNL, #nullable enable Z.1 — Heavyweight 368 Z. + komplexester Platzierungs-Fall im Batch)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-06
    title: "FileFilterEvaluatorTests → Unit (in-process FileFilterEvaluator.IsTestFile + TestFilePath; 3 [Fact] + 2 [Theory]×(4+5)=9 [InlineData] = 12 Test-Cases zur Laufzeit; Standard-Insert ohne // @covers, neue Trait-Zeile L12, class L13; keine BOM, CRLF+TrNL, kein Nullable)"
    source: "konzept.md §Wie Schritt 2; Code-Verifikation Planer-Schritt-2 (L67-71 Theory#1 mit 4 [InlineData], L84-89 Theory#2 mit 5 [InlineData])"
  - id: item-07
    title: "PathOverridesTests → Unit (in-process Project-Override-Anwendung; 10 [Fact]; Standard-Insert ohne // @covers, neue Trait-Zeile L8, class L9; **BOM-tragend** — Byte-Scan vorher/nachher, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-08
    title: "RuleMetadataRegistryTests → Unit (in-process RuleMetadataRegistry; 3 [Fact]; Standard-Insert ohne // @covers, neue Trait-Zeile L10, class L11; keine BOM, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
created_by: planer  # planer | orchestrator (nur bei mechanischem Korrektur-Transkript ohne Ermessen, siehe ../spec.md §6.2.1)
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-07T16:30:00+02:00
related_to: []  # Pointer auf andere step-NNN (Task-interne Abhängigkeiten) oder auf step-review.md (Fix-Modus) — nie Fakten cachen, nur verweisen. Siehe ../spec.md §10.6. Nicht zu verwechseln mit `corrects` oben (eigene, budget-relevante Semantik).
---

# Step 009: Category-Traits für Configuration-Tests nachziehen (Batch 8)

## Bezug

- **Task:** `flaky-and-test-performance`
- **Epic:** `EPIC-02` aus `roadmap.md` — Category-Traits flächendeckend
  nachziehen. Achter von N Batches; **erster und einziger** Batch auf
  den `Configuration/`-Ordner (8 Klassen homogen Unit passen genau in
  den 8-Item-Deckel von `spec.md` §10.6).
- **Konzept-Referenz:** `konzept.md` §"Wie" Schritt 2 ("Category-Traits
  nachziehen — alle ~1000 ungetraggten Tests einordnen"), §"Muss-Haven"
  Traits-Punkt ("konsequente Category-Traits ... auf **allen** Tests —
  aktuell nur 86 von ~1087"), §"Definition of Done" Punkt "Alle Tests
  tragen einen Category-Trait".
- **Vorgänger-Steps:** `step-001` (EPIC-01, approved, Spike-Befund
  negativ), `step-002` (EPIC-02 Batch 1, Suppression, 8 Klassen,
  approved), `step-003` (EPIC-02 Batch 2, Metrics, 7 Klassen, approved),
  `step-004` (EPIC-02 Batch 3, Web, 5 Klassen, approved),
  `step-005` (EPIC-02 Batch 4, Arch/Diag/FalsePositives/Cache,
  7 Klassen, approved), `step-006` (EPIC-02 Batch 5, Evals, 3 Klassen,
  approved), `step-007` (EPIC-02 Batch 6, Output Teil 1/2, 5 Klassen
  D–O, approved), `step-008` (EPIC-02 Batch 7, Output Teil 2/2,
  4 Klassen P–V, approved, Commit `95ab4d5`). Die sieben vorherigen
  Batches lieferten die etablierte Klassifikations-Heuristik
  (Subprozess-Marker = Integration; sonst Unit), die Trait-Syntax-
  Konvention (`[Trait("Category", "Unit")]`, CamelCase-Großbuchstabe),
  die Trait-Platzierungs-Bibliothek (Standard-Insert,
  `// @covers`-Block-Insert, XML-Doc-Variante, additive method-level
  Traits), die Heuristik-Punkte 1–6 (Klassen-Homogenität → Klassen-
  Trait; bestehende Traits respektieren/additiv ergänzen; `null!` als
  Edge-Input; Klassen-Trait additiv zu bestehenden method-level Traits
  bei homogenen Klassen; Hypothesen-Auflösungs-Pflicht für offene
  "möglicherweise…"-Annotationen in der CodeMap; **Helper-Klassen ohne
  Testmethoden sind keine Testklassen**, in step-007 etabliert, in
  step-008 ohne Ausnahme bestätigt = vollständig abgehakt), und die
  DoD-Struktur (Build grün, Voll-Test grün, Unit-Filter grün,
  Integration-Filter best-effort, Self-Lint `OK`, numerische
  Plausibilitätsprüfung, konkreter Subject-Vorschlag mit exakter
  Längen-Angabe).
- **`Configuration/`-Schnitt-Entscheidung:** 8 Test-Klassen, homogen
  Unit (0 Subprozess-Marker, 0 `IClassFixture`, 0 bestehende Traits,
  regex-verifiziert), passen genau in den 8-Item-Deckel = **ein** Batch
  (genau am Deckel, keine Reserve). Empfehlung (a) aus dem
  Orchestrator-Auftrag umgesetzt. Alternative (b) 4+4-Aufteilung
  geprüft, verworfen: keine sinnvolle Trennlinie (alle 8 Klassen
  gleicher Komplexitäts-Stufe, keine EOL/BOM-Unterschiede, die eine
  Aufteilung rechtfertigen würden — die BOM-Inhomogenität ist über
  das 8er-Set verteilt, nicht in 2 Clustern konzentriert, und ist
  außerdem **nicht** entscheidungsrelevant für die Trait-Mechanik).
- **Anti-Loop-Check** gegen `codemap.md` (Stand step-008-Doku-Commit,
  ~52 Einträge, 6 Sektionen): die `Configuration/`-Zeile in der
  Sektion "Test-Verzeichnisse — geplant für EPIC-02-Folge-Batches"
  trägt den Vermerk "8 Klassen; rein Unit, geplant für Batch „Reine-
  Unit-Ordner, groß" (zuletzt: step-002)" — **keine** offene Hypothese,
  **keine** bestehende Entscheidung widerspricht diesem Plan. Die
  Heuristik-Punkte 1–6 sind in step-002..step-008 etabliert und
  bestätigt. **Keine weitere bestehende Entscheidung** in der CodeMap
  widerspricht diesem Plan.

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen der acht Zieldateien + Inventur des `Configuration/`-Ordners
vorgefunden (relevant für step-009):

- **Ziel-Ordner-Inventar step-009 (8 Test-Klassen, alle alphabetisch
  A–R, homogen Unit):**
  - `src/AiNetLinter.Tests/Configuration/AgentFeaturesTests.cs` —
    1 Test-Klasse, **16 `[Fact]`**, 355 Zeilen (zweitgrößte Datei
    im Batch), **// @covers-Block-Insert** (4 `// @covers`-Marker
    Z. 12-15, class Z. 16). Keine BOM, kein `#nullable enable`
    am Dateianfang (erste Zeile `using AiNetLinter.Configuration;`).
  - `src/AiNetLinter.Tests/Configuration/ConfigLoaderRulesJsonTests.cs` —
    1 Test-Klasse, **4 `[Fact]`**, 42 Zeilen, **// @covers-Block-Insert**
    (1 `// @covers`-Marker Z. 9, class Z. 10). **BOM-tragend**,
    `#nullable enable` am Dateianfang (Z. 1).
  - `src/AiNetLinter.Tests/Configuration/ConfigNormalizerTests.cs` —
    1 Test-Klasse, **3 `[Fact]`**, 46 Zeilen, **Standard-Insert**
    (kein `// @covers`, class Z. 5). **BOM-tragend**, **kein**
    `#nullable enable` am Dateianfang (erste Zeile
    `using AiNetLinter.Configuration;`).
  - `src/AiNetLinter.Tests/Configuration/ConfigSyncerTests.cs` —
    1 Test-Klasse, **9 `[Fact]`**, 254 Zeilen, **Standard-Insert**
    (kein `// @covers`, class Z. 10). **BOM-tragend**,
    `#nullable enable` am Dateianfang.
  - `src/AiNetLinter.Tests/Configuration/DeveloperExperienceTests.cs` —
    1 Test-Klasse, **11 `[Fact]`**, 368 Zeilen (größte Datei im
    Batch), **XML-Doc-Variante** (5 `// @covers`-Marker Z. 23-27,
    Leerzeile Z. 28, XML-Doc Z. 29-31, class Z. 32 → **komplexester
    Platzierungs-Fall im Batch**: 3 Schichten — `// @covers` +
    Leerzeile + XML-Doc + class). Keine BOM, `#nullable enable` am
    Dateianfang.
  - `src/AiNetLinter.Tests/Configuration/FileFilterEvaluatorTests.cs` —
    1 Test-Klasse, **3 `[Fact]` + 2 `[Theory]`** = 5 Methoden,
    203 Zeilen, **Standard-Insert** (kein `// @covers`, class Z. 12).
    Keine BOM, kein `#nullable enable` am Dateianfang. Die 2
    `[Theory]`-Methoden (L67 Theory#1 + L84 Theory#2) tragen
    zusammen 9 `[InlineData]` (4+5) → **12 Test-Cases zur Laufzeit**
    (3 Fact + 9 InlineData = 12).
  - `src/AiNetLinter.Tests/Configuration/PathOverridesTests.cs` —
    1 Test-Klasse, **10 `[Fact]`**, 166 Zeilen, **Standard-Insert**
    (kein `// @covers`, class Z. 8). **BOM-tragend**, `#nullable
    enable` am Dateianfang.
  - `src/AiNetLinter.Tests/Configuration/RuleMetadataRegistryTests.cs` —
    1 Test-Klasse, **3 `[Fact]`**, 49 Zeilen, **Standard-Insert**
    (kein `// @covers`, class Z. 10). Keine BOM, `#nullable enable`
    am Dateianfang.
- **`Configuration/`-Schnitt-Begründung (8 Klassen in 1 Batch, am
  Deckel):**
  - **Warum 8 in 1 Batch:** 8 Klassen homogen Unit = exakt der
    8-Item-Deckel aus `task-state.md` Config (`max_batch_items: 8`).
    Keine Aufteilung nötig — alle Klassen gleicher Komplexitäts-Stufe
    (kein Heavyweight über 30 Facts wie step-008
    `ViolationMarkdownFormatterTests`, max 16 Facts in
    `AgentFeaturesTests`).
  - **Warum nicht 4+4 (oder 5+3):** keine sinnvolle Trennlinie
    gefunden. Alphabetisch wäre A–F / G–R (oder ähnlich), aber alle
    8 Klassen sind gleichartig genug, dass die künstliche Trennung
    reinen Overhead produzieren würde (1 zusätzlicher Step = 1
    zusätzlicher Plan + Coder + Kritiker-Runde, ohne technischen
    Mehrwert). Die BOM-Inhomogenität (4/8 mit BOM) ist **nicht**
    in 2 Clustern konzentriert — die BOM-tragenden Dateien sind
    `ConfigLoaderRulesJsonTests` (B), `ConfigNormalizerTests` (C),
    `ConfigSyncerTests` (D), `PathOverridesTests` (P), also über das
    Alphabet verteilt.
  - **Warum nicht `Configuration/` + `Maps/` (6 Klassen) mischen:**
    8 + 6 = 14 Items = **über** dem 8-Item-Deckel (man bräuchte 2
    Batches, dann wäre der erste Batch immer noch Configuration/
    8 Klassen und der zweite Maps/ 6 Klassen — kein Effizienzgewinn
    gegenüber der sequenziellen Abarbeitung). Außerdem verletzt das
    Mischen zweier Ordner die etablierte "1 Ordner = 1 Batch"-Linie
    (step-002..step-008).
- **Bestehende Trait-Verteilung** (verifiziert per `grep -cE
  '\[Trait\('` über alle 8 Dateien):
  - **0 Klassen-Traits, 0 method-level Traits** über alle 8 Dateien.
    Alle 8 Klassen sind "jungfräulich" — keine Vorab-Klassifikation
    zu respektieren, keine method-level Traits additiv zu ergänzen.
    Reiner Klassen-Trait-Insert ist die einfachste denkbare
    Variante.
- **Subprozess-Marker im gesamten 8-Datei-Set** (verifiziert per
  `grep -cE 'Process\.Start|McpTestClient|CliProcessRunner|
  Program\.Main|IClassFixture|SubprocessConcurrencyGate'`
  über alle 8 Dateien): **0/0/0/0/0/0/0/0 Treffer pro Datei.** Damit
  ist der gesamte Batch homogen **Unit** — keine Integration-Klasse.
  Passt zur etablierten Heuristik (Punkte 1–3) und zur
  step-002/003/004/005/006/007/008-Bestätigung.
- **Testmethoden-Inventar step-009** (regex-basiert per
  `grep -cE '\[(Fact|Theory)\]'`):
  - `AgentFeaturesTests.cs`: **16 `[Fact]` + 0 `[Theory]`** = 16 Methoden
  - `ConfigLoaderRulesJsonTests.cs`: **4 `[Fact]` + 0 `[Theory]`** = 4 Methoden
  - `ConfigNormalizerTests.cs`: **3 `[Fact]` + 0 `[Theory]`** = 3 Methoden
  - `ConfigSyncerTests.cs`: **9 `[Fact]` + 0 `[Theory]`** = 9 Methoden
  - `DeveloperExperienceTests.cs`: **11 `[Fact]` + 0 `[Theory]`** = 11 Methoden
  - `FileFilterEvaluatorTests.cs`: **3 `[Fact]` + 2 `[Theory]`** = 5 Methoden
  - `PathOverridesTests.cs`: **10 `[Fact]` + 0 `[Theory]`** = 10 Methoden
  - `RuleMetadataRegistryTests.cs`: **3 `[Fact]` + 0 `[Theory]`** = 3 Methoden
  - **Summe Methoden: 16+4+3+9+11+3+10+3 = 59 `[Fact]` + 2 `[Theory]` = 61 Methoden**
- **Test-Case-Inventar step-009** (regex-basiert für `[Fact]`,
  `[InlineData]`-Reihen pro `[Theory]`-Methode):
  - `AgentFeaturesTests.cs`: 16 = **16 Test-Cases**
  - `ConfigLoaderRulesJsonTests.cs`: 4 = **4 Test-Cases**
  - `ConfigNormalizerTests.cs`: 3 = **3 Test-Cases**
  - `ConfigSyncerTests.cs`: 9 = **9 Test-Cases**
  - `DeveloperExperienceTests.cs`: 11 = **11 Test-Cases**
  - `FileFilterEvaluatorTests.cs`: 3 + 4 (L67-71 Theory#1 mit
    4 `[InlineData]`) + 5 (L84-89 Theory#2 mit 5 `[InlineData]`) =
    **12 Test-Cases** (5 Methoden → 12 Test-Cases via
    `[Theory]+[InlineData]`-Expansion)
  - `PathOverridesTests.cs`: 10 = **10 Test-Cases**
  - `RuleMetadataRegistryTests.cs`: 3 = **3 Test-Cases**
  - **Summe Test-Cases: 16+4+3+9+11+12+10+3 = 68 Test-Cases**
- **Numerische Plausibilität** für die DoD-Disziplin
  (regex-basiert, gemäß step-003-Review NITPICK "regex statt
  manuell zählen"):
  - **Filter-Delta step-009:** Unit steigt um **+68**
    (= 16+4+3+9+11+12+10+3), Integration unverändert (+0), Total
    unverändert (+0).
  - **Erwarteter Unit-Filter nach step-009:**
    589 (Stand nach step-008) + 68 = **657**.
  - **Integration bleibt 113, Total bleibt 1325.**
  - **Methoden-Summe (61) ≠ Test-Case-Summe (68)** — die
    Diskrepanz von 7 kommt ausschließlich aus
    `FileFilterEvaluatorTests.cs` (5 Methoden → 12 Test-Cases
    via `[Theory]+[InlineData]`-Expansion; 2 `[Theory]`-Methoden
    tragen 9 `[InlineData]`-Zeilen, also +7 jenseits der 5
    Methoden = 12 Cases). Der Coder dokumentiert im
    `step-result.md` **beide** Zahlen (regex-basierte Methoden-
    Zählung pro Datei UND tatsächlicher Unit-Filter-Lauf-Wert) und
    gleicht sie gegen die Planer-Prognose ab.
- **Klassen-Deklarationen — Trait-Platzierungs-Varianten**
  (verifiziert per `grep -nE 'public sealed class|/// <summary>|
  // @covers'` über alle 8 Dateien):
  - **Standard-Insert zwischen `namespace …;` und
    `public sealed class …`** (5 Klassen, kein `// @covers`, kein
    XML-Doc, kein `: IDisposable`):
    - `ConfigNormalizerTests.cs:5` (`public sealed class
      ConfigNormalizerTests` ohne `IDisposable`, ohne `// @covers`,
      ohne XML-Doc; **BOM-tragend**, **kein** `#nullable enable` —
      erste Zeile ist `using AiNetLinter.Configuration;` Z. 1
      — die Trait-Zeile gehört zwischen Z. 4 (Leerzeile nach
      Namespace) und Z. 5 (Klasse))
    - `ConfigSyncerTests.cs:10` (gleiche Konstellation außer
      `#nullable enable` vorhanden und BOM-tragend)
    - `FileFilterEvaluatorTests.cs:12` (gleiche Konstellation
      außer kein `#nullable enable` und kein BOM)
    - `PathOverridesTests.cs:8` (gleiche Konstellation außer
      `#nullable enable` vorhanden und BOM-tragend)
    - `RuleMetadataRegistryTests.cs:10` (gleiche Konstellation
      außer kein BOM)
  - **`// @covers`-Block-Insert zwischen letztem `// @covers` und
    `public sealed class …`** (2 Klassen):
    - `AgentFeaturesTests.cs:12-16` (4 `// @covers` Z. 12-15,
      Leerzeile nicht vorhanden — Z. 11 ist die Leerzeile nach
      `namespace …;`, Z. 12-15 sind die 4 `// @covers` ohne
      Leerzeile dazwischen, dann class Z. 16 — die Trait-Zeile
      gehört zwischen Z. 15 und Z. 16, class verschiebt sich
      auf Z. 17 nach Edit)
    - `ConfigLoaderRulesJsonTests.cs:9-10` (1 `// @covers` Z. 9,
      class Z. 10; **BOM-tragend**, `#nullable enable` Z. 1;
      analoge Struktur)
  - **XML-Doc-Variante zwischen `</summary>` und
    `public sealed class …`** (1 Klasse — komplexester Fall
    im Batch, 3 Schichten):
    - `DeveloperExperienceTests.cs:23-32` (5 `// @covers` Z. 23-27,
      Leerzeile Z. 28, XML-Doc Z. 29-31 mit `/// <summary>` Z. 29,
      `/// Tests für die neuen Developer-Experience-Features
      (Project Overrides, AI-Context-Footprint, Repo-Playbook).`
      Z. 30, `/// </summary>` Z. 31, dann class Z. 32 — die
      Trait-Zeile gehört zwischen Z. 31 (`</summary>`) und Z. 32
      (Klasse), class verschiebt sich auf Z. 33 nach Edit; die 5
      `// @covers` Z. 23-27 sind **nicht** zwischen Trait und class,
      sondern **oberhalb** der XML-Doc-Schicht — der Trait wird
      gemäß etablierter XML-Doc-Variante (siehe step-008 item-02
      `RuleLegendRegistryTests`) **nach** dem XML-Doc, **vor** der
      class eingefügt)
- **EOL-/BOM-/Trailing-NL-Status** (verifiziert per PowerShell-Byte-
  Check über alle 8 step-009-Dateien):

  | Datei                                       | BOM  | CR   | LF   | Trailing-NL | Erste 3 Bytes          |
  |---------------------------------------------|------|-----:|-----:|-------------|------------------------|
  | `Configuration/AgentFeaturesTests.cs`       |  ✗   |  413 |  413 |     ✓       | `75 73 69` (`using`)   |
  | `Configuration/ConfigLoaderRulesJsonTests.cs` |  ✓   |   48 |   48 |     ✓       | `EF BB BF` (BOM)       |
  | `Configuration/ConfigNormalizerTests.cs`    |  ✓   |   58 |   58 |     ✓       | `EF BB BF` (BOM)       |
  | `Configuration/ConfigSyncerTests.cs`        |  ✓   |  289 |  289 |     ✓       | `EF BB BF` (BOM)       |
  | `Configuration/DeveloperExperienceTests.cs` |  ✗   |  414 |  414 |     ✓       | `23 6E 75` (`#nu`)     |
  | `Configuration/FileFilterEvaluatorTests.cs` |  ✗   |  230 |  230 |     ✓       | `75 73 69` (`using`)   |
  | `Configuration/PathOverridesTests.cs`       |  ✓   |  189 |  189 |     ✓       | `EF BB BF` (BOM)       |
  | `Configuration/RuleMetadataRegistryTests.cs` |  ✗   |   54 |   54 |     ✓       | `23 6E 75` (`#nu`)     |

  **Beobachtungen:**
  - **EOL-Inhomogenität: keine** — alle 8 Dateien **uniform CRLF**
    (CR-Zahl = LF-Zahl in allen 8 Dateien, kein gemischter Status).
    **TD-003 (LF-only `McpLintConsoleTests.cs` in `Output/`)
    betrifft step-009 NICHT** — der `Configuration/`-Ordner ist
    diesbezüglich sauber, Standard-Edit-Tool reicht für die
    EOL-Erhaltung.
  - **Trailing-NL: alle 8 Dateien mit Trailing-NL** (letztes Byte
    = LF in allen 8 Dateien) — Standard-Edit-Tool reicht auch hier.
  - **BOM-Inhomogenität: 4 von 8 mit BOM** (neue Beobachtung für
    `Configuration/`, in Output/ nicht vorhanden — `Output/` ist
    dort uniform ohne BOM). Konkret:
    - **MIT BOM:** `ConfigLoaderRulesJsonTests`, `ConfigNormalizerTests`,
      `ConfigSyncerTests`, `PathOverridesTests` (4 von 8 = 50 %)
    - **OHNE BOM:** `AgentFeaturesTests`, `DeveloperExperienceTests`,
      `FileFilterEvaluatorTests`, `RuleMetadataRegistryTests` (4 von 8 = 50 %)
    - **Konsequenz für den Coder:** das Standard-Edit-Tool erhält
      die BOM in der Regel (Bytes vor und nach dem Edit sind
      identisch), aber der Coder **muss** für alle 4 BOM-tragenden
      Dateien explizit per
      `[System.IO.File]::ReadAllBytes(...)`-Scan **vor** und
      **nach** dem Edit verifizieren, dass die ersten 3 Bytes
      weiterhin `EF BB BF` sind. Falls das Standard-Edit-Tool die
      BOM überschreibt (z. B. durch "Datei komplett neu schreiben"
      statt "Zeile einfügen"), muss der Coder auf einen byte-genauen
      Python-Helper analog step-007 (`McpLintConsoleTests.cs`
      LF-only) umstellen.
  - **Pattern-Beobachtung (Heuristik-Punkt 7, in spe):** die BOM-
    Inhomogenität in `Configuration/` ist eine **neue Inhomogenitäts-
    Dimension** (Encoding/BOM) — Output/ hatte nur EOL-Inhomogenität
    (TD-003, LF-only) und `#nullable enable`-Inhomogenität (TD-004,
    5/10). Configuration/ hat **keine** EOL-Inhomogenität, hat
    **eine andere** `#nullable enable`-Verteilung als Output/ (5/8
    mit Direktive: `ConfigLoaderRulesJsonTests`, `ConfigSyncerTests`,
    `DeveloperExperienceTests`, `PathOverridesTests`,
    `RuleMetadataRegistryTests`; 3/8 ohne: `AgentFeaturesTests`,
    `ConfigNormalizerTests`, `FileFilterEvaluatorTests`; die
    Teilmenge ist eine **andere** als die BOM-Teilmenge, also nicht
    trivial korreliert), und **die hier erstmals beobachtete BOM-
    Inhomogenität**. Alle 3 Inhomogenitäts-Dimensionen haben eine
    gemeinsame wahrscheinliche Wurzel (Repository-/Editor-/
    Checkout-Spezialverhalten, vermutlich `core.autocrlf`/`core
    .autocrlf-input` + Encoding-Defaults), aber **kein** step-009-
    Scope zur Klärung. Die BOM-Inhomogenität wird in der Roadmap
    EPIC-02-Zeile als Heuristik-Punkt 7 (in spe) dokumentiert —
    **nicht** als TD-Eintrag angelegt (analog TD-003-Vorgehen: erst
    beobachten, dann entscheiden ob Konsolidierungs-Wunsch entsteht).
- **Bündelungs-Begründung (8 Klassen in 1 Batch, genau am Deckel):**
  die 8 step-009-Klassen sind der gesamte `Configuration/`-Ordner.
  Eine Aufteilung in 2+ Einzel-Step-Planungen wäre reiner Overhead
  ohne Mehrwert. **Vorteile der Bündelung:** (1) **berechtigt durch
  homogenen Charakter** — alle 8 Klassen sind `Unit` ohne
  Subprozess-Marker, einheitliche Heuristik-Anwendung; (2) **Klassen-
  Level-Mix bleibt überschaubar** — keine Integration-Klasse im
  Set, also kein Misch-Heuristik-Diskussion; (3) **Trait-
  Platzierungs-Varianten sind im step-008-Bibliotheks-Set
  abgedeckt** (Standard-Insert in 5 Klassen, `// @covers`-Block-
  Insert in 2 Klassen, XML-Doc-Variante in 1 Klasse — keine
  Datei benötigt eine **neue** Platzierungs-Sonderbehandlung);
  (4) **passt exakt in den 8-Item-Deckel** (8 Items + 0 Reserve);
  (5) **kleiner Diff-Umfang** (8 Trait-Zeilen + ggf. Doku-Commit,
  deutlich unter dem 40-Zeilen-Deckel); (6) **folgt der step-002/
  003/004/005/006/007/008-Logik** für "1 Ordner = 1 Batch" der
  kleinen/mittleren Unit-Ordner; (7) **schließt den `Configuration/`-
  Ordner vollständig ab** in 1 Schritt — der Folge-Schritt kann auf
  `Core/Checkers/` (27 Klassen) oder einen der nächsten kleineren
  Unit-Ordner zielen.
- **Alternative, verworfen — Heavyweights isolieren:** die beiden
  größten Dateien im Batch (`AgentFeaturesTests.cs` 355 Z. / 16 Facts
  und `DeveloperExperienceTests.cs` 368 Z. / 11 Facts) in einem
  eigenen Step zu taggen, wäre möglich (je 1 Step, der 8-Item-Deckel
  voll ausnutzt). Verworfen, weil (a) beide Dateien kleiner sind
  als der step-008 Heavyweight `ViolationMarkdownFormatterTests.cs`
  (473 Z. / 30 Facts), also keine echte Heavyweight-Sonderbehandlung
  nötig; (b) der 1-Datei-1-Step-Overhead den Nutzen nicht
  rechtfertigt (gleicher Diff-Umfang, gleicher Trait-Mechanik); (c)
  die alphabetische Konsistenz durchbrochen würde. Beide
  "semi-Heavyweights" werden im Batch mitgetaggtt und im
  `step-result.md` mit Diff-Statistik separat ausgewiesen.
- **Alternative, verworfen — `Configuration/` Teil 1/2 + `Maps/`-
  Anfang mischen:** `Maps/` + `Maps/Skeleton/` haben 6 Klassen
  (siehe `roadmap.md` und `codemap.md`); eine Mischung würde
  8 + 6 = 14 Items ergeben = **über** 8-Item-Deckel = 2 Batches
  statt 1 (kein Effizienzgewinn); zudem verletzt das Mischen
  zweier Ordner die "1 Ordner = 1 Batch"-Linie.
- **TD-003-Kontext-Hinweis** (NICHT step-009-Scope, nur Beobachtung):
  die in step-007 als TD-003 dokumentierte EOL-Inhomogenität
  (`Output/McpLintConsoleTests.cs` LF-only, alle anderen 9 `Output/`-
  Dateien CRLF) **betrifft step-009 nicht** — alle 8 step-009-
  Dateien sind uniform CRLF, **kein** byte-genauer Python-Helper
  nötig. **Aber:** die hier neu beobachtete **BOM-Inhomogenität**
  in `Configuration/` ist ein **anderer** Inhomogenitäts-Typ als
  TD-003 (Encoding statt EOL); Konsolidierung ist ebenfalls
  Nutzer-/Repo-Konvention-Entscheidung, **nicht** in step-009-Scope.
  Falls der Nutzer die BOM-Konsolidierung wünscht, ist das ein
  eigenständiger Folge-Schritt (analog TD-003-Variante (a) `git
  add --renormalize .`).
- **TD-004-Kontext-Hinweis** (NICHT step-009-Scope, nur Beobachtung):
  TD-004 dokumentiert `#nullable enable`-Inhomogenität in `Output/`
  (5/10 ohne Direktive, step-008-Beobachtung). Configuration/ hat
  eine **andere** `#nullable enable`-Verteilung (5/8 **mit** Direktive,
  3/8 **ohne** — **umgekehrte** Mehrheit als in Output/), und die
  Teilmenge der ohne-Direktive-Dateien ist eine **andere** als die
  BOM-Teilmenge. Es handelt sich also um 2 unabhängige
  Inhomogenitäts-Dimensionen. Da TD-004 als `auto_fixable: nein`
  markiert ist (Klärung mit `*.Tests`-Profil-Overrides zuerst nötig)
  und der Plan-DoD-Konsolidierungs-Wunsch explizit Nutzer-Sache ist,
  wird die Configuration/-Nullable-Inhomogenität **nicht** in
  step-009-Scope gezogen. Falls der Nutzer nach der TD-004-Klärung
  (Variante c) eine Konsolidierung wünscht, kann das als
  `auto_fixable: ja`-Bündel in einem späteren Schritt laufen.

## Intention

Alle 8 Testklassen im `Configuration/`-Ordner (`AgentFeaturesTests`,
`ConfigLoaderRulesJsonTests`, `ConfigNormalizerTests`,
`ConfigSyncerTests`, `DeveloperExperienceTests`,
`FileFilterEvaluatorTests`, `PathOverridesTests`,
`RuleMetadataRegistryTests`) mit `[Trait("Category", "Unit")]` auf
Klassen-Ebene versehen. Dieser Step ist der achte von N Batches, die
zusammen die EPIC-02-DoD erreichen ("alle ~1000 Tests getaggt"),
und der **erste und einzige** Batch auf den `Configuration/`-Ordner
(8 Klassen homogen Unit = exakt der 8-Item-Deckel = 1 Batch).
**Mit step-009 ist der `Configuration/`-Ordner vollständig
abgeschlossen** (8 Test-Klassen alle entschieden) — der Folge-
Schritt kann auf `Core/Checkers/`, `Core/`, `Maps/`, `Mcp/`,
`Commands/` oder `Cli/` als nächsten EPIC-02-Batch zielen.

Der Step liefert **vier nennenswerte Befunde**:

1. **`Configuration/`-Ordner vollständig abgeschlossen** in 1
   Schritt: 8 Test-Klassen, alle homogen Unit, passen genau in
   den 8-Item-Deckel. Der nächste Planer-Aufruf kann
   `Configuration/` als abgehakt voraussetzen.
2. **Heuristik-Punkt 7 (in spe, neu beobachtet, nicht
   angewendet):** BOM-Inhomogenität in `Configuration/` (4 von
   8 Dateien mit UTF-8-BOM, 4 ohne) — eine neue Inhomogenitäts-
   Dimension analog zu TD-003 (EOL) und TD-004 (Nullable), aber
   **nicht** deckungsgleich mit diesen (BOM ≠ EOL ≠ Nullable).
   Konsolidierung ist Nutzer-/Repo-Konvention-Entscheidung (analog
   TD-003/TD-004-Vorgehen); in der Roadmap EPIC-02-Zeile als
   "Heuristik-Punkt 7 (in spe)" dokumentiert, **kein** TD-Eintrag
   angelegt, **kein** `auto_fixable`-Anhängen in step-009.
3. **Trait-Platzierungs-Bibliothek vollständig bestätigt:** alle
   3 in step-007/008 etablierten Varianten (Standard-Insert in
   5 Klassen, `// @covers`-Block-Insert in 2 Klassen, XML-Doc-
   Variante in 1 Klasse) werden in step-009 angewendet — die
   `DeveloperExperienceTests`-Kombination aus `// @covers` + XML-Doc
   ist der bisher komplexeste Anwendungsfall (3 Schichten
   übereinander), die Mechanik aus step-008 item-02
   (`RuleLegendRegistryTests` XML-Doc-Variante) passt aber
   unverändert.
4. **Numerische Plausibilität** (regex-basiert pro Datei, gemäß
   step-003-Review NITPICK): **68 Test-Cases zur Laufzeit** aus
   61 Methoden (16+4+3+9+11+3+10+3 `[Fact]` + 2 `[Theory]` in
   `FileFilterEvaluatorTests` mit 9 `[InlineData]`-Zeilen
   verteilt auf 2 `[Theory]`-Methoden) — Diskrepanz Methoden (61)
   vs. Test-Cases (68) = 7, kommt **ausschließlich** aus der
   `[Theory]+[InlineData]`-Expansion in `FileFilterEvaluatorTests`
   (5 Methoden → 12 Test-Cases, also +7 jenseits der Methoden).

## Konkrete Änderungen

**Bei `step_type: batch`** (gemäß `../spec.md` §10.6): pro Item aus
der `items`-Liste im Frontmatter eine Unterüberschrift mit Datei-
Pfad und Zeilen-Angabe (verifiziert per Datei-Inspektion in Schritt 2).

### item-01: AgentFeaturesTests — `src/AiNetLinter.Tests/Configuration/AgentFeaturesTests.cs` (Z. 16, // @covers-Block-Insert)

- **Was:** zwischen letztem `// @covers GitChangedFilesResolver`
  (Z. 15) und `public sealed class AgentFeaturesTests` (Z. 16)
  eine neue Zeile `[Trait("Category", "Unit")]` einfügen;
  class verschiebt sich auf Z. 17 nach Edit.
- **Warum:** Klassen-Homogenität Unit (0 Subprozess-Marker,
  0 IClassFixture, alle 16 `[Fact]` in-process Config-Konstruktion
  via `TestHelper.CreateDefaultConfig()`), Standard-`// @covers`-
  Block-Insert-Variante analog step-008 (kein XML-Doc hier, daher
  Trait direkt vor class nach dem `// @covers`-Block).
- **BOM-Hinweis:** diese Datei hat **keine** BOM, erste 3 Bytes
  `75 73 69` (`using`). Kein BOM-Konservierungs-Scan nötig
  (Datei fängt mit `using` an, das bleibt nach Edit so).
- **Nullable-Hinweis:** diese Datei hat **kein** `#nullable
  enable` am Dateianfang (erste Zeile `using AiNetLinter.
  Configuration;` Z. 1). Kein Nullable-Edit nötig, Trait-Insert
  erfolgt **zwischen** Z. 15 und Z. 16 — `#nullable enable` ist
  hier kein Thema.

### item-02: ConfigLoaderRulesJsonTests — `src/AiNetLinter.Tests/Configuration/ConfigLoaderRulesJsonTests.cs` (Z. 10, // @covers-Block-Insert)

- **Was:** zwischen `// @covers ConfigLoader` (Z. 9) und
  `public sealed class ConfigLoaderRulesJsonTests` (Z. 10) eine
  neue Zeile `[Trait("Category", "Unit")]` einfügen; class
  verschiebt sich auf Z. 11 nach Edit.
- **Warum:** Klassen-Homogenität Unit (4 `[Fact]`, alle in-process
  `ConfigLoader.LoadRulesJsonContent`-Aufrufe), Standard-
  `// @covers`-Block-Insert-Variante.
- **BOM-Hinweis:** diese Datei **hat BOM** (erste 3 Bytes
  `EF BB BF`). Der Coder **muss** vor und nach dem Edit per
  `[System.IO.File]::ReadAllBytes(...)` verifizieren, dass die
  ersten 3 Bytes weiterhin `EF BB BF` sind. Falls das Edit-Tool
  die BOM überschreibt: byte-genauen Python-Helper analog
  step-007 verwenden. **Wichtig:** das Standard-Edit-Tool
  (`edit`-Tool) erhält die BOM in der Regel — die Prüfung ist
  eine **Sicherheits-Verifikation**, kein erwarteter Workaround.
- **Nullable-Hinweis:** diese Datei hat `#nullable enable` am
  Dateianfang (Z. 1), kein Nullable-Edit nötig.

### item-03: ConfigNormalizerTests — `src/AiNetLinter.Tests/Configuration/ConfigNormalizerTests.cs` (Z. 5, Standard-Insert)

- **Was:** zwischen `namespace AiNetLinter.Tests.Configuration;`
  (Z. 3) und `public sealed class ConfigNormalizerTests` (Z. 5)
  eine neue Zeile `[Trait("Category", "Unit")]` einfügen;
  class verschiebt sich auf Z. 6 nach Edit.
- **Warum:** Klassen-Homogenität Unit (3 `[Fact]`, alle in-process
  `ConfigNormalizer`-Aufrufe), Standard-Insert-Variante (kein
  `// @covers`, kein XML-Doc, kein `: IDisposable`).
- **BOM-Hinweis:** diese Datei **hat BOM** (erste 3 Bytes
  `EF BB BF`). Byte-Scan-Verifikation vor/nach Edit erforderlich
  (gleiche Logik wie item-02).
- **Nullable-Hinweis:** diese Datei hat **kein** `#nullable
  enable` am Dateianfang (erste Zeile `using AiNetLinter.
  Configuration;` Z. 1). Kein Nullable-Edit nötig.

### item-04: ConfigSyncerTests — `src/AiNetLinter.Tests/Configuration/ConfigSyncerTests.cs` (Z. 10, Standard-Insert)

- **Was:** zwischen `namespace AiNetLinter.Tests.Configuration;`
  (Z. 8) und `public sealed class ConfigSyncerTests` (Z. 10)
  eine neue Zeile `[Trait("Category", "Unit")]` einfügen;
  class verschiebt sich auf Z. 11 nach Edit.
- **Warum:** Klassen-Homogenität Unit (9 `[Fact]`, alle in-process
  `ConfigSyncer` + Konfigurations-IO via `TestHelper`-basierte
  Config-Konstruktion), Standard-Insert-Variante.
- **BOM-Hinweis:** diese Datei **hat BOM** (erste 3 Bytes
  `EF BB BF`). Byte-Scan-Verifikation vor/nach Edit erforderlich
  (gleiche Logik wie item-02).
- **Nullable-Hinweis:** diese Datei hat `#nullable enable` am
  Dateianfang (Z. 2 — `Get-Content` zeigt Z. 1 als BOM, Z. 2 als
  Leerzeile, Z. 3 als `using System.IO;`? Verifikation per
  PowerShell-Byte-Scan siehe DoD).

### item-05: DeveloperExperienceTests — `src/AiNetLinter.Tests/Configuration/DeveloperExperienceTests.cs` (Z. 32, XML-Doc-Variante)

- **Was:** zwischen `/// </summary>` (Z. 31) und
  `public sealed class DeveloperExperienceTests` (Z. 32) eine
  neue Zeile `[Trait("Category", "Unit")]` einfügen; class
  verschiebt sich auf Z. 33 nach Edit. **Die 5 `// @covers`-
  Marker Z. 23-27 bleiben unverändert** (sie sind **oberhalb** der
  XML-Doc-Schicht, der Trait wird gemäß etablierter XML-Doc-
  Variante **nach** dem XML-Doc eingefügt).
- **Warum:** Klassen-Homogenität Unit (11 `[Fact]`, alle in-process
  DevEx-Komponenten), XML-Doc-Variante — der bisher komplexeste
  Anwendungsfall im Batch (3 Schichten: `// @covers` + Leerzeile
  + XML-Doc + class). Mechanik analog step-008 item-02
  (`RuleLegendRegistryTests` XML-Doc-Variante, identisches
  Pattern: Trait direkt nach `</summary>`, direkt vor class).
- **BOM-Hinweis:** diese Datei hat **keine** BOM, erste 3 Bytes
  `23 6E 75` (`#nu` von `#nullable enable`). Kein BOM-
  Konservierungs-Scan nötig.
- **Nullable-Hinweis:** diese Datei hat `#nullable enable` am
  Dateianfang (Z. 1), kein Nullable-Edit nötig.
- **Komplexitäts-Hinweis:** der Coder muss beim Edit exakt die
  `</summary>`-Zeile Z. 31 als Anker verwenden, nicht versehentlich
  die XML-Doc-Start-Tag `/// <summary>` Z. 29 oder eine der
  `// @covers`-Zeilen Z. 23-27. Empfehlung: `Read`-Tool
  verwenden, exakte Zeile verifizieren, dann `edit`-Tool mit
  eindeutigem `old_string` (z. B. `</summary>\n\npublic sealed
  class DeveloperExperienceTests`) — die `</summary>`-Zeile
  ist im gesamten step-009-Set nur in dieser einen Datei
  vorhanden, daher Eindeutigkeit gewährleistet.

### item-06: FileFilterEvaluatorTests — `src/AiNetLinter.Tests/Configuration/FileFilterEvaluatorTests.cs` (Z. 12, Standard-Insert)

- **Was:** zwischen `namespace AiNetLinter.Tests.Configuration;`
  (Z. 10) und `public sealed class FileFilterEvaluatorTests`
  (Z. 12) eine neue Zeile `[Trait("Category", "Unit")]`
  einfügen; class verschiebt sich auf Z. 13 nach Edit.
- **Warum:** Klassen-Homogenität Unit (3 `[Fact]` + 2 `[Theory]`
  = 5 Methoden, 12 Test-Cases zur Laufzeit via `[InlineData]`-
  Expansion — alle in-process `FileFilterEvaluator.IsTestFile` +
  `TestFilePath`-Aufrufe), Standard-Insert-Variante.
- **BOM-Hinweis:** diese Datei hat **keine** BOM, erste 3 Bytes
  `75 73 69` (`using`). Kein BOM-Konservierungs-Scan nötig.
- **Nullable-Hinweis:** diese Datei hat **kein** `#nullable
  enable` am Dateianfang (erste Zeile `using Xunit;` Z. 1).
  Kein Nullable-Edit nötig.

### item-07: PathOverridesTests — `src/AiNetLinter.Tests/Configuration/PathOverridesTests.cs` (Z. 8, Standard-Insert)

- **Was:** zwischen `namespace AiNetLinter.Tests.Configuration;`
  (Z. 6) und `public sealed class PathOverridesTests` (Z. 8) eine
  neue Zeile `[Trait("Category", "Unit")]` einfügen; class
  verschiebt sich auf Z. 9 nach Edit.
- **Warum:** Klassen-Homogenität Unit (10 `[Fact]`, alle in-process
  Project-Override-Anwendungen), Standard-Insert-Variante.
- **BOM-Hinweis:** diese Datei **hat BOM** (erste 3 Bytes
  `EF BB BF`). Byte-Scan-Verifikation vor/nach Edit erforderlich
  (gleiche Logik wie item-02).
- **Nullable-Hinweis:** diese Datei hat `#nullable enable` am
  Dateianfang (Z. 1, gefolgt von `using`-Block), kein
  Nullable-Edit nötig.

### item-08: RuleMetadataRegistryTests — `src/AiNetLinter.Tests/Configuration/RuleMetadataRegistryTests.cs` (Z. 10, Standard-Insert)

- **Was:** zwischen `namespace AiNetLinter.Tests.Configuration;`
  (Z. 8) und `public sealed class RuleMetadataRegistryTests`
  (Z. 10) eine neue Zeile `[Trait("Category", "Unit")]`
  einfügen; class verschiebt sich auf Z. 11 nach Edit.
- **Warum:** Klassen-Homogenität Unit (3 `[Fact]`, alle in-process
  `RuleMetadataRegistry`-Aufrufe), Standard-Insert-Variante.
- **BOM-Hinweis:** diese Datei hat **keine** BOM, erste 3 Bytes
  `23 6E 75` (`#nu` von `#nullable enable`). Kein BOM-
  Konservierungs-Scan nötig.
- **Nullable-Hinweis:** diese Datei hat `#nullable enable` am
  Dateianfang (Z. 1), kein Nullable-Edit nötig.

## Tests

- [ ] `dotnet build` — Solution-Root, muss grün sein (0 Warnungen,
  0 Fehler; `TreatWarningsAsErrors=true` in beiden Projekten)
- [ ] `dotnet test --no-build` — voller Testlauf, muss grün sein
  (1325 Tests, 0 Fehler; Lauf-Dauer als Sekundär-Indikator)
- [ ] `dotnet test --no-build --filter "Category=Unit"` — Unit-Filter,
  muss **657 Tests** zeigen (Stand nach step-008: 589, +68 step-009
  erwartet)
- [ ] `dotnet test --no-build --filter "Category=Integration"` —
  Integration-Filter, muss **113 Tests** zeigen (unverändert)
- [ ] `dotnet run --project src/AiNetLinter -- --config rules.json --path .`
  — Self-Lint-Äquivalent (TD-001-konformer Ersatz für die fehlende
  `--self-lint`-Option), muss `OK` liefern

## Definition of Done

- [ ] Alle 8 "Konkrete Änderungen" (item-01..item-08) umgesetzt —
      jede Datei hat genau **eine** neue Zeile
      `[Trait("Category", "Unit")]` an der im Plan angegebenen
      Stelle, class-Zeile verschiebt sich um +1
- [ ] **Numerische Plausibilitätsprüfung** im `step-result.md`:
      - Methoden-Inventar pro Datei (regex-basiert per
        `Select-String -Pattern '\[(Fact|Theory)\b'`, **nicht**
        manuell gezählt — gemäß step-003-Review NITPICK):
        AgentFeaturesTests=16, ConfigLoaderRulesJsonTests=4,
        ConfigNormalizerTests=3, ConfigSyncerTests=9,
        DeveloperExperienceTests=11, FileFilterEvaluatorTests=5
        (3 Fact+2 Theory), PathOverridesTests=10,
        RuleMetadataRegistryTests=3 = **61 Methoden** (59 `[Fact]`
        + 2 `[Theory]`)
      - Test-Case-Inventar pro Datei (regex-basiert, mit
        `[InlineData]`-Reihen pro `[Theory]`-Methode explizit
        gezählt): 16+4+3+9+11+12+10+3 = **68 Test-Cases** zur
        Laufzeit
      - **Filter-Delta:** Unit 589 → 657 (+68 ✓), Integration
        113 → 113 (±0 ✓), Total 1325 → 1325 (±0 ✓)
      - **Diskrepanz Methoden (61) vs. Test-Cases (68) = 7**
        kommt **ausschließlich** aus `FileFilterEvaluatorTests`
        (5 Methoden → 12 Test-Cases via 2 `[Theory]` mit 4+5=9
        `[InlineData]`-Zeilen = 7 jenseits der 5 Methoden)
- [ ] Build-Command `dotnet build` grün
- [ ] Test-Command `dotnet test` grün
- [ ] Unit-Filter-Lauf (`--filter "Category=Unit"`) grün mit
      **657 Tests** (s. numerische Plausibilität)
- [ ] Integration-Filter-Lauf (`--filter "Category=Integration"`)
      grün mit 113 Tests (best-effort — bei Flake des pre-existing
      `McpServerCommandLoadingStateTests.LoadState_...
      ReportsLoadedImmediately`-Tests 1× Re-Run erlaubt;
      EPIC-06-Ziel, nicht step-009-Scope)
- [ ] Self-Lint-Äquivalent `dotnet run --project src/AiNetLinter --
      --config rules.json --path .` liefert `OK`
- [ ] **BOM-Konservierung verifiziert** für die 4 BOM-tragenden
      Dateien (`ConfigLoaderRulesJsonTests`, `ConfigNormalizerTests`,
      `ConfigSyncerTests`, `PathOverridesTests`): `[System.IO.File]
      ::ReadAllBytes(...)` vor und nach dem Edit zeigt weiterhin
      erste 3 Bytes `EF BB BF`. Falls bei einer der 4 Dateien die
      BOM verloren geht: byte-genauen Python-Helper analog
      step-007 einsetzen und im `step-result.md` §"Abweichungen"
      dokumentieren. **Falls alle 4 Bytes identisch sind** (was
      erwartet wird): keine Sonderbehandlung nötig, im
      `step-result.md` §"Beobachtungen" explizit als bestätigt
      vermerken.
- [ ] **EOL-/Trailing-NL-Konservierung stichprobenartig
      verifiziert** für mindestens 3 Dateien (z. B. `AgentFeaturesTests`,
      `ConfigSyncerTests`, `FileFilterEvaluatorTests`): per
      `[System.IO.File]::ReadAllBytes(...)` vor und nach dem
      Edit CR-Zahl = LF-Zahl, letztes Byte = LF. Alle 8 Dateien
      uniform CRLF + Trailing-NL (verifiziert vom Planer),
      Standard-Edit-Tool reicht.
- [ ] Keine `// @covers`-Marker, keine XML-Doc-Inhalte, keine
      `using`-Statements, keine Methoden-Bodies verändert — rein
      additiver Trait-Insert pro Datei
- [ ] **CodeMap-Update:** `tasks/flaky-and-test-performance/
      codemap.md` `Configuration/`-Zeile in der Sektion
      "Test-Verzeichnisse — geplant für EPIC-02-Folge-Batches"
      aktualisiert von "8 Klassen; rein Unit, geplant für Batch
      „Reine-Unit-Ordner, groß" (zuletzt: step-002)" auf
      "8 Klassen, alle Unit, mit `[Trait("Category", "Unit")]`
      auf Klassen-Ebene versehen (zuletzt: step-009)";
      `last_updated`-Zeitstempel vorgespult
- [ ] `tasks/flaky-and-test-performance/step-009/step-result.md`
      geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf
      `done (pending audit)` gesetzt
- [ ] **Commit auf `main`** (Branch-Konvention aus `task-state.md`
      Config `target_branch: main`) — Conventional Commit auf
      Deutsch, **konkreter Subject-Vorschlag** (TD-002-Disziplin,
      exakt eingehaltene ≤72-Zeichen-Grenze inkl. Suffix):
      `test: Configuration-Tests Kategorie-taggen [flaky-and-test-performance]`
      **(71 Zeichen, 1 Zeichen Reserve zur 72-Grenze)**, verifiziert
      per PowerShell `('test: Configuration-Tests Kategorie-taggen
      [flaky-and-test-performance]').Length -le 72` = `True` —
      parallel zur etablierten Konvention aus step-002
      (`Suppression-Tests Kategorie-taggen`), step-006
      (`Evals-Tests Kategorie-taggen`), step-007/008
      (`Output-Tests Kategorie-taggen 1/2`/`2/2`); keine
      `1/1`-Markierung nötig (Configuration/ ist ein einzelner
      Batch, kein Halb-Ordner-Schnitt).

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — auto-generierte
  C#-Codequalitätsregeln aus `rules.json`: für step-009
  relevant sind `EnforceSealedClasses: false` für `*.Tests`-
  Override (alle 8 step-009-Klassen sind `public sealed class`,
  der Trait-Insert ändert daran nichts), `MaxMethodLineCount: 100`
  (keine Methode im step-009-Set erreicht 100 Z., auch nicht
  `AgentFeaturesTests` oder `DeveloperExperienceTests` — die
  Heavyweight-Dateien beziehen sich auf Datei-Größe, nicht auf
  einzelne Methoden), `EnforceNullableEnable` (in 3/8 Dateien
  **fehlt** die Direktive, aber `*.Tests`-Profil-Override analog
  `EnforceSealedClasses` hebt die Regel vermutlich auf — Build
  grün bestätigt, kein step-009-Scope zur Klärung; siehe TD-004
  für projektweite Diskussion).
- `.agents/rules/AiNetLinterRichtlinien.mdc` — manuelle
  Architektur- und Workflow-Leitlinien: für step-009 relevant
  sind §4 (Commit-Konventionen — Subject ≤ 72 Zeichen, Suffix
  `[flaky-and-test-performance]`; konkreter Subject im DoD oben
  vorgegeben, 71 Zeichen), §5 (sparsame Kommentare — keine
  Verweise auf `step-NNN`/`TD-XXX`/`EPIC-XX` in Code-Kommentaren
  — step-009 ändert keine Kommentare, nur Trait-Inserts), §6
  (Zero-Warning-Direktive — `TreatWarningsAsErrors=true` muss
  grün bleiben; Trait-Insert führt keine neuen Warnings ein, da
  `[Trait(...)]` ein xUnit-Standard-Attribut ist).

## Bekannte Ausnahmen

- **BOM-Inhomogenität in `Configuration/` (4/8 mit BOM, 4/8 ohne
  BOM):** analog TD-003 (EOL-Inhomogenität in `Output/`) und
  TD-004 (Nullable-Inhomogenität in `Output/`) eine
  Repository-Konsistenz-Frage, **nicht** in step-009-Scope
  gelöst. Konsolidierung ist Nutzer-/Repo-Konvention-Entscheidung
  (out of scope; in `roadmap.md` EPIC-02-Zeile als Heuristik-
  Punkt 7 in spe dokumentiert). **Konservierung pro Datei ist
  in step-009-Scope** (Standard-Edit-Tool erhält BOM, Byte-Scan
  vor/nach Edit verifiziert — siehe DoD).
- **EOL-Inhomogenität TD-003 (`Output/McpLintConsoleTests.cs`
  LF-only):** betrifft step-009 **nicht** (alle 8 `Configuration/`-
  Dateien uniform CRLF), keine Sonderbehandlung in step-009.
  TD-003 bleibt offen, eigenständiger Folge-Schritt.
- **Nullable-Inhomogenität TD-004 (`Output/`, 5/10 ohne
  `#nullable enable`):** betrifft step-009 **nicht** in Bezug
  auf die Mechanik (3/8 step-009-Dateien ohne Direktive, aber
  `*.Tests`-Override hebt die Regel vermutlich auf — Build
  grün bestätigt). Configuration/ hat eine **andere** Nullable-
  Verteilung (5/8 **mit** Direktive, 3/8 **ohne** — umgekehrte
  Mehrheit als Output/) und eine **andere** Teilmenge der
  ohne-Direktive-Dateien als die BOM-Teilmenge. Beide
  Inhomogenitäten sind unabhängige Dimensionen, gemeinsame
  wahrscheinliche Wurzel (Repository-/Editor-Spezialverhalten),
  **kein** step-009-Scope zur Klärung.
- **Pre-existing Flaky-Test `McpServerCommandLoadingStateTests
  .LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`
  (in `Commands/McpServerCommandLoadingStateTests.cs` Z. 112-150,
  EPIC-06-Ziel):** Integration-Filter-Lauf kann den Flake
  reproduzieren — laut Konzept §"Definition of Done" und DoD-
  Konvention aus step-008 ist **1 Re-Run erlaubt**; bei
  wiederholtem Flake ist es kein step-009-Issue, sondern EPIC-06-
  Problem.
- **`Configuration/`-Schnitt am 8-Item-Deckel = exakt:** keine
  Reserve, kein Misch-Batch mit anderen Ordnern möglich ohne
  Deckel-Verletzung. Falls eine 9. Configuration-Klasse in
  Zukunft hinzukommt, muss der Batch aufgeteilt oder ein
  2-Batch-Cluster geplant werden — heute nicht relevant
  (verifiziert: 8 `.cs`-Dateien im Ordner, alle Test-Klassen).

## Code-Skizze (Beispiele für die 3 Platzierungs-Varianten)

```
// Standard-Insert (5 Klassen, z. B. ConfigNormalizerTests.cs)
namespace AiNetLinter.Tests.Configuration;

[Trait("Category", "Unit")]
public sealed class ConfigNormalizerTests
{
    // ...
}

// Standard-// @covers-Block-Insert (2 Klassen, z. B. ConfigLoaderRulesJsonTests.cs)
#nullable enable

using System.IO;
// ...

namespace AiNetLinter.Tests.Configuration;

// @covers ConfigLoader
[Trait("Category", "Unit")]
public sealed class ConfigLoaderRulesJsonTests
{
    // ...
}

// XML-Doc-Variante (1 Klasse, DeveloperExperienceTests.cs)
#nullable enable

// @covers ImpactExecutor
// @covers PostAnalysisChecks
// @covers TestProjectDetector
// @covers AgentRulesGenerator

/// <summary>
/// Tests für die neuen Developer-Experience-Features (Project Overrides, AI-Context-Footprint, Repo-Playbook).
/// </summary>
[Trait("Category", "Unit")]
public sealed class DeveloperExperienceTests
{
    // ...
}
```

## Notes

- **BOM-Konservierungs-Workflow für die 4 BOM-tragenden Dateien**
  (`ConfigLoaderRulesJsonTests`, `ConfigNormalizerTests`,
  `ConfigSyncerTests`, `PathOverridesTests`):

  ```powershell
  # Vor Edit:
  $bytes = [System.IO.File]::ReadAllBytes('pfad/zur/datei.cs')
  $bom = ($bytes[0..2] -join ',') -eq '239,187,191'  # 0xEF,0xBB,0xBF
  Write-Output "BOM-vor-Edit: $bom"

  # Edit durchfuehren (edit-Tool mit old_string/new_string)

  # Nach Edit:
  $bytes2 = [System.IO.File]::ReadAllBytes('pfad/zur/datei.cs')
  $bom2 = ($bytes2[0..2] -join ',') -eq '239,187,191'
  Write-Output "BOM-nach-Edit: $bom2"
  if (-not $bom2) { Write-Error "BOM verloren! byte-genauen Python-Helper einsetzen." }
  ```

  Das `edit`-Tool erhält die BOM in der Regel (es ersetzt nur
  den exakten `old_string`-Match, nicht die Datei-Header-Bytes).
  Der Byte-Scan ist eine **Sicherheits-Verifikation** — wenn er
  bei allen 4 Dateien `BOM-vor-Edit: True` UND
  `BOM-nach-Edit: True` zeigt, ist alles in Ordnung und keine
  Sonderbehandlung nötig. Falls bei einer Datei `BOM-nach-Edit:
  False` auftaucht: das ist der Trigger für den byte-genauen
  Python-Helper analog step-007.
- **EOL-Konservierung:** alle 8 Dateien uniform CRLF + Trailing-NL
  (verifiziert), Standard-Edit-Tool reicht. Stichprobe (3 Dateien)
  in DoD vorgeschrieben, weil ein Vorab-Scan **aller** 8 Dateien
  Overhead wäre — die Uniformität erlaubt die Stichprobe.
- **Anti-Loop-Check nochmal explizit für die 4 aus dem
  Heuristik-Punkt-7-Befund resultierenden BOM-Dateien:** keine
  der 4 BOM-tragenden Dateien hat eine offene Hypothese in
  `codemap.md` (alle 4 sind in der `Configuration/`-Zeile als
  "8 Klassen, alle Unit" zusammengefasst — keine Per-Datei-
  Annotation); der Trait-Insert ändert an der BOM-Situation
  nichts (er fügt eine Zeile ein, kein Datei-Header-Replace).
- **Trait-Platzierungs-Bibliothek nach step-009:** alle 3 in
  step-007/008 etablierten Varianten sind in step-009 angewendet
  und ohne Sonderbehandlung bestätigt. Die Bibliothek ist
  damit für die EPIC-02-Folge-Batches (kleine Unit-Ordner
  `Maps/`, `Cli/`, ggf. Anfang von `Core/`) als abgeschlossen
  anzusehen. Für die größeren Folge-Batches (`Core/Checkers/`
  27 Klassen, `Core/` 19 Klassen, `Mcp/` 19 Klassen, `Commands/`
  17 Klassen) sind **keine** weiteren Platzierungs-Varianten
  zu erwarten — die einzige verbleibende Variante in xUnit-v3
  wäre `DisableParallelization` auf Klassen-Ebene (für Tests
  mit externem State), aber das wird per `[Collection]`-
  Mechanismus gelöst, nicht per Trait.
- **`Configuration/`-Schnitt-Begründung im Plan = `Configuration/`
  -Schnitt-Begründung in der Roadmap:** die Roadmap EPIC-02-Zeile
  wurde im Schritt 1 dieses Planer-Aufrufs aktualisiert
  (In-Arbeit-Annotation auf step-009 gerollt, Rest-Bestand
  rechnerisch runter auf 117 Klassen / 14+ Batches, Heuristik-
  Punkt 7 als neue Beobachtung dokumentiert). Der Roadmap-Diff
  ist Teil dieses Step-Commits (Orchestrator committet Roadmap-
  Diff + neuen Step-Plan zusammen, siehe `../../orchestrator.md`
  Schritt 3b).
- **Schritt-2-Verifikations-Anker für den Coder:** alle Zeilen-
  Angaben in diesem Plan (L1-L368-Bereiche je Datei) sind per
  `Read`-Tool bzw. `Get-Content -Encoding UTF8` direkt vor
  dem Edit verifizierbar; bei Zeilen-Drift (z. B. weil eine
  andere Person zwischen Planer-Aufruf und Coder-Aufruf an der
  Datei editiert hat) muss der Coder die exakten Zeilen frisch
  per `Read` ermitteln und das Edit entsprechend anpassen —
  **nicht** blind die Plan-Zahlen übernehmen.
- **Numerische Plausibilität step-009 vs. step-008:** Methoden-
  Pro-Datei-Zahlen für step-009 (16+4+3+9+11+5+10+3 = 61) sind
  im Vergleich zu step-008 (3+5+30+4 = 43) **größer** (insgesamt
  +18 Methoden), aber die Test-Case-Prognose (+68) ist
  deutlich kleiner als step-008 (+221), weil keine
  `[Theory]+[MemberData]`-Expansion auf `KnownRuleNames.Count=59`
  im Spiel ist (Configuration/ hat nur `[Theory]+[InlineData]`
  in 1 Klasse mit 9 InlineData-Zeilen, kein `[MemberData]`).
  Filter-Delta-Erwartung: **Unit 589 → 657 (+68)**,
  Integration unverändert, Total unverändert.
- **Vergleich zu step-007 (5 Klassen, +16 Unit-Delta) und
  step-008 (4 Klassen, +221 Unit-Delta):** step-009 ist mit
  +68 Unit-Delta aus 8 Klassen **zwischen** den beiden
  Schwestern angesiedelt — weniger als step-008 (Heavyweight
  `ViolationMarkdownFormatterTests` allein liefert +30,
  `RuleLegendRegistryTests` mit 59-MemberData-Expansion liefert
  +179), aber deutlich mehr als step-007 (5 kleine/mittlere
  Klassen). Filter-Lauf-Statistik sollte sich entsprechend
  konsistent verhalten.
- **Schritt-9 vs. Schritt-2 Naming-Konvention:** dieser Step
  heißt `step-009`, nicht `step-09` oder `step-9` — flache
  Task-weite Sequenz mit Null-Padding gemäß Template
  `../../templates/step-plan.md` (Schritt-Vorgabe: `NNN` =
  nächste freie dreistellige Nummer, fortlaufend über den
  ganzen Task). step-001..step-008 alle `step-NNN` mit
  Null-Padding.
- **Heuristik-Fortschreibung für Folge-Batches** (1-6 etabliert,
  7 in spe):
  - **Punkt 1:** Klassen-Homogenität → Klassen-Trait (in allen
    8 step-002..step-009-Batches bestätigt)
  - **Punkt 2:** bestehende Traits respektieren / additiv
    ergänzen (in step-002 Suppression mit 7+1, step-007
    `McpLintConsoleTests` mit 3 method-level bestätigt)
  - **Punkt 3:** `null!` als Edge-Input (in step-002 angewendet)
  - **Punkt 4:** Klassen-Trait additiv zu bestehenden method-
    level Traits bei homogenen Klassen (in step-002 und step-007
    bestätigt)
  - **Punkt 5:** Hypothesen-Auflösungs-Pflicht für offene
    "möglicherweise…"-Annotationen in der CodeMap (in
    step-006 `ListEvalsCommandTests` widerlegt)
  - **Punkt 6:** Helper-Klassen ohne Testmethoden sind keine
    Testklassen (in step-007 etabliert, in step-008 ohne
    Ausnahme bestätigt = vollständig abgehakt)
  - **Punkt 7 (in spe, neu in step-009):** BOM-Inhomogenität
    in `Configuration/` (4/8 mit BOM, 4/8 ohne) als neue
    Inhomogenitäts-Dimension. **Konservierung pro Datei
    mechanisch**, **Konsolidierung Nutzer-Sache** (analog
    TD-003/TD-004). In step-009 dokumentiert, in `roadmap.md`
    EPIC-02-Zeile vermerkt, **nicht** als TD-Eintrag angelegt
    (Beobachtung ohne Konsolidierungs-Auftrag), **nicht** als
    `auto_fixable: ja`-Bündel angehängt (kein step-009-Scope).
- **Was der Coder nach step-009 explizit im `step-result.md`
  dokumentieren muss** (gemäß etablierter Konvention aus
  step-002..step-008):
  - Vorher-Zeilenzahlen pro Datei (verifiziert per
    `Get-Content -Encoding UTF8 | Measure-Object -Line`); nachher
    = vorher + 1 für alle 8 Dateien (reiner 1-Zeilen-Insert pro
    Datei, keine Multi-Line-Edits)
  - **BOM-Scan-Ergebnis** für die 4 BOM-tragenden Dateien (Tabelle
    mit BOM-vor / BOM-nach, idealerweise beide `True` — falls
    ein `False` auftritt: byte-genauen Python-Helper-Pfad
    dokumentieren)
  - **EOL-Scan-Ergebnis** als Stichprobe für mindestens 3 Dateien
    (Tabelle mit CR/LF/TrNL vor und nach Edit, idealerweise alle
    Werte unverändert)
  - Build- und Test-Output (alle 4 Läufe wie im DoD)
  - Filter-Delta-Abgleich (Unit 589 → 657, Integration 113,
    Total 1325)
  - Per-Klasse-Filter-Aufschlüsselung als Plausibilitätsprobe
    (Summenprobe 16+4+3+9+11+12+10+3 = 68 ✓)
  - Methoden-Inventar pro Datei (regex-basiert, 61 Methoden)
  - Subject-Länge exakt eingehalten (71 Zeichen, 1 Zeichen Reserve)
  - Bekannte Unschärfen (Per-Klasse-Filter nicht direkt
    verifizierbar, Per-Datei-Summenprobe als Plausibilität;
    analog step-008-Konvention)
